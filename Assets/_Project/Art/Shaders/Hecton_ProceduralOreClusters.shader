Shader "Hecton8/World/ProceduralOreClusters"
{
    Properties
    {
        _CoreTint("Core Ore Tint", Color) = (0.95, 0.78, 0.34, 1)
        _EdgeTint("Edge Crystal Tint", Color) = (0.18, 0.72, 0.86, 1)
        _VisualOnlyTint("Visual Cluster Tint", Color) = (0.35, 0.92, 1.00, 1)
        _QualityOverkill("Quality Overkill", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float4x4> _OreMatrices;

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreTint;
                float4 _EdgeTint;
                float4 _VisualOnlyTint;
                float _QualityOverkill;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 local01 : TEXCOORD2;
                float active : TEXCOORD3;
                float variant : TEXCOORD4;
            };

            float3 SafeNormalize3(float3 value, float3 fallback)
            {
                float lengthSq = dot(value, value);
                return lengthSq > 1e-6 ? value * rsqrt(lengthSq) : fallback;
            }

            float3 ResolveOreVertex(uint id)
            {
                uint faceVertex = id % 6u;
                uint face = (id / 6u) % 6u;
                float2 uv;
                uv.x = (faceVertex == 1u || faceVertex == 2u || faceVertex == 5u) ? 1.0 : -1.0;
                uv.y = (faceVertex == 2u || faceVertex == 4u || faceVertex == 5u) ? 1.0 : -1.0;

                if (face == 0u) return float3(uv.x * 0.26, uv.y * 0.26, 0.82);
                if (face == 1u) return float3(-uv.x * 0.22, uv.y * 0.22, -0.32);
                if (face == 2u) return float3(0.34, uv.y * 0.20, -uv.x * 0.34);
                if (face == 3u) return float3(-0.34, uv.y * 0.20, uv.x * 0.34);
                if (face == 4u) return float3(uv.x * 0.28, 0.34, -uv.y * 0.26);
                return float3(uv.x * 0.28, -0.18, uv.y * 0.26);
            }

            float3 ResolveOreNormal(uint id)
            {
                uint face = (id / 6u) % 6u;
                if (face == 0u) return float3(0.0, 0.0, 1.0);
                if (face == 1u) return float3(0.0, 0.0, -1.0);
                if (face == 2u) return float3(1.0, 0.0, 0.0);
                if (face == 3u) return float3(-1.0, 0.0, 0.0);
                if (face == 4u) return float3(0.0, 1.0, 0.0);
                return float3(0.0, -1.0, 0.0);
            }

            Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                Varyings output;
                uint safeVertex = vertexID % 36u;
                float4x4 matrix = _OreMatrices[instanceID];
                float activity = abs(matrix._m00) + abs(matrix._m11) + abs(matrix._m22) + abs(matrix._m33);
                float active = step(0.0001, activity);
                float3 localPosition = ResolveOreVertex(safeVertex);
                float3 localNormal = ResolveOreNormal(safeVertex);
                float4 positionWS = mul(matrix, float4(localPosition, 1.0));
                float3 normalWS = SafeNormalize3(mul((float3x3)matrix, localNormal), float3(0.0, 1.0, 0.0));
                output.positionCS = TransformWorldToHClip(positionWS.xyz);
                output.normalWS = normalWS;
                output.positionWS = positionWS.xyz;
                output.local01 = localPosition * 0.5 + 0.5;
                output.active = active;
                output.variant = frac((float)instanceID * 0.61803398875);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(input.active - 0.5);
                float3 lightDirection = SafeNormalize3(float3(0.25, 0.82, 0.51), float3(0.0, 1.0, 0.0));
                float ndotl = saturate(dot(SafeNormalize3(input.normalWS, float3(0.0, 1.0, 0.0)), lightDirection));
                float glint = smoothstep(0.86, 1.0, frac(dot(input.positionWS.xz, float2(0.173, 0.119)) + input.variant));
                float3 baseColor = lerp(_CoreTint.rgb, _EdgeTint.rgb, saturate(input.local01.z));
                baseColor = lerp(baseColor, _VisualOnlyTint.rgb, glint * saturate(_QualityOverkill));
                float3 color = baseColor * lerp(0.42, 1.15, ndotl) + glint * _QualityOverkill * 0.25;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
