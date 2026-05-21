Shader "Hidden/Hecton8/Deprecated/DeferredDecal_SHINOBU275_DO_NOT_BIND"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Composite"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #define VISOR_WOUND_BLOOD 1u
            #define VISOR_WOUND_ACID 2u
            #define VISOR_WOUND_HULL_DENT 3u
            #define VISOR_WOUND_GLASS_CRACK 4u
            #define VISOR_WOUND_BURN 5u

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 screenUV : TEXCOORD0;
            };

            struct VisorWoundData
            {
                float4 LocalToWorldC0;
                float4 LocalToWorldC1;
                float4 LocalToWorldC2;
                float4 LocalToWorldC3;
                uint DecalTypeHash;
                float Opacity01;
                float BirthTime;
                uint Flags;
            };

            TEXTURE2D_X(_BlitTexture);
            TEXTURE2D_ARRAY(_GlobalVisorWoundAtlas);
            SAMPLER(sampler_GlobalVisorWoundAtlas);

            StructuredBuffer<VisorWoundData> _GlobalVisorWounds;
            int _GlobalVisorWoundCount;
            float4 _GlobalVisorWoundParams; // x atlas slices, y quality, z intensity, w atlas enabled
            float4 _GlobalVisorWoundRefractionParams; // x normal refraction intensity, y max active, z thermal, w spare
            float4 _GlobalVisorWoundTint;
            float4 _GlobalVisorWoundCameraWS;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.screenUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(output.screenUV * 2.0 - 1.0, 0.0, 1.0);
            #if UNITY_UV_STARTS_AT_TOP
                output.screenUV.y = 1.0 - output.screenUV.y;
            #endif
                return output;
            }

            bool TryResolveScenePosition(float2 screenUV, out float3 positionWS)
            {
                float rawDepth = SampleSceneDepth(screenUV);
            #if UNITY_REVERSED_Z
                bool validDepth = rawDepth > 0.0001;
            #else
                bool validDepth = rawDepth < 0.9999;
            #endif
                if (!validDepth)
                {
                    positionWS = float3(0.0, 0.0, 0.0);
                    return false;
                }

                positionWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                return true;
            }

            float3 ResolveWoundLocalPosition(VisorWoundData wound, float3 cameraRelativePosition)
            {
                float3 origin = wound.LocalToWorldC3.xyz;
                float3 relative = cameraRelativePosition - origin;
                float3 xAxis = wound.LocalToWorldC0.xyz;
                float3 yAxis = wound.LocalToWorldC1.xyz;
                float3 zAxis = wound.LocalToWorldC2.xyz;
                float xLenSq = max(dot(xAxis, xAxis), 0.0001);
                float yLenSq = max(dot(yAxis, yAxis), 0.0001);
                float zLenSq = max(dot(zAxis, zAxis), 0.0001);
                return float3(
                    dot(relative, xAxis) / xLenSq,
                    dot(relative, yAxis) / yLenSq,
                    dot(relative, zAxis) / zLenSq);
            }

            half4 SampleProceduralVisorWound(VisorWoundData wound, float3 localPosition)
            {
                float2 projectorUv = localPosition.xy + 0.5;
                float2 centered = projectorUv * 2.0 - 1.0;
                float radial = saturate(1.0 - dot(centered, centered));
                float quality = saturate(_GlobalVisorWoundParams.y);
                uint packedPayload = wound.DecalTypeHash & 255u;
                uint woundType = packedPayload & 15u;
                float birthPhase = frac(wound.BirthTime * 0.000244140625) * 4096.0;

                float branchNoise = sin(centered.x * 43.0 + centered.y * 17.0 + birthPhase * 0.13);
                float ringNoise = 0.72 + 0.28 * sin(centered.x * 37.0 + centered.y * 19.0 + packedPayload * 1.71);
                float bloodAlpha = radial * radial * lerp(1.0, ringNoise, quality);
                float mainCrack = saturate(1.0 - abs(centered.y + branchNoise * 0.045) * lerp(26.0, 44.0, quality));
                float crossCrack = saturate(1.0 - abs(centered.x - branchNoise * 0.035) * lerp(34.0, 64.0, quality));
                float crackAlpha = saturate((mainCrack + crossCrack * 0.45) * radial);
                float tornEdge = smoothstep(0.12, 0.62, radial) * (1.0 - smoothstep(0.62, 0.98, radial));

                half3 scorch = half3(0.08h, 0.055h, 0.035h);
                half3 blood = half3(0.24h, 0.010h, 0.006h);
                half3 acid = half3(0.13h, 0.30h, 0.055h);
                half3 dent = half3(0.16h, 0.15h, 0.13h);
                half3 glass = half3(0.72h, 0.86h, 0.92h);
                half3 burn = half3(0.13h, 0.045h, 0.018h);

                if (woundType == VISOR_WOUND_GLASS_CRACK)
                    return half4(glass, half(saturate(crackAlpha + tornEdge * 0.16)));

                if (woundType == VISOR_WOUND_BLOOD)
                    return half4(blood, half(bloodAlpha));

                if (woundType == VISOR_WOUND_ACID)
                    return half4(acid, half(bloodAlpha * 0.92 + tornEdge * 0.2));

                if (woundType == VISOR_WOUND_HULL_DENT)
                    return half4(dent, half(tornEdge * 0.7));

                if (woundType == VISOR_WOUND_BURN)
                    return half4(burn, half(bloodAlpha * 0.8 + tornEdge * 0.35));

                return half4(scorch, half(bloodAlpha));
            }

            half4 SampleVisorWound(VisorWoundData wound, float3 localPosition)
            {
                float2 projectorUv = localPosition.xy + 0.5;
                if (_GlobalVisorWoundParams.w > 0.5)
                {
                    uint sliceCount = (uint)max(_GlobalVisorWoundParams.x, 1.0);
                    uint packedPayload = wound.DecalTypeHash & 255u;
                    uint slice = ((packedPayload >> 4) & 15u) % sliceCount;
                    return SAMPLE_TEXTURE2D_ARRAY(_GlobalVisorWoundAtlas, sampler_GlobalVisorWoundAtlas, projectorUv, slice);
                }

                return SampleProceduralVisorWound(wound, localPosition);
            }

            void ProjectVisorWounds(float3 scenePositionWS, out half3 accumulated, out float2 refractOffset)
            {
                accumulated = half3(0.0h, 0.0h, 0.0h);
                refractOffset = float2(0.0, 0.0);
                float3 cameraRelativePosition = scenePositionWS - _GlobalVisorWoundCameraWS.xyz;
                float quality = saturate(_GlobalVisorWoundParams.y);

                [loop]
                for (int woundIndex = 0; woundIndex < 128; woundIndex++)
                {
                    if (woundIndex >= _GlobalVisorWoundCount)
                        break;

                    VisorWoundData wound = _GlobalVisorWounds[woundIndex];
                    if ((wound.Flags & 1u) == 0u || wound.Opacity01 <= 0.0001)
                        continue;

                    float3 localPosition = ResolveWoundLocalPosition(wound, cameraRelativePosition);
                    if (any(abs(localPosition) > float3(0.5, 0.5, 0.5)))
                        continue;

                    half4 woundSample = SampleVisorWound(wound, localPosition);
                    half depthWeight = lerp(1.15h, 2.0h, half(quality));
                    half depthFade = saturate(1.0h - abs(localPosition.z) * depthWeight);
                    half weight = woundSample.a * half(wound.Opacity01) * depthFade * half(_GlobalVisorWoundParams.z);
                    half4 woundTint = half4(_GlobalVisorWoundTint);
                    accumulated += woundSample.rgb * woundTint.rgb * (weight * woundTint.a);

                    if ((wound.DecalTypeHash & 15u) == VISOR_WOUND_GLASS_CRACK)
                    {
                        float2 fractureSeed = localPosition.xy + float2(0.0003, -0.0007);
                        float2 fractureNormal = fractureSeed * rsqrt(max(dot(fractureSeed, fractureSeed), 0.0001));
                        float refractionGain = max(0.0, _GlobalVisorWoundRefractionParams.x);
                        refractOffset += fractureNormal * (float)weight * refractionGain * lerp(0.0015, 0.0065, quality);
                    }
                }
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.screenUV);
                if (_GlobalVisorWoundCount <= 0)
                    return sourceColor;

                if (!TryResolveScenePosition(input.screenUV, out float3 scenePositionWS))
                    return sourceColor;

                ProjectVisorWounds(scenePositionWS, out half3 woundColor, out float2 refractOffset);
                float refractWeight = saturate(length(refractOffset) * 180.0);
                if (refractWeight > 0.001)
                {
                    float2 refractUv = clamp(input.screenUV + refractOffset, float2(0.001, 0.001), float2(0.999, 0.999));
                    half3 refractedColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, refractUv).rgb;
                    sourceColor.rgb = lerp(sourceColor.rgb, refractedColor, half(refractWeight));
                }

                return half4(sourceColor.rgb + woundColor, sourceColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
