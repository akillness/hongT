// Batch import pipeline: re-skinned character FBX -> Humanoid avatar + URP
// materials + Resources prefab; bench Mixamo FBX -> Humanoid in-place clips.
// Runs headless: Unity -batchmode -executeMethod CinderCourt.EditorTools.CharacterImportPipeline.ImportAll
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CinderCourt.Sim;
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

        // action -> (bench fbx base name, loop)
        //
        // ORDER IS A CONTRACT. BuildController uses the ARRAY INDEX as the
        // animator's "action" condition value, so rows 0..10 MUST stay aligned
        // with the ActorAction enum (a frozen sim type). Rows past 10 are
        // View-only substates the sim never emits: the View resolves them from
        // state it already owns (combo index), which is how #9/#4 land without
        // amending the frozen contract. Append only; never reorder.
        // ClipTableTests pins the alignment.
        static readonly (string action, string file, bool loop)[] Clips =
        {
            // 2026-08-10 weapon-family pass (interview-directed): the character
            // carries a weapon, so unarmed takes read as the wrong FAMILY even
            // when their arcs measure fine (same root as the boxing-attack
            // defect fixed at 1742c10). idle/bighit/die/show swapped to
            // generated takes triaged VISUALLY from rendered previews —
            // action ids are opaque, so the eye does family judgement and the
            // probes do fit judgement. Map: mesh-gen/manifest.json.
            // move/run stay: no weapon-family locomotion found in any sampled
            // band (bow-flavored walks only) — sub-100 ids are the remaining
            // unexplored space. hit/defence/cast joined the family pass on
            // 2026-08-10 via the 130-178 band harvest (mesh-gen manifest).
            ("idle", "Combat Idle", true),
            ("move", "Walking", true),
            ("run", "Running", true),
            ("hit", "Hit Reaction", false),
            ("bighit", "Abdominal Hit Fall", false),
            ("attack", "Charged Slash", false),
            ("critical", "Thrust Slash", false),
            ("avoid", "Dodging", false),
            ("defence", "Block Three", false),
            ("die", "Shot In The Back", false),
            ("show", "Angry Ground Stomp", false),
            // --- View-only substates (index > ActorAction range) ---
            ("attack2", "Axe Spin Attack", false),                   // #9 combo 2nd
            ("attack3", "Weapon Combo 2", false),                    // #9 combo 3rd
            ("cast", "Mage Cast", false),                            // #4 skill cast
        };

        // --- clip trims -------------------------------------------------------
        // A mixamo action clip is authored as a standalone performance: settle
        // into stance, wind up, strike, recover, settle again. The sim holds an
        // attack pose for a FIXED window (arena 5 frames @ 12 fps = 0.417 s) and
        // drops it the instant that window closes, so an untrimmed 2.417 s swing
        // can only ever show its preamble. Measured in the running editor
        // (2026-02-04): every player swing was cut at normalizedTime 0.10-0.35
        // and the weapon never travelled.
        //
        // Measured trim window (AnimationClip.SampleAnimation over the whole
        // clip, right-hand speed relative to the hips, 24 fps source): frames
        // 0-17 are stance, the strike accelerates at f18, peaks f21-25 (max
        // 6.9 u/s at f24) and is spent by f26; f27+ is recovery. 16..28 keeps
        // the arc and nothing else -- 12 frames = 0.5 s, which ActorView paces
        // at 1.2x, and it places the contact frames at 0.17-0.31 s of the pose,
        // inside the sim's own active window (SimConfig.AttackActiveFrom/To =
        // 0.167..0.333 s). Actions with no row here import whole.
        //
        // show / cast (2026-08-09). ActorView.cs claimed for months that these
        // two states were "fitted to this window (CharacterImportPipeline)" —
        // they were not: no ClipTrims row, no baked state speed, m_Speed 1 in
        // the controller. Mutant Roaring imported whole (130 f / 5.42 s) into
        // RoarDuration's 1.1 s, so the boss entrance showed ~20% of a roar and
        // cut mid-bellow; the cast clip (65 f / 2.71 s) into CastPoseDuration's
        // 0.30 s showed ~11%. Measured with Assets/Editor/ClipWindowProbe.cs —
        // the same SampleAnimation rig described above, now checked in:
        //   Mutant Roaring   peak f21, motion f15-28 -> trim 8..34  = 1.083 s
        //   2H Magic Attack  peak f27, motion f26-28 -> trim 23..30 = 0.292 s
        // Both windows CONTAIN the measured motion and land within 17 ms of
        // their targets, so each plays at speed 1 and simply ends when the
        // window does. Trimming beats retiming here: a speed fit would have to
        // reach 4.9x for the roar, past MaxPoseSpeed, and a 5x roar is a chirp.
        //
        // Reaction clips were measured too. hit (f0-2, preamble 2.2%) stays
        // untrimmed — already tight. The bighit numbers that used to sit here
        // (f2-3, 5.3%) were measured on Knock Down 1, which the 2026-08-10
        // weapon-family pass DISPLACED; bighit now carries a measured row
        // below — see the ClipTrims comment for the current mechanism.
        // Dodging (46.2%) and Body Block (28.3%) carry preamble but play at
        // their authored pace, so trimming them would change established feel
        // for no measured defect. Listed so the next reader knows each
        // omission is a decision, not an oversight.
        //
        // 2026-08-10. Six combat takes were replaced with generated ones
        // (Higgsfield 3d_rigging; docs/provenance/motion.json):
        //   attack attack2 attack3 critical defence bighit
        // attack's old 16..28 row was peak f21-25 of "Standing Melee Attack
        // Horizontal". Those numbers cut a DIFFERENT swing at arbitrary points
        // and would have done it silently (a short window always passes the
        // length guard below), so the row was dropped and re-measured.
        //
        // Re-measured with ClipWindowProbe against the new takes. The pose the
        // sim holds is 5 f @12 fps = 0.4167 s = 10 f at the clips' own 24 fps,
        // and the damage-active span inside it is 0.167..0.333 s
        // (SimConfig.AttackActiveFrom/To). Each window below is 10 frames, so
        // ResolvePoseSpeed lands on 1.00x — no retime — and each places its
        // measured motion inside the active span:
        //   attack   Punch Combo 1  motion f17-21 peak f18 -> 13..23,
        //                           motion at 0.167..0.333 s of the pose
        //   critical Thrust Slash   motion f62-63 peak f62 -> 58..68,
        //                           motion at 0.167..0.208 s
        // Thrust Slash is the reason critical needs a row at all: 86.7% of its
        // 3.00 s is windup and the strike is the last 2 frames, so untrimmed it
        // would be clamped to MaxPoseSpeed 4x and still show only the windup.
        //
        // attack2 / attack3 ALSO need rows, and the claim that they did not was
        // wrong. It compared PREAMBLE against the takes they replaced (22.3 vs
        // 39.2, 32.6 vs 57.8 percent) — the right comparison for "is this take
        // better", the wrong one for "does it fit". Both are combo swings and
        // ActorView.ResolvePoseSpeed squeezes them into HackSpec.ComboSwing:
        //   attack2  1.79 s into 0.30 s -> 4.00x (MaxPoseSpeed clamp), 93% shown
        //   attack3  3.83 s into 0.42 s -> 4.00x clamp,                43% shown
        // A 4x swing is a twitch, and the clamp means it does not even finish.
        // The takes they displaced were in the same state (Hook Punch 4.00x,
        // Standing Melee Combo 4.00x) so this is inherited, not introduced —
        // it is still wrong. MEASURED 2026-08-10 by ClipWindowProbe.
        //
        // Windows are per combo index, not the arena's 0.4167:
        //   hit 2  ComboSwing[1] 0.30 s = 7 f, active 0.10..0.22 s
        //     Right Upper Hook motion f10-14 peak f13 -> 8..15,
        //     motion at 0.083..0.250 s, pose speed 0.97x
        //   hit 3  ComboSwing[2] 0.42 s = 10 f, active 0.14..0.30 s
        //     Punch Combo 5 motion f30-34 peak f33 -> 27..37,
        //     motion at 0.125..0.292 s, pose speed 0.99x
        // Each window is centred on the sim's own active span, so the strike is
        // live while the arm is actually travelling.
        //
        // defence stays untrimmed: not squeezed into a fixed pose window
        // (PoseValueForClip returns -1, pose speed stays 1), plays at its
        // authored pace. bighit USED to sit in this list; the 2026-08-10
        // take swap gave it a measured row below — a to-death take cannot
        // represent a survival reaction whole.
        // 2026-08-10, SECOND PASS — the first one picked the wrong FAMILY.
        //
        // attack/attack2/attack3 were swapped to Punch_Combo_1 /
        // Right_Upper_Hook / Punch_Combo_5. Those are boxing takes, and the
        // player carries a weapon bound to RightHand (ActorView.cs:390-392),
        // so the character jabbed with a sword in its fist. The user reported
        // it as "휘두르지 않는다" and they were right.
        //
        // Nothing measured caught it because every measurement was LINEAR.
        // A punch has real hand speed — it just travels out and back along one
        // ray instead of crossing an arc. Assets/Editor/SwingArcProbe.cs was
        // written for this: it measures the hand's swept ANGLE about the
        // shoulder, in the hips' frame so body rotation does not count.
        //
        //   peak hand speed      punches 4.67 / 3.54 / 5.74
        //                        weapons 37.2 / 37.7 / 48.4     (8-10x)
        //   swept arc, untrimmed punches 136.7 / 67.6 / 106.5 deg
        //                        weapons 720 / 1007 / 2245 deg
        //
        // The library's weapon cluster is ids 237-242 (Charged_Axe_Chop,
        // Axe_Spin_Attack, Thrust_Slash, Weapon_Combo_2, Charged_Slash) —
        // narrow, and 40 ids away from the punch cluster at 195-205 where the
        // first pass sampled. docs/provenance/motion-generated.json carries the
        // map.
        //
        // Re-measured trims. Windows are per combo index (HackSpec.ComboSwing)
        // and each is centred on the sim's active span:
        //   attack   Charged Slash    motion f26 peak f26 -> 22..29, 0.97x
        //   attack2  Axe Spin Attack  motion f33-34 peak f34 -> 30..37, 0.97x
        //   attack3  Weapon Combo 2   motion f50-51 peak f51 -> 45..55, 0.99x
        static readonly (string action, int firstFrame, int lastFrame)[] ClipTrims =
        {
            // attack2/attack3 windows RE-MEASURED 2026-08-10 in WORLD space
            // (mesh-gen/swing-windows.json). The hips-relative probe that
            // picked the old attack2 row is blind on a spin — the whole body
            // rotates, so hand-relative-to-hips speed is high everywhere and
            // the chosen window (30..37) was actually post-sweep FOOTWORK
            // (world hand/foot ratio 1.8; user-visible as "legs move,
            // no swing"). The blade sweep of Axe Spin Attack is 20..27
            // (ratio 5.1). Weapon Combo 2 carries three swings; 47..57 keeps
            // the biggest with the cleanest feet (6.7 vs 5.3 at 45..55).
            // 2026-08-10 THIRD measurement, after re-rigging every generated
            // take onto the shipped player mesh. Windows are re-derived on the
            // NEW rig by swept ANGLE about the right shoulder in the hips'
            // frame (mesh-gen/swing-windows.json) — the quantity SwingArcProbe
            // and ClipTrimFitTests judge a swing by. Every row moved except
            // attack2, and the ones that moved were not marginal:
            //   attack   22..29  91.9 deg  ->  26..33  187.1 deg
            //   attack2  20..27 160.5 deg  (kept; the 30..37 it replaced
            //                               sweeps 32.5 deg — the footwork)
            //   attack3  47..57 176.3 deg  ->  45..55  219.5 deg
            //   critical 58..68  61.2 deg  ->  31..41  181.6 deg
            // A window measured on one rig does not survive a rig change:
            // same frames, different body, different arc.
            ("attack", 26, 33),
            ("attack2", 20, 27),
            ("attack3", 45, 55),
            ("critical", 31, 41),
            // show 8..34 is RE-MEASURED for Angry Ground Stomp (2026-08-10),
            // not inherited from Mutant Roaring: ClipWindowProbe motion
            // f22-24 sits inside it and 26 f = 1.083 s fits RoarDuration.
            // That the numbers coincide with the old row is coincidence —
            // a stale trim on a swapped take is the trap 1742c10 hit.
            ("show", 8, 34),
            // bighit displays for at most 0.26 s: the state is View-driven
            // knockback only (ActorView.cs — _knockbackTime's two writers are
            // BossSlamKnockbackTime 0.26 / ComboKnockbackTime 0.18; the sim
            // never emits ActorAction.BigHit), and PoseValueForClip returns
            // -1 so nothing retimes. A trim can therefore choose WHICH ~6
            // frames of the take are ever seen. Abdominal Hit Fall's most
            // expressive span is the impact recoil — hips-height curve
            // (mesh-gen/hips-height.json): rise to 1.18 at f8-10, crumple
            // from f22, FIRST floor contact f28 (0.33), final floor f50+.
            // 4..11 shows exactly that recoil and, as a guard, sits 17
            // frames clear of the earliest floor contact even if the
            // knockback window ever grows.
            ("bighit", 4, 11),
            // die must fit ENEMY death, not the player's: enemies despawn
            // after SimConfig.EnemyFade = 0.34 s (8 f) of shrink-fade, so an
            // untrimmed 4.67 s Shot In The Back showed 7.3% — all standing
            // preamble, no collapse (the Mutant Roaring defect, worse ratio).
            // Hips-height curve (mesh-gen/hips-height.json): stand ~1.10 to
            // f34, collapse f36-70, floor f72+. 62..70 is the final fall
            // (0.40 -> 0.20), 8 f = 0.333 s <= EnemyFade, so the whole
            // kneel-to-prone arc lands inside the fade window. The player's
            // death holds the prone last frame under the GameOver panel.
            ("die", 62, 70),
            // cast re-measured for Mage Cast (swing-windows.json): strike
            // 27..34 = 7 f = 0.292 s = CastPoseDuration fit, speed 1.
            ("cast", 27, 34),
        };

        /// <summary>Index of the first View-only substate — everything below is
        /// an <see cref="ActorAction"/> the sim can emit.</summary>
        internal const int SimActionCount = 11;
        internal static string ActionNameAt(int index) => Clips[index].action;
        internal static int ClipCount => Clips.Length;
        /// <summary>Source take behind an action. ClipWindowProbe reads this so
        /// the clip table stays the single source of truth — a probe with its
        /// own copy of the list measures whatever the table used to say.</summary>
        internal static string ClipFileAt(int index) => Clips[index].file;

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
            // Toon first: the 12 shipped character materials moved to
            // CinderCourt/ToonLit (시안 02 stage 4, commit 892768d) by editing
            // the serialized assets, and this generator recreates those assets
            // on reimport — left on URP/Lit it would silently revert the whole
            // cast to PBR the next time any character FBX is touched. Lit stays
            // as the degrade path (same fallback the shader itself declares).
            var urpLit = Shader.Find("CinderCourt/ToonLit")
                ?? Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) { Debug.LogWarning("no character shader available"); return; }
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
            foreach (var (action, file, loop) in Clips)
            {
                var path = $"{ClipDir}/{file}.fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                if (importer == null)
                    throw new InvalidOperationException($"missing clip fbx {path}");
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                // The .meta OUTLIVES the .fbx. Replacing a take with one built
                // on a different rig leaves the previous avatar's explicit
                // bone map behind, and CreateFromThisModel does NOT discard it
                // — Unity keeps resolving human bones against names the new
                // skeleton does not have ("Transform 'Spine1' for human bone
                // 'Neck' not found"), fails the avatar, and emits NO clip at
                // all. MEASURED 2026-08-10: every re-rigged take imported to
                // zero clips while its FBX carried 249 fcurves, and the four
                // tests that caught it reported "no imported clip" and "hand
                // moves 0.0" — never "bad avatar". Clearing both arrays forces
                // the auto-mapper to read the rig that is actually there.
                var human = importer.humanDescription;
                human.human = Array.Empty<HumanBone>();
                human.skeleton = Array.Empty<SkeletonBone>();
                importer.humanDescription = human;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                // defaultClipAnimations is DERIVED FROM THE LAST IMPORT, not from
                // the file on disk. Replacing an FBX while its meta still held a
                // foreign avatar produced an import with zero takes, and reading
                // the property here then reported "no animation takes" about a
                // file that carries 249 fcurves. Settings above only reach the
                // importer on SaveAndReimport — so reimport first, then ask.
                importer.SaveAndReimport();
                var takes = importer.defaultClipAnimations;
                if (takes.Length == 0)
                    throw new InvalidOperationException($"no animation takes in {path}");
                var take = takes[0];
                take.name = action;
                // Trim BEFORE the loop/lock flags: the range is what defines the
                // clip's length, and every flag below is relative to it.
                foreach (var (trimmed, firstFrame, lastFrame) in ClipTrims)
                {
                    if (!string.Equals(trimmed, action, StringComparison.Ordinal)) continue;
                    if (lastFrame > take.lastFrame)
                        throw new InvalidOperationException(
                            $"clip '{action}' trim {firstFrame}..{lastFrame} exceeds the " +
                            $"source take (0..{take.lastFrame}) in {path}");
                    take.firstFrame = firstFrame;
                    take.lastFrame = lastFrame;
                }
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
