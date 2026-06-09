using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class CrestWaterlineFallbackEditTests
    {
        [Test]
        public void Crest4KinematicsAdapter_SanitizesMissingRootAndStaleTuningSeaLevel()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Plugins",
                "Crest",
                "Crest4KinematicsAdapter.cs");
            string contracts = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Plugins",
                "Crest",
                "OceanKinematics",
                "OceanKinematicsContracts.cs");

            string normalized = NormalizeNewlines(source);

            Assert.That(source, Does.Contain("OceanKinematicsWaterlineUtility.ResolveRuntimeSeaLevelY(oceanRenderer.Root.position.y)"));
            Assert.That(source, Does.Contain("return AnalyticalGerstnerWaveConstants.DefaultSeaLevelY;"));
            Assert.That(source, Does.Contain("tuning.OceanSurfaceY = OceanKinematicsWaterlineUtility.ResolveRuntimeSeaLevelY(tuning.OceanSurfaceY);"));
            Assert.That(source, Does.Not.Contain("AnalyticalGerstnerWaveConstants.ResolveSeaLevelY(oceanRenderer.Root.position.y)"));
            Assert.That(source, Does.Not.Contain("AnalyticalGerstnerWaveConstants.ResolveSeaLevelY(tuning.OceanSurfaceY)"));
            Assert.That(contracts, Does.Contain("public static class OceanKinematicsWaterlineUtility"));
            Assert.That(contracts, Does.Contain("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY"));
            Assert.That(contracts, Does.Not.Contain("math.abs(seaLevelY) > 0.0001f"));
            Assert.That(normalized, Does.Not.Contain("return 0f;\n        }\n\n        private static float ResolveGlobalQualityWeight"));
            Assert.That(source, Does.Not.Contain("tuning.OceanSurfaceY = math.select(0f"));
        }

        [Test]
        public void CrestOceanRuntimeAdapter_DefaultsFluidApproximationToProductionSeaLevel()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Plugins",
                "Crest",
                "CrestOceanRuntimeAdapter.cs");

            Assert.That(source, Does.Contain("private float seaLevelFallback = AnalyticalGerstnerWaveConstants.DefaultSeaLevelY;"));
            Assert.That(source, Does.Contain("return OceanKinematicsWaterlineUtility.ResolveRuntimeSeaLevelY((float)oceanRootAUP.y);"));
            Assert.That(source, Does.Contain("return TryResolveSeaLevel(waterLevel, out waterLevel);"));
            Assert.That(source, Does.Contain("return OceanKinematicsWaterlineUtility.ResolveRuntimeSeaLevelY(value);"));
            Assert.That(source, Does.Contain("private static bool TryResolveSeaLevel(float value, out float seaLevel)"));
            Assert.That(source, Does.Contain("return OceanKinematicsWaterlineUtility.TryResolveRuntimeSeaLevelY(value, out seaLevel);"));
            Assert.That(source, Does.Not.Contain("math.abs(value) > 0.0001f"));
            Assert.That(source, Does.Not.Contain("[SerializeField, Min(0f)] private float seaLevelFallback"));
            Assert.That(source, Does.Not.Contain("return math.isfinite(waterLevel);"));
            Assert.That(source, Does.Not.Contain("math.select(0f, value"));
        }

        [Test]
        public void OceanKinematicsVaultRuntime_SanitizesSurfaceYBeforeTelemetryAndMacroState()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Plugins",
                "Crest",
                "OceanKinematics",
                "OceanKinematicsVaultRuntime.cs");

            Assert.That(source, Does.Contain("entry.OceanSurfaceY = OceanKinematicsWaterlineUtility.ResolveRuntimeSeaLevelY(tuning.OceanSurfaceY);"));
            Assert.That(source, Does.Contain("state.RestingWaterHeight = OceanKinematicsWaterlineUtility.ResolveRuntimeSeaLevelY(tuning.OceanSurfaceY);"));
            Assert.That(source, Does.Not.Contain("AnalyticalGerstnerWaveConstants.ResolveSeaLevelY(tuning.OceanSurfaceY)"));
            Assert.That(source, Does.Not.Contain("math.select(0f, tuning.OceanSurfaceY"));
        }

        [Test]
        public void OceanKinematicsJobs_AllowCalibratedZeroSeaLevelForAllSurfaceBranches()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Plugins",
                "Crest",
                "OceanKinematics",
                "OceanKinematicsJobs.cs");

            Assert.That(CountOccurrences(source, "OceanKinematicsWaterlineUtility.ResolveRuntimeSeaLevelY(Tuning.OceanSurfaceY)"), Is.GreaterThanOrEqualTo(5));
            Assert.That(source, Does.Not.Contain("AnalyticalGerstnerWaveConstants.ResolveSeaLevelY(Tuning.OceanSurfaceY)"));
            Assert.That(source, Does.Not.Contain("SanitizeFinite(Tuning.OceanSurfaceY, 0f)"));
        }

        [Test]
        public void OceanKinematicsDearLieReadbackRejectsNonFiniteSamplesInsteadOfCachingZeroSurface()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Plugins",
                "Crest",
                "OceanKinematics",
                "OceanKinematicsJobs.cs");

            Assert.That(source, Does.Contain("bool sampleFinite = math.all(math.isfinite(sample));"));
            Assert.That(source, Does.Contain("cached.Flags = OceanKinematicsConstants.FlagAsyncCached | OceanKinematicsConstants.FlagNonFinite;"));
            Assert.That(source, Does.Contain("continue;"));
            Assert.That(source, Does.Contain("result.WaterHeight = sample.x;"));
            Assert.That(source, Does.Not.Contain("result.WaterHeight = math.select(0f, sample.x, math.isfinite(sample.x));"));
        }

        private static string ReadProjectFile(params string[] parts)
        {
            string path = Path.Combine(parts);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return File.ReadAllText(Path.Combine(projectRoot, path));
        }

        private static string NormalizeNewlines(string value)
        {
            return value.Replace("\r\n", "\n");
        }

        private static int CountOccurrences(string source, string token)
        {
            int count = 0;
            int index = 0;
            while (true)
            {
                index = source.IndexOf(token, index, System.StringComparison.Ordinal);
                if (index < 0)
                    return count;

                count++;
                index += token.Length;
            }
        }
    }
}
