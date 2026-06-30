using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Unity.Collections;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Optimization;

namespace Hecton8.Optimization.Tests
{
    [TestFixture]
    public class VRAMMonitorTests
    {
        private GameObject _go;
        private VRAMMonitor _monitor;

        private class MockDataVault : IDataVault
        {
            public bool LockReleased { get; private set; }
            public NativeArray<VramTelemetryEntry> MockRing;

            public bool TryAcquireWriteLock<T>(in VaultGenerationHandle<T> handle, SystemID owner, out NativeArray<T> data) where T : struct
            {
                data = (NativeArray<T>)(object)MockRing;
                return true;
            }

            public void ReleaseWriteLock<T>(in VaultGenerationHandle<T> handle, SystemID owner) where T : struct
            {
                LockReleased = true;
            }

            public void Dispose() {}
            public bool IsAllocationLocked => false;
            public bool IsCompactionFenceActive => false;
            public VaultGenerationHandle<T> EnsureGenerationHandle<T>(BufferID bufferId, int capacity, SystemID owner, NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : struct => default;
            public void ReleaseBuffer<T>(in VaultGenerationHandle<T> handle) where T : struct {}
            public bool TryAcquireReadLock<T>(in VaultGenerationHandle<T> handle, SystemID reader, out NativeArray<T> data) where T : struct { data = default; return false; }
            public void ReleaseReadLock<T>(in VaultGenerationHandle<T> handle, SystemID reader) where T : struct {}
            public NativeArray<VaultBufferMeta> GetMacroDatabaseMetadataUnsafe() => default;
            public NativeArray<byte> GetMacroDatabaseDataUnsafe() => default;
            public uint MacroDatabaseVersion => 0;
            public VaultDatabaseMeta GetMacroDatabaseParameters() => default;
        }

        private class TestableVRAMMonitor : VRAMMonitor
        {
            protected override uint ResolveTelemetryFlags()
            {
                throw new InvalidOperationException("Simulated exception during write");
            }
        }

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("VRAMMonitorTest");
            _monitor = _go.AddComponent<VRAMMonitor>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void SlowTick_ExecutesMeasureVRAMAndCheckThresholds()
        {
            _monitor.SlowTick();

            Assert.That(_monitor.PressureState, Is.EqualTo(VRAMMonitor.VRAMPressureState.Stable));
            Assert.That(_monitor.PressureStateCode, Is.EqualTo(0));

            _monitor.GetVRAMBreakdown(out long tex, out long rt, out long total);
            Assert.That(tex, Is.EqualTo(0));
            Assert.That(rt, Is.EqualTo(0));
            Assert.That(total, Is.EqualTo(0));
        }

        [Test]
        public void CheckThresholds_LogsWarning_WhenOverBudget()
        {
            // Set the memory values to simulate over-budget using reflection
            var texProp = typeof(VRAMMonitor).GetProperty("TextureMemoryBytes", BindingFlags.Public | BindingFlags.Instance);
            texProp.DeclaringType.GetProperty("TextureMemoryBytes").SetValue(_monitor, 1000L, null);

            var budgetField = typeof(VRAMMonitor).GetField("_budgetThresholds", BindingFlags.NonPublic | BindingFlags.Instance);
            var budget = new VRAMBudgetThresholds {
                TextureMemoryBudgetBytes = 500L,
                RenderTextureMemoryBudgetBytes = 500L,
                TotalVRAMBudgetBytes = 500L
            };
            budgetField.SetValue(_monitor, budget);

            // Call CheckThresholds using reflection
            var checkMethod = typeof(VRAMMonitor).GetMethod("CheckThresholds", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.DoesNotThrow(() => checkMethod.Invoke(_monitor, null));
        }

        [Test]
        public void UpdatePressureState_Critical_WhenOverBudget()
        {
            var budgetField = typeof(VRAMMonitor).GetField("_budgetThresholds", BindingFlags.NonPublic | BindingFlags.Instance);
            var budget = new VRAMBudgetThresholds {
                TextureMemoryBudgetBytes = 500L,
                RenderTextureMemoryBudgetBytes = 500L,
                TotalVRAMBudgetBytes = 500L
            };
            budgetField.SetValue(_monitor, budget);

            var texProp = typeof(VRAMMonitor).GetProperty("TextureMemoryBytes", BindingFlags.Public | BindingFlags.Instance);
            texProp.DeclaringType.GetProperty("TextureMemoryBytes").SetValue(_monitor, 1000L, null);

            var updateMethod = typeof(VRAMMonitor).GetMethod("UpdatePressureState", BindingFlags.NonPublic | BindingFlags.Instance);
            updateMethod.Invoke(_monitor, null);

            Assert.That(_monitor.PressureState, Is.EqualTo(VRAMMonitor.VRAMPressureState.Critical));
        }

        [Test]
        public void UpdatePressureState_Warning_WhenNearBudget()
        {
            var budgetField = typeof(VRAMMonitor).GetField("_budgetThresholds", BindingFlags.NonPublic | BindingFlags.Instance);
            var budget = new VRAMBudgetThresholds {
                TextureMemoryBudgetBytes = 1000L,
                RenderTextureMemoryBudgetBytes = 1000L,
                TotalVRAMBudgetBytes = 1000L
            };
            budgetField.SetValue(_monitor, budget);

            var texProp = typeof(VRAMMonitor).GetProperty("TextureMemoryBytes", BindingFlags.Public | BindingFlags.Instance);
            texProp.DeclaringType.GetProperty("TextureMemoryBytes").SetValue(_monitor, 900L, null);

            var texUtilProp = typeof(VRAMMonitor).GetProperty("TextureBudgetUtilization", BindingFlags.Public | BindingFlags.Instance);
            texUtilProp.DeclaringType.GetProperty("TextureBudgetUtilization").SetValue(_monitor, 0.9f, null);

            var updateMethod = typeof(VRAMMonitor).GetMethod("UpdatePressureState", BindingFlags.NonPublic | BindingFlags.Instance);
            updateMethod.Invoke(_monitor, null);

            Assert.That(_monitor.PressureState, Is.EqualTo(VRAMMonitor.VRAMPressureState.Warning));
        }

        [Test]
        public void UpdatePressureState_Stable_WhenBelowBudget()
        {
            var budgetField = typeof(VRAMMonitor).GetField("_budgetThresholds", BindingFlags.NonPublic | BindingFlags.Instance);
            var budget = new VRAMBudgetThresholds {
                TextureMemoryBudgetBytes = 1000L,
                RenderTextureMemoryBudgetBytes = 1000L,
                TotalVRAMBudgetBytes = 1000L
            };
            budgetField.SetValue(_monitor, budget);

            var texProp = typeof(VRAMMonitor).GetProperty("TextureMemoryBytes", BindingFlags.Public | BindingFlags.Instance);
            texProp.DeclaringType.GetProperty("TextureMemoryBytes").SetValue(_monitor, 100L, null);

            var texUtilProp = typeof(VRAMMonitor).GetProperty("TextureBudgetUtilization", BindingFlags.Public | BindingFlags.Instance);
            texUtilProp.DeclaringType.GetProperty("TextureBudgetUtilization").SetValue(_monitor, 0.1f, null);

            var updateMethod = typeof(VRAMMonitor).GetMethod("UpdatePressureState", BindingFlags.NonPublic | BindingFlags.Instance);
            updateMethod.Invoke(_monitor, null);

            Assert.That(_monitor.PressureState, Is.EqualTo(VRAMMonitor.VRAMPressureState.Stable));
        }

        [Test]
        public void WriteTelemetrySample_WhenExceptionThrown_ReleasesWriteLock()
        {
            // Arrange
            var go = new GameObject();
            var monitor = go.AddComponent<TestableVRAMMonitor>();

            var mockVault = new MockDataVault();
            mockVault.MockRing = new NativeArray<VramTelemetryEntry>(10, Allocator.Temp);

            // Inject mock vault
            var dataVaultField = typeof(VRAMMonitor).GetField("_dataVault", BindingFlags.Instance | BindingFlags.NonPublic);
            dataVaultField.SetValue(monitor, mockVault);

            // Set handle so it doesn't return early
            var handleField = typeof(VRAMMonitor).GetField("_vramTelemetryHandle", BindingFlags.Instance | BindingFlags.NonPublic);
            var dummyHandle = new VaultGenerationHandle<VramTelemetryEntry>();
            object boxedHandle = dummyHandle;
            typeof(VaultGenerationHandle<VramTelemetryEntry>).GetField("BufferID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(boxedHandle, 1u);
            handleField.SetValue(monitor, boxedHandle);

            // Act
            var writeMethod = typeof(VRAMMonitor).GetMethod("WriteTelemetrySample", BindingFlags.Instance | BindingFlags.NonPublic);

            try
            {
                writeMethod.Invoke(monitor, null);
                Assert.Fail("Expected an exception to be thrown.");
            }
            catch (TargetInvocationException ex)
            {
                if (!(ex.InnerException is InvalidOperationException))
                {
                    Assert.Fail("Unexpected exception thrown: " + ex);
                }
            }
            finally
            {
                mockVault.MockRing.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
            }

            // Assert
            Assert.That(mockVault.LockReleased, Is.True, "Data vault lock was not released after exception in try block.");
        }
    }
}
