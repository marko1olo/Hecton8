using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class MarchingCubesLookupTableTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            byte caseMask = 0;
            float[] cornerDensities = new float[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
            float isoLevel = 0f;

            // Act
            int result = MarchingCubesLookupTable.Calculate(caseMask, cornerDensities, isoLevel);
            int result2 = MarchingCubesLookupTable.Calculate(caseMask, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, isoLevel);

            // Assert: Verify expected output behaviour
            Assert.AreEqual(0x000, result, "Verify standard calculations return expected results.");
            Assert.AreEqual(0x000, result2, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            byte caseMask = 255;
            float[] cornerDensities = new float[] { -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f };
            float isoLevel = 0f;

            // Act
            int result = MarchingCubesLookupTable.Calculate(caseMask, cornerDensities, isoLevel);
            int result2 = MarchingCubesLookupTable.Calculate(caseMask, -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f, isoLevel);

            // Assert
            Assert.AreEqual(0x000, result, "Verify boundary constraints clamp correctly.");
            Assert.AreEqual(0x000, result2, "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            byte caseMask = 1;
            float[] cornerDensities = new float[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };
            float isoLevel = 0f;

            // Act
            int result = MarchingCubesLookupTable.Calculate(caseMask, cornerDensities, isoLevel);
            int result2 = MarchingCubesLookupTable.Calculate(caseMask, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, isoLevel);

            // Assert
            Assert.AreEqual(0x109, result, "Verify zero inputs are handled without divide-by-zero or exception.");
            Assert.AreEqual(0x109, result2, "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            byte caseMask = 2;
            float[] cornerDensities = new float[] { -1f, 1f, -1f, 1f, -1f, 1f, -1f, 1f };
            float isoLevel = -10f;

            // Act
            int result = MarchingCubesLookupTable.Calculate(caseMask, cornerDensities, isoLevel);
            int result2 = MarchingCubesLookupTable.Calculate(caseMask, -1f, 1f, -1f, 1f, -1f, 1f, -1f, 1f, isoLevel);

            // Assert
            Assert.AreEqual(0x203, result, "Verify negative inputs clamp gracefully or throw.");
            Assert.AreEqual(0x203, result2, "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            byte caseMask = 127;
            float[] cornerDensities = new float[] { float.NaN, 0f, 0f, 0f, 0f, 0f, 0f, 0f };
            float isoLevel = 0f;

            // Act & Assert
            bool throwsException = false;
            try {
                MarchingCubesLookupTable.Calculate(caseMask, cornerDensities, isoLevel);
            } catch(ArgumentException) {
                throwsException = true;
            }
            Assert.IsTrue(throwsException, "Verify robust calculation and overflow protection.");

            bool throwsException2 = false;
            try {
                MarchingCubesLookupTable.Calculate(caseMask, float.NaN, 0f, 0f, 0f, 0f, 0f, 0f, 0f, isoLevel);
            } catch(ArgumentException) {
                throwsException2 = true;
            }
            Assert.IsTrue(throwsException2, "Verify robust calculation and overflow protection.");
        }

        [Test]
        public void Test_NullDensities_Throws()
        {
#pragma warning disable CS8625
            bool throwsException = false;
            try {
                MarchingCubesLookupTable.Calculate(0, null, 0f);
            } catch(ArgumentNullException) {
                throwsException = true;
            }
            Assert.IsTrue(throwsException);
#pragma warning restore CS8625
        }

        [Test]
        public void Test_ShortDensities_Throws()
        {
            bool throwsException = false;
            try {
                MarchingCubesLookupTable.Calculate(0, new float[7], 0f);
            } catch(ArgumentException) {
                throwsException = true;
            }
            Assert.IsTrue(throwsException);
        }

        /// <summary>
        /// TryCalculate is the Burst-legal path: the marching-cubes extract job is
        /// [BurstCompile], and a throw reached from Burst-compiled code aborts the process
        /// in a player build instead of skipping the cell. It must never throw.
        /// </summary>
        [Test]
        public void Test_TryCalculate_NeverThrowsOnNonFinite_Case06()
        {
            int flags;
            bool ok;

            Assert.DoesNotThrow(() =>
                MarchingCubesLookupTable.TryCalculate(127, float.NaN, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, out _));

            ok = MarchingCubesLookupTable.TryCalculate(127, float.NaN, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, out flags);
            Assert.IsFalse(ok, "NaN density must be reported as unusable");
            Assert.AreEqual(0, flags, "unusable input must emit no edges, so the cell produces no triangles");

            ok = MarchingCubesLookupTable.TryCalculate(127, 0f, 0f, 0f, 0f, 0f, 0f, 0f, float.PositiveInfinity, 0f, out flags);
            Assert.IsFalse(ok);
            Assert.AreEqual(0, flags);

            ok = MarchingCubesLookupTable.TryCalculate(127, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, float.NaN, out flags);
            Assert.IsFalse(ok, "non-finite isoLevel must be reported as unusable");
            Assert.AreEqual(0, flags);
        }

        /// <summary>
        /// For every finite input the Burst-safe path must agree exactly with the throwing
        /// path across all 256 cube cases, so switching a call site cannot change geometry.
        /// </summary>
        [Test]
        public void Test_TryCalculate_MatchesCalculateForAllCases_Case07()
        {
            for (int mask = 0; mask < 256; mask++)
            {
                byte m = (byte)mask;
                int expected = MarchingCubesLookupTable.Calculate(m, 1f, -1f, 1f, -1f, 1f, -1f, 1f, -1f, 0f);
                bool ok = MarchingCubesLookupTable.TryCalculate(m, 1f, -1f, 1f, -1f, 1f, -1f, 1f, -1f, 0f, out int actual);

                Assert.IsTrue(ok, "finite input must be usable, mask " + mask);
                Assert.AreEqual(expected, actual, "edge flags must match, mask " + mask);
            }
        }

        /// <summary>
        /// Empty and full cubes cross no edges, so they must emit nothing.
        /// </summary>
        [Test]
        public void Test_TryCalculate_EmptyAndFullCubesEmitNothing_Case08()
        {
            bool okEmpty = MarchingCubesLookupTable.TryCalculate(0, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 0f, out int empty);
            bool okFull = MarchingCubesLookupTable.TryCalculate(255, -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f, 0f, out int full);

            Assert.IsTrue(okEmpty);
            Assert.IsTrue(okFull);
            Assert.AreEqual(0x000, empty, "an entirely outside cube crosses no edge");
            Assert.AreEqual(0x000, full, "an entirely inside cube crosses no edge");
        }
    }
}
