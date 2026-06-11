Shader "Hecton8/Physics/TetherLineStrip"
{
    Properties
    {
        [MainColor] _TetherColor ("Tether Color", Color) = (0.22, 0.92, 0.96, 0.92)
        _TetherStressColor ("Tether Stress Color", Color) = (1.0, 0.38, 0.12, 0.96)
        _TetherStress01 ("Tether Stress", Range(0, 1)) = 0
        _TetherSegmentStressScale ("Tether Segment Stress Scale", Float) = 2.5
        _TetherRadius ("Tether Radius", Float) = 0.045
        _TetherVisualTier ("Tether Visual Tier", Float) = 0
        _TetherCrystalDensity ("Tether Salt Crystal Density", Range(0, 1)) = 0
        _TetherSiltIntensity ("Tether Silt Wake Intensity", Range(0, 1)) = 0
        _TetherVisualClock ("Tether Visual Clock", Float) = 0
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

            StructuredBuffer<float4> _TetherPositions;
            StructuredBuffer<float> _TetherSegmentTensions;

            struct TetherDrawParams
            {
                float4 Color;
                float4 StressColor;
                float4 Params0; // x=global stress, y=segment stress scale, z=point count, w=radius.
                float4 Params1; // x=indirect mode, y=visual tier, z=crystal density, w=silt intensity.
                float4 Params2; // x=visual clock, yzw reserved.
            };

            StructuredBuffer<TetherDrawParams> _TetherDrawParams;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float2 cableUV : TEXCOORD0;
                half stress01 : TEXCOORD1;
                half segmentStress01 : TEXCOORD2;
                half visualTier : TEXCOORD3;
                half4 color : COLOR0;
            };

            float HectonDitherCoverage(float2 positionCS)
            {
                float2 pixel = floor(positionCS);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float HectonHash11(float value)
            {
                float p = frac(value * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float HectonTriangle(float value)
            {
                return abs(frac(value) * 2.0 - 1.0);
            }

            float HectonHighTierFiberOcclusion(float2 cableUV, float stress01)
            {
                float occlusion = 0.0;
                [unroll]
                for (int tap = 0; tap < 16; tap++)
                {
                    float tapOffset = (tap + 0.5) * 0.0625;
                    float twistA = HectonTriangle(cableUV.y * 7.0 + cableUV.x * 0.45 + tapOffset);
                    float twistB = HectonTriangle(cableUV.y * -5.0 + cableUV.x * 0.35 + tapOffset * 1.7);
                    occlusion += smoothstep(0.18, 0.92, 1.0 - abs(twistA - twistB));
                }

                return saturate((occlusion * 0.0625) * (0.35 + stress01 * 0.65));
            }

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                TetherDrawParams drawParams = _TetherDrawParams[0];
                int pointCount = max((int)(drawParams.Params0.z + 0.5), 0);
                int segmentCount = max(pointCount - 1, 0);
                int indirectMode = drawParams.Params1.x >= 0.5 ? 1 : 0;
                int segmentIndex = indirectMode != 0
                    ? clamp((int)input.instanceID, 0, max(segmentCount - 1, 0))
                    : clamp((int)(input.vertexID / 6u), 0, max(segmentCount - 1, 0));
                int localVertex = (int)(input.vertexID % 6u);
                int cornerIndex = localVertex == 0 ? 0 :
                    localVertex == 1 ? 1 :
                    localVertex == 2 ? 2 :
                    localVertex == 3 ? 2 :
                    localVertex == 4 ? 1 : 3;

                float3 p0 = _TetherPositions[segmentIndex].xyz;
                float3 p1 = _TetherPositions[min(segmentIndex + 1, max(pointCount - 1, 0))].xyz;
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

                float globalStress01 = saturate(drawParams.Params0.x);
                float segmentStress01 = saturate(_TetherSegmentTensions[segmentIndex] * max(drawParams.Params0.y, 0.0));
                float stress01 = saturate(max(globalStress01, segmentStress01));
                float visualClock = max(drawParams.Params2.x, 0.0);
                float pulse = abs(frac(visualClock * 6.0) * 2.0 - 1.0);
                float stressPulse = stress01 * pulse;
                float radius = max(drawParams.Params0.w * (1.0 + stressPulse * 0.18), 0.001);
                bool useEnd = cornerIndex >= 2;
                bool positiveSide = cornerIndex == 1 || cornerIndex == 3;
                float3 basePosition = useEnd ? p1 : p0;
                float3 positionWS = basePosition + side * (positiveSide ? radius : -radius);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.cableUV = float2(positiveSide ? 1.0 : -1.0, segmentIndex + (useEnd ? 1.0 : 0.0));
                output.stress01 = (half)stress01;
                output.segmentStress01 = (half)segmentStress01;
                output.visualTier = (half)drawParams.Params1.y;
                output.color = lerp(drawParams.Color, drawParams.StressColor, stress01);
                output.color.rgb += stressPulse * 0.08;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(input.color.a - (half)HectonDitherCoverage(input.positionCS.xy));
                half3 color = input.color.rgb;
                UNITY_BRANCH
                if (input.visualTier >= 2.0h)
                {
                    TetherDrawParams drawParams = _TetherDrawParams[0];
                    float visualClock = max(drawParams.Params2.x, 0.0);
                    float stress01 = saturate((float)input.stress01);
                    float edge01 = saturate(abs(input.cableUV.x));
                    float fiber = HectonHighTierFiberOcclusion(input.cableUV, stress01);
                    float saltHash = HectonHash11(floor(input.cableUV.y * 37.0) + floor((input.cableUV.x + 1.0) * 13.0));
                    float salt = step(1.0 - saturate(drawParams.Params1.z) * 0.075, saltHash);
                    float saltPulse = HectonTriangle(visualClock * 9.0 + input.cableUV.y * 0.37);
                    float glint = salt * stress01 * edge01 * (0.35 + saltPulse * 0.65);
                    float siltHash = HectonHash11(floor(input.cableUV.y * 53.0) + 19.0);
                    float silt = siltHash * saturate(drawParams.Params1.w) * stress01 * (1.0 - edge01 * 0.35);
                    color *= (half)(0.84 + fiber * 0.32);
                    color += (half3)(glint * float3(0.9, 0.96, 1.0));
                    color += (half3)(silt * float3(0.20, 0.32, 0.28));

                    UNITY_BRANCH
                    if (input.visualTier >= 3.0h)
                    {
                        float ultraRim = edge01 * edge01 * edge01 * stress01;
                        color += (half3)(ultraRim * float3(0.08, 0.18, 0.22));
                    }
                }

                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
