// ============================================================================================
//  Hecton8/Construction/ModuleHardSurfaceLit
//
//  The consumer for the four hard-surface vertex-colour wear channels fixed by `3dmodel.md`
//  section 4 and `3DMODEL_HARD_SURFACE_MODULES.md` section 5. Before this shader existed the
//  channels were baked correctly by ModuleArchitect1712 and read by nothing:
//  `Universal Render Pipeline/Lit` has no COLOR semantic in its Attributes struct, so every
//  wear byte on the six generated modules was inert.
//
//  Channel contract (`3dmodel.md:123-126`, `3DMODEL_HARD_SURFACE_MODULES.md:82-85`):
//    R = exposed edge wear / salt-polished rim mask.
//        Written as saturate(convexity * exposure * materialWearCoefficient + noise)
//        at ModuleArchitect1712.cs:1394. High on chamfers, bevels and door lips
//        (ResolveWearCoefficient = 1.00, ModuleArchitect1712.cs:1413-1433), low on
//        protected step walls (0.20).
//    G = rust / oxidation / biofilm / fluid stain.
//        Written as saturate(cavity * (0.35 + downwardBias) * quality + noise)
//        at ModuleArchitect1712.cs:1395.
//    B = baked ambient occlusion and cavity darkness.
//        WARNING - NOT A BAKE. Written as saturate(1 - cavity) at
//        ModuleArchitect1712.cs:1396, where `cavity` is the analytic pocket-occlusion
//        estimate declared at ModuleHardSurfaceDetail1712.cs:41-43 ("depth over opening
//        width, NOT a ray-traced bake"). It is an approximation of vertex-scale cavity
//        darkness, it is not ground truth, and it MUST be combined multiplicatively with
//        the material occlusion map rather than replacing it - see
//        `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt:258`
//        ("ao_final = min(baked_ao_lightmap, 1.0) * noir_mask.a // multiplicative AO stack").
//        Do not later mistake this channel for a baked AO map.
//    A = emissive seam / warning paint / decal eligibility.
//        Convention is source-owned by ModuleHardSurfaceDetail1712.cs:357-358:
//        A >= EmissiveAlphaThreshold (0.94) marks an emissive seam strip; lower non-zero
//        values are decal / warning-paint eligibility weight; zero forbids both.
//        Only GasketAttributes emits 1.0 (ModuleHardSurfaceDetail1712.cs:385); the next
//        highest is PlateAttributes at 0.85 (:388), so the reserved band is clean.
//
//  Mask packing. The six live materials under
//  Assets/_Project/Art/Materials/Construction/Mat_Module_*.mat bind ONE Gemini
//  `MaskMap_UnityURP` texture to both `_MetallicGlossMap` and `_OcclusionMap`
//  (ConstructionGeminiMaterialApplier.cs:127-128). URP Lit therefore reads it as
//  R = metallic (LitInput.hlsl:261 via SampleMetallicSpecGloss),
//  G = occlusion (LitInput.hlsl:164, `.g`),
//  A = smoothness (LitInput.hlsl:265, multiplied by _Smoothness at :142).
//  B is unused by URP Lit and stays unused here. This shader decodes the same channels in
//  the same order so swapping the shader does not change the base material read; only the
//  vertex-colour layers are additive.
//
//  Height. `_ParallaxMap` is sampled on `.g` to match URP
//  (com.unity.render-pipelines.core ShaderLibrary/ParallaxMapping.hlsl:41).
//
//  SRP Batcher. Every material property lives in one `CBUFFER_START(UnityPerMaterial)`
//  block in the shared HLSLINCLUDE ahead of all passes, float4 first and 16-byte aligned,
//  matching the discipline HectonMasterShaderAudit1615.cs:142-158 enforces on
//  Hecton_Master_Lit. `_H8GlobalQualityWeight` is a global, declared outside the CBUFFER and
//  absent from Properties, exactly as HectonMasterShaderAudit1615.cs:165-167 requires.
//  `MaterialPropertyBlock` is banned on this geometry (`AGENTS.md` Runtime Hot-Path Law,
//  `REND_GPU_Sovereignty.txt:29`) - the modules are MeshRenderer GameObjects sharing one
//  material, which is the GPU Resident Drawer path `REND_GPU_Sovereignty.txt:27` requires.
//
//  Quality. No binary quality branch anywhere (`AGENTS.md` GlobalQualityWeight And
//  Scalability). Parallax step count, normal scale, procedural rust breakup and cavity
//  micro-contrast are continuous lerps of `_H8GlobalQualityWeight`. Edge wear itself is NOT
//  quality-gated: `TASTE.md:340` requires "material wear through packed masks and shared
//  detail" to survive at GlobalQualityWeight 0.
// ============================================================================================
Shader "Hecton8/Construction/ModuleHardSurfaceLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [Normal] _BumpMap("Normal", 2D) = "bump" {}
        _MaskMap("Packed Mask (R Metallic G Occlusion A Smoothness)", 2D) = "white" {}
        _ParallaxMap("Height (G)", 2D) = "black" {}

        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _ModuleEdgeMetalColor("Edge Bare Metal", Color) = (0.52, 0.55, 0.58, 1)
        _ModuleOxideColor("Oxide / Rust Tint", Color) = (0.72, 0.40, 0.19, 1)
        _ModuleBiofilmColor("Biofilm / Underside Stain Tint", Color) = (0.30, 0.42, 0.34, 1)
        [HDR] _ModuleSeamEmissionColor("Seam Emission (DECAY_AMBER)", Color) = (0.55, 0.28, 0.04, 1)

        _ModuleWearParams("Wear: EdgeR OxideG CavityB ChannelTrust", Vector) = (1, 1, 1, 1)
        _ModuleSurfaceParams("Surface: MetallicMap SmoothnessMap AO Normal", Vector) = (1, 1, 1, 1)
        _ModuleEdgeResponse("Edge: MetallicGain SmoothGain AlbedoLift Contrast", Vector) = (0.85, 0.55, 0.72, 0.35)
        _ModuleOxideResponse("Oxide: RoughGain MetalLoss Unused AlbedoBlend", Vector) = (0.62, 0.85, 0, 0.68)
        _ModuleSeamParams("Seam: Threshold Band PaintAdhesion EmissionScale", Vector) = (0.94, 0.04, 0.55, 1)
        _ModulePomParams("POM: Scale Steps Bias QualityCap", Vector) = (0, 4, 0, 1)
        _ModuleNoirParams("Noir: Ambient Specular CavityMicro OcclusionFloor", Vector) = (0.34, 0.42, 0.35, 0.06)
        _ModuleRustSiltParams("RustSilt: SiltStrength RustStrength Unused Unused", Vector) = (0.22, 0.46, 0, 0)
        _ModuleSiltTint("Silt Tint", Color) = (0.23, 0.28, 0.26, 1)

        _Metallic("Metallic Fallback", Range(0, 1)) = 0
        _Smoothness("Smoothness Scale", Range(0, 1)) = 0.42
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1
        _BumpScale("Normal Scale", Range(0, 2)) = 1
        _Parallax("Height Scale", Range(0, 0.08)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Lit"
        }

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        // Existing first-party wear/rust/silt math and safe-normalize helpers, already shipping in
        // Hecton8/World/WreckIndirectLit, Hecton8/Flora/KelpMaster and Hecton8/Flora/CoralMaster.
        // Reused instead of re-deriving rust noise here (`AGENTS.md` Global Lookup Before Creating
        // Files, Use existing quality assets before rewriting). Verified 2026-07-29 that this
        // include declares no global colliding with any property name above.
        #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);
        SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MaskMap);
        SAMPLER(sampler_MaskMap);
        TEXTURE2D(_ParallaxMap);
        SAMPLER(sampler_ParallaxMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;              //   0..15
            float4 _BumpMap_ST;              //  16..31
            float4 _MaskMap_ST;              //  32..47
            float4 _ParallaxMap_ST;          //  48..63
            float4 _BaseColor;               //  64..79
            float4 _ModuleEdgeMetalColor;    //  80..95
            float4 _ModuleOxideColor;        //  96..111
            float4 _ModuleBiofilmColor;      // 112..127
            float4 _ModuleSeamEmissionColor; // 128..143
            float4 _ModuleWearParams;        // 144..159 x=edge R weight, y=oxide G weight, z=cavity B weight, w=vertex channel trust
            float4 _ModuleSurfaceParams;     // 160..175 x=metallic map weight, y=smoothness map weight, z=AO map weight, w=normal scale
            float4 _ModuleEdgeResponse;      // 176..191 x=metallic gain, y=smoothness gain, z=albedo lift, w=edge contrast
            float4 _ModuleOxideResponse;     // 192..207 x=roughness gain, y=metallic loss, z=reserved, w=albedo blend
            float4 _ModuleSeamParams;        // 208..223 x=emissive alpha threshold, y=ramp band, z=paint adhesion, w=emission scale
            float4 _ModulePomParams;         // 224..239 x=height scale, y=max steps, z=height bias, w=quality cap
            float4 _ModuleNoirParams;        // 240..255 x=ambient, y=specular, z=cavity micro-contrast, w=occlusion floor
            float4 _ModuleRustSiltParams;    // 256..271 x=silt strength, y=rust strength, z/w=reserved
            float4 _ModuleSiltTint;          // 272..287
            float _Metallic;                 // 288..291
            float _Smoothness;               // 292..295
            float _OcclusionStrength;        // 296..299
            float _BumpScale;                // 300..303
            float _Parallax;                 // 304..307
            float _H8ModulePadding0;         // 308..311
            float _H8ModulePadding1;         // 312..315
            float _H8ModulePadding2;         // 316..319 -> 320 bytes, 16-byte aligned
        CBUFFER_END

        // Global, never a material property and never inside UnityPerMaterial - putting a global in
        // the per-material CBUFFER breaks SRP Batcher layout. Same rule and same literal form the
        // master-shader audit asserts (HectonMasterShaderAudit1615.cs:165-167). Written by
        // HomeostasisBrain.ScalabilityDictator.cs:234 / HectonCelestialEngine.cs:7528.
        float _H8GlobalQualityWeight;
        float3 _LightDirection;
        float3 _LightPosition;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half4 tangentWS : TEXCOORD2;
            half3 viewDirWS : TEXCOORD3;
            float2 uv : TEXCOORD4;
            float4 shadowCoord : TEXCOORD5;
            half fogFactor : TEXCOORD6;
            half4 wearColor : TEXCOORD7;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        struct DepthNormalsVaryings
        {
            float4 positionCS : SV_POSITION;
            half3 normalWS : TEXCOORD0;
            half4 tangentWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        struct DepthOnlyVaryings
        {
            float4 positionCS : SV_POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float H8ModuleQuality()
        {
            float globalQuality = isfinite(_H8GlobalQualityWeight) ? saturate(_H8GlobalQualityWeight) : 0.0;
            float materialCap = isfinite(_ModulePomParams.w) ? saturate(_ModulePomParams.w) : 1.0;
            return saturate(globalQuality * materialCap);
        }

        float H8ModuleSafeRcp(float value)
        {
            float safeValue = max(abs(value), 0.0001);
            float signValue = lerp(-1.0, 1.0, step(0.0, value));
            return signValue * rcp(safeValue);
        }

        float2 H8ModuleSafeRcp2(float2 value)
        {
            float2 safeValue = max(abs(value), float2(0.0001, 0.0001));
            float2 signValue = lerp(float2(-1.0, -1.0), float2(1.0, 1.0), step(float2(0.0, 0.0), value));
            return signValue * rcp(safeValue);
        }

        half3 H8ModuleSafeNormalize(half3 value, half3 fallbackValue)
        {
            half lenSq = dot(value, value);
            half valid = (half)step(0.0001, (float)lenSq);
            half3 normalized = value * (half)rsqrt(max((float)lenSq, 0.0001));
            return lerp(fallbackValue, normalized, valid);
        }

        // Non-finite vertex data must never reach the wear layers. `3dmodel.md:90` forbids
        // non-finite colours on a saved mesh and section 10 validates it, but a broken import or a
        // hand-edited mesh must degrade to "no wear", not to NaN albedo.
        half4 H8ModuleSanitizeWearColor(half4 value)
        {
            half4 finiteMask = half4(
                (half)(isfinite((float)value.r) ? 1.0 : 0.0),
                (half)(isfinite((float)value.g) ? 1.0 : 0.0),
                (half)(isfinite((float)value.b) ? 1.0 : 0.0),
                (half)(isfinite((float)value.a) ? 1.0 : 0.0));
            // Channel B defaults to 1.0 (fully exposed) when absent, so a colourless mesh reads as
            // "no cavity darkening" rather than "fully occluded".
            half4 fallback = half4(0.0h, 0.0h, 1.0h, 0.0h);
            half4 finite = saturate(lerp(fallback, value, finiteMask));

            // Missing-vertex-stream guard. D3D11 supplies (0,0,0,0) when a mesh has no COLOR
            // stream, and B = 0 means "fully occluded cavity" in this contract, so an unguarded
            // read would render a colourless mesh solid black. Exact all-zero cannot occur in
            // ModuleArchitect1712 output: B = saturate(1 - cavity) (ModuleArchitect1712.cs:1396)
            // and the highest authored cavity is CollarAttributes at 0.80
            // (ModuleHardSurfaceDetail1712.cs:384), so B >= 0.20 on every generated vertex.
            // Treating exact zero as "absent" is therefore lossless for this family and turns a
            // black-module failure into a clean no-wear fallback.
            half present = (half)step(0.0001, (float)dot(finite, half4(1.0h, 1.0h, 1.0h, 1.0h)));
            return lerp(fallback, finite, present);
        }

        float2 H8ModuleResolveParallaxUv(
            float2 uv,
            half3 viewDirWS,
            half3 normalWS,
            half4 tangentWS,
            float quality)
        {
            float steps = floor(saturate(quality) * clamp(_ModulePomParams.y, 0.0, 8.0) + 0.5);
            if (steps <= 0.0)
                return uv;

            half3 tangent = H8ModuleSafeNormalize(tangentWS.xyz, half3(1.0h, 0.0h, 0.0h));
            half3 normal = H8ModuleSafeNormalize(normalWS, half3(0.0h, 1.0h, 0.0h));
            half3 bitangent = H8ModuleSafeNormalize(cross(normal, tangent) * tangentWS.w, half3(0.0h, 0.0h, 1.0h));
            float3 viewTS = float3(
                dot(viewDirWS, tangent),
                dot(viewDirWS, bitangent),
                dot(viewDirWS, normal));
            // URP ParallaxOffset1Step biases z by 0.42 to keep grazing angles bounded
            // (com.unity.render-pipelines.core ShaderLibrary/ParallaxMapping.hlsl).
            float viewZ = viewTS.z + 0.42;
            float2 viewScaled = viewTS.xy * H8ModuleSafeRcp(viewZ);
            // URP samples the height map on `.g` (ParallaxMapping.hlsl:41).
            float height = (float)SAMPLE_TEXTURE2D(_ParallaxMap, sampler_ParallaxMap, uv).g;
            float amplitude = max(_ModulePomParams.x, _Parallax) * quality;
            float biasedHeight = (height + _ModulePomParams.z) * amplitude - amplitude * 0.5;
            float2 totalOffset = viewScaled * biasedHeight;
            float invSteps = H8ModuleSafeRcp(max(steps, 1.0));
            float2 resolvedUv = uv;

            [loop]
            for (int i = 0; i < 8; i++)
            {
                float active = step((float)i, steps - 0.5);
                resolvedUv += totalOffset * invSteps * active;
            }

            return resolvedUv;
        }

        // Display-pretransform-safe screen UV. The raw GetNormalizedScreenSpaceUV form is
        // explicitly rejected by the project's own master-shader audit
        // (HectonMasterShaderAudit1615.cs:216) because it is wrong under Android display
        // pretransform, so the corrected form is used here too.
        float2 H8ModuleNormalizedScreenSpaceUv(float4 positionCS)
        {
        #if defined(UNITY_PRETRANSFORM_TO_DISPLAY_ORIENTATION)
            float2 preRotatedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
            switch (UNITY_DISPLAY_ORIENTATION_PRETRANSFORM)
            {
                default:
                case UNITY_DISPLAY_ORIENTATION_PRETRANSFORM_0:
                    return preRotatedScreenSpaceUV;
                case UNITY_DISPLAY_ORIENTATION_PRETRANSFORM_90:
                    return float2(1.0 - preRotatedScreenSpaceUV.y, preRotatedScreenSpaceUV.x);
                case UNITY_DISPLAY_ORIENTATION_PRETRANSFORM_180:
                    return float2(1.0 - preRotatedScreenSpaceUV.x, 1.0 - preRotatedScreenSpaceUV.y);
                case UNITY_DISPLAY_ORIENTATION_PRETRANSFORM_270:
                    return float2(preRotatedScreenSpaceUV.y, 1.0 - preRotatedScreenSpaceUV.x);
            }

            return preRotatedScreenSpaceUV;
        #else
            return GetNormalizedScreenSpaceUV(positionCS);
        #endif
        }

        half3 H8ModuleNormalWS(half3 normalWS, half4 tangentWS, float2 uv, float quality)
        {
            half normalScale = (half)max(_ModuleSurfaceParams.w, _BumpScale);
            normalScale *= (half)lerp(0.62, 1.0, quality);
            half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), normalScale);
            half3 normal = H8ModuleSafeNormalize(normalWS, half3(0.0h, 1.0h, 0.0h));
            half3 tangent = H8ModuleSafeNormalize(tangentWS.xyz, half3(1.0h, 0.0h, 0.0h));
            half3 bitangent = H8ModuleSafeNormalize(cross(normal, tangent) * tangentWS.w, half3(0.0h, 0.0h, 1.0h));
            float3x3 tangentToWorld = float3x3((float3)tangent, (float3)bitangent, (float3)normal);
            return H8ModuleSafeNormalize((half3)TransformTangentToWorld(normalTS, tangentToWorld), normal);
        }

        // Decodes the mask exactly as URP Lit does for these materials, so a shader swap is
        // appearance-neutral before the wear layers are applied. See the header block.
        void H8ModuleDecodeMask(
            half4 packedMask,
            out half metallic,
            out half smoothness,
            out half occlusionMap)
        {
            metallic = saturate(lerp((half)_Metallic, packedMask.r, (half)saturate(_ModuleSurfaceParams.x)));
            half mappedSmoothness = packedMask.a * (half)saturate(_Smoothness);
            smoothness = saturate(lerp((half)saturate(_Smoothness), mappedSmoothness, (half)saturate(_ModuleSurfaceParams.y)));
            half occlusionWeight = (half)saturate(_OcclusionStrength * saturate(_ModuleSurfaceParams.z));
            occlusionMap = saturate(lerp(1.0h, packedMask.g, occlusionWeight));
        }

        // ------------------------------------------------------------------------------------
        //  Channel A - emissive seam gate and paint adhesion.
        //  Source of truth for the 0.94 split is ModuleHardSurfaceDetail1712.cs:357-358.
        //  Above threshold: gasket seam emissive. Below threshold and non-zero: painted /
        //  decal-eligible surface, which physically means the paint is intact and resists the
        //  edge-wear reveal - it does NOT mean "tint this amber". Panels sit at 0.45 and door
        //  flanges at 0.55 (ModuleHardSurfaceDetail1712.cs:377, :382); tinting those would put
        //  warning paint on half the module.
        // ------------------------------------------------------------------------------------
        void H8ModuleResolveSeam(half seamChannel, out half seamEmissive, out half paintAdhesion)
        {
            half threshold = (half)clamp(_ModuleSeamParams.x, 0.05, 1.0);
            half band = (half)clamp(_ModuleSeamParams.y, 0.005, 0.5);
            half lower = max(threshold - band, 0.0h);
            seamEmissive = saturate((seamChannel - lower) * (half)H8ModuleSafeRcp(max((float)(threshold - lower), 0.0001)));
            half paintRaw = saturate(seamChannel * (half)H8ModuleSafeRcp(max((float)threshold, 0.0001)));
            paintAdhesion = saturate(paintRaw * (1.0h - seamEmissive));
        }

        half3 H8ModuleLighting(
            float3 positionWS,
            float4 positionCS,
            float4 shadowCoord,
            half fogFactor,
            half3 viewDirWS,
            half3 albedo,
            half3 normalWS,
            half metallic,
            half smoothness,
            half occlusion,
            half3 emission)
        {
            half3 viewDir = H8ModuleSafeNormalize(viewDirWS, half3(0.0h, 0.0h, 1.0h));
            half ambientWeight = (half)saturate(_ModuleNoirParams.x);

            InputData inputData = (InputData)0;
            inputData.positionWS = positionWS;
            inputData.positionCS = positionCS;
            inputData.normalWS = normalWS;
            inputData.viewDirectionWS = viewDir;
            inputData.fogCoord = fogFactor;
            inputData.normalizedScreenSpaceUV = H8ModuleNormalizedScreenSpaceUv(positionCS);
            inputData.shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);
            inputData.shadowCoord = shadowCoord;
            inputData.bakedGI = SampleSH(normalWS) * ambientWeight;

            SurfaceData surfaceData = (SurfaceData)0;
            surfaceData.albedo = albedo;
            surfaceData.metallic = metallic;
            surfaceData.specular = half3(0.04h, 0.04h, 0.04h) * (half)saturate(_ModuleNoirParams.y);
            surfaceData.smoothness = smoothness;
            surfaceData.normalTS = half3(0.0h, 0.0h, 1.0h);
            surfaceData.emission = emission;
            surfaceData.occlusion = occlusion;
            surfaceData.alpha = 1.0h;
            surfaceData.clearCoatMask = 0.0h;
            surfaceData.clearCoatSmoothness = 0.0h;

            half4 color = UniversalFragmentPBR(inputData, surfaceData);
            return color.rgb;
        }

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            output.positionCS = positionInputs.positionCS;
            output.positionWS = positionInputs.positionWS;
            output.normalWS = H8ModuleSafeNormalize((half3)normalInputs.normalWS, half3(0.0h, 1.0h, 0.0h));
            output.tangentWS = half4(H8ModuleSafeNormalize((half3)normalInputs.tangentWS, half3(1.0h, 0.0h, 0.0h)), input.tangentOS.w);
            output.viewDirWS = H8ModuleSafeNormalize((half3)GetWorldSpaceViewDir(positionInputs.positionWS), half3(0.0h, 0.0h, 1.0h));
            output.uv = input.uv;
            output.shadowCoord = GetShadowCoord(positionInputs);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            output.wearColor = H8ModuleSanitizeWearColor(input.color);
            return output;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float quality = H8ModuleQuality();
            half4 wearColor = H8ModuleSanitizeWearColor(input.wearColor);
            half channelTrust = (half)saturate(_ModuleWearParams.w);

            float2 baseUv = TRANSFORM_TEX(input.uv, _BaseMap);
            float2 parallaxUv = H8ModuleResolveParallaxUv(baseUv, input.viewDirWS, input.normalWS, input.tangentWS, quality);
            float2 parallaxRawDelta = (parallaxUv - baseUv) * H8ModuleSafeRcp2(_BaseMap_ST.xy);
            float2 maskUv = TRANSFORM_TEX(input.uv + parallaxRawDelta, _MaskMap);
            float2 normalUv = TRANSFORM_TEX(input.uv + parallaxRawDelta, _BumpMap);

            half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, parallaxUv) * (half4)_BaseColor;
            half4 packedMask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, maskUv);

            half metallic;
            half smoothness;
            half occlusionMap;
            H8ModuleDecodeMask(packedMask, metallic, smoothness, occlusionMap);

            half3 albedo = baseSample.rgb;
            half3 normalWS = H8ModuleNormalWS(input.normalWS, input.tangentWS, normalUv, quality);

            // ---- Channel A: seam gate + paint adhesion ------------------------------------
            half seamEmissive;
            half paintAdhesion;
            H8ModuleResolveSeam(wearColor.a, seamEmissive, paintAdhesion);
            seamEmissive *= channelTrust;
            paintAdhesion *= channelTrust;

            // ---- Channel R: exposed edge wear, metal through paint at the chamfers ---------
            // `3DMODEL_HARD_SURFACE_MODULES.md:83` - high on exposed convex bevels, low on
            // protected flat fields. Intact paint (A below the seam threshold) holds the coating
            // and suppresses the reveal, so chamfers (A = 0, wear coefficient 1.00) strip first
            // and panel fields (A = 0.45, coefficient 0.35) stay painted.
            half edgeContrast = 1.0h + (half)saturate(_ModuleEdgeResponse.w);
            half edgeRaw = saturate(wearColor.r * (half)saturate(_ModuleWearParams.x) * channelTrust * edgeContrast);
            half edge = saturate(edgeRaw * lerp(1.0h, 1.0h - (half)saturate(_ModuleSeamParams.z), paintAdhesion));

            albedo = lerp(albedo, (half3)_ModuleEdgeMetalColor.rgb, edge * (half)saturate(_ModuleEdgeResponse.z));
            metallic = saturate(lerp(metallic, 1.0h, edge * (half)saturate(_ModuleEdgeResponse.x)));
            smoothness = saturate(lerp(smoothness, 1.0h, edge * (half)saturate(_ModuleEdgeResponse.y)));

            // ---- Channel G: oxidation above, biofilm below --------------------------------
            // One channel carries both states, which `3dmodel.md:124` explicitly allows ("rust,
            // oxidation, biofilm, or fluid stain"). The split is the world normal's up-ness, which
            // mirrors the generator's own reasoning at ModuleArchitect1712.cs:1389: upward faces
            // are salt-polished and rain-washed, downward faces trap water and silt.
            half upness = saturate((half)normalWS.y * 0.5h + 0.5h);
            half3 stainTint = lerp((half3)_ModuleBiofilmColor.rgb, (half3)_ModuleOxideColor.rgb, upness);
            half stain = saturate(wearColor.g * (half)saturate(_ModuleWearParams.y) * channelTrust);

            albedo = lerp(albedo, albedo * stainTint, stain * (half)saturate(_ModuleOxideResponse.w));
            smoothness = saturate(smoothness * lerp(1.0h, 1.0h - (half)saturate(_ModuleOxideResponse.x), stain));
            metallic = saturate(metallic * lerp(1.0h, 1.0h - (half)saturate(_ModuleOxideResponse.y), stain));

            // Existing project rust/silt breakup, continuous in quality so compact keeps the wear
            // read while high tiers gain the noise detail. Fed with the real vertex edge mask
            // instead of a screen-space guess.
            half rustSiltQuality = (half)lerp(0.35, 1.0, quality);
            HectonCoreLitApplyProceduralRustSilt(
                input.positionWS,
                normalWS,
                normalWS,
                saturate(edge + stain * 0.45h),
                saturate(stain * 0.65h + edge * 0.35h),
                (half)saturate(_ModuleRustSiltParams.x) * rustSiltQuality,
                (half)saturate(_ModuleRustSiltParams.y) * rustSiltQuality,
                (half3)_ModuleSiltTint.rgb,
                (half3)_ModuleOxideColor.rgb,
                albedo,
                metallic,
                smoothness);

            // ---- Channel B: analytic cavity darkness, MULTIPLIED with the occlusion map ----
            // `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt:258` mandates a multiplicative AO
            // stack. The occlusion floor at _ModuleNoirParams.w exists because that same mandate
            // (Section0, Section11) forbids pure black on scene geometry.
            // Continuous, never a branch: at quality 0 the cavity layer keeps (1 - micro) of its
            // authority so compact hardware still reads the recesses; at quality 1 it is full.
            half cavityMicro = (half)saturate(_ModuleNoirParams.z);
            half cavityWeight = (half)saturate(_ModuleWearParams.z) * channelTrust * lerp(1.0h - cavityMicro, 1.0h, (half)quality);
            half vertexExposure = HectonCoreLitResolveVertexAmbientOcclusion((float)wearColor.b);
            half occlusionVertex = saturate(lerp(1.0h, vertexExposure, cavityWeight));
            half occlusion = max(occlusionMap * occlusionVertex, (half)saturate(_ModuleNoirParams.w));

            // ---- Channel A emission -------------------------------------------------------
            half3 emission = (half3)_ModuleSeamEmissionColor.rgb * seamEmissive * (half)max(_ModuleSeamParams.w, 0.0);

            half3 lit = H8ModuleLighting(
                input.positionWS,
                input.positionCS,
                input.shadowCoord,
                input.fogFactor,
                input.viewDirWS,
                albedo,
                normalWS,
                metallic,
                smoothness,
                occlusion,
                emission);

            half3 finalColor = MixFog(lit, input.fogFactor);
            return half4(finalColor, 1.0h);
        }

        DepthNormalsVaryings DepthNormalsVert(Attributes input)
        {
            DepthNormalsVaryings output = (DepthNormalsVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            output.positionCS = positionInputs.positionCS;
            output.normalWS = H8ModuleSafeNormalize((half3)normalInputs.normalWS, half3(0.0h, 1.0h, 0.0h));
            output.tangentWS = half4(H8ModuleSafeNormalize((half3)normalInputs.tangentWS, half3(1.0h, 0.0h, 0.0h)), input.tangentOS.w);
            output.uv = input.uv;
            return output;
        }

        half4 DepthNormalsFrag(DepthNormalsVaryings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float quality = H8ModuleQuality();
            float2 normalUv = TRANSFORM_TEX(input.uv, _BumpMap);
            half3 normalWS = H8ModuleNormalWS(input.normalWS, input.tangentWS, normalUv, quality);
            return half4(normalWS, 0.0h);
        }

        DepthOnlyVaryings DepthOnlyVert(Attributes input)
        {
            DepthOnlyVaryings output = (DepthOnlyVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            return output;
        }

        half4 DepthOnlyFrag(DepthOnlyVaryings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            return 0;
        }

        DepthOnlyVaryings ShadowVert(Attributes input)
        {
            DepthOnlyVaryings output = (DepthOnlyVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
        #if _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 lightDirectionWS = normalize(_LightPosition - positionWS);
        #else
            float3 lightDirectionWS = _LightDirection;
        #endif
            float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            positionCS = ApplyShadowClamping(positionCS);
            output.positionCS = positionCS;
            return output;
        }

        half4 ShadowFrag(DepthOnlyVaryings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            return 0;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            // URP 17.5.0 renamed the Forward+ light-loop keyword to _CLUSTER_LIGHT_LOOP
            // (com.unity.render-pipelines.universal@17.5.0 Shaders/Lit.shader:57). Without it,
            // additional lights are silently empty on a Forward+ renderer - and Forward+ is
            // required by `REND_GPU_Sovereignty.txt:25` for the GPU Resident Drawer path these
            // modules sit on, so the keyword is mandatory, not optional.
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            // Variant budget (`shaders.md` Variant And Performance Law, `COMMON_SENSE.md` 16):
            // soft shadows, lightmaps, shadowmask, additional-light shadows and URP SSAO are
            // pruned. URP SSAO is separately forbidden by
            // `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt:7`. Same pruning shape as the
            // shipping Hecton8/World/WreckIndirectLit ForwardLit pass.
            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED SHADOWS_SHADOWMASK LIGHTMAP_SHADOW_MIXING _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH _SCREEN_SPACE_OCCLUSION _REFLECTION_PROBE_BLENDING _REFLECTION_PROBE_BOX_PROJECTION
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthOnlyVert
            #pragma fragment DepthOnlyFrag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON
            ENDHLSL
        }
    }

    FallBack Off
}
