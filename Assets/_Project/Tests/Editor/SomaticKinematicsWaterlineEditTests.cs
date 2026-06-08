using System;
using System.IO;
using Hecton8.Gameplay;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class SomaticKinematicsWaterlineEditTests
    {
        [Test]
        public void EmergencyTuningSeaLevelMatchesRuntimeWaterline()
        {
            SomaticKinematicsTuningData tuning = SomaticKinematicsTuningData.CreateEmergency();

            Assert.That(SomaticKinematicsRuntime.DefaultSeaLevelY, Is.EqualTo(14.02f).Within(0.0001f));
            Assert.That(tuning.SeaLevelY, Is.EqualTo(SomaticKinematicsRuntime.DefaultSeaLevelY).Within(0.0001f));

            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "SomaticKinematicsRuntime.cs");
            StringAssert.Contains("public const float DefaultSeaLevelY = 14.02f;", source);
            StringAssert.Contains("tuning.SeaLevelY = SomaticKinematicsRuntime.DefaultSeaLevelY;", source);
            StringAssert.Contains("tuning.SeaLevelY = SanitizeSeaLevelY(tuning.SeaLevelY, fallback.SeaLevelY);", source);
            StringAssert.Contains("math.abs(value) <= 1000f", source);
            StringAssert.DoesNotContain("tuning.SeaLevelY = " + "0.0f;", source);
        }

        private static string ReadProjectFile(params string[] parts)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(parts));
            return File.ReadAllText(path);
        }
    }
}
