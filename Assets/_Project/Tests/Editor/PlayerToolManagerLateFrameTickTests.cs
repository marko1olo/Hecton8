#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Gameplay;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class PlayerToolManagerLateFrameTickTests
    {
        private GameObject _go;
        private PlayerToolManager _manager;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("Tester");
            _manager = _go.AddComponent<PlayerToolManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
            }
        }

        private void SetPrivateField(string fieldName, object value)
        {
            var field = typeof(PlayerToolManager).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Assert.Fail($"Field {fieldName} not found");
            }
            field.SetValue(_manager, value);
        }

        private object GetPrivateField(string fieldName)
        {
            var field = typeof(PlayerToolManager).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Assert.Fail($"Field {fieldName} not found");
                return null;
            }
            return field.GetValue(_manager);
        }

        [Test]
        public void LateFrameTick_FlushesPendingFlags()
        {
            // Arrange
            // Set all the boolean flags related to flush methods. These names are verified via grep.
            SetPrivateField("_pendingSwapExecution", true);
            SetPrivateField("_pendingCurrentToolDespawn", true);
            SetPrivateField("_pendingToolSpawnExecution", true);
            SetPrivateField("_pendingToolPoolDespawn", true);
            SetPrivateField("_hasPendingHandAnchorLocalPosition", true);
            SetPrivateField("_pendingToolPoseFlush", true);

            // Set fields so that null reference exceptions are avoided inside the flush methods
            var anchorGo = new GameObject("Anchor");
            _manager.handAnchor = anchorGo.transform;

            // For Tool Despawn
            var toolInstanceGo = new GameObject("ToolInstance");
            SetPrivateField("_currentInstance", toolInstanceGo);
            SetPrivateField("_currentTool", null); // we leave this null to avoid OnUnequip throwing on a missing PlayerTool component

            // For Swap Execution
            _manager.toolPrefabs = new GameObject[0];
            SetPrivateField("_pendingSlotIndex", -1);

            // Act
            Assert.DoesNotThrow(() => _manager.LateFrameTick());

            // Assert
            Assert.That(GetPrivateField("_pendingSwapExecution"), Is.False, "_pendingSwapExecution should be flushed to false");
            Assert.That(GetPrivateField("_pendingCurrentToolDespawn"), Is.False, "_pendingCurrentToolDespawn should be flushed to false");
            Assert.That(GetPrivateField("_pendingToolSpawnExecution"), Is.False, "_pendingToolSpawnExecution should be flushed to false");
            // Note: _pendingToolPoolDespawn is flushed inside LateFrameTick AND inside DespawnCurrentTool (via QueueToolPoolDespawn and then LateFrameTick flushes it).
            // But when LateFrameTick executes, it first flushes Despawn, which Queues Pool Despawn, and then flushes Pool Despawn.
            // So it should ultimately be false.
            Assert.That(GetPrivateField("_pendingToolPoolDespawn"), Is.False, "_pendingToolPoolDespawn should be flushed to false");
            Assert.That(GetPrivateField("_hasPendingHandAnchorLocalPosition"), Is.False, "_hasPendingHandAnchorLocalPosition should be flushed to false");
            Assert.That(GetPrivateField("_pendingToolPoseFlush"), Is.False, "_pendingToolPoseFlush should be flushed to false");

            UnityEngine.Object.DestroyImmediate(anchorGo);
            UnityEngine.Object.DestroyImmediate(toolInstanceGo);
        }

        [Test]
        public void LateFrameTick_SetsFlushingToolLifecyclePresentationFalse()
        {
            // Arrange
            // LateFrameTick sets _flushingToolLifecyclePresentation to true and then false synchronously.

            // Act
            _manager.LateFrameTick();

            // Assert
            Assert.That(GetPrivateField("_flushingToolLifecyclePresentation"), Is.False, "_flushingToolLifecyclePresentation should end up false");
        }
    }
}
#endif
