Shader "Hidden/Hecton8/VegetationIndirectShadowCaster"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            #define HECTON_MAX_INTERACTION_POINTS 12

            CBUFFER_START(UnityPerMaterial)
                float _HectonImpostorWidth;
                float _HectonImpostorHeight;
            CBUFFER_END

            struct FloraInteractionPointGpuData
            {
                float4 positionRadius;
                float4 velocitySpeed;
            };

            StructuredBuffer<float4x4> _HectonInstanceMatrices;
            StructuredBuffer<float4> _HectonVegetationInstanceData;
            StructuredBuffer<uint> _HectonVisibleInstanceIndices;
            float4 _ChunkWorldOffset;
            float4 _GlobalFloatingOffset;
            float4 _HectonFloatingOriginOffset;
            StructuredBuffer<FloraInteractionPointGpuData> _HectonFloraInteractionPoints;

            float4 _HectonVegetationCurrentVector;
            float4 _GlobalOceanFlow;
            float4 _SargassumGlobalDriftOffset;
            float4 _HectonVegetationWakeTrailWorldRect;
            float4 _SargassumCutMaskWorldRect;
            float _HectonVegetationCurrentStrength;
            float _HectonVegetationCurrentNoiseScale;
            float _HectonVegetationCurrentTimeScale;
            float _HectonVegetationCurrentVerticalFactor;
            float _HectonVegetationWakeTrailActive;
            float _HectonVegetationWaterLevel;
            float _SargassumCutMaskActive;
            int _HectonFloraInteractionCount;
            float3 _LightDirection;

            TEXTURE2D(_HectonVegetationWakeTrailRT);
            SAMPLER(sampler_HectonVegetationWakeTrailRT);
            TEXTURE2D(_SargassumCutMaskRT);
            SAMPLER(sampler_SargassumCutMaskRT);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 shadowData : TEXCOORD1;
            };

            float3 TransformPoint(float4x4 matrixValue, float3 localPosition)
            {
                return mul(matrixValue, float4(localPosition, 1.0)).xyz + _HectonFloatingOriginOffset.xyz;
            }

            float3 TransformDirection(float4x4 matrixValue, float3 direction)
            {
                return normalize(mul((float3x3)matrixValue, direction));
            }

            float2 SafeNormalize2(float2 value)
            {
                float lenSq = dot(value, value);
                return lenSq > 0.0001 ? value * rsqrt(lenSq) : float2(1.0, 0.0);
            }

            float3 SafeNormalize3(float3 value)
            {
                float lenSq = dot(value, value);
                return lenSq > 0.0001 ? value * rsqrt(lenSq) : float3(0.0, 1.0, 0.0);
            }

            float2 ResolvePlanarCurrentDirection()
            {
                float2 flow = dot(_GlobalOceanFlow.xz, _GlobalOceanFlow.xz) > 0.0001
                    ? _GlobalOceanFlow.xz
                    : _HectonVegetationCurrentVector.xz;
                return SafeNormalize2(flow);
            }

            float ResolvePlanarCurrentStrength()
            {
                return max(length(_GlobalOceanFlow.xyz), _HectonVegetationCurrentStrength);
            }

            float Hash21(float2 value)
            {
                return frac(sin(dot(value, float2(12.9898, 78.233))) * 43758.5453);
            }

            void ResolveInstanceShape(float instanceType, float heightScale, float widthScale, out float instanceHeight, out float instanceWidth)
            {
                if (instanceType < 0.5)
                {
                    instanceHeight = lerp(0.35, 1.4, heightScale);
                    instanceWidth = lerp(0.65, 1.25, saturate(widthScale));
                    return;
                }

                if (instanceType < 1.5)
                {
                    instanceHeight = lerp(10.0, 20.0, heightScale);
                    instanceWidth = lerp(0.55, 1.6, saturate(widthScale));
                    return;
                }

                instanceHeight = lerp(0.75, 2.4, heightScale);
                instanceWidth = lerp(0.75, 1.35, saturate(widthScale));
            }

            float4 EvaluateWakeTrailData(float3 positionWS)
            {
                if (_HectonVegetationWakeTrailActive < 0.5)
                    return float4(0.5, 0.5, 0.0, 0.0);

                float2 uv = float2(
                    (positionWS.x - _HectonVegetationWakeTrailWorldRect.x) * _HectonVegetationWakeTrailWorldRect.z,
                    (positionWS.z - _HectonVegetationWakeTrailWorldRect.y) * _HectonVegetationWakeTrailWorldRect.w);
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return float4(0.5, 0.5, 0.0, 0.0);

                return SAMPLE_TEXTURE2D_LOD(_HectonVegetationWakeTrailRT, sampler_HectonVegetationWakeTrailRT, uv, 0);
            }

            half EvaluateGlobalSargassumCutMask(float3 positionWS)
            {
                if (_SargassumCutMaskActive < 0.5)
                    return 0.0h;

                float2 uv = float2(
                    (positionWS.x - _SargassumCutMaskWorldRect.x) * _SargassumCutMaskWorldRect.z,
                    (positionWS.z - _SargassumCutMaskWorldRect.y) * _SargassumCutMaskWorldRect.w);
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return 0.0h;

                return SAMPLE_TEXTURE2D(_SargassumCutMaskRT, sampler_SargassumCutMaskRT, uv).r;
            }

            half ResolveVegetationCutMask(float instanceType, float3 positionWS)
            {
                if (instanceType > 0.5 && instanceType < 1.5)
                    return 0.0h;

                return EvaluateGlobalSargassumCutMask(positionWS);
            }

            float EvaluateSargassumOrganicDensity(float2 worldXZ)
            {
                float2 sample = worldXZ * 0.024 + _SargassumGlobalDriftOffset.xz * 0.014;
                float coarse = Hash21(floor(sample));
                float fine = Hash21(floor(sample * 1.87 + 21.0));
                float wave = sin(sample.x * 1.18 + sample.y * 0.86 + _Time.y * 0.12) * 0.5 + 0.5;
                return saturate(coarse * 0.44 + fine * 0.34 + wave * 0.22);
            }

            half ResolveSargassumShadowCoverage(float3 positionWS, float heightMask)
            {
                float organicDensity = EvaluateSargassumOrganicDensity(positionWS.xz + float2(heightMask * 1.1, -heightMask * 0.9));
                float laceNoise = Hash21(floor(positionWS.xz * 1.65 + heightMask * 19.0));
                float interiorBias = lerp(0.58, 0.8, saturate(heightMask));
                return saturate(organicDensity * 1.15 + laceNoise * 0.18 - interiorBias);
            }

            float3 ResolveWakeTrailOffset(float3 evaluationPositionWS, float3 baseNormalWS, float bendMask, float instanceType)
            {
                float4 wakeTrailData = EvaluateWakeTrailData(evaluationPositionWS);
                float wakeIntensity = saturate(wakeTrailData.b);
                if (wakeIntensity <= 0.0001)
                    return float3(0.0, 0.0, 0.0);

                float2 encodedDirection = wakeTrailData.rg * 2.0 - 1.0;
                float3 wakeDirection = SafeNormalize3(float3(encodedDirection.x, 0.0, encodedDirection.y));
                float3 planarWakeDirection = SafeNormalize3(wakeDirection - baseNormalWS * dot(wakeDirection, baseNormalWS));
                float typeScale = instanceType < 0.5 ? 0.7 : (instanceType < 1.5 ? 1.0 : 0.3);
                return planarWakeDirection * (wakeIntensity * bendMask * typeScale);
            }

            float3 ResolveInteractionOffset(float3 evaluationPositionWS, float3 baseNormalWS, float bendMask)
            {
                float3 interactionOffset = float3(0.0, 0.0, 0.0);
                int activeInteractionCount = min(_HectonFloraInteractionCount, HECTON_MAX_INTERACTION_POINTS);

                [loop]
                for (int i = 0; i < activeInteractionCount; i++)
                {
                    FloraInteractionPointGpuData interactionPoint = _HectonFloraInteractionPoints[i];
                    float3 velocity = interactionPoint.velocitySpeed.xyz;
                    float speed = interactionPoint.velocitySpeed.w;
                    float speedFactor = saturate(speed * 0.18);
                    float3 delta = evaluationPositionWS - interactionPoint.positionRadius.xyz;
                    delta.y *= 0.22;

                    float bendRadius = max(interactionPoint.positionRadius.w, 0.05);
                    float dist = length(delta);
                    float proximity = saturate(1.0 - dist / bendRadius);
                    if (proximity <= 0.0001 || speedFactor <= 0.0001)
                        continue;

                    float3 planarVelocityDir = SafeNormalize3(velocity - baseNormalWS * dot(velocity, baseNormalWS));
                    interactionOffset += planarVelocityDir * (proximity * speedFactor);
                }

                return interactionOffset * bendMask;
            }

            float3 AnimatePositionWS(float3 localPosition, float3 normalOS, float2 uv, float4x4 instanceMatrix, float4 instanceData)
            {
                float3 originWS = TransformPoint(instanceMatrix, float3(0.0, 0.0, 0.0)) + _GlobalFloatingOffset.xyz;
                float instanceType = clamp(round(instanceData.x), 0.0, 2.0);
                float heightScale = saturate(instanceData.y);
                float widthScale = max(0.2, instanceData.z);
                float variation = frac(instanceData.w);
                float heightMask = saturate(uv.y);
                float bendMask = heightMask * heightMask;
                float instanceNoise = Hash21(originWS.xz + variation);
                float instanceHeight;
                float instanceWidth;
                ResolveInstanceShape(instanceType, heightScale, widthScale, instanceHeight, instanceWidth);

                if (instanceType < 0.5)
                {
                    localPosition.y = heightMask * instanceHeight;
                    localPosition.x *= instanceWidth * lerp(1.0, 0.42, heightMask);
                }
                else if (instanceType < 1.5)
                {
                    localPosition.y = heightMask * instanceHeight;
                    localPosition.x *= instanceWidth * lerp(1.0, 0.18, heightMask);
                }
                else
                {
                    localPosition.y = heightMask * instanceHeight;
                    localPosition.x *= instanceWidth * lerp(1.0, 0.30, heightMask);
                }

                float3 baseNormalWS = TransformDirection(instanceMatrix, normalOS);
                float3 driftOffsetWS = instanceType > 1.5 ? _SargassumGlobalDriftOffset.xyz : float3(0.0, 0.0, 0.0);
                float3 animatedPositionWS = TransformPoint(instanceMatrix, localPosition) + driftOffsetWS;
                float timeValue = _Time.y;
                float2 currentVector = ResolvePlanarCurrentDirection();
                float currentStrength = ResolvePlanarCurrentStrength();
                float swayWave = sin(timeValue * (0.55 + _HectonVegetationCurrentTimeScale * 0.35) + instanceNoise * 6.28318 + originWS.x * 0.015 + originWS.z * 0.01);
                animatedPositionWS.xz += currentVector * (currentStrength * 0.28 * bendMask * swayWave);
                animatedPositionWS.y += swayWave * (_HectonVegetationCurrentVerticalFactor * 0.12 * bendMask);
                animatedPositionWS += ResolveWakeTrailOffset(animatedPositionWS, baseNormalWS, bendMask, instanceType);
                animatedPositionWS += ResolveInteractionOffset(animatedPositionWS, baseNormalWS, bendMask);
                return animatedPositionWS;
            }

            Varyings ShadowVert(Attributes input, uint instanceID : SV_InstanceID)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                uint sourceInstanceIndex = _HectonVisibleInstanceIndices[instanceID];
                float4x4 instanceMatrix = _HectonInstanceMatrices[sourceInstanceIndex];
                float4 instanceData = _HectonVegetationInstanceData[sourceInstanceIndex];
                float heightMask = saturate(input.uv.y);
                float instanceType = clamp(round(instanceData.x), 0.0, 2.0);
                float3 animatedPositionWS = AnimatePositionWS(input.positionOS.xyz, input.normalOS, input.uv, instanceMatrix, instanceData);
                float3 normalWS = TransformDirection(instanceMatrix, input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(animatedPositionWS, normalWS, _LightDirection));
                output.positionWS = animatedPositionWS;
                output.shadowData = float2(instanceType, heightMask);

                #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                half cutMask = ResolveVegetationCutMask(input.shadowData.x, input.positionWS);
                clip(0.08h - cutMask);

                if (input.shadowData.x > 1.5)
                {
                    half porousCoverage = ResolveSargassumShadowCoverage(input.positionWS, input.shadowData.y);
                    clip(porousCoverage - 0.16h);
                }

                return 0;
            }
            ENDHLSL
        }
    }
}
