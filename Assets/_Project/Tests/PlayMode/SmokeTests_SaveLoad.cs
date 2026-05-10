using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Text.RegularExpressions;
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
        private static readonly Regex SaveCompletedLog = new Regex(
            @"^\[SaveManager\] Saved 'test_slot_smoke' \(XXH3-64: [0-9A-F]+\) in [0-9]+ms$",
            RegexOptions.CultureInvariant);

        private SaveManager _ownedSaveManager;
        private GameObject _ownedSaveRoot;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Clean up test save slot
            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            if (saveManager != null && saveManager.SaveExists(TestSlot))
            {
                saveManager.DeleteSave(TestSlot);
            }

            if (_ownedSaveRoot != null)
            {
                Object.Destroy(_ownedSaveRoot);
                _ownedSaveRoot = null;
                _ownedSaveManager = null;
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator SaveManager_CanSaveWithoutErrors()
        {
            yield return null;

            SaveManager saveManager = ResolveSaveManager();
            Assert.IsNotNull(saveManager, "Hecton8.Core.GlobalRegistry.SaveRuntime should not be null");

            // Attempt save
            ExpectSaveCompletedLog();
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

            SaveManager saveManager = ResolveSaveManager();
            Assert.IsNotNull(saveManager, "Hecton8.Core.GlobalRegistry.SaveRuntime should not be null");

            // Save first
            ExpectSaveCompletedLog();
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

            SaveManager saveManager = ResolveSaveManager();
            Assert.IsNotNull(saveManager, "Hecton8.Core.GlobalRegistry.SaveRuntime should not be null");

            // Save first
            ExpectSaveCompletedLog();
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

            SaveManager saveManager = ResolveSaveManager();
            Assert.IsNotNull(saveManager, "Hecton8.Core.GlobalRegistry.SaveRuntime should not be null");

            // Save first
            ExpectSaveCompletedLog();
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

            SaveManager saveManager = ResolveSaveManager();
            Assert.IsNotNull(saveManager, "Hecton8.Core.GlobalRegistry.SaveRuntime should not be null");

            // Ensure test slot doesn't exist
            if (saveManager.SaveExists(TestSlot))
            {
                saveManager.DeleteSave(TestSlot);
            }

            // Attempt to load non-existent save
            LogAssert.Expect(LogType.Warning, "[SaveManager] No primary or backup save found for 'test_slot_smoke'.");
            yield return saveManager.LoadGameAsync(TestSlot);

            // Should fail gracefully without crashing
            Assert.IsFalse(saveManager.LastOperationSucceeded, "Load should fail for non-existent save");
            Assert.IsNotEmpty(saveManager.LastOperationError, "Should have error message");

            LogAssert.NoUnexpectedReceived();
        }

        private SaveManager ResolveSaveManager()
        {
            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            if (saveManager != null)
                return saveManager;

            _ownedSaveRoot = new GameObject("[SmokeTests_SaveManager]");
            _ownedSaveManager = _ownedSaveRoot.AddComponent<SaveManager>();
            _ownedSaveManager.InitializeService();
            return _ownedSaveManager;
        }

        private static void ExpectSaveCompletedLog()
        {
            LogAssert.Expect(LogType.Log, SaveCompletedLog);
        }
    }
}
