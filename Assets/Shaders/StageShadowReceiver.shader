// Character-only realtime shadow receiver for the painted dungeon floor.
//
// The floor art is deliberately URP/Unlit, so replacing it with a lit
// material would recolour every stage. This pass instead draws after the
// painted floor but remains inside RenderQueueRange.opaque: URP's Mobile
// Forward renderer still exposes the main-light shadow data here, while the
// transparent phase intentionally does not receive shadows in this project.
//
// The receiver contributes neutral black only where the main shadow map says
// a caster blocks the light. Fully lit pixels return zero colour and zero
// alpha, preserving the authored floor exactly outside character shadows.
Shader "CinderCourt/StageShadowReceiver"
{
    Properties
    {
        // Kept below a full blackout so hazards and painted contours remain
        // readable when an actor shadow crosses them. Runtime tuning stays in
        // this bounded range; the Development-only QA toggle is owned by the
        // receiver component, not by a shipping shader keyword.
        _ShadowStrength ("Shadow Strength", Range(0.50, 0.65)) = 0.62
    }

    SubShader
    {
        Tags
        {
            // Geometry is 2000, so +499 is queue 2499: after the lower-queue
            // floor, still before URP's transparent settings/pass boundary.
            "Queue" = "Geometry+499"
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "StageShadowReceiver"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            // Match URP 17.5's forward main-light shadow variant set. The
            // current quality policy uses hard shadows, but keeping the full
            // package variant spelling prevents an RP-asset change from
            // silently compiling a no-shadow receiver.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half _ShadowStrength;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half shadowAlpha = saturate(1.0h - mainLight.shadowAttenuation)
                                   * saturate(_ShadowStrength);
                return half4(0.0h, 0.0h, 0.0h, shadowAlpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
