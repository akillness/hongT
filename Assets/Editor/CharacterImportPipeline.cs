// Batch import pipeline: re-skinned character FBX -> Humanoid avatar + URP
// materials + Resources prefab; bench Mixamo FBX -> Humanoid in-place clips.
// Runs headless: Unity -batchmode -executeMethod CinderCourt.EditorTools.CharacterImportPipeline.ImportAll
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    public static class CharacterImportPipeline
    {
        const string CharacterDir = "Assets/Art/Characters";
        const string ClipDir = "Assets/Art/Motion";
        const string PrefabDir = "Assets/Resources/Characters";
        const string ControllerPath = "Assets/Art/Motion/CinderActor.controller";

        // Full roster per docs/SIM_SPEC.md §Roster. Import fails when any is absent.
        static readonly string[] Roster =
        {
            "guard", "ember-cohort", "scout", "shade", "possessed",
            "shadow-commander-boss", "broken-court-monarch-boss",
        };

        // Bones are renamed to Unity-canonical humanoid names by
        // tools/blender/reskin_character.py; Mecanim auto-maps them.

        // action -> (bench fbx base name, loop)
        static readonly (string action, string file, bool loop)[] Clips =
        {
            ("idle", "Unarmed Idle", true),
            ("move", "Walking", true),
            ("run", "Running", true),
            ("hit", "Standing React Small From Left", false),
            ("bighit", "Receive Uppercut To The Face", false),
            ("attack", "Punching", false),
            ("critical", "Illegal Elbow Punch", false),
            ("avoid", "Dodging", false),
            ("defence", "Body Block", false),
            ("die", "Dying", false),
            ("show", "Mutant Roaring", false),
        };

        [MenuItem("CinderCourt/Import All Characters And Clips")]
        public static void ImportAll()
        {
            try
            {
                ReimportCharacters();
                ReimportClips();
                BuildController();
                BuildPrefabs();
                AssetDatabase.SaveAssets();
                Debug.Log("[CharacterImportPipeline] DONE");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError($"[CharacterImportPipeline] FAILED: {error}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        static IEnumerable<string> CharacterFbxPaths() =>
            Directory.Exists(CharacterDir)
                ? Directory.GetFiles(CharacterDir, "*.fbx", SearchOption.TopDirectoryOnly)
                    .Select(p => p.Replace('\\', '/'))
                : Enumerable.Empty<string>();

        static void ReimportCharacters()
        {
            var present = CharacterFbxPaths()
                .Select(Path.GetFileNameWithoutExtension).ToHashSet();
            var absent = Roster.Where(id => !present.Contains(id)).ToList();
            if (absent.Count > 0)
                throw new InvalidOperationException(
                    $"roster incomplete, missing FBX: {string.Join(", ", absent)} " +
                    "(run tools/blender/reskin_all.sh)");
            foreach (var path in CharacterFbxPaths())
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                if (importer == null) continue;
                importer.animationType = ModelImporterAnimationType.Human;
                // Bones were renamed to Unity-canonical humanoid names by
                // tools/blender/reskin_character.py, so auto-mapping succeeds.
                // Do NOT assign humanDescription with an empty skeleton[] —
                // that yields an invalid avatar.
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.ExtractTextures(Path.GetDirectoryName(path));
                importer.SaveAndReimport();
                var avatar = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<Avatar>().FirstOrDefault();
                if (avatar == null || !avatar.isValid || !avatar.isHuman)
                    throw new InvalidOperationException(
                        $"invalid humanoid avatar after import: {path} " +
                        "(check reskin report for missing bones)");
                RemapToUrpLit(importer, path);
                Debug.Log($"[Import] character {Path.GetFileName(path)}");
            }
        }


        static void RemapToUrpLit(ModelImporter importer, string path)
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) { Debug.LogWarning("URP/Lit shader missing"); return; }
            var dir = Path.GetDirectoryName(path)!.Replace('\\', '/');
            var textures = Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(dir, "*.jpg", SearchOption.AllDirectories))
                .Select(p => p.Replace('\\', '/')).ToList();
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            var changed = false;
            foreach (var asset in assets)
            {
                if (asset is not Material material) continue;
                var replacement = new Material(urpLit) { name = material.name };
                var albedoTexture = material.mainTexture;
                if (albedoTexture == null && textures.Count > 0)
                {
                    var guess = textures.FirstOrDefault(t =>
                        t.ToLowerInvariant().Contains("basecolor") ||
                        t.ToLowerInvariant().Contains("albedo") ||
                        t.ToLowerInvariant().Contains(material.name.ToLowerInvariant()));
                    if (guess != null)
                        albedoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(guess);
                }
                if (albedoTexture != null) replacement.SetTexture("_BaseMap", albedoTexture);
                replacement.SetFloat("_Smoothness", 0.15f);
                var materialDir = $"{dir}/Materials";
                Directory.CreateDirectory(materialDir);
                var materialPath = $"{materialDir}/{Sanitize(material.name)}.mat";
                AssetDatabase.CreateAsset(replacement, materialPath);
                importer.AddRemap(
                    new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name),
                    AssetDatabase.LoadAssetAtPath<Material>(materialPath));
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }

        static string Sanitize(string name) =>
            string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_'));

        static void ReimportClips()
        {
            foreach (var (action, file, loop) in Clips)
            {
                var path = $"{ClipDir}/{file}.fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                if (importer == null)
                    throw new InvalidOperationException($"missing clip fbx {path}");
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                var takes = importer.defaultClipAnimations;
                if (takes.Length == 0)
                    throw new InvalidOperationException($"no animation takes in {path}");
                var take = takes[0];
                take.name = action;
                take.loopTime = loop;
                take.loopPose = loop;
                take.lockRootRotation = true;
                take.lockRootHeightY = true;
                take.lockRootPositionXZ = true;   // in-place: sim owns displacement
                take.keepOriginalOrientation = true;
                take.keepOriginalPositionY = true;
                take.keepOriginalPositionXZ = true;
                importer.clipAnimations = new[] { take };
                importer.SaveAndReimport();
                var clipAvatar = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<Avatar>().FirstOrDefault();
                if (clipAvatar == null || !clipAvatar.isValid || !clipAvatar.isHuman)
                    throw new InvalidOperationException($"invalid clip avatar: {path}");
                Debug.Log($"[Import] clip {action} <- {file}.fbx");
            }
        }

        static AnimationClip LoadClip(string action)
        {
            var (_, file, _) = Clips.First(c => c.action == action);
            var assets = AssetDatabase.LoadAllAssetsAtPath($"{ClipDir}/{file}.fbx");
            var clip = assets.OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == action && !c.name.StartsWith("__preview__"));
            if (clip == null)
                throw new InvalidOperationException($"clip '{action}' not found in {file}.fbx");
            return clip;
        }

        static void BuildController()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath)!);
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("action", AnimatorControllerParameterType.Int);
            var machine = controller.layers[0].stateMachine;

            var actions = Clips.Select(c => c.action).ToArray();
            var states = new Dictionary<string, AnimatorState>();
            foreach (var action in actions)
            {
                var state = machine.AddState(action);
                state.motion = LoadClip(action);
                states[action] = state;
            }
            machine.defaultState = states["idle"];

            // Any-state -> each state on action index; loops don't self-transition.
            for (var i = 0; i < actions.Length; i++)
            {
                var transition = machine.AddAnyStateTransition(states[actions[i]]);
                transition.AddCondition(AnimatorConditionMode.Equals, i, "action");
                transition.hasExitTime = false;
                transition.duration = 0.08f;      // short crossfade, spec §fades
                transition.canTransitionToSelf = false;
            }
            // One-shots return to idle when finished (die stays clamped).
            foreach (var action in actions)
            {
                if (action is "idle" or "move" or "run" or "die") continue;
                var back = states[action].AddTransition(states["idle"]);
                back.hasExitTime = true;
                back.exitTime = 0.95f;
                back.duration = 0.1f;
                back.AddCondition(AnimatorConditionMode.Equals, 0, "action");
            }
            EditorUtility.SetDirty(controller);
        }

        static void BuildPrefabs()
        {
            Directory.CreateDirectory(PrefabDir);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            foreach (var path in CharacterFbxPaths())
            {
                var id = Path.GetFileNameWithoutExtension(path);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                var prefabPath = $"{PrefabDir}/{id}.prefab";
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                UnityEngine.Object.DestroyImmediate(instance);
                Debug.Log($"[Prefab] {prefabPath}");
            }
        }
    }
}
