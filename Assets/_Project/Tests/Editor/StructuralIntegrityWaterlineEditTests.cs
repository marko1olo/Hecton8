using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class StructuralIntegrityWaterlineEditTests
    {
        [Test]
        public void StructuralIntegrityRuntimeSanitizesSeaLevelAupYBeforeTuningAndMockStress()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Habitat", "Deformation", "Runtime", "StructuralIntegrityCalculatorRuntime.cs");
            string writeDefaults = ExtractMethodBody(source, "private void WriteDefaultTuning(NativeArray<StructuralTuningDTO> tuning)");
            string mockStress = ExtractMethodBody(source, "private bool GenerateEmergencyMockStressData()");
            string sanitize = ExtractMethodBody(source, "private static StructuralTuningDTO SanitizeTuning(in StructuralTuningDTO source)");
            string runtimeSeaLevel = ExtractMethodBody(source, "private double3 ResolveRuntimeSeaLevelAup()");
            string oceanSeaLevel = ExtractMethodBody(source, "private static bool TryResolveSeaLevelAupY(float candidateSeaLevelY, out double seaLevelAupY)");
            string serializedSeaLevel = ExtractMethodBody(source, "private static double ResolveSeaLevelAupY(double candidateSeaLevelAupY)");

            StringAssert.Contains("private const float DefaultSeaLevelAupY = 14.02f;", source);
            StringAssert.Contains("[SerializeField] private Vector3 seaLevelAup = new Vector3(0f, DefaultSeaLevelAupY, 0f);", source);
            StringAssert.Contains("double3 runtimeSeaLevelAup = ResolveRuntimeSeaLevelAup();", writeDefaults);
            StringAssert.Contains("SeaLevelAup = ResolveSeaLevelAup(seaLevelAup)", writeDefaults);
            StringAssert.Contains("sanitized.SeaLevelAup = runtimeSeaLevelAup;", writeDefaults);
            StringAssert.Contains("SeaLevelAup = ResolveRuntimeSeaLevelAup()", mockStress);
            StringAssert.Contains("current.SeaLevelAup = ResolveRuntimeSeaLevelAup();", source);
            StringAssert.Contains("tuning.SeaLevelAup = SanitizeSeaLevelAup(tuning.SeaLevelAup);", sanitize);
            StringAssert.Contains("if (TryResolveOceanSeaLevelAupY(out double oceanSeaLevelAupY))", runtimeSeaLevel);
            StringAssert.Contains("resolved.y = oceanSeaLevelAupY;", runtimeSeaLevel);
            StringAssert.Contains("return resolved;", runtimeSeaLevel);
            StringAssert.Contains("Hecton8.World.WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY", oceanSeaLevel);
            StringAssert.DoesNotContain("math.abs(candidateSeaLevelY) > 0.0001f", oceanSeaLevel);
            StringAssert.Contains("private static double ResolveSeaLevelAupY(double candidateSeaLevelAupY)", source);
            StringAssert.Contains("math.abs(candidateSeaLevelAupY) > 0.0001d", serializedSeaLevel);
            StringAssert.Contains("math.abs(candidateSeaLevelAupY) <= 1000d", serializedSeaLevel);
            StringAssert.DoesNotContain("SeaLevelAup = new double3(seaLevelAup.x, seaLevelAup.y, seaLevelAup.z)", source);
            StringAssert.DoesNotContain("return SanitizeSeaLevelAup(resolved);", runtimeSeaLevel);
            StringAssert.DoesNotContain("tuning.SeaLevelAup = double3.zero;", source);
        }

        [Test]
        public void HabitatDamageBakeSanitizesSeaLevelAupYBeforeDepthAndPreviewDefaults()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Habitat", "Deformation", "Editor", "DamageBake", "HabitatDamageBakePipeline.cs");
            string resolveDepth = ExtractMethodBody(source, "public static double ResolveDepthMeters(double3 moduleAup, double3 seaLevelAup)");
            string currentSettings = ExtractMethodBody(source, "private HabitatDamageBakeSettings CurrentSettings()");

            StringAssert.Contains("public const double DefaultSeaLevelAupY = 14.02d;", source);
            StringAssert.Contains("double3 resolvedSeaLevelAup = SanitizeSeaLevelAup(seaLevelAup);", resolveDepth);
            StringAssert.Contains("double depth = resolvedSeaLevelAup.y - moduleAup.y;", resolveDepth);
            StringAssert.Contains("private static double3 SanitizeSeaLevelAup(double3 candidateSeaLevelAup)", source);
            StringAssert.Contains("private static double ResolveSeaLevelAupY(double candidateSeaLevelAupY)", source);
            StringAssert.Contains("math.abs(candidateSeaLevelAupY) <= 1000d", source);
            StringAssert.Contains("SeaLevelAup = new double3(0d, HabitatDamageBakeConstants.DefaultSeaLevelAupY, 0d)", currentSettings);
            StringAssert.DoesNotContain("SeaLevelAup = double3.zero", source);
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
