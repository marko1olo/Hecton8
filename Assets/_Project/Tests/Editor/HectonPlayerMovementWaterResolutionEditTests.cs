#if UNITY_EDITOR
using System;
using System.Reflection;
using Hecton8.Gameplay;
using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class HectonPlayerMovementWaterResolutionEditTests
    {
        private static readonly Type s_PlayerMovementType = typeof(HectonPlayerMovement);

        [Test]
        public void ResolveFallbackWaterSurfaceY_RejectsZeroOceanSeaLevelAndKeepsFluidLevel()
        {
            MethodInfo resolveFallback = s_PlayerMovementType.GetMethod(
                "ResolveEffectiveFallbackWaterSurfaceY",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(float),
                    typeof(bool),
                    typeof(float),
                    typeof(bool),
                    typeof(float),
                    typeof(bool),
                    typeof(float)
                },
                null);
            Assert.IsNotNull(resolveFallback, "Missing static water fallback resolver.");

            object resolved = resolveFallback.Invoke(
                null,
                new object[]
                {
                    WorldWaterLevelCalibrationMath.DefaultWaterLevelY,
                    true,
                    0f,
                    false,
                    WorldWaterLevelCalibrationMath.DefaultWaterLevelY,
                    true,
                    -100f
                });

            Assert.AreEqual(-100f, (float)resolved, 0.0001f);
        }

        [Test]
        public void ResolveFallbackWaterSurfaceY_WhenOnlyZeroOceanExistsKeepsCalibrationFallback()
        {
            MethodInfo resolveFallback = s_PlayerMovementType.GetMethod(
                "ResolveEffectiveFallbackWaterSurfaceY",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(float),
                    typeof(bool),
                    typeof(float),
                    typeof(bool),
                    typeof(float),
                    typeof(bool),
                    typeof(float)
                },
                null);
            Assert.IsNotNull(resolveFallback, "Missing static water fallback resolver.");

            object resolved = resolveFallback.Invoke(
                null,
                new object[]
                {
                    -1000f,
                    true,
                    0f,
                    false,
                    WorldWaterLevelCalibrationMath.DefaultWaterLevelY,
                    false,
                    WorldWaterLevelCalibrationMath.DefaultWaterLevelY
                });

            Assert.AreEqual(-1000f, (float)resolved, 0.0001f);
        }

        [Test]
        public void ComputeImmersionRatio_FluidLevelBelowPlayerLeavesPlayerDry()
        {
            MethodInfo computeImmersion = s_PlayerMovementType.GetMethod(
                "ComputeImmersionRatio",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float)
                },
                null);
            Assert.IsNotNull(computeImmersion, "Missing static immersion ratio resolver.");

            const float waterSurfaceY = -100f;
            const float playerCenterY = -50f;
            const float bodyHeight = 1.8f;
            const float feetY = playerCenterY - bodyHeight * 0.5f;
            const float headY = playerCenterY + bodyHeight * 0.5f;

            object resolved = computeImmersion.Invoke(
                null,
                new object[]
                {
                    waterSurfaceY,
                    feetY,
                    headY,
                    bodyHeight
                });
            float immersionRatio = (float)resolved;
            bool submerged = immersionRatio > 0.01f;

            Assert.AreEqual(0f, immersionRatio, 0.0001f);
            Assert.IsFalse(submerged);
        }

        [Test]
        public void TryResolveOceanWaterSurfaceY_RejectsDefaultZeroSeaLevel()
        {
            MethodInfo tryResolveOcean = s_PlayerMovementType.GetMethod(
                "TryResolveOceanWaterSurfaceY",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(float),
                    typeof(float).MakeByRefType()
                },
                null);
            Assert.IsNotNull(tryResolveOcean, "Missing ocean sea-level guard.");

            object[] args = { 0f, 0f };
            object resolved = tryResolveOcean.Invoke(null, args);

            Assert.IsFalse((bool)resolved);
            Assert.AreEqual(WorldWaterLevelCalibrationMath.DefaultWaterLevelY, (float)args[1], 0.0001f);
            Assert.IsTrue(math.isfinite((float)args[1]));
        }

        [Test]
        public void ProjectVelocityOntoGroundPlanePreserveMagnitude_KeepsSpeedAndRemovesNormalComponent()
        {
            MethodInfo projectVelocity = s_PlayerMovementType.GetMethod(
                "ProjectVelocityOntoGroundPlanePreserveMagnitude",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(UnityEngine.Vector3),
                    typeof(UnityEngine.Vector3)
                },
                null);
            Assert.IsNotNull(projectVelocity, "Missing shoreline velocity projection helper.");

            UnityEngine.Vector3 velocity = new UnityEngine.Vector3(0f, 0f, 5f);
            UnityEngine.Vector3 groundNormal = new UnityEngine.Vector3(0f, 0.70710678f, -0.70710678f);
            object projectedObject = projectVelocity.Invoke(null, new object[] { velocity, groundNormal });
            UnityEngine.Vector3 projected = (UnityEngine.Vector3)projectedObject;

            Assert.AreEqual(velocity.magnitude, projected.magnitude, 0.0001f);
            Assert.AreEqual(0f, UnityEngine.Vector3.Dot(projected, groundNormal.normalized), 0.0001f);
            Assert.IsTrue(math.isfinite(projected.x));
            Assert.IsTrue(math.isfinite(projected.y));
            Assert.IsTrue(math.isfinite(projected.z));
        }

        [Test]
        public void SuppressGroundPenetratingVelocity_RemovesOnlyIntoGroundComponent()
        {
            MethodInfo suppressVelocity = s_PlayerMovementType.GetMethod(
                "SuppressGroundPenetratingVelocity",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(UnityEngine.Vector3),
                    typeof(UnityEngine.Vector3)
                },
                null);
            Assert.IsNotNull(suppressVelocity, "Missing shoreline ground-snap velocity helper.");

            UnityEngine.Vector3 velocity = new UnityEngine.Vector3(2f, -4f, 3f);
            object resolvedObject = suppressVelocity.Invoke(null, new object[] { velocity, UnityEngine.Vector3.up });
            UnityEngine.Vector3 resolved = (UnityEngine.Vector3)resolvedObject;

            Assert.AreEqual(2f, resolved.x, 0.0001f);
            Assert.AreEqual(0f, resolved.y, 0.0001f);
            Assert.AreEqual(3f, resolved.z, 0.0001f);
        }
    }
}
#endif
