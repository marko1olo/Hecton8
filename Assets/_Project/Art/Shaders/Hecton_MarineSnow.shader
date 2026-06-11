Shader "Hecton8/VFX/MarineSnow"
{
    Properties
    {
        _MarineSnowTint ("Marine Snow Tint", Color) = (0.54, 0.61, 0.58, 0.55)
        _MarineSnowRenderParams ("Render Params", Vector) = (0.55, 3.2, 18.0, 0.0)
        [NoScaleOffset] _MarineSnowMaskAtlas ("Baked Mask Atlas (R Density G Bio B Flow A AO)", 2D) = "white" {}
        [NoScaleOffset] _MarineSnowNormalAtlas ("Baked Normal Atlas", 2D) = "bump" {}
        _MarineSnowAtlasParams ("Atlas Columns Rows NormalWeight MaskWeight", Vector) = (8, 8, 0, 0)
        _MarineSnowFlipbookParams ("Flipbook TimeScale LifePhase AOGain BioGain", Vector) = (0.18, 0.15, 0.22, 0.35)
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Hecton_WaterExtinction.hlsl"

            struct ParticleDataDTO
            {
                float3 Position;
                float Lifetime;
                float3 Velocity;
                float Size;
            };

            struct ParticleRenderMetaDTO
            {
                float3 PreviousPosition;
                uint Flags;
                float2 Uv;
                float2 Pad;
            };

            struct SiltParticle
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

            StructuredBuffer<ParticleDataDTO> _MarineSnowParticles;
            StructuredBuffer<ParticleRenderMetaDTO> _MarineSnowParticleMeta;
            StructuredBuffer<uint> _MarineSnowVisibleParticleIndices;
            TEXTURE2D(_MarineSnowMaskAtlas);
            SAMPLER(sampler_MarineSnowMaskAtlas);
            TEXTURE2D(_MarineSnowNormalAtlas);
            SAMPLER(sampler_MarineSnowNormalAtlas);

            struct MarineSnowFrameData
            {
                float4 CameraPositionTime;
                float4 CameraRightDeltaTime;
                float4 CameraUpDensity;
                float4 FlowFieldCenterCellSize;
                float4 ShellParams;
                float4 MetaParams;
                float4 CameraVelocityStretch;
                float4 Pad0;
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
            float4 _PropwashBiomeTint;
            float4 _MarineSnowAtlasParams;
            float4 _MarineSnowFlipbookParams;
            float4 _MarineSnowMaskAtlas_TexelSize;

            SiltParticle LoadSiltParticle(uint index)
            {
                ParticleDataDTO data = _MarineSnowParticles[index];
                ParticleRenderMetaDTO meta = _MarineSnowParticleMeta[index];
                SiltParticle particle;
                particle.Pos = data.Position;
                particle.Life = data.Lifetime;
                particle.Vel = data.Velocity;
                particle.Size = data.Size;
                particle.PrevPos = meta.PreviousPosition;
                particle.Flags = meta.Flags;
                particle.UV = meta.Uv;
                particle.Pad = meta.Pad;
                return particle;
            }

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 atlasUV : TEXCOORD2;
                float headlightBoost : TEXCOORD3;
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

            float2 DominantAxisDirection2(float2 value)
            {
                float2 absValue = abs(value);
                if (absValue.x <= 0.0001 && absValue.y <= 0.0001)
                    return float2(0.0, 1.0);

                if (absValue.x >= absValue.y)
                    return float2(value.x >= 0.0 ? 1.0 : -1.0, 0.0);

                return float2(0.0, value.y >= 0.0 ? 1.0 : -1.0);
            }

            float2 ResolveMarineSnowFrameLocalUv(float2 spriteUV)
            {
                float2 grid = max(_MarineSnowAtlasParams.xy, 1.0.xx);
                float2 halfTexelInFrame = min(_MarineSnowMaskAtlas_TexelSize.xy * grid * 0.5, float2(0.125, 0.125));
                halfTexelInFrame = max(halfTexelInFrame, float2(0.00012207, 0.00012207));
                return saturate(spriteUV) * saturate(1.0.xx - halfTexelInFrame * 2.0) + halfTexelInFrame;
            }

            float2 ResolveMarineSnowFlipbookUV(float2 spriteUV, float2 particleUV, float life01)
            {
                float2 grid = max(_MarineSnowAtlasParams.xy, 1.0.xx);
                float frameCount = max(grid.x * grid.y, 1.0);
                float phase = _MarineSnowCameraPosition_Time.w * max(_MarineSnowFlipbookParams.x, 0.0);
                phase += (1.0 - saturate(life01)) * _MarineSnowFlipbookParams.y;
                phase += particleUV.x * 0.73 + particleUV.y * 0.37;
                float frame = floor(frac(phase) * frameCount);
                float row = floor(frame * rcp(max(grid.x, 1.0)));
                float col = frame - row * grid.x;
                return (float2(col, row) + ResolveMarineSnowFrameLocalUv(spriteUV)) * rcp(grid);
            }

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
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
                SiltParticle particle = LoadSiltParticle(particleIndex);
                float active = step(0.5, _MarineSnowMetaParams.w);
                float densityScale = saturate(_MarineSnowCameraUp_Density.w);
                float2 corner = ResolveQuadCorner(input.vertexID);
                float3 cameraRight = _MarineSnowCameraRight_DeltaTime.xyz;
                float3 cameraUp = _MarineSnowCameraUp_Density.xyz;
                float maxDistance = max(_MarineSnowRenderParams.z, 0.25);
                float3 cameraDelta = particle.Pos - _MarineSnowCameraPosition_Time.xyz;
                float invMaxDistanceSq = rcp(max(maxDistance * maxDistance, 0.0001));
                float distanceFade = saturate(1.0 - dot(cameraDelta, cameraDelta) * invMaxDistanceSq);
                float isBubble = ((particle.Flags & 1u) != 0u) ? 1.0 : 0.0;
                float isPropwashSilt = ((particle.Flags & 8u) != 0u) ? 1.0 : 0.0;
                float headlightBoost = saturate(particle.Pad.y);
                float size = particle.Size * lerp(0.65, 1.0, distanceFade) * lerp(1.0, 1.65, isBubble);
                float stretchScale = max(1.0, _MarineSnowCameraVelocity_Stretch.w);
                float2 screenMotion = float2(
                    dot(-_MarineSnowCameraVelocity_Stretch.xyz, cameraRight),
                    dot(-_MarineSnowCameraVelocity_Stretch.xyz, cameraUp));
                float2 stretchAxis = DominantAxisDirection2(screenMotion);
                float2 crossAxis = float2(-stretchAxis.y, stretchAxis.x);
                float2 stretchedCorner =
                    stretchAxis * (dot(corner, stretchAxis) * stretchScale) +
                    crossAxis * dot(corner, crossAxis);
                float3 billboardOffset = (cameraRight * stretchedCorner.x + cameraUp * stretchedCorner.y) * size;
                float3 worldPosition = particle.Pos + billboardOffset;

                output.positionCS = TransformWorldToHClip(worldPosition);
                output.positionWS = particle.Pos;
                output.uv = corner * 0.5 + 0.5;
                output.atlasUV = ResolveMarineSnowFlipbookUV(output.uv, particle.UV, particle.Life);
                output.headlightBoost = headlightBoost;
                float4 propwashSiltTint = float4(saturate(_PropwashBiomeTint.rgb), _MarineSnowTint.a);
                float4 baseSiltColor = lerp(_MarineSnowTint, propwashSiltTint, isPropwashSilt);
                output.color = lerp(baseSiltColor, float4(0.72, 0.88, 0.94, _MarineSnowTint.a * 0.72), isBubble);
                output.color.rgb *= 1.0 + headlightBoost * 0.85;
                output.color.a *= active * densityScale * particle.Life * distanceFade * (1.0 + headlightBoost * 0.65);
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
                float radialShape = FastRadialSoftness(radial, _MarineSnowRenderParams.y);
                float4 maskPacked = SAMPLE_TEXTURE2D(_MarineSnowMaskAtlas, sampler_MarineSnowMaskAtlas, input.atlasUV);
                float maskWeight = saturate(_MarineSnowAtlasParams.w);
                float alpha = lerp(radialShape, saturate(maskPacked.r), maskWeight) * input.color.a;
                float coverage = step(MarineSnowDither01(input.positionCS.xy), saturate(alpha));
                Light mainLight = GetMainLight();
                half3 extinctionColor = H8WaterExtinctionResolveRgbByWorld(input.positionWS, (half)_ExtinctionLUTRuntime.y);
                half3 abyssFloor = half3(0.015h, 0.027h, 0.040h);
                half depth01 = (half)H8WaterExtinctionDepth01FromWorld(input.positionWS);
                half scatter = saturate((half)mainLight.color.r + 0.15h) * lerp(0.92h, 0.34h, depth01);
                float2 flowOffset = (maskPacked.b * 2.0 - 1.0) * maskWeight * _MarineSnowMaskAtlas_TexelSize.xy * float2(2.0, -1.35);
                float4 normalPacked = SAMPLE_TEXTURE2D(_MarineSnowNormalAtlas, sampler_MarineSnowNormalAtlas, input.atlasUV + flowOffset);
                float3 normalTS = normalize((float3)UnpackNormal(normalPacked));
                float2 centeredFlow = centered + flowOffset * _MarineSnowAtlasParams.xy * 8.0;
                float2 centeredDirection = centeredFlow * rsqrt(max(dot(centeredFlow, centeredFlow), 0.0001));
                float normalDetail = saturate(normalTS.z * 0.72 + dot(normalTS.xy, centeredDirection) * 0.28 + maskPacked.a * 0.15);
                float normalWeight = saturate(_MarineSnowAtlasParams.z) * saturate(input.headlightBoost);
                half headlightNormal = (half)lerp(1.0, lerp(0.72, 1.42, normalDetail), normalWeight);
                half ao = (half)lerp(1.0, lerp(1.0 - saturate(_MarineSnowFlipbookParams.z), 1.0, saturate(maskPacked.a)), maskWeight);
                half3 litColor = (half3)input.color.rgb * max(extinctionColor, abyssFloor) * scatter * headlightNormal * ao;
                litColor += (half3)input.color.rgb * (half)(maskPacked.g * maskWeight * saturate(input.headlightBoost) * saturate(_MarineSnowFlipbookParams.w));
                return half4(litColor, coverage);
            }
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }

            ZWrite Off
            ZTest LEqual
            ColorMask RG
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"

            struct ParticleDataDTO
            {
                float3 Position;
                float Lifetime;
                float3 Velocity;
                float Size;
            };

            struct ParticleRenderMetaDTO
            {
                float3 PreviousPosition;
                uint Flags;
                float2 Uv;
                float2 Pad;
            };

            struct SiltParticle
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

            StructuredBuffer<ParticleDataDTO> _MarineSnowParticles;
            StructuredBuffer<ParticleRenderMetaDTO> _MarineSnowParticleMeta;
            StructuredBuffer<uint> _MarineSnowVisibleParticleIndices;
            TEXTURE2D(_MarineSnowMaskAtlas);
            SAMPLER(sampler_MarineSnowMaskAtlas);

            struct MarineSnowFrameData
            {
                float4 CameraPositionTime;
                float4 CameraRightDeltaTime;
                float4 CameraUpDensity;
                float4 FlowFieldCenterCellSize;
                float4 ShellParams;
                float4 MetaParams;
                float4 CameraVelocityStretch;
                float4 Pad0;
            };

            StructuredBuffer<MarineSnowFrameData> _HectonMarineSnowFrame;

            #define _MarineSnowCameraPosition_Time (_HectonMarineSnowFrame[0].CameraPositionTime)
            #define _MarineSnowCameraRight_DeltaTime (_HectonMarineSnowFrame[0].CameraRightDeltaTime)
            #define _MarineSnowCameraUp_Density (_HectonMarineSnowFrame[0].CameraUpDensity)
            #define _MarineSnowMetaParams (_HectonMarineSnowFrame[0].MetaParams)
            #define _MarineSnowCameraVelocity_Stretch (_HectonMarineSnowFrame[0].CameraVelocityStretch)

            float4 _MarineSnowRenderParams;
            float4 _MarineSnowAtlasParams;
            float4 _MarineSnowFlipbookParams;
            float4 _MarineSnowMaskAtlas_TexelSize;

            SiltParticle LoadSiltParticle(uint index)
            {
                ParticleDataDTO data = _MarineSnowParticles[index];
                ParticleRenderMetaDTO meta = _MarineSnowParticleMeta[index];
                SiltParticle particle;
                particle.Pos = data.Position;
                particle.Life = data.Lifetime;
                particle.Vel = data.Velocity;
                particle.Size = data.Size;
                particle.PrevPos = meta.PreviousPosition;
                particle.Flags = meta.Flags;
                particle.UV = meta.Uv;
                particle.Pad = meta.Pad;
                return particle;
            }

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 positionCSNoJitter : POSITION_CS_NO_JITTER;
                float4 previousPositionCSNoJitter : PREV_POSITION_CS_NO_JITTER;
                float2 uv : TEXCOORD0;
                float alpha : TEXCOORD1;
                float2 atlasUV : TEXCOORD2;
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

            float2 DominantAxisDirection2(float2 value)
            {
                float2 absValue = abs(value);
                if (absValue.x <= 0.0001 && absValue.y <= 0.0001)
                    return float2(0.0, 1.0);

                if (absValue.x >= absValue.y)
                    return float2(value.x >= 0.0 ? 1.0 : -1.0, 0.0);

                return float2(0.0, value.y >= 0.0 ? 1.0 : -1.0);
            }

            float2 ResolveMarineSnowFrameLocalUv(float2 spriteUV)
            {
                float2 grid = max(_MarineSnowAtlasParams.xy, 1.0.xx);
                float2 halfTexelInFrame = min(_MarineSnowMaskAtlas_TexelSize.xy * grid * 0.5, float2(0.125, 0.125));
                halfTexelInFrame = max(halfTexelInFrame, float2(0.00012207, 0.00012207));
                return saturate(spriteUV) * saturate(1.0.xx - halfTexelInFrame * 2.0) + halfTexelInFrame;
            }

            float2 ResolveMarineSnowFlipbookUV(float2 spriteUV, float2 particleUV, float life01)
            {
                float2 grid = max(_MarineSnowAtlasParams.xy, 1.0.xx);
                float frameCount = max(grid.x * grid.y, 1.0);
                float phase = _MarineSnowCameraPosition_Time.w * max(_MarineSnowFlipbookParams.x, 0.0);
                phase += (1.0 - saturate(life01)) * _MarineSnowFlipbookParams.y;
                phase += particleUV.x * 0.73 + particleUV.y * 0.37;
                float frame = floor(frac(phase) * frameCount);
                float row = floor(frame * rcp(max(grid.x, 1.0)));
                float col = frame - row * grid.x;
                return (float2(col, row) + ResolveMarineSnowFrameLocalUv(spriteUV)) * rcp(grid);
            }

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
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
                SiltParticle particle = LoadSiltParticle(particleIndex);
                float active = step(0.5, _MarineSnowMetaParams.w);
                float densityScale = saturate(_MarineSnowCameraUp_Density.w);
                float2 corner = ResolveQuadCorner(input.vertexID);
                float3 cameraRight = _MarineSnowCameraRight_DeltaTime.xyz;
                float3 cameraUp = _MarineSnowCameraUp_Density.xyz;
                float maxDistance = max(_MarineSnowRenderParams.z, 0.25);
                float3 cameraDelta = particle.Pos - _MarineSnowCameraPosition_Time.xyz;
                float invMaxDistanceSq = rcp(max(maxDistance * maxDistance, 0.0001));
                float distanceFade = saturate(1.0 - dot(cameraDelta, cameraDelta) * invMaxDistanceSq);
                float isBubble = ((particle.Flags & 1u) != 0u) ? 1.0 : 0.0;
                float headlightBoost = saturate(particle.Pad.y);
                float size = particle.Size * lerp(0.65, 1.0, distanceFade) * lerp(1.0, 1.65, isBubble);
                float stretchScale = max(1.0, _MarineSnowCameraVelocity_Stretch.w);
                float2 screenMotion = float2(
                    dot(-_MarineSnowCameraVelocity_Stretch.xyz, cameraRight),
                    dot(-_MarineSnowCameraVelocity_Stretch.xyz, cameraUp));
                float2 stretchAxis = DominantAxisDirection2(screenMotion);
                float2 crossAxis = float2(-stretchAxis.y, stretchAxis.x);
                float2 stretchedCorner =
                    stretchAxis * (dot(corner, stretchAxis) * stretchScale) +
                    crossAxis * dot(corner, crossAxis);
                float3 billboardOffset = (cameraRight * stretchedCorner.x + cameraUp * stretchedCorner.y) * size;
                float3 currentWorldPosition = particle.Pos + billboardOffset;
                float3 previousWorldPosition = particle.PrevPos + billboardOffset;

                output.positionCS = TransformWorldToHClip(currentWorldPosition);
                output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, float4(currentWorldPosition, 1.0));
                output.previousPositionCSNoJitter = mul(_PrevViewProjMatrix, float4(previousWorldPosition, 1.0));
                ApplyMotionVectorZBias(output.positionCS);
                output.uv = corner * 0.5 + 0.5;
                output.atlasUV = ResolveMarineSnowFlipbookUV(output.uv, particle.UV, particle.Life);
                output.alpha = active * densityScale * particle.Life * distanceFade * (1.0 + headlightBoost * 0.65);
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

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 centered = input.uv * 2.0 - 1.0;
                float radial = saturate(1.0 - dot(centered, centered));
                float radialShape = FastRadialSoftness(radial, _MarineSnowRenderParams.y);
                float maskDensity = SAMPLE_TEXTURE2D(_MarineSnowMaskAtlas, sampler_MarineSnowMaskAtlas, input.atlasUV).r;
                float alpha = lerp(radialShape, saturate(maskDensity), saturate(_MarineSnowAtlasParams.w)) * input.alpha;
                float coverage = step(MarineSnowDither01(input.positionCS.xy), saturate(alpha));
                return float4(CalcNdcMotionVectorFromCsPositions(input.positionCSNoJitter, input.previousPositionCSNoJitter), 0, saturate(coverage));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
