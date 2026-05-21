#ifndef HECTON8_UBER_NOIR_INCLUDED
#define HECTON8_UBER_NOIR_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#if defined(H8_UBERNOIR_SHADOW_CASTER_PASS)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#endif
#include "Hecton_WaterExtinction.hlsl"
#include "Hecton_CustomLightProbeGrid.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
#include "Post/Hecton_SnellRefractionCore.hlsl"

#if defined(H8_UBERNOIR_MOTION_VECTOR_PASS)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"
#endif

#define H8_UBER_NOIR_PI 3.14159265359
#define H8_UBER_NOIR_EPS 0.0001
#define H8_UBER_NOIR_POM_STEPS 16
#define H8_UBER_NOIR_MAX_HULL_DENTS 16
#define H8_UBER_NOIR_MAX_GPU_HULL_DENTS 512
#define H8_UBER_NOIR_MAX_DEFORMATION_STATES 256
#define H8_UBER_NOIR_MAX_GLOBAL_WAKES 16
#define H8_UBER_NOIR_MATERIAL_CAPACITY 8192
#define H8_UBER_NOIR_AGING_CAPACITY 4096
#define H8_UBER_NOIR_DEGRADATION_INDEX_INVALID 0xFFFFFFFFu
#define H8_UBER_NOIR_FLAG_TEXTURE_ARRAYS 1u
#define H8_UBER_NOIR_FLAG_DEBUG_HEATMAP 2u

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_MaskMap);
SAMPLER(sampler_MaskMap);

TEXTURE2D(_BumpMap);
SAMPLER(sampler_BumpMap);
TEXTURE2D(_RustDetailMap);
SAMPLER(sampler_RustDetailMap);
TEXTURE2D(_BlueNoiseTex);
SAMPLER(sampler_BlueNoiseTex);

TEXTURE2D_ARRAY(_H8UberNoirAlbedoArray);
SAMPLER(sampler_H8UberNoirAlbedoArray);
TEXTURE2D_ARRAY(_H8UberNoirNormalArray);
SAMPLER(sampler_H8UberNoirNormalArray);
TEXTURE2D_ARRAY(_H8UberNoirMaskArray);
SAMPLER(sampler_H8UberNoirMaskArray);

struct H8UberNoirInstanceData
{
    float4x4 ObjectToWorld;
    float4x4 WorldToObject;
    float4 SeedFadeFlags; // x=seed, y=fade01, z=feature flags, w=degradationIndexPlusOne
};

struct H8UberNoirMaterialStateDTO
{
    float WearAge;
    float SaltAccumulation;
    float BioGrowthMask;
    uint TextureSetHash;
    float PowerLevel;
    float Depth01;
    float MossLayer01;
    uint Flags;
};

struct H8VisualAgingParamsDTO
{
    float4 RustAndCorrosion;
    float4 SaltAndBiomass;
    float4 StressAndMicroFractures;
    float4 DepthAndPressure;
};

struct H8InstanceDegradationDTO
{
    uint InstanceID;
    float RustAmount;
    float ScorchAmount;
    float BioFouling;
    float StructuralStress;
    uint3 Padding;
};

#if defined(H8_UBERNOIR_USE_INSTANCE_BUFFER)
StructuredBuffer<H8UberNoirInstanceData> _H8UberNoirInstanceData;
#endif

StructuredBuffer<H8UberNoirMaterialStateDTO> _H8UberNoirMaterialStates;
StructuredBuffer<H8VisualAgingParamsDTO> _GlobalBaseAgingParams;
float4 _GlobalBaseAgingRuntime; // x=active count, y=buffer enabled, z=GlobalQualityWeight, w=flags
StructuredBuffer<H8InstanceDegradationDTO> _GlobalUberNoirDegradation;
float4 _GlobalUberNoirDegradationRuntime; // x=active count, y=buffer enabled, z=GlobalQualityWeight, w=flags

struct H8BulkheadStateDTO
{
    uint EdgeHashID;
    float ClosureProgress;
    uint AssociatedLock;
    uint SiblingNodeHash;
    uint Flags;
    uint Reserved0;
    uint Reserved1;
    uint Reserved2;
};
StructuredBuffer<H8BulkheadStateDTO> _GlobalBulkheadStates;
float4 _GlobalBulkheadParams; // x=count, y=enabled, z=frame, w=GlobalQualityWeight

CBUFFER_START(H8UberNoirMaterialGlobals)
    float4 _H8UberNoirSubsurfaceColor;
    float4 _H8UberNoirCausticSpeed; // x=scroll speed, y=intensity, z=salt line depth, w=GlobalQualityWeight
    float _H8UberNoirGlobalWearMultiplier;
    uint _H8UberNoirDebugMode;
    uint _H8UberNoirTextureSetCount;
    uint _H8UberNoirMaterialFlags;
CBUFFER_END

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _MaskMap_ST;
    float4 _RustDetailMap_ST;
    float4 _BaseColor;
    float4 _EmissionColor;
    float4 _RustTint;
    float4 _RustPitTint;
    float4 _BiolumLowColor;
    float4 _BiolumHighColor;
    float4 _NoirAbyssFloorColor;
    float4 _NoirFogColor;
    float4 _UberNoirFeatureFlags;    // x=POM, y=reserved, z=bending, w=dither transparency
    float4 _UberNoirInstanceParams;  // x=buffer offset, y=buffer count, z=use buffer, w=seed bias
    float _UberNoirInstanceCapacity; // total structured-buffer rows, zero disables external reads
    float4 _UberNoirParallaxParams;  // x=scale, y=min view z, z=height bias, w=reserved
    float4 _UberNoirRustParams;      // x=strength, y=POM threshold, z=normal strength, w=wet smoothness
    float4 _UberNoirBendParams;      // x=local strength, y=grid scale, z=panel bow, w=low scar
    float4 _UberNoirBiolumParams;    // x=intensity, y=spectral shift, z=pulse sharpness, w=seed scale
    float4 _UberNoirDitherParams;    // x=cutoff, y=fog alpha, z=temporal strength, w=alpha scale
    float4 _UberNoirLightingParams;  // x=specular, y=roughness floor, z=ambient, w=emission scale
    float4 _UberNoirRefractionParams;// x=strength, y=water density, z=blend, w=chromatic
    float4 _UberNoirIorLut;          // x=air, y=water, z=dense water, w=glass
    float _HectonHandRadiationMask;
    float _Metallic;
    float _Smoothness;
    float _OcclusionStrength;
    float _BumpScale;
    float _Cutoff;
    float _NoirFogAlpha;
    float _UberNoirPadding0;
    float _UberNoirPadding1;
CBUFFER_END

// Frame/runtime globals. These are uploaded once by system owners, not per-material mutation.
#ifndef HECTON_CORE_LIT_INCLUDED
float4 _TotalUniverseOffset;
float4 _BiolumMasterPhase;
float4 _HectonSubmarineCrushCenterRadius;
float4 _HectonSubmarineCrushDepthParams;
float4 _HectonHabitatStressCenterRadius;
float4 _HectonHabitatStressParams;
float4 _HectonUberNoirRuntimeParams;
float4 _HectonPowerBrownoutParams; // x=supply, y=severity, z=phase seconds, w=GlobalQualityWeight
float4 _HectonRespawnDearLieParams; // x=fade, y=chromatic, z=grain, w=GlobalQualityWeight
float _HectonDeathFadeIntensity;
float4 _HectonHullDentParams;
float4 _HectonHullDents[H8_UBER_NOIR_MAX_HULL_DENTS];
struct H8HullDentDTO
{
    float3 Position;
    float Radius;
    float3 Normal;
    float Depth;
};
StructuredBuffer<H8HullDentDTO> _HectonHullDentDTOBuffer;
float4 _HectonHullDentDTOParams; // x=active count, y=dto enabled, z=max depth scar, w=profile metadata
struct H8DeformationStateDTO
{
    float3 LocalPosition;
    float Radius;
    float3 Normal;
    float Depth;
    float Age;
    float Severity;
    uint DamageTypeHash;
    uint SourceHash;
    uint Frame;
    uint Flags;
    uint Reserved0;
    uint Reserved1;
};
StructuredBuffer<H8DeformationStateDTO> _HectonDeformationStateBuffer;
float4 _HectonDeformationStateParams; // x=active count, y=max shader dents, z=max depth scar, w=GlobalQualityWeight
float4 _HectonMaterialDecayRuntime;
float4 _HectonPlayerBloodSplatter;
float _HectonActiveShaderFeatureMask;
float _HectonEquipmentRust01;
float _H8GlobalQualityWeight;
float _HectonHandRadiationDose;
float _HectonHandRadiationMutation01;
float4 _HectonHandRadiationTint;
#endif
float4 _GlobalWakeBuffer[H8_UBER_NOIR_MAX_GLOBAL_WAKES];
float4 _GlobalWakeVectors[H8_UBER_NOIR_MAX_GLOBAL_WAKES];
float4 _GlobalWakeParams; // x=slot limit, y=budget pressure, z=active count, w=stress
struct H8CavitationShockwaveDTO
{
    float4 CenterRadius;
    float4 IntensityAgeQualityFlags;
    float4 CurlPhase;
    float4 Reserved;
};
StructuredBuffer<H8CavitationShockwaveDTO> _H8CavitationShockwaves;
int _H8CavitationShockwaveCount;
float4 _H8CavitationShockwaveParams; // x=quality, y=intensity scale, z=uploaded count, w=frame

#if defined(H8_UBERNOIR_SHADOW_CASTER_PASS)
float3 _LightDirection;
float3 _LightPosition;
#endif

struct H8UberNoirAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    uint instanceID : SV_InstanceID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct H8UberNoirVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    half4 tangentWS : TEXCOORD2;
    half3 viewDirWS : TEXCOORD3;
    float4 uvPack : TEXCOORD4;
    half fogFactor : TEXCOORD5;
    half instanceSeed : TEXCOORD6;
    half instanceFade : TEXCOORD7;
    float4 uvAux : TEXCOORD8; // xy=base UV scale, zw=mask UV
    half3 extinctionColor : TEXCOORD9;
    half dentScar : TEXCOORD10;
    nointerpolation uint materialIndex : TEXCOORD11;
    half3 deformationNormalWS : TEXCOORD12;
    nointerpolation uint degradationIndex : TEXCOORD13;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

struct H8UberNoirSurface
{
    half3 albedo;
    half3 normalWS;
    half3 emission;
    half metallic;
    half smoothness;
    half roughness;
    half occlusion;
    half alpha;
    half rustMask;
    half4 orm;
    half sssMask;
    half anisotropy;
    half powerLevel;
};

struct H8UberNoirWearVitality
{
    half RustPitMask;
    half MossVeinMask;
    half SaltCrystalMask;
    half WetEdgeMask;
    half NormalWeight;
};

float H8UberNoirSafeRcp(float value)
{
    float safeMagnitude = max(abs(value), H8_UBER_NOIR_EPS);
    float signValue = lerp(-1.0, 1.0, step(0.0, value));
    return signValue * rcp(safeMagnitude);
}

float H8UberNoirSafeRsqrt(float value)
{
    return rsqrt(max(value, H8_UBER_NOIR_EPS));
}

float H8UberNoirSafePow(float value, float exponent)
{
    float safeValue = max(value, H8_UBER_NOIR_EPS);
    float safeExponent = max(exponent, H8_UBER_NOIR_EPS);
    return pow(safeValue, safeExponent);
}

float H8UberNoirSafePow01(float value, float exponent)
{
    return saturate(H8UberNoirSafePow(saturate(value), exponent));
}

float3 H8UberNoirFinite3(float3 value, float3 fallbackValue)
{
    return all(isfinite(value)) ? value : fallbackValue;
}

float3 H8UberNoirSafeNormalize(float3 value, float3 fallbackValue)
{
    float finiteMask = all(isfinite(value)) ? 1.0 : 0.0;
    float3 safeValue = finiteMask > 0.5 ? value : fallbackValue;
    float lenSqRaw = dot(safeValue, safeValue);
    float validMask = finiteMask * step(H8_UBER_NOIR_EPS, lenSqRaw);
    float lenSq = max(lenSqRaw, H8_UBER_NOIR_EPS);

    float3 absValue = abs(safeValue);
    float3 axisX = float3(safeValue.x < 0.0 ? -1.0 : 1.0, 0.0, 0.0);
    float3 axisY = float3(0.0, safeValue.y < 0.0 ? -1.0 : 1.0, 0.0);
    float3 axisZ = float3(0.0, 0.0, safeValue.z < 0.0 ? -1.0 : 1.0);
    float3 axisApprox = absValue.x >= absValue.y && absValue.x >= absValue.z
        ? axisX
        : (absValue.y >= absValue.z ? axisY : axisZ);
    float3 precise = safeValue * H8UberNoirSafeRsqrt(lenSq);
    float materialWeight = isfinite(_H8UberNoirCausticSpeed.w) ? saturate(_H8UberNoirCausticSpeed.w) : 0.0;
    float globalWeight = isfinite(_H8GlobalQualityWeight) ? saturate(_H8GlobalQualityWeight) : 0.0;
    float q = saturate(max(globalWeight, materialWeight));
    float axisWeight = (1.0 - q) * (1.0 - q) * (3.0 - 2.0 * (1.0 - q));
    float3 normalizedValue = H8UberNoirFinite3(lerp(precise, axisApprox, axisWeight * 0.65), precise);
    return lerp(fallbackValue, normalizedValue, validMask);
}

half3 H8UberNoirSafeNormalizeHalf(half3 value, half3 fallbackValue)
{
    half finiteMask = all(isfinite(value)) ? 1.0h : 0.0h;
    half3 safeValue = finiteMask > 0.5h ? value : fallbackValue;
    half lenSqRaw = dot(safeValue, safeValue);
    half validMask = finiteMask * (half)step((half)H8_UBER_NOIR_EPS, lenSqRaw);
    half lenSq = max(lenSqRaw, (half)H8_UBER_NOIR_EPS);

    half3 absValue = abs(safeValue);
    half3 axisX = half3(safeValue.x < 0.0h ? -1.0h : 1.0h, 0.0h, 0.0h);
    half3 axisY = half3(0.0h, safeValue.y < 0.0h ? -1.0h : 1.0h, 0.0h);
    half3 axisZ = half3(0.0h, 0.0h, safeValue.z < 0.0h ? -1.0h : 1.0h);
    half3 axisApprox = absValue.x >= absValue.y && absValue.x >= absValue.z
        ? axisX
        : (absValue.y >= absValue.z ? axisY : axisZ);
    half3 precise = safeValue * (half)H8UberNoirSafeRsqrt((float)lenSq);
    half materialWeight = isfinite(_H8UberNoirCausticSpeed.w) ? (half)saturate(_H8UberNoirCausticSpeed.w) : (half)0.0;
    half globalWeight = isfinite(_H8GlobalQualityWeight) ? (half)saturate(_H8GlobalQualityWeight) : (half)0.0;
    half q = (half)saturate(max((float)globalWeight, (float)materialWeight));
    half inverseQ = (half)1.0 - q;
    half axisWeight = inverseQ * inverseQ * ((half)3.0 - (half)2.0 * inverseQ);
    half3 normalizedValue = lerp(precise, axisApprox, axisWeight * (half)0.65);
    return lerp(fallbackValue, normalizedValue, validMask);
}

float H8UberNoirTriangle01(float value)
{
    return 1.0 - abs(frac(value) * 2.0 - 1.0);
}

float H8UberNoirSmoothRange01(float edge0, float edge1, float value)
{
    float range = max(edge1 - edge0, H8_UBER_NOIR_EPS);
    float t = saturate((value - edge0) * H8UberNoirSafeRcp(range));
    return t * t * (3.0 - 2.0 * t);
}

float H8UberNoirHighCostAllowed()
{
    float globalAllow = isfinite(_HectonUberNoirRuntimeParams.y) ? saturate(_HectonUberNoirRuntimeParams.y) : 0.0;
    float stress01 = isfinite(_HectonUberNoirRuntimeParams.x) ? saturate(_HectonUberNoirRuntimeParams.x) : 0.0;
    float stressGate = 1.0 - H8UberNoirSmoothRange01(0.72, 0.88, stress01);
    return globalAllow * stressGate;
}

float H8UberNoirVisualOverkill01()
{
    float overkill = isfinite(_HectonUberNoirRuntimeParams.w) ? saturate(_HectonUberNoirRuntimeParams.w) : 0.0;
    return overkill * H8UberNoirHighCostAllowed();
}

float H8UberNoirGlobalQualityWeight()
{
    float materialWeight = isfinite(_H8UberNoirCausticSpeed.w) ? saturate(_H8UberNoirCausticSpeed.w) : 0.0;
    float globalWeight = isfinite(_H8GlobalQualityWeight) ? saturate(_H8GlobalQualityWeight) : 0.0;
    return saturate(max(globalWeight, materialWeight));
}

float3 H8UberNoirApplyBulkheadClosureOS(float3 positionOS, inout float3 normalOS, uint resolvedInstanceID)
{
    float enabled = isfinite(_GlobalBulkheadParams.y) ? saturate(_GlobalBulkheadParams.y) : 0.0;
    float rawCount = isfinite(_GlobalBulkheadParams.x) ? _GlobalBulkheadParams.x : 0.0;
    uint count = (uint)clamp(rawCount, 0.0, (float)H8_UBER_NOIR_MAX_DEFORMATION_STATES);
    if (enabled <= 0.0 || count == 0u)
        return positionOS;

    float rawInstanceOffset = isfinite(_UberNoirInstanceParams.x) ? _UberNoirInstanceParams.x : 0.0;
    uint instanceOffset = (uint)clamp(rawInstanceOffset, 0.0, (float)H8_UBER_NOIR_MAX_DEFORMATION_STATES);
    uint index = (resolvedInstanceID + instanceOffset) % count;
    H8BulkheadStateDTO state = _GlobalBulkheadStates[index];
    if ((state.Flags & 1u) == 0u)
        return positionOS;

    float closure = isfinite(state.ClosureProgress) ? saturate(state.ClosureProgress) : 0.0;
    if (closure <= 0.0001)
        return positionOS;

    float qGlobal = H8UberNoirGlobalQualityWeight();
    float qUpload = isfinite(_GlobalBulkheadParams.w) ? saturate(_GlobalBulkheadParams.w) : qGlobal;
    float q = saturate(lerp(qGlobal, qUpload, 0.5));
    float eased = closure * closure * (3.0 - 2.0 * closure);
    float3 safePosition = H8UberNoirFinite3(positionOS, float3(0.0, 0.0, 0.0));
    float3 safeNormal = H8UberNoirSafeNormalize(normalOS, float3(0.0, 1.0, 0.0));
    float side = safePosition.x < 0.0 ? -1.0 : 1.0;
    float timePhase = _GlobalBulkheadParams.z * 0.017 + safePosition.y * 0.31 + safePosition.z * 0.19;
    float ripple = (H8UberNoirTriangle01(timePhase) - 0.5) * 2.0;
    float visualOverkill = lerp(0.35, 1.0, q);
    safePosition.y -= eased * lerp(1.65, 2.45, q);
    safePosition.x += side * eased * lerp(0.015, 0.12, q) * (0.7 + 0.3 * ripple);
    safePosition.z += safeNormal.z * eased * lerp(0.005, 0.045, q * q);
    normalOS = H8UberNoirSafeNormalize(safeNormal + float3(side * eased * 0.08 * visualOverkill, eased * 0.12, ripple * 0.025 * q), safeNormal);
    return H8UberNoirFinite3(safePosition, positionOS);
}

float H8UberNoirFeatureScalar(float value)
{
    return isfinite(value) ? saturate(value) : 0.0;
}

float4 H8UberNoirFiniteSaturate4(float4 value, float4 fallback)
{
    return all(isfinite(value)) ? saturate(value) : fallback;
}

float H8UberNoirFiniteSaturate(float value, float fallback)
{
    return isfinite(value) ? saturate(value) : fallback;
}

float3 H8UberNoirMaterialStablePosition(float3 positionWS)
{
    return H8UberNoirFinite3(positionWS, float3(0.0, 0.0, 0.0));
}

float3 H8UberNoirVisualAgingStablePosition(float4 uvPack, float4 uvAux, half instanceSeed, H8VisualAgingParamsDTO aging)
{
    float3 uvSeed = float3(uvPack.xy * 37.0, uvAux.z * 19.0 + (float)instanceSeed * 11.0);
    uvSeed = H8UberNoirFinite3(uvSeed, float3(0.0, 0.0, 0.0));
    float3 localAupSeed = H8UberNoirFinite3(aging.DepthAndPressure.xyz, float3(0.0, 0.0, 0.0)) * 0.001;
    return H8UberNoirFinite3(uvSeed + localAupSeed, uvSeed);
}

half H8UberNoirResolveBrownoutPower(float3 positionWS, float instanceSeed, half materialPower)
{
    float supply01 = isfinite(_HectonPowerBrownoutParams.x) ? saturate(_HectonPowerBrownoutParams.x) : 1.0;
    float severity01 = isfinite(_HectonPowerBrownoutParams.y) ? saturate(_HectonPowerBrownoutParams.y) : 0.0;
    float quality = saturate(max(H8UberNoirGlobalQualityWeight(), isfinite(_HectonPowerBrownoutParams.w) ? _HectonPowerBrownoutParams.w : 0.0));
    float phase = _Time.y + (isfinite(_HectonPowerBrownoutParams.z) ? _HectonPowerBrownoutParams.z : 0.0);
    float3 stablePosition = H8UberNoirMaterialStablePosition(positionWS);
    float coarsePhase = phase * lerp(3.0, 9.5, quality) + dot(stablePosition.xz, float2(0.019, -0.031)) + instanceSeed * 0.37;
    float flicker01 = H8UberNoirTriangle01(coarsePhase);
    float finePhase = phase * lerp(11.0, 23.0, quality) + dot(stablePosition.xy, float2(-0.047, 0.029)) + instanceSeed * 1.73;
    float flickerDetail = H8UberNoirVisualOverkill01() * H8UberNoirSmoothRange01(0.28, 0.72, quality);
    flicker01 = lerp(flicker01, flicker01 * 0.72 + H8UberNoirTriangle01(finePhase) * 0.28, flickerDetail);
    float brownoutFloor = lerp(0.52, 0.24, quality);
    float flickerGain = lerp(1.0, lerp(brownoutFloor, 1.08, flicker01), severity01);
    float supplyGate = lerp(1.0, supply01, severity01);
    return (half)saturate((float)materialPower * supplyGate * flickerGain);
}

H8UberNoirMaterialStateDTO H8UberNoirDefaultMaterialState()
{
    H8UberNoirMaterialStateDTO state;
    state.WearAge = saturate(max(
        H8UberNoirFiniteSaturate(_HectonEquipmentRust01, 0.0),
        H8UberNoirFiniteSaturate(_HectonMaterialDecayRuntime.x, 0.0)));
    state.SaltAccumulation = state.WearAge * 0.35;
    state.BioGrowthMask = H8UberNoirFiniteSaturate(_BiolumMasterPhase.z, 0.0);
    state.TextureSetHash = 0u;
    state.PowerLevel = 1.0;
    state.Depth01 = 0.0;
    state.MossLayer01 = 0.0;
    state.Flags = 0u;
    return state;
}

H8UberNoirMaterialStateDTO H8UberNoirLoadMaterialState(uint materialIndex)
{
    H8UberNoirMaterialStateDTO state = H8UberNoirDefaultMaterialState();
    uint runtimeActive = (_H8UberNoirMaterialFlags != 0u) ? 1u : 0u;
    [branch]
    if (runtimeActive != 0u)
    {
        state = _H8UberNoirMaterialStates[min(materialIndex, (uint)(H8_UBER_NOIR_MATERIAL_CAPACITY - 1))];
        state.WearAge = H8UberNoirFiniteSaturate(state.WearAge, 0.0);
        state.SaltAccumulation = H8UberNoirFiniteSaturate(state.SaltAccumulation, 0.0);
        state.BioGrowthMask = H8UberNoirFiniteSaturate(state.BioGrowthMask, 0.0);
        state.PowerLevel = H8UberNoirFiniteSaturate(state.PowerLevel, 1.0);
        state.Depth01 = H8UberNoirFiniteSaturate(state.Depth01, 0.0);
        state.MossLayer01 = H8UberNoirFiniteSaturate(state.MossLayer01, 0.0);
    }
    return state;
}

H8VisualAgingParamsDTO H8UberNoirDefaultVisualAging()
{
    H8VisualAgingParamsDTO aging;
    float rust = max(
        H8UberNoirFiniteSaturate(_HectonEquipmentRust01, 0.0),
        H8UberNoirFiniteSaturate(_HectonMaterialDecayRuntime.x, 0.0));
    aging.RustAndCorrosion = float4(rust, rust * 0.35, rust * 0.22, 0.0);
    aging.SaltAndBiomass = float4(rust * 0.18, H8UberNoirFiniteSaturate(_BiolumMasterPhase.z, 0.0) * 0.12, 0.0, 0.0);
    aging.StressAndMicroFractures = float4(H8UberNoirFiniteSaturate(_HectonUberNoirRuntimeParams.x, 0.0), 0.0, 0.0, H8UberNoirGlobalQualityWeight());
    aging.DepthAndPressure = float4(0.0, 0.0, 0.0, 0.0);
    return aging;
}

H8InstanceDegradationDTO H8UberNoirDefaultInstanceDegradation()
{
    H8InstanceDegradationDTO degradation;
    float rust = max(
        H8UberNoirFiniteSaturate(_HectonEquipmentRust01, 0.0),
        H8UberNoirFiniteSaturate(_HectonMaterialDecayRuntime.x, 0.0));
    degradation.InstanceID = 0u;
    degradation.RustAmount = rust;
    degradation.ScorchAmount = 0.0;
    degradation.BioFouling = H8UberNoirFiniteSaturate(_BiolumMasterPhase.z, 0.0) * 0.12;
    degradation.StructuralStress = H8UberNoirFiniteSaturate(_HectonUberNoirRuntimeParams.x, 0.0);
    degradation.Padding = uint3(0u, 0u, 0u);
    return degradation;
}

float H8UberNoirDegradationPayloadBlend()
{
    float activeCount = isfinite(_GlobalUberNoirDegradationRuntime.x) ? clamp(_GlobalUberNoirDegradationRuntime.x, 0.0, (float)H8_UBER_NOIR_AGING_CAPACITY) : 0.0;
    float payloadEnabled = isfinite(_GlobalUberNoirDegradationRuntime.y) ? saturate(_GlobalUberNoirDegradationRuntime.y) : 0.0;
    return payloadEnabled * H8UberNoirSmoothRange01(0.0, 1.0, activeCount);
}

uint H8UberNoirResolveDegradationIndex(H8UberNoirInstanceData instanceData, uint resolvedInstanceID)
{
    float encodedIndex = instanceData.SeedFadeFlags.w;
    [branch]
    if (isfinite(encodedIndex) && encodedIndex >= 1.0)
        return min((uint)floor(encodedIndex - 1.0 + 0.5), (uint)(H8_UBER_NOIR_AGING_CAPACITY - 1));

    [branch]
    if (resolvedInstanceID < (uint)H8_UBER_NOIR_AGING_CAPACITY)
        return resolvedInstanceID;

    return H8_UBER_NOIR_DEGRADATION_INDEX_INVALID;
}

H8InstanceDegradationDTO H8UberNoirLoadInstanceDegradation(uint degradationIndex)
{
    H8InstanceDegradationDTO degradation = H8UberNoirDefaultInstanceDegradation();
    float activeCountFloat = isfinite(_GlobalUberNoirDegradationRuntime.x) ? clamp(_GlobalUberNoirDegradationRuntime.x, 0.0, (float)H8_UBER_NOIR_AGING_CAPACITY) : 0.0;
    float payloadEnabled = isfinite(_GlobalUberNoirDegradationRuntime.y) ? saturate(_GlobalUberNoirDegradationRuntime.y) : 0.0;
    uint activeCount = (uint)activeCountFloat;
    [branch]
    if (payloadEnabled > H8_UBER_NOIR_EPS &&
        degradationIndex != H8_UBER_NOIR_DEGRADATION_INDEX_INVALID &&
        degradationIndex < activeCount &&
        degradationIndex < (uint)H8_UBER_NOIR_AGING_CAPACITY)
    {
        H8InstanceDegradationDTO payload = _GlobalUberNoirDegradation[degradationIndex];
        payload.RustAmount = H8UberNoirFiniteSaturate(payload.RustAmount, 0.0);
        payload.ScorchAmount = H8UberNoirFiniteSaturate(payload.ScorchAmount, 0.0);
        payload.BioFouling = H8UberNoirFiniteSaturate(payload.BioFouling, 0.0);
        payload.StructuralStress = H8UberNoirFiniteSaturate(payload.StructuralStress, 0.0);
        float payloadBlend = H8UberNoirDegradationPayloadBlend();
        degradation.InstanceID = payload.InstanceID;
        degradation.RustAmount = lerp(degradation.RustAmount, payload.RustAmount, payloadBlend);
        degradation.ScorchAmount = lerp(degradation.ScorchAmount, payload.ScorchAmount, payloadBlend);
        degradation.BioFouling = lerp(degradation.BioFouling, payload.BioFouling, payloadBlend);
        degradation.StructuralStress = lerp(degradation.StructuralStress, payload.StructuralStress, payloadBlend);
    }
    return degradation;
}

H8VisualAgingParamsDTO H8UberNoirLoadVisualAging(uint degradationIndex)
{
    H8VisualAgingParamsDTO aging = H8UberNoirDefaultVisualAging();
    float activeCountFloat = isfinite(_GlobalBaseAgingRuntime.x) ? clamp(_GlobalBaseAgingRuntime.x, 0.0, (float)H8_UBER_NOIR_AGING_CAPACITY) : 0.0;
    float payloadEnabled = isfinite(_GlobalBaseAgingRuntime.y) ? saturate(_GlobalBaseAgingRuntime.y) : 0.0;
    uint activeCount = (uint)activeCountFloat;
    [branch]
    if (payloadEnabled > H8_UBER_NOIR_EPS &&
        degradationIndex != H8_UBER_NOIR_DEGRADATION_INDEX_INVALID &&
        degradationIndex < activeCount &&
        degradationIndex < (uint)H8_UBER_NOIR_AGING_CAPACITY)
    {
        H8VisualAgingParamsDTO payload = _GlobalBaseAgingParams[degradationIndex];
        payload.RustAndCorrosion = H8UberNoirFiniteSaturate4(payload.RustAndCorrosion, float4(0.0, 0.0, 0.0, 0.0));
        payload.SaltAndBiomass = H8UberNoirFiniteSaturate4(payload.SaltAndBiomass, float4(0.0, 0.0, 0.0, 0.0));
        payload.StressAndMicroFractures = H8UberNoirFiniteSaturate4(payload.StressAndMicroFractures, float4(0.0, 0.0, 0.0, H8UberNoirGlobalQualityWeight()));
        payload.DepthAndPressure.xyz = H8UberNoirFinite3(payload.DepthAndPressure.xyz, float3(0.0, 0.0, 0.0));
        payload.DepthAndPressure.w = isfinite(payload.DepthAndPressure.w) ? saturate(payload.DepthAndPressure.w) : 0.0;
        float payloadBlend = H8UberNoirSmoothRange01(0.0, 1.0, activeCountFloat) * payloadEnabled;
        aging.RustAndCorrosion = lerp(aging.RustAndCorrosion, payload.RustAndCorrosion, payloadBlend);
        aging.SaltAndBiomass = lerp(aging.SaltAndBiomass, payload.SaltAndBiomass, payloadBlend);
        aging.StressAndMicroFractures = lerp(aging.StressAndMicroFractures, payload.StressAndMicroFractures, payloadBlend);
        aging.DepthAndPressure = lerp(aging.DepthAndPressure, payload.DepthAndPressure, payloadBlend);
    }

    H8InstanceDegradationDTO degradation = H8UberNoirLoadInstanceDegradation(degradationIndex);
    float degradationBlend = H8UberNoirDegradationPayloadBlend();
    [branch]
    if (degradationBlend > H8_UBER_NOIR_EPS)
    {
        aging.RustAndCorrosion.x = saturate(max(aging.RustAndCorrosion.x, degradation.RustAmount));
        aging.SaltAndBiomass.y = saturate(max(aging.SaltAndBiomass.y, degradation.BioFouling));
        aging.SaltAndBiomass.z = saturate(max(aging.SaltAndBiomass.z, degradation.ScorchAmount));
        aging.StressAndMicroFractures.x = saturate(max(aging.StressAndMicroFractures.x, degradation.StructuralStress));
    }
    return aging;
}

float H8UberNoirVisualAgingPayloadBlend()
{
    float activeCount = isfinite(_GlobalBaseAgingRuntime.x) ? clamp(_GlobalBaseAgingRuntime.x, 0.0, (float)H8_UBER_NOIR_AGING_CAPACITY) : 0.0;
    float payloadEnabled = isfinite(_GlobalBaseAgingRuntime.y) ? saturate(_GlobalBaseAgingRuntime.y) : 0.0;
    return payloadEnabled * H8UberNoirSmoothRange01(0.0, 1.0, activeCount);
}

float H8UberNoirVisualAgingQualityWeight()
{
    float baseQuality = isfinite(_H8GlobalQualityWeight) ? saturate(_H8GlobalQualityWeight) : 0.0;
    float runtimeQuality = isfinite(_GlobalBaseAgingRuntime.z) ? saturate(_GlobalBaseAgingRuntime.z) : 0.0;
    return saturate(lerp(baseQuality, runtimeQuality, H8UberNoirVisualAgingPayloadBlend()));
}

uint H8UberNoirTextureSlice(uint textureSetHash, uint offset)
{
    uint count = max(_H8UberNoirTextureSetCount, 1u);
    return ((textureSetHash & 65535u) + offset) % count;
}

float3 H8UberNoirTriplanarWeights(half3 normalWS)
{
    float3 weights = abs((float3)normalWS);
    weights = weights * weights;
    return weights * H8UberNoirSafeRcp(max(weights.x + weights.y + weights.z, H8_UBER_NOIR_EPS));
}

half4 H8UberNoirSampleAlbedoArray(uint slice, float2 uv, float3 stablePosition, half3 normalWS, float quality)
{
    half4 uvSample = SAMPLE_TEXTURE2D_ARRAY(_H8UberNoirAlbedoArray, sampler_H8UberNoirAlbedoArray, uv, slice);
    float useTriplanar = saturate((quality - 0.5) * 2.0);
    [branch]
    if (useTriplanar <= H8_UBER_NOIR_EPS)
        return uvSample;

    float3 weights = H8UberNoirTriplanarWeights(normalWS);
    float scale = lerp(0.035, 0.085, saturate(quality));
    half4 xSample = SAMPLE_TEXTURE2D_ARRAY(_H8UberNoirAlbedoArray, sampler_H8UberNoirAlbedoArray, stablePosition.zy * scale, slice);
    half4 ySample = SAMPLE_TEXTURE2D_ARRAY(_H8UberNoirAlbedoArray, sampler_H8UberNoirAlbedoArray, stablePosition.xz * scale, slice);
    half4 zSample = SAMPLE_TEXTURE2D_ARRAY(_H8UberNoirAlbedoArray, sampler_H8UberNoirAlbedoArray, stablePosition.xy * scale, slice);
    half4 triplanarSample = xSample * (half)weights.x + ySample * (half)weights.y + zSample * (half)weights.z;
    return lerp(uvSample, triplanarSample, (half)useTriplanar);
}

float H8UberNoirHash12(float2 value)
{
    float3 hash = frac(float3(value.xyx) * float3(0.1031, 0.1030, 0.0973));
    hash += dot(hash, hash.yzx + 33.33);
    return frac((hash.x + hash.y) * hash.z);
}

float H8UberNoirValueNoise2(float2 value)
{
    float2 cell = floor(value);
    float2 local = frac(value);
    float2 smoothValue = local * local * (3.0 - 2.0 * local);
    float a = H8UberNoirHash12(cell);
    float b = H8UberNoirHash12(cell + float2(1.0, 0.0));
    float c = H8UberNoirHash12(cell + float2(0.0, 1.0));
    float d = H8UberNoirHash12(cell + float2(1.0, 1.0));
    return lerp(lerp(a, b, smoothValue.x), lerp(c, d, smoothValue.x), smoothValue.y);
}

float3 H8UberNoirApplyHandRadiationMutationOS(float3 positionOS, inout float3 normalOS, float instanceSeed)
{
    float materialMask = H8UberNoirFeatureScalar(_HectonHandRadiationMask);
    float mutation01 = H8UberNoirFeatureScalar(_HectonHandRadiationMutation01) * materialMask;
    if (mutation01 <= H8_UBER_NOIR_EPS)
        return positionOS;

    float q = H8UberNoirGlobalQualityWeight();
    float3 safePosition = H8UberNoirFinite3(positionOS, float3(0.0, 0.0, 0.0));
    float3 safeNormal = H8UberNoirSafeNormalize(normalOS, float3(0.0, 1.0, 0.0));
    float2 scarUv = safePosition.xy * lerp(9.0, 31.0, q) + instanceSeed + _Time.yy * lerp(0.08, 0.22, q);
    float blisterNoise = H8UberNoirValueNoise2(scarUv);
    float poreNoise = H8UberNoirValueNoise2(scarUv * 2.37 + 13.17);
    float blister = saturate((blisterNoise - 0.42) * 2.15) * saturate(0.55 + poreNoise);
    float pulse = 0.75 + 0.25 * H8UberNoirTriangle01(_Time.y * lerp(0.35, 1.65, q) + instanceSeed);
    float displacement = blister * mutation01 * pulse * lerp(0.0015, 0.0095, q);
    normalOS = H8UberNoirSafeNormalize(safeNormal + float3((poreNoise - 0.5) * mutation01 * 0.08, blister * mutation01 * 0.06, 0.0), safeNormal);
    return H8UberNoirFinite3(safePosition + safeNormal * displacement, positionOS);
}

float H8UberNoirMaterialMacroNoise(float3 stablePosition, half instanceSeed, float quality)
{
    float cheapNoise = H8UberNoirTriangle01(dot(stablePosition.xz, float2(0.071, -0.053)));
    float detailWeight = H8UberNoirSmoothRange01(0.30, 0.58, quality);
    [branch]
    if (detailWeight <= H8_UBER_NOIR_EPS)
        return cheapNoise;

    float richNoise = H8UberNoirValueNoise2(stablePosition.xz * 0.041 + instanceSeed);
    return lerp(cheapNoise, richNoise, detailWeight);
}

float H8UberNoirAgingGrowthMask(float3 stablePosition, H8VisualAgingParamsDTO aging, half instanceSeed, float quality)
{
    float agingActive = saturate(max(max(aging.RustAndCorrosion.x, aging.RustAndCorrosion.y), max(aging.SaltAndBiomass.x, aging.StressAndMicroFractures.y)));
    float3 localAup = aging.DepthAndPressure.xyz;
    float aupSeed = dot(localAup, float3(0.00031, -0.00017, 0.00023));
    float seed = aging.RustAndCorrosion.w + instanceSeed;
    float weldBias = saturate(1.0 - abs(frac(stablePosition.y * 0.19 + seed + aupSeed) * 2.0 - 1.0));
    float edgeWeight = H8UberNoirSmoothRange01(0.08, 0.35, quality) * agingActive;
    float edgeBias = 0.0;
    [branch]
    if (edgeWeight > H8_UBER_NOIR_EPS)
        edgeBias = saturate(1.0 - abs((float)dot(H8UberNoirSafeNormalize(stablePosition + 0.001, float3(0.0, 1.0, 0.0)), float3(0.577, 0.577, 0.577))));
    float cheap = saturate((weldBias * (0.62 + 0.18 * (1.0 - edgeWeight)) + edgeBias * 0.38 * edgeWeight) * (0.35 + aging.DepthAndPressure.w));
    float detailWeight = H8UberNoirSmoothRange01(0.30, 0.74, quality) * agingActive;
    [branch]
    if (detailWeight <= H8_UBER_NOIR_EPS)
        return cheap;

    float2 p = stablePosition.xz * lerp(0.031, 0.091, detailWeight) + seed + aupSeed;
    float n0 = H8UberNoirValueNoise2(p);
    float n1 = H8UberNoirValueNoise2(p * 2.17 + aupSeed);
    float rich = saturate((n0 * 0.68 + n1 * 0.32 + weldBias * 0.25) * (0.62 + aging.RustAndCorrosion.x + aging.RustAndCorrosion.y));
    return lerp(cheap, rich, detailWeight);
}

void H8UberNoirApplyGlassMicroFracture(
    H8VisualAgingParamsDTO aging,
    float3 stablePosition,
    half3 normalWS,
    half instanceSeed,
    float quality,
    inout H8UberNoirSurface surface)
{
    half glassMask = (half)saturate(aging.SaltAndBiomass.w);
    half fracture01 = (half)saturate(aging.StressAndMicroFractures.y * glassMask);
    [branch]
    if (fracture01 <= 0.001h)
        return;

    float seed = aging.RustAndCorrosion.w + instanceSeed;
    float3 localAup = aging.DepthAndPressure.xyz;
    float aupSeed = dot(localAup, float3(0.00031, -0.00017, 0.00023));
    float cheapLine = 1.0 - abs(frac(dot(stablePosition.xz, float2(0.19, -0.27)) + seed + aupSeed) * 2.0 - 1.0);
    float detailWeight = H8UberNoirSmoothRange01(0.30, 0.82, quality) * fracture01;
    float branchMask = cheapLine;
    [branch]
    if (detailWeight > H8_UBER_NOIR_EPS)
    {
        float2 p0 = stablePosition.xy * lerp(18.0, 58.0, detailWeight) + seed + aupSeed;
        float2 p1 = stablePosition.zy * lerp(25.0, 83.0, saturate(detailWeight * detailWeight)) + seed * 1.73 + aupSeed;
        float n0 = H8UberNoirValueNoise2(p0);
        float n1 = H8UberNoirValueNoise2(p1);
        float radial = 1.0 - saturate(length(frac(stablePosition.xz * 0.071 + seed) - 0.5) * 2.0);
        float richMask = max(cheapLine, max(n0, n1) * 0.72 + radial * 0.28);
        branchMask = lerp(cheapLine, richMask, detailWeight);
    }

    half crackMask = saturate((half)((branchMask - lerp(0.93, 0.48, fracture01)) * lerp(5.0, 10.0, detailWeight))) * fracture01;
    half viewCatch = saturate(dot(abs(normalWS), half3(0.27h, 0.46h, 0.27h)));
    half3 glassWhiten = max((half3)_NoirAbyssFloorColor.rgb, half3(0.58h, 0.72h, 0.78h));
    surface.albedo = lerp(surface.albedo, glassWhiten, crackMask * (0.18h + viewCatch * 0.34h));
    surface.smoothness = lerp(surface.smoothness, 0.96h, crackMask * 0.62h);
    surface.roughness = saturate(1.0h - surface.smoothness);
    surface.emission += (half3)_BiolumHighColor.rgb * crackMask * 0.012h * (half)H8UberNoirVisualOverkill01();
}

H8UberNoirWearVitality H8UberNoirResolveWearVitality(
    float3 stablePosition,
    float2 wearUv,
    half dynamicRust,
    half dynamicSalt,
    half dynamicMoss,
    half instanceSeed,
    float quality)
{
    H8UberNoirWearVitality vitality;
    float cheapPhase = dot(stablePosition.xz, float2(0.047, -0.083)) + (float)instanceSeed * 7.13;
    float cheapMask = H8UberNoirTriangle01(cheapPhase);
    half cheapPores = (half)saturate((cheapMask - 0.54) * 2.17);
    half cheapVeins = (half)saturate((H8UberNoirTriangle01(cheapPhase * 0.63 + stablePosition.y * 0.011) - 0.62) * 2.63);
    half cheapCrystals = (half)saturate((H8UberNoirTriangle01(cheapPhase * 1.47 + 0.21) - 0.70) * 3.34);
    half cheapWeight = (half)H8UberNoirSmoothRange01(0.05, 0.18, quality);
    vitality.RustPitMask = cheapPores * dynamicRust * 0.28h * cheapWeight;
    vitality.MossVeinMask = cheapVeins * dynamicMoss * 0.20h * cheapWeight;
    vitality.SaltCrystalMask = cheapCrystals * dynamicSalt * 0.22h * cheapWeight;
    vitality.WetEdgeMask = saturate((dynamicRust + dynamicSalt) * cheapMask * 0.18h * cheapWeight);
    vitality.NormalWeight = 0.0h;
    float detailWeight = H8UberNoirSmoothRange01(0.30, 0.58, quality) * H8UberNoirHighCostAllowed();
    [branch]
    if (detailWeight <= H8_UBER_NOIR_EPS)
        return vitality;

    float safeSeed = isfinite((float)instanceSeed) ? (float)instanceSeed : 0.0;
    float2 poreUv = stablePosition.xz * 0.083 + wearUv * 11.0 + safeSeed;
    float2 veinUv = stablePosition.xy * 0.057 + stablePosition.zy * 0.019 + safeSeed * 1.73;
    float poreNoise = H8UberNoirValueNoise2(poreUv);
    float veinNoise = H8UberNoirValueNoise2(veinUv);
    float crystalNoise = H8UberNoirHash12(floor(wearUv * 131.0 + stablePosition.xz * 0.017 + safeSeed));
    float veinAxis = H8UberNoirTriangle01(stablePosition.x * 0.071 + stablePosition.z * 0.043 + veinNoise * 0.37 + safeSeed);
    half richRustPits = (half)saturate((poreNoise - 0.62) * 2.63) * dynamicRust;
    half richMossVeins = (half)saturate((veinAxis - 0.68) * 3.13) * dynamicMoss;
    half richSaltCrystals = (half)saturate((crystalNoise - 0.78) * 4.54) * dynamicSalt;
    half richWetEdges = (half)H8UberNoirValueNoise2(stablePosition.xz * 0.027 + _HectonMaterialDecayRuntime.w * 0.05 + safeSeed);
    half w = (half)detailWeight;
    vitality.RustPitMask = lerp(vitality.RustPitMask, richRustPits, w);
    vitality.MossVeinMask = lerp(vitality.MossVeinMask, richMossVeins, w);
    vitality.SaltCrystalMask = lerp(vitality.SaltCrystalMask, richSaltCrystals, w);
    vitality.WetEdgeMask = lerp(vitality.WetEdgeMask, richWetEdges * saturate((dynamicRust + dynamicSalt) * 0.45h), w);
    vitality.NormalWeight = saturate(w * (richRustPits + richMossVeins + richSaltCrystals));
    return vitality;
}

void H8UberNoirApplyWearVitalityColor(H8UberNoirWearVitality vitality, inout H8UberNoirSurface surface)
{
    half rustPit = saturate(vitality.RustPitMask);
    half mossVein = saturate(vitality.MossVeinMask);
    half saltCrystal = saturate(vitality.SaltCrystalMask);
    half wetEdge = saturate(vitality.WetEdgeMask);
    half3 rustPitTint = max((half3)_RustPitTint.rgb, half3(0.10h, 0.035h, 0.018h));
    half3 mossVeinTint = max((half3)_BiolumLowColor.rgb * 0.34h, half3(0.035h, 0.13h, 0.075h));
    half3 saltCrystalTint = max((half3)_NoirAbyssFloorColor.rgb, half3(0.68h, 0.76h, 0.82h));
    surface.albedo = lerp(surface.albedo, rustPitTint, rustPit * 0.54h);
    surface.albedo = lerp(surface.albedo, saltCrystalTint, saltCrystal * 0.42h);
    surface.albedo = lerp(surface.albedo, mossVeinTint, mossVein * 0.38h);
    surface.smoothness = lerp(surface.smoothness, 0.07h, rustPit * 0.72h);
    surface.smoothness = lerp(surface.smoothness, 0.94h, saltCrystal * 0.58h);
    surface.smoothness = lerp(surface.smoothness, max(surface.smoothness, 0.62h), wetEdge * 0.34h);
    surface.occlusion = lerp(surface.occlusion, max(0.34h, surface.occlusion * 0.72h), rustPit * 0.36h);
    surface.emission += (half3)_BiolumLowColor.rgb * mossVein * 0.024h * saturate(surface.powerLevel);
    surface.roughness = saturate(1.0h - surface.smoothness);
    surface.rustMask = saturate(max(surface.rustMask, max(rustPit, saltCrystal)));
}

void H8UberNoirApplyWearVitalityNormal(
    H8UberNoirWearVitality vitality,
    float3 safeNormalWS,
    float3 safeTangentWS,
    float3 safeBitangentWS,
    inout H8UberNoirSurface surface)
{
    half normalMask = saturate(vitality.NormalWeight * 0.72h);
    [branch]
    if (normalMask <= 0.001h)
        return;

    half2 slope = half2(
        vitality.SaltCrystalMask - vitality.RustPitMask,
        vitality.MossVeinMask - vitality.SaltCrystalMask) * normalMask;
    half3 microNormalTS = H8UberNoirSafeNormalizeHalf(half3(slope.x, slope.y, 1.0h), half3(0.0h, 0.0h, 1.0h));
    float3 baseNormalWS = H8UberNoirSafeNormalize((float3)surface.normalWS, safeNormalWS);
    float3 microNormalWS = H8UberNoirSafeNormalize(
        safeTangentWS * microNormalTS.x + safeBitangentWS * microNormalTS.y + baseNormalWS * microNormalTS.z,
        baseNormalWS);
    surface.normalWS = (half3)H8UberNoirSafeNormalize(lerp(baseNormalWS, microNormalWS, normalMask), baseNormalWS);
}

float2 H8UberNoirScreenUV(float4 positionCS)
{
    float2 screenUV = positionCS.xy * H8UberNoirSafeRcp(positionCS.w) * 0.5 + 0.5;
#if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
    screenUV = UnityStereoTransformScreenSpaceTex(screenUV);
#endif
    return saturate(screenUV);
}

half H8UberNoirCheapDither(float4 positionCS)
{
    float2 screenUV = H8UberNoirScreenUV(positionCS);
    float2 pixel = floor(screenUV * _ScaledScreenParams.xy);
    return (half)H8WaterExtinctionInterleavedGradientNoise(pixel);
}

half H8UberNoirBlueNoise(float4 positionCS)
{
    float2 screenUV = H8UberNoirScreenUV(positionCS);
    float2 r2 = frac(_Time.y * float2(0.75487766, 0.56984029) * max(_UberNoirDitherParams.z, 0.0));
    return SAMPLE_TEXTURE2D(_BlueNoiseTex, sampler_BlueNoiseTex, screenUV * (_ScaledScreenParams.xy * (1.0 / 64.0)) + r2).r;
}

half H8UberNoirFogIgnDither(float4 positionCS, half fogCurve)
{
    float2 screenUV = H8UberNoirScreenUV(positionCS);
    float2 pixel = floor(screenUV * _ScaledScreenParams.xy);
    half transitionEdge = saturate(1.0h - abs(fogCurve - 0.5h) * 2.0h);
    half noise = (half)H8WaterExtinctionInterleavedGradientNoise(pixel);
    half active = (half)H8WaterExtinctionActive();
    return (noise - 0.5h) * transitionEdge * 0.0078125h * active;
}

void H8UberNoirClipDitheredTransparency(half alpha, float4 positionCS)
{
    half threshold = (half)_Cutoff;
    half ditherActive = (half)H8UberNoirFeatureScalar(_UberNoirFeatureFlags.w);
    half blueNoiseAllowed = ditherActive * (half)H8UberNoirHighCostAllowed() * (half)H8UberNoirSmoothRange01(0.22, 0.58, H8UberNoirGlobalQualityWeight());
    [branch]
    if (blueNoiseAllowed > (half)H8_UBER_NOIR_EPS)
        threshold = H8UberNoirBlueNoise(positionCS);
    else
        threshold = lerp(threshold, H8UberNoirCheapDither(positionCS), ditherActive);
    half coverage = saturate(alpha * (half)max(_UberNoirDitherParams.w, 0.0));
    clip(coverage - threshold);
}

H8UberNoirInstanceData H8UberNoirBuildDefaultInstance()
{
    H8UberNoirInstanceData instanceData;
    instanceData.ObjectToWorld = GetObjectToWorldMatrix();
    instanceData.WorldToObject = GetWorldToObjectMatrix();
    instanceData.SeedFadeFlags = float4(0.0, 1.0, 0.0, 0.0);
    return instanceData;
}

H8UberNoirInstanceData H8UberNoirLoadInstance(uint instanceID)
{
    H8UberNoirInstanceData instanceData = H8UberNoirBuildDefaultInstance();
#if defined(H8_UBERNOIR_USE_INSTANCE_BUFFER)
    float bufferOffsetSource = _UberNoirInstanceParams.x;
    float bufferCountSource = _UberNoirInstanceParams.y;
    float useBufferSource = _UberNoirInstanceParams.z;
    float bufferCapacitySource = _UberNoirInstanceCapacity;
    uint bufferCount = (uint)(isfinite(bufferCountSource) ? max(bufferCountSource, 0.0) : 0.0);
    uint bufferOffset = (uint)(isfinite(bufferOffsetSource) ? max(bufferOffsetSource, 0.0) : 0.0);
    uint bufferCapacity = (uint)(isfinite(bufferCapacitySource) ? max(bufferCapacitySource, 0.0) : 0.0);
    float useBuffer = H8UberNoirFeatureScalar(useBufferSource);
    [branch]
    if ((useBuffer > H8_UBER_NOIR_EPS) && (bufferCount > 0u) && (bufferCapacity > bufferOffset))
    {
        uint safeCount = min(bufferCount, bufferCapacity - bufferOffset);
        uint clampedId = min(instanceID, safeCount - 1u);
        uint bufferIndex = bufferOffset + clampedId;
        [branch]
        if (bufferIndex >= bufferOffset && bufferIndex < bufferCapacity)
            instanceData = _H8UberNoirInstanceData[bufferIndex];
    }
#endif
    return instanceData;
}

float4x4 H8UberNoirObjectToRuntimeWorld(float4x4 objectToWorld)
{
    float3 runtimeTranslation = H8UberNoirFinite3(
        float3(objectToWorld._m03, objectToWorld._m13, objectToWorld._m23),
        float3(0.0, 0.0, 0.0));
    objectToWorld._m03 = runtimeTranslation.x;
    objectToWorld._m13 = runtimeTranslation.y;
    objectToWorld._m23 = runtimeTranslation.z;
    return objectToWorld;
}

float3 H8UberNoirTransformNormal(float3 normalOS, float4x4 worldToObject)
{
    float3 normalWS = mul(normalOS, (float3x3)worldToObject);
    return H8UberNoirSafeNormalize(normalWS, float3(0.0, 1.0, 0.0));
}

float H8UberNoirUnpackDentRadius(float packed)
{
    float safePacked = isfinite(packed) ? max(packed, 0.0) : 0.0;
    float packedInt = floor(safePacked + 0.5);
    return fmod(packedInt, 256.0) * 0.0625;
}

float H8UberNoirUnpackDentDepth(float packed)
{
    float safePacked = isfinite(packed) ? max(packed, 0.0) : 0.0;
    float packedInt = floor(safePacked + 0.5);
    return fmod(floor(packedInt * (1.0 / 256.0)), 256.0) * (1.0 / 255.0);
}

int H8UberNoirActiveDeformationStateCount()
{
    float active = isfinite(_HectonDeformationStateParams.x) ? max(_HectonDeformationStateParams.x, 0.0) : 0.0;
    float limit = isfinite(_HectonDeformationStateParams.y) ? max(_HectonDeformationStateParams.y, 0.0) : 0.0;
    return (int)clamp(min(active, limit), 0.0, (float)H8_UBER_NOIR_MAX_DEFORMATION_STATES);
}

float H8UberNoirGaussianFalloff(float distSq, float radius)
{
    float radiusSq = max(radius * radius, H8_UBER_NOIR_EPS);
    float exponent = -distSq * (0.7213475204444817 * H8UberNoirSafeRcp(radiusSq));
    return exp2(max(exponent, -16.0));
}

float3 H8UberNoirEvaluateDeformationNormalBiasOS(float3 positionOS, float3 normalOS)
{
    float featureMask = H8UberNoirFeatureScalar(_UberNoirFeatureFlags.z);
    int activeCount = H8UberNoirActiveDeformationStateCount();
    if (activeCount <= 0 || featureMask <= 0.0)
        return float3(0.0, 0.0, 0.0);

    float3 safePositionOS = H8UberNoirFinite3(positionOS, float3(0.0, 0.0, 0.0));
    float3 safeNormalOS = H8UberNoirSafeNormalize(normalOS, float3(0.0, 1.0, 0.0));
    float quality = isfinite(_HectonDeformationStateParams.w)
        ? saturate(_HectonDeformationStateParams.w)
        : H8UberNoirGlobalQualityWeight();
    float3 bias = float3(0.0, 0.0, 0.0);

    [loop]
    for (int i = 0; i < activeCount; i++)
    {
        H8DeformationStateDTO dent = _HectonDeformationStateBuffer[i];
        if ((dent.Flags & 1u) == 0u)
            continue;

        float radius = max(dent.Radius, H8_UBER_NOIR_EPS);
        float depth = max(dent.Depth, 0.0);
        float3 dentNormalOS = H8UberNoirSafeNormalize(dent.Normal, safeNormalOS);
        float3 delta = safePositionOS - dent.LocalPosition;
        float distSq = dot(delta, delta);
        float gaussian = H8UberNoirGaussianFalloff(distSq, radius);
        float normalMask = saturate(dot(safeNormalOS, dentNormalOS) * 0.5 + 0.5);
        float3 tangentDelta = delta - dentNormalOS * dot(delta, dentNormalOS);
        float derivative = depth * gaussian * H8UberNoirSafeRcp(max(radius * radius, H8_UBER_NOIR_EPS));
        bias += tangentDelta * (derivative * normalMask);
    }

    return H8UberNoirFinite3(bias * featureMask * lerp(0.35, 1.0, quality), float3(0.0, 0.0, 0.0));
}

float H8UberNoirHullDentProxyWeight()
{
    return saturate(_HectonHullDentParams.y);
}

float H8UberNoirHullDentExactWeight()
{
    return saturate(1.0 - H8UberNoirHullDentProxyWeight());
}

float3 H8UberNoirApplyHullDentsOS(float3 positionOS, float3 normalOS)
{
    float featureMask = H8UberNoirFeatureScalar(_UberNoirFeatureFlags.z);
    float strength = max(_UberNoirBendParams.x, 0.0) * featureMask;
    float3 dentedPosition = H8UberNoirFinite3(positionOS, float3(0.0, 0.0, 0.0));
    float3 safeNormalOS = H8UberNoirSafeNormalize(normalOS, float3(0.0, 1.0, 0.0));

    int deformationActiveCount = H8UberNoirActiveDeformationStateCount();
    if (deformationActiveCount > 0)
    {
        [loop]
        for (int deformationIndex = 0; deformationIndex < deformationActiveCount; deformationIndex++)
        {
            H8DeformationStateDTO deformation = _HectonDeformationStateBuffer[deformationIndex];
            if ((deformation.Flags & 1u) == 0u)
                continue;

            float radius = max(deformation.Radius, H8_UBER_NOIR_EPS);
            float depth = max(deformation.Depth, 0.0);
            float3 dentNormalOS = H8UberNoirSafeNormalize(deformation.Normal, safeNormalOS);
            float3 delta = dentedPosition - deformation.LocalPosition;
            float distSq = dot(delta, delta);
            float gaussian = H8UberNoirGaussianFalloff(distSq, radius);
            float normalMask = saturate(dot(safeNormalOS, dentNormalOS) * 0.5 + 0.5);
            dentedPosition -= dentNormalOS * (gaussian * depth * strength * normalMask);
        }

        return H8UberNoirFinite3(dentedPosition, positionOS);
    }

    int dtoActiveCount = (int)clamp(_HectonHullDentDTOParams.x * H8UberNoirFeatureScalar(_HectonHullDentDTOParams.y), 0.0, (float)H8_UBER_NOIR_MAX_GPU_HULL_DENTS);
    if (dtoActiveCount > 0)
    {
        [loop]
        for (int dtoIndex = 0; dtoIndex < dtoActiveCount; dtoIndex++)
        {
            H8HullDentDTO dtoDent = _HectonHullDentDTOBuffer[dtoIndex];
            float radius = max(dtoDent.Radius, H8_UBER_NOIR_EPS);
            float depth = max(dtoDent.Depth, 0.0);
            float3 dentNormalOS = H8UberNoirSafeNormalize(dtoDent.Normal, safeNormalOS);
            float3 delta = dentedPosition - dtoDent.Position;
            float falloff = saturate(1.0 - dot(delta, delta) * H8UberNoirSafeRcp(radius * radius));
            float normalMask = saturate(dot(safeNormalOS, dentNormalOS) * 0.5 + 0.5);
            dentedPosition -= dentNormalOS * (falloff * falloff * depth * strength * normalMask);
        }

        return H8UberNoirFinite3(dentedPosition, positionOS);
    }

    float exactWeight = H8UberNoirHullDentExactWeight();
    float activeCount = clamp(_HectonHullDentParams.x, 0.0, (float)H8_UBER_NOIR_MAX_HULL_DENTS);
    if (activeCount <= 0.5 || exactWeight <= H8_UBER_NOIR_EPS)
        return H8UberNoirFinite3(dentedPosition, positionOS);

    [unroll(H8_UBER_NOIR_MAX_HULL_DENTS)]
    for (int dentIndex = 0; dentIndex < H8_UBER_NOIR_MAX_HULL_DENTS; dentIndex++)
    {
        float active = step((float)dentIndex + 0.5, activeCount);
        float4 dent = _HectonHullDents[dentIndex];
        float radius = max(H8UberNoirUnpackDentRadius(dent.w), H8_UBER_NOIR_EPS);
        float depth = H8UberNoirUnpackDentDepth(dent.w);
        float3 delta = dentedPosition - dent.xyz;
        float falloff = saturate(1.0 - dot(delta, delta) * H8UberNoirSafeRcp(radius * radius));
        dentedPosition -= safeNormalOS * (falloff * falloff * depth * strength * active * exactWeight);
    }

    return H8UberNoirFinite3(dentedPosition, positionOS);
}

void H8UberNoirBuildTangentFrame(
    half3 normalWS,
    half4 tangentWS,
    out float3 safeNormalWS,
    out float3 safeTangentWS,
    out float3 safeBitangentWS)
{
    safeNormalWS = H8UberNoirSafeNormalize((float3)normalWS, float3(0.0, 1.0, 0.0));
    safeTangentWS = H8UberNoirSafeNormalize((float3)tangentWS.xyz, float3(1.0, 0.0, 0.0));
    safeBitangentWS = H8UberNoirSafeNormalize(cross(safeNormalWS, safeTangentWS) * tangentWS.w, float3(0.0, 0.0, 1.0));
}

float H8UberNoirBucklingMask(float3 positionWS, half instanceSeed)
{
    float gridScaleSource = _UberNoirBendParams.y;
    float gridScale = isfinite(gridScaleSource) ? max(gridScaleSource, H8_UBER_NOIR_EPS) : 1.0;
    float safeInstanceSeed = isfinite((float)instanceSeed) ? (float)instanceSeed : 0.0;
    float3 stablePosition = H8UberNoirMaterialStablePosition(positionWS) * gridScale;
    stablePosition = H8UberNoirFinite3(stablePosition, float3(0.0, 0.0, 0.0));
    float2 cellA = floor(stablePosition.xz + safeInstanceSeed * 17.0);
    float2 cellB = floor(stablePosition.xy * 1.37 + safeInstanceSeed * 29.0);
    float panelA = H8UberNoirTriangle01(dot(cellA, float2(0.31, 0.47)));
    float panelB = H8UberNoirTriangle01(dot(cellB, float2(0.23, 0.41)));
    float crease = H8UberNoirTriangle01(dot(stablePosition, float3(0.019, 0.031, 0.043)));
    return saturate(panelA * 0.52 + panelB * 0.34 + crease * 0.28);
}

float H8UberNoirRadiusMask(float3 positionWS, float4 centerRadius)
{
    float positionFinite = all(isfinite(positionWS)) ? 1.0 : 0.0;
    float centerFinite = all(isfinite(centerRadius)) ? 1.0 : 0.0;
    float valid = positionFinite * centerFinite;
    float3 safePositionWS = H8UberNoirFinite3(positionWS, float3(0.0, 0.0, 0.0));
    float4 safeCenterRadius = centerFinite > 0.5 ? centerRadius : float4(safePositionWS, 0.0);
    float radius = max(safeCenterRadius.w, 0.0);
    float3 delta = safePositionWS - safeCenterRadius.xyz;
    float radiusSq = max(radius * radius, H8_UBER_NOIR_EPS);
    float active = step(H8_UBER_NOIR_EPS, radius);
    float falloff = 1.0 - saturate(dot(delta, delta) * H8UberNoirSafeRcp(radiusSq));
    return falloff * active * valid;
}

float3 H8UberNoirApplyDynamicHullBendingWS(float3 positionWS, float3 normalWS, half instanceSeed)
{
    float3 safePositionWS = H8UberNoirFinite3(positionWS, float3(0.0, 0.0, 0.0));
    float featureSource = _UberNoirFeatureFlags.z;
    float featureMask = H8UberNoirFeatureScalar(featureSource);
    float quality = H8UberNoirGlobalQualityWeight();
    float detailWeight = H8UberNoirSmoothRange01(0.16, 0.48, quality) * H8UberNoirHighCostAllowed();
    float localStrengthSource = _UberNoirBendParams.x;
    float localStrength = (isfinite(localStrengthSource) ? max(localStrengthSource, 0.0) : 0.0) * detailWeight;
    [branch]
    if (featureMask <= 0.0 || localStrength <= H8_UBER_NOIR_EPS)
        return safePositionWS;

    float crushDepthSource = _HectonSubmarineCrushDepthParams.y;
    float crushCurrentSource = _HectonSubmarineCrushDepthParams.x;
    float crushDisplacementSource = _HectonSubmarineCrushDepthParams.z;
    float crushDepth = isfinite(crushDepthSource) ? max(crushDepthSource, H8_UBER_NOIR_EPS) : H8_UBER_NOIR_EPS;
    float crushCurrent = isfinite(crushCurrentSource) ? max(crushCurrentSource, 0.0) : 0.0;
    float crush01 = saturate(crushCurrent * H8UberNoirSafeRcp(crushDepth));
    float crushDisplacement = isfinite(crushDisplacementSource) ? max(crushDisplacementSource, 0.0) * crush01 : 0.0;
    crushDisplacement = isfinite(crushDisplacement) ? crushDisplacement : 0.0;
    float crushMask = 0.0;
    [branch]
    if (crushDisplacement > H8_UBER_NOIR_EPS)
        crushMask = H8UberNoirRadiusMask(safePositionWS, _HectonSubmarineCrushCenterRadius);

    float habitatStressSource = _HectonHabitatStressParams.x;
    float habitatDisplacementSource = _HectonHabitatStressParams.y;
    float habitatStress01 = isfinite(habitatStressSource) ? saturate(habitatStressSource) : 0.0;
    float habitatDisplacement = isfinite(habitatDisplacementSource)
        ? max(habitatDisplacementSource, 0.0) * habitatStress01
        : 0.0;
    habitatDisplacement = isfinite(habitatDisplacement) ? habitatDisplacement : 0.0;
    float habitatMask = 0.0;
    [branch]
    if (habitatDisplacement > H8_UBER_NOIR_EPS)
        habitatMask = H8UberNoirRadiusMask(safePositionWS, _HectonHabitatStressCenterRadius);

    float weightedDisplacement = crushDisplacement * crushMask + habitatDisplacement * habitatMask;
    [branch]
    if (weightedDisplacement <= H8_UBER_NOIR_EPS)
        return safePositionWS;

    float buckle = H8UberNoirBucklingMask(safePositionWS, instanceSeed) * 2.0 - 1.0;
    float displacement = weightedDisplacement * buckle * localStrength * featureMask;
    displacement = isfinite(displacement) ? displacement : 0.0;
    return H8UberNoirFinite3(safePositionWS + H8UberNoirSafeNormalize(normalWS, float3(0.0, 1.0, 0.0)) * displacement, safePositionWS);
}

float H8UberNoirEvaluateHullDentScarOS(float3 positionOS)
{
    float featureMask = H8UberNoirFeatureScalar(_UberNoirFeatureFlags.z);
    float scar = 0.0;
    int deformationActiveCount = H8UberNoirActiveDeformationStateCount();
    if (deformationActiveCount > 0)
    {
        float3 safePositionOS = H8UberNoirFinite3(positionOS, float3(0.0, 0.0, 0.0));
        [loop]
        for (int deformationIndex = 0; deformationIndex < deformationActiveCount; deformationIndex++)
        {
            H8DeformationStateDTO deformation = _HectonDeformationStateBuffer[deformationIndex];
            if ((deformation.Flags & 1u) == 0u)
                continue;

            float radius = max(deformation.Radius, H8_UBER_NOIR_EPS);
            float depth = max(deformation.Depth, 0.0);
            float3 delta = safePositionOS - deformation.LocalPosition;
            scar = max(scar, H8UberNoirGaussianFalloff(dot(delta, delta), radius) * depth);
        }

        return saturate(scar * featureMask);
    }

    int dtoActiveCount = (int)clamp(_HectonHullDentDTOParams.x * H8UberNoirFeatureScalar(_HectonHullDentDTOParams.y), 0.0, (float)H8_UBER_NOIR_MAX_GPU_HULL_DENTS);
    if (dtoActiveCount > 0)
    {
        [loop]
        for (int dtoIndex = 0; dtoIndex < dtoActiveCount; dtoIndex++)
        {
            H8HullDentDTO dtoDent = _HectonHullDentDTOBuffer[dtoIndex];
            float radius = max(dtoDent.Radius, H8_UBER_NOIR_EPS);
            float depth = max(dtoDent.Depth, 0.0);
            float3 delta = positionOS - dtoDent.Position;
            float falloff = saturate(1.0 - dot(delta, delta) * H8UberNoirSafeRcp(radius * radius));
            scar = max(scar, falloff * falloff * depth);
        }

        return saturate(scar * featureMask);
    }

    float proxyWeight = H8UberNoirHullDentProxyWeight();
    float exactWeight = H8UberNoirHullDentExactWeight();
    float proxyScar = saturate(_HectonHullDentParams.z * proxyWeight);
    float activeCount = clamp(_HectonHullDentParams.x, 0.0, (float)H8_UBER_NOIR_MAX_HULL_DENTS);
    if (activeCount <= 0.5 || exactWeight <= H8_UBER_NOIR_EPS)
        return saturate(proxyScar * featureMask);

    [unroll(H8_UBER_NOIR_MAX_HULL_DENTS)]
    for (int dentIndex = 0; dentIndex < H8_UBER_NOIR_MAX_HULL_DENTS; dentIndex++)
    {
        float active = step((float)dentIndex + 0.5, activeCount);
        float4 dent = _HectonHullDents[dentIndex];
        float radius = max(H8UberNoirUnpackDentRadius(dent.w), H8_UBER_NOIR_EPS);
        float depth = H8UberNoirUnpackDentDepth(dent.w);
        float3 delta = positionOS - dent.xyz;
        float falloff = saturate(1.0 - dot(delta, delta) * H8UberNoirSafeRcp(radius * radius));
        scar = max(scar, falloff * falloff * depth * active);
    }

    return saturate(max(scar * exactWeight, proxyScar) * featureMask);
}

half3 H8UberNoirApplyBentHullNormalBiasWS(half3 normalWS, half3 viewDirWS)
{
    float featureMask = H8UberNoirFeatureScalar(_UberNoirFeatureFlags.z);
    float detailWeight = H8UberNoirSmoothRange01(0.18, 0.52, H8UberNoirGlobalQualityWeight()) * H8UberNoirHighCostAllowed();
    float crushDepth = max(_HectonSubmarineCrushDepthParams.y, H8_UBER_NOIR_EPS);
    float crush01 = saturate(max(_HectonSubmarineCrushDepthParams.x, 0.0) * H8UberNoirSafeRcp(crushDepth));
    float habitat01 = saturate(_HectonHabitatStressParams.x);
    float dentScar01 = saturate(_HectonHullDentParams.z);
    float bendBias = saturate(max(crush01, habitat01) * max(_UberNoirBendParams.z, 0.0) + dentScar01 * 0.35);
    bendBias *= featureMask * detailWeight;

    float3 safeNormal = H8UberNoirSafeNormalize((float3)normalWS, float3(0.0, 1.0, 0.0));
    float3 safeView = H8UberNoirSafeNormalize((float3)viewDirWS, float3(0.0, 0.0, 1.0));
    float3 biasedNormal = H8UberNoirSafeNormalize(safeNormal + safeView * (bendBias * 0.58), safeNormal);
    return (half3)H8UberNoirSafeNormalize(lerp(safeNormal, biasedNormal, bendBias), safeNormal);
}

void H8UberNoirApplyGlobalWakeWS(inout float3 positionWS, inout float3 normalWS, half instanceSeed)
{
    float3 safePositionWS = H8UberNoirFinite3(positionWS, float3(0.0, 0.0, 0.0));
    float3 safeNormalWS = H8UberNoirSafeNormalize(normalWS, float3(0.0, 1.0, 0.0));
    float rawWakeCount = isfinite(_GlobalWakeParams.x) ? max(_GlobalWakeParams.x, 0.0) : 0.0;
    int rawSlotLimit = min((int)rawWakeCount, H8_UBER_NOIR_MAX_GLOBAL_WAKES);
    if (rawSlotLimit <= 0)
    {
        positionWS = safePositionWS;
        normalWS = safeNormalWS;
        return;
    }

    float3 wakeOffsetWS = float3(0.0, 0.0, 0.0);
    float3 normalImpulseWS = float3(0.0, 0.0, 0.0);
    float seed = frac((float)instanceSeed * 11.13);

    float quality = H8UberNoirGlobalQualityWeight();
    float detailWeight = H8UberNoirSmoothRange01(0.16, 0.66, quality) * H8UberNoirHighCostAllowed();
    int qualitySlotLimit = max(1, min(H8_UBER_NOIR_MAX_GLOBAL_WAKES, (int)ceil(lerp(1.0, (float)H8_UBER_NOIR_MAX_GLOBAL_WAKES, detailWeight))));
    int slotLimit = min(rawSlotLimit, qualitySlotLimit);

    [loop]
    for (int i = 0; i < slotLimit; i++)
    {
        float4 wake = _GlobalWakeBuffer[i];
        float4 wakeVector = _GlobalWakeVectors[i];
        float intensity = max(wake.w, 0.0);
        float radius = max(wakeVector.w, 0.0);

        float3 delta = safePositionWS - wake.xyz;
        float distSq = dot(delta, delta);
        float radiusSq = max(radius * radius, H8_UBER_NOIR_EPS);
        float active = step(H8_UBER_NOIR_EPS, intensity) *
            step(H8_UBER_NOIR_EPS, radius) *
            step(distSq, radiusSq);

        float3 pushAxisWS = H8UberNoirSafeNormalize(wakeVector.xyz, float3(0.0, 0.0, 1.0));
        float3 radialWS = H8UberNoirSafeNormalize(delta, pushAxisWS);
        float falloff = saturate(1.0 - distSq * H8UberNoirSafeRcp(radiusSq));
        float falloffSq = falloff * falloff;
        float3 upCurlWS = H8UberNoirSafeNormalize(cross(pushAxisWS, safeNormalWS), float3(0.0, 0.0, 0.0));
        if (dot(upCurlWS, upCurlWS) <= H8_UBER_NOIR_EPS)
            upCurlWS = H8UberNoirSafeNormalize(cross(pushAxisWS, float3(0.0, 1.0, 0.0)), float3(1.0, 0.0, 0.0));

        float3 vortexWS = H8UberNoirSafeNormalize(cross(pushAxisWS, radialWS) + upCurlWS * 0.35, upCurlWS);
        float spatialPhase = H8UberNoirTriangle01(dot(safePositionWS.xz + seed, float2(0.173, 0.219)) + (float)i * 0.131);
        float directionalGate = saturate(dot(radialWS, pushAxisWS) * 0.5 + 0.5);
        float overkill = H8UberNoirVisualOverkill01();
        float lowPush = intensity * falloffSq * 0.035;
        float curvatureStrength = intensity * falloffSq * (0.055 + spatialPhase * 0.025) * lerp(1.0, 1.45, overkill) * detailWeight;
        float pushStrength = lerp(lowPush, intensity * falloffSq * (0.022 + directionalGate * 0.018), detailWeight);
        wakeOffsetWS += (radialWS * pushStrength + vortexWS * curvatureStrength) * active;
        normalImpulseWS += (radialWS * (falloff * intensity * lerp(0.18, 0.22, detailWeight)) + vortexWS * (curvatureStrength * lerp(3.5, 4.8, overkill))) * active;
    }

    positionWS = H8UberNoirFinite3(safePositionWS + wakeOffsetWS, safePositionWS);
    normalWS = H8UberNoirSafeNormalize(safeNormalWS + normalImpulseWS, safeNormalWS);
}

half H8UberNoirResolveRust01()
{
    return saturate((half)max(_HectonEquipmentRust01, _HectonMaterialDecayRuntime.x) * (half)max(_UberNoirRustParams.x, 0.0));
}

float2 H8UberNoirResolveRustPomUv(
    float2 rawUv,
    float2 baseUv,
    float2 baseUvScale,
    half3 viewDirWS,
    half3 normalWS,
    half4 tangentWS,
    half visualRust01,
    float quality,
    out half4 rustPacked,
    out half rustMask)
{
    half rust01 = max(H8UberNoirResolveRust01(), saturate(visualRust01));
    rustPacked = half4(0.0h, 0.5h, 0.5h, 1.0h);
    rustMask = rust01;

    [branch]
    if (rust01 <= (half)H8_UBER_NOIR_EPS)
        return baseUv;

    float rustDetailWeight = H8UberNoirSmoothRange01(0.30, 0.54, quality);
    [branch]
    if (rustDetailWeight <= H8_UBER_NOIR_EPS)
        return baseUv;

    float rustStValid = step(H8_UBER_NOIR_EPS, abs(_RustDetailMap_ST.x) + abs(_RustDetailMap_ST.y));
    float2 rustScale = lerp(float2(1.0, 1.0), _RustDetailMap_ST.xy, rustStValid);
    float2 rustOffset = _RustDetailMap_ST.zw * rustStValid;
    float2 rustUv = rawUv * rustScale + rustOffset;
    rustPacked = SAMPLE_TEXTURE2D(_RustDetailMap, sampler_RustDetailMap, rustUv);

    float rustActive = H8UberNoirSmoothRange01(_UberNoirRustParams.y, _UberNoirRustParams.y + 0.08, rust01);
    float materialQualityPressure = H8UberNoirFiniteSaturate(_HectonMaterialDecayRuntime.z, 0.0);
    float decayAllowed = 1.0 - H8UberNoirSmoothRange01(0.18, 0.72, materialQualityPressure);
    float pomQuality = H8UberNoirSmoothRange01(0.58, 0.92, quality);
    float pomEnabled = H8UberNoirFeatureScalar(_UberNoirFeatureFlags.x) * rustActive * decayAllowed * H8UberNoirHighCostAllowed() * pomQuality;
    [branch]
    if (pomEnabled <= 0.0)
        return baseUv;

    float3 safeNormalWS;
    float3 safeTangentWS;
    float3 safeBitangentWS;
    H8UberNoirBuildTangentFrame(normalWS, tangentWS, safeNormalWS, safeTangentWS, safeBitangentWS);
    float3 safeViewWS = H8UberNoirSafeNormalize((float3)viewDirWS, safeNormalWS);
    float3 viewDirTS = float3(dot(safeViewWS, safeTangentWS), dot(safeViewWS, safeBitangentWS), max(dot(safeViewWS, safeNormalWS), max(_UberNoirParallaxParams.y, 0.16)));
    float viewInvZ = H8UberNoirSafeRcp(viewDirTS.z);
    float sampledHeight01 = H8UberNoirFiniteSaturate((float)rustPacked.r, 0.0);
    float layerDepth = max(_UberNoirParallaxParams.z, 0.0);
    float heightBias = (sampledHeight01 - layerDepth) * 2.0 - 1.0;
    float parallaxScale = max(_UberNoirParallaxParams.x, 0.0) * rust01 * pomEnabled;
    float2 resolvedUv = rustUv - viewDirTS.xy * viewInvZ * parallaxScale * heightBias;

    rustPacked = SAMPLE_TEXTURE2D(_RustDetailMap, sampler_RustDetailMap, resolvedUv);
    half pitMask = saturate((rustPacked.r - 0.34h) * 1.85h);
    rustMask = saturate(rust01 * lerp(0.58h, 1.0h, pitMask));
    float2 invRustScale = float2(H8UberNoirSafeRcp(rustScale.x), H8UberNoirSafeRcp(rustScale.y));
    return baseUv + (resolvedUv - rustUv) * invRustScale * baseUvScale;
}

half3 H8UberNoirDecodeRustNormalTS(half4 rustPacked, half strength)
{
    half2 xy = (rustPacked.gb * 2.0h - 1.0h) * saturate(strength);
    half z = (half)H8UberNoirSafeRsqrt(1.0h + dot(xy, xy));
    return half3(xy, z);
}

void H8UberNoirApplyRustCorrosion(
    float2 wearUv,
    float3 agingStablePosition,
    half4 tangentWS,
    half4 rustPacked,
    half rustMask,
    inout H8UberNoirSurface surface)
{
    half finalRustMask = saturate(rustMask);
    [branch]
    if (finalRustMask <= (half)H8_UBER_NOIR_EPS)
        return;

    float3 safeNormalWS;
    float3 safeTangentWS;
    float3 safeBitangentWS;
    H8UberNoirBuildTangentFrame(surface.normalWS, tangentWS, safeNormalWS, safeTangentWS, safeBitangentWS);
    half3 rustNormalTS = H8UberNoirDecodeRustNormalTS(rustPacked, finalRustMask * (half)_UberNoirRustParams.z);
    float3 rustNormalWS = H8UberNoirSafeNormalize(
        safeTangentWS * rustNormalTS.x + safeBitangentWS * rustNormalTS.y + safeNormalWS * rustNormalTS.z,
        safeNormalWS);
    surface.normalWS = (half3)H8UberNoirSafeNormalize(lerp(safeNormalWS, rustNormalWS, finalRustMask), safeNormalWS);

    half cavity = saturate((rustPacked.r - 0.42h) * 1.72h);
    surface.albedo = lerp(surface.albedo, (half3)_RustTint.rgb, finalRustMask * 0.62h);
    surface.albedo = lerp(surface.albedo, (half3)_RustPitTint.rgb, cavity * finalRustMask * 0.42h);
    surface.metallic = lerp(surface.metallic, 0.0h, finalRustMask);
    surface.smoothness = lerp(surface.smoothness, saturate(1.0h - rustPacked.a), finalRustMask);
    surface.roughness = saturate(1.0h - surface.smoothness);

    half overkill = (half)H8UberNoirVisualOverkill01();
    float3 stableAgingPosition = H8UberNoirFinite3(agingStablePosition, float3(0.0, 0.0, 0.0));
    half crystalNoise = (half)H8UberNoirHash12(floor(wearUv * 97.0 + stableAgingPosition.xz * 0.031));
    half crystal = saturate((crystalNoise - 0.82h) * 5.6h) * finalRustMask * overkill;
    surface.albedo = lerp(surface.albedo, max((half3)_NoirAbyssFloorColor.rgb, half3(0.62h, 0.70h, 0.76h)), crystal * 0.26h);
    surface.smoothness = lerp(surface.smoothness, 0.93h, crystal * 0.58h);
    surface.emission += (half3)_BiolumLowColor.rgb * crystal * 0.018h;
    surface.roughness = saturate(1.0h - surface.smoothness);

    half recentWet = saturate((half)_HectonMaterialDecayRuntime.y);
    surface.smoothness = lerp(surface.smoothness, saturate((half)_UberNoirRustParams.w), recentWet);
    surface.roughness = saturate(1.0h - surface.smoothness);

    half bloodActive = saturate((half)_HectonPlayerBloodSplatter.w);
    half bloodSource = saturate((half)max(_HectonPlayerBloodSplatter.x, _HectonPlayerBloodSplatter.y));
    half noiseA = (half)H8UberNoirHash12(floor(wearUv * 39.0 + _HectonMaterialDecayRuntime.w * 0.11));
    half patch = saturate((noiseA - 0.56h) * 2.65h) * bloodSource * bloodActive;
    surface.albedo = lerp(surface.albedo, (half3)_RustPitTint.rgb * 0.35h, patch * 0.72h);
    surface.smoothness = lerp(surface.smoothness, 1.0h, patch * saturate((half)_HectonPlayerBloodSplatter.z));
    surface.roughness = saturate(1.0h - surface.smoothness);
}

void H8UberNoirApplyScorchDegradation(
    H8VisualAgingParamsDTO aging,
    float3 agingStablePosition,
    half4 rustPacked,
    float3 safeNormalWS,
    float3 safeTangentWS,
    float3 safeBitangentWS,
    inout H8UberNoirSurface surface)
{
    half scorch01 = saturate((half)aging.SaltAndBiomass.z);
    [branch]
    if (scorch01 <= (half)H8_UBER_NOIR_EPS)
        return;

    float quality = H8UberNoirVisualAgingQualityWeight();
    float3 localAup = aging.DepthAndPressure.xyz;
    float3 stableAgingPosition = H8UberNoirFinite3(agingStablePosition, float3(0.0, 0.0, 0.0));
    float aupSeed = dot(localAup, float3(0.00043, 0.00019, -0.00031)) + aging.RustAndCorrosion.w;
    float cheapChar = H8UberNoirTriangle01(dot(stableAgingPosition.xy, float2(0.113, -0.079)) + aupSeed);
    float detailWeight = H8UberNoirSmoothRange01(0.30, 0.74, quality);
    float burnNoise = cheapChar;
    [branch]
    if (detailWeight > H8_UBER_NOIR_EPS)
    {
        float n0 = H8UberNoirValueNoise2(stableAgingPosition.xy * lerp(0.047, 0.143, detailWeight) + aupSeed);
        float n1 = H8UberNoirValueNoise2(stableAgingPosition.zy * lerp(0.081, 0.219, detailWeight) - aupSeed);
        burnNoise = lerp(cheapChar, saturate(n0 * 0.68 + n1 * 0.32), detailWeight);
    }

    half stress = saturate((half)aging.StressAndMicroFractures.x);
    half burnMask = saturate(scorch01 * ((half)burnNoise * 0.72h + stress * 0.38h));
    half3 charTint = max((half3)_NoirAbyssFloorColor.rgb * 0.18h, half3(0.018h, 0.014h, 0.012h));
    half3 hotEdgeTint = max((half3)_RustPitTint.rgb, half3(0.42h, 0.16h, 0.055h));
    surface.albedo = lerp(surface.albedo, charTint, burnMask * 0.72h);
    surface.albedo = lerp(surface.albedo, hotEdgeTint, saturate((half)burnNoise - 0.82h) * scorch01 * 0.18h);
    surface.smoothness = lerp(surface.smoothness, 0.045h, burnMask * 0.58h);
    surface.roughness = saturate(1.0h - surface.smoothness);
    surface.emission += hotEdgeTint * saturate(scorch01 - 0.72h) * saturate((half)burnNoise - 0.66h) * 0.025h;

    float normalDetailWeight = H8UberNoirSmoothRange01(0.30, 0.74, quality);
    half normalWeight = burnMask * (half)normalDetailWeight * lerp(0.18h, 0.78h, (half)H8UberNoirVisualOverkill01());
    [branch]
    if (normalWeight > 0.001h)
    {
        half3 scorchNormalTS = H8UberNoirDecodeRustNormalTS(rustPacked, normalWeight);
        float3 scorchNormalWS = H8UberNoirSafeNormalize(
            safeTangentWS * scorchNormalTS.x + safeBitangentWS * scorchNormalTS.y + safeNormalWS * scorchNormalTS.z,
            safeNormalWS);
        surface.normalWS = (half3)H8UberNoirSafeNormalize(lerp((float3)surface.normalWS, scorchNormalWS, normalWeight), safeNormalWS);
    }
}

half3 H8UberNoirResolveBiolumEmission(float3 positionWS, half emissionMask, half instanceSeed)
{
    half detailWeight = (half)(H8UberNoirSmoothRange01(0.24, 0.62, H8UberNoirGlobalQualityWeight()) * H8UberNoirHighCostAllowed());
    float phase01 = frac(_BiolumMasterPhase.x + _BiolumMasterPhase.y + instanceSeed * _UberNoirBiolumParams.w + dot(positionWS.xz, float2(0.013, -0.017)));
    half trianglePulse = (half)H8UberNoirTriangle01(phase01);
    half pulse = (half)H8UberNoirSafePow01(trianglePulse, max(_UberNoirBiolumParams.z, 0.25));
    half spectral = saturate(pulse + (pulse - 0.5h) * (half)_UberNoirBiolumParams.y);
    half3 spectralColor = lerp((half3)_BiolumLowColor.rgb, (half3)_BiolumHighColor.rgb, spectral);
    return spectralColor * (_EmissionColor.rgb * (half)_UberNoirBiolumParams.x * emissionMask * detailWeight);
}

float H8UberNoirEvaluateProceduralCaustics(float2 uv)
{
    float quality = H8UberNoirGlobalQualityWeight();
    float detailWeight = H8UberNoirSmoothRange01(0.34, 0.82, quality) * H8UberNoirHighCostAllowed();
    float speed = isfinite(_H8UberNoirCausticSpeed.x) ? max(_H8UberNoirCausticSpeed.x, 0.001) : 0.025;
    float phase = _Time.y * speed;
    float cheapPhase = dot(uv, float2(0.71, -0.43)) + phase;
    float cheap = H8UberNoirTriangle01(cheapPhase) * H8UberNoirTriangle01(cheapPhase * 1.37 + 0.19);
    if (detailWeight <= H8_UBER_NOIR_EPS)
        return cheap;

    float2 uvA = uv * lerp(14.0, 37.0, detailWeight) + phase * float2(0.031, -0.024);
    float2 uvB = uv * lerp(23.0, 61.0, detailWeight) + phase * float2(-0.019, 0.037);
    float layerA = H8UberNoirValueNoise2(uvA);
    float layerB = H8UberNoirValueNoise2(uvB);
    float sharpness = lerp(1.15, 3.25, H8UberNoirVisualOverkill01() * detailWeight);
    float rich = H8UberNoirSafePow01(layerA * layerB, sharpness);
    return lerp(cheap, rich, detailWeight);
}

half3 H8UberNoirEvaluateDeferredCausticsStub(float3 positionWS, half3 normalWS, Light mainLight)
{
    float quality = H8UberNoirGlobalQualityWeight();
    float detailWeight = H8UberNoirSmoothRange01(0.30, 0.70, quality) * H8UberNoirHighCostAllowed();
    if (detailWeight <= H8_UBER_NOIR_EPS)
        return half3(0.0h, 0.0h, 0.0h);

    float3 stablePosition = H8UberNoirMaterialStablePosition(positionWS);
    float normalMask = saturate(normalWS.y);
    float lightMask = saturate(mainLight.distanceAttenuation * lerp(1.0, mainLight.shadowAttenuation, detailWeight));
    float intensitySource = isfinite(_H8UberNoirCausticSpeed.y) ? max(_H8UberNoirCausticSpeed.y, 0.0) : 1.0;
    float intensity = normalMask * lightMask * intensitySource * detailWeight;
    if (intensity <= H8_UBER_NOIR_EPS)
        return half3(0.0h, 0.0h, 0.0h);

    float2 uv = stablePosition.xz * lerp(0.014, 0.046, detailWeight) + (float2)normalWS.xz * lerp(0.018, 0.052, detailWeight);
    float caustic = H8UberNoirEvaluateProceduralCaustics(uv);
    half3 tint = max((half3)_BiolumHighColor.rgb * 0.22h + (half3)_NoirFogColor.rgb * 0.16h, half3(0.018h, 0.052h, 0.064h));
    half gain = (half)(caustic * intensity * lerp(0.05, 0.24, H8UberNoirVisualOverkill01()));
    return tint * gain;
}

H8UberNoirSurface H8UberNoirSampleSurface(H8UberNoirVaryings input)
{
    H8UberNoirSurface surface;
    H8UberNoirMaterialStateDTO materialState = H8UberNoirLoadMaterialState(input.materialIndex);
    H8VisualAgingParamsDTO visualAging = H8UberNoirLoadVisualAging(input.degradationIndex);
    float quality = H8UberNoirVisualAgingQualityWeight();
    float3 stablePosition = H8UberNoirMaterialStablePosition(input.positionWS);
    float3 agingStablePosition = H8UberNoirVisualAgingStablePosition(input.uvPack, input.uvAux, input.instanceSeed, visualAging);
    float macroNoise = H8UberNoirMaterialMacroNoise(stablePosition, input.instanceSeed, quality);
    float wearMultiplier = max(isfinite(_H8UberNoirGlobalWearMultiplier) ? _H8UberNoirGlobalWearMultiplier : 0.0, (_H8UberNoirMaterialFlags == 0u) ? 1.0 : 0.0);
    float agingGrowthMask = H8UberNoirAgingGrowthMask(agingStablePosition, visualAging, input.instanceSeed, quality);
    float saltLinePhase = isfinite(_H8UberNoirCausticSpeed.z) ? _H8UberNoirCausticSpeed.z : 0.0;
    half dynamicRust = saturate((half)(((materialState.WearAge + H8UberNoirResolveRust01()) * (0.35 + macroNoise) * wearMultiplier) + visualAging.RustAndCorrosion.x * agingGrowthMask + visualAging.RustAndCorrosion.y * 0.42));
    half dynamicSalt = saturate((half)(materialState.SaltAccumulation * (0.45 + H8UberNoirTriangle01(stablePosition.y * 0.012 + saltLinePhase * 0.01)) + visualAging.SaltAndBiomass.x * (0.35 + agingGrowthMask * 0.65)));
    half dynamicMoss = saturate((half)(materialState.BioGrowthMask * (0.35 + macroNoise) * lerp(0.35, 1.0, quality) + visualAging.SaltAndBiomass.y * lerp(0.18, 0.84, quality)));
    half dynamicScorch = saturate((half)(visualAging.SaltAndBiomass.z * (0.42 + agingGrowthMask * 0.58) + visualAging.StressAndMicroFractures.z * 0.08));
    float2 baseUv = input.uvPack.xy;
    float2 rawUv = input.uvPack.zw;
    float2 wearUv = baseUv;
    float2 maskUv = input.uvAux.zw;
    half4 rustPacked;
    half rustMask;
    wearUv = H8UberNoirResolveRustPomUv(rawUv, baseUv, input.uvAux.xy, input.viewDirWS, input.normalWS, input.tangentWS, dynamicRust, quality, rustPacked, rustMask);
    maskUv += wearUv - baseUv;
    H8UberNoirWearVitality vitality = H8UberNoirResolveWearVitality(
        stablePosition,
        wearUv,
        dynamicRust,
        dynamicSalt,
        dynamicMoss,
        input.instanceSeed,
        quality);

    half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, wearUv) * _BaseColor;
    half4 armSample = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, maskUv);
    float textureArrayUse = ((_H8UberNoirMaterialFlags & H8_UBER_NOIR_FLAG_TEXTURE_ARRAYS) != 0u) ? 1.0 : 0.0;
    float textureArrayBlend = textureArrayUse * H8UberNoirSmoothRange01(0.30, 0.74, quality);
    [branch]
    if (textureArrayBlend > H8_UBER_NOIR_EPS)
    {
        uint cleanSlice = H8UberNoirTextureSlice(materialState.TextureSetHash, 0u);
        uint rustSlice = H8UberNoirTextureSlice(materialState.TextureSetHash, 1u);
        uint mossSlice = H8UberNoirTextureSlice(materialState.TextureSetHash, 2u);
        uint scorchSlice = H8UberNoirTextureSlice(materialState.TextureSetHash, 3u);
        half4 cleanArray = H8UberNoirSampleAlbedoArray(cleanSlice, wearUv, stablePosition, input.normalWS, quality);
        half4 rustArray = H8UberNoirSampleAlbedoArray(rustSlice, wearUv, stablePosition, input.normalWS, quality);
        half4 mossArray = H8UberNoirSampleAlbedoArray(mossSlice, wearUv, stablePosition, input.normalWS, quality);
        half4 scorchArray = H8UberNoirSampleAlbedoArray(scorchSlice, wearUv, stablePosition, input.normalWS, quality);
        half4 maskArray = SAMPLE_TEXTURE2D_ARRAY(_H8UberNoirMaskArray, sampler_H8UberNoirMaskArray, maskUv, cleanSlice);
        half4 wornArray = lerp(cleanArray, rustArray, dynamicRust);
        wornArray = lerp(wornArray, mossArray, dynamicMoss);
        wornArray = lerp(wornArray, scorchArray, dynamicScorch);
        baseSample = lerp(baseSample, wornArray * _BaseColor, (half)textureArrayBlend);
        armSample = lerp(armSample, maskArray, (half)textureArrayBlend);
    }

    half brownoutPowerLevel = H8UberNoirResolveBrownoutPower(input.positionWS, input.instanceSeed, (half)materialState.PowerLevel);
    float surfaceDetailWeight = H8UberNoirSmoothRange01(0.30, 0.56, quality);
    [branch]
    if (surfaceDetailWeight <= H8_UBER_NOIR_EPS)
    {
        half roughness = max(saturate(armSample.g), (half)_UberNoirLightingParams.y);
        half rust01 = max(H8UberNoirResolveRust01(), dynamicRust);
        half saltCrust = saturate(max(dynamicSalt, rust01 * 0.28h) * (0.55h + (half)H8UberNoirTriangle01(dot(stablePosition.xz, float2(0.071, -0.053)))));
        half3 saltTint = lerp((half3)_RustTint.rgb, (half3)_RustPitTint.rgb, saltCrust);
        half3 mossTint = max((half3)_BiolumLowColor.rgb * 0.22h, half3(0.04h, 0.11h, 0.07h));
        surface.albedo = lerp(baseSample.rgb, saltTint, saturate((saltCrust + rust01) * 0.42h));
        surface.albedo = lerp(surface.albedo, mossTint, dynamicMoss * 0.32h);
        surface.albedo = lerp(surface.albedo, max((half3)_NoirAbyssFloorColor.rgb * 0.18h, half3(0.018h, 0.014h, 0.012h)), dynamicScorch * 0.54h);
        surface.normalWS = input.normalWS;
        surface.emission = _EmissionColor.rgb * armSample.a * (half)_UberNoirLightingParams.w * brownoutPowerLevel;
        surface.metallic = 0.0h;
        surface.occlusion = saturate(lerp(1.0h, armSample.r, (half)_OcclusionStrength));
        surface.smoothness = lerp(saturate((1.0h - roughness) * (half)_Smoothness), 0.24h, saltCrust);
        surface.smoothness = lerp(surface.smoothness, 0.05h, dynamicScorch * 0.48h);
        surface.roughness = max(roughness, saturate(1.0h - surface.smoothness));
        surface.alpha = baseSample.a;
        surface.rustMask = saltCrust;
        surface.orm = armSample;
        surface.sssMask = dynamicMoss;
        surface.anisotropy = 0.0h;
        surface.powerLevel = brownoutPowerLevel;
        H8UberNoirApplyWearVitalityColor(vitality, surface);
        surface.albedo = lerp(surface.albedo, (half3)_RustPitTint.rgb, (half)visualAging.RustAndCorrosion.z * dynamicRust * 0.22h);
        H8UberNoirApplyGlassMicroFracture(visualAging, agingStablePosition, surface.normalWS, input.instanceSeed, quality, surface);
        surface.normalWS = (half3)H8UberNoirSafeNormalize(
            (float3)surface.normalWS + (float3)input.deformationNormalWS * saturate(input.dentScar),
            (float3)input.normalWS);
        return surface;
    }

    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, wearUv), (half)_BumpScale);
    [branch]
    if (textureArrayBlend > H8_UBER_NOIR_EPS)
    {
        uint normalSlice = H8UberNoirTextureSlice(materialState.TextureSetHash, 0u);
        half4 normalArray = SAMPLE_TEXTURE2D_ARRAY(_H8UberNoirNormalArray, sampler_H8UberNoirNormalArray, wearUv, normalSlice);
        normalTS = lerp(normalTS, UnpackNormalScale(normalArray, (half)_BumpScale), (half)textureArrayBlend);
    }

    float3 safeNormalWS;
    float3 safeTangentWS;
    float3 safeBitangentWS;
    H8UberNoirBuildTangentFrame(input.normalWS, input.tangentWS, safeNormalWS, safeTangentWS, safeBitangentWS);
    float3 normalWS = H8UberNoirSafeNormalize(
        safeTangentWS * normalTS.x + safeBitangentWS * normalTS.y + safeNormalWS * normalTS.z,
        safeNormalWS);

    surface.albedo = baseSample.rgb;
    surface.normalWS = (half3)normalWS;
    surface.metallic = saturate(armSample.b * (half)_Metallic);
    surface.occlusion = saturate(lerp(1.0h, armSample.r, (half)_OcclusionStrength));
    surface.roughness = max(saturate(armSample.g), (half)_UberNoirLightingParams.y);
    surface.smoothness = saturate((1.0h - surface.roughness) * (half)_Smoothness);
    surface.alpha = baseSample.a;
    surface.rustMask = max(rustMask, dynamicRust);
    surface.orm = armSample;
    surface.sssMask = saturate(dynamicMoss + armSample.a * (half)materialState.BioGrowthMask);
    surface.anisotropy = saturate(armSample.b * (half)((materialState.Flags & 1u) != 0u ? 1.0h : 0.35h));
    surface.powerLevel = brownoutPowerLevel;
    surface.emission = _EmissionColor.rgb * armSample.a * (half)_UberNoirLightingParams.w * surface.powerLevel;

    H8UberNoirApplyRustCorrosion(wearUv, agingStablePosition, input.tangentWS, rustPacked, max(rustMask, dynamicRust), surface);
    H8UberNoirApplyScorchDegradation(visualAging, agingStablePosition, rustPacked, safeNormalWS, safeTangentWS, safeBitangentWS, surface);
    surface.albedo = lerp(surface.albedo, max((half3)_BiolumLowColor.rgb * 0.24h, half3(0.035h, 0.10h, 0.055h)), dynamicMoss * 0.45h);
    surface.albedo = lerp(surface.albedo, max((half3)_NoirAbyssFloorColor.rgb, half3(0.72h, 0.78h, 0.82h)), dynamicSalt * 0.28h);
    surface.albedo = lerp(surface.albedo, (half3)_RustPitTint.rgb, (half)visualAging.RustAndCorrosion.z * max(rustMask, dynamicRust) * 0.35h);
    surface.smoothness = lerp(surface.smoothness, 0.07h, (half)visualAging.RustAndCorrosion.z * 0.28h);
    surface.roughness = saturate(1.0h - surface.smoothness);
    H8UberNoirApplyWearVitalityColor(vitality, surface);
    H8UberNoirApplyWearVitalityNormal(vitality, safeNormalWS, safeTangentWS, safeBitangentWS, surface);
    H8UberNoirApplyGlassMicroFracture(visualAging, agingStablePosition, surface.normalWS, input.instanceSeed, quality, surface);
    half dentScar = saturate(input.dentScar);
    if (dentScar > 0.001h)
    {
        half overkill = (half)H8UberNoirVisualOverkill01();
        half scratchStrength = dentScar * lerp(0.42h, 1.25h, overkill);
        half3 scratchNormalTS = H8UberNoirDecodeRustNormalTS(rustPacked, scratchStrength);
        float3 scratchNormalWS = H8UberNoirSafeNormalize(
            safeTangentWS * scratchNormalTS.x + safeBitangentWS * scratchNormalTS.y + safeNormalWS * scratchNormalTS.z,
            safeNormalWS);
        surface.normalWS = (half3)H8UberNoirSafeNormalize(lerp((float3)surface.normalWS, scratchNormalWS, dentScar), safeNormalWS);
        surface.albedo = lerp(surface.albedo, (half3)_RustPitTint.rgb, dentScar * 0.38h);
        surface.smoothness = lerp(surface.smoothness, 0.08h, dentScar * 0.72h);
        surface.metallic = lerp(surface.metallic, 0.85h, dentScar * 0.24h);
    }
    surface.normalWS = (half3)H8UberNoirSafeNormalize(
        (float3)surface.normalWS + (float3)input.deformationNormalWS * saturate(input.dentScar * lerp(0.45h, 1.25h, (half)quality)),
        safeNormalWS);
    surface.emission += H8UberNoirResolveBiolumEmission(input.positionWS, armSample.a, input.instanceSeed) * lerp(0.35h, 1.0h, surface.powerLevel);
    return surface;
}

half3 H8UberNoirEvaluateMainLighting(H8UberNoirVaryings input, H8UberNoirSurface surface)
{
    float quality = H8UberNoirGlobalQualityWeight();
    half q = (half)quality;
    half detailWeight = (half)(H8UberNoirSmoothRange01(0.20, 0.56, quality) * H8UberNoirHighCostAllowed());
    half3 normalWS = H8UberNoirSafeNormalizeHalf(surface.normalWS, half3(0.0h, 1.0h, 0.0h));
    if (detailWeight <= (half)H8_UBER_NOIR_EPS)
    {
        Light mainLight = GetMainLight();
        half3 lightDir = H8UberNoirSafeNormalizeHalf((half3)mainLight.direction, half3(0.0h, 1.0h, 0.0h));
        half nDotL = saturate(dot(normalWS, lightDir));
        half attenuation = saturate((half)mainLight.distanceAttenuation);
        half attenuationGate = (half)step(0.0001h, nDotL) * (half)step(0.0001h, attenuation);
        half3 diffuse = surface.albedo * mainLight.color * (nDotL * attenuation * attenuationGate);
        half3 ambientProbe = H8CustomLightProbeResolveAmbient(input.positionWS, normalWS, max((half3)_NoirFogColor.rgb, half3(0.0h, 0.0h, 0.0h)));
        half3 ambient = ambientProbe * surface.albedo * surface.occlusion * (half)_UberNoirLightingParams.z;
        half wrap = lerp(0.12h, 0.42h, (half)_H8UberNoirSubsurfaceColor.w);
        half wrappedDiffuse = saturate((dot(normalWS, lightDir) + wrap) / (1.0h + wrap)) * attenuation;
        half thicknessMask = saturate(surface.sssMask * (1.0h - surface.metallic));
        half3 sss = (half3)_H8UberNoirSubsurfaceColor.rgb * surface.albedo * wrappedDiffuse * thicknessMask * q * 0.25h;
        half diffuseGain = lerp(0.55h, 1.0h, 1.0h - surface.roughness);
        return ambient + diffuse * diffuseGain + sss + surface.emission;
    }

    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    Light mainLight = GetMainLight(shadowCoord);
    half3 viewDirWS = H8UberNoirSafeNormalizeHalf(input.viewDirWS, half3(0.0h, 0.0h, 1.0h));
    half3 lightDir = H8UberNoirSafeNormalizeHalf((half3)mainLight.direction, half3(0.0h, 1.0h, 0.0h));

    half nDotL = saturate(dot(normalWS, lightDir));
    half attenuationNoShadow = saturate((half)mainLight.distanceAttenuation);
    half attenuationShadowed = saturate((half)(mainLight.distanceAttenuation * mainLight.shadowAttenuation));
    half attenuation = lerp(attenuationNoShadow, attenuationShadowed, detailWeight);
    half attenuationGate = (half)step(0.0001h, nDotL) * (half)step(0.0001h, attenuation);
    half3 diffuse = surface.albedo * mainLight.color * (nDotL * attenuation * attenuationGate);

    half3 halfDir = H8UberNoirSafeNormalizeHalf(lightDir + viewDirWS, lightDir);
    half nDotH = saturate(dot(normalWS, halfDir));
    half specPower = lerp(4.0h, 64.0h, surface.smoothness);
    half specular = (half)H8UberNoirSafePow01(nDotH, specPower) * (half)_UberNoirLightingParams.x * attenuationGate * detailWeight;
    half3 tangentDir = H8UberNoirSafeNormalizeHalf(input.tangentWS.xyz, half3(1.0h, 0.0h, 0.0h));
    half tangentDotH = saturate(abs(dot(tangentDir, halfDir)));
    half anisotropicSpec = (half)H8UberNoirSafePow01(saturate(1.0h - tangentDotH * 0.72h + nDotH * 0.28h), lerp(8.0h, 96.0h, surface.smoothness));
    specular = lerp(specular, anisotropicSpec * (half)_UberNoirLightingParams.x * attenuationGate * detailWeight, surface.anisotropy * q * detailWeight);
    half3 f0 = lerp(half3(0.04h, 0.04h, 0.04h), surface.albedo, surface.metallic);
    half3 ambientProbe = H8CustomLightProbeResolveAmbient(input.positionWS, normalWS, max((half3)_NoirFogColor.rgb, half3(0.0h, 0.0h, 0.0h)));
    half3 ambient = ambientProbe * surface.albedo * surface.occlusion * (half)_UberNoirLightingParams.z;
    half wrap = lerp(0.12h, lerp(0.42h, 0.72h, detailWeight), (half)_H8UberNoirSubsurfaceColor.w);
    half wrappedDiffuse = saturate((dot(normalWS, lightDir) + wrap) / (1.0h + wrap)) * attenuation;
    half thicknessMask = saturate(surface.sssMask * (1.0h - surface.metallic));
    half sssGain = lerp(0.25h, 1.0h, detailWeight);
    half3 sss = (half3)_H8UberNoirSubsurfaceColor.rgb * surface.albedo * wrappedDiffuse * thicknessMask * q * sssGain;

    half diffuseGain = lerp(lerp(0.55h, 1.0h, 1.0h - surface.roughness), 1.0h, detailWeight);
    half3 caustics = H8UberNoirEvaluateDeferredCausticsStub(input.positionWS, normalWS, mainLight) * surface.albedo * detailWeight;
    return ambient + diffuse * diffuseGain + f0 * specular + caustics + sss + surface.emission;
}

half3 H8UberNoirResolveExtinctionColor(H8UberNoirVaryings input)
{
    half3 cheapExtinction = max(input.extinctionColor, half3(0.0h, 0.0h, 0.0h));
    half detailWeight = (half)H8UberNoirSmoothRange01(0.18, 0.46, H8UberNoirGlobalQualityWeight());
    if (detailWeight <= (half)H8_UBER_NOIR_EPS)
        return cheapExtinction;

    half3 richExtinction = H8WaterExtinctionResolveRgbByWorld(input.positionWS, (half)_ExtinctionLUTRuntime.y);
    return lerp(cheapExtinction, richExtinction, detailWeight);
}

void H8UberNoirApplyExtinctionToSurface(half3 extinctionColor, inout H8UberNoirSurface surface)
{
    half emissiveMask = saturate(surface.orm.a);
    half3 extinctAlbedo = surface.albedo * extinctionColor;
    surface.albedo = lerp(extinctAlbedo, surface.albedo, emissiveMask);
}

half3 H8UberNoirApplyNoirFog(half3 color, half fogFactor, half3 extinctionColor, float4 positionCS)
{
    half fog = saturate(fogFactor * (half)max(_NoirFogAlpha, _UberNoirDitherParams.y));
    half fogCurve = fog * fog * (0.82h + fog * 0.18h);
    fogCurve = saturate(fogCurve + H8UberNoirFogIgnDither(positionCS, fogCurve));
    half3 abyssFloor = max((half3)_NoirAbyssFloorColor.rgb, half3(0.0h, 0.0h, 0.0h));
    half3 floorColor = max((half3)_NoirFogColor.rgb, abyssFloor);
    half extinctionFogBlend = H8WaterExtinctionFogBlend();
    floorColor = H8WaterExtinctionApplyFogTint(floorColor, extinctionColor, extinctionFogBlend, floorColor, abyssFloor);
    half extinctionMax = max(extinctionColor.r, max(extinctionColor.g, extinctionColor.b));
    floorColor = lerp(floorColor, abyssFloor, saturate(fogCurve * (1.0h - extinctionMax)));
    return lerp(color, floorColor, fogCurve);
}

float2 H8UberNoirCavitationRefractionOffset(float3 positionWS, float2 screenUV)
{
    int sourceCount = max(0, min(_H8CavitationShockwaveCount, (int)max(_H8CavitationShockwaveParams.z, 0.0)));
    float globalQuality = H8UberNoirGlobalQualityWeight();
    float featureQuality = saturate(_H8CavitationShockwaveParams.x);
    float detailWeight = H8UberNoirSmoothRange01(0.42, 0.86, globalQuality) * featureQuality * H8UberNoirHighCostAllowed();
    int slotLimit = min(sourceCount, max(1, (int)ceil(lerp(1.0, 18.0, detailWeight))));
    float2 offset = float2(0.0, 0.0);

    [loop]
    for (int i = 0; i < slotLimit; i++)
    {
        H8CavitationShockwaveDTO wave = _H8CavitationShockwaves[i];
        float radius = max(wave.CenterRadius.w, 0.001);
        float3 delta = positionWS - wave.CenterRadius.xyz;
        float distanceSq = max(dot(delta, delta), 0.0001);
        float age = saturate(wave.IntensityAgeQualityFlags.y);
        float intensity = saturate(wave.IntensityAgeQualityFlags.x * _H8CavitationShockwaveParams.y);
        float radiusSq = max(radius * radius, 0.0001);
        float shellWidthSq = max(0.45, radius * lerp(0.18, 0.055, detailWeight));
        shellWidthSq = max(shellWidthSq * max(radius * 2.0, 0.25), 0.0001);
        float shellSq = saturate(1.0 - abs(distanceSq - radiusSq) * H8UberNoirSafeRcp(shellWidthSq));
        float shell = shellSq;
        [branch]
        if (detailWeight > H8_UBER_NOIR_EPS)
        {
            float distance = distanceSq * H8UberNoirSafeRsqrt(distanceSq);
            float shellWidth = max(0.45, radius * lerp(0.10, 0.035, detailWeight));
            float preciseShell = saturate(1.0 - abs(distance - radius) * H8UberNoirSafeRcp(shellWidth));
            shell = lerp(shellSq, preciseShell, detailWeight);
        }

        float fade = saturate(1.0 - age);
        float3 radial = delta * H8UberNoirSafeRsqrt(distanceSq);
        float cheapCurl = H8UberNoirTriangle01(dot(delta.xz, wave.CurlPhase.xy) * 0.017 + wave.CurlPhase.z * 0.23 + _H8CavitationShockwaveParams.w * 0.011) * 2.0 - 1.0;
        float curl = cheapCurl;
        [branch]
        if (detailWeight > 0.35)
        {
            float richCurl = sin((dot(delta.xz, wave.CurlPhase.xy) + wave.CurlPhase.z * 37.0 + _H8CavitationShockwaveParams.w * 0.071) * 0.19);
            curl = lerp(cheapCurl, richCurl, detailWeight);
        }

        float2 radial2 = H8UberNoirSafeNormalize(float3(radial.x, radial.z, 0.0), float3(1.0, 0.0, 0.0)).xy;
        float2 tangent2 = float2(-radial2.y, radial2.x);
        offset += (radial2 + tangent2 * curl * lerp(0.18, 0.42, detailWeight)) * (shell * fade * intensity * lerp(0.0011, 0.0048, detailWeight));
    }

    return clamp(offset, float2(-0.025, -0.025), float2(0.025, 0.025));
}

half3 H8UberNoirApplyScreenRefraction(H8UberNoirVaryings input, H8UberNoirSurface surface, half3 color)
{
    float cavitationActive = saturate((float)_H8CavitationShockwaveCount) * saturate(_H8CavitationShockwaveParams.y);
    float qualityWeight = H8UberNoirSmoothRange01(0.30, 0.76, H8UberNoirGlobalQualityWeight());
    float materialRefractionActive = saturate(_UberNoirRefractionParams.x * 32.0) * saturate(_UberNoirRefractionParams.z * 32.0);
    float cavitationRefractionActive = cavitationActive * saturate(_UberNoirRefractionParams.z * 32.0);
    float active = max(materialRefractionActive, cavitationRefractionActive) * H8UberNoirHighCostAllowed() * qualityWeight;
    [branch]
    if (active <= H8_UBER_NOIR_EPS)
        return color;

    float2 screenUV = H8UberNoirScreenUV(input.positionCS);
    float3 safeNormal = H8UberNoirSafeNormalize((float3)surface.normalWS, float3(0.0, 1.0, 0.0));
    float3 safeView = H8UberNoirSafeNormalize((float3)input.viewDirWS, float3(0.0, 0.0, 1.0));
    float nDotV = saturate(dot(safeNormal, safeView));
    float2 snellOffset = HectonSnellUvOffset(
        safeNormal.xy,
        nDotV,
        saturate(_UberNoirRefractionParams.y),
        _UberNoirIorLut,
        _UberNoirRefractionParams.x,
        active,
        1.0);
    float2 cavitationOffset = H8UberNoirCavitationRefractionOffset(input.positionWS, screenUV) * active;
    float2 refractedUV = saturate(screenUV + snellOffset + cavitationOffset);
    half3 refractedColor = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, refractedUV).rgb;
    float chromatic = saturate(_UberNoirRefractionParams.w) * active;
    [branch]
    if (chromatic > H8_UBER_NOIR_EPS)
    {
        float2 chromaOffset = (snellOffset + cavitationOffset) * chromatic * 0.45;
        half3 chromaColor = refractedColor;
        chromaColor.r = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, saturate(screenUV + chromaOffset)).r;
        chromaColor.b = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, saturate(screenUV - chromaOffset)).b;
        refractedColor = lerp(refractedColor, chromaColor, chromatic);
    }
    return lerp(color, max(refractedColor, (half3)_NoirAbyssFloorColor.rgb), saturate(_UberNoirRefractionParams.z) * active);
}

half3 H8UberNoirApplyRespawnDearLie(H8UberNoirVaryings input, half3 color)
{
    float fade = saturate(max(_HectonDeathFadeIntensity, _HectonRespawnDearLieParams.x));
    [branch]
    if (fade <= H8_UBER_NOIR_EPS)
        return color;

    float quality = saturate(max(H8UberNoirGlobalQualityWeight(), _HectonRespawnDearLieParams.w));
    float detailWeight = H8UberNoirSmoothRange01(0.18, 0.72, quality) * H8UberNoirHighCostAllowed();
    float2 screenCell = floor(H8UberNoirScreenUV(input.positionCS) * lerp(96.0, 384.0, detailWeight));
    half grain = (half)((H8UberNoirHash12(screenCell + _Time.yy * 17.0) - 0.5) * fade * _HectonRespawnDearLieParams.z * detailWeight * lerp(0.018, 0.065, quality));
    half3 abyss = (half3)max(_NoirAbyssFloorColor.rgb, float3(0.0, 0.0, 0.0));
    float chroma = saturate(_HectonRespawnDearLieParams.y) * detailWeight;
    half3 chromaColor = half3(
        color.r * (half)(1.0 + 0.10 * chroma),
        color.g,
        color.b * (half)(1.0 - 0.08 * chroma));
    half3 lieColor = abyss + half3(0.0h, 0.004h, 0.006h) * (half)(quality * detailWeight);
    return lerp(chromaColor + grain, lieColor, (half)saturate(fade * lerp(0.86, 0.98, quality)));
}

half4 H8UberNoirFragment(H8UberNoirVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    H8UberNoirSurface surface = H8UberNoirSampleSurface(input);
    surface.alpha *= saturate(input.instanceFade);
    H8UberNoirClipDitheredTransparency(surface.alpha, input.positionCS);
    [branch]
    if (((_H8UberNoirMaterialFlags & H8_UBER_NOIR_FLAG_DEBUG_HEATMAP) != 0u) && _H8UberNoirDebugMode != 0u)
    {
        H8UberNoirMaterialStateDTO debugState = H8UberNoirLoadMaterialState(input.materialIndex);
        return half4((half)debugState.WearAge, (half)debugState.BioGrowthMask, (half)debugState.SaltAccumulation, 1.0h);
    }
    half3 extinctionColor = H8UberNoirResolveExtinctionColor(input);
    half3 color = H8UberNoirEvaluateMainLighting(input, surface);
    Light waterOpticsMainLight = GetMainLight();
    color = H8WaterOpticsApplyBeerLambert(
        color,
        input.positionWS,
        input.viewDirWS,
        (half3)waterOpticsMainLight.direction,
        (half3)waterOpticsMainLight.color,
        extinctionColor);
    color = H8UberNoirApplyNoirFog(color, input.fogFactor, extinctionColor, input.positionCS);
    color = H8UberNoirApplyScreenRefraction(input, surface, color);
    color = H8UberNoirApplyRespawnDearLie(input, color);
    half3 abyssFloor = (half3)_NoirAbyssFloorColor.rgb;
    color = all(isfinite(color)) ? max(color, abyssFloor) : abyssFloor;
    return half4(color, 1.0h);
}

#if defined(H8_UBERNOIR_MOTION_VECTOR_PASS)
struct H8UberNoirMotionVaryings
{
    float4 positionCS : SV_POSITION;
    float4 positionCSNoJitter : POSITION_CS_NO_JITTER;
    float4 previousPositionCSNoJitter : PREV_POSITION_CS_NO_JITTER;
    float2 baseUv : TEXCOORD0;
    half instanceFade : TEXCOORD1;
    UNITY_VERTEX_OUTPUT_STEREO
};

H8UberNoirMotionVaryings H8UberNoirMotionVertex(H8UberNoirAttributes input)
{
    H8UberNoirMotionVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    uint resolvedInstanceID = input.instanceID;
#if UNITY_ANY_INSTANCING_ENABLED
    resolvedInstanceID = unity_InstanceID;
#endif

    H8UberNoirInstanceData instanceData = H8UberNoirLoadInstance(resolvedInstanceID);
    float3 positionOS = H8UberNoirFinite3(input.positionOS.xyz, float3(0.0, 0.0, 0.0));
    float3 normalOS = H8UberNoirSafeNormalize(input.normalOS, float3(0.0, 1.0, 0.0));
    float instanceSeedSource = instanceData.SeedFadeFlags.x + _UberNoirInstanceParams.w;
    float safeInstanceSeed = isfinite(instanceSeedSource) ? instanceSeedSource : 0.0;
    float instanceFadeSource = instanceData.SeedFadeFlags.y;
    float safeInstanceFade = isfinite(instanceFadeSource) ? saturate(instanceFadeSource) : 1.0;
    float3 dentedPositionOS = H8UberNoirApplyHullDentsOS(positionOS, normalOS);
    dentedPositionOS = H8UberNoirApplyBulkheadClosureOS(dentedPositionOS, normalOS, resolvedInstanceID);
    dentedPositionOS = H8UberNoirApplyHandRadiationMutationOS(dentedPositionOS, normalOS, safeInstanceSeed);

    float4x4 currentObjectToRuntimeWorld = H8UberNoirObjectToRuntimeWorld(instanceData.ObjectToWorld);
    float4x4 previousObjectToRuntimeWorld = H8UberNoirObjectToRuntimeWorld(UNITY_PREV_MATRIX_M);
    float3 normalWS = H8UberNoirTransformNormal(normalOS, instanceData.WorldToObject);
    float3 previousNormalWS = H8UberNoirTransformNormal(normalOS, UNITY_PREV_MATRIX_I_M);
    float3 currentPositionWS = mul(currentObjectToRuntimeWorld, float4(dentedPositionOS, 1.0)).xyz;
    currentPositionWS = H8UberNoirApplyDynamicHullBendingWS(currentPositionWS, normalWS, (half)safeInstanceSeed);
    H8UberNoirApplyGlobalWakeWS(currentPositionWS, normalWS, (half)safeInstanceSeed);
    float3 previousPositionWS = mul(previousObjectToRuntimeWorld, float4(dentedPositionOS, 1.0)).xyz;
    previousPositionWS = H8UberNoirApplyDynamicHullBendingWS(previousPositionWS, previousNormalWS, (half)safeInstanceSeed);
    H8UberNoirApplyGlobalWakeWS(previousPositionWS, previousNormalWS, (half)safeInstanceSeed);

    output.positionCS = TransformWorldToHClip(currentPositionWS);
    output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, float4(currentPositionWS, 1.0));
    output.previousPositionCSNoJitter = mul(_PrevViewProjMatrix, float4(previousPositionWS, 1.0));
    output.baseUv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
    output.instanceFade = (half)safeInstanceFade;
    ApplyMotionVectorZBias(output.positionCS);
    return output;
}

half4 H8UberNoirMotionFragment(H8UberNoirMotionVaryings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUv).a * (half)_BaseColor.a * saturate(input.instanceFade);
    H8UberNoirClipDitheredTransparency(alpha, input.positionCS);
    return half4(CalcNdcMotionVectorFromCsPositions(input.positionCSNoJitter, input.previousPositionCSNoJitter), 0.0h, 0.0h);
}
#endif

#if defined(H8_UBERNOIR_SHADOW_CASTER_PASS)
struct H8UberNoirShadowVaryings
{
    float4 positionCS : SV_POSITION;
    float2 baseUv : TEXCOORD0;
    half instanceFade : TEXCOORD1;
};

H8UberNoirShadowVaryings H8UberNoirShadowVertex(H8UberNoirAttributes input)
{
    H8UberNoirShadowVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);

    uint resolvedInstanceID = input.instanceID;
#if UNITY_ANY_INSTANCING_ENABLED
    resolvedInstanceID = unity_InstanceID;
#endif

    H8UberNoirInstanceData instanceData = H8UberNoirLoadInstance(resolvedInstanceID);
    float4x4 objectToRuntimeWorld = H8UberNoirObjectToRuntimeWorld(instanceData.ObjectToWorld);
    float3 positionOS = H8UberNoirFinite3(input.positionOS.xyz, float3(0.0, 0.0, 0.0));
    float3 normalOS = H8UberNoirSafeNormalize(input.normalOS, float3(0.0, 1.0, 0.0));
    float instanceSeedSource = instanceData.SeedFadeFlags.x + _UberNoirInstanceParams.w;
    float safeInstanceSeed = isfinite(instanceSeedSource) ? instanceSeedSource : 0.0;
    float instanceFadeSource = instanceData.SeedFadeFlags.y;
    float safeInstanceFade = isfinite(instanceFadeSource) ? saturate(instanceFadeSource) : 1.0;

    float3 dentedPositionOS = H8UberNoirApplyHullDentsOS(positionOS, normalOS);
    dentedPositionOS = H8UberNoirApplyBulkheadClosureOS(dentedPositionOS, normalOS, resolvedInstanceID);
    dentedPositionOS = H8UberNoirApplyHandRadiationMutationOS(dentedPositionOS, normalOS, safeInstanceSeed);
    float3 positionWS = mul(objectToRuntimeWorld, float4(dentedPositionOS, 1.0)).xyz;
    float3 normalWS = H8UberNoirTransformNormal(normalOS, instanceData.WorldToObject);
    positionWS = H8UberNoirApplyDynamicHullBendingWS(positionWS, normalWS, (half)safeInstanceSeed);
    H8UberNoirApplyGlobalWakeWS(positionWS, normalWS, (half)safeInstanceSeed);

#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirectionWS = H8UberNoirSafeNormalize(_LightPosition - positionWS, _LightDirection);
#else
    float3 lightDirectionWS = _LightDirection;
#endif

    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    output.positionCS = ApplyShadowClamping(positionCS);
    output.baseUv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
    output.instanceFade = (half)safeInstanceFade;
    return output;
}

half4 H8UberNoirShadowFragment(H8UberNoirShadowVaryings input) : SV_Target
{
    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUv).a * (half)_BaseColor.a * saturate(input.instanceFade);
    H8UberNoirClipDitheredTransparency(alpha, input.positionCS);
    return half4(0.0h, 0.0h, 0.0h, 0.0h);
}
#endif

H8UberNoirVaryings H8UberNoirVertex(H8UberNoirAttributes input)
{
    H8UberNoirVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    uint resolvedInstanceID = input.instanceID;
#if UNITY_ANY_INSTANCING_ENABLED
    resolvedInstanceID = unity_InstanceID;
#endif

    H8UberNoirInstanceData instanceData = H8UberNoirLoadInstance(resolvedInstanceID);
    float4x4 objectToRuntimeWorld = H8UberNoirObjectToRuntimeWorld(instanceData.ObjectToWorld);
    float3 positionOS = H8UberNoirFinite3(input.positionOS.xyz, float3(0.0, 0.0, 0.0));
    float3 normalOS = H8UberNoirSafeNormalize(input.normalOS, float3(0.0, 1.0, 0.0));
    float instanceSeedSource = instanceData.SeedFadeFlags.x + _UberNoirInstanceParams.w;
    float safeInstanceSeed = isfinite(instanceSeedSource) ? instanceSeedSource : 0.0;
    float instanceFadeSource = instanceData.SeedFadeFlags.y;
    float safeInstanceFade = isfinite(instanceFadeSource) ? saturate(instanceFadeSource) : 1.0;
    float dentScarOS = H8UberNoirEvaluateHullDentScarOS(positionOS);
    float3 deformationNormalBiasOS = H8UberNoirEvaluateDeformationNormalBiasOS(positionOS, normalOS);
    positionOS = H8UberNoirApplyHullDentsOS(positionOS, normalOS);
    positionOS = H8UberNoirApplyBulkheadClosureOS(positionOS, normalOS, resolvedInstanceID);
    positionOS = H8UberNoirApplyHandRadiationMutationOS(positionOS, normalOS, safeInstanceSeed);
    float3 positionWS = mul(objectToRuntimeWorld, float4(positionOS, 1.0)).xyz;
    float3 normalWS = H8UberNoirTransformNormal(normalOS, instanceData.WorldToObject);
    float3 deformationNormalBiasWS = H8UberNoirFinite3(mul((float3x3)objectToRuntimeWorld, deformationNormalBiasOS), float3(0.0, 0.0, 0.0));
    positionWS = H8UberNoirApplyDynamicHullBendingWS(positionWS, normalWS, (half)safeInstanceSeed);
    H8UberNoirApplyGlobalWakeWS(positionWS, normalWS, (half)safeInstanceSeed);

    float3 tangentWS = H8UberNoirSafeNormalize(mul((float3x3)objectToRuntimeWorld, input.tangentOS.xyz), float3(1.0, 0.0, 0.0));
    float3 viewDirWS = H8UberNoirSafeNormalize(GetWorldSpaceViewDir(positionWS), float3(0.0, 0.0, 1.0));
    normalWS = (float3)H8UberNoirApplyBentHullNormalBiasWS((half3)normalWS, (half3)viewDirWS);
    output.positionWS = positionWS;
    output.positionCS = TransformWorldToHClip(positionWS);
    output.normalWS = (half3)normalWS;
    output.deformationNormalWS = (half3)deformationNormalBiasWS;
    output.tangentWS = half4((half3)tangentWS, input.tangentOS.w);
    output.viewDirWS = (half3)viewDirWS;
    float2 rawUv = input.uv;
    output.uvPack = float4(rawUv * _BaseMap_ST.xy + _BaseMap_ST.zw, rawUv);
    output.uvAux = float4(_BaseMap_ST.xy, rawUv * _MaskMap_ST.xy + _MaskMap_ST.zw);
    output.fogFactor = ComputeFogFactor(output.positionCS.z);
    output.dentScar = (half)dentScarOS;
    output.materialIndex = min(resolvedInstanceID, (uint)(H8_UBER_NOIR_MATERIAL_CAPACITY - 1));
    output.degradationIndex = H8UberNoirResolveDegradationIndex(instanceData, resolvedInstanceID);
    output.instanceSeed = (half)saturate(frac(safeInstanceSeed));
    output.instanceFade = (half)safeInstanceFade;
    output.extinctionColor = H8WaterExtinctionResolveRgbByWorld(positionWS, (half)_ExtinctionLUTRuntime.y);
    return output;
}

#endif
