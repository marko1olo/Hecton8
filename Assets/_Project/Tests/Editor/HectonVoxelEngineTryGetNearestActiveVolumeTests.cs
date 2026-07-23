using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using Unity.Mathematics;
using Hecton8.Caves;
using Hecton8.World;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    public class HectonVoxelEngineTryGetNearestActiveVolumeTests
    {
        private GameObject _engineObject;
        private HectonVoxelEngine _engine;
        private List<GameObject> _mockVolumes;

        // HectonFloatingOrigin instance for modifying _totalOffsetDouble
        private GameObject _floatingOriginObject;
        private HectonFloatingOrigin _floatingOrigin;

        [SetUp]
        public void SetUp()
        {
            _engineObject = new GameObject("Engine");
            _engine = _engineObject.AddComponent<HectonVoxelEngine>();
            _mockVolumes = new List<GameObject>();

            _floatingOriginObject = new GameObject("FloatingOrigin");
            _floatingOrigin = _floatingOriginObject.AddComponent<HectonFloatingOrigin>();

            // Set as active runtime using reflection if needed
            var activeRuntimeField = typeof(HectonFloatingOrigin).GetField("s_activeRuntime", BindingFlags.NonPublic | BindingFlags.Static);
            if (activeRuntimeField != null)
            {
                activeRuntimeField.SetValue(null, _floatingOrigin);
            }

            SetOriginOffset(new double3(0, 0, 0));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var vol in _mockVolumes)
            {
                if (vol != null)
                {
                    GameObject.DestroyImmediate(vol);
                }
            }
            if (_engineObject != null)
            {
                GameObject.DestroyImmediate(_engineObject);
            }
            if (_floatingOriginObject != null)
            {
                GameObject.DestroyImmediate(_floatingOriginObject);
            }

            var activeRuntimeField = typeof(HectonFloatingOrigin).GetField("s_activeRuntime", BindingFlags.NonPublic | BindingFlags.Static);
            if (activeRuntimeField != null)
            {
                activeRuntimeField.SetValue(null, null);
            }
        }

        private void SetOriginOffset(double3 offset)
        {
            var offsetField = typeof(HectonFloatingOrigin).GetField("_totalOffsetDouble", BindingFlags.NonPublic | BindingFlags.Instance);
            if (offsetField != null)
            {
                offsetField.SetValue(_floatingOrigin, offset);
            }
        }

        private HectonVoxelVolume CreateActiveVolume(double3 aupPos, bool hasRuntimeData = true, VoxelBakeState bakeState = VoxelBakeState.Complete)
        {
            var volumeObject = new GameObject("MockVolume");
            _mockVolumes.Add(volumeObject);

            var volume = volumeObject.AddComponent<HectonVoxelVolume>();

            var posField = typeof(HectonVoxelVolume).GetField("_generationAbsoluteUniversePositionDouble", BindingFlags.NonPublic | BindingFlags.Instance);
            posField.SetValue(volume, aupPos);

            var readyField = typeof(HectonVoxelVolume).GetField("_runtimeDataReady", BindingFlags.NonPublic | BindingFlags.Instance);
            readyField.SetValue(volume, hasRuntimeData);

            var bakeField = typeof(HectonVoxelVolume).GetField("_bakeState", BindingFlags.NonPublic | BindingFlags.Instance);
            bakeField.SetValue(volume, bakeState);

            var activeVolumesField = typeof(HectonVoxelEngine).GetField("_activeVolumes", BindingFlags.NonPublic | BindingFlags.Instance);
            var activeVolumes = (List<GameObject>)activeVolumesField.GetValue(_engine);
            activeVolumes.Add(volumeObject);

            var activeVolumeComponentsField = typeof(HectonVoxelEngine).GetField("_activeVolumeComponents", BindingFlags.NonPublic | BindingFlags.Instance);
            var activeVolumeComponents = (List<HectonVoxelVolume>)activeVolumeComponentsField.GetValue(_engine);
            activeVolumeComponents.Add(volume);

            return volume;
        }

        [Test]
        public void TryGetNearestActiveVolume_WithValidVolumes_ReturnsNearest()
        {
            // Arrange
            var queryPos = new Vector3(0, 0, 0);

            var farVolume = CreateActiveVolume(new double3(100, 0, 0));
            var nearVolume = CreateActiveVolume(new double3(10, 0, 0));
            var veryFarVolume = CreateActiveVolume(new double3(1000, 0, 0));

            // Act
            bool result = _engine.TryGetNearestActiveVolume(queryPos, out HectonVoxelVolume nearestVolume);

            // Assert
            Assert.IsTrue(result);
            Assert.IsNotNull(nearestVolume);
            Assert.AreEqual(nearVolume, nearestVolume);
        }

        [Test]
        public void TryGetNearestActiveVolume_WithNoVolumes_ReturnsFalse()
        {
            // Arrange
            var queryPos = new Vector3(0, 0, 0);

            // Act
            bool result = _engine.TryGetNearestActiveVolume(queryPos, out HectonVoxelVolume nearestVolume);

            // Assert
            Assert.IsFalse(result);
            Assert.IsNull(nearestVolume);
        }

        [Test]
        public void TryGetNearestActiveVolume_IgnoresInvalidVolumes()
        {
            // Arrange
            var queryPos = new Vector3(0, 0, 0);

            // Create a near volume, but without runtime data
            CreateActiveVolume(new double3(10, 0, 0), hasRuntimeData: false);

            // Create a near volume, but with incomplete bake state
            CreateActiveVolume(new double3(20, 0, 0), bakeState: VoxelBakeState.Baking);

            // Create a valid far volume
            var farValidVolume = CreateActiveVolume(new double3(100, 0, 0));

            // Act
            bool result = _engine.TryGetNearestActiveVolume(queryPos, out HectonVoxelVolume nearestVolume);

            // Assert
            Assert.IsTrue(result);
            Assert.IsNotNull(nearestVolume);
            Assert.AreEqual(farValidVolume, nearestVolume); // Should skip the near ones because they are invalid
        }

        [Test]
        public void TryGetNearestActiveVolume_WhenTryResolveRuntimeAupFails_ReturnsFalse()
        {
            // Arrange
            // Set floating origin to NaN to make TryResolveRuntimeAup fail
            SetOriginOffset(new double3(double.NaN, double.NaN, double.NaN));

            CreateActiveVolume(new double3(10, 0, 0));
            var queryPos = new Vector3(0, 0, 0);

            // Act
            bool result = _engine.TryGetNearestActiveVolume(queryPos, out HectonVoxelVolume nearestVolume);

            // Assert
            Assert.IsFalse(result);
            Assert.IsNull(nearestVolume);
        }
    }
}
