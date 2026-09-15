Shader "Custom/HeatGlowLit"
{
    Properties
    {
        [Header(Base)]
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(Heat Glow)]
        _GlowColor ("Glow Color", Color) = (1, 0.35, 0.05, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 10)) = 3
        _SurfaceGlow ("Surface Glow (non-edge baseline)", Range(0, 2)) = 0.5

        [Header(Edge Glow)]
        _FresnelPower ("Fresnel Power", Range(0.1, 8)) = 2
        _FresnelIntensity ("Fresnel Intensity", Range(0, 10)) = 4

        [Header(Pulse)]
        _PulseSpeed ("Pulse Speed", Float) = 0
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _GlowColor;
                half _GlowIntensity;
                half _SurfaceGlow;
                half _FresnelPower;
                half _FresnelIntensity;
                half _PulseSpeed;
                half _PulseAmount;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = vp.positionCS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(vp.positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(IN.viewDirWS);

                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = baseTex.rgb * _BaseColor.rgb;

                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS);
                half3 lit = albedo * (mainLight.color * NdotL + ambient);

                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                half pulse = 1.0h - _PulseAmount + _PulseAmount * (sin(_Time.y * _PulseSpeed * 6.2831853) * 0.5h + 0.5h);

                half3 glow = _GlowColor.rgb * (_SurfaceGlow + fresnel * _FresnelIntensity) * _GlowIntensity * pulse;

                return half4(lit + glow, _BaseColor.a * baseTex.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
