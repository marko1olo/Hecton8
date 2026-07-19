using System;
using System.Reflection;
using Hecton8.SaveSystem;
using NUnit.Framework;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public sealed class SaveManagerEnsureInitialTests
    {
        private Type _bufferSetType;
        private object _bufferSet;
        private MethodInfo _ensureInitialMethod;

        [SetUp]
        public void Setup()
        {
            _bufferSetType = typeof(SaveManager).GetNestedType("SaveManagerNativeBufferSet", BindingFlags.NonPublic);
            Assert.IsNotNull(_bufferSetType, "Could not find SaveManagerNativeBufferSet type.");

            _bufferSet = Activator.CreateInstance(_bufferSetType);

            _ensureInitialMethod = _bufferSetType.GetMethod("EnsureInitial", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(_ensureInitialMethod, "Could not find EnsureInitial method.");
        }

        [TearDown]
        public void TearDown()
        {
            if (_bufferSet != null)
            {
                var disposeMethod = _bufferSetType.GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance);
                disposeMethod?.Invoke(_bufferSet, null);
            }
        }

        private object GetFieldValue(string fieldName)
        {
            var field = _bufferSetType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            return field.GetValue(_bufferSet);
        }

        private bool IsNativeArrayCreated(string fieldName)
        {
            var arrayObj = GetFieldValue(fieldName);
            if (arrayObj == null) return false;

            var type = arrayObj.GetType();
            var property = type.GetProperty("IsCreated");
            if (property == null)
            {
                var field = type.GetField("IsCreated");
                if (field != null)
                {
                    return (bool)field.GetValue(arrayObj);
                }
                return false;
            }
            return (bool)property.GetValue(arrayObj);
        }

        [Test]
        public void EnsureInitial_CreatesExpectedNativeBuffers()
        {
            // Initially, buffers should not be created
            Assert.IsFalse(IsNativeArrayCreated("SaveTelemetryRing"), "SaveTelemetryRing should not be created initially.");
            Assert.IsFalse(IsNativeArrayCreated("WfcOutpostTelemetryRing"), "WfcOutpostTelemetryRing should not be created initially.");
            Assert.IsFalse(IsNativeArrayCreated("WfcOutpostEventTelemetryRing"), "WfcOutpostEventTelemetryRing should not be created initially.");
            Assert.IsFalse(IsNativeArrayCreated("WfcOutpostPackedWords"), "WfcOutpostPackedWords should not be created initially.");
            Assert.IsFalse(IsNativeArrayCreated("WfcOutpostRestoreWords"), "WfcOutpostRestoreWords should not be created initially.");
            Assert.IsFalse(IsNativeArrayCreated("WfcOutpostPayloadBuffer"), "WfcOutpostPayloadBuffer should not be created initially.");
            Assert.IsFalse(IsNativeArrayCreated("WfcOutpostGridSnapshotScratch"), "WfcOutpostGridSnapshotScratch should not be created initially.");
            Assert.IsFalse(IsNativeArrayCreated("WfcOutpostSnapshotCache"), "WfcOutpostSnapshotCache should not be created initially.");
            Assert.IsFalse(IsNativeArrayCreated("SaveStagingBuffer"), "SaveStagingBuffer should not be created initially.");
            Assert.IsFalse(IsNativeArrayCreated("LoadCandidateScratch"), "LoadCandidateScratch should not be created initially.");

            // Call EnsureInitial
            _ensureInitialMethod.Invoke(_bufferSet, null);

            // Buffers should now be created
            Assert.IsTrue(IsNativeArrayCreated("SaveTelemetryRing"), "SaveTelemetryRing was not created by EnsureInitial.");
            Assert.IsTrue(IsNativeArrayCreated("WfcOutpostTelemetryRing"), "WfcOutpostTelemetryRing was not created by EnsureInitial.");
            Assert.IsTrue(IsNativeArrayCreated("WfcOutpostEventTelemetryRing"), "WfcOutpostEventTelemetryRing was not created by EnsureInitial.");
            Assert.IsTrue(IsNativeArrayCreated("WfcOutpostPackedWords"), "WfcOutpostPackedWords was not created by EnsureInitial.");
            Assert.IsTrue(IsNativeArrayCreated("WfcOutpostRestoreWords"), "WfcOutpostRestoreWords was not created by EnsureInitial.");
            Assert.IsTrue(IsNativeArrayCreated("WfcOutpostPayloadBuffer"), "WfcOutpostPayloadBuffer was not created by EnsureInitial.");
            Assert.IsTrue(IsNativeArrayCreated("WfcOutpostGridSnapshotScratch"), "WfcOutpostGridSnapshotScratch was not created by EnsureInitial.");
            Assert.IsTrue(IsNativeArrayCreated("WfcOutpostSnapshotCache"), "WfcOutpostSnapshotCache was not created by EnsureInitial.");
            Assert.IsTrue(IsNativeArrayCreated("SaveStagingBuffer"), "SaveStagingBuffer was not created by EnsureInitial.");
            Assert.IsTrue(IsNativeArrayCreated("LoadCandidateScratch"), "LoadCandidateScratch was not created by EnsureInitial.");
        }
    }
}
