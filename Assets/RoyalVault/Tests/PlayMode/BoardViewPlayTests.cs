using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RoyalVault.Core;
using RoyalVault.Game;

namespace RoyalVault.Tests.PlayMode
{
    /// <summary>
    /// Proves the presentation layer actually assembles and stays in step with the simulation.
    ///
    /// The point is not to judge how the game looks — that needs human eyes — but to catch the
    /// failures that would otherwise only appear on a device: the board not building, the views
    /// drifting out of sync with the model, or a rejected move corrupting the display.
    /// </summary>
    public class BoardViewPlayTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
        }

        private IEnumerator BuildBoard(LevelDefinition level, BoardView[] result)
        {
            _root = new GameObject("TestCanvas", typeof(Canvas));
            Canvas canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject area = new GameObject("BoardArea", typeof(RectTransform));
            area.transform.SetParent(_root.transform, false);
            RectTransform rect = (RectTransform)area.transform;
            rect.sizeDelta = new Vector2(1000f, 1400f);

            BoardView board = area.AddComponent<BoardView>();
            yield return null;

            board.Bind(new PuzzleSession(level.BuildBoard()), rect);
            yield return null;

            result[0] = board;
        }

        [UnityTest]
        public IEnumerator BoardBuildsATrayAndAPieceForEveryOneInTheModel()
        {
            LevelDefinition level = LevelLibrary.Level01();
            BoardView[] holder = new BoardView[1];
            yield return BuildBoard(level, holder);
            BoardView board = holder[0];

            Assert.IsNotNull(board.Session);

            int trayViews = board.GetComponentsInChildren<TrayView>(true).Length;
            Assert.AreEqual(level.TotalTrayCount, trayViews, "Every tray in the model needs a view.");

            int pieceViews = board.GetComponentsInChildren<PieceView>(true).Length;
            Assert.AreEqual(level.TotalPieceCount, pieceViews, "Every piece in the model needs a view.");
        }

        [UnityTest]
        public IEnumerator TraysDoNotOverlapAndStayInsideTheBoardArea()
        {
            // Guards the responsive layout: on a small screen the computed slot size must still
            // produce trays that fit, or the board silently spills off the edge of the phone.
            LevelDefinition level = LevelLibrary.Level04();   // the widest Phase 1 board
            BoardView[] holder = new BoardView[1];
            yield return BuildBoard(level, holder);
            BoardView board = holder[0];

            RectTransform area = (RectTransform)board.transform;
            TrayView[] trays = board.GetComponentsInChildren<TrayView>(true);

            foreach (TrayView tray in trays)
            {
                Vector2 half = tray.Rect.sizeDelta * 0.5f;
                Vector2 centre = tray.Rect.anchoredPosition;

                Assert.LessOrEqual(Mathf.Abs(centre.x) + half.x, area.rect.width * 0.5f + 1f,
                    "Tray " + tray.Index + " overflows the board horizontally.");
                Assert.LessOrEqual(Mathf.Abs(centre.y) + half.y, area.rect.height * 0.5f + 1f,
                    "Tray " + tray.Index + " overflows the board vertically.");
            }
        }

        [UnityTest]
        public IEnumerator UndoResyncRebuildsTheViewToMatchTheModel()
        {
            LevelDefinition level = LevelLibrary.Level01();
            BoardView[] holder = new BoardView[1];
            yield return BuildBoard(level, holder);
            BoardView board = holder[0];

            PuzzleSession session = board.Session;

            // Drive the model directly, then force the view to catch up.
            MoveResult result = session.TryMove(new Move(0, 2));
            Assert.IsTrue(result.Success);

            board.ResyncFromModel();
            yield return null;

            TrayView[] trays = board.GetComponentsInChildren<TrayView>(true);
            for (int i = 0; i < trays.Length; i++)
            {
                Assert.AreEqual(session.Board[trays[i].Index].Count, trays[i].Pieces.Count,
                    "Tray " + trays[i].Index + " view is out of step with the model.");
            }

            session.Undo();
            board.ResyncFromModel();
            yield return null;

            trays = board.GetComponentsInChildren<TrayView>(true);
            for (int i = 0; i < trays.Length; i++)
            {
                Assert.AreEqual(session.Board[trays[i].Index].Count, trays[i].Pieces.Count,
                    "Tray " + trays[i].Index + " did not return to its pre-move state.");
            }
        }

        [UnityTest]
        public IEnumerator EveryFormProducesADistinctSilhouette()
        {
            // The accessibility promise: shape alone must identify a piece. If two forms rasterise
            // to the same pixels, colour becomes load-bearing and colour-blind players lose.
            JewelForm[] forms =
            {
                JewelForm.Ring, JewelForm.Necklace, JewelForm.Earrings,
                JewelForm.Bracelet, JewelForm.Pendant, JewelForm.Crown
            };

            Color32[][] rasters = new Color32[forms.Length][];
            for (int i = 0; i < forms.Length; i++)
            {
                rasters[i] = ProceduralSprites.RasteriseForm(forms[i]);
            }
            yield return null;

            for (int a = 0; a < forms.Length; a++)
            {
                for (int b = a + 1; b < forms.Length; b++)
                {
                    int differing = 0;
                    for (int p = 0; p < rasters[a].Length; p++)
                    {
                        if (Mathf.Abs(rasters[a][p].a - rasters[b][p].a) > 40) differing++;
                    }

                    float ratio = differing / (float)rasters[a].Length;
                    Assert.Greater(ratio, 0.05f,
                        forms[a] + " and " + forms[b] + " look too similar (" +
                        (ratio * 100f).ToString("F1") + "% of pixels differ).");
                }
            }
        }
    }
}
