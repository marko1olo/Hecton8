using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class VehicleDamageWaterlineEditTests
    {
        [Test]
        public void VehicleComponentDamageDepthUsesProductionSeaLevelInsteadOfZeroPlane()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Physics",
                "Vehicles",
                "VehicleComponentDamageRuntime.cs");
            string resolveDepth = ExtractMethodBody(source, "private static float ResolveDepthMeters(double3 rootAup)");

            StringAssert.Contains("private const double DefaultSeaLevelAupY = 14.02d;", source);
            StringAssert.Contains("double depthMeters = DefaultSeaLevelAupY - rootAup.y;", resolveDepth);
            StringAssert.DoesNotContain("const double seaLevelAupY = 0d;", source);
            StringAssert.DoesNotContain("double depthMeters = seaLevelAupY - rootAup.y;", source);
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
