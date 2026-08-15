// Cel-shaded lit surface for the dungeon's stone, with an inverted-hull outline.
//
// WHY HAND-WRITTEN AND NOT SHADER GRAPH. A .shadergraph is a serialized node blob:
// it cannot be read in a diff, reviewed in a PR, or corrected by editing text. Every
// other shader in this project (Vfx-WardFresnel, UI-Icon-Glow, StageShadowReceiver)
// is authored the same way for the same reason.
//
// WHAT THE BANDS COST HERE, SPECIFICALLY. Cel shading gets its look from quantising a
// lighting gradient, so it needs a gradient to quantise. This dungeon lights with one
// directional key (StageMood, LightShadows.Hard), one directional fill, and four
// unshadowed points — a deliberately flat rig. So the banding alone changes less than
// a toon mockup suggests, and the OUTLINE carries most of the style. That is why the
// outline lives in this file rather than in a separate screen-space pass: it is the
// feature, not a garnish.
//
// The outline is an inverted hull (Pass 1, Cull Front, vertices pushed along normals).
// It costs one extra draw per renderer and no fullscreen pass, which suits a scene of
// many small props on a WebGL target far better than a depth/normal edge detect that
// pays for every pixel whether or not anything is on screen.
Shader "CinderCourt/ToonLit"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _BumpMap ("Normal", 2D) = "bump" {}

        // Two steps, not three: at this camera distance a third band lands inside a
        // few pixels on most props and reads as noise rather than as shading.
        _Steps ("Light Steps", Range(2, 4)) = 2
        // Where the lit band starts. Above 0.5 the stone reads as mostly-shadowed,
        // which suits a dungeon; the mockups sit near here.
        _StepThreshold ("Step Threshold", Range(0.05, 0.9)) = 0.42
        // Floor under the darkest band. Pure black would swallow the silhouette
        // against an equally dark floor, and this project reads by VALUE contrast.
        _ShadowFloor ("Shadow Floor", Range(0, 0.8)) = 0.34

        _RimColor ("Rim Colour", Color) = (0.72, 0.78, 1.0, 1)
        _RimPower ("Rim Power", Range(1, 12)) = 4
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.35

        // Self-lit term, added AFTER the band clamp so a rank glow is not
        // quantised away (2026-08-13: the equip props' fine/basic difference IS
        // this colour, which is why they were the one family this shader could
        // not take). Default BLACK: every ToonLit material shipped before this
        // renders byte-identically.
        _EmissionColor ("Emission", Color) = (0, 0, 0, 0)

        _OutlineColor ("Outline Colour", Color) = (0.03, 0.03, 0.05, 1)
        // World units, not screen pixels: a screen-constant outline needs the vertex
        // stage to know the projection scale, and this camera is fixed, so a world
        // width is stable and one instruction cheaper.
        _OutlineWidth ("Outline Width", Range(0, 0.08)) = 0.018
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // ---- Pass 0: outline hull -------------------------------------------
        // Drawn FIRST so the lit pass writes over its interior. Front faces culled,
        // so only the shell that survives behind the model is visible as a rim.
        Pass
        {
            Name "ToonOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // NOTE: this CBUFFER must stay field-for-field identical to the lit
            // pass's below — a mismatch drops SRP Batcher compatibility for
            // every ToonLit renderer in the game (19 terrain + 12 character +
            // env stone/floor), which no test and no pixel probe would catch.
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _RimColor;
                float4 _OutlineColor;
                float4 _EmissionColor;
                float _Steps, _StepThreshold, _ShadowFloor;
                float _RimPower, _RimStrength, _OutlineWidth;
            CBUFFER_END

            struct OutlineAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct OutlineVaryings   { float4 positionCS : SV_POSITION; };

            OutlineVaryings OutlineVertex(OutlineAttributes input)
            {
                OutlineVaryings output;
                // Extrude along the NORMAL in object space, then transform. Doing it
                // in clip space would make the width swim as the camera orbits, and
                // the dungeon camera does rotate between stages.
                float3 inflated = input.positionOS.xyz + input.normalOS * _OutlineWidth;
                output.positionCS = TransformObjectToHClip(inflated);
                return output;
            }

            half4 OutlineFragment(OutlineVaryings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // ---- Pass 1: cel-shaded lit surface ---------------------------------
        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex ToonVertex
            #pragma fragment ToonFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Field-for-field identical to the outline pass's CBUFFER above.
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _RimColor;
                float4 _OutlineColor;
                float4 _EmissionColor;
                float _Steps, _StepThreshold, _ShadowFloor;
                float _RimPower, _RimStrength, _OutlineWidth;
            CBUFFER_END

            TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);   SAMPLER(sampler_BumpMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
            };

            Varyings ToonVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            // Quantise a 0..1 light term into flat bands with a floor under the
            // darkest one. Steps is a float property so it can be tuned per material
            // without a keyword; the extra rounding is one instruction.
            half Quantise(half lambert)
            {
                half shifted = saturate((lambert - _StepThreshold) / max(1e-3, 1.0 - _StepThreshold));
                half steps = max(2.0, floor(_Steps));
                return saturate(floor(shifted * steps) / (steps - 1.0));
            }

            // MAIN light: the floor is the "ambient" term that keeps an unlit face from
            // going pure black, because this project reads by value contrast and a
            // silhouette lost against a dark floor is a readability defect (§E0.5).
            half Bands(half lambert)
            {
                return lerp(_ShadowFloor, 1.0, Quantise(lambert));
            }

            // ADDITIONAL lights: NO floor. Each point light ADDS its term, so carrying
            // the same 0.34 minimum would deposit it once per light — with four points
            // that is +1.36 of flat light on every surface regardless of where the
            // lights are. Measured: floors blew out to near-white and the whole arena
            // washed to the fill light's hue. A floor is a property of the scene's
            // ambient, not of each lamp in it.
            half BandsAdditive(half lambert)
            {
                return Quantise(lambert);
            }

            half4 ToonFragment(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float3 normalWS = normalize(input.normalWS);

                // Main light, with its real shadow attenuation: StageMood's key is
                // LightShadows.Hard and the standing solids are now casters, so the
                // shadow term is live data here rather than a constant 1.
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half lambert = saturate(dot(normalWS, mainLight.direction));
                half shaded = Bands(lambert * mainLight.shadowAttenuation);
                half3 lighting = mainLight.color * shaded;

                #ifdef _ADDITIONAL_LIGHTS
                // The four point lights are the dungeon's mood. They are banded too —
                // leaving them smooth would put a soft gradient next to a hard one and
                // read as a shader bug rather than a style.
                uint lightCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < lightCount; ++i)
                {
                    Light light = GetAdditionalLight(i, input.positionWS);
                    half point_ = saturate(dot(normalWS, light.direction));
                    lighting += light.color * light.distanceAttenuation * BandsAdditive(point_);
                }
                #endif

                // CLAMP before the albedo multiply. Banding throws away intensity: a
                // face that merely points at the key snaps to the top band, so the sum
                // of key + four points + rim can exceed 1 and a large upward slab
                // renders pure white. Measured on abyss-chancel's accent panels, which
                // read as flat white sheets until this line existed. URP/Lit hid the
                // problem because its falloff is continuous — quantising is exactly
                // what removes the headroom.
                half3 color = albedo.rgb * saturate(lighting);

                // Rim: the cheapest way to keep a dark prop off a dark floor once the
                // midtones have been flattened away by banding.
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(saturate(1.0 - saturate(dot(normalWS, viewDir))), _RimPower);
                // Rim AFTER the clamp, and scaled by how lit the surface already is:
                // a rim added at full strength to an already-saturated slab is pure
                // additive blowout, and the rim exists to separate a dark prop from a
                // dark floor — it has no work to do on a bright one.
                color += _RimColor.rgb * rim * _RimStrength * (1.0 - saturate(lighting.g));

                // Emission LAST, before fog: a rank glow must survive the band
                // clamp above. Putting it inside `lighting` would quantise it
                // into the same two steps as the key light, which is exactly
                // how a fine-band weapon and a basic one would collapse onto
                // one another. Zero by default, so materials that never set it
                // pay one add of a black constant.
                //
                // MODULATED BY ALBEDO, not added flat. A constant add is the
                // same value on every texel, so it raises the floor uniformly
                // and the sheet's pattern goes with it: measured 2026-08-13, a
                // fine hammer at emission (1.52, 0.56, 0.27) over albedo ~0.25
                // clipped to a flat peach slab with no iron left in it. Scaling
                // by albedo keeps the glow INSIDE the material — bright texels
                // glow more than dark ones, so the weave still reads at full
                // rank, and the basic/fine difference stays a difference in the
                // same surface rather than one prop turning into a lamp.
                color += _EmissionColor.rgb * albedo.rgb;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // Shadow casting: reuse URP's own pass so these surfaces keep working with
        // StageShadowPolicy's caster promotion. Writing a bespoke one would be a
        // second place for the shadow bias contract to live.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    Fallback "Universal Render Pipeline/Lit"
}
