using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RoyalVault.Game
{
    /// <summary>
    /// One velvet tray. Owns its slot geometry and the visual state that belongs to the tray
    /// itself — the rim, the seal flash, the rejection shake — but knows nothing about the rules.
    /// </summary>
    public sealed class TrayView : MonoBehaviour, IPointerClickHandler
    {
        public int Index { get; private set; }
        public float SlotSize { get; private set; }

        private RectTransform _rect;
        private Image _velvet;
        private Image _rim;
        private Image _glow;
        private readonly List<PieceView> _pieces = new List<PieceView>();
        private RectTransform _pieceLayer;

        private int _capacity;
        private Action<int> _onClicked;
        private Coroutine _shake;

        public RectTransform Rect { get { return _rect; } }
        public IList<PieceView> Pieces { get { return _pieces; } }

        public static TrayView Create(Transform parent, int index, int capacity, float slotSize,
                                      Action<int> onClicked)
        {
            GameObject go = new GameObject("Tray_" + index, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            TrayView view = go.AddComponent<TrayView>();
            view.Build(index, capacity, slotSize, onClicked);
            return view;
        }

        private void Build(int index, int capacity, float slotSize, Action<int> onClicked)
        {
            Index = index;
            _capacity = capacity;
            SlotSize = slotSize;
            _onClicked = onClicked;

            _rect = (RectTransform)transform;
            float width = slotSize * 1.28f;
            float height = slotSize * capacity + slotSize * 0.34f;
            _rect.sizeDelta = new Vector2(width, height);

            // The root image is the gold RIM and the touch target, sized generously for thumbs.
            // The velvet is then inset inside it. Doing it the other way round does not work:
            // a child always draws over its parent's image, so a stretched rim would simply
            // cover the velvet and the tray would read as a flat gold slab.
            _rim = GetComponent<Image>();
            _rim.color = RoyalPalette.TrayRim;
            _rim.sprite = ProceduralSprites.RoundedRect(28);
            _rim.type = Image.Type.Sliced;
            _rim.raycastTarget = true;

            _velvet = UiFactory.ImagePanel("Velvet", transform, RoyalPalette.TrayVelvet, 24);
            RectTransform velvetRect = (RectTransform)_velvet.transform;
            UiFactory.Stretch(velvetRect);
            velvetRect.offsetMin = new Vector2(4f, 4f);
            velvetRect.offsetMax = new Vector2(-4f, -4f);

            // Empty slot recesses, so an empty tray still reads as "this holds four things".
            for (int i = 0; i < capacity; i++)
            {
                Image slot = UiFactory.ImagePanel("Slot_" + i, transform, RoyalPalette.TraySlot, 20);
                RectTransform slotRect = (RectTransform)slot.transform;
                slotRect.sizeDelta = new Vector2(slotSize * 0.82f, slotSize * 0.82f);
                slotRect.anchoredPosition = SlotPosition(i);
            }

            _glow = UiFactory.ImagePanel("SealGlow", transform, new Color(1f, 1f, 1f, 0f));
            _glow.sprite = ProceduralSprites.SoftGlow();
            RectTransform glowRect = (RectTransform)_glow.transform;
            glowRect.sizeDelta = new Vector2(width * 2.1f, height * 1.5f);

            _pieceLayer = UiFactory.Panel("Pieces", transform);
            UiFactory.Stretch(_pieceLayer);
        }

        /// <summary>Slot 0 is the bottom of the tray, matching the simulation's stack order.</summary>
        public Vector2 SlotPosition(int slotIndex)
        {
            float height = _rect.sizeDelta.y;
            float bottom = -height * 0.5f + SlotSize * 0.5f + SlotSize * 0.17f;
            return new Vector2(0f, bottom + slotIndex * SlotSize);
        }

        public Vector2 LiftedPosition()
        {
            return new Vector2(0f, _rect.sizeDelta.y * 0.5f + SlotSize * 0.55f);
        }

        public Transform PieceLayer { get { return _pieceLayer; } }

        public void AddPiece(PieceView piece, int slotIndex)
        {
            _pieces.Add(piece);
            piece.Rect.SetParent(_pieceLayer, false);
            piece.Rect.anchoredPosition = SlotPosition(slotIndex);
        }

        public PieceView RemoveTop()
        {
            if (_pieces.Count == 0) return null;
            PieceView piece = _pieces[_pieces.Count - 1];
            _pieces.RemoveAt(_pieces.Count - 1);
            return piece;
        }

        public PieceView Top
        {
            get { return _pieces.Count == 0 ? null : _pieces[_pieces.Count - 1]; }
        }

        public void SetSealed(bool isSealed)
        {
            _rim.color = isSealed ? RoyalPalette.TraySealedRim : RoyalPalette.TrayRim;
            _velvet.color = isSealed
                ? Color.Lerp(RoyalPalette.TrayVelvet, RoyalPalette.Gold, 0.14f)
                : RoyalPalette.TrayVelvet;
        }

        public void SetHighlighted(bool highlighted)
        {
            _rim.color = highlighted ? RoyalPalette.Champagne : RoyalPalette.TrayRim;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_onClicked != null) _onClicked(Index);
        }

        /// <summary>A short, controlled shake. Deliberately small — a rejection should inform, not punish.</summary>
        public IEnumerator Shake()
        {
            if (_shake != null) StopCoroutine(_shake);
            _shake = StartCoroutine(ShakeRoutine());
            yield return _shake;
        }

        private IEnumerator ShakeRoutine()
        {
            Vector2 origin = _rect.anchoredPosition;
            const float duration = 0.22f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float decay = 1f - t;
                float offset = Mathf.Sin(t * Mathf.PI * 6f) * 11f * decay;
                _rect.anchoredPosition = origin + new Vector2(offset, 0f);
                yield return null;
            }

            _rect.anchoredPosition = origin;
            _shake = null;
        }

        /// <summary>
        /// The Royal Match seal: gold light sweeps the tray, the pieces flash, the rim locks to
        /// bright gold. Kept under half a second so it never gets in the way of the next move.
        /// </summary>
        public IEnumerator PlaySealSequence()
        {
            SetSealed(true);

            for (int i = 0; i < _pieces.Count; i++)
            {
                StartCoroutine(_pieces[i].Sparkle(0.34f));
                yield return new WaitForSeconds(0.045f);
            }

            const float duration = 0.34f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = Mathf.Sin(t * Mathf.PI);
                _glow.color = new Color(RoyalPalette.TraySealedRim.r, RoyalPalette.TraySealedRim.g,
                                        RoyalPalette.TraySealedRim.b, pulse * 0.55f);
                yield return null;
            }

            _glow.color = new Color(1f, 1f, 1f, 0f);
        }
    }
}
