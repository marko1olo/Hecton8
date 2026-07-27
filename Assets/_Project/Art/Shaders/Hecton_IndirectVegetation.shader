Shader "Hecton8/Vegetation/IndirectStrip"
{
    Properties
    {
        _GrassBaseColor ("Grass Base Color", Color) = (0.18, 0.48, 0.20, 1)
        _GrassTipColor ("Grass Tip Color", Color) = (0.58, 0.82, 0.42, 1)
        _KelpBaseColor ("Kelp Base Color", Color) = (0.08, 0.24, 0.16, 1)
        _KelpTipColor ("Kelp Tip Color", Color) = (0.22, 0.56, 0.36, 1)
        _SargassumBaseColor ("Sargassum Base Color", Color) = (0.32, 0.38, 0.10, 1)
        _SargassumTipColor ("Sargassum Tip Color", Color) = (0.64, 0.56, 0.18, 1)
        _SeedlingColor ("Seedling Color", Color) = (0.18, 0.42, 0.26, 1)
        _TranslucencyColor ("Translucency Color", Color) = (0.28, 0.72, 0.38, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.92
        _AlphaClip ("Alpha Clip", Range(0, 1)) = 0.08
        [NoScaleOffset] _FloraAlphaMask ("Flora Alpha Mask", 2D) = "white" {}
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.75
        _TranslucencyStrength ("Translucency Strength", Range(0, 2)) = 0.38
        _GrassWindAmplitude ("Grass Wind Amplitude", Range(0, 2)) = 0.26
        _GrassWindFrequency ("Grass Wind Frequency", Range(0, 12)) = 6.4
        _GrassWindSpeed ("Grass Wind Speed", Range(0, 12)) = 2.8
        _KelpCurrentAmplitude ("Kelp Current Amplitude", Range(0, 4)) = 1.25
        _KelpCurrentFrequency ("Kelp Current Frequency", Range(0, 6)) = 0.85
        _KelpCurrentSpeed ("Kelp Current Speed", Range(0, 4)) = 0.42
        _SargassumWaveAmplitude ("Sargassum Wave Amplitude", Range(0, 4)) = 0.55
        _SargassumWaveFrequency ("Sargassum Wave Frequency", Range(0, 8)) = 1.2
        _SargassumWaveSpeed ("Sargassum Wave Speed", Range(0, 6)) = 1.1
        _SargassumPulsationAmplitude ("Sargassum Pulsation Amplitude", Range(0, 1)) = 0.12
        _SargassumPulsationFrequency ("Sargassum Pulsation Frequency", Range(0, 6)) = 1.4
        _SargassumPulsationSpeed ("Sargassum Pulsation Speed", Range(0, 4)) = 0.52
        _SargassumWoundCurlStrength ("Sargassum Wound Curl Strength", Range(0, 1)) = 0.2
        _InteractionPushStrength ("Interaction Push Strength", Range(0, 4)) = 1.35
        _InteractionVelocityBias ("Interaction Velocity Bias", Range(0, 1)) = 0.85
        _InteractionDistancePower ("Interaction Distance Power", Range(1, 4)) = 2.2
        _NormalResponse ("Normal Response", Range(0, 1)) = 0.32
        _AnisotropicSssStrength ("Anisotropic SSS Strength", Range(0, 2)) = 0.72
        _AnisotropicSssPower ("Anisotropic SSS Power", Range(1, 12)) = 4.5
        _OrganicSssDistortion ("Organic SSS Distortion", Range(0, 2)) = 0.45
        _OrganicSssPower ("Organic SSS Power", Range(0.1, 16)) = 4.2
        _OrganicSssScale ("Organic SSS Scale", Range(0, 4)) = 1.05
        _BacklightViewBias ("Backlight View Bias", Range(0, 1)) = 0.58
        _EdgeBloomStrength ("Edge Bloom Strength", Range(0, 2)) = 0.62
        _LocalCausticStrength ("Local Caustic Strength", Range(0, 1)) = 0.18
        _LocalCausticScale ("Local Caustic Scale", Range(0.1, 4)) = 0.82
        _LocalCausticSpeed ("Local Caustic Speed", Range(0, 4)) = 0.48
        _CullFadeDistance ("Cull Fade Distance", Range(0, 32)) = 10
        _SurfaceWaterLevelFallback ("Surface Water Level Fallback", Float) = 4900
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
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma skip_variants _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "UnityIndirect.cginc"
            #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"
            #include "Assets/_Project/Art/Shaders/Hecton_CustomLightProbeGrid.hlsl"

            #define HECTON_MAX_INTERACTION_POINTS 12
            #define HECTON_MAX_PROCEDURAL_WAKE_POINTS 32
            #define HECTON_MAX_IMPACT_SPHERES 8
            #define HECTON_KELP_FADE_START_DEPTH 150.0
            #define HECTON_KELP_FADE_END_DEPTH 200.0
            #define HECTON_ABYSS_SUN_FADE_START_DEPTH 350.0
            #define HECTON_ABYSS_SUN_BLACKOUT_DEPTH 500.0
            #define HECTON_ABYSS_SUN_ABSOLUTE_DEPTH 600.0
            #define HECTON_ABYSS_LIGHT_EXTINCTION_START_DEPTH 1000.0
            #define HECTON_ABYSS_LIGHT_EXTINCTION_FULL_DEPTH 1600.0
            #define HECTON_VEGETATION_ADDITIONAL_LIGHT_CAP 4u

            CBUFFER_START(UnityPerMaterial)
                half4 _GrassBaseColor;
                half4 _GrassTipColor;
                half4 _KelpBaseColor;
                half4 _KelpTipColor;
                half4 _SargassumBaseColor;
                half4 _SargassumTipColor;
                half4 _SeedlingColor;
                half4 _TranslucencyColor;
                half _Opacity;
                half _AlphaClip;
                half _AmbientStrength;
                half _TranslucencyStrength;
                half _GrassWindAmplitude;
                half _GrassWindFrequency;
                half _GrassWindSpeed;
                half _KelpCurrentAmplitude;
                half _KelpCurrentFrequency;
                half _KelpCurrentSpeed;
                half _SargassumWaveAmplitude;
                half _SargassumWaveFrequency;
                half _SargassumWaveSpeed;
                half _SargassumPulsationAmplitude;
                half _SargassumPulsationFrequency;
                half _SargassumPulsationSpeed;
                half _SargassumWoundCurlStrength;
                half _InteractionPushStrength;
                half _InteractionVelocityBias;
                half _InteractionDistancePower;
                half _NormalResponse;
                half _AnisotropicSssStrength;
                half _AnisotropicSssPower;
                half _OrganicSssDistortion;
                half _OrganicSssPower;
                half _OrganicSssScale;
                half _BacklightViewBias;
                half _EdgeBloomStrength;
                half _LocalCausticStrength;
                half _LocalCausticScale;
                half _LocalCausticSpeed;
                half _CullFadeDistance;
                float _SurfaceWaterLevelFallback;
                float4 _HectonVegetationRuntimeLodParams; // x pass, y near, z far, w transition
                float4 _HectonVegetationRuntimeDrawParams; // x snap flags, y impostor width, z impostor height, w indirect enabled
            CBUFFER_END

            struct FloraInteractionPointGpuData
            {
                float4 positionRadius;
                float4 velocitySpeed;
            };

            struct HectonVegetationInstanceData
            {
                float Type;
                float HeightScale;
                float WidthScale;
                float Variation;
                float TemplateIndex;
                float RuntimeState;
                float RuntimeFlags;
                float PulseFrequency;
                float4 BioluminescenceColor;
                float SwaySpeed;
                float BendAmplitude;
                float HealthNormalized;
                float Reserved0;
            };

            StructuredBuffer<float4x4> _HectonInstanceMatrices;
            StructuredBuffer<HectonVegetationInstanceData> _HectonVegetationInstanceData;
            StructuredBuffer<float> _HectonFloraPhaseSeeds;
            StructuredBuffer<float> _HectonFloraAges01;
            StructuredBuffer<float4> _HectonFloraScatterVisualPayload;
            StructuredBuffer<uint> _HectonVisibleInstanceIndices;
            StructuredBuffer<uint> _HectonFloraSnapFlags;
            StructuredBuffer<float2> _MarineSnowFlowField;
            StructuredBuffer<float4> _HectonFloraSwayDisplacementField;
            StructuredBuffer<float4> _PredatorAUPBuffer;
            float4 _ChunkWorldOffset;
            float4 _GlobalFloatingOffset;
            StructuredBuffer<FloraInteractionPointGpuData> _HectonFloraInteractionPoints;
            StructuredBuffer<float4> _HectonImpactSpheres;
            float4 _HectonFloraWakeBuffer[HECTON_MAX_PROCEDURAL_WAKE_POINTS];

            float4 _MarineSnowFlowFieldCenterCellSize;
            float4 _HectonFloraSwayFieldCenterCellSize;
            float4 _HectonFloraSwayFieldParams;
            float4 _HectonFloraSwayFieldRingOffset;
            float _HectonFloraVertexColorDebug;
            float4 _HectonVegetationFogColor;
            float4 _HectonVegetationAmbientColor;
            float4 _HectonVegetationCurrentVector;
            float4 _GlobalOceanFlow;
            float4 _HectonOceanSurfaceWave0A;
            float4 _HectonOceanSurfaceWave0B;
            float4 _HectonOceanSurfaceWave1A;
            float4 _HectonOceanSurfaceWave1B;
            float4 _HectonOceanSurfaceWave2A;
            float4 _HectonOceanSurfaceWave2B;
            float4 _HectonOceanSurfaceWaveMeta;
            float4 _HectonFloatingOriginOffset;
            float _HectonFloraScatterVisualPayloadEnabled;
            float4 _SargassumGlobalDriftOffset;
            float4 _SargassumCutMaskWorldRect;
            float4 _HectonShallowWaterFieldWorldRect;
            float4 _HectonPlayerRuntimePosition;
            float4 _HectonPlayerFloraInteractionParams;
            float4 _HectonFloraPredatorThreatParams;
            float4 _HectonFloraPredatorThreatPositionRadius;
            float4 _PredatorAUPParams;
            float4 _BiolumFlashBangAUP;
            float4 _BiolumFlashBangParams;
            float4x4 _GlobalBiolumDearLieGroups;
            float4 _GlobalBiolumParams;
            float4 _HectonFloraLifecycleParams;
            float4 _HectonFloraCascadeParams;
            float4 _HectonSubmarineWashSphere;
            float4 _HectonSubmarineWashVelocity;
            float4 _HectonSubmarineWashAupGrid;
            float4 _HectonSubmarineWashAupLocal;
            float4 _HectonFlowSynchronyParams;
            float4 _HectonFloraWakeParams;
            float _HectonSeasonCycle;
            float _SeasonCycle;
            float _HectonVegetationDepth;
            float _HectonVegetationLightFactor;
            float _HectonVegetationTurbidity;
            float _HectonVegetationWaterLevel;
            float _HectonVegetationCurrentStrength;
            float _HectonVegetationCurrentNoiseScale;
            float _HectonVegetationCurrentTimeScale;
            float _HectonVegetationCurrentVerticalFactor;
            float _HectonFloraSnapFlagsEnabled;
            float _SargassumCutMaskActive;
            float _HectonShallowWaterFieldActive;
            float _ShearFoamAmount;
            int _HectonFloraFlowFieldResolution;
            int _HectonFloraInteractionCount;
            int _HectonFloraWakeCount;
            int _HectonImpactSphereCount;
            int _PredatorAUPCount;

            CBUFFER_START(_GlobalFloraSway)
                float4 _GlobalFloraSwayGlobalFlowVector;
                float4 _GlobalFloraSwaySwayMathParams;
            CBUFFER_END

            TEXTURE2D(_SargassumCutMaskRT);
            SAMPLER(sampler_SargassumCutMaskRT);
            TEXTURE2D(_FloraAlphaMask);
            SAMPLER(sampler_FloraAlphaMask);
            TEXTURE2D(_HectonShallowWaterFieldRT);
            SAMPLER(sampler_HectonShallowWaterFieldRT);
            // Declares the abyssal buffer, its five globals, its Texture3D+sampler and
            // ResolveAbyssalFlowField. This pass is the reference for that function; it reads it from
            // the shared include so the other three passes cannot drift from it - they used to omit
            // the whole field because these declarations only ever existed here.
            #include "Assets/_Project/Art/Shaders/HectonIndirectVegetationAbyssalFlow.hlsl"

            struct Attributes
            {
                uint instanceID : SV_InstanceID;
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 originWS : TEXCOORD2;
                half heightMask : TEXCOORD3;
                half lodAlpha : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                half instanceType : TEXCOORD6;
                half kelpDepthFade : TEXCOORD7;
                half edgeMask : TEXCOORD8;
                half curvatureMask : TEXCOORD9;
                half entropyProgress : TEXCOORD10;
                half parasiteMask : TEXCOORD11;
                half runtimeState : TEXCOORD12;
                half pulseFrequency : TEXCOORD13;
                half4 biolumColor : TEXCOORD14;
                half flowMagnitude : TEXCOORD15;
                half biomeLayer : TEXCOORD16;
                half cascadeSeed : TEXCOORD17;
                half growth01 : TEXCOORD18;
                half health01 : TEXCOORD19;
                half geneticTraits : TEXCOORD20;
                half2 biolumPulseData : TEXCOORD21; // x = spatial pulse offset, y = four-state sync group plus baked AO fraction.
                half globalBiolumVertexPulse : TEXCOORD22;
                half2 uv : TEXCOORD23;
            };

            float2 HectonDecodePackedPresentation(float packedPresentationAlpha)
            {
                float packedValue = round(saturate(packedPresentationAlpha) * 65535.0);
                float biolumByte = fmod(packedValue, 256.0);
                float damageByte = floor(packedValue * 0.00390625);
                return float2(biolumByte, damageByte) * 0.0039215686;
            }

            half4 ResolveSyncedBiolumColor(half4 authoredBiolumColor)
            {
                return authoredBiolumColor;
            }

            // Canonical per-instance hash, now shared with the DepthOnly / MotionVectors / Shadow
            // passes so a plant and its shadow sway on the same wind phase. See the include header.
            #include "Assets/_Project/Art/Shaders/HectonIndirectVegetationHash.hlsl"

            // This pass is the reference for the sway wave; it now reads it from the shared include
            // instead of owning a private copy, so the other three passes cannot drift from it again.
            #include "Assets/_Project/Art/Shaders/HectonIndirectVegetationWave.hlsl"

            float ValueNoise3D(float3 samplePosition)
            {
                float3 cell = floor(samplePosition);
                float3 fracPart = frac(samplePosition);
                float3 smoothPart = fracPart * fracPart * (3.0 - 2.0 * fracPart);

                float n000 = Hash31(cell + float3(0.0, 0.0, 0.0));
                float n100 = Hash31(cell + float3(1.0, 0.0, 0.0));
                float n010 = Hash31(cell + float3(0.0, 1.0, 0.0));
                float n110 = Hash31(cell + float3(1.0, 1.0, 0.0));
                float n001 = Hash31(cell + float3(0.0, 0.0, 1.0));
                float n101 = Hash31(cell + float3(1.0, 0.0, 1.0));
                float n011 = Hash31(cell + float3(0.0, 1.0, 1.0));
                float n111 = Hash31(cell + float3(1.0, 1.0, 1.0));

                float nx00 = lerp(n000, n100, smoothPart.x);
                float nx10 = lerp(n010, n110, smoothPart.x);
                float nx01 = lerp(n001, n101, smoothPart.x);
                float nx11 = lerp(n011, n111, smoothPart.x);
                float nxy0 = lerp(nx00, nx10, smoothPart.y);
                float nxy1 = lerp(nx01, nx11, smoothPart.y);
                return lerp(nxy0, nxy1, smoothPart.z) * 2.0 - 1.0;
            }

            float ResolveIndirectVegetationQualityWeight()
            {
                float qualityWeight = _GlobalFloraSwaySwayMathParams.w;
                return isfinite(qualityWeight) ? saturate(qualityWeight) : 0.0;
            }

            float SampleCurrentNoise3D(float3 samplePosition)
            {
                float layer0 = FastSinApprox(dot(samplePosition, float3(1.11, 0.73, 1.37)));
                float layer1 = FastCosApprox(dot(samplePosition.zxy + 17.0, float3(0.83, 1.27, 1.07)));
                float layer2 = FastSinApprox(dot(samplePosition.yzx - 9.0, float3(1.41, 0.69, 0.92)));
                float cheapNoise = layer0 * 0.55 + layer1 * 0.30 + layer2 * 0.15;
                float highTapWeight = smoothstep(0.45, 0.95, ResolveIndirectVegetationQualityWeight());
                if (highTapWeight <= 0.0001)
                    return cheapNoise;

                float lowFrequency = ValueNoise3D(samplePosition);
                float highFrequency = ValueNoise3D(samplePosition * 1.83 + float3(19.7, 7.1, 13.4));
                float richNoise = lowFrequency * 0.68 + highFrequency * 0.32;
                return lerp(cheapNoise, richNoise, highTapWeight);
            }

            float2 SampleMarineSnowFlowFieldXZ(float3 positionWS)
            {
                int resolution = _HectonFloraFlowFieldResolution;
                float cellSize = max(_MarineSnowFlowFieldCenterCellSize.w, 0.001);
                if (resolution <= 1 || cellSize <= 0.0)
                    return float2(0.0, 0.0);

                float halfExtent = (resolution - 1) * cellSize * 0.5;
                float2 centerXZ = _MarineSnowFlowFieldCenterCellSize.xz;
                float2 gridPosition = ((positionWS.xz - centerXZ) + halfExtent.xx) / cellSize;
                gridPosition = clamp(gridPosition, float2(0.0, 0.0), float2(resolution - 1, resolution - 1));

                int2 baseCell = (int2)floor(gridPosition);
                int2 nextCell = min(baseCell + 1, int2(resolution - 1, resolution - 1));
                float2 fracValue = frac(gridPosition);

                int index00 = baseCell.x + baseCell.y * resolution;
                int index10 = nextCell.x + baseCell.y * resolution;
                int index01 = baseCell.x + nextCell.y * resolution;
                int index11 = nextCell.x + nextCell.y * resolution;

                float2 sample00 = _MarineSnowFlowField[index00];
                float2 sample10 = _MarineSnowFlowField[index10];
                float2 sample01 = _MarineSnowFlowField[index01];
                float2 sample11 = _MarineSnowFlowField[index11];

                float2 sample0 = lerp(sample00, sample10, fracValue.x);
                float2 sample1 = lerp(sample01, sample11, fracValue.x);
                return lerp(sample0, sample1, fracValue.y);
            }

            float3 ResolveMarineSnowFlowField(float3 positionWS)
            {
                float2 flowXZ = SampleMarineSnowFlowFieldXZ(positionWS);
                return float3(flowXZ.x, 0.0, flowXZ.y);
            }

            int WrapFloraSwayFieldCoord(int value, int resolution)
            {
                int safeResolution = max(resolution, 1);
                int wrapped = value % safeResolution;
                return wrapped < 0 ? wrapped + safeResolution : wrapped;
            }

            float4 SampleFloraSwayFieldCell(int3 cell, int resolution, int3 ringOffset)
            {
                int3 safeCell = clamp(cell, int3(0, 0, 0), int3(resolution - 1, resolution - 1, resolution - 1));
                int3 physicalCell = int3(
                    WrapFloraSwayFieldCoord(safeCell.x + ringOffset.x, resolution),
                    WrapFloraSwayFieldCoord(safeCell.y + ringOffset.y, resolution),
                    WrapFloraSwayFieldCoord(safeCell.z + ringOffset.z, resolution));
                int index = physicalCell.x + physicalCell.y * resolution + physicalCell.z * resolution * resolution;
                return _HectonFloraSwayDisplacementField[index];
            }

            float3 ResolveFloraSwayFieldOffset(float3 positionWS, half bendMask, half heightMask, float instanceType, out float fieldWeight)
            {
                fieldWeight = 0.0;
                int resolution = (int)max(_HectonFloraSwayFieldParams.x, 0.0);
                float active = saturate(_HectonFloraSwayFieldParams.y);
                float rawQualityWeight = _HectonFloraSwayFieldParams.z;
                float qualityWeight = isfinite(rawQualityWeight) ? saturate(rawQualityWeight) : 0.0;
                float cellSize = max(_HectonFloraSwayFieldCenterCellSize.w, 0.001);
                if (active <= 0.0001 || resolution <= 1 || bendMask <= 0.0001 || cellSize <= 0.001)
                    return float3(0.0, 0.0, 0.0);

                float halfExtent = (resolution - 1) * cellSize * 0.5;
                float3 gridPosition = ((positionWS - _HectonFloraSwayFieldCenterCellSize.xyz) + halfExtent.xxx) / cellSize;
                if (any(gridPosition < float3(0.0, 0.0, 0.0)) || any(gridPosition > float3(resolution - 1, resolution - 1, resolution - 1)))
                    return float3(0.0, 0.0, 0.0);

                float3 baseCellFloat = floor(gridPosition);
                int3 baseCell = (int3)baseCellFloat;
                int3 ringOffset = (int3)round(_HectonFloraSwayFieldRingOffset.xyz);
                float3 cellFrac = saturate(gridPosition - baseCellFloat);
                float trilinearWeight = smoothstep(0.22, 0.55, qualityWeight);
                float4 fieldSample = SampleFloraSwayFieldCell((int3)round(gridPosition), resolution, ringOffset);
                if (trilinearWeight > 0.001)
                {
                    cellFrac *= trilinearWeight;
                    float4 c000 = SampleFloraSwayFieldCell(baseCell + int3(0, 0, 0), resolution, ringOffset);
                    float4 c100 = SampleFloraSwayFieldCell(baseCell + int3(1, 0, 0), resolution, ringOffset);
                    float4 c010 = SampleFloraSwayFieldCell(baseCell + int3(0, 1, 0), resolution, ringOffset);
                    float4 c110 = SampleFloraSwayFieldCell(baseCell + int3(1, 1, 0), resolution, ringOffset);
                    float4 c001 = SampleFloraSwayFieldCell(baseCell + int3(0, 0, 1), resolution, ringOffset);
                    float4 c101 = SampleFloraSwayFieldCell(baseCell + int3(1, 0, 1), resolution, ringOffset);
                    float4 c011 = SampleFloraSwayFieldCell(baseCell + int3(0, 1, 1), resolution, ringOffset);
                    float4 c111 = SampleFloraSwayFieldCell(baseCell + int3(1, 1, 1), resolution, ringOffset);
                    fieldSample = lerp(
                        lerp(lerp(c000, c100, cellFrac.x), lerp(c010, c110, cellFrac.x), cellFrac.y),
                        lerp(lerp(c001, c101, cellFrac.x), lerp(c011, c111, cellFrac.x), cellFrac.y),
                        cellFrac.z);
                }
                if (!all(isfinite(fieldSample)))
                    return float3(0.0, 0.0, 0.0);

                float energy = saturate(fieldSample.w) * active;
                float typeScale = instanceType < 0.5 ? 0.62 : (instanceType < 1.5 ? 1.22 : 0.68);
                float heightScale = bendMask * lerp(0.16, 1.18, heightMask);
                float overkillGain = lerp(0.74, 1.18, qualityWeight);
                fieldWeight = energy * heightScale;
                return fieldSample.xyz * (heightScale * typeScale * overkillGain);
            }

            float3 ResolveGlobalAmbientFloraSwayOffset(float3 positionWS, half stiffnessMask, half heightMask, float instanceType)
            {
                float4 globalFlowVector = _GlobalFloraSwayGlobalFlowVector;
                float4 swayMathParams = _GlobalFloraSwaySwayMathParams;
                float3 flowDirection = globalFlowVector.xyz;
                float lengthSq = dot(flowDirection, flowDirection);
                if (lengthSq <= 0.0001 || !all(isfinite(globalFlowVector)) || !all(isfinite(swayMathParams)))
                    return float3(0.0, 0.0, 0.0);

                flowDirection *= rsqrt(lengthSq);
                float qualityWeight = saturate(swayMathParams.w);
                float qualityGate = smoothstep(0.1, 0.4, qualityWeight);
                if (qualityGate <= 0.0001)
                    return float3(0.0, 0.0, 0.0);

                float amplitude = max(swayMathParams.y, 0.0) * qualityGate;
                float frequency = max(swayMathParams.z, 0.0);
                float phase = dot(positionWS, flowDirection) * frequency;
                float wave = FastSinApprox(swayMathParams.x + phase);
                float typeScale = instanceType < 0.5 ? 0.62 : (instanceType < 1.5 ? 1.0 : 0.46);
                float bend = saturate(stiffnessMask) * saturate(heightMask * heightMask);
                return flowDirection * (wave * amplitude * bend * typeScale);
            }


            float3 SafeNormalize3(float3 value);

            float ResolveFlowSynchronyPhase(float3 positionWS, float instanceNoise)
            {
                return _HectonFlowSynchronyParams.z + dot(positionWS.xz, float2(0.031, -0.027)) + instanceNoise * 6.28318;
            }

            float3 ResolveFlowSynchronyOffset(float3 positionWS, float bendMask, float instanceType, float instanceNoise)
            {
                if (bendMask <= 0.0001)
                    return float3(0.0, 0.0, 0.0);

                float3 flowSample = ResolveMarineSnowFlowField(positionWS) + ResolveAbyssalFlowField(positionWS);
                float flowMagnitudeSq = dot(flowSample.xz, flowSample.xz);
                if (flowMagnitudeSq <= 0.00000001)
                    return float3(0.0, 0.0, 0.0);

                float typeScale = instanceType < 0.5 ? 0.24 : (instanceType < 1.5 ? 0.42 : 0.18);
                float flowWave = FastSinApprox(ResolveFlowSynchronyPhase(positionWS, instanceNoise));
                float flowScale = flowWave * max(_HectonFlowSynchronyParams.x, 1.0) * typeScale * bendMask;
                return float3(flowSample.x, 0.0, flowSample.z) * flowScale;
            }

            float ResolveBayer4x4(float2 pixel)
            {
                float2 cell = fmod(pixel, 4.0);
                return (
                    (cell.x < 0.5 && cell.y < 0.5) ? 0.0 :
                    (cell.x < 1.5 && cell.y < 0.5) ? 8.0 :
                    (cell.x < 2.5 && cell.y < 0.5) ? 2.0 :
                    (cell.y < 0.5) ? 10.0 :
                    (cell.x < 0.5 && cell.y < 1.5) ? 12.0 :
                    (cell.x < 1.5 && cell.y < 1.5) ? 4.0 :
                    (cell.x < 2.5 && cell.y < 1.5) ? 14.0 :
                    (cell.y < 1.5) ? 6.0 :
                    (cell.x < 0.5 && cell.y < 2.5) ? 3.0 :
                    (cell.x < 1.5 && cell.y < 2.5) ? 11.0 :
                    (cell.x < 2.5 && cell.y < 2.5) ? 1.0 :
                    (cell.y < 2.5) ? 9.0 :
                    (cell.x < 0.5) ? 15.0 :
                    (cell.x < 1.5) ? 7.0 :
                    (cell.x < 2.5) ? 13.0 :
                    5.0) / 16.0;
            }

            float TemporalDitherNoise(float2 positionCS, float lodAlpha)
            {
                float2 pixel = floor(positionCS);
                float framePhase = fmod(floor(_Time.y * 60.0), 4.0);
                float bayer = ResolveBayer4x4(pixel + float2(framePhase, framePhase * 2.0));
                float hash = frac(52.9829189 * frac(dot(pixel + framePhase, float2(0.06711056, 0.00583715))));
                float temporalBlend = saturate(1.0 - abs(lodAlpha * 2.0 - 1.0));
                return lerp(hash, bayer, temporalBlend);
            }

            float InterleavedGradientNoise(float2 positionCS)
            {
                float2 pixel = floor(positionCS);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float ResolveVegetationIgn(float4 positionCS)
            {
                float2 pixel = floor(positionCS.xy);
                return InterleavedGradientNoise(pixel);
            }

            float LinearStep01(float edge0, float edge1, float value)
            {
                return saturate((value - edge0) * rcp(max(edge1 - edge0, 0.0001)));
            }

            half ResolveLodDitherCoverage(half ignNoise, float lodAlpha)
            {
                return (half)step(ignNoise, saturate(lodAlpha));
            }

            half ResolveVegetationVisibilityGate(half signal, half threshold, half feather)
            {
                return saturate((signal - threshold) * rcp(max(feather, 0.0001h)));
            }

            float3 TransformPoint(float4x4 matrixValue, float3 localPosition)
            {
                return mul(matrixValue, float4(localPosition, 1.0)).xyz;
            }

            float3 TransformDirection(float4x4 matrixValue, float3 direction)
            {
                return SafeNormalize3(mul((float3x3)matrixValue, direction));
            }

            float ResolveWaterLevel()
            {
                return _HectonVegetationWaterLevel > 0.01 ? _HectonVegetationWaterLevel : _SurfaceWaterLevelFallback;
            }

            float EvaluateOceanGerstnerLift(float2 worldXZ, float4 waveA, float4 waveB, float timeSeconds)
            {
                if (waveB.w < 0.5 || waveA.z <= 0.0 || waveA.w <= 0.01)
                    return 0.0;

                float directionLenSq = dot(waveA.xy, waveA.xy);
                float2 direction = directionLenSq > 0.0001 ? waveA.xy * rsqrt(max(directionLenSq, 0.0001)) : float2(1.0, 0.0);
                float waveNumber = 6.28318530718 * rcp(max(0.01, waveA.w));
                float phaseVelocity = (0.85 + waveA.w * 0.23) * max(0.01, waveB.z);
                float phase = waveNumber * dot(direction, worldXZ) - phaseVelocity * waveNumber * timeSeconds + waveB.y;
                return FastCosApprox(phase) * waveA.z;
            }

            float EvaluateOceanSurfaceLift(float2 worldXZ)
            {
                if (_HectonOceanSurfaceWaveMeta.x < 0.5)
                    return 0.0;

                float timeSeconds = _HectonOceanSurfaceWaveMeta.y > 0.0 ? _HectonOceanSurfaceWaveMeta.y : _Time.y;
                float lift = 0.0;
                lift += EvaluateOceanGerstnerLift(worldXZ, _HectonOceanSurfaceWave0A, _HectonOceanSurfaceWave0B, timeSeconds);
                lift += EvaluateOceanGerstnerLift(worldXZ, _HectonOceanSurfaceWave1A, _HectonOceanSurfaceWave1B, timeSeconds);
                lift += EvaluateOceanGerstnerLift(worldXZ, _HectonOceanSurfaceWave2A, _HectonOceanSurfaceWave2B, timeSeconds);
                return lift;
            }

            float ResolveWaterDepth(float3 positionWS)
            {
                return max(0.0, ResolveWaterLevel() - positionWS.y);
            }

            float ResolveAbyssalFactor(float3 positionWS)
            {
                return saturate((ResolveWaterDepth(positionWS) - HECTON_ABYSS_SUN_FADE_START_DEPTH) /
                    (HECTON_ABYSS_SUN_BLACKOUT_DEPTH - HECTON_ABYSS_SUN_FADE_START_DEPTH));
            }

            float ResolveAbyssalSunVisibility(float3 positionWS)
            {
                float waterDepth = ResolveWaterDepth(positionWS);
                if (waterDepth >= HECTON_ABYSS_SUN_ABSOLUTE_DEPTH)
                    return 0.0;

                float normalizedDepth = saturate((waterDepth - HECTON_ABYSS_SUN_FADE_START_DEPTH) /
                    (HECTON_ABYSS_SUN_ABSOLUTE_DEPTH - HECTON_ABYSS_SUN_FADE_START_DEPTH));
                return exp2(-12.0 * normalizedDepth * normalizedDepth);
            }

            float ResolveAbyssalAmbientVisibility(float3 positionWS)
            {
                float waterDepth = ResolveWaterDepth(positionWS);
                if (waterDepth >= HECTON_ABYSS_SUN_ABSOLUTE_DEPTH)
                    return 0.0;

                float normalizedDepth = saturate((waterDepth - HECTON_ABYSS_SUN_FADE_START_DEPTH) /
                    (HECTON_ABYSS_SUN_ABSOLUTE_DEPTH - HECTON_ABYSS_SUN_FADE_START_DEPTH));
                return exp2(-9.0 * normalizedDepth * normalizedDepth);
            }

            float ResolveAbyssalAdditionalLightVisibility(float3 positionWS, float cameraDistanceSq)
            {
                float waterDepth = ResolveWaterDepth(positionWS);
                if (waterDepth <= HECTON_ABYSS_LIGHT_EXTINCTION_START_DEPTH)
                    return 1.0;

                float normalizedDepth = saturate((waterDepth - HECTON_ABYSS_LIGHT_EXTINCTION_START_DEPTH) *
                    rcp(max(HECTON_ABYSS_LIGHT_EXTINCTION_FULL_DEPTH - HECTON_ABYSS_LIGHT_EXTINCTION_START_DEPTH, 0.001)));
                float extinction = lerp(0.0, 0.026, normalizedDepth * normalizedDepth);
                float cameraDistanceProxy = min(cameraDistanceSq * 0.01, 220.0);
                return exp2(-cameraDistanceProxy * extinction);
            }

            float EvaluateSchlickPhase(float cosTheta, float anisotropy)
            {
                float k = anisotropy * 0.5;
                float denominator = max(1.0 - k * cosTheta, 0.08);
                return (1.0 - k * k) * rcp(12.56637 * denominator * denominator);
            }

            float ApproxMagnitude2(float2 value)
            {
                float2 axis = abs(value);
                float major = max(axis.x, axis.y);
                float minor = min(axis.x, axis.y);
                return major + minor * 0.375;
            }

            float ApproxMagnitude3(float3 value)
            {
                float3 axis = abs(value);
                float major = max(max(axis.x, axis.y), axis.z);
                float minor = min(min(axis.x, axis.y), axis.z);
                float mid = axis.x + axis.y + axis.z - major - minor;
                return major + mid * 0.375 + minor * 0.125;
            }

            float2 SafeNormalize2(float2 value)
            {
                float approxLen = ApproxMagnitude2(value);
                return approxLen > 0.0001 ? value * rcp(approxLen) : float2(1.0, 0.0);
            }

            float3 SafeNormalize3(float3 value)
            {
                float approxLen = ApproxMagnitude3(value);
                return approxLen > 0.0001 ? value * rcp(approxLen) : float3(0.0, 1.0, 0.0);
            }

            float SanitizeNonNegativeFinite(float value)
            {
                return isfinite(value) ? max(value, 0.0) : 0.0;
            }

            float SanitizePositiveFinite(float value, float fallbackValue)
            {
                return isfinite(value) && value > fallbackValue ? value : fallbackValue;
            }

            float3 SanitizeFinite3(float3 value, float3 fallbackValue)
            {
                return all(isfinite(value)) ? value : fallbackValue;
            }

            void DecodeProceduralWakePacked(float packedRadiusIntensity, out float radius, out float intensity)
            {
                float packedValue = isfinite(packedRadiusIntensity) ? max(0.0, packedRadiusIntensity) : 0.0;
                float radiusQuantized = floor(packedValue * 0.0009765625);
                float intensityQuantized = packedValue - radiusQuantized * 1024.0;
                radius = max(radiusQuantized * 0.0625, 0.001);
                intensity = saturate(intensityQuantized * 0.0009775171);
            }

            float3 ResolveProceduralWakeOffset(
                float3 positionWS,
                half bendMask,
                half heightMask,
                float instanceType,
                out float wakeShear)
            {
                wakeShear = 0.0;
                float wakeQuality = smoothstep(0.12, 0.65, ResolveIndirectVegetationQualityWeight());
                if (wakeQuality <= 0.0001)
                    return float3(0.0, 0.0, 0.0);

                float3 wakeOffset = float3(0.0, 0.0, 0.0);
                int wakeCount = min(_HectonFloraWakeCount, HECTON_MAX_PROCEDURAL_WAKE_POINTS);
                UNITY_LOOP
                for (int wakeIndex = 0; wakeIndex < wakeCount; wakeIndex++)
                {
                    float4 wake = _HectonFloraWakeBuffer[wakeIndex];
                    float3 wakePositionWS = SanitizeFinite3(wake.xyz, positionWS);
                    float radius;
                    float intensity;
                    DecodeProceduralWakePacked(wake.w, radius, intensity);
                    float3 worldPos = positionWS;
                    float3 wakeDelta = worldPos - wakePositionWS;
                    float distanceSq = dot(wakeDelta, wakeDelta);
                    float radiusSq = max(radius * radius, 0.001);
                    float influence = saturate(1.0 - distanceSq * rcp(radiusSq));
                    influence = influence * influence * (3.0 - 2.0 * influence);
                    float2 radialDirection = SafeNormalize2(wakeDelta.xz + float2(0.001, -0.001));
                    float typeScale = instanceType < 0.5 ? 0.55 : (instanceType < 1.5 ? 1.35 : 0.72);
                    float rootPinnedHeight = bendMask * lerp(0.22, 1.15, heightMask);
                    float bendStrength = influence * intensity * rootPinnedHeight * typeScale * wakeQuality;
                    wakeOffset.xz += radialDirection * (bendStrength * radius * 0.22);
                    wakeOffset.y += bendStrength * (instanceType < 1.5 ? 0.08 : 0.025);
                    wakeShear = max(wakeShear, influence * intensity);
                }

                return wakeOffset;
            }


            float2 ResolvePlanarOceanFlowDirection(float2 fallbackFlow)
            {
                float2 flow = dot(_GlobalOceanFlow.xz, _GlobalOceanFlow.xz) > 0.0001 ? _GlobalOceanFlow.xz : fallbackFlow;
                return SafeNormalize2(flow);
            }

            float ResolvePlanarOceanFlowStrength(float2 fallbackFlow, float fallbackStrength)
            {
                float2 flow = dot(_GlobalOceanFlow.xz, _GlobalOceanFlow.xz) > 0.0001 ? _GlobalOceanFlow.xz : fallbackFlow;
                float flowStrengthSq = dot(flow, flow);
                return max(saturate(flowStrengthSq), fallbackStrength);
            }

            float3 ResolveCausticSamplePositionWS(float3 positionWS)
            {
                return positionWS;
            }

            half ResolveLocalLightCaustic(float3 positionWS, half3 normalWS, half heightMask)
            {
                float3 samplePositionWS = ResolveCausticSamplePositionWS(positionWS);
                float scale = max(_LocalCausticScale, 0.05h);
                float2 causticUv = samplePositionWS.xz * scale
                    + float2(_Time.y * _LocalCausticSpeed, _Time.y * (_LocalCausticSpeed * 0.67h));
                float primaryWave = FastSinApprox(causticUv.x * 2.1 + FastSinApprox(causticUv.y * 1.2));
                float secondaryWave = FastCosApprox(causticUv.y * 2.4 - causticUv.x * 0.9);
                float tertiaryWave = FastSinApprox((causticUv.x + causticUv.y) * 1.36 + _Time.y * (_LocalCausticSpeed * 0.53));
                half normalMod = saturate(0.34h + abs(normalWS.y) * 0.42h + heightMask * 0.16h);
                half caustic = saturate(0.56h + (primaryWave * secondaryWave + tertiaryWave * 0.42h) * _LocalCausticStrength);
                return lerp(1.0h, caustic, saturate(_LocalCausticStrength * normalMod));
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

            uint MathHashAup3(uint3 value)
            {
                return MathHashUint3(value);
            }

            float4 ResolveAupGeneticHash(float3 aup)
            {
                int3 quantizedAup = (int3)floor(aup * 0.03125);
                uint3 seed = (uint3)(quantizedAup + int3(1048576, 1048576, 1048576));
                return float4(
                    Hash01FromUint(MathHashAup3(seed ^ uint3(0xA511E9B3u, 0x63D83595u, 0xB6C4A793u))),
                    Hash01FromUint(MathHashAup3(seed ^ uint3(0x1B56C4E9u, 0xC13FA9A9u, 0x91E10DA5u))),
                    Hash01FromUint(MathHashAup3(seed ^ uint3(0x8F6E37A1u, 0x4D2C6DFBu, 0xE9B5DBB7u))),
                    Hash01FromUint(MathHashAup3(seed ^ uint3(0x7FEB352Du, 0x846CA68Bu, 0xD1B54A35u))));
            }

            void ResolveAupGeneticShape(
                float4 genetics,
                float instanceType,
                out float heightMultiplier,
                out float widthMultiplier,
                out float2 leanDirection,
                out float leanMeters)
            {
                float tallRange = instanceType < 0.5 ? 0.16 : (instanceType < 1.5 ? 0.24 : 0.18);
                float wideRange = instanceType < 0.5 ? 0.13 : (instanceType < 1.5 ? 0.20 : 0.15);
                heightMultiplier = lerp(1.0 - tallRange, 1.0 + tallRange, genetics.x);
                widthMultiplier = lerp(1.0 - wideRange, 1.0 + wideRange, genetics.y);

                float2 rawLean = genetics.xy * 2.0 - 1.0;
                leanDirection = SafeNormalize2(rawLean + float2(0.001, -0.001));
                float maxLean = instanceType < 0.5 ? 0.08 : (instanceType < 1.5 ? 0.85 : 0.22);
                leanMeters = (genetics.z * 2.0 - 1.0) * maxLean * lerp(0.35, 1.0, genetics.w);
            }

            float DecodeGeneticTraits(float runtimeFlags)
            {
                float packed = floor(max(runtimeFlags, 0.0));
                return fmod(floor(packed / 256.0), 256.0);
            }

            half HasGeneticTrait(half geneticTraits, half traitBit)
            {
                return (half)step(0.5h, fmod(floor(geneticTraits / traitBit), 2.0h));
            }

            float ResolveSpatialHashPulseOffset(float3 localPulseSeed)
            {
                const float cellSizeMeters = 24.0;
                float2 spatialCell = floor((localPulseSeed.xy + localPulseSeed.zz * float2(5.17, -3.43)) / cellSizeMeters);
                return Hash21(spatialCell + float2(17.31, 91.77)) * 6.28318;
            }

            half3 ResolveSeasonalColorDrift(half3 color, half biomeLayer, float3 positionWS)
            {
                float safeSeasonCycle = isfinite(_SeasonCycle) ? _SeasonCycle : 0.0;
                half season01 = (half)frac(max(SanitizeNonNegativeFinite(_HectonSeasonCycle), safeSeasonCycle));
                half shelfMask = 1.0h - step(2.5h, biomeLayer);
                half spatialBias = (half)Hash21(floor(positionWS.xz * 0.0025));
                half bloom = 0.5h + 0.5h * (half)FastSinApprox((season01 + spatialBias * 0.035h) * 6.28318h);
                half decay = 0.5h + 0.5h * (half)FastCosApprox(((season01 - 0.72h) + spatialBias * 0.025h) * 6.28318h);
                half3 bloomTint = half3(0.90h, 1.08h, 1.02h);
                half3 decayTint = half3(1.08h, 0.91h, 0.72h);
                half3 drifted = color * lerp(half3(1.0h, 1.0h, 1.0h), bloomTint, bloom * 0.08h * shelfMask);
                return drifted * lerp(half3(1.0h, 1.0h, 1.0h), decayTint, decay * 0.11h * shelfMask);
            }

            float ResolveLodAlpha(float distanceToCameraSq, float passMode)
            {
                float nearDistance = max(_HectonVegetationRuntimeLodParams.y, 0.01);
                float farDistance = max(_HectonVegetationRuntimeLodParams.z, nearDistance);
                float transitionRange = max(_HectonVegetationRuntimeLodParams.w, 0.01);
                float nearFadeStart = max(0.0, nearDistance - transitionRange);
                float nearFadeEnd = nearDistance + transitionRange;
                float nearFadeStartSq = nearFadeStart * nearFadeStart;
                float nearFadeEndSq = nearFadeEnd * nearFadeEnd;
                float nearBand = smoothstep(nearFadeStartSq, nearFadeEndSq, distanceToCameraSq);

                float nearAlpha = 1.0 - nearBand;

                float farFadeStart = max(nearDistance, farDistance - transitionRange);
                float farFadeEnd = farDistance + transitionRange;
                float farFadeStartSq = farFadeStart * farFadeStart;
                float farFadeEndSq = farFadeEnd * farFadeEnd;
                float farAlpha = nearBand * (1.0 - smoothstep(farFadeStartSq, farFadeEndSq, distanceToCameraSq));

                return passMode < 0.5 ? nearAlpha : farAlpha;
            }

            half ResolveCullFadeCoverage(float3 positionWS, float4 positionCS)
            {
                float farDistance = max(_HectonVegetationRuntimeLodParams.z, _HectonVegetationRuntimeLodParams.y);
                float fadeDistance = max((float)_CullFadeDistance, 0.0);
                if (farDistance <= 1.0 || fadeDistance <= 0.001)
                    return 1.0h;

                float fadeStart = max(0.0, farDistance - fadeDistance);
                float3 cameraDelta = positionWS - _WorldSpaceCameraPos;
                float distanceSq = dot(cameraDelta, cameraDelta);
                half fade = (half)(1.0 - smoothstep(fadeStart * fadeStart, farDistance * farDistance, distanceSq));
                fade = (half)(ceil(saturate(fade) * 4.0) * 0.25);
                return (half)step((half)InterleavedGradientNoise(floor(positionCS.xy)), fade);
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

                return SAMPLE_TEXTURE2D_LOD(_SargassumCutMaskRT, sampler_SargassumCutMaskRT, uv, 0).r;
            }

            half ResolveVegetationCutMask(float instanceType, float3 positionWS)
            {
                if (instanceType > 0.5h && instanceType < 1.5h)
                    return 0.0h;

                return EvaluateGlobalSargassumCutMask(positionWS);
            }

            float2 DecodeShallowWaterVelocity(float2 encodedVelocity)
            {
                return encodedVelocity * 2.0 - 1.0;
            }

            float4 EvaluateShallowWaterFieldData(float3 positionWS)
            {
                if (_HectonShallowWaterFieldActive < 0.5)
                    return float4(0.5, 0.5, 0.0, 0.0);

                float2 uv = float2(
                    (positionWS.x - _HectonShallowWaterFieldWorldRect.x) * _HectonShallowWaterFieldWorldRect.z,
                    (positionWS.z - _HectonShallowWaterFieldWorldRect.y) * _HectonShallowWaterFieldWorldRect.w);
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return float4(0.5, 0.5, 0.0, 0.0);

                return SAMPLE_TEXTURE2D_LOD(_HectonShallowWaterFieldRT, sampler_HectonShallowWaterFieldRT, uv, 0);
            }

            #include "Assets/_Project/Art/Shaders/HectonIndirectVegetationWakeTrail.hlsl"

            float3 ResolveSubmarineWashOffset(float3 evaluationPositionWS, float3 baseNormalWS, float bendMask, float heightMask, float instanceType)
            {
                float washRadius = SanitizeNonNegativeFinite(_HectonSubmarineWashSphere.w);
                float washSpeed = SanitizeNonNegativeFinite(_HectonSubmarineWashVelocity.w);
                if (bendMask <= 0.0001 || washRadius <= 0.0001 || washSpeed <= 0.0001)
                    return float3(0.0, 0.0, 0.0);

                float3 washCenterWS = SanitizeFinite3(_HectonSubmarineWashSphere.xyz, evaluationPositionWS);
                float3 washVelocityWS = SanitizeFinite3(_HectonSubmarineWashVelocity.xyz, float3(0.0, 0.0, 0.0));
                float3 delta = evaluationPositionWS - washCenterWS;
                float radius = SanitizePositiveFinite(washRadius, 0.05);
                float radiusSq = radius * radius;
                float distSq = dot(delta, delta);
                if (distSq >= radiusSq)
                    return float3(0.0, 0.0, 0.0);

                float proximity = 1.0 - smoothstep(0.0, radiusSq, distSq);
                proximity *= proximity;
                float3 awayDirection = SafeNormalize3(float3(delta.x, 0.0, delta.z));
                float3 velocityDirection = washVelocityWS - baseNormalWS * dot(washVelocityWS, baseNormalWS);
                velocityDirection = SafeNormalize3(velocityDirection);
                float3 bendDirection = SafeNormalize3(lerp(awayDirection, velocityDirection, 0.65));
                float speedFactor = saturate(washSpeed * 0.045);
                float shockwave01 = saturate((washSpeed - 15.0) * 0.10);
                float typeScale = instanceType < 0.5 ? 0.55 : (instanceType < 1.5 ? 1.25 : 0.72);
                float flattening = proximity * bendMask * typeScale * (speedFactor * lerp(0.35, 1.0, heightMask) + shockwave01 * lerp(0.55, 1.45, heightMask));
                float downwardBias = lerp(0.02, 0.12 + shockwave01 * 0.26, heightMask) * flattening;
                return bendDirection * flattening + float3(0.0, -downwardBias, 0.0);
            }

            float ResolveAbyssalFlowSnapMask(float3 rootPositionWS, float3 evaluationPositionWS, float bendMask, float heightMask, float instanceType)
            {
                float washRadius = SanitizeNonNegativeFinite(_HectonSubmarineWashSphere.w);
                float washSpeed = SanitizeNonNegativeFinite(_HectonSubmarineWashVelocity.w);
                if (instanceType < 0.5 || instanceType > 1.5 || bendMask <= 0.0001 || washRadius <= 0.0001)
                    return 0.0;

                float speedGate = smoothstep(10.0, 12.5, washSpeed);
                if (speedGate <= 0.0001)
                    return 0.0;

                float radius = SanitizePositiveFinite(washRadius, 0.05);
                float3 washCenterWS = SanitizeFinite3(_HectonSubmarineWashSphere.xyz, rootPositionWS);
                float3 washVelocityWS = SanitizeFinite3(_HectonSubmarineWashVelocity.xyz, float3(0.0, 0.0, 0.0));
                float3 rootDelta = rootPositionWS - washCenterWS;
                rootDelta.y *= 0.25;
                float radiusSq = radius * radius;
                float rootDeltaSq = dot(rootDelta, rootDelta);
                if (rootDeltaSq >= radiusSq)
                    return 0.0;

                float innerRadius = radius * 0.35;
                float proximity = 1.0 - smoothstep(innerRadius * innerRadius, radiusSq, rootDeltaSq);
                if (proximity <= 0.0001)
                    return 0.0;

                float2 velocityDirection = ResolvePlanarOceanFlowDirection(washVelocityWS.xz);
                float2 radialDirection = SafeNormalize2(rootDelta.xz + float2(0.001, -0.001));
                float directionalGate = saturate(dot(velocityDirection, radialDirection) * 0.5 + 0.5);
                float3 abyssalFlow = ResolveAbyssalFlowField(evaluationPositionWS);
                float flowSq = dot(abyssalFlow.xz, abyssalFlow.xz);
                float flowGate = smoothstep(0.000225, 0.0324, flowSq);
                return speedGate * proximity * lerp(0.55, 1.0, flowGate) * lerp(0.70, 1.0, directionalGate) * saturate(heightMask + 0.35);
            }

            float EvaluateSargassumOrganicDensity(float2 worldXZ)
            {
                float2 sample = worldXZ * 0.024 + _SargassumGlobalDriftOffset.xz * 0.014;
                float coarse = Hash21(floor(sample));
                float fine = Hash21(floor(sample * 1.87 + 21.0));
                float wave = FastSinApprox(sample.x * 1.18 + sample.y * 0.86 + _Time.y * 0.12) * 0.5 + 0.5;
                return saturate(coarse * 0.44 + fine * 0.34 + wave * 0.22);
            }

            half ResolveSargassumPorousCoverage(float3 positionWS, float heightMask)
            {
                float organicDensity = EvaluateSargassumOrganicDensity(positionWS.xz + float2(heightMask * 1.1, -heightMask * 0.9));
                float laceNoise = Hash21(floor(positionWS.xz * 1.65 + heightMask * 19.0));
                float interiorBias = lerp(0.58, 0.8, saturate(heightMask));
                return saturate(organicDensity * 1.15 + laceNoise * 0.18 - interiorBias);
            }

            #include "Assets/_Project/Art/Shaders/HectonIndirectVegetationInteraction.hlsl"

            #include "Assets/_Project/Art/Shaders/HectonIndirectVegetationPlayerBend.hlsl"

            float3 ResolveImpactOffset(float3 evaluationPositionWS, float3 baseNormalWS, float bendMask)
            {
                if (bendMask <= 0.0001 || _HectonImpactSphereCount <= 0)
                    return float3(0.0, 0.0, 0.0);

                float3 impactOffset = float3(0.0, 0.0, 0.0);
                int impactCount = min(_HectonImpactSphereCount, HECTON_MAX_IMPACT_SPHERES);

                [loop]
                for (int i = 0; i < impactCount; i++)
                {
                    float4 impactSphere = _HectonImpactSpheres[i];
                    if (!all(isfinite(impactSphere.xyz)))
                        continue;

                    float radius = SanitizePositiveFinite(impactSphere.w, 0.05);
                    float radiusSq = radius * radius;
                    float3 delta = evaluationPositionWS - impactSphere.xyz;
                    float distSq = dot(delta, delta);
                    float proximity = 1.0 - smoothstep(0.0, radiusSq, distSq);
                    if (proximity <= 0.0001)
                        continue;

                    float3 planarDirection = SafeNormalize3(delta - baseNormalWS * dot(delta, baseNormalWS));
                    impactOffset += (planarDirection + float3(0.0, -0.18, 0.0)) * (proximity * proximity);
                }

                return impactOffset * bendMask;
            }

            float3 CalculateUnderwaterCurrents(
                float3 originWS,
                float3 basePositionWS,
                float bendMask,
                float heightMask,
                float instanceType,
                float instanceNoise,
                float timeValue,
                float2 currentVector,
                float currentStrength,
                out float torsion)
            {
                float currentTimeScale = max(_HectonVegetationCurrentTimeScale, 0.05);
                float currentNoiseScale = max(_HectonVegetationCurrentNoiseScale, 0.002);
                float3 abyssalFlow = ResolveAbyssalFlowField(basePositionWS);
                float3 sampledFlow = ResolveMarineSnowFlowField(basePositionWS) + abyssalFlow;
                float2 localFlowVector = sampledFlow.xz;
                float abyssalFlowSq = dot(abyssalFlow.xz, abyssalFlow.xz);
                bool hasLocalFlow = dot(localFlowVector, localFlowVector) > 0.0001;
                float2 resolvedCurrentVector = hasLocalFlow ? localFlowVector : currentVector;
                float currentMagnitudeSq = dot(resolvedCurrentVector, resolvedCurrentVector);
                float currentMagnitude = max(currentStrength, saturate(currentMagnitudeSq));
                float2 currentDirection = ResolvePlanarOceanFlowDirection(resolvedCurrentVector);
                if (currentMagnitude <= 0.0001 || dot(currentDirection, currentDirection) <= 0.0001)
                {
                    torsion = 0.0;
                    return float3(0.0, 0.0, 0.0);
                }
                float2 currentPerpendicular = float2(-currentDirection.y, currentDirection.x);

                float3 samplePosition = float3(
                    basePositionWS.x * currentNoiseScale,
                    basePositionWS.y * (currentNoiseScale * 0.58),
                    basePositionWS.z * currentNoiseScale);
                samplePosition += float3(
                    timeValue * currentTimeScale * 0.11,
                    timeValue * currentTimeScale * 0.037,
                    -timeValue * currentTimeScale * 0.083);

                float gustNoise = SampleCurrentNoise3D(samplePosition + instanceNoise * 4.13);
                float eddyNoise = SampleCurrentNoise3D(samplePosition.zxy * 1.57 + float3(11.0, 19.7, 5.0));
                float phaseA = dot(originWS.xz, float2(0.018, 0.012)) + timeValue * (0.38 + currentTimeScale * 0.42) + instanceNoise * 6.28318;
                float phaseB = dot(originWS.xz, float2(-0.011, 0.016)) - timeValue * (0.27 + currentTimeScale * 0.33) + basePositionWS.y * 0.045;
                float phaseC = dot(originWS.xz, float2(0.007, -0.009)) + timeValue * 0.21 + heightMask * 1.7;

                float surge = FastSinApprox(phaseA) * 0.55 + FastCosApprox(phaseB) * 0.35 + FastSinApprox(phaseC) * 0.25;
                float curl = FastCosApprox(phaseA * 0.73 - phaseB * 1.12) * 0.45 + eddyNoise * 0.65;
                float2 flowXZ = currentDirection * (0.55 + surge * 0.45 + gustNoise * 0.55) +
                    currentPerpendicular * (curl * 0.42);
                if (hasLocalFlow)
                    flowXZ += localFlowVector * 0.65;
                if (abyssalFlowSq > 0.00000001)
                {
                    float abyssalPhase = timeValue * max(_KelpCurrentFrequency, 0.01) +
                        dot(basePositionWS.xz, float2(0.071, -0.053)) +
                        instanceNoise * 6.28318;
                    flowXZ += abyssalFlow.xz * FastSinApprox(abyssalPhase);
                }

                float verticalFlow = (gustNoise * 0.35 + surge * 0.18) * _HectonVegetationCurrentVerticalFactor;
                float3 flowVector = float3(flowXZ.x, verticalFlow, flowXZ.y);
                float typeAmplitude = instanceType < 0.5
                    ? _GrassWindAmplitude * 0.38
                    : (instanceType < 1.5 ? _KelpCurrentAmplitude * 0.72 : _SargassumWaveAmplitude * 0.28);
                float tipWeight = lerp(0.18, 1.0, bendMask);
                float strengthScale = (0.45 + currentMagnitude * 0.55) * typeAmplitude * tipWeight;

                torsion = (curl * 0.6 + surge * 0.4) * strengthScale;
                return flowVector * strengthScale;
            }

            #include "Assets/_Project/Art/Shaders/HectonIndirectVegetationBillboard.hlsl"

            float ResolveOrganicEntropyProgress(float encodedHeightScale, float encodedWidthScale, float timeValue)
            {
                if (!isfinite(encodedHeightScale))
                    return 0.0;

                if (encodedHeightScale >= 0.0)
                    return 0.0;

                float safeWidthScale = isfinite(encodedWidthScale) ? encodedWidthScale : 0.0;
                float entropyDuration = safeWidthScale < 0.0 ? 600.0 : 0.85;
                float entropyStartTime = safeWidthScale < 0.0 ? abs(safeWidthScale) : max(0.0, safeWidthScale);
                return saturate((timeValue - entropyStartTime) / entropyDuration);
            }

            float ResolveOrganicWidthScale(float encodedWidthScale, float entropyProgress)
            {
                if (encodedWidthScale < 0.0)
                    return lerp(1.0, 0.12, entropyProgress);

                return max(0.2, encodedWidthScale);
            }

            float ResolveParasiteMask(float runtimeFlags)
            {
                float variationFlags = floor(SanitizeNonNegativeFinite(runtimeFlags));
                return step(0.5, fmod(variationFlags, 2.0));
            }

            float ResolveBiomeLayer(float runtimeFlags)
            {
                float variationFlags = floor(SanitizeNonNegativeFinite(runtimeFlags));
                return fmod(floor(variationFlags / 16.0), 4.0);
            }

            void ResolveRuntimeStateWeights(float runtimeState, out float agitatedWeight, out float dyingWeight)
            {
                float safeRuntimeState = isfinite(runtimeState) ? runtimeState : 0.0;
                agitatedWeight = saturate(1.0 - abs(safeRuntimeState - 1.0));
                dyingWeight = saturate(1.0 - abs(safeRuntimeState - 2.0));
            }

            float ResolveMetadataGrowth01(float encodedGrowth01)
            {
                if (!isfinite(encodedGrowth01))
                    return 1.0;

                if (encodedGrowth01 < 0.0)
                    return -1.0;

                return encodedGrowth01 > 0.0001 ? saturate(SanitizeNonNegativeFinite(encodedGrowth01)) : 1.0;
            }

            float ResolveGrowth01(uint sourceInstanceIndex, float encodedGrowth01)
            {
                float soaAge01 = _HectonFloraAges01[sourceInstanceIndex];
                if (!isfinite(soaAge01))
                    return ResolveMetadataGrowth01(encodedGrowth01);

                if (soaAge01 < 0.0)
                    return -1.0;

                if (soaAge01 > 0.0001 || encodedGrowth01 <= 0.0001)
                    return saturate(SanitizeNonNegativeFinite(soaAge01));

                return ResolveMetadataGrowth01(encodedGrowth01);
            }

            float4 ResolveScatterVisualPayload(uint sourceInstanceIndex)
            {
                float payloadWeight = smoothstep(0.55, 0.9, ResolveIndirectVegetationQualityWeight());
                if (_HectonFloraScatterVisualPayloadEnabled > 0.5 && payloadWeight > 0.0001)
                {
                    float4 payload = _HectonFloraScatterVisualPayload[sourceInstanceIndex];
                    return all(isfinite(payload)) ? saturate(payload) * payloadWeight : float4(0.0, 0.0, 0.0, 0.0);
                }

                return float4(0.0, 0.0, 0.0, 0.0);
            }

            half3 ResolveBiolumGroupTint(int stateIndex)
            {
                half3 tint0 = half3(0.18h, 0.88h, 1.00h);
                half3 tint1 = half3(0.32h, 1.00h, 0.62h);
                half3 tint2 = half3(0.74h, 0.38h, 1.00h);
                half3 tint3 = half3(1.00h, 0.72h, 0.32h);
                half idx = (half)stateIndex;
                half3 lowPair = lerp(tint0, tint1, step(0.5h, idx));
                half3 highPair = lerp(tint2, tint3, step(2.5h, idx));
                return lerp(lowPair, highPair, step(1.5h, idx));
            }

            half ResolveIndirectVegetationGlobalBiolumVertexPulse(float3 localAupCoord, half biolumSyncGroup)
            {
                if (!all(isfinite(localAupCoord)))
                    return 0.0h;

                float4 safeParams = all(isfinite(_GlobalBiolumParams)) ? _GlobalBiolumParams : float4(0.0, 0.0, 0.0, 0.0);
                int activeCount = min(max((int)floor(SanitizeNonNegativeFinite(safeParams.x)), 0), 4);
                if (activeCount <= 0)
                    return 0.0h;

                float groupRaw = floor(SanitizeNonNegativeFinite((float)biolumSyncGroup));
                int stateIndex = min((int)fmod(groupRaw, (float)activeCount), activeCount - 1);
                float4 stateRaw = _GlobalBiolumDearLieGroups[stateIndex];
                float4 state = all(isfinite(stateRaw)) ? stateRaw : float4(0.0, 0.0, 0.0, 0.0);
                float phase = SanitizeNonNegativeFinite(state.x);
                float frequency = max(SanitizeNonNegativeFinite(state.y), 0.0025);
                float amplitude = saturate(SanitizeNonNegativeFinite(state.z));
                float offset = SanitizeNonNegativeFinite(state.w);
                float spatialPhase = dot(localAupCoord, float3(0.021, 0.013, 0.059)) * frequency + offset + groupRaw * 0.77;
                half wave = 0.5h + 0.5h * (half)FastSinApprox(phase + spatialPhase);
                return saturate(wave * (half)amplitude);
            }

            half4 ResolveIndirectVegetationGlobalBiolum(float3 localAupCoord, half biolumSyncGroup, half vertexPulse)
            {
                if (!all(isfinite(localAupCoord)))
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);

                float4 safeParams = all(isfinite(_GlobalBiolumParams)) ? _GlobalBiolumParams : float4(0.0, 0.0, 0.0, 0.0);
                half quality01 = saturate((half)SanitizeNonNegativeFinite(safeParams.y));
                int activeCount = min(max((int)floor(SanitizeNonNegativeFinite(safeParams.x)), 0), 4);
                if (activeCount <= 0)
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);

                float groupRaw = floor(SanitizeNonNegativeFinite((float)biolumSyncGroup));
                int stateIndex = min((int)fmod(groupRaw, (float)activeCount), activeCount - 1);
                float4 stateRaw = _GlobalBiolumDearLieGroups[stateIndex];
                float4 state = all(isfinite(stateRaw)) ? stateRaw : float4(0.0, 0.0, 0.0, 0.0);
                half strobe = saturate((half)SanitizeNonNegativeFinite(safeParams.z));
                int secondaryIndex = stateIndex + 1;
                if (secondaryIndex >= activeCount)
                    secondaryIndex = 0;

                float4 secondaryStateRaw = _GlobalBiolumDearLieGroups[secondaryIndex];
                float4 secondaryState = all(isfinite(secondaryStateRaw)) ? secondaryStateRaw : float4(0.0, 0.0, 0.0, 0.0);
                float phase = SanitizeNonNegativeFinite(state.x);
                float frequency = max(SanitizeNonNegativeFinite(state.y), 0.0025);
                float amplitude = clamp(SanitizeNonNegativeFinite(state.z), 0.0, 10.0);
                float offset = SanitizeNonNegativeFinite(state.w);
                float spatialPhase = dot(localAupCoord, float3(0.021, 0.013, 0.059)) * frequency + offset + groupRaw * 0.77;
                half cheapWave = saturate(vertexPulse);
                half pixelWave = 0.5h + 0.5h * (half)FastSinApprox(phase + spatialPhase);
                float secondaryOffset = SanitizeNonNegativeFinite(secondaryState.w);
                float secondaryFrequency = max(SanitizeNonNegativeFinite(secondaryState.y), 0.0025);
                half interference = 0.5h + 0.5h * (half)FastSinApprox(
                    SanitizeNonNegativeFinite(secondaryState.x) +
                    dot(localAupCoord.xzy, float3(0.041, 0.029, 0.067)) * secondaryFrequency +
                    secondaryOffset);
                half filament = 0.5h + 0.5h * (half)FastSinApprox(
                    phase + dot(localAupCoord, float3(0.173, 0.097, 0.131)) * frequency + offset);
                half overkillWave = saturate(pixelWave * 0.72h + interference * 0.21h + filament * 0.07h);
                half qualityCurve = quality01 * quality01 * (3.0h - 2.0h * quality01);
                half resolvedWave = lerp(pixelWave, overkillWave, qualityCurve);
                half intensity = clamp(lerp(cheapWave, resolvedWave * (half)amplitude, quality01) + strobe * 10.0h, 0.0h, 10.0h);
                half3 baseTint = ResolveBiolumGroupTint(stateIndex);
                half3 secondaryTint = ResolveBiolumGroupTint(secondaryIndex);
                half3 color = lerp(baseTint, secondaryTint, qualityCurve * interference * 0.32h);
                color = lerp(color, half3(1.0h, 1.0h, 1.0h), strobe);
                return half4(color, intensity);
            }

            half ResolveBiolumPredatorDim(float3 positionWS)
            {
                half threatExposure = (half)saturate(SanitizeNonNegativeFinite(_HectonFloraPredatorThreatParams.x));
                half dimStrength = (half)saturate(SanitizeNonNegativeFinite(_HectonFloraPredatorThreatParams.z));
                half legacyDim = 1.0h;
                if (threatExposure > 0.0001h && dimStrength > 0.0001h)
                {
                    float4 predatorThreat = _HectonFloraPredatorThreatPositionRadius;
                    float threatRadius = SanitizeNonNegativeFinite(predatorThreat.w);
                    float paramRadius = SanitizePositiveFinite(_HectonFloraPredatorThreatParams.y, 15.0);
                    float dimRadius = max(max(threatRadius, paramRadius), 15.0);
                    half predatorProximity = 1.0h;
                    if (threatRadius > 0.001 && all(isfinite(predatorThreat.xyz)))
                    {
                        float3 predatorDelta = positionWS - predatorThreat.xyz;
                        float dimRadiusSq = dimRadius * dimRadius;
                        predatorProximity = (half)(1.0 - LinearStep01(0.0, dimRadiusSq, dot(predatorDelta, predatorDelta)));
                    }
                    legacyDim = saturate(1.0h - (threatExposure * predatorProximity * dimStrength));
                }

                int predatorCount = min(max(_PredatorAUPCount, 0), 32);
                if (predatorCount <= 0)
                    return legacyDim;

                half bufferDimStrength = (half)saturate(SanitizeNonNegativeFinite(_PredatorAUPParams.y));
                if (bufferDimStrength <= 0.0001h)
                    return legacyDim;

                float baseRadius = max(SanitizePositiveFinite(_PredatorAUPParams.x, 15.0), 15.0);
                half predatorGate = 0.0h;
                [loop]
                for (int predatorIndex = 0; predatorIndex < predatorCount; predatorIndex++)
                {
                    float4 predatorAup = _PredatorAUPBuffer[predatorIndex];
                    if (!all(isfinite(predatorAup.xyz)))
                        continue;

                    float dimRadius = max(SanitizePositiveFinite(predatorAup.w, baseRadius), 15.0);
                    float3 predatorDelta = positionWS - predatorAup.xyz;
                    float dimRadiusSq = dimRadius * dimRadius;
                    float gate = 1.0 - LinearStep01(dimRadiusSq * 0.3025, dimRadiusSq, dot(predatorDelta, predatorDelta));
                    predatorGate = max(predatorGate, (half)gate);
                }

                half bufferDim = saturate(1.0h - predatorGate * bufferDimStrength);
                return min(legacyDim, bufferDim);
            }

            half ResolveBiolumFlashBangBoost(float3 positionWS)
            {
                float flashStartTime = isfinite(_BiolumFlashBangParams.x) ? _BiolumFlashBangParams.x : _Time.y;
                half duration = (half)SanitizePositiveFinite(_BiolumFlashBangParams.y, 0.001);
                half age = (half)(_Time.y - flashStartTime);
                if (age < 0.0h || age > duration)
                    return 1.0h;

                float radius = SanitizePositiveFinite(_BiolumFlashBangAUP.w, 0.1);
                if (!all(isfinite(_BiolumFlashBangAUP.xyz)))
                    return 1.0h;

                float3 flashDelta = positionWS - _BiolumFlashBangAUP.xyz;
                float radiusSq = radius * radius;
                half distanceGate = (half)(1.0 - LinearStep01(radiusSq * 0.4225, radiusSq, dot(flashDelta, flashDelta)));
                half timeGate = (half)(1.0 - LinearStep01(duration * 0.45h, duration, age));
                half flashStrength = (half)SanitizeNonNegativeFinite(_BiolumFlashBangParams.z);
                return 1.0h + distanceGate * timeGate * flashStrength;
            }

            half ResolveSeasonalBloomEmissionScale()
            {
                half bloomWeight = (half)saturate(SanitizeNonNegativeFinite(_HectonFloraLifecycleParams.x));
                half bloomScale = (half)max(1.0, SanitizeNonNegativeFinite(_HectonFloraLifecycleParams.z));
                return lerp(1.0h, bloomScale, bloomWeight);
            }

            half ResolveCascadeEmissionScale(half cascadeSeed)
            {
                if (!isfinite((float)cascadeSeed))
                    return 0.0h;

                if (cascadeSeed <= -99999.0h)
                    return 0.0h;

                half cascadeTime = (half)(isfinite(_HectonFloraCascadeParams.x) ? _HectonFloraCascadeParams.x : 0.0);
                half pulseDuration = (half)SanitizePositiveFinite(_HectonFloraCascadeParams.y, 0.05);
                half emissionBoost = (half)SanitizeNonNegativeFinite(_HectonFloraCascadeParams.z);
                half releaseDuration = (half)max((float)pulseDuration, SanitizePositiveFinite(_HectonFloraCascadeParams.w, pulseDuration));
                half age = cascadeTime - cascadeSeed;
                if (age < 0.0h || age > releaseDuration)
                    return 0.0h;

                half rise = (half)LinearStep01(0.0h, pulseDuration * 0.18h, age);
                half crest = (half)(1.0 - LinearStep01(pulseDuration * 0.52h, pulseDuration, age));
                half tail = (half)(1.0 - LinearStep01(pulseDuration, releaseDuration, age));
                return rise * max(crest, tail * 0.55h) * emissionBoost;
            }

            half ResolveEdgeInwardNecrosisMask(Varyings input)
            {
                half damage01 = saturate(1.0h - input.health01);
                half edgeSignal = saturate(max(input.edgeMask, input.curvatureMask * 0.82h));
                edgeSignal = saturate(edgeSignal + smoothstep(0.72h, 1.0h, input.heightMask) * 0.14h);

                half noise = (half)ValueNoise3D(
                    input.positionWS * 2.75 +
                    float3(input.cascadeSeed * 13.17h, _Time.y * 0.035, input.pulseFrequency * 5.0h));
                half inwardThreshold = saturate(1.08h - damage01 * 1.26h + (noise - 0.5h) * 0.32h);
                half hardCreep = step(inwardThreshold, edgeSignal);
                half featheredCreep = smoothstep(
                    inwardThreshold - 0.10h,
                    inwardThreshold + 0.06h,
                    saturate(edgeSignal + noise * 0.08h));
                return saturate(max(hardCreep, featheredCreep) * smoothstep(0.04h, 0.30h, damage01));
            }

            half ResolveNecrosisEdgeSignal(Varyings input)
            {
                half edgeSignal = saturate(max(input.edgeMask, input.curvatureMask * 0.82h));
                return saturate(edgeSignal + smoothstep(0.72h, 1.0h, input.heightMask) * 0.14h);
            }

            float ResolveCameraDistanceSq(float3 positionWS)
            {
                float3 cameraDelta = positionWS - _WorldSpaceCameraPos;
                return dot(cameraDelta, cameraDelta);
            }

            half ResolveDistanceBiolumDimming(float cameraDistanceSq)
            {
                return lerp(1.0h, 0.12h, (half)LinearStep01(2500.0, 12100.0, cameraDistanceSq));
            }

            half ResolveDistanceBiolumPixelGate(float cameraDistanceSq, float4 positionCS)
            {
                half pixelCoverage = lerp(1.0h, 0.22h, (half)LinearStep01(2500.0, 8100.0, cameraDistanceSq));
                return step((half)InterleavedGradientNoise(positionCS.xy), pixelCoverage);
            }

            half3 ResolveBiolumSporeEmission(Varyings input, half3 emissionColor, half sourceEnergy)
            {
                half energy = saturate(sourceEnergy * saturate(input.growth01) * saturate(input.health01));
                if (energy <= 0.0001h)
                    return half3(0.0h, 0.0h, 0.0h);

                float3 sporeCell = floor(input.positionWS * 2.25 + float3(0.0, _Time.y * 0.85, 0.0));
                half sporeSeed = (half)Hash31(sporeCell + input.biolumPulseData.x);
                half screenSeed = (half)InterleavedGradientNoise(input.positionCS.xy + sporeSeed * 43.0h);
                half sparkleGate = step(0.965h, sporeSeed) * step(0.58h, screenSeed);
                half pulse = 0.45h + 0.55h * (0.5h + 0.5h * (half)FastSinApprox(_Time.y * 4.7h + sporeSeed * 6.28318h));
                half edgeLaunchMask = saturate(input.edgeMask * 1.35h + input.heightMask * 0.35h);
                return emissionColor * (sparkleGate * pulse * edgeLaunchMask * energy * 0.42h);
            }

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                uint sourceInstanceIndex = input.instanceID;
#if UNITY_ANY_INSTANCING_ENABLED
                sourceInstanceIndex = unity_InstanceID;
#endif
                if (_HectonVegetationRuntimeDrawParams.w > 0.5)
                {
                    InitIndirectDrawArgs(0);
                    sourceInstanceIndex = _HectonVisibleInstanceIndices[GetIndirectInstanceID(sourceInstanceIndex)];
                }
                float4x4 instanceMatrix = _HectonInstanceMatrices[sourceInstanceIndex];
                HectonVegetationInstanceData instanceData = _HectonVegetationInstanceData[sourceInstanceIndex];
                float3 floatingOriginOffsetWS = _GlobalFloatingOffset.xyz;
                float3 stableAupSeed = TransformPoint(instanceMatrix, float3(0.0, 0.0, 0.0));
                float3 originWS = stableAupSeed + floatingOriginOffsetWS;
                float3 cameraDelta = originWS - _WorldSpaceCameraPos;
                float cameraDistanceSq = dot(cameraDelta, cameraDelta);
                float lodAlpha = ResolveLodAlpha(cameraDistanceSq, _HectonVegetationRuntimeLodParams.x);
                float2 packedPresentation = HectonDecodePackedPresentation(instanceData.BioluminescenceColor.a);

                float instanceType = clamp(round(instanceData.Type), 0.0, 2.0);
                float encodedHeightScale = instanceData.HeightScale;
                float encodedWidthScale = instanceData.WidthScale;
                float authoredSwaySpeed = max(instanceData.SwaySpeed, 0.05);
                float authoredBendAmplitude = max(instanceData.BendAmplitude, 0.0);
                float normalizedHealth = saturate(min(instanceData.HealthNormalized, 1.0 - packedPresentation.y));
                float growth01 = ResolveGrowth01(sourceInstanceIndex, instanceData.Reserved0);
                float4 scatterVisualPayload = ResolveScatterVisualPayload(sourceInstanceIndex);
                float visibleGrowth01 = saturate(growth01);
                float timeValue = _Time.y * authoredSwaySpeed;
                float entropyProgress = ResolveOrganicEntropyProgress(encodedHeightScale, encodedWidthScale, timeValue);
                float parasiteMask = ResolveParasiteMask(instanceData.RuntimeFlags);
                float biomeLayer = ResolveBiomeLayer(instanceData.RuntimeFlags);
                float geneticTraits = DecodeGeneticTraits(instanceData.RuntimeFlags);
                float wiltSuppression = lerp(1.0, 0.18, entropyProgress);
                float heightScale = saturate(abs(encodedHeightScale));
                float widthScale = ResolveOrganicWidthScale(encodedWidthScale, entropyProgress);
                float heightMask = saturate(input.uv.y);
                float vertexSwayMask = saturate(input.color.r);
                float vertexBiolumMask = saturate(input.color.g);
                float vertexAoVisibility = saturate(input.color.b);
                float vertexWearMask = saturate(input.color.a);
                float stiffnessMask = vertexSwayMask;
                float bendMask = heightMask * heightMask * authoredBendAmplitude * stiffnessMask;
                float curvatureMask = vertexWearMask;
                float4 aupGeneticHash = ResolveAupGeneticHash(stableAupSeed);
                float2 originXZ = stableAupSeed.xz;
                float instanceNoise = aupGeneticHash.w;
                float agitatedWeight;
                float dyingWeight;
                ResolveRuntimeStateWeights(instanceData.RuntimeState, agitatedWeight, dyingWeight);
                float resolvedWaterLevel = ResolveWaterLevel();

                float instanceHeight;
                float instanceWidth;
                float aupHeightMultiplier;
                float aupWidthMultiplier;
                float2 aupLeanDirection;
                float aupLeanMeters;
                ResolveAupGeneticShape(
                    aupGeneticHash,
                    instanceType,
                    aupHeightMultiplier,
                    aupWidthMultiplier,
                    aupLeanDirection,
                    aupLeanMeters);
                ResolveInstanceShape(instanceType, heightScale, widthScale, instanceHeight, instanceWidth);
                instanceHeight *= aupHeightMultiplier;
                instanceWidth *= aupWidthMultiplier;

                float3 localPosition = input.positionOS.xyz;
                float3 baseNormalWS = TransformDirection(instanceMatrix, input.normalOS);
                float3 driftOffsetWS = instanceType > 1.5 ? _SargassumGlobalDriftOffset.xyz : float3(0.0, 0.0, 0.0);
                float3 renderOriginWS = originWS + driftOffsetWS;
                float growthHeightScale = visibleGrowth01;
                float growthWidthScale = sqrt(max(visibleGrowth01, 0.0));

                if (instanceType < 0.5)
                {
                    localPosition.y = heightMask * instanceHeight;
                    localPosition.x *= instanceWidth * lerp(1.0, 0.42, heightMask);
                }
                else if (instanceType < 1.5)
                {
                    localPosition.y = heightMask * instanceHeight;
                    localPosition.x *= instanceWidth * lerp(1.0, 0.18, heightMask);
                    localPosition.z += FastSinApprox(heightMask * PI) * instanceHeight * 0.024;
                }
                else
                {
                    localPosition.y = heightMask * instanceHeight;
                    localPosition.x *= instanceWidth * lerp(1.0, 0.30, heightMask);
                }

                localPosition.y *= growthHeightScale;
                localPosition.xz *= growthWidthScale;
                localPosition.xz += aupLeanDirection * (aupLeanMeters * heightMask * heightMask * growthHeightScale);

                float3 basePositionWS = TransformPoint(instanceMatrix, localPosition) + driftOffsetWS + floatingOriginOffsetWS;
                float2 fallbackCurrentVector = dot(_GlobalOceanFlow.xz, _GlobalOceanFlow.xz) > 0.0001 ? _GlobalOceanFlow.xz : _HectonVegetationCurrentVector.xz;
                float3 sampledFlowVector = ResolveMarineSnowFlowField(basePositionWS) + ResolveAbyssalFlowField(basePositionWS);
                float2 sampledCurrentVector = sampledFlowVector.xz;
                float sampledCurrentSq = dot(sampledCurrentVector, sampledCurrentVector);
                float flowMagnitude = saturate(sampledCurrentSq);
                float2 currentVector = sampledCurrentSq > 0.0001 ? sampledCurrentVector : fallbackCurrentVector;
                float currentStrength = max(
                    ResolvePlanarOceanFlowStrength(_HectonVegetationCurrentVector.xz, _HectonVegetationCurrentStrength),
                    flowMagnitude);
                float2 currentDirection = ResolvePlanarOceanFlowDirection(currentVector);
                float3 animatedPositionWS = basePositionWS;
                float3 wakeTrailOffset = ResolveWakeTrailOffset(basePositionWS, baseNormalWS, bendMask, heightMask, instanceType);
                float3 submarineWashOffset = ResolveSubmarineWashOffset(basePositionWS, baseNormalWS, bendMask, heightMask, instanceType);
                float floraSwayFieldWeight;
                float3 floraSwayFieldOffset = ResolveFloraSwayFieldOffset(basePositionWS, bendMask, heightMask, instanceType, floraSwayFieldWeight);
                float3 ambientFloraSwayOffset = ResolveGlobalAmbientFloraSwayOffset(basePositionWS, stiffnessMask, heightMask, instanceType);
                submarineWashOffset *= (1.0 - saturate(_HectonFloraSwayFieldParams.y));
                float proceduralWakeShear;
                float3 proceduralWakeOffset = ResolveProceduralWakeOffset(basePositionWS, bendMask, heightMask, instanceType, proceduralWakeShear);
                proceduralWakeShear = max(proceduralWakeShear, floraSwayFieldWeight);
                float3 flowSynchronyOffset = ResolveFlowSynchronyOffset(basePositionWS, bendMask, instanceType, instanceNoise);

                if (_HectonVegetationRuntimeLodParams.x < 0.5)
                {
                    float stateSwayScale = lerp(1.0, 1.28, agitatedWeight) * lerp(1.0, 0.52, dyingWeight);
                    float detailAmplitude = saturate(lodAlpha + 0.2) * wiltSuppression * stateSwayScale * lerp(0.35, 1.0, normalizedHealth);
                    float currentTorsion;
                    float3 currentOffset = CalculateUnderwaterCurrents(
                        renderOriginWS,
                        basePositionWS,
                        bendMask,
                        heightMask,
                        instanceType,
                        instanceNoise,
                        timeValue,
                        currentVector,
                        currentStrength,
                        currentTorsion);

                    if (instanceType < 0.5)
                    {
                        float phase = timeValue * _GrassWindSpeed + instanceNoise * 6.28318 +
                            originXZ.x * (_GrassWindFrequency * 0.35) +
                            originXZ.y * (_GrassWindFrequency * 0.28);
                        float2 flowDrivenGrass = sampledCurrentSq > 0.0001 ? sampledCurrentVector : currentDirection;
                        float2 grassWind = SafeNormalize2(flowDrivenGrass + float2(
                            FastSinApprox(phase),
                            FastCosApprox(phase * 1.37 + heightMask * _GrassWindFrequency)) * 0.18);
                        animatedPositionWS += wakeTrailOffset;
                        animatedPositionWS += submarineWashOffset;
                        animatedPositionWS += ambientFloraSwayOffset;
                        animatedPositionWS += floraSwayFieldOffset;
                        animatedPositionWS += proceduralWakeOffset;
                        animatedPositionWS += flowSynchronyOffset;
                        animatedPositionWS += currentOffset * (0.85 * detailAmplitude);
                        animatedPositionWS.xz += grassWind * (_GrassWindAmplitude * bendMask * detailAmplitude);
                        animatedPositionWS.y += FastSinApprox(phase * 1.9 + aupGeneticHash.z * 6.28318) * (0.05 * bendMask * detailAmplitude);
                    }
                    else if (instanceType < 1.5)
                    {
                        float phase = timeValue * (_KelpCurrentSpeed + _HectonVegetationCurrentTimeScale) +
                            (originXZ.x + originXZ.y) * max(_HectonVegetationCurrentNoiseScale, 0.001) +
                            instanceNoise * 7.0;
                        float2 abyssalKelpFlow = sampledCurrentSq > 0.0001 ? sampledCurrentVector : currentVector;
                        float2 noiseFlow = abyssalKelpFlow + float2(
                            FastSinApprox(phase),
                            FastCosApprox(phase * 0.71 + heightMask * _KelpCurrentFrequency)) * (currentStrength * 0.18);
                        float2 kelpFlow = ResolvePlanarOceanFlowDirection(currentVector + noiseFlow * currentStrength);
                        float kelpAmplitude = _KelpCurrentAmplitude * lerp(0.45, 1.0, authoredBendAmplitude);
                        kelpAmplitude *= lerp(0.8, 1.0, smoothstep(0.1, 0.65, ResolveIndirectVegetationQualityWeight()));
                        animatedPositionWS += wakeTrailOffset * 1.1;
                        animatedPositionWS += submarineWashOffset * 1.15;
                        animatedPositionWS += ambientFloraSwayOffset * 1.08;
                        animatedPositionWS += floraSwayFieldOffset * 1.12;
                        animatedPositionWS += proceduralWakeOffset * 1.18;
                        animatedPositionWS += flowSynchronyOffset;
                        animatedPositionWS += currentOffset * (1.15 * detailAmplitude);
                        animatedPositionWS.xz += kelpFlow * (kelpAmplitude * bendMask * detailAmplitude);
                        animatedPositionWS.xz += float2(currentTorsion, -currentTorsion) * (bendMask * 0.42 * detailAmplitude);
                        animatedPositionWS.y += FastSinApprox(phase * 0.55 + heightMask * 2.2) *
                            (_KelpCurrentAmplitude * 0.12 * _HectonVegetationCurrentVerticalFactor * bendMask * detailAmplitude);
                    }
                    else
                    {
                        float phase = timeValue * _SargassumWaveSpeed + instanceNoise * 8.0 +
                            dot(originXZ, float2(_SargassumWaveFrequency * 0.2, _SargassumWaveFrequency * 0.16));
                        float organicDensity = EvaluateSargassumOrganicDensity(renderOriginWS.xz);
                        float edgePulse = saturate(1.0 - abs(organicDensity * 2.0 - 1.0));
                        float oceanLift = EvaluateOceanSurfaceLift(renderOriginWS.xz);
                        float oceanLiftBlend = saturate(_HectonOceanSurfaceWaveMeta.x);
                        float localLiftScale = lerp(1.0, 0.28, oceanLiftBlend);
                        float waveLift = oceanLift + FastSinApprox(phase) * (_SargassumWaveAmplitude * localLiftScale * lerp(0.35, 1.0, authoredBendAmplitude));
                        float bob = FastCosApprox(phase * 1.31 + aupGeneticHash.y * 6.0) * (_SargassumWaveAmplitude * 0.18 * lerp(0.35, 1.0, authoredBendAmplitude));
                        float verticalFromRoot = basePositionWS.y - renderOriginWS.y;
                        renderOriginWS.y = resolvedWaterLevel + driftOffsetWS.y + waveLift;
                        animatedPositionWS.y = renderOriginWS.y + verticalFromRoot + bob * bendMask;
                        float2 surfaceDrift = currentDirection + float2(FastSinApprox(phase * 0.73), FastCosApprox(phase * 0.91)) * (currentStrength * 0.15);
                        animatedPositionWS.xz += SafeNormalize2(surfaceDrift) * (_SargassumWaveAmplitude * 0.22 * bendMask * detailAmplitude);
                        float pulsePhase = timeValue * _SargassumPulsationSpeed + instanceNoise * 9.7 + organicDensity * (_SargassumPulsationFrequency * 6.28318);
                        float pulse = FastSinApprox(pulsePhase) * _SargassumPulsationAmplitude * edgePulse * bendMask * detailAmplitude;
                        float2 radialWS = SafeNormalize2(animatedPositionWS.xz - renderOriginWS.xz + float2(0.001, 0.001));
                        animatedPositionWS += wakeTrailOffset * 0.45;
                        animatedPositionWS += submarineWashOffset * 0.72;
                        animatedPositionWS += ambientFloraSwayOffset * 0.58;
                        animatedPositionWS += floraSwayFieldOffset * 0.68;
                        animatedPositionWS += proceduralWakeOffset * 0.62;
                        animatedPositionWS += flowSynchronyOffset;
                        animatedPositionWS.xz += currentOffset.xz * (0.5 * detailAmplitude);
                        animatedPositionWS.y += currentOffset.y * (0.18 * detailAmplitude);
                        animatedPositionWS.xz += radialWS * pulse;
                        animatedPositionWS.y += pulse * 0.18;
                    }
                }
                else
                {
                    float farPhase = timeValue * (0.6 + _HectonVegetationCurrentTimeScale * 0.5) + instanceNoise * 9.0;
                    float farCurrentTorsion;
                    float3 farCurrentOffset = CalculateUnderwaterCurrents(
                        renderOriginWS,
                        basePositionWS,
                        bendMask * saturate(lodAlpha + 0.1),
                        heightMask,
                        instanceType,
                        instanceNoise,
                        timeValue,
                        currentVector,
                        currentStrength,
                        farCurrentTorsion);

                    if (instanceType > 1.5)
                    {
                        float oceanLift = EvaluateOceanSurfaceLift(renderOriginWS.xz);
                        float oceanLiftBlend = saturate(_HectonOceanSurfaceWaveMeta.x);
                        renderOriginWS.y = resolvedWaterLevel + driftOffsetWS.y + oceanLift +
                            FastSinApprox(farPhase) * (_SargassumWaveAmplitude * lerp(0.9, 0.24, oceanLiftBlend));
                    }

                    animatedPositionWS = ResolveBillboardPositionWS(renderOriginWS, localPosition, instanceHeight, instanceWidth, heightMask, ResolveVegetationViewPositionWS());

                    float2 farFlow = ResolvePlanarOceanFlowDirection(currentVector + float2(FastSinApprox(farPhase), FastCosApprox(farPhase * 0.83)) * currentStrength);
                    float farStateSwayScale = lerp(1.0, 1.18, agitatedWeight) * lerp(1.0, 0.58, dyingWeight);
                    float farSwayStrength = (instanceType < 0.5 ? _GrassWindAmplitude * 0.55 : _KelpCurrentAmplitude * 0.42) * wiltSuppression * farStateSwayScale * lerp(0.35, 1.0, authoredBendAmplitude) * lerp(0.35, 1.0, normalizedHealth);
                    animatedPositionWS += wakeTrailOffset * 0.8;
                    animatedPositionWS += submarineWashOffset * 0.75;
                    animatedPositionWS += ambientFloraSwayOffset * 0.52;
                    animatedPositionWS += floraSwayFieldOffset * 0.55;
                    animatedPositionWS += proceduralWakeOffset * 0.42;
                    animatedPositionWS += flowSynchronyOffset * 0.85;
                    animatedPositionWS.xz += farFlow * (farSwayStrength * bendMask * lodAlpha);
                    animatedPositionWS += farCurrentOffset * 0.65;
                }

                float3 interactionOffset = ResolveInteractionOffset(animatedPositionWS, baseNormalWS, bendMask, ResolveVegetationViewDistanceSq(animatedPositionWS), 0.0);
                float3 playerBendOffset = ResolvePlayerBendOffset(animatedPositionWS, baseNormalWS, bendMask, instanceType);
                float3 impactOffset = ResolveImpactOffset(animatedPositionWS, baseNormalWS, bendMask);
                float fieldDrivenBend = saturate(_HectonFloraSwayFieldParams.y);
                interactionOffset *= (1.0 - fieldDrivenBend);
                playerBendOffset *= (1.0 - fieldDrivenBend);
                float interactionTypeScale = ResolveInteractionTypeScale(instanceType);
                animatedPositionWS += impactOffset * 0.95;
                animatedPositionWS += interactionOffset * (_InteractionPushStrength * interactionTypeScale);
                animatedPositionWS += playerBendOffset * (_InteractionPushStrength * 1.1);
                animatedPositionWS.xz += currentDirection * (agitatedWeight * bendMask * instanceHeight * 0.035);
                animatedPositionWS = lerp(animatedPositionWS, renderOriginWS, dyingWeight * bendMask * 0.18);
                animatedPositionWS.y -= dyingWeight * instanceHeight * lerp(0.03, 0.16, heightMask);

                float seasonalDecayWeight =
                    saturate(SanitizeNonNegativeFinite(_HectonFloraLifecycleParams.y)) *
                    saturate(SanitizeNonNegativeFinite(_HectonFloraLifecycleParams.w));
                if (seasonalDecayWeight > 0.0001)
                {
                    float seasonalWiltWeight = seasonalDecayWeight *
                        saturate(lerp(0.18, 1.0, heightMask) * lerp(0.35, 1.0, bendMask));
                    animatedPositionWS = lerp(animatedPositionWS, renderOriginWS, seasonalWiltWeight * 0.24);
                    animatedPositionWS.y -= instanceHeight * seasonalWiltWeight * lerp(0.04, 0.19, heightMask);
                    animatedPositionWS.xz += currentDirection * (-seasonalWiltWeight * instanceHeight * 0.018 * heightMask);
                }

                float snappedFlag = 0.0;
                if (_HectonVegetationRuntimeDrawParams.x > 0.5)
                    snappedFlag = saturate((float)_HectonFloraSnapFlags[sourceInstanceIndex]);

                float snappedMask = max(snappedFlag, ResolveAbyssalFlowSnapMask(renderOriginWS, animatedPositionWS, bendMask, heightMask, instanceType));
                if (snappedMask > 0.0001)
                {
                    float snappedWeight = saturate(snappedMask * lerp(0.22, 1.0, heightMask) * lerp(0.35, 1.0, curvatureMask));
                    float2 snappedDirection = SafeNormalize2(currentDirection + aupLeanDirection * 0.37 + float2(0.001, -0.001));
                    animatedPositionWS = lerp(animatedPositionWS, renderOriginWS, snappedWeight * 0.68);
                    animatedPositionWS.y -= snappedWeight * instanceHeight * lerp(0.18, 0.72, heightMask);
                    animatedPositionWS.xz += snappedDirection * (-snappedWeight * instanceHeight * 0.08 * heightMask);
                    entropyProgress = max(entropyProgress, snappedWeight * 0.82);
                }

                if (instanceType > 1.5)
                {
                    half cutMask = ResolveVegetationCutMask(instanceType, animatedPositionWS);
                    float woundCurl = smoothstep(0.05, 0.9, cutMask) * bendMask * _SargassumWoundCurlStrength;
                    if (woundCurl > 0.0001)
                    {
                        float2 woundDir = SafeNormalize2(animatedPositionWS.xz - renderOriginWS.xz + float2(0.001, 0.001));
                        animatedPositionWS.xz -= woundDir * woundCurl;
                        animatedPositionWS.y -= woundCurl * 0.22;
                    }
                }

                if (entropyProgress > 0.0001)
                {
                    float entropyWeight = saturate(entropyProgress * lerp(0.22, 1.0, heightMask) * lerp(0.35, 1.0, curvatureMask));
                    animatedPositionWS = lerp(animatedPositionWS, renderOriginWS, entropyWeight * 0.72);
                    animatedPositionWS.y -= entropyWeight * instanceHeight * lerp(0.12, 0.58, heightMask);
                    animatedPositionWS.xz = lerp(animatedPositionWS.xz, renderOriginWS.xz, entropyWeight * heightMask * 0.28);
                    animatedPositionWS.xz += currentDirection * (-entropyWeight * instanceHeight * 0.03 * heightMask);
                }

                float3 swayOffset = animatedPositionWS - basePositionWS;
                float3 normalWS = SafeNormalize3(baseNormalWS - swayOffset * (_NormalResponse * bendMask));
                if (proceduralWakeShear > 0.0001)
                {
                    float3 cameraLeanNormal = SafeNormalize3(_WorldSpaceCameraPos - animatedPositionWS);
                    normalWS = SafeNormalize3(lerp(normalWS, cameraLeanNormal, saturate(proceduralWakeShear * lerp(0.16, 0.38, heightMask))));
                }

                if (_HectonVegetationRuntimeLodParams.x >= 0.5)
                {
                    float3 cameraDelta = _WorldSpaceCameraPos - animatedPositionWS;
                    float3 viewFacingNormal = SafeNormalize3(float3(cameraDelta.x, 0.3, cameraDelta.z));
                    normalWS = SafeNormalize3(lerp(normalWS, viewFacingNormal, 0.78));
                }

                float kelpDepthBelowWater = max(0.0, resolvedWaterLevel - renderOriginWS.y);
                float kelpDepthFade = saturate((kelpDepthBelowWater - HECTON_KELP_FADE_START_DEPTH) *
                    rcp(max(HECTON_KELP_FADE_END_DEPTH - HECTON_KELP_FADE_START_DEPTH, 0.001)));

                output.positionWS = animatedPositionWS;
                output.originWS = renderOriginWS;
                output.normalWS = normalWS;
                output.positionCS = TransformWorldToHClip(animatedPositionWS);
                output.heightMask = heightMask;
                output.lodAlpha = lodAlpha;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.instanceType = instanceType;
                output.kelpDepthFade = kelpDepthFade;
                output.edgeMask = saturate(abs(input.uv.x * 2.0 - 1.0) + scatterVisualPayload.y * 0.16);
                output.curvatureMask = saturate(curvatureMask + scatterVisualPayload.x * 0.12);
                output.entropyProgress = entropyProgress;
                output.parasiteMask = parasiteMask;
                output.runtimeState = instanceData.RuntimeState;
                output.pulseFrequency = max(0.01, instanceData.PulseFrequency);
                half4 authoredBiolumColor = half4(instanceData.BioluminescenceColor.rgb, saturate(max(packedPresentation.x, vertexBiolumMask) * lerp(1.0, 1.28, scatterVisualPayload.w)));
                output.biolumColor = _HectonFloraVertexColorDebug > 0.5 ? half4(input.color) : ResolveSyncedBiolumColor(authoredBiolumColor);
                output.flowMagnitude = saturate(flowMagnitude + scatterVisualPayload.z * 0.18);
                output.biomeLayer = biomeLayer;
                output.cascadeSeed = _HectonFloraPhaseSeeds[sourceInstanceIndex];
                output.growth01 = growth01;
                output.health01 = normalizedHealth;
                output.geneticTraits = geneticTraits;
                float safeTemplateIndex = isfinite(instanceData.TemplateIndex) ? instanceData.TemplateIndex : -1.0;
                float safeVariation = isfinite(instanceData.Variation) ? saturate(instanceData.Variation) : 0.0;
                float templateSyncSeed = round(safeTemplateIndex);
                float fallbackSyncSeed = round(instanceType * 17.0 + safeVariation * 1024.0);
                float sourceIndexSeed = (float)(sourceInstanceIndex & 1023u);
                float3 localBiolumCoord = animatedPositionWS - renderOriginWS;
                float3 localPulseSeed = float3(
                    localBiolumCoord.x + templateSyncSeed * 0.173,
                    localBiolumCoord.z + safeVariation * 13.0,
                    sourceIndexSeed * 0.03125);
                half spatialPulseOffset = (half)ResolveSpatialHashPulseOffset(localPulseSeed);
                half biolumSyncGroup = (half)fmod(abs(templateSyncSeed >= 0.0 ? templateSyncSeed : fallbackSyncSeed), 4.0);
                output.biolumPulseData = half2(spatialPulseOffset, biolumSyncGroup + (half)(vertexAoVisibility * 0.125));
                output.globalBiolumVertexPulse = ResolveIndirectVegetationGlobalBiolumVertexPulse(localBiolumCoord, biolumSyncGroup);
                output.uv = half2(input.uv);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(input.growth01 + 0.0001h);
                if (_HectonFloraVertexColorDebug > 0.5)
                    return half4(input.biolumColor.rgb, 1.0h);

                half screenIgn = (half)ResolveVegetationIgn(input.positionCS);
                half cutMask = ResolveVegetationCutMask(input.instanceType, input.positionWS);
                half coverageVisibility = ResolveVegetationVisibilityGate(0.08h - cutMask, 0.0h, 0.025h);
                coverageVisibility *= ResolveLodDitherCoverage(screenIgn, input.lodAlpha);

                half porousCoverageMask = input.instanceType > 1.5h ? ResolveSargassumPorousCoverage(input.positionWS, input.heightMask) : 1.0h;
                if (input.instanceType > 1.5h)
                    coverageVisibility *= ResolveVegetationVisibilityGate(porousCoverageMask, 0.16h, 0.08h);

                half entropyCoverage = 1.0h - input.entropyProgress * saturate(lerp(0.28h, 1.0h, input.heightMask) * lerp(0.35h, 1.0h, input.curvatureMask));
                half coverage = saturate(_Opacity) * porousCoverageMask * entropyCoverage;
                coverageVisibility *= (half)step(InterleavedGradientNoise(input.positionCS.xy), coverage);
                coverageVisibility *= ResolveCullFadeCoverage(input.positionWS, input.positionCS);
                half leafAlpha = SAMPLE_TEXTURE2D(_FloraAlphaMask, sampler_FloraAlphaMask, input.uv).a;
                coverageVisibility *= saturate(leafAlpha);
                clip(coverageVisibility - max((half)_AlphaClip, 0.01h));

                half3 normalWS = SafeNormalize3(input.normalWS);
                half3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight();
                half3 lightDirectionWS = SafeNormalize3(mainLight.direction);
                half abyssFactor = ResolveAbyssalFactor(input.positionWS);
                half sunVisibility = ResolveAbyssalSunVisibility(input.positionWS);
                half ambientVisibility = ResolveAbyssalAmbientVisibility(input.positionWS);
                half NdotL = saturate(dot(normalWS, lightDirectionWS));
                half wrapDiffuse = saturate(NdotL * 0.5h + 0.5h);
                half backLight = saturate(dot(-normalWS, lightDirectionWS));
                half rimBase = 1.0h - saturate(dot(normalWS, viewDirectionWS));
                half rim = rimBase * rimBase * rimBase;

                half3 baseColor;
                half3 tipColor;
                if (input.instanceType < 0.5h)
                {
                    baseColor = _GrassBaseColor.rgb;
                    tipColor = _GrassTipColor.rgb;
                }
                else if (input.instanceType < 1.5h)
                {
                    baseColor = _KelpBaseColor.rgb;
                    tipColor = _KelpTipColor.rgb;
                }
                else
                {
                    baseColor = _SargassumBaseColor.rgb;
                    tipColor = _SargassumTipColor.rgb;
                }

                half3 gradientColor = lerp(baseColor, tipColor, input.heightMask);
                half growthVisible01 = saturate(input.growth01);
                gradientColor = lerp(_SeedlingColor.rgb, gradientColor, growthVisible01);
                gradientColor = ResolveSeasonalColorDrift(gradientColor, input.biomeLayer, input.positionWS);
                half traitBytePresent = step(0.5h, input.geneticTraits);
                half poisonousTrait = HasGeneticTrait(input.geneticTraits, 1.0h) * traitBytePresent;
                half edibleTrait = HasGeneticTrait(input.geneticTraits, 2.0h) * traitBytePresent;
                gradientColor = lerp(gradientColor, gradientColor * half3(0.78h, 1.12h, 0.92h), poisonousTrait * 0.18h);
                gradientColor = lerp(gradientColor, gradientColor * half3(1.08h, 1.02h, 0.88h), edibleTrait * 0.10h);
                half gradientLuma = dot(gradientColor, half3(0.299h, 0.587h, 0.114h));
                half3 decayColor = lerp(half3(gradientLuma, gradientLuma, gradientLuma), half3(0.32h, 0.29h, 0.24h), 0.55h);
                gradientColor = lerp(gradientColor, decayColor, input.entropyProgress * 0.92h);
                half necrosisMask = ResolveEdgeInwardNecrosisMask(input);
                if (input.health01 < 0.985h)
                {
                    half edgeSignal = ResolveNecrosisEdgeSignal(input);
                    half necrosisClipNoise = (half)ValueNoise3D(
                        input.positionWS * 4.7 +
                        float3(input.cascadeSeed * 11.0h, _Time.y * 0.021h, input.pulseFrequency * 3.0h));
                    half inwardThreshold = saturate(1.0h - saturate(input.health01) + necrosisClipNoise * 0.22h);
                    coverageVisibility *= (half)step(inwardThreshold, edgeSignal);
                }
                coverageVisibility = saturate(coverageVisibility);
                clip(coverageVisibility - max((half)_AlphaClip, 0.01h));

                half necrosisNoise = (half)ValueNoise3D(input.positionWS * 5.6 + float3(0.0, input.cascadeSeed * 7.0h, _Time.y * 0.025));
                half3 necrosisColor = lerp(half3(0.025h, 0.018h, 0.012h), half3(0.20h, 0.10h, 0.035h), necrosisNoise);
                gradientColor = lerp(gradientColor, necrosisColor, necrosisMask);
                half3 parasiteGlowTint = input.instanceType < 1.5h
                    ? half3(0.18h, 0.95h, 0.72h)
                    : half3(0.14h, 0.78h, 1.00h);
                gradientColor = lerp(gradientColor, gradientColor + parasiteGlowTint * 0.38h, input.parasiteMask * saturate(1.0h - input.entropyProgress * 0.65h));
                half vertexWearMask = saturate(input.curvatureMask);
                gradientColor = lerp(gradientColor, gradientColor * half3(0.90h, 0.84h, 0.72h), vertexWearMask * 0.14h);
                half biomeTintStrength = 0.10h;
                half3 biomeAmbientTint = lerp(half3(1.0h, 1.0h, 1.0h), saturate(_HectonVegetationAmbientColor.rgb + 0.16h), biomeTintStrength);
                gradientColor *= biomeAmbientTint;
                if (input.biomeLayer > 2.5h)
                {
                    half deadZoneLuma = dot(gradientColor, half3(0.299h, 0.587h, 0.114h));
                    gradientColor = lerp(half3(deadZoneLuma, deadZoneLuma, deadZoneLuma), gradientColor, 0.82h);
                }

                if (input.instanceType > 0.5h && input.instanceType < 1.5h)
                {
                    half lightFactor = saturate(_HectonVegetationLightFactor);
                    half turbidity = saturate(_HectonVegetationTurbidity * 0.5h);
                    half fogBlend = saturate(0.35h + input.kelpDepthFade * 0.65h);
                    gradientColor = lerp(gradientColor, _HectonVegetationFogColor.rgb, fogBlend);
                    half kelpLuma = dot(gradientColor, half3(0.299h, 0.587h, 0.114h));
                    gradientColor = lerp(half3(kelpLuma, kelpLuma, kelpLuma), gradientColor, 1.0h - input.kelpDepthFade);
                    gradientColor *= lerp(1.0h, 0.72h, turbidity);
                    gradientColor *= lerp(0.38h, 1.0h, lightFactor);
                }

                half shearFoam = saturate(_ShearFoamAmount) * saturate(input.heightMask * 0.72h + input.edgeMask * 0.28h);
                half shearFloraMask = input.instanceType > 0.5h ? 1.0h : 0.35h;
                gradientColor = lerp(gradientColor, half3(0.56h, 0.82h, 0.88h), shearFoam * shearFloraMask * 0.32h);

                half3 probeAmbient = H8CustomLightProbeResolveAmbient(input.positionWS, normalWS, (half3)_HectonVegetationAmbientColor.rgb);
                half3 ambient = lerp(_HectonVegetationAmbientColor.rgb, probeAmbient, 0.55h) * (_AmbientStrength * ambientVisibility);
                half vertexAoVisibility = saturate(frac(input.biolumPulseData.y) * 8.0h);
                half bakedAoLighting = lerp(0.52h, 1.0h, vertexAoVisibility);
                half3 diffuse = gradientColor * ambient;
                diffuse += gradientColor * (mainLight.color * wrapDiffuse * sunVisibility);
                diffuse *= bakedAoLighting;
                half3 transmission = _TranslucencyColor.rgb * backLight * input.heightMask * _TranslucencyStrength * sunVisibility * lerp(0.76h, 1.0h, vertexAoVisibility);
                half3 bladeAxisWS = SafeNormalize3(input.positionWS - input.originWS + float3(0.0, 0.02, 0.0));
                half3 lightViewBisector = SafeNormalize3(lightDirectionWS + viewDirectionWS);
                half bladeAlignment = saturate(1.0h - abs(dot(bladeAxisWS, lightViewBisector)));
                half backlightPhase = FastVegetationPower01(saturate(dot(lightDirectionWS, -viewDirectionWS)), lerp(2.0h, 7.0h, _BacklightViewBias));
                half anisotropicPhase = FastVegetationPower01(bladeAlignment, _AnisotropicSssPower);
                half anisotropicMask = input.instanceType < 0.5h ? 0.42h : (input.instanceType < 1.5h ? 1.0h : 0.78h);
                half thicknessMask = saturate(0.28h + input.heightMask * 0.72h);
                half kelpMask = input.instanceType > 0.5h && input.instanceType < 1.5h ? 1.0h : 0.0h;
                half sargassumMask = input.instanceType > 1.5h ? 1.0h : 0.0h;
                half edgeBacklightMask = saturate(input.edgeMask * input.edgeMask);
                half edgeBiasedThickness = lerp(thicknessMask * 0.48h, thicknessMask * 1.38h, edgeBacklightMask);
                half3 kelpGoldTint = lerp(tipColor, half3(1.0h, 0.78h, 0.34h), 0.58h);
                half3 sargassumGoldTint = lerp(tipColor, half3(1.0h, 0.82h, 0.28h), 0.74h);
                half3 anisotropicTint = lerp(_TranslucencyColor.rgb, kelpGoldTint, kelpMask * 0.82h);
                anisotropicTint = lerp(anisotropicTint, sargassumGoldTint, sargassumMask * (0.62h + edgeBacklightMask * 0.38h));
                half edgeFocusedBacklight = lerp(1.0h, edgeBiasedThickness * 1.12h, sargassumMask);
                half3 anisotropicSss = anisotropicTint * (anisotropicPhase * backlightPhase * edgeFocusedBacklight * anisotropicMask * _AnisotropicSssStrength);
                half organicSssMask = saturate(edgeBiasedThickness * lerp(0.45h, 1.0h, input.curvatureMask));
                half3 organicSss = HectonCoreLitEvaluateOrganicSss(
                    viewDirectionWS,
                    lightDirectionWS,
                    normalWS,
                    anisotropicTint * organicSssMask,
                    _OrganicSssDistortion,
                    _OrganicSssPower,
                    _OrganicSssScale * organicSssMask);
                half edgeBloomMask = saturate(backlightPhase * edgeBacklightMask * rim);
                half3 edgeBloomTint = lerp(kelpGoldTint, sargassumGoldTint, sargassumMask);
                half3 edgeBloom = edgeBloomTint * (edgeBloomMask * _EdgeBloomStrength * (0.35h + 0.65h * max(kelpMask, sargassumMask)));
                half rimLightingVisibility = max(sunVisibility, ambientVisibility);
                half localCausticMask = ResolveLocalLightCaustic(input.positionWS, normalWS, input.heightMask);
                half3 finalColor = diffuse + transmission + tipColor * rim * (0.08h * rimLightingVisibility);
                finalColor += organicSss * mainLight.color * sunVisibility;
                finalColor += anisotropicSss * mainLight.color * sunVisibility;
                finalColor += edgeBloom * mainLight.color * (1.45h * sunVisibility);
                half agitatedWeight = saturate(1.0h - abs(input.runtimeState - 1.0h));
                half dyingWeight = saturate(1.0h - abs(input.runtimeState - 2.0h));
                half agePulseSpeed = lerp(2.65h, 0.72h, growthVisible01);
                half pulsePhase = (_Time.y * max(0.01h, input.pulseFrequency) * agePulseSpeed * 6.28318h) + input.biolumPulseData.x + input.heightMask * 3.1h;
                half seedlingPulse = lerp(0.68h, 1.34h, 0.5h + 0.5h * (half)FastSinApprox(pulsePhase));
                half maturePulse = lerp(0.84h, 1.22h, 0.5h + 0.5h * (half)FastSinApprox(pulsePhase * 0.42h));
                half pulseStrength = lerp(seedlingPulse, maturePulse, smoothstep(0.58h, 1.0h, growthVisible01));
                half stateEmissionScale = lerp(1.0h, 1.18h, agitatedWeight);
                stateEmissionScale = lerp(stateEmissionScale, 0.28h, dyingWeight);
                half predatorDim = ResolveBiolumPredatorDim(input.positionWS);
                half parasiteBiolumBoost = lerp(1.0h, 1.12h, input.parasiteMask);
                half biolumVisibility = saturate((1.0h - input.entropyProgress * 0.65h) * (1.0h - necrosisMask));
                half flowReactiveBoost = 1.0h + (max(0.0h, input.flowMagnitude) * 0.5h);
                float cameraDistanceSq = ResolveCameraDistanceSq(input.positionWS);
                half distanceBiolumDimming = ResolveDistanceBiolumDimming(cameraDistanceSq);
                half distanceBiolumPixelGate = ResolveDistanceBiolumPixelGate(cameraDistanceSq, input.positionCS);
                half seasonalBloomScale = ResolveSeasonalBloomEmissionScale();
                float safePixelSeasonCycle = isfinite(_SeasonCycle) ? _SeasonCycle : 0.0;
                half decaySeasonPulse = 0.5h + 0.5h * (half)FastCosApprox((safePixelSeasonCycle - 0.75h) * 6.28318h);
                half decaySeasonWeight = (half)saturate(SanitizeNonNegativeFinite(_HectonFloraLifecycleParams.y)) *
                    lerp(0.55h, 1.0h, decaySeasonPulse);
                half seasonalDecaySuppression = lerp(
                    1.0h,
                    0.78h,
                    saturate(decaySeasonWeight * (half)SanitizeNonNegativeFinite(_HectonFloraLifecycleParams.w)));
                half cascadeEmissionScale = 1.0h + ResolveCascadeEmissionScale(input.cascadeSeed);
                half flashBangScale = ResolveBiolumFlashBangBoost(input.positionWS);
                half flashlightPhotophobia = HectonCoreLitResolveFlashlightPhotophobia(input.positionWS);
                half emitsLightTrait = HasGeneticTrait(input.geneticTraits, 4.0h);
                half geneticEmissionGate = lerp(1.0h, emitsLightTrait, traitBytePresent);
                float3 fragmentLocalAupCoord = input.positionWS - (float3)input.originWS;
                half4 globalBiolumState = ResolveIndirectVegetationGlobalBiolum(fragmentLocalAupCoord, input.biolumPulseData.y, input.globalBiolumVertexPulse);
                half authoredBiolumGate = step(0.001h, input.biolumColor.a);
                half globalBiolumMask = step(0.001h, globalBiolumState.w) * authoredBiolumGate;
                half3 biolumColor = lerp(input.biolumColor.rgb, globalBiolumState.rgb, globalBiolumMask);
                half biolumIntensity = max(input.biolumColor.a, globalBiolumState.w * authoredBiolumGate);
                half biolumEnergy = biolumIntensity * pulseStrength * stateEmissionScale * predatorDim * parasiteBiolumBoost * biolumVisibility * flowReactiveBoost * distanceBiolumDimming * distanceBiolumPixelGate * seasonalBloomScale * seasonalDecaySuppression * cascadeEmissionScale * flashBangScale * flashlightPhotophobia * geneticEmissionGate;
                half ageEmissionScale = lerp(0.35h, 1.18h, smoothstep(0.12h, 1.0h, growthVisible01));
                biolumEnergy = clamp(biolumEnergy * ageEmissionScale * growthVisible01 * saturate(input.health01), 0.0h, 10.0h);
                half3 biolumEmission = biolumColor * biolumEnergy;
                half sporeSourceEnergy = biolumIntensity * biolumVisibility * seasonalBloomScale * cascadeEmissionScale * flashlightPhotophobia * geneticEmissionGate;
                half3 sporeEmission = ResolveBiolumSporeEmission(input, biolumColor, sporeSourceEnergy);
                half3 decayTint = lerp(half3(1.0h, 1.0h, 1.0h), half3(0.92h, 0.84h, 0.68h), decaySeasonWeight * 0.22h);
                finalColor *= decayTint;
                finalColor += biolumEmission;
                finalColor += sporeEmission;

                #ifdef _ADDITIONAL_LIGHTS
                uint addLightCount = GetAdditionalLightsCount();
                addLightCount = min(addLightCount, HECTON_VEGETATION_ADDITIONAL_LIGHT_CAP);
                half3 additionalDiffuse = half3(0.0h, 0.0h, 0.0h);
                half3 localVolumetric = half3(0.0h, 0.0h, 0.0h);
                half abyssLightVisibility = ResolveAbyssalAdditionalLightVisibility(input.positionWS, cameraDistanceSq);
                for (uint lightIndex = 0u; lightIndex < addLightCount; lightIndex++)
                {
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    float additionalShadowAttenuation = HectonCoreLitResolveFlashlightAdditionalShadow(lightIndex, input.positionWS, normalWS, addLight.shadowAttenuation);
                    half attenuation = addLight.distanceAttenuation * additionalShadowAttenuation;
                    if (attenuation <= 0.0001h)
                        continue;

                    half localNdotL = saturate(dot(normalWS, addLight.direction));
                    half localBacklight = saturate(dot(-normalWS, addLight.direction));
                    half causticLightMask = lerp(1.0h, localCausticMask, saturate(localNdotL * attenuation));
                    half3 localContribution = gradientColor * (addLight.color * localNdotL * attenuation * causticLightMask);
                    half3 localTranslucency = edgeBloomTint * (localBacklight * input.heightMask * attenuation * (_TranslucencyStrength * 0.8h));
                    half phaseCos = saturate(dot(viewDirectionWS, -addLight.direction));
                    half forwardScatter = EvaluateSchlickPhase(phaseCos, 0.68h);
                    half volumetricMask = saturate((0.4h + _HectonVegetationTurbidity) * (0.55h + abyssFactor * 0.9h));
                    half selfShadowGuard = saturate(lerp(0.28h, 1.0h, max(edgeBacklightMask, input.heightMask)) * lerp(1.0h, porousCoverageMask, sargassumMask));
                    half shaftRange = saturate(0.22h + localBacklight * 0.78h);
                    half3 localShaft = addLight.color * (forwardScatter * attenuation * volumetricMask * selfShadowGuard * shaftRange * (0.08h + 0.18h * max(kelpMask, sargassumMask)));

                    additionalDiffuse += (localContribution + localTranslucency) * abyssLightVisibility;
                    localVolumetric += localShaft * abyssLightVisibility;
                }
                finalColor += additionalDiffuse;
                finalColor += localVolumetric;
                #endif
                half3 abyssFogColor = lerp(_HectonVegetationFogColor.rgb, half3(0.01h, 0.03h, 0.045h), abyssFactor);
                half fogBlend = saturate(input.fogFactor * lerp(1.0h, 1.25h, abyssFactor));
                finalColor = lerp(finalColor, abyssFogColor, fogBlend);
                half lowQualityDitherWeight = (half)(1.0 - smoothstep(0.1, 0.35, ResolveIndirectVegetationQualityWeight()));
                half ditherOffset = (half)((screenIgn - 0.5) * (1.0 / 255.0)) * lowQualityDitherWeight;
                half3 ditheredFinalColor = max(finalColor + ditherOffset, half3(0.0015h, 0.0023h, 0.0031h));
                finalColor = lerp(finalColor, ditheredFinalColor, lowQualityDitherWeight);
                coverageVisibility = saturate(coverageVisibility);
                finalColor = lerp(half3(0.0015h, 0.0023h, 0.0031h), finalColor, coverageVisibility);
                return half4(finalColor, coverageVisibility);
            }
            ENDHLSL
        }
    }
}
