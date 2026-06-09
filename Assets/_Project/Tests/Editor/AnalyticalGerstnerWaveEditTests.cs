using System;
using System.IO;
using Hecton8.Physics;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class AnalyticalGerstnerWaveEditTests
    {
        [Test]
        public void DefaultTuningSeaLevelMatchesRuntimeWaterline()
        {
            GerstnerWaveTuningDTO tuning = GerstnerWaveTuningDTO.Default();

            Assert.That(AnalyticalGerstnerWaveConstants.DefaultSeaLevelY, Is.EqualTo(14.02f).Within(0.0001f));
            Assert.That(tuning.SeaLevelY, Is.EqualTo(AnalyticalGerstnerWaveConstants.DefaultSeaLevelY).Within(0.0001f));
            Assert.That(AnalyticalGerstnerWaveConstants.ResolveSeaLevelY(0f), Is.EqualTo(AnalyticalGerstnerWaveConstants.DefaultSeaLevelY).Within(0.0001f));
            Assert.That(AnalyticalGerstnerWaveConstants.ResolveSeaLevelY(float.NaN), Is.EqualTo(AnalyticalGerstnerWaveConstants.DefaultSeaLevelY).Within(0.0001f));
            Assert.That(AnalyticalGerstnerWaveConstants.ResolveSeaLevelY(4900f), Is.EqualTo(AnalyticalGerstnerWaveConstants.DefaultSeaLevelY).Within(0.0001f));
            Assert.That(AnalyticalGerstnerWaveConstants.ResolveSeaLevelY(128f), Is.EqualTo(128f).Within(0.0001f));

            string contracts = ReadProjectFile("Assets", "_Project", "Scripts", "Physics", "Buoyancy", "AnalyticalGerstnerWaveContracts.cs");
            StringAssert.Contains("public const float DefaultSeaLevelY = 14.02f;", contracts);
            StringAssert.Contains("math.abs(seaLevelY) <= 1000f", contracts);
            StringAssert.Contains("value.SeaLevelY = AnalyticalGerstnerWaveConstants.DefaultSeaLevelY;", contracts);
            StringAssert.DoesNotContain("value.SeaLevelY = " + "0f;", contracts);
        }

        [Test]
        public void RuntimeNormalizesStaleSeaLevelBeforeSchedulingAndColdBootCommit()
        {
            string runtime = ReadProjectFile("Assets", "_Project", "Scripts", "Physics", "Buoyancy", "AnalyticalGerstnerWaveRuntime.cs");

            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", runtime);
            StringAssert.Contains("tuning.SeaLevelY = ResolveRuntimeSeaLevelY(tuning.SeaLevelY);", runtime);
            StringAssert.Contains("private float ResolveRuntimeSeaLevelY(float fallbackSeaLevelY)", runtime);
            StringAssert.Contains("private bool TryResolveOceanSeaLevelY(out float seaLevelY)", runtime);
            StringAssert.Contains("TrySanitizeOceanRuntimeSeaLevelY(oceanKinematics.SeaLevel, out seaLevelY)", runtime);
            StringAssert.Contains("private static bool TrySanitizeOceanRuntimeSeaLevelY(float value, out float seaLevelY)", runtime);
            StringAssert.Contains("AnalyticalGerstnerWaveConstants.ResolveSeaLevelY(fallbackSeaLevelY)", runtime);
            StringAssert.Contains("serviceSlot == GlobalRegistryServiceSlot.OceanKinematics", runtime);
            StringAssert.Contains("_oceanKinematicsService = null;", runtime);
            StringAssert.Contains("float resolvedSeaLevelY = AnalyticalGerstnerWaveConstants.ResolveSeaLevelY(tuningDto.SeaLevelY);", runtime);
            StringAssert.Contains("tuningDto.SeaLevelY = resolvedSeaLevelY;", runtime);
            StringAssert.Contains("tuning[0] = tuningDto;", runtime);
        }

        private static string ReadProjectFile(params string[] parts)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(parts));
            return File.ReadAllText(path);
        }
    }
}
