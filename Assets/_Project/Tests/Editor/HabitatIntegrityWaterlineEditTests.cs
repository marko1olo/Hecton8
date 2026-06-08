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

            StringAssert.Contains("private const float DefaultSeaLevelY = OceanSurfaceAtmosphereConstants.DefaultSeaLevel;", source);
            StringAssert.Contains("float seaLevelY = DefaultSeaLevelY;", depthBody);
            StringAssert.Contains("terrainProvider != null && TryResolveSeaLevelY(terrainProvider.WaterSurfaceLevel, out float terrainSeaLevelY)", depthBody);
            StringAssert.Contains("_atmosphereRuntime != null && TryResolveSeaLevelY(_atmosphereRuntime.SeaLevelY, out float atmosphereSeaLevelY)", depthBody);
            StringAssert.Contains("private static bool TryResolveSeaLevelY(float candidateSeaLevelY, out float seaLevelY)", source);
            StringAssert.Contains("math.abs(candidateSeaLevelY) > 0.0001f", source);
            StringAssert.Contains("math.abs(candidateSeaLevelY) <= 1000f", source);
            StringAssert.DoesNotContain("terrainProvider != null && math.isfinite(terrainProvider.WaterSurfaceLevel)", depthBody);
            StringAssert.DoesNotContain("_atmosphereRuntime != null && math.isfinite(_atmosphereRuntime.SeaLevelY)", depthBody);
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
