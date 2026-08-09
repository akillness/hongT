// Ward shell — fresnel rim instead of a flat alpha sphere.
//
// The ward is the highest screen-time effect in the game: a 3 s bubble the
// player parks inside, cast far more often than a nova. It shipped as a Unity
// primitive Sphere with a flat transparent cyan (a = 0.28) and a 10 Hz
// renderer-toggle blink for the last 0.5 s — the most-seen defensive visual was
// also the most primitive-looking one (VfxDirector.cs:236-242, 1718-1727).
//
// Fresnel is the whole idea: a shell should be nearly invisible where you look
// straight through it and bright where it turns away, because that is what a
// thin curved surface does to light. Flat alpha cannot express that at any
// value — too low and the shell vanishes, too high and it fogs the fight.
//
// WebGL contract (CLAUDE.md §1):
//   * no compute, no depth/opaque texture reads, no grabpass — this samples
//     nothing but its own interpolators;
//   * ZWrite Off + Cull Off so the far side reads through the near side, which
//     is what makes it look like a bubble instead of a disc;
//   * Blend SrcAlpha One (additive) matching ViewWorld.MakeAdditive, so the
//     shell accumulates past the Bloom threshold (CinderPostProfile 1.05) with
//     the rest of the VFX grammar instead of muddying;
//   * "DisableBatching" = "True": the fresnel needs the object's own normals in
//     world space, and static batching would bake them into a shared mesh.
//
// STRIPPING. Every runtime material in this project is a clone of a serialized
// seed for exactly this reason (ViewWorld.cs:44-57); a shader referenced by no
// asset is removed from a WebGL build and renders pink. Assets/Resources/
// Materials/ward-fresnel-seed.mat is that reference — do not delete it, and do
// not construct this shader with Shader.Find on the runtime path.
Shader "CinderCourt/Vfx/WardFresnel"
{
    Properties
    {
        _BaseColor ("Shell Color", Color) = (0.45, 0.85, 1, 1)
        // Rim sharpness. 1 = broad wash (reads as fog), 6 = thin bright edge.
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3
        // Brightness at the silhouette, where the shell turns away from view.
        _RimIntensity ("Rim Intensity", Range(0, 4)) = 1.6
        // Floor so the shell is never fully invisible head-on — the player has
        // to be able to see the volume they are standing in.
        _CoreAlpha ("Core Alpha", Range(0, 1)) = 0.10
        // Expiry pulse. Amplitude 0 = steady. The old implementation toggled
        // the RENDERER at 10 Hz, which is a strobe; a brightness pulse carries
        // the same "about to end" information without flashing geometry on and
        // off, and it degrades to steady under reduced motion by setting 0.
        _PulseAmplitude ("Pulse Amplitude", Range(0, 1)) = 0
        _PulseSpeed ("Pulse Speed", Range(0, 20)) = 10
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "DisableBatching" = "True"
        }

        Pass
        {
            Name "WardShell"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _FresnelPower;
                float  _RimIntensity;
                float  _CoreAlpha;
                float  _PulseAmplitude;
                float  _PulseSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(input.viewDirWS);
                // Cull Off means back faces arrive with an inverted normal;
                // abs() makes both sides rim the same way, which is what turns
                // the sphere into a readable bubble rather than a half shell.
                float facing = abs(dot(n, v));
                float fresnel = pow(saturate(1.0 - facing), _FresnelPower);

                float pulse = 1.0 + _PulseAmplitude * sin(_Time.y * _PulseSpeed);

                half3 rgb = _BaseColor.rgb * (_RimIntensity * fresnel + _CoreAlpha) * pulse;
                half  a   = saturate((fresnel * _RimIntensity + _CoreAlpha)) * _BaseColor.a;
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }

    // URP/Unlit is referenced by the committed transparent seed, so it always
    // survives stripping — a safe landing spot if this shader is ever missing.
    FallBack "Universal Render Pipeline/Unlit"
}
