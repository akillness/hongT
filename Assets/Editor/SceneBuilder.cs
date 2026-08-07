// Builds Assets/Scenes/CinderCourt.unity headlessly. Idempotent: recreates the
// scene from scratch each run. Camera framing reproduces the original 2.5D
// court read: fixed camera, arena fully visible, backdrop plate on the floor.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using CinderCourt.View;

namespace CinderCourt.EditorTools
{
    public static class SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/CinderCourt.unity";
        const string BackdropTexture = "Assets/Art/Textures/cinder-court-backdrop.png";
        /// <summary>Grain for the out-of-arena dark. Reuses a stage stone map -
        /// they are generated seamless-tileable, so no new asset is needed.</summary>
        const string VoidFloorTexture =
            "Assets/Resources/Textures/Env/abyss-chancel-stone.png";
        // World mapping: sim (x, y) px -> Unity (x*S, 0, -y*S).
        //
        // S is DERIVED, never a literal. It was hardcoded 0.01f, and when the
        // runtime moved ViewWorld.Scale 0.01 -> 0.0125 nothing here followed:
        // the painted court plate stayed centred on sim(768,512)*0.01 =
        // (7.68,-5.12) while the arena moved to (9.6,-6.4), so the backdrop sat
        // 154x102 sim px off-centre at 80% of the area it had to cover. Same
        // raw-constant drift that hit the furniture caps; deriving it makes the
        // baked scene track the runtime quotient by construction.
        //
        // Scope note: only the SimWorld() call sites follow S. The camera
        // transform (L35) and VoidFloor's 40x26 extent are deliberate literals
        // — the camera is scene-authored against the OLD quotient on purpose
        // (CameraRig.Awake rebases it via LegacyScaleRatio, so moving it here
        // would double-compensate), and VoidFloor is oversized on purpose to
        // cover the widest frustum tier.
        const float S = ViewWorld.Scale;

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
            // fogColor is deliberately NOT equal to backgroundColor. Setting
            // them equal (the obvious first try) makes fog erase the very
            // geometry added to fill the outskirts: anything past fogEnd
            // renders at exactly the clear colour, so the void floor becomes
            // indistinguishable from the void it exists to hide. Measured on
            // the live build: 18.7% of the frame still sat at clear colour.
            // A slightly lifted, warmer haze keeps distant floor READABLE as
            // floor while still dissolving the apron's hard rim.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.075f, 0.062f, 0.092f);
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

            // --- Void floor -------------------------------------------------
            // Fog alone CANNOT close the outskirts, and the geometry proves
            // it. At the dungeon orbit the depths are:
            //   far playable edge 18.68 u | NEAR apron edge 14.05 u
            // The near apron rim is CLOSER to the camera than the far edge of
            // the play area, so any distance band that hides the rim also
            // hazes the arena. Distance fog can only ever dissolve the FAR
            // rim — which it does, 99% — leaving the near and side edges as
            // hard lines against the clear colour. Verified in the live
            // build, not assumed.
            //
            // So put something there. A single unlit quad well below the
            // apron, in the clear colour, turns "the world stops here" into
            // "the floor continues into the dark". 40 x 26 world u covers the
            // frustum footprint at the widest tier (boss orbit 21, 16:9).
            // Cost: 2 triangles, 1 draw call, no texture.
            var voidFloor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            voidFloor.name = "VoidFloor";
            Object.DestroyImmediate(voidFloor.GetComponent<Collider>());
            voidFloor.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            // Under the apron (which sits at y ~= 0), centred on the arena.
            voidFloor.transform.position = new Vector3(
                SimWorld(1536f) / 2f, -0.35f, -SimWorld(1024f) / 2f);
            voidFloor.transform.localScale = new Vector3(40f, 26f, 1f);
            var voidMaterial = new Material(shader) { name = "VoidFloor" };
            // Value matched to the apron's SHADOW tone, not to the background.
            // Measured on the live build: with the floor at 0.055 the seam was
            // a 4x luminance step (sum 39 -> 151 across one sample), which
            // reads as "the world ends here" even though floor IS present.
            // The floor must be dark enough to recede but close enough that
            // the apron edge becomes a gradient rather than a cliff.
            // 0.105/0.092/0.125 was the UNTEXTURED tuning. URP Unlit is
            // _BaseColor x _BaseMap, so binding a map below (mean luma 0.336)
            // multiplies that value down to an effective 0.035 - 1.6x DARKER
            // than the 0.055 this very comment records as already failing
            // ("the world ends here"). Compensate by the measured texture mean
            // so the tuned appearance survives the multiply.
            //
            // Re-tuned the same way the original was: sampled the deployed
            // frame either side of the plate edge. apron 48.34 vs void 22.28 =
            // a 2.17x step; x1.36 brings it to ~1.6x, a gradient rather than
            // the 4x cliff. Deliberately still DARKER than the apron - the
            // arena must stay the bright centre (E0.5 readability, and the
            // key art's own composition).
            const float voidTextureMeanLuma = 0.336f;
            const float voidSeamCorrection = 1.36f;
            var voidTone = new Color(0.105f, 0.092f, 0.125f, 1f)
                * (voidSeamCorrection / voidTextureMeanLuma);
            voidTone.a = 1f;
            voidMaterial.SetColor("_BaseColor", voidTone);
            // MEASURED: this quad is the dark mass that owns the frame. Landing
            // the stage textures took the dominant 24-step colour bucket from
            // 86.2% to 54.5%, and realigning the painted backdrop moved it a
            // further 0.5 pt only — because the backdrop covers 24% of this
            // quad's area and sits entirely inside the terrain plate. What is
            // left over IS VoidFloor, flat and untextured.
            //
            // So give it grain. URP Unlit multiplies _BaseColor x _BaseMap, so
            // the shadow-tone value above is preserved exactly — only texture
            // detail is added, never brightness. Tiling matches the world
            // density the rest of the environment uses (1 tile per 1.28 u, the
            // E2 module grid): a coarser void floor would put a visible grain
            // step right at the apron edge, re-creating the "world ends here"
            // line this quad exists to remove.
            //
            // One shared map for every stage, on purpose. This is the darkness
            // OUTSIDE the arena — it carries no stage identity, so a per-stage
            // bind would cost runtime code and a scene lookup for nothing.
            const float voidTilesPerUnit = 1f / 1.28f;
            var voidTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(VoidFloorTexture);
            if (voidTexture != null)
            {
                voidMaterial.SetTexture("_BaseMap", voidTexture);
                voidMaterial.SetTextureScale("_BaseMap",
                    new Vector2(40f * voidTilesPerUnit, 26f * voidTilesPerUnit));
            }
            else Debug.LogWarning($"[SceneBuilder] void texture missing at {VoidFloorTexture}");
            AssetDatabase.DeleteAsset("Assets/Art/Materials/VoidFloor.mat");
            AssetDatabase.CreateAsset(voidMaterial, "Assets/Art/Materials/VoidFloor.mat");
            voidFloor.GetComponent<MeshRenderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/VoidFloor.mat");
            voidFloor.GetComponent<MeshRenderer>().shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

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
