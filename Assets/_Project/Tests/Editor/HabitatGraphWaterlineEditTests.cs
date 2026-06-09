using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class HabitatGraphWaterlineEditTests
    {
        [Test]
        public void ConstructionDepthFallbackUsesRuntimeSeaLevel()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Construction", "HabitatGraphManager.cs");
            string resolveBody = ExtractMethodBody(source, "private float ResolveRuntimeSeaLevelY()");
            string oceanSanitizerBody = ExtractMethodBody(source, "private static bool TryResolveOceanSeaLevelY(float candidateSeaLevelY, out float seaLevelY)");
            string fallbackSanitizerBody = ExtractMethodBody(source, "private static bool TryResolveSeaLevelY(float candidateSeaLevelY, out float seaLevelY)");

            StringAssert.Contains("private const float DefaultSeaLevelY = OceanSurfaceAtmosphereConstants.DefaultSeaLevel;", source);
            StringAssert.Contains("TryResolveOceanSeaLevelY(oceanKinematics.SeaLevel, out float oceanSeaLevelY)", resolveBody);
            StringAssert.Contains("TryResolveSeaLevelY(atmosphereReadModel.SeaLevelY, out float seaLevelY)", resolveBody);
            StringAssert.Contains("? seaLevelY", resolveBody);
            StringAssert.Contains(": DefaultSeaLevelY;", resolveBody);
            StringAssert.Contains("private static bool TryResolveOceanSeaLevelY(float candidateSeaLevelY, out float seaLevelY)", source);
            StringAssert.Contains("private static bool TryResolveSeaLevelY(float candidateSeaLevelY, out float seaLevelY)", source);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", oceanSanitizerBody);
            StringAssert.DoesNotContain("math.abs(candidateSeaLevelY) > 0.0001f", oceanSanitizerBody);
            StringAssert.Contains("math.abs(candidateSeaLevelY) > 0.0001f", fallbackSanitizerBody);
            StringAssert.Contains("WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", fallbackSanitizerBody);
            StringAssert.Contains("seaLevelY = DefaultSeaLevelY;", source);
            StringAssert.DoesNotContain("math.isfinite(atmosphereReadModel.SeaLevelY)", resolveBody);
            StringAssert.DoesNotContain("? atmosphereReadModel.SeaLevelY", resolveBody);
            StringAssert.DoesNotContain(": " + "0f;", resolveBody);
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
