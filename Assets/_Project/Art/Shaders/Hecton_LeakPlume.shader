Shader "HECTON/VFX/LeakPlume"
{
    Properties
    {
        [MainTexture] _MainTex ("Plume Texture", 2D) = "white" {}
        [HDR] _TintColor ("Tint", Color) = (0.64, 0.76, 0.8, 0.18)
        _Opacity ("Opacity", Range(0, 2)) = 1
        _LuminanceBias ("Luminance Bias", Range(0, 1)) = 0.08
        _LuminancePower ("Luminance Power", Range(0.25, 4)) = 1.35
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.22
        [HideInInspector] _UseLeakParticleBuffer ("Use Leak Particle Buffer", Float) = 0
        [HideInInspector] _LeakPlumeParticleSize ("Leak Plume Particle Size", Float) = 0.18
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest+30"
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "UniversalMaterialType" = "Unlit"
        }

        Cull Off
        Lighting Off
        ZWrite On
        Blend Off
        AlphaToMask On

        Pass
        {
            Name "LeakPlume"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _TintColor;
                half _Opacity;
                half _LuminanceBias;
                half _LuminancePower;
                half _EdgeSoftness;
                float _UseLeakParticleBuffer;
                float _LeakPlumeParticleSize;
                float4x4 _SubmarineLocalToWorld;
                float3 _CameraRightWS;
                float _LeakPlumePad0;
                float3 _CameraUpWS;
                float _LeakPlumePad1;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            StructuredBuffer<float4> _LeakPlumeParticleBuffer;
            struct H8BreachJetDTO
            {
                float3 LocalPosition;
                float Radius;
                float3 Normal;
                float Intensity01;
                float Age;
                uint DamageTypeHash;
                uint Frame;
                uint Flags;
                uint Reserved0;
                uint Reserved1;
                uint Reserved2;
                uint Reserved3;
            };
            StructuredBuffer<H8BreachJetDTO> _HectonBreachJetBuffer;
            float4 _HectonBreachJetParams; // x=count, y=capacity, z=quality, w=reserved

            struct Attributes
            {
                uint instanceID : SV_InstanceID;
                uint vertexID   : SV_VertexID;
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4 color       : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4 color       : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 ResolveProceduralQuadCorner(uint vertexID)
            {
                if (vertexID == 0) return float2(-1.0, -1.0);
                if (vertexID == 1) return float2(-1.0,  1.0);
                if (vertexID == 2) return float2( 1.0,  1.0);
                if (vertexID == 3) return float2(-1.0, -1.0);
                if (vertexID == 4) return float2( 1.0,  1.0);
                return float2(1.0, -1.0);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                if (_UseLeakParticleBuffer > 0.5)
                {
                    uint instanceID = input.instanceID;
                #if UNITY_ANY_INSTANCING_ENABLED
                    instanceID = unity_InstanceID;
                #endif
                    if (_HectonBreachJetParams.x > 0.5)
                    {
                        H8BreachJetDTO jet = _HectonBreachJetBuffer[instanceID];
                        float2 corner = ResolveProceduralQuadCorner(input.vertexID);
                        float intensity = saturate(jet.Intensity01);
                        float size = max(_LeakPlumeParticleSize, 0.01) * lerp(0.8, 2.6, intensity) * max(jet.Radius, 0.05);
                        float3 centerWS = mul(_SubmarineLocalToWorld, float4(jet.LocalPosition, 1.0)).xyz;
                        float3 normalWSRaw = mul((float3x3)_SubmarineLocalToWorld, jet.Normal);
                        float3 normalWS = normalWSRaw * rsqrt(max(dot(normalWSRaw, normalWSRaw), 0.0001));
                        float forward = max(corner.y, 0.0) * size * lerp(2.0, 5.0, intensity);
                        float3 worldPosition = centerWS +
                            (_CameraRightWS * corner.x + _CameraUpWS * corner.y) * size +
                            normalWS * forward;

                        output.positionCS = TransformWorldToHClip(worldPosition);
                        output.uv = corner * 0.5 + 0.5;
                        output.color = half4(1.0h, 1.0h, 1.0h, (half)intensity);
                        return output;
                    }

                    float4 particle = _LeakPlumeParticleBuffer[instanceID];
                    float2 corner = ResolveProceduralQuadCorner(input.vertexID);
                    float size = max(_LeakPlumeParticleSize, 0.01) * lerp(0.55, 1.35, saturate(particle.w));
                    float3 centerWS = mul(_SubmarineLocalToWorld, float4(particle.xyz, 1.0)).xyz;
                    float3 worldPosition = centerWS + (_CameraRightWS * corner.x + _CameraUpWS * corner.y) * size;

                    output.positionCS = TransformWorldToHClip(worldPosition);
                    output.uv = corner * 0.5 + 0.5;
                    output.color = half4(1.0h, 1.0h, 1.0h, (half)saturate(particle.w));
                    return output;
                }

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half FastMaskPower(half value, half power)
            {
                half v2 = value * value;
                half v4 = v2 * v2;
                half low = lerp(value, v2, saturate(power - 1.0h));
                half high = lerp(v2, v4, saturate((power - 2.0h) * 0.5h));
                return power < 2.0h ? low : high;
            }

            float InterleavedGradientNoise(float2 pixelPosition)
            {
                float2 pixel = floor(pixelPosition);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            half ResolveSceneDepthCutoutFade(float4 positionCS)
            {
                if (positionCS.w <= 0.0001)
                    return 1.0h;

                float2 screenUV = positionCS.xy * rcp(positionCS.w) * 0.5 + 0.5;
                if (any(screenUV < 0.0) || any(screenUV > 1.0))
                    return 1.0h;

                float sceneRawDepth = SampleSceneDepth(screenUV);
            #if UNITY_REVERSED_Z
                float sceneDepthValid = step(0.0001, sceneRawDepth);
            #else
                float sceneDepthValid = step(sceneRawDepth, 0.9999);
            #endif
                float rawFragmentDepth = saturate(positionCS.z * rcp(positionCS.w));
                float sceneDepthMeters = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                float fragmentDepthMeters = LinearEyeDepth(rawFragmentDepth, _ZBufferParams);
                half depthFade = (half)saturate((sceneDepthMeters - fragmentDepthMeters) * 3.0);
                return lerp(1.0h, depthFade, (half)sceneDepthValid);
            }

            void ClipDitheredAlpha(half alpha, float4 positionCS)
            {
                clip((float)(alpha * ResolveSceneDepthCutoutFade(positionCS)) - InterleavedGradientNoise(positionCS.xy));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 plumeSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half luminance = dot(plumeSample.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half derivedMask = saturate((luminance - _LuminanceBias) / max(_EdgeSoftness, 0.001h));
                derivedMask = FastMaskPower(derivedMask, max(_LuminancePower, 0.001h));

                half3 color = plumeSample.rgb * _TintColor.rgb * input.color.rgb;
                half alpha = saturate(derivedMask * _TintColor.a * input.color.a * _Opacity);
                ClipDitheredAlpha(alpha, input.positionCS);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Hecton8/InternalBlackError"
}
