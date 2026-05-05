// ============================================================================
// HECTON-8 -- Hecton_AlienSky_Master.shader
// Atmospheric sky dome shader for the exomoon Hecton.
// Unity 6 | URP 17+ | SRP Batcher Compatible
//
// v5.3 — ATMOSPHERIC PERSPECTIVE HORIZON FIX
//
//   [FIX] Replaced v5.2's hard cloud cutoff with atmospheric perspective.
//         v5.2 used smoothstep to REMOVE clouds near horizon → visible gap.
//         v5.3 keeps clouds visible but blends them into sky/haze color.
//         At horizon, clouds become a soft uniform layer matching the sky.
//         This hides UV stretching artifacts naturally without gaps.
//
//   [FIX] HORIZON_CLAMP set to 0.08 (v5.2 had 0.12, too aggressive).
//
//   [FIX] atmosClarity factor controls per-layer atmosphere:
//         - Cloud detail fades (no mipmap aliasing source)
//         - Cloud threshold lowers (continuous soft coverage)
//         - Cloud softness widens (no sharp mask edges)
//         - Cloud color → sky color (atmospheric perspective)
//         - Backlit glow fades (no bright streaks)
//         - Cirrus fades smoothly
//
//   [PERF] One smoothstep + two lerps added. Zero texture samples added.
//
// v5.1 PRESERVED:
//   ✓ Eclipse sky darkening via eclipseVis
//   ✓ All sunset/golden hour logic
//   ✓ Belt of Venus
//   ✓ Star NASA-Punk flicker + elevation fade
//   ✓ Aegir cloud illumination at night
//   ✓ Planar ceiling UV, flowmap, dither
//   ✓ SRP Batcher compatible
//   ✓ 5 texture samples total
// ============================================================================

Shader "HECTON/Sky/Hecton_AlienSky_Master"
{
    Properties
    {
        [Header(Cloud Texture Atlas)]
        _MainCloudTex ("Cloud Atlas RGBA", 2D) = "gray" {}

        [Header(Star Field)]
        _StarTiling ("Star Tiling", Vector) = (3, 3, 0, 0)
        [HDR] _StarColor ("Star Tint", Color) = (1.0, 1.0, 1.0, 1)
        _StarIntensity ("Star Brightness", Range(0, 10)) = 2.0
        _StarTwinkleSpeed ("Twinkle Speed", Range(0.5, 8.0)) = 2.5
        _StarSeed ("Star Seed", Float) = 99173
        _AtmosphereDensity ("Atmosphere Density", Range(0, 1)) = 0.0

        [Header(Sky Colors HDR)]
        [HDR] _SkyColorZenith ("Zenith Color", Color) = (0.05, 0.08, 0.25, 1)
        [HDR] _SkyColorHorizon ("Horizon Color", Color) = (0.4, 0.35, 0.5, 1)
        [HDR] _SkyColorNadir ("Nadir Color", Color) = (0.02, 0.03, 0.08, 1)
        _SkyLuminanceMultiplier ("Sky Luminance Multiplier", Range(0, 4)) = 1.0

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
        _HorizonMistShelfIntensity ("Horizon Mist Shelf Intensity", Range(0, 2)) = 0.0
        _HorizonMistShelfHeight ("Horizon Mist Shelf Height", Range(0.04, 0.32)) = 0.16
        _HorizonMistShelfSoftness ("Horizon Mist Shelf Softness", Range(0.02, 0.24)) = 0.1

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

        [Header(Celestial Transmittance)]
        _CelestialTransmittanceTiling ("Transmittance Tiling", Vector) = (0.04, 0.06, 0, 0)
        _CelestialTransmittanceScrollSpeed ("Transmittance Scroll Speed", Range(0.0, 0.01)) = 0.001
        _CelestialTransmittanceThreshold ("Transmittance Threshold", Range(0, 1)) = 0.52
        _CelestialTransmittanceSoftness ("Transmittance Softness", Range(0.01, 0.5)) = 0.24
        _CelestialTransmittanceStrength ("Transmittance Strength", Range(0, 1)) = 0.4
        _CelestialStarFade ("Star Fade", Range(0, 1)) = 0.85
        _CelestialSunFade ("Sun Fade", Range(0, 1)) = 0.65
        _CelestialHaloFade ("Halo Fade", Range(0, 1)) = 0.55

        [Header(Shared Celestial Atmosphere)]
        _AtmosphereTransmittanceWeight ("Atmosphere Transmittance Weight", Range(0, 1.5)) = 1.0
        _AtmosphereInscatterWeight ("Atmosphere Inscatter Weight", Range(0, 2.0)) = 1.0

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
            #include "Hecton_CelestialAtmosphere.hlsl"

            TEXTURE2D(_MainCloudTex);       SAMPLER(sampler_MainCloudTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainCloudTex_ST;

                float4 _StarTiling;
                half4  _StarColor;
                half   _StarIntensity;
                half   _StarTwinkleSpeed;
                float  _StarSeed;
                half   _AtmosphereDensity;

                half4  _SkyColorZenith;
                half4  _SkyColorHorizon;
                half4  _SkyColorNadir;
                half   _SkyLuminanceMultiplier;

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
                half   _HorizonMistShelfIntensity;
                half   _HorizonMistShelfHeight;
                half   _HorizonMistShelfSoftness;

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

                float4 _CelestialTransmittanceTiling;
                float  _CelestialTransmittanceScrollSpeed;
                half   _CelestialTransmittanceThreshold;
                half   _CelestialTransmittanceSoftness;
                half   _CelestialTransmittanceStrength;
                half   _CelestialStarFade;
                half   _CelestialSunFade;
                half   _CelestialHaloFade;
                half   _AtmosphereTransmittanceWeight;
                half   _AtmosphereInscatterWeight;

                float  _GameTime;
                float  _NightBlend;
                float  _SunElevation;
                float  _EclipseOcclusion;
                float4 _WindDirection;

                half   _DitherScale;
            CBUFFER_END

            float4 _SunDirection;
            float4 _AegirDirection;
            float4 _MeteorShowerParams;     // x=intensity, y=seed, z=synced flash, w=event age
            float4 _MeteorShowerDirection;  // xy=sky UV travel direction, z=streak length, w=streak width

            static const half  HALF_ZERO = 0.0h;
            static const half  HALF_ONE  = 1.0h;

            static const float3 FALLBACK_SUN_DIR   = float3(0.57735, 0.57735, 0.57735);
            static const float3 FALLBACK_AEGIR_DIR = float3(0.0, 0.93633, -0.35112);
            static const float  DIR_THRESHOLD      = 0.001;
            static const float  HORIZON_CLAMP      = 0.08;    // v5.3: was 0.12 (v5.2), 0.05 (v5.1)

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

            float3 SafeNormalizeDir(float3 v, float3 fallback)
            {
                float lenSq = dot(v, v);
                return (lenSq < DIR_THRESHOLD * DIR_THRESHOLD)
                    ? fallback
                    : v * rsqrt(lenSq);
            }

            void ResolvePhaseSkyColors(
                half nightFactor,
                out half3 zenithColor,
                out half3 horizonColor,
                out half3 nadirColor)
            {
                zenithColor = _SkyColorZenith.rgb;
                horizonColor = _SkyColorHorizon.rgb;
                nadirColor = _SkyColorNadir.rgb;
            }

            float hash(float2 p)
            {
                p += float2(_StarSeed * 0.071, _StarSeed * 0.113);
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            half3 SampleMeteorGpuParticles(float3 Vf, half zenithMask, half skyVisibility)
            {
                float intensity = saturate(_MeteorShowerParams.x);
                if (intensity <= 0.0001 || zenithMask <= 0.001h || skyVisibility <= 0.001h)
                    return half3(0.0h, 0.0h, 0.0h);

                float2 meteorUV;
                meteorUV.x = atan2(Vf.z, Vf.x) * (0.5 / 3.14159265) + 0.5;
                meteorUV.y = asin(Vf.y) * (1.0 / 3.14159265) + 0.5;

                float2 travelDir = _MeteorShowerDirection.xy;
                float travelLenSq = dot(travelDir, travelDir);
                travelDir = travelLenSq < 0.0001
                    ? float2(-0.907, -0.421)
                    : travelDir * rsqrt(travelLenSq);
                float2 sideDir = float2(-travelDir.y, travelDir.x);

                float streakLength = max(_MeteorShowerDirection.z, 0.02);
                float streakWidth = max(_MeteorShowerDirection.w, 0.0005);
                float eventAge = max(_MeteorShowerParams.w, 0.0);
                float seed = _MeteorShowerParams.y;
                float streamTime = eventAge * 2.25 + seed * 0.013;

                half3 meteor = half3(0.0h, 0.0h, 0.0h);
                [unroll]
                for (int i = 0; i < 6; i++)
                {
                    float streamId = floor(streamTime) - (float)i;
                    float localT = frac(streamTime + hash(float2(streamId, seed + (float)i * 13.17)));
                    float active = step(0.40, hash(float2(streamId + 19.0, seed + 31.0)));
                    float2 origin = float2(
                        hash(float2(streamId + 3.0, seed + 7.0)),
                        lerp(0.56, 0.98, hash(float2(streamId + 11.0, seed + 23.0))));
                    float2 head = origin + travelDir * ((localT - 0.18) * 0.74);

                    float2 delta = meteorUV - head;
                    delta.x = frac(delta.x + 0.5) - 0.5;

                    float behind = dot(delta, -travelDir);
                    float lateral = abs(dot(delta, sideDir));
                    float width = streakWidth * lerp(0.7, 1.45, hash(float2(streamId + 41.0, seed)));
                    float trail = smoothstep(streakLength, 0.0, behind) * step(0.0, behind);
                    float core = smoothstep(width, 0.0, lateral) * trail;
                    float headCore = smoothstep(width * 2.4, 0.0, length(delta));
                    float birthFade = smoothstep(0.03, 0.16, localT) * (1.0 - smoothstep(0.82, 1.0, localT));
                    float energy = active * birthFade * (core + headCore * 1.45);

                    half warmth = (half)hash(float2(streamId + 71.0, seed));
                    half3 meteorColor = lerp(
                        half3(0.62h, 0.82h, 1.0h),
                        half3(1.0h, 0.74h, 0.52h),
                        warmth);
                    meteor += meteorColor * (half)energy;
                }

                half syncedFlash = (half)saturate(_MeteorShowerParams.z);
                meteor += half3(0.62h, 0.78h, 1.0h) * syncedFlash * 0.42h;
                return meteor * (half)intensity * skyVisibility * zenithMask;
            }

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

            float2 ComputeCelestialTransmittanceUV(float3 V)
            {
                float2 uv;
                uv.x = atan2(V.z, V.x) * (0.5 / 3.14159265) + 0.5;
                uv.y = V.y * 0.5 + 0.5;
                uv *= _CelestialTransmittanceTiling.xy;
                uv.x += _WindDirection.x * _GameTime * _CelestialTransmittanceScrollSpeed;
                uv.y += _WindDirection.y * _GameTime * (_CelestialTransmittanceScrollSpeed * 0.25);
                return uv;
            }

            half SampleCelestialTransmittance(float3 V, half horizonFactor)
            {
                float2 uv = ComputeCelestialTransmittanceUV(V);
                half sample = SAMPLE_TEXTURE2D(
                    _MainCloudTex, sampler_MainCloudTex, uv).r;

                half edge0 = saturate(_CelestialTransmittanceThreshold
                                    - _CelestialTransmittanceSoftness);
                half edge1 = saturate(_CelestialTransmittanceThreshold
                                    + _CelestialTransmittanceSoftness);
                half softField = smoothstep(edge0, edge1, sample);

                half horizonBoost = pow(saturate(1.0h - abs(horizonFactor)), 2.0h);
                return softField
                     * _CelestialTransmittanceStrength
                     * saturate(0.25h + horizonBoost * 1.25h);
            }

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

            half4 SkyFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 Vf = normalize(input.viewDirWS);
                half3  V  = (half3)Vf;
                half   horizonFactor = input.horizonFactor;
                half celestialExtinction = SampleCelestialTransmittance(Vf, horizonFactor);
                half celestialTransmittance = saturate(1.0h - celestialExtinction);

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
                // ECLIPSE + SUNSET MASKS
                // =======================================
                half sunElevation = (half)_SunElevation;
                half eclipseVis   = 1.0h - (half)_EclipseOcclusion;
                half nightFactor  = saturate((half)_NightBlend);
                half eclipseNight = max(nightFactor, (half)_EclipseOcclusion);
                half solarCloudFade = smoothstep(-0.08h, 0.24h, sunElevation);
                half cloudDayReturn = solarCloudFade * solarCloudFade;
                cloudDayReturn *= cloudDayReturn;
                half nightCloudVisibility = lerp(0.002h, 1.0h, cloudDayReturn);

                half skyNightDimFactor = lerp(1.0h, 0.18h, nightFactor);
                half skyDimFactor = skyNightDimFactor * lerp(1.0h, 0.12h,
                    smoothstep(0.1h, 0.9h, (half)_EclipseOcclusion));

                half sunsetFactor = saturate(1.0h - abs(sunElevation) * 8.0h);
                sunsetFactor *= eclipseVis;
                half sunsetSpot     = pow(sunViewDot, 4.0h) * sunsetFactor;
                half beltOfVenus    = pow(saturate(dot(V, sunDir)), 3.0h)
                                    * sunsetFactor * 0.4h;

                // =======================================
                // BASE SKY GRADIENT
                // =======================================
                half zenithMask  = saturate(horizonFactor);
                half nadirMask   = saturate(-horizonFactor);
                half horizonMask = 1.0h - zenithMask - nadirMask;

                half3 phaseZenithColor;
                half3 phaseHorizonColor;
                half3 phaseNadirColor;
                ResolvePhaseSkyColors(
                    nightFactor,
                    phaseZenithColor,
                    phaseHorizonColor,
                    phaseNadirColor);

                half3 skyColor = phaseZenithColor  * zenithMask
                               + phaseHorizonColor * horizonMask
                               + phaseNadirColor   * nadirMask;

                skyColor *= skyDimFactor * max(_SkyLuminanceMultiplier, 0.0h);

                half3 sunsetHorizonContrib = _SunsetHorizonColor.rgb
                                           * sunsetSpot
                                           * horizonMask;
                skyColor += sunsetHorizonContrib;

                half3 beltColor = half3(0.6h, 0.3h, 0.5h) * beltOfVenus * horizonMask;
                skyColor += beltColor;

                // =======================================
                // LAYER 0: STAR FIELD
                // =======================================
                half3 starContrib = half3(0.0h, 0.0h, 0.0h);

                if (eclipseNight > 0.01h && zenithMask > 0.01h)
                {
                    float2 starUV;
                    starUV.x = atan2(Vf.z, Vf.x) * (0.5 / 3.14159265) + 0.5;
                    starUV.y = asin(Vf.y) * (1.0 / 3.14159265) + 0.5;
                    starUV *= _StarTiling.xy;

                    float2 starGrid = starUV * 128.0;
                    float2 starCell = floor(starGrid);
                    float2 starLocal = frac(starGrid) - 0.5;
                    float densityHash = hash(starCell);
                    float2 starOffset = float2(hash(starCell + 17.0), hash(starCell + 43.0)) - 0.5;
                    float starDistance = length(starLocal - starOffset * 0.65);
                    half starCore = (half)(step(0.985, densityHash) * smoothstep(0.055, 0.0, starDistance));
                    half3 proceduralStarColor = lerp(
                        half3(0.72h, 0.82h, 1.0h),
                        half3(1.0h, 0.92h, 0.78h),
                        (half)hash(starCell + 91.0));
                    float starPhase = hash(starCell + 131.0) * 6.28318;

                    half starDayFade = saturate(-sunElevation * 10.0h);
                    half starVisibility = max(nightFactor * starDayFade,
                                              (half)_EclipseOcclusion);

                    half horizonTwinkle = saturate(1.0h - abs(Vf.y));
                    half atmosphereTwinkle = saturate(_AtmosphereDensity);
                    float twinkleSpeed = (float)_StarTwinkleSpeed *
                        (1.0 + (float)horizonTwinkle * 2.4 + (float)atmosphereTwinkle * 3.1);
                    float quantizedTwinkleTime = floor(_GameTime * twinkleSpeed * lerp(3.0, 8.0, (float)horizonTwinkle));
                    float noiseTwinkle = hash(starCell + float2(quantizedTwinkleTime, quantizedTwinkleTime * 1.37) + 211.0);
                    half flicker = 0.72h
                        + (0.18h + 0.26h * horizonTwinkle * atmosphereTwinkle)
                            * (half)sin(_GameTime * twinkleSpeed + starPhase)
                        + (half)((noiseTwinkle - 0.5) * (float)atmosphereTwinkle * (0.18 + (float)horizonTwinkle * 0.24));
                    flicker = saturate(flicker);

                    starContrib = proceduralStarColor
                                * _StarColor.rgb
                                * _StarIntensity
                                * starCore
                                * flicker
                                * starVisibility
                                * zenithMask;
                    starContrib *= lerp(1.0h, celestialTransmittance, _CelestialStarFade);
                }

                skyColor += starContrib;
                half meteorVisibility = saturate(max(nightFactor, (half)_EclipseOcclusion) + saturate(-sunElevation * 2.0h) * 0.35h);
                skyColor += SampleMeteorGpuParticles(Vf, zenithMask, meteorVisibility);

                // =======================================
                // LAYER 3: HORIZON HAZE
                // =======================================
                half hazeRaw = saturate(1.0h - abs(horizonFactor));
                half hazeSoft = smoothstep(0.0h, 1.0h, hazeRaw);
                half hazeMask = saturate(pow(hazeSoft, _HazeFalloff) * _HazeIntensity);

                half3 hazeSunTint = lerp(
                    HALF_ONE,
                    _SunScatterColor.rgb,
                    sunViewDot * _HazeSunTintStrength);

                half3 hazeColor = _HazeColor.rgb * hazeSunTint;
                hazeColor *= skyDimFactor * lerp(1.0h, 0.14h, nightFactor);

                half hazeVeil = saturate(hazeMask * lerp(0.62h, 0.42h, nightFactor));
                skyColor = lerp(skyColor, hazeColor, hazeVeil * 0.32h);
                skyColor += hazeColor * hazeMask * 0.68h;

                // =======================================
                // v5.3: ATMOSPHERIC PERSPECTIVE
                //
                // Instead of cutting clouds at horizon (v5.2 gap),
                // we simulate atmospheric scattering:
                //   - Near horizon: air between viewer and clouds
                //     scatters light, washing out detail and color
                //   - Clouds become a soft uniform layer matching sky
                //   - UV aliasing becomes invisible (no contrast)
                //   - No gap, physically correct
                //
                // atmosClarity:
                //   0.0 = at horizon (thick atmosphere, full wash)
                //   1.0 = above ~17° (clear, full detail)
                // =======================================
                half atmosClarity = smoothstep(0.05h, 0.30h, horizonFactor);

                // =======================================
                // LAYER 1: CIRRUS CLOUDS
                // v5.3: dissolves into sky at horizon
                // =======================================
                float2 cirrusUV = ComputeCirrusUV(
                    Vf, _CirrusTiling.xy, _CirrusSpeedMult);

                half4 cirrusSample = SAMPLE_TEXTURE2D(
                    _MainCloudTex, sampler_MainCloudTex, cirrusUV);

                half cirrusDensity = cirrusSample.r;

                half cirrusBacklit = pow(
                    saturate(1.0h - dot(V, -L)),
                    _BacklitPower * 0.5h) * cirrusDensity;

                cirrusBacklit *= eclipseVis;

                half3 cirrusColor = _CirrusColor.rgb
                                  + cirrusBacklit * _BacklitColor.rgb * 0.3h;

                // v5.3: cirrus fades into sky at horizon
                skyColor = lerp(
                    skyColor,
                    skyColor + cirrusColor,
                    cirrusDensity * _CirrusOpacity * atmosClarity * lerp(1.0h, 0.18h, nightFactor) * nightCloudVisibility);

                // =======================================
                // LAYER 2: MAIN CLOUDS
                // v5.3: atmospheric perspective at horizon
                // =======================================
                float2 cloudBaseUV = ComputeSkyUV(
                    Vf, _CloudTiling.xy, _CloudSpeedMult);

                half2 cloudRG = SampleFlowmap(
                    TEXTURE2D_ARGS(_MainCloudTex, sampler_MainCloudTex),
                    cloudBaseUV);

                half cloudDensity = cloudRG.x;
                half cloudDetail  = cloudRG.y;

                // v5.3: detail fades at horizon — kills mipmap aliasing source.
                // Without fine detail, the stretched UV produces smooth gradients
                // instead of high-frequency banding.
                cloudDensity -= cloudDetail * _DetailStrength * atmosClarity;

                // v5.3: at horizon — lower threshold (everything becomes cloud),
                // wider softness (ultra-smooth edges). Result: continuous soft
                // layer instead of flickering mask from aliased density values.
                half adjThreshold = _CloudDensityThreshold * lerp(0.15h, 1.0h, atmosClarity);
                half adjSoftness  = _CloudSoftness + (1.0h - atmosClarity) * 0.25h;
                half cloudMask = smoothstep(adjThreshold, adjThreshold + adjSoftness, cloudDensity);

                half cloudNdotL = saturate(dot(V, L));

                half3 cloudLitDay     = _CloudColorLit.rgb;
                half3 cloudLitSunset  = lerp(cloudLitDay,
                                             _SunsetCloudColor.rgb,
                                             sunsetSpot);
                half3 cloudLitFinal   = lerp(cloudLitSunset,
                                             _NightCloudColor.rgb,
                                             eclipseNight);

                // v5.3: Aegir cloud glow fades at horizon (no purple streaks)
                half aegirDotForClouds = saturate(dot(V, aegirDir));
                half aegirGlowIntensity = max(_AegirGlowIntensity, 0.0h);
                half3 aegirCloudGlow   = _AegirHaloColor.rgb
                                       * aegirDotForClouds
                                       * eclipseNight
                                       * aegirGlowIntensity
                                       * cloudMask
                                       * atmosClarity
                                       * lerp(0.02h, 1.0h, cloudDayReturn);

                half3 cloudBaseColor = lerp(
                    _CloudColorShadow.rgb,
                    cloudLitFinal,
                    cloudNdotL);

                cloudBaseColor += aegirCloudGlow;
                cloudBaseColor *= max(_SkyLuminanceMultiplier, 0.0h);
                cloudBaseColor *= nightCloudVisibility;

                half backlitSunsetBoost = 1.0h + sunsetFactor * 1.5h;
                half backlitFactor = pow(
                    saturate(sunViewDot),
                    _BacklitPower);

                // v5.3: backlit fades at horizon (primary barcode source at night)
                half3 backlitGlow = _BacklitColor.rgb
                                  * backlitFactor
                                  * cloudMask
                                  * _BacklitIntensity
                                  * backlitSunsetBoost
                                  * eclipseVis
                                  * lerp(1.0h, 0.16h, nightFactor)
                                  * atmosClarity
                                  * cloudDayReturn;

                half3 cloudColor = cloudBaseColor + backlitGlow;

                // v5.3: ATMOSPHERIC PERSPECTIVE — the key fix.
                // At horizon, cloud color becomes sky color.
                // lerp(skyColor, cloudColor, 0) = skyColor → no contrast
                // → no visible aliasing → no barcode → no gap.
                // Above ~17°: clouds render normally.
                cloudColor = lerp(skyColor, cloudColor, atmosClarity);

                // v5.3: gentle height fade. Since cloudColor = skyColor at horizon,
                // this is cosmetic — lerp(sky, sky, mask) = sky regardless.
                // Prevents any residual edge at the very bottom of the dome.
                half finalCloudMask = cloudMask
                                    * saturate(horizonFactor * 4.0h)
                                    * lerp(0.01h, 1.0h, cloudDayReturn);
                skyColor = lerp(skyColor, cloudColor, finalCloudMask);

                // Low ocean mist shelf: a soft air-mass veil hugging the horizon.
                // This uses the same haze owner color instead of introducing a
                // separate horizon paint pass, so ocean/fog/sky stay linked.
                half mistShelfLower = smoothstep(-_HorizonMistShelfSoftness, 0.0h, horizonFactor);
                half mistShelfUpper = 1.0h - smoothstep(
                    _HorizonMistShelfHeight,
                    _HorizonMistShelfHeight + _HorizonMistShelfSoftness,
                    horizonFactor);
                half mistShelfBand = mistShelfLower * mistShelfUpper;
                half mistShelfBreakup = lerp(0.82h, 1.08h, saturate(cloudRG.x * 0.72h + cirrusDensity * 0.28h));
                half mistShelfDensity = mistShelfBand
                                      * hazeMask
                                      * mistShelfBreakup
                                      * _HorizonMistShelfIntensity
                                      * lerp(1.0h, 0.28h, nightFactor);

                half3 mistShelfColor = lerp(hazeColor, skyColor, 0.18h);
                mistShelfColor = lerp(mistShelfColor, _SunScatterColor.rgb, sunsetFactor * 0.08h);
                skyColor = lerp(skyColor, mistShelfColor, mistShelfDensity * 0.42h);
                skyColor += mistShelfColor * mistShelfDensity * 0.12h;

                float4 sharedAtmosphereSample = SampleHectonCelestialAtmosphere(
                    Vf,
                    _SkyColorHorizon.rgb,
                    _SkyColorZenith.rgb);
                skyColor = (half3)ApplyHectonCelestialAtmosphere(
                    skyColor,
                    sharedAtmosphereSample,
                    _AtmosphereTransmittanceWeight,
                    _AtmosphereInscatterWeight);

                // =======================================
                // SUN DISC
                // =======================================
                half sunDist = 1.0h - sunViewDot;
                half sunSoftness = max(_SunEdgeSoftness, 0.0001h);
                half sunRadius = max(_SunSize, 0.0001h);
                half softRadius = max(sunRadius + sunSoftness * 18.0h, 0.0002h);
                half normalizedSunDist = sunDist / softRadius;
                half normalizedSunDistSq = normalizedSunDist * normalizedSunDist;
                half sunCore = exp2(-normalizedSunDistSq * 18.0h);
                half sunDisc = exp2(-normalizedSunDistSq * 6.5h);
                half sunCorona = exp2(-normalizedSunDistSq * 0.95h);
                half sunOuterCorona = exp2(-normalizedSunDistSq * 0.28h);
                half sunAboveHorizon = smoothstep(-0.04h, 0.08h, _SunElevation);

                half sunVisibility = (1.0h - finalCloudMask) * eclipseVis * sunAboveHorizon;
                sunVisibility *= lerp(1.0h, celestialTransmittance, _CelestialSunFade);
                half3 softDiscColor = lerp(_SunScatterColor.rgb, _SunDiscColor.rgb, 0.55h);
                half3 sunDiscColor = softDiscColor * (sunDisc * 0.55h + sunCore * 0.65h);
                skyColor += sunDiscColor * sunVisibility;

                // =======================================
                // SUN SCATTERING
                // =======================================
                half sunScatter = pow(
                    saturate(sunViewDot),
                    _SunScatterPower);
                half coronaScatter = sunCorona * 0.75h + sunOuterCorona * 0.35h;
                half3 skyScatterTint = lerp(_SunScatterColor.rgb, skyColor, 0.18h);
                half3 sunGlow = _SunScatterColor.rgb
                              * (sunScatter * 0.35h + coronaScatter)
                              * _SunScatterIntensity;
                sunGlow += skyScatterTint * sunOuterCorona * (_SunScatterIntensity * 0.12h);

                sunGlow *= (1.0h - finalCloudMask * 0.7h) * eclipseVis * sunAboveHorizon;
                sunGlow *= lerp(1.0h, celestialTransmittance, _CelestialSunFade);
                skyColor += sunGlow;

                // =======================================
                // AEGIR HALO
                // =======================================
                half aegirDot = saturate(dot(V, aegirDir));

                half aegirHalo = pow(aegirDot, _AegirHaloPower)
                               * _AegirHaloIntensity
                               * aegirGlowIntensity;

                aegirHalo *= (1.0h - finalCloudMask * 0.5h);
                aegirHalo *= lerp(1.0h, celestialTransmittance, _CelestialHaloFade);
                skyColor += _AegirHaloColor.rgb * aegirHalo;

                return half4(skyColor, 1.0h);
            }

            ENDHLSL
        }
    }

    FallBack Off
}
