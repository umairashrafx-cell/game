using NUnit.Framework;
using RoyalVault.Core;

namespace RoyalVault.Tests
{
    [TestFixture]
    public class PuzzleSessionTests
    {
        private static PuzzleSession NewSession(params Tray[] trays)
        {
            return new PuzzleSession(new BoardState(trays));
        }

        [Test]
        public void LegalMove_TransfersTheTopPieceOnly()
        {
            PuzzleSession session = NewSession(
                new Tray(4, J.Ruby(J.Ring), J.Gold(J.Crown)),
                new Tray(4));

            MoveResult result = session.TryMove(new Move(0, 1));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(J.Gold(J.Crown), result.Piece, "The top piece moves, not the bottom one.");
            Assert.AreEqual(1, session.Board[0].Count);
            Assert.AreEqual(J.Ruby(J.Ring), session.Board[0].Top);
            Assert.AreEqual(1, session.MoveCount);
        }

        [Test]
        public void IllegalMove_IsRejectedWithAReasonAndChangesNothing()
        {
            PuzzleSession session = NewSession(
                new Tray(4, J.Ruby(J.Ring), J.Ruby(J.Necklace)),  // locked to ruby
                new Tray(4, J.Gold(J.Crown)));

            MoveResult result = session.TryMove(new Move(1, 0));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MoveRejection.NoSharedAttribute, result.Rejection);
            Assert.AreEqual(2, session.Board[0].Count, "A rejected move must not mutate the board.");
            Assert.AreEqual(1, session.Board[1].Count);
            Assert.AreEqual(0, session.MoveCount, "A rejected move must not enter the history.");
        }

        [Test]
        public void CannotLiftFromASealedTray()
        {
            PuzzleSession session = NewSession(
                new Tray(2, J.Ruby(J.Ring), J.Ruby(J.Crown)),   // sealed
                new Tray(2));

            Assert.AreEqual(MoveRejection.SourceSealed, session.Validate(new Move(0, 1)));
        }

        [Test]
        public void CannotPlaceOntoASealedTray()
        {
            PuzzleSession session = NewSession(
                new Tray(2, J.Ruby(J.Ring), J.Ruby(J.Crown)),   // sealed, and would otherwise accept ruby
                new Tray(2, J.Ruby(J.Pendant)));

            Assert.AreEqual(MoveRejection.DestinationSealed, session.Validate(new Move(1, 0)));
        }

        [Test]
        public void MovingOntoAnEmptyTrayIsAlwaysAllowed()
        {
            PuzzleSession session = NewSession(
                new Tray(4, J.Ruby(J.Ring), J.Gold(J.Crown)),
                new Tray(4));

            Assert.AreEqual(MoveRejection.None, session.Validate(new Move(0, 1)));
        }

        [Test]
        public void Undo_RestoresThePreviousStateExactly()
        {
            PuzzleSession session = NewSession(
                new Tray(4, J.Ruby(J.Ring), J.Gold(J.Crown)),
                new Tray(4));

            string before = session.Board.GetCanonicalKey();
            session.TryMove(new Move(0, 1));
            Assert.AreNotEqual(before, session.Board.GetCanonicalKey());

            Assert.IsTrue(session.Undo());

            Assert.AreEqual(before, session.Board.GetCanonicalKey());
            Assert.AreEqual(0, session.MoveCount);
            Assert.IsFalse(session.CanUndo);
        }

        [Test]
        public void Undo_UnsealsATrayAndTheSealCountFollows()
        {
            PuzzleSession session = NewSession(
                new Tray(2, J.Ruby(J.Ring)),
                new Tray(2, J.Ruby(J.Crown)));

            MoveResult result = session.TryMove(new Move(1, 0));
            Assert.IsTrue(result.SealedDestination);
            Assert.IsTrue(session.Board[0].IsSealed);
            Assert.AreEqual(1, session.SealedTrayCount);

            session.Undo();

            Assert.IsFalse(session.Board[0].IsSealed);
            Assert.AreEqual(0, session.SealedTrayCount);
        }

        [Test]
        public void UndoOnAFreshBoard_IsHarmless()
        {
            PuzzleSession session = NewSession(new Tray(4, J.Ruby(J.Ring)), new Tray(4));
            Assert.IsFalse(session.Undo());
            Assert.AreEqual(0, session.MoveCount);
        }

        [Test]
        public void RoyalChain_CountsConsecutiveSeals()
        {
            PuzzleSession session = NewSession(
                new Tray(2, J.Ruby(J.Ring)),
                new Tray(2, J.Ruby(J.Crown)),
                new Tray(2, J.Gold(J.Ring)),
                new Tray(2, J.Gold(J.Crown)));

            Assert.AreEqual(1, session.TryMove(new Move(1, 0)).RoyalChain);
            Assert.AreEqual(2, session.TryMove(new Move(3, 2)).RoyalChain);
            Assert.AreEqual(2, session.BestRoyalChain);
        }

        [Test]
        public void RoyalChain_BreaksOnUndo_BecauseTheChainRewardsCommitment()
        {
            PuzzleSession session = NewSession(
                new Tray(2, J.Ruby(J.Ring)),
                new Tray(2, J.Ruby(J.Crown)),
                new Tray(2, J.Gold(J.Ring)),
                new Tray(2, J.Gold(J.Crown)));

            session.TryMove(new Move(1, 0));
            Assert.AreEqual(1, session.RoyalChain);

            session.Undo();
            Assert.AreEqual(0, session.RoyalChain);
            Assert.AreEqual(1, session.BestRoyalChain, "The best chain achieved is still remembered.");
        }

        [Test]
        public void RoyalChain_DoesNotAdvanceOnANonSealingMove()
        {
            PuzzleSession session = NewSession(
                new Tray(2, J.Ruby(J.Ring)),
                new Tray(2, J.Ruby(J.Crown)),
                new Tray(4, J.Gold(J.Ring), J.Gold(J.Crown)));

            session.TryMove(new Move(1, 0));
            Assert.AreEqual(1, session.RoyalChain);

            session.TryMove(new Move(2, 1));   // moves into the now-empty tray, seals nothing
            Assert.AreEqual(1, session.RoyalChain, "A move that seals nothing leaves the chain where it was.");
        }

        [Test]
        public void WinIsDetectedWhenEveryTrayIsEmptyOrSealed()
        {
            PuzzleSession session = NewSession(
                new Tray(2, J.Ruby(J.Ring)),
                new Tray(2, J.Ruby(J.Crown)),
                new Tray(2));

            Assert.IsFalse(session.IsSolved);

            bool raised = false;
            session.Solved += () => raised = true;

            MoveResult result = session.TryMove(new Move(1, 0));

            Assert.IsTrue(result.Solved);
            Assert.IsTrue(session.IsSolved);
            Assert.IsTrue(raised, "The Solved event drives the win sequence.");
        }

        [Test]
        public void DeadEndIsDetectedWhenNoLegalMoveRemains()
        {
            // Two full, mutually incompatible trays and no spare space.
            PuzzleSession session = NewSession(
                new Tray(2, J.Ruby(J.Ring), J.Gold(J.Crown)),
                new Tray(2, J.Emerald(J.Necklace), J.Pearl(J.Bracelet)));

            Assert.IsFalse(session.IsSolved);
            Assert.IsTrue(session.IsDeadEnd);
        }

        [Test]
        public void ShufflingPiecesBetweenEquivalentEmptyTraysIsNotOfferedAsAMove()
        {
            // Without this pruning the player could "move" forever without progressing,
            // and dead-end detection would never fire.
            BoardState board = new BoardState(
                new Tray(4, J.Ruby(J.Ring)),
                new Tray(4),
                new Tray(4));

            Assert.AreEqual(0, board.GetLegalMoves().Count);
            Assert.IsFalse(board.HasLegalMove);
        }
    }
}
