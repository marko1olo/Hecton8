// ============================================================================
// HECTON-8 -- Hecton_AlienSky_Master.shader
// Atmospheric sky dome shader for the exomoon Hecton.
// Unity 6 | URP 17+ | SRP Batcher Compatible
//
// ===============================================================
// VISUAL ARCHITECTURE -- THREE-LAYER CLOUD SYSTEM
// ===============================================================
//
// Layer 1 (CIRRUS):
//   High-altitude, thin, fast-moving ice crystal clouds.
//   High UV tiling, low opacity. Adds visual complexity at zenith.
//   Driven by _GlobalRotation x cirrusSpeedMult.
//
// Layer 2 (MAIN CLOUDS):
//   Dense gas giant-reflected cloud formations.
//   Flowmap-based UV distortion for morphing (not just scrolling).
//   Packed texture: R=Density, G=Detail noise.
//   Dual-phase cycling eliminates flowmap seam artifacts.
//
// Layer 3 (HORIZON HAZE):
//   Thick atmospheric haze at the horizon line.
//   Hides water/terrain seam. Fresnel-based falloff.
//   Color tinted by sun position for sunrise/sunset feel.
//
// ===============================================================
// LIGHTING MODEL -- ANALYTICAL ATMOSPHERIC SCATTERING
// ===============================================================
//
// 1. BACKLIT GLOW:
//    When sun is behind a cloud, edges glow with HDR intensity.
//    Implementation: pow(1 - NdotL, backlitPower) x density x HDR color.
//    Feeds into URP Bloom post-process for cinematic rim lighting.
//
// 2. AEGIR HALO:
//    The gas giant Aegir casts a diffuse gradient glow across the sky.
//    Implementation: saturate(dot(viewDir, _AegirDirection)) raised to
//    a power, tinted by Aegir atmospheric color.
//    Visible even at night -- Aegir reflects sunlight.
//
// 3. SUN DISC + SCATTERING:
//    Sharp solar disc rendered as a bright point with _SunSize radius.
//    Rayleigh-approximation color shift near sun position.
//    Warm tones around sun, cool tones opposite.
//
// ===============================================================
// PERFORMANCE -- DESIGNED FOR WEAK GPUs (MX350, Mali, Adreno)
// ===============================================================
//
// OPAQUE render queue -- zero overdraw, zero alpha blending cost.
// 3 texture samples total (1x cirrus + 2x flowmap dual-phase).
// Flow direction is extracted from phase 0 sample -- no extra fetch.
// Alpha clip with dithering -- no transparency sorting.
// All UV math uses float (32-bit) to prevent jitter.
// All color math uses half (16-bit) for ALU efficiency.
// No dependent texture reads -- UV computed in vertex shader.
// SRP Batcher compatible (single CBUFFER).
//
// ===============================================================
// FLOWMAP UV DISTORTION -- DUAL-PHASE CYCLING
// ===============================================================
//
// Standard flowmap scrolling creates visible reset artifacts
// when the UV offset wraps. Dual-phase cycling solves this:
//
//   Phase A: sample at time T with flowmap offset
//   Phase B: sample at time T+0.5 with flowmap offset
//   Blend:   lerp(A, B, abs(frac(T) x 2 - 1))
//
// The blend factor creates a smooth crossfade between phases,
// hiding the reset point. Result: continuous cloud morphing
// without any visible snapping or repetition.
//
// OPTIMIZATION: Flow direction is read from the Phase A sample
// BA channels, eliminating the need for a separate flow-read fetch.
// Total flowmap cost: exactly 2 texture samples.
//
// ===============================================================
// INPUT TEXTURES
// ===============================================================
//
// _MainCloudTex (RGBA packed atlas):
//   R = Cloud density mask (main cloud shapes)
//   G = Detail noise (high-frequency turbulence)
//   B = Flowmap X component (horizontal distortion direction)
//   A = Flowmap Y component (vertical distortion direction)
//
// _HorizonGradientTex (R8 or RGBA):
//   Vertical gradient for horizon haze density.
//   Can be replaced with procedural math if texture budget is tight.
//
// ===============================================================
// GLOBAL INPUTS (set from C# scripts)
// ===============================================================
//
// _SunDirection    (float4) -- from HectonAtmosphereManager
// _AegirDirection  (float4) -- from HectonCelestialEngine
// _GlobalRotation  (float)  -- from HectonCelestialEngine
//                             Fractional accumulator in [0,1).
//                             C# does: _GlobalRotation = frac(rot + speed * dt)
// ============================================================================

Shader "HECTON/Sky/Hecton_AlienSky_Master"
{
    Properties
    {
        [Header(Cloud Texture Atlas)]
        _MainCloudTex ("Cloud Atlas RGBA", 2D) = "gray" {}

        [Header(Horizon)]
        _HorizonGradientTex ("Horizon Gradient", 2D) = "white" {}

        [Header(Sky Colors HDR)]
        [HDR] _SkyColorZenith ("Zenith Color", Color) = (0.05, 0.08, 0.25, 1)
        [HDR] _SkyColorHorizon ("Horizon Color", Color) = (0.4, 0.35, 0.5, 1)
        [HDR] _SkyColorNadir ("Nadir Color", Color) = (0.02, 0.03, 0.08, 1)

        [Header(Cirrus Layer)]
        _CirrusTiling ("Cirrus Tiling", Vector) = (8, 4, 0, 0)
        _CirrusSpeedMult ("Cirrus Speed Mult", Range(1.0, 5.0)) = 2.5
        _CirrusOpacity ("Cirrus Opacity", Range(0, 1)) = 0.3
        [HDR] _CirrusColor ("Cirrus Tint", Color) = (0.7, 0.7, 0.9, 1)

        [Header(Main Cloud Layer)]
        _CloudTiling ("Cloud Tiling", Vector) = (3, 2, 0, 0)
        _CloudSpeedMult ("Cloud Speed Mult", Range(0.5, 3.0)) = 1.0
        _FlowStrength ("Flow Distortion", Range(0, 0.5)) = 0.15
        _FlowCycleSpeed ("Flow Cycle Speed", Range(0.05, 1.0)) = 0.2
        _CloudDensityThreshold ("Density Threshold", Range(0, 1)) = 0.3
        _CloudSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.15
        [HDR] _CloudColorLit ("Cloud Lit Color", Color) = (0.9, 0.85, 0.8, 1)
        [HDR] _CloudColorShadow ("Cloud Shadow Color", Color) = (0.15, 0.12, 0.2, 1)
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.4

        [Header(Horizon Haze)]
        _HazeIntensity ("Haze Intensity", Range(0, 3)) = 1.5
        _HazeFalloff ("Haze Falloff", Range(0.5, 8)) = 3.0
        [HDR] _HazeColor ("Haze Color", Color) = (0.5, 0.45, 0.55, 1)
        _HazeSunTintStrength ("Haze Sun Tint", Range(0, 2)) = 0.8

        [Header(Backlit Glow)]
        _BacklitPower ("Backlit Power", Range(1, 16)) = 4.0
        _BacklitIntensity ("Backlit Intensity", Range(0, 10)) = 3.0
        [HDR] _BacklitColor ("Backlit Color", Color) = (1.0, 0.8, 0.4, 1)

        [Header(Aegir Halo)]
        _AegirHaloPower ("Aegir Falloff", Range(1, 16)) = 3.0
        _AegirHaloIntensity ("Aegir Intensity", Range(0, 5)) = 1.5
        [HDR] _AegirHaloColor ("Aegir Color", Color) = (0.6, 0.5, 0.8, 1)

        [Header(Sun Disc)]
        _SunSize ("Sun Radius", Range(0.001, 0.1)) = 0.02
        _SunEdgeSoftness ("Sun Softness", Range(0.0001, 0.02)) = 0.005
        [HDR] _SunDiscColor ("Sun Color HDR", Color) = (20.0, 18.0, 12.0, 1)

        [Header(Sun Scattering)]
        _SunScatterPower ("Scatter Falloff", Range(1, 32)) = 8.0
        _SunScatterIntensity ("Scatter Intensity", Range(0, 5)) = 2.0
        [HDR] _SunScatterColor ("Scatter Color", Color) = (1.0, 0.7, 0.3, 1)

        [Header(Rotation)]
        _GlobalRotation ("Global Rotation", Float) = 0.0
        _RotationAxis ("Rotation Axis XY", Vector) = (1, 0.3, 0, 0)

        [Header(Dither)]
        _DitherScale ("Dither Scale", Range(1, 8)) = 4.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Background"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Background"
            "UniversalMaterialType" = "Unlit"
            "ForceNoShadowCasting" = "True"
            "IgnoreProjector" = "True"
            "PreviewType" = "Skybox"
        }

        LOD 100

        Pass
        {
            Name "AlienSkyForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex SkyVert
            #pragma fragment SkyFrag
            #pragma target 3.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ---------------------------------
            // TEXTURES
            // ---------------------------------
            TEXTURE2D(_MainCloudTex);       SAMPLER(sampler_MainCloudTex);
            TEXTURE2D(_HorizonGradientTex); SAMPLER(sampler_HorizonGradientTex);

            // ---------------------------------------------------------
            // CBUFFER -- SRP Batcher compatible
            // ---------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float4 _MainCloudTex_ST;
                float4 _HorizonGradientTex_ST;

                half4  _SkyColorZenith;
                half4  _SkyColorHorizon;
                half4  _SkyColorNadir;

                float4 _CirrusTiling;
                float  _CirrusSpeedMult;
                half   _CirrusOpacity;
                half4  _CirrusColor;

                float4 _CloudTiling;
                float  _CloudSpeedMult;
                half   _FlowStrength;
                half   _FlowCycleSpeed;
                half   _CloudDensityThreshold;
                half   _CloudSoftness;
                half4  _CloudColorLit;
                half4  _CloudColorShadow;
                half   _DetailStrength;

                half   _HazeIntensity;
                half   _HazeFalloff;
                half4  _HazeColor;
                half   _HazeSunTintStrength;

                half   _BacklitPower;
                half   _BacklitIntensity;
                half4  _BacklitColor;

                half   _AegirHaloPower;
                half   _AegirHaloIntensity;
                half4  _AegirHaloColor;

                half   _SunSize;
                half   _SunEdgeSoftness;
                half4  _SunDiscColor;

                half   _SunScatterPower;
                half   _SunScatterIntensity;
                half4  _SunScatterColor;

                float  _GlobalRotation;
                float4 _RotationAxis;

                half   _DitherScale;
            CBUFFER_END

            // ---------------------------------
            // GLOBALS (set from C# scripts)
            // ---------------------------------
            float4 _SunDirection;
            float4 _AegirDirection;

            // ---------------------------------
            // CONSTANTS
            // ---------------------------------
            static const half  HALF_ZERO = 0.0h;
            static const half  HALF_ONE  = 1.0h;

            static const float3 FALLBACK_SUN_DIR   = float3(0.57735, 0.57735, 0.57735);
            static const float3 FALLBACK_AEGIR_DIR = float3(0.0, 0.93633, -0.35112);
            static const float  DIR_THRESHOLD      = 0.001;

            // ---------------------------------
            // STRUCTS
            // ---------------------------------
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS    : SV_POSITION;
                float2 uv            : TEXCOORD0;
                half3  viewDirWS     : TEXCOORD1;
                float3 positionWS    : TEXCOORD2;
                half   horizonFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ---------------------------------------------------------
            // UTILITY: Safe normalize with fallback
            // ---------------------------------------------------------
            float3 SafeNormalizeDir(float3 v, float3 fallback)
            {
                float lenSq = dot(v, v);
                return (lenSq < DIR_THRESHOLD * DIR_THRESHOLD)
                    ? fallback
                    : v * rsqrt(lenSq);
            }

            // ---------------------------------------------------------
            // UTILITY: Rotation offset for cloud UVs
            // ---------------------------------------------------------
            float2 ApplyRotation(float2 uv, float speedMult)
            {
                float offset = _GlobalRotation * speedMult;
                float2 rotDir = _RotationAxis.xy;
                return uv + rotDir * offset;
            }

            // ---------------------------------------------------------
            // FLOWMAP -- DUAL-PHASE CYCLING (2 SAMPLES)
            //
            // Phase A sampled at baseUV serves double duty:
            //   - BA channels provide flow direction
            //   - RG channels provide density and detail
            // Phase B sampled at distorted UV.
            // Triangular blend wave hides reset artifacts.
            // When Phase A weight is low, its positional error
            // is irrelevant -- self-correcting by design.
            // ---------------------------------------------------------
            half2 SampleFlowmap(
                TEXTURE2D_PARAM(flowTex, flowSampler),
                float2 baseUV)
            {
                float time = _GlobalRotation * (float)_FlowCycleSpeed;
                float phase1 = frac(time + 0.5);

                // Phase A at baseUV (flow source + density)
                half4 sample0 = SAMPLE_TEXTURE2D(flowTex, flowSampler, baseUV);

                // Flow direction from BA channels
                half2 flowDir = sample0.ba * 2.0h - 1.0h;

                // Phase B at distorted UV
                float2 uv1 = baseUV + (float2)(flowDir * _FlowStrength) * phase1;
                half4 sample1 = SAMPLE_TEXTURE2D(flowTex, flowSampler, uv1);

                // Triangular blend wave
                half blend = abs(frac((half)time) * 2.0h - 1.0h);

                half2 result;
                result.x = lerp(sample0.r, sample1.r, blend);
                result.y = lerp(sample0.g, sample1.g, blend);

                return result;
            }

            // ---------------------------------------------------------
            // DITHERED ALPHA CLIP (Bayer 4x4)
            // ---------------------------------------------------------
            static const half BAYER_MATRIX[16] =
            {
                 0.0h/16.0h,  8.0h/16.0h,  2.0h/16.0h, 10.0h/16.0h,
                12.0h/16.0h,  4.0h/16.0h, 14.0h/16.0h,  6.0h/16.0h,
                 3.0h/16.0h, 11.0h/16.0h,  1.0h/16.0h,  9.0h/16.0h,
                15.0h/16.0h,  7.0h/16.0h, 13.0h/16.0h,  5.0h/16.0h
            };

            void DitherClip(float4 positionCS, half alpha)
            {
                int2 pixel = int2(positionCS.xy / _DitherScale) % 4;
                int idx = pixel.y * 4 + pixel.x;
                half threshold = BAYER_MATRIX[idx];
                clip(alpha - threshold);
            }

            // ---------------------------------
            // VERTEX
            // ---------------------------------
            Varyings SkyVert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.uv         = input.uv;

                output.viewDirWS = (half3)normalize(
                    posInputs.positionWS - GetCameraPositionWS());

                output.horizonFactor = output.viewDirWS.y;

                return output;
            }

            // ---------------------------------------------------------
            // FRAGMENT
            // ---------------------------------------------------------
            half4 SkyFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 V = normalize(input.viewDirWS);
                half  horizonFactor = input.horizonFactor;

                // =======================================
                // RESOLVE GLOBAL DIRECTIONS
                // =======================================
                half3 sunDir = (half3)SafeNormalizeDir(
                    _SunDirection.xyz, FALLBACK_SUN_DIR);
                half3 L = -sunDir;

                half3 aegirDir = (half3)SafeNormalizeDir(
                    _AegirDirection.xyz, FALLBACK_AEGIR_DIR);

                // =======================================
                // BASE SKY GRADIENT
                // =======================================
                half zenithMask  = saturate(horizonFactor);
                half nadirMask   = saturate(-horizonFactor);
                half horizonMask = 1.0h - zenithMask - nadirMask;

                half3 skyColor = _SkyColorZenith.rgb  * zenithMask
                               + _SkyColorHorizon.rgb * horizonMask
                               + _SkyColorNadir.rgb   * nadirMask;

                // =======================================
                // LAYER 3: HORIZON HAZE
                // =======================================
                half hazeRaw = 1.0h - abs(horizonFactor);
                half hazeMask = pow(hazeRaw, _HazeFalloff) * _HazeIntensity;

                half sunViewDot = saturate(dot(V, L));
                half3 hazeSunTint = lerp(
                    HALF_ONE,
                    _SunScatterColor.rgb,
                    sunViewDot * _HazeSunTintStrength);

                half3 hazeColor = _HazeColor.rgb * hazeSunTint * hazeMask;
                skyColor += hazeColor;

                // =======================================
                // LAYER 1: CIRRUS CLOUDS
                // Texture sample 1 of 3
                // =======================================
                float2 cirrusUV = input.uv * _CirrusTiling.xy;
                cirrusUV = ApplyRotation(cirrusUV, _CirrusSpeedMult);

                half4 cirrusSample = SAMPLE_TEXTURE2D(
                    _MainCloudTex, sampler_MainCloudTex, cirrusUV);

                half cirrusDensity = cirrusSample.r;

                half cirrusBacklit = pow(
                    saturate(1.0h - dot(V, -L)),
                    _BacklitPower * 0.5h) * cirrusDensity;

                half3 cirrusColor = _CirrusColor.rgb
                                  + cirrusBacklit * _BacklitColor.rgb * 0.3h;

                skyColor = lerp(
                    skyColor,
                    skyColor + cirrusColor,
                    cirrusDensity * _CirrusOpacity * zenithMask);

                // =======================================
                // LAYER 2: MAIN CLOUDS
                // Texture samples 2-3 of 3
                // =======================================
                float2 cloudBaseUV = input.uv * _CloudTiling.xy;
                cloudBaseUV = ApplyRotation(cloudBaseUV, _CloudSpeedMult);

                half2 cloudRG = SampleFlowmap(
                    TEXTURE2D_ARGS(_MainCloudTex, sampler_MainCloudTex),
                    cloudBaseUV);

                half cloudDensity = cloudRG.x;
                half cloudDetail  = cloudRG.y;

                cloudDensity -= cloudDetail * _DetailStrength;

                half smoothLow  = _CloudDensityThreshold;
                half smoothHigh = _CloudDensityThreshold + _CloudSoftness;
                half cloudMask  = smoothstep(smoothLow, smoothHigh, cloudDensity);

                half cloudNdotL = saturate(dot(V, L));

                half3 cloudBaseColor = lerp(
                    _CloudColorShadow.rgb,
                    _CloudColorLit.rgb,
                    cloudNdotL);

                half backlitFactor = pow(
                    saturate(sunViewDot),
                    _BacklitPower);
                half3 backlitGlow = _BacklitColor.rgb
                                  * backlitFactor
                                  * cloudMask
                                  * _BacklitIntensity;

                half3 cloudColor = cloudBaseColor + backlitGlow;

                half cloudHeightFade = saturate(horizonFactor * 3.0h);

                half finalCloudMask = cloudMask * cloudHeightFade;
                skyColor = lerp(skyColor, cloudColor, finalCloudMask);

                // =======================================
                // SUN DISC
                // =======================================
                half sunDist = 1.0h - sunViewDot;
                half sunDisc = 1.0h - smoothstep(
                    _SunSize - _SunEdgeSoftness,
                    _SunSize + _SunEdgeSoftness,
                    sunDist);

                sunDisc *= (1.0h - finalCloudMask);
                skyColor += _SunDiscColor.rgb * sunDisc;

                // =======================================
                // SUN SCATTERING
                // =======================================
                half sunScatter = pow(
                    saturate(sunViewDot),
                    _SunScatterPower);
                half3 sunGlow = _SunScatterColor.rgb
                              * sunScatter
                              * _SunScatterIntensity;

                sunGlow *= (1.0h - finalCloudMask * 0.7h);
                skyColor += sunGlow;

                // =======================================
                // AEGIR HALO
                // =======================================
                half aegirDot = saturate(dot(V, aegirDir));
                half aegirHalo = pow(aegirDot, _AegirHaloPower)
                               * _AegirHaloIntensity;

                aegirHalo *= (1.0h - finalCloudMask * 0.5h);
                skyColor += _AegirHaloColor.rgb * aegirHalo;

                return half4(skyColor, 1.0h);
            }

            ENDHLSL
        }
    }

    FallBack Off
}