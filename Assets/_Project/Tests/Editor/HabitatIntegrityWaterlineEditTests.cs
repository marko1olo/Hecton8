using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class HabitatIntegrityWaterlineEditTests
    {
        [Test]
        public void HabitatPressureDepthUsesRuntimeSeaLevelFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "HabitatIntegrityManager.cs");
            string depthBody = ExtractMethodBody(source, "private float ResolveDepthMeters()");
            string waterlineBody = ExtractMethodBody(source, "private float ResolveWaterSurfaceLevelY()");
            string oceanSanitizerBody = ExtractMethodBody(source, "private static bool TryResolveOceanSeaLevelY(float candidateSeaLevelY, out float seaLevelY)");
            string fallbackSanitizerBody = ExtractMethodBody(source, "private static bool TryResolveSeaLevelY(float candidateSeaLevelY, out float seaLevelY)");

            StringAssert.Contains("private const float DefaultSeaLevelY = OceanSurfaceAtmosphereConstants.DefaultSeaLevel;", source);
            StringAssert.Contains("float seaLevelY = ResolveWaterSurfaceLevelY();", depthBody);
            StringAssert.Contains("if (TryResolveOceanWaterSurfaceLevel(out float oceanSeaLevelY))", waterlineBody);
            StringAssert.Contains("terrainProvider != null && TryResolveSeaLevelY(terrainProvider.WaterSurfaceLevel, out float terrainSeaLevelY)", waterlineBody);
            StringAssert.Contains("_atmosphereRuntime != null && TryResolveSeaLevelY(_atmosphereRuntime.SeaLevelY, out float atmosphereSeaLevelY)", waterlineBody);
            StringAssert.Contains("TryResolveOceanSeaLevelY(oceanKinematics.SeaLevel, out seaLevelY)", source);
            StringAssert.Contains("private static bool TryResolveOceanSeaLevelY(float candidateSeaLevelY, out float seaLevelY)", source);
            StringAssert.Contains("private static bool TryResolveSeaLevelY(float candidateSeaLevelY, out float seaLevelY)", source);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", oceanSanitizerBody);
            StringAssert.DoesNotContain("math.abs(candidateSeaLevelY) > 0.0001f", oceanSanitizerBody);
            StringAssert.Contains("math.abs(candidateSeaLevelY) > 0.0001f", fallbackSanitizerBody);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", fallbackSanitizerBody);
            StringAssert.DoesNotContain("terrainProvider != null && math.isfinite(terrainProvider.WaterSurfaceLevel)", waterlineBody);
            StringAssert.DoesNotContain("_atmosphereRuntime != null && math.isfinite(_atmosphereRuntime.SeaLevelY)", waterlineBody);
            StringAssert.DoesNotContain("float seaLevelY = " + "0f;", depthBody);
        }

        private static string ReadProjectFile(params string[] parts)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(parts));
            return File.ReadAllText(path);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
            int brace = source.IndexOf('{', start);
            Assert.That(brace, Is.GreaterThanOrEqualTo(0), signature);

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(brace, i - brace + 1);
                }
            }

            Assert.Fail("Could not extract method body for " + signature);
            return string.Empty;
        }
    }
}
