using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class VisualPressureAgingWaterlineEditTests
    {
        [Test]
        public void DegradationJobUsesProductionSeaLevelFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Graphics", "Materials", "VisualPressureAgingRuntime.cs");
            string jobBody = ExtractTypeBody(source, "internal unsafe struct CompileDegradationParametersJob");

            StringAssert.Contains("private const double DefaultSeaLevelY = 14.02d;", jobBody);
            StringAssert.Contains(": new double3(0.0, DefaultSeaLevelY, 0.0);", jobBody);
            StringAssert.Contains("double seaY = ResolveSeaLevelY(seaLevel.y);", jobBody);
            StringAssert.Contains("private static double ResolveSeaLevelY(double seaLevelY)", jobBody);
            StringAssert.Contains("math.abs(seaLevelY) <= 1000d", jobBody);
            StringAssert.DoesNotContain("new double3(" + "0.0);", jobBody);
            StringAssert.DoesNotContain("? seaLevel.y : " + "0.0", jobBody);
        }

        private static string ReadProjectFile(params string[] parts)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(parts));
            return File.ReadAllText(path);
        }

        private static string ExtractTypeBody(string source, string signature)
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

            Assert.Fail("Could not extract type body for " + signature);
            return string.Empty;
        }
    }
}
