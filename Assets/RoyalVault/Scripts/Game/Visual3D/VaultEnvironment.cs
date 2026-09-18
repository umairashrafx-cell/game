using UnityEngine;
using UnityEngine.Rendering;

#if URP_PRESENT
using UnityEngine.Rendering.Universal;
#endif

namespace RoyalVault.Game.Visual3D
{
    /// <summary>
    /// Builds the vault the jewelry sits in: camera, lighting, backdrop and grading.
    ///
    /// This is where most of the "expensive" look actually comes from. The meshes are only a few
    /// hundred triangles each — what makes gold read as gold is having something for it to
    /// reflect, and what makes a gem read as cut glass is a hard specular highlight against a
    /// dark surround. So the priorities here are, in order:
    ///
    ///   1. A reflection probe. A metal with nothing to reflect renders almost black.
    ///   2. A warm key light with a cool fill, so the gold has a hue gradient across it.
    ///   3. A dark, low-contrast backdrop, so the gems are the brightest thing on screen.
    ///   4. Bloom, and only a little, on the brightest highlights.
    ///
    /// All of it is cheap: three lights, no realtime shadows, one probe rendered once at startup.
    /// </summary>
    public static class VaultEnvironment
    {
        public static Camera Build(Transform root)
        {
            Camera camera = BuildCamera(root);
            BuildLighting(root);
            BuildBackdrop(root);
            BuildReflectors(root);
            BuildReflectionProbe(root);
            BuildPostProcessing(root);
            return camera;
        }

        private static Camera BuildCamera(Transform root)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject go = new GameObject("Main Camera", typeof(Camera));
                go.tag = "MainCamera";
                camera = go.GetComponent<Camera>();
            }

            camera.transform.SetParent(root, false);
            camera.orthographic = false;
            camera.fieldOfView = 44f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Hex("140F0D");

            // Slightly above the board looking gently down. A dead-on camera was tried first and
            // was a mistake: viewed square-on, a ring lying in the XY plane presents no depth at
            // all and the whole board reads as flat icons. A few degrees is enough to reveal the
            // thickness of every piece without making the upper trays harder to judge.
            camera.transform.position = new Vector3(0f, 0.9f, -11f);
            camera.transform.rotation = Quaternion.Euler(4.5f, 0f, 0f);

#if URP_PRESENT
            // URP cameras have post-processing switched OFF by default. Without this the Volume
            // is built, configured and completely ignored — which is exactly what happened on the
            // first attempt, and why the gems had no glow and the image looked washed out.
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.Medium;
            data.renderShadows = false;
#endif

            return camera;
        }

        private static void BuildLighting(Transform root)
        {
            // Warm key from the upper left.
            Light key = MakeLight(root, "KeyLight", Hex("FFE2B0"), 1.35f);
            key.transform.rotation = Quaternion.Euler(38f, 28f, 0f);
            key.shadows = LightShadows.None;

            // Cool fill from the lower right stops the shadow side going muddy.
            Light fill = MakeLight(root, "FillLight", Hex("8FB4E8"), 0.55f);
            fill.transform.rotation = Quaternion.Euler(-16f, -52f, 0f);
            fill.shadows = LightShadows.None;

            // Rim from behind puts a bright edge on every stone.
            Light rim = MakeLight(root, "RimLight", Hex("FFF2D8"), 0.85f);
            rim.transform.rotation = Quaternion.Euler(-28f, 168f, 0f);
            rim.shadows = LightShadows.None;

            // Metals reflect the ambient probe, so this gradient IS the metal's appearance.
            // A dark ambient was tried first and made the gold look like flat yellow plastic —
            // there was simply nothing for it to reflect. A bright warm "sky" over a near-black
            // "ground" gives every curved gold surface a light-to-dark sweep, which is what the
            // eye reads as polished metal.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("D8BC86");
            RenderSettings.ambientEquatorColor = Hex("6A5442");
            RenderSettings.ambientGroundColor = Hex("140F0C");
            RenderSettings.fog = false;
        }

        private static Light MakeLight(Transform root, string name, Color color, float intensity)
        {
            GameObject go = new GameObject(name, typeof(Light));
            go.transform.SetParent(root, false);

            Light light = go.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            return light;
        }

        private static void BuildBackdrop(Transform root)
        {
            // A large panel well behind the board, plus a warmer panel behind that, to give the
            // vault a sense of receding space instead of a flat colour fill.
            Panel(root, "VaultWall", new Vector3(40f, 40f, 0.5f), new Vector3(0f, 0f, 9f),
                  JewelryMaterialFactory.VaultWall());

            Panel(root, "VaultFloorGlow", new Vector3(16f, 9f, 0.5f), new Vector3(0f, -1.2f, 6.2f),
                  JewelryMaterialFactory.VaultFloor());
        }

        /// <summary>
        /// Bright panels sitting BEHIND the camera. The player never sees them directly — they
        /// exist purely to be reflected by the gold and the gem facets. Without them the metal
        /// has an empty black room to mirror and reads as flat plastic.
        /// </summary>
        private static void BuildReflectors(Transform root)
        {
            // Large warm source above and behind, like a softbox over a jeweller's bench.
            Reflector(root, "ReflectorKey", new Vector3(26f, 14f, 0.4f),
                      new Vector3(0f, 9f, -17f), Quaternion.Euler(52f, 0f, 0f),
                      JewelryMaterialFactory.ReflectorWarm());

            // Cool edge sources left and right give the metal a hue break across its curve.
            Reflector(root, "ReflectorLeft", new Vector3(0.4f, 16f, 14f),
                      new Vector3(-13f, 0f, -8f), Quaternion.identity,
                      JewelryMaterialFactory.ReflectorCool());

            Reflector(root, "ReflectorRight", new Vector3(0.4f, 16f, 14f),
                      new Vector3(13f, 0f, -8f), Quaternion.identity,
                      JewelryMaterialFactory.ReflectorCool());
        }

        private static void Reflector(Transform root, string name, Vector3 size, Vector3 position,
                                      Quaternion rotation, Material material)
        {
            GameObject go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;

            go.GetComponent<MeshFilter>().sharedMesh = JewelryMeshFactory.Slab(size);

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void Panel(Transform root, string name, Vector3 size, Vector3 position, Material material)
        {
            GameObject go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;

            go.GetComponent<MeshFilter>().sharedMesh = JewelryMeshFactory.Slab(size);

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// Without this, every metal in the scene renders nearly black. Rendered once at startup
        /// and then left alone, so it costs nothing per frame.
        /// </summary>
        private static void BuildReflectionProbe(Transform root)
        {
            GameObject go = new GameObject("VaultReflectionProbe", typeof(ReflectionProbe));
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0f, 0f, -1f);

            ReflectionProbe probe = go.GetComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 128;
            probe.cullingMask = ~0;
            probe.size = new Vector3(60f, 60f, 60f);
            probe.intensity = 1.1f;
            probe.RenderProbe();
        }

        private static void BuildPostProcessing(Transform root)
        {
#if URP_PRESENT
            GameObject go = new GameObject("PostProcessing", typeof(Volume));
            go.transform.SetParent(root, false);

            Volume volume = go.GetComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;

            // Bloom threshold sits just below the brightest specular hits, so the glow lands on
            // gem facets and gold highlights rather than washing the whole frame.
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.threshold.Override(0.70f);
            bloom.intensity.Override(1.15f);
            bloom.scatter.Override(0.68f);
            bloom.tint.Override(Hex("FFE9C4"));

            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            // No exposure lift. Raising it crushed the vault toward milky brown instead of the
            // near-black it is authored as; contrast does the work of separating the jewelry
            // from the velvet without lifting the shadows.
            ColorAdjustments colour = profile.Add<ColorAdjustments>(true);
            colour.postExposure.Override(-0.10f);
            colour.contrast.Override(26f);
            colour.saturation.Override(16f);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.34f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(Hex("0A0706"));
#endif
        }

        private static Color Hex(string hex)
        {
            Color color;
            return ColorUtility.TryParseHtmlString("#" + hex, out color) ? color : Color.magenta;
        }
    }
}
