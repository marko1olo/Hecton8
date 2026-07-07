using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CandiceAIforGames.Data;
using System.Reflection;

namespace CandiceAIforGames.Data.Tests
{
    [TestFixture]
    public class CandiceSaveSystemTests
    {
        private CandiceSaveSystem saveSystem;

        [SetUp]
        public void SetUp()
        {
            saveSystem = new CandiceSaveSystem();
        }

        [Test]
        public void SetQuery_WithValidParameters_SetsQueryOnSQLiteProvider()
        {
            // Arrange
            CandiceSQLiteProvider sqliteProvider = new CandiceSQLiteProvider("Data Source=test.s3db");

            FieldInfo providerField = typeof(CandiceSaveSystem).GetField("providerBase", BindingFlags.NonPublic | BindingFlags.Instance);
            providerField.SetValue(saveSystem, sqliteProvider);

            string expectedQuery = "SELECT * FROM test_table";
            var expectedParams = new Dictionary<object, object>
            {
                { "@id", 1 },
                { "@name", "test_name" }
            };

            // Act
            saveSystem.SetQuery(expectedQuery, expectedParams);

            // Assert
            FieldInfo queryField = typeof(CandiceSQLiteProvider).GetField("query", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo queryParamsField = typeof(CandiceSQLiteProvider).GetField("queryParameters", BindingFlags.NonPublic | BindingFlags.Instance);

            string actualQuery = queryField.GetValue(sqliteProvider) as string;
            Dictionary<object, object> actualParams = queryParamsField.GetValue(sqliteProvider) as Dictionary<object, object>;

            Assert.AreEqual(expectedQuery, actualQuery, "Query should be correctly set on the SQLite Provider.");
            Assert.AreEqual(expectedParams, actualParams, "Parameters should be correctly set on the SQLite Provider.");
        }

        [Test]
        public void SetQuery_WithNullProvider_DoesNotThrow()
        {
            // Arrange
            FieldInfo providerField = typeof(CandiceSaveSystem).GetField("providerBase", BindingFlags.NonPublic | BindingFlags.Instance);
            providerField.SetValue(saveSystem, null);

            // Act & Assert
            Assert.DoesNotThrow(() => saveSystem.SetQuery("SELECT * FROM test", null));
        }
    }
}
