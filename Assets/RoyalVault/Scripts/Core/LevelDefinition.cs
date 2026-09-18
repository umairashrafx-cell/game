using System;
using System.Collections.Generic;

namespace RoyalVault.Core
{
    /// <summary>
    /// Pure data description of a level. Contains no engine types so levels can be authored,
    /// diffed, bulk-validated and solved without opening the editor.
    /// </summary>
    public sealed class LevelDefinition
    {
        public int Id;
        public int World;
        public string DisplayName;

        /// <summary>Slots per tray. Uniform within a level for now.</summary>
        public int TrayCapacity = 4;

        /// <summary>Starting contents, bottom slot first. These trays begin full and mixed.</summary>
        public JewelryPiece[][] FilledTrays = new JewelryPiece[0][];

        /// <summary>Spare working trays. Fewer empties means a harder level.</summary>
        public int EmptyTrayCount = 2;

        /// <summary>Shortest solution length, filled in by the solver. Used to set star thresholds.</summary>
        public int ParMoves;

        public BoardState BuildBoard()
        {
            List<Tray> trays = new List<Tray>();
            for (int i = 0; i < FilledTrays.Length; i++)
            {
                trays.Add(new Tray(TrayCapacity, FilledTrays[i]));
            }
            for (int i = 0; i < EmptyTrayCount; i++)
            {
                trays.Add(new Tray(TrayCapacity));
            }
            return new BoardState(trays.ToArray());
        }

        public int TotalTrayCount { get { return FilledTrays.Length + EmptyTrayCount; } }

        public int TotalPieceCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < FilledTrays.Length; i++) total += FilledTrays[i].Length;
                return total;
            }
        }
    }

    /// <summary>Terse constructors so hand-authored levels stay readable.</summary>
    public static class J
    {
        public static JewelryPiece Gold(JewelForm form) { return new JewelryPiece(JewelMaterial.Gold, form); }
        public static JewelryPiece Diamond(JewelForm form) { return new JewelryPiece(JewelMaterial.Diamond, form); }
        public static JewelryPiece Ruby(JewelForm form) { return new JewelryPiece(JewelMaterial.Ruby, form); }
        public static JewelryPiece Emerald(JewelForm form) { return new JewelryPiece(JewelMaterial.Emerald, form); }
        public static JewelryPiece Sapphire(JewelForm form) { return new JewelryPiece(JewelMaterial.Sapphire, form); }
        public static JewelryPiece Pearl(JewelForm form) { return new JewelryPiece(JewelMaterial.Pearl, form); }

        public const JewelForm Ring = JewelForm.Ring;
        public const JewelForm Necklace = JewelForm.Necklace;
        public const JewelForm Earrings = JewelForm.Earrings;
        public const JewelForm Bracelet = JewelForm.Bracelet;
        public const JewelForm Pendant = JewelForm.Pendant;
        public const JewelForm Crown = JewelForm.Crown;
    }
}
