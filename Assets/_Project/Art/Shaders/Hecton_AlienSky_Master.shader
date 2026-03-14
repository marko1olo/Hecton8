// ============================================================================
// HECTON-8 -- Hecton_AlienSky_Master.shader  v5.0
// Atmospheric sky dome shader for the exomoon Hecton.
// Unity 6 | URP 17+ | SRP Batcher Compatible
//
// ===============================================================
// TEXTURE SLOTS (v5.0 — Multi-Texture System)
// ===============================================================
//
// _StarTex (RGB):
//   Star field texture. Bright pixels = stars on black background.
//   Sampled with spherical UV + high tiling for uniform coverage.
//   Per-star twinkling via hash() + sin(_GameTime).
//   Visibility controlled by _NightBlend and _StarSkyExposure.
//
// _HighCloudTex (R channel used):
//   Thin, wispy high-altitude clouds (cirrus/ice crystals).
//   Single-channel density mask. Tinted by _HighCloudColor.
//   Planar ceiling projection with slow speed + view parallax.
//   Separate from main clouds for independent artistic control.
//
// _MainCloudAtlas (RGBA packed):
//   R = Cloud density mask (main cloud shapes)
//   G = Detail noise (high-frequency turbulence)
//   B = Flowmap X component (horizontal distortion direction)
//   A = Flowmap Y component (vertical distortion direction)
//   Planar ceiling projection + flowmap dual-phase cycling.
//   Moves faster than high clouds for depth parallax.
//
// ===============================================================
// LAYER ORDER (bottom to top in compositing)
// ===============================================================
//
// 0. Sky Gradient (Zenith/Horizon/Nadir colors, lerped by _NightBlend)
// 1. Stars (additive, visible only at night, twinkle, exposure-gated)
// 2. Horizon Haze (atmospheric scattering at horizon)
// 3. High Clouds (slow, thin, parallax-shifted)
// 4. Main Clouds (fast, dense, flowmap-morphing)
// 5. Sun Disc + Scattering (occluded by clouds)
// 6. Aegir Halo (gas giant atmospheric glow)
//
// Stars are rendered AFTER gradient but BEFORE clouds,
// so clouds naturally occlude the star field.
//
// ===============================================================
// NIGHT LOGIC (v5.0 — _NightBlend + _StarSkyExposure)
// ===============================================================
//
// _NightBlend (float, 0..1):
//   Set from C# (HectonCelestialEngine). 0 = full day, 1 = full night.
//   Controls:
//     - Sky gradient colors (lerp between day/night profiles in C#)
//     - Star visibility (stars multiply by _NightBlend)
//     - High cloud opacity reduction at night (optional, artistic)
//
// _StarSkyExposure (float, 0..5):
//   Additional star suppression based on sun proximity.
//   When the camera looks toward the sun, the sky around it is
//   too bright for stars to be visible (even at twilight).
//   Implementation: starBrightness *= saturate(1 - sunViewDot * _StarSkyExposure)
//   This creates a natural gradient: stars visible away from sun,
//   invisible near it, even during the night-side twilight.
//
// ===============================================================
// PARALLAX MODEL (v5.0 — Deep Parallax)
// ===============================================================
//
// Three mechanisms create depth illusion:
//
// 1. SPEED PARALLAX:
//    High clouds move slower than main clouds.
//    _HighCloudSpeedMult < _CloudSpeedMult by default.
//    Mimics atmospheric layers at different altitudes.
//
// 2. VIEW-DEPENDENT PARALLAX (High Clouds):
//    UV shift proportional to V.xz added to high cloud UVs.
//    Camera rotation causes high clouds to shift relative to
//    main clouds. _HighCloudParallaxStrength controls magnitude.
//
// 3. WIND DIRECTION DIVERGENCE:
//    High clouds can have a slightly different wind direction
//    via the parallax shift, creating natural shear between layers.
//
// ===============================================================
// TIMING MODEL -- _GameTime
// ===============================================================
//
// _GameTime is a continuously increasing float from C#.
//   _gameTime += deltaTime; // never wraps, never resets
//
// The shader computes offsets as:
//   offset = _GameTime * speed
// GPU texture units handle UV wrapping (Repeat mode).
// frac() used only where the algorithm requires it (flowmap cycling).
//
// ===============================================================
// PERFORMANCE -- DESIGNED FOR WEAK GPUs (MX350, Mali, Adreno)
// ===============================================================
//
// OPAQUE render queue -- zero overdraw, zero alpha blending cost.
// 4 texture samples total:
//   1x _StarTex (stars, skipped during day via branch)
//   1x _HighCloudTex (high clouds)
//   2x _MainCloudAtlas (flowmap dual-phase)
// All UV math: float (32-bit) for precision.
// All color math: half (16-bit) for ALU efficiency.
// Star twinkling: pure ALU (hash + sin) -- no extra texture.
// Coherent branching: star skip during day, no divergence.
// SRP Batcher compatible (single CBUFFER).
// Cull Front (inverted skydome), ZWrite Off.
// ============================================================================

Shader "HECTON/Sky/Hecton_AlienSky_Master"
{
    Properties
    {
        [Header(--- TEXTURES ---)]
        _StarTex ("Star Field (RGB)", 2D) = "black" {}
        _HighCloudTex ("High Clouds (R=Density)", 2D) = "black" {}
        _MainCloudAtlas ("Main Cloud Atlas (RGBA)", 2D) = "gray" {}

        [Header(--- STARS ---)]
        _StarTiling ("Star Tiling", Vector) = (3, 3, 0, 0)
        [HDR] _StarColor ("Star Tint", Color) = (1.0, 1.0, 1.0, 1)
        _StarIntensity ("Star Brightness", Range(0, 10)) = 2.0
        _StarTwinkleSpeed ("Twinkle Speed", Range(0.5, 8.0)) = 2.5
        _StarSkyExposure ("Star Sky Exposure (Sun Kill)", Range(0, 5)) = 1.5

        [Header(--- SKY COLORS HDR ---)]
        [HDR] _SkyColorZenith ("Zenith Color", Color) = (0.05, 0.08, 0.25, 1)
        [HDR] _SkyColorHorizon ("Horizon Color", Color) = (0.4, 0.35, 0.5, 1)
        [HDR] _SkyColorNadir ("Nadir Color", Color) = (0.02, 0.03, 0.08, 1)

        [Header(--- HIGH CLOUDS Layer 1 ---)]
        _HighCloudTiling ("High Cloud Tiling", Vector) = (6, 3, 0, 0)
        _HighCloudSpeedMult ("High Cloud Speed", Range(0.01, 2.0)) = 0.3
        _HighCloudOpacity ("High Cloud Opacity", Range(0, 1)) = 0.25
        [HDR] _HighCloudColor ("High Cloud Tint", Color) = (0.75, 0.75, 0.95, 1)
        _HighCloudParallaxStrength ("High Cloud Parallax", Range(0, 0.5)) = 0.1

        [Header(--- MAIN CLOUDS Layer 2 ---)]
        _CloudTiling ("Cloud Tiling", Vector) = (3, 2, 0, 0)
        _CloudSpeedMult ("Cloud Speed Mult", Range(0.5, 3.0)) = 1.0
        _FlowStrength ("Flow Distortion", Range(0, 0.5)) = 0.15
        _FlowCycleSpeed ("Flow Cycle Speed", Range(0.05, 1.0)) = 0.2
        _CloudDensityThreshold ("Density Threshold", Range(0, 1)) = 0.3
        _CloudSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.15
        [HDR] _CloudColorLit ("Cloud Lit Color", Color) = (0.9, 0.85, 0.8, 1)
        [HDR] _CloudColorShadow ("Cloud Shadow Color", Color) = (0.15, 0.12, 0.2, 1)
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.4

        [Header(--- HORIZON HAZE ---)]
        _HazeIntensity ("Haze Intensity", Range(0, 3)) = 1.5
        _HazeFalloff ("Haze Falloff", Range(0.5, 8)) = 3.0
        [HDR] _HazeColor ("Haze Color", Color) = (0.5, 0.45, 0.55, 1)
        _HazeSunTintStrength ("Haze Sun Tint", Range(0, 2)) = 0.8

        [Header(--- BACKLIT GLOW ---)]
        _BacklitPower ("Backlit Power", Range(1, 16)) = 4.0
        _BacklitIntensity ("Backlit Intensity", Range(0, 10)) = 3.0
        [HDR] _BacklitColor ("Backlit Color", Color) = (1.0, 0.8, 0.4, 1)

        [Header(--- AEGIR HALO ---)]
        _AegirHaloPower ("Aegir Falloff", Range(1, 16)) = 3.0
        _AegirHaloIntensity ("Aegir Intensity", Range(0, 5)) = 1.5
        [HDR] _AegirHaloColor ("Aegir Color", Color) = (0.6, 0.5, 0.8, 1)

        [Header(--- SUN DISC ---)]
        _SunSize ("Sun Radius", Range(0.0001, 0.05)) = 0.002
        _SunEdgeSoftness ("Sun Softness", Range(0.0001, 0.01)) = 0.001
        [HDR] _SunDiscColor ("Sun Color HDR", Color) = (20.0, 18.0, 12.0, 1)

        [Header(--- SUN SCATTERING ---)]
        _SunScatterPower ("Scatter Falloff", Range(1, 32)) = 8.0
        _SunScatterIntensity ("Scatter Intensity", Range(0, 5)) = 2.0
        [HDR] _SunScatterColor ("Scatter Color", Color) = (1.0, 0.7, 0.3, 1)

        [Header(--- WIND AND TIMING ---)]
        _GameTime ("Game Time (set from C#)", Float) = 0.0
        _NightBlend ("Night Blend (set from C#)", Range(0, 1)) = 0.0
        _WindDirection ("Wind Direction XZ", Vector) = (1, 0.2, 0, 0)

        [Header(--- DITHER ---)]
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

            // ---------------------------------------------------------
            // TEXTURES — Three dedicated slots (v5.0)
            // ---------------------------------------------------------
            TEXTURE2D(_StarTex);            SAMPLER(sampler_StarTex);
            TEXTURE2D(_HighCloudTex);       SAMPLER(sampler_HighCloudTex);
            TEXTURE2D(_MainCloudAtlas);     SAMPLER(sampler_MainCloudAtlas);

            // ---------------------------------------------------------
            // CBUFFER -- SRP Batcher compatible
            //
            // All properties declared here MUST match Properties block.
            // float for UV/time math, half for visual parameters.
            // ---------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                // Texture STs (required for TRANSFORM_TEX if used)
                float4 _StarTex_ST;
                float4 _HighCloudTex_ST;
                float4 _MainCloudAtlas_ST;

                // Stars
                float4 _StarTiling;
                half4  _StarColor;
                half   _StarIntensity;
                half   _StarTwinkleSpeed;
                half   _StarSkyExposure;

                // Sky gradient
                half4  _SkyColorZenith;
                half4  _SkyColorHorizon;
                half4  _SkyColorNadir;

                // High Clouds (Layer 1)
                float4 _HighCloudTiling;
                float  _HighCloudSpeedMult;
                half   _HighCloudOpacity;
                half4  _HighCloudColor;
                half   _HighCloudParallaxStrength;

                // Main Clouds (Layer 2)
                float4 _CloudTiling;
                float  _CloudSpeedMult;
                half   _FlowStrength;
                half   _FlowCycleSpeed;
                half   _CloudDensityThreshold;
                half   _CloudSoftness;
                half4  _CloudColorLit;
                half4  _CloudColorShadow;
                half   _DetailStrength;

                // Horizon Haze
                half   _HazeIntensity;
                half   _HazeFalloff;
                half4  _HazeColor;
                half   _HazeSunTintStrength;

                // Backlit Glow
                half   _BacklitPower;
                half   _BacklitIntensity;
                half4  _BacklitColor;

                // Aegir Halo
                half   _AegirHaloPower;
                half   _AegirHaloIntensity;
                half4  _AegirHaloColor;

                // Sun Disc
                half   _SunSize;
                half   _SunEdgeSoftness;
                half4  _SunDiscColor;

                // Sun Scattering
                half   _SunScatterPower;
                half   _SunScatterIntensity;
                half4  _SunScatterColor;

                // Timing & Wind
                float  _GameTime;
                float  _NightBlend;
                float4 _WindDirection;

                // Dither
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

            // Pi constants for spherical UV
            static const float  INV_PI             = 0.31830988618;
            static const float  INV_TWO_PI         = 0.15915494309;

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
            // HASH FUNCTION FOR STAR TWINKLING
            //
            // Pseudo-random number generator for 2D input.
            // Returns a value in [0, 1). Used to give each star
            // a unique phase offset for its twinkling animation.
            // Improved hash with better distribution than frac(sin(dot)).
            // ---------------------------------------------------------
            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // ---------------------------------------------------------
            // PLANAR CEILING PROJECTION (Main Clouds)
            //
            // Projects a flat texture plane above the camera.
            // V.xz / max(V.y, HORIZON_CLAMP) gives natural perspective.
            // Used for main cloud layer (Layer 2).
            // ---------------------------------------------------------
            float2 ComputeSkyUV(float3 V, float2 tiling, float speedMult)
            {
                float projY = max(V.y, HORIZON_CLAMP);
                float2 skyUV = V.xz / projY;

                // Apply tiling
                skyUV *= tiling;

                // Linear wind translation using _GameTime
                skyUV += _WindDirection.xy * _GameTime * speedMult;

                return skyUV;
            }

            // ---------------------------------------------------------
            // PARALLAX-ENHANCED PLANAR PROJECTION (High Clouds)
            //
            // Same as ComputeSkyUV but with view-dependent parallax:
            //   1. Slower speed (high altitude = slower apparent motion)
            //   2. V.xz offset creates motion parallax on camera rotation
            //
            // The parallax shift is proportional to V.xz (horizontal
            // view component). At zenith (V.y=1, V.xz≈0), no parallax.
            // At angles, parallax increases -- matching real perspective.
            // This fakes depth between cloud layers without 3D geometry.
            // ---------------------------------------------------------
            float2 ComputeHighCloudUV(float3 V, float2 tiling, float speedMult)
            {
                float projY = max(V.y, HORIZON_CLAMP);
                float2 skyUV = V.xz / projY;

                // Apply tiling
                skyUV *= tiling;

                // Wind translation (slower for high clouds)
                skyUV += _WindDirection.xy * _GameTime * speedMult;

                // View-dependent parallax shift
                // V.xz represents horizontal view direction.
                // Shifting high cloud UVs by this creates apparent
                // lateral motion when the camera rotates, making
                // high clouds appear at a different altitude.
                skyUV += V.xz * (float)_HighCloudParallaxStrength;

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
            // Uses _GameTime * _FlowCycleSpeed for timing.
            // frac() applied where the algorithm requires it.
            // ---------------------------------------------------------
            half2 SampleFlowmap(
                TEXTURE2D_PARAM(flowTex, flowSampler),
                float2 baseUV)
            {
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
                half3 L = -sunDir;  // direction TO the sun (negated from-sun convention)

                half3 aegirDir = (half3)SafeNormalizeDir(
                    _AegirDirection.xyz, FALLBACK_AEGIR_DIR);

                // =======================================
                // COMMON DOT PRODUCTS (computed once, reused)
                // =======================================
                half sunViewDot  = saturate(dot(V, L));
                half zenithMask  = saturate(horizonFactor);
                half nadirMask   = saturate(-horizonFactor);
                half horizonMask = 1.0h - zenithMask - nadirMask;

                // Night blend factor from C# (0 = day, 1 = night)
                half nightFactor = (half)_NightBlend;

                // =======================================
                // LAYER 0: BASE SKY GRADIENT
                //
                // Colors are pre-lerped between day/night profiles
                // in C# (HectonCelestialEngine.UpdateSkyMaterial).
                // The gradient here just blends zenith/horizon/nadir.
                // =======================================
                half3 skyColor = _SkyColorZenith.rgb  * zenithMask
                               + _SkyColorHorizon.rgb * horizonMask
                               + _SkyColorNadir.rgb   * nadirMask;

                // =======================================
                // LAYER 1: STAR FIELD
                //
                // Rendered AFTER gradient, BEFORE clouds.
                // Clouds will naturally occlude stars.
                //
                // VISIBILITY LOGIC:
                //   1. nightFactor: 0 at day (stars invisible),
                //      1 at night (stars fully visible).
                //   2. _StarSkyExposure: suppresses stars near the sun.
                //      Even at night, looking toward the sun's position
                //      (low on horizon at sunset) washes out nearby stars.
                //   3. zenithMask: fade stars at/below horizon.
                //
                // TWINKLING:
                //   hash(floor(starUV * cellSize)) gives each star cell
                //   a unique phase. sin(_GameTime * speed + phase)
                //   oscillates brightness gently.
                //
                // OPTIMIZATION:
                //   Entire block skipped when nightFactor < 0.01.
                //   This is a coherent branch (all sky fragments above
                //   horizon take the same path) -- no GPU divergence.
                // =======================================
                half3 starContrib = half3(0.0h, 0.0h, 0.0h);

                if (nightFactor > 0.01h && zenithMask > 0.01h)
                {
                    // Spherical UV for stars (uniform distribution)
                    // Stars are at "infinity" -- no planar perspective needed.
                    // atan2/asin give uniform angular coverage.
                    float2 starUV;
                    starUV.x = atan2(Vf.z, Vf.x) * INV_TWO_PI + 0.5;
                    starUV.y = asin(Vf.y) * INV_PI + 0.5;
                    starUV *= _StarTiling.xy;

                    // Sample star texture
                    half4 starSample = SAMPLE_TEXTURE2D(
                        _StarTex, sampler_StarTex, starUV);

                    // Per-star twinkling
                    // Cell resolution of 64 gives ~4000 unique twinkle phases
                    // across the visible sky. Enough for natural variation.
                    float2 starCell = floor(starUV * 64.0);
                    float starPhase = hash(starCell) * 6.28318; // [0, 2π)
                    float twinkleWave = 0.7 + 0.3 * sin(
                        _GameTime * (float)_StarTwinkleSpeed + starPhase);

                    // Sun exposure kill: suppress stars near the sun
                    // sunViewDot is high when looking toward the sun.
                    // _StarSkyExposure controls how aggressively stars
                    // are killed by sun proximity.
                    // At _StarSkyExposure = 1.5: stars disappear within
                    // ~40 degrees of the sun even at night.
                    half sunKill = saturate(1.0h - sunViewDot * _StarSkyExposure);

                    // Final star brightness
                    half twinkle = (half)twinkleWave;
                    starContrib = starSample.rgb
                                * _StarColor.rgb
                                * _StarIntensity
                                * twinkle
                                * nightFactor   // day/night gate
                                * sunKill       // sun proximity gate
                                * zenithMask;   // horizon fade
                }

                // Additive: stars are bright points on dark sky
                skyColor += starContrib;

                // =======================================
                // LAYER 2: HORIZON HAZE
                //
                // Applied before clouds so clouds render ON TOP.
                // Atmospheric scattering simulation at the horizon.
                // Sun-tinted for warm sunset/sunrise effect.
                // =======================================
                half hazeRaw = 1.0h - abs(horizonFactor);
                half hazeMask = pow(hazeRaw, _HazeFalloff) * _HazeIntensity;

                half3 hazeSunTint = lerp(
                    HALF_ONE,
                    _SunScatterColor.rgb,
                    sunViewDot * _HazeSunTintStrength);

                half3 hazeColor = _HazeColor.rgb * hazeSunTint * hazeMask;
                skyColor += hazeColor;

                // =======================================
                // LAYER 3: HIGH CLOUDS (Layer 1 visual)
                //
                // Thin, wispy, slow-moving upper atmosphere clouds.
                // Separate texture (_HighCloudTex) for independent
                // artistic control over density and structure.
                //
                // PARALLAX: Uses ComputeHighCloudUV which adds
                // view-dependent V.xz shift. This makes high clouds
                // appear to float at a different altitude than main
                // clouds when the camera rotates horizontally.
                //
                // SPEED: _HighCloudSpeedMult is intentionally LOW
                // (default 0.3). High clouds move slowly, main clouds
                // move faster. The speed difference creates temporal
                // parallax (layers drift apart over time).
                //
                // BACKLIT: Simplified backlit glow (half power).
                // High clouds are thin, so backlit effect is subtle.
                //
                // Texture sample 1 of 4.
                // =======================================
                float2 highCloudUV = ComputeHighCloudUV(
                    Vf, _HighCloudTiling.xy, _HighCloudSpeedMult);

                half highCloudDensity = SAMPLE_TEXTURE2D(
                    _HighCloudTex, sampler_HighCloudTex, highCloudUV).r;

                // Backlit glow for high clouds (subtle)
                half highCloudBacklit = pow(
                    saturate(1.0h - dot(V, -L)),
                    _BacklitPower * 0.5h) * highCloudDensity;

                half3 highCloudTint = _HighCloudColor.rgb
                                    + highCloudBacklit * _BacklitColor.rgb * 0.2h;

                // Composite high clouds onto sky
                // Fade by zenithMask (no clouds at/below horizon)
                // and by _HighCloudOpacity for artist control.
                half highCloudAlpha = highCloudDensity
                                    * _HighCloudOpacity
                                    * zenithMask;

                skyColor = lerp(
                    skyColor,
                    skyColor + highCloudTint,
                    highCloudAlpha);

                // =======================================
                // LAYER 4: MAIN CLOUDS (Layer 2 visual)
                //
                // Dense, morphing cloud formations.
                // _MainCloudAtlas RGBA:
                //   R = density, G = detail, BA = flowmap XY.
                //
                // FLOWMAP DUAL-PHASE:
                //   Two samples with triangular blend wave
                //   eliminate UV reset artifacts.
                //   Flow direction from BA channels of Phase A.
                //
                // SPEED: _CloudSpeedMult is higher than
                // _HighCloudSpeedMult. Main clouds move faster,
                // creating speed parallax between layers.
                //
                // BACKLIT: Full-power backlit glow with HDR color.
                // Dense clouds catch sunlight dramatically.
                //
                // Texture samples 2-3 of 4.
                // =======================================
                float2 cloudBaseUV = ComputeSkyUV(
                    Vf, _CloudTiling.xy, _CloudSpeedMult);

                half2 cloudRG = SampleFlowmap(
                    TEXTURE2D_ARGS(_MainCloudAtlas, sampler_MainCloudAtlas),
                    cloudBaseUV);

                half cloudDensity = cloudRG.x;
                half cloudDetail  = cloudRG.y;

                // Subtract detail noise from density for erosion effect
                cloudDensity -= cloudDetail * _DetailStrength;

                // Smooth density threshold with soft edges
                half smoothLow  = _CloudDensityThreshold;
                half smoothHigh = _CloudDensityThreshold + _CloudSoftness;
                half cloudMask  = smoothstep(smoothLow, smoothHigh, cloudDensity);

                // Cloud lighting: shadow/lit based on sun alignment
                half cloudNdotL = saturate(dot(V, L));

                half3 cloudBaseColor = lerp(
                    _CloudColorShadow.rgb,
                    _CloudColorLit.rgb,
                    cloudNdotL);

                // Backlit glow: bright edges when sun is behind clouds
                half backlitFactor = pow(
                    saturate(sunViewDot),
                    _BacklitPower);
                half3 backlitGlow = _BacklitColor.rgb
                                  * backlitFactor
                                  * cloudMask
                                  * _BacklitIntensity;

                half3 cloudColor = cloudBaseColor + backlitGlow;

                // Fade clouds near horizon (prevent hard cutoff
                // where planar projection stretches to infinity)
                half cloudHeightFade = saturate(horizonFactor * 3.0h);

                half finalCloudMask = cloudMask * cloudHeightFade;
                skyColor = lerp(skyColor, cloudColor, finalCloudMask);

                // =======================================
                // SUN DISC
                //
                // Angular size ~0.002 radians (~0.1 degrees).
                // smoothstep anti-aliases the edge.
                // Occluded by dense clouds (main layer).
                // =======================================
                half sunDist = 1.0h - sunViewDot;
                half sunDisc = 1.0h - smoothstep(
                    _SunSize - _SunEdgeSoftness,
                    _SunSize + _SunEdgeSoftness,
                    sunDist);

                // Clouds occlude the sun disc
                sunDisc *= (1.0h - finalCloudMask);
                skyColor += _SunDiscColor.rgb * sunDisc;

                // =======================================
                // SUN SCATTERING
                //
                // Rayleigh-approximation glow around the sun.
                // Warm tones near sun, fading outward.
                // Partially occluded by clouds.
                // =======================================
                half sunScatter = pow(
                    saturate(sunViewDot),
                    _SunScatterPower);
                half3 sunGlow = _SunScatterColor.rgb
                              * sunScatter
                              * _SunScatterIntensity;

                // Clouds partially block scattering (70% occlusion)
                sunGlow *= (1.0h - finalCloudMask * 0.7h);
                skyColor += sunGlow;

                // =======================================
                // AEGIR HALO
                //
                // Diffuse glow from the gas giant Aegir.
                // Visible even at night (Aegir reflects sunlight).
                // Partially occluded by clouds (50% occlusion).
                // =======================================
                half aegirDot = saturate(dot(V, aegirDir));
                half aegirHalo = pow(aegirDot, _AegirHaloPower)
                               * _AegirHaloIntensity;

                // Clouds partially block Aegir halo
                aegirHalo *= (1.0h - finalCloudMask * 0.5h);
                skyColor += _AegirHaloColor.rgb * aegirHalo;

                return half4(skyColor, 1.0h);
            }

            ENDHLSL
        }
    }

    FallBack Off
}