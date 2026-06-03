Shader "Hecton8/UI/Hecton Hologram Map"
{
    Properties
    {
        _Tint("Tint", Color) = (0.0, 0.82, 1.0, 1.0)
        _Opacity("Opacity", Range(0, 1)) = 0.72
        _Glow("Glow", Range(0, 8)) = 1.25
        _Quality("Quality", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "HologramMap"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<uint> _CartographyVoxelR8;

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Opacity;
                half _Glow;
                half _Quality;
                float4x4 _PointCloudLocalToWorld;
                float4 _CartographyGridParams;
                float4 _CartographyVisualParams;
            CBUFFER_END

            struct Attributes
            {
                uint vertexId : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float2 ResolveProceduralQuad01(uint vertexId)
            {
                float id = (float)(vertexId % 6u);
                float positiveX = saturate(step(1.5, id) - step(2.5, id) + step(3.5, id));
                float positiveY = saturate(step(0.5, id) - step(2.5, id) + step(3.5, id) - step(4.5, id));
                return float2(positiveX, positiveY);
            }

            Varyings Vert(Attributes input)
            {
                float2 uv = ResolveProceduralQuad01(input.vertexId);
                float2 local = uv - 0.5;
                float3 worldPosition = mul(_PointCloudLocalToWorld, float4(local.x, local.y, 0.0, 1.0)).xyz;
                Varyings output;
                output.positionCS = TransformWorldToHClip(worldPosition);
                output.uv = uv;
                return output;
            }

            uint ResolveVoxelIndex(uint3 cell, uint axis)
            {
                return cell.x + cell.y * axis + cell.z * axis * axis;
            }

            half ReadPackedR8(uint voxelIndex)
            {
                uint packed = _CartographyVoxelR8[voxelIndex >> 2];
                uint lane = (voxelIndex & 3u) << 3;
                return (half)((packed >> lane) & 255u) * (half)(1.0 / 255.0);
            }

            half SampleVoxelNearest(float3 uvw, uint axis)
            {
                float safeAxis = max((float)axis - 1.0, 1.0);
                uint3 cell = (uint3)floor(saturate(uvw) * safeAxis + 0.5);
                return ReadPackedR8(ResolveVoxelIndex(cell, axis));
            }

            half WireMask(float3 uvw, uint axis)
            {
                float3 cell = frac(saturate(uvw) * max((float)axis, 1.0));
                float3 edge = min(cell, 1.0 - cell);
                float nearestEdge = min(edge.x, min(edge.y, edge.z));
                float width = lerp(0.065, 0.022, saturate(_Quality));
                return (half)(1.0 - saturate((nearestEdge - width) * rcp(max(width * 0.9, 0.0001))));
            }

            float Triangle01(float phase)
            {
                return 1.0 - abs(frac(phase) * 2.0 - 1.0);
            }

            float SignedTriangle01(float phase)
            {
                return Triangle01(phase) * 2.0 - 1.0;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                uint axis = (uint)max(_CartographyGridParams.x, 1.0);
                half quality = saturate(_Quality * _CartographyGridParams.z);
                int steps = (int)(8.0 + quality * 56.0 + 0.5);
                float timePhase = _CartographyVisualParams.x;
                float2 uv = input.uv;
                float2 centered = uv * 2.0 - 1.0;
                float shell = saturate(1.0 - dot(centered, centered) * 0.22);
                half density = 0.0h;
                half edgeAccum = 0.0h;

                [loop]
                for (int i = 0; i < 64; i++)
                {
                    if (i >= steps)
                        break;

                    float t = ((float)i + 0.5) / max((float)steps, 1.0);
                    float wobble = SignedTriangle01(((uv.y + t) * 41.0 + timePhase * 3.7) * 0.15915494 + 0.25) * lerp(0.002, 0.014, quality);
                    float3 uvw = float3(saturate(uv.x + wobble), saturate(t), saturate(uv.y - wobble));
                    half voxel = SampleVoxelNearest(uvw, axis);
                    half wire = WireMask(uvw, axis);
                    density = max(density, voxel * (0.22h + wire * 0.78h));
                    edgeAccum += voxel * wire * (half)(1.0 / 64.0);
                }

                half scan = (half)(0.72 + 0.28 * Triangle01((uv.y * 720.0 + timePhase * 9.0) * 0.15915494));
                half flicker = (half)(0.88 + 0.12 * Hash21(floor(uv * 192.0) + timePhase));
                half chroma = SampleVoxelNearest(float3(saturate(uv.x + 0.006), 0.5, saturate(uv.y - 0.004)), axis);
                half alpha = saturate((density + edgeAccum * _Glow + chroma * 0.18h) * _Opacity * shell * scan * flicker);
                half3 cyan = _Tint.rgb * (0.35h + _Glow * 0.28h);
                half3 color = cyan * alpha + half3(chroma * 0.03h, density * 0.09h, chroma * 0.16h);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
