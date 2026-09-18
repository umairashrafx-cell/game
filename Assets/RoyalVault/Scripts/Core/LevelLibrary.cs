using System.Collections.Generic;

namespace RoyalVault.Core
{
    /// <summary>
    /// The Phase 1 vertical-slice levels, hand-designed rather than generated.
    ///
    /// Design lever that drives the whole early curve: a tray is completed by <c>TrayCapacity</c>
    /// pieces sharing one attribute, so an attribute value occurring FEWER than TrayCapacity times
    /// can never complete a tray. Levels 1-4 therefore keep every Form below capacity, which makes
    /// material the only way to finish a tray and reduces the game to a gentle "match the gem"
    /// sort. Level 5 raises one Form to exactly capacity, and the real Dual Identity choice —
    /// match the gem or match the shape — appears for the first time.
    ///
    /// The player is never told this. They discover it.
    /// </summary>
    public static class LevelLibrary
    {
        public static List<LevelDefinition> BuildPhaseOneLevels()
        {
            return new List<LevelDefinition>
            {
                Level01(), Level02(), Level03(), Level04(), Level05()
            };
        }

        /// <summary>
        /// L1 — "The First Drawer". Two gems, six slots, and every Form appearing EXACTLY ONCE.
        ///
        /// That last detail is what makes this level unfailable, and it is worth stating plainly:
        /// if no two pieces share a Form, then two pieces can only ever be placed together when
        /// they share a GEM. A wrong pairing is not punished — it is simply refused. So every
        /// move the player is allowed to make is a correct one, and the level cannot be played
        /// into a dead end.
        ///
        /// The earlier version used four slots and four forms, which meant a player could pair a
        /// Gold Ring with a Diamond Ring, lock the tray to "rings", and poison it forever because
        /// only two rings existed. Playtesting hit exactly that dead end on the very first level.
        /// A tutorial level must not be able to punish the player for experimenting.
        /// </summary>
        public static LevelDefinition Level01()
        {
            return new LevelDefinition
            {
                Id = 1,
                World = 1,
                DisplayName = "The First Drawer",
                TrayCapacity = 3,
                EmptyTrayCount = 2,
                FilledTrays = new[]
                {
                    new[] { J.Gold(J.Ring),       J.Diamond(J.Bracelet), J.Gold(J.Necklace) },
                    new[] { J.Diamond(J.Pendant), J.Gold(J.Earrings),    J.Diamond(J.Crown) }
                }
            };
        }

        /// <summary>
        /// L2 — "A Third Gem". Ruby joins. Layout chosen by the authoring tool from 40 candidates
        /// as a deliberately gentle one (search resistance 20).
        /// </summary>
        public static LevelDefinition Level02()
        {
            return new LevelDefinition
            {
                Id = 2,
                World = 1,
                DisplayName = "A Third Gem",
                TrayCapacity = 4,
                EmptyTrayCount = 2,
                FilledTrays = new[]
                {
                    new[] { J.Ruby(J.Crown),     J.Gold(J.Bracelet),  J.Diamond(J.Ring),    J.Diamond(J.Necklace) },
                    new[] { J.Gold(J.Earrings),  J.Gold(J.Necklace),  J.Ruby(J.Bracelet),   J.Ruby(J.Earrings) },
                    new[] { J.Diamond(J.Pendant), J.Diamond(J.Crown), J.Ruby(J.Pendant),    J.Gold(J.Ring) }
                }
            };
        }

        /// <summary>
        /// L3 — "Tight Fit". The same gems as L2, jumbled far more aggressively (resistance 39).
        ///
        /// Difficulty comes from the layout, not from taking a spare tray away. Dropping to one
        /// spare tray was measured and rejected: it makes levels solvable only about half the
        /// time by luck of the draw, which reads to a player as the game cheating.
        /// </summary>
        public static LevelDefinition Level03()
        {
            return new LevelDefinition
            {
                Id = 3,
                World = 1,
                DisplayName = "Tight Fit",
                TrayCapacity = 4,
                EmptyTrayCount = 2,
                FilledTrays = new[]
                {
                    new[] { J.Gold(J.Necklace),    J.Ruby(J.Bracelet),  J.Gold(J.Ring),     J.Diamond(J.Pendant) },
                    new[] { J.Diamond(J.Necklace), J.Ruby(J.Earrings),  J.Diamond(J.Crown), J.Gold(J.Earrings) },
                    new[] { J.Ruby(J.Crown),       J.Gold(J.Bracelet),  J.Ruby(J.Pendant),  J.Diamond(J.Ring) }
                }
            };
        }

        /// <summary>
        /// L4 — "The Emerald Arrives". Four gems, sixteen pieces, three spare trays so the jump in
        /// board size does not also become a jump in pressure (resistance 944).
        /// Every form still occurs at most three times, so gem-sets remain the only way to finish.
        /// </summary>
        public static LevelDefinition Level04()
        {
            return new LevelDefinition
            {
                Id = 4,
                World = 1,
                DisplayName = "The Emerald Arrives",
                TrayCapacity = 4,
                EmptyTrayCount = 3,
                FilledTrays = new[]
                {
                    new[] { J.Emerald(J.Ring),     J.Diamond(J.Necklace), J.Ruby(J.Earrings), J.Gold(J.Ring) },
                    new[] { J.Emerald(J.Bracelet), J.Ruby(J.Pendant),     J.Diamond(J.Ring),  J.Emerald(J.Earrings) },
                    new[] { J.Emerald(J.Necklace), J.Ruby(J.Bracelet),    J.Ruby(J.Crown),    J.Gold(J.Bracelet) },
                    new[] { J.Diamond(J.Crown),    J.Diamond(J.Pendant),  J.Gold(J.Necklace), J.Gold(J.Earrings) }
                }
            };
        }

        /// <summary>
        /// L5 — "The Ring Gallery". Exactly four rings now exist on the board, so for the first
        /// time a tray can be completed by SHAPE instead of by gem, and the spare trays drop back
        /// to two (resistance 1325).
        ///
        /// This is the level that teaches the real mechanic, and it does it without a single line
        /// of tutorial text: the player reaches for a gem-set, notices four rings, and the second
        /// half of the game opens up on its own.
        /// </summary>
        public static LevelDefinition Level05()
        {
            return new LevelDefinition
            {
                Id = 5,
                World = 1,
                DisplayName = "The Ring Gallery",
                TrayCapacity = 4,
                EmptyTrayCount = 2,
                FilledTrays = new[]
                {
                    new[] { J.Gold(J.Earrings),  J.Emerald(J.Ring),     J.Ruby(J.Pendant),     J.Diamond(J.Necklace) },
                    new[] { J.Gold(J.Necklace),  J.Emerald(J.Earrings), J.Ruby(J.Ring),        J.Gold(J.Bracelet) },
                    new[] { J.Ruby(J.Earrings),  J.Emerald(J.Crown),    J.Emerald(J.Necklace), J.Ruby(J.Bracelet) },
                    new[] { J.Diamond(J.Crown),  J.Diamond(J.Ring),     J.Gold(J.Ring),        J.Diamond(J.Pendant) }
                }
            };
        }
    }
}
