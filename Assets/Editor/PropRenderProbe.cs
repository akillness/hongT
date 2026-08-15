// Renders each equip prop alone, large, and measures whether its surface
// actually varies — the question a dungeon frame cannot answer.
//
// WHY. At the shipped camera a 0.42 m dagger is roughly 40 px, and at 40 px a
// textured prop and a flat-tinted one are the same handful of pixels. That is
// an honest statement about the game, but it makes an in-game capture unable to
// decide whether the material work landed: a flat-looking prop there might be a
// broken sheet, or might be a correct sheet with nowhere to show. Those are
// different bugs and one of them is not a bug.
//
// So render the prefab by itself into a RenderTexture at a size where the
// surface is resolvable, and report the luminance spread over the prop's own
// pixels. This isolates the MATERIAL question from the CAMERA question. It is
// deliberately not a pass/fail gate — it is a measurement whose number belongs
// in the report next to the in-game frame.
//
//   Unity -batchmode -executeMethod CinderCourt.EditorTools.PropRenderProbe.RenderAll
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    public static class PropRenderProbe
    {
        const string PrefabDir = "Assets/Resources/Props";
        const string OutDir = "_workspace/current/qa/prop-render";
        const int Size = 512;

        [MenuItem("CinderCourt/Probe Prop Rendering")]
        public static void RenderAll()
        {
            // Every exit path must reach EditorApplication.Exit, including the
            // throws. -executeMethod does NOT imply -quit, so an uncaught
            // exception leaves the editor idling with the project locked until
            // something kills it — the guard below would then trade a misleading
            // artifact for a silent stall, which is not an improvement.
            try
            {
                // A null graphics device renders nothing, and ReadPixels returns
                // a uniform buffer. Measured 2026-08-13 under the batch wrapper's
                // -nographics: every prop reported mean 0.804, spread 0.000, over
                // all 262144 pixels — the magenta clear masked NOTHING, so not one
                // prop pixel existed. That output is worse than none: a flat dark
                // render is indistinguishable from the near-black defect this
                // pipeline exists to catch, and would be read as evidence of it.
                if (SystemInfo.graphicsDeviceType
                    == UnityEngine.Rendering.GraphicsDeviceType.Null)
                    throw new InvalidOperationException(
                        "PropRenderProbe needs a real graphics device — use "
                        + "`unity_batch.sh method-gfx`, not `method`. A null device "
                        + "yields a uniform buffer that reads like the near-black "
                        + "prop defect.");
                Directory.CreateDirectory(OutDir);
                var rows = new List<string>();
                foreach (var path in Directory.GetFiles(PrefabDir, "*.prefab").OrderBy(p => p))
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;
                    rows.Add(RenderOne(prefab, name));
                }
                File.WriteAllText($"{OutDir}/report.txt", string.Join("\n", rows) + "\n");
                Debug.Log($"[PropRenderProbe]\n{string.Join("\n", rows)}");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PropRenderProbe] FAILED: {e}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        static string RenderOne(GameObject prefab, string name)
        {
            var instance = UnityEngine.Object.Instantiate(prefab);
            var cameraObject = new GameObject("probe-camera");
            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1,
            };
            // A key light, because CinderToonLit bands on N.L: with no light the
            // whole prop collapses to the shadow floor and every sheet measures
            // flat for a reason that has nothing to do with the sheet.
            var lightObject = new GameObject("probe-light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            lightObject.transform.rotation = Quaternion.Euler(38f, -140f, 0f);

            try
            {
                var bounds = new Bounds(instance.transform.position, Vector3.zero);
                var any = false;
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    if (!any) { bounds = renderer.bounds; any = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                if (!any) return $"{name,-30} NO-RENDERER";

                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                // Magenta background: nothing in this palette is magenta, so the
                // prop mask below is exact rather than a threshold guess.
                camera.backgroundColor = new Color(1f, 0f, 1f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = bounds.extents.magnitude * 1.05f;
                camera.transform.position = bounds.center
                    + new Vector3(0.6f, 0.45f, -1f).normalized * (bounds.extents.magnitude * 4f);
                camera.transform.LookAt(bounds.center);
                camera.targetTexture = rt;
                camera.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var shot = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                shot.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                shot.Apply();
                RenderTexture.active = previous;

                File.WriteAllBytes($"{OutDir}/{name}.png", shot.EncodeToPNG());

                var lums = new List<float>();
                double sumR = 0, sumG = 0, sumB = 0;
                foreach (var p in shot.GetPixels())
                {
                    // Exclude the magenta clear and its antialiased fringe.
                    if (p.r > 0.75f && p.b > 0.75f && p.g < 0.25f) continue;
                    lums.Add(0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b);
                    sumR += p.r; sumG += p.g; sumB += p.b;
                }
                UnityEngine.Object.DestroyImmediate(shot);
                if (lums.Count < 64) return $"{name,-30} TOO-FEW-PIXELS {lums.Count}";

                lums.Sort();
                var p05 = lums[(int)(lums.Count * 0.05f)];
                var p95 = lums[Math.Min(lums.Count - 1, (int)(lums.Count * 0.95f))];
                var mean = lums.Average();
                // WARMTH (R-G), not just luminance. The band signature is an
                // emission colour, so the fine/basic difference is chiefly a HUE
                // shift; a luminance-only readout under-reports it and would make
                // a legible rank look marginal (weapons separate 4.4x in warmth
                // and only 1.06x in mean).
                //
                // SIGN IS PER-FAMILY, NOT GLOBAL. Weapons and the cloak glow
                // ember (0.95,0.35,0.17), so rank reads WARMER. The lantern glows
                // cyan (0.17,0.68,0.84), so its rank reads COOLER and warmth goes
                // DOWN with band — measured basic +0.060 -> fine +0.030. That is
                // the correct direction for a cyan lamp, not a regression; read
                // the lantern on B-R and the others on R-G.
                var r = sumR / lums.Count;
                var g = sumG / lums.Count;
                var b = sumB / lums.Count;
                return $"{name,-30} px={lums.Count,6} mean={mean:F3} "
                     + $"p05={p05:F3} p95={p95:F3} spread={p95 - p05:F3} "
                     + $"rgb=({r:F3},{g:F3},{b:F3}) warmth={r - g:+0.000;-0.000}";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(lightObject);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }
    }
}
