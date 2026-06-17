import re

with open('C:/hades/Hecton8/Assets/_Project/Shaders/HectonTerrainLitPasses.hlsl', 'r') as f:
    content = f.read()

# Replace the SplatmapFragment definition up to ENDHLSL or end of file
old_func_regex = r'(GBufferFragOutput|void) SplatmapFragment\(.*?^}'
new_func = '''void SplatmapFragment(
    Varyings IN
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
    )
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
#ifdef _ALPHATEST_ON
    ClipHoles(IN.uvMainAndLM.xy);
#endif

    half3 normalWS = half3(0, 1, 0);
#if defined(_NORMALMAP) && !defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
    normalWS = IN.normal.xyz;
#else
    normalWS = normalize(IN.normal);
#endif

    TerrainSample ts = SampleHectonTerrain(IN.uvMainAndLM.xy, IN.uvMainAndLM.xy, IN.positionWS, normalWS);

    half3 albedo = ts.albedo;
    half3 normalTS = ts.normalTS;
    half metallic = ts.metallic;
    half smoothness = ts.smoothness;
    half occlusion = ts.ao;
    half alpha = 1.0;

#if defined(_NORMALMAP) && !defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
    float3 viewDirWS = half3(IN.normal.w, IN.tangent.w, IN.bitangent.w);
    normalWS = TransformTangentToWorld(normalTS, half3x3(IN.tangent.xyz, IN.bitangent.xyz, IN.normal.xyz));
#else
    float3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
#endif

    normalWS = NormalizeNormalPerPixel(normalWS);

    InputData inputData;
    InitializeInputData(IN, normalTS, inputData);
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = viewDirWS;

    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = albedo;
    surfaceData.metallic = metallic;
    surfaceData.smoothness = smoothness;
    surfaceData.normalTS = normalTS;
    surfaceData.occlusion = occlusion;
    surfaceData.alpha = alpha;

#ifdef _ALPHATEST_ON
    surfaceData.alpha = alpha;
    clip(surfaceData.alpha - 0.5);
#endif

    outColor = UniversalFragmentPBR(inputData, surfaceData);
    outColor.rgb = MixFog(outColor.rgb, inputData.fogCoord);

#ifdef _WRITE_RENDERING_LAYERS
    uint renderingLayers = GetMeshRenderingLayer();
    outRenderingLayers = outRenderingLayersSetup(outColor, renderingLayers);
#endif
}
'''

content = re.sub(old_func_regex, new_func, content, flags=re.MULTILINE|re.DOTALL)

with open('C:/hades/Hecton8/Assets/_Project/Shaders/HectonTerrainLitPasses.hlsl', 'w') as f:
    f.write(content)
