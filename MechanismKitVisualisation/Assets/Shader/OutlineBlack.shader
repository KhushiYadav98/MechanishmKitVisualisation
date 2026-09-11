Shader "Custom/OutlineBlack"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width (world units)", Range(0.0, 1.0)) = 0.02
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
            Name "Outline"

            // Only draw the back faces, pushed outward -> shows as a rim
            // around the silhouette of the normally-rendered front faces.
            Cull Front
            ZWrite On
            ZTest LEqual

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
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                half _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // Offset in world space (not object space) so the outline width
                // is a fixed real-world size regardless of each imported part's
                // own local mesh scale (CAD imports often vary wildly here).
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

                // Guard against zero-length / degenerate normals, which are common
                // on CAD-tessellated meshes (duplicate/non-manifold vertices) and
                // otherwise blow up into huge spikes after normalize().
                float normalLen = length(normalWS);
                float3 safeNormalWS = (normalLen > 1e-5) ? (normalWS / normalLen) : float3(0, 0, 0);

                positionWS += safeNormalWS * _OutlineWidth;
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
