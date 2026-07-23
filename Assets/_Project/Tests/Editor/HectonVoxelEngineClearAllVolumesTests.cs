using NUnit.Framework;
using UnityEngine;
using System.Reflection;

namespace Hecton8.Tests
{
    public class HectonVoxelEngineClearAllVolumesTests
    {
        [Test]
        public void ClearAllVolumes_WhenCalled_SetsActiveVolumeCountToZero()
        {
            // Arrange
            var engineObject = new GameObject("Engine");
            var engine = engineObject.AddComponent<HectonVoxelEngine>();

            // Access the private _activeVolumes list using Reflection
            var activeVolumesField = typeof(HectonVoxelEngine).GetField("_activeVolumes", BindingFlags.NonPublic | BindingFlags.Instance);
            var activeVolumesList = (System.Collections.Generic.List<GameObject>)activeVolumesField.GetValue(engine);

            // Add a mock active volume
            var mockVolume = new GameObject("MockVolume");
            activeVolumesList.Add(mockVolume);

            // Access the private _activeVolumeComponents list using Reflection
            var activeVolumeComponentsField = typeof(HectonVoxelEngine).GetField("_activeVolumeComponents", BindingFlags.NonPublic | BindingFlags.Instance);
            var activeVolumeComponentsList = (System.Collections.Generic.List<HectonVoxelVolume>)activeVolumeComponentsField.GetValue(engine);
            activeVolumeComponentsList.Add(mockVolume.AddComponent<HectonVoxelVolume>());

            Assert.AreEqual(1, engine.ActiveVolumeCount, "Active volume count should be 1 after arrangement.");

            // Act
            // Since this is EditMode test, Destroy is called within ClearAllVolumes but doesn't immediately delete objects.
            // We just want to test that the list is cleared and ActiveVolumeCount is 0.
            engine.ClearAllVolumes();

            // Assert
            Assert.AreEqual(0, engine.ActiveVolumeCount, "Active volume count should be 0 after calling ClearAllVolumes.");

            // Clean up using DestroyImmediate for EditMode
            GameObject.DestroyImmediate(engineObject);
            if (mockVolume != null) {
                GameObject.DestroyImmediate(mockVolume);
            }
        }
    }
}
