#if UNITY_EDITOR
using System.IO;
using Hecton8.Core.Contracts;
using Hecton8.Caves;
using Hecton8.Physics.KCC;
using Hecton8.Physics.KCC.Editor;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class HeadlessKccSmokeTests
    {
        [Test]
        public void OceanKinematicsRuntimeService_HasNoForbiddenHeadlessDependency()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(projectRoot, "Assets", "_Project", "Scripts", "Core", "OceanKinematicsRuntimeService.cs");
            string source = File.ReadAllText(path);

            Assert.IsFalse(source.Contains("Camera.main"), "OceanKinematicsRuntimeService must not read Camera.main.");
            Assert.IsFalse(source.Contains("Time.deltaTime"), "OceanKinematicsRuntimeService must use injected tick dt.");
            Assert.IsFalse(source.Contains("FindObjectOfType"), "OceanKinematicsRuntimeService must not search the scene.");
            Assert.IsFalse(source.Contains("GameObject.Find"), "OceanKinematicsRuntimeService must not search the scene.");
        }

        [Test]
        public void HeadlessKcc_Layouts_AreExplicitAndAligned()
        {
            HeadlessKccLayoutAssertions.AssertAll();
        }

        [Test]
        public void HeadlessKcc_SmokeRunner_UsesShinobu355SingleHeavyEntryPoint()
        {
            Assert.AreEqual(100, HydrodynamicKccRuntime.KccSmokeDefaultPhantomCount);
            Assert.AreEqual(10000, HydrodynamicKccRuntime.KccSmokeDefaultFrameCount);
        }

        [Test]
        public void HeadlessKcc_SmokeRunner_Preserves100MpsConeProbe()
        {
            bool valid = Shinobu355KccSmokeRunner.ValidateApexConeFallContract(out float displacementPerFrameMeters, out float tuningMaxSpeedMetersPerSecond);
            Assert.IsTrue(valid);
            Assert.AreEqual(1.6666667f, displacementPerFrameMeters, 0.0001f);
            Assert.GreaterOrEqual(tuningMaxSpeedMetersPerSecond, Shinobu355KccSmokeRunner.ConeFallProofSpeedMetersPerSecond);
        }

        [Test]
        public void VoxelSonarSdfMath_Raymarch_IsBoundedAndOverflowSafe()
        {
            NativeArray<byte> sdf = new NativeArray<byte>(64, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try
            {
                for (int z = 0; z < 4; z++)
                for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                {
                    float signedDistance = x - 1.5f;
                    int index = x + 4 * (y + 4 * z);
                    sdf[index] = EncodeSignedDistanceByte(signedDistance, 2f);
                }

                bool insideToOutsideHit = VoxelSonarSdfMath.TryRaymarchEncodedSdf(
                    sdf.AsReadOnly(),
                    new int3(4, 4, 4),
                    float3.zero,
                    new float3(1f, 1f, 1f),
                    2f,
                    new float3(0f, 1.5f, 1.5f),
                    new float3(1f, 0f, 0f),
                    1000000000f,
                    0.000001f,
                    out VoxelSonarSdfRaycastHit rayHit);

                Assert.IsTrue(insideToOutsideHit);
                Assert.AreEqual(VoxelSonarSdfRaycastHit.FlagHit, rayHit.Flags & VoxelSonarSdfRaycastHit.FlagHit);
                Assert.That(rayHit.Distance, Is.InRange(1.25f, 1.75f));
                Assert.That(rayHit.Normal.x, Is.GreaterThan(0.5f));

                bool outsideToInsideHit = VoxelSonarSdfMath.TryRaymarchEncodedSdf(
                    sdf.AsReadOnly(),
                    new int3(4, 4, 4),
                    float3.zero,
                    new float3(1f, 1f, 1f),
                    2f,
                    new float3(3f, 1.5f, 1.5f),
                    new float3(-1f, 0f, 0f),
                    4f,
                    0.01f,
                    out VoxelSonarSdfRaycastHit reverseRayHit);

                Assert.IsTrue(outsideToInsideHit);
                Assert.That(reverseRayHit.Distance, Is.InRange(1.25f, 1.75f));
                Assert.That(reverseRayHit.Normal.x, Is.GreaterThan(0.5f));

                bool outsideAwayHit = VoxelSonarSdfMath.TryRaymarchEncodedSdf(
                    sdf.AsReadOnly(),
                    new int3(4, 4, 4),
                    float3.zero,
                    new float3(1f, 1f, 1f),
                    2f,
                    new float3(3f, 1.5f, 1.5f),
                    new float3(1f, 0f, 0f),
                    1f,
                    0.01f,
                    out _);

                Assert.IsFalse(outsideAwayHit);

                bool oversizedGridRejected = VoxelSonarSdfMath.TryRaymarchEncodedSdf(
                    sdf.AsReadOnly(),
                    new int3(65536, 65536, 2),
                    float3.zero,
                    new float3(1f, 1f, 1f),
                    2f,
                    float3.zero,
                    new float3(1f, 0f, 0f),
                    1f,
                    0.1f,
                    out _);

                Assert.IsFalse(oversizedGridRejected);
            }
            finally
            {
                if (sdf.IsCreated)
                    sdf.Dispose();
            }
        }

        [Test]
        public void VoxelSdfRaymarchJob_InvalidInputClearsStaleHit()
        {
            NativeArray<VoxelSdfRaycastHit> result = new NativeArray<VoxelSdfRaycastHit>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try
            {
                result[0] = new VoxelSdfRaycastHit
                {
                    Point = new Vector3(1f, 2f, 3f),
                    Normal = Vector3.up,
                    Distance = 9f,
                    Density = 1f,
                    Hit = 1
                };

                VoxelSdfRaymarchJob job = new VoxelSdfRaymarchJob
                {
                    EncodedSdf = default,
                    GridDimensions = new int3(0, 0, 0),
                    VolumeOrigin = float3.zero,
                    CellSize = new float3(1f, 1f, 1f),
                    SdfRange = 0f,
                    Origin = float3.zero,
                    Direction = new float3(1f, 0f, 0f),
                    MaxDistance = 1f,
                    StepMeters = 0.1f,
                    Result = result
                };

                job.Execute();

                Assert.AreEqual(0, result[0].Hit);
                Assert.AreEqual(0f, result[0].Distance);
                Assert.AreEqual(Vector3.zero, result[0].Point);
            }
            finally
            {
                if (result.IsCreated)
                    result.Dispose();
            }
        }

        [Test]
        public void VoxelSonarSdfMath_Raymarch_RejectsOutsideAwayBoundaryClampHit()
        {
            NativeArray<byte> sdf = new NativeArray<byte>(64, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try
            {
                for (int z = 0; z < 4; z++)
                for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                {
                    int index = x + 4 * (y + 4 * z);
                    sdf[index] = EncodeSignedDistanceByte(x, 2f);
                }

                bool awayHit = VoxelSonarSdfMath.TryRaymarchEncodedSdf(
                    sdf.AsReadOnly(),
                    new int3(4, 4, 4),
                    float3.zero,
                    new float3(1f, 1f, 1f),
                    2f,
                    new float3(-2f, 1.5f, 1.5f),
                    new float3(-1f, 0f, 0f),
                    8f,
                    0.01f,
                    out _);

                Assert.IsFalse(awayHit);

                bool entryHit = VoxelSonarSdfMath.TryRaymarchEncodedSdf(
                    sdf.AsReadOnly(),
                    new int3(4, 4, 4),
                    float3.zero,
                    new float3(1f, 1f, 1f),
                    2f,
                    new float3(-2f, 1.5f, 1.5f),
                    new float3(1f, 0f, 0f),
                    8f,
                    0.01f,
                    out VoxelSonarSdfRaycastHit entryHitResult);

                Assert.IsTrue(entryHit);
                Assert.That(entryHitResult.Distance, Is.InRange(1.9f, 2.1f));
            }
            finally
            {
                if (sdf.IsCreated)
                    sdf.Dispose();
            }
        }

        [Test]
        public void VoxelSdfRaymarchJob_RejectsOutsideAwayBoundaryClampHit()
        {
            NativeArray<byte> sdf = new NativeArray<byte>(64, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            NativeArray<VoxelSdfRaycastHit> result = new NativeArray<VoxelSdfRaycastHit>(1, Allocator.Temp, NativeArrayOptions.ClearMemory);
            try
            {
                for (int z = 0; z < 4; z++)
                for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                {
                    int index = x + 4 * (y + 4 * z);
                    sdf[index] = EncodeSignedDistanceByte(x, 2f);
                }

                VoxelSdfRaymarchJob awayJob = new VoxelSdfRaymarchJob
                {
                    EncodedSdf = sdf.AsReadOnly(),
                    GridDimensions = new int3(4, 4, 4),
                    VolumeOrigin = float3.zero,
                    CellSize = new float3(1f, 1f, 1f),
                    SdfRange = 2f,
                    Origin = new float3(-2f, 1.5f, 1.5f),
                    Direction = new float3(-1f, 0f, 0f),
                    MaxDistance = 8f,
                    StepMeters = 0.01f,
                    Result = result
                };

                awayJob.Execute();

                Assert.AreEqual(0, result[0].Hit);

                VoxelSdfRaymarchJob entryJob = awayJob;
                entryJob.Direction = new float3(1f, 0f, 0f);
                entryJob.Execute();

                Assert.AreEqual(1, result[0].Hit);
                Assert.That(result[0].Distance, Is.InRange(1.9f, 2.1f));
            }
            finally
            {
                if (result.IsCreated)
                    result.Dispose();
                if (sdf.IsCreated)
                    sdf.Dispose();
            }
        }

        [Test]
        public void VoxelSonarSdfMath_SurfaceResolverPrefersOwnerRayDirectedRoute()
        {
            OwnerDirectedSurfaceResolverStub readModel = new OwnerDirectedSurfaceResolverStub();

            bool resolved = VoxelSonarSdfMath.TryResolveNearestSdfSurface(
                readModel,
                new float3(10f, 0f, 0f),
                new float3(-1f, 0f, 0f),
                32f,
                0.1f,
                out VoxelSonarSdfRaycastHit hit);

            Assert.IsTrue(resolved);
            Assert.AreEqual(1, readModel.SurfaceResolveCalls);
            Assert.AreEqual(0, readModel.NearestPayloadReadCalls);
            Assert.AreEqual(VoxelSonarSdfRaycastHit.FlagHit, hit.Flags & VoxelSonarSdfRaycastHit.FlagHit);
            Assert.AreEqual(4f, hit.Distance, 0.0001f);
        }

        private static byte EncodeSignedDistanceByte(float signedDistance, float range)
        {
            float normalized = math.saturate((signedDistance / math.max(0.0001f, range) + 1f) * 0.5f);
            return (byte)math.clamp((int)math.round(normalized * 255f), 0, 255);
        }

        private sealed class OwnerDirectedSurfaceResolverStub : IVoxelSonarSdfReadModel, IVoxelSonarSdfSurfaceResolver
        {
            public int SurfaceResolveCalls;
            public int NearestPayloadReadCalls;

            public bool TryResolveNearestSonarSdfSurface(
                float3 runtimeOrigin,
                float3 runtimeDirection,
                float maxDistance,
                float stepMeters,
                out VoxelSonarSdfRaycastHit hit,
                out NativeArray<byte>.ReadOnly encodedSdf,
                out int3 gridDimensions,
                out float3 volumeOrigin,
                out float3 cellSize,
                out float sdfRange)
            {
                SurfaceResolveCalls++;
                encodedSdf = default;
                gridDimensions = default;
                volumeOrigin = default;
                cellSize = default;
                sdfRange = 0f;
                hit = new VoxelSonarSdfRaycastHit
                {
                    Point = runtimeOrigin + math.normalize(runtimeDirection) * 4f,
                    Normal = new float3(1f, 0f, 0f),
                    Distance = 4f,
                    Flags = VoxelSonarSdfRaycastHit.FlagHit
                };
                return true;
            }

            public bool TryReadNearestSonarSdf(
                float3 runtimeOrigin,
                out NativeArray<byte>.ReadOnly encodedSdf,
                out int3 gridDimensions,
                out float3 volumeOrigin,
                out float3 cellSize,
                out float sdfRange)
            {
                NearestPayloadReadCalls++;
                encodedSdf = default;
                gridDimensions = default;
                volumeOrigin = default;
                cellSize = default;
                sdfRange = 0f;
                return false;
            }

            public bool TryRaymarchNearestSonarSdf(
                float3 runtimeOrigin,
                float3 runtimeDirection,
                float maxDistance,
                float stepMeters,
                out VoxelSonarSdfRaycastHit hit,
                out NativeArray<byte>.ReadOnly encodedSdf,
                out int3 gridDimensions,
                out float3 volumeOrigin,
                out float3 cellSize,
                out float sdfRange)
            {
                Assert.Fail("VoxelSonarSdfMath must not call the legacy raymarch bridge when the owner-directed resolver is present.");
                hit = default;
                encodedSdf = default;
                gridDimensions = default;
                volumeOrigin = default;
                cellSize = default;
                sdfRange = 0f;
                return false;
            }

            public bool TrySampleNearestSonarSdf(float3 runtimePosition, out float density, out float density01)
            {
                density = 0f;
                density01 = 0f;
                return false;
            }
        }
    }
}
#endif
