using Hecton8.SaveSystem;
using NUnit.Framework;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public sealed class SaveDataMigrationDeterministicTrimEditTests
    {
        [Test]
        public void MigrateInPlace_RepairsDictionaryKeysBeforeCapacityTrim()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.toolDurabilityMap.Clear();
            for (int i = 0; i < SaveData.MaxToolDurabilityRecords; i++)
                data.toolDurabilityMap[$"tool.{i:00}"] = i;

            data.toolDurabilityMap[" "] = 99f;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(SaveData.MaxToolDurabilityRecords, data.toolDurabilityMap.Count);
            Assert.IsFalse(data.toolDurabilityMap.ContainsKey(" "));
            for (int i = 0; i < SaveData.MaxToolDurabilityRecords; i++)
                Assert.IsTrue(data.toolDurabilityMap.ContainsKey($"tool.{i:00}"), $"Missing tool.{i:00}");

            StringAssert.Contains("tool durability keys repaired", summary);
        }

        [Test]
        public void MigrateInPlace_TrimsCustomModDataByStableOrdinalKey()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.CustomModData.Clear();
            for (int i = 0; i <= SaveData.MaxCustomModDataEntries; i++)
                data.CustomModData[$"custom.{i:00}"] = i.ToString();

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(SaveData.MaxCustomModDataEntries, data.CustomModData.Count);
            Assert.IsTrue(data.CustomModData.ContainsKey("custom.00"));
            Assert.IsTrue(data.CustomModData.ContainsKey("custom.63"));
            Assert.IsFalse(data.CustomModData.ContainsKey("custom.64"));
            StringAssert.Contains("custom mod data capped", summary);
        }
    }
}
