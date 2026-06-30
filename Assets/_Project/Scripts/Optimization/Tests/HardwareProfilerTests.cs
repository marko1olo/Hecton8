using NUnit.Framework;
using System;
using System.Reflection;
using Hecton8.Optimization;

namespace Hecton8.Optimization.Tests
{
    [TestFixture]
    public class HardwareProfilerTests
    {
        [Test]
        public void CaptureSystemInfoSnapshot_ValidatesDefaultValues()
        {
            var snapshot = HardwareProfiler.CaptureSystemInfoSnapshot();

            Assert.That(snapshot.GraphicsMemoryMegabytes, Is.GreaterThanOrEqualTo(0));
            Assert.That(snapshot.SystemMemoryMegabytes, Is.GreaterThanOrEqualTo(0));
            Assert.That(snapshot.ProcessorCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(snapshot.HardwareScore, Is.InRange(0, 100));
            Assert.That(snapshot.StartupSurvivalPressureByte, Is.InRange((byte)0, (byte)255));
        }

        [Test]
        public void ResolveHardwareScore_LowEnd_ReturnsCorrectScore()
        {
            var method = typeof(HardwareProfiler).GetMethod("ResolveHardwareScore", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            // Low end: VRAM < 1800 (+5), CPU < 6 (+8), RAM < 8000 (+4) = 17
            int score = (int)method.Invoke(null, new object[] { 1024, 4096, 4 });
            Assert.That(score, Is.EqualTo(17));
        }

        [Test]
        public void ResolveHardwareScore_HighEnd_ReturnsMaxScore()
        {
            var method = typeof(HardwareProfiler).GetMethod("ResolveHardwareScore", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            // High end: VRAM >= 8200 (+50), CPU >= 12 (+30), RAM >= 32000 (+20) = 100
            int score = (int)method.Invoke(null, new object[] { 8500, 32768, 16 });
            Assert.That(score, Is.EqualTo(100));
        }

        [Test]
        public void ResolveHardwareScore_ExtremeEnd_CapsAt100()
        {
            var method = typeof(HardwareProfiler).GetMethod("ResolveHardwareScore", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            int score = (int)method.Invoke(null, new object[] { 24000, 64000, 64 });
            Assert.That(score, Is.EqualTo(100));
        }

        [Test]
        public void ResolveSystemInfoSurvivalPressure01_LowEnd_HighPressure()
        {
            var method = typeof(HardwareProfiler).GetMethod("ResolveSystemInfoSurvivalPressure01", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            float pressure = (float)method.Invoke(null, new object[] { 1500, 2 });
            Assert.That(pressure, Is.EqualTo(0.6666667f).Within(0.001f));
        }

        [Test]
        public void ResolveSystemInfoSurvivalPressure01_HighEnd_LowPressure()
        {
            var method = typeof(HardwareProfiler).GetMethod("ResolveSystemInfoSurvivalPressure01", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            float pressure = (float)method.Invoke(null, new object[] { 8000, 12 });
            Assert.That(pressure, Is.EqualTo(0.0f).Within(0.001f));
        }

        [Test]
        public void Snapshot_ClampsPressureCorrectly()
        {
            var snapshot1 = new HardwareProfiler.HardwareProfilerSnapshot(1000, 1000, 1, 10, 0.5f);
            Assert.That(snapshot1.StartupSurvivalPressureByte, Is.EqualTo(128));

            var snapshot2 = new HardwareProfiler.HardwareProfilerSnapshot(1000, 1000, 1, 10, 1.5f);
            Assert.That(snapshot2.StartupSurvivalPressureByte, Is.EqualTo(255));

            var snapshot3 = new HardwareProfiler.HardwareProfilerSnapshot(1000, 1000, 1, 10, -0.5f);
            Assert.That(snapshot3.StartupSurvivalPressureByte, Is.EqualTo(0));
        }
    }
}
