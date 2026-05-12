Shader "Hecton8/VFX/AbyssalBlackSmoke"
{
    Properties
    {
        _AshTint ("Ash Tint", Color) = (0.08, 0.08, 0.08, 0.28)
        _AshHotTint ("Ash Hot Tint", Color) = (0.22, 0.17, 0.12, 0.34)
        _Softness ("Softness", Range(0.5, 4.0)) = 2.2
        _MaxViewDistance ("Max View Distance", Float) = 95
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest+30"
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend Off
        ZWrite On
        AlphaToMask On
        Cull Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct AshParticleData
            {
                float3 PositionWS;
                float Size;
                float3 VelocityWS;
                float Alpha;
                float Lifetime;
                float MaxLifetime;
                float Seed;
                float VentIndex;
            };

            StructuredBuffer<AshParticleData> _AshParticles;

            float3 _CameraPositionWS;
            float3 _CameraRightWS;
            float3 _CameraUpWS;
            float4 _AshTint;
            float4 _AshHotTint;
            float _Softness;
            float _MaxViewDistance;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 ResolveQuadCorner(uint vertexID)
            {
                if (vertexID == 0) return float2(-1.0, -1.0);
                if (vertexID == 1) return float2(-1.0,  1.0);
                if (vertexID == 2) return float2( 1.0,  1.0);
                if (vertexID == 3) return float2(-1.0, -1.0);
                if (vertexID == 4) return float2( 1.0,  1.0);
                return float2(1.0, -1.0);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                uint instanceID = input.instanceID;
            #if UNITY_ANY_INSTANCING_ENABLED
                instanceID = unity_InstanceID;
            #endif
                AshParticleData particle = _AshParticles[instanceID];
                float2 corner = ResolveQuadCorner(input.vertexID);
                float maxViewDistance = max(_MaxViewDistance, 0.001);
                float3 cameraDelta = particle.PositionWS - _CameraPositionWS;
                float distanceFade = saturate(1.0 - dot(cameraDelta, cameraDelta) / (maxViewDistance * maxViewDistance));
                float size = particle.Size * lerp(0.55, 1.0, distanceFade);
                float3 billboardOffset = (_CameraRightWS * corner.x + _CameraUpWS * corner.y) * size;
                float3 worldPosition = particle.PositionWS + billboardOffset;

                output.positionCS = TransformWorldToHClip(worldPosition);
                output.uv = corner * 0.5 + 0.5;

                float heatT = saturate(particle.Lifetime / max(particle.MaxLifetime, 0.001));
                output.color = lerp(_AshHotTint, _AshTint, heatT);
                output.color.a *= particle.Alpha;
                return output;
            }

            float FastRadialSoftness(float radial, float softness)
            {
                float radial2 = radial * radial;
                float radial4 = radial2 * radial2;
                return lerp(radial, radial4, saturate((softness - 1.0) * 0.3333));
            }

            float InterleavedGradientNoise(float2 pixelPosition)
            {
                float2 pixel = floor(pixelPosition);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float ResolveSceneDepthCutoutFade(float4 positionCS)
            {
                if (positionCS.w <= 0.0001)
                    return 1.0;

                float2 screenUV = positionCS.xy * rcp(positionCS.w) * 0.5 + 0.5;
                if (any(screenUV < 0.0) || any(screenUV > 1.0))
                    return 1.0;

                float sceneRawDepth = SampleSceneDepth(screenUV);
            #if UNITY_REVERSED_Z
                float sceneDepthValid = step(0.0001, sceneRawDepth);
            #else
                float sceneDepthValid = step(sceneRawDepth, 0.9999);
            #endif
                float rawFragmentDepth = saturate(positionCS.z * rcp(positionCS.w));
                float sceneDepthMeters = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                float fragmentDepthMeters = LinearEyeDepth(rawFragmentDepth, _ZBufferParams);
                float depthFade = saturate((sceneDepthMeters - fragmentDepthMeters) * 2.5);
                return lerp(1.0, depthFade, sceneDepthValid);
            }

            void ClipDitheredAlpha(float alpha, float4 positionCS)
            {
                clip(alpha * ResolveSceneDepthCutoutFade(positionCS) - InterleavedGradientNoise(positionCS.xy));
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 centered = input.uv * 2.0 - 1.0;
                float radial = saturate(1.0 - dot(centered, centered));
                float alpha = FastRadialSoftness(radial, _Softness) * input.color.a;
                ClipDitheredAlpha(alpha, input.positionCS);
                return half4(input.color.rgb, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Hecton8/InternalBlackError"
}
