using System;
using System.IO;
using Hecton8.Power;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class SolarPowerWaterlineEditTests
    {
        [Test]
        public void SolarSeaLevelFallbackUsesRuntimeWaterline()
        {
            double3 origin = new double3(10.0, 100.0, -30.0);

            Assert.That(SolarPowerGenerationConstants.DefaultSeaLevelY, Is.EqualTo(14.02f).Within(0.0001f));
            Assert.That(SolarPowerGenerationConstants.ResolveSeaLevelDeltaMeters(0f), Is.EqualTo(SolarPowerGenerationConstants.DefaultSeaLevelY).Within(0.0001f));
            Assert.That(SolarPowerGenerationConstants.ResolveSeaLevelDeltaMeters(float.NaN), Is.EqualTo(SolarPowerGenerationConstants.DefaultSeaLevelY).Within(0.0001f));
            Assert.That(SolarPowerGenerationConstants.ResolveSeaLevelDeltaMeters(4900f), Is.EqualTo(SolarPowerGenerationConstants.DefaultSeaLevelY).Within(0.0001f));
            Assert.That(SolarPowerGenerationConstants.ResolveSeaLevelDeltaMeters(5f), Is.EqualTo(5f).Within(0.0001f));

            double3 sea = SolarPowerGenerationConstants.BuildSeaLevelAUP(origin, 0f);
            Assert.That(sea.x, Is.EqualTo(origin.x).Within(0.0001d));
            Assert.That(sea.y, Is.EqualTo(origin.y + SolarPowerGenerationConstants.DefaultSeaLevelY).Within(0.0001d));
            Assert.That(sea.z, Is.EqualTo(origin.z).Within(0.0001d));
        }

        [Test]
        public void SolarConditionsRoutesSanitizeStaleZeroSeaLevel()
        {
            string contracts = ReadProjectFile("Assets", "_Project", "Scripts", "Power", "PowerGridSolarContracts.cs");
            string panel = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "SolarPanel.cs");

            StringAssert.Contains("public const float DefaultSeaLevelY = 14.02f;", contracts);
            StringAssert.Contains("math.abs(seaLevelDeltaMeters) <= 1000f", contracts);
            StringAssert.Contains("conditions.SeaLevelAUP = SolarPowerGenerationConstants.BuildSeaLevelAUP(RuntimeOriginAUP, TideHeightMeters);", contracts);
            StringAssert.Contains("conditions.SeaLevelAUP = SolarPowerGenerationConstants.BuildSeaLevelAUP(conditions.RuntimeOriginAUP, SolarPowerGenerationConstants.DefaultSeaLevelY);", contracts);
            StringAssert.Contains("bool seaLevelAupDefault = math.lengthsq(result.SeaLevelAUP) <= 0.000001d;", contracts);
            StringAssert.Contains("seaLevelAupDefault ||", contracts);
            StringAssert.Contains("math.abs(seaLevelDeltaMeters) > 1000d", contracts);
            StringAssert.Contains("result.SeaLevelAUP = SolarPowerGenerationConstants.BuildSeaLevelAUP(result.RuntimeOriginAUP, (float)seaLevelDeltaMeters);", contracts);
            StringAssert.DoesNotContain("result.SeaLevelAUP = default;", contracts);

            StringAssert.Contains("[SerializeField] private float seaLevelRuntimeY = SolarPowerGenerationConstants.DefaultSeaLevelY;", panel);
            StringAssert.Contains("float baseSeaLevelY = SolarPowerGenerationConstants.ResolveSeaLevelDeltaMeters(seaLevelRuntimeY);", panel);
            StringAssert.Contains("conditions.SeaLevelAUP = SolarPowerGenerationConstants.BuildSeaLevelAUP(runtimeOrigin, baseSeaLevelY + tideHeightMeters);", panel);
        }

        private static string ReadProjectFile(params string[] parts)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(parts));
            return File.ReadAllText(path);
        }
    }
}
