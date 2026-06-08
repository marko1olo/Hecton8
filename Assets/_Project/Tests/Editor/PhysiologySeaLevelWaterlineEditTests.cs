using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class PhysiologySeaLevelWaterlineEditTests
    {
        [Test]
        public void SuitIntegrityRuntimeSanitizesSerializedSeaLevelBeforePressureJobs()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Physiology", "ShinobuSuitIntegrityRuntime.cs");
            string slowTickBody = ExtractMethodBody(source, "public void SlowTick()");
            string mockBody = ExtractMethodBody(source, "public bool GenerateMockHydrostaticPressureData()");

            StringAssert.Contains("private const double DefaultSeaLevelAupY = 14.02d;", source);
            StringAssert.Contains("[SerializeField] private double seaLevelAupY = DefaultSeaLevelAupY;", source);
            StringAssert.Contains("double resolvedSeaLevelAupY = ResolveSeaLevelAupY(seaLevelAupY);", slowTickBody);
            StringAssert.Contains("new double3(playerDouble.x, resolvedSeaLevelAupY, playerDouble.z)", slowTickBody);
            StringAssert.Contains("new double3(0d, ResolveSeaLevelAupY(seaLevelAupY), 0d)", mockBody);
            StringAssert.Contains("private static double ResolveSeaLevelAupY(double candidateSeaLevelAupY)", source);
            StringAssert.Contains("math.abs(candidateSeaLevelAupY) <= 1000d", source);
            StringAssert.DoesNotContain("new double3(playerDouble.x, seaLevelAupY, playerDouble.z)", slowTickBody);
        }

        [Test]
        public void PhysiologyRuntimeSanitizesSerializedSeaLevelBeforeEnvironmentDepth()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Physiology", "ShinobuPhysiologyRuntime.cs");
            string environmentBody = ExtractMethodBody(source, "private void WriteEnvironmentSeed(");

            StringAssert.Contains("private const double DefaultSeaLevelAupY = 14.02d;", source);
            StringAssert.Contains("[SerializeField] private double seaLevelAupY = DefaultSeaLevelAupY;", source);
            StringAssert.Contains("double resolvedSeaLevelAupY = ResolveSeaLevelAupY(seaLevelAupY);", environmentBody);
            StringAssert.Contains("seaLevelAup.y = resolvedSeaLevelAupY;", environmentBody);
            StringAssert.Contains("private static double ResolveSeaLevelAupY(double candidateSeaLevelAupY)", source);
            StringAssert.Contains("math.abs(candidateSeaLevelAupY) <= 1000d", source);
            StringAssert.DoesNotContain("seaLevelAup.y = seaLevelAupY;", environmentBody);
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
