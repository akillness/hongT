// Does a generated clip FBX satisfy Unity's humanoid mapper with NO bone
// rename?
//
// This is the instrument behind the deletion of a 22-entry BONE_MAP from
// tools/blender/clip_from_glb.py. Measured 2026-08-10: the Higgsfield
// 3d_rigging rig is Mixamo-named (LeftUpLeg / LeftForeArm / Spine02 /
// lower-case `neck`) and Unity's auto-mapper accepts it untouched —
//     CONTROL shipped Hook Punch  : isHuman True, 15/15
//     GENERATED Punch Combo 1     : isHuman True, 15/15
// so the rename pass was dead code. The clip path in CharacterImportPipeline
// has no rename step either (:291-292); the rename comment near the top of
// that file is about CHARACTERS, via reskin_character.py.
//
// Kept in the tree because clip_from_glb.py and
// docs/provenance/motion-generated.json both cite this measurement. A citation
// whose instrument has been deleted is not a measurement, it is a claim.
//
//   Unity -batchmode -quit -executeMethod CinderCourt.EditorTools.ClipAvatarProbe.Run
//
// Candidates are read from /tmp/clipfbx (where clip_from_glb.py writes), so the
// probe is inert until someone has actually converted one.
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    public static class ClipAvatarProbe
    {
        const string StageDir = "Assets/_ClipProof";
        const string CandidateDir = "/tmp/clipfbx";
        const string Control = "Assets/Art/Motion/Unarmed Idle.fbx";

        static readonly string[] RequiredHumanBones =
        {
            "Hips", "Spine", "Head",
            "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightUpperArm", "RightLowerArm", "RightHand",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
            "RightUpperLeg", "RightLowerLeg", "RightFoot",
        };

        public static void Run()
        {
            var sb = new StringBuilder("[ClipAvatarProbe]\n");
            Directory.CreateDirectory(StageDir);

            var subjects = new List<(string label, string source, string staged)>
            {
                ("CONTROL shipped Mixamo clip", Control, null),
            };
            if (Directory.Exists(CandidateDir))
                foreach (var fbx in Directory.GetFiles(CandidateDir, "*.fbx"))
                    subjects.Add(($"GENERATED {Path.GetFileNameWithoutExtension(fbx)}",
                                  fbx, $"{StageDir}/{Path.GetFileName(fbx)}"));
            else
                sb.Append($"  (no candidates: {CandidateDir} does not exist — run "
                          + "tools/blender/clip_from_glb.py first)\n");

            var allHuman = true;
            foreach (var (label, source, staged) in subjects)
            {
                var path = staged ?? source;
                if (staged != null)
                {
                    File.Copy(source, staged, overwrite: true);
                    AssetDatabase.ImportAsset(staged, ImportAssetOptions.ForceUpdate);
                }

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    sb.Append($"  {label}: no ModelImporter at {path}\n");
                    allHuman = false;
                    continue;
                }
                // Exactly what ReimportClips does to every shipped clip.
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.SaveAndReimport();

                Avatar avatar = null;
                var clips = new List<AnimationClip>();
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Avatar a) avatar = a;
                    if (asset is AnimationClip c && !c.name.StartsWith("__preview"))
                        clips.Add(c);
                }

                var mapped = new HashSet<string>();
                var desc = importer.humanDescription;
                if (desc.human != null)
                    foreach (var hb in desc.human)
                        mapped.Add(hb.humanName.Replace(" ", string.Empty));
                var absent = new List<string>();
                foreach (var name in RequiredHumanBones)
                    if (!mapped.Contains(name)) absent.Add(name);

                var human = avatar != null && avatar.isValid && avatar.isHuman;
                if (!human || clips.Count == 0) allHuman = false;

                sb.Append($"  --- {label}\n");
                sb.Append($"      isHuman  : {human}\n");
                sb.Append($"      required : {RequiredHumanBones.Length - absent.Count}"
                          + $"/{RequiredHumanBones.Length}"
                          + (absent.Count == 0 ? "" : "  MISSING " + string.Join(", ", absent))
                          + "\n");
                sb.Append($"      clips    : {clips.Count}");
                foreach (var c in clips)
                    sb.Append($"  '{c.name}' {c.length:F2}s {c.frameRate:F0}fps");
                sb.Append("\n");
            }

            Debug.Log(sb.ToString());
            AssetDatabase.DeleteAsset(StageDir);
            AssetDatabase.Refresh();
            EditorApplication.Exit(allHuman ? 0 : 2);
        }
    }
}
