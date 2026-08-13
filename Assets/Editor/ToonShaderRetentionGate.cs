// Build-time proof that CinderCourt/ToonLit survives WebGL shader stripping.
//
// WHY THIS EXISTS. Every other check in this repo can only infer the answer:
//
//   * EditMode tests always have the shader, so no EditMode assertion can see a
//     build-time strip. EquipPropTests says so explicitly.
//   * `material.shader.name` is invariant under a ShaderLab Fallback
//     (CinderToonLit.shader declares `Fallback "Universal Render Pipeline/Lit"`),
//     so even a strict name assertion reads "CinderCourt/ToonLit" while the
//     renderer has quietly fallen back to Lit. An earlier test was named for
//     stripping and was structurally incapable of observing it.
//   * A browser capture observes the CONSEQUENCE, by eye, after a full build and
//     deploy — and only if someone looks at the right pixels.
//
// IPreprocessShaders.OnProcessShader is the one place where variant retention is
// a FACT rather than an inference: the build calls it once per shader/snippet
// with the surviving variant list. Counting there turns "we looked at a
// screenshot" into a build-log artifact, and lets the build FAIL when the count
// is zero rather than shipping a silently PBR-shaded cast.
//
// WHAT THIS DOES NOT DO: it does not remove the Fallback. The fallback is
// runtime insurance for a player — better a wrong-looking prop than a magenta
// one. The two roles were collapsed into one mechanism that could only hide the
// answer; this separates them, so the fallback stays and the build-time hook is
// what notices whether it was ever needed.
using System.Collections.Generic;
using CinderCourt.View;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace CinderCourt.EditorTools
{
    public sealed class ToonShaderRetentionGate : IPreprocessShaders, IPreprocessBuildWithReport,
        IPostprocessBuildWithReport
    {
        /// <summary>Must run AFTER URP's own stripper (callbackOrder 0) so the
        /// count reflects what actually survives, not what existed before
        /// stripping. A higher number runs later.</summary>
        public int callbackOrder => 1000;

        static readonly Dictionary<string, int> Retained = new();

        public void OnPreprocessBuild(BuildReport report)
        {
            Retained.Clear();
            Debug.Log("[ToonShaderRetentionGate] armed");
        }

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet,
            IList<ShaderCompilerData> data)
        {
            if (shader == null || data.Count == 0) return;
            Retained.TryGetValue(shader.name, out var running);
            Retained[shader.name] = running + data.Count;
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            // (no state flag: OnProcessShader accumulates unconditionally, and
            // OnPreprocessBuild clears, so a stale count cannot survive a build.)
            // GUARD ON EVIDENCE, NOT ON report.summary.result. Measured
            // 2026-08-13: during OnPostprocessBuild the summary still reads
            // `Unknown` — this callback runs INSIDE BuildPipeline.BuildPlayer,
            // before the result is finalised. A `!= Succeeded` early-return
            // therefore skipped the assertion on every single build, and the
            // gate logged nothing while passing. It was inert for two builds
            // before an unconditional entry log exposed it.
            //
            // "Did any shader compile at all" is the property actually wanted:
            // a failed or cancelled build compiles none, and failing here would
            // bury the real error under a misleading one.
            if (Retained.Count == 0)
            {
                Debug.Log("[ToonShaderRetentionGate] no shaders compiled — "
                    + "build did not reach shader processing, skipping the check");
                return;
            }

            Retained.TryGetValue(ViewWorld.ToonLitShaderName, out var toon);
            var summary = $"[ToonShaderRetentionGate] {ViewWorld.ToonLitShaderName}: "
                + $"{toon} variants retained across {Retained.Count} shaders";
            if (toon <= 0)
            {
                // Hard failure. Zero retained variants means every ToonLit
                // renderer in the build — 12 props, the terrain set, the
                // characters, the env kit — silently renders through the
                // URP/Lit fallback: no banding, no outline, and prop rank
                // emission gone entirely because URP/Lit gates emission behind
                // an _EMISSION keyword none of these materials set.
                throw new BuildFailedException(
                    $"{ViewWorld.ToonLitShaderName} was stripped from this build "
                    + "(0 variants retained). Every toon surface would fall back to "
                    + "URP/Lit. Check that the materials are still reachable from a "
                    + "serialized asset under Resources, or add the shader to "
                    + "Graphics Settings > Always Included Shaders.");
            }
            Debug.Log(summary);
        }
    }
}
