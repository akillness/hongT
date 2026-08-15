// Does a reskin_from_fbx output satisfy Unity's humanoid mapper, and if not,
// WHICH bones does the auto-mapper refuse?
//
// This is the instrument behind two decisions that are otherwise unfalsifiable:
//
//  1. tools/blender/reskin_from_fbx.py FIT_TARGET defaults to "none" — scale
//     NOTHING. Measured 2026-08-10, all four rows with the import-time
//     transform bake held fixed:
//         scout mesh (1.6993) on guard rig (1.1257), nothing scaled
//             -> isHuman True, 22/22
//         same pair, mesh scaled DOWN to the rig   -> False, 20/22 (hands)
//         same pair, rig scaled UP to the mesh     -> False, 20/22 (hands)
//         generated mesh (2.6993) on scout rig, mesh down -> False, 14/22
//     A height disparity is fine; re-scaling geometry under an authored rest
//     pose is what the auto-mapper refuses.
//
//  2. That script's MAX_UNSCALED_RATIO ceiling of 1.55 sits between two
//     measured points (1.5095 passed, 1.5885 failed) and its comment says to
//     move it only by measuring another pair. This is how.
//
// Kept in the tree for that reason — a citation whose instrument has been
// deleted is not a measurement, it is a claim.
//
//   Unity -batchmode -quit -executeMethod CinderCourt.EditorTools.ReskinAvatarProbe.Run
//
// Subjects are read from /tmp/rp by default (where the reskin runs write), so
// the probe is inert until someone has actually produced a candidate.
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    public static class ReskinAvatarProbe
    {
        const string StageDir = "Assets/_ReskinProof";
        const string CandidateDir = "/tmp/rp";
        const string Control = "Assets/Art/Characters/guard.fbx";

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
            var sb = new StringBuilder("[ReskinAvatarProbe]\n");
            Directory.CreateDirectory(StageDir);

            var subjects = new List<(string label, string source, string staged)>
            {
                ("CONTROL shipped guard", Control, null),
            };
            if (Directory.Exists(CandidateDir))
                foreach (var fbx in Directory.GetFiles(CandidateDir, "*.fbx"))
                    subjects.Add(($"CANDIDATE {Path.GetFileNameWithoutExtension(fbx)}",
                                  fbx, $"{StageDir}/{Path.GetFileName(fbx)}"));
            else
                sb.Append($"  (no candidates: {CandidateDir} does not exist — run "
                          + "tools/blender/reskin_from_fbx.py first)\n");

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
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = false;
                importer.SaveAndReimport();

                Avatar avatar = null;
                GameObject root = null;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Avatar a) avatar = a;
                    if (asset is GameObject go && go.transform.parent == null) root = go;
                }

                // WHICH bones mapped matters more than how many: the auto-mapper
                // drops the ones whose rest pose it does not believe, and the
                // identity of those bones is the diagnosis.
                var mapped = new HashSet<string>();
                var desc = importer.humanDescription;
                if (desc.human != null)
                    foreach (var hb in desc.human)
                        mapped.Add(hb.humanName.Replace(" ", string.Empty));
                var absent = new List<string>();
                foreach (var name in RequiredHumanBones)
                    if (!mapped.Contains(name)) absent.Add(name);

                var human = avatar != null && avatar.isValid && avatar.isHuman;
                if (!human) allHuman = false;

                sb.Append($"  --- {label}\n");
                sb.Append($"      isHuman  : {human}\n");
                sb.Append($"      required : {RequiredHumanBones.Length - absent.Count}"
                          + $"/{RequiredHumanBones.Length}\n");
                sb.Append($"      NOT MAP  : "
                          + (absent.Count == 0 ? "(none)" : string.Join(", ", absent)) + "\n");
                var skin = root != null ? root.GetComponentInChildren<SkinnedMeshRenderer>() : null;
                if (skin?.sharedMesh != null)
                    sb.Append($"      mesh     : {skin.sharedMesh.triangles.Length / 3} tris, "
                              + $"{skin.bones.Length} bones, bounds={skin.bounds.size}\n");
            }

            Debug.Log(sb.ToString());
            AssetDatabase.DeleteAsset(StageDir);
            AssetDatabase.Refresh();
            EditorApplication.Exit(allHuman ? 0 : 2);
        }
    }
}
