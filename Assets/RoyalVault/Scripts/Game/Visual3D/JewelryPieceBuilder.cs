using UnityEngine;
using RoyalVault.Core;

namespace RoyalVault.Game.Visual3D
{
    /// <summary>
    /// Assembles a piece of jewelry from primitives.
    ///
    /// Two rules govern every shape here, and they exist for gameplay rather than decoration:
    ///
    /// * **Silhouette carries the Form.** A ring, a crown and a pair of earrings must be
    ///   distinguishable as black shapes, because Form is half the matching rule.
    /// * **The stone carries the Material.** The stone is always the largest, brightest,
    ///   most saturated element, because Material is the other half.
    ///
    /// Pieces are built facing +Z so they present their silhouette to the camera. Torus meshes
    /// are generated in the XY plane, so a ring already faces the player with no rotation.
    /// </summary>
    public static class JewelryPieceBuilder
    {
        /// <summary>
        /// The angle every piece is displayed at. This single value does more for the look of the
        /// game than any other: viewed square-on, a ring lying in the XY plane is a flat disc and
        /// the board reads as 2D icons no matter how good the materials are. Turning it a little
        /// off-axis reveals the thickness of the band and lets highlights travel across the metal.
        ///
        /// Kept modest on purpose — the silhouette still has to identify the Form at a glance,
        /// because that is half the matching rule.
        /// </summary>
        public static readonly Quaternion PresentationRotation = Quaternion.Euler(-14f, 26f, 0f);

        public static GameObject Build(JewelryPiece piece, Transform parent)
        {
            GameObject root = new GameObject("Jewel_" + piece.Material + "_" + piece.Form);
            if (parent != null) root.transform.SetParent(parent, false);

            Material stone = JewelryMaterialFactory.Stone(piece.Material);
            Material setting = JewelryMaterialFactory.Setting(piece.Material);

            switch (piece.Form)
            {
                case JewelForm.Ring:      BuildRing(root.transform, stone, setting); break;
                case JewelForm.Necklace:  BuildNecklace(root.transform, stone, setting); break;
                case JewelForm.Earrings:  BuildEarrings(root.transform, stone, setting); break;
                case JewelForm.Bracelet:  BuildBracelet(root.transform, stone, setting); break;
                case JewelForm.Pendant:   BuildPendant(root.transform, stone, setting); break;
                case JewelForm.Crown:     BuildCrown(root.transform, stone, setting); break;
                default:                  BuildPendant(root.transform, stone, setting); break;
            }

            return root;
        }

        // --------------------------------------------------------------------------- helpers

        private static GameObject Part(Transform parent, string name, Mesh mesh, Material material,
                                       Vector3 position, Quaternion rotation, Vector3 scale)
        {
            GameObject go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;

            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            // Small props lit by a couple of lights — shadow casting costs more than it adds.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;

            return go;
        }

        /// <summary>Faces a gem's table toward the camera (+Z) instead of straight up.</summary>
        private static readonly Quaternion GemFacing = Quaternion.Euler(-90f, 0f, 0f);

        // ----------------------------------------------------------------------------- forms

        private static void BuildRing(Transform root, Material stone, Material setting)
        {
            Part(root, "Band", JewelryMeshFactory.Torus(0.32f, 0.062f), setting,
                 Vector3.zero, Quaternion.identity, Vector3.one);

            Part(root, "Stone", JewelryMeshFactory.BrilliantGem(12), stone,
                 new Vector3(0f, 0.34f, 0.02f), GemFacing, Vector3.one * 0.40f);

            // Two tiny shoulder stones make the setting read as jewelry rather than a washer.
            Part(root, "AccentL", JewelryMeshFactory.BrilliantGem(8), stone,
                 new Vector3(-0.19f, 0.27f, 0.02f), GemFacing, Vector3.one * 0.15f);
            Part(root, "AccentR", JewelryMeshFactory.BrilliantGem(8), stone,
                 new Vector3(0.19f, 0.27f, 0.02f), GemFacing, Vector3.one * 0.15f);
        }

        private static void BuildNecklace(Transform root, Material stone, Material setting)
        {
            // A flattened open chain, wider than it is tall.
            Part(root, "Chain", JewelryMeshFactory.Torus(0.40f, 0.040f), setting,
                 new Vector3(0f, 0.10f, 0f), Quaternion.identity, new Vector3(1.05f, 0.82f, 1f));

            Part(root, "Drop", JewelryMeshFactory.BrilliantGem(12), stone,
                 new Vector3(0f, -0.30f, 0.02f), GemFacing, Vector3.one * 0.46f);

            Part(root, "Bail", JewelryMeshFactory.Torus(0.07f, 0.022f), setting,
                 new Vector3(0f, -0.14f, 0f), Quaternion.identity, Vector3.one);
        }

        private static void BuildEarrings(Transform root, Material stone, Material setting)
        {
            // Deliberately a PAIR — two of anything reads as earrings instantly.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = 0.26f * side;

                Part(root, "Hook" + side, JewelryMeshFactory.Torus(0.11f, 0.026f), setting,
                     new Vector3(x, 0.26f, 0f), Quaternion.identity, new Vector3(1f, 1.15f, 1f));

                Part(root, "Stone" + side, JewelryMeshFactory.BrilliantGem(10), stone,
                     new Vector3(x, -0.06f, 0.02f), GemFacing, Vector3.one * 0.38f);
            }
        }

        private static void BuildBracelet(Transform root, Material stone, Material setting)
        {
            // Wide and flat, so it never reads as a ring.
            Part(root, "Cuff", JewelryMeshFactory.Torus(0.42f, 0.085f), setting,
                 Vector3.zero, Quaternion.identity, new Vector3(1.12f, 0.66f, 1f));

            // Stones set around the upper curve.
            float[] angles = { 200f, 250f, 290f, 340f };
            for (int i = 0; i < angles.Length; i++)
            {
                float radians = angles[i] * Mathf.Deg2Rad;
                Vector3 position = new Vector3(
                    Mathf.Cos(radians) * 0.42f * 1.12f,
                    Mathf.Sin(radians) * 0.42f * 0.66f,
                    0.05f);

                Part(root, "Stone" + i, JewelryMeshFactory.BrilliantGem(8), stone,
                     position, GemFacing, Vector3.one * 0.19f);
            }
        }

        private static void BuildPendant(Transform root, Material stone, Material setting)
        {
            Part(root, "Loop", JewelryMeshFactory.Torus(0.11f, 0.030f), setting,
                 new Vector3(0f, 0.40f, 0f), Quaternion.identity, Vector3.one);

            Part(root, "Stone", JewelryMeshFactory.BrilliantGem(14), stone,
                 new Vector3(0f, -0.04f, 0.02f), GemFacing, Vector3.one * 0.74f);

            Part(root, "Collar", JewelryMeshFactory.Torus(0.20f, 0.028f), setting,
                 new Vector3(0f, -0.04f, -0.04f), Quaternion.identity, Vector3.one);
        }

        private static void BuildCrown(Transform root, Material stone, Material setting)
        {
            Part(root, "Band", JewelryMeshFactory.Band(0.36f, 0.20f), setting,
                 new Vector3(0f, -0.22f, 0f), Quaternion.identity, new Vector3(1f, 1f, 0.55f));

            float[] offsets = { -0.26f, 0f, 0.26f };
            float[] heights = { 0.30f, 0.42f, 0.30f };

            for (int i = 0; i < offsets.Length; i++)
            {
                Part(root, "Spike" + i, JewelryMeshFactory.Spike(0.09f, heights[i]), setting,
                     new Vector3(offsets[i], -0.12f, 0f), Quaternion.identity,
                     new Vector3(1f, 1f, 0.55f));

                Part(root, "Tip" + i, JewelryMeshFactory.BrilliantGem(8), stone,
                     new Vector3(offsets[i], -0.12f + heights[i] + 0.04f, 0.01f),
                     GemFacing, Vector3.one * 0.17f);
            }

            Part(root, "Centre", JewelryMeshFactory.BrilliantGem(12), stone,
                 new Vector3(0f, -0.20f, 0.10f), GemFacing, Vector3.one * 0.30f);
        }
    }
}
