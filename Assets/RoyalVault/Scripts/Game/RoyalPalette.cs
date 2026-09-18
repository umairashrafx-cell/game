using UnityEngine;
using RoyalVault.Core;

namespace RoyalVault.Game
{
    /// <summary>
    /// The game's colour identity. Deliberately not "everything is gold": the gems need strong
    /// hue separation to read instantly on a small screen, so gold is reserved for the vault
    /// furniture and highlights while the gems own the saturated end of the palette.
    ///
    /// Every material is also separated in LIGHTNESS, not just hue, so the board stays readable
    /// for colour-blind players even before the shape silhouettes do their work.
    /// </summary>
    public static class RoyalPalette
    {
        // Vault furniture — warm, dark, luxurious.
        public static readonly Color VaultBackground = Hex("241E1B");
        public static readonly Color VaultPanel = Hex("332A25");
        public static readonly Color TrayVelvet = Hex("3E332C");
        public static readonly Color TraySlot = Hex("2C2420");
        public static readonly Color TrayRim = Hex("C9A961");
        public static readonly Color TraySealedRim = Hex("F2D48A");

        public static readonly Color Ivory = Hex("F7F1E6");
        public static readonly Color Champagne = Hex("D9C7A3");
        public static readonly Color Gold = Hex("C9A961");
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.35f);

        public static Color MaterialColor(JewelMaterial material)
        {
            switch (material)
            {
                case JewelMaterial.Gold:     return Hex("E8B84B");   // warm, mid-bright
                case JewelMaterial.Diamond:  return Hex("DCEBF5");   // lightest
                case JewelMaterial.Ruby:     return Hex("C5283D");   // dark, saturated red
                case JewelMaterial.Emerald:  return Hex("1E8A5A");   // dark green
                case JewelMaterial.Sapphire: return Hex("2A5CAA");   // dark blue
                case JewelMaterial.Pearl:    return Hex("F3E6DA");   // near-white, warm
                default: return Color.magenta;                        // deliberately loud if unset
            }
        }

        /// <summary>A darker edge of the same hue, used to keep pieces from floating on the velvet.</summary>
        public static Color MaterialEdge(JewelMaterial material)
        {
            Color c = MaterialColor(material);
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            return Color.HSVToRGB(h, Mathf.Clamp01(s * 1.05f), Mathf.Clamp01(v * 0.62f));
        }

        public static Color Hex(string hex)
        {
            Color color;
            return ColorUtility.TryParseHtmlString("#" + hex, out color) ? color : Color.magenta;
        }
    }
}
