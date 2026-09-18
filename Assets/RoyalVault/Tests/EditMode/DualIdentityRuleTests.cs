using NUnit.Framework;
using RoyalVault.Core;

namespace RoyalVault.Tests
{
    /// <summary>
    /// The Dual Identity rule is the whole game. If these pass, the game is correct;
    /// if they fail, nothing else matters.
    /// </summary>
    [TestFixture]
    public class DualIdentityRuleTests
    {
        [Test]
        public void EmptyTray_AcceptsAnyPiece()
        {
            Tray tray = new Tray(4);
            Assert.IsTrue(tray.CanAccept(J.Ruby(J.Ring)));
            Assert.IsTrue(tray.CanAccept(J.Pearl(J.Crown)));
            Assert.IsFalse(tray.Identity.IsLocked, "An empty tray has committed to nothing.");
        }

        [Test]
        public void SinglePiece_LeavesBothAttributesOpen()
        {
            Tray tray = new Tray(4, J.Ruby(J.Ring));

            Assert.AreEqual(JewelMaterial.Ruby, tray.Identity.Material);
            Assert.AreEqual(JewelForm.Ring, tray.Identity.Form);
            Assert.IsFalse(tray.Identity.IsLocked, "One piece is not enough to commit the tray.");

            Assert.IsTrue(tray.CanAccept(J.Ruby(J.Crown)), "Shares the gem.");
            Assert.IsTrue(tray.CanAccept(J.Pearl(J.Ring)), "Shares the shape.");
            Assert.IsFalse(tray.CanAccept(J.Pearl(J.Crown)), "Shares neither.");
        }

        [Test]
        public void SecondPiece_LocksTrayToTheSharedMaterial()
        {
            Tray tray = new Tray(4, J.Ruby(J.Ring), J.Ruby(J.Necklace));

            Assert.IsTrue(tray.Identity.IsLocked);
            Assert.AreEqual(JewelMaterial.Ruby, tray.Identity.Material);
            Assert.AreEqual(JewelForm.None, tray.Identity.Form, "Shape is no longer a binder.");

            Assert.IsTrue(tray.CanAccept(J.Ruby(J.Crown)));
            Assert.IsFalse(tray.CanAccept(J.Gold(J.Ring)), "Tray is committed to ruby; a gold ring no longer fits.");
        }

        [Test]
        public void SecondPiece_LocksTrayToTheSharedForm()
        {
            Tray tray = new Tray(4, J.Ruby(J.Ring), J.Gold(J.Ring));

            Assert.IsTrue(tray.Identity.IsLocked);
            Assert.AreEqual(JewelForm.Ring, tray.Identity.Form);
            Assert.AreEqual(JewelMaterial.None, tray.Identity.Material);

            Assert.IsTrue(tray.CanAccept(J.Pearl(J.Ring)));
            Assert.IsFalse(tray.CanAccept(J.Ruby(J.Crown)), "Tray is committed to rings.");
        }

        [Test]
        public void IdenticalPieces_KeepBothAttributesOpen()
        {
            // Two identical pieces share BOTH attributes, so the tray has not had to choose yet.
            Tray tray = new Tray(4, J.Ruby(J.Ring), J.Ruby(J.Ring));

            Assert.IsFalse(tray.Identity.IsLocked, "Sharing both attributes commits to neither.");
            Assert.IsTrue(tray.CanAccept(J.Ruby(J.Crown)), "Can still become a ruby tray.");
            Assert.IsTrue(tray.CanAccept(J.Gold(J.Ring)), "Can still become a ring tray.");
            Assert.IsFalse(tray.CanAccept(J.Gold(J.Crown)));
        }

        [Test]
        public void FullTray_AcceptsNothingEvenWhenCompatible()
        {
            Tray tray = new Tray(2, J.Ruby(J.Ring), J.Ruby(J.Crown));
            Assert.IsTrue(tray.IsFull);
            Assert.IsFalse(tray.CanAccept(J.Ruby(J.Necklace)));
        }

        [Test]
        public void FullCoherentTray_IsSealed()
        {
            Tray tray = new Tray(3, J.Ruby(J.Ring), J.Ruby(J.Crown), J.Ruby(J.Pendant));
            Assert.IsTrue(tray.IsSealed, "A full tray sharing one gem is a completed set.");
        }

        [Test]
        public void FullMixedTray_IsNotSealed_SoStartingLayoutsStayPlayable()
        {
            // Levels open with full, deliberately jumbled trays. If "full" alone counted as sealed,
            // the player could never lift the first piece and every level would be unplayable.
            Tray tray = new Tray(3, J.Ruby(J.Ring), J.Gold(J.Crown), J.Emerald(J.Pendant));

            Assert.IsTrue(tray.IsFull);
            Assert.IsFalse(tray.Identity.IsCoherent);
            Assert.IsFalse(tray.IsSealed);
        }
    }
}
