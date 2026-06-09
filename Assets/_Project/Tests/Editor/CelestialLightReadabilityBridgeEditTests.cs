using System;
using System.IO;
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Rendering.WaterOptics;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class CelestialLightReadabilityBridgeEditTests
    {
        [Test]
        public void LightReadabilitySnapshotLayoutStaysBlittableAndSeparateFromCelestialRuntime()
        {
            Assert.That(UnsafeUtility.SizeOf<CelestialRuntimeSnapshot>(), Is.EqualTo(144));
            Assert.That(UnsafeUtility.SizeOf<CelestialLightReadabilitySnapshot>(), Is.EqualTo(144));
            Assert.That(UnsafeUtility.SizeOf<AudioStemTelemetryEntry>(), Is.EqualTo(64));
            Assert.That(AdaptiveStemAudioMixer.TelemetryFlagCelestialLightBound, Is.EqualTo(1u << 5));
            Assert.That(AdaptiveStemAudioMixer.TelemetryFlagCelestialLightMissing, Is.EqualTo(1u << 6));
            Assert.That(AdaptiveStemAudioMixer.TelemetryFlagCelestialLightFallback, Is.EqualTo(1u << 7));
            Assert.That(AdaptiveStemAudioMixer.TelemetryFlagCelestialLightAbyssCritical, Is.EqualTo(1u << 8));
            Assert.That(AdaptiveStemAudioMixer.TelemetryFlagCelestialLightQualityReduced, Is.EqualTo(1u << 9));
            Assert.That(AdaptiveStemAudioMixer.TelemetryFlagCelestialLightTwilight, Is.EqualTo(1u << 10));
            Assert.That(AdaptiveStemAudioMixer.TelemetryFlagCelestialLightNight, Is.EqualTo(1u << 11));
            Assert.That(CelestialLightReadabilityFlags.LightPhaseDay, Is.EqualTo((CelestialLightReadabilityFlags)(1u << 9)));
            Assert.That(CelestialLightReadabilityFlags.LightPhaseTwilight, Is.EqualTo((CelestialLightReadabilityFlags)(1u << 10)));
            Assert.That(CelestialLightReadabilityFlags.LightPhaseNight, Is.EqualTo((CelestialLightReadabilityFlags)(1u << 11)));
            Assert.That(WaterOpticsRuntime.TelemetryFlagCelestialLightMissing, Is.EqualTo(1u << 8));
            Assert.That(WaterOpticsRuntime.TelemetryFlagCelestialLightFallback, Is.EqualTo(1u << 9));
            Assert.That(WaterOpticsRuntime.TelemetryFlagCelestialLightArtificialCritical, Is.EqualTo(1u << 10));
            Assert.That(WaterOpticsRuntime.TelemetryFlagCelestialLightQualityReduced, Is.EqualTo(1u << 11));
            Assert.That(WaterOpticsRuntime.TelemetryFlagCelestialLightTwilight, Is.EqualTo(1u << 12));
            Assert.That(WaterOpticsRuntime.TelemetryFlagCelestialLightNight, Is.EqualTo(1u << 13));
        }

        [Test]
        public void DepthStrataResolveRequiredGameplayBands()
        {
            Assert.That(CelestialLightReadabilityUtility.ResolveDepthStratum(25f), Is.EqualTo(CelestialLightDepthStratum.SurfaceTo50Meters));
            Assert.That(CelestialLightReadabilityUtility.ResolveDepthStratum(75f), Is.EqualTo(CelestialLightDepthStratum.Shallow50To100Meters));
            Assert.That(CelestialLightReadabilityUtility.ResolveDepthStratum(250f), Is.EqualTo(CelestialLightDepthStratum.Mesophotic100To500Meters));
            Assert.That(CelestialLightReadabilityUtility.ResolveDepthStratum(1000f), Is.EqualTo(CelestialLightDepthStratum.Deep500To2000Meters));
            Assert.That(CelestialLightReadabilityUtility.ResolveDepthStratum(2500f), Is.EqualTo(CelestialLightDepthStratum.Abyss2000PlusMeters));
        }

        [Test]
        public void NoonDepthModelKeepsShallowsReadableAndDeepArtificialLightCritical()
        {
            CelestialRuntimeSnapshot celestial = BuildCelestial(new float3(0.25f, 0.92f, 0.15f), eclipse01: 0f);
            CelestialLightReadabilitySnapshot shallow = CelestialLightReadabilityUtility.Evaluate(
                in celestial,
                25f,
                0.5f,
                42f,
                1f,
                1f,
                new float3(1f, 0.96f, 0.9f),
                1f,
                1u);
            CelestialLightReadabilitySnapshot mesophotic = CelestialLightReadabilityUtility.Evaluate(
                in celestial,
                250f,
                0.5f,
                42f,
                1f,
                1f,
                new float3(1f, 0.96f, 0.9f),
                1f,
                2u);
            CelestialLightReadabilitySnapshot abyss = CelestialLightReadabilityUtility.Evaluate(
                in celestial,
                2500f,
                0.5f,
                42f,
                1f,
                1f,
                new float3(1f, 0.96f, 0.9f),
                1f,
                3u);

            Assert.That(shallow.DirectSun01, Is.GreaterThan(0.55f));
            Assert.That(shallow.UnderwaterVisibilityMeters, Is.GreaterThan(75f));
            Assert.That(shallow.CausticWeight01, Is.GreaterThan(0.35f));
            Assert.That(mesophotic.MesophoticFalloff01, Is.GreaterThan(0.25f));
            Assert.That(mesophotic.DirectSun01, Is.LessThan(shallow.DirectSun01));
            Assert.That(abyss.DirectSun01, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(abyss.DeepDarkness01, Is.GreaterThan(0.90f));
            Assert.That(abyss.ArtificialLightWeight01, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(abyss.BlackCrushFloor01, Is.GreaterThanOrEqualTo(0.055f));
            Assert.That((abyss.Flags & (uint)CelestialLightReadabilityFlags.ArtificialLightCritical), Is.Not.EqualTo(0u));
        }

        [Test]
        public void DepthAndTimeGridAvoidsBlackCrushAndOverbrightWater()
        {
            float[] depths = { 25f, 75f, 250f, 1000f, 2500f };
            float[] elevations = { 44f, 4f, -8f };
            float4 baseline = new float4(0.09f, 0.42f, 0.70f, 0.85f);
            uint sequence = 1u;

            for (int d = 0; d < depths.Length; d++)
            {
                for (int e = 0; e < elevations.Length; e++)
                {
                    CelestialRuntimeSnapshot celestial = BuildCelestial(
                        new float3(0.18f, math.sin(math.radians(elevations[e])), 0.72f),
                        eclipse01: e == 1 ? 0.45f : 0f);
                    CelestialLightReadabilitySnapshot light = CelestialLightReadabilityUtility.Evaluate(
                        in celestial,
                        depths[d],
                        e == 2 ? 0.02f : 0.5f,
                        elevations[e],
                        1f,
                        1f,
                        new float3(1f, 0.95f, 0.88f),
                        0.72f,
                        sequence++);
                    float4 waterLight = CelestialLightReadabilityUtility.ModulateWaterDirectionalLight(baseline, in light);

                    Assert.That((light.Flags & (uint)CelestialLightReadabilityFlags.Valid), Is.Not.EqualTo(0u));
                    Assert.That(math.isfinite(light.DirectSun01), Is.True);
                    Assert.That(math.isfinite(light.AmbientReadability01), Is.True);
                    Assert.That(math.isfinite(light.BlackCrushFloor01), Is.True);
                    Assert.That(math.all(math.isfinite(waterLight)), Is.True);
                    Assert.That(light.AmbientReadability01, Is.GreaterThanOrEqualTo(light.BlackCrushFloor01));
                    Assert.That(light.BlackCrushFloor01, Is.GreaterThanOrEqualTo(0.055f));
                    Assert.That(math.cmax(light.SunColorIntensity.xyz), Is.LessThanOrEqualTo(1.0001f));
                    Assert.That(light.DirectSun01, Is.InRange(0f, 1f));
                    Assert.That(light.CausticWeight01, Is.InRange(0f, 1f));
                    Assert.That(math.cmax(waterLight.xyz), Is.LessThanOrEqualTo(1.0001f));
                    Assert.That(waterLight.w, Is.InRange(0.04f, 1.25f));
                    if (depths[d] >= 500f)
                        Assert.That(light.CausticWeight01, Is.EqualTo(0f).Within(0.0001f));
                }
            }
        }

        [Test]
        public void LightPhaseFlagsComeFromReadabilitySnapshot()
        {
            CelestialRuntimeSnapshot celestial = BuildCelestial(new float3(0.25f, 0.92f, 0.15f), eclipse01: 0f);
            CelestialLightReadabilitySnapshot day = CelestialLightReadabilityUtility.Evaluate(
                in celestial,
                25f,
                0.5f,
                42f,
                1f,
                1f,
                new float3(1f, 0.96f, 0.9f),
                1f,
                4u);
            CelestialLightReadabilitySnapshot twilight = CelestialLightReadabilityUtility.Evaluate(
                in celestial,
                25f,
                0.25f,
                4f,
                1f,
                1f,
                new float3(1f, 0.96f, 0.9f),
                1f,
                5u);
            CelestialLightReadabilitySnapshot night = CelestialLightReadabilityUtility.Evaluate(
                in celestial,
                25f,
                0.02f,
                -8f,
                1f,
                1f,
                new float3(1f, 0.96f, 0.9f),
                1f,
                6u);
            CelestialRuntimeSnapshot eclipseCelestial = BuildCelestial(new float3(0.25f, 0.92f, 0.15f), eclipse01: 0.85f);
            CelestialLightReadabilitySnapshot eclipse = CelestialLightReadabilityUtility.Evaluate(
                in eclipseCelestial,
                25f,
                0.5f,
                42f,
                1f,
                1f,
                new float3(1f, 0.96f, 0.9f),
                1f,
                7u);

            AssertPhase(day, CelestialLightReadabilityFlags.LightPhaseDay);
            AssertPhase(twilight, CelestialLightReadabilityFlags.LightPhaseTwilight);
            AssertPhase(night, CelestialLightReadabilityFlags.LightPhaseNight);
            AssertPhase(eclipse, CelestialLightReadabilityFlags.LightPhaseNight);
            Assert.That((eclipse.Flags & (uint)CelestialLightReadabilityFlags.EclipseOrNight), Is.Not.EqualTo(0u));
        }

        [Test]
        public void InvalidCelestialFallsBackWithoutBlackCrushOrNonFiniteValues()
        {
            CelestialRuntimeSnapshot invalid = default;
            CelestialLightReadabilitySnapshot snapshot = CelestialLightReadabilityUtility.Evaluate(
                in invalid,
                float.NaN,
                float.PositiveInfinity,
                float.NaN,
                float.NaN,
                float.NegativeInfinity,
                new float3(float.NaN, 0f, 0f),
                float.NaN,
                7u);

            Assert.That((snapshot.Flags & (uint)CelestialLightReadabilityFlags.Valid), Is.Not.EqualTo(0u));
            Assert.That((snapshot.Flags & (uint)CelestialLightReadabilityFlags.Fallback), Is.Not.EqualTo(0u));
            Assert.That(math.all(math.isfinite(snapshot.SunDirection)), Is.True);
            Assert.That(math.isfinite(snapshot.AmbientReadability01), Is.True);
            Assert.That(snapshot.BlackCrushFloor01, Is.GreaterThan(0f));
        }

        [Test]
        public void MissingAtmosphericLightingProfileMarksReadabilityFallback()
        {
            CelestialRuntimeSnapshot celestial = BuildCelestial(new float3(0.25f, 0.92f, 0.15f), eclipse01: 0f);
            CelestialLightReadabilitySnapshot snapshot = CelestialLightReadabilityUtility.Evaluate(
                in celestial,
                25f,
                0.5f,
                42f,
                1f,
                1f,
                new float3(1f, 0.96f, 0.9f),
                1f,
                8u,
                lightingStateFallback: true);

            Assert.That((snapshot.Flags & (uint)CelestialLightReadabilityFlags.Valid), Is.Not.EqualTo(0u));
            Assert.That((snapshot.Flags & (uint)CelestialLightReadabilityFlags.Fallback), Is.Not.EqualTo(0u));
            Assert.That(snapshot.CelestialSequence, Is.EqualTo(celestial.Sequence));
            Assert.That(snapshot.DirectSun01, Is.InRange(0f, 1f));
        }

        [Test]
        public void HdrSunInputsAreCappedBeforeTheyReachWaterReadability()
        {
            CelestialRuntimeSnapshot celestial = BuildCelestial(new float3(0.25f, 0.92f, 0.15f), eclipse01: 0f);
            CelestialLightReadabilitySnapshot snapshot = CelestialLightReadabilityUtility.Evaluate(
                in celestial,
                25f,
                0.5f,
                42f,
                64f,
                128f,
                new float3(8f, 4f, 2f),
                1f,
                8u);
            float4 waterLight = CelestialLightReadabilityUtility.ModulateWaterDirectionalLight(
                new float4(4f, 3f, 2f, 9f),
                in snapshot);

            Assert.That(math.cmax(snapshot.SunColorIntensity.xyz), Is.LessThanOrEqualTo(1.0001f));
            Assert.That(snapshot.SunColorIntensity.w, Is.LessThanOrEqualTo(1.2501f));
            Assert.That(math.all(math.isfinite(waterLight)), Is.True);
            Assert.That(math.cmax(waterLight.xyz), Is.LessThanOrEqualTo(1.0001f));
            Assert.That(waterLight.w, Is.LessThanOrEqualTo(1.2501f));
        }

        [Test]
        public void LowQualityLightReadabilityKeepsGameplayFloorAndMarksQualityReduced()
        {
            CelestialRuntimeSnapshot celestial = BuildCelestial(new float3(0.25f, 0.92f, 0.15f), eclipse01: 0f);
            CelestialLightReadabilitySnapshot lowQuality = CelestialLightReadabilityUtility.Evaluate(
                in celestial,
                1000f,
                0.5f,
                42f,
                1f,
                1f,
                new float3(1f, 0.95f, 0.88f),
                0.35f,
                9u);
            CelestialLightReadabilitySnapshot highQuality = CelestialLightReadabilityUtility.Evaluate(
                in celestial,
                1000f,
                0.5f,
                42f,
                1f,
                1f,
                new float3(1f, 0.95f, 0.88f),
                1f,
                10u);

            Assert.That((lowQuality.Flags & (uint)CelestialLightReadabilityFlags.QualityReduced), Is.Not.EqualTo(0u));
            Assert.That((lowQuality.Flags & (uint)CelestialLightReadabilityFlags.BlackCrushGuard), Is.Not.EqualTo(0u));
            Assert.That(lowQuality.BlackCrushFloor01, Is.GreaterThanOrEqualTo(0.055f));
            Assert.That(lowQuality.UnderwaterVisibilityMeters, Is.GreaterThanOrEqualTo(1f));
            Assert.That(lowQuality.UnderwaterVisibilityMeters, Is.LessThan(highQuality.UnderwaterVisibilityMeters));
            Assert.That(lowQuality.FogDensityMultiplier, Is.GreaterThan(highQuality.FogDensityMultiplier));
        }

        [Test]
        public void WaterAndCausticsUseLightBridgeWithoutChangingNativeLayouts()
        {
            CelestialRuntimeSnapshot celestial = BuildCelestial(new float3(0f, 1f, 0f), eclipse01: 0f);
            CelestialLightReadabilitySnapshot shallow = CelestialLightReadabilityUtility.Evaluate(
                in celestial,
                25f,
                0.5f,
                44f,
                1f,
                1f,
                new float3(1f, 0.95f, 0.9f),
                1f,
                1u);
            CelestialLightReadabilitySnapshot abyss = CelestialLightReadabilityUtility.Evaluate(
                in celestial,
                2500f,
                0.5f,
                44f,
                1f,
                1f,
                new float3(1f, 0.95f, 0.9f),
                1f,
                2u);

            float4 baseline = new float4(0.09f, 0.42f, 0.70f, 0.85f);
            float4 shallowLight = CelestialLightReadabilityUtility.ModulateWaterDirectionalLight(baseline, in shallow);
            float4 abyssLight = CelestialLightReadabilityUtility.ModulateWaterDirectionalLight(baseline, in abyss);

            Assert.That(shallowLight.w, Is.GreaterThan(abyssLight.w));
            Assert.That(abyssLight.w, Is.GreaterThan(0.04f));
            Assert.That(CelestialLightReadabilityUtility.ResolveCausticsIntensityMultiplier(in shallow), Is.GreaterThan(0.2f));
            Assert.That(CelestialLightReadabilityUtility.ResolveCausticsIntensityMultiplier(in abyss), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(CelestialLightReadabilityUtility.ResolveCausticsMaxDepthMeters(in abyss, 72f), Is.LessThan(72f));
        }

        [Test]
        public void PhoticTerrainShaderConsumesLightReadabilityWithoutBakedOverbrightFill()
        {
            string root = Directory.GetCurrentDirectory();
            string terrain = File.ReadAllText(Path.Combine(root, "Assets/_Project/Art/Shaders/H8_PhoticTerrainLit_1453.shader"));

            StringAssert.Contains("float4 _HectonCelestialLightReadability0", terrain);
            StringAssert.Contains("float4 _HectonCelestialLightReadability1", terrain);
            StringAssert.Contains("float4 _HectonCelestialLightReadability2", terrain);
            StringAssert.Contains("float4 _HectonCelestialLightReadability3", terrain);
            StringAssert.Contains("half lightSignal = (half)(", terrain);
            StringAssert.Contains("half causticGate = lerp(1.0h, (half)saturate(_HectonCelestialLightReadability2.x), lightKnown);", terrain);
            StringAssert.Contains("half playableFloor = lerp(0.11h, max(0.055h, (half)_HectonCelestialLightReadability3.w), lightKnown);", terrain);
            StringAssert.Contains("half runtimeFill = lerp(", terrain);
            StringAssert.Contains("ambientReadability + directSun * 0.24h + artificialLight * 0.08h", terrain);
            StringAssert.Contains("saturate(runtimeFill * (0.54h + top * 0.46h))", terrain);
            StringAssert.Contains("_CausticStrength * causticGate", terrain);
            StringAssert.Contains("col = min(col, half3(1.0h, 1.0h, 1.0h));", terrain);
            StringAssert.DoesNotContain("half waterFill = _FillLight *", terrain);
        }

        [Test]
        public void PhoticWaterVolumeShaderConsumesLightReadabilityForVisibilityAndFog()
        {
            string root = Directory.GetCurrentDirectory();
            string waterVolume = File.ReadAllText(Path.Combine(root, "Assets/_Project/Art/Shaders/H8_PhoticWaterVolume_1429.shader"));

            StringAssert.Contains("float4 _HectonCelestialLightReadability0", waterVolume);
            StringAssert.Contains("float4 _HectonCelestialLightReadability1", waterVolume);
            StringAssert.Contains("float4 _HectonCelestialLightReadability2", waterVolume);
            StringAssert.Contains("float4 _HectonCelestialLightReadability3", waterVolume);
            StringAssert.Contains("half visibility01 = (half)saturate(_HectonCelestialLightReadability0.w / 112.0);", waterVolume);
            StringAssert.Contains("half deepDarkness = (half)saturate(_HectonCelestialLightReadability1.y);", waterVolume);
            StringAssert.Contains("half fogPressure = (half)saturate((_HectonCelestialLightReadability2.y - 0.72) * 0.24271844);", waterVolume);
            StringAssert.Contains("half hazePressure = saturate(max(fogPressure, deepDarkness * 0.74h) * (1.0h - visibility01 * 0.28h));", waterVolume);
            StringAssert.Contains("half alpha = min(0.82h, saturate(color.a * _Alpha * input.color.a * noise * alphaScale));", waterVolume);
        }

        [Test]
        public void RuntimeConsumersBindThroughCelestialLightReadModel()
        {
            string root = Directory.GetCurrentDirectory();
            string celestial = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/HectonCelestialEngine.cs"));
            string seismic = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs"));
            string water = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/Rendering/WaterOptics/WaterOpticsRuntime.cs"));
            string waterTuner = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/Rendering/WaterOptics/Editor/AbyssalOpticsTunerWindow.cs"));
            string waterValidator = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/Rendering/WaterOptics/Editor/WaterOpticsLayoutValidator.cs"));
            string playerFlashlight = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/PlayerFlashlight.cs"));
            string modularEquipment = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/ModularEquipmentEngine.cs"));
            string caustics = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/Rendering/AbyssalCaustics/AbyssalDeferredCausticsRuntime.cs"));
            string biolum = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs"));
            string globalWeather = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/Environment/GlobalWeatherDirector.cs"));
            string surfaceWeather = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs"));
            string audio = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs"));
            string audioTuner = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/Audio/Editor/AdaptiveAudioTunerWindow.cs"));
            string shadows = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/Graphics/Culling/AbyssalShadowCullingRuntime.cs"));
            string arWaypoints = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/UI/ARWaypointOverlay.cs"));
            string suitAdvisory = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/UI/SuitAdvisoryController.cs"));
            string worldReadability = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/World/WorldReadabilityDirector.cs"));
            string orbitalRelativity = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs"));

            StringAssert.Contains("PublishCelestialLightReadabilitySnapshot", celestial);
            StringAssert.Contains("TryClaimCelestialRuntimeAuthority", celestial);
            StringAssert.Contains("DisableDuplicateCelestialPresentation", celestial);
            StringAssert.Contains("Duplicate runtime owner disabled; keeping the existing celestial source of truth.", celestial);
            StringAssert.Contains("aegirRenderer.enabled = false;", celestial);
            StringAssert.Contains("enabled = false;", celestial);
            StringAssert.Contains("PublishCelestialRuntimeSnapshot(!usingPublishedCelestialSnapshot)", celestial);
            StringAssert.Contains("PublishCelestialLightReadabilitySnapshot(_currentDepthMeters)", celestial);
            StringAssert.Contains("if (ShouldCullCelestialForAbyss(out float abyssDepthMeters))", celestial);
            StringAssert.Contains("PublishCelestialLightReadabilitySnapshot(abyssDepthMeters);", celestial);
            StringAssert.Contains("ClearCelestialRuntimeSnapshot();", celestial);
            StringAssert.Contains("GlobalRegistry.PublishCelestialLightReadabilitySnapshot(in emptyLightSnapshot)", celestial);
            StringAssert.Contains("Shader.SetGlobalVector(_ID_HectonCelestialLightReadability0, Vector4.zero)", celestial);
            StringAssert.Contains("Shader.SetGlobalVector(_ID_HectonCelestialLightReadability3, Vector4.zero)", celestial);
            StringAssert.Contains("lightingStateFallback = _surfaceAtmosphericLightingState.IsValid == 0", celestial);
            StringAssert.Contains("lightingStateFallback);", celestial);
            StringAssert.Contains("QualitySettings.GetQualityLevel", celestial);
            StringAssert.Contains("ResolveUnityQualityTierWeight01", celestial);
            StringAssert.Contains("float quality = ResolveUnityQualityTierWeight01();", celestial);
            StringAssert.Contains("DynamicResolutionScaler scaler = _cachedDynamicResolution;", celestial);
            StringAssert.Contains("quality = math.min(quality, math.saturate(scaler.CurrentRenderScale));", celestial);
            StringAssert.Contains("ResolveCelestialQualityFromUnityTier", celestial);
            StringAssert.Contains("case 1: return 0.55f", celestial);
            StringAssert.Contains("case 6: return 1.00f", celestial);
            StringAssert.Contains("private ICelestialRuntimeSnapshotReadModel _celestialSnapshotReadModel;", seismic);
            StringAssert.Contains("ReadPublishedCelestialSnapshot()", seismic);
            StringAssert.Contains("IsCelestialSnapshotReadable(in published)", seismic);
            StringAssert.DoesNotContain("GlobalRegistry.PublishCelestialRuntimeSnapshot(in celestial)", seismic);
            StringAssert.Contains("ICelestialLightReadabilityReadModel", water);
            StringAssert.Contains("public const uint TelemetryFlagCelestialLightMissing = 1u << 8", water);
            StringAssert.Contains("public const uint TelemetryFlagCelestialLightFallback = 1u << 9", water);
            StringAssert.Contains("public const uint TelemetryFlagCelestialLightArtificialCritical = 1u << 10", water);
            StringAssert.Contains("public const uint TelemetryFlagCelestialLightQualityReduced = 1u << 11", water);
            StringAssert.Contains("public const uint TelemetryFlagCelestialLightTwilight = 1u << 12", water);
            StringAssert.Contains("public const uint TelemetryFlagCelestialLightNight = 1u << 13", water);
            StringAssert.Contains("CacheCelestialLightReadModel(currentService as ICelestialLightReadabilityReadModel)", water);
            StringAssert.Contains("CacheCelestialLightReadModel(GlobalRegistry.CelestialLightReadabilityReadModel)", water);
            StringAssert.Contains("IsCelestialLightReadModelUsable", water);
            StringAssert.Contains("readModel is Behaviour behaviour", water);
            StringAssert.Contains("ModulateWaterDirectionalLight", water);
            StringAssert.Contains("ApplyCelestialLightVisibilityLimits", water);
            StringAssert.Contains("ApplyCelestialAbsorptionCoefficients", water);
            StringAssert.Contains("ApplyCelestialScatteringCoefficients", water);
            StringAssert.Contains("light.UnderwaterVisibilityMeters", water);
            StringAssert.Contains("light.AbsorptionMultiplier", water);
            StringAssert.Contains("light.ScatteringMultiplier", water);
            StringAssert.Contains("lightQuality = math.saturate(math.select(light.Quality01, 1f", water);
            StringAssert.Contains("deepDarkness = math.saturate(math.select(light.DeepDarkness01, 0f", water);
            StringAssert.Contains("travelCompression = math.lerp(8f, 24f", water);
            StringAssert.Contains("math.max(128f, visibility * travelCompression)", water);
            StringAssert.Contains("MaxReadableWaterLightColor = 1f", water);
            StringAssert.Contains("MaxReadableWaterLightIntensity = 1.25f", water);
            StringAssert.Contains("_telemetryStatus", waterTuner);
            StringAssert.Contains("WaterOpticsRuntime.TelemetryFlagCelestialLightMissing", waterTuner);
            StringAssert.Contains("WaterOpticsRuntime.TelemetryFlagCelestialLightFallback", waterTuner);
            StringAssert.Contains("WaterOpticsRuntime.TelemetryFlagCelestialLightArtificialCritical", waterTuner);
            StringAssert.Contains("WaterOpticsRuntime.TelemetryFlagCelestialLightQualityReduced", waterTuner);
            StringAssert.Contains("WaterOpticsRuntime.TelemetryFlagCelestialLightTwilight", waterTuner);
            StringAssert.Contains("WaterOpticsRuntime.TelemetryFlagCelestialLightNight", waterTuner);
            StringAssert.Contains("Celestial light bridge: missing", waterTuner);
            StringAssert.Contains("Celestial light bridge: artificial critical", waterTuner);
            StringAssert.Contains("Celestial light bridge: fallback", waterTuner);
            StringAssert.Contains("Celestial light bridge: quality reduced", waterTuner);
            StringAssert.Contains("Celestial light bridge: night phase", waterTuner);
            StringAssert.Contains("Celestial light bridge: twilight phase", waterTuner);
            StringAssert.Contains("Celestial light bridge: bound", waterTuner);
            StringAssert.Contains("HasCelestialReadabilityBridge", waterValidator);
            StringAssert.Contains("Celestial readability bridge", waterValidator);
            StringAssert.Contains("CelestialLightReadabilityUtilityPath", waterValidator);
            StringAssert.Contains("MaxReadableSunSourceIntensity = 1.25f", waterValidator);
            StringAssert.Contains("AbyssalOpticsTunerPath", waterValidator);
            StringAssert.Contains("WaterOpticsRuntime.TelemetryFlagCelestialLightMissing", waterValidator);
            StringAssert.Contains("WaterOpticsRuntime.TelemetryFlagCelestialLightArtificialCritical", waterValidator);
            StringAssert.Contains("WaterOpticsRuntime.TelemetryFlagCelestialLightQualityReduced", waterValidator);
            StringAssert.Contains("WaterOpticsRuntime.TelemetryFlagCelestialLightTwilight", waterValidator);
            StringAssert.Contains("WaterOpticsRuntime.TelemetryFlagCelestialLightNight", waterValidator);
            StringAssert.Contains("ICelestialLightReadabilityReadModel", playerFlashlight);
            StringAssert.Contains("GlobalRegistryServiceSlot.CelestialEngineRuntime", playerFlashlight);
            StringAssert.Contains("ResolveCelestialArtificialLightPressure01", playerFlashlight);
            StringAssert.Contains("ResolveCelestialBeamIntensityMultiplier", playerFlashlight);
            StringAssert.Contains("ResolveCelestialBeamRangeMultiplier", playerFlashlight);
            StringAssert.Contains("CelestialBeamMaxIntensityMultiplier = 1.08f", playerFlashlight);
            StringAssert.Contains("CelestialBeamMaxRangeMultiplier = 1.16f", playerFlashlight);
            StringAssert.Contains("CelestialBeamMaxShaftMultiplier = 1.20f", playerFlashlight);
            StringAssert.Contains("CelestialLightReadabilityFlags.ArtificialLightCritical", playerFlashlight);
            StringAssert.Contains("readModel is Behaviour behaviour", playerFlashlight);
            StringAssert.Contains("IsCelestialLightReadModelUsable(fallback) ? fallback : null", playerFlashlight);
            StringAssert.DoesNotContain("_celestialLightReadModel = IsCelestialLightReadModelUsable(readModel)", playerFlashlight);
            StringAssert.Contains("flashlight.PresentationRange", modularEquipment);
            StringAssert.Contains("flashlight.PresentationIntensity", modularEquipment);
            StringAssert.Contains("FlagCelestialLightBound", caustics);
            StringAssert.Contains("ResolveCausticsIntensityMultiplier", caustics);
            StringAssert.Contains("IsCelestialLightReadModelUsable", caustics);
            StringAssert.Contains("readModel is Behaviour behaviour", caustics);
            StringAssert.Contains("IsCelestialLightReadModelUsable(fallback) ? fallback : null", caustics);
            StringAssert.DoesNotContain("_celestialLightReadModel = GlobalRegistry.CelestialLightReadabilityReadModel;", caustics);
            StringAssert.Contains("_cachedCelestialLight", biolum);
            StringAssert.Contains("ResolveCelestialRuntimeSnapshotReadModel", biolum);
            StringAssert.Contains("ResolveCelestialLightReadModel", biolum);
            StringAssert.Contains("CacheCelestialRuntimeSnapshotReadModel(currentService as ICelestialRuntimeSnapshotReadModel)", biolum);
            StringAssert.Contains("CacheCelestialLightReadModel(currentService as ICelestialLightReadabilityReadModel)", biolum);
            StringAssert.Contains("IsCelestialRuntimeSnapshotReadModelUsable", biolum);
            StringAssert.Contains("IsCelestialLightReadModelUsable", biolum);
            StringAssert.Contains("readModel is Behaviour behaviour", biolum);
            StringAssert.Contains("IsCelestialLightReadModelUsable(fallback) ? fallback : null", biolum);
            StringAssert.Contains("light.BiolumWeight01", biolum);
            StringAssert.Contains("CachePlayerContext(GlobalRegistry.Player)", biolum);
            StringAssert.Contains("CachePlayerContext(currentService as IPlayerRuntimeContext)", biolum);
            StringAssert.Contains("ResolvePlayerContext()", biolum);
            StringAssert.Contains("IsPlayerContextUsable", biolum);
            StringAssert.Contains("InvalidateCachedCameraReference()", biolum);
            StringAssert.Contains("IsTransformUsable(_cachedCameraTransform)", biolum);
            StringAssert.DoesNotContain("_cachedPlayerContext = currentService as IPlayerRuntimeContext;", biolum);
            StringAssert.DoesNotContain("IPlayerRuntimeContext playerContext = _cachedPlayerContext;", biolum);
            StringAssert.Contains("ICelestialRuntimeSnapshotReadModel", globalWeather);
            StringAssert.Contains("CacheCelestialRuntimeSnapshotReadModel(currentService as ICelestialRuntimeSnapshotReadModel)", globalWeather);
            StringAssert.Contains("IsCelestialRuntimeSnapshotReadModelUsable", globalWeather);
            StringAssert.Contains("CachePlayerContext(currentService as IPlayerRuntimeContext)", globalWeather);
            StringAssert.Contains("CachePlayerContext(GlobalRegistry.Player)", globalWeather);
            StringAssert.Contains("IPlayerRuntimeContext playerContext = ResolvePlayerContext();", globalWeather);
            StringAssert.Contains("private static bool IsPlayerContextUsable(IPlayerRuntimeContext playerContext)", globalWeather);
            StringAssert.Contains("playerContext is Behaviour behaviour", globalWeather);
            StringAssert.Contains("readModel is Behaviour behaviour", globalWeather);
            StringAssert.DoesNotContain("_cachedPlayerContext = currentService as IPlayerRuntimeContext;", globalWeather);
            StringAssert.DoesNotContain("IPlayerRuntimeContext playerContext = _cachedPlayerContext;", globalWeather);
            StringAssert.Contains("CacheCelestialEngine(currentService as HectonCelestialEngine)", surfaceWeather);
            StringAssert.Contains("ResolveCachedCelestialEngine", surfaceWeather);
            StringAssert.Contains("IsCelestialEngineUsable", surfaceWeather);
            StringAssert.Contains("engine != null && (!Application.isPlaying || engine.isActiveAndEnabled)", surfaceWeather);
            StringAssert.Contains("ResolveCelestialLightReadability", audio);
            StringAssert.Contains("light.DeepDarkness01", audio);
            StringAssert.Contains("light.ArtificialLightWeight01", audio);
            StringAssert.Contains("GlobalRegistryServiceSlot.CelestialEngineRuntime", audio);
            StringAssert.Contains("IsCelestialLightReadModelUsable", audio);
            StringAssert.Contains("readModel is Behaviour behaviour", audio);
            StringAssert.Contains("IsCelestialLightReadModelUsable(fallback) ? fallback : null", audio);
            StringAssert.DoesNotContain("_celestialLightReadModel = GlobalRegistry.CelestialLightReadabilityReadModel;", audio);
            StringAssert.Contains("BuildCelestialLightTelemetryFlags", audio);
            StringAssert.Contains("TelemetryFlagCelestialLightMissing", audio);
            StringAssert.Contains("TelemetryFlagCelestialLightFallback", audio);
            StringAssert.Contains("TelemetryFlagCelestialLightAbyssCritical", audio);
            StringAssert.Contains("TelemetryFlagCelestialLightQualityReduced", audio);
            StringAssert.Contains("TelemetryFlagCelestialLightTwilight", audio);
            StringAssert.Contains("TelemetryFlagCelestialLightNight", audio);
            StringAssert.Contains("CelestialLightReadabilityFlags.LightPhaseNight", audio);
            StringAssert.Contains("CelestialLightReadabilityFlags.LightPhaseTwilight", audio);
            StringAssert.Contains("DrawCelestialLightTelemetry", audioTuner);
            StringAssert.Contains("TelemetryFlagCelestialLightMissing", audioTuner);
            StringAssert.Contains("TelemetryFlagCelestialLightQualityReduced", audioTuner);
            StringAssert.Contains("TelemetryFlagCelestialLightTwilight", audioTuner);
            StringAssert.Contains("TelemetryFlagCelestialLightNight", audioTuner);
            StringAssert.Contains("Celestial light bridge", audioTuner);
            StringAssert.Contains("LightReadabilitySnapshot", shadows);
            StringAssert.Contains("GlobalRegistryServiceSlot.CelestialEngineRuntime", shadows);
            StringAssert.Contains("IsCelestialLightReadModelUsable", shadows);
            StringAssert.Contains("readModel is Behaviour behaviour", shadows);
            StringAssert.Contains("IsCelestialLightReadModelUsable(fallback) ? fallback : null", shadows);
            StringAssert.DoesNotContain("_celestialLightReadModel = GlobalRegistry.CelestialLightReadabilityReadModel;", shadows);
            StringAssert.Contains("ICelestialLightReadabilityReadModel", arWaypoints);
            StringAssert.Contains("ResolveWaypointLightAlphaMultiplier", arWaypoints);
            StringAssert.Contains("GlobalRegistryServiceSlot.CelestialEngineRuntime", arWaypoints);
            StringAssert.Contains("IsCelestialLightReadModelUsable", arWaypoints);
            StringAssert.Contains("readModel is Behaviour behaviour", arWaypoints);
            StringAssert.Contains("IsCelestialLightReadModelUsable(fallback) ? fallback : null", arWaypoints);
            StringAssert.Contains("ResolveLightPhaseWaypointLift", arWaypoints);
            StringAssert.Contains("CelestialLightReadabilityFlags.LightPhaseNight", arWaypoints);
            StringAssert.Contains("CelestialLightReadabilityFlags.LightPhaseTwilight", arWaypoints);
            StringAssert.DoesNotContain("_celestialLightReadModel = GlobalRegistry.CelestialLightReadabilityReadModel;", arWaypoints);
            StringAssert.Contains("ICelestialLightReadabilityReadModel", suitAdvisory);
            StringAssert.Contains("EvaluateCelestialVisibilityAdvisory", suitAdvisory);
            StringAssert.Contains("ResolveCelestialVisibilityAdvisory01", suitAdvisory);
            StringAssert.Contains("light.Sequence == _lastCelestialLightSequence", suitAdvisory);
            StringAssert.Contains("GlobalRegistryServiceSlot.CelestialEngineRuntime", suitAdvisory);
            StringAssert.Contains("ResetCelestialVisibilityAdvisoryState();", suitAdvisory);
            StringAssert.Contains("IsCelestialLightReadModelUsable", suitAdvisory);
            StringAssert.Contains("readModel is Behaviour behaviour", suitAdvisory);
            StringAssert.Contains("IsCelestialLightReadModelUsable(fallback) ? fallback : null", suitAdvisory);
            StringAssert.DoesNotContain("_cachedCelestialLightReadModel = GlobalRegistry.CelestialLightReadabilityReadModel;", suitAdvisory);
            StringAssert.Contains("NotifyCritical(in _advisoryMessageBuffer)", suitAdvisory);
            StringAssert.Contains("AppendCelestialLightPhase", suitAdvisory);
            StringAssert.Contains("CelestialLightReadabilityFlags.LightPhaseNight", suitAdvisory);
            StringAssert.Contains("CelestialLightReadabilityFlags.LightPhaseTwilight", suitAdvisory);
            StringAssert.Contains("PublishCelestialVisibilityTelemetry", suitAdvisory);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, math.saturate(visibility01));", suitAdvisory);
            StringAssert.Contains("catch (Exception telemetryException)", suitAdvisory);
            StringAssert.Contains("LogCelestialVisibilityTelemetryException(telemetryException);", suitAdvisory);
            StringAssert.Contains("[System.Diagnostics.Conditional(\"DEVELOPMENT_BUILD\")]", suitAdvisory);
            StringAssert.Contains("SuitAdvisory.CelestialVisibility.Fallback", suitAdvisory);
            StringAssert.Contains("SuitAdvisory.CelestialVisibility.Artificial", suitAdvisory);
            StringAssert.Contains("ICelestialLightReadabilityReadModel", worldReadability);
            StringAssert.Contains("GlobalRegistryServiceSlot.CelestialEngineRuntime", worldReadability);
            StringAssert.Contains("ResetCelestialLightGuidanceState();", worldReadability);
            StringAssert.Contains("IsCelestialLightReadModelUsable", worldReadability);
            StringAssert.Contains("readModel is Behaviour behaviour", worldReadability);
            StringAssert.Contains("IsCelestialLightReadModelUsable(fallback) ? fallback : null", worldReadability);
            StringAssert.Contains("ResolveCelestialLightReadability(resetGuidanceOnMissing: true)", worldReadability);
            StringAssert.Contains("ResolveCelestialLightReadabilityForDiagnostics", worldReadability);
            StringAssert.Contains("ResolveCelestialLightReadability(resetGuidanceOnMissing: false)", worldReadability);
            StringAssert.DoesNotContain("_cachedCelestialLightReadModel = GlobalRegistry.CelestialLightReadabilityReadModel;", worldReadability);
            StringAssert.Contains("case GlobalRegistryServiceSlot.Dispatcher:", worldReadability);
            StringAssert.Contains("TryUnregister();", worldReadability);
            StringAssert.Contains("TryRegister();", worldReadability);
            StringAssert.Contains("ResolveCelestialLightGuidanceMask", worldReadability);
            StringAssert.Contains("TryQueueCelestialLightGuidance", worldReadability);
            StringAssert.Contains("ResolveCelestialLightGuidanceMessage", worldReadability);
            StringAssert.Contains("WorldReadabilityDirector.Notification.CelestialLight", worldReadability);
            StringAssert.Contains("private uint _pendingNotificationContextHash;", worldReadability);
            StringAssert.Contains("QueueOrPublish(message, severity, _CelestialLightNotificationContextHash);", worldReadability);
            StringAssert.Contains("ReportReadabilityNotificationMiss(severity, _pendingNotificationContextHash);", worldReadability);
            StringAssert.Contains("PublishReadabilityNotificationMissTelemetry(contextHash, severity, _notificationMissCount);", worldReadability);
            StringAssert.Contains("catch (Exception telemetryException)", worldReadability);
            StringAssert.Contains("LogReadabilityNotificationTelemetryException(telemetryException);", worldReadability);
            StringAssert.Contains("CelestialLightDepthStratum.Deep500To2000Meters", worldReadability);
            StringAssert.Contains("CelestialLightReadabilityFlags.ArtificialLightCritical", worldReadability);
            StringAssert.Contains("CelestialLightReadabilityFlags.LightPhaseTwilight", worldReadability);
            StringAssert.Contains("CelestialLightReadabilityFlags.LightPhaseNight", worldReadability);
            StringAssert.Contains("Optics are unstable. Trust instrument depth, sonar, and beacon routes.", worldReadability);
            StringAssert.Contains("ResolveCelestialRuntimeSnapshotReadModel", orbitalRelativity);
            StringAssert.Contains("CacheCelestialRuntimeSnapshotReadModel(currentService as ICelestialRuntimeSnapshotReadModel)", orbitalRelativity);
            StringAssert.Contains("CacheCelestialRuntimeSnapshotReadModel(GlobalRegistry.CelestialRuntimeSnapshotReadModel)", orbitalRelativity);
            StringAssert.Contains("IsCelestialRuntimeSnapshotReadModelUsable", orbitalRelativity);
            StringAssert.Contains("readModel is Behaviour behaviour", orbitalRelativity);
            StringAssert.Contains("ICelestialRuntimeSnapshotReadModel readModel = ResolveCelestialRuntimeSnapshotReadModel();", orbitalRelativity);
            StringAssert.Contains("IsCelestialRuntimeSnapshotReadModelUsable(fallback) ? fallback : null", orbitalRelativity);
            StringAssert.Contains("TryReadPublishedCelestialSnapshot(", orbitalRelativity);
            StringAssert.Contains("IsCelestialSnapshotReadable(in snapshot)", orbitalRelativity);
            StringAssert.Contains("ReportCelestialSnapshotFallbackIfNeeded(failure)", orbitalRelativity);
            StringAssert.Contains("CelestialSnapshotReadFailure.MissingService", orbitalRelativity);
            StringAssert.Contains("CelestialSnapshotReadFailure.InvalidSnapshot", orbitalRelativity);
            StringAssert.Contains("ShouldReportCelestialSnapshotFallback", orbitalRelativity);
            StringAssert.Contains("ResolveCelestialSnapshotFallbackSeverity", orbitalRelativity);
            StringAssert.Contains("CelestialSnapshotFallbackAnomalyCooldownFrames", orbitalRelativity);
            StringAssert.DoesNotContain("_celestialSnapshotReadModel = GlobalRegistry.CelestialRuntimeSnapshotReadModel;", orbitalRelativity);
        }

        [Test]
        public void AtmosphereManagerLeavesAegirDirectionOwnedByCelestialRuntime()
        {
            string root = Directory.GetCurrentDirectory();
            string atmosphere = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/HectonAtmosphereManager.cs"));

            StringAssert.Contains("private bool ShouldPublishAegirDirectionFallback()", atmosphere);
            StringAssert.Contains("private void CacheCelestialEngine(HectonCelestialEngine engine)", atmosphere);
            StringAssert.Contains("private HectonCelestialEngine ResolveCachedCelestialEngine()", atmosphere);
            StringAssert.Contains("private static bool IsCelestialEngineUsable(HectonCelestialEngine engine)", atmosphere);
            StringAssert.Contains("private IPlayerRuntimeContext ResolvePlayerRuntimeContext()", atmosphere);
            StringAssert.Contains("private void CachePlayerContext(IPlayerRuntimeContext playerContext)", atmosphere);
            StringAssert.Contains("private static bool IsPlayerContextUsable(IPlayerRuntimeContext playerContext)", atmosphere);
            StringAssert.Contains("CachePlayerContext(GlobalRegistry.Player)", atmosphere);
            StringAssert.Contains("CachePlayerContext(currentService as IPlayerRuntimeContext)", atmosphere);
            StringAssert.Contains("ClearPlayerRuntimeReferences();", atmosphere);
            StringAssert.Contains("IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();", atmosphere);
            StringAssert.Contains("playerContext is Behaviour behaviour", atmosphere);
            StringAssert.Contains("IsTransformUsable(_playerCameraTransform)", atmosphere);
            StringAssert.Contains("IsPlayerMovementUsable(contextMovement)", atmosphere);
            StringAssert.Contains("return !IsCelestialEngineUsable(_cachedCelestialEngine);", atmosphere);
            StringAssert.Contains("engine != null && (!Application.isPlaying || engine.isActiveAndEnabled)", atmosphere);
            StringAssert.Contains("case GlobalRegistryServiceSlot.CelestialEngineRuntime:", atmosphere);
            StringAssert.Contains("QueueAegirDirectionOwnerRefresh();", atmosphere);
            StringAssert.Contains("private void QueueAegirDirectionOwnerRefresh()", atmosphere);
            StringAssert.DoesNotContain("_playerRuntimeContext = currentService as IPlayerRuntimeContext;", atmosphere);
            StringAssert.DoesNotContain("_playerRuntimeContext = GlobalRegistry.Player;", atmosphere);
            StringAssert.DoesNotContain("IPlayerRuntimeContext playerContext = _playerRuntimeContext;", atmosphere);
            Assert.That(CountOccurrences(atmosphere, "Shader.SetGlobalVector(_shaderID_AegirDirection"), Is.EqualTo(2));
            Assert.That(CountOccurrences(atmosphere, "if (ShouldPublishAegirDirectionFallback())"), Is.GreaterThanOrEqualTo(2));
            AssertGuardedByAegirFallback(
                atmosphere,
                "Shader.SetGlobalVector(_shaderID_AegirDirection, new Vector4(0f, 0f, 1f, 0f))");
            AssertGuardedByAegirFallback(
                atmosphere,
                "Shader.SetGlobalVector(_shaderID_AegirDirection, new Vector4(directionPayload.x, directionPayload.y, directionPayload.z, directionPayload.w))");
        }

        [Test]
        public void AegirSkyShadersConsumeRuntimeProjectionQualityAndWaterReadability()
        {
            string root = Directory.GetCurrentDirectory();
            string celestial = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/HectonCelestialEngine.cs"));
            string orbital = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs"));
            string sky = File.ReadAllText(Path.Combine(root, "Assets/_Project/Art/Shaders/Sky/Hecton_AegirSky.shader"));
            string impostor = File.ReadAllText(Path.Combine(root, "Assets/_Project/Art/Shaders/H8_AegirGasGiantImpostor_1428.shader"));

            StringAssert.Contains("PublishAegirSkyProjectionGlobals(aegirDirection);", celestial);
            StringAssert.Contains("ClearAegirSkyProjectionGlobals();", celestial);
            StringAssert.Contains("ResolveAegirSkyProjectionQuality01", celestial);
            StringAssert.Contains("ResolveAegirSkyProjectionVisibility01", celestial);
            StringAssert.Contains("SanitizeAegirProjectionScalar", celestial);
            StringAssert.Contains("SaturateAegirProjection01", celestial);
            StringAssert.Contains("pressureQuality > 0f", celestial);
            StringAssert.Contains("(snapshot.Flags & (uint)CelestialLightReadabilityFlags.Valid) != 0u", celestial);
            StringAssert.Contains("!math.all(math.isfinite(normal))", celestial);
            StringAssert.Contains("_ID_H8AegirSunDirection,", celestial);
            StringAssert.Contains("_ID_H8AegirStormEmission", celestial);
            StringAssert.Contains("Shader.SetGlobalFloat(_ID_H8GlobalQualityWeight, quality);", celestial);
            StringAssert.Contains("Shader.SetGlobalFloat(_ID_H8AegirStormEmission, ResolveAegirSkyProjectionStormEmission());", celestial);
            StringAssert.Contains("Shader.SetGlobalFloat(_ID_H8AegirStormEmission, 1f);", celestial);
            StringAssert.Contains("ReportAegirStormEmissionInvalidIfNeeded", celestial);
            StringAssert.Contains("_AegirStormEmissionInvalidWarningHash", celestial);
            StringAssert.Contains("block.SetFloat(_ID_H8GlobalQualityWeight", celestial);
            StringAssert.Contains("block.SetFloat(_ID_StormEmission, ResolveAegirSkyProjectionStormEmission());", celestial);
            StringAssert.Contains("block.SetVector(_ID_H8AegirSunDirection", celestial);
            StringAssert.Contains("ValidateAegirRendererMaterialCold", celestial);
            StringAssert.Contains("aegirRenderer.sharedMaterial = aegirFallbackMaterial;", celestial);
            StringAssert.Contains("aegirRenderer.enabled = false;", celestial);
            StringAssert.Contains("keeping sky projection globals authoritative", celestial);
            StringAssert.Contains("renderer will use shader fallback while sky projection remains authoritative", celestial);

            StringAssert.Contains("_aegirStormEmissionId", orbital);
            StringAssert.Contains("Shader.SetGlobalFloat(_aegirStormEmissionId, 1f);", orbital);

            StringAssert.Contains("float4 _H8AegirSunDirection;", sky);
            StringAssert.Contains("float4 _H8AegirPlanetCenterRadius;", sky);
            StringAssert.Contains("float4 _H8AegirRingPlaneInner;", sky);
            StringAssert.Contains("float4 _H8AegirOrbitScalars;", sky);
            StringAssert.Contains("float _H8AegirStormEmission;", sky);
            StringAssert.Contains("float _H8GlobalQualityWeight;", sky);
            StringAssert.Contains("float AegirStormEmission()", sky);
            StringAssert.Contains("clamp(_H8AegirStormEmission, 0.0, 4.0)", sky);
            StringAssert.Contains("float quality = saturate(max(_H8GlobalQualityWeight, _H8AegirOrbitScalars.w));", sky);
            StringAssert.Contains("float systemVisibility = saturate(1.0 - _H8AegirSunDirection.w);", sky);
            StringAssert.Contains("DrawScreenSpaceAegir(color, input.positionCS, quality, flowSpeed, systemVisibility, alpha)", sky);
            StringAssert.Contains("color = lerp(color, planetColor, systemVisibility);", sky);
            StringAssert.Contains("stormBand * cloudTexture * 0.15 * stormEmission", sky);
            StringAssert.Contains("bands += float3(0.095, 0.052, 0.022) * stormSignal * stormEmission", sky);

            StringAssert.Contains("float4 _H8AegirSunDirection;", impostor);
            StringAssert.Contains("float _H8GlobalQualityWeight;", impostor);
            StringAssert.Contains("float4 _HectonCelestialLightReadability0;", impostor);
            StringAssert.Contains("half quality = (half)saturate(max(_H8GlobalQualityWeight, 0.16));", impostor);
            StringAssert.Contains("runtimeLightSq > 0.0001 ? normalize((half3)_H8AegirSunDirection.xyz) : normalize((half3)_LightDirection.xyz)", impostor);
            StringAssert.Contains("half readabilityKnown = readabilitySignal > 0.0001 ? 1.0h : 0.0h;", impostor);
            StringAssert.Contains("systemVisibility = min(systemVisibility, lerp(1.0h, waterVisibility, readabilityKnown));", impostor);
            StringAssert.Contains("color *= lerp(0.16h, 1.0h, max(systemVisibility, 0.035h));", impostor);
        }

        private static void AssertGuardedByAegirFallback(string source, string guardedWrite)
        {
            int writeIndex = source.IndexOf(guardedWrite, StringComparison.Ordinal);
            Assert.That(writeIndex, Is.GreaterThanOrEqualTo(0), guardedWrite);

            int guardIndex = source.LastIndexOf(
                "if (ShouldPublishAegirDirectionFallback())",
                writeIndex,
                StringComparison.Ordinal);
            Assert.That(guardIndex, Is.GreaterThanOrEqualTo(0), guardedWrite);
            Assert.That(writeIndex - guardIndex, Is.LessThan(512), guardedWrite);
        }

        private static void AssertPhase(CelestialLightReadabilitySnapshot snapshot, CelestialLightReadabilityFlags expectedPhase)
        {
            uint phaseMask =
                (uint)CelestialLightReadabilityFlags.LightPhaseDay |
                (uint)CelestialLightReadabilityFlags.LightPhaseTwilight |
                (uint)CelestialLightReadabilityFlags.LightPhaseNight;
            Assert.That((snapshot.Flags & (uint)expectedPhase), Is.Not.EqualTo(0u));
            Assert.That(CountSetBits(snapshot.Flags & phaseMask), Is.EqualTo(1));
        }

        private static int CountSetBits(uint value)
        {
            int count = 0;
            while (value != 0u)
            {
                value &= value - 1u;
                count++;
            }

            return count;
        }

        private static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int index = 0;
            while (true)
            {
                index = source.IndexOf(value, index, StringComparison.Ordinal);
                if (index < 0)
                    return count;

                count++;
                index += value.Length;
            }
        }

        private static CelestialRuntimeSnapshot BuildCelestial(float3 sunDirection, float eclipse01)
        {
            CelestialRuntimeSnapshot snapshot = default;
            snapshot.AbsoluteUniverseTime = 1234d;
            snapshot.SunDirection = math.normalize(sunDirection);
            snapshot.EclipseOcclusion01 = eclipse01;
            snapshot.GlobalBiolumMultiplier = 1f;
            snapshot.Flags = (uint)CelestialRuntimeFlags.Valid;
            snapshot.Sequence = 11u;
            return snapshot;
        }
    }
}
