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
            #pragma multi_compile _ HECTON_GPU_INDIRECT
            #pragma shader_feature_local _QUALITY_MX350 _QUALITY_HIGH
            #pragma skip_variants _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "UnityIndirect.cginc"
            #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"

            #define HECTON_MAX_INTERACTION_POINTS 12
            #define HECTON_MAX_IMPACT_SPHERES 8
            #define HECTON_KELP_FADE_START_DEPTH 150.0
            #define HECTON_KELP_FADE_END_DEPTH 200.0
            #define HECTON_ABYSS_SUN_FADE_START_DEPTH 350.0
            #define HECTON_ABYSS_SUN_BLACKOUT_DEPTH 500.0
            #define HECTON_ABYSS_SUN_ABSOLUTE_DEPTH 600.0
            #define HECTON_ABYSS_LIGHT_EXTINCTION_START_DEPTH 1000.0
            #define HECTON_ABYSS_LIGHT_EXTINCTION_FULL_DEPTH 1600.0
#if defined(_QUALITY_MX350)
            #define HECTON_VEGETATION_ADDITIONAL_LIGHT_CAP 2u
#else
            #define HECTON_VEGETATION_ADDITIONAL_LIGHT_CAP 4u
#endif

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
                float _HectonLodPassMode;
                float _HectonLodNearDistance;
                float _HectonLodFarDistance;
                float _HectonLodTransitionRange;
                float _HectonImpostorWidth;
                float _HectonImpostorHeight;
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
            StructuredBuffer<uint> _HectonVisibleInstanceIndices;
            StructuredBuffer<uint> _HectonFloraSnapFlags;
            StructuredBuffer<float2> _MarineSnowFlowField;
            StructuredBuffer<float4> _AbyssalFlowFieldResult;
            StructuredBuffer<float4> _PredatorAUPBuffer;
            float4 _ChunkWorldOffset;
            float4 _GlobalFloatingOffset;
            StructuredBuffer<FloraInteractionPointGpuData> _HectonFloraInteractionPoints;
            StructuredBuffer<float4> _HectonImpactSpheres;

            float4 _MarineSnowFlowFieldCenterCellSize;
            float4 _HectonVegetationFogColor;
            float4 _HectonVegetationAmbientColor;
            float4 _HectonVegetationCurrentVector;
            float4 _GlobalOceanFlow;
            float4 _HectonFloatingOriginOffset;
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
            float4 _HectonFloraLifecycleParams;
            float4 _HectonFloraCascadeParams;
            float4 _HectonSubmarineWashSphere;
            float4 _HectonSubmarineWashVelocity;
            float4 _HectonSubmarineWashAupGrid;
            float4 _HectonSubmarineWashAupLocal;
            float4 _HectonFlowSynchronyParams;
            float4 _AbyssalGridResolution;
            float4 _AbyssalFlowCenter;
            float4 _AbyssalFlowSpacing;
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
            int _HectonFloraFlowFieldResolution;
            int _HectonFloraInteractionCount;
            int _HectonImpactSphereCount;
            int _PredatorAUPCount;

            TEXTURE2D(_SargassumCutMaskRT);
            SAMPLER(sampler_SargassumCutMaskRT);
            TEXTURE2D(_HectonShallowWaterFieldRT);
            SAMPLER(sampler_HectonShallowWaterFieldRT);
            TEXTURE2D(_BlueNoiseTex);
            SAMPLER(sampler_BlueNoiseTex);
            float4 _BlueNoiseTex_TexelSize;

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
                half spatialPulseOffset : TEXCOORD21;
            };

            uint MathHashUint3(uint3 value)
            {
                value ^= value.yzx * uint3(0x9E3779B9u, 0x85EBCA6Bu, 0xC2B2AE35u);
                value = (value ^ (value >> 16)) * uint3(0x85EBCA6Bu, 0xC2B2AE35u, 0x27D4EB2Fu);
                value ^= value.zxy * uint3(0x165667B1u, 0xD3A2646Cu, 0x9E3779B9u);
                value ^= value >> 13;
                return value.x ^ value.y ^ value.z;
            }

            float Hash01FromUint(uint value)
            {
                return (float)(value & 0x00FFFFFFu) * (1.0 / 16777215.0);
            }

            uint3 QuantizeHashSeed3(float3 value)
            {
                int3 quantized = (int3)floor(value * 16.0);
                return (uint3)(quantized + int3(1048576, 1048576, 1048576));
            }

            float Hash21(float2 value)
            {
                uint3 seed = QuantizeHashSeed3(float3(value, 0.0));
                return Hash01FromUint(MathHashUint3(seed ^ uint3(0xA511E9B3u, 0x63D83595u, 0xB6C4A793u)));
            }

            float Hash31(float3 value)
            {
                return Hash01FromUint(MathHashUint3(QuantizeHashSeed3(value)));
            }

            float WrapPhasePi(float phase)
            {
                const float twoPi = 6.28318530718;
                const float invTwoPi = 0.15915494309;
                return phase - floor((phase + 3.14159265359) * invTwoPi) * twoPi;
            }

            float FastSinApprox(float phase)
            {
                float x = WrapPhasePi(phase);
                float x2 = x * x;
                return x * (1.0 - x2 * (0.1666666716 - x2 * (0.0083333310 - x2 * 0.0001984127)));
            }

            float FastCosApprox(float phase)
            {
                return FastSinApprox(phase + 1.57079632679);
            }

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

            float SampleCurrentNoise3D(float3 samplePosition)
            {
                #if defined(_QUALITY_HIGH)
                    float lowFrequency = ValueNoise3D(samplePosition);
                    float highFrequency = ValueNoise3D(samplePosition * 1.83 + float3(19.7, 7.1, 13.4));
                    return lowFrequency * 0.68 + highFrequency * 0.32;
                #else
                    float layer0 = FastSinApprox(dot(samplePosition, float3(1.11, 0.73, 1.37)));
                    float layer1 = FastCosApprox(dot(samplePosition.zxy + 17.0, float3(0.83, 1.27, 1.07)));
                    float layer2 = FastSinApprox(dot(samplePosition.yzx - 9.0, float3(1.41, 0.69, 0.92)));
                    return layer0 * 0.55 + layer1 * 0.30 + layer2 * 0.15;
                #endif
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

            float3 ResolveAbyssalFlowField(float3 positionWS)
            {
                int resolutionX = (int)max(_AbyssalGridResolution.x, 0.0);
                int resolutionY = (int)max(_AbyssalGridResolution.y, 0.0);
                int resolutionZ = (int)max(_AbyssalGridResolution.z, 0.0);
                int nodeCount = (int)max(_AbyssalGridResolution.w, 0.0);
                if (resolutionX <= 1 || resolutionY <= 1 || resolutionZ <= 1 || nodeCount <= 0)
                    return float3(0.0, 0.0, 0.0);

                float horizontalCellSize = max(_AbyssalFlowSpacing.x, 0.001);
                float verticalCellSize = max(_AbyssalFlowSpacing.y, 0.001);
                int3 halfExtent = int3(resolutionX >> 1, resolutionY >> 1, resolutionZ >> 1);
                float3 localPosition = positionWS - _AbyssalFlowCenter.xyz;
                int3 coord = int3(round(float3(
                    localPosition.x / horizontalCellSize,
                    localPosition.y / verticalCellSize,
                    localPosition.z / horizontalCellSize))) + halfExtent;
                coord = clamp(coord, int3(0, 0, 0), int3(resolutionX - 1, resolutionY - 1, resolutionZ - 1));

                int index = coord.y * resolutionX * resolutionZ + coord.z * resolutionX + coord.x;
                if (index < 0 || index >= nodeCount)
                    return float3(0.0, 0.0, 0.0);

                return _AbyssalFlowFieldResult[index].xyz;
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

            float ResolveVegetationBlueNoise(float4 positionCS)
            {
                float2 pixel = floor(positionCS.xy);
                float fallback = InterleavedGradientNoise(pixel);
                float useBlueNoise = step(0.0001, _BlueNoiseTex_TexelSize.z) * step(0.0001, _BlueNoiseTex_TexelSize.w);
                float2 r2Offset = frac(floor(_Time.y * 60.0) * float2(0.75487766, 0.56984029));
                float2 texelScale = lerp(float2(1.0 / 64.0, 1.0 / 64.0), _BlueNoiseTex_TexelSize.xy, useBlueNoise);
                float2 blueNoiseUV = frac(pixel * texelScale + r2Offset);
                float sampled = SAMPLE_TEXTURE2D(_BlueNoiseTex, sampler_BlueNoiseTex, blueNoiseUV).r;
                return lerp(fallback, sampled, useBlueNoise);
            }

            half ResolveVegetationVisibilityGate(half signal, half threshold, half feather)
            {
                return saturate((signal - threshold) / max(feather, 0.0001h));
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

                float normalizedDepth = saturate((waterDepth - HECTON_ABYSS_LIGHT_EXTINCTION_START_DEPTH) /
                    (HECTON_ABYSS_LIGHT_EXTINCTION_FULL_DEPTH - HECTON_ABYSS_LIGHT_EXTINCTION_START_DEPTH));
                float extinction = lerp(0.0, 0.026, normalizedDepth * normalizedDepth);
                float cameraDistanceProxy = min(cameraDistanceSq * 0.01, 220.0);
                return exp2(-cameraDistanceProxy * extinction);
            }

            float EvaluateSchlickPhase(float cosTheta, float anisotropy)
            {
                float k = anisotropy * 0.5;
                float denominator = max(1.0 - k * cosTheta, 0.08);
                return (1.0 - k * k) / (12.56637 * denominator * denominator);
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

            half FastVegetationPower01(half value, half exponent)
            {
                half v = saturate(value);
                half v2 = v * v;
                half v4 = v2 * v2;
                half v8 = v4 * v4;
                half v16 = v8 * v8;
                half low = lerp(v, v4, saturate((exponent - 1.0h) * 0.33333333h));
                half high = lerp(v4, v16, saturate((exponent - 4.0h) * 0.08333333h));
                return lerp(low, high, step(4.0h, exponent));
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

            float ResolveSpatialHashPulseOffset(float3 positionWS)
            {
                const float cellSizeMeters = 24.0;
                float2 spatialCell = floor(positionWS.xz / cellSizeMeters);
                return Hash21(spatialCell + float2(17.31, 91.77)) * 6.28318;
            }

            half3 ResolveSeasonalColorDrift(half3 color, half biomeLayer, float3 positionWS)
            {
                half season01 = (half)frac(max(_HectonSeasonCycle, _SeasonCycle));
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
                float nearDistance = max(_HectonLodNearDistance, 0.01);
                float farDistance = max(_HectonLodFarDistance, nearDistance);
                float transitionRange = max(_HectonLodTransitionRange, 0.01);
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
                float farDistance = max(_HectonLodFarDistance, _HectonLodNearDistance);
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

            float ResolveInteractionDistance()
            {
                return max(12.0, min(_HectonLodNearDistance + _HectonLodTransitionRange, 55.0));
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

            float3 ResolveWakeTrailOffset(float3 evaluationPositionWS, float3 baseNormalWS, float bendMask, float heightMask, float instanceType)
            {
                if (bendMask <= 0.0001)
                    return float3(0.0, 0.0, 0.0);

                float4 shallowWaterData = EvaluateShallowWaterFieldData(evaluationPositionWS);
                float displacement = saturate(shallowWaterData.b);
                float2 planarVelocity = DecodeShallowWaterVelocity(shallowWaterData.rg);
                float velocityMagnitudeSq = dot(planarVelocity, planarVelocity);
                float velocityMagnitude = saturate(velocityMagnitudeSq);
                if (displacement <= 0.0001 && velocityMagnitude <= 0.0001)
                    return float3(0.0, 0.0, 0.0);

                float3 wakeDirection = SafeNormalize3(float3(planarVelocity.x, 0.0, planarVelocity.y));
                float3 planarWakeDirection = wakeDirection - baseNormalWS * dot(wakeDirection, baseNormalWS);
                planarWakeDirection = SafeNormalize3(planarWakeDirection);
                float typeScale = instanceType < 0.5 ? 0.72 : (instanceType < 1.5 ? 1.05 : 0.38);
                float flattening = (displacement + velocityMagnitude * 0.5) * bendMask * typeScale;
                if (instanceType > 0.5 && instanceType < 1.5)
                {
                    float whipFactor = saturate((velocityMagnitude - 0.58) * 2.8 + displacement * 0.75);
                    flattening *= lerp(1.0, 2.35, whipFactor);
                }
                float downwardBias = lerp(0.04, 0.18, heightMask) * flattening;
                return (planarWakeDirection + baseNormalWS * 0.02) * flattening + float3(0.0, -downwardBias, 0.0);
            }

            float3 ResolveSubmarineWashOffset(float3 evaluationPositionWS, float3 baseNormalWS, float bendMask, float heightMask, float instanceType)
            {
                if (bendMask <= 0.0001 || _HectonSubmarineWashSphere.w <= 0.0001 || _HectonSubmarineWashVelocity.w <= 0.0001)
                    return float3(0.0, 0.0, 0.0);

                float3 delta = evaluationPositionWS - _HectonSubmarineWashSphere.xyz;
                float radius = max(_HectonSubmarineWashSphere.w, 0.05);
                float radiusSq = radius * radius;
                float distSq = dot(delta, delta);
                if (distSq >= radiusSq)
                    return float3(0.0, 0.0, 0.0);

                float proximity = 1.0 - smoothstep(0.0, radiusSq, distSq);
                proximity *= proximity;
                float3 awayDirection = SafeNormalize3(float3(delta.x, 0.0, delta.z));
                float3 velocityDirection = _HectonSubmarineWashVelocity.xyz - baseNormalWS * dot(_HectonSubmarineWashVelocity.xyz, baseNormalWS);
                velocityDirection = SafeNormalize3(velocityDirection);
                float3 bendDirection = SafeNormalize3(lerp(awayDirection, velocityDirection, 0.65));
                float speedFactor = saturate(_HectonSubmarineWashVelocity.w * 0.045);
                float shockwave01 = saturate((_HectonSubmarineWashVelocity.w - 15.0) * 0.10);
                float typeScale = instanceType < 0.5 ? 0.55 : (instanceType < 1.5 ? 1.25 : 0.72);
                float flattening = proximity * bendMask * typeScale * (speedFactor * lerp(0.35, 1.0, heightMask) + shockwave01 * lerp(0.55, 1.45, heightMask));
                float downwardBias = lerp(0.02, 0.12 + shockwave01 * 0.26, heightMask) * flattening;
                return bendDirection * flattening + float3(0.0, -downwardBias, 0.0);
            }

            float ResolveAbyssalFlowSnapMask(float3 rootPositionWS, float3 evaluationPositionWS, float bendMask, float heightMask, float instanceType)
            {
                if (instanceType < 0.5 || instanceType > 1.5 || bendMask <= 0.0001 || _HectonSubmarineWashSphere.w <= 0.0001)
                    return 0.0;

                float speedGate = smoothstep(10.0, 12.5, _HectonSubmarineWashVelocity.w);
                if (speedGate <= 0.0001)
                    return 0.0;

                float radius = max(_HectonSubmarineWashSphere.w, 0.05);
                float3 rootDelta = rootPositionWS - _HectonSubmarineWashSphere.xyz;
                rootDelta.y *= 0.25;
                float radiusSq = radius * radius;
                float rootDeltaSq = dot(rootDelta, rootDelta);
                if (rootDeltaSq >= radiusSq)
                    return 0.0;

                float innerRadius = radius * 0.35;
                float proximity = 1.0 - smoothstep(innerRadius * innerRadius, radiusSq, rootDeltaSq);
                if (proximity <= 0.0001)
                    return 0.0;

                float2 velocityDirection = ResolvePlanarOceanFlowDirection(_HectonSubmarineWashVelocity.xz);
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

            float3 ResolveInteractionOffset(float3 evaluationPositionWS, float3 baseNormalWS, float bendMask, float distanceToCameraSq)
            {
                float interactionDistance = ResolveInteractionDistance();
                if (bendMask <= 0.0001 || distanceToCameraSq > interactionDistance * interactionDistance)
                    return float3(0.0, 0.0, 0.0);

                float3 interactionOffset = float3(0.0, 0.0, 0.0);
                int activeInteractionCount = min(_HectonFloraInteractionCount, HECTON_MAX_INTERACTION_POINTS);

                [loop]
                for (int i = 0; i < activeInteractionCount; i++)
                {
                    FloraInteractionPointGpuData interactionPoint = _HectonFloraInteractionPoints[i];
                    float3 velocity = interactionPoint.velocitySpeed.xyz;
                    float speed = interactionPoint.velocitySpeed.w;
                    float speedFactor = saturate(speed * 0.18);
                    if (speedFactor <= 0.0001)
                        continue;

                    float3 delta = evaluationPositionWS - interactionPoint.positionRadius.xyz;
                    delta.y *= 0.22;

                    float bendRadius = max(interactionPoint.positionRadius.w, 0.05);
                    float bendRadiusSq = bendRadius * bendRadius;
                    float distSq = dot(delta, delta);
                    float proximity = 1.0 - smoothstep(0.0, bendRadiusSq, distSq);
                    proximity = FastVegetationPower01((half)proximity, max(_InteractionDistancePower, 1.0h));
                    if (proximity <= 0.0001)
                        continue;

                    float3 planarVelocityDir = velocity - baseNormalWS * dot(velocity, baseNormalWS);
                    planarVelocityDir = SafeNormalize3(planarVelocityDir);
                    float3 radialDirection = SafeNormalize3(float3(delta.x, 0.0, delta.z));
                    float3 bendDirection = SafeNormalize3(lerp(radialDirection, planarVelocityDir, _InteractionVelocityBias));
                    float directionalBias = 0.65 + 0.35 * saturate(dot(-radialDirection, planarVelocityDir));

                    interactionOffset += (bendDirection + baseNormalWS * 0.04) * (proximity * speedFactor * directionalBias);
                }

                return interactionOffset * bendMask;
            }

            float3 ResolvePlayerBendOffset(float3 evaluationPositionWS, float3 baseNormalWS, float bendMask, float instanceType)
            {
                float playerRadius = _HectonPlayerRuntimePosition.w;
                if (bendMask <= 0.0001 ||
                    _HectonPlayerFloraInteractionParams.w < 0.5 ||
                    playerRadius <= 0.0001)
                {
                    return float3(0.0, 0.0, 0.0);
                }

                float3 playerRuntimePosition = _HectonPlayerRuntimePosition.xyz;
                float playerSpeed = _HectonPlayerFloraInteractionParams.x;
                float playerPush = _HectonPlayerFloraInteractionParams.y;
                if (playerSpeed <= 0.0001 || playerPush <= 0.0001)
                    return float3(0.0, 0.0, 0.0);

                float3 delta = evaluationPositionWS - playerRuntimePosition;
                delta.y *= 0.22;
                float radiusSq = playerRadius * playerRadius;
                float distSq = dot(delta, delta);
                if (distSq >= radiusSq)
                    return float3(0.0, 0.0, 0.0);

                float proximity = 1.0 - smoothstep(0.0, radiusSq, distSq);
                proximity *= proximity;
                float typeScale = instanceType < 0.5 ? 0.72 : (instanceType < 1.5 ? 1.08 : 0.52);
                float lift = lerp(0.01, 0.05, bendMask) * proximity * typeScale;
                float pushStrength = saturate(playerSpeed * 0.16) * playerPush * typeScale;
                return (SafeNormalize3(float3(delta.x, 0.0, delta.z)) + baseNormalWS * 0.04) *
                    (proximity * pushStrength * bendMask) + float3(0.0, -lift, 0.0);
            }

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
                    float radius = max(impactSphere.w, 0.05);
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

            float3 ResolveBillboardPositionWS(float3 originWS, float3 localPosition, float instanceHeight, float instanceWidth, float heightMask)
            {
                float3 cameraDelta = _WorldSpaceCameraPos - originWS;
                float3 cameraForwardXZ = SafeNormalize3(float3(cameraDelta.x, 0.0, cameraDelta.z));
                float3 billboardRight = SafeNormalize3(float3(cameraForwardXZ.z, 0.0, -cameraForwardXZ.x));
                float3 billboardUp = float3(0.0, 1.0, 0.0);
                float widthAtHeight = instanceWidth * lerp(1.0, 0.42, heightMask) * max(_HectonImpostorWidth, 0.25);
                float heightScale = instanceHeight * max(_HectonImpostorHeight, 0.25);

                return originWS +
                    billboardRight * (localPosition.x * widthAtHeight) +
                    billboardUp * (heightMask * heightScale);
            }

            float ResolveOrganicEntropyProgress(float encodedHeightScale, float encodedWidthScale, float timeValue)
            {
                if (encodedHeightScale >= 0.0)
                    return 0.0;

                float entropyDuration = encodedWidthScale < 0.0 ? 600.0 : 0.85;
                float entropyStartTime = encodedWidthScale < 0.0 ? abs(encodedWidthScale) : max(0.0, encodedWidthScale);
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
                float variationFlags = floor(max(runtimeFlags, 0.0));
                return step(0.5, fmod(variationFlags, 2.0));
            }

            float ResolveBiomeLayer(float runtimeFlags)
            {
                float variationFlags = floor(max(runtimeFlags, 0.0));
                return fmod(floor(variationFlags / 16.0), 4.0);
            }

            void ResolveRuntimeStateWeights(float runtimeState, out float agitatedWeight, out float dyingWeight)
            {
                agitatedWeight = saturate(1.0 - abs(runtimeState - 1.0));
                dyingWeight = saturate(1.0 - abs(runtimeState - 2.0));
            }

            float ResolveGrowth01(float encodedGrowth01)
            {
                return encodedGrowth01 > 0.0001 ? saturate(encodedGrowth01) : 1.0;
            }

            half ResolveBiolumPredatorDim(float3 positionWS)
            {
                half threatExposure = saturate(_HectonFloraPredatorThreatParams.x);
                half dimStrength = saturate(_HectonFloraPredatorThreatParams.z);
                half legacyDim = 1.0h;
                if (threatExposure > 0.0001h && dimStrength > 0.0001h)
                {
                    float4 predatorThreat = _HectonFloraPredatorThreatPositionRadius;
                    float dimRadius = max(max(predatorThreat.w, _HectonFloraPredatorThreatParams.y), 15.0);
                    half predatorProximity = 1.0h;
                    if (predatorThreat.w > 0.001)
                    {
                        float3 predatorDelta = positionWS - predatorThreat.xyz;
                        float dimRadiusSq = dimRadius * dimRadius;
                        predatorProximity = (half)(1.0 - smoothstep(0.0, dimRadiusSq, dot(predatorDelta, predatorDelta)));
                    }
                    legacyDim = saturate(1.0h - (threatExposure * predatorProximity * dimStrength));
                }

                int predatorCount = min(max(_PredatorAUPCount, 0), 32);
                if (predatorCount <= 0)
                    return legacyDim;

                half bufferDimStrength = saturate((half)_PredatorAUPParams.y);
                if (bufferDimStrength <= 0.0001h)
                    return legacyDim;

                float baseRadius = max(_PredatorAUPParams.x, 15.0);
                half predatorGate = 0.0h;
                [loop]
                for (int predatorIndex = 0; predatorIndex < predatorCount; predatorIndex++)
                {
                    float4 predatorAup = _PredatorAUPBuffer[predatorIndex];
                    float dimRadius = max(max(predatorAup.w, baseRadius), 15.0);
                    float3 predatorDelta = positionWS - predatorAup.xyz;
                    float dimRadiusSq = dimRadius * dimRadius;
                    float gate = 1.0 - smoothstep(dimRadiusSq * 0.3025, dimRadiusSq, dot(predatorDelta, predatorDelta));
                    predatorGate = max(predatorGate, (half)gate);
                }

                half bufferDim = saturate(1.0h - predatorGate * bufferDimStrength);
                return min(legacyDim, bufferDim);
            }

            half ResolveBiolumFlashBangBoost(float3 positionWS)
            {
                half duration = max(0.001h, (half)_BiolumFlashBangParams.y);
                half age = (half)(_Time.y - _BiolumFlashBangParams.x);
                if (age < 0.0h || age > duration)
                    return 1.0h;

                float radius = max(0.1, _BiolumFlashBangAUP.w);
                float3 flashDelta = positionWS - _BiolumFlashBangAUP.xyz;
                float radiusSq = radius * radius;
                half distanceGate = (half)(1.0 - smoothstep(radiusSq * 0.4225, radiusSq, dot(flashDelta, flashDelta)));
                half timeGate = 1.0h - smoothstep(duration * 0.45h, duration, age);
                half flashStrength = max(0.0h, (half)_BiolumFlashBangParams.z);
                return 1.0h + distanceGate * timeGate * flashStrength;
            }

            half ResolveSeasonalBloomEmissionScale()
            {
                half bloomWeight = saturate(_HectonFloraLifecycleParams.x);
                half bloomScale = max(1.0h, (half)_HectonFloraLifecycleParams.z);
                return lerp(1.0h, bloomScale, bloomWeight);
            }

            half ResolveCascadeEmissionScale(half cascadeSeed)
            {
                if (cascadeSeed <= -99999.0h)
                    return 0.0h;

                half cascadeTime = (half)_HectonFloraCascadeParams.x;
                half pulseDuration = max(0.05h, (half)_HectonFloraCascadeParams.y);
                half emissionBoost = max(0.0h, (half)_HectonFloraCascadeParams.z);
                half releaseDuration = max(pulseDuration, (half)_HectonFloraCascadeParams.w);
                half age = cascadeTime - cascadeSeed;
                if (age < 0.0h || age > releaseDuration)
                    return 0.0h;

                half rise = smoothstep(0.0h, pulseDuration * 0.18h, age);
                half crest = 1.0h - smoothstep(pulseDuration * 0.52h, pulseDuration, age);
                half tail = 1.0h - smoothstep(pulseDuration, releaseDuration, age);
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
                return lerp(1.0h, 0.12h, (half)smoothstep(2500.0, 12100.0, cameraDistanceSq));
            }

            half ResolveDistanceBiolumPixelGate(float cameraDistanceSq, float4 positionCS)
            {
                half pixelCoverage = lerp(1.0h, 0.22h, (half)smoothstep(2500.0, 8100.0, cameraDistanceSq));
                return step((half)InterleavedGradientNoise(positionCS.xy), pixelCoverage);
            }

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
                #if defined(HECTON_GPU_INDIRECT)
                    InitIndirectDrawArgs(0);
                    sourceInstanceIndex = _HectonVisibleInstanceIndices[GetIndirectInstanceID(sourceInstanceIndex)];
                #endif
                float4x4 instanceMatrix = _HectonInstanceMatrices[sourceInstanceIndex];
                HectonVegetationInstanceData instanceData = _HectonVegetationInstanceData[sourceInstanceIndex];
                float3 floatingOriginOffsetWS = _GlobalFloatingOffset.xyz;
                float3 stableAupSeed = TransformPoint(instanceMatrix, float3(0.0, 0.0, 0.0));
                float3 originWS = stableAupSeed + floatingOriginOffsetWS;
                float3 cameraDelta = originWS - _WorldSpaceCameraPos;
                float cameraDistanceSq = dot(cameraDelta, cameraDelta);
                float lodAlpha = ResolveLodAlpha(cameraDistanceSq, _HectonLodPassMode);

                float instanceType = clamp(round(instanceData.Type), 0.0, 2.0);
                float encodedHeightScale = instanceData.HeightScale;
                float encodedWidthScale = instanceData.WidthScale;
                float authoredSwaySpeed = max(instanceData.SwaySpeed, 0.05);
                float authoredBendAmplitude = max(instanceData.BendAmplitude, 0.0);
                float normalizedHealth = saturate(instanceData.HealthNormalized);
                float growth01 = ResolveGrowth01(instanceData.Reserved0);
                float timeValue = _Time.y * authoredSwaySpeed;
                float entropyProgress = ResolveOrganicEntropyProgress(encodedHeightScale, encodedWidthScale, timeValue);
                float parasiteMask = ResolveParasiteMask(instanceData.RuntimeFlags);
                float biomeLayer = ResolveBiomeLayer(instanceData.RuntimeFlags);
                float geneticTraits = DecodeGeneticTraits(instanceData.RuntimeFlags);
                float wiltSuppression = lerp(1.0, 0.18, entropyProgress);
                float heightScale = saturate(abs(encodedHeightScale));
                float widthScale = ResolveOrganicWidthScale(encodedWidthScale, entropyProgress);
                float heightMask = saturate(input.uv.y);
                float bendMask = heightMask * heightMask * authoredBendAmplitude;
                float curvatureMask = saturate(input.color.a);
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
                float growthHeightScale = growth01;

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
                float3 flowSynchronyOffset = ResolveFlowSynchronyOffset(basePositionWS, bendMask, instanceType, instanceNoise);

                if (_HectonLodPassMode < 0.5)
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
                        float2 grassWind = SafeNormalize2(float2(
                            FastSinApprox(phase),
                            FastCosApprox(phase * 1.37 + heightMask * _GrassWindFrequency)));
                        animatedPositionWS += wakeTrailOffset;
                        animatedPositionWS += submarineWashOffset;
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
                        float2 noiseFlow = float2(
                            FastSinApprox(phase),
                            FastCosApprox(phase * 0.71 + heightMask * _KelpCurrentFrequency));
                        float2 kelpFlow = ResolvePlanarOceanFlowDirection(currentVector + noiseFlow * currentStrength);
                        float kelpAmplitude = _KelpCurrentAmplitude * lerp(0.45, 1.0, authoredBendAmplitude);
                        #if defined(_QUALITY_MX350)
                        kelpAmplitude *= 0.8;
                        #endif
                        animatedPositionWS += wakeTrailOffset * 1.1;
                        animatedPositionWS += submarineWashOffset * 1.15;
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
                        float waveLift = FastSinApprox(phase) * (_SargassumWaveAmplitude * lerp(0.35, 1.0, authoredBendAmplitude));
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
                        renderOriginWS.y = resolvedWaterLevel + driftOffsetWS.y + FastSinApprox(farPhase) * (_SargassumWaveAmplitude * 0.9);
                    }

                    animatedPositionWS = ResolveBillboardPositionWS(renderOriginWS, localPosition, instanceHeight, instanceWidth, heightMask);

                    float2 farFlow = ResolvePlanarOceanFlowDirection(currentVector + float2(FastSinApprox(farPhase), FastCosApprox(farPhase * 0.83)) * currentStrength);
                    float farStateSwayScale = lerp(1.0, 1.18, agitatedWeight) * lerp(1.0, 0.58, dyingWeight);
                    float farSwayStrength = (instanceType < 0.5 ? _GrassWindAmplitude * 0.55 : _KelpCurrentAmplitude * 0.42) * wiltSuppression * farStateSwayScale * lerp(0.35, 1.0, authoredBendAmplitude) * lerp(0.35, 1.0, normalizedHealth);
                    animatedPositionWS += wakeTrailOffset * 0.8;
                    animatedPositionWS += submarineWashOffset * 0.75;
                    animatedPositionWS += flowSynchronyOffset * 0.85;
                    animatedPositionWS.xz += farFlow * (farSwayStrength * bendMask * lodAlpha);
                    animatedPositionWS += farCurrentOffset * 0.65;
                }

                float3 interactionOffset = ResolveInteractionOffset(animatedPositionWS, baseNormalWS, bendMask, cameraDistanceSq);
                float3 playerBendOffset = ResolvePlayerBendOffset(animatedPositionWS, baseNormalWS, bendMask, instanceType);
                float3 impactOffset = ResolveImpactOffset(animatedPositionWS, baseNormalWS, bendMask);
                float interactionTypeScale = instanceType < 0.5 ? 0.7 : (instanceType < 1.5 ? 1.15 : 0.85);
                animatedPositionWS += impactOffset * 0.95;
                animatedPositionWS += interactionOffset * (_InteractionPushStrength * interactionTypeScale);
                animatedPositionWS += playerBendOffset * (_InteractionPushStrength * 1.1);
                animatedPositionWS.xz += currentDirection * (agitatedWeight * bendMask * instanceHeight * 0.035);
                animatedPositionWS = lerp(animatedPositionWS, renderOriginWS, dyingWeight * bendMask * 0.18);
                animatedPositionWS.y -= dyingWeight * instanceHeight * lerp(0.03, 0.16, heightMask);

                float seasonalDecayWeight = saturate(_HectonFloraLifecycleParams.y) * saturate(_HectonFloraLifecycleParams.w);
                if (seasonalDecayWeight > 0.0001)
                {
                    float seasonalWiltWeight = seasonalDecayWeight *
                        saturate(lerp(0.18, 1.0, heightMask) * lerp(0.35, 1.0, bendMask));
                    animatedPositionWS = lerp(animatedPositionWS, renderOriginWS, seasonalWiltWeight * 0.24);
                    animatedPositionWS.y -= instanceHeight * seasonalWiltWeight * lerp(0.04, 0.19, heightMask);
                    animatedPositionWS.xz += currentDirection * (-seasonalWiltWeight * instanceHeight * 0.018 * heightMask);
                }

                float snappedFlag = 0.0;
                if (_HectonFloraSnapFlagsEnabled > 0.5)
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

                if (_HectonLodPassMode >= 0.5)
                {
                    float3 cameraDelta = _WorldSpaceCameraPos - animatedPositionWS;
                    float3 viewFacingNormal = SafeNormalize3(float3(cameraDelta.x, 0.3, cameraDelta.z));
                    normalWS = SafeNormalize3(lerp(normalWS, viewFacingNormal, 0.78));
                }

                float kelpDepthBelowWater = max(0.0, resolvedWaterLevel - renderOriginWS.y);
                float kelpDepthFade = saturate((kelpDepthBelowWater - HECTON_KELP_FADE_START_DEPTH) / (HECTON_KELP_FADE_END_DEPTH - HECTON_KELP_FADE_START_DEPTH));

                output.positionWS = animatedPositionWS;
                output.originWS = renderOriginWS;
                output.normalWS = normalWS;
                output.positionCS = TransformWorldToHClip(animatedPositionWS);
                output.heightMask = heightMask;
                output.lodAlpha = lodAlpha;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.instanceType = instanceType;
                output.kelpDepthFade = kelpDepthFade;
                output.edgeMask = saturate(abs(input.uv.x * 2.0 - 1.0));
                output.curvatureMask = curvatureMask;
                output.entropyProgress = entropyProgress;
                output.parasiteMask = parasiteMask;
                output.runtimeState = instanceData.RuntimeState;
                output.pulseFrequency = max(0.01, instanceData.PulseFrequency);
                output.biolumColor = half4(instanceData.BioluminescenceColor.rgb, instanceData.BioluminescenceColor.a);
                output.flowMagnitude = flowMagnitude;
                output.biomeLayer = biomeLayer;
                output.cascadeSeed = _HectonFloraPhaseSeeds[sourceInstanceIndex];
                output.growth01 = growth01;
                output.health01 = normalizedHealth;
                output.geneticTraits = geneticTraits;
                output.spatialPulseOffset = (half)ResolveSpatialHashPulseOffset(stableAupSeed);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half cutMask = ResolveVegetationCutMask(input.instanceType, input.positionWS);
                half coverageVisibility = ResolveVegetationVisibilityGate(0.08h - cutMask, 0.0h, 0.025h);

                half porousCoverageMask = input.instanceType > 1.5h ? ResolveSargassumPorousCoverage(input.positionWS, input.heightMask) : 1.0h;
                if (input.instanceType > 1.5h)
                    coverageVisibility *= ResolveVegetationVisibilityGate(porousCoverageMask, 0.16h, 0.08h);

                half entropyCoverage = 1.0h - input.entropyProgress * saturate(lerp(0.28h, 1.0h, input.heightMask) * lerp(0.35h, 1.0h, input.curvatureMask));
                half coverage = saturate(_Opacity) * porousCoverageMask * entropyCoverage;
                coverageVisibility *= (half)step(InterleavedGradientNoise(input.positionCS.xy), coverage);
                coverageVisibility *= ResolveCullFadeCoverage(input.positionWS, input.positionCS);

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
                gradientColor = lerp(_SeedlingColor.rgb, gradientColor, input.growth01);
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
                clip(coverageVisibility - 0.01h);

                half necrosisNoise = (half)ValueNoise3D(input.positionWS * 5.6 + float3(0.0, input.cascadeSeed * 7.0h, _Time.y * 0.025));
                half3 necrosisColor = lerp(half3(0.025h, 0.018h, 0.012h), half3(0.20h, 0.10h, 0.035h), necrosisNoise);
                gradientColor = lerp(gradientColor, necrosisColor, necrosisMask);
                half3 parasiteGlowTint = input.instanceType < 1.5h
                    ? half3(0.18h, 0.95h, 0.72h)
                    : half3(0.14h, 0.78h, 1.00h);
                gradientColor = lerp(gradientColor, gradientColor + parasiteGlowTint * 0.38h, input.parasiteMask * saturate(1.0h - input.entropyProgress * 0.65h));
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

                half3 ambient = lerp(_HectonVegetationAmbientColor.rgb, SampleSH(normalWS), 0.55h) * (_AmbientStrength * ambientVisibility);
                half3 diffuse = gradientColor * ambient;
                diffuse += gradientColor * (mainLight.color * wrapDiffuse * sunVisibility);
                half3 transmission = _TranslucencyColor.rgb * backLight * input.heightMask * _TranslucencyStrength * sunVisibility;
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
                half pulsePhase = (_Time.y * max(0.01h, input.pulseFrequency) * 6.28318h) + input.spatialPulseOffset + input.heightMask * 3.1h;
                half pulseStrength = lerp(0.68h, 1.34h, 0.5h + 0.5h * (half)FastSinApprox(pulsePhase));
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
                half decaySeasonPulse = 0.5h + 0.5h * (half)FastCosApprox((_SeasonCycle - 0.75h) * 6.28318h);
                half decaySeasonWeight = saturate(_HectonFloraLifecycleParams.y) * lerp(0.55h, 1.0h, decaySeasonPulse);
                half seasonalDecaySuppression = lerp(1.0h, 0.78h, saturate(decaySeasonWeight * _HectonFloraLifecycleParams.w));
                half cascadeEmissionScale = 1.0h + ResolveCascadeEmissionScale(input.cascadeSeed);
                half flashBangScale = ResolveBiolumFlashBangBoost(input.positionWS);
                half flashlightPhotophobia = HectonCoreLitResolveFlashlightPhotophobia(input.positionWS);
                half emitsLightTrait = HasGeneticTrait(input.geneticTraits, 4.0h);
                half geneticEmissionGate = lerp(1.0h, emitsLightTrait, traitBytePresent);
                half3 biolumEmission = input.biolumColor.rgb *
                    (input.biolumColor.a * pulseStrength * stateEmissionScale * predatorDim * parasiteBiolumBoost * biolumVisibility * flowReactiveBoost * distanceBiolumDimming * distanceBiolumPixelGate * seasonalBloomScale * seasonalDecaySuppression * cascadeEmissionScale * flashBangScale * flashlightPhotophobia * geneticEmissionGate);
                biolumEmission *= saturate(input.growth01) * saturate(input.health01);
                half3 decayTint = lerp(half3(1.0h, 1.0h, 1.0h), half3(0.92h, 0.84h, 0.68h), decaySeasonWeight * 0.22h);
                finalColor *= decayTint;
                finalColor += biolumEmission;

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
#if defined(_QUALITY_MX350)
                half ditherOffset = (half)((ResolveVegetationBlueNoise(input.positionCS) - 0.5) * (1.0 / 255.0));
                finalColor = max(finalColor + ditherOffset, half3(0.0015h, 0.0023h, 0.0031h));
#endif
                coverageVisibility = saturate(coverageVisibility);
                finalColor = lerp(half3(0.0015h, 0.0023h, 0.0031h), finalColor, coverageVisibility);
                return half4(finalColor, coverageVisibility);
            }
            ENDHLSL
        }
    }
}
