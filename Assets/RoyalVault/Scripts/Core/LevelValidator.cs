using System.Collections.Generic;

namespace RoyalVault.Core
{
    public sealed class LevelValidationReport
    {
        public int LevelId;
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public SolveResult Solve;

        public bool IsValid { get { return Errors.Count == 0; } }

        public override string ToString()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("Level ").Append(LevelId).Append(": ");
            sb.Append(IsValid ? "OK" : "INVALID");
            if (Solve != null) sb.Append(" — ").Append(Solve);
            for (int i = 0; i < Errors.Count; i++) sb.Append("\n    ERROR: ").Append(Errors[i]);
            for (int i = 0; i < Warnings.Count; i++) sb.Append("\n    warn:  ").Append(Warnings[i]);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Structural and solvability checks for authored levels.
    ///
    /// The structural checks catch authoring mistakes instantly; the solver proves the level can
    /// actually be finished. A level that fails either must never reach a player.
    /// </summary>
    public static class LevelValidator
    {
        public static LevelValidationReport Validate(LevelDefinition level, int stateBudget = LevelSolver.DefaultStateBudget)
        {
            LevelValidationReport report = new LevelValidationReport();
            report.LevelId = level.Id;

            if (level.TrayCapacity <= 1)
                report.Errors.Add("TrayCapacity must be at least 2.");

            if (level.FilledTrays == null || level.FilledTrays.Length == 0)
            {
                report.Errors.Add("Level has no filled trays.");
                return report;
            }

            for (int i = 0; i < level.FilledTrays.Length; i++)
            {
                JewelryPiece[] tray = level.FilledTrays[i];
                if (tray == null)
                {
                    report.Errors.Add("Filled tray " + i + " is null.");
                    continue;
                }
                if (tray.Length > level.TrayCapacity)
                    report.Errors.Add("Filled tray " + i + " has " + tray.Length + " pieces but capacity is " + level.TrayCapacity + ".");

                for (int slot = 0; slot < tray.Length; slot++)
                {
                    if (tray[slot].Material == JewelMaterial.None || tray[slot].Form == JewelForm.None)
                        report.Errors.Add("Tray " + i + " slot " + slot + " has an incomplete piece.");
                }
            }

            if (level.EmptyTrayCount < 1)
                report.Errors.Add("A level needs at least one empty tray to be playable.");

            int pieces = level.TotalPieceCount;
            if (level.TrayCapacity > 1 && pieces % level.TrayCapacity != 0)
                report.Errors.Add("Piece count " + pieces + " is not a multiple of tray capacity " +
                                  level.TrayCapacity + ", so the pieces cannot fill whole trays.");

            if (level.TrayCapacity > 1)
            {
                int traysNeeded = pieces / level.TrayCapacity;
                if (traysNeeded > level.TotalTrayCount)
                    report.Errors.Add("Needs " + traysNeeded + " completed trays but only " +
                                      level.TotalTrayCount + " trays exist.");
            }

            // A completed tray requires TrayCapacity pieces sharing one attribute. If no attribute
            // value occurs often enough anywhere on the board, the level cannot possibly be solved
            // and there is no point paying for a search to find that out.
            ReportUncompletablePieces(level, report);

            if (report.Errors.Count > 0) return report;

            report.Solve = LevelSolver.Solve(level.BuildBoard(), stateBudget);

            if (report.Solve.ExhaustedSearchBudget)
                report.Errors.Add("Solvability could not be proven within the search budget.");
            else if (!report.Solve.Solved)
                report.Errors.Add("Level is UNSOLVABLE.");
            else
            {
                if (report.Solve.MoveCount < pieces / 2)
                    report.Warnings.Add("Solvable in only " + report.Solve.MoveCount +
                                        " moves — may be too trivial to be interesting.");
            }

            return report;
        }

        private static void ReportUncompletablePieces(LevelDefinition level, LevelValidationReport report)
        {
            Dictionary<JewelMaterial, int> byMaterial = new Dictionary<JewelMaterial, int>();
            Dictionary<JewelForm, int> byForm = new Dictionary<JewelForm, int>();

            for (int i = 0; i < level.FilledTrays.Length; i++)
            {
                JewelryPiece[] tray = level.FilledTrays[i];
                if (tray == null) continue;
                for (int slot = 0; slot < tray.Length; slot++)
                {
                    JewelryPiece piece = tray[slot];
                    int count;
                    byMaterial.TryGetValue(piece.Material, out count);
                    byMaterial[piece.Material] = count + 1;
                    byForm.TryGetValue(piece.Form, out count);
                    byForm[piece.Form] = count + 1;
                }
            }

            for (int i = 0; i < level.FilledTrays.Length; i++)
            {
                JewelryPiece[] tray = level.FilledTrays[i];
                if (tray == null) continue;
                for (int slot = 0; slot < tray.Length; slot++)
                {
                    JewelryPiece piece = tray[slot];
                    bool materialCanFill = byMaterial[piece.Material] >= level.TrayCapacity;
                    bool formCanFill = byForm[piece.Form] >= level.TrayCapacity;
                    if (!materialCanFill && !formCanFill)
                    {
                        report.Errors.Add("Piece '" + piece + "' can never belong to a completed tray: only " +
                                          byMaterial[piece.Material] + " of its material and " +
                                          byForm[piece.Form] + " of its form exist, but a tray needs " +
                                          level.TrayCapacity + ".");
                        return;
                    }
                }
            }
        }
    }
}
