Shader "UI/Icon-Glow"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Base Color", Color) = (1, 1, 1, 1)
        _GlowColor ("Glow Color", Color) = (1, 0.6, 0.2, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 1
        _ShadowOffset ("Shadow Offset", Vector) = (1, -1, 0, 0)
        _ShadowColor ("Shadow Color", Color) = (0, 0, 0, 0.3)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.02
    }
    
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Back
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Shadow"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _ShadowOffset;
            float4 _ShadowColor;

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 posOS = input.positionOS.xyz;
                
                // Offset for shadow
                posOS.xy += _ShadowOffset.xy * 0.01;
                
                output.positionHCS = TransformObjectToHClip(posOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                return tex * _ShadowColor * input.color.a * tex.a;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Main"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : TEXCOORD1;
                float2 posOS_xy : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _Color;
            float4 _GlowColor;
            float _GlowIntensity;
            float _OutlineWidth;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                output.posOS_xy = input.positionOS.xy;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 col = tex * _Color * input.color;
                
                // Rim glow based on alpha gradient
                half alpha = tex.a;
                half rimGlow = saturate(alpha * (1 - alpha) * 4);
                
                half4 glow = _GlowColor * rimGlow * _GlowIntensity;
                col.rgb = col.rgb + glow.rgb * tex.a;
                col.a = tex.a * input.color.a;
                
                return col;
            }
            ENDHLSL
        }
    }
    
    FallBack "UI/Default"
}
