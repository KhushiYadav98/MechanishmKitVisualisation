Shader "Custom/Hologram"
{
    Properties
    {
        [Header(Base)]
        _BaseColor ("Base Color", Color) = (0.1, 0.6, 1, 1)
        _BaseAlpha ("Base Alpha", Range(0,1)) = 0.15

        [Header(Emission Texture)]
        [NoScaleOffset] _EmissionMap ("Emission Mask", 2D) = "black" {}
        _EmissionMapColor ("Emission Mask Color", Color) = (0.6, 0.95, 1, 1)
        _EmissionMapIntensity ("Emission Mask Intensity", Range(0, 10)) = 3.0

        [Header(Fresnel Rim)]
        _RimColor ("Rim Color", Color) = (0.4, 0.85, 1, 1)
        _RimPower ("Rim Power", Range(0.1, 8)) = 2.5
        _RimIntensity ("Rim Intensity", Range(0, 10)) = 3.0

        [Header(Flicker)]
        _FlickerSpeed ("Flicker Speed", Float) = 8
        _FlickerAmount ("Flicker Amount", Range(0, 0.5)) = 0.05

        [Header(Emission)]
        _EmissionIntensity ("Overall Emission Intensity", Range(0, 10)) = 1.5
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
            Name "Hologram"
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
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float3 viewDirWS   : TEXCOORD3;
            };

            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _BaseAlpha;
                half4 _EmissionMapColor;
                half _EmissionMapIntensity;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half _FlickerSpeed;
                half _FlickerAmount;
                half _EmissionIntensity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = vp.positionCS;
                OUT.positionWS = vp.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                OUT.viewDirWS = GetWorldSpaceViewDir(vp.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(IN.viewDirWS);

                // Fresnel: bright glowing edges, transparent facing surfaces
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
                half3 rim = _RimColor.rgb * fresnel * _RimIntensity;

                // Optional baked emission mask (e.g. panel lines, labels) - white on black texture
                half emissionMask = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv).r;
                half3 emissionTex = _EmissionMapColor.rgb * emissionMask * _EmissionMapIntensity;

                // Subtle overall brightness flicker (no spatial movement)
                float flicker = 1.0 - _FlickerAmount + _FlickerAmount * sin(_Time.y * _FlickerSpeed);

                half3 color = (_BaseColor.rgb * _BaseAlpha + rim + emissionTex) * _EmissionIntensity * flicker;
                half alpha = saturate(_BaseAlpha + fresnel * _RimIntensity * 0.3 + emissionMask);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
