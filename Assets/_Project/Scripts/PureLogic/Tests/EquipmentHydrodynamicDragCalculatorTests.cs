#nullable disable
using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class EquipmentHydrodynamicDragCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            ulong activeMask = 0b101; // Bits 0 and 2 are active
            float[] dragTable = new float[] { 0.5f, 0.2f, 0.8f, 0.1f };

            // Act
            float result = EquipmentHydrodynamicDragCalculator.Compute(activeMask, dragTable);

            // Assert: Verify expected output behaviour
            // Bit 0 = 0.5f
            // Bit 2 = 0.8f
            // Base = 1.0f
            // Total = 1.0f + 0.5f + 0.8f = 2.3f
            Assert.That(result, Is.EqualTo(2.3f).Within(0.001f), "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            // Test mask goes exactly up to 64 items (bit 63)
            ulong activeMask = 1UL << 63;
            float[] dragTable = new float[64];
            dragTable[63] = 4.2f;

            // Act
            float result = EquipmentHydrodynamicDragCalculator.Compute(activeMask, dragTable);

            // Assert
            Assert.That(result, Is.EqualTo(5.2f).Within(0.001f), "Verify boundary constraint (64th bit) computes correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            ulong activeMask = 0UL;
            float[] dragTable = null;

            // Act
            float resultNull = EquipmentHydrodynamicDragCalculator.Compute(activeMask, dragTable);
            float resultEmpty = EquipmentHydrodynamicDragCalculator.Compute(activeMask, new float[0]);

            // Assert
            Assert.That(resultNull, Is.EqualTo(1.0f), "Null array should return base drag.");
            Assert.That(resultEmpty, Is.EqualTo(1.0f), "Empty array should return base drag.");

            // Mask zero but valid array
            float resultZeroMask = EquipmentHydrodynamicDragCalculator.Compute(0UL, new float[] { 1f, 2f });
            Assert.That(resultZeroMask, Is.EqualTo(1.0f), "Verify zero mask yields 1.0 drag.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            ulong activeMask = 0b11; // Bits 0 and 1 active
            float[] dragTable = new float[] { -0.5f, -1.2f }; // Negative drag values

            // Act
            float result = EquipmentHydrodynamicDragCalculator.Compute(activeMask, dragTable);

            // Assert
            // Negative values are ignored (clamped to 0 effective addition), so base 1.0 is returned
            Assert.That(result, Is.EqualTo(1.0f).Within(0.001f), "Verify negative inputs clamp gracefully or are ignored.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            ulong activeMask = 0b111;
            float[] dragTable = new float[] { float.NaN, float.PositiveInfinity, float.MaxValue };

            // Act
            float result = EquipmentHydrodynamicDragCalculator.Compute(activeMask, dragTable);

            // Assert
            // NaN and Infinity are ignored. float.MaxValue could overflow, but float.IsInfinity catches infinity outputs.
            // In this specific test, we'll see MaxValue + 1.0f which stays within float but might lose precision.
            // The main test is that it doesn't return NaN or Infinity.
            Assert.That(!float.IsNaN(result) && !float.IsInfinity(result), Is.True, "Verify robust calculation and overflow protection.");
        }
    }
}
