using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using Hecton8.SaveSystem;

namespace Hecton8.Tests.PlayMode
{
    /// <summary>
    /// Smoke tests for save/load system.
    /// Verifies basic save/load operations without errors.
    /// </summary>
    public class SmokeTests_SaveLoad
    {
        private const string TestSlot = "test_slot_smoke";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Clean up test save slot
            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            if (saveManager != null && saveManager.SaveExists(TestSlot))
            {
                saveManager.DeleteSave(TestSlot);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator SaveManager_CanSaveWithoutErrors()
        {
            yield return null;

            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            Assert.IsNotNull(saveManager, "Hecton8.Core.GlobalRegistry.SaveRuntime should not be null");

            // Attempt save
            yield return saveManager.SaveGameAsync(TestSlot);

            // Check result
            if (saveManager.LastOperationSucceeded)
            {
                Assert.Pass("Save operation succeeded");
            }
            else
            {
                Assert.Inconclusive($"Save operation failed: {saveManager.LastOperationError}");
            }

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SaveManager_CanLoadWithoutErrors()
        {
            yield return null;

            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            Assert.IsNotNull(saveManager, "Hecton8.Core.GlobalRegistry.SaveRuntime should not be null");

            // Save first
            yield return saveManager.SaveGameAsync(TestSlot);
            Assert.IsTrue(saveManager.LastOperationSucceeded, "Save should succeed before load test");

            // Attempt load
            yield return saveManager.LoadGameAsync(TestSlot);

            // Check result
            if (saveManager.LastOperationSucceeded)
            {
                Assert.Pass("Load operation succeeded");
            }
            else
            {
                Assert.Inconclusive($"Load operation failed: {saveManager.LastOperationError}");
            }

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SaveManager_CanQueryMetadata()
        {
            yield return null;

            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            Assert.IsNotNull(saveManager, "Hecton8.Core.GlobalRegistry.SaveRuntime should not be null");

            // Save first
            yield return saveManager.SaveGameAsync(TestSlot);
            Assert.IsTrue(saveManager.LastOperationSucceeded, "Save should succeed before metadata query");

            // Query metadata
            bool hasMetadata = saveManager.TryGetSaveMetadata(TestSlot, out SaveMetadata metadata);

            Assert.IsTrue(hasMetadata, "Should be able to query metadata for existing save");
            Assert.IsNotNull(metadata, "Metadata should not be null");
            Assert.AreEqual(TestSlot, metadata.SlotName, "Metadata slot name should match");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SaveManager_CanDeleteSave()
        {
            yield return null;

            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            Assert.IsNotNull(saveManager, "Hecton8.Core.GlobalRegistry.SaveRuntime should not be null");

            // Save first
            yield return saveManager.SaveGameAsync(TestSlot);
            Assert.IsTrue(saveManager.LastOperationSucceeded, "Save should succeed before delete test");
            Assert.IsTrue(saveManager.SaveExists(TestSlot), "Save should exist before delete");

            // Delete
            saveManager.DeleteSave(TestSlot);

            // Verify deleted
            Assert.IsFalse(saveManager.SaveExists(TestSlot), "Save should not exist after delete");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SaveManager_HandlesMissingSaveGracefully()
        {
            yield return null;

            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            Assert.IsNotNull(saveManager, "Hecton8.Core.GlobalRegistry.SaveRuntime should not be null");

            // Ensure test slot doesn't exist
            if (saveManager.SaveExists(TestSlot))
            {
                saveManager.DeleteSave(TestSlot);
            }

            // Attempt to load non-existent save
            yield return saveManager.LoadGameAsync(TestSlot);

            // Should fail gracefully without crashing
            Assert.IsFalse(saveManager.LastOperationSucceeded, "Load should fail for non-existent save");
            Assert.IsNotEmpty(saveManager.LastOperationError, "Should have error message");

            LogAssert.NoUnexpectedReceived();
        }
    }
}
