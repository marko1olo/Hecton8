// ============================================================================
// HECTON-8 -- Hecton_AlienSky_Master.shader
// Atmospheric sky dome shader for the exomoon Hecton.
// Unity 6 | URP 17+ | SRP Batcher Compatible
//
// v5.3 -- ATMOSPHERIC PERSPECTIVE HORIZON FIX
//
//   [FIX] Replaced v5.2's hard cloud cutoff with atmospheric perspective.
//         v5.2 used smoothstep to REMOVE clouds near horizon -> visible gap.
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
//         - Cloud color -> sky color (atmospheric perspective)
//         - Backlit glow fades (no bright streaks)
//         - Cirrus fades smoothly
//
//   [PERF] One smoothstep + two lerps added. Zero texture samples added.
//
// v5.1 PRESERVED:
//   [OK] Eclipse sky darkening via eclipseVis
//   [OK] All sunset/golden hour logic
//   [OK] Belt of Venus
//   [OK] Star NASA-Punk flicker + elevation fade
//   [OK] Aegir cloud illumination at night
//   [OK] Planar ceiling UV, flowmap, dither
//   [OK] SRP Batcher compatible
//   [OK] 5 texture samples total
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
        _StarTwinkleLUT ("Star Twinkle LUT", 2D) = "white" {}
        _StarSeed ("Star Seed", Float) = 99173
        _BakedStarCubemap ("Startup Baked Star Cubemap", 2DArray) = "" {}
        _BakedStarCubemapReady ("Baked Star Cubemap Ready", Range(0, 1)) = 0.0
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

        [Header(Aegir Lensing)]
        _AegirLensingRadius ("Aegir Lensing Radius", Range(0.01, 0.6)) = 0.32
        _AegirLensingEdgeWidth ("Aegir Lensing Edge Width", Range(0.005, 0.25)) = 0.07
        _AegirLensingStrength ("Aegir Lensing Strength", Range(0, 0.08)) = 0.018
        _AegirLensingTint ("Aegir Lensing Tint", Range(0, 1)) = 0.18

        [Header(Aurora)]
        _AuroraIntensity ("Aurora Intensity", Range(0, 4)) = 0.65
        _AuroraScale ("Aurora Scale", Vector) = (2.2, 5.8, 0, 0)
        _AuroraSpeed ("Aurora Speed", Range(0, 0.2)) = 0.028
        _AuroraHorizonFade ("Aurora Horizon Fade", Range(0, 0.7)) = 0.18
        [HDR] _AuroraColorA ("Aurora Color A", Color) = (0.02, 0.95, 0.72, 1)
        [HDR] _AuroraColorB ("Aurora Color B", Color) = (0.45, 0.28, 1.35, 1)

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
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON
            #pragma skip_variants POINT POINT_COOKIE SHADOWS_CUBE
            #pragma skip_variants _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Hecton_CelestialAtmosphere.hlsl"

            TEXTURE2D(_MainCloudTex);       SAMPLER(sampler_MainCloudTex);
            TEXTURE2D(_StarTwinkleLUT);     SAMPLER(sampler_StarTwinkleLUT);
            TEXTURE2D_ARRAY(_BakedStarCubemap); SAMPLER(sampler_BakedStarCubemap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainCloudTex_ST;

                float4 _StarTiling;
                half4  _StarColor;
                half   _StarIntensity;
                half   _StarTwinkleSpeed;
                float  _StarSeed;
                half   _BakedStarCubemapReady;
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

                half   _AegirLensingRadius;
                half   _AegirLensingEdgeWidth;
                half   _AegirLensingStrength;
                half   _AegirLensingTint;

                half   _AuroraIntensity;
                float4 _AuroraScale;
                half   _AuroraSpeed;
                half   _AuroraHorizonFade;
                half4  _AuroraColorA;
                half4  _AuroraColorB;

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
            float4x4 _HectonSkyRotation;
            int _HectonSkyOccluderCount;
            float4 _HectonSkyOccluders[8];
            float4 _MeteorShowerParams;     // x=intensity, y=seed, z=synced flash, w=event age
            float4 _MeteorShowerDirection;  // xy=sky UV travel direction, z=streak length, w=streak width
            float _HectonFreezeFrameDither;
            float _GamePaused;

            static const half  HALF_ZERO = 0.0h;
            static const half  HALF_ONE  = 1.0h;

            static const float3 FALLBACK_SUN_DIR   = float3(0.57735, 0.57735, 0.57735);
            static const float3 FALLBACK_AEGIR_DIR = float3(0.0, 0.93633, -0.35112);
            static const float  DIR_THRESHOLD      = 0.001;
            static const float  HORIZON_CLAMP      = 0.08;    // v5.3: was 0.12 (v5.2), 0.05 (v5.1)
            static const float  HECTON_PI          = 3.14159265;
            static const float  HECTON_HALF_PI     = 1.57079633;
            static const float  HECTON_INV_PI      = 0.31830988618;
            static const float  HECTON_HALF_INV_PI = 0.15915494309;

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

            float HectonFastAsinUnit(float y)
            {
                float x = clamp(y, -1.0, 1.0);
                float ax = abs(x);
                float root = sqrt(max(0.0, 1.0 - ax));
                float approx = 1.5707288 + ax * (-0.2121144 + ax * (0.0742610 - 0.0187293 * ax));
                return (1.5707963 - root * approx) * sign(x);
            }

            float HectonFastLatitude01(float y)
            {
                return HectonFastAsinUnit(y) * HECTON_INV_PI + 0.5;
            }

            float HectonFastAtan2(float y, float x)
            {
                float ax = abs(x);
                float ay = abs(y);
                float major = max(ax, ay);
                float minor = min(ax, ay);
                float ratio = minor / max(major, 0.00000001);
                float ratioSq = ratio * ratio;
                float poly = (((-0.0464964749 * ratioSq + 0.15931422) * ratioSq - 0.327622764) * ratioSq + 1.0) * ratio;
                float swapped = step(ax, ay);
                float angle = lerp(poly, HECTON_HALF_PI - poly, swapped);
                float xNeg = 1.0 - step(0.0, x);
                angle = lerp(angle, HECTON_PI - angle, xNeg);
                float yNeg = 1.0 - step(0.0, y);
                angle = lerp(angle, -angle, yNeg);
                return angle * step(0.00000001, major);
            }

            float HectonFastLongitude01(float z, float x)
            {
                return HectonFastAtan2(z, x) * HECTON_HALF_INV_PI + 0.5;
            }

            void HectonDirectionToStarArrayUv(float3 direction, out float2 uv, out uint face)
            {
                float3 absDirection = abs(direction);
                if (absDirection.x >= absDirection.y && absDirection.x >= absDirection.z)
                {
                    face = direction.x >= 0.0 ? 0u : 1u;
                    uv = direction.x >= 0.0
                        ? float2(-direction.z, direction.y) / max(absDirection.x, 0.000001)
                        : float2(direction.z, direction.y) / max(absDirection.x, 0.000001);
                }
                else if (absDirection.y >= absDirection.x && absDirection.y >= absDirection.z)
                {
                    face = direction.y >= 0.0 ? 2u : 3u;
                    uv = direction.y >= 0.0
                        ? float2(direction.x, -direction.z) / max(absDirection.y, 0.000001)
                        : float2(direction.x, direction.z) / max(absDirection.y, 0.000001);
                }
                else
                {
                    face = direction.z >= 0.0 ? 4u : 5u;
                    uv = direction.z >= 0.0
                        ? float2(direction.x, direction.y) / max(absDirection.z, 0.000001)
                        : float2(-direction.x, direction.y) / max(absDirection.z, 0.000001);
                }

                uv = saturate(uv * 0.5 + 0.5);
            }

            half3 SampleBakedStarArray(float3 direction)
            {
                float2 uv;
                uint face;
                HectonDirectionToStarArrayUv(SafeNormalizeDir(direction, FALLBACK_AEGIR_DIR), uv, face);
                return (half3)SAMPLE_TEXTURE2D_ARRAY(_BakedStarCubemap, sampler_BakedStarCubemap, uv, face).rgb;
            }

            half ComputeSkyOccluderVisibility(float3 viewDir)
            {
                half visibility = 1.0h;
                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    if (i >= _HectonSkyOccluderCount)
                        break;

                    float4 occluder = _HectonSkyOccluders[i];
                    float3 occluderDir = SafeNormalizeDir(occluder.xyz, FALLBACK_AEGIR_DIR);
                    float radius = max(occluder.w, 0.00001);
                    half blocked = (half)step(cos(radius), dot(viewDir, occluderDir));
                    visibility *= 1.0h - blocked;
                }

                return visibility;
            }

            float HectonSkyHash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float HectonSkyValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = HectonSkyHash21(i);
                float b = HectonSkyHash21(i + float2(1.0, 0.0));
                float c = HectonSkyHash21(i + float2(0.0, 1.0));
                float d = HectonSkyHash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float HectonSkyAnimationTime()
            {
                return _GameTime * (1.0 - saturate(_GamePaused));
            }

            float3 ApplyAegirLensing(float3 viewDir, float3 aegirDir, out half lensMask)
            {
                float viewDot = saturate(dot(viewDir, aegirDir));
                float lensRadius = max((float)_AegirLensingRadius, 0.001);
                float edgeWidth = max((float)_AegirLensingEdgeWidth, 0.001);
                float innerRadius = max(lensRadius - edgeWidth, 0.001);
                float outerRadius = lensRadius + edgeWidth;
                float centerDot = saturate(1.0 - 0.5 * lensRadius * lensRadius);
                float innerDot = saturate(1.0 - 0.5 * innerRadius * innerRadius);
                float outerDot = saturate(1.0 - 0.5 * outerRadius * outerRadius);
                float ring = smoothstep(outerDot, centerDot, viewDot) * (1.0 - smoothstep(centerDot, innerDot, viewDot));

                float3 tangent = viewDir - aegirDir * viewDot;
                tangent = SafeNormalizeDir(tangent, float3(1.0, 0.0, 0.0));
                float strength = max((float)_AegirLensingStrength, 0.0) * ring;
                lensMask = (half)ring;
                return normalize(viewDir + tangent * strength);
            }

            half3 ComputeAurora(
                float3 viewDir,
                half horizonFactor,
                half nightFactor,
                half eclipseOcclusion,
                half skyVisibility)
            {
                half intensity = max(_AuroraIntensity, 0.0h);
                if (intensity <= 0.001h || horizonFactor <= 0.0h || skyVisibility <= 0.001h)
                    return half3(0.0h, 0.0h, 0.0h);

                float2 uv;
                uv.x = HectonFastLongitude01(viewDir.z, viewDir.x);
                uv.y = saturate(viewDir.y * 0.5 + 0.5);

                float2 noiseUv = uv * _AuroraScale.xy;
                float animationTime = HectonSkyAnimationTime();
                noiseUv.x += animationTime * (float)_AuroraSpeed;
                noiseUv.y += animationTime * (float)_AuroraSpeed * 0.37;

                float n0 = HectonSkyValueNoise(noiseUv);
                float n1 = HectonSkyValueNoise(noiseUv * 2.13 + 17.3) * 0.5;
                float n2 = HectonSkyValueNoise(noiseUv * 4.07 + 41.7) * 0.25;
                float noise = saturate((n0 + n1 + n2) * (1.0 / 1.75));
                float curtainPhase = uv.x * 18.0 + noise * 3.0 + animationTime * (float)_AuroraSpeed * 5.0;
                half curtain = pow(
                    saturate(1.0h - abs((half)frac(curtainPhase) - 0.5h) * 2.0h),
                    3.0h);
                half filament = smoothstep(0.38h, 0.92h, (half)noise);
                half lowerFade = smoothstep(_AuroraHorizonFade, 0.86h, horizonFactor);
                half zenithFade = 1.0h - smoothstep(0.92h, 1.0h, horizonFactor);
                half darkness = saturate(max(nightFactor, eclipseOcclusion) + saturate(-(half)_SunElevation * 1.2h));
                half alpha = intensity * lowerFade * zenithFade * darkness * curtain * filament * skyVisibility;
                half colorMix = saturate((half)noise * 1.2h);
                return lerp(_AuroraColorA.rgb, _AuroraColorB.rgb, colorMix) * alpha;
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
                meteorUV.x = HectonFastLongitude01(Vf.z, Vf.x);
                meteorUV.y = HectonFastLatitude01(Vf.y);

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
                skyUV += _WindDirection.xy * HectonSkyAnimationTime() * speedMult;
                return skyUV;
            }

            float2 ComputeCirrusUV(float3 V, float2 tiling, float speedMult)
            {
                float projY = max(V.y, HORIZON_CLAMP);
                float2 skyUV = V.xz / projY;
                skyUV *= tiling;
                skyUV += _WindDirection.xy * HectonSkyAnimationTime() * speedMult;
                skyUV += V.xz * _CirrusParallaxStrength;
                return skyUV;
            }

            float2 ComputeCelestialTransmittanceUV(float3 V)
            {
                float2 uv;
                uv.x = HectonFastLongitude01(V.z, V.x);
                uv.y = V.y * 0.5 + 0.5;
                uv *= _CelestialTransmittanceTiling.xy;
                float animationTime = HectonSkyAnimationTime();
                uv.x += _WindDirection.x * animationTime * _CelestialTransmittanceScrollSpeed;
                uv.y += _WindDirection.y * animationTime * (_CelestialTransmittanceScrollSpeed * 0.25);
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
                float time = HectonSkyAnimationTime() * (float)_FlowCycleSpeed;
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

            half3 ApplyFreezeFrameDither(half3 color, float4 positionCS)
            {
                half freeze = (half)saturate(_HectonFreezeFrameDither);
                float2 pixel = floor(positionCS.xy);
                half noise = (half)frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
                half scanline = (half)step(0.5, frac(positionCS.y * 0.5));
                half ditherMask = (half)step(noise, freeze);
                half3 frozenTint = color * 0.74h + half3(0.015h, 0.050h, 0.070h) * 0.26h;
                frozenTint += ((noise - 0.5h) * 0.052h) + (scanline * 0.018h);
                frozenTint *= lerp(1.0h, 0.82h + ditherMask * 0.18h, freeze);
                return lerp(color, frozenTint, freeze);
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
                half   horizonFactor = input.horizonFactor;

                // =======================================
                // RESOLVE GLOBAL DIRECTIONS
                // =======================================
                half3 sunDir = (half3)SafeNormalizeDir(
                    _SunDirection.xyz, FALLBACK_SUN_DIR);
                half3 L = -sunDir;

                half3 aegirDir = (half3)SafeNormalizeDir(
                    _AegirDirection.xyz, FALLBACK_AEGIR_DIR);

                half aegirLensMask;
                float3 sampledVf = ApplyAegirLensing(Vf, (float3)aegirDir, aegirLensMask);
                half3  V  = (half3)sampledVf;
                half celestialExtinction = SampleCelestialTransmittance(sampledVf, horizonFactor);
                half celestialTransmittance = saturate(1.0h - celestialExtinction);

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
                // Surface sky cannot collapse to a flat cyan wash when the sun is low.
                // Aegir and moonlight still reveal cloud mass; only contrast/detail recedes.
                half nightCloudVisibility = lerp(0.46h, 1.0h, cloudDayReturn);

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
                half skyOccluderVisibility = ComputeSkyOccluderVisibility(Vf);

                if (eclipseNight > 0.01h && zenithMask > 0.01h)
                {
                    float3 starLookupDir = normalize(mul((float3x3)_HectonSkyRotation, sampledVf));
                    float2 starUV;
                    starUV.x = HectonFastLongitude01(starLookupDir.z, starLookupDir.x);
                    starUV.y = HectonFastLatitude01(starLookupDir.y);
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

                    half horizonTwinkle = saturate(1.0h - abs(sampledVf.y));
                    half atmosphereTwinkle = saturate(_AtmosphereDensity);
                    float twinkleSpeed = (float)_StarTwinkleSpeed *
                        (1.0 + (float)horizonTwinkle * 2.4 + (float)atmosphereTwinkle * 3.1);
                    float animationTime = HectonSkyAnimationTime();
                    float quantizedTwinkleTime = floor(animationTime * twinkleSpeed * lerp(3.0, 8.0, (float)horizonTwinkle));
                    float twinkleLutU = frac(
                        dot(starCell, float2(0.00390625, 0.0078125))
                        + quantizedTwinkleTime * 0.03125
                        + _StarSeed * 0.0009765625);
                    float noiseTwinkle = SAMPLE_TEXTURE2D(
                        _StarTwinkleLUT,
                        sampler_StarTwinkleLUT,
                        float2(twinkleLutU, 0.5)).r;
                    half flicker = 0.72h
                        + (0.18h + 0.26h * horizonTwinkle * atmosphereTwinkle)
                            * (half)sin(animationTime * twinkleSpeed + starPhase)
                        + (half)((noiseTwinkle - 0.5) * (float)atmosphereTwinkle * (0.18 + (float)horizonTwinkle * 0.24));
                    flicker = saturate(flicker);

                    half3 bakedStarColor = SampleBakedStarArray(starLookupDir);
                    half bakedReady = step(0.5h, _BakedStarCubemapReady);
                    half bakedLuma = dot(bakedStarColor, half3(0.2126h, 0.7152h, 0.0722h));
                    half bakedBrightMask = smoothstep(0.08h, 0.42h, bakedLuma);
                    half3 proceduralStar = proceduralStarColor * starCore * flicker;
                    half3 bakedStar = bakedStarColor * lerp(1.0h, flicker, bakedBrightMask);
                    half3 starSourceColor = lerp(proceduralStar, bakedStar, bakedReady);

                    starContrib = starSourceColor
                                * _StarColor.rgb
                                * _StarIntensity
                                * starVisibility
                                * zenithMask
                                * skyOccluderVisibility;
                    starContrib *= lerp(1.0h, celestialTransmittance, _CelestialStarFade);
                }

                skyColor += starContrib;
                half meteorVisibility = saturate(max(nightFactor, (half)_EclipseOcclusion) + saturate(-sunElevation * 2.0h) * 0.35h);
                skyColor += SampleMeteorGpuParticles(sampledVf, zenithMask, meteorVisibility) * skyOccluderVisibility;
                skyColor += ComputeAurora(
                    sampledVf,
                    horizonFactor,
                    nightFactor,
                    (half)_EclipseOcclusion,
                    skyOccluderVisibility);

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
                //   1.0 = above about 17 degrees (clear, full detail)
                // =======================================
                half atmosClarity = smoothstep(-0.012h, 0.22h, horizonFactor);

                // =======================================
                // LAYER 1: CIRRUS CLOUDS
                // v5.3: dissolves into sky at horizon
                // =======================================
                float2 cirrusUV = ComputeCirrusUV(
                    sampledVf, _CirrusTiling.xy, _CirrusSpeedMult);

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
                    sampledVf, _CloudTiling.xy, _CloudSpeedMult);

                half2 cloudRG = SampleFlowmap(
                    TEXTURE2D_ARGS(_MainCloudTex, sampler_MainCloudTex),
                    cloudBaseUV);

                half cloudDensity = cloudRG.x;
                half cloudDetail  = cloudRG.y;

                // v5.3: detail fades at horizon -- kills mipmap aliasing source.
                // Without fine detail, the stretched UV produces smooth gradients
                // instead of high-frequency banding.
                cloudDensity -= cloudDetail * _DetailStrength * atmosClarity;

                // v5.3: at horizon -- lower threshold (everything becomes cloud),
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
                half aegirDotForClouds = saturate(dot((half3)Vf, aegirDir));
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

                // v5.3: ATMOSPHERIC PERSPECTIVE -- the key fix.
                // At horizon, cloud color becomes sky color.
                // lerp(skyColor, cloudColor, 0) = skyColor -> no contrast
                // -> no visible aliasing -> no barcode -> no gap.
                // Above about 17 degrees: clouds render normally.
                half cloudPerspective = lerp(0.62h, 1.0h, atmosClarity);
                cloudColor = lerp(skyColor, cloudColor, cloudPerspective);

                // Keep a believable lower cloud floor at the waterline. Haze softens it,
                // but it must not erase all cloud structure from surface screenshots.
                half horizonCloudFloor = lerp(0.66h, 1.0h, saturate(horizonFactor * 2.5h));
                half cloudBodyMask = saturate(max(cloudMask, cirrusDensity * _CirrusOpacity * 0.35h));
                half finalCloudMask = cloudBodyMask
                                    * horizonCloudFloor
                                    * lerp(0.42h, 1.0h, cloudDayReturn);
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
                    sampledVf,
                    _SkyColorHorizon.rgb,
                    _SkyColorZenith.rgb,
                    _SunDirection.xyz);
                skyColor = (half3)ApplyHectonCelestialAtmosphere(
                    skyColor,
                    sharedAtmosphereSample,
                    _AtmosphereTransmittanceWeight,
                    _AtmosphereInscatterWeight);

                // Surface readability pass. The atmosphere LUT is allowed to tint
                // and soften clouds, but not erase the authored cloud deck into a
                // flat cyan/black wash in surface screenshots or Scene View.
                half surfaceReadability = saturate(
                    (1.0h - nightFactor)
                    * (1.0h - (half)_EclipseOcclusion)
                    * (1.0h - nadirMask));
                half3 surfaceSkyFloor = lerp(
                    _SkyColorHorizon.rgb,
                    _SkyColorZenith.rgb,
                    saturate(zenithMask * 0.82h + horizonMask * 0.18h));
                skyColor = max(
                    skyColor,
                    surfaceSkyFloor * surfaceReadability * 0.24h);

                // Surface skybox fallback: the camera clear path can expose the nadir
                // below the authored sky meshes. Keep that lower hemisphere as bright
                // horizon air, not black faux water. Zero extra samples; continuous
                // day/eclipse weighting preserves the same path on Low/Mid/High/Ultra.
                half surfaceNadirReadability = saturate(
                    (1.0h - nightFactor)
                    * (1.0h - (half)_EclipseOcclusion)
                    * smoothstep(0.02h, 0.56h, nadirMask));
                half3 surfaceNadirFloor = lerp(
                    _SkyColorHorizon.rgb,
                    max(_SkyColorNadir.rgb, _SkyColorHorizon.rgb * 0.62h),
                    saturate(nadirMask * 0.82h));
                surfaceNadirFloor *= max(_SkyLuminanceMultiplier, 0.78h);
                skyColor = max(
                    skyColor,
                    surfaceNadirFloor * surfaceNadirReadability * 0.78h);
                skyColor = lerp(
                    skyColor,
                    max(skyColor, surfaceNadirFloor),
                    surfaceNadirReadability * 0.34h);

                float2 authoredCloudReadUV;
                authoredCloudReadUV.x = HectonFastLongitude01(sampledVf.z, sampledVf.x)
                                      * max(_CloudTiling.x, 0.001)
                                      + _WindDirection.x * HectonSkyAnimationTime() * _CloudSpeedMult * 0.08;
                authoredCloudReadUV.y = saturate(sampledVf.y * 0.62 + 0.42)
                                      * max(_CloudTiling.y, 0.001)
                                      + _WindDirection.y * HectonSkyAnimationTime() * _CloudSpeedMult * 0.035;
                half authoredCloudDensity = SAMPLE_TEXTURE2D(
                    _MainCloudTex,
                    sampler_MainCloudTex,
                    authoredCloudReadUV).r;
                half authoredCloudMask = smoothstep(0.30h, 0.70h, authoredCloudDensity);

                half postAtmosphereCloudMask = smoothstep(0.42h, 0.86h, cloudRG.x);
                postAtmosphereCloudMask = max(postAtmosphereCloudMask, authoredCloudMask * 0.82h);
                postAtmosphereCloudMask = max(postAtmosphereCloudMask, finalCloudMask * 0.72h);
                postAtmosphereCloudMask *= (1.0h - nadirMask)
                                        * lerp(0.34h, 0.68h, cloudDayReturn)
                                        * lerp(0.72h, 1.0h, atmosClarity);
                half3 postAtmosphereCloudColor = lerp(
                    _CloudColorShadow.rgb,
                    _CloudColorLit.rgb,
                    saturate(cloudNdotL * 0.48h + 0.36h));
                postAtmosphereCloudColor = lerp(
                    postAtmosphereCloudColor,
                    _NightCloudColor.rgb,
                    eclipseNight * 0.55h);
                postAtmosphereCloudColor *= max(_SkyLuminanceMultiplier, 0.78h);
                postAtmosphereCloudColor = lerp(
                    skyColor,
                    postAtmosphereCloudColor,
                    0.78h);
                skyColor = lerp(
                    skyColor,
                    postAtmosphereCloudColor,
                    saturate(postAtmosphereCloudMask));

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
                half aegirDot = saturate(dot((half3)Vf, aegirDir));

                half aegirHalo = pow(aegirDot, _AegirHaloPower)
                               * _AegirHaloIntensity
                               * aegirGlowIntensity;

                aegirHalo *= (1.0h - finalCloudMask * 0.5h);
                aegirHalo *= lerp(1.0h, celestialTransmittance, _CelestialHaloFade);
                skyColor += _AegirHaloColor.rgb * aegirHalo;
                skyColor += _AegirHaloColor.rgb
                          * aegirLensMask
                          * _AegirLensingTint
                          * lerp(0.35h, 1.0h, eclipseNight)
                          * (1.0h - finalCloudMask * 0.35h);
                skyColor = ApplyFreezeFrameDither(skyColor, input.positionCS);

                return half4(skyColor, 1.0h);
            }

            ENDHLSL
        }
    }

    FallBack Off
}
