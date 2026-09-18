using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RoyalVault.Game;

namespace RoyalVault.EditorTools
{
    /// <summary>
    /// Generates the game scene from code and registers it in the build settings.
    ///
    /// The scene deliberately contains almost nothing — a camera and a single bootstrap object.
    /// Everything else is built at runtime. That keeps scene YAML out of the repository, where it
    /// merges badly and hides layout decisions that belong in reviewable code.
    /// </summary>
    public static class SceneBuilder
    {
        private const string ScenesFolder = "Assets/RoyalVault/Scenes";
        private const string GameScenePath = ScenesFolder + "/Game.unity";

        [MenuItem("Royal Vault/Rebuild Game Scene")]
        public static void BuildGameScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RoyalPalette.VaultBackground;
            camera.orthographic = true;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            new GameObject("GameBootstrap", typeof(GameBootstrap));

            if (!Directory.Exists(ScenesFolder)) Directory.CreateDirectory(ScenesFolder);
            EditorSceneManager.SaveScene(scene, GameScenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(GameScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Royal Vault: built " + GameScenePath + " and set it as the startup scene.");
        }

        /// <summary>Entry point for headless generation via -executeMethod.</summary>
        public static void BuildGameSceneHeadless()
        {
            BuildGameScene();
            EditorApplication.Exit(0);
        }
    }
}
