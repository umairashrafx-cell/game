using UnityEngine;
using UnityEngine.UI;

namespace RoyalVault.Game
{
    /// <summary>
    /// Small helpers for building UI in code. The whole game is assembled procedurally rather
    /// than from prefabs, which keeps layout responsive by construction and keeps the project
    /// free of hand-edited scene YAML.
    /// </summary>
    public static class UiFactory
    {
        public static RectTransform Panel(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image ImagePanel(string name, Transform parent, Color color, int cornerRadius = 0)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            Image image = go.GetComponent<Image>();
            image.color = color;
            if (cornerRadius > 0)
            {
                image.sprite = ProceduralSprites.RoundedRect(cornerRadius);
                image.type = Image.Type.Sliced;
            }
            image.raycastTarget = false;
            return image;
        }

        public static Text Label(string name, Transform parent, string text, int fontSize,
                                 Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            Text label = go.GetComponent<Text>();
            // The built-in legacy font is always present, so the prototype needs no font asset
            // import to display text in a headless-built project.
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        public static Button TextButton(string name, Transform parent, string caption,
                                        Vector2 size, Color background, Color foreground)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)go.transform;
            rect.sizeDelta = size;

            Image image = go.GetComponent<Image>();
            image.color = background;
            image.sprite = ProceduralSprites.RoundedRect(24);
            image.type = Image.Type.Sliced;

            Text label = Label(name + "Label", go.transform, caption, 34, foreground);
            Stretch((RectTransform)label.transform);

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            return button;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
                                  Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
