// Does a swing clip actually SWING?
//
// ClipWindowProbe measures the hand's LINEAR speed, which is what a trim
// window needs — it answers "when does the motion happen". It cannot answer
// "what kind of motion", and that gap shipped a defect: cycle-11 replaced
// three weapon swings with boxing punches (Punch_Combo_1 / Right_Upper_Hook /
// Punch_Combo_5). A punch has real hand speed, so every linear measurement
// stayed green while the character — who carries a sword bound to
// RightHand (ActorView.cs:390-392) — stopped describing an arc.
//
// The discriminating quantity is ANGULAR: how far the hand sweeps AROUND the
// shoulder, in degrees, over the clip. A jab travels out and back along one
// ray (small sweep, large radial change); a swing carries the hand across an
// arc (large sweep).
//
// Writes _workspace/current/engineering/swing-arcs.json.
//   Unity -batchmode -quit -executeMethod CinderCourt.EditorTools.SwingArcProbe.Run
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    public static class SwingArcProbe
    {
        const string MotionDir = "Assets/Art/Motion";
        // Tracked, not _workspace: ClipTrimFitTests reads this file, and a
        // measurement a test depends on has to travel with the repo. In
        // _workspace it would be absent on a fresh checkout and the arc guard
        // would go red for an environment reason — which is how a guard gets
        // deleted instead of investigated.
        const string OutPath = "docs/provenance/swing-arcs.json";
        const string RigResource = "Characters/human-command-boss";
        const float SampleStep = 1f / 60f;

        /// <summary>Takes to measure: whatever the clip table currently names for
        /// the swing actions, plus the Mixamo takes they displaced as controls.
        /// Read from the pipeline so this cannot drift like the old probe did.</summary>
        static readonly string[] Controls =
        {
            "Standing Melee Attack Horizontal",
            "Hook Punch",
            "Standing Melee Combo Attack Ver. 2",
            "Illegal Elbow Punch",
        };
        static readonly string[] SwingActions = { "attack", "attack2", "attack3", "critical" };

        public static void Run()
        {
            var prefab = Resources.Load<GameObject>(RigResource);
            if (prefab == null)
            {
                Debug.LogError($"[SwingArcProbe] rig missing: Resources/{RigResource}");
                EditorApplication.Exit(2);
                return;
            }
            var rig = Object.Instantiate(prefab);
            var animator = rig.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogError("[SwingArcProbe] rig is not humanoid");
                Object.DestroyImmediate(rig);
                EditorApplication.Exit(2);
                return;
            }

            var shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (shoulder == null || hand == null || hips == null)
            {
                Debug.LogError("[SwingArcProbe] rig is missing shoulder/hand/hips");
                Object.DestroyImmediate(rig);
                EditorApplication.Exit(2);
                return;
            }

            var names = new List<string>();
            foreach (var action in SwingActions)
            {
                var file = ClipFileFor(action);
                if (file != null && !names.Contains(file)) names.Add(file);
            }
            foreach (var c in Controls)
                if (!names.Contains(c)) names.Add(c);

            var rows = new List<string>();
            var report = new StringBuilder("[SwingArcProbe]\n");
            report.Append($"  {"clip",-36}{"arcDeg",8}{"reach",8}{"ratio",8}\n");
            foreach (var name in names)
            {
                var clip = LoadClip($"{MotionDir}/{name}.fbx");
                if (clip == null) continue;
                var m = Measure(clip, rig, shoulder, hand, hips);
                report.Append($"  {name,-36}{m.arcDegrees,8:F1}{m.reachDelta,8:F3}"
                              + $"{m.arcPerReach,8:F1}\n");
                rows.Add("{\"clip\":\"" + name + "\",\"arcDegrees\":"
                    + m.arcDegrees.ToString("F3", CultureInfo.InvariantCulture)
                    + ",\"reachDelta\":"
                    + m.reachDelta.ToString("F4", CultureInfo.InvariantCulture)
                    + ",\"arcPerReach\":"
                    + m.arcPerReach.ToString("F3", CultureInfo.InvariantCulture) + "}");
            }
            Object.DestroyImmediate(rig);

            Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
            File.WriteAllText(OutPath,
                "{\n  \"method\": \"hand angle about the right shoulder, sampled at "
                + "1/60 s on the humanoid rig; arcDegrees is the total swept angle, "
                + "reachDelta the max change in shoulder-to-hand distance. A jab "
                + "moves along one ray (low arc, high reach); a swing crosses an "
                + "arc (high arc).\",\n  \"clips\": [\n    "
                + string.Join(",\n    ", rows) + "\n  ]\n}\n");
            Debug.Log(report.ToString());
            EditorApplication.Exit(0);
        }

        struct Arc
        {
            public float arcDegrees;
            public float reachDelta;
            public float arcPerReach;
        }

        static Arc Measure(AnimationClip clip, GameObject rig,
                           Transform shoulder, Transform hand, Transform hips)
        {
            var swept = 0f;
            var minReach = float.MaxValue;
            var maxReach = 0f;
            Vector3 previous = Vector3.zero;
            var have = false;

            for (var t = 0f; t <= clip.length; t += SampleStep)
            {
                clip.SampleAnimation(rig, t);
                // Hand position RELATIVE to the shoulder, in the hips' frame, so
                // the body turning does not read as a swing.
                var local = hips.InverseTransformPoint(hand.position)
                            - hips.InverseTransformPoint(shoulder.position);
                var reach = local.magnitude;
                if (reach < 1e-4f) continue;
                minReach = Mathf.Min(minReach, reach);
                maxReach = Mathf.Max(maxReach, reach);
                var dir = local / reach;
                if (have) swept += Vector3.Angle(previous, dir);
                previous = dir;
                have = true;
            }

            var reachDelta = maxReach - (minReach == float.MaxValue ? 0f : minReach);
            return new Arc
            {
                arcDegrees = swept,
                reachDelta = reachDelta,
                // Degrees of sweep per unit of in-and-out travel. A jab is
                // dominated by reach; a swing by angle.
                arcPerReach = reachDelta > 1e-4f ? swept / reachDelta : swept,
            };
        }

        static string ClipFileFor(string action)
        {
            for (var i = 0; i < CharacterImportPipeline.ClipCount; i++)
                if (CharacterImportPipeline.ActionNameAt(i) == action)
                    return CharacterImportPipeline.ClipFileAt(i);
            return null;
        }

        static AnimationClip LoadClip(string path)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip c && !c.name.StartsWith("__preview"))
                    return c;
            return null;
        }
    }
}
