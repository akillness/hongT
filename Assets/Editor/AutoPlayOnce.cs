using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    /// <summary>
    /// One-shot play trigger for an already-open GUI editor session: while the
    /// marker file exists, open the game scene and enter play mode, then
    /// consume the marker. Polls on the editor update loop because
    /// [InitializeOnLoad] alone only fires on domain reload — a marker touched
    /// after the reload would never be seen. Inert without the marker.
    /// Usage: `touch Temp/autoplay-once` then focus the editor.
    /// </summary>
    [InitializeOnLoad]
    public static class AutoPlayOnce
    {
        const string Marker = "Temp/autoplay-once";
        const string ScenePath = "Assets/Scenes/CinderCourt.unity";

        static AutoPlayOnce()
        {
            EditorApplication.update += TryPlay;
        }

        static void TryPlay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                || !File.Exists(Marker))
            {
                return;
            }
            File.Delete(Marker);
            var active = EditorSceneManager.GetActiveScene();
            Debug.Log($"AutoPlayOnce: active scene '{active.path}' dirty={active.isDirty}");
            if (active.path != ScenePath)
            {
                // A pristine (non-dirty) scene — typically the Untitled default of a
                // fresh Library — can be replaced without prompting. Only a dirty
                // scene gets the save dialog.
                if (active.isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.LogWarning("AutoPlayOnce: scene save declined — not entering play mode.");
                    return;
                }
                EditorSceneManager.OpenScene(ScenePath);
            }
            Debug.Log("AutoPlayOnce: entering play mode (CinderCourt).");
            EditorApplication.EnterPlaymode();
        }
    }
}
