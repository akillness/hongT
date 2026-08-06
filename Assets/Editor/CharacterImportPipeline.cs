// Batch import pipeline: re-skinned character FBX -> Humanoid avatar + URP
// materials + Resources prefab; bench Mixamo FBX -> Humanoid in-place clips.
// Runs headless: Unity -batchmode -executeMethod CinderCourt.EditorTools.CharacterImportPipeline.ImportAll
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CinderCourt.Sim;
using CinderCourt.View;
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

        // Bones are renamed to Unity-canonical humanoid names by
        // tools/blender/reskin_character.py; Mecanim auto-maps them.

        // action -> (bench fbx base name, loop, window)
        //
        // ORDER IS A CONTRACT. BuildController uses the ARRAY INDEX as the
        // animator's "action" condition value, so rows 0..10 MUST stay aligned
        // with the ActorAction enum (a frozen sim type). Rows past 10 are
        // View-only substates the sim never emits: the View resolves them from
        // state it already owns (combo index), which is how #9/#4 land without
        // amending the frozen contract. Append only; never reorder.
        // ClipTableTests pins the alignment.
        //
        // WINDOW is the authoritative number of seconds the pose is actually
        // held — the sim's action window for sim-driven rows, the View's pose
        // timer for View-owned rows. 0 means "no window": loops, and `die`,
        // whose clip is meant to run at authored speed under the shrink fade.
        //
        // Why it exists: the bench clips are Mixamo takes of 0.75-5.75 s, but
        // the frozen sim holds a swing for 0.30 s. Playing them at speed 1
        // meant the animator entered the state, showed the WIND-UP, and was
        // yanked back to idle by the next `action` write before the strike
        // frame ever rendered — measured: `attack` reached 24% of its clip,
        // `attack3` 10%. The authored motion was never on screen. Numbers are
        // the gate, and the sim's numbers are frozen, so the VIEW retimes:
        // BuildController fits each state's speed to its window.
        static readonly (string action, string file, bool loop, float window)[] Clips =
        {
            ("idle", "Unarmed Idle", true, 0f),
            ("move", "Walking", true, 0f),
            ("run", "Running", true, 0f),
            // Player recoil is gated by the damage grace, the only window that
            // outlives the single tick the sim leaves `Hit` set.
            ("hit", "Standing React Small From Left", false, SimConfig.PlayerHitGrace),
            // Launch reaction: the View infers it from step velocity and holds
            // it for the sim's knockback time. The enemy value (0.18) is the
            // common case; the 0.26 boss slam simply holds the last frame.
            ("bighit", "Receive Uppercut To The Face", false, HackSpec.ComboKnockbackTime),
            // `attack` serves BOTH the arena/prologue swing (5/12 s) and dungeon
            // combo hits 0-1 (0.30 s). Fitted to the SHORTER one so the clip
            // completes in every mode it plays in.
            ("attack", "Punching", false, HackSpec.ComboSwing[0]),
            // Launcher finisher and the charged heavy both run the last swing.
            ("critical", "Illegal Elbow Punch", false, HackSpec.ComboSwing[HackSpec.ComboLength - 1]),
            ("avoid", "Dodging", false, HackSpec.DashTime),
            ("defence", "Body Block", false, SimConfig.WardDuration),
            // No window: the player's death plays out at authored speed behind
            // the game-over panel, and an enemy's is carried by the 0.34 s
            // shrink fade, not by the clip. Fitting either would be a blur.
            ("die", "Dying", false, 0f),
            ("show", "Mutant Roaring", false, ActorView.RoarDuration),
            // --- View-only substates (index > ActorAction range) ---
            ("attack2", "Hook Punch", false, HackSpec.ComboSwing[1]),                        // #9 combo 2nd
            ("attack3", "Standing Melee Combo Attack Ver. 2", false, HackSpec.ComboSwing[2]), // #9 combo 3rd
            ("cast", "Standing 2H Magic Attack 01", false, ActorView.CastPoseDuration),       // #4 skill cast
        };

        /// <summary>Above this, a window-fitted clip stops reading as motion and
        /// starts reading as a blur. Not a clamp — the speed is still applied,
        /// because a legible-but-fast strike beats a wind-up that never lands.
        /// It marks the row as MISCAST so the log names the clips that need a
        /// shorter source take rather than hiding them behind a silent cap.</summary>
        internal const float LegibleSpeedCeiling = 5f;

        /// <summary>Index of the first View-only substate — everything below is
        /// an <see cref="ActorAction"/> the sim can emit.</summary>
        internal const int SimActionCount = 11;
        internal static string ActionNameAt(int index) => Clips[index].action;
        internal static float WindowAt(int index) => Clips[index].window;
        internal static int ClipCount => Clips.Length;

        /// <summary>The speed a state must run at for its clip to finish exactly
        /// when its window closes. 0-window rows keep authored speed.</summary>
        internal static float FitSpeed(float clipLength, float window)
            => window <= 0f || clipLength <= 0f ? 1f : clipLength / window;

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

        /// <summary>Rebuild ONLY the animator controller from clips that are
        /// already imported. The state speeds are derived from the sim windows,
        /// so retuning a window must not force a multi-minute FBX reimport (and
        /// must not touch the .meta files that reimport rewrites).
        /// Unity -batchmode -executeMethod
        /// CinderCourt.EditorTools.CharacterImportPipeline.RebuildController</summary>
        [MenuItem("CinderCourt/Rebuild Animator Controller")]
        public static void RebuildController()
        {
            try
            {
                BuildController();
                AssetDatabase.SaveAssets();
                Debug.Log("[CharacterImportPipeline] CONTROLLER DONE");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError($"[CharacterImportPipeline] CONTROLLER FAILED: {error}");
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
            var absent = CharacterRoster.Ids.Where(id => !present.Contains(id)).ToList();
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
                var textureDir = $"{Path.GetDirectoryName(path)!.Replace('\\', '/')}/" +
                                 $"{Path.GetFileNameWithoutExtension(path)}-textures";
                Directory.CreateDirectory(textureDir);
                importer.ExtractTextures(textureDir);
                RepairTextureExtensions(textureDir);
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

        /// <summary>
        /// GLB-embedded textures extract WITHOUT extensions ("texture_diffuse"),
        /// so Unity assigns DefaultImporter and LoadAssetAtPath&lt;Texture2D&gt;
        /// returns null. Sniff magic bytes, rename, refresh.
        /// </summary>
        static void RepairTextureExtensions(string textureDir)
        {
            if (!Directory.Exists(textureDir)) return;
            var renamed = false;
            foreach (var file in Directory.GetFiles(textureDir))
            {
                if (file.EndsWith(".meta") || Path.HasExtension(file)) continue;
                var head = new byte[4];
                using (var stream = File.OpenRead(file)) stream.Read(head, 0, 4);
                string extension = null;
                if (head[0] == 0x89 && head[1] == 0x50) extension = ".png";
                else if (head[0] == 0xFF && head[1] == 0xD8) extension = ".jpg";
                if (extension == null) continue;
                File.Delete(file + ".meta");
                if (File.Exists(file + extension)) File.Delete(file + extension);
                File.Move(file, file + extension);
                renamed = true;
            }
            if (renamed) AssetDatabase.Refresh();
        }


        static void RemapToUrpLit(ModelImporter importer, string path)
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) { Debug.LogWarning("URP/Lit shader missing"); return; }
            var dir = Path.GetDirectoryName(path)!.Replace('\\', '/');
            var id = Path.GetFileNameWithoutExtension(path);
            // Textures were extracted into the per-character folder (see
            // ReimportCharacters). Restrict the albedo search to THAT folder —
            // a shared folder collides because every GLB names its texture
            // "texture_diffuse".
            var textureDir = $"{dir}/{id}-textures";
            var textures = Directory.Exists(textureDir)
                ? Directory.GetFiles(textureDir, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(p => !p.EndsWith(".meta"))
                    .Select(p => p.Replace('\\', '/')).ToList()
                : new List<string>();

            // Stale remaps from a prior run keep materials external (and possibly
            // pointing at deleted GUIDs) — with a live remap the material is NOT
            // embedded as a sub-asset, so the foreach below would find nothing.
            var staleRemaps = importer.GetExternalObjectMap()
                .Where(entry => entry.Key.type == typeof(Material))
                .Select(entry => entry.Key).ToList();
            if (staleRemaps.Count > 0)
            {
                foreach (var identifier in staleRemaps)
                    importer.RemoveRemap(identifier);
                importer.SaveAndReimport();
            }
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            var changed = false;
            foreach (var asset in assets)
            {
                if (asset is not Material material) continue;
                var replacement = new Material(urpLit) { name = $"{id}-{material.name}" };
                var albedoTexture = material.mainTexture;
                if (albedoTexture == null && textures.Count > 0)
                {
                    var guess = textures.FirstOrDefault(t =>
                        t.ToLowerInvariant().Contains("diffuse") ||
                        t.ToLowerInvariant().Contains("basecolor") ||
                        t.ToLowerInvariant().Contains("albedo"))
                        ?? textures[0];
                    albedoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(guess);
                }
                if (albedoTexture != null) replacement.SetTexture("_BaseMap", albedoTexture);
                var normalPath = textures.FirstOrDefault(t =>
                    t.ToLowerInvariant().Contains("normal"));
                if (normalPath != null)
                {
                    // Mark as normal map first, or URP samples it as color.
                    if (AssetImporter.GetAtPath(normalPath) is TextureImporter normalImporter &&
                        normalImporter.textureType != TextureImporterType.NormalMap)
                    {
                        normalImporter.textureType = TextureImporterType.NormalMap;
                        normalImporter.SaveAndReimport();
                    }
                    var normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                    if (normalTexture != null)
                    {
                        replacement.SetTexture("_BumpMap", normalTexture);
                        replacement.EnableKeyword("_NORMALMAP");
                    }
                }
                replacement.SetFloat("_Smoothness", 0.15f);
                var materialDir = $"{dir}/Materials";
                Directory.CreateDirectory(materialDir);
                // Per-character path — a shared "model.mat" would be recreated
                // (new GUID) by every later character, orphaning earlier remaps
                // into magenta missing-material references.
                var materialPath = $"{materialDir}/{Sanitize(id)}-{Sanitize(material.name)}.mat";
                AssetDatabase.DeleteAsset(materialPath);
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
            foreach (var (action, file, loop, _) in Clips)
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
            var (_, file, _, _) = Clips.First(c => c.action == action);
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
            foreach (var (action, _, _, window) in Clips)
            {
                var state = machine.AddState(action);
                var clip = LoadClip(action);
                state.motion = clip;
                // Retime to the authoritative window (see the Clips table). Left
                // at 1, the state is cut off the moment the sim rewrites
                // `action` — the strike frame never renders.
                var speed = FitSpeed(clip.length, window);
                state.speed = speed;
                if (window > 0f)
                {
                    var miscast = speed > LegibleSpeedCeiling ? "  MISCAST" : string.Empty;
                    Debug.Log($"[Controller] {action} clip={clip.length:F3}s " +
                              $"window={window:F3}s speed={speed:F2}x{miscast}");
                }
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
