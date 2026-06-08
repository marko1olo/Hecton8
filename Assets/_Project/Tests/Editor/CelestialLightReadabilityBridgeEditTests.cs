using System.IO;
using Hecton8.Core;
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
        public void RuntimeConsumersBindThroughCelestialLightReadModel()
        {
            string root = Directory.GetCurrentDirectory();
            string water = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/Rendering/WaterOptics/WaterOpticsRuntime.cs"));
            string caustics = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/Rendering/AbyssalCaustics/AbyssalDeferredCausticsRuntime.cs"));
            string biolum = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs"));
            string audio = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs"));
            string shadows = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/Graphics/Culling/AbyssalShadowCullingRuntime.cs"));
            string arWaypoints = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/UI/ARWaypointOverlay.cs"));

            StringAssert.Contains("ICelestialLightReadabilityReadModel", water);
            StringAssert.Contains("TelemetryFlagCelestialLightMissing", water);
            StringAssert.Contains("ModulateWaterDirectionalLight", water);
            StringAssert.Contains("FlagCelestialLightBound", caustics);
            StringAssert.Contains("ResolveCausticsIntensityMultiplier", caustics);
            StringAssert.Contains("_cachedCelestialLight", biolum);
            StringAssert.Contains("light.BiolumWeight01", biolum);
            StringAssert.Contains("ResolveCelestialLightReadability", audio);
            StringAssert.Contains("light.DeepDarkness01", audio);
            StringAssert.Contains("LightReadabilitySnapshot", shadows);
            StringAssert.Contains("GlobalRegistryServiceSlot.CelestialEngineRuntime", shadows);
            StringAssert.Contains("ICelestialLightReadabilityReadModel", arWaypoints);
            StringAssert.Contains("ResolveWaypointLightAlphaMultiplier", arWaypoints);
            StringAssert.Contains("GlobalRegistryServiceSlot.CelestialEngineRuntime", arWaypoints);
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
