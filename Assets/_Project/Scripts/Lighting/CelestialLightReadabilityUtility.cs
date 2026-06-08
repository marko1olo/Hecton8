using Unity.Mathematics;

namespace Hecton8.Core
{
    public static class CelestialLightReadabilityUtility
    {
        private const float SunDirectionEpsilonSq = 0.000001f;
        private const float MaxModeledDepthMeters = 12000f;
        private const float MinPlayableAbyssReadability = 0.055f;

        public static CelestialLightDepthStratum ResolveDepthStratum(float depthMeters)
        {
            float depth = SanitizeDepthMeters(depthMeters);
            if (depth < 50f)
                return CelestialLightDepthStratum.SurfaceTo50Meters;
            if (depth < 100f)
                return CelestialLightDepthStratum.Shallow50To100Meters;
            if (depth < 500f)
                return CelestialLightDepthStratum.Mesophotic100To500Meters;
            if (depth < 2000f)
                return CelestialLightDepthStratum.Deep500To2000Meters;
            return CelestialLightDepthStratum.Abyss2000PlusMeters;
        }

        public static CelestialLightReadabilitySnapshot Evaluate(
            in CelestialRuntimeSnapshot celestial,
            float depthMeters,
            float timeOfDay01,
            float sunElevationDegrees,
            float surfaceSunIntensity,
            float directionalLightIntensity,
            float3 directionalLightColor,
            float quality01,
            uint sequence)
        {
            bool celestialValid = IsCelestialValid(in celestial);
            float3 sunDirection = celestialValid
                ? NormalizeSafe(celestial.SunDirection, new float3(0f, 1f, 0f))
                : new float3(0f, 1f, 0f);
            float depth = SanitizeDepthMeters(depthMeters);
            float quality = Sanitize01(quality01, 1f);
            float phase01 = Sanitize01(timeOfDay01, 0f);
            float elevation01 = ResolveSunElevation01(sunElevationDegrees, sunDirection);
            float eclipse01 = celestialValid ? Sanitize01(celestial.EclipseOcclusion01, 0f) : 0f;
            float sourceIntensity = math.max(
                SanitizeNonNegative(surfaceSunIntensity, 1f),
                SanitizeNonNegative(directionalLightIntensity, 1f));
            float surfaceDirect = math.saturate(sourceIntensity * elevation01 * (1f - eclipse01 * 0.86f));
            float surfaceAmbient = math.saturate(0.18f + elevation01 * 0.76f - eclipse01 * 0.42f);
            CelestialLightDepthStratum stratum = ResolveDepthStratum(depth);

            float direct;
            float ambient;
            float visibility;
            float mesophotic;
            float deepDarkness;
            float artificial;
            float biolum;
            float caustic;
            float fog;
            float scatter;
            float absorption;
            float exposure;
            float emissive;
            float blackFloor;
            ResolveDepthBehavior(
                stratum,
                depth,
                surfaceDirect,
                surfaceAmbient,
                quality,
                out direct,
                out ambient,
                out visibility,
                out mesophotic,
                out deepDarkness,
                out artificial,
                out biolum,
                out caustic,
                out fog,
                out scatter,
                out absorption,
                out exposure,
                out emissive,
                out blackFloor);

            float nightOrEclipse = math.saturate((1f - elevation01) + eclipse01);
            biolum = math.saturate(math.max(biolum, nightOrEclipse * 0.55f));
            emissive = math.saturate(math.max(emissive, biolum * 0.82f));
            caustic = math.saturate(caustic * (1f - eclipse01));
            float3 lightColor = SanitizeColor(directionalLightColor);

            uint flags = CelestialLightReadabilityFlagsValue(
                celestialValid,
                depth > 0.01f,
                nightOrEclipse,
                caustic,
                biolum,
                artificial,
                blackFloor,
                quality);

            CelestialLightReadabilitySnapshot snapshot = default;
            snapshot.AbsoluteUniverseTime = celestialValid ? celestial.AbsoluteUniverseTime : 0d;
            snapshot.SunDirection = sunDirection;
            snapshot.DepthMeters = depth;
            snapshot.TimeOfDay01 = phase01;
            snapshot.SunElevation01 = elevation01;
            snapshot.DirectSun01 = direct;
            snapshot.AmbientReadability01 = ambient;
            snapshot.UnderwaterVisibilityMeters = visibility;
            snapshot.MesophoticFalloff01 = mesophotic;
            snapshot.DeepDarkness01 = deepDarkness;
            snapshot.ArtificialLightWeight01 = artificial;
            snapshot.BiolumWeight01 = biolum;
            snapshot.CausticWeight01 = caustic;
            snapshot.FogDensityMultiplier = fog;
            snapshot.ScatteringMultiplier = scatter;
            snapshot.AbsorptionMultiplier = absorption;
            snapshot.ExposureCompensation = exposure;
            snapshot.EmissiveWeight01 = emissive;
            snapshot.SurfaceLight01 = surfaceDirect;
            snapshot.Quality01 = quality;
            snapshot.BlackCrushFloor01 = blackFloor;
            snapshot.SunColorIntensity = new float4(lightColor, sourceIntensity);
            snapshot.DepthStrataParams = new float4(
                ResolveDepthBandStartMeters(stratum),
                ResolveDepthBandEndMeters(stratum),
                math.rcp(math.max(1f, visibility)),
                depth > 0.01f ? 1f : 0f);
            snapshot.Flags = flags;
            snapshot.DepthStratum = (uint)stratum;
            snapshot.CelestialSequence = celestialValid ? celestial.Sequence : 0u;
            snapshot.Sequence = sequence;
            return snapshot;
        }

        public static float4 ModulateWaterDirectionalLight(float4 baseline, in CelestialLightReadabilitySnapshot light)
        {
            float4 safe = math.all(math.isfinite(baseline))
                ? baseline
                : new float4(0.09f, 0.42f, 0.70f, 0.85f);
            safe.xyz = math.max(safe.xyz, 0f);
            safe.w = math.max(safe.w, 0f);

            if ((light.Flags & (uint)CelestialLightReadabilityFlags.Valid) == 0u)
                return safe;

            float readability = math.max(light.BlackCrushFloor01, light.DirectSun01 + light.AmbientReadability01 * 0.35f);
            float biolumLift = light.BiolumWeight01 * 0.045f;
            float3 sunTint = math.max(light.SunColorIntensity.xyz, 0f);
            float tintWeight = math.saturate(light.DirectSun01 * 0.22f);
            safe.xyz = math.lerp(safe.xyz, math.max(safe.xyz, sunTint), tintWeight);
            safe.w = math.min(8f, safe.w * math.max(light.BlackCrushFloor01, readability) + biolumLift);
            return safe;
        }

        public static float ResolveCausticsIntensityMultiplier(in CelestialLightReadabilitySnapshot light)
        {
            if ((light.Flags & (uint)CelestialLightReadabilityFlags.Valid) == 0u)
                return 1f;

            return math.saturate(light.CausticWeight01 * math.lerp(0.72f, 1f, light.Quality01));
        }

        public static float ResolveCausticsMaxDepthMeters(in CelestialLightReadabilitySnapshot light, float fallbackMaxDepthMeters)
        {
            float fallback = math.max(1f, SanitizeNonNegative(fallbackMaxDepthMeters, 72f));
            if ((light.Flags & (uint)CelestialLightReadabilityFlags.Valid) == 0u)
                return fallback;

            float bandEnd = math.max(1f, light.DepthStrataParams.y);
            float readableReach = math.max(1f, light.UnderwaterVisibilityMeters * 1.35f);
            return math.min(fallback, math.min(bandEnd, readableReach));
        }

        private static void ResolveDepthBehavior(
            CelestialLightDepthStratum stratum,
            float depth,
            float surfaceDirect,
            float surfaceAmbient,
            float quality,
            out float direct,
            out float ambient,
            out float visibility,
            out float mesophotic,
            out float deepDarkness,
            out float artificial,
            out float biolum,
            out float caustic,
            out float fog,
            out float scatter,
            out float absorption,
            out float exposure,
            out float emissive,
            out float blackFloor)
        {
            switch (stratum)
            {
                case CelestialLightDepthStratum.SurfaceTo50Meters:
                {
                    float t = math.saturate(depth / 50f);
                    direct = math.saturate(surfaceDirect * math.lerp(1f, 0.72f, t));
                    ambient = math.max(math.lerp(0.74f, 0.58f, t), surfaceAmbient * math.lerp(0.92f, 0.72f, t));
                    visibility = math.lerp(112f, 68f, t);
                    mesophotic = 0f;
                    deepDarkness = math.lerp(0f, 0.12f, t);
                    artificial = math.lerp(0.05f, 0.22f, t);
                    biolum = math.lerp(0.08f, 0.24f, t);
                    caustic = direct * math.lerp(0.92f, 0.58f, t);
                    fog = math.lerp(0.72f, 1.05f, t);
                    scatter = math.lerp(0.86f, 1.08f, t);
                    absorption = math.lerp(0.82f, 1.05f, t);
                    exposure = math.lerp(0f, 0.08f, t);
                    emissive = biolum * 0.42f;
                    blackFloor = math.lerp(0.12f, 0.10f, t);
                    break;
                }
                case CelestialLightDepthStratum.Shallow50To100Meters:
                {
                    float t = math.saturate((depth - 50f) / 50f);
                    direct = math.saturate(surfaceDirect * math.lerp(0.64f, 0.34f, t));
                    ambient = math.max(math.lerp(0.54f, 0.36f, t), surfaceAmbient * math.lerp(0.55f, 0.38f, t));
                    visibility = math.lerp(68f, 42f, t);
                    mesophotic = math.lerp(0.05f, 0.24f, t);
                    deepDarkness = math.lerp(0.14f, 0.26f, t);
                    artificial = math.lerp(0.24f, 0.42f, t);
                    biolum = math.lerp(0.26f, 0.44f, t);
                    caustic = direct * math.lerp(0.46f, 0.18f, t);
                    fog = math.lerp(1.05f, 1.42f, t);
                    scatter = math.lerp(1.08f, 1.32f, t);
                    absorption = math.lerp(1.05f, 1.42f, t);
                    exposure = math.lerp(0.08f, 0.2f, t);
                    emissive = biolum * 0.58f;
                    blackFloor = math.lerp(0.10f, 0.085f, t);
                    break;
                }
                case CelestialLightDepthStratum.Mesophotic100To500Meters:
                {
                    float t = math.saturate((depth - 100f) / 400f);
                    direct = math.saturate(surfaceDirect * math.lerp(0.24f, 0.035f, t));
                    ambient = math.max(math.lerp(0.31f, 0.14f, t), surfaceAmbient * math.lerp(0.28f, 0.10f, t));
                    visibility = math.lerp(42f, 18f, t);
                    mesophotic = SmoothStep01(t);
                    deepDarkness = math.lerp(0.30f, 0.58f, t);
                    artificial = math.lerp(0.46f, 0.70f, t);
                    biolum = math.lerp(0.48f, 0.72f, t);
                    caustic = direct * math.lerp(0.14f, 0f, t);
                    fog = math.lerp(1.42f, 2.45f, t);
                    scatter = math.lerp(1.32f, 1.95f, t);
                    absorption = math.lerp(1.42f, 2.55f, t);
                    exposure = math.lerp(0.2f, 0.42f, t);
                    emissive = biolum * 0.72f;
                    blackFloor = math.lerp(0.085f, 0.07f, t);
                    break;
                }
                case CelestialLightDepthStratum.Deep500To2000Meters:
                {
                    float t = math.saturate((depth - 500f) / 1500f);
                    direct = math.saturate(surfaceDirect * math.lerp(0.025f, 0.004f, t));
                    ambient = math.max(math.lerp(0.12f, 0.075f, t), surfaceAmbient * math.lerp(0.08f, 0.035f, t));
                    visibility = math.lerp(18f, 8f, t);
                    mesophotic = 1f;
                    deepDarkness = math.lerp(0.62f, 0.88f, t);
                    artificial = math.lerp(0.74f, 0.94f, t);
                    biolum = math.lerp(0.74f, 0.90f, t);
                    caustic = 0f;
                    fog = math.lerp(2.45f, 3.75f, t);
                    scatter = math.lerp(1.95f, 2.65f, t);
                    absorption = math.lerp(2.55f, 4.25f, t);
                    exposure = math.lerp(0.42f, 0.68f, t);
                    emissive = biolum * 0.86f;
                    blackFloor = math.lerp(0.07f, 0.058f, t);
                    break;
                }
                default:
                {
                    float t = math.saturate((depth - 2000f) / 3000f);
                    direct = 0f;
                    ambient = math.lerp(0.075f, MinPlayableAbyssReadability, t);
                    visibility = math.lerp(8f, 5f, t);
                    mesophotic = 1f;
                    deepDarkness = math.lerp(0.90f, 0.97f, t);
                    artificial = 1f;
                    biolum = math.lerp(0.92f, 0.98f, t);
                    caustic = 0f;
                    fog = math.lerp(3.75f, 4.85f, t);
                    scatter = math.lerp(2.65f, 3.20f, t);
                    absorption = math.lerp(4.25f, 5.25f, t);
                    exposure = math.lerp(0.70f, 0.86f, t);
                    emissive = math.lerp(0.84f, 0.94f, t);
                    blackFloor = math.lerp(0.058f, MinPlayableAbyssReadability, t);
                    break;
                }
            }

            float qualityFloor = math.lerp(0.84f, 1f, quality);
            visibility *= qualityFloor;
            caustic *= qualityFloor;
            fog *= math.lerp(1.12f, 1f, quality);
            scatter *= math.lerp(0.92f, 1f, quality);
            direct = math.saturate(direct);
            ambient = math.saturate(math.max(ambient, blackFloor));
            visibility = math.max(1f, visibility);
            deepDarkness = math.saturate(deepDarkness);
            artificial = math.saturate(artificial);
            biolum = math.saturate(biolum);
            caustic = math.saturate(caustic);
            exposure = math.max(0f, exposure);
            emissive = math.saturate(emissive);
        }

        private static uint CelestialLightReadabilityFlagsValue(
            bool celestialValid,
            bool underwater,
            float nightOrEclipse,
            float caustic,
            float biolum,
            float artificial,
            float blackFloor,
            float quality)
        {
            uint flags = (uint)CelestialLightReadabilityFlags.Valid;
            if (!celestialValid)
                flags |= (uint)CelestialLightReadabilityFlags.Fallback;
            if (underwater)
                flags |= (uint)CelestialLightReadabilityFlags.Underwater;
            if (nightOrEclipse > 0.25f)
                flags |= (uint)CelestialLightReadabilityFlags.EclipseOrNight;
            if (caustic > 0.02f)
                flags |= (uint)CelestialLightReadabilityFlags.CausticsAllowed;
            if (biolum > 0.45f)
                flags |= (uint)CelestialLightReadabilityFlags.BiolumFavored;
            if (artificial > 0.65f)
                flags |= (uint)CelestialLightReadabilityFlags.ArtificialLightCritical;
            if (blackFloor >= MinPlayableAbyssReadability)
                flags |= (uint)CelestialLightReadabilityFlags.BlackCrushGuard;
            if (quality < 0.55f)
                flags |= (uint)CelestialLightReadabilityFlags.QualityReduced;
            return flags;
        }

        private static float ResolveSunElevation01(float sunElevationDegrees, float3 sunDirection)
        {
            if (math.isfinite(sunElevationDegrees))
                return math.saturate((sunElevationDegrees + 8f) / 56f);

            return math.saturate((sunDirection.y + 0.10f) * 0.9090909f);
        }

        private static bool IsCelestialValid(in CelestialRuntimeSnapshot celestial)
        {
            return (celestial.Flags & (uint)CelestialRuntimeFlags.Valid) != 0u &&
                   !double.IsNaN(celestial.AbsoluteUniverseTime) &&
                   !double.IsInfinity(celestial.AbsoluteUniverseTime) &&
                   math.all(math.isfinite(celestial.SunDirection)) &&
                   math.lengthsq(celestial.SunDirection) > SunDirectionEpsilonSq;
        }

        private static float ResolveDepthBandStartMeters(CelestialLightDepthStratum stratum)
        {
            switch (stratum)
            {
                case CelestialLightDepthStratum.Shallow50To100Meters: return 50f;
                case CelestialLightDepthStratum.Mesophotic100To500Meters: return 100f;
                case CelestialLightDepthStratum.Deep500To2000Meters: return 500f;
                case CelestialLightDepthStratum.Abyss2000PlusMeters: return 2000f;
                default: return 0f;
            }
        }

        private static float ResolveDepthBandEndMeters(CelestialLightDepthStratum stratum)
        {
            switch (stratum)
            {
                case CelestialLightDepthStratum.SurfaceTo50Meters: return 50f;
                case CelestialLightDepthStratum.Shallow50To100Meters: return 100f;
                case CelestialLightDepthStratum.Mesophotic100To500Meters: return 500f;
                case CelestialLightDepthStratum.Deep500To2000Meters: return 2000f;
                default: return MaxModeledDepthMeters;
            }
        }

        private static float3 SanitizeColor(float3 value)
        {
            float3 safe = math.all(math.isfinite(value)) ? value : new float3(1f, 1f, 1f);
            return math.max(safe, 0f);
        }

        private static float SanitizeDepthMeters(float depthMeters)
        {
            return math.clamp(math.select(depthMeters, 0f, !math.isfinite(depthMeters) || depthMeters < 0f), 0f, MaxModeledDepthMeters);
        }

        private static float Sanitize01(float value, float fallback)
        {
            return math.saturate(math.select(value, fallback, !math.isfinite(value)));
        }

        private static float SanitizeNonNegative(float value, float fallback)
        {
            return math.max(0f, math.select(value, fallback, !math.isfinite(value) || value < 0f));
        }

        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            bool valid = math.isfinite(lenSq) && lenSq > SunDirectionEpsilonSq;
            return math.select(fallback, value * math.rsqrt(math.max(lenSq, SunDirectionEpsilonSq)), valid);
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }
    }
}
