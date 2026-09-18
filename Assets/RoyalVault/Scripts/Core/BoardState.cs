using System;
using System.Collections.Generic;
using System.Text;

namespace RoyalVault.Core
{
    /// <summary>A single lift-and-place: take the top piece of <see cref="From"/> and put it on <see cref="To"/>.</summary>
    public readonly struct Move : IEquatable<Move>
    {
        public readonly int From;
        public readonly int To;

        public Move(int from, int to)
        {
            From = from;
            To = to;
        }

        public bool Equals(Move other) { return From == other.From && To == other.To; }
        public override bool Equals(object obj) { return obj is Move && Equals((Move)obj); }
        public override int GetHashCode() { return (From << 8) ^ To; }
        public override string ToString() { return From + "->" + To; }
    }

    /// <summary>Why a move was rejected. Used to drive distinct player feedback.</summary>
    public enum MoveRejection
    {
        None = 0,
        SameTray,
        TrayIndexOutOfRange,
        SourceEmpty,
        SourceSealed,
        DestinationFull,
        DestinationSealed,
        NoSharedAttribute
    }

    /// <summary>
    /// The board: a fixed set of trays. Holds no presentation state and no engine types,
    /// so it can be cloned and searched by the solver at high speed.
    /// </summary>
    public sealed class BoardState
    {
        private readonly Tray[] _trays;

        public int TrayCount { get { return _trays.Length; } }
        public Tray this[int index] { get { return _trays[index]; } }

        public BoardState(params Tray[] trays)
        {
            if (trays == null || trays.Length == 0)
                throw new ArgumentException("A board needs at least one tray.");
            _trays = trays;
        }

        public BoardState Clone()
        {
            Tray[] copies = new Tray[_trays.Length];
            for (int i = 0; i < _trays.Length; i++) copies[i] = _trays[i].Clone();
            return new BoardState(copies);
        }

        /// <summary>
        /// The level is won when every tray is either empty or a sealed, completed set.
        /// Move legality already guarantees coherence, so fullness is the real test.
        /// </summary>
        public bool IsSolved
        {
            get
            {
                for (int i = 0; i < _trays.Length; i++)
                {
                    Tray tray = _trays[i];
                    if (tray.IsEmpty) continue;
                    if (!tray.IsSealed) return false;
                }
                return true;
            }
        }

        public MoveRejection Validate(Move move)
        {
            if (move.From < 0 || move.From >= _trays.Length ||
                move.To < 0 || move.To >= _trays.Length)
                return MoveRejection.TrayIndexOutOfRange;

            if (move.From == move.To) return MoveRejection.SameTray;

            Tray from = _trays[move.From];
            Tray to = _trays[move.To];

            if (from.IsEmpty) return MoveRejection.SourceEmpty;
            if (from.IsSealed) return MoveRejection.SourceSealed;
            if (to.IsSealed) return MoveRejection.DestinationSealed;
            if (to.IsFull) return MoveRejection.DestinationFull;

            if (!to.Identity.Accepts(from.Top)) return MoveRejection.NoSharedAttribute;

            return MoveRejection.None;
        }

        public bool IsLegal(Move move) { return Validate(move) == MoveRejection.None; }

        /// <summary>Applies a move that has already been validated.</summary>
        public void ApplyUnchecked(Move move)
        {
            _trays[move.To].Push(_trays[move.From].Pop());
        }

        /// <summary>Exact inverse of <see cref="ApplyUnchecked"/>. Always valid on the move just applied.</summary>
        public void RevertUnchecked(Move move)
        {
            _trays[move.From].Push(_trays[move.To].Pop());
        }

        /// <summary>
        /// All legal moves from this position. Destinations that are equivalent (several empty
        /// trays of the same capacity) are collapsed to one, which keeps the solver's branching
        /// factor down and stops hint systems suggesting arbitrary duplicates.
        /// </summary>
        public void GetLegalMoves(List<Move> results)
        {
            results.Clear();
            for (int from = 0; from < _trays.Length; from++)
            {
                Tray source = _trays[from];
                if (source.IsEmpty || source.IsSealed) continue;

                for (int to = 0; to < _trays.Length; to++)
                {
                    if (from == to) continue;
                    Tray destination = _trays[to];

                    if (destination.IsEmpty)
                    {
                        // Relocating a tray's only piece into an empty tray changes nothing.
                        if (source.Count == 1 && source.Capacity == destination.Capacity) continue;

                        // Empty trays of equal size are interchangeable; keep only the first.
                        if (IsDuplicateEmptyDestination(to, destination.Capacity)) continue;
                    }

                    if (IsLegal(new Move(from, to))) results.Add(new Move(from, to));
                }
            }
        }

        private bool IsDuplicateEmptyDestination(int index, int capacity)
        {
            for (int i = 0; i < index; i++)
            {
                if (_trays[i].IsEmpty && _trays[i].Capacity == capacity) return true;
            }
            return false;
        }

        public List<Move> GetLegalMoves()
        {
            List<Move> results = new List<Move>();
            GetLegalMoves(results);
            return results;
        }

        /// <summary>
        /// False means the player is stuck. Uses the same pruning as <see cref="GetLegalMoves"/>
        /// so that "no moves offered" and "dead end" can never disagree — a board whose only
        /// remaining moves are null moves really is a dead end.
        /// </summary>
        public bool HasLegalMove
        {
            get
            {
                GetLegalMoves(_scratchMoves);
                return _scratchMoves.Count > 0;
            }
        }

        private readonly List<Move> _scratchMoves = new List<Move>(32);

        /// <summary>
        /// Order-independent key for the solver's visited set. Tray contents are sorted so that
        /// boards differing only by which interchangeable tray holds what collapse to one state.
        /// </summary>
        public string GetCanonicalKey()
        {
            string[] parts = new string[_trays.Length];
            StringBuilder sb = new StringBuilder(16);
            for (int i = 0; i < _trays.Length; i++)
            {
                sb.Length = 0;
                _trays[i].AppendKey(sb);
                parts[i] = sb.ToString();
            }
            Array.Sort(parts, StringComparer.Ordinal);
            return string.Concat(parts);
        }

        /// <summary>Every piece on the board, used by level validation.</summary>
        public List<JewelryPiece> GetAllPieces()
        {
            List<JewelryPiece> pieces = new List<JewelryPiece>();
            for (int i = 0; i < _trays.Length; i++)
            {
                Tray tray = _trays[i];
                for (int slot = 0; slot < tray.Count; slot++) pieces.Add(tray[slot]);
            }
            return pieces;
        }
    }
}
