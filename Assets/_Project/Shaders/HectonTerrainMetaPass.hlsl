#ifndef HECTON_TERRAIN_META_PASS_INCLUDED
#define HECTON_TERRAIN_META_PASS_INCLUDED

// Custom Meta pass for HECTON terrain: reads albedo from _AlbedoArray via
// SampleHectonTerrain() instead of stock URP _Splat0..3 (which are unbound).
// Without this, lightmap baking produces BLACK terrain.
//
// We define our own Attributes/Varyings because the stock URP UniversalMetaPass
// Varyings struct does NOT include positionWS, which SampleHectonTerrain needs
// for biplanar projection and distance-based LOD.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
#include "HectonTerrainSampling.hlsl"

struct HectonMetaAttributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float2 uv0          : TEXCOORD0;
    float2 uv1          : TEXCOORD1;
    float2 uv2          : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct HectonMetaVaryings
{
    float4 positionCS   : SV_POSITION;
    float2 uv           : TEXCOORD0;
    float3 positionWS   : TEXCOORD3;
#ifdef EDITOR_VISUALIZATION
    float2 VizUV        : TEXCOORD1;
    float4 LightCoord   : TEXCOORD2;
#endif
};

HectonMetaVaryings TerrainVertexMeta(HectonMetaAttributes input)
{
    HectonMetaVaryings output = (HectonMetaVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    TerrainInstancing(input.positionOS, input.normalOS, input.uv0);
    input.uv1 = input.uv2 = input.uv0;

    output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1, input.uv2);
    output.uv = input.uv0; // terrain control UV (0..1 across tile)
    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);

#ifdef EDITOR_VISUALIZATION
    UnityEditorVizData(input.positionOS.xyz, input.uv0, input.uv1, input.uv2, output.VizUV, output.LightCoord);
#endif
    return output;
}

half4 TerrainFragmentMeta(HectonMetaVaryings input) : SV_Target
{
#ifdef _ALPHATEST_ON
    ClipHoles(input.uv);
#endif

    float2 controlUV = input.uv;
    float3 worldPos = input.positionWS;
    float3 worldNormal = float3(0, 1, 0); // Meta pass: flat normal is sufficient for albedo extraction
    float3 viewDirTS = float3(0, 0, 1);

    TerrainSample ts = SampleHectonTerrain(controlUV, controlUV, worldPos, worldNormal, viewDirTS);

    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = ts.albedo;
    surfaceData.metallic = ts.metallic;
    surfaceData.smoothness = ts.smoothness;
    surfaceData.alpha = 1.0;

    BRDFData brdfData;
    InitializeBRDFData(surfaceData.albedo, surfaceData.metallic, half3(0, 0, 0), surfaceData.smoothness, surfaceData.alpha, brdfData);

    MetaInput metaInput;
    metaInput.Albedo = brdfData.diffuse + brdfData.specular * brdfData.roughness * 0.5;
    metaInput.Emission = half3(0, 0, 0);
#ifdef EDITOR_VISUALIZATION
    metaInput.VizUV = input.VizUV;
    metaInput.LightCoord = input.LightCoord;
#endif
    return UnityMetaFragment(metaInput);
}

#endif
