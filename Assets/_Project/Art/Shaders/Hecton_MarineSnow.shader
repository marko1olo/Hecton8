Shader "Hecton8/VFX/MarineSnow"
{
    Properties
    {
        _MarineSnowTint ("Marine Snow Tint", Color) = (0.54, 0.61, 0.58, 0.55)
        _MarineSnowRenderParams ("Render Params", Vector) = (0.55, 3.2, 18.0, 0.0)
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
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Particle
            {
                float3 Pos;
                float Life;
                float3 Vel;
                float Size;
                float3 PrevPos;
                uint Flags;
                float2 UV;
                float2 Pad;
            };

            StructuredBuffer<Particle> _MarineSnowParticles;
            StructuredBuffer<uint> _MarineSnowVisibleParticleIndices;

            struct MarineSnowFrameData
            {
                float4 CameraPositionTime;
                float4 CameraRightDeltaTime;
                float4 CameraUpDensity;
                float4 FlowFieldCenterCellSize;
                float4 ShellParams;
                float4 MetaParams;
                float4 CameraVelocityStretch;
            };

            StructuredBuffer<MarineSnowFrameData> _HectonMarineSnowFrame;

            #define _MarineSnowCameraPosition_Time (_HectonMarineSnowFrame[0].CameraPositionTime)
            #define _MarineSnowCameraRight_DeltaTime (_HectonMarineSnowFrame[0].CameraRightDeltaTime)
            #define _MarineSnowCameraUp_Density (_HectonMarineSnowFrame[0].CameraUpDensity)
            #define _MarineSnowFlowFieldCenterCellSize (_HectonMarineSnowFrame[0].FlowFieldCenterCellSize)
            #define _MarineSnowShellParams (_HectonMarineSnowFrame[0].ShellParams)
            #define _MarineSnowMetaParams (_HectonMarineSnowFrame[0].MetaParams)
            #define _MarineSnowCameraVelocity_Stretch (_HectonMarineSnowFrame[0].CameraVelocityStretch)

            float4 _MarineSnowTint;
            float4 _MarineSnowRenderParams;

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

            float ApproxLength2(float2 value)
            {
                float2 absValue = abs(value);
                float major = max(absValue.x, absValue.y);
                float minor = min(absValue.x, absValue.y);
                return major + minor * 0.375;
            }

            float2 FastNormalize2Approx(float2 value)
            {
                float approxLength = ApproxLength2(value);
                if (!isfinite(approxLength) || approxLength <= 0.0001)
                    return float2(0.0, 1.0);

                return value * rcp(approxLength);
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
                uint particleIndex = _MarineSnowVisibleParticleIndices[instanceID];
                Particle particle = _MarineSnowParticles[particleIndex];
                float active = step(0.5, _MarineSnowMetaParams.w);
                float densityScale = saturate(_MarineSnowCameraUp_Density.w);
                float2 corner = ResolveQuadCorner(input.vertexID);
                float3 cameraRight = _MarineSnowCameraRight_DeltaTime.xyz;
                float3 cameraUp = _MarineSnowCameraUp_Density.xyz;
                float maxDistance = max(_MarineSnowRenderParams.z, 0.25);
                float3 cameraDelta = particle.Pos - _MarineSnowCameraPosition_Time.xyz;
                float invMaxDistanceSq = rcp(maxDistance * maxDistance);
                float distanceFade = saturate(1.0 - dot(cameraDelta, cameraDelta) * invMaxDistanceSq);
                float isBubble = ((particle.Flags & 1u) != 0u) ? 1.0 : 0.0;
                float size = particle.Size * lerp(0.65, 1.0, distanceFade) * lerp(1.0, 1.65, isBubble);
                float stretchScale = max(1.0, _MarineSnowCameraVelocity_Stretch.w);
                float2 screenMotion = float2(
                    dot(-_MarineSnowCameraVelocity_Stretch.xyz, cameraRight),
                    dot(-_MarineSnowCameraVelocity_Stretch.xyz, cameraUp));
                float2 stretchAxis = FastNormalize2Approx(screenMotion);
                float2 crossAxis = float2(-stretchAxis.y, stretchAxis.x);
                float2 stretchedCorner =
                    stretchAxis * (dot(corner, stretchAxis) * stretchScale) +
                    crossAxis * dot(corner, crossAxis);
                float3 billboardOffset = (cameraRight * stretchedCorner.x + cameraUp * stretchedCorner.y) * size;
                float3 worldPosition = particle.Pos + billboardOffset;

                output.positionCS = TransformWorldToHClip(worldPosition);
                output.uv = corner * 0.5 + 0.5;
                output.color = lerp(_MarineSnowTint, float4(0.72, 0.88, 0.94, _MarineSnowTint.a * 0.72), isBubble);
                output.color.a *= active * densityScale * particle.Life * distanceFade;
                return output;
            }

            float FastRadialSoftness(float radial, float softness)
            {
                float radial2 = radial * radial;
                float radial4 = radial2 * radial2;
                return lerp(radial, radial4, saturate((softness - 1.0) * 0.3333));
            }

            float MarineSnowDither01(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 centered = input.uv * 2.0 - 1.0;
                float radial = saturate(1.0 - dot(centered, centered));
                float alpha = FastRadialSoftness(radial, _MarineSnowRenderParams.y) * input.color.a;
                float coverage = step(MarineSnowDither01(input.positionCS.xy), saturate(alpha));
                return half4(input.color.rgb, coverage);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
