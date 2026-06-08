using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class SpatialAudioWaterlineEditTests
    {
        [Test]
        public void VirtualListenerDepthUsesProductionSeaLevelForAcousticVirtualization()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "SpatialAudioManager.cs");
            string runTick = ExtractMethodBody(source, "private void RunSpatialAudioTickCore(float deltaTime)");
            string resolveDepth = ExtractMethodBody(source, "private static float ResolveVirtualListenerDepthMeters(Vector3 listenerAupRuntimePosition)");

            StringAssert.Contains("private const float DefaultSeaLevelY = OceanSurfaceAtmosphereConstants.DefaultSeaLevel;", source);
            StringAssert.Contains("float listenerDepthMeters = ResolveVirtualListenerDepthMeters(listenerAupRuntimePosition);", runTick);
            StringAssert.Contains("math.max(0f, DefaultSeaLevelY - listenerAupRuntimePosition.y)", resolveDepth);
            StringAssert.DoesNotContain("float listenerDepthMeters = math.max(0f, -listenerAupRuntimePosition.y);", source);
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
