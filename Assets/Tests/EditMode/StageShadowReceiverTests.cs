using System.IO;
using CinderCourt.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class StageShadowReceiverTests
    {
        const string ShaderPath = "Assets/Shaders/StageShadowReceiver.shader";
        const string MaterialPath = "Assets/Resources/Materials/StageShadowReceiver.mat";
        const string ResourcePath = "Materials/StageShadowReceiver";
        const string ShaderName = "CinderCourt/StageShadowReceiver";
        const int ReceiverQueue = 2499;
        const float MinReadableStrength = 0.50f;
        const float MaxReadableStrength = 0.65f;

        static Material LoadMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.That(material, Is.Not.Null,
                MaterialPath + " must be committed so the receiver shader and "
                + "its main-shadow variants survive WebGL stripping");
            return material;
        }

        [Test]
        public void ReceiverMaterial_IsCommittedAtThePinnedResourcesPath()
        {
            var asset = LoadMaterial();
            var resource = Resources.Load<Material>(ResourcePath);
            Assert.That(resource, Is.SameAs(asset),
                "StageMood must load the serialized receiver by the pinned "
                + "Resources path rather than constructing it with Shader.Find");
        }

        [Test]
        public void ReceiverMaterial_PinsOpaquePhaseIdentityAndStrength()
        {
            var material = LoadMaterial();
            Assert.That(material.shader, Is.Not.Null, "receiver shader GUID is unresolved");
            Assert.That(material.shader.name, Is.EqualTo(ShaderName));
            Assert.That(material.shader.isSupported, Is.True,
                "receiver shader does not compile on the active URP editor platform");
            Assert.That(material.shader.passCount, Is.EqualTo(1),
                "the receiver is one opaque-phase composite draw");
            Assert.That(material.FindPass("StageShadowReceiver"), Is.EqualTo(0));
            Assert.That(material.renderQueue, Is.EqualTo(ReceiverQueue),
                "2499 is the last deliberate opaque slot before transparent settings");
            Assert.That(material.GetTag("RenderType", false, "<none>"), Is.EqualTo("Opaque"));
            Assert.That(material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.False,
                "the transparent keyword makes URP discard screen/main shadow reception");
            Assert.That(material.HasProperty("_ShadowStrength"), Is.True);
            Assert.That(material.GetFloat("_ShadowStrength"),
                Is.InRange(MinReadableStrength, MaxReadableStrength),
                "receiver strength must remain visible without reading as a black blob");
        }

        [Test]
        public void ReceiverMaterial_InvalidShaderFailsClosed()
        {
            Assert.That(StageShadowPolicy.IsValidReceiverMaterial(LoadMaterial()), Is.True);
            var invalid = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            try
            {
                Assert.That(StageShadowPolicy.IsValidReceiverMaterial(invalid), Is.False);
                Assert.That(StageShadowPolicy.IsValidReceiverMaterial(null), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(invalid);
            }
        }

        [Test]
        public void ReceiverShader_PinsForwardShadowVariantsAndRenderState()
        {
            var source = File.ReadAllText(ShaderPath);
            Assert.That(source, Does.Contain("Shader \"" + ShaderName + "\""));
            Assert.That(source, Does.Contain("\"Queue\" = \"Geometry+499\""));
            Assert.That(source, Does.Contain("\"RenderType\" = \"Opaque\""));
            Assert.That(source, Does.Contain("Tags { \"LightMode\" = \"UniversalForward\" }"));
            Assert.That(source, Does.Contain("Blend SrcAlpha OneMinusSrcAlpha"));
            Assert.That(source, Does.Contain("ZWrite Off"));
            Assert.That(source, Does.Contain("ZTest LEqual"));
            Assert.That(source, Does.Contain("ColorMask RGB"),
                "the receiver must darken colour without corrupting the target alpha channel");
            Assert.That(source, Does.Contain(
                "_ShadowStrength (\"Shadow Strength\", Range(0.50, 0.65)) = 0.62"),
                "the shader fallback must preserve the same readable floor-darkening band");
            Assert.That(source, Does.Contain(
                "#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN"));
            Assert.That(source, Does.Contain("TransformWorldToShadowCoord(input.positionWS)"));
            Assert.That(source, Does.Contain("GetMainLight(shadowCoord)"));
            Assert.That(source, Does.Not.Contain("_SURFACE_TYPE_TRANSPARENT"),
                "the receiver must remain an opaque-phase shadow consumer");
        }

        [Test]
        public void ReceiverShader_FullyLitPixelsContributeExactlyZeroAndShadowsStayNeutral()
        {
            var source = File.ReadAllText(ShaderPath);
            Assert.That(source, Does.Contain(
                "saturate(1.0h - mainLight.shadowAttenuation)"),
                "alpha must be zero when realtime shadow attenuation is one");
            Assert.That(source, Does.Contain(
                "return half4(0.0h, 0.0h, 0.0h, shadowAlpha);"),
                "the composite must be neutral black with no RGB tint");
        }

        [Test]
        public void ReceiverRetention_IsConnectedToTheExistingBuildSeedPath()
        {
            var seeds = File.ReadAllText("Assets/Editor/RuntimeMaterialSeeds.cs");
            var build = File.ReadAllText("Assets/Editor/BuildScript.cs");
            Assert.That(seeds, Does.Contain(
                "StageShadowReceiverAssetPath = Dir + \"/StageShadowReceiver.mat\""));
            Assert.That(seeds, Does.Contain(ShaderPath));
            Assert.That(build, Does.Contain("if (!RuntimeMaterialSeeds.Seed())"),
                "BuildWebGL must fail closed when a serialized runtime material seed is invalid");
        }

        [Test]
        public void CharacterShadow_IsRenderingLayerBitOne()
        {
            var tagManager = File.ReadAllText("ProjectSettings/TagManager.asset");
            Assert.That(tagManager, Does.Contain(
                "  m_RenderingLayers:\n  - Default\n  - CharacterShadow\n"),
                "bit 0 remains Default lighting and bit 1 is the character-only shadow allow-list");
        }
    }
}
