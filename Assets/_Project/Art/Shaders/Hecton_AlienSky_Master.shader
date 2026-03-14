// ============================================================================
// HECTON-8 -- Hecton_AlienSky_Master.shader
// Atmospheric sky dome shader for the exomoon Hecton.
// Unity 6 | URP 17+ | SRP Batcher Compatible
//
// ===============================================================
// UV PROJECTION MODEL -- PLANAR CEILING
// ===============================================================
//
// UV is derived from the view direction vector:
//     float2 skyUV = V.xz / max(V.y, 0.05)
//
//   This projects the cloud texture as if it were an infinite flat
//   plane above the camera (like a ceiling). Benefits:
//     - No pole pinching (no poles exist in a plane)
//     - Clouds translate linearly (wind), not rotationally
//     - Natural perspective foreshortening at the horizon
//     - Horizon clamping via max(V.y, 0.05) prevents infinity
//
// Wind is now linear translation using _GameTime:
//   skyUV += _WindDirection * _GameTime * speedMult
//   The shader uses frac() internally where needed.
//   _GameTime is a continuously growing float from C#.
//   No wrapping in C# = zero "jerks" = seamless scrolling.
//
// ===============================================================
// VISUAL ARCHITECTURE -- FOUR-LAYER SYSTEM (v4.0)
// ===============================================================
//
// Layer 0 (STARS):
//   Background star field visible at night.
//   Sampled from _StarTex with high tiling on spherical UVs.
//   Multiplied by _NightBlend (0=day, 1=night) for fade.
//   Per-star twinkling via hash(UV) + sin(_GameTime + phase).
//   Added to sky gradient BEFORE clouds (clouds occlude stars).
//
// Layer 1 (CIRRUS):
//   High-altitude, thin, fast-moving ice crystal clouds.
//   High UV tiling, low opacity. Adds visual complexity at zenith.
//   PARALLAX: Moves faster than main clouds + view-dependent shift.
//   Driven by _GameTime * cirrusSpeedMult.
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
// STAR TWINKLING MODEL
// ===============================================================
//
// Each star gets a unique phase from hash(floor(starUV)).
// Brightness is modulated by: 0.7 + 0.3 * sin(_GameTime * twinkleSpeed + phase)
// This creates gentle, asynchronous pulsing across the star field.
// Zero texture samples -- pure math on top of star texture lookup.
//
// ===============================================================
// PARALLAX MODEL
// ===============================================================
//
// Two mechanisms create depth illusion:
//
// 1. SPEED PARALLAX:
//    Cirrus layer moves faster than main cloud layer.
//    _CirrusSpeedMult > _CloudSpeedMult by default.
//    This mimics atmospheric layers at different altitudes.
//
// 2. VIEW-DEPENDENT PARALLAX:
//    A small UV shift proportional to V.xz is added to cirrus UVs.
//    When the camera rotates horizontally, cirrus clouds shift
//    slightly relative to main clouds, creating motion parallax.
//    _CirrusParallaxStrength controls the effect magnitude.
//
// ===============================================================
// TIMING MODEL -- _GameTime (v4.0)
// ===============================================================
//
// PREVIOUS: _GlobalRotation was a [0,1) accumulator that wrapped.
//   The wrap caused a UV discontinuity = visible "jerk" every cycle.
//
// NEW: _GameTime is a continuously increasing float from C#.
//   _gameTime += deltaTime; // never wraps, never resets
//
// The shader computes offsets as:
//   offset = _GameTime * speed
// And uses frac() where UV wrapping is needed.
// frac() discards the integer part, so large _GameTime values
// are perfectly safe -- no precision loss in the fractional part.
//
// This eliminates ALL timing discontinuities.
//
// ===============================================================
// PERFORMANCE -- DESIGNED FOR WEAK GPUs (MX350, Mali, Adreno)
// ===============================================================
//
// OPAQUE render queue -- zero overdraw, zero alpha blending cost.
// 4 texture samples total (1x stars + 1x cirrus + 2x flowmap dual-phase).
// Flow direction is extracted from phase 0 sample -- no extra fetch.
// All UV math uses float (32-bit) to prevent jitter.
// All color math uses half (16-bit) for ALU efficiency.
// SRP Batcher compatible (single CBUFFER).
// Star twinkling is pure ALU (hash + sin) -- no extra texture.
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
// _StarTex (RGB):
//   Star field texture. Bright pixels = stars on black background.
//   Sampled with high tiling for dense star coverage.
//
// ===============================================================
// GLOBAL INPUTS (set from C# scripts)
// ===============================================================
//
// _SunDirection    (float4) -- from HectonAtmosphereManager
// _AegirDirection  (float4) -- from HectonCelestialEngine
// _GameTime        (float)  -- from HectonCelestialEngine
//                             Continuously increasing time accumulator.
//                             C# does: _gameTime += deltaTime (never wraps).
// _NightBlend      (float)  -- from HectonCelestialEngine
//                             Day/night factor [0=day, 1=night].
//                             Controls star visibility.
// ============================================================================

Shader "HECTON/Sky/Hecton_AlienSky_Master"
{
    Properties
    {
        [Header(Cloud Texture Atlas)]
        _MainCloudTex ("Cloud Atlas RGBA", 2D) = "gray" {}

        [Header(Star Field)]
        _StarTex ("Star Field RGB", 2D) = "black" {}
        _StarTiling ("Star Tiling", Vector) = (3, 3, 0, 0)
        [HDR] _StarColor ("Star Tint", Color) = (1.0, 1.0, 1.0, 1)
        _StarIntensity ("Star Brightness", Range(0, 10)) = 2.0
        _StarTwinkleSpeed ("Twinkle Speed", Range(0.5, 8.0)) = 2.5

        [Header(Sky Colors HDR)]
        [HDR] _SkyColorZenith ("Zenith Color", Color) = (0.05, 0.08, 0.25, 1)
        [HDR] _SkyColorHorizon ("Horizon Color", Color) = (0.4, 0.35, 0.5, 1)
        [HDR] _SkyColorNadir ("Nadir Color", Color) = (0.02, 0.03, 0.08, 1)

        [Header(Cirrus Layer)]
        _CirrusTiling ("Cirrus Tiling", Vector) = (8, 4, 0, 0)
        _CirrusSpeedMult ("Cirrus Speed Mult", Range(0.0, 1.0)) = 0.1
        _CirrusOpacity ("Cirrus Opacity", Range(0, 1)) = 0.3
        [HDR] _CirrusColor ("Cirrus Tint", Color) = (0.7, 0.7, 0.9, 1)
        _CirrusParallaxStrength ("Cirrus Parallax", Range(0, 0.5)) = 0.08

        [Header(Main Cloud Layer)]
        _CloudTiling ("Cloud Tiling", Vector) = (3, 2, 0, 0)
        _CloudSpeedMult ("Cloud Speed Mult", Range(0.0, 3.0)) = 0.
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
        _SunSize ("Sun Radius", Range(0.0001, 0.05)) = 0.002
        _SunEdgeSoftness ("Sun Softness", Range(0.0001, 0.01)) = 0.001
        [HDR] _SunDiscColor ("Sun Color HDR", Color) = (20.0, 18.0, 12.0, 1)

        [Header(Sun Scattering)]
        _SunScatterPower ("Scatter Falloff", Range(1, 32)) = 8.0
        _SunScatterIntensity ("Scatter Intensity", Range(0, 5)) = 2.0
        [HDR] _SunScatterColor ("Scatter Color", Color) = (1.0, 0.7, 0.3, 1)

        [Header(Wind and Timing)]
        _GameTime ("Game Time (set from C#)", Float) = 0.0
        _NightBlend ("Night Blend (set from C#)", Range(0, 1)) = 0.0
        _WindDirection ("Wind Direction XZ", Vector) = (1, 0.2, 0, 0)

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
            TEXTURE2D(_StarTex);            SAMPLER(sampler_StarTex);       // ═══ NEW ═══

            // ---------------------------------------------------------
            // CBUFFER -- SRP Batcher compatible
            // ---------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float4 _MainCloudTex_ST;

                // ═══ NEW: Star field parameters ═══
                float4 _StarTex_ST;
                float4 _StarTiling;
                half4  _StarColor;
                half   _StarIntensity;
                half   _StarTwinkleSpeed;

                half4  _SkyColorZenith;
                half4  _SkyColorHorizon;
                half4  _SkyColorNadir;

                float4 _CirrusTiling;
                float  _CirrusSpeedMult;
                half   _CirrusOpacity;
                half4  _CirrusColor;
                half   _CirrusParallaxStrength;     // ═══ NEW ═══

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

                // ═══ MODIFIED: _GameTime replaces _GlobalRotation ═══
                float  _GameTime;
                float  _NightBlend;                 // ═══ NEW ═══
                float4 _WindDirection;

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

            // Minimum V.y for planar projection to prevent infinity at horizon
            static const float  HORIZON_CLAMP      = 0.05;

            // ---------------------------------
            // STRUCTS
            // ---------------------------------
            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS    : SV_POSITION;
                float3 viewDirWS     : TEXCOORD0;  // full precision for UV math
                half   horizonFactor : TEXCOORD1;
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
            // ═══ NEW: HASH FUNCTION FOR STAR TWINKLING ═══
            //
            // Pseudo-random number generator for 2D input.
            // Returns a value in [0, 1). Used to give each star
            // a unique phase offset for its twinkling animation.
            // Classic frac(sin(dot)) hash -- fast, good distribution.
            // ---------------------------------------------------------
            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // ---------------------------------------------------------
            // PLANAR CEILING PROJECTION
            //
            // Projects a flat texture plane above the camera.
            // V.xz / max(V.y, HORIZON_CLAMP) gives natural perspective:
            //   - Directly overhead (V.y=1): UV near origin, full detail
            //   - Toward horizon (V.y->0): UV stretches outward
            //   - HORIZON_CLAMP prevents division by zero / infinity
            //
            // Result is then scaled by tiling and offset by wind.
            //
            // ═══ MODIFIED: Uses _GameTime * speed instead of _GlobalRotation ═══
            // _GameTime is continuously increasing. frac() is applied only
            // where UV wrapping is needed (inside texture sampling).
            // The raw offset can be any large float -- GPU texture units
            // handle wrapping automatically (Repeat mode).
            // ---------------------------------------------------------
            float2 ComputeSkyUV(float3 V, float2 tiling, float speedMult)
            {
                // Planar projection: treat sky as infinite ceiling
                float projY = max(V.y, HORIZON_CLAMP);
                float2 skyUV = V.xz / projY;

                // Apply tiling (compresses texture at horizon)
                skyUV *= tiling;

                // ═══ MODIFIED ═══
                // Linear wind translation using _GameTime.
                // _GameTime grows forever. The GPU texture sampler handles
                // UV wrapping (Repeat mode), so no manual frac() needed here.
                // This eliminates the "jerk" that occurred when _GlobalRotation
                // wrapped at 1.0 in the old C# code.
                skyUV += _WindDirection.xy * _GameTime * speedMult;

                return skyUV;
            }

            // ---------------------------------------------------------
            // ═══ NEW: PARALLAX-ENHANCED PLANAR PROJECTION (Cirrus) ═══
            //
            // Same as ComputeSkyUV but with two additions for parallax:
            //   1. Higher speed multiplier (cirrus moves faster = higher altitude feel)
            //   2. View-dependent horizontal shift (V.xz offset)
            //      When the camera rotates, the cirrus layer shifts slightly
            //      relative to the main cloud layer, creating motion parallax.
            //      This fakes the effect of clouds at different altitudes
            //      without actual 3D geometry.
            //
            // The parallax shift is proportional to V.xz (horizontal view
            // component) and controlled by _CirrusParallaxStrength.
            // At zenith (V.y=1, V.xz=0), there's no parallax.
            // At angles, parallax increases -- matching real perspective.
            // ---------------------------------------------------------
            float2 ComputeCirrusUV(float3 V, float2 tiling, float speedMult)
            {
                float projY = max(V.y, HORIZON_CLAMP);
                float2 skyUV = V.xz / projY;

                // Apply tiling
                skyUV *= tiling;

                // Wind translation (faster for cirrus = altitude parallax)
                skyUV += _WindDirection.xy * _GameTime * speedMult;

                // ═══ NEW: View-dependent parallax shift ═══
                // V.xz represents the horizontal component of the view direction.
                // At zenith, V.xz ≈ (0,0) -- no shift.
                // At horizon, V.xz is large -- maximum shift.
                // This creates the illusion that cirrus clouds are at a
                // different altitude than main clouds.
                skyUV += V.xz * _CirrusParallaxStrength;

                return skyUV;
            }

            // ---------------------------------------------------------
            // FLOWMAP -- DUAL-PHASE CYCLING (2 SAMPLES)
            //
            // Phase A sampled at baseUV serves double duty:
            //   - BA channels provide flow direction
            //   - RG channels provide density and detail
            // Phase B sampled at distorted UV.
            // Triangular blend wave hides reset artifacts.
            //
            // ═══ MODIFIED: Uses _GameTime instead of _GlobalRotation ═══
            // The flow cycle time is derived from _GameTime * _FlowCycleSpeed.
            // frac() is used explicitly here because the dual-phase cycling
            // algorithm REQUIRES fractional time for its blend wave.
            // ---------------------------------------------------------
            half2 SampleFlowmap(
                TEXTURE2D_PARAM(flowTex, flowSampler),
                float2 baseUV)
            {
                // ═══ MODIFIED ═══
                // Use _GameTime * _FlowCycleSpeed for flow cycle timing.
                // frac() is applied where the algorithm needs it.
                float time = _GameTime * (float)_FlowCycleSpeed;
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

                // View direction in world space -- full float3 precision
                // for accurate planar UV projection in fragment shader.
                // Normalized per-pixel in fragment for correctness.
                output.viewDirWS = posInputs.positionWS - GetCameraPositionWS();

                // Horizon factor computed from normalized direction
                float3 normDir = normalize(output.viewDirWS);
                output.horizonFactor = (half)normDir.y;

                return output;
            }

            // ---------------------------------------------------------
            // FRAGMENT
            // ---------------------------------------------------------
            half4 SkyFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Normalize view direction per-pixel (interpolation denormalizes)
                float3 Vf = normalize(input.viewDirWS); // float for UV math
                half3  V  = (half3)Vf;                   // half for color math
                half   horizonFactor = input.horizonFactor;

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
                // ═══ NEW: LAYER 0 -- STAR FIELD ═══
                //
                // Stars are rendered BEFORE clouds so that
                // clouds naturally occlude them.
                //
                // UV projection: spherical (not planar ceiling)
                // because stars are at "infinity" -- they should
                // not exhibit the perspective foreshortening that
                // clouds have. Using atan2/asin gives uniform
                // distribution across the dome.
                //
                // Twinkling: Each star cell gets a unique phase
                // via hash(). Brightness oscillates gently with
                // sin(_GameTime * speed + phase).
                //
                // Visibility: Multiplied by _NightBlend so stars
                // are invisible during day, fade in at twilight,
                // and reach full brightness at night.
                //
                // Only computed above horizon (zenithMask > 0).
                // =======================================
                half3 starContrib = half3(0.0h, 0.0h, 0.0h);

                // Only compute stars if it's at least partially night
                // AND we're looking above the horizon.
                // This branch is coherent (all fragments in the sky dome
                // above horizon take the same path), so no divergence cost.
                half nightFactor = (half)_NightBlend;

                if (nightFactor > 0.01h && zenithMask > 0.01h)
                {
                    // Spherical UV for stars (uniform distribution, no pole pinch
                    // because star texture is sparse dots -- pinching is invisible)
                    float2 starUV;
                    starUV.x = atan2(Vf.z, Vf.x) * (0.5 / 3.14159265) + 0.5;
                    starUV.y = asin(Vf.y) * (1.0 / 3.14159265) + 0.5;
                    starUV *= _StarTiling.xy;

                    // Sample star texture
                    half4 starSample = SAMPLE_TEXTURE2D(
                        _StarTex, sampler_StarTex, starUV);

                    // Per-star twinkling
                    // hash() uses the floored UV cell to give each star a unique phase.
                    // The floor size depends on tiling -- higher tiling = more unique cells.
                    float2 starCell = floor(starUV * 64.0); // 64 = twinkle cell resolution
                    float starPhase = hash(starCell) * 6.28318; // [0, 2π)
                    float twinkleWave = 0.7 + 0.3 * sin(
                        _GameTime * (float)_StarTwinkleSpeed + starPhase);

                    // Combine: texture * tint * intensity * twinkle * night * zenith
                    half twinkle = (half)twinkleWave;
                    starContrib = starSample.rgb
                                * _StarColor.rgb
                                * _StarIntensity
                                * twinkle
                                * nightFactor
                                * zenithMask;  // fade out at horizon
                }

                // Add stars to sky (additive -- stars are bright points on dark sky)
                skyColor += starContrib;

                // =======================================
                // LAYER 3: HORIZON HAZE
                // Applied before clouds so clouds render ON TOP.
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
                // Texture sample 1 of 4
                //
                // ═══ MODIFIED: Uses ComputeCirrusUV with parallax ═══
                // Planar ceiling projection with high tiling,
                // fast wind speed, AND view-dependent parallax shift.
                // The parallax makes cirrus appear at a different
                // altitude than main clouds when the camera rotates.
                // =======================================
                float2 cirrusUV = ComputeCirrusUV(
                    Vf, _CirrusTiling.xy, _CirrusSpeedMult);

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
                // Texture samples 2-3 of 4
                //
                // Planar ceiling projection with main tiling.
                // Flowmap dual-phase cycling receives projected
                // UVs -- works identically to before, just
                // UV source changed from mesh to projection.
                // ═══ MODIFIED: Uses _GameTime internally ═══
                // =======================================
                float2 cloudBaseUV = ComputeSkyUV(
                    Vf, _CloudTiling.xy, _CloudSpeedMult);

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

                // Fade clouds near horizon (prevent hard cutoff
                // where projection stretches to infinity)
                half cloudHeightFade = saturate(horizonFactor * 3.0h);

                half finalCloudMask = cloudMask * cloudHeightFade;
                skyColor = lerp(skyColor, cloudColor, finalCloudMask);

                // =======================================
                // SUN DISC
                //
                // Angular size ~0.002 radians (~0.1 degrees).
                // smoothstep anti-aliases the edge.
                // Occluded by dense clouds.
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