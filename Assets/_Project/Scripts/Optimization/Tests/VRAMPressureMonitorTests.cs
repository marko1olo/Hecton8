using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Optimization;
using Hecton8.Core;

namespace Hecton8.Optimization.Tests
{
    public class VRAMPressureMonitorTests
    {
        [Test]
        public void SlowTick_ChecksFrameAndSamples()
        {
            var go = new GameObject("VRAMMonitor");
            var monitor = go.AddComponent<VRAMPressureMonitor>();

            var forceSampleField = typeof(VRAMPressureMonitor).GetField("_forceSampleQueued", BindingFlags.NonPublic | BindingFlags.Instance);
            forceSampleField.SetValue(monitor, true);

            monitor.SlowTick();

            Assert.IsFalse((bool)forceSampleField.GetValue(monitor), "forceSampleQueued should be cleared");

            var hasSampleProp = typeof(VRAMPressureMonitor).GetProperty("HasSample", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsTrue((bool)hasSampleProp.GetValue(monitor), "HasSample should be set after ticking");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void SlowTick_IgnoresSampleIfNotQueuedAndFrameNotReached()
        {
            var go = new GameObject("VRAMMonitor");
            var monitor = go.AddComponent<VRAMPressureMonitor>();

            var forceSampleField = typeof(VRAMPressureMonitor).GetField("_forceSampleQueued", BindingFlags.NonPublic | BindingFlags.Instance);
            forceSampleField.SetValue(monitor, false);

            var nextSampleFrameField = typeof(VRAMPressureMonitor).GetField("_nextSampleFrame", BindingFlags.NonPublic | BindingFlags.Instance);
            nextSampleFrameField.SetValue(monitor, int.MaxValue); // Make sure frame is not reached

            monitor.SlowTick();

            var hasSampleProp = typeof(VRAMPressureMonitor).GetProperty("HasSample", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsFalse((bool)hasSampleProp.GetValue(monitor), "HasSample should NOT be set because sample frame not reached");

            Object.DestroyImmediate(go);
        }
    }
}
