Shader "Hecton8/Vegetation/IndirectStrip"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (0.16, 0.42, 0.18, 1)
        _TipColor ("Tip Color", Color) = (0.42, 0.76, 0.46, 1)
        _DeepTintColor ("Deep Tint Color", Color) = (0.05, 0.16, 0.18, 1)
        _TranslucencyColor ("Translucency Color", Color) = (0.30, 0.72, 0.42, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.9
        _AlphaClip ("Alpha Clip", Range(0, 1)) = 0.08
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.65
        _TranslucencyStrength ("Translucency Strength", Range(0, 2)) = 0.45
        _SurfaceWindAmplitude ("Surface Wind Amplitude", Range(0, 2)) = 0.35
        _SurfaceWindFrequency ("Surface Wind Frequency", Range(0, 8)) = 1.5
        _SurfaceWindSpeed ("Surface Wind Speed", Range(0, 8)) = 0.9
        _CurrentAmplitude ("Current Amplitude", Range(0, 2)) = 0.22
        _CurrentFrequency ("Current Frequency", Range(0, 8)) = 0.75
        _CurrentSpeed ("Current Speed", Range(0, 4)) = 0.28
        _InteractionPushStrength ("Interaction Push Strength", Range(0, 4)) = 1.2
        _NormalResponse ("Normal Response", Range(0, 1)) = 0.35
        _SurfaceBlendDepth ("Surface Blend Depth", Range(0.1, 10)) = 2.5
        _DepthTintStrength ("Depth Tint Strength", Range(0, 1)) = 0.55
        _DepthTintDistance ("Depth Tint Distance", Range(1, 200)) = 24
        _DepthAlphaStrength ("Depth Alpha Strength", Range(0, 1)) = 0.45
        _DepthAlphaDistance ("Depth Alpha Distance", Range(1, 200)) = 36
        _DistanceFadeStart ("Distance Fade Start", Range(1, 200)) = 45
        _DistanceCull ("Distance Cull", Range(1, 200)) = 65
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "UniversalMaterialType" = "Lit"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local _QUALITY_MX350 _QUALITY_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define HECTON_MAX_INTERACTION_POINTS 12

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TipColor;
                half4 _DeepTintColor;
                half4 _TranslucencyColor;
                half _Opacity;
                half _AlphaClip;
                half _AmbientStrength;
                half _TranslucencyStrength;
                half _SurfaceWindAmplitude;
                half _SurfaceWindFrequency;
                half _SurfaceWindSpeed;
                half _CurrentAmplitude;
                half _CurrentFrequency;
                half _CurrentSpeed;
                half _InteractionPushStrength;
                half _NormalResponse;
                half _SurfaceBlendDepth;
                half _DepthTintStrength;
                half _DepthTintDistance;
                half _DepthAlphaStrength;
                half _DepthAlphaDistance;
                half _DistanceFadeStart;
                half _DistanceCull;
            CBUFFER_END

            StructuredBuffer<float4x4> _HectonInstanceMatrices;
            StructuredBuffer<float4> _HectonFloraInteractionPoints;

            float4 _HectonVegetationFogColor;
            float4 _HectonVegetationAmbientColor;
            float4 _HectonVegetationCurrentVector;
            float _HectonVegetationDepth;
            float _HectonVegetationLightFactor;
            float _HectonVegetationTurbidity;
            float _HectonVegetationWaterLevel;
            float _HectonVegetationCurrentStrength;
            float _HectonVegetationCurrentNoiseScale;
            float _HectonVegetationCurrentTimeScale;
            float _HectonVegetationCurrentVerticalFactor;
            int _HectonFloraInteractionCount;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 originWS : TEXCOORD2;
                half heightMask : TEXCOORD3;
                half distanceFade : TEXCOORD4;
                half fogFactor : TEXCOORD5;
            };

            float Hash11(float value)
            {
                return frac(sin(value * 127.1) * 43758.5453);
            }

            float Hash21(float2 value)
            {
                return frac(sin(dot(value, float2(12.9898, 78.233))) * 43758.5453);
            }

            float DitherNoise(float2 positionCS)
            {
                float2 pixel = floor(positionCS);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float3 TransformPoint(float4x4 matrixValue, float3 localPosition)
            {
                return mul(matrixValue, float4(localPosition, 1.0)).xyz;
            }

            float3 TransformDirection(float4x4 matrixValue, float3 direction)
            {
                return normalize(mul((float3x3)matrixValue, direction));
            }

            Varyings Vert(Attributes input, uint instanceID : SV_InstanceID)
            {
                Varyings output;

                float4x4 instanceMatrix = _HectonInstanceMatrices[instanceID];
                float3 originWS = TransformPoint(instanceMatrix, float3(0.0, 0.0, 0.0));
                float3 basePositionWS = TransformPoint(instanceMatrix, input.positionOS.xyz);
                float3 baseNormalWS = TransformDirection(instanceMatrix, input.normalOS);

                float heightMask = saturate(input.uv.y);
                float bendMask = heightMask * heightMask;
                float2 originXZ = originWS.xz;
                float instanceNoise = Hash21(originXZ);

                float underwaterBlend = saturate((_HectonVegetationWaterLevel - originWS.y) / max(_SurfaceBlendDepth, 0.001));
                float surfaceBlend = 1.0 - underwaterBlend;

                float surfacePhase = _Time.y * _SurfaceWindSpeed + originWS.x * (_SurfaceWindFrequency * 0.35) + originWS.z * (_SurfaceWindFrequency * 0.28) + instanceNoise * 6.28318;
                float2 surfaceWind = normalize(float2(
                    sin(surfacePhase),
                    cos(surfacePhase * 1.23 + input.positionOS.y * _SurfaceWindFrequency)));

                float currentPhase = _Time.y * (_CurrentSpeed + _HectonVegetationCurrentTimeScale) +
                    (originWS.x + originWS.z) * max(_HectonVegetationCurrentNoiseScale, 0.001) +
                    instanceNoise * 11.0;
                float2 noiseFlow = float2(
                    sin(currentPhase),
                    cos(currentPhase * 0.73 + originWS.y * (_CurrentFrequency * 0.5)));
                float2 currentVector = _HectonVegetationCurrentVector.xz;
                float2 currentFlow = currentVector + noiseFlow * (_HectonVegetationCurrentStrength * 0.5);
                float currentFlowLength = length(currentFlow);
                float2 normalizedCurrentFlow = currentFlowLength > 0.0001 ? currentFlow / currentFlowLength : noiseFlow;

                float surfaceAmplitude = _SurfaceWindAmplitude;
                float currentAmplitude = _CurrentAmplitude;
                #if defined(_QUALITY_MX350)
                surfaceAmplitude *= 0.72;
                currentAmplitude *= 0.78;
                #endif

                float2 flowXZ = surfaceWind * (surfaceAmplitude * surfaceBlend) +
                    normalizedCurrentFlow * (currentAmplitude * underwaterBlend);
                float verticalOffset = sin(currentPhase * 0.65 + heightMask * 2.7) *
                    (_CurrentAmplitude * 0.12 * underwaterBlend * _HectonVegetationCurrentVerticalFactor);

                float3 interactionOffset = float3(0.0, 0.0, 0.0);
                int activeInteractionCount = min(_HectonFloraInteractionCount, HECTON_MAX_INTERACTION_POINTS);
                [loop]
                for (int i = 0; i < activeInteractionCount; i++)
                {
                    float4 interactionPoint = _HectonFloraInteractionPoints[i];
                    float3 delta = basePositionWS - interactionPoint.xyz;
                    delta.y *= 0.2;
                    float radius = max(interactionPoint.w, 0.05);
                    float distSq = dot(delta, delta);
                    float influence = saturate(1.0 - distSq / (radius * radius));
                    influence *= influence;
                    float3 interactionDirection = delta * rsqrt(max(distSq, 0.0001));
                    interactionOffset += interactionDirection * influence;
                }

                float3 animatedPositionWS = basePositionWS;
                animatedPositionWS.xz += flowXZ * bendMask;
                animatedPositionWS += interactionOffset * (_InteractionPushStrength * bendMask);
                animatedPositionWS.y += verticalOffset * bendMask;

                float distanceToCamera = distance(originWS, _WorldSpaceCameraPos);
                float distanceFade = saturate((_DistanceCull - distanceToCamera) / max(_DistanceCull - _DistanceFadeStart, 0.001));
                animatedPositionWS = lerp(originWS, animatedPositionWS, distanceFade);

                float3 combinedOffset = float3(flowXZ.x, verticalOffset, flowXZ.y) + interactionOffset * _InteractionPushStrength;
                float3 normalWS = normalize(baseNormalWS - combinedOffset * (_NormalResponse * bendMask));

                output.positionWS = animatedPositionWS;
                output.originWS = originWS;
                output.normalWS = normalWS;
                output.positionCS = TransformWorldToHClip(animatedPositionWS);
                output.heightMask = heightMask;
                output.distanceFade = distanceFade;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight();
                half3 lightDirectionWS = normalize(mainLight.direction);
                half NdotL = saturate(dot(normalWS, lightDirectionWS));
                half wrapDiffuse = saturate(NdotL * 0.5h + 0.5h);
                half backLight = saturate(dot(-normalWS, lightDirectionWS));
                half rim = pow(1.0h - saturate(dot(normalWS, viewDirectionWS)), 3.0h);

                half3 gradientColor = lerp(_BaseColor.rgb, _TipColor.rgb, input.heightMask);
                half depthBelowWater = max(0.0h, _HectonVegetationWaterLevel - input.originWS.y);
                half depthTint = saturate(depthBelowWater / max(_DepthTintDistance, 0.001h));
                half alphaDepthFade = saturate(1.0h - depthBelowWater / max(_DepthAlphaDistance, 0.001h));
                half lightFactor = saturate(_HectonVegetationLightFactor);
                half turbidity = saturate(_HectonVegetationTurbidity * 0.5h);

                half3 fogTint = lerp(gradientColor, _HectonVegetationFogColor.rgb, depthTint * _DepthTintStrength);
                half3 deepTint = lerp(fogTint, _DeepTintColor.rgb, depthTint * (0.35h + turbidity * 0.35h));
                half3 litColor = deepTint;
                litColor *= lerp(1.0h, 0.72h, turbidity);
                litColor *= lerp(0.55h, 1.0h, lightFactor);

                half3 ambient = lerp(_HectonVegetationAmbientColor.rgb, SampleSH(normalWS), 0.55h) * _AmbientStrength;
                half3 diffuse = litColor * (ambient + mainLight.color * wrapDiffuse);
                half3 transmission = _TranslucencyColor.rgb * backLight * input.heightMask * _TranslucencyStrength;
                half3 finalColor = diffuse + transmission + _TipColor.rgb * rim * 0.08h;

                half alpha = saturate(_Opacity * input.distanceFade * lerp(1.0h, alphaDepthFade, _DepthAlphaStrength));
                float dither = DitherNoise(input.positionCS.xy / max(input.positionCS.w, 0.0001));
                clip(alpha - max(_AlphaClip, dither));

                finalColor = MixFog(finalColor, input.fogFactor);
                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }
    }
}
