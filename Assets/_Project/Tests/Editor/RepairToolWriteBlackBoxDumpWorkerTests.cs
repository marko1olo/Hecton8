using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Gameplay;

namespace Hecton8.Tests.Gameplay
{
    [TestFixture]
    public class RepairToolWriteBlackBoxDumpWorkerTests
    {
        private RepairTool _repairTool;
        private MethodInfo _workerMethod;
        private FieldInfo _inFlightField;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("RepairToolTest");
            _repairTool = go.AddComponent<RepairTool>();

            _workerMethod = typeof(RepairTool).GetMethod("WriteRepairBlackBoxDumpWorker", BindingFlags.NonPublic | BindingFlags.Instance);
            _inFlightField = typeof(RepairTool).GetField("_repairBlackBoxDumpInFlight", BindingFlags.NonPublic | BindingFlags.Instance);

            // Setting _repairBlackBoxDumpSnapshot to null forces WriteRepairBlackBoxSnapshotCold
            // to throw a NullReferenceException when indexing the snapshot array.
            typeof(RepairTool).GetField("_repairBlackBoxDumpSnapshot", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_repairTool, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_repairTool != null)
            {
                UnityEngine.Object.DestroyImmediate(_repairTool.gameObject);
            }
        }

        [Test]
        public void Test_WriteRepairBlackBoxDumpWorker_Exception_ResetsInFlightFlag()
        {
            // Arrange
            _inFlightField.SetValue(_repairTool, 1);

            // Act
            // If the catch block inside WriteRepairBlackBoxDumpWorker works, it won't throw an unhandled exception.
            Assert.DoesNotThrow(() =>
            {
                _workerMethod.Invoke(_repairTool, null);
            });

            // Assert
            // The finally block should reset _repairBlackBoxDumpInFlight to 0.
            int inFlight = (int)_inFlightField.GetValue(_repairTool);
            Assert.That(inFlight, Is.EqualTo(0));
        }
    }
}
