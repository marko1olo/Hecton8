using System;
using System.IO;
using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class GlobalWorldSamplerQualityEditTests
    {
        [Test]
        public void SamplingCadence_ConsumesContinuousQualityWeight()
        {
            Assert.That(GlobalWorldSampler.ResolveSamplingCadenceDivisor(0f), Is.EqualTo(GlobalWorldSampler.LowQualityCadenceDivisor));
            Assert.That(GlobalWorldSampler.ResolveSamplingCadenceDivisor(0.25f), Is.EqualTo(10));
            Assert.That(GlobalWorldSampler.ResolveSamplingCadenceDivisor(0.5f), Is.EqualTo(7));
            Assert.That(GlobalWorldSampler.ResolveSamplingCadenceDivisor(0.75f), Is.EqualTo(3));
            Assert.That(GlobalWorldSampler.ResolveSamplingCadenceDivisor(1f), Is.EqualTo(1));
        }

        [Test]
        public void SamplingCadence_SanitizesInvalidQualityAsFullQuality()
        {
            Assert.That(GlobalWorldSampler.ResolveSamplingCadenceDivisor(float.NaN), Is.EqualTo(1));
            Assert.That(GlobalWorldSampler.ResolveSamplingCadenceDivisor(float.NegativeInfinity), Is.EqualTo(1));
            Assert.That(GlobalWorldSampler.ResolveSamplingCadenceDivisor(float.PositiveInfinity), Is.EqualTo(1));
            Assert.That(GlobalWorldSampler.ResolveSamplingCadenceDivisor(-1f), Is.EqualTo(GlobalWorldSampler.LowQualityCadenceDivisor));
            Assert.That(GlobalWorldSampler.ResolveSamplingCadenceDivisor(2f), Is.EqualTo(1));
        }

        [Test]
        public void ShouldSampleOnFrame_UsesResolvedCadence()
        {
            Assert.That(GlobalWorldSampler.ShouldSampleOnFrame(0u, 0f), Is.True);
            Assert.That(GlobalWorldSampler.ShouldSampleOnFrame(1u, 0f), Is.False);
            Assert.That(GlobalWorldSampler.ShouldSampleOnFrame((uint)GlobalWorldSampler.LowQualityCadenceDivisor, 0f), Is.True);
            Assert.That(GlobalWorldSampler.ShouldSampleOnFrame(1u, 1f), Is.True);
            Assert.That(GlobalWorldSampler.ShouldSampleOnFrame(1025u, float.NaN), Is.True);
        }

        [Test]
        public void SamplingWeights_GateExpensiveAndOverkillTerrainBranchesContinuously()
        {
            Assert.That(GlobalWorldSampler.ResolveExpensiveSamplingWeight(0f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(GlobalWorldSampler.ResolveExpensiveSamplingWeight(0.3f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(GlobalWorldSampler.ResolveExpensiveSamplingWeight(0.65f), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(GlobalWorldSampler.ResolveExpensiveSamplingWeight(1f), Is.EqualTo(1f).Within(0.0001f));

            Assert.That(GlobalWorldSampler.ResolveOverkillSamplingWeight(0.5f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(GlobalWorldSampler.ResolveOverkillSamplingWeight(0.75f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(GlobalWorldSampler.ResolveOverkillSamplingWeight(0.875f), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(GlobalWorldSampler.ResolveOverkillSamplingWeight(1f), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(GlobalWorldSampler.ResolveOverkillSamplingWeight(float.NaN), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void DefaultWorldSamplerSeaLevelMatchesRuntimeWaterline()
        {
            GlobalWorldSamplerData data = GlobalWorldSampler.FromDataVaultAliases(
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                double3.zero,
                double3.zero,
                new float3(128f, 96f, 128f),
                4,
                double3.zero,
                new float3(2f, 2f, 2f),
                new int3(4, 4, 4),
                24f,
                1u,
                2u,
                3u,
                1);

            Assert.That(GlobalWorldSampler.DefaultSeaLevel, Is.EqualTo(14.02f).Within(0.0001f));
            Assert.That(data.SeaLevel, Is.EqualTo(GlobalWorldSampler.DefaultSeaLevel).Within(0.0001f));

            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "World", "GlobalWorldSampler.cs");
            string source = File.ReadAllText(path);
            StringAssert.Contains("public const float DefaultSeaLevel = 14.02f;", source);
            StringAssert.Contains("data.SeaLevel = DefaultSeaLevel;", source);
            StringAssert.Contains("data.SeaLevel = GlobalWorldSampler.DefaultSeaLevel;", source);
            StringAssert.DoesNotContain("data.SeaLevel = " + "0f;", source);
        }

        [Test]
        public void TerrainHoleSynchronizer_SyncsDelayedHoleTexture()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts", "World", "VegetationTerrainHoleSynchronizer.cs");
            string source = File.ReadAllText(path);
            int setIndex = source.IndexOf("state.TerrainData.SetHolesDelayLOD", StringComparison.Ordinal);
            int syncIndex = source.IndexOf("SyncTexture(TerrainData.HolesTextureName)", StringComparison.Ordinal);
            Assert.That(setIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(syncIndex, Is.GreaterThan(setIndex));
        }
    }
}
