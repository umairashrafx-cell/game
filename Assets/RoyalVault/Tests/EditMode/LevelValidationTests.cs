using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using RoyalVault.Core;

namespace RoyalVault.Tests
{
    /// <summary>
    /// The guarantee that no impossible level ever reaches a player. Every shipped level is
    /// solved by brute force here; if a level cannot be proven solvable, the build fails.
    /// </summary>
    [TestFixture]
    public class LevelValidationTests
    {
        [Test]
        public void EveryShippedLevelIsStructurallyValidAndProvablySolvable()
        {
            List<LevelDefinition> levels = LevelLibrary.BuildPhaseOneLevels();
            StringBuilder log = new StringBuilder();
            bool allValid = true;

            foreach (LevelDefinition level in levels)
            {
                LevelValidationReport report = LevelValidator.Validate(level);
                log.Append(level.DisplayName).Append(" — ").Append(report).Append('\n');
                if (!report.IsValid) allValid = false;
            }

            UnityEngine.Debug.Log("Level validation:\n" + log);
            Assert.IsTrue(allValid, "One or more levels failed validation:\n" + log);
        }

        [Test]
        public void ReplayingASolverSolutionActuallyFinishesTheLevel()
        {
            // Guards against the solver and the live rule engine ever drifting apart.
            foreach (LevelDefinition level in LevelLibrary.BuildPhaseOneLevels())
            {
                SolveResult solve = LevelSolver.Solve(level.BuildBoard());
                Assert.IsTrue(solve.Solved, level.DisplayName + " could not be solved.");

                PuzzleSession session = new PuzzleSession(level.BuildBoard());
                foreach (Move move in solve.Solution)
                {
                    MoveResult result = session.TryMove(move);
                    Assert.IsTrue(result.Success,
                        level.DisplayName + ": solver produced an illegal move " + move +
                        " (" + result.Rejection + ")");
                }

                Assert.IsTrue(session.IsSolved, level.DisplayName + " did not finish after replaying the solution.");
            }
        }

        [Test]
        public void DifficultyRisesAcrossThePhaseOneLevels()
        {
            // Measured as how much of the search space a depth-first proof has to walk. Solution
            // LENGTH is deliberately not used: the depth-first solver returns the first solution
            // it finds, not the shortest, so its length says little about difficulty.
            int previous = -1;
            string previousName = null;

            foreach (LevelDefinition level in LevelLibrary.BuildPhaseOneLevels())
            {
                SolveResult solve = LevelSolver.Solve(level.BuildBoard());
                Assert.IsTrue(solve.Solved, level.DisplayName + " is not solvable.");

                if (previous >= 0)
                {
                    Assert.Greater(solve.StatesExplored, previous,
                        level.DisplayName + " does not resist the solver more than " + previousName +
                        " — the opening levels must get harder, not flatter.");
                }

                previous = solve.StatesExplored;
                previousName = level.DisplayName;
            }
        }

        [Test]
        public void TheFirstLevelCannotBePlayedIntoADeadEnd()
        {
            // The strongest guarantee a tutorial level can offer: not "unlikely to fail" but
            // "cannot fail". Explores every position reachable from the opening and asserts that
            // none of them is a dead end.
            //
            // This holds because every Form in level 1 occurs exactly once, so two pieces can
            // only ever share a gem. A wrong pairing is refused rather than allowed-and-punished.
            BoardState start = LevelLibrary.Level01().BuildBoard();

            HashSet<string> visited = new HashSet<string>();
            Stack<BoardState> pending = new Stack<BoardState>();

            pending.Push(start);
            visited.Add(start.GetCanonicalKey());

            int explored = 0;
            while (pending.Count > 0)
            {
                BoardState state = pending.Pop();
                explored++;

                if (!state.IsSolved && !state.HasLegalMove)
                {
                    Assert.Fail("Level 1 can be played into a dead end after " + explored +
                                " positions. A first level must never strand the player.");
                }

                foreach (Move move in state.GetLegalMoves())
                {
                    BoardState next = state.Clone();
                    next.ApplyUnchecked(move);
                    if (visited.Add(next.GetCanonicalKey())) pending.Push(next);
                }
            }

            Assert.Greater(explored, 1, "The search should have explored real positions.");
            UnityEngine.Debug.Log("Level 1 proven dead-end free across " + explored + " reachable positions.");
        }

        [Test]
        public void ValidatorRejectsALevelWhosePiecesCanNeverCompleteATray()
        {
            LevelDefinition broken = new LevelDefinition
            {
                Id = 999,
                TrayCapacity = 4,
                EmptyTrayCount = 1,
                FilledTrays = new[]
                {
                    // Four pieces sharing no attribute in sufficient numbers to fill a tray.
                    new[] { J.Gold(J.Ring), J.Diamond(J.Necklace), J.Ruby(J.Earrings), J.Emerald(J.Bracelet) }
                }
            };

            LevelValidationReport report = LevelValidator.Validate(broken);
            Assert.IsFalse(report.IsValid, "A level nobody can finish must be rejected.");
        }

        [Test]
        public void ValidatorRejectsAPieceCountThatCannotFillWholeTrays()
        {
            LevelDefinition broken = new LevelDefinition
            {
                Id = 998,
                TrayCapacity = 4,
                EmptyTrayCount = 1,
                FilledTrays = new[]
                {
                    new[] { J.Gold(J.Ring), J.Gold(J.Necklace), J.Gold(J.Earrings) }
                }
            };

            LevelValidationReport report = LevelValidator.Validate(broken);
            Assert.IsFalse(report.IsValid);
        }

        [Test]
        public void ValidatorRejectsALevelWithNoEmptyTray()
        {
            LevelDefinition broken = LevelLibrary.Level01();
            broken.EmptyTrayCount = 0;

            LevelValidationReport report = LevelValidator.Validate(broken);
            Assert.IsFalse(report.IsValid);
        }
    }
}
