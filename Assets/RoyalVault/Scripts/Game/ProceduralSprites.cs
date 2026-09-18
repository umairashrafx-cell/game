using System.Collections.Generic;
using UnityEngine;
using RoyalVault.Core;

namespace RoyalVault.Game
{
    /// <summary>
    /// Generates every sprite the prototype needs at runtime, so the game has a real visual
    /// identity with no imported art at all. Production art replaces this later by swapping the
    /// sprite lookup — nothing else needs to change.
    ///
    /// The important part is not that it saves assets: it is that each jewelry FORM gets a
    /// distinct silhouette. Colour alone never identifies a piece, which is what the
    /// accessibility requirement actually demands.
    ///
    /// Sprites are generated once and cached; nothing here runs during play.
    /// </summary>
    public static class ProceduralSprites
    {
        public const int FormResolution = 128;
        private const int Supersample = 3;   // 3x3 coverage sampling for clean edges

        private static readonly Dictionary<JewelForm, Sprite> FormCache = new Dictionary<JewelForm, Sprite>();
        private static readonly Dictionary<int, Sprite> RoundedRectCache = new Dictionary<int, Sprite>();
        private static Sprite _softGlow;

        public static Sprite Form(JewelForm form)
        {
            Sprite cached;
            if (FormCache.TryGetValue(form, out cached)) return cached;

            Texture2D texture = new Texture2D(FormResolution, FormResolution, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            texture.SetPixels32(RasteriseForm(form));
            // Drop the CPU copy once uploaded — these textures are never read back at runtime,
            // and on a low-end phone the saved memory matters more than readability.
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, FormResolution, FormResolution),
                                          new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "Form_" + form;
            FormCache[form] = sprite;
            return sprite;
        }

        /// <summary>
        /// Rasterises a form's silhouette to an alpha mask. Exposed separately from
        /// <see cref="Form"/> so the shapes can be compared in tests without keeping the uploaded
        /// textures readable at runtime.
        /// </summary>
        public static Color32[] RasteriseForm(JewelForm form)
        {
            Color32[] pixels = new Color32[FormResolution * FormResolution];
            float step = 1f / (Supersample + 1);

            for (int y = 0; y < FormResolution; y++)
            {
                for (int x = 0; x < FormResolution; x++)
                {
                    int hits = 0;
                    for (int sy = 1; sy <= Supersample; sy++)
                    {
                        for (int sx = 1; sx <= Supersample; sx++)
                        {
                            float nx = ((x + sx * step) / FormResolution) * 2f - 1f;
                            float ny = ((y + sy * step) / FormResolution) * 2f - 1f;
                            if (IsInside(form, nx, ny)) hits++;
                        }
                    }

                    byte alpha = (byte)(255f * hits / (Supersample * Supersample));
                    pixels[y * FormResolution + x] = new Color32(255, 255, 255, alpha);
                }
            }

            return pixels;
        }

        /// <summary>
        /// Silhouettes in normalised space where both axes run -1..1. Kept as simple boolean
        /// predicates rather than signed-distance fields because they are far easier to verify
        /// by eye, and supersampling hides the difference.
        /// </summary>
        private static bool IsInside(JewelForm form, float x, float y)
        {
            switch (form)
            {
                case JewelForm.Ring:
                    return Annulus(x, y + 0.08f, 0.46f, 0.74f) || Disc(x, y - 0.66f, 0.17f);

                case JewelForm.Bracelet:
                    // A wide, flat band — instantly distinguishable from a ring at a glance.
                    return EllipseAnnulus(x, y, 0.92f, 0.56f, 0.62f);

                case JewelForm.Necklace:
                    // An open chain hanging from a clasp.
                    return (Annulus(x, y + 0.10f, 0.56f, 0.80f) && y < 0.34f) || Disc(x, y - 0.74f, 0.14f);

                case JewelForm.Earrings:
                    // A deliberately paired silhouette — two of something reads as "earrings".
                    return Disc(x + 0.40f, y + 0.18f, 0.32f) || Disc(x - 0.40f, y + 0.18f, 0.32f)
                        || Disc(x + 0.40f, y - 0.58f, 0.12f) || Disc(x - 0.40f, y - 0.58f, 0.12f);

                case JewelForm.Pendant:
                    return Diamond(x, y + 0.14f, 0.74f) || Annulus(x, y - 0.72f, 0.10f, 0.20f);

                case JewelForm.Crown:
                    return Crown(x, y);

                default:
                    return Disc(x, y, 0.7f);
            }
        }

        private static bool Disc(float x, float y, float radius)
        {
            return x * x + y * y <= radius * radius;
        }

        private static bool Annulus(float x, float y, float inner, float outer)
        {
            float d2 = x * x + y * y;
            return d2 <= outer * outer && d2 >= inner * inner;
        }

        private static bool EllipseAnnulus(float x, float y, float rx, float ry, float innerScale)
        {
            float outer = (x * x) / (rx * rx) + (y * y) / (ry * ry);
            float inner = (x * x) / (rx * rx * innerScale * innerScale) + (y * y) / (ry * ry * innerScale * innerScale);
            return outer <= 1f && inner >= 1f;
        }

        private static bool Diamond(float x, float y, float size)
        {
            return Mathf.Abs(x) + Mathf.Abs(y) <= size;
        }

        private static bool Crown(float x, float y)
        {
            float ax = Mathf.Abs(x);
            if (ax > 0.80f) return false;

            // Solid band across the bottom.
            if (y >= -0.62f && y <= -0.10f) return true;

            // Three peaks rising from the band.
            if (y > -0.10f && y <= 0.72f)
            {
                float peakHeight = 0.72f;
                float[] centres = { -0.52f, 0f, 0.52f };
                for (int i = 0; i < centres.Length; i++)
                {
                    float halfWidth = 0.26f * (1f - Mathf.InverseLerp(-0.10f, peakHeight, y));
                    if (Mathf.Abs(x - centres[i]) <= halfWidth) return true;
                }
            }
            return false;
        }

        /// <summary>A rounded rectangle for panels, trays and buttons. Sliced so it scales cleanly.</summary>
        public static Sprite RoundedRect(int cornerRadius)
        {
            Sprite cached;
            if (RoundedRectCache.TryGetValue(cornerRadius, out cached)) return cached;

            int size = cornerRadius * 2 + 8;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color32[] pixels = new Color32[size * size];
            float step = 1f / (Supersample + 1);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int hits = 0;
                    for (int sy = 1; sy <= Supersample; sy++)
                    {
                        for (int sx = 1; sx <= Supersample; sx++)
                        {
                            if (InsideRoundedRect(x + sx * step, y + sy * step, size, cornerRadius)) hits++;
                        }
                    }
                    byte alpha = (byte)(255f * hits / (Supersample * Supersample));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                                          0, SpriteMeshType.FullRect,
                                          new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius));
            sprite.name = "RoundedRect_" + cornerRadius;
            RoundedRectCache[cornerRadius] = sprite;
            return sprite;
        }

        private static bool InsideRoundedRect(float x, float y, int size, int radius)
        {
            float maxX = size - radius;
            float maxY = size - radius;

            float cx = Mathf.Clamp(x, radius, maxX);
            float cy = Mathf.Clamp(y, radius, maxY);

            float dx = x - cx;
            float dy = y - cy;
            return dx * dx + dy * dy <= (float)radius * radius;
        }

        /// <summary>A soft radial falloff used for glows and the Royal Match flash.</summary>
        public static Sprite SoftGlow()
        {
            if (_softGlow != null) return _softGlow;

            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)size) * 2f - 1f;
                    float ny = (y / (float)size) * 2f - 1f;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;                       // squared falloff reads as light, not a disc
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            _softGlow = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                                      0, SpriteMeshType.FullRect);
            _softGlow.name = "SoftGlow";
            return _softGlow;
        }
    }
}
