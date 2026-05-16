Shader "Hecton8/Physics/TetherLineStrip"
{
    Properties
    {
        [MainColor] _TetherColor ("Tether Color", Color) = (0.22, 0.92, 0.96, 0.92)
        _TetherStressColor ("Tether Stress Color", Color) = (1.0, 0.38, 0.12, 0.96)
        _TetherStress01 ("Tether Stress", Range(0, 1)) = 0
        _TetherSegmentStressScale ("Tether Segment Stress Scale", Float) = 2.5
        _TetherRadius ("Tether Radius", Float) = 0.045
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "UniversalMaterialType" = "Unlit"
        }

        Pass
        {
            Name "TetherProceduralTube"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual
            Blend Off
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS
            #pragma skip_variants POINT POINT_COOKIE _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float3> _TetherPositions;
            StructuredBuffer<float> _TetherSegmentTensions;

            CBUFFER_START(UnityPerMaterial)
                float4 _TetherColor;
                float4 _TetherStressColor;
                float _TetherStress01;
                float _TetherSegmentStressScale;
                float _TetherRadius;
                int _TetherPointCount;
                int _TetherIndirectMode;
            CBUFFER_END

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                half4 color : COLOR0;
            };

            float HectonDitherCoverage(float2 positionCS)
            {
                float2 pixel = floor(positionCS);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                int segmentCount = max(_TetherPointCount - 1, 0);
                int segmentIndex = _TetherIndirectMode != 0
                    ? clamp((int)input.instanceID, 0, max(segmentCount - 1, 0))
                    : clamp((int)(input.vertexID / 6u), 0, max(segmentCount - 1, 0));
                int localVertex = (int)(input.vertexID % 6u);
                int cornerIndex = localVertex == 0 ? 0 :
                    localVertex == 1 ? 1 :
                    localVertex == 2 ? 2 :
                    localVertex == 3 ? 2 :
                    localVertex == 4 ? 1 : 3;

                float3 p0 = _TetherPositions[segmentIndex];
                float3 p1 = _TetherPositions[min(segmentIndex + 1, max(_TetherPointCount - 1, 0))];
                float3 segment = p1 - p0;
                float segmentLenSq = max(dot(segment, segment), 0.000001);
                float3 segmentDir = segment * rsqrt(segmentLenSq);
                float3 mid = (p0 + p1) * 0.5;
                float3 viewVector = _WorldSpaceCameraPos.xyz - mid;
                float viewLenSq = max(dot(viewVector, viewVector), 0.000001);
                float3 viewDir = viewVector * rsqrt(viewLenSq);
                float3 side = cross(viewDir, segmentDir);
                float sideLenSq = dot(side, side);
                float3 fallbackSide = cross(float3(0.0, 1.0, 0.0), segmentDir);
                float fallbackLenSq = dot(fallbackSide, fallbackSide);
                fallbackSide = fallbackLenSq > 0.000001 ? fallbackSide * rsqrt(fallbackLenSq) : float3(1.0, 0.0, 0.0);
                side = sideLenSq > 0.000001 ? side * rsqrt(sideLenSq) : fallbackSide;

                float globalStress01 = saturate(_TetherStress01);
                float segmentStress01 = saturate(_TetherSegmentTensions[segmentIndex] * _TetherSegmentStressScale);
                float stress01 = saturate(max(globalStress01, segmentStress01));
                float pulse = abs(frac(_Time.y * 6.0) * 2.0 - 1.0);
                float stressPulse = stress01 * pulse;
                float radius = max(_TetherRadius * (1.0 + stressPulse * 0.18), 0.001);
                bool useEnd = cornerIndex >= 2;
                bool positiveSide = cornerIndex == 1 || cornerIndex == 3;
                float3 basePosition = useEnd ? p1 : p0;
                float3 positionWS = basePosition + side * (positiveSide ? radius : -radius);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = lerp(_TetherColor, _TetherStressColor, stress01);
                output.color.rgb += stressPulse * 0.08;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(input.color.a - (half)HectonDitherCoverage(input.positionCS.xy));
                return half4(input.color.rgb, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
