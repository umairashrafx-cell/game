using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using RoyalVault.Core;

namespace RoyalVault.Tests
{
    /// <summary>
    /// Level authoring assistant. Given the shape a level should have — which gems, how many
    /// spare trays, whether shape-sets should be completable — it searches seeded deals, keeps
    /// only those the solver can finish, prefers the one needing the most moves, and prints it
    /// as pasteable C#.
    ///
    /// This exists because hand-authoring against the Dual Identity rule is genuinely hard: an
    /// evenly-jumbled tray looks like a good puzzle and is often provably impossible. Designing
    /// against the solver instead of against intuition is the only way to keep levels both
    /// intentional and fair.
    /// </summary>
    [TestFixture]
    public class LevelAuthoringTool
    {
        private sealed class Spec
        {
            public string Name;
            public JewelMaterial[] Materials;
            public int Capacity = 4;
            public int Empties = 2;
            /// <summary>Forms are spread evenly unless a form is named here to be repeated to capacity.</summary>
            public JewelForm ForceCompletableForm = JewelForm.None;
        }

        private static readonly JewelForm[] Forms =
        {
            JewelForm.Ring, JewelForm.Necklace, JewelForm.Earrings,
            JewelForm.Bracelet, JewelForm.Pendant, JewelForm.Crown
        };

        private static List<JewelryPiece> BuildPieces(Spec spec)
        {
            List<JewelryPiece> pieces = new List<JewelryPiece>();
            int next = 0;

            foreach (JewelMaterial material in spec.Materials)
            {
                for (int i = 0; i < spec.Capacity; i++)
                {
                    // One piece per material takes the featured form, so it reaches exactly
                    // capacity across the board and a shape-set becomes completable.
                    if (i == 0 && spec.ForceCompletableForm != JewelForm.None)
                    {
                        pieces.Add(new JewelryPiece(material, spec.ForceCompletableForm));
                        continue;
                    }

                    JewelForm form;
                    do
                    {
                        form = Forms[next % Forms.Length];
                        next++;
                    } while (form == spec.ForceCompletableForm);

                    pieces.Add(new JewelryPiece(material, form));
                }
            }
            return pieces;
        }

        private static LevelDefinition Deal(List<JewelryPiece> pieces, Spec spec, int seed)
        {
            List<JewelryPiece> shuffled = new List<JewelryPiece>(pieces);
            Random rng = new Random(seed);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                JewelryPiece tmp = shuffled[i]; shuffled[i] = shuffled[j]; shuffled[j] = tmp;
            }

            int trayCount = shuffled.Count / spec.Capacity;
            JewelryPiece[][] trays = new JewelryPiece[trayCount][];
            for (int t = 0; t < trayCount; t++)
                trays[t] = shuffled.GetRange(t * spec.Capacity, spec.Capacity).ToArray();

            return new LevelDefinition
            {
                Id = seed,
                TrayCapacity = spec.Capacity,
                EmptyTrayCount = spec.Empties,
                FilledTrays = trays
            };
        }

        [Test, Explicit]
        public void FindVerifiedLayouts()
        {
            Spec[] specs =
            {
                new Spec { Name = "Level02", Materials = new[] { JewelMaterial.Gold, JewelMaterial.Diamond, JewelMaterial.Ruby }, Empties = 2 },
                new Spec { Name = "Level03", Materials = new[] { JewelMaterial.Gold, JewelMaterial.Diamond, JewelMaterial.Ruby }, Empties = 2 },
                new Spec { Name = "Level04", Materials = new[] { JewelMaterial.Gold, JewelMaterial.Diamond, JewelMaterial.Ruby, JewelMaterial.Emerald }, Empties = 3 },
                new Spec { Name = "Level05", Materials = new[] { JewelMaterial.Gold, JewelMaterial.Diamond, JewelMaterial.Ruby, JewelMaterial.Emerald }, Empties = 2, ForceCompletableForm = JewelForm.Ring }
            };

            StringBuilder report = new StringBuilder("\n=== VERIFIED LEVEL LAYOUTS ===\n");

            // Level03 must be tougher than Level02 from the same gems, so take the second-best
            // candidate for 02 and the hardest for 03.
            foreach (Spec spec in specs)
            {
                List<JewelryPiece> pieces = BuildPieces(spec);
                List<KeyValuePair<int, LevelDefinition>> solvable = new List<KeyValuePair<int, LevelDefinition>>();

                for (int seed = 1; seed <= 40; seed++)
                {
                    LevelDefinition candidate = Deal(pieces, spec, seed);
                    LevelValidationReport validation = LevelValidator.Validate(candidate, 200000);
                    if (!validation.IsValid) continue;

                    // Rank by how much of the search space the depth-first proof had to walk.
                    // Optimal par via breadth-first search is far too slow to run on every
                    // candidate at 16 pieces, and states-explored is a good enough proxy for
                    // "this layout resists a naive approach".
                    solvable.Add(new KeyValuePair<int, LevelDefinition>(validation.Solve.StatesExplored, candidate));
                }

                solvable.Sort((a, b) => b.Key.CompareTo(a.Key));

                report.Append("\n--- ").Append(spec.Name)
                      .Append("  (").Append(solvable.Count).Append(" solvable of 40 seeds)\n");

                if (solvable.Count == 0)
                {
                    report.Append("    NO SOLVABLE LAYOUT FOUND\n");
                    continue;
                }

                int pick = spec.Name == "Level02" ? Math.Min(solvable.Count - 1, solvable.Count / 2) : 0;
                KeyValuePair<int, LevelDefinition> chosen = solvable[pick];

                report.Append("    search resistance (states explored) = ").Append(chosen.Key).Append('\n');
                report.Append(ToCode(chosen.Value));
            }

            UnityEngine.Debug.Log(report.ToString());
        }

        private static string ToCode(LevelDefinition level)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("                EmptyTrayCount = ").Append(level.EmptyTrayCount).Append(",\n");
            sb.Append("                FilledTrays = new[]\n                {\n");
            for (int t = 0; t < level.FilledTrays.Length; t++)
            {
                sb.Append("                    new[] { ");
                for (int s = 0; s < level.FilledTrays[t].Length; s++)
                {
                    if (s > 0) sb.Append(", ");
                    JewelryPiece piece = level.FilledTrays[t][s];
                    sb.Append("J.").Append(piece.Material).Append("(J.").Append(piece.Form).Append(')');
                }
                sb.Append(" }").Append(t < level.FilledTrays.Length - 1 ? "," : "").Append('\n');
            }
            sb.Append("                }\n");
            return sb.ToString();
        }
    }
}
