Shader "Custom/LaserBeam"
{
    Properties
    {
        [Header(Laser Color)]
        _Color ("Laser Color", Color) = (1, 0.05, 0.05, 1)
        _Intensity ("Intensity", Range(0, 20)) = 6

        [Header(Edge Glow)]
        _FresnelPower ("Fresnel Power", Range(0.1, 8)) = 2
        _FresnelBoost ("Fresnel Boost", Range(0, 5)) = 1.5

        [Header(Vertical Fade)]
        _HeightFadePower ("Height Fade Power", Range(0.1, 8)) = 1
        _InvertHeightFade ("Invert Height Fade", Range(0, 1)) = 0

        [Header(Turbulence)]
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _NoiseScale ("Noise Scale", Float) = 2
        _ScrollSpeed ("Scroll Speed", Float) = 0.5
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.3

        [Header(Flicker)]
        _FlickerSpeed ("Flicker Speed", Float) = 0
        _FlickerAmount ("Flicker Amount", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            Name "LaserBeam"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
                float2 uv          : TEXCOORD2;
            };

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _FresnelPower;
                half _FresnelBoost;
                half _HeightFadePower;
                half _InvertHeightFade;
                float4 _NoiseTex_ST;
                half _NoiseScale;
                half _ScrollSpeed;
                half _NoiseStrength;
                half _FlickerSpeed;
                half _FlickerAmount;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = vp.positionCS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(vp.positionWS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(IN.viewDirWS);

                // Brighter at grazing angles (silhouette / near-overlapping front+back faces).
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);

                // Fade along the cone's height (UV.y): tune power/invert to match your mesh's UV direction.
                float heightT = _InvertHeightFade > 0.5 ? (1.0 - IN.uv.y) : IN.uv.y;
                float heightFade = pow(saturate(heightT), _HeightFadePower);

                // Optional scrolling turbulence (no-op if _NoiseTex is left as default white).
                float2 noiseUV = IN.uv * _NoiseScale + float2(0.0, _Time.y * _ScrollSpeed);
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;
                float turbulence = lerp(1.0, noise, _NoiseStrength);

                float flicker = 1.0 - _FlickerAmount + _FlickerAmount * (sin(_Time.y * _FlickerSpeed * 6.2831853) * 0.5 + 0.5);

                half3 color = _Color.rgb * _Intensity * (0.5 + fresnel * _FresnelBoost) * heightFade * turbulence * flicker;
                half alpha = saturate((0.4 + fresnel) * heightFade * turbulence);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
