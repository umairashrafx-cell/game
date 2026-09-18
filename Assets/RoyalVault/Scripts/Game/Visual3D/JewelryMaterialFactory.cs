using System.Collections.Generic;
using UnityEngine;
using RoyalVault.Core;

namespace RoyalVault.Game.Visual3D
{
    /// <summary>
    /// Physically-based materials for the jewelry and the vault.
    ///
    /// Deliberately resolves its shader at runtime, preferring URP's Lit and falling back to the
    /// built-in Standard shader. A half-finished pipeline migration is the classic way to end up
    /// with a screen full of magenta, and the game should survive that rather than break.
    ///
    /// Gems are not simply "coloured plastic": they carry near-mirror smoothness plus a faint
    /// emission of their own hue, which is what suggests light bouncing around inside a cut
    /// stone. Combined with the flat-shaded facets from the mesh factory, that is most of the
    /// illusion.
    /// </summary>
    public static class JewelryMaterialFactory
    {
        private static Shader _litShader;
        private static bool _usingUrp;
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        private static Shader LitShader
        {
            get
            {
                if (_litShader != null) return _litShader;

                _litShader = Shader.Find("Universal Render Pipeline/Lit");
                _usingUrp = _litShader != null;

                if (_litShader == null) _litShader = Shader.Find("Standard");
                if (_litShader == null) _litShader = Shader.Find("Sprites/Default");

                return _litShader;
            }
        }

        public static bool UsingUrp
        {
            get { Shader unused = LitShader; return _usingUrp; }
        }

        private static Material Create(string key, Color baseColor, float metallic, float smoothness,
                                       Color emission, float emissionStrength)
        {
            Material cached;
            if (Cache.TryGetValue(key, out cached) && cached != null) return cached;

            Material material = new Material(LitShader);
            material.name = key;

            // URP and the built-in Standard shader disagree on property names, so set both.
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
            if (material.HasProperty("_Color")) material.SetColor("_Color", baseColor);

            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);

            if (emissionStrength > 0f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", emission * emissionStrength);
            }

            material.enableInstancing = true;
            Cache[key] = material;
            return material;
        }

        /// <summary>The stone at the heart of a piece. This is what communicates the gem identity.</summary>
        public static Material Stone(JewelMaterial jewel)
        {
            switch (jewel)
            {
                // Emission is pushed above the bloom threshold on purpose. A faceted stone in a
                // near-black vault otherwise turns into a dark hole wherever a facet happens to
                // face away from a light — the emission keeps every facet alive and gives bloom
                // something to catch, which is what reads as "the gem is catching the light".

                case JewelMaterial.Gold:
                    // Gold has no gemstone, so a gold piece is gold throughout.
                    return Create("stone_gold", Hex("F0C04A"), 1f, 0.88f, Hex("C89A2E"), 0.45f);

                case JewelMaterial.Diamond:
                    return Create("stone_diamond", Hex("EAF4FF"), 0f, 0.99f, Hex("BFE0FF"), 0.95f);

                case JewelMaterial.Ruby:
                    return Create("stone_ruby", Hex("C21E38"), 0f, 0.96f, Hex("FF2A4A"), 1.10f);

                case JewelMaterial.Emerald:
                    return Create("stone_emerald", Hex("13915A"), 0f, 0.95f, Hex("1EE08A"), 1.00f);

                case JewelMaterial.Sapphire:
                    return Create("stone_sapphire", Hex("2356B8"), 0f, 0.96f, Hex("3A7DFF"), 1.10f);

                case JewelMaterial.Pearl:
                    // Softer and far less glossy — a pearl should not look like glass.
                    return Create("stone_pearl", Hex("F6ECE0"), 0.18f, 0.72f, Hex("FFF4E6"), 0.28f);

                default:
                    return Create("stone_unknown", Color.magenta, 0f, 0.5f, Color.black, 0f);
            }
        }

        /// <summary>
        /// The metal a piece is set in. Gold pieces get bright yellow gold; coloured stones get a
        /// slightly warmer, deeper gold so the stone stays the brightest thing on the piece.
        /// </summary>
        public static Material Setting(JewelMaterial jewel)
        {
            if (jewel == JewelMaterial.Diamond || jewel == JewelMaterial.Pearl)
                return Create("setting_white", Hex("D8DCE4"), 1f, 0.90f, Color.black, 0f);

            return Create("setting_gold", Hex("C9A961"), 1f, 0.84f, Color.black, 0f);
        }

        // ------------------------------------------------------------------- vault furniture

        /// <summary>
        /// Deliberately near-black. The ambient is bright so that metal has something to reflect,
        /// but that same ambient lights every diffuse surface too — at a mid-brown albedo the
        /// velvet washed out to milky beige and the jewelry stopped being the brightest thing on
        /// screen. Velvet should swallow light, not bounce it.
        /// </summary>
        public static Material Velvet()
        {
            return Create("velvet", Hex("1C1613"), 0f, 0.06f, Color.black, 0f);
        }

        public static Material TrayFrame()
        {
            return Create("tray_frame", Hex("B4924F"), 1f, 0.72f, Color.black, 0f);
        }

        /// <summary>
        /// A waiting, empty tray. Deliberately much dimmer than a tray holding jewelry: with
        /// every frame at full brightness the empty trays were as loud as the full ones and the
        /// eye had nowhere to land. Attention should fall on where the pieces actually are.
        /// </summary>
        public static Material TrayFrameEmpty()
        {
            return Create("tray_frame_empty", Hex("4A3D24"), 1f, 0.55f, Color.black, 0f);
        }

        public static Material TrayFrameSealed()
        {
            return Create("tray_frame_sealed", Hex("F2D48A"), 1f, 0.88f, Hex("F2D48A"), 0.35f);
        }

        public static Material TrayFrameHighlighted()
        {
            return Create("tray_frame_highlight", Hex("F5E6C8"), 1f, 0.86f, Hex("F5E6C8"), 0.22f);
        }

        public static Material VaultFloor()
        {
            return Create("vault_floor", Hex("120D0B"), 0.1f, 0.30f, Color.black, 0f);
        }

        public static Material VaultWall()
        {
            return Create("vault_wall", Hex("0B0807"), 0f, 0.18f, Color.black, 0f);
        }

        /// <summary>
        /// The recessed panel the trays are displayed against. A touch lighter than the
        /// surrounding stone so the board reads as standing in a lit alcove rather than floating
        /// in front of a flat wall.
        /// </summary>
        public static Material VaultNiche()
        {
            return Create("vault_niche", Hex("17100C"), 0f, 0.20f, Color.black, 0f);
        }

        /// <summary>Polished stone for columns — darker than gold, but still catching highlights.</summary>
        public static Material VaultStone()
        {
            return Create("vault_stone", Hex("191310"), 0.15f, 0.48f, Color.black, 0f);
        }

        /// <summary>
        /// Bright panels placed behind the camera, where the player never sees them but the
        /// reflection probe does. This is the studio-lighting trick: polished metal looks
        /// expensive because of what it reflects, and a dark room gives it nothing. These give
        /// the gold its bright rolling highlights.
        /// </summary>
        public static Material ReflectorWarm()
        {
            return Create("reflector_warm", Hex("FFE6BE"), 0f, 0.1f, Hex("FFE6BE"), 2.6f);
        }

        public static Material ReflectorCool()
        {
            return Create("reflector_cool", Hex("CFE2FF"), 0f, 0.1f, Hex("CFE2FF"), 1.5f);
        }

        private static Color Hex(string hex)
        {
            Color color;
            return ColorUtility.TryParseHtmlString("#" + hex, out color) ? color : Color.magenta;
        }
    }
}
