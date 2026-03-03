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

        [Header(Blending)]
        _SlopeSharpness ("Slope Blend Sharpness", Range(1,32)) = 8.0
        _TriplanarThreshold ("Triplanar Slope Threshold", Range(0.01, 0.99)) = 0.3

        [Header(Biome)]
        _BiomeTint      ("Biome Tint", Color) = (0.15, 0.12, 0.1, 1)

        [Header(Depth)]
        _DarkenPower    ("Depth Darken Power", Range(0.1,10)) = 2.5
        _DepthColor     ("Abyss Color", Color) = (0.02, 0.04, 0.08, 1)

        [Header(Cave Glow)]
        _CaveGlowColor  ("Cave Glow Color", Color) = (0.2, 0.8, 1.0, 1)
        _CaveGlowPower  ("Cave Glow Intensity", Range(0,10)) = 3.0

        [Header(Surface)]
        _Metallic       ("Metallic", Range(0,1)) = 0.0
        _BaseSmooth     ("Smoothness", Range(0,1)) = 0.5
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
        // SHARED HLSL — included in every pass automatically
        // ================================================================
        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // -------------------------------------------------------------
        // SRP Batcher: ALL material properties in ONE contiguous CBUFFER.
        // No _ST floats needed — we use world-space UVs with _Scale.
        // -------------------------------------------------------------
        CBUFFER_START(UnityPerMaterial)
            float  _SandScale;
            float  _RockScale;
            float  _RockNormalStr;
            float  _SlopeSharpness;
            float  _TriplanarThreshold;
            float  _DarkenPower;
            float  _CaveGlowPower;
            half   _Metallic;
            half   _BaseSmooth;
            half4  _SandColor;
            half4  _RockColor;
            half4  _BiomeTint;
            half4  _DepthColor;
            half4  _CaveGlowColor;
        CBUFFER_END

        // Textures declared outside CBUFFER (URP requirement)
        TEXTURE2D(_SandTex);    SAMPLER(sampler_SandTex);
        TEXTURE2D(_RockTex);    SAMPLER(sampler_RockTex);
        TEXTURE2D(_RockNormal); SAMPLER(sampler_RockNormal);

        // -------------------------------------------------------------
        // Triplanar weights (custom name to avoid CommonMaterial clash)
        // -------------------------------------------------------------
        float3 HectonTriplanarWeights(float3 normalWS)
        {
            float3 w = abs(normalWS);
            w = saturate(w - 0.2);
            w = w * w * w;
            w /= (dot(w, float3(1.0, 1.0, 1.0)) + 1e-6);
            return w;
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
            // Always sample XZ plane (Y-weight projection) — cheapest
            half4 ySample = SAMPLE_TEXTURE2D(tex, samp, posWS.xz * scale);

            // Early out for flat terrain — single texture read
            if (triplanarBlend < 0.001)
                return ySample;

            // Full triplanar for steep surfaces
            half4 xSample = SAMPLE_TEXTURE2D(tex, samp, posWS.zy * scale);
            half4 zSample = SAMPLE_TEXTURE2D(tex, samp, posWS.xy * scale);
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
            half3 ny = UnpackNormalScale(
                SAMPLE_TEXTURE2D(tex, samp, posWS.xz * scale), strength);
            ny = half3(ny.x, 0.0, ny.y);

            if (triplanarBlend < 0.001)
                return ny;

            // ZY plane normal (X-weight)
            half3 nx = UnpackNormalScale(
                SAMPLE_TEXTURE2D(tex, samp, posWS.zy * scale), strength);
            nx = half3(0.0, nx.y, nx.x);

            // XY plane normal (Z-weight)
            half3 nz = UnpackNormalScale(
                SAMPLE_TEXTURE2D(tex, samp, posWS.xy * scale), strength);
            nz = half3(nz.x, nz.y, 0.0);

            half3 triNormal = nx * weights.x + ny * weights.y + nz * weights.z;

            return lerp(ny, triNormal, triplanarBlend);
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
                // Pack triplanar blend into w of positionWS? No — keep it separate for clarity
                half   triBlend    : TEXCOORD4;
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

                // Precompute triplanar blend in vertex shader to save fragment cost
                // slope < threshold → 0 (flat, cheap 2D), slope >= threshold → ramp to 1
                half slope = IN.color.r;
                OUT.triBlend = saturate((slope - _TriplanarThreshold) / (1.0 - _TriplanarThreshold + 1e-6));

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
                half triBlend = IN.triBlend;

                // ---- Triplanar weights (computed even for partial blend) ----
                float3 triW = HectonTriplanarWeights(IN.normalWS);

                // ---- Adaptive texture sampling ----
                half4 sandSample = HectonSampleAdaptive(
                    TEXTURE2D_ARGS(_SandTex, sampler_SandTex),
                    IN.positionWS, triW, _SandScale, triBlend);

                half4 rockSample = HectonSampleAdaptive(
                    TEXTURE2D_ARGS(_RockTex, sampler_RockTex),
                    IN.positionWS, triW, _RockScale, triBlend);

                half3 rockNormalWS = HectonSampleAdaptiveNormal(
                    TEXTURE2D_ARGS(_RockNormal, sampler_RockNormal),
                    IN.positionWS, triW, _RockScale, _RockNormalStr, triBlend);

                // ---- Slope blending ----
                half slopeBlend = saturate(pow(abs(slope), _SlopeSharpness));
                half3 albedo = lerp(
                    sandSample.rgb * _SandColor.rgb,
                    rockSample.rgb * _RockColor.rgb,
                    slopeBlend);
                half smoothness = lerp(sandSample.a, rockSample.a, slopeBlend) * _BaseSmooth;

                // ---- Biome tint ----
                albedo = lerp(albedo, albedo * _BiomeTint.rgb, biome);

                // ---- Depth darkening ----
                half depthFactor = pow(abs(depth), _DarkenPower);
                albedo     = lerp(albedo, albedo * _DepthColor.rgb, depthFactor);
                smoothness = smoothness * (1.0 - depthFactor);

                // ---- Cave emission ----
                half3 emission = cave * _CaveGlowColor.rgb * _CaveGlowPower;

                // ---- Final world normal ----
                float3 finalNormalWS = NormalizeNormalPerPixel(
                    IN.normalWS + rockNormalWS * slopeBlend);

                // ============================================================
                // InputData — fully initialized to prevent magenta
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
                    // No lightmap — sample Spherical Harmonics
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
                // SurfaceData — fully initialized to prevent magenta
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
        // PASS 4: DepthNormals — manual pass, avoids Unity 6 include bugs
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
            #pragma vertex   DNVert
            #pragma fragment DNFrag
            #pragma multi_compile_instancing

            // Only Core.hlsl is included via HLSLINCLUDE — minimal dependencies

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
                // Pack: x = slopeBlend, y = triBlend (save ALU in fragment)
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
                OUT.blendData.y = saturate((slope - _TriplanarThreshold) / (1.0 - _TriplanarThreshold + 1e-6));

                return OUT;
            }

            half4 DNFrag(DNVary IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float3 triW = HectonTriplanarWeights(IN.normalWS);

                half3 rockN = HectonSampleAdaptiveNormal(
                    TEXTURE2D_ARGS(_RockNormal, sampler_RockNormal),
                    IN.positionWS, triW, _RockScale, _RockNormalStr,
                    IN.blendData.y); // triplanar blend

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