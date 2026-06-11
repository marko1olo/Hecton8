Shader "Hecton8/VFX/PhantomDrones"
{
    Properties
    {
        _BaseTint ("Base Tint", Color) = (0.10, 0.85, 1.00, 0.85)
        _EdgeBoost ("Edge Boost", Range(0, 4)) = 1.7
        _SignalGlitch ("Signal Glitch", Range(0, 1)) = 0.28
        _SignalBandStrength ("Signal Band Strength", Range(0, 1)) = 0.22
        _SignalShearStrength ("Signal Shear Strength", Range(0, 0.5)) = 0.08
        _DistanceFadeStart ("Distance Fade Start", Float) = 45.0
        _DistanceFadeEnd ("Distance Fade End", Float) = 92.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend Off
        ZWrite On
        Cull Off
        AlphaToMask On

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define HECTON_TWO_PI 6.28318530718

            StructuredBuffer<float4x4> _PhantomMatrices;
            StructuredBuffer<float4> _PhantomColors;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseTint;
                float _EdgeBoost;
                float _SignalGlitch;
                float _SignalBandStrength;
                float _SignalShearStrength;
                float _DistanceFadeStart;
                float _DistanceFadeEnd;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float signalBand : TEXCOORD2;
                float4 color : COLOR0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float HectonHash11(float value)
            {
                float hash = frac(value * 0.1031);
                hash *= hash + 33.33;
                hash *= hash + hash;
                return frac(hash);
            }

            float HectonFastTriangleSine(float phase)
            {
                return (1.0 - abs(frac(phase * 0.15915494 + 0.25) * 2.0 - 1.0)) * 2.0 - 1.0;
            }

            float2 SafeNormalize2(float2 value, float2 fallback)
            {
                float lengthSq = dot(value, value);
                return lengthSq > 1e-6 ? value * rsqrt(lengthSq) : fallback;
            }

            float3 SafeNormalize3(float3 value, float3 fallback)
            {
                float lengthSq = dot(value, value);
                return lengthSq > 1e-6 ? value * rsqrt(lengthSq) : fallback;
            }

            float HectonDitherCoverage(float2 positionCS)
            {
                float2 pixel = floor(positionCS);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                uint instanceID = input.instanceID;
            #if UNITY_ANY_INSTANCING_ENABLED
                instanceID = unity_InstanceID;
            #endif
                float4x4 instanceMatrix = _PhantomMatrices[instanceID];
                float4 positionWS = mul(instanceMatrix, input.positionOS);
                float3 normalWS = SafeNormalize3(mul((float3x3)instanceMatrix, input.normalOS), float3(0.0, 1.0, 0.0));
                float signalPhase = frac((float)instanceID * 0.01713 + _Time.y * 0.071);
                float signalHash = HectonHash11(floor(signalPhase * 251.0) + (float)instanceID * 17.0);
                float signalGlitch = step(0.93, signalHash) * _SignalGlitch;
                float bandPhase = frac(positionWS.y * 0.073 + _Time.y * 0.21 + (float)instanceID * 0.0031);
                float signalBand = smoothstep(0.46, 0.50, bandPhase) * (1.0 - smoothstep(0.50, 0.56, bandPhase));
                float shearPhase = frac((float)instanceID * 0.031 + _Time.y * 0.43);
                float shearPulse = smoothstep(0.87, 0.91, shearPhase) * (1.0 - smoothstep(0.91, 0.98, shearPhase));
                float shearPhaseRadians = signalPhase * HECTON_TWO_PI;
                float2 shearDir = SafeNormalize2(
                    float2(HectonFastTriangleSine(shearPhaseRadians), HectonFastTriangleSine(shearPhaseRadians + 1.5707963)),
                    float2(1.0, 0.0));
                positionWS.xz += shearDir * shearPulse * _SignalShearStrength;
                float distanceFadeStartSq = _DistanceFadeStart * _DistanceFadeStart;
                float distanceFadeEnd = max(_DistanceFadeEnd, _DistanceFadeStart + 0.001);
                float distanceFadeEndSq = max(distanceFadeStartSq + 0.001, distanceFadeEnd * distanceFadeEnd);
                float3 cameraDelta = positionWS.xyz - _WorldSpaceCameraPos;
                float distanceToCameraSq = dot(cameraDelta, cameraDelta);
                float distanceFade = 1.0 - smoothstep(distanceFadeStartSq, distanceFadeEndSq, distanceToCameraSq);

                output.positionCS = TransformWorldToHClip(positionWS.xyz);
                output.normalWS = normalWS;
                output.viewDirWS = SafeNormalize3(_WorldSpaceCameraPos.xyz - positionWS.xyz, float3(0.0, 0.0, 1.0));
                output.signalBand = signalBand;
                output.color = _PhantomColors[instanceID] * _BaseTint;
                output.color.rgb = lerp(output.color.rgb, output.color.brg, signalGlitch);
                output.color.rgb += _BaseTint.rgb * signalBand * _SignalBandStrength;
                output.color.a *= distanceFade;
                output.color.a *= 1.0 + signalGlitch * 0.35;
                output.color.a *= 1.0 + signalBand * _SignalBandStrength;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half rimBase = saturate(1.0h - abs((half)dot(input.normalWS, input.viewDirWS)));
                half rimSq = rimBase * rimBase;
                half rimQuad = rimSq * rimSq;
                half rimShaped = lerp(rimBase, rimQuad, saturate(((half)_EdgeBoost - 1.0h) * 0.3333h));
                half rim = lerp(1.0h, rimShaped, saturate((half)_EdgeBoost));
                half visibility = saturate(input.color.a);
                clip(visibility - max((half)HectonDitherCoverage(input.positionCS.xy), 0.0005h));
                half emission = saturate(visibility + (rim + (half)input.signalBand * 0.18h) * visibility);
                return half4(input.color.rgb * emission, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
