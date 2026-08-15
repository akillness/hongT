// Measures where a motion clip's READABLE moment actually is, so ClipTrims
// rows are measured rather than guessed.
//
// FIRST ATTEMPT FAILED, and the failure is worth keeping written down: sampling
// onto a synthetic transform hierarchy built from GetCurveBindings returned
// peak frame 0 and a full-length window for ALL SEVEN clips — a metric that
// gives the same answer for every input is dead (CLAUDE.md §4m), and it would
// have "measured" trims that were pure noise. Cause: these are HUMANOID rigs.
// Their curves are muscle values addressed through an Avatar, not TRS curves on
// named bone paths, so GetCurveBindings yields muscle bindings that no plain
// GameObject can receive. The fix is to sample onto a REAL character prefab
// with its Animator/Avatar, which is what the shipped characters already are.
//
// Batch entry:
//   Unity -batchmode -quit -executeMethod CinderCourt.EditorTools.ClipWindowProbe.Run
// Writes _workspace/current/engineering/clip-windows.json.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    public static class ClipWindowProbe
    {
        const string MotionDir = "Assets/Art/Motion";
        const string OutPath = "_workspace/current/engineering/clip-windows.json";
        // Any humanoid character works as the measuring rig — the question is
        // where the MOTION peaks, and retargeting maps the same clip onto every
        // one of them. The player mesh is the honest choice: it is the body the
        // player actually watches perform these clips.
        const string RigResource = "Characters/human-command-boss";

        /// <summary>Takes no longer in the clip table, kept as controls: a new
        /// measurement means more read against the performance it displaced
        /// than in isolation. Drop a row once its comparison is spent.</summary>
        static readonly string[] DisplacedControls =
        {
            "Standing Melee Attack Horizontal",  // was attack, until 2026-08-10
            "Body Block",                        // was defence
            "Receive Uppercut To The Face",      // was bighit
            "Illegal Elbow Punch",               // was critical
            "Hook Punch",                        // was attack2
            "Standing Melee Combo Attack Ver. 2",// was attack3
        };

        /// <summary>Every take the pipeline actually imports, plus the controls.
        ///
        /// Read from CharacterImportPipeline rather than copied: this probe
        /// previously carried its own list and silently kept measuring six
        /// takes the table had already replaced, which is exactly the drift a
        /// trim row cannot survive.</summary>
        static string[] BuildTargets()
        {
            var names = new List<string>();
            for (var i = 0; i < CharacterImportPipeline.ClipCount; i++)
                names.Add(CharacterImportPipeline.ClipFileAt(i));
            foreach (var control in DisplacedControls)
                if (!names.Contains(control)) names.Add(control);
            return names.ToArray();
        }

        public static void Run()
        {
            var prefab = Resources.Load<GameObject>(RigResource);
            if (prefab == null)
            {
                Debug.LogError($"[ClipWindowProbe] rig missing: Resources/{RigResource}");
                EditorApplication.Exit(1);
                return;
            }

            var sb = new StringBuilder();
            sb.Append("{\n  \"measuredAt\": \"")
              .Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
              .Append("\",\n  \"rig\": \"").Append(RigResource)
              .Append("\",\n  \"method\": \"AnimationClip.SampleAnimation at 1/60 s onto the ")
              .Append("humanoid rig, tracking the fastest-moving hand relative to the hips. ")
              .Append("Peak frame = the readable moment; window = the contiguous span above ")
              .Append("45% of peak speed. Frames are reported in the clip's own frame rate.\",\n")
              .Append("  \"clips\": [\n");

            var first = true;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var animator = instance.GetComponentInChildren<Animator>();
                if (animator == null || animator.avatar == null || !animator.isHuman)
                {
                    Debug.LogError("[ClipWindowProbe] rig is not a humanoid Animator — "
                        + "SampleAnimation cannot retarget muscle curves without one");
                    EditorApplication.Exit(1);
                    return;
                }
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var hands = new List<Transform>();
                foreach (var bone in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
                {
                    var t = animator.GetBoneTransform(bone);
                    if (t != null) hands.Add(t);
                }
                if (hips == null || hands.Count == 0)
                {
                    Debug.LogError("[ClipWindowProbe] rig exposes no hips/hands");
                    EditorApplication.Exit(1);
                    return;
                }

                foreach (var name in BuildTargets())
                {
                    var path = $"{MotionDir}/{name}.fbx";
                    var clip = LoadClip(path);
                    if (clip == null)
                    {
                        Debug.LogWarning($"[ClipWindowProbe] no clip in {path}");
                        continue;
                    }
                    var m = Measure(clip, instance, hips, hands);
                    if (!first) sb.Append(",\n");
                    first = false;
                    sb.Append("    {\"file\": \"").Append(name)
                      .Append("\", \"frameRate\": ").Append(F(clip.frameRate))
                      .Append(", \"lengthSeconds\": ").Append(F(clip.length))
                      .Append(", \"frames\": ").Append(m.TotalFrames)
                      .Append(", \"peakFrame\": ").Append(m.PeakFrame)
                      .Append(", \"peakSpeed\": ").Append(F(m.PeakSpeed))
                      .Append(", \"windowFirstFrame\": ").Append(m.FirstFrame)
                      .Append(", \"windowLastFrame\": ").Append(m.LastFrame)
                      .Append(", \"windowSeconds\": ").Append(F(m.WindowSeconds))
                      .Append(", \"preambleFraction\": ").Append(F(m.PreambleFraction))
                      .Append(", \"speedSpread\": ").Append(F(m.SpeedSpread))
                      .Append("}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            sb.Append("\n  ]\n}\n");
            Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
            File.WriteAllText(OutPath, sb.ToString());
            Debug.Log($"[ClipWindowProbe] wrote {OutPath}");
            EditorApplication.Exit(0);
        }

        static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);

        static AnimationClip LoadClip(string path)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;
            return null;
        }

        struct Result
        {
            public int TotalFrames, PeakFrame, FirstFrame, LastFrame;
            public float PeakSpeed, WindowSeconds, PreambleFraction, SpeedSpread;
        }

        static Result Measure(AnimationClip clip, GameObject rig,
                              Transform hips, List<Transform> hands)
        {
            const float fps = 60f;
            var steps = Mathf.Max(2, Mathf.RoundToInt(clip.length * fps));
            var speeds = new float[steps];
            var prev = new Vector3[hands.Count];

            for (var i = 0; i < steps; i++)
            {
                clip.SampleAnimation(rig, i / fps);
                var best = 0f;
                for (var h = 0; h < hands.Count; h++)
                {
                    // Hip-relative so a clip with root motion does not read its
                    // own travel as hand speed.
                    var local = hands[h].position - hips.position;
                    if (i > 0) best = Mathf.Max(best, (local - prev[h]).magnitude * fps);
                    prev[h] = local;
                }
                speeds[i] = best;
            }

            var peak = 0; var peakSpeed = 0f; var minSpeed = float.MaxValue;
            for (var i = 1; i < steps; i++)   // i=0 has no delta
            {
                if (speeds[i] > peakSpeed) { peakSpeed = speeds[i]; peak = i; }
                if (speeds[i] < minSpeed) minSpeed = speeds[i];
            }

            var gate = peakSpeed * 0.45f;
            var lo = peak; while (lo > 1 && speeds[lo - 1] >= gate) lo--;
            var hi = peak; while (hi < steps - 1 && speeds[hi + 1] >= gate) hi++;

            var scale = clip.frameRate / fps;
            return new Result
            {
                TotalFrames = Mathf.RoundToInt(clip.length * clip.frameRate),
                PeakFrame = Mathf.RoundToInt(peak * scale),
                FirstFrame = Mathf.RoundToInt(lo * scale),
                LastFrame = Mathf.RoundToInt(hi * scale),
                PeakSpeed = peakSpeed,
                WindowSeconds = (hi - lo + 1) / fps,
                PreambleFraction = clip.length > 0f ? (lo / fps) / clip.length : 0f,
                // Guards against the dead-metric failure that killed the first
                // probe: if peak == min the sampler is not seeing motion at all.
                SpeedSpread = peakSpeed - (minSpeed == float.MaxValue ? 0f : minSpeed),
            };
        }
    }
}
