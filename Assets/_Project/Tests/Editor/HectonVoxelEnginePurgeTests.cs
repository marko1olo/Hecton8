using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

namespace Hecton8.Tests
{
    public class HectonVoxelEnginePurgeTests
    {
        [Test]
        public void PurgeNullVolumes_RemovesNullsAndUpdatesCount()
        {
            var go = new GameObject("Engine");
            var engine = go.AddComponent<HectonVoxelEngine>();

            FieldInfo activeVolumesField = typeof(HectonVoxelEngine).GetField("_activeVolumes", BindingFlags.Instance | BindingFlags.NonPublic);
            var activeVolumes = activeVolumesField.GetValue(engine) as List<GameObject>;

            var go1 = new GameObject("Vol1");
            var go2 = new GameObject("Vol2");
            var go3 = new GameObject("Vol3");

            activeVolumes.Add(go1);
            activeVolumes.Add(null);
            activeVolumes.Add(go2);
            activeVolumes.Add(null);
            activeVolumes.Add(go3);

            FieldInfo activeVolumeComponentsField = typeof(HectonVoxelEngine).GetField("_activeVolumeComponents", BindingFlags.Instance | BindingFlags.NonPublic);
            var activeVolumeComponents = activeVolumeComponentsField.GetValue(engine) as List<HectonVoxelVolume>;
            activeVolumeComponents.Add(go1.AddComponent<HectonVoxelVolume>());
            activeVolumeComponents.Add(null);
            activeVolumeComponents.Add(go2.AddComponent<HectonVoxelVolume>());
            activeVolumeComponents.Add(null);
            activeVolumeComponents.Add(go3.AddComponent<HectonVoxelVolume>());

            Assert.AreEqual(5, activeVolumes.Count);

            engine.PurgeNullVolumes();

            Assert.AreEqual(3, activeVolumes.Count);
            Assert.IsTrue(activeVolumes.Contains(go1));
            Assert.IsTrue(activeVolumes.Contains(go2));
            Assert.IsTrue(activeVolumes.Contains(go3));
            Assert.IsFalse(activeVolumes.Contains(null));

            GameObject.DestroyImmediate(go);
            GameObject.DestroyImmediate(go1);
            GameObject.DestroyImmediate(go2);
            GameObject.DestroyImmediate(go3);
        }
    }
}
