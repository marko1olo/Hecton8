using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class VoxelExplosionDeformationVolumeCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float currentSdf = 1.0f;
            float distanceToEpicenter = 5.0f;
            float explosionRadius = 10.0f;
            float blastForce = 4.0f;

            // Act
            float result = VoxelExplosionDeformationVolumeCalculator.Compute(currentSdf, distanceToEpicenter, explosionRadius, blastForce);

            // Assert: Verify expected output behaviour
            // falloff = 1 - 5/10 = 0.5
            // displacement = 4 * 0.5 * 0.5 = 1.0
            // expected = 1.0 - 1.0 = 0.0
            Assert.That(result, Is.EqualTo(0.0f).Within(0.0001f), "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float currentSdf = 1.0f;
            float distanceToEpicenter = 10.0f;
            float explosionRadius = 10.0f;
            float blastForce = 4.0f;

            // Act
            float result1 = VoxelExplosionDeformationVolumeCalculator.Compute(currentSdf, distanceToEpicenter, explosionRadius, blastForce);
            float result2 = VoxelExplosionDeformationVolumeCalculator.Compute(currentSdf, 11.0f, explosionRadius, blastForce);

            // Assert
            Assert.That(result1, Is.EqualTo(1.0f).Within(0.0001f), "Verify distance == explosionRadius returns currentSdf.");
            Assert.That(result2, Is.EqualTo(1.0f).Within(0.0001f), "Verify distance > explosionRadius yields no change.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float currentSdf = 1.0f;

            // Act
            float resultZeroRadius = VoxelExplosionDeformationVolumeCalculator.Compute(currentSdf, 5.0f, 0.0f, 4.0f);
            float resultZeroForce = VoxelExplosionDeformationVolumeCalculator.Compute(currentSdf, 5.0f, 10.0f, 0.0f);
            float resultEpicenter = VoxelExplosionDeformationVolumeCalculator.Compute(currentSdf, 0.0f, 10.0f, 4.0f);

            // Assert
            Assert.That(resultZeroRadius, Is.EqualTo(1.0f), "Zero radius yields no change.");
            Assert.That(resultZeroForce, Is.EqualTo(1.0f), "Zero force yields no change.");

            // Epicenter: falloff = 1. displacement = 4. expected = 1 - 4 = -3
            Assert.That(resultEpicenter, Is.EqualTo(-3.0f).Within(0.0001f), "Epicenter shifts toward maximum negative SDF (hollowed).");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float currentSdf = 1.0f;

            // Act
            float resultNegativeRadius = VoxelExplosionDeformationVolumeCalculator.Compute(currentSdf, 5.0f, -10.0f, 4.0f);
            float resultNegativeForce = VoxelExplosionDeformationVolumeCalculator.Compute(currentSdf, 5.0f, 10.0f, -4.0f);
            float resultNegativeDistance = VoxelExplosionDeformationVolumeCalculator.Compute(currentSdf, -5.0f, 10.0f, 4.0f);

            // Assert
            Assert.That(resultNegativeRadius, Is.EqualTo(1.0f), "Negative radius yields no change.");
            Assert.That(resultNegativeForce, Is.EqualTo(1.0f), "Negative force yields no change.");

            // Negative distance should clamp to 0. So it acts like epicenter.
            // Expected = 1.0 - 4.0 = -3.0
            Assert.That(resultNegativeDistance, Is.EqualTo(-3.0f).Within(0.0001f), "Negative distance clamps to 0 (epicenter).");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float currentSdf = 1.0f;

            // Act
            float resultNaN1 = VoxelExplosionDeformationVolumeCalculator.Compute(float.NaN, 5.0f, 10.0f, 4.0f);
            float resultNaN2 = VoxelExplosionDeformationVolumeCalculator.Compute(currentSdf, float.NaN, 10.0f, 4.0f);
            float resultInfinity = VoxelExplosionDeformationVolumeCalculator.Compute(currentSdf, float.PositiveInfinity, 10.0f, 4.0f);
            float resultLarge = VoxelExplosionDeformationVolumeCalculator.Compute(currentSdf, 1e15f, 10.0f, 4.0f);

            // Assert
            Assert.That(resultNaN1, Is.EqualTo(0.0f), "NaN SDF should return 0.");
            Assert.That(resultNaN2, Is.EqualTo(1.0f), "NaN parameter (non-SDF) should return original SDF.");
            Assert.That(resultInfinity, Is.EqualTo(1.0f), "Infinity distance should return original SDF.");
            Assert.That(resultLarge, Is.EqualTo(1.0f), "Very large distance should return original SDF.");
        }
    }
}
