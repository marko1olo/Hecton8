PROJECT_ROOT=$PWD
mkdir -p /tmp/hecton_tests
cd /tmp/hecton_tests/PureLogicTests
rm Systems/CeilingConcavityAirPocketVolumeCalculator.cs || true
rm Tests/CeilingConcavityAirPocketVolumeCalculatorTests.cs || true

cat << 'SYS' > Systems/CeilingConcavityAirPocketVolumeCalculator.cs
using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    public static class CeilingConcavityAirPocketVolumeCalculator
    {
        public static float Compute(Vector3 normal, float ceilingDepth, float waterlineClearance, float boundaryRadius)
        {
            if (float.IsNaN(ceilingDepth) || float.IsInfinity(ceilingDepth)) ceilingDepth = 0f;
            if (float.IsNaN(waterlineClearance) || float.IsInfinity(waterlineClearance)) waterlineClearance = 0f;
            if (float.IsNaN(boundaryRadius) || float.IsInfinity(boundaryRadius)) boundaryRadius = 0f;
            if (float.IsNaN(normal.X) || float.IsInfinity(normal.X) || float.IsNaN(normal.Y) || float.IsInfinity(normal.Y) || float.IsNaN(normal.Z) || float.IsInfinity(normal.Z)) normal = new Vector3(0, -1, 0);

            ceilingDepth = Math.Max(0f, ceilingDepth);
            waterlineClearance = Math.Max(0f, waterlineClearance);
            boundaryRadius = Math.Max(0f, boundaryRadius);

            float flatness = Math.Max(0f, Vector3.Dot(normal, new Vector3(0, -1, 0)));
            float concavityFactor = 1f - flatness;

            float height = ceilingDepth + waterlineClearance;
            float rawVolume = (float)Math.PI * boundaryRadius * boundaryRadius * height;

            float finalVolume = rawVolume * concavityFactor;

            return float.IsNaN(finalVolume) || float.IsInfinity(finalVolume) ? 0f : finalVolume;
        }
    }
}
SYS

cat << 'TEST' > Tests/CeilingConcavityAirPocketVolumeCalculatorTests.cs
using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class CeilingConcavityAirPocketVolumeCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            Vector3 normal = new Vector3(0.5f, -0.5f, 0.5f);
            float volume = CeilingConcavityAirPocketVolumeCalculator.Compute(normal, 5f, 2f, 3f);
            Assert.That(volume, Is.GreaterThan(0f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            Vector3 normal = new Vector3(0f, -1f, 0f);
            float volume = CeilingConcavityAirPocketVolumeCalculator.Compute(normal, 10f, 5f, 5f);
            Assert.That(volume, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            Vector3 normal = new Vector3(1f, 0f, 0f);
            float volume = CeilingConcavityAirPocketVolumeCalculator.Compute(normal, 0f, 0f, 0f);
            Assert.That(volume, Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            Vector3 normal = new Vector3(1f, 0f, 0f);
            float volume = CeilingConcavityAirPocketVolumeCalculator.Compute(normal, -10f, -5f, -2f);
            Assert.That(volume, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            Vector3 normal = new Vector3(1f, 0f, 0f);
            float volume = CeilingConcavityAirPocketVolumeCalculator.Compute(normal, float.PositiveInfinity, float.NaN, float.PositiveInfinity);
            Assert.That(volume, Is.EqualTo(0f));
        }
    }
}
TEST
dotnet test
