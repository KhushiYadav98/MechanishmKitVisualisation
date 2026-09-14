Shader "Custom/BlinkHighlight"
{
    Properties
    {
        [Header(Highlight)]
        _Color ("Highlight Color", Color) = (0.3, 0.8, 1, 1)
        _MinAlpha ("Min Alpha", Range(0, 1)) = 0.15
        _MaxAlpha ("Max Alpha", Range(0, 1)) = 0.85
        _BlinkSpeed ("Blink Speed (cycles per second)", Float) = 1.5

        [Header(Edge Glow)]
        _FresnelPower ("Fresnel Power", Range(0.1, 8)) = 2.5
        _FresnelBoost ("Fresnel Boost", Range(0, 5)) = 1.5
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
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "BlinkHighlight"
            Tags { "LightMode" = "UniversalForward" }

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
                half4 _Color;
                half _MinAlpha;
                half _MaxAlpha;
                half _BlinkSpeed;
                half _FresnelPower;
                half _FresnelBoost;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = vp.positionCS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(vp.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(IN.viewDirWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);

                // Smooth 0..1 pulse over time.
                half pulse = sin(_Time.y * _BlinkSpeed * 6.2831853) * 0.5h + 0.5h;
                half alpha = lerp(_MinAlpha, _MaxAlpha, pulse);

                half3 color = _Color.rgb * (1.0 + fresnel * _FresnelBoost);
                alpha = saturate(alpha + fresnel * _FresnelBoost * alpha);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
