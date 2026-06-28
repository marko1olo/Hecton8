using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class BatteryChargeCurveCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float chargerRateWatts = 1000f;
            float batteryCapacityWh = 10f; // 36000 Ws, so capacity is never the limit for 1s
            float cvTransitionLevel = 0.8f;
            float deltaTime = 1f;

            // Act - Empty battery, should charge at full rate
            float actualDrawnEmpty = BatteryChargeCurveCalculator.Compute(0f, chargerRateWatts, batteryCapacityWh, cvTransitionLevel, deltaTime);

            // Act - Above CV transition, should taper off
            float actualDrawnTaper = BatteryChargeCurveCalculator.Compute(0.9f, chargerRateWatts, batteryCapacityWh, cvTransitionLevel, deltaTime);

            // Act - Full battery, should be near zero
            float actualDrawnFull = BatteryChargeCurveCalculator.Compute(1f, chargerRateWatts, batteryCapacityWh, cvTransitionLevel, deltaTime);

            // Assert
            Assert.AreEqual(1000f, actualDrawnEmpty, 0.001f, "Empty battery should draw full rate in CC phase.");
            Assert.AreEqual(500f, actualDrawnTaper, 0.001f, "Battery at 90% (halfway past 80% transition) should draw half rate.");
            Assert.AreEqual(0f, actualDrawnFull, 0.001f, "Full battery should not draw any power.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float chargerRateWatts = 5000f;
            float batteryCapacityWh = 1f; // 3600 Ws total
            float cvTransitionLevel = 0.8f;
            float deltaTime = 1f;

            // Act - Almost full, drawing full rate would exceed capacity
            // at 0.99, remaining capacity is 0.01 * 3600 = 36 Ws.
            // Even though taper would allow 5000 * ((1-0.99)/0.2) = 250 W, the remaining capacity limits actual drawn to 36W
            float actualDrawnLimit = BatteryChargeCurveCalculator.Compute(0.99f, chargerRateWatts, batteryCapacityWh, cvTransitionLevel, deltaTime);

            // Assert
            Assert.AreEqual(36f, actualDrawnLimit, 0.01f, "Should clamp actual watts drawn so battery does not exceed 1.0 charge level.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Act
            float drawnNoCapacity = BatteryChargeCurveCalculator.Compute(0.5f, 1000f, 0f, 0.8f, 1f);
            float drawnNoDeltaTime = BatteryChargeCurveCalculator.Compute(0.5f, 1000f, 100f, 0.8f, 0f);
            float drawnNoChargerRate = BatteryChargeCurveCalculator.Compute(0.5f, 0f, 100f, 0.8f, 1f);
            float drawnZeroTransition = BatteryChargeCurveCalculator.Compute(0.5f, 1000f, 100f, 0f, 1f);

            // Assert
            Assert.AreEqual(0f, drawnNoCapacity, "Zero battery capacity should draw 0.");
            Assert.AreEqual(0f, drawnNoDeltaTime, "Zero delta time should draw 0.");
            Assert.AreEqual(0f, drawnNoChargerRate, "Zero charger rate should draw 0.");
            // When transition is zero, it's clamped to 0.001, so at 0.5 we are past it
            // 0.5 is halfway, taper is ~0.5. 1000 * ~0.5 = ~500.5
            Assert.IsTrue(drawnZeroTransition > 490f && drawnZeroTransition < 510f, "Zero transition level should clamp and not cause exception or div by zero.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float chargerRateWatts = -1000f;
            float batteryCapacityWh = -50f;
            float cvTransitionLevel = -0.5f;
            float deltaTime = -1f;
            float chargeLevel01 = -0.5f;

            // Act
            float actualDrawn = BatteryChargeCurveCalculator.Compute(chargeLevel01, chargerRateWatts, batteryCapacityWh, cvTransitionLevel, deltaTime);

            // Assert
            Assert.AreEqual(0f, actualDrawn, "Negative inputs should clamp to safe bounds and yield 0 if no valid charging can happen.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float chargerRateWatts = 1e9f;
            float batteryCapacityWh = 1e6f;
            float cvTransitionLevel = 0.5f;
            float deltaTime = 3600f;

            // Act - NaN handling
            float nanDrawn = BatteryChargeCurveCalculator.Compute(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN);

            // Act - Infinity handling
            float infDrawn = BatteryChargeCurveCalculator.Compute(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

            // Act - Extreme values
            float extremeDrawn = BatteryChargeCurveCalculator.Compute(0f, chargerRateWatts, batteryCapacityWh, cvTransitionLevel, deltaTime);

            // Assert
            Assert.AreEqual(0f, nanDrawn, "NaNs should clamp/fallback safely.");
            Assert.AreEqual(0f, infDrawn, "Infinity should clamp/fallback safely.");

            // Extreme drawn is limited by capacity. Capacity is 1e6 Wh = 3.6e9 Ws.
            // Delta time is 3600. Max Ws is 3.6e9. Rate requested is 1e9 W -> 3.6e12 Ws.
            // Capped at 3.6e9 Ws. So 3.6e9 / 3600 = 1e6 Watts actual.
            Assert.AreEqual(1e6f, extremeDrawn, 1f, "Extreme parameters should calculate correctly without overflow if within floats.");
        }
    }
}
