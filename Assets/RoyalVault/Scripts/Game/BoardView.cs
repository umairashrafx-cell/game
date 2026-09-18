using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RoyalVault.Core;

namespace RoyalVault.Game
{
    /// <summary>
    /// Presents a <see cref="PuzzleSession"/> and turns taps into moves.
    ///
    /// Every rule decision is delegated to the core simulation — this class never decides whether
    /// a move is legal, only how it looks. That separation is what lets the rules be proven
    /// correct in tests that never open a window.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public event Action<MoveResult> MovePlayed;
        public event Action LevelSolved;

        private PuzzleSession _session;
        private readonly List<TrayView> _trays = new List<TrayView>();
        private RectTransform _rect;
        private int _selectedTray = -1;
        private bool _animating;
        private float _slotSize;

        public bool IsAnimating { get { return _animating; } }
        public PuzzleSession Session { get { return _session; } }

        public void Bind(PuzzleSession session, RectTransform area)
        {
            _session = session;
            _rect = area;
            Rebuild();
        }

        public void Clear()
        {
            for (int i = 0; i < _trays.Count; i++)
            {
                if (_trays[i] != null) Destroy(_trays[i].gameObject);
            }
            _trays.Clear();
            _selectedTray = -1;
            _animating = false;
        }

        private void Rebuild()
        {
            Clear();

            BoardState board = _session.Board;
            int trayCount = board.TrayCount;
            int capacity = board[0].Capacity;

            Layout layout = ComputeLayout(trayCount, capacity, _rect.rect.size);
            _slotSize = layout.SlotSize;

            for (int i = 0; i < trayCount; i++)
            {
                TrayView tray = TrayView.Create(_rect, i, capacity, layout.SlotSize, OnTrayClicked);
                tray.Rect.anchoredPosition = layout.PositionOf(i);
                _trays.Add(tray);

                Tray model = board[i];
                for (int slot = 0; slot < model.Count; slot++)
                {
                    PieceView piece = PieceView.Create(tray.PieceLayer, model[slot], layout.SlotSize * 0.86f);
                    tray.AddPiece(piece, slot);
                }

                tray.SetSealed(model.IsSealed);
            }
        }

        private struct Layout
        {
            public float SlotSize;
            public int Columns;
            public float CellWidth;
            public float CellHeight;
            public int TrayCount;

            public Vector2 PositionOf(int index)
            {
                int rows = Mathf.CeilToInt(TrayCount / (float)Columns);
                int row = index / Columns;
                int column = index % Columns;

                // Centre the final row when it is short, so the board never looks lopsided.
                int itemsInRow = Mathf.Min(Columns, TrayCount - row * Columns);
                float rowWidth = itemsInRow * CellWidth;

                float x = -rowWidth * 0.5f + CellWidth * (column + 0.5f);
                float y = (rows - 1) * CellHeight * 0.5f - row * CellHeight;
                return new Vector2(x, y);
            }
        }

        /// <summary>
        /// Chooses a tray grid that fills the available area. Everything is derived from the
        /// measured rect rather than fixed pixel positions, so the board adapts to tall phones,
        /// short phones and notches without special cases.
        /// </summary>
        private static Layout ComputeLayout(int trayCount, int capacity, Vector2 area)
        {
            Layout best = new Layout();
            float bestSlot = 0f;

            for (int columns = 1; columns <= trayCount; columns++)
            {
                int rows = Mathf.CeilToInt(trayCount / (float)columns);

                float cellWidth = area.x / columns;
                float cellHeight = area.y / rows;

                // A tray is 1.28 slots wide and (capacity + 0.34) slots tall, plus breathing room.
                float slotFromWidth = cellWidth / 1.28f * 0.88f;
                float slotFromHeight = cellHeight / (capacity + 0.34f) * 0.92f;
                float slot = Mathf.Min(slotFromWidth, slotFromHeight);

                if (slot > bestSlot)
                {
                    bestSlot = slot;
                    best = new Layout
                    {
                        SlotSize = slot,
                        Columns = columns,
                        CellWidth = cellWidth,
                        CellHeight = cellHeight,
                        TrayCount = trayCount
                    };
                }
            }

            return best;
        }

        private void OnTrayClicked(int index)
        {
            if (_animating || _session == null) return;
            if (_session.IsSolved) return;

            if (_selectedTray < 0)
            {
                TryLift(index);
                return;
            }

            if (_selectedTray == index)
            {
                Deselect();
                return;
            }

            StartCoroutine(AttemptMove(_selectedTray, index));
        }

        private void TryLift(int index)
        {
            Tray model = _session.Board[index];
            if (model.IsEmpty || model.IsSealed)
            {
                HapticService.Play(HapticStrength.Warning);
                StartCoroutine(_trays[index].Shake());
                return;
            }

            _selectedTray = index;
            _trays[index].SetHighlighted(true);

            PieceView top = _trays[index].Top;
            if (top != null)
            {
                top.SetLifted(true, _slotSize);
                StartCoroutine(top.AnimateArcTo(_trays[index].LiftedPosition(), 0.12f, 0f));
            }

            HapticService.Play(HapticStrength.Selection);
        }

        private void Deselect()
        {
            if (_selectedTray < 0) return;

            TrayView tray = _trays[_selectedTray];
            tray.SetHighlighted(false);
            tray.SetSealed(_session.Board[_selectedTray].IsSealed);

            PieceView top = tray.Top;
            if (top != null)
            {
                top.SetLifted(false, _slotSize);
                int slot = _session.Board[_selectedTray].Count - 1;
                StartCoroutine(top.AnimateArcTo(tray.SlotPosition(slot), 0.10f, 0f));
            }

            _selectedTray = -1;
        }

        private IEnumerator AttemptMove(int from, int to)
        {
            Move move = new Move(from, to);
            MoveRejection rejection = _session.Validate(move);

            if (rejection != MoveRejection.None)
            {
                HapticService.Play(HapticStrength.Warning);
                yield return _trays[to].Shake();
                Deselect();
                yield break;
            }

            _animating = true;

            TrayView source = _trays[from];
            TrayView destination = _trays[to];

            PieceView piece = source.RemoveTop();
            int destinationSlot = _session.Board[to].Count;

            // Apply the move to the simulation first, then play the animation that describes it.
            MoveResult result = _session.TryMove(move);

            piece.SetLifted(false, _slotSize);
            piece.Rect.SetParent(destination.PieceLayer, true);

            Vector2 target = destination.SlotPosition(destinationSlot);
            yield return piece.AnimateArcTo(target, 0.20f, _slotSize * 0.55f);

            destination.AddPiece(piece, destinationSlot);

            source.SetHighlighted(false);
            source.SetSealed(_session.Board[from].IsSealed);

            HapticService.Play(result.SealedDestination ? HapticStrength.Success : HapticStrength.Light);

            if (result.SealedDestination)
            {
                yield return destination.PlaySealSequence();
            }
            else
            {
                destination.SetSealed(false);
            }

            _selectedTray = -1;
            _animating = false;

            if (MovePlayed != null) MovePlayed(result);

            if (result.Solved)
            {
                HapticService.Play(HapticStrength.Celebration);
                if (LevelSolved != null) LevelSolved();
            }
        }

        /// <summary>Undo is a full visual resync rather than a reverse animation — simpler and never drifts.</summary>
        public void ResyncFromModel()
        {
            if (_animating) return;
            _selectedTray = -1;
            Rebuild();
        }
    }
}
