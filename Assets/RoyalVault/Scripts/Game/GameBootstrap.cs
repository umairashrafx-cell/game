using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RoyalVault.Core;
using RoyalVault.Game.Visual3D;

namespace RoyalVault.Game
{
    /// <summary>
    /// Composition root. Builds the 3D vault, the board, and the flat HUD that floats over it,
    /// then drives the level loop.
    ///
    /// The board is real geometry; the HUD stays a screen-space canvas because text and buttons
    /// have no business being in world space on a phone. This is the only class that knows how
    /// the two halves fit together.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private const int DesignWidth = 1080;
        private const int DesignHeight = 1920;

        private List<LevelDefinition> _levels;
        private int _levelIndex;

        private Board3D _board;
        private Camera _camera;
        private Text _levelLabel;
        private Text _movesLabel;
        private Text _chainLabel;
        private RectTransform _winBanner;
        private Text _winText;
        private Button _undoButton;
        private RectTransform _deadEndBanner;

        private void Start()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;

            _levels = LevelLibrary.BuildPhaseOneLevels();

            DevScreenshot.AttachIfRequested();
            BuildWorld();
            BuildInterface();
            LoadLevel(0);
        }

        private void BuildWorld()
        {
            GameObject world = new GameObject("VaultWorld");
            _camera = VaultEnvironment.Build(world.transform);

            GameObject boardRoot = new GameObject("Board");
            boardRoot.transform.SetParent(world.transform, false);

            _board = boardRoot.AddComponent<Board3D>();
            _board.MovePlayed += OnMovePlayed;
            _board.LevelSolved += OnLevelSolved;
        }

        // ---------------------------------------------------------------- interface construction

        private void BuildInterface()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            GameObject canvasObject = new GameObject("RoyalVaultCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(DesignWidth, DesignHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform safeArea = UiFactory.Panel("SafeArea", canvasObject.transform);
            UiFactory.Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            BuildHud(safeArea);
            BuildBottomBar(safeArea);
            BuildWinBanner(safeArea);
            BuildDeadEndBanner(safeArea);
        }

        private void BuildHud(RectTransform parent)
        {
            RectTransform hud = UiFactory.Panel("Hud", parent);
            UiFactory.Anchor(hud, new Vector2(0f, 1f), new Vector2(1f, 1f),
                             new Vector2(48f, -240f), new Vector2(-48f, -52f));

            _levelLabel = UiFactory.Label("LevelName", hud, "", 54, RoyalPalette.Ivory, TextAnchor.UpperLeft);
            UiFactory.Stretch((RectTransform)_levelLabel.transform);

            _movesLabel = UiFactory.Label("Moves", hud, "", 40, RoyalPalette.Champagne, TextAnchor.LowerLeft);
            UiFactory.Stretch((RectTransform)_movesLabel.transform);

            _chainLabel = UiFactory.Label("Chain", hud, "", 48, RoyalPalette.Gold, TextAnchor.LowerRight);
            UiFactory.Stretch((RectTransform)_chainLabel.transform);
        }

        private void BuildBottomBar(RectTransform parent)
        {
            RectTransform bar = UiFactory.Panel("BottomBar", parent);
            UiFactory.Anchor(bar, new Vector2(0f, 0f), new Vector2(1f, 0f),
                             new Vector2(40f, 44f), new Vector2(-40f, 174f));

            _undoButton = UiFactory.TextButton("Undo", bar, "UNDO", new Vector2(300f, 118f),
                                               RoyalPalette.VaultPanel, RoyalPalette.Ivory);
            RectTransform undoRect = (RectTransform)_undoButton.transform;
            undoRect.anchorMin = new Vector2(0f, 0.5f);
            undoRect.anchorMax = new Vector2(0f, 0.5f);
            undoRect.anchoredPosition = new Vector2(160f, 0f);
            _undoButton.onClick.AddListener(OnUndoPressed);

            Button restart = UiFactory.TextButton("Restart", bar, "RESTART", new Vector2(320f, 118f),
                                                  RoyalPalette.VaultPanel, RoyalPalette.Ivory);
            RectTransform restartRect = (RectTransform)restart.transform;
            restartRect.anchorMin = new Vector2(1f, 0.5f);
            restartRect.anchorMax = new Vector2(1f, 0.5f);
            restartRect.anchoredPosition = new Vector2(-170f, 0f);
            restart.onClick.AddListener(() => LoadLevel(_levelIndex));
        }

        private void BuildWinBanner(RectTransform parent)
        {
            _winBanner = UiFactory.Panel("WinBanner", parent);
            UiFactory.Anchor(_winBanner, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                             new Vector2(70f, -210f), new Vector2(-70f, 210f));

            Image panel = UiFactory.ImagePanel("Panel", _winBanner, RoyalPalette.VaultPanel, 40);
            UiFactory.Stretch((RectTransform)panel.transform);

            _winText = UiFactory.Label("WinText", _winBanner, "", 60, RoyalPalette.Ivory);
            RectTransform textRect = (RectTransform)_winText.transform;
            UiFactory.Anchor(textRect, new Vector2(0f, 0.5f), new Vector2(1f, 1f),
                             new Vector2(30f, 0f), new Vector2(-30f, -30f));

            Button next = UiFactory.TextButton("Next", _winBanner, "CONTINUE", new Vector2(380f, 120f),
                                               RoyalPalette.Gold, RoyalPalette.VaultBackground);
            RectTransform nextRect = (RectTransform)next.transform;
            nextRect.anchorMin = new Vector2(0.5f, 0f);
            nextRect.anchorMax = new Vector2(0.5f, 0f);
            nextRect.anchoredPosition = new Vector2(0f, 100f);
            next.onClick.AddListener(OnContinuePressed);

            _winBanner.gameObject.SetActive(false);
        }

        /// <summary>
        /// Shown when no legal move remains. Offers one obvious action rather than explaining the
        /// rules — a stranded player wants out of the hole, not a lecture.
        /// </summary>
        private void BuildDeadEndBanner(RectTransform parent)
        {
            _deadEndBanner = UiFactory.Panel("DeadEndBanner", parent);
            UiFactory.Anchor(_deadEndBanner, new Vector2(0f, 0f), new Vector2(1f, 0f),
                             new Vector2(60f, 200f), new Vector2(-60f, 470f));

            Image panel = UiFactory.ImagePanel("Panel", _deadEndBanner, RoyalPalette.VaultPanel, 36);
            UiFactory.Stretch((RectTransform)panel.transform);

            Text message = UiFactory.Label("Message", _deadEndBanner, "No moves left", 46, RoyalPalette.Ivory);
            RectTransform messageRect = (RectTransform)message.transform;
            UiFactory.Anchor(messageRect, new Vector2(0f, 0.5f), new Vector2(1f, 1f),
                             new Vector2(24f, 0f), new Vector2(-24f, -18f));

            Button takeBack = UiFactory.TextButton("TakeBack", _deadEndBanner, "TAKE BACK MOVE",
                                                   new Vector2(460f, 110f),
                                                   RoyalPalette.Gold, RoyalPalette.VaultBackground);
            RectTransform takeBackRect = (RectTransform)takeBack.transform;
            takeBackRect.anchorMin = new Vector2(0.5f, 0f);
            takeBackRect.anchorMax = new Vector2(0.5f, 0f);
            takeBackRect.anchoredPosition = new Vector2(0f, 78f);
            takeBack.onClick.AddListener(OnUndoPressed);

            _deadEndBanner.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------------- level flow

        private void LoadLevel(int index)
        {
            _levelIndex = Mathf.Clamp(index, 0, _levels.Count - 1);
            LevelDefinition level = _levels[_levelIndex];

            PuzzleSession session = new PuzzleSession(level.BuildBoard());

            _winBanner.gameObject.SetActive(false);
            _deadEndBanner.gameObject.SetActive(false);
            _levelLabel.text = "LEVEL " + level.Id + "   ·   " + level.DisplayName.ToUpperInvariant();

            _board.Bind(session, _camera);
            UpdateHud();
        }

        private void OnMovePlayed(MoveResult result)
        {
            UpdateHud();
            _deadEndBanner.gameObject.SetActive(result.DeadEnd);
        }

        private void OnLevelSolved()
        {
            StartCoroutine(ShowWin());
        }

        private IEnumerator ShowWin()
        {
            yield return new WaitForSeconds(0.40f);

            PuzzleSession session = _board.Session;
            bool lastLevel = _levelIndex >= _levels.Count - 1;

            string headline = lastLevel ? "VAULT SECTION RESTORED" : "COLLECTION COMPLETE";
            _winText.text = headline + "\n\n" + session.MoveCount + " moves"
                            + (session.BestRoyalChain > 1 ? "   ·   Royal Chain x" + session.BestRoyalChain : "");

            _winBanner.gameObject.SetActive(true);
        }

        private void OnContinuePressed()
        {
            _winBanner.gameObject.SetActive(false);
            LoadLevel(_levelIndex >= _levels.Count - 1 ? 0 : _levelIndex + 1);
        }

        private void OnUndoPressed()
        {
            if (_board.IsAnimating || _board.Session == null) return;
            if (!_board.Session.Undo()) return;

            HapticService.Play(HapticStrength.Light);
            _deadEndBanner.gameObject.SetActive(false);
            _board.ResyncFromModel();
            UpdateHud();
        }

        private void UpdateHud()
        {
            PuzzleSession session = _board.Session;
            if (session == null) return;

            _movesLabel.text = session.MoveCount + " moves";
            _chainLabel.text = session.RoyalChain > 1 ? "ROYAL CHAIN x" + session.RoyalChain : "";
            _undoButton.interactable = session.CanUndo;
        }
    }
}
