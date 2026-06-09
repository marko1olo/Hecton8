using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class SubmarineBallastWaterlineEditTests
    {
        [Test]
        public void BallastFallbackExternalDepthUsesProductionSeaLevelWhenFluidDynamicsIsUnavailable()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "SubmarineAutoLevelBallastController.cs");
            string prepareSample = ExtractMethodBody(source, "private bool PrepareBallastFluidSample(float fixedDeltaTime)");
            string fallbackDepth = ExtractMethodBody(source, "private float ResolveFallbackExternalDepthMeters(Vector3 worldCenter)");

            StringAssert.Contains("private const float DefaultSeaLevelY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;", source);
            StringAssert.Contains(": ResolveFallbackExternalDepthMeters(worldCenter);", prepareSample);
            StringAssert.Contains("float seaLevelY = ResolveFallbackSeaLevelY();", fallbackDepth);
            StringAssert.Contains("math.max(0f, seaLevelY - worldCenter.y)", fallbackDepth);
            StringAssert.DoesNotContain(": math.max(0f, -worldCenter.y);", source);
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
