using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RoyalVault.Core;
using RoyalVault.Game.Visual3D;

namespace RoyalVault.Tests.PlayMode
{
    /// <summary>
    /// Proves the 3D presentation layer assembles and stays in step with the simulation.
    ///
    /// The point is not to judge how the game looks — that needs human eyes — but to catch the
    /// failures that would otherwise only show up on a device: the board not building, the views
    /// drifting out of sync with the model, or pieces spilling outside their trays.
    /// </summary>
    public class BoardViewPlayTests
    {
        private GameObject _root;
        private Camera _camera;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
        }

        private IEnumerator BuildBoard(LevelDefinition level, Board3D[] result)
        {
            _root = new GameObject("TestWorld");

            GameObject cameraObject = new GameObject("TestCamera", typeof(Camera));
            cameraObject.transform.SetParent(_root.transform, false);
            _camera = cameraObject.GetComponent<Camera>();
            _camera.orthographic = false;
            _camera.fieldOfView = 44f;
            _camera.transform.position = new Vector3(0f, 0f, -11f);

            GameObject boardObject = new GameObject("TestBoard");
            boardObject.transform.SetParent(_root.transform, false);

            Board3D board = boardObject.AddComponent<Board3D>();
            yield return null;

            board.Bind(new PuzzleSession(level.BuildBoard()), _camera);
            yield return null;

            result[0] = board;
        }

        [UnityTest]
        public IEnumerator BoardBuildsATrayAndAPieceForEveryOneInTheModel()
        {
            LevelDefinition level = LevelLibrary.Level01();
            Board3D[] holder = new Board3D[1];
            yield return BuildBoard(level, holder);
            Board3D board = holder[0];

            Assert.IsNotNull(board.Session);

            int trays = board.GetComponentsInChildren<Tray3D>(true).Length;
            Assert.AreEqual(level.TotalTrayCount, trays, "Every tray in the model needs a view.");

            int pieces = board.GetComponentsInChildren<JewelryPieceVisual>(true).Length;
            Assert.AreEqual(level.TotalPieceCount, pieces, "Every piece in the model needs a view.");
        }

        [UnityTest]
        public IEnumerator PiecesStayInsideTheirTray()
        {
            // Guards the piece-to-tray scale relationship. Pieces overflowing the frame was a real
            // regression once, and it is invisible to every other test.
            LevelDefinition level = LevelLibrary.Level04();   // the widest Phase 1 board
            Board3D[] holder = new Board3D[1];
            yield return BuildBoard(level, holder);
            Board3D board = holder[0];

            foreach (Tray3D tray in board.GetComponentsInChildren<Tray3D>(true))
            {
                float halfWidth = tray.SlotSize * 1.40f * 0.5f;

                foreach (GameObject piece in tray.Pieces)
                {
                    Renderer[] renderers = piece.GetComponentsInChildren<Renderer>();
                    Assert.Greater(renderers.Length, 0, "A piece rendered nothing at all.");

                    Bounds bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                    float offset = Mathf.Abs(bounds.center.x - tray.transform.position.x);
                    Assert.LessOrEqual(offset + bounds.extents.x, halfWidth * 1.08f,
                        "A piece overflows its tray horizontally.");
                }
            }
        }

        [UnityTest]
        public IEnumerator UndoResyncRebuildsTheViewToMatchTheModel()
        {
            LevelDefinition level = LevelLibrary.Level01();
            Board3D[] holder = new Board3D[1];
            yield return BuildBoard(level, holder);
            Board3D board = holder[0];

            PuzzleSession session = board.Session;

            MoveResult result = session.TryMove(new Move(0, 2));
            Assert.IsTrue(result.Success);

            board.ResyncFromModel();
            yield return null;

            foreach (Tray3D tray in board.GetComponentsInChildren<Tray3D>(true))
            {
                Assert.AreEqual(session.Board[tray.Index].Count, tray.Pieces.Count,
                    "Tray " + tray.Index + " view is out of step with the model.");
            }

            session.Undo();
            board.ResyncFromModel();
            yield return null;

            foreach (Tray3D tray in board.GetComponentsInChildren<Tray3D>(true))
            {
                Assert.AreEqual(session.Board[tray.Index].Count, tray.Pieces.Count,
                    "Tray " + tray.Index + " did not return to its pre-move state.");
            }
        }

        [UnityTest]
        public IEnumerator EveryFormBuildsDistinctGeometry()
        {
            // The accessibility promise: shape alone must identify a piece, so colour is never
            // the only signal. Compared by mesh bounds, which is a coarse but honest proxy for
            // "these two silhouettes are not the same".
            JewelForm[] forms =
            {
                JewelForm.Ring, JewelForm.Necklace, JewelForm.Earrings,
                JewelForm.Bracelet, JewelForm.Pendant, JewelForm.Crown
            };

            Vector3[] sizes = new Vector3[forms.Length];
            GameObject holder = new GameObject("FormProbe");

            for (int i = 0; i < forms.Length; i++)
            {
                GameObject piece = JewelryPieceBuilder.Build(
                    new JewelryPiece(JewelMaterial.Ruby, forms[i]), holder.transform);

                Renderer[] renderers = piece.GetComponentsInChildren<Renderer>();
                Assert.Greater(renderers.Length, 0, forms[i] + " produced no geometry.");

                Bounds bounds = renderers[0].bounds;
                for (int r = 1; r < renderers.Length; r++) bounds.Encapsulate(renderers[r].bounds);
                sizes[i] = bounds.size;
            }

            yield return null;

            for (int a = 0; a < forms.Length; a++)
            {
                for (int b = a + 1; b < forms.Length; b++)
                {
                    float difference = (sizes[a] - sizes[b]).magnitude;
                    Assert.Greater(difference, 0.02f,
                        forms[a] + " and " + forms[b] + " have near-identical proportions.");
                }
            }

            Object.Destroy(holder);
        }
    }
}
