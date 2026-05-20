Shader "Hecton8/AbyssalSwarmProcedural"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.08, 0.42, 0.55, 0.72)
        _BellyColor ("Belly Color", Color) = (0.42, 0.88, 0.95, 0.42)
        _SilhouetteScale ("Silhouette Scale", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "AbyssalSwarmProcedural"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct BoidMatrixDTO
            {
                float4 C0;
                float4 C1;
                float4 C2;
                float4 C3;
            };

            StructuredBuffer<BoidMatrixDTO> _H8ShinobuBoidMatrices;
            StructuredBuffer<float4> _H8ShinobuBoidCustomData;
            StructuredBuffer<uint> _H8ShinobuBoidVisibleIndices;
            int _H8ShinobuBoidActiveCount;
            int _H8ShinobuBoidUseVisibleIndices;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BellyColor;
                float _SilhouetteScale;
            CBUFFER_END

            struct Varyings
            {
                float4 PositionCS : SV_POSITION;
                float4 Color : COLOR0;
            };

            float3 ResolveLocalVertex(uint vertexID)
            {
                uint id = vertexID % 3u;
                if (id == 0u)
                    return float3(0.0, 0.055, 0.28);
                if (id == 1u)
                    return float3(-0.07, -0.035, -0.18);
                return float3(0.07, -0.035, -0.18);
            }

            float4 TransformColumnMajor(BoidMatrixDTO matrixDto, float3 localPosition)
            {
                return (matrixDto.C0 * localPosition.x) +
                       (matrixDto.C1 * localPosition.y) +
                       (matrixDto.C2 * localPosition.z) +
                       matrixDto.C3;
            }

            Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                Varyings output;
                uint sourceInstance = instanceID;
                if (_H8ShinobuBoidUseVisibleIndices != 0)
                    sourceInstance = _H8ShinobuBoidVisibleIndices[instanceID];

                uint safeInstance = min(sourceInstance, max((uint)_H8ShinobuBoidActiveCount, 1u) - 1u);
                float alive = step((float)safeInstance + 0.5, (float)_H8ShinobuBoidActiveCount);
                BoidMatrixDTO matrixDto = _H8ShinobuBoidMatrices[safeInstance];
                float4 custom = _H8ShinobuBoidCustomData[safeInstance];
                float quality = saturate(custom.w);
                float speciesPhase = frac(custom.x * 0.173);
                float bodyScale = max(_SilhouetteScale, 0.001) * lerp(0.72, 1.18, quality);
                float3 localPosition = ResolveLocalVertex(vertexID) * bodyScale;
                float4 worldPosition = TransformColumnMajor(matrixDto, localPosition);
                output.PositionCS = mul(UNITY_MATRIX_VP, worldPosition);
                float shimmer = lerp(0.65, 1.15, speciesPhase);
                float4 color = lerp(_BaseColor, _BellyColor, saturate(localPosition.y * 7.0 + 0.45));
                color.rgb *= shimmer;
                color.a *= alive * lerp(0.46, 0.86, quality);
                output.Color = color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(input.Color);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
