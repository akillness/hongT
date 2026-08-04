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

            // --- Light --------------------------------------------------------
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.82f, 0.62f);   // ember warmth
            light.intensity = 1.15f;
            lightObject.transform.rotation = Quaternion.Euler(55f, -28f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.30f, 0.42f);

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
    }
}
