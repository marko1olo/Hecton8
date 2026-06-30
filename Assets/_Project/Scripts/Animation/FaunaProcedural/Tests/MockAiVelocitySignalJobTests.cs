using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Hecton8.Animation.FaunaProcedural;

namespace Hecton8.Animation.FaunaProcedural.Tests
{
    [TestFixture]
    public class MockAiVelocitySignalJobTests
    {
        [Test]
        public void Test_Execute_HappyPath()
        {
            var signals = new NativeArray<MockAiVelocitySignal>(1, Allocator.TempJob);
            try
            {
                signals[0] = new MockAiVelocitySignal { EntityHash = 12345 };

                var job = new MockAiVelocitySignalJob
                {
                    Signals = signals,
                    SectorHash = 67890,
                    SimulationFrame = 1,
                    GlobalQualityWeight = 1.0f
                };

                job.Execute(0);

                var result = signals[0];
                Assert.That(result.Weight01, Is.EqualTo(1.0f));
                Assert.That(result.EntityHash, Is.EqualTo(12345u));
                Assert.That(result.SectorHash, Is.EqualTo(67890u));
                Assert.That(result.SimulationFrame, Is.EqualTo(1u));
                Assert.That(result.Flags, Is.EqualTo(ProceduralBoneBlenderConstants.TelemetryFlagMockSignal));
                Assert.That(result.SpeedHint >= 1.0f && result.SpeedHint <= 8.0f, Is.True);
            }
            finally
            {
                signals.Dispose();
            }
        }

        [Test]
        public void Test_Execute_OutOfBounds()
        {
            var signals = new NativeArray<MockAiVelocitySignal>(1, Allocator.TempJob);
            try
            {
                signals[0] = new MockAiVelocitySignal { EntityHash = 12345 };

                var job = new MockAiVelocitySignalJob
                {
                    Signals = signals,
                    SectorHash = 67890,
                    SimulationFrame = 1,
                    GlobalQualityWeight = 1.0f
                };

                job.Execute(1);

                var result = signals[0];
                Assert.That(result.Weight01, Is.EqualTo(0.0f));
                Assert.That(result.EntityHash, Is.EqualTo(12345u));
                Assert.That(result.SectorHash, Is.EqualTo(0u));
                Assert.That(result.SimulationFrame, Is.EqualTo(0u));
                Assert.That(result.Flags, Is.EqualTo(0u));
            }
            finally
            {
                signals.Dispose();
            }
        }

        [Test]
        public void Test_Execute_ZeroEntityHash()
        {
            var signals = new NativeArray<MockAiVelocitySignal>(1, Allocator.TempJob);
            try
            {
                signals[0] = new MockAiVelocitySignal { EntityHash = 0 };

                var job = new MockAiVelocitySignalJob
                {
                    Signals = signals,
                    SectorHash = 67890,
                    SimulationFrame = 1,
                    GlobalQualityWeight = 1.0f
                };

                job.Execute(0);

                var result = signals[0];
                uint expectedHash = 1 * 0x9E3779B9u;
                Assert.That(result.EntityHash, Is.EqualTo(expectedHash));
                Assert.That(result.SectorHash, Is.EqualTo(67890u));
                Assert.That(result.SimulationFrame, Is.EqualTo(1u));
                Assert.That(result.Flags, Is.EqualTo(ProceduralBoneBlenderConstants.TelemetryFlagMockSignal));
            }
            finally
            {
                signals.Dispose();
            }
        }

        [Test]
        public void Test_Execute_ZeroGlobalQualityWeight()
        {
            var signals = new NativeArray<MockAiVelocitySignal>(1, Allocator.TempJob);
            try
            {
                signals[0] = new MockAiVelocitySignal { EntityHash = 12345 };

                var job = new MockAiVelocitySignalJob
                {
                    Signals = signals,
                    SectorHash = 67890,
                    SimulationFrame = 1,
                    GlobalQualityWeight = 0.0f
                };

                job.Execute(0);

                var result = signals[0];
                Assert.That(result.Weight01, Is.EqualTo(0.0f));
                Assert.That(result.EntityHash, Is.EqualTo(12345u));
                Assert.That(result.SectorHash, Is.EqualTo(67890u));
                Assert.That(result.SimulationFrame, Is.EqualTo(1u));
                Assert.That(result.Flags, Is.EqualTo(ProceduralBoneBlenderConstants.TelemetryFlagMockSignal));
            }
            finally
            {
                signals.Dispose();
            }
        }

        [Test]
        public void Test_Execute_UninitializedSignals()
        {
            var signals = new NativeArray<MockAiVelocitySignal>();

            var job = new MockAiVelocitySignalJob
            {
                Signals = signals,
                SectorHash = 67890,
                SimulationFrame = 1,
                GlobalQualityWeight = 1.0f
            };

            Assert.DoesNotThrow(() => job.Execute(0));
        }
    }
}
