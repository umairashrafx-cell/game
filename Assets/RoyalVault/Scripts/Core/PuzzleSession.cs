using System;
using System.Collections.Generic;

namespace RoyalVault.Core
{
    /// <summary>Everything the presentation layer needs to know about one attempted move.</summary>
    public readonly struct MoveResult
    {
        public readonly bool Success;
        public readonly MoveRejection Rejection;
        public readonly Move Move;
        public readonly JewelryPiece Piece;

        /// <summary>The destination tray sealed as a completed set — fire Royal Match.</summary>
        public readonly bool SealedDestination;

        /// <summary>Chain length after this move. 0 when no chain is running.</summary>
        public readonly int RoyalChain;

        /// <summary>The destination committed to a single attribute on this move.</summary>
        public readonly bool LockedIdentity;

        public readonly bool Solved;
        public readonly bool DeadEnd;

        public MoveResult(bool success, MoveRejection rejection, Move move, JewelryPiece piece,
                          bool sealedDestination, int royalChain, bool lockedIdentity,
                          bool solved, bool deadEnd)
        {
            Success = success;
            Rejection = rejection;
            Move = move;
            Piece = piece;
            SealedDestination = sealedDestination;
            RoyalChain = royalChain;
            LockedIdentity = lockedIdentity;
            Solved = solved;
            DeadEnd = deadEnd;
        }

        public static MoveResult Rejected(Move move, MoveRejection reason)
        {
            return new MoveResult(false, reason, move, JewelryPiece.Empty, false, 0, false, false, false);
        }
    }

    /// <summary>
    /// One attempt at one level: the board, the move history, and the Royal Chain.
    ///
    /// This is the unit the tests exercise and the unit the view observes. It owns no engine
    /// types, so the entire game's rules can be verified from the command line.
    /// </summary>
    public sealed class PuzzleSession
    {
        private readonly BoardState _board;
        private readonly List<Move> _history = new List<Move>(128);

        public BoardState Board { get { return _board; } }
        public int MoveCount { get { return _history.Count; } }
        public bool CanUndo { get { return _history.Count > 0; } }
        public int RoyalChain { get; private set; }
        public int BestRoyalChain { get; private set; }
        public int SealedTrayCount { get; private set; }
        public bool IsSolved { get { return _board.IsSolved; } }

        /// <summary>True when the player has no legal move left and has not solved the level.</summary>
        public bool IsDeadEnd { get { return !_board.IsSolved && !_board.HasLegalMove; } }

        public event Action<MoveResult> MoveApplied;
        public event Action<int> TraySealed;
        public event Action<Move> MoveUndone;
        public event Action Solved;

        public PuzzleSession(BoardState board)
        {
            if (board == null) throw new ArgumentNullException("board");
            _board = board;
            RecountSealedTrays();
        }

        public MoveRejection Validate(Move move) { return _board.Validate(move); }

        public MoveResult TryMove(Move move)
        {
            MoveRejection rejection = _board.Validate(move);
            if (rejection != MoveRejection.None) return MoveResult.Rejected(move, rejection);

            JewelryPiece piece = _board[move.From].Top;
            bool wasLocked = _board[move.To].Identity.IsLocked;

            _board.ApplyUnchecked(move);
            _history.Add(move);

            bool sealedNow = _board[move.To].IsSealed;
            if (sealedNow)
            {
                SealedTrayCount++;
                RoyalChain++;
                if (RoyalChain > BestRoyalChain) BestRoyalChain = RoyalChain;
            }

            bool lockedNow = !wasLocked && _board[move.To].Identity.IsLocked;
            bool solved = _board.IsSolved;
            bool deadEnd = !solved && !_board.HasLegalMove;

            MoveResult result = new MoveResult(true, MoveRejection.None, move, piece,
                                               sealedNow, RoyalChain, lockedNow, solved, deadEnd);

            if (MoveApplied != null) MoveApplied(result);
            if (sealedNow && TraySealed != null) TraySealed(move.To);
            if (solved && Solved != null) Solved();

            return result;
        }

        /// <summary>
        /// Reverses the last move exactly. Undo deliberately breaks the Royal Chain — the chain
        /// rewards committed decisions, so it must not survive taking one back.
        /// </summary>
        public bool Undo()
        {
            if (_history.Count == 0) return false;

            Move move = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);

            bool wasSealed = _board[move.To].IsSealed;
            _board.RevertUnchecked(move);
            if (wasSealed) SealedTrayCount--;

            RoyalChain = 0;

            if (MoveUndone != null) MoveUndone(move);
            return true;
        }

        private void RecountSealedTrays()
        {
            SealedTrayCount = 0;
            for (int i = 0; i < _board.TrayCount; i++)
            {
                if (_board[i].IsSealed) SealedTrayCount++;
            }
        }

        public IReadOnlyList<Move> History { get { return _history; } }
    }
}
