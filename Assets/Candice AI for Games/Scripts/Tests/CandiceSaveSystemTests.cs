using NUnit.Framework;
using System.Reflection;
using CandiceAIforGames.Data;

namespace CandiceAIforGames.Tests
{
    public class CandiceSaveSystemTests
    {
        [Test]
        public void ChangeDatabaseName_UpdatesDatabaseNameField()
        {
            // Arrange
            var saveSystem = new CandiceSaveSystem();
            string newDbName = "MyTestDB";

            // Act
            saveSystem.ChangeDatabaseName(newDbName);

            // Assert
            FieldInfo dbNameField = typeof(CandiceSaveSystem).GetField("databaseName", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(dbNameField, Is.Not.Null, "databaseName field not found via reflection.");
            string actualDbName = (string)dbNameField.GetValue(saveSystem);
            Assert.That(actualDbName, Is.EqualTo(newDbName), "The databaseName field was not updated correctly.");
        }

        [Test]
        public void ChangeDatabaseName_WithSQLiteProvider_ExecutesWithoutError()
        {
            // Arrange
            var saveSystem = new CandiceSaveSystem();
            saveSystem.Initialise(""); // This initializes CandiceSQLiteProvider
            string newDbName = "AnotherTestDB";

            // Act & Assert
            Assert.DoesNotThrow(() => saveSystem.ChangeDatabaseName(newDbName), "ChangeDatabaseName should not throw when provider is CandiceSQLiteProvider.");

            FieldInfo dbNameField = typeof(CandiceSaveSystem).GetField("databaseName", BindingFlags.NonPublic | BindingFlags.Instance);
            string actualDbName = (string)dbNameField.GetValue(saveSystem);
            Assert.That(actualDbName, Is.EqualTo(newDbName), "The databaseName field was not updated correctly when SQLite provider is active.");
        }
    }
using System;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
    private const string LegacyFileSerializationDisabledMessage = "Candice legacy file serialization is disabled. Vendor file saves are quarantined; use the first-party save authority.";

    [SetUp]
    public void SetUp()
        var field = typeof(CandiceSaveSystem).GetField("s_loggedLegacyFileSerializationDisabled", BindingFlags.NonPublic | BindingFlags.Static);
        if (field != null)
            field.SetValue(null, false);

    public void SaveToFile_LogsWarning_WhenCalled()

        LogAssert.Expect(LogType.Warning, LegacyFileSerializationDisabledMessage);

        saveSystem.SaveToFile(new object(), "test_file.bin");

    public void SaveToFile_DoesNotLogWarning_WhenCalledMultipleTimes()

        LogAssert.Expect(LogType.Warning, LegacyFileSerializationDisabledMessage);
        saveSystem.SaveToFile(new object(), "test_file.bin");

        saveSystem.SaveToFile(new object(), "test_file.bin");
}
