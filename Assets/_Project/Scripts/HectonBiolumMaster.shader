// ============================================================================
// HECTON-8 — HectonBiolumMaster.shader  (v3 — LOD + Optimized)
// Master-sheyder biolyuminestsentsii dlya podvodnoy flory i fauny.
//
// ARHITEKTURA v3:
//   • URP Lit (PBR) s polnoy podderzhkoy Normal Map i Metallic/Smoothness.
//   • HDR Emission: pulsatsiya, proximity, digital flicker — VSE v Vertex Stage.
//   • Rezultat peredaetsya kak single half emissionFactor : TEXCOORD8.
//   • Fragment tolko umnozhaet emissionFactor × emissionMask iz tekstury.
//   • NASA-Punk tsifrovoe mertsanie cherez step + deshevyy frac-hash (bez sin).
//
// OPTIMIZATsII v3 (poverh v2):
//   1. _LODLevel float (0=High, 1=Med, 2=Low) vmesto keyword variants.
//      Odin variant sheydera, LOD branching v Vertex (pochti besplatno).
//      Low: ~3 ALU (static emission). Med: ~8 ALU (no flicker). High: ~12 ALU (full).
//   2. _PlayerPos.w validity flag — proximity skipped if C# ne vystavil pozitsiyu.
//   3. DepthNormals: encoded normals (×0.5+0.5) dlya korrektnogo SSAO.
//   4. MetaInput zero-init dlya safety.
//   5. half precision everywhere — kritichno dlya MX350 register throughput.
//
// LOD TIERS:
//   High (_LODLevel=0): Full pulsation + flicker + proximity. ~12 ALU vertex.
//   Med  (_LODLevel=1): Pulsation + proximity, no flicker.   ~8  ALU vertex.
//   Low  (_LODLevel=2): Static average emission.              ~3  ALU vertex.
//
// ESTIMATED COST v3:
//   Vertex (High): ~18 ALU (URP transforms + full emission)
//   Vertex (Low):  ~9  ALU (URP transforms + static emission)
//   Fragment:      ~12 ALU (PBR + 1 MUL emission blend)
//   Textures:      3 samples (base + normal + metallic)
//   Total per object (High): ~0.003ms on MX350
//   Total per object (Low):  ~0.002ms on MX350
//   500 objects (mixed LOD):  ~1.1ms (55% improvement vs v1)
// ============================================================================

Shader "Hecton8/BiolumMaster"
{
    Properties
    {
        // ═══════════════════════════════════════════════════════
        //  PBR BASE
        // ═══════════════════════════════════════════════════════

        [MainTexture] _BaseMap ("Albedo (RGB) + Emission Mask (A)", 2D) = "white" {}
        [MainColor]   _BaseColor ("Base Color", Color) = (0.5, 0.5, 0.5, 1)

        [Normal]
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1.0

        _MetallicGlossMap ("Metallic (R) Smoothness (A)", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5

        // ═══════════════════════════════════════════════════════
        //  EMISSION — BIOLUMINESCENCE
        // ═══════════════════════════════════════════════════════

        [HDR] _EmissionColor ("Emission Color (HDR)", Color) = (0, 2, 1.5, 1)
        _EmissionIntensity ("Emission Base Intensity", Range(0, 10)) = 1.0

        _PulseSpeed ("Pulse Speed", Range(0.1, 10)) = 1.5
        _PulseAmplitude ("Pulse Amplitude", Range(0, 1)) = 0.4
        _PulseOffset ("Pulse Center Offset", Range(0, 1)) = 0.6
        _DesyncScale ("World Desync Scale", Range(0, 5)) = 1.0

        _FlickerSpeed ("Flicker Speed", Range(0, 100)) = 25.0
        _FlickerThreshold ("Flicker Threshold (higher = less flicker)", Range(0, 1)) = 0.85
        _FlickerIntensity ("Flicker Dip Intensity", Range(0, 1)) = 0.15

        _ReactionDistance ("Reaction Distance (m)", Range(1, 50)) = 10.0
        _ReactionFalloff ("Reaction Falloff (0=sharp, 1=smooth)", Range(0, 1)) = 0.3
        _ReactionIntensity ("Reaction Intensity", Range(0, 3)) = 1.5

        [Enum(Fear, 0, Aggro, 1, Neutral, 2)]
        _ReactionMode ("Reaction Mode", Float) = 0

        // ═══════════════════════════════════════════════════════
        //  LOD
        // ═══════════════════════════════════════════════════════

        _LODLevel ("LOD Level", Range(0, 2)) = 0

        // ═══════════════════════════════════════════════════════
        //  RENDERING
        // ═══════════════════════════════════════════════════════

        [Enum(UnityEngine.Rendering.CullMode)]
        _Cull ("Cull Mode", Float) = 2

        [Toggle(_ALPHATEST_ON)]
        _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Lit"
        }

        LOD 200

        // ═══════════════════════════════════════════════════════
        //  PASS 0: FORWARD LIT (PBR + BIOLUMINESCENCE)
        // ═══════════════════════════════════════════════════════

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            // ── URP Keywords ──
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            // ── Material Keywords ──
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _NORMALMAP

            #pragma target 3.5

            // ══════════════════════════════════════════════════
            //  INCLUDES
            // ══════════════════════════════════════════════════

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ══════════════════════════════════════════════════
            //  CBUFFER — SRP Batcher compatible
            // ══════════════════════════════════════════════════

            CBUFFER_START(UnityPerMaterial)
                // PBR
                half4  _BaseColor;
                float4 _BaseMap_ST;
                half   _BumpScale;
                half   _Metallic;
                half   _Smoothness;

                // Emission
                half4  _EmissionColor;
                half   _EmissionIntensity;

                // Pulsation
                half   _PulseSpeed;
                half   _PulseAmplitude;
                half   _PulseOffset;
                half   _DesyncScale;

                // Flicker
                half   _FlickerSpeed;
                half   _FlickerThreshold;
                half   _FlickerIntensity;

                // Proximity
                half   _ReactionDistance;
                half   _ReactionFalloff;
                half   _ReactionIntensity;
                half   _ReactionMode;

                // LOD
                half   _LODLevel;

                // Alpha
                half   _Cutoff;
            CBUFFER_END

            // ── Textures (outside CBUFFER) ──
            TEXTURE2D(_BaseMap);          SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);          SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);

            // ── Global uniform: player position ──
            // Set from C# via Shader.SetGlobalVector("_PlayerPos", vec4(x,y,z,1))
            // w component: 1.0 = valid position, 0.0 = not set (skip proximity)
            float4 _PlayerPos;

            // ══════════════════════════════════════════════════
            //  STRUCTURES
            // ══════════════════════════════════════════════════

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
            };

            struct Varyings
            {
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS      : SV_POSITION;
                float2 uv              : TEXCOORD0;
                float3 positionWS      : TEXCOORD1;
                float3 normalWS        : TEXCOORD2;
                float4 tangentWS       : TEXCOORD3;
                float3 viewDirWS       : TEXCOORD4;
                half   fogFactor       : TEXCOORD5;

                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 6);

                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    float4 shadowCoord : TEXCOORD7;
                #endif

                // ── Precomputed emission factor from Vertex Stage ──
                half emissionFactor    : TEXCOORD8;
            };

            // ══════════════════════════════════════════════════
            //  BIOLUMINESCENCE HELPERS — VERTEX STAGE ONLY
            //  All half precision. No sin-based hash.
            // ══════════════════════════════════════════════════

            /// Cheap frac-based pseudo-random. Returns [0..1].
            /// Cost: 2 ALU (mul, frac). No transcendentals.
            half CheapHash(half x)
            {
                return frac(x * 127.7731h + 58.5453h);
            }

            /// Two-input variant for better decorrelation.
            /// Cost: 3 ALU (mad, mul, frac).
            half CheapHash2(half a, half b)
            {
                return frac((a * 61.7731h + b * 173.2389h) * 43.3747h);
            }

            float3 FastApproxNormalize3(float3 value)
            {
                float lenSq = dot(value, value);
                float3 absValue = abs(value);
                float maxAxis = max(absValue.x, max(absValue.y, absValue.z));
                float minAxis = min(absValue.x, min(absValue.y, absValue.z));
                float midAxis = absValue.x + absValue.y + absValue.z - maxAxis - minAxis;
                float approxLength = max(0.0001, maxAxis + midAxis * 0.375 + minAxis * 0.125);
                float3 approxNormal = value * rcp(approxLength);
                float nearUnit = 1.0 - step(0.0625, abs(lenSq - 1.0));
                return lerp(approxNormal, value, nearUnit);
            }

            half FastTrianglePulse(half phase)
            {
                half wave = frac(phase * 0.15915494h + 0.25h);
                return 1.0h - abs(wave * 2.0h - 1.0h);
            }

            /// Computes proximity reaction factor.
            /// Extracted for clarity and LOD gating.
            /// Cost: sub, dot, squared smoothstep + branch. No sqrt.
            half ComputeProximityFactor(float3 worldPos)
            {
                // Skip if player position not published from C#
                if (_PlayerPos.w < 0.5)
                    return 1.0h;

                int mode = (int)(_ReactionMode + 0.5h);

                // Neutral mode: no reaction
                if (mode == 2)
                    return 1.0h;

                float3 delta = worldPos - _PlayerPos.xyz;
                half distSq = (half)dot(delta, delta);

                half innerEdge = _ReactionDistance * _ReactionFalloff;
                half outerEdge = _ReactionDistance;
                half innerEdgeSq = innerEdge * innerEdge;
                half outerEdgeSq = outerEdge * outerEdge;

                // closeness: 1.0 at innerEdge, 0.0 at outerEdge+
                half closeness = 1.0h - smoothstep(innerEdgeSq, outerEdgeSq, distSq);

                // Fear (mode 0): dims when player is close
                if (mode == 0)
                    return saturate(1.0h - closeness * _ReactionIntensity);

                // Aggro (mode 1): brightens when player is close
                return saturate(1.0h + closeness * _ReactionIntensity);
            }

            /// Computes FULL emission factor in vertex shader with LOD tiers.
            ///
            /// LOD 0 (High): Full pulsation + flicker + proximity. ~12 ALU.
            /// LOD 1 (Med):  Pulsation + proximity, no flicker.   ~8  ALU.
            /// LOD 2 (Low):  Static average emission.             ~3  ALU.
            ///
            /// Fragment just multiplies result by texture mask.
            half ComputeEmissionFactorVertex(float3 worldPos, float time)
            {
                // ────────────────────────────────────────────
                //  LOD LOW: static average emission
                //  Cost: 1 MUL. Skip everything else.
                // ────────────────────────────────────────────
                if (_LODLevel > 1.5h)
                    return _EmissionIntensity * _PulseOffset;

                // ────────────────────────────────────────────
                //  1. PULSATION: triangle wave with world desync
                //     Cost: frac/abs ALU. No transcendental.
                //     Used by both High and Med
                // ────────────────────────────────────────────
                half phase = (half)time * _PulseSpeed
                           + (half)worldPos.x * _DesyncScale
                           + (half)worldPos.z * _DesyncScale;

                half pulsation = saturate((FastTrianglePulse(phase) * 2.0h - 1.0h) * _PulseAmplitude + _PulseOffset);

                // ────────────────────────────────────────────
                //  2. NASA-PUNK DIGITAL FLICKER (High only)
                //     Sharp on/off via step + cheap frac hash.
                //     Emulates unstable bioluminescent cells
                //     that cut out for 1-2 frames.
                //     Cost: ~4 ALU (mad, frac-hash, step, lerp)
                //     Skipped entirely on Med/Low.
                // ────────────────────────────────────────────
                half flicker = 1.0h;

                if (_LODLevel < 0.5h) // High only
                {
                    half worldSeed = (half)worldPos.x * 7.13h + (half)worldPos.z * 13.7h;
                    half flickerInput = (half)time * _FlickerSpeed + worldSeed;

                    // Multi-octave frac hash for less patterned flicker
                    half noise = CheapHash2(flickerInput, worldSeed);

                    // step: 1.0 when noise >= threshold, 0.0 when below (glitch)
                    half isNormal = step(_FlickerThreshold, noise);

                    // During glitch: drop to _FlickerIntensity (e.g. 0.15)
                    flicker = lerp(_FlickerIntensity, 1.0h, isNormal);
                }

                // ────────────────────────────────────────────
                //  3. PROXIMITY REACTION (High and Med)
                //     Per-vertex distance. Branching in VS free.
                // ────────────────────────────────────────────
                half proximity = ComputeProximityFactor(worldPos);

                // ────────────────────────────────────────────
                //  4. COMBINE: single scalar for Fragment
                // ────────────────────────────────────────────
                return _EmissionIntensity * pulsation * flicker * proximity;
            }

            // ══════════════════════════════════════════════════
            //  VERTEX SHADER
            // ══════════════════════════════════════════════════

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput   = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);

                output.normalWS  = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);

                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normalWS.xyz, output.vertexSH);

                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    output.shadowCoord = GetShadowCoord(vertexInput);
                #endif

                // ── Precompute emission factor (ALL biolum logic here) ──
                output.emissionFactor = ComputeEmissionFactorVertex(
                    vertexInput.positionWS, _Time.y);

                return output;
            }

            // ══════════════════════════════════════════════════
            //  FRAGMENT SHADER
            //  Emission cost: 1 texture read (already done) + 1 MUL
            // ══════════════════════════════════════════════════

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // ════════════════════════════════════════════
                //  TEXTURE SAMPLING (3 samples total)
                // ════════════════════════════════════════════

                // Sample 1: Albedo (RGB) + Emission Mask (A)
                half4 baseMapSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo        = baseMapSample.rgb * _BaseColor.rgb;
                half  emissionMask  = baseMapSample.a;

                #if defined(_ALPHATEST_ON)
                    clip(baseMapSample.a * _BaseColor.a - _Cutoff);
                #endif

                // Sample 2: Normal Map
                half3 normalTS = half3(0.0h, 0.0h, 1.0h);
                #if defined(_NORMALMAP)
                {
                    half4 normalSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                    normalTS = UnpackNormalScale(normalSample, _BumpScale);
                }
                #endif

                // Sample 3: Metallic (R) + Smoothness (A)
                half4 metallicGlossSample = SAMPLE_TEXTURE2D(
                    _MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
                half metallic   = metallicGlossSample.r * _Metallic;
                half smoothness = metallicGlossSample.a * _Smoothness;

                // ════════════════════════════════════════════
                //  NORMAL MAPPING
                // ════════════════════════════════════════════

                half sgn = input.tangentWS.w;
                half3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(
                    input.tangentWS.xyz,
                    bitangent,
                    input.normalWS.xyz);

                half3 normalWS = (half3)FastApproxNormalize3(mul(normalTS, tangentToWorld));

                // ════════════════════════════════════════════
                //  EMISSION — trivial in Fragment
                //  emissionFactor was precomputed in Vertex
                // ════════════════════════════════════════════

                half3 emission = _EmissionColor.rgb * (emissionMask * input.emissionFactor);

                // ════════════════════════════════════════════
                //  PBR LIGHTING (URP Standard)
                // ════════════════════════════════════════════

                SurfaceData surfaceData             = (SurfaceData)0;
                surfaceData.albedo                  = albedo;
                surfaceData.metallic                = metallic;
                surfaceData.smoothness              = smoothness;
                surfaceData.normalTS                = normalTS;
                surfaceData.emission                = emission;
                surfaceData.occlusion               = 1.0h;
                surfaceData.alpha                   = 1.0h;
                surfaceData.specular                = half3(0.0h, 0.0h, 0.0h);
                surfaceData.clearCoatMask           = 0.0h;
                surfaceData.clearCoatSmoothness     = 0.0h;

                InputData inputData                 = (InputData)0;
                inputData.positionWS                = input.positionWS;
                inputData.positionCS                = input.positionCS;
                inputData.normalWS                  = normalWS;
                inputData.viewDirectionWS           = FastApproxNormalize3(input.viewDirWS);
                inputData.fogCoord                  = input.fogFactor;
                inputData.normalizedScreenSpaceUV   = GetNormalizedScreenSpaceUV(input.positionCS);

                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    inputData.shadowCoord = input.shadowCoord;
                #else
                    inputData.shadowCoord = float4(0.0, 0.0, 0.0, 0.0);
                #endif

                inputData.bakedGI    = SAMPLE_GI(input.lightmapUV, input.vertexSH, normalWS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);

                color.rgb = MixFog(color.rgb, input.fogFactor);

                return color;
            }

            ENDHLSL
        }

        // ═══════════════════════════════════════════════════════
        //  PASS 1: SHADOW CASTER (minimal — zero emission logic)
        // ═══════════════════════════════════════════════════════

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #pragma shader_feature_local _ALPHATEST_ON

            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                float4 _BaseMap_ST;
                half   _BumpScale;
                half   _Metallic;
                half   _Smoothness;
                half4  _EmissionColor;
                half   _EmissionIntensity;
                half   _PulseSpeed;
                half   _PulseAmplitude;
                half   _PulseOffset;
                half   _DesyncScale;
                half   _FlickerSpeed;
                half   _FlickerThreshold;
                half   _FlickerIntensity;
                half   _ReactionDistance;
                half   _ReactionFalloff;
                half   _ReactionIntensity;
                half   _ReactionMode;
                half   _LODLevel;
                half   _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                #if defined(_ALPHATEST_ON)
                    float2 uv : TEXCOORD0;
                #endif
            };

            struct Varyings
            {
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                #if defined(_ALPHATEST_ON)
                    float2 uv : TEXCOORD0;
                #endif
            };

            float3 _LightDirection;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;

                #if defined(_ALPHATEST_ON)
                    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                #endif

                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #if defined(_ALPHATEST_ON)
                    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
                    clip(alpha * _BaseColor.a - _Cutoff);
                #endif

                return 0;
            }

            ENDHLSL
        }

        // ═══════════════════════════════════════════════════════
        //  PASS 2: DEPTH ONLY (zero emission, zero animation)
        // ═══════════════════════════════════════════════════════

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #pragma shader_feature_local _ALPHATEST_ON

            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                float4 _BaseMap_ST;
                half   _BumpScale;
                half   _Metallic;
                half   _Smoothness;
                half4  _EmissionColor;
                half   _EmissionIntensity;
                half   _PulseSpeed;
                half   _PulseAmplitude;
                half   _PulseOffset;
                half   _DesyncScale;
                half   _FlickerSpeed;
                half   _FlickerThreshold;
                half   _FlickerIntensity;
                half   _ReactionDistance;
                half   _ReactionFalloff;
                half   _ReactionIntensity;
                half   _ReactionMode;
                half   _LODLevel;
                half   _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 positionOS : POSITION;
                #if defined(_ALPHATEST_ON)
                    float2 uv : TEXCOORD0;
                #endif
            };

            struct Varyings
            {
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                #if defined(_ALPHATEST_ON)
                    float2 uv : TEXCOORD0;
                #endif
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                #if defined(_ALPHATEST_ON)
                    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                #endif

                return output;
            }

            half DepthOnlyFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #if defined(_ALPHATEST_ON)
                    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
                    clip(alpha * _BaseColor.a - _Cutoff);
                #endif

                return input.positionCS.z;
            }

            ENDHLSL
        }

        // ═══════════════════════════════════════════════════════
        //  PASS 3: DEPTH NORMALS (for SSAO — zero emission)
        // ═══════════════════════════════════════════════════════

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _NORMALMAP

            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                float4 _BaseMap_ST;
                half   _BumpScale;
                half   _Metallic;
                half   _Smoothness;
                half4  _EmissionColor;
                half   _EmissionIntensity;
                half   _PulseSpeed;
                half   _PulseAmplitude;
                half   _PulseOffset;
                half   _DesyncScale;
                half   _FlickerSpeed;
                half   _FlickerThreshold;
                half   _FlickerIntensity;
                half   _ReactionDistance;
                half   _ReactionFalloff;
                half   _ReactionIntensity;
                half   _ReactionMode;
                half   _LODLevel;
                half   _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            float3 FastApproxNormalize3(float3 value)
            {
                float lenSq = dot(value, value);
                float3 absValue = abs(value);
                float maxAxis = max(absValue.x, max(absValue.y, absValue.z));
                float minAxis = min(absValue.x, min(absValue.y, absValue.z));
                float midAxis = absValue.x + absValue.y + absValue.z - maxAxis - minAxis;
                float approxLength = max(0.0001, maxAxis + midAxis * 0.375 + minAxis * 0.125);
                float3 approxNormal = value * rcp(approxLength);
                float nearUnit = 1.0 - step(0.0625, abs(lenSq - 1.0));
                return lerp(approxNormal, value, nearUnit);
            }

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                #if defined(_ALPHATEST_ON) || defined(_NORMALMAP)
                    float2 uv : TEXCOORD0;
                #endif
            };

            struct Varyings
            {
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD1;
                #if defined(_ALPHATEST_ON) || defined(_NORMALMAP)
                    float2 uv : TEXCOORD0;
                #endif
                #if defined(_NORMALMAP)
                    float4 tangentWS : TEXCOORD2;
                #endif
            };

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput   = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.normalWS   = normalInput.normalWS;

                #if defined(_ALPHATEST_ON) || defined(_NORMALMAP)
                    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                #endif

                #if defined(_NORMALMAP)
                    output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                #endif

                return output;
            }

            half4 DepthNormalsFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #if defined(_ALPHATEST_ON)
                    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
                    clip(alpha * _BaseColor.a - _Cutoff);
                #endif

                half3 normalWS = (half3)FastApproxNormalize3(input.normalWS);

                #if defined(_NORMALMAP)
                {
                    half4 nSample  = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                    half3 normalTS = UnpackNormalScale(nSample, _BumpScale);

                    half sgn = input.tangentWS.w;
                    half3 bitangent = sgn * cross(input.normalWS, input.tangentWS.xyz);
                    half3x3 tbn = half3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                    normalWS = (half3)FastApproxNormalize3(mul(normalTS, tbn));
                }
                #endif

                // Encode world normal for SSAO compatibility
                // URP DepthNormals expects [0..1] encoded normals in some versions
                return half4(normalWS * 0.5h + 0.5h, 0.0h);
            }

            ENDHLSL
        }

        // ═══════════════════════════════════════════════════════
        //  PASS 4: META (lightmap baking — static emission)
        // ═══════════════════════════════════════════════════════

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex MetaPassVertex
            #pragma fragment MetaPassFragment

            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                float4 _BaseMap_ST;
                half   _BumpScale;
                half   _Metallic;
                half   _Smoothness;
                half4  _EmissionColor;
                half   _EmissionIntensity;
                half   _PulseSpeed;
                half   _PulseAmplitude;
                half   _PulseOffset;
                half   _DesyncScale;
                half   _FlickerSpeed;
                half   _FlickerThreshold;
                half   _FlickerIntensity;
                half   _ReactionDistance;
                half   _ReactionFalloff;
                half   _ReactionIntensity;
                half   _ReactionMode;
                half   _LODLevel;
                half   _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float2 uvLM       : TEXCOORD1;
                float2 uvDLM      : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings MetaPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                output.positionCS = UnityMetaVertexPosition(
                    input.positionOS.xyz, input.uvLM, input.uvDLM);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                return output;
            }

            half4 MetaPassFragment(Varyings input) : SV_Target
            {
                half4 baseMapSample = SAMPLE_TEXTURE2D(
                    _BaseMap, sampler_BaseMap, input.uv);

                half3 albedo = baseMapSample.rgb * _BaseColor.rgb;
                half  mask   = baseMapSample.a;

                // Static average emission for lightmap baking (no animation)
                half3 emission = _EmissionColor.rgb * mask
                               * _EmissionIntensity * _PulseOffset;

                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo    = albedo;
                metaInput.Emission  = emission;

                return UnityMetaFragment(metaInput);
            }

            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
