using System.Collections.Generic;

namespace RoyalVault.Core
{
    public sealed class SolveResult
    {
        public bool Solved;
        public bool ExhaustedSearchBudget;
        public int StatesExplored;
        public List<Move> Solution = new List<Move>();

        /// <summary>
        /// Length of the solution found. Optimal only when produced by
        /// <see cref="LevelSolver.SolveShortest"/>; from the depth-first solver this is an
        /// upper bound, so it must not be used directly as a star threshold.
        /// </summary>
        public int MoveCount { get { return Solution.Count; } }

        public override string ToString()
        {
            if (Solved) return "solved in " + MoveCount + " moves (" + StatesExplored + " states)";
            if (ExhaustedSearchBudget) return "UNKNOWN — search budget exhausted after " + StatesExplored + " states";
            return "UNSOLVABLE (" + StatesExplored + " states exhausted)";
        }
    }

    /// <summary>
    /// Solvers over board states, used to prove that every shipped level is completable.
    ///
    /// <see cref="Solve"/> is depth-first: it answers "can this be finished at all?" using memory
    /// proportional to the depth rather than the frontier, which is what makes a definitive
    /// answer affordable on larger boards. Breadth-first search was tried first and had to be
    /// abandoned — its frontier exploded on 16-piece boards, so adding a spare tray made levels
    /// report as UNSOLVABLE when the search had merely run out of budget. A validator that says
    /// "impossible" when it means "I gave up" is worse than no validator at all.
    ///
    /// <see cref="SolveShortest"/> keeps the breadth-first search for par move counts on small
    /// boards, where the optimal answer is both affordable and genuinely useful.
    /// </summary>
    public static class LevelSolver
    {
        public const int DefaultStateBudget = 400000;

        private sealed class Node
        {
            public BoardState State;
            public Node Parent;
            public Move Move;
        }

        private sealed class Frame
        {
            public BoardState State;
            public List<Move> Moves;
            public int Index;
            public Move ArrivedBy;
        }

        /// <summary>
        /// Depth-first proof of solvability. Returns a valid solution when one exists, or an
        /// explicit "budget exhausted" result — never a false "unsolvable".
        /// </summary>
        public static SolveResult Solve(BoardState start, int stateBudget = DefaultStateBudget)
        {
            SolveResult result = new SolveResult();

            if (start.IsSolved)
            {
                result.Solved = true;
                return result;
            }

            HashSet<string> visited = new HashSet<string>();
            List<Frame> stack = new List<Frame>(64);

            BoardState root = start.Clone();
            visited.Add(root.GetCanonicalKey());
            stack.Add(new Frame { State = root, Moves = root.GetLegalMoves(), Index = 0 });

            while (stack.Count > 0)
            {
                Frame frame = stack[stack.Count - 1];

                if (frame.Index >= frame.Moves.Count)
                {
                    stack.RemoveAt(stack.Count - 1);
                    continue;
                }

                if (result.StatesExplored >= stateBudget)
                {
                    result.ExhaustedSearchBudget = true;
                    return result;
                }

                Move move = frame.Moves[frame.Index++];

                BoardState next = frame.State.Clone();
                next.ApplyUnchecked(move);

                if (!visited.Add(next.GetCanonicalKey())) continue;
                result.StatesExplored++;

                Frame child = new Frame
                {
                    State = next,
                    Moves = next.GetLegalMoves(),
                    Index = 0,
                    ArrivedBy = move
                };
                stack.Add(child);

                if (next.IsSolved)
                {
                    result.Solved = true;
                    for (int i = 1; i < stack.Count; i++) result.Solution.Add(stack[i].ArrivedBy);
                    return result;
                }
            }

            return result;
        }

        /// <summary>
        /// Breadth-first search returning the shortest solution. Use on small boards only —
        /// check <see cref="SolveResult.ExhaustedSearchBudget"/> before trusting a negative.
        /// </summary>
        public static SolveResult SolveShortest(BoardState start, int stateBudget = DefaultStateBudget)
        {
            SolveResult result = new SolveResult();

            if (start.IsSolved)
            {
                result.Solved = true;
                return result;
            }

            HashSet<string> visited = new HashSet<string>();
            Queue<Node> frontier = new Queue<Node>();
            List<Move> moveBuffer = new List<Move>(32);

            Node root = new Node { State = start.Clone(), Parent = null };
            frontier.Enqueue(root);
            visited.Add(root.State.GetCanonicalKey());

            while (frontier.Count > 0)
            {
                if (result.StatesExplored >= stateBudget)
                {
                    result.ExhaustedSearchBudget = true;
                    return result;
                }

                Node current = frontier.Dequeue();
                result.StatesExplored++;

                current.State.GetLegalMoves(moveBuffer);
                for (int i = 0; i < moveBuffer.Count; i++)
                {
                    Move move = moveBuffer[i];

                    BoardState next = current.State.Clone();
                    next.ApplyUnchecked(move);

                    string key = next.GetCanonicalKey();
                    if (!visited.Add(key)) continue;

                    Node child = new Node { State = next, Parent = current, Move = move };

                    if (next.IsSolved)
                    {
                        result.Solved = true;
                        Reconstruct(child, result.Solution);
                        return result;
                    }

                    frontier.Enqueue(child);
                }
            }

            return result;
        }

        private static void Reconstruct(Node goal, List<Move> into)
        {
            into.Clear();
            for (Node node = goal; node != null && node.Parent != null; node = node.Parent)
            {
                into.Add(node.Move);
            }
            into.Reverse();
        }
    }
}
