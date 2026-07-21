using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

namespace Hecton8.Tests.Editor
{
    public class HectonVoxelEngineTests
    {
        [Test]
        public void DespawnVolume_RemovesFromActiveListAndDestroysVolume()
        {
            var engineObject = new GameObject("Engine");
            var engine = engineObject.AddComponent<HectonVoxelEngine>();

            var volumeObject = new GameObject("Volume");
            var voxelVolume = volumeObject.AddComponent<Hecton8.Caves.HectonVoxelVolume>();

            // Access the underlying lists via reflection
            var activeVolumesField = typeof(HectonVoxelEngine).GetField("_activeVolumes", BindingFlags.NonPublic | BindingFlags.Instance);
            var activeVolumes = (List<GameObject>)activeVolumesField.GetValue(engine);

            var activeVolumeComponentsField = typeof(HectonVoxelEngine).GetField("_activeVolumeComponents", BindingFlags.NonPublic | BindingFlags.Instance);
            var activeVolumeComponents = (List<Hecton8.Caves.HectonVoxelVolume>)activeVolumeComponentsField.GetValue(engine);

            // Directly inject the state to simulate an active registered volume
            activeVolumes.Add(volumeObject);
            activeVolumeComponents.Add(voxelVolume);

            Assert.AreEqual(1, activeVolumes.Count, "Volume was not injected properly into active list.");
            Assert.AreEqual(1, activeVolumeComponents.Count, "Component was not injected properly into active list.");

            // Action
            engine.DespawnVolume(volumeObject);

            // Assert
            Assert.AreEqual(0, activeVolumes.Count, "Volume was not removed from active list.");
            Assert.AreEqual(0, activeVolumeComponents.Count, "Component was not removed from active list.");

            // Note: Since we changed engine to use SafeDestroy, EditMode tests will now use DestroyImmediate!
            Assert.IsTrue(volumeObject == null, "Volume object was not destroyed.");
        }
    }
}
