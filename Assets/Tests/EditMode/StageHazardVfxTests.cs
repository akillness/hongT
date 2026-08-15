using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CinderCourt.Sim;
using CinderCourt.View;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class StageHazardVfxTests
    {
        readonly System.Collections.Generic.List<GameObject> _roots =
            new System.Collections.Generic.List<GameObject>();

        bool _hadReducedMotionPref;
        int _reducedMotionPrefValue;

        [SetUp]
        public void SetUp()
        {
            _hadReducedMotionPref = PlayerPrefs.HasKey("al:reduced-motion");
            _reducedMotionPrefValue = PlayerPrefs.GetInt("al:reduced-motion");
            ViewPrefs.ReducedMotion = false;
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _roots.Count; i++)
                if (_roots[i] != null) UnityEngine.Object.DestroyImmediate(_roots[i]);
            _roots.Clear();

            ViewPrefs.ReducedMotion = _reducedMotionPrefValue == 1;
            if (_hadReducedMotionPref)
                PlayerPrefs.SetInt("al:reduced-motion", _reducedMotionPrefValue);
            else
                PlayerPrefs.DeleteKey("al:reduced-motion");
            PlayerPrefs.Save();
        }

        [Test]
        public void SetStageContext_IsAdditiveAndGameViewWiresRunLifecycle()
        {
            var method = typeof(VfxDirector).GetMethod(
                "SetStageContext", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null,
                "VfxDirector must expose an additive stage-context setter; "
                + "SyncHazards and HazardState stay unchanged.");
            Assert.That(method.GetParameters(), Has.Length.EqualTo(1));
            Assert.That(method.GetParameters()[0].ParameterType, Is.EqualTo(typeof(string)));

            var gameView = Source("Assets/Scripts/View/GameView.cs");
            AssertOrder(gameView,
                "_logicalStageId = logicalStageId ?? string.Empty;",
                "Vfx.SetStageContext(_logicalStageId)",
                "GameView must publish the logical stage only after assigning it.");
            var endRun = Slice(gameView, "public void EndRun()", "void OnDisable()");
            Assert.That(endRun,
                Does.Match("Vfx\\s*!=\\s*null\\)\\s*Vfx\\.SetStageContext\\((null|string\\.Empty|\"\")\\)"),
                "EndRun must clear VfxDirector's stage context; ClearTransient alone only "
                + "destroys spawned views.");
        }

        [Test]
        public void HazardView_AddsPhysicalSurfaceWithoutReplacingStateSlots()
        {
            var hazardView = typeof(VfxDirector).GetNestedType(
                "HazardView", BindingFlags.NonPublic);
            Assert.That(hazardView, Is.Not.Null, "VfxDirector.HazardView must remain nested.");

            Assert.That(hazardView.GetField("Root"), Is.Not.Null);
            Assert.That(hazardView.GetField("Ring"), Is.Not.Null);
            Assert.That(hazardView.GetField("FillDisc"), Is.Not.Null);
            Assert.That(hazardView.GetField("Body"), Is.Not.Null);
            Assert.That(hazardView.GetField("Aux"), Is.Not.Null);
            Assert.That(hazardView.GetField("Edge"), Is.Not.Null);

            Assert.That(hazardView.GetField("Surface"), Is.Not.Null,
                "Stage textures need a separate physical surface layer below Ring/Fill/Body/Aux/Edge.");
            Assert.That(hazardView.GetField("SurfaceMaterial"), Is.Not.Null,
                "The physical surface needs its own material so state alpha/color animation "
                + "does not mutate the stage texture.");
        }

        [Test]
        public void UnknownStageContext_KeepsLegacyVentFallbackRenderable()
        {
            var director = NewDirector();
            SetStageContext(director, "no-such-stage");

            director.SyncHazards(new[]
            {
                new HazardState
                {
                    Kind = HazardKind.EmberVent,
                    X = 640f,
                    Y = 480f,
                    Radius = CampaignSpec.VentRadius,
                    CycleT = 0.4f,
                },
            });

            var view = FirstHazardView(director);
            Assert.That(Field<Renderer>(view, "Ring"), Is.Not.Null,
                "unknown/non-campaign stages must keep the existing vent ring fallback.");
            Assert.That(Field<Transform>(view, "FillDisc"), Is.Not.Null,
                "unknown/non-campaign stages must keep the existing imminence fill fallback.");
            Assert.That(Field<Transform>(view, "Surface"), Is.Null,
                "unknown/non-campaign stages must not create a stale stage-specific surface.");
        }

        [Test]
        public void TideCurrent_PreservesExactJudgedAspectAndStateLayers()
        {
            var director = NewDirector();
            director.SyncHazards(new[]
            {
                new HazardState
                {
                    Kind = HazardKind.TideCurrent,
                    X = SimConfig.ArenaX,
                    Y = SimConfig.ArenaY,
                    HalfW = CampaignSpec.CurrentHalfW,
                    HalfH = CampaignSpec.CurrentHalfH,
                    Active = true,
                },
            });

            var view = FirstHazardView(director);
            var body = Field<Transform>(view, "Body");
            var edge = Field<Transform>(view, "Edge");
            var aux = Field<Transform>(view, "Aux");

            Assert.That(body, Is.Not.Null, "current bed must remain a physical band.");
            Assert.That(edge, Is.Not.Null, "current edge telegraph must remain above the bed.");
            Assert.That(aux, Is.Not.Null, "current chevrons must remain above the bed.");
            Assert.That(body.localScale.x,
                Is.EqualTo(CampaignSpec.CurrentHalfW * 2f * ViewWorld.Scale).Within(0.0001f));
            Assert.That(body.localScale.y,
                Is.EqualTo(CampaignSpec.CurrentHalfH * 2f * ViewWorld.Scale).Within(0.0001f),
                "current is the one rectangular hazard; its Y scale is not iso-squashed.");
        }

        [Test]
        public void EmberPylon_StageTextureCoversAuraAndBodyWithoutReplacingHpBand()
        {
            var director = NewDirector();
            SetStageContext(director, "ash-verdict");
            director.SyncHazards(new[]
            {
                new HazardState
                {
                    Kind = HazardKind.EmberPylon,
                    X = SimConfig.ArenaX,
                    Y = SimConfig.ArenaY,
                    Radius = CampaignSpec.PylonBodyRadius,
                    Hp = CampaignSpec.PylonHp,
                },
            });

            var view = FirstHazardView(director);
            var body = Field<Transform>(view, "Body");
            var coreSurface = body.Find("PylonBodySurface");
            var surfaceMaterial = Field<Material>(view, "SurfaceMaterial");

            Assert.That(Field<Transform>(view, "Surface"), Is.Not.Null,
                "the generated pylon underlay must form the opaque aura below its state ring.");
            Assert.That(coreSurface, Is.Not.Null,
                "the same stage resource needs a top-facing body albedo without wrapping floor marks around the core.");
            Assert.That(coreSurface.GetComponent<Renderer>().sharedMaterial,
                Is.SameAs(surfaceMaterial));
            Assert.That(Field<Transform>(view, "Aux"), Is.Not.Null,
                "the existing HP band remains the canonical pylon state read above the physical texture.");
            Assert.That(Field<Material>(view, "AuxMaterial"), Is.Not.SameAs(surfaceMaterial),
                "stage albedo must not replace or tint the HP band material.");
        }

        [Test]
        public void AshWall_ReducedMotionKeepsOpaqueSurfaceAndBoundaryOnly()
        {
            ViewPrefs.ReducedMotion = true;
            var director = NewDirector();
            SetStageContext(director, "ash-march");
            var depth = 220f;
            var hazards = new[]
            {
                new HazardState
                {
                    Kind = HazardKind.AshWall,
                    X = CampaignSpec.WallEdgeX,
                    Y = SimConfig.ArenaY,
                    Radius = CampaignSpec.StoneWallRadius,
                    Active = true,
                    Telegraphing = true,
                    FrontX = CampaignSpec.WallEdgeX + depth,
                },
                new HazardState
                {
                    Kind = HazardKind.AshWall,
                    X = CampaignSpec.WallEdgeRightX,
                    Y = SimConfig.ArenaY,
                    Radius = CampaignSpec.StoneWallRadius,
                    Active = true,
                    Telegraphing = true,
                    FrontX = CampaignSpec.WallEdgeRightX - depth,
                },
            };

            director.SyncHazards(hazards);
            director.SyncHazards(hazards);

            var expectedScale = depth / CampaignSpec.WallDepthMax;
            for (var i = 0; i < hazards.Length; i++)
            {
                var view = HazardViewAt(director, i, hazards.Length);
                var surface = Field<Transform>(view, "Surface");
                var surfaceRenderer = Field<Renderer>(view, "SurfaceRenderer");
                var block = new MaterialPropertyBlock();
                surfaceRenderer.GetPropertyBlock(block);
                var st = block.GetVector(Shader.PropertyToID("_BaseMap_ST"));

                Assert.That(surface, Is.Not.Null,
                    "campaign AshWall needs an opaque stage surface below its semantic layers.");
                Assert.That(surface.gameObject.activeSelf, Is.True,
                    "reduced motion must keep the opaque swallowed floor cover visible.");
                Assert.That(surface.localScale.x,
                    Is.EqualTo(depth * ViewWorld.Scale).Within(0.0001f));
                Assert.That(st.x, Is.EqualTo(expectedScale).Within(0.0001f),
                    "AshWall must reveal only the travelled fraction of the max-depth source.");
                Assert.That(st.z,
                    Is.EqualTo(i == 0 ? 0f : 1f - expectedScale).Within(0.0001f),
                    "left and right walls must sample from their respective authored source edge.");
                Assert.That(Field<Transform>(view, "Edge").gameObject.activeSelf, Is.True,
                    "reduced motion still needs a stable wall boundary read.");
                Assert.That(Field<Transform>(view, "Body").gameObject.activeSelf, Is.False,
                    "reduced motion must hide the animated translucent swallowed-band layer.");
                Assert.That(Field<Transform>(view, "Aux").gameObject.activeSelf, Is.False,
                    "reduced motion must hide the animated wall curtain.");
            }
        }

        [Test]
        public void AshWall_UsesFixedDensityUvCropAndMirrorsRightOrigin()
        {
            var source = Source("Assets/Scripts/View/VfxDirector.cs");
            var ashWallCase = Slice(source, "case HazardKind.AshWall:", "break;");

            Assert.That(ashWallCase, Does.Contain("BaseMapStId"),
                "AshWall band texture must crop/reveal at fixed texel density instead of "
                + "stretching a square texture to the current wall depth.");
            Assert.That(ashWallCase, Does.Contain("AshWallMaxDepthWorld"),
                "AshWall UV scale must be normalized by the authored maximum swallow depth.");
            Assert.That(ashWallCase, Does.Not.Contain("SurfaceRepeatSt"),
                "the swallowed band is one max-depth source crop; repeating it would create false secondary fronts.");
            Assert.That(ashWallCase, Does.Match(@"(?s)fromRight.*BaseMapStId|BaseMapStId.*fromRight"),
                "right-origin AshWall must mirror the fixed-density UV offset; otherwise "
                + "one side samples the wrong edge of the generated band.");
        }

        [Test]
        public void StageSurfaceMaterials_AreCachedPerBindingNotPerHazardInstance()
        {
            var source = Source("Assets/Scripts/View/VfxDirector.cs");
            Assert.That(source,
                Does.Match(@"Dictionary<[^;]*(Surface|Hazard|Material)[^;]*>\s+_[A-Za-z0-9_]*surface[A-Za-z0-9_]*Material")
                    .Or.Match(@"StageHazardTextureResolver"),
                "stage physical surfaces must be cached per binding/context. A new material "
                + "for every repeated vent or wall segment is a WebGL memory regression.");
        }

        VfxDirector NewDirector()
        {
            var root = new GameObject(nameof(StageHazardVfxTests));
            _roots.Add(root);
            return root.AddComponent<VfxDirector>();
        }

        static void SetStageContext(VfxDirector director, string stageId)
        {
            var method = typeof(VfxDirector).GetMethod(
                "SetStageContext", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "VfxDirector.SetStageContext(string) is required.");
            method.Invoke(director, new object[] { stageId });
        }

        static object FirstHazardView(VfxDirector director)
            => HazardViewAt(director, 0, 1);

        static object HazardViewAt(VfxDirector director, int index, int expectedCount)
        {
            var field = typeof(VfxDirector).GetField(
                "_hazardViews", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "VfxDirector must keep run-scoped hazard views.");
            var views = (Array)field.GetValue(director);
            Assert.That(views, Is.Not.Null.And.Length.EqualTo(expectedCount));
            return views.GetValue(index);
        }

        static T Field<T>(object owner, string name) where T : class
        {
            var field = owner.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, owner.GetType().Name + "." + name + " missing");
            return field.GetValue(owner) as T;
        }

        static string Source(string relativePath)
            => File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));

        static string Slice(string source, string startToken, string endToken)
        {
            var start = source.IndexOf(startToken, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), startToken + " missing");
            var end = source.IndexOf(endToken, start + startToken.Length, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), endToken + " missing after " + startToken);
            return source.Substring(start, end - start);
        }

        static void AssertOrder(string source, string earlier, string later, string message)
        {
            var a = source.IndexOf(earlier, StringComparison.Ordinal);
            var b = source.IndexOf(later, StringComparison.Ordinal);
            Assert.That(a, Is.GreaterThanOrEqualTo(0), earlier + " missing");
            Assert.That(b, Is.GreaterThanOrEqualTo(0), later + " missing");
            Assert.That(a, Is.LessThan(b), message);
        }
    }
}
