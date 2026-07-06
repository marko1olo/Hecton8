using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hecton8.Physics;
using Hecton8.Core;
using Unity.Mathematics;
using Hecton8.Core.Contracts;
using Unity.Collections;

namespace Hecton8.Tests.Physics
{
    public class BuoyancyObjectGroundResolutionTests
    {
        private GameObject _go;
        private BuoyancyObject _buoyancyObject;
        private Rigidbody _rb;
        private MockTerrainProvider _mockTerrain;
        private MockVoxelSdfReadModel _mockVoxelSdf;

        private class MockTerrainProvider : ITerrainProvider
        {
            public bool IsAvailable { get; set; }
            public float WaterSurfaceLevel => 0f;

            public float Height { get; set; } = 0f;
            public Vector3 Normal { get; set; } = Vector3.up;

            public bool Initialize() => true;
            public void Shutdown() {}
            public void Dispose() {}

            public bool TryGetTerrainArtifactIdentity(out TerrainArtifactIdentityDTO identity)
            {
                identity = default;
                return false;
            }

            public bool TryGetHeight(float x, float z, out float height)
            {
                height = Height;
                return true;
            }

            public bool TryGetNormal(float x, float z, float normalStepMeters, out Vector3 normal)
            {
                normal = Normal;
                return true;
            }
        }

        private class MockVoxelSdfReadModel : IVoxelSonarSdfReadModel
        {
            public bool ReturnHit { get; set; } = false;
            public VoxelSonarSdfRaycastHit Hit { get; set; }

            public bool TryReadNearestSonarSdf(float3 runtimeOrigin, out NativeArray<byte>.ReadOnly encodedSdf, out int3 gridDimensions, out float3 volumeOrigin, out float3 cellSize, out float sdfRange)
            {
                encodedSdf = default; gridDimensions = default; volumeOrigin = default; cellSize = default; sdfRange = default;
                return false;
            }

            public bool TryRaymarchNearestSonarSdf(float3 runtimeOrigin, float3 runtimeDirection, float maxDistance, float stepMeters, out VoxelSonarSdfRaycastHit hit, out NativeArray<byte>.ReadOnly encodedSdf, out int3 gridDimensions, out float3 volumeOrigin, out float3 cellSize, out float sdfRange)
            {
                hit = Hit;
                encodedSdf = default; gridDimensions = default; volumeOrigin = default; cellSize = default; sdfRange = default;
                return ReturnHit;
            }
        }

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("BuoyancyTestObject");
            _rb = _go.AddComponent<Rigidbody>();
            _buoyancyObject = _go.AddComponent<BuoyancyObject>();

            _mockTerrain = new MockTerrainProvider();
            _mockVoxelSdf = new MockVoxelSdfReadModel();

            // Set private fields via reflection
            typeof(BuoyancyObject).GetField("_terrainProvider", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_buoyancyObject, _mockTerrain);
            typeof(BuoyancyObject).GetField("_voxelSdfReadModel", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_buoyancyObject, _mockVoxelSdf);
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(_go);
        }

        private bool InvokeTryResolveCachedGroundHit(Vector3 origin, float range, int layerMask, out KinematicSurfaceHit hit)
        {
            MethodInfo method = typeof(BuoyancyObject).GetMethod("TryResolveCachedGroundHit", BindingFlags.NonPublic | BindingFlags.Instance);
            object[] args = new object[] { origin, range, layerMask, null };
            bool result = (bool)method.Invoke(_buoyancyObject, args);
            hit = (KinematicSurfaceHit)args[3];
            return result;
        }

        [Test]
        public void TryResolveCachedGroundHit_InvalidOriginOrRange_ReturnsFalse()
        {
            KinematicSurfaceHit hit;
            bool result1 = InvokeTryResolveCachedGroundHit(new Vector3(float.NaN, 0, 0), 10f, -1, out hit);
            Assert.IsFalse(result1);

            bool result2 = InvokeTryResolveCachedGroundHit(Vector3.zero, float.NaN, -1, out hit);
            Assert.IsFalse(result2);

            bool result3 = InvokeTryResolveCachedGroundHit(Vector3.zero, -1f, -1, out hit);
            Assert.IsFalse(result3);
        }

        [Test]
        public void TryResolveTerrainGroundHit_TerrainAvailable_ReturnsTrueAndPopulatesHit()
        {
            _mockTerrain.IsAvailable = true;
            _mockTerrain.Height = -5f; // Origin is 0, height is -5, distance is 5
            _mockTerrain.Normal = new Vector3(0, 1, 0);

            KinematicSurfaceHit hit;
            bool result = InvokeTryResolveCachedGroundHit(Vector3.zero, 10f, HectonLayerMasks.TerrainLayerMask, out hit);

            Assert.IsTrue(result);
            Assert.AreEqual(5f, hit.distance);
            Assert.AreEqual(new Vector3(0, -5f, 0), hit.point);
            Assert.AreEqual(new Vector3(0, 1, 0), hit.normal);
        }

        [Test]
        public void TryResolveTerrainGroundHit_TerrainNotAvailable_ReturnsFalse()
        {
            _mockTerrain.IsAvailable = false;

            KinematicSurfaceHit hit;
            bool result = InvokeTryResolveCachedGroundHit(Vector3.zero, 10f, HectonLayerMasks.TerrainLayerMask, out hit);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryResolveTerrainGroundHit_TerrainHeightOutOfRange_ReturnsFalse()
        {
            _mockTerrain.IsAvailable = true;
            _mockTerrain.Height = -15f; // Distance is 15, range is 10

            KinematicSurfaceHit hit;
            bool result = InvokeTryResolveCachedGroundHit(Vector3.zero, 10f, HectonLayerMasks.TerrainLayerMask, out hit);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryResolveTerrainGroundHit_TerrainHeightAboveOrigin_ReturnsFalse()
        {
            _mockTerrain.IsAvailable = true;
            _mockTerrain.Height = 5f; // Origin 0, height 5 -> distance -5 (invalid)

            KinematicSurfaceHit hit;
            bool result = InvokeTryResolveCachedGroundHit(Vector3.zero, 10f, HectonLayerMasks.TerrainLayerMask, out hit);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryResolveVoxelGroundHit_VoxelHitValid_ReturnsTrueAndPopulatesHit()
        {
            _mockTerrain.IsAvailable = false; // Fallthrough to voxel

            _mockVoxelSdf.ReturnHit = true;
            VoxelSonarSdfRaycastHit sdfHit = new VoxelSonarSdfRaycastHit
            {
                Flags = VoxelSonarSdfRaycastHit.FlagHit,
                Point = new float3(0, -3f, 0),
                Normal = new float3(0, 1, 0),
                Distance = 3f
            };
            _mockVoxelSdf.Hit = sdfHit;

            KinematicSurfaceHit hit;
            bool result = InvokeTryResolveCachedGroundHit(Vector3.zero, 10f, HectonLayerMasks.VoxelCaveLayerMask, out hit);

            Assert.IsTrue(result);
            Assert.AreEqual(3f, hit.distance);
            Assert.AreEqual(new Vector3(0, -3f, 0), hit.point);
            Assert.AreEqual(new Vector3(0, 1, 0), hit.normal);
        }

        [Test]
        public void TryResolveVoxelGroundHit_VoxelHitOutOfRange_ReturnsFalse()
        {
            _mockTerrain.IsAvailable = false;

            _mockVoxelSdf.ReturnHit = true;
            VoxelSonarSdfRaycastHit sdfHit = new VoxelSonarSdfRaycastHit
            {
                Flags = VoxelSonarSdfRaycastHit.FlagHit,
                Point = new float3(0, -15f, 0),
                Normal = new float3(0, 1, 0),
                Distance = 15f
            };
            _mockVoxelSdf.Hit = sdfHit;

            KinematicSurfaceHit hit;
            bool result = InvokeTryResolveCachedGroundHit(Vector3.zero, 10f, HectonLayerMasks.VoxelCaveLayerMask, out hit);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryResolveVoxelGroundHit_VoxelReturnsNoHitFlag_ReturnsFalse()
        {
            _mockTerrain.IsAvailable = false;

            _mockVoxelSdf.ReturnHit = true; // TryRaymarch returns true but flags don't have FlagHit
            VoxelSonarSdfRaycastHit sdfHit = new VoxelSonarSdfRaycastHit
            {
                Flags = 0, // Missing FlagHit
                Point = new float3(0, -3f, 0),
                Normal = new float3(0, 1, 0),
                Distance = 3f
            };
            _mockVoxelSdf.Hit = sdfHit;

            KinematicSurfaceHit hit;
            bool result = InvokeTryResolveCachedGroundHit(Vector3.zero, 10f, HectonLayerMasks.VoxelCaveLayerMask, out hit);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryResolveCachedGroundHit_WrongLayerMask_ReturnsFalse()
        {
            _mockTerrain.IsAvailable = true;
            _mockTerrain.Height = -5f;
            _mockTerrain.Normal = new Vector3(0, 1, 0);

            _mockVoxelSdf.ReturnHit = true;
            _mockVoxelSdf.Hit = new VoxelSonarSdfRaycastHit
            {
                Flags = VoxelSonarSdfRaycastHit.FlagHit,
                Point = new float3(0, -3f, 0),
                Normal = new float3(0, 1, 0),
                Distance = 3f
            };

            // Use a mask that doesn't include Terrain or VoxelCave/VoxelProxy
            int wrongMask = HectonLayerMasks.WaterLayerMask; // Assumed 4

            KinematicSurfaceHit hit;
            bool result = InvokeTryResolveCachedGroundHit(Vector3.zero, 10f, wrongMask, out hit);

            Assert.IsFalse(result);
        }
    }
}
