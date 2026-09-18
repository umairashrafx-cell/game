using UnityEngine;

namespace RoyalVault.Game
{
    /// <summary>
    /// Keeps content inside the device's safe area, so notches, punch-holes and gesture bars
    /// never cover a tray or a button. Re-applies on orientation and resolution changes rather
    /// than assuming the value read at startup stays true.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private ScreenOrientation _lastOrientation;
        private Vector2Int _lastResolution;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea ||
                Screen.orientation != _lastOrientation ||
                Screen.width != _lastResolution.x ||
                Screen.height != _lastResolution.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            _lastSafeArea = Screen.safeArea;
            _lastOrientation = Screen.orientation;
            _lastResolution = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width <= 0 || Screen.height <= 0) return;

            Vector2 min = _lastSafeArea.position;
            Vector2 max = _lastSafeArea.position + _lastSafeArea.size;

            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
