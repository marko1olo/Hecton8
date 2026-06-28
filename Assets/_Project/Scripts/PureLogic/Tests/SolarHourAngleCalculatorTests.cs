using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SolarHourAngleCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            // Noon at equator on equinox
            float worldTimeSeconds = 43200f; // 12 hours
            float dayLengthSeconds = 86400f; // 24 hours
            float latitude = 0f;
            float axialTilt = 0f;

            // Act
            float elevation = SolarHourAngleCalculator.Compute(worldTimeSeconds, dayLengthSeconds, latitude, axialTilt);

            // Assert: Verify expected output behaviour
            // At noon on equator at equinox, sun should be directly overhead (90 degrees elevation)
            Assert.AreEqual(90f, elevation, 0.001f, "Noon sun elevation at equator should be 90 degrees.");

            // Midnight at equator on equinox
            worldTimeSeconds = 0f;
            elevation = SolarHourAngleCalculator.Compute(worldTimeSeconds, dayLengthSeconds, latitude, axialTilt);
            Assert.AreEqual(-90f, elevation, 0.001f, "Midnight sun elevation at equator should be -90 degrees.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            // North pole at summer solstice
            float worldTimeSeconds = 0f;
            float dayLengthSeconds = 86400f;
            float latitude = 90f;
            float axialTilt = 23.5f;

            // Act
            float elevationMidnight = SolarHourAngleCalculator.Compute(worldTimeSeconds, dayLengthSeconds, latitude, axialTilt);
            worldTimeSeconds = 43200f; // Noon
            float elevationNoon = SolarHourAngleCalculator.Compute(worldTimeSeconds, dayLengthSeconds, latitude, axialTilt);

            // Assert
            // At north pole on summer solstice, sun elevation should be roughly constant at the axial tilt (23.5 deg)
            Assert.AreEqual(23.5f, elevationMidnight, 0.001f, "Sun elevation should be roughly 23.5 degrees.");
            Assert.AreEqual(23.5f, elevationNoon, 0.001f, "Sun elevation should be roughly 23.5 degrees.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float worldTimeSeconds = 0f;
            float dayLengthSeconds = 0f;
            float latitude = 0f;
            float axialTilt = 0f;

            // Act
            float elevation = SolarHourAngleCalculator.Compute(worldTimeSeconds, dayLengthSeconds, latitude, axialTilt);

            // Assert
            Assert.AreEqual(0f, elevation, "When dayLengthSeconds is 0, should return 0 without throwing divide-by-zero.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Negative time should wrap correctly
            float worldTimeSeconds = -43200f; // -12 hours = noon
            float dayLengthSeconds = 86400f;
            float latitude = 0f;
            float axialTilt = 0f;

            // Act
            float elevation = SolarHourAngleCalculator.Compute(worldTimeSeconds, dayLengthSeconds, latitude, axialTilt);

            // Assert
            Assert.AreEqual(90f, elevation, 0.001f, "Negative world time should be properly handled (wrapped) without errors.");

            // Negative day length should clamp/return 0 gracefully
            float elevationNegDay = SolarHourAngleCalculator.Compute(worldTimeSeconds, -86400f, latitude, axialTilt);
            Assert.AreEqual(0f, elevationNegDay, "Negative day length should clamp gracefully and return 0.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float worldTimeSeconds = float.PositiveInfinity;
            float dayLengthSeconds = 86400f;
            float latitude = 0f;
            float axialTilt = 0f;

            // Act
            float elevationInf = SolarHourAngleCalculator.Compute(worldTimeSeconds, dayLengthSeconds, latitude, axialTilt);
            float elevationNaN = SolarHourAngleCalculator.Compute(float.NaN, dayLengthSeconds, latitude, axialTilt);
            float elevationExt = SolarHourAngleCalculator.Compute(1e30f, dayLengthSeconds, latitude, axialTilt);

            // Assert
            Assert.AreEqual(0f, elevationInf, "Infinity values should return 0 safely.");
            Assert.AreEqual(0f, elevationNaN, "NaN values should return 0 safely.");
            Assert.IsTrue(float.IsFinite(elevationExt), "Extreme large float should yield a finite result.");
        }
    }
}
