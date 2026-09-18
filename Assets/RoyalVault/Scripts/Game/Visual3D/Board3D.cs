using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using RoyalVault.Core;

namespace RoyalVault.Game.Visual3D
{
    /// <summary>
    /// The 3D board. Presents a <see cref="PuzzleSession"/> and turns taps into moves.
    ///
    /// As with the flat version, this class never decides whether a move is legal — it asks the
    /// simulation and then animates the answer. That is what allowed the entire rendering layer
    /// to be replaced without touching a single rule or breaking one of the 32 tests.
    /// </summary>
    public sealed class Board3D : MonoBehaviour
    {
        public event Action<MoveResult> MovePlayed;
        public event Action LevelSolved;

        private PuzzleSession _session;
        private Camera _camera;
        private readonly List<Tray3D> _trays = new List<Tray3D>();
        private int _selectedTray = -1;
        private bool _animating;
        private float _slotSize;
        private float _pieceScale;

        public bool IsAnimating { get { return _animating; } }
        public PuzzleSession Session { get { return _session; } }

        /// <summary>Fraction of the visible frame kept clear for the HUD and the button bar.</summary>
        private const float TopMargin = 0.13f;
        private const float BottomMargin = 0.15f;
        private const float SideMargin = 0.03f;

        public void Bind(PuzzleSession session, Camera camera)
        {
            _session = session;
            _camera = camera;
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

            Vector2 visible = VisibleWorldSize();
            float usableWidth = visible.x * (1f - SideMargin * 2f);
            float usableHeight = visible.y * (1f - TopMargin - BottomMargin);
            float verticalShift = visible.y * (BottomMargin - TopMargin) * 0.5f;

            Layout layout = ComputeLayout(trayCount, capacity, usableWidth, usableHeight);
            _slotSize = layout.SlotSize;
            // Fills the slot without crossing the tray frame. The widest form (the bracelet) is
            // about 1.05 units across, and the tray is 1.40 slots wide, so this keeps a margin.
            _pieceScale = layout.SlotSize * 0.96f;

            for (int i = 0; i < trayCount; i++)
            {
                Tray3D tray = Tray3D.Create(transform, i, capacity, layout.SlotSize);

                Vector2 position = layout.PositionOf(i);
                tray.SetRestPosition(new Vector3(position.x, position.y + verticalShift, 0f));
                _trays.Add(tray);

                Tray model = board[i];
                for (int slot = 0; slot < model.Count; slot++)
                {
                    GameObject piece = JewelryPieceBuilder.Build(model[slot], null);
                    tray.AddPiece(piece, slot, _pieceScale);
                }

                tray.SetSealed(model.IsSealed);
            }
        }

        /// <summary>World-space size of the camera frustum at the board plane (z = 0).</summary>
        private Vector2 VisibleWorldSize()
        {
            float distance = Mathf.Abs(_camera.transform.position.z);

            if (_camera.orthographic)
            {
                float height = _camera.orthographicSize * 2f;
                return new Vector2(height * _camera.aspect, height);
            }

            float frustumHeight = 2f * distance * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return new Vector2(frustumHeight * _camera.aspect, frustumHeight);
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

                int itemsInRow = Mathf.Min(Columns, TrayCount - row * Columns);
                float rowWidth = itemsInRow * CellWidth;

                float x = -rowWidth * 0.5f + CellWidth * (column + 0.5f);
                float y = (rows - 1) * CellHeight * 0.5f - row * CellHeight;
                return new Vector2(x, y);
            }
        }

        private static Layout ComputeLayout(int trayCount, int capacity, float width, float height)
        {
            Layout best = new Layout();
            float bestSlot = 0f;

            for (int columns = 1; columns <= trayCount; columns++)
            {
                int rows = Mathf.CeilToInt(trayCount / (float)columns);

                float cellWidth = width / columns;
                float cellHeight = height / rows;

                float slotFromWidth = cellWidth / 1.40f * 0.90f;
                float slotFromHeight = cellHeight / (capacity + 0.36f) * 0.92f;
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

        // ------------------------------------------------------------------------------ input

        private void Update()
        {
            if (_session == null || _animating || _session.IsSolved) return;
            if (!Input.GetMouseButtonDown(0)) return;

            // Never let a tap that landed on a button also reach the board.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Tray3D hit = RaycastTray(Input.mousePosition);
            if (hit != null) OnTrayTapped(hit.Index);
        }

        private Tray3D RaycastTray(Vector3 screenPosition)
        {
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 200f)) return null;
            return hit.collider.GetComponentInParent<Tray3D>();
        }

        private void OnTrayTapped(int index)
        {
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

            GameObject top = _trays[index].Top;
            if (top != null)
            {
                StartCoroutine(MoveTo(top.transform, _trays[index].LiftedWorldPosition(), 0.13f, 0f));
            }

            HapticService.Play(HapticStrength.Selection);
        }

        private void Deselect()
        {
            if (_selectedTray < 0) return;

            Tray3D tray = _trays[_selectedTray];
            tray.SetSealed(_session.Board[_selectedTray].IsSealed);

            GameObject top = tray.Top;
            if (top != null)
            {
                int slot = _session.Board[_selectedTray].Count - 1;
                StartCoroutine(MoveTo(top.transform, tray.SlotWorldPosition(slot), 0.11f, 0f));
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

            Tray3D source = _trays[from];
            Tray3D destination = _trays[to];

            GameObject piece = source.RemoveTop();
            int destinationSlot = _session.Board[to].Count;

            // Update the simulation first, then animate what it decided.
            MoveResult result = _session.TryMove(move);

            piece.transform.SetParent(null, true);
            yield return MoveTo(piece.transform, destination.SlotWorldPosition(destinationSlot),
                                0.22f, _slotSize * 0.5f);

            destination.AddPiece(piece, destinationSlot, _pieceScale);

            source.SetHighlighted(false);
            source.SetSealed(_session.Board[from].IsSealed);

            HapticService.Play(result.SealedDestination ? HapticStrength.Success : HapticStrength.Light);

            if (result.SealedDestination) yield return destination.PlaySealSequence();
            else destination.SetSealed(false);

            _selectedTray = -1;
            _animating = false;

            if (MovePlayed != null) MovePlayed(result);

            if (result.Solved)
            {
                HapticService.Play(HapticStrength.Celebration);
                if (LevelSolved != null) LevelSolved();
            }
        }

        /// <summary>
        /// Moves a piece along an arc that also pulls it toward the camera, so it passes over the
        /// other trays rather than sliding through them.
        /// </summary>
        private IEnumerator MoveTo(Transform piece, Vector3 target, float duration, float arcHeight)
        {
            Vector3 start = piece.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - t) * (1f - t);

                Vector3 position = Vector3.Lerp(start, target, eased);
                float lift = Mathf.Sin(eased * Mathf.PI);
                position.y += lift * arcHeight;
                position.z -= lift * 0.45f;

                piece.position = position;
                yield return null;
            }

            piece.position = target;
        }

        /// <summary>Undo is a full rebuild rather than a reverse animation — simpler, never drifts.</summary>
        public void ResyncFromModel()
        {
            if (_animating) return;
            _selectedTray = -1;
            Rebuild();
        }
    }
}
