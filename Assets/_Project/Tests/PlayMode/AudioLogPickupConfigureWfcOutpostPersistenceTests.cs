using NUnit.Framework;
using UnityEngine;
using Hecton8.Narrative;
using Hecton8.Core.Contracts;
using System.Reflection;

namespace Hecton8.Narrative.Tests
{
    [TestFixture]
    public class AudioLogPickupConfigureWfcOutpostPersistenceTests
    {
        private GameObject _go;
        private AudioLogPickup _pickup;
        private FieldInfo _sectorHashField;
        private FieldInfo _cellIndexField;
        private FieldInfo _flagsField;
        private FieldInfo _persistenceConfiguredField;
        private FieldInfo _alreadyDiscoveredField;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject();
            _pickup = _go.AddComponent<AudioLogPickup>();

            var type = typeof(AudioLogPickup);
            _sectorHashField = type.GetField("_wfcOutpostSectorHash", BindingFlags.NonPublic | BindingFlags.Instance);
            _cellIndexField = type.GetField("_wfcOutpostCellIndex", BindingFlags.NonPublic | BindingFlags.Instance);
            _flagsField = type.GetField("_wfcOutpostFlags", BindingFlags.NonPublic | BindingFlags.Instance);
            _persistenceConfiguredField = type.GetField("_wfcOutpostPersistenceConfigured", BindingFlags.NonPublic | BindingFlags.Instance);
            _alreadyDiscoveredField = type.GetField("_alreadyDiscovered", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        [Test]
        public void ConfigureWfcOutpostPersistence_ValidInputWithoutLootedFlag_SetsFieldsAndRestoresBaseline()
        {
            ulong sectorHash = 12345UL;
            ushort cellIndex = 10;
            byte initialFlags = (byte)WfcOutpostCellStateFlags.PowerOn;

            _pickup.ConfigureWfcOutpostPersistence(sectorHash, cellIndex, initialFlags);

            Assert.That(_sectorHashField.GetValue(_pickup), Is.EqualTo(sectorHash));
            Assert.That(_cellIndexField.GetValue(_pickup), Is.EqualTo(cellIndex));
            Assert.That(_flagsField.GetValue(_pickup), Is.EqualTo((byte)WfcOutpostCellStateFlags.PowerOn));
            Assert.That(_persistenceConfiguredField.GetValue(_pickup), Is.True);
            Assert.That(_alreadyDiscoveredField.GetValue(_pickup), Is.False);
        }

        [Test]
        public void ConfigureWfcOutpostPersistence_ValidInputWithLootedFlag_SetsFieldsAndAppliesLootedState()
        {
            ulong sectorHash = 54321UL;
            ushort cellIndex = 20;
            byte initialFlags = (byte)(WfcOutpostCellStateFlags.DatapadLooted | WfcOutpostCellStateFlags.PowerOn);

            _pickup.ConfigureWfcOutpostPersistence(sectorHash, cellIndex, initialFlags);

            Assert.That(_sectorHashField.GetValue(_pickup), Is.EqualTo(sectorHash));
            Assert.That(_cellIndexField.GetValue(_pickup), Is.EqualTo(cellIndex));
            Assert.That(_flagsField.GetValue(_pickup), Is.EqualTo((byte)(WfcOutpostCellStateFlags.DatapadLooted | WfcOutpostCellStateFlags.PowerOn)));
            Assert.That(_persistenceConfiguredField.GetValue(_pickup), Is.True);
            Assert.That(_alreadyDiscoveredField.GetValue(_pickup), Is.True);
        }

        [Test]
        public void ConfigureWfcOutpostPersistence_ZeroSectorHash_ClearsFields()
        {
            // First configure it properly
            _pickup.ConfigureWfcOutpostPersistence(12345UL, 10, (byte)WfcOutpostCellStateFlags.DatapadLooted);

            // Then call with invalid sector hash
            _pickup.ConfigureWfcOutpostPersistence(0UL, 10, (byte)WfcOutpostCellStateFlags.DatapadLooted);

            Assert.That(_sectorHashField.GetValue(_pickup), Is.EqualTo(0UL));
            Assert.That(_cellIndexField.GetValue(_pickup), Is.EqualTo((ushort)0));
            Assert.That(_flagsField.GetValue(_pickup), Is.EqualTo((byte)0));
            Assert.That(_persistenceConfiguredField.GetValue(_pickup), Is.False);
            Assert.That(_alreadyDiscoveredField.GetValue(_pickup), Is.False);
        }

        [Test]
        public void ConfigureWfcOutpostPersistence_InvalidCellIndex_ClearsFields()
        {
            // First configure it properly
            _pickup.ConfigureWfcOutpostPersistence(12345UL, 10, (byte)WfcOutpostCellStateFlags.DatapadLooted);

            // Then call with invalid cell index
            ushort invalidCellIndex = (ushort)WfcOutpostPersistenceConstants.CellCount;
            _pickup.ConfigureWfcOutpostPersistence(12345UL, invalidCellIndex, (byte)WfcOutpostCellStateFlags.DatapadLooted);

            Assert.That(_sectorHashField.GetValue(_pickup), Is.EqualTo(0UL));
            Assert.That(_cellIndexField.GetValue(_pickup), Is.EqualTo((ushort)0));
            Assert.That(_flagsField.GetValue(_pickup), Is.EqualTo((byte)0));
            Assert.That(_persistenceConfiguredField.GetValue(_pickup), Is.False);
            Assert.That(_alreadyDiscoveredField.GetValue(_pickup), Is.False);
        }
    }
}
