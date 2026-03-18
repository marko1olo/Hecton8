// ============================================================================
// HECTON-8 -- Hecton_AlienSky_Master.shader
// Atmospheric sky dome shader for the exomoon Hecton.
// Unity 6 | URP 17+ | SRP Batcher Compatible
//
// v4.1 — DYNAMIC SUNSET / NIGHT LIGHTING
//
//   [ADD] Sunset gradient: sunsetFactor × sunsetSpot masks fire when
//         sun is near horizon, injecting _SunsetHorizonColor into
//         horizon band in sun direction. Belt of Venus on opposite side.
//   [ADD] Contextual cloud lighting: _CloudColorLit lerped with
//         _SunsetCloudColor (sunset) and _NightCloudColor (night).
//   [ADD] Aegir cloud illumination: purple-violet glow on clouds
//         at night, proportional to Aegir alignment × _AegirGlowIntensity.
//   [ADD] Star elevation fade: stars vanish when _SunElevation > -0.1
//         (pre-dawn), independent of _NightBlend.
//   [ADD] Star NASA-Punk flicker: per-star sin() oscillation using
//         _GameTime + hash-derived phase offset.
//   [ADD] Aegir halo night boost: halo intensifies at night.
//   [ADD] Properties: _SunsetHorizonColor, _SunsetCloudColor,
//         _NightCloudColor, _AegirGlowIntensity, _SunElevation.
//   [PERF] Zero new texture samples. ~45 extra ALU instructions.
//
// PRESERVED FROM v4.0:
//   ✓ Planar ceiling UV projection (zero pole pinch)
//   ✓ Flowmap dual-phase cycling (2 samples, zero jerk)
//   ✓ _GameTime accumulator (replaces _GlobalRotation)
//   ✓ _NightBlend star visibility
//   ✓ Cirrus parallax
//   ✓ Star twinkling via hash
//   ✓ Backlit glow, Aegir halo, sun disc, sun scattering
//   ✓ Horizon haze with sun tint
//   ✓ Dithered alpha clip
//   ✓ SRP Batcher compatible (single CBUFFER)
//   ✓ 4 texture samples total
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

        [Header(Sunset and Night Colors)]
        [HDR] _SunsetHorizonColor ("Sunset Horizon Color", Color) = (3.5, 0.6, 0.05, 1)
        [HDR] _SunsetCloudColor ("Sunset Cloud Color", Color) = (2.0, 0.7, 0.2, 1)
        _NightCloudColor ("Night Cloud Color", Color) = (0.04, 0.03, 0.08, 1)
        _AegirGlowIntensity ("Aegir Night Glow", Range(0, 5)) = 1.5

        [Header(Cirrus Layer)]
        _CirrusTiling ("Cirrus Tiling", Vector) = (8, 4, 0, 0)
        _CirrusSpeedMult ("Cirrus Speed Mult", Range(0.0, 1.0)) = 0.1
        _CirrusOpacity ("Cirrus Opacity", Range(0, 1)) = 0.3
        [HDR] _CirrusColor ("Cirrus Tint", Color) = (0.7, 0.7, 0.9, 1)
        _CirrusParallaxStrength ("Cirrus Parallax", Range(0, 0.5)) = 0.08

        [Header(Main Cloud Layer)]
        _CloudTiling ("Cloud Tiling", Vector) = (3, 2, 0, 0)
        _CloudSpeedMult ("Cloud Speed Mult", Range(0.0, 3.0)) = 0.3
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
        _SunElevation ("Sun Elevation (set from C#)", Range(-1, 1)) = 0.5
        _EclipseOcclusion ("Eclipse Occlusion (set from C#)", Range(0, 1)) = 0.0
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
            TEXTURE2D(_StarTex);            SAMPLER(sampler_StarTex);

            // ---------------------------------------------------------
            // CBUFFER -- SRP Batcher compatible
            // ---------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float4 _MainCloudTex_ST;

                float4 _StarTex_ST;
                float4 _StarTiling;
                half4  _StarColor;
                half   _StarIntensity;
                half   _StarTwinkleSpeed;

                half4  _SkyColorZenith;
                half4  _SkyColorHorizon;
                half4  _SkyColorNadir;

                // v4.1: Sunset and night
                half4  _SunsetHorizonColor;
                half4  _SunsetCloudColor;
                half4  _NightCloudColor;
                half   _AegirGlowIntensity;

                float4 _CirrusTiling;
                float  _CirrusSpeedMult;
                half   _CirrusOpacity;
                half4  _CirrusColor;
                half   _CirrusParallaxStrength;

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

                float  _GameTime;
                float  _NightBlend;
                float  _SunElevation;   // v4.1: sun altitude -1..+1
                float  _EclipseOcclusion; // v5.1: 0=no eclipse, 1=full eclipse
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
                float3 viewDirWS     : TEXCOORD0;
                half   horizonFactor : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ---------------------------------------------------------
            // UTILITY
            // ---------------------------------------------------------
            float3 SafeNormalizeDir(float3 v, float3 fallback)
            {
                float lenSq = dot(v, v);
                return (lenSq < DIR_THRESHOLD * DIR_THRESHOLD)
                    ? fallback
                    : v * rsqrt(lenSq);
            }

            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // ---------------------------------------------------------
            // PLANAR CEILING PROJECTION
            // ---------------------------------------------------------
            float2 ComputeSkyUV(float3 V, float2 tiling, float speedMult)
            {
                float projY = max(V.y, HORIZON_CLAMP);
                float2 skyUV = V.xz / projY;
                skyUV *= tiling;
                skyUV += _WindDirection.xy * _GameTime * speedMult;
                return skyUV;
            }

            float2 ComputeCirrusUV(float3 V, float2 tiling, float speedMult)
            {
                float projY = max(V.y, HORIZON_CLAMP);
                float2 skyUV = V.xz / projY;
                skyUV *= tiling;
                skyUV += _WindDirection.xy * _GameTime * speedMult;
                skyUV += V.xz * _CirrusParallaxStrength;
                return skyUV;
            }

            // ---------------------------------------------------------
            // FLOWMAP -- DUAL-PHASE CYCLING
            // ---------------------------------------------------------
            half2 SampleFlowmap(
                TEXTURE2D_PARAM(flowTex, flowSampler),
                float2 baseUV)
            {
                float time = _GameTime * (float)_FlowCycleSpeed;
                float phase1 = frac(time + 0.5);

                half4 sample0 = SAMPLE_TEXTURE2D(flowTex, flowSampler, baseUV);
                half2 flowDir = sample0.ba * 2.0h - 1.0h;

                float2 uv1 = baseUV + (float2)(flowDir * _FlowStrength) * phase1;
                half4 sample1 = SAMPLE_TEXTURE2D(flowTex, flowSampler, uv1);

                half blend = abs(frac((half)time) * 2.0h - 1.0h);

                half2 result;
                result.x = lerp(sample0.r, sample1.r, blend);
                result.y = lerp(sample0.g, sample1.g, blend);
                return result;
            }

            // ---------------------------------------------------------
            // DITHER
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
                output.viewDirWS = posInputs.positionWS - GetCameraPositionWS();

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

                float3 Vf = normalize(input.viewDirWS);
                half3  V  = (half3)Vf;
                half   horizonFactor = input.horizonFactor;

                // =======================================
                // RESOLVE GLOBAL DIRECTIONS
                // =======================================
                half3 sunDir = (half3)SafeNormalizeDir(
                    _SunDirection.xyz, FALLBACK_SUN_DIR);
                half3 L = -sunDir;

                half3 aegirDir = (half3)SafeNormalizeDir(
                    _AegirDirection.xyz, FALLBACK_AEGIR_DIR);

                half sunViewDot = saturate(dot(V, L));

                // =======================================
                // v4.1: SUNSET / GOLDEN HOUR MASKS
                //
                // sunsetFactor: narrow band at horizon.
                //   _SunElevation = 0   → 1.0 (peak)
                //   _SunElevation = ±0.125 → ≈ 0
                //
                // sunsetSpot: directional — in sun direction.
                //   pow(4) creates tight warm spot.
                //
                // beltOfVenus: opposite to sun.
                //   Subtle pink/purple counter-glow.
                // =======================================
                half sunElevation   = (half)_SunElevation;
                // v5.1: Eclipse visibility — 0 when fully eclipsed
                half eclipseVis = 1.0h - (half)_EclipseOcclusion;
                half sunsetFactor   = saturate(1.0h - abs(sunElevation) * 8.0h);
                // v5.1: No sunset glow during eclipse
                sunsetFactor *= eclipseVis;
                half sunsetSpot     = pow(sunViewDot, 4.0h) * sunsetFactor;
                half beltOfVenus    = pow(saturate(dot(V, sunDir)), 3.0h)
                                    * sunsetFactor * 0.4h;

                // =======================================
                // BASE SKY GRADIENT
                // v4.1: Sunset horizon injection
                // =======================================
                half zenithMask  = saturate(horizonFactor);
                half nadirMask   = saturate(-horizonFactor);
                half horizonMask = 1.0h - zenithMask - nadirMask;

                half3 skyColor = _SkyColorZenith.rgb  * zenithMask
                               + _SkyColorHorizon.rgb * horizonMask
                               + _SkyColorNadir.rgb   * nadirMask;

                // v4.1: Warm sunset glow at horizon in sun direction
                half3 sunsetHorizonContrib = _SunsetHorizonColor.rgb
                                           * sunsetSpot
                                           * horizonMask;
                skyColor += sunsetHorizonContrib;

                // v4.1: Belt of Venus (anti-solar pink band)
                half3 beltColor = half3(0.6h, 0.3h, 0.5h) * beltOfVenus * horizonMask;
                skyColor += beltColor;

                // =======================================
                // LAYER 0: STAR FIELD
                // v4.1: Elevation fade + NASA-Punk flicker
                // =======================================
                half3 starContrib = half3(0.0h, 0.0h, 0.0h);
                half nightFactor = (half)_NightBlend;

                if (nightFactor > 0.01h && zenithMask > 0.01h)
                {
                    float2 starUV;
                    starUV.x = atan2(Vf.z, Vf.x) * (0.5 / 3.14159265) + 0.5;
                    starUV.y = asin(Vf.y) * (1.0 / 3.14159265) + 0.5;
                    starUV *= _StarTiling.xy;

                    half4 starSample = SAMPLE_TEXTURE2D(
                        _StarTex, sampler_StarTex, starUV);

                    float2 starCell = floor(starUV * 64.0);
                    float starPhase = hash(starCell) * 6.28318;

                    // v4.1: Elevation-based star fade
                    // Stars vanish when sun rises above -0.1 elevation
                    half starDayFade = saturate(-sunElevation * 10.0h);
                    // v5.1: Stars visible during eclipse even in "daytime"
                    half starVisibility = nightFactor * max(starDayFade, (half)_EclipseOcclusion);

                    // v4.1: NASA-Punk flicker using _GameTime
                    half flicker = 0.8h + 0.2h * (half)sin(
                        _GameTime * (float)_StarTwinkleSpeed + starPhase);

                    starContrib = starSample.rgb
                                * _StarColor.rgb
                                * _StarIntensity
                                * flicker
                                * starVisibility
                                * zenithMask;
                }

                skyColor += starContrib;

                // =======================================
                // LAYER 3: HORIZON HAZE
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
                // LAYER 1: CIRRUS CLOUDS
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
                // v4.1: Contextual lighting + Aegir glow
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

                // v4.1: Contextual cloud lighting
                // Day → sunset → night color chain
                half3 cloudLitDay     = _CloudColorLit.rgb;
                half3 cloudLitSunset  = lerp(cloudLitDay,
                                             _SunsetCloudColor.rgb,
                                             sunsetSpot);
                half3 cloudLitFinal   = lerp(cloudLitSunset,
                                             _NightCloudColor.rgb,
                                             (half)_NightBlend);

                // v4.1: Aegir night illumination of clouds
                half aegirDotForClouds = saturate(dot(V, aegirDir));
                half3 aegirCloudGlow   = half3(0.4h, 0.2h, 0.8h)
                                       * aegirDotForClouds
                                       * (half)_NightBlend
                                       * _AegirGlowIntensity
                                       * cloudMask;

                half3 cloudBaseColor = lerp(
                    _CloudColorShadow.rgb,
                    cloudLitFinal,
                    cloudNdotL);

                // Aegir glow is additive (it's a light source)
                cloudBaseColor += aegirCloudGlow;

                // v4.1: Backlit intensifies at sunset (sun is low = more rim light)
                half backlitSunsetBoost = 1.0h + sunsetFactor * 1.5h;
                half backlitFactor = pow(
                    saturate(sunViewDot),
                    _BacklitPower);
                half3 backlitGlow = _BacklitColor.rgb
                                  * backlitFactor
                                  * cloudMask
                                  * _BacklitIntensity
                                  * backlitSunsetBoost;

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

                sunDisc *= (1.0h - finalCloudMask) * eclipseVis;
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

                sunGlow *= (1.0h - finalCloudMask * 0.7h) * eclipseVis;
                skyColor += sunGlow;

                // =======================================
                // AEGIR HALO
                // v4.1: Night boost via _AegirGlowIntensity
                // =======================================
                half aegirDot = saturate(dot(V, aegirDir));

                // v4.1: At night, Aegir halo intensifies
                half nightBoost = 1.0h + (half)_NightBlend * _AegirGlowIntensity;

                half aegirHalo = pow(aegirDot, _AegirHaloPower)
                               * _AegirHaloIntensity
                               * nightBoost;

                aegirHalo *= (1.0h - finalCloudMask * 0.5h);
                skyColor += _AegirHaloColor.rgb * aegirHalo;

                return half4(skyColor, 1.0h);
            }

            ENDHLSL
        }
    }

    FallBack Off
}