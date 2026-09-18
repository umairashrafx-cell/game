using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using RoyalVault.Core;

namespace RoyalVault.Tests
{
    /// <summary>
    /// A design instrument, not a regression test. Sweeps starting layouts and spare-tray counts
    /// and reports which combinations the solver can actually finish, so levels are tuned against
    /// measurements instead of intuition. Marked Explicit so it never slows the normal suite.
    /// </summary>
    [TestFixture]
    public class LevelDesignSweep
    {
        private static readonly JewelForm[] Forms =
        {
            JewelForm.Ring, JewelForm.Necklace, JewelForm.Earrings,
            JewelForm.Bracelet, JewelForm.Pendant, JewelForm.Crown
        };

        /// <summary>
        /// Builds the piece multiset, assigning forms by a running counter so they are spread as
        /// evenly as possible across the whole board. Even spreading is what keeps each form's
        /// total below tray capacity, which is the lever that stops shape-sets from being
        /// completable in the teaching levels.
        /// </summary>
        private static List<JewelryPiece> BuildPieces(JewelMaterial[] materials, int capacity)
        {
            List<JewelryPiece> pieces = new List<JewelryPiece>();
            int next = 0;
            for (int m = 0; m < materials.Length; m++)
            {
                for (int i = 0; i < capacity; i++)
                {
                    pieces.Add(new JewelryPiece(materials[m], Forms[next % Forms.Length]));
                    next++;
                }
            }
            return pieces;
        }

        private static int MaxFormCount(List<JewelryPiece> pieces)
        {
            Dictionary<JewelForm, int> counts = new Dictionary<JewelForm, int>();
            foreach (JewelryPiece piece in pieces)
            {
                int c;
                counts.TryGetValue(piece.Form, out c);
                counts[piece.Form] = c + 1;
            }
            int max = 0;
            foreach (int c in counts.Values) if (c > max) max = c;
            return max;
        }

        private static LevelDefinition Deal(List<JewelryPiece> pieces, int capacity, int emptyTrays, int seed)
        {
            List<JewelryPiece> shuffled = new List<JewelryPiece>(pieces);
            Random rng = new Random(seed);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                JewelryPiece tmp = shuffled[i]; shuffled[i] = shuffled[j]; shuffled[j] = tmp;
            }

            int trayCount = shuffled.Count / capacity;
            JewelryPiece[][] trays = new JewelryPiece[trayCount][];
            for (int t = 0; t < trayCount; t++)
            {
                trays[t] = shuffled.GetRange(t * capacity, capacity).ToArray();
            }

            return new LevelDefinition
            {
                Id = seed,
                TrayCapacity = capacity,
                EmptyTrayCount = emptyTrays,
                FilledTrays = trays
            };
        }

        [Test, Explicit]
        public void SweepSpareTrayRequirements()
        {
            JewelMaterial[][] materialSets =
            {
                new[] { JewelMaterial.Gold, JewelMaterial.Diamond },
                new[] { JewelMaterial.Gold, JewelMaterial.Diamond, JewelMaterial.Ruby },
                new[] { JewelMaterial.Gold, JewelMaterial.Diamond, JewelMaterial.Ruby, JewelMaterial.Emerald }
            };

            StringBuilder report = new StringBuilder("\n=== SPARE TRAY SWEEP ===\n");

            foreach (int capacity in new[] { 3, 4 })
            {
                foreach (JewelMaterial[] materials in materialSets)
                {
                    List<JewelryPiece> pieces = BuildPieces(materials, capacity);

                    for (int empties = 1; empties <= 4; empties++)
                    {
                        int solvable = 0;
                        int unknown = 0;
                        const int seeds = 8;

                        for (int seed = 1; seed <= seeds; seed++)
                        {
                            LevelDefinition level = Deal(pieces, capacity, empties, seed);
                            SolveResult solve = LevelSolver.Solve(level.BuildBoard(), 200000);
                            if (solve.Solved) solvable++;
                            else if (solve.ExhaustedSearchBudget) unknown++;
                        }

                        report.Append("cap=").Append(capacity)
                              .Append(" materials=").Append(materials.Length)
                              .Append(" maxForm=").Append(MaxFormCount(pieces))
                              .Append(" empties=").Append(empties)
                              .Append(" -> solvable ").Append(solvable).Append('/').Append(seeds)
                              .Append("  unsolvable ").Append(seeds - solvable - unknown)
                              .Append("  unknown ").Append(unknown)
                              .Append('\n');
                    }
                }
            }

            UnityEngine.Debug.Log(report.ToString());
        }
    }
}
