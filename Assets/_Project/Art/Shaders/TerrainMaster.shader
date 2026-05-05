Shader "HECTON/Terrain/TerrainMaster"
{
    Properties
    {
        [Header(Sand Layer)]
        _SandTex        ("Sand Albedo (RGB) Smooth (A)", 2D) = "white" {}
        _SandScale      ("Sand Tiling", Float) = 0.1
        _SandColor      ("Sand Tint", Color) = (0.86, 0.78, 0.62, 1)

        [Header(Rock Layer)]
        _RockTex        ("Rock Albedo (RGB) Smooth (A)", 2D) = "gray" {}
        _RockScale      ("Rock Tiling", Float) = 0.1
        _RockColor      ("Rock Tint", Color) = (0.5, 0.5, 0.5, 1)
        _RockNormal     ("Rock Normal Map", 2D) = "bump" {}
        _RockNormalStr  ("Rock Normal Strength", Range(0,2)) = 1.0

        [Header(Texture Array Packing)]
        [NoScaleOffset] _TerrainAlbedoArray ("Terrain Albedo Array", 2DArray) = "" {}
        [NoScaleOffset] _TerrainNormalArray ("Terrain Normal Array", 2DArray) = "" {}
        _TerrainLayerCount ("Terrain Array Layer Count", Float) = 8
        _BiomeLayerStride ("Biome Layer Stride", Float) = 2
        _SandLayerIndex ("Sand Layer Offset", Float) = 0
        _RockLayerIndex ("Rock Layer Offset", Float) = 1

        [Header(Blending)]
        _SlopeSharpness ("Slope Blend Sharpness", Range(1,32)) = 8.0
        _TriplanarThreshold ("Triplanar Slope Threshold", Range(0.01, 0.99)) = 0.3

        [Header(Biome)]
        _BiomeTint      ("Biome Tint", Color) = (0.15, 0.12, 0.1, 1)
        _BiomeEdgeBleedScale ("Biome Edge Bleed Scale", Float) = 0.018
        _BiomeEdgeBleedStrength ("Biome Edge Bleed Strength", Range(0,1)) = 0.38

        [Header(Depth)]
        _DarkenPower    ("Depth Darken Power", Range(0.1,10)) = 2.5
        _DepthColor     ("Abyss Color", Color) = (0.02, 0.04, 0.08, 1)

        [Header(Cave Glow)]
        _CaveGlowColor  ("Cave Glow Color", Color) = (0.2, 0.8, 1.0, 1)
        _CaveGlowPower  ("Cave Glow Intensity", Range(0,10)) = 3.0
        _NoirSiltPulseColor ("Noir Silt Pulse Color", Color) = (0.0, 0.38, 0.55, 1)
        _NoirSiltPulseStrength ("Noir Silt Pulse Strength", Range(0,1)) = 0.06
        _NoirSiltPulseScale ("Noir Silt Pulse Scale", Float) = 0.045

        [Header(Surface)]
        _Metallic       ("Metallic", Range(0,1)) = 0.0
        _BaseSmooth     ("Smoothness", Range(0,1)) = 0.5

        [Header(Planetary Canvas)]
        _FadeDistance   ("AUP Fade Distance", Float) = 2600
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        // ================================================================
        // SHARED HLSL â€” included in every pass automatically
        // ================================================================
        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // -------------------------------------------------------------
        // SRP Batcher: ALL material properties in ONE contiguous CBUFFER.
        // No _ST floats needed â€” we use world-space UVs with _Scale.
        // -------------------------------------------------------------
        CBUFFER_START(UnityPerMaterial)
            float  _SandScale;
            float  _RockScale;
            float  _RockNormalStr;
            float  _TerrainLayerCount;
            float  _BiomeLayerStride;
            float  _SandLayerIndex;
            float  _RockLayerIndex;
            float  _SlopeSharpness;
            float  _TriplanarThreshold;
            float  _BiomeEdgeBleedScale;
            float  _BiomeEdgeBleedStrength;
            float  _DarkenPower;
            float  _CaveGlowPower;
            float  _FadeDistance;
            float  _NoirSiltPulseStrength;
            float  _NoirSiltPulseScale;
            half   _Metallic;
            half   _BaseSmooth;
            half4  _SandColor;
            half4  _RockColor;
            half4  _BiomeTint;
            half4  _DepthColor;
            half4  _CaveGlowColor;
            half4  _NoirSiltPulseColor;
        CBUFFER_END

        // Textures declared outside CBUFFER (URP requirement)
        TEXTURE2D(_SandTex);    SAMPLER(sampler_SandTex);
        TEXTURE2D(_RockTex);    SAMPLER(sampler_RockTex);
        TEXTURE2D(_RockNormal); SAMPLER(sampler_RockNormal);
        TEXTURE2D_ARRAY(_TerrainAlbedoArray); SAMPLER(sampler_TerrainAlbedoArray);
        TEXTURE2D_ARRAY(_TerrainNormalArray); SAMPLER(sampler_TerrainNormalArray);
        TEXTURE2D(_HectonDistantTerrainShadowMask); SAMPLER(sampler_HectonDistantTerrainShadowMask);
        float4 _SargassumCanopyShadowParams;
        float4 _SargassumCanopyLightingParams;
        float4 _HectonTerrainFadeParams;
        float4 _HectonTerrainFadeRuntimeOrigin;
        float4 _HectonTerrainFadeAupOrigin;
        float4 _HectonDistantTerrainShadowRect;
        float4 _HectonDistantTerrainShadowParams;

        // -------------------------------------------------------------
        // Texture array slice: vertex alpha selects biome, vertex red slope selects material offset.
        // -------------------------------------------------------------
        float HectonResolveTerrainArraySlice(half slopeBlend, half biome)
        {
            float safeLayerCount = max(1.0, _TerrainLayerCount);
            float safeStride = max(1.0, _BiomeLayerStride);
            float biomeCount = max(1.0, floor(safeLayerCount / max(safeStride, 0.0001)));
            float biomeBase = round(saturate(biome) * (biomeCount - 1.0)) * safeStride;
            float materialOffset = lerp(_SandLayerIndex, _RockLayerIndex, step(0.5h, slopeBlend));
            return clamp(biomeBase + materialOffset, 0.0, safeLayerCount - 1.0);
        }

        // -------------------------------------------------------------
        // ADAPTIVE sampling: cheap 2D on flats, full triplanar on slopes.
        // triplanarBlend: 0 = flat (XZ only), 1 = full triplanar
        // -------------------------------------------------------------
        half4 HectonSampleAdaptive(
            TEXTURE2D_PARAM(tex, samp),
            float3 posWS,
            float3 weights,
            float  scale,
            half   triplanarBlend)
        {
            // Always sample XZ plane (Y-weight projection) â€” cheapest
            half4 ySample = half4(1.0h, 1.0h, 1.0h, 1.0h);

            // Early out for flat terrain â€” single texture read
            if (triplanarBlend < 0.001)
                return ySample;

            // Full triplanar for steep surfaces
            half4 xSample = ySample;
            half4 zSample = ySample;
            half4 triResult = xSample * weights.x + ySample * weights.y + zSample * weights.z;

            return lerp(ySample, triResult, triplanarBlend);
        }

        // -------------------------------------------------------------
        // ADAPTIVE normal sampling: same logic
        // Returns offset normal in world space (not normalized)
        // -------------------------------------------------------------
        half3 HectonSampleAdaptiveNormal(
            TEXTURE2D_PARAM(tex, samp),
            float3 posWS,
            float3 weights,
            float  scale,
            float  strength,
            half   triplanarBlend)
        {
            // XZ plane normal (Y-weight)
            half3 ny = half3(0.0h, 0.0h, 1.0h);
            ny = half3(ny.x, 0.0, ny.y);

            if (triplanarBlend < 0.001)
                return ny;

            // ZY plane normal (X-weight)
            half3 nx = half3(0.0h, 0.0h, 1.0h);
            nx = half3(0.0, nx.y, nx.x);

            // XY plane normal (Z-weight)
            half3 nz = half3(0.0h, 0.0h, 1.0h);
            nz = half3(nz.x, nz.y, 0.0);

            half3 triNormal = nx * weights.x + ny * weights.y + nz * weights.z;

            return lerp(ny, triNormal, triplanarBlend);
        }

        void HectonResolveTerrainProjection(float3 posWS, float3 normalWS, float scale, out float2 uv, out half dominantAxis)
        {
            half3 absNormal = saturate(abs(normalWS));
            if (absNormal.x >= absNormal.y && absNormal.x >= absNormal.z)
            {
                uv = posWS.zy * scale;
                dominantAxis = 0.0h;
            }
            else if (absNormal.z >= absNormal.y)
            {
                uv = posWS.xy * scale;
                dominantAxis = 2.0h;
            }
            else
            {
                uv = posWS.xz * scale;
                dominantAxis = 1.0h;
            }
        }

        half4 HectonSampleTerrainAlbedoArray(float3 posWS, float3 normalWS, float scale, float slice)
        {
            float2 uv;
            half dominantAxis;
            HectonResolveTerrainProjection(posWS, normalWS, scale, uv, dominantAxis);
            return SAMPLE_TEXTURE2D_ARRAY(_TerrainAlbedoArray, sampler_TerrainAlbedoArray, uv, slice);
        }

        half3 HectonSampleTerrainNormalArray(float3 posWS, float3 normalWS, float scale, float strength, float slice)
        {
            float2 uv;
            half dominantAxis;
            HectonResolveTerrainProjection(posWS, normalWS, scale, uv, dominantAxis);

            half3 tangentNormal = UnpackNormalScale(
                SAMPLE_TEXTURE2D_ARRAY(_TerrainNormalArray, sampler_TerrainNormalArray, uv, slice), strength);
            half3 normalSign = sign((half3)normalWS);
            if (dominantAxis < 0.5h)
                return half3(tangentNormal.z * normalSign.x, tangentNormal.y, tangentNormal.x);

            if (dominantAxis > 1.5h)
                return half3(tangentNormal.x, tangentNormal.y, tangentNormal.z * normalSign.z);

            return half3(tangentNormal.x, tangentNormal.z * normalSign.y, tangentNormal.y);
        }

        half EvaluateSargassumCanopyShadow(float3 positionWS)
        {
            if (_SargassumCanopyLightingParams.w < 0.5)
                return 0.0h;

            float2 delta = positionWS.xz - _SargassumCanopyShadowParams.xy;
            float distance01 = length(delta) * _SargassumCanopyShadowParams.z;
            half radialFalloff = saturate(1.0 - distance01);
            radialFalloff *= radialFalloff;
            half canopyWindow = saturate(_SargassumCanopyLightingParams.z);
            half canopyOcclusion = saturate(_SargassumCanopyShadowParams.w) * (1.0h - canopyWindow * 0.55h);
            return radialFalloff * canopyOcclusion;
        }

        half EvaluatePlanetaryTerrainFade(float3 positionWS)
        {
            if (_HectonTerrainFadeParams.z < 0.5)
                return 1.0h;

            float fadeDistance = max(max(_FadeDistance, _HectonTerrainFadeParams.x), 1.0);
            float fadeWidth = max(1.0 / max(_HectonTerrainFadeParams.y, 0.0001), 1.0);
            float distanceXZ = distance(positionWS.xz, _HectonTerrainFadeRuntimeOrigin.xz);
            float fadeStart = max(0.0, fadeDistance - fadeWidth);
            return (half)(1.0 - smoothstep(fadeStart, fadeDistance, distanceXZ));
        }

        half HectonInterleavedGradientNoise(float2 pixel)
        {
            return (half)frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
        }

        half ApplyPlanetaryDitherFade(half fade, float4 positionCS)
        {
            half edgeBand = saturate(1.0h - abs(fade * 2.0h - 1.0h));
            half noise = HectonInterleavedGradientNoise(positionCS.xy);
            half strength = saturate((half)_HectonTerrainFadeParams.w);
            return saturate(fade + (noise - 0.5h) * edgeBand * strength);
        }

        half EvaluateDistantTerrainHeightShadow(float3 positionWS)
        {
            if (_HectonDistantTerrainShadowParams.z < 0.5)
                return 0.0h;

            float2 uv = (positionWS.xz - _HectonDistantTerrainShadowRect.xy) * _HectonDistantTerrainShadowRect.zw;
            if (any(uv < 0.0) || any(uv > 1.0))
                return 0.0h;

            half mask = SAMPLE_TEXTURE2D(_HectonDistantTerrainShadowMask, sampler_HectonDistantTerrainShadowMask, uv).r;
            return saturate(mask * (half)_HectonDistantTerrainShadowParams.x);
        }

        float2 HectonHash22(float2 value)
        {
            float3 p3 = frac(float3(value.xyx) * float3(0.1031, 0.1030, 0.0973));
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.xx + p3.yz) * p3.zy);
        }

        half HectonVoronoiEdgeNoise2D(float2 position)
        {
            float2 baseCell = floor(position);
            float2 local = frac(position);
            float nearest = 8.0;
            float secondNearest = 8.0;
            float nearestCellSignal = 0.5;

            [unroll]
            for (int y = -1; y <= 1; y++)
            {
                [unroll]
                for (int x = -1; x <= 1; x++)
                {
                    float2 offset = float2(x, y);
                    float2 cell = baseCell + offset;
                    float2 jitter = HectonHash22(cell);
                    float2 delta = offset + jitter - local;
                    float distanceSq = dot(delta, delta);
                    if (distanceSq < nearest)
                    {
                        secondNearest = nearest;
                        nearest = distanceSq;
                        nearestCellSignal = HectonHash22(cell + float2(19.17, 19.17)).x;
                    }
                    else if (distanceSq < secondNearest)
                    {
                        secondNearest = distanceSq;
                    }
                }
            }

            half edgeRidge = (half)(1.0 - saturate((secondNearest - nearest) * 2.75));
            return saturate((half)nearestCellSignal * 0.72h + edgeRidge * 0.28h);
        }

        half ResolveBiomeEdgeBleed(float3 positionWS, half biome)
        {
            half transitionMask = saturate(1.0h - abs(biome * 2.0h - 1.0h));
            if (transitionMask <= 0.0001h || _BiomeEdgeBleedStrength <= 0.0001)
                return biome;

            float scale = max(_BiomeEdgeBleedScale, 0.0001);
            half macroCells = HectonVoronoiEdgeNoise2D(positionWS.xz * scale);
            half fractureCells = HectonVoronoiEdgeNoise2D(positionWS.xz * (scale * 2.17) + float2(19.37, -11.13));
            half edgeNoise = saturate(macroCells * 0.64h + fractureCells * 0.36h);
            half bleed = (edgeNoise - 0.5h) * transitionMask * (half)_BiomeEdgeBleedStrength;
            return saturate(biome + bleed);
        }

        half ApplyNoirSiltPulse(float3 positionWS, half depthFactor, half slopeBlend, inout half3 albedo, inout half3 emission)
        {
            half strength = saturate((half)_NoirSiltPulseStrength);
            if (strength <= 0.0001h)
                return 0.0h;

            float scale = max(_NoirSiltPulseScale, 0.0001);
            float2 pulseCoord = positionWS.xz * scale + positionWS.yy * 0.011;
            half grainA = HectonInterleavedGradientNoise(pulseCoord * 173.31);
            half grainB = HectonInterleavedGradientNoise(pulseCoord.yx * 91.70 + _Time.yy * 0.037);
            half pulse = saturate(((grainA * 0.65h) + (grainB * 0.35h) - 0.52h) * 3.2h);
            half mask = pulse * saturate(depthFactor * (1.0h - slopeBlend * 0.35h)) * strength;
            albedo = lerp(albedo, albedo * _NoirSiltPulseColor.rgb, mask * 0.65h);
            emission += _NoirSiltPulseColor.rgb * (mask * 0.025h);
            return mask;
        }

        ENDHLSL

        // ================================================================
        // PASS 1: UniversalForward
        // ================================================================
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest  LEqual
            Cull   Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma require 2darray
            #pragma vertex   ForwardVert
            #pragma fragment ForwardFrag

            // Shadow keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            // Lightmaps
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            // Light layers, reflection probes
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            // Fog
            #pragma multi_compile_fog
            // GPU instancing
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // -- Vertex color packing: R=Slope G=Depth B=Cave A=Biome --

            struct ForwardAttributes
            {
                float4 positionOS  : POSITION;
                float3 normalOS    : NORMAL;
                float4 tangentOS   : TANGENT;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
                float2 staticLightmapUV  : TEXCOORD1;
                float2 dynamicLightmapUV : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ForwardVaryings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                // Pack vertex colors: xy = (slope, depth), zw = (cave, biome)
                half4  vColor      : TEXCOORD2;
                half   fogFactor   : TEXCOORD3;
                // Pack triplanar blend into w of positionWS? No â€” keep it separate for clarity
                #if defined(LIGHTMAP_ON)
                float2 staticLightmapUV  : TEXCOORD5;
                #endif
                #if defined(DYNAMICLIGHTMAP_ON)
                float2 dynamicLightmapUV : TEXCOORD6;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ForwardVaryings ForwardVert(ForwardAttributes IN)
            {
                ForwardVaryings OUT = (ForwardVaryings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = nrmInputs.normalWS;
                OUT.vColor     = IN.color;
                OUT.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);

                // slope < threshold â†’ 0 (flat, cheap 2D), slope >= threshold â†’ ramp to 1

                #if defined(LIGHTMAP_ON)
                OUT.staticLightmapUV = IN.staticLightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
                #endif
                #if defined(DYNAMICLIGHTMAP_ON)
                OUT.dynamicLightmapUV = IN.dynamicLightmapUV * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif

                return OUT;
            }

            half4 ForwardFrag(ForwardVaryings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // ---- Unpack Vertex Colors ----
                half slope = IN.vColor.r;
                half depth = IN.vColor.g;
                half cave  = IN.vColor.b;
                half biome = IN.vColor.a;
                // ---- Texture array sampling ----
                half slopeBlend = saturate(pow(abs(slope), _SlopeSharpness));
                float terrainScale = lerp(_SandScale, _RockScale, slopeBlend);
                float terrainSlice = HectonResolveTerrainArraySlice(slopeBlend, biome);
                half4 terrainSample = HectonSampleTerrainAlbedoArray(
                    IN.positionWS, IN.normalWS, terrainScale, terrainSlice);
                half3 rockNormalWS = HectonSampleTerrainNormalArray(
                    IN.positionWS, IN.normalWS, terrainScale, _RockNormalStr, terrainSlice);
                half3 albedo = terrainSample.rgb * lerp(_SandColor.rgb, _RockColor.rgb, slopeBlend);
                half smoothness = terrainSample.a * _BaseSmooth;

                // ---- Biome tint ----
                half biomeBleed = ResolveBiomeEdgeBleed(IN.positionWS, biome);
                albedo = lerp(albedo, albedo * _BiomeTint.rgb, biomeBleed);

                // ---- Depth darkening ----
                half depthFactor = pow(abs(depth), _DarkenPower);
                albedo     = lerp(albedo, albedo * _DepthColor.rgb, depthFactor);
                smoothness = smoothness * (1.0 - depthFactor);

                // ---- Cave emission ----
                half3 emission = cave * _CaveGlowColor.rgb * _CaveGlowPower;
                ApplyNoirSiltPulse(IN.positionWS, depthFactor, slopeBlend, albedo, emission);
                half canopyShadow = EvaluateSargassumCanopyShadow(IN.positionWS);
                if (canopyShadow > 0.0001h)
                {
                    albedo = lerp(albedo, albedo * 0.34h, canopyShadow);
                    emission *= 1.0h - canopyShadow * 0.8h;
                }
                half distantHeightShadow = EvaluateDistantTerrainHeightShadow(IN.positionWS);
                if (distantHeightShadow > 0.0001h)
                {
                    albedo = lerp(albedo, albedo * 0.22h, distantHeightShadow);
                    emission *= 1.0h - distantHeightShadow * 0.9h;
                }

                // ---- Final world normal ----
                float3 finalNormalWS = NormalizeNormalPerPixel(
                    IN.normalWS + rockNormalWS * slopeBlend);

                // ============================================================
                // InputData â€” fully initialized to prevent magenta
                // ============================================================
                InputData inputData = (InputData)0;
                inputData.positionWS              = IN.positionWS;
                inputData.positionCS              = IN.positionCS;
                inputData.normalWS                = finalNormalWS;
                inputData.viewDirectionWS         = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.fogCoord                = InitializeInputDataFog(
                                                        float4(IN.positionWS, 1.0), IN.fogFactor);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask              = half4(1.0, 1.0, 1.0, 1.0);

                #if defined(LIGHTMAP_ON)
                    inputData.staticLightmapUV    = IN.staticLightmapUV;
                #endif
                #if defined(DYNAMICLIGHTMAP_ON)
                    inputData.dynamicLightmapUV   = IN.dynamicLightmapUV;
                #endif

                // Shadow coord
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    inputData.shadowCoord = ComputeScreenPos(IN.positionCS);
                #elif defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #else
                    inputData.shadowCoord = float4(0.0, 0.0, 0.0, 0.0);
                #endif

                // ---- Ambient / GI (Unity 6 / URP 17+ compatible) ----
                // SampleSH(float3) was removed/changed in URP 17.
                // Use SampleSHPixel which is the correct URP 17 API,
                // or fall back to manual SH evaluation.
                #if defined(LIGHTMAP_ON)
                    // Use lightmap for baked GI
                    #if defined(DYNAMICLIGHTMAP_ON)
                        inputData.bakedGI = SAMPLE_GI(
                            IN.staticLightmapUV,
                            IN.dynamicLightmapUV,
                            half3(0, 0, 0),
                            finalNormalWS);
                    #else
                        inputData.bakedGI = SAMPLE_GI(
                            IN.staticLightmapUV,
                            half3(0, 0, 0),
                            finalNormalWS);
                    #endif
                #else
                    // No lightmap â€” sample Spherical Harmonics
                    // URP 17+ approach: SampleSHPixel with vertex SH passed as 0
                    // The vertex SH (OUTPUT_SH4) is broken, so we compute per-pixel.
                    #if defined(EVALUATE_SH_VERTEX) || defined(EVALUATE_SH_MIXED)
                        // If URP defines these, use SampleSHPixel
                        inputData.bakedGI = SampleSHPixel(half3(0, 0, 0), finalNormalWS);
                    #else
                        // Robust fallback: manually evaluate L0+L1+L2 SH
                        // unity_SHAr/Ag/Ab/Br/Bg/Bb/C are always available
                        inputData.bakedGI = max(half3(0, 0, 0),
                            SampleSH(finalNormalWS));
                    #endif
                #endif

                // ============================================================
                // SurfaceData â€” fully initialized to prevent magenta
                // ============================================================
                SurfaceData surfData    = (SurfaceData)0;
                surfData.albedo         = albedo;
                surfData.metallic       = _Metallic;
                surfData.specular       = half3(0.0, 0.0, 0.0);
                surfData.smoothness     = smoothness;
                surfData.normalTS       = half3(0.0, 0.0, 1.0);
                surfData.emission       = emission;
                surfData.occlusion      = 1.0;
                surfData.alpha          = 1.0;
                surfData.clearCoatMask  = 0.0;
                surfData.clearCoatSmoothness = 0.0;

                // ---- PBR lighting ----
                half4 color = UniversalFragmentPBR(inputData, surfData);

                // ---- Fog ----
                color.rgb = MixFog(color.rgb, inputData.fogCoord);

                half planetaryFade = ApplyPlanetaryDitherFade(EvaluatePlanetaryTerrainFade(IN.positionWS), IN.positionCS);
                half3 noirFogFloor = half3(0.0015h, 0.0023h, 0.0031h);
                color.rgb = lerp(noirFogFloor, color.rgb, planetaryFade);

                return color;
            }

            ENDHLSL
        }

        // ================================================================
        // PASS 2: ShadowCaster
        // ================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest  LEqual
            Cull   Back
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma require 2darray
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // These are set by URP shadow system
            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttr
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVary
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVary ShadowVert(ShadowAttr IN)
            {
                ShadowVary OUT = (ShadowVary)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nrmWS = TransformObjectToWorldNormal(IN.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDir = normalize(_LightPosition - posWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif

                float4 posCS = TransformWorldToHClip(
                    ApplyShadowBias(posWS, nrmWS, lightDir));

                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                OUT.positionCS = posCS;
                return OUT;
            }

            half4 ShadowFrag(ShadowVary IN) : SV_Target
            {
                return 0;
            }

            ENDHLSL
        }

        // ================================================================
        // PASS 3: DepthOnly
        // ================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ZTest  LEqual
            Cull   Back
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma require 2darray
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct DepthAttr
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVary
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVary DepthVert(DepthAttr IN)
            {
                DepthVary OUT = (DepthVary)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 DepthFrag(DepthVary IN) : SV_Target
            {
                return 0;
            }

            ENDHLSL
        }

        // ================================================================
        // PASS 4: DepthNormals â€” manual pass, avoids Unity 6 include bugs
        // Uses adaptive sampling for consistency & performance
        // ================================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            ZTest  LEqual
            Cull   Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma require 2darray
            #pragma vertex   DNVert
            #pragma fragment DNFrag
            #pragma multi_compile_instancing

            // Only Core.hlsl is included via HLSLINCLUDE â€” minimal dependencies

            struct DNAttr
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DNVary
            {
                float4 positionCS  : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                // Pack: x = slopeBlend, y = biome texture-array index source
                half2  blendData   : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DNVary DNVert(DNAttr IN)
            {
                DNVary OUT = (DNVary)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = nrmInputs.normalWS;

                half slope = IN.color.r;
                OUT.blendData.x = saturate(pow(abs(slope), _SlopeSharpness));
                OUT.blendData.y = IN.color.a;

                return OUT;
            }

            half4 DNFrag(DNVary IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float terrainScale = lerp(_SandScale, _RockScale, IN.blendData.x);
                float terrainSlice = HectonResolveTerrainArraySlice(IN.blendData.x, IN.blendData.y);
                half3 rockN = HectonSampleTerrainNormalArray(
                    IN.positionWS, IN.normalWS, terrainScale, _RockNormalStr, terrainSlice);

                float3 finalN = normalize(IN.normalWS + rockN * IN.blendData.x);

                // Encode for URP DepthNormals texture
                // URP 17 provides NormalizeNormalPerPixel; encode as octahedral or [0,1]
                // Safe fallback that works across URP versions:
                return half4(finalN * 0.5 + 0.5, 0.0);
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
