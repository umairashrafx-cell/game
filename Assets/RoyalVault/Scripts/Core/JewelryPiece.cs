using System;

namespace RoyalVault.Core
{
    /// <summary>
    /// The gemstone / metal a piece is made from. Carries the piece's colour identity.
    /// Named JewelMaterial rather than Material to avoid confusion with UnityEngine.Material
    /// in the presentation layer.
    /// </summary>
    public enum JewelMaterial : byte
    {
        None = 0,
        Gold = 1,
        Diamond = 2,
        Ruby = 3,
        Emerald = 4,
        Sapphire = 5,
        Pearl = 6
    }

    /// <summary>
    /// The kind of jewelry a piece is. Carries the piece's silhouette identity.
    /// Form is deliberately load-bearing so that colour is never the only signal,
    /// which is what makes the game playable for colour-blind players.
    /// </summary>
    public enum JewelForm : byte
    {
        None = 0,
        Ring = 1,
        Necklace = 2,
        Earrings = 3,
        Bracelet = 4,
        Pendant = 5,
        Crown = 6
    }

    /// <summary>
    /// A single jewelry piece: exactly one material and one form.
    /// Immutable 2-byte value type so boards can be cloned cheaply by the solver
    /// without generating garbage.
    /// </summary>
    public readonly struct JewelryPiece : IEquatable<JewelryPiece>
    {
        public readonly JewelMaterial Material;
        public readonly JewelForm Form;

        public JewelryPiece(JewelMaterial material, JewelForm form)
        {
            Material = material;
            Form = form;
        }

        /// <summary>Represents an empty slot.</summary>
        public static readonly JewelryPiece Empty = new JewelryPiece(JewelMaterial.None, JewelForm.None);

        public bool IsEmpty
        {
            get { return Material == JewelMaterial.None && Form == JewelForm.None; }
        }

        /// <summary>
        /// True when the two pieces have at least one attribute in common.
        /// This is the atom the whole Dual Identity rule is built from.
        /// </summary>
        public bool SharesAttributeWith(JewelryPiece other)
        {
            return Material == other.Material || Form == other.Form;
        }

        public bool Equals(JewelryPiece other)
        {
            return Material == other.Material && Form == other.Form;
        }

        public override bool Equals(object obj)
        {
            return obj is JewelryPiece && Equals((JewelryPiece)obj);
        }

        public override int GetHashCode()
        {
            return ((int)Material << 8) | (int)Form;
        }

        /// <summary>Compact single-character-per-attribute encoding used by the solver's visited set.</summary>
        public int ToCode()
        {
            return ((int)Material << 4) | (int)Form;
        }

        public override string ToString()
        {
            return IsEmpty ? "—" : Material + " " + Form;
        }
    }
}
