using NUnit.Framework;
using UnityEngine;
using Unity.Collections;
using Hecton8.Core.Memory;
using Hecton8.Core;
using System;
using System.Reflection;

namespace Hecton8.Optimization.Tests
{
    [TestFixture]
    public class VRAMMonitorTests
    {
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
