// W16 terrain flipbook — the three contracts that can silently rot.
//
// 1. PLACEMENT. The decal must land on ambient ground slabs and never on the
//    rock furniture that rings a hazard. Both families share the "env-floor-"
//    pivot prefix, so the discriminator is the CHILD kind, and that is a fact
//    about EnvironmentBuilder that no compiler checks.
//
// 2. TINT SOURCE. EnvironmentBuilder keeps one WHITE shared floor material and
//    puts the stage colour in a per-renderer MaterialPropertyBlock, then ends
//    Build with StaticBatchingUtility.Combine. If either of those changes — or
//    if combining drops property blocks — the theme decision silently collapses
//    every stage onto one sheet. These tests read the tint back off a REAL
//    built stage rather than recomputing it, so they fail loudly instead.
//
// 3. FRAME WINDOW. A flipbook that mis-maps _BaseMap_ST samples the wrong cell
//    or bleeds two cells at once; the arithmetic is worth pinning exactly.
//
// EditMode only: build → inspect → DestroyImmediate.
using NUnit.Framework;
using UnityEngine;
using CinderCourt.View;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class TerrainFlipbookTests
    {
        // Expected sheet family per stage, derived from each stage's accent
        // (StageCatalog) through EnvironmentBuilder's floor tint. Listed as data
        // rather than recomputed so a palette change has to be acknowledged here.
        static readonly (string Stage, TerrainFlipbook.Theme Theme)[] StageThemes =
        {
            ("cinder-span",    TerrainFlipbook.Theme.Lava),   // #F25A2B ember
            ("ember-gallery",  TerrainFlipbook.Theme.Lava),   // #F26E33 ember
            ("abyss-chancel",  TerrainFlipbook.Theme.Ice),    // #8F66FF violet
            ("witness-well",   TerrainFlipbook.Theme.Ice),    // #73C7FF cyan
            ("echo-throne",    TerrainFlipbook.Theme.Ice),    // #73C7FF cyan
            ("ash-verdict",    TerrainFlipbook.Theme.Lava),   // #DEC768 gold
            ("cinder-sluice",  TerrainFlipbook.Theme.Ice),    // #3FA8C8 teal
            ("ember-bastion",  TerrainFlipbook.Theme.Lava),   // #E88A2E orange
            ("ash-march",      TerrainFlipbook.Theme.Shift),  // #B8B0A4 grey
        };

        // ------------------------------------------------------ placement ----

        [Test]
        public void Placement_SelectsAmbientSlabsAndRejectsHazardFurniture()
        {
            // Synthetic hierarchy: the three shapes EnvironmentBuilder emits
            // that this rule has to tell apart.
            var root = new GameObject("StageEnvironment");
            try
            {
                var slab = Child(root, "env-floor-000");
                var slabPiece = Child(slab, "piece-00");
                slabPiece.AddComponent<MeshFilter>();
                slabPiece.AddComponent<MeshRenderer>().sharedMaterial =
                    ViewWorld.MakeUnlit(Color.white, false);

                // Hazard-ring furniture: SAME pivot prefix, cloned library mesh.
                var furniture = Child(root, "env-floor-500");
                var part = Child(furniture, "part-00");
                part.AddComponent<MeshFilter>();
                part.AddComponent<MeshRenderer>().sharedMaterial =
                    ViewWorld.MakeUnlit(Color.white, false);

                // A wall is not floor at all.
                var wall = Child(root, "env-wall-000");
                var wallPiece = Child(wall, "piece-00");
                wallPiece.AddComponent<MeshFilter>();
                wallPiece.AddComponent<MeshRenderer>().sharedMaterial =
                    ViewWorld.MakeUnlit(Color.white, false);

                Assert.That(TerrainFlipbook.PanelOf(slab.transform), Is.Not.Null,
                    "an ambient floor slab is the one surface this layer decorates");
                Assert.That(TerrainFlipbook.PanelOf(furniture.transform), Is.Null,
                    "hazard-ring furniture shares the env-floor- prefix and must "
                    + "still be rejected — decorating it would put animated ember "
                    + "on the rocks that frame a vent telegraph");
                Assert.That(TerrainFlipbook.PanelOf(wall.transform), Is.Null,
                    "only floor pivots qualify");
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ----------------------------------------------------- tint source ---

        [Test]
        public void TintSource_SurvivesTheRealBuildIncludingStaticBatching()
        {
            var block = new MaterialPropertyBlock();
            foreach (var (stageId, expected) in StageThemes)
            {
                var root = EnvironmentBuilder.Build(stageId);
                Assert.That(root, Is.Not.Null, $"{stageId} must build");
                try
                {
                    var found = 0;
                    var theme = TerrainFlipbook.Theme.None;
                    for (var i = 0; i < root.transform.childCount; i++)
                    {
                        var panel = TerrainFlipbook.PanelOf(root.transform.GetChild(i));
                        if (panel == null) continue;
                        if (!TerrainFlipbook.TryFloorTint(panel, block, out var tint))
                            continue;
                        found++;
                        var panelTheme = TerrainFlipbook.ThemeForFloorTint(tint);
                        if (found == 1) theme = panelTheme;
                        Assert.That(panelTheme, Is.EqualTo(theme),
                            $"{stageId}: every ambient panel carries the same "
                            + "stage tint, so they must all resolve to one theme");
                    }

                    // Most layouts emit 6..10 ambient panels. cinder-sluice is
                    // the measured exception: its hazard table is dense enough
                    // that NearAnyHazard filters EVERY ambient slab, so the
                    // stage builds hazard furniture (part-) only and the layer
                    // is a strict no-op there — the safe fallback this
                    // component promises. Zero is only a failure when ambient
                    // slabs exist but none carried a readable tint.
                    if (found == 0)
                    {
                        // Guard only against AMBIENT slabs (piece- children of
                        // env-floor- hosts, the exact PanelOf discriminator) —
                        // other builder zones reuse the piece- prefix for
                        // non-floor geometry, which is not this layer's canvas.
                        var anyPiece = false;
                        for (var i = 0; i < root.transform.childCount && !anyPiece; i++)
                        {
                            var host = root.transform.GetChild(i);
                            if (!host.name.StartsWith("env-floor-")) continue;
                            for (var c = 0; c < host.childCount; c++)
                                if (host.GetChild(c).name.StartsWith("piece-")) { anyPiece = true; break; }
                        }
                        Assert.That(anyPiece, Is.False,
                            $"{stageId}: ambient slabs exist but no panel yielded "
                            + "a readable stage tint. Either EnvironmentBuilder "
                            + "stopped carrying the floor colour in a "
                            + "MaterialPropertyBlock, or "
                            + "StaticBatchingUtility.Combine dropped it.");
                        continue;
                    }
                    Assert.That(theme, Is.EqualTo(expected),
                        $"{stageId} sheet family");
                }
                finally { Object.DestroyImmediate(root); }
            }
        }

        [Test]
        public void ThemeBand_KeepsEveryStageClearOfTheNeutralEdge()
        {
            // A stage sitting ON the band edge would flip theme from a rounding
            // change in the palette. Measure the margin, do not assume it.
            var block = new MaterialPropertyBlock();
            foreach (var (stageId, expected) in StageThemes)
            {
                var root = EnvironmentBuilder.Build(stageId);
                try
                {
                    Color tint = default;
                    var readable = false;
                    for (var i = 0; i < root.transform.childCount; i++)
                    {
                        var panel = TerrainFlipbook.PanelOf(root.transform.GetChild(i));
                        if (panel != null
                            && TerrainFlipbook.TryFloorTint(panel, block, out tint))
                        {
                            readable = true;
                            break;
                        }
                    }
                    // No ambient slab (cinder-sluice, see the tint-source
                    // fixture) -> no decal -> no theme to keep clear of the
                    // band. The structural guard lives in that fixture.
                    if (!readable) continue;
                    var warmth = tint.r - tint.b;
                    var band = TerrainFlipbook.NeutralBand;
                    if (expected == TerrainFlipbook.Theme.Shift)
                        Assert.That(Mathf.Abs(warmth), Is.LessThan(band * 0.5f),
                            $"{stageId} is the neutral stage and must sit well "
                            + "inside the band, not on its edge");
                    else
                        Assert.That(Mathf.Abs(warmth), Is.GreaterThan(band * 1.5f),
                            $"{stageId} must clear the neutral band with margin");
                }
                finally { Object.DestroyImmediate(root); }
            }
        }

        // ---------------------------------------------------- frame window ---

        [Test]
        public void FrameWindow_TilesTheSheetExactlyOnceWithNoOverlap()
        {
            var seen = new bool[TerrainFlipbook.FrameCount];
            for (var frame = 0; frame < TerrainFlipbook.FrameCount; frame++)
            {
                var st = TerrainFlipbook.FrameSt(frame);
                Assert.That(st.x, Is.EqualTo(1f / TerrainFlipbook.GridCols).Within(1e-6f),
                    "u tiling must be exactly one cell wide");
                Assert.That(st.y, Is.EqualTo(1f / TerrainFlipbook.GridRows).Within(1e-6f),
                    "v tiling must be exactly one cell tall");
                // The window must sit fully inside the sheet: an offset past
                // 1 - tiling samples the wrap and bleeds a neighbouring frame.
                Assert.That(st.z, Is.InRange(0f, 1f - st.x + 1e-6f));
                Assert.That(st.w, Is.InRange(0f, 1f - st.y + 1e-6f));

                var col = Mathf.RoundToInt(st.z * TerrainFlipbook.GridCols);
                var row = Mathf.RoundToInt((1f - st.w) * TerrainFlipbook.GridRows) - 1;
                var cell = row * TerrainFlipbook.GridCols + col;
                Assert.That(cell, Is.InRange(0, TerrainFlipbook.FrameCount - 1));
                Assert.That(seen[cell], Is.False,
                    $"frame {frame} reuses a cell another frame already claimed");
                seen[cell] = true;
            }
            foreach (var used in seen)
                Assert.That(used, Is.True, "every sheet cell must be played");
        }

        [Test]
        public void FrameWindow_FrameZeroIsTheTopLeftCell()
        {
            // Sheets are authored top-left first, but UV origin is bottom-left —
            // getting this backwards plays the sheet upside down and nothing but
            // an eyeball would notice.
            var st = TerrainFlipbook.FrameSt(0);
            Assert.That(st.z, Is.EqualTo(0f).Within(1e-6f), "frame 0 sits at u = 0");
            Assert.That(st.w,
                Is.EqualTo(1f - 1f / TerrainFlipbook.GridRows).Within(1e-6f),
                "frame 0 sits on the TOP row, i.e. the highest v window");
        }

        // ---------------------------------------------------------- sheets ---

        [Test]
        public void Sheets_EveryPlayableThemeHasItsOwnDistinctPath()
        {
            Assert.That(TerrainFlipbook.SheetPath(TerrainFlipbook.Theme.None), Is.Null,
                "None must resolve to no sheet, which is what makes the "
                + "missing-asset path a complete no-op");
            var lava = TerrainFlipbook.SheetPath(TerrainFlipbook.Theme.Lava);
            var ice = TerrainFlipbook.SheetPath(TerrainFlipbook.Theme.Ice);
            var shift = TerrainFlipbook.SheetPath(TerrainFlipbook.Theme.Shift);
            Assert.That(lava, Is.Not.Null.And.Not.EqualTo(ice).And.Not.EqualTo(shift));
            Assert.That(ice, Is.Not.Null.And.Not.EqualTo(shift));
        }

        [Test]
        public void Tint_HoldsAmbientAlphaBelowTheTelegraphBand()
        {
            // These decals share the floor with hazard telegraphs whose
            // ring/fill alphas run 0.5..1.0. Ambient ground texture that climbs
            // into that band stops being background and starts competing with
            // the one signal the player cannot afford to miss.
            var floor = new Color(0.45f, 0.30f, 0.23f, 1f);
            foreach (var theme in new[]
                     {
                         TerrainFlipbook.Theme.Lava,
                         TerrainFlipbook.Theme.Ice,
                         TerrainFlipbook.Theme.Shift,
                     })
            {
                var tint = TerrainFlipbook.DecalTint(floor, theme);
                Assert.That(tint.a, Is.LessThanOrEqualTo(0.35f),
                    $"{theme} decal alpha must stay in the ambient band");
                Assert.That(tint.a, Is.GreaterThan(0f),
                    $"{theme} decal must actually be visible");
            }
        }

        static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }
    }
}
