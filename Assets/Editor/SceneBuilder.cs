// Builds Assets/Scenes/CinderCourt.unity headlessly. Idempotent: recreates the
// scene from scratch each run. Camera framing reproduces the original 2.5D
// court read: fixed camera, arena fully visible, backdrop plate on the floor.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CinderCourt.EditorTools
{
    public static class SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/CinderCourt.unity";
        const string BackdropTexture = "Assets/Art/Textures/cinder-court-backdrop.png";
        // World mapping: sim (x, y) px -> Unity (x*S, 0, -y*S). S=0.01 -> 15.36 x 10.24 m.
        const float S = 0.01f;

        [MenuItem("CinderCourt/Build Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera -------------------------------------------------------
            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.043f, 0.035f, 0.06f);
            camera.fieldOfView = 32f;
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 80f;
            // Arena center (768, 604) -> (7.68, 0, -6.04). Pitch ~44°, pulled back.
            cameraObject.transform.position = new Vector3(7.68f, 11.8f, -17.6f);
            cameraObject.transform.rotation = Quaternion.Euler(44f, 0f, 0f);

            // --- §Lane V4: URP post volume (bloom + vignette) -----------------
            // Desktop p95 measured 10.0 ms on the live build (gate 16.7 ms,
            // headroom ~6.7 ms). Mobile is ungated-unmeasured -> PostFxGate
            // disables the camera flag there at runtime (spec: degrade, not
            // ship-and-hope). Profile is a serialized asset so URP keeps the
            // post shaders in the WebGL build.
            var profile = BuildPostProfile();
            var volumeObject = new GameObject("PostVolume");
            var volume = volumeObject.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            var cameraData = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;   // PostFxGate turns this off on mobile
            cameraObject.AddComponent(System.Type.GetType(
                "CinderCourt.View.PostFxGate, CinderCourt.View"));

            // --- Light --------------------------------------------------------
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.82f, 0.62f);   // ember warmth
            light.intensity = 1.15f;
            lightObject.transform.rotation = Quaternion.Euler(55f, -28f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.30f, 0.42f);

            // --- Outskirt fill: fog matched to the clear colour ---------------
            // Measured: the apron is a fixed 1700x1577 sim-unit rectangle while
            // the camera's ground footprint is a trapezoid that widens with
            // distance. At 3:2 that leaves 18.9% of the frame on the clear
            // colour; at 16:9 it is 51.7%. Covering it with geometry needs a
            // 2.31x prefab scale, which would wreck texel density and the
            // sense of scale.
            //
            // Instead: fogColor == backgroundColor. The apron's hard edge
            // dissolves into the background, so the empty region reads as
            // distance rather than as a missing floor.
            //
            // Band sized against the RUNTIME dungeon camera, not this editor
            // one: CameraRig places a 55-degree orbit at distance 17, giving
            // view depths of 15.61 u at the near playable edge, 17.00 at the
            // arena centre, 18.68 at the far playable edge, and 22.47 at the
            // apron rim. Linear 19 -> 22.5 therefore leaves the ENTIRE
            // playable area at 0% fog and dissolves the rim 99.1%. (16 -> 25
            // hazed the far playable edge 29.8% while leaving the rim only
            // 71.9% dissolved — a visible luminance step right on the seam.)
            // Cost: fog variants already ship (GraphicsSettings), so this adds
            // zero draw calls, zero triangles, and no new shader variants.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = camera.backgroundColor;
            RenderSettings.fogStartDistance = 19f;
            RenderSettings.fogEndDistance = 22.5f;

            // --- Backdrop plate -------------------------------------------------
            var plate = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plate.name = "CourtBackdrop";
            Object.DestroyImmediate(plate.GetComponent<Collider>());
            plate.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            plate.transform.position = new Vector3(
                SimWorld(1536f) / 2f, -0.01f, -SimWorld(1024f) / 2f);
            plate.transform.localScale = new Vector3(SimWorld(1536f), SimWorld(1024f), 1f);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackdropTexture);
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader) { name = "CourtBackdrop" };
            if (texture != null) material.SetTexture("_BaseMap", texture);
            else Debug.LogWarning($"[SceneBuilder] backdrop texture missing at {BackdropTexture}");
            Directory.CreateDirectory("Assets/Art/Materials");
            AssetDatabase.DeleteAsset("Assets/Art/Materials/CourtBackdrop.mat");
            AssetDatabase.CreateAsset(material, "Assets/Art/Materials/CourtBackdrop.mat");
            plate.GetComponent<MeshRenderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/CourtBackdrop.mat");

            // --- Game root -------------------------------------------------------
            var root = new GameObject("GameRoot");
            var bootstrapType = System.Type.GetType(
                "CinderCourt.View.GameBootstrap, CinderCourt.View");
            if (bootstrapType != null)
            {
                root.AddComponent(bootstrapType);
            }
            else if (System.Environment.GetEnvironmentVariable(
                         "CINDER_ALLOW_NO_BOOTSTRAP") == "1")
            {
                Debug.LogWarning("[SceneBuilder] GameBootstrap missing — allowed by env override");
            }
            else
            {
                // A scene without the bootstrap is a dead build that would still
                // sail through BuildScript+deploy. Fail loudly instead.
                throw new System.InvalidOperationException(
                    "GameBootstrap type not found (View lane not landed or not compiling). " +
                    "Set CINDER_ALLOW_NO_BOOTSTRAP=1 to build a scaffold scene anyway.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[SceneBuilder] saved {ScenePath}");
            if (Application.isBatchMode)
            {
                AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
            }
        }

        static float SimWorld(float px) => px * S;

        /// <summary>§V4: serialized VolumeProfile (bloom + vignette). Asset on
        /// disk keeps URP post shaders/variants in the WebGL build; runtime
        /// only toggles the camera flag (PostFxGate).</summary>
        static UnityEngine.Rendering.VolumeProfile BuildPostProfile()
        {
            const string path = "Assets/Settings/CinderPostProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            // DEFECT FIX: profile.Add<T>() builds the component in memory but
            // does NOT parent it to the profile asset, so both entries
            // serialized as {fileID: 0} — the shipped asset carried two NULL
            // references and post-processing has been inert since it landed.
            // AddObjectToAsset is what makes the component a real sub-asset.
            // (This also means the "desktop p95 10.0 ms" note above measured a
            // build with post effectively OFF; re-measure after this lands.)
            if (!profile.TryGet(out UnityEngine.Rendering.Universal.Bloom bloom))
            {
                bloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>(false);
                bloom.name = nameof(UnityEngine.Rendering.Universal.Bloom);
                AssetDatabase.AddObjectToAsset(bloom, profile);
            }
            bloom.active = true;
            bloom.intensity.Override(0.55f);
            bloom.threshold.Override(1.05f);   // only genuine emissives bloom
            bloom.scatter.Override(0.6f);
            if (!profile.TryGet(out UnityEngine.Rendering.Universal.Vignette vignette))
            {
                vignette = profile.Add<UnityEngine.Rendering.Universal.Vignette>(false);
                vignette.name = nameof(UnityEngine.Rendering.Universal.Vignette);
                AssetDatabase.AddObjectToAsset(vignette, profile);
            }
            vignette.active = true;
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.02f, 0.02f, 0.05f));
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
