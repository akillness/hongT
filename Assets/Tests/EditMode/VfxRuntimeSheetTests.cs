using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CinderCourt.Sim;
using CinderCourt.View;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class VfxRuntimeSheetTests
    {
        readonly List<GameObject> _roots = new List<GameObject>();
        readonly List<Texture2D> _textures = new List<Texture2D>();
        readonly Dictionary<string, object> _statics = new Dictionary<string, object>();
        readonly HashSet<GameObject> _preexisting = new HashSet<GameObject>();

        bool _hadReducedMotionPref;
        int _reducedMotionPrefValue;

        [SetUp]
        public void SetUp()
        {
            _hadReducedMotionPref = PlayerPrefs.HasKey("al:reduced-motion");
            _reducedMotionPrefValue = PlayerPrefs.GetInt("al:reduced-motion");
            ViewPrefs.ReducedMotion = false;

            _preexisting.Clear();
            foreach (var existing in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include))
                _preexisting.Add(existing);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var pair in _statics)
                StaticField(pair.Key).SetValue(null, pair.Value);
            _statics.Clear();

            for (var i = 0; i < _roots.Count; i++)
                if (_roots[i] != null) Object.DestroyImmediate(_roots[i]);
            _roots.Clear();

            foreach (var live in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include))
                if (live != null && live.transform.parent == null
                    && !_preexisting.Contains(live))
                    Object.DestroyImmediate(live);
            _preexisting.Clear();

            for (var i = 0; i < _textures.Count; i++)
                if (_textures[i] != null) Object.DestroyImmediate(_textures[i]);
            _textures.Clear();

            ViewPrefs.ReducedMotion = _reducedMotionPrefValue == 1;
            if (_hadReducedMotionPref)
                PlayerPrefs.SetInt("al:reduced-motion", _reducedMotionPrefValue);
            else
                PlayerPrefs.DeleteKey("al:reduced-motion");
            PlayerPrefs.Save();
        }

        [Test]
        public void FxResourceShapeGate_AcceptsOnlyFourByFourFlipbooksForSheets()
        {
            Assert.That(VfxDirector.IsFxFlipbookShape(Texture(64, 64)), Is.True);
            Assert.That(VfxDirector.IsFxFlipbookShape(Texture(64, 32)), Is.False,
                "4x4 sheets must have square cells; otherwise _BaseMap_ST frames crop the wrong region");
            Assert.That(VfxDirector.IsFxFlipbookShape(Texture(62, 62)), Is.False,
                "non-divisible dimensions cannot be split into the 4x4 runtime frame grid");

            Assert.That(VfxDirector.IsFxMaskShape(Texture(63, 31)), Is.True,
                "single-mask assets are not atlases; any positive texture shape is valid");
            Assert.That(VfxDirector.IsFxMaskShape(null), Is.False);
        }

        [Test]
        public void VentTelegraphSheet_PreservesRingTintAndStepsFrameFromCycle()
        {
            InjectTexture("_telegraphRingSheet", "_telegraphRingSheetProbed", Texture(64, 64));
            ViewPrefs.ReducedMotion = true;
            var director = NewDirector();

            director.SyncHazards(new[]
            {
                new HazardState
                {
                    Kind = HazardKind.EmberVent,
                    X = 640f,
                    Y = 480f,
                    Radius = CampaignSpec.VentRadius,
                    CycleT = CampaignSpec.VentPeriod * 0.5f,
                    Telegraphing = true,
                },
            });

            var root = director.transform.Find("Hazard-EmberVent");
            Assert.That(root, Is.Not.Null);
            var telegraph = root.Find("VentTelegraphQuad");
            Assert.That(telegraph, Is.Not.Null,
                "the optional telegraph-ring sheet must add a quad without removing the legacy vent disc");

            var ringColor = root.GetComponentInChildren<Renderer>().sharedMaterial.color;
            var telegraphMaterial = telegraph.GetComponent<Renderer>().sharedMaterial;
            AssertColor(telegraphMaterial.color, ringColor,
                "the sheet is a grayscale mask; identity must remain the per-call vent tint");
            Assert.That(telegraphMaterial.GetVector("_BaseMap_ST"),
                Is.EqualTo(TerrainFlipbook.FrameSt(7)),
                "reduced-motion warning must hold on authored frame 7, the brightest total-luminance frame");
            Assert.That(telegraphMaterial.color.a, Is.EqualTo(1f),
                "reduced motion holds the warning steady at peak alpha, not at a dim trough");
        }

        [Test]
        public void VentTelegraphReducedMotion_HoldsBrightestFrameAcrossCycleSamples()
        {
            InjectTexture("_telegraphRingSheet", "_telegraphRingSheetProbed", Texture(64, 64));
            ViewPrefs.ReducedMotion = true;
            var director = NewDirector();

            var first = SyncVentAndReadTelegraphSt(director, CampaignSpec.VentPeriod * 0.15f);
            var second = SyncVentAndReadTelegraphSt(director, CampaignSpec.VentPeriod * 0.85f);

            Assert.That(first, Is.EqualTo(TerrainFlipbook.FrameSt(7)));
            Assert.That(second, Is.EqualTo(first),
                "reduced motion must not animate the sheet when CycleT changes");
        }

        [Test]
        public void VentTelegraphNormalMotion_AdvancesFrameFromCycle()
        {
            InjectTexture("_telegraphRingSheet", "_telegraphRingSheetProbed", Texture(64, 64));
            ViewPrefs.ReducedMotion = false;
            var director = NewDirector();

            var first = SyncVentAndReadTelegraphSt(director, CampaignSpec.VentPeriod * 0.15f);
            var second = SyncVentAndReadTelegraphSt(director, CampaignSpec.VentPeriod * 0.85f);

            Assert.That(first, Is.Not.EqualTo(second),
                "normal-motion telegraph playback must still advance from CycleT");
            Assert.That(first, Is.EqualTo(VfxDirector.FxFrameSt(0.15f)));
            Assert.That(second, Is.EqualTo(VfxDirector.FxFrameSt(0.85f)));
        }

        [Test]
        public void ShardStreakTexture_UsesVelocityAlignedQuad_AndClearTransientReleasesIt()
        {
            var mask = Texture(37, 19);
            InjectTexture("_shardStreakMask", "_shardStreakMaskProbed", mask);
            var director = NewDirector();

            InvokeSpawnShard(director, new Color(0.62f, 0.95f, 1f, 0.85f),
                new Vector3(-1f, 0f, 0.18f));

            var quad = FindChild(director.transform, "ShardStreakQuad");
            Assert.That(quad, Is.Not.Null);
            Assert.That(quad.gameObject.activeSelf, Is.True);
            var material = quad.GetComponent<Renderer>().sharedMaterial;
            Assert.That(material.mainTexture, Is.SameAs(mask));
            AssertColor(material.color, new Color(0.62f, 0.95f, 1f, 0.85f),
                "the dash streak mask must not carry identity; tint stays per call");

            director.ClearTransient();
            Assert.That(quad.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ShardStreakMissing_FallsBackToLegacyLineShard()
        {
            InjectTexture("_shardStreakMask", "_shardStreakMaskProbed", null);
            var director = NewDirector();

            InvokeSpawnShard(director, new Color(0.62f, 0.95f, 1f, 0.85f),
                new Vector3(-1f, 0f, 0.18f));

            var line = FindLine(director, "Shard");
            Assert.That(line, Is.Not.Null);
            Assert.That(line.enabled, Is.True,
                "a missing optional mask must leave the shipped line-shard fallback alive");
            Assert.That(FindChild(director.transform, "ShardStreakQuad"), Is.Null);
        }

        [Test]
        public void StaticShardMask_ReusedAfterEruption_ResetBaseMapStToFullTexture()
        {
            InjectTexture("_eruptionSheet", "_eruptionSheetProbed", Texture(64, 64));
            InjectTexture("_shardStreakMask", "_shardStreakMaskProbed", Texture(37, 19));
            var director = NewDirector();

            InvokeSpawnEruptionCrown(director);
            var crown = FindChild(director.transform, "EruptionCrownQuad");
            Assert.That(crown, Is.Not.Null);
            var material = crown.GetComponent<Renderer>().sharedMaterial;
            Assert.That(material.GetVector("_BaseMap_ST"), Is.EqualTo(TerrainFlipbook.FrameSt(0)));

            SetShardCursor(director, 0);
            InvokeSpawnShard(director, new Color(0.62f, 0.95f, 1f, 0.85f),
                new Vector3(-1f, 0f, 0.18f));

            var reused = FindChild(director.transform, "ShardStreakQuad");
            Assert.That(reused, Is.SameAs(crown),
                "the one-slot reuse path must be covered: this regression is about stale ST on a reused material");
            Assert.That(material.GetVector("_BaseMap_ST"), Is.EqualTo(VfxDirector.FullTextureSt),
                "static masks must reset _BaseMap_ST to the full texture after a flipbook used the slot");
        }

        [Test]
        public void EruptionSheet_SpawnsOneCenteredAuthoredCrown_AndClearTransientReleasesIt()
        {
            InjectTexture("_eruptionSheet", "_eruptionSheetProbed", Texture(64, 64));
            var director = NewDirector();

            InvokeSpawnEruptionCrown(director);

            var crowns = FindChildren(director.transform, "EruptionCrownQuad");
            Assert.That(crowns.Count, Is.EqualTo(1),
                "the authored eruption sheet is a whole crown, not one sprite per legacy shard");
            var crown = crowns[0];
            Assert.That(Vector3.Distance(crown.position, ViewWorld.ToWorld(640f, 480f, 0.05f)),
                Is.LessThan(0.0001f), "the authored crown must be centered on the cast, not placed around the rim");
            Assert.That(crown.localScale.x,
                Is.EqualTo(190f * ViewWorld.Scale * 2f).Within(0.0001f),
                "crown width comes from the real radiusSim footprint");
            Assert.That(crown.localScale.y, Is.EqualTo(0f).Within(0.0001f),
                "the crown starts collapsed and StepShardPool extends it over life");

            StepShards(director, 0.1f);
            Assert.That(crown.localScale.x,
                Is.EqualTo(190f * ViewWorld.Scale * 2f).Within(0.0001f),
                "StepShardPool must preserve stored Length as the authored crown width");
            Assert.That(crown.localScale.y, Is.GreaterThan(0.5f));

            director.ClearTransient();
            Assert.That(crown.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void WaveWarnings_UseShockwaveSheetWhenAvailable_AndClearTransientReleasesQuads()
        {
            InjectTexture("_shockwaveSheet", "_shockwaveSheetProbed", Texture(64, 64));
            var director = NewDirector();

            director.SpawnWaveWarnings(2, boss: false);

            var quad = FindChild(director.transform, "WaveWarningQuad");
            Assert.That(quad, Is.Not.Null);
            Assert.That(quad.gameObject.activeSelf, Is.True);
            var line = FindLine(director, "WaveWarning");
            Assert.That(line, Is.Not.Null);
            Assert.That(line.enabled, Is.False,
                "the textured warning owns the same pool slot; the ring is the fallback, not a second effect");

            director.ClearTransient();
            Assert.That(quad.gameObject.activeSelf, Is.False);
        }

        VfxDirector NewDirector()
        {
            var root = new GameObject(nameof(VfxRuntimeSheetTests));
            _roots.Add(root);
            var director = root.AddComponent<VfxDirector>();
            typeof(VfxDirector).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(director, null);
            return director;
        }

        Texture2D Texture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            _textures.Add(texture);
            return texture;
        }

        Vector4 SyncVentAndReadTelegraphSt(VfxDirector director, float cycleT)
        {
            director.SyncHazards(new[]
            {
                new HazardState
                {
                    Kind = HazardKind.EmberVent,
                    X = 640f,
                    Y = 480f,
                    Radius = CampaignSpec.VentRadius,
                    CycleT = cycleT,
                    Telegraphing = true,
                },
            });
            var telegraph = FindChild(director.transform, "VentTelegraphQuad");
            Assert.That(telegraph, Is.Not.Null);
            return telegraph.GetComponent<Renderer>().sharedMaterial.GetVector("_BaseMap_ST");
        }

        void InjectTexture(string textureField, string probedField, Texture2D texture)
        {
            SetStatic(textureField, texture);
            SetStatic(probedField, true);
        }

        void SetStatic(string name, object value)
        {
            var field = StaticField(name);
            if (!_statics.ContainsKey(name)) _statics.Add(name, field.GetValue(null));
            field.SetValue(null, value);
        }

        static FieldInfo StaticField(string name)
        {
            var field = typeof(VfxDirector).GetField(name,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"VfxDirector.{name} is gone");
            return field;
        }

        static void InvokeSpawnShard(VfxDirector director, Color color, Vector3 direction)
        {
            var spawn = typeof(VfxDirector).GetMethod("SpawnShard",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(spawn, Is.Not.Null);
            spawn.Invoke(director, new object[]
            {
                640f, 480f, color, 0.9f, 0.22f, direction, 0f,
            });
        }

        static void InvokeSpawnEruptionCrown(VfxDirector director)
        {
            var spawn = typeof(VfxDirector).GetMethod("SpawnEruptionCrown",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(spawn, Is.Not.Null);
            spawn.Invoke(director, new object[]
            {
                640f, 480f, new Color(0.42f, 0.95f, 0.62f, 0.9f),
                190f, 1.15f, 0.75f, 10,
            });
        }

        static void StepShards(VfxDirector director, float deltaTime)
        {
            var field = typeof(VfxDirector).GetField("_shards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var step = typeof(VfxDirector).GetMethod("StepShardPool",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(step, Is.Not.Null);
            step.Invoke(null, new[] { field.GetValue(director), deltaTime });
        }

        static void SetShardCursor(VfxDirector director, int cursor)
        {
            var field = typeof(VfxDirector).GetField("_shardCursor",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(director, cursor);
        }

        static Transform FindChild(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        static List<Transform> FindChildren(Transform root, string name)
        {
            var found = new List<Transform>();
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) found.Add(child);
            return found;
        }

        static LineRenderer FindLine(VfxDirector director, string name)
        {
            foreach (var line in director.GetComponentsInChildren<LineRenderer>(true))
                if (line.gameObject.name == name) return line;
            return null;
        }

        static void AssertColor(Color actual, Color expected, string reason)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f), reason);
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f), reason);
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f), reason);
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f), reason);
        }
    }
}
