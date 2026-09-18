using System;

namespace RoyalVault.Core
{
    /// <summary>
    /// What a tray has committed to. Computed as the intersection of the attributes of
    /// every piece currently in the tray.
    ///
    ///   empty tray                        -> Unconstrained (accepts anything)
    ///   [Ruby Ring]                       -> Material=Ruby, Form=Ring   (undecided: accepts ruby OR rings)
    ///   [Ruby Ring, Ruby Necklace]        -> Material=Ruby, Form=None   (locked to ruby)
    ///   [Ruby Ring, Gold Ring]            -> Material=None, Form=Ring   (locked to rings)
    ///   [Ruby Ring, Gold Necklace]        -> Broken — cannot arise from legal play, only
    ///                                        from an authored starting layout.
    /// </summary>
    public readonly struct TrayIdentity
    {
        public readonly JewelMaterial Material;
        public readonly JewelForm Form;
        public readonly bool IsEmptyTray;

        public TrayIdentity(JewelMaterial material, JewelForm form, bool isEmptyTray)
        {
            Material = material;
            Form = form;
            IsEmptyTray = isEmptyTray;
        }

        public static readonly TrayIdentity Unconstrained =
            new TrayIdentity(JewelMaterial.None, JewelForm.None, true);

        /// <summary>
        /// True when every piece in the tray shares at least one attribute with all the others.
        /// A full, coherent tray is a completed set and seals.
        /// </summary>
        public bool IsCoherent
        {
            get { return IsEmptyTray || Material != JewelMaterial.None || Form != JewelForm.None; }
        }

        /// <summary>True once the tray has narrowed to a single committed attribute.</summary>
        public bool IsLocked
        {
            get
            {
                if (IsEmptyTray) return false;
                bool hasMaterial = Material != JewelMaterial.None;
                bool hasForm = Form != JewelForm.None;
                return hasMaterial ^ hasForm;
            }
        }

        public bool Accepts(JewelryPiece piece)
        {
            if (IsEmptyTray) return true;
            if (Material != JewelMaterial.None && piece.Material == Material) return true;
            if (Form != JewelForm.None && piece.Form == Form) return true;
            return false;
        }
    }

    /// <summary>
    /// A vertical velvet jewelry tray. Pieces stack from the bottom up and only the
    /// topmost piece can be lifted, which is what creates the planning tension.
    /// </summary>
    public sealed class Tray
    {
        private readonly JewelryPiece[] _slots;
        private int _count;

        public int Capacity { get { return _slots.Length; } }
        public int Count { get { return _count; } }
        public bool IsEmpty { get { return _count == 0; } }
        public bool IsFull { get { return _count == _slots.Length; } }

        public Tray(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException("capacity");
            _slots = new JewelryPiece[capacity];
            _count = 0;
        }

        public Tray(int capacity, params JewelryPiece[] initial) : this(capacity)
        {
            if (initial == null) return;
            if (initial.Length > capacity)
                throw new ArgumentException("More pieces than the tray has slots.");
            for (int i = 0; i < initial.Length; i++) _slots[i] = initial[i];
            _count = initial.Length;
        }

        /// <summary>Slot 0 is the bottom of the tray; slot Count-1 is the accessible top.</summary>
        public JewelryPiece this[int index]
        {
            get
            {
                if (index < 0 || index >= _count) throw new IndexOutOfRangeException();
                return _slots[index];
            }
        }

        public JewelryPiece Top
        {
            get { return _count == 0 ? JewelryPiece.Empty : _slots[_count - 1]; }
        }

        public TrayIdentity Identity
        {
            get
            {
                if (_count == 0) return TrayIdentity.Unconstrained;

                JewelMaterial material = _slots[0].Material;
                JewelForm form = _slots[0].Form;
                for (int i = 1; i < _count; i++)
                {
                    if (_slots[i].Material != material) material = JewelMaterial.None;
                    if (_slots[i].Form != form) form = JewelForm.None;
                }
                return new TrayIdentity(material, form, false);
            }
        }

        /// <summary>
        /// A sealed tray is a completed set: full and coherent. Sealing triggers Royal Match
        /// and the tray can no longer be taken from.
        ///
        /// Note that "full" alone is not enough — levels deliberately start with full, mixed
        /// trays, and those must stay playable.
        /// </summary>
        public bool IsSealed
        {
            get { return IsFull && Identity.IsCoherent; }
        }

        public bool CanAccept(JewelryPiece piece)
        {
            if (IsFull) return false;
            return Identity.Accepts(piece);
        }

        public void Push(JewelryPiece piece)
        {
            if (IsFull) throw new InvalidOperationException("Tray is full.");
            _slots[_count++] = piece;
        }

        public JewelryPiece Pop()
        {
            if (_count == 0) throw new InvalidOperationException("Tray is empty.");
            JewelryPiece piece = _slots[--_count];
            _slots[_count] = JewelryPiece.Empty;
            return piece;
        }

        public Tray Clone()
        {
            Tray copy = new Tray(Capacity);
            Array.Copy(_slots, copy._slots, _slots.Length);
            copy._count = _count;
            return copy;
        }

        /// <summary>Stable encoding used to deduplicate states inside the solver.</summary>
        public void AppendKey(System.Text.StringBuilder sb)
        {
            sb.Append((char)('a' + Capacity));
            for (int i = 0; i < _count; i++) sb.Append((char)('0' + _slots[i].ToCode()));
            sb.Append('|');
        }
    }
}
