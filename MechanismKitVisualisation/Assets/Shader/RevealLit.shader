Shader "Custom/RevealLit"
{
    Properties
    {
        [Header(Base)]
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(Reveal)]
        _RevealHeight ("Reveal Height (world Y)", Float) = 0
        [Toggle] _InvertClip ("Invert Clip (0 = show below, 1 = show above)", Float) = 0

        [Header(Boundary Line)]
        _BoundaryColor ("Boundary Color", Color) = (0.3, 0.8, 1, 1)
        _BoundaryWidth ("Boundary Width", Range(0.001, 1)) = 0.03
        _BoundaryIntensity ("Boundary Intensity", Range(0, 10)) = 4
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
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _RevealHeight;
                half _InvertClip;
                half4 _BoundaryColor;
                half _BoundaryWidth;
                half _BoundaryIntensity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = vp.positionCS;
                OUT.positionWS = vp.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float signedDist = IN.positionWS.y - _RevealHeight;
                // InvertClip = 0: keep pixels below the height (reveal grows upward).
                // InvertClip = 1: keep pixels above the height (hide grows upward).
                float keep = (_InvertClip > 0.5) ? signedDist : -signedDist;
                clip(keep);

                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = baseTex.rgb * _BaseColor.rgb;

                float3 normalWS = normalize(IN.normalWS);
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS);
                half3 lit = albedo * (mainLight.color * NdotL + ambient);

                float distToLine = abs(signedDist);
                half boundary = smoothstep(_BoundaryWidth, 0.0, distToLine);
                half3 color = lit + _BoundaryColor.rgb * boundary * _BoundaryIntensity;

                return half4(color, _BaseColor.a * baseTex.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
