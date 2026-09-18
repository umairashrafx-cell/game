using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

#if URP_PRESENT
using UnityEngine.Rendering.Universal;
#endif

namespace RoyalVault.EditorTools
{
    /// <summary>
    /// Creates the Universal Render Pipeline asset and makes it the active pipeline.
    ///
    /// Done in code rather than by hand because the settings that matter for this game are
    /// opinionated and easy to get wrong in the inspector: HDR on (so bloom has headroom above
    /// white), shadows off (nothing here casts them), and MSAA at 4x, which is cheap on mobile
    /// GPUs and does more for the look of thin gold edges than any amount of post-processing.
    /// </summary>
    public static class RenderPipelineSetup
    {
        private const string SettingsFolder = "Assets/RoyalVault/Settings";
        private const string PipelineAssetPath = SettingsFolder + "/RoyalVaultURP.asset";

        [MenuItem("Royal Vault/Setup Render Pipeline")]
        public static void Setup()
        {
#if URP_PRESENT
            if (!Directory.Exists(SettingsFolder)) Directory.CreateDirectory(SettingsFolder);

            UniversalRenderPipelineAsset pipeline =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);

            if (pipeline == null)
            {
                UniversalRendererData rendererData =
                    ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, SettingsFolder + "/RoyalVaultRenderer.asset");

                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, PipelineAssetPath);
            }

            // HDR is what lets highlights exceed white so bloom has something to find.
            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 4;
            pipeline.shadowDistance = 0f;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;

            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;

            EnsureAlwaysIncludedShaders(
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Unlit");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Royal Vault: URP configured and set as the active render pipeline.");
#else
            Debug.LogError("Royal Vault: URP package is not installed — cannot configure the pipeline.");
#endif
        }

        /// <summary>
        /// Forces shaders into the build even though nothing in the project references them.
        ///
        /// Royal Vault creates every material at runtime via <c>Shader.Find</c>, so from the
        /// build pipeline's point of view URP's Lit shader is unused and gets stripped. In the
        /// editor everything looks correct; in a player <c>Shader.Find</c> returns null, the
        /// material falls back to an unlit shader, and the jewelry renders as flat silhouettes
        /// with no lighting at all. This is the fix, and it is required for any runtime-generated
        /// material.
        /// </summary>
        private static void EnsureAlwaysIncludedShaders(params string[] shaderNames)
        {
            UnityEngine.Object settings =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
            if (settings == null)
            {
                UnityEngine.Object[] all =
                    AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
                if (all == null || all.Length == 0)
                {
                    Debug.LogWarning("Royal Vault: could not open GraphicsSettings to register shaders.");
                    return;
                }
                settings = all[0];
            }

            SerializedObject serialized = new SerializedObject(settings);
            SerializedProperty list = serialized.FindProperty("m_AlwaysIncludedShaders");
            if (list == null)
            {
                Debug.LogWarning("Royal Vault: m_AlwaysIncludedShaders not found.");
                return;
            }

            foreach (string name in shaderNames)
            {
                Shader shader = Shader.Find(name);
                if (shader == null)
                {
                    Debug.LogWarning("Royal Vault: shader not found: " + name);
                    continue;
                }

                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    {
                        present = true;
                        break;
                    }
                }
                if (present) continue;

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                Debug.Log("Royal Vault: registered always-included shader " + name);
            }

            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Entry point for headless setup via -executeMethod.</summary>
        public static void SetupHeadless()
        {
            Setup();
            EditorApplication.Exit(0);
        }
    }
}
