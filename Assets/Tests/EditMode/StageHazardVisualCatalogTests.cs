using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class StageHazardVisualCatalogTests
    {
        const int ExpectedEffectivePairCount = 33;
        const string HazardResourceRoot = "Textures/Hazards/";

        static readonly IReadOnlyDictionary<HazardKind, string> KindTokens =
            new Dictionary<HazardKind, string>
            {
                { HazardKind.EmberVent, "ember-vent" },
                { HazardKind.ObsidianPillar, "obsidian-pillar" },
                { HazardKind.RelicAltar, "relic-altar" },
                { HazardKind.TideCurrent, "tide-current" },
                { HazardKind.EmberPylon, "ember-pylon" },
                { HazardKind.AshWall, "ash-wall" },
                { HazardKind.StoneWall, "stone-wall" },
            };

        static readonly IReadOnlyDictionary<HazardKind, string> PrimaryRoles =
            new Dictionary<HazardKind, string>
            {
                { HazardKind.EmberVent, "underlay" },
                { HazardKind.ObsidianPillar, "body" },
                { HazardKind.RelicAltar, "underlay" },
                { HazardKind.TideCurrent, "bed" },
                { HazardKind.EmberPylon, "underlay" },
                { HazardKind.AshWall, "band" },
                { HazardKind.StoneWall, "body" },
            };

        [Test]
        public void Bindings_CoverExactlyTheSourceDerivedCampaignStageHazardPairs()
        {
            var expected = SourceEffectivePairs();
            var actual = CatalogBindings()
                .Select(binding => new StageKind(
                    RequireString(binding, "StageId"),
                    RequireKind(binding, "Kind")))
                .ToHashSet();

            Assert.That(expected.Count, Is.EqualTo(ExpectedEffectivePairCount),
                "The runtime-composed campaign hazard surface set must stay at 33 stage/kind pairs, including generated #17 StoneWall.");
            Assert.That(actual, Is.EquivalentTo(expected),
                "StageHazardVisualCatalog must bind every actual campaign stage/kind and no stale or extra pair.");
        }

        [Test]
        public void Bindings_UseStableStageKindRolePathTokens()
        {
            foreach (var binding in CatalogBindings())
            {
                var stageId = RequireString(binding, "StageId");
                var kind = RequireKind(binding, "Kind");
                var role = RequireString(binding, "PrimaryRole", "Role");
                var resourcePath = RequireString(binding, "ResourcePath");
                var expectedRole = PrimaryRoles[kind];
                var expectedPath = HazardResourceRoot + stageId + "-" + KindTokens[kind] + "-" + expectedRole;

                Assert.That(role, Is.EqualTo(expectedRole), $"{stageId}/{kind}: primary role drift");
                Assert.That(resourcePath, Is.EqualTo(expectedPath),
                    $"{stageId}/{kind}: runtime path must be extensionless and match the generated asset filename tokens.");
                Assert.That(resourcePath, Does.Not.EndWith(".png"));
                Assert.That(resourcePath, Does.Not.Contain("//"));
            }
        }

        [Test]
        public void TryGetBinding_RejectsUnknownAndNonCampaignStageContexts()
        {
            var catalog = RequireType("CinderCourt.View.StageHazardVisualCatalog");
            var bindingType = RequireType("CinderCourt.View.HazardSurfaceBinding");
            var tryGet = catalog.GetMethod(
                "TryGetBinding",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(HazardKind), bindingType.MakeByRefType() },
                null);

            Assert.That(tryGet, Is.Not.Null,
                "StageHazardVisualCatalog.TryGetBinding(string, HazardKind, out HazardSurfaceBinding) is the fallback/rejection contract.");

            foreach (var stageId in new[] { null, "", "no-such-stage", "prologue", "arena", "training-vent" })
            {
                var args = new object[] { stageId, HazardKind.EmberVent, null };
                var found = (bool)tryGet.Invoke(null, args);
                Assert.That(found, Is.False, stageId ?? "<null>");
                Assert.That(args[2], Is.Null.Or.EqualTo(DefaultValue(bindingType)),
                    "Rejected contexts must not leak a previous campaign binding.");
            }
        }

        [Test]
        public void Resolver_RejectedBindingReturnsExplicitFallbackMiss()
        {
            var resolver = Activator.CreateInstance(RequireType("CinderCourt.View.StageHazardTextureResolver"));
            var resolve = resolver.GetType().GetMethod(
                "Resolve",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(string), typeof(HazardKind) },
                null);

            Assert.That(resolve, Is.Not.Null,
                "StageHazardTextureResolver.Resolve(string, HazardKind) must expose the safe-miss shape for VfxDirector.");

            var result = resolve.Invoke(resolver, new object[] { "no-such-stage", HazardKind.EmberVent });
            Assert.That(result, Is.Not.Null, "a rejected binding must be an explicit result, not an exception/null.");
            Assert.That(RequireBool(result, "Found", "Loaded", "HasTexture"), Is.False);
            Assert.That(OptionalObject(result, "Texture"), Is.Null);
            Assert.That(RequireBool(result, "IsFallback", "Fallback", "UsesFallback"), Is.True,
                "runtime may keep primitive rendering, but must label the miss as fallback.");
        }

        [Test]
        public void Manifest_ProvesExactRuntimeBindingsFileHashesAndConsumers()
        {
            var expected = CatalogBindings()
                .Select(binding => new StageKind(
                    RequireString(binding, "StageId"),
                    RequireKind(binding, "Kind")))
                .ToHashSet();
            Assert.That(expected.Count, Is.EqualTo(ExpectedEffectivePairCount),
                "StageHazardVisualCatalog bindings are the runtime consumer contract validated by the manifest.");

            var manifestPath = ProjectPath("docs/provenance/stage-hazard-textures.json");
            Assert.That(File.Exists(manifestPath), Is.True, "stage hazard texture provenance manifest is required.");

            var manifest = JsonUtility.FromJson<ManifestDocument>(File.ReadAllText(manifestPath));
            Assert.That(manifest, Is.Not.Null, "manifest JSON must deserialize into the validation contract.");
            Assert.That(manifest.required_asset_count, Is.EqualTo(ExpectedEffectivePairCount));
            Assert.That(manifest.assets, Is.Not.Null);
            Assert.That(manifest.assets.Length, Is.EqualTo(ExpectedEffectivePairCount),
                "manifest must contain exactly one generated asset per runtime stage/hazard binding.");

            var actual = new HashSet<StageKind>();
            var resourcePaths = CatalogBindings()
                .ToDictionary(
                    binding => new StageKind(RequireString(binding, "StageId"), RequireKind(binding, "Kind")),
                    binding => RequireString(binding, "ResourcePath"));

            foreach (var asset in manifest.assets)
            {
                Assert.That(asset.stage_id, Is.Not.Empty);
                Assert.That(asset.hazard_kind, Is.Not.Empty, asset.stage_id);
                Assert.That(asset.role, Is.Not.Empty, asset.stage_id + "/" + asset.hazard_kind);
                Assert.That(asset.output_path, Does.StartWith("Assets/Resources/Textures/Hazards/"),
                    asset.stage_id + "/" + asset.hazard_kind);
                Assert.That(asset.source_output, Does.StartWith("_workspace/current/engineering/hazard-texture-gen/source/"),
                    asset.stage_id + "/" + asset.hazard_kind);
                Assert.That(asset.runtime_consumer, Does.StartWith("VfxDirector."),
                    asset.stage_id + "/" + asset.hazard_kind + " must name the runtime surface consumer.");
                Assert.That(asset.validation_artifacts, Is.Not.Null,
                    asset.stage_id + "/" + asset.hazard_kind + " must list review/capture evidence.");
                Assert.That(asset.validation_artifacts.Length, Is.GreaterThan(0),
                    asset.stage_id + "/" + asset.hazard_kind + " is still pending visual acceptance evidence.");
                Assert.That(asset.decision, Is.EqualTo("accepted"),
                    asset.stage_id + "/" + asset.hazard_kind + " must be accepted only after GTI generation and review.");
                Assert.That(asset.sha256, Does.Match("^[0-9a-f]{64}$"),
                    asset.stage_id + "/" + asset.hazard_kind + " must record the generated PNG SHA-256.");
                Assert.That(asset.sha256, Is.Not.EqualTo(new string('0', 64)),
                    asset.stage_id + "/" + asset.hazard_kind + " still has the skeleton hash.");

                var kind = ParseHazardKind(asset.hazard_kind, asset.stage_id);
                var key = new StageKind(asset.stage_id, kind);
                Assert.That(actual.Add(key), Is.True, "duplicate manifest binding " + key);
                Assert.That(expected.Contains(key), Is.True, "manifest includes stale or non-runtime binding " + key);

                Assert.That(PrimaryRoles[kind], Is.EqualTo(asset.role), key + ": role drift");
                Assert.That(resourcePaths[key] + ".png", Is.EqualTo(ToResourceAssetPath(asset.output_path)),
                    key + ": output path must match the runtime extensionless Resources path.");
                Assert.That(asset.runtime_consumer, Is.EqualTo(ExpectedConsumer(kind)), key + ": runtime consumer drift");

                var outputPath = ProjectPath(asset.output_path);
                Assert.That(File.Exists(outputPath), Is.True, key + ": generated asset file is missing.");
                Assert.That(Sha256File(outputPath), Is.EqualTo(asset.sha256), key + ": manifest hash does not match PNG.");
                Assert.That(asset.dimensions, Is.EqualTo("512x512"), key + ": generated runtime texture size must stay WebGL-bounded.");
                Assert.That(asset.mode, Is.EqualTo("RGB"), key + ": generated runtime texture must be opaque RGB.");
            }

            Assert.That(actual, Is.EquivalentTo(expected),
                "manifest must reject missing, stale, or extra stage/hazard mappings.");
        }

        static HashSet<StageKind> SourceEffectivePairs()
        {
            var pairs = new HashSet<StageKind>();
            foreach (var entry in StageCatalog.Entries)
            {
                var hazards = StageCatalog.PactFor(entry.Id);
                Assert.That(hazards, Is.Not.Null, entry.Id);
                foreach (var hazard in hazards)
                    pairs.Add(new StageKind(entry.Id, hazard.Kind));
                pairs.Add(new StageKind(entry.Id, HazardKind.StoneWall));
            }
            return pairs;
        }

        static IEnumerable<object> CatalogBindings()
        {
            var type = RequireType("CinderCourt.View.StageHazardVisualCatalog");
            var member = type.GetMember("Bindings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault();
            Assert.That(member, Is.Not.Null,
                "StageHazardVisualCatalog must expose static Bindings for import/provenance validation.");

            object value;
            if (member is PropertyInfo property)
                value = property.GetValue(null);
            else if (member is FieldInfo field)
                value = field.GetValue(null);
            else
                throw new AssertionException("Bindings must be a field or property.");

            Assert.That(value, Is.AssignableTo<IEnumerable>(),
                "Bindings must be enumerable so tests and manifest validation can compare it to StageCatalog.");

            foreach (var item in (IEnumerable)value)
                yield return item;
        }

        static Type RequireType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            throw new AssertionException(fullName + " is required by the stage hazard visual remaster contract.");
        }

        static string RequireString(object target, params string[] names)
        {
            var value = RequireValue(target, names);
            Assert.That(value, Is.TypeOf<string>(), string.Join("/", names));
            return (string)value;
        }

        static HazardKind RequireKind(object target, string name)
        {
            var value = RequireValue(target, name);
            Assert.That(value, Is.TypeOf<HazardKind>(), name);
            return (HazardKind)value;
        }

        static bool RequireBool(object target, params string[] names)
        {
            var value = RequireValue(target, names);
            Assert.That(value, Is.TypeOf<bool>(), string.Join("/", names));
            return (bool)value;
        }

        static object RequireValue(object target, params string[] names)
        {
            foreach (var name in names)
            {
                var property = target.GetType().GetProperty(
                    name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null) return property.GetValue(target);

                var field = target.GetType().GetField(
                    name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) return field.GetValue(target);
            }

            throw new AssertionException(target.GetType().FullName + " missing member " + string.Join("/", names));
        }

        static object OptionalObject(object target, string name)
        {
            var property = target.GetType().GetProperty(
                name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null) return property.GetValue(target);

            var field = target.GetType().GetField(
                name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(target);
        }

        static object DefaultValue(Type type)
            => type.IsValueType ? Activator.CreateInstance(type) : null;

        static HazardKind ParseHazardKind(string value, string stageId)
        {
            HazardKind kind;
            Assert.That(Enum.TryParse(value, out kind), Is.True, stageId + "/" + value);
            Assert.That(KindTokens.ContainsKey(kind), Is.True, stageId + "/" + value);
            return kind;
        }

        static string ExpectedConsumer(HazardKind kind)
        {
            switch (kind)
            {
                case HazardKind.EmberVent: return "VfxDirector.EmberVent.Surface";
                case HazardKind.ObsidianPillar: return "VfxDirector.ObsidianPillar.Surface";
                case HazardKind.RelicAltar: return "VfxDirector.RelicAltar.Surface";
                case HazardKind.TideCurrent: return "VfxDirector.TideCurrent.Surface";
                case HazardKind.EmberPylon: return "VfxDirector.EmberPylon.Surface";
                case HazardKind.AshWall: return "VfxDirector.AshWall.Surface";
                case HazardKind.StoneWall: return "VfxDirector.StoneWall.Surface";
                default:
                    throw new AssertionException("No manifest consumer expectation for " + kind);
            }
        }

        static string ToResourceAssetPath(string outputPath)
        {
            const string prefix = "Assets/Resources/";
            Assert.That(outputPath, Does.StartWith(prefix));
            return outputPath.Substring(prefix.Length);
        }

        static string ProjectPath(string relativePath)
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));

        static string Sha256File(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        [Serializable]
        sealed class ManifestDocument
        {
            public int required_asset_count;
            public ManifestAsset[] assets;
        }

        [Serializable]
        sealed class ManifestAsset
        {
            public string stage_id;
            public string hazard_kind;
            public string role;
            public string source_output;
            public string output_path;
            public string sha256;
            public string dimensions;
            public string mode;
            public string runtime_consumer;
            public string[] validation_artifacts;
            public string decision;
        }

        readonly struct StageKind : IEquatable<StageKind>
        {
            readonly string _stageId;
            readonly HazardKind _kind;

            public StageKind(string stageId, HazardKind kind)
            {
                _stageId = stageId;
                _kind = kind;
            }

            public bool Equals(StageKind other)
                => string.Equals(_stageId, other._stageId, StringComparison.Ordinal)
                && _kind == other._kind;

            public override bool Equals(object obj)
                => obj is StageKind other && Equals(other);

            public override int GetHashCode()
                => ((_stageId != null ? _stageId.GetHashCode() : 0) * 397) ^ (int)_kind;

            public override string ToString()
                => _stageId + "/" + _kind;
        }
    }
}
