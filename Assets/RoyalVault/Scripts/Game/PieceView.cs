using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using RoyalVault.Core;

namespace RoyalVault.Game
{
    /// <summary>
    /// One jewelry piece on screen. Draws a darker edge behind the silhouette so pieces sit on
    /// the velvet instead of floating on it.
    /// </summary>
    public sealed class PieceView : MonoBehaviour
    {
        public JewelryPiece Piece { get; private set; }

        private RectTransform _rect;
        private Image _edge;
        private Image _face;
        private Image _sheen;

        public RectTransform Rect { get { return _rect; } }

        public static PieceView Create(Transform parent, JewelryPiece piece, float size)
        {
            GameObject go = new GameObject("Piece_" + piece.Material + "_" + piece.Form, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            PieceView view = go.AddComponent<PieceView>();
            view.Build(piece, size);
            return view;
        }

        private void Build(JewelryPiece piece, float size)
        {
            Piece = piece;
            _rect = (RectTransform)transform;
            _rect.sizeDelta = new Vector2(size, size);

            Sprite silhouette = ProceduralSprites.Form(piece.Form);

            _edge = UiFactory.ImagePanel("Edge", transform, RoyalPalette.MaterialEdge(piece.Material));
            _edge.sprite = silhouette;
            RectTransform edgeRect = (RectTransform)_edge.transform;
            edgeRect.sizeDelta = new Vector2(size, size);
            edgeRect.anchoredPosition = new Vector2(0f, -size * 0.045f);

            _face = UiFactory.ImagePanel("Face", transform, RoyalPalette.MaterialColor(piece.Material));
            _face.sprite = silhouette;
            ((RectTransform)_face.transform).sizeDelta = new Vector2(size * 0.92f, size * 0.92f);

            // A soft highlight in the upper-left sells the idea of a polished gem.
            _sheen = UiFactory.ImagePanel("Sheen", transform, new Color(1f, 1f, 1f, 0.22f));
            _sheen.sprite = ProceduralSprites.SoftGlow();
            RectTransform sheenRect = (RectTransform)_sheen.transform;
            sheenRect.sizeDelta = new Vector2(size * 0.44f, size * 0.44f);
            sheenRect.anchoredPosition = new Vector2(-size * 0.16f, size * 0.18f);
        }

        public void SetSize(float size)
        {
            _rect.sizeDelta = new Vector2(size, size);
            ((RectTransform)_edge.transform).sizeDelta = new Vector2(size, size);
            ((RectTransform)_face.transform).sizeDelta = new Vector2(size * 0.92f, size * 0.92f);
        }

        public IEnumerator AnimateArcTo(Vector2 target, float duration, float arcHeight)
        {
            Vector2 start = _rect.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Ease-out so the piece arrives softly rather than snapping into the slot.
                float eased = 1f - (1f - t) * (1f - t);
                Vector2 position = Vector2.Lerp(start, target, eased);
                position.y += Mathf.Sin(eased * Mathf.PI) * arcHeight;

                _rect.anchoredPosition = position;
                yield return null;
            }

            _rect.anchoredPosition = target;
        }

        public IEnumerator Sparkle(float duration)
        {
            float elapsed = 0f;
            Color baseColor = _sheen.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = Mathf.Sin(t * Mathf.PI);

                _sheen.color = new Color(1f, 1f, 1f, 0.22f + pulse * 0.66f);
                float scale = 1f + pulse * 0.12f;
                _rect.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            _sheen.color = baseColor;
            _rect.localScale = Vector3.one;
        }

        public void SetLifted(bool lifted, float size)
        {
            _rect.localScale = lifted ? new Vector3(1.12f, 1.12f, 1f) : Vector3.one;
            _sheen.color = new Color(1f, 1f, 1f, lifted ? 0.5f : 0.22f);
        }
    }
}
