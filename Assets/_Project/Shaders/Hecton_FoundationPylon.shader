Shader "Hecton8/Construction/FoundationPylon"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.04, 0.72, 0.86, 0.86)
        _EmbeddedColor ("Embedded Color", Color) = (0.02, 0.12, 0.15, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "FoundationPylonProcedural"
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define H8_FOUNDATION_FLAG_ACTIVE 1u
            #define H8_FOUNDATION_SEGMENTS 16u

            struct PylonMatrixRaw
            {
                float4 c0;
                float4 c1;
                float4 c2;
                float4 c3;
            };

            struct PylonSurfaceRaw
            {
                float4 surfaceNormalFlare;
                float4 axisRadius;
                float4 hitLocalLength;
                uint4 meta;
            };

            StructuredBuffer<PylonMatrixRaw> _H8FoundationPylonMatrices;
            StructuredBuffer<PylonSurfaceRaw> _H8FoundationPylonSurfaces;

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _EmbeddedColor;
            float4 _H8FoundationPylonCameraWorldOffset;
            CBUFFER_END

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR0;
                float3 worldPos : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float length01 : TEXCOORD2;
                nointerpolation uint flags : TEXCOORD3;
            };

            static const float2 H8PylonCircle[16] =
            {
                float2(1.000000, 0.000000),
                float2(0.923880, 0.382683),
                float2(0.707107, 0.707107),
                float2(0.382683, 0.923880),
                float2(0.000000, 1.000000),
                float2(-0.382683, 0.923880),
                float2(-0.707107, 0.707107),
                float2(-0.923880, 0.382683),
                float2(-1.000000, 0.000000),
                float2(-0.923880, -0.382683),
                float2(-0.707107, -0.707107),
                float2(-0.382683, -0.923880),
                float2(0.000000, -1.000000),
                float2(0.382683, -0.923880),
                float2(0.707107, -0.707107),
                float2(0.923880, -0.382683)
            };

            float3 SafeNormalize(float3 value, float3 fallback)
            {
                float lenSq = dot(value, value);
                return lenSq > 1.0e-6 ? value * rsqrt(lenSq) : fallback;
            }

            float2 UnitCircle(uint segment)
            {
                return H8PylonCircle[segment & 15u];
            }

            void ResolveCylinderVertex(uint vertexId, out float3 local, out float3 normalOS)
            {
                uint segment = (vertexId / 6u) % H8_FOUNDATION_SEGMENTS;
                uint triVertex = vertexId % 6u;
                uint nextSegment = (segment + 1u) % H8_FOUNDATION_SEGMENTS;
                bool useNext = triVertex == 2u || triVertex == 4u || triVertex == 5u;
                bool useTop = triVertex == 1u || triVertex == 2u || triVertex == 4u;
                float2 ring = UnitCircle(useNext ? nextSegment : segment);
                local = float3(ring.x * 0.5, useTop ? 0.5 : -0.5, ring.y * 0.5);
                normalOS = SafeNormalize(float3(ring.x, 0.0, ring.y), float3(0.0, 1.0, 0.0));
            }

            Varyings vert(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
            {
                Varyings output;
                PylonMatrixRaw pylonMatrix = _H8FoundationPylonMatrices[instanceId];
                PylonSurfaceRaw surface = _H8FoundationPylonSurfaces[instanceId];
                uint flags = surface.meta.x;

                float3 local;
                float3 normalOS;
                ResolveCylinderVertex(vertexId, local, normalOS);

                float active = ((flags & H8_FOUNDATION_FLAG_ACTIVE) != 0u) ? 1.0 : 0.0;
                float bottom = local.y < 0.0 ? 1.0 : 0.0;
                float flare = saturate(surface.surfaceNormalFlare.w) * bottom;
                local.xz *= 1.0 + flare;
                local *= active;

                float4 world =
                    (pylonMatrix.c0 * local.x) +
                    (pylonMatrix.c1 * local.y) +
                    (pylonMatrix.c2 * local.z) +
                    pylonMatrix.c3;
                float3 worldPosition = world.xyz + _H8FoundationPylonCameraWorldOffset.xyz;
                output.positionHCS = TransformWorldToHClip(worldPosition);
                output.worldPos = worldPosition;
                output.normalWS = SafeNormalize(
                    (pylonMatrix.c0.xyz * normalOS.x) +
                    (pylonMatrix.c2.xyz * normalOS.z) +
                    (surface.surfaceNormalFlare.xyz * bottom * 0.35),
                    float3(0.0, 1.0, 0.0));
                output.length01 = saturate(surface.hitLocalLength.w / 42.0);
                output.flags = flags;
                float embed = bottom * saturate(flare);
                half3 color = lerp(_BaseColor.rgb, _EmbeddedColor.rgb, (half)embed);
                output.color = half4(color, _BaseColor.a * (half)active);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                clip(((input.flags & H8_FOUNDATION_FLAG_ACTIVE) != 0u) ? 1.0 : -1.0);
                float3 viewDir = SafeNormalize(GetWorldSpaceViewDir(input.worldPos), float3(0.0, 0.0, 1.0));
                float3 normalWS = SafeNormalize(input.normalWS, float3(0.0, 1.0, 0.0));
                float rimBase = saturate(1.0 - abs(dot(normalWS, viewDir)));
                float rim = rimBase * rimBase;
                float weldLine = frac(input.worldPos.y * lerp(3.0, 9.0, input.length01));
                float band = smoothstep(0.0, 0.08, weldLine) * (1.0 - smoothstep(0.12, 0.24, weldLine));
                half4 color = input.color;
                color.rgb += (half3)(rim * 0.18 + band * 0.08);
                color.a = 1.0h;
                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
