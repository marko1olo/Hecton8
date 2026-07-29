using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Hecton8.Editor.SaveSystem;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.SaveSystem;
using Hecton8.Scavenging;
using Hecton8.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public sealed class WorldPickupStateCodecEditTests
    {
        // Batchmode starts the editor on an untitled scene that is never saved, and Unity rejects
        // EditorSceneManager.NewScene(..., NewSceneMode.Additive) while such a scene is loaded:
        // "System.InvalidOperationException : Cannot create a new scene additively with an untitled
        // scene unsaved." Seven tests in this fixture create an additive scene, so all seven threw
        // before reaching any product code. Giving the untitled scene a real asset path (and keeping
        // it saved between tests) satisfies the precondition once for the whole fixture; the scratch
        // scene asset is removed again in OneTimeTearDown.
        private const string BatchmodeHostScenePath =
            "Assets/_Project/Tests/Editor/SaveSystem/__WorldPickupStateCodecEditTests_BatchmodeHost.unity";

        private bool _batchmodeHostSceneCreated;

        [SetUp]
        public void EnsureAdditiveSceneCreationIsPermitted()
        {
            // Interactively the editor already sits on a saved scene, or on a clean untitled one that
            // Unity accepts, so leave a developer's open scene completely alone.
            if (!Application.isBatchMode)
                return;

            Scene active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path))
            {
                Assert.IsTrue(
                    EditorSceneManager.SaveScene(active, BatchmodeHostScenePath),
                    "Could not give the untitled batchmode scene an asset path, so Unity refuses every additive NewScene in this fixture.");
                _batchmodeHostSceneCreated = true;
                return;
            }

            if (_batchmodeHostSceneCreated
                && active.isDirty
                && string.Equals(active.path, BatchmodeHostScenePath, StringComparison.Ordinal))
            {
                EditorSceneManager.SaveScene(active);
            }
        }

        [OneTimeTearDown]
        public void RemoveBatchmodeHostScene()
        {
            if (!_batchmodeHostSceneCreated)
                return;

            _batchmodeHostSceneCreated = false;

            // Detach the editor from the scratch asset before deleting it. Only this fixture's own
            // empty host scene is discarded here - it never held anything a developer authored.
            Scene active = SceneManager.GetActiveScene();
            if (string.Equals(active.path, BatchmodeHostScenePath, StringComparison.Ordinal))
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

            AssetDatabase.DeleteAsset(BatchmodeHostScenePath);
        }

        [Test]
        public void TryBuildIdentity_StableIdIgnoresPositionForPersistenceKey()
        {
            bool first = WorldPickupStateCodec.TryBuildIdentity(
                "Assets/_Project/Scenes/WorldPickupIdentity.unity",
                " authored.pickup.001 ",
                "Item.CopperOre",
                new Vector3(1.25f, 2f, 3.5f),
                out long firstPersistenceKey,
                out long firstChunkKey);
            bool second = WorldPickupStateCodec.TryBuildIdentity(
                "Assets/_Project/Scenes/WorldPickupIdentity.unity",
                "authored.pickup.001",
                "Item.CopperOre",
                new Vector3(129.25f, 2f, 3.5f),
                out long secondPersistenceKey,
                out long secondChunkKey);

            Assert.IsTrue(first);
            Assert.IsTrue(second);
            Assert.AreNotEqual(0L, firstPersistenceKey);
            Assert.AreEqual(firstPersistenceKey, secondPersistenceKey);
            Assert.AreNotEqual(firstChunkKey, secondChunkKey);
        }

        [Test]
        public void TryBuildIdentity_MissingStableIdDoesNotCreatePrimaryKey()
        {
            bool emptyStableId = WorldPickupStateCodec.TryBuildIdentity(
                "Assets/_Project/Scenes/WorldPickupIdentity.unity",
                string.Empty,
                "Item.Quartz",
                new Vector3(4f, 5f, 6f),
                out long emptyStablePersistenceKey,
                out long emptyStableChunkKey);
            bool nullStableId = WorldPickupStateCodec.TryBuildIdentity(
                "Assets/_Project/Scenes/WorldPickupIdentity.unity",
                null,
                "Item.Quartz",
                new Vector3(4f, 5f, 6f),
                out long nullStablePersistenceKey,
                out long nullStableChunkKey);

            Assert.IsFalse(emptyStableId);
            Assert.IsFalse(nullStableId);
            Assert.AreEqual(0L, emptyStablePersistenceKey);
            Assert.AreEqual(0L, emptyStableChunkKey);
            Assert.AreEqual(0L, nullStablePersistenceKey);
            Assert.AreEqual(0L, nullStableChunkKey);
        }

        [Test]
        public void TryBuildIdentity_StableIdSurvivesItemPersistentIdChange()
        {
            bool first = WorldPickupStateCodec.TryBuildIdentity(
                "Assets/_Project/Scenes/WorldPickupIdentity.unity",
                "authored.pickup.slot.001",
                "Item.CopperOre",
                new Vector3(4f, 5f, 6f),
                out long firstPersistenceKey,
                out _);
            bool second = WorldPickupStateCodec.TryBuildIdentity(
                "Assets/_Project/Scenes/WorldPickupIdentity.unity",
                "authored.pickup.slot.001",
                "Item.UpdatedCopperOre",
                new Vector3(4f, 5f, 6f),
                out long secondPersistenceKey,
                out _);

            Assert.IsTrue(first);
            Assert.IsTrue(second);
            Assert.AreEqual(firstPersistenceKey, secondPersistenceKey);
        }

        [Test]
        public void TryBuildIdentity_DifferentStableIdsProduceDifferentPersistenceKeys()
        {
            bool first = WorldPickupStateCodec.TryBuildIdentity(
                "Assets/_Project/Scenes/WorldPickupIdentity.unity",
                "authored.pickup.slot.001",
                "Item.CopperOre",
                new Vector3(4f, 5f, 6f),
                out long firstPersistenceKey,
                out _);
            bool second = WorldPickupStateCodec.TryBuildIdentity(
                "Assets/_Project/Scenes/WorldPickupIdentity.unity",
                "authored.pickup.slot.002",
                "Item.CopperOre",
                new Vector3(4f, 5f, 6f),
                out long secondPersistenceKey,
                out _);

            Assert.IsTrue(first);
            Assert.IsTrue(second);
            Assert.AreNotEqual(firstPersistenceKey, secondPersistenceKey);
        }

        [Test]
        public void TryBuildIdentity_StableIdBuildsDespiteMissingFunctionalItemIdentity()
        {
            bool result = WorldPickupStateCodec.TryBuildIdentity(
                "Assets/_Project/Scenes/WorldPickupIdentity.unity",
                "authored.pickup.slot.001",
                " \t\r\n",
                new Vector3(4f, 5f, 6f),
                out long persistenceKey,
                out long chunkKey);

            Assert.IsTrue(result);
            Assert.AreNotEqual(0L, persistenceKey);
            Assert.AreNotEqual(0L, chunkKey);
        }

        [Test]
        public void TryBuildIdentity_StableTransformOverloadAllowsMissingItemData()
        {
            const string tempScenePath = "Assets/_Project/Tests/Editor/SaveSystem/__WorldPickupStateCodecMissingItemEditTests_Temp.unity";
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = default;
            GameObject host = null;
            try
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                Assert.IsTrue(EditorSceneManager.SaveScene(scene, tempScenePath));

                host = new GameObject("WorldPickupStateCodecMissingItemEditTests.Pickup");
                SceneManager.MoveGameObjectToScene(host, scene);

                bool result = WorldPickupStateCodec.TryBuildIdentity(
                    host.transform,
                    scene,
                    itemData: null,
                    stableWorldStateId: "authored.pickup.slot.missing-item",
                    anchorPosition: host.transform.position,
                    out long persistenceKey,
                    out long chunkKey);

                Assert.IsTrue(result);
                Assert.AreNotEqual(0L, persistenceKey);
                Assert.AreNotEqual(0L, chunkKey);
            }
            finally
            {
                if (host != null)
                    UnityEngine.Object.DestroyImmediate(host);

                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);

                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, removeScene: true);

                AssetDatabase.DeleteAsset(tempScenePath);
            }
        }

        [Test]
        public void TryBuildIdentity_LegacyTransformOverloadDoesNotCreatePrimaryKey()
        {
            const string tempScenePath = "Assets/_Project/Tests/Editor/SaveSystem/__WorldPickupStateLegacyOverloadEditTests_Temp.unity";
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = default;
            GameObject host = null;
            ItemData itemData = null;
            try
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                Assert.IsTrue(EditorSceneManager.SaveScene(scene, tempScenePath));

                itemData = ScriptableObject.CreateInstance<ItemData>();
                itemData.name = "WorldPickupStateLegacyOverloadEditTests.Item";
                host = new GameObject("WorldPickupStateLegacyOverloadEditTests.Pickup");
                SceneManager.MoveGameObjectToScene(host, scene);

                bool result = WorldPickupStateCodec.TryBuildIdentity(
                    host.transform,
                    scene,
                    itemData,
                    host.transform.position,
                    out long persistenceKey,
                    out long chunkKey);

                Assert.IsFalse(result);
                Assert.AreEqual(0L, persistenceKey);
                Assert.AreEqual(0L, chunkKey);
            }
            finally
            {
                if (host != null)
                    UnityEngine.Object.DestroyImmediate(host);

                if (itemData != null)
                    UnityEngine.Object.DestroyImmediate(itemData);

                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);

                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, removeScene: true);

                AssetDatabase.DeleteAsset(tempScenePath);
            }
        }

        [Test]
        public void TryResolveOrPromoteCollectedPickup_PromotesLegacyKeyToPrimaryKey()
        {
            GameObject host = new GameObject("WorldPickupStateCodecEditTests.WorldStateManager");
            try
            {
                WorldStateManager manager = host.AddComponent<WorldStateManager>();
                SetPrivateField(manager, "_depletedNodeIds", new HashSet<string>());
                SetPrivateField(manager, "_depletedPickupKeys", new HashSet<long>());
                const long legacyKey = 10101L;
                const long primaryKey = 20202L;
                const long chunkKey = 30303L;

                manager.RegisterCollectedPickup(legacyKey, legacyKey);

                Assert.IsTrue(manager.TryResolveOrPromoteCollectedPickup(primaryKey, chunkKey, legacyKey));
                Assert.IsTrue(manager.IsPickupDepleted(primaryKey));
                Assert.IsFalse(manager.IsPickupDepleted(legacyKey));

                SaveData data = SaveData.CreateNew(0.0);
                manager.PopulateSaveData(data);

                Assert.AreEqual(1, data.worldState.depletedPickupWordCount);
                Assert.AreEqual(primaryKey, data.worldState.depletedPickupWords[0]);
                Assert.AreEqual(chunkKey, data.worldState.depletedPickupChunkKeys[0]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TryResolveOrPromoteCollectedPickup_PrimaryAndLegacyPresentDeduplicatesLegacy()
        {
            GameObject host = new GameObject("WorldPickupStateCodecEditTests.WorldStateManager.Deduplicate");
            try
            {
                WorldStateManager manager = host.AddComponent<WorldStateManager>();
                SetPrivateField(manager, "_depletedNodeIds", new HashSet<string>());
                SetPrivateField(manager, "_depletedPickupKeys", new HashSet<long>());
                const long legacyKey = 40404L;
                const long primaryKey = 50505L;
                const long chunkKey = 60606L;

                manager.RegisterCollectedPickup(primaryKey, primaryKey);
                manager.RegisterCollectedPickup(legacyKey, legacyKey);

                Assert.IsTrue(manager.TryResolveOrPromoteCollectedPickup(primaryKey, chunkKey, legacyKey));
                Assert.IsTrue(manager.IsPickupDepleted(primaryKey));
                Assert.IsFalse(manager.IsPickupDepleted(legacyKey));

                SaveData data = SaveData.CreateNew(0.0);
                manager.PopulateSaveData(data);

                Assert.AreEqual(1, data.worldState.depletedPickupWordCount);
                Assert.AreEqual(primaryKey, data.worldState.depletedPickupWords[0]);
                Assert.AreEqual(chunkKey, data.worldState.depletedPickupChunkKeys[0]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TryResolveOrPromoteCollectedPickup_ZeroChunkFallsBackToPrimaryKey()
        {
            GameObject host = new GameObject("WorldPickupStateCodecEditTests.WorldStateManager.ZeroChunk");
            try
            {
                WorldStateManager manager = host.AddComponent<WorldStateManager>();
                SetPrivateField(manager, "_depletedNodeIds", new HashSet<string>());
                SetPrivateField(manager, "_depletedPickupKeys", new HashSet<long>());
                const long legacyKey = 70707L;
                const long primaryKey = 80808L;

                manager.RegisterCollectedPickup(legacyKey, legacyKey);

                Assert.IsTrue(manager.TryResolveOrPromoteCollectedPickup(primaryKey, 0L, legacyKey));

                SaveData data = SaveData.CreateNew(0.0);
                manager.PopulateSaveData(data);

                Assert.AreEqual(1, data.worldState.depletedPickupWordCount);
                Assert.AreEqual(primaryKey, data.worldState.depletedPickupWords[0]);
                Assert.AreEqual(primaryKey, data.worldState.depletedPickupChunkKeys[0]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TryResolveOrPromoteCollectedPickup_PopulatedSaveRoundTripsPromotedPrimaryChunk()
        {
            GameObject sourceHost = new GameObject("WorldPickupStateCodecEditTests.WorldStateManager.RoundTrip.Source");
            GameObject loadedHost = new GameObject("WorldPickupStateCodecEditTests.WorldStateManager.RoundTrip.Loaded");
            try
            {
                WorldStateManager source = sourceHost.AddComponent<WorldStateManager>();
                SetPrivateField(source, "_depletedNodeIds", new HashSet<string>());
                SetPrivateField(source, "_depletedPickupKeys", new HashSet<long>());
                const long legacyKey = 90909L;
                const long primaryKey = 100100L;
                const long chunkKey = 110110L;

                source.RegisterCollectedPickup(legacyKey, legacyKey);
                Assert.IsTrue(source.TryResolveOrPromoteCollectedPickup(primaryKey, chunkKey, legacyKey));

                SaveData data = SaveData.CreateNew(0.0);
                source.PopulateSaveData(data);

                WorldStateManager loaded = loadedHost.AddComponent<WorldStateManager>();
                SetPrivateField(loaded, "_depletedNodeIds", new HashSet<string>());
                SetPrivateField(loaded, "_depletedPickupKeys", new HashSet<long>());
                loaded.LoadFromSaveData(data);

                Assert.IsTrue(loaded.IsPickupDepleted(primaryKey));
                Assert.IsFalse(loaded.IsPickupDepleted(legacyKey));

                SaveData roundTrip = SaveData.CreateNew(0.0);
                loaded.PopulateSaveData(roundTrip);

                Assert.AreEqual(1, roundTrip.worldState.depletedPickupWordCount);
                Assert.AreEqual(primaryKey, roundTrip.worldState.depletedPickupWords[0]);
                Assert.AreEqual(chunkKey, roundTrip.worldState.depletedPickupChunkKeys[0]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sourceHost);
                UnityEngine.Object.DestroyImmediate(loadedHost);
            }
        }

        [Test]
        public void WorldStateManager_LoadNullClearsRuntimeStateAndAppliesSceneRestore()
        {
            GameObject host = new GameObject("WorldPickupStateCodecEditTests.WorldStateManager.NullLoad");
            try
            {
                WorldStateManager manager = host.AddComponent<WorldStateManager>();
                SetPrivateField(manager, "_depletedNodeIds", new HashSet<string>());
                SetPrivateField(manager, "_depletedPickupKeys", new HashSet<long>());

                manager.RegisterDepletedNode("null-load-node");
                manager.RegisterCollectedPickup(121212L, 343434L);

                Assert.AreEqual(1, manager.DepletedCount);
                Assert.AreEqual(1, manager.DepletedPickupCount);

                manager.LoadFromSaveData(null);

                Assert.AreEqual(0, manager.DepletedCount);
                Assert.AreEqual(0, manager.DepletedPickupCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void WorldStateManager_ApplyToScenePromotesLegacyPickupIdentityAndDeactivatesPickup()
        {
            const string tempScenePath = "Assets/_Project/Tests/Editor/SaveSystem/__WorldPickupStateApplyLegacyEditTests_Temp.unity";
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = default;
            GameObject managerHost = null;
            ItemData itemData = null;
            try
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                Assert.IsTrue(EditorSceneManager.SaveScene(scene, tempScenePath));

                itemData = ScriptableObject.CreateInstance<ItemData>();
                itemData.name = "WorldPickupStateApplyLegacyEditTests.Item";

                PickupItem pickup = CreatePickup("WorldPickupStateApplyLegacyEditTests.Pickup");
                SceneManager.MoveGameObjectToScene(pickup.gameObject, scene);
                SetPrivateField(pickup, "itemData", itemData);
                SetPrivateField(pickup, "quantity", 3);
                SetPrivateField(pickup, "persistWorldState", true);
                SetPrivateField(pickup, "stableWorldStateId", "apply-scene-stable-id");
                InvokePrivateInstance(pickup, "CaptureWorldStateRestoreBaseline");
                InvokePrivateInstance(pickup, "InvalidateWorldStateIdentity");
                InvokePrivateInstance(pickup, "CaptureWorldStateIdentityCold");

                Assert.IsTrue(pickup.TryGetWorldStatePersistenceIdentity(
                    out long primaryPersistenceKey,
                    out long primaryChunkKey,
                    out long legacyPersistenceKey));
                Assert.AreNotEqual(0L, primaryPersistenceKey);
                Assert.AreNotEqual(0L, primaryChunkKey);
                Assert.AreNotEqual(0L, legacyPersistenceKey);
                Assert.AreNotEqual(primaryPersistenceKey, legacyPersistenceKey);

                managerHost = new GameObject("WorldPickupStateApplyLegacyEditTests.WorldStateManager");
                WorldStateManager manager = managerHost.AddComponent<WorldStateManager>();
                SetPrivateField(manager, "_depletedNodeIds", new HashSet<string>());
                SetPrivateField(manager, "_depletedPickupKeys", new HashSet<long>());
                manager.RegisterCollectedPickup(legacyPersistenceKey, legacyPersistenceKey);

                Assert.IsTrue(pickup.gameObject.activeSelf);
                manager.ApplyToScene();

                Assert.IsFalse(pickup.gameObject.activeSelf);
                Assert.IsTrue(manager.IsPickupDepleted(primaryPersistenceKey));
                Assert.IsFalse(manager.IsPickupDepleted(legacyPersistenceKey));

                SaveData roundTrip = SaveData.CreateNew(0.0);
                manager.PopulateSaveData(roundTrip);
                Assert.AreEqual(1, roundTrip.worldState.depletedPickupWordCount);
                Assert.AreEqual(primaryPersistenceKey, roundTrip.worldState.depletedPickupWords[0]);
                Assert.AreEqual(primaryChunkKey, roundTrip.worldState.depletedPickupChunkKeys[0]);

                manager.ClearAll();
                SetPrivateField(pickup, "quantity", 0);
                manager.ApplyToScene();

                Assert.IsTrue(pickup.gameObject.activeSelf);
                Assert.AreEqual(3, pickup.Quantity);
            }
            finally
            {
                if (managerHost != null)
                    UnityEngine.Object.DestroyImmediate(managerHost);

                if (itemData != null)
                    UnityEngine.Object.DestroyImmediate(itemData);

                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);

                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, removeScene: true);

                AssetDatabase.DeleteAsset(tempScenePath);
            }
        }

        [Test]
        public void WorldStateManager_ApplyToSceneReactivatesResourceNodeWhenLoadedSlotIsNotDepleted()
        {
            GameObject nodeHost = null;
            GameObject managerHost = null;
            try
            {
                nodeHost = new GameObject("WorldStateManagerResourceNodeRestoreTests.Node");
                ResourceNode node = nodeHost.AddComponent<ResourceNode>();
                node.SetUniqueId("world-state-resource-node-restore-test");

                managerHost = new GameObject("WorldStateManagerResourceNodeRestoreTests.Manager");
                WorldStateManager manager = managerHost.AddComponent<WorldStateManager>();
                SetPrivateField(manager, "_depletedNodeIds", new HashSet<string>());
                SetPrivateField(manager, "_depletedPickupKeys", new HashSet<long>());

                manager.RegisterDepletedNode(node.UniqueId);
                Assert.IsTrue(node.gameObject.activeSelf);
                manager.ApplyToScene();

                Assert.IsFalse(node.gameObject.activeSelf);

                manager.ClearAll();
                manager.ApplyToScene();

                Assert.IsTrue(node.gameObject.activeSelf);
            }
            finally
            {
                if (nodeHost != null)
                    UnityEngine.Object.DestroyImmediate(nodeHost);

                if (managerHost != null)
                    UnityEngine.Object.DestroyImmediate(managerHost);
            }
        }

        [Test]
        public void PickupItem_OnValidateRegeneratesDuplicateStableWorldStateIdInSavedEditorScene()
        {
            const string tempScenePath = "Assets/_Project/Tests/Editor/SaveSystem/__WorldPickupStateCodecEditTests_Temp.unity";
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = default;
            ItemData itemData = null;
            try
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                Assert.IsTrue(EditorSceneManager.SaveScene(scene, tempScenePath));

                itemData = ScriptableObject.CreateInstance<ItemData>();
                itemData.name = "WorldPickupStateCodecEditTests.Item";

                PickupItem first = CreatePickup("WorldPickupStateCodecEditTests.First");
                PickupItem second = CreatePickup("WorldPickupStateCodecEditTests.Second");
                SceneManager.MoveGameObjectToScene(first.gameObject, scene);
                SceneManager.MoveGameObjectToScene(second.gameObject, scene);
                Assert.IsTrue(EditorSceneManager.SaveScene(scene, tempScenePath));

                SetPrivateField(first, "itemData", itemData);
                SetPrivateField(second, "itemData", itemData);
                SetPrivateField(first, "persistWorldState", true);
                SetPrivateField(second, "persistWorldState", true);
                SetPrivateField(first, "stableWorldStateId", "duplicate-stable-id");
                SetPrivateField(second, "stableWorldStateId", "duplicate-stable-id");
                Assert.IsFalse(scene.isDirty);

                InvokePrivateInstance(second, "OnValidate");

                string firstStableId = ReadSerializedString(first, "stableWorldStateId");
                string secondStableId = ReadSerializedString(second, "stableWorldStateId");

                Assert.AreEqual("duplicate-stable-id", firstStableId);
                Assert.IsFalse(string.IsNullOrWhiteSpace(secondStableId));
                Assert.AreNotEqual(firstStableId, secondStableId);
                Assert.IsTrue(scene.isDirty);
            }
            finally
            {
                if (itemData != null)
                    UnityEngine.Object.DestroyImmediate(itemData);

                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);

                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, removeScene: true);

                AssetDatabase.DeleteAsset(tempScenePath);
            }
        }

        [Test]
        public void PickupItem_OnValidateLeavesMissingItemIdentityUnrepaired()
        {
            const string tempScenePath = "Assets/_Project/Tests/Editor/SaveSystem/__WorldPickupStateOnValidateMissingItemEditTests_Temp.unity";
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = default;
            try
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                Assert.IsTrue(EditorSceneManager.SaveScene(scene, tempScenePath));

                PickupItem pickup = CreatePickup("WorldPickupStateOnValidateMissingItemEditTests.Pickup");
                SceneManager.MoveGameObjectToScene(pickup.gameObject, scene);
                Assert.IsTrue(EditorSceneManager.SaveScene(scene, tempScenePath));

                SetPrivateField<ItemData>(pickup, "itemData", null);
                SetPrivateField(pickup, "persistWorldState", true);
                SetPrivateField(pickup, "stableWorldStateId", string.Empty);
                Assert.IsFalse(scene.isDirty);

                UnityEngine.TestTools.LogAssert.Expect(
                    LogType.Error,
                    "[PickupItem] Persistent scene pickup cannot seed stableWorldStateId without item persistent ID.");

                InvokePrivateInstance(pickup, "OnValidate");

                Assert.AreEqual(string.Empty, ReadSerializedString(pickup, "stableWorldStateId"));
                Assert.IsFalse(scene.isDirty);
            }
            finally
            {
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);

                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, removeScene: true);

                AssetDatabase.DeleteAsset(tempScenePath);
            }
        }

        [Test]
        public void WorldPickupStateAuthoringValidator_RepairModeRegeneratesDuplicateStableWorldStateIdsInScopedScene()
        {
            const string tempScenePath = "Assets/_Project/Tests/Editor/SaveSystem/__WorldPickupStateValidatorEditTests_Temp.unity";
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = default;
            ItemData itemData = null;
            try
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                Assert.IsTrue(EditorSceneManager.SaveScene(scene, tempScenePath));

                itemData = ScriptableObject.CreateInstance<ItemData>();
                itemData.name = "WorldPickupStateValidatorEditTests.Item";

                PickupItem first = CreatePickup("WorldPickupStateValidatorEditTests.First");
                PickupItem second = CreatePickup("WorldPickupStateValidatorEditTests.Second");
                SceneManager.MoveGameObjectToScene(first.gameObject, scene);
                SceneManager.MoveGameObjectToScene(second.gameObject, scene);
                Assert.IsTrue(EditorSceneManager.SaveScene(scene, tempScenePath));

                SetPrivateField(first, "itemData", itemData);
                SetPrivateField(second, "itemData", itemData);
                SetPrivateField(first, "persistWorldState", true);
                SetPrivateField(second, "persistWorldState", true);
                SetPrivateField(first, "stableWorldStateId", "validator-duplicate-stable-id");
                SetPrivateField(second, "stableWorldStateId", "validator-duplicate-stable-id");

                WorldPickupStableIdScanResult result = WorldPickupStateAuthoringValidator.ScanOpenScenePickupStableIds(
                    repair: true,
                    requiredScenePath: tempScenePath);

                string firstStableId = ReadSerializedString(first, "stableWorldStateId");
                string secondStableId = ReadSerializedString(second, "stableWorldStateId");

                Assert.AreEqual(1, result.IssueCount);
                Assert.AreEqual(1, result.RepairedCount);
                Assert.AreEqual(0, result.UnresolvedCount);
                Assert.IsTrue(
                    string.Equals(firstStableId, "validator-duplicate-stable-id", StringComparison.Ordinal) ||
                    string.Equals(secondStableId, "validator-duplicate-stable-id", StringComparison.Ordinal));
                Assert.IsFalse(string.IsNullOrWhiteSpace(firstStableId));
                Assert.IsFalse(string.IsNullOrWhiteSpace(secondStableId));
                Assert.AreNotEqual(firstStableId, secondStableId);
                Assert.IsTrue(scene.isDirty);
            }
            finally
            {
                if (itemData != null)
                    UnityEngine.Object.DestroyImmediate(itemData);

                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);

                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, removeScene: true);

                AssetDatabase.DeleteAsset(tempScenePath);
            }
        }

        [Test]
        public void WorldPickupStateAuthoringValidator_RepairModeLeavesMissingItemIdentityUnresolved()
        {
            const string tempScenePath = "Assets/_Project/Tests/Editor/SaveSystem/__WorldPickupStateValidatorMissingItemEditTests_Temp.unity";
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = default;
            try
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                Assert.IsTrue(EditorSceneManager.SaveScene(scene, tempScenePath));

                PickupItem pickup = CreatePickup("WorldPickupStateValidatorMissingItemEditTests.Pickup");
                SceneManager.MoveGameObjectToScene(pickup.gameObject, scene);
                Assert.IsTrue(EditorSceneManager.SaveScene(scene, tempScenePath));

                SetPrivateField<ItemData>(pickup, "itemData", null);
                SetPrivateField(pickup, "persistWorldState", true);
                SetPrivateField(pickup, "stableWorldStateId", string.Empty);
                Assert.IsFalse(scene.isDirty);

                UnityEngine.TestTools.LogAssert.Expect(
                    LogType.Error,
                    "[WorldPickupStateAuthoringValidator] Persistent pickup has no item persistent ID.");

                WorldPickupStableIdScanResult result = WorldPickupStateAuthoringValidator.ScanOpenScenePickupStableIds(
                    repair: true,
                    requiredScenePath: tempScenePath);

                Assert.AreEqual(1, result.IssueCount);
                Assert.AreEqual(0, result.RepairedCount);
                Assert.AreEqual(1, result.UnresolvedCount);
                Assert.AreEqual(string.Empty, ReadSerializedString(pickup, "stableWorldStateId"));
                Assert.IsFalse(scene.isDirty);
            }
            finally
            {
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);

                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, removeScene: true);

                AssetDatabase.DeleteAsset(tempScenePath);
            }
        }

        [Test]
        public void PickupItem_OnValidateInvalidatesCachedPersistenceIdentityAndRebuildsItFromTheNewStableId()
        {
            // Replaces a source-text test whose central assertion was
            //   normalizedOnValidate.StartsWith("{\n            InvalidateWorldStateIdentity();")
            // - a claim about twelve spaces of indentation and a line ending, not about behaviour. It
            // passed for any body that merely began with that text and failed for a correct body that
            // was reformatted. What it was standing in for is real and observable: the cached
            // persistence identity must be dropped by OnValidate before every early return, otherwise a
            // pickup keeps answering with the key of its previous stableWorldStateId and the save layer
            // suppresses the wrong object.
            const string tempScenePath = "Assets/_Project/Tests/Editor/SaveSystem/__WorldPickupStateOnValidateInvalidationEditTests_Temp.unity";
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = default;
            ItemData itemData = null;
            try
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                Assert.IsTrue(EditorSceneManager.SaveScene(scene, tempScenePath));

                itemData = ScriptableObject.CreateInstance<ItemData>();
                itemData.name = "WorldPickupStateOnValidateInvalidationEditTests.Item";

                PickupItem pickup = CreatePickup("WorldPickupStateOnValidateInvalidationEditTests.Pickup");
                SceneManager.MoveGameObjectToScene(pickup.gameObject, scene);
                Assert.IsTrue(EditorSceneManager.SaveScene(scene, tempScenePath));

                SetPrivateField(pickup, "itemData", itemData);
                SetPrivateField(pickup, "persistWorldState", true);
                SetPrivateField(pickup, "stableWorldStateId", "onvalidate-invalidation-first");
                InvokePrivateInstance(pickup, "InvalidateWorldStateIdentity");
                InvokePrivateInstance(pickup, "CaptureWorldStateIdentityCold");

                Assert.IsTrue(
                    pickup.TryGetWorldStatePersistenceIdentity(out long firstKey, out long firstChunkKey),
                    "A persistent authored pickup in a saved scene must resolve a world-state identity.");
                Assert.AreNotEqual(0L, firstKey);
                Assert.AreNotEqual(0L, firstChunkKey);

                // Writing the serialized field alone must NOT move the cached key. This is the half that
                // makes OnValidate's invalidation load-bearing instead of decorative; without it the
                // next assertion could pass for the wrong reason.
                SetPrivateField(pickup, "stableWorldStateId", "onvalidate-invalidation-second");
                Assert.IsTrue(pickup.TryGetWorldStatePersistenceIdentity(out long staleKey, out _));
                Assert.AreEqual(
                    firstKey,
                    staleKey,
                    "The identity is not actually cached, so this fixture cannot prove OnValidate invalidates it.");

                InvokePrivateInstance(pickup, "OnValidate");

                Assert.IsFalse(
                    pickup.TryGetWorldStatePersistenceIdentity(out long clearedKey, out long clearedChunkKey),
                    "OnValidate left a stale persistence identity cached, so a re-authored stableWorldStateId still resolves to the old save key.");
                Assert.AreEqual(0L, clearedKey);
                Assert.AreEqual(0L, clearedChunkKey);

                InvokePrivateInstance(pickup, "CaptureWorldStateIdentityCold");
                Assert.IsTrue(pickup.TryGetWorldStatePersistenceIdentity(out long secondKey, out long secondChunkKey));
                Assert.AreNotEqual(
                    firstKey,
                    secondKey,
                    "A different stableWorldStateId must produce a different persistence key.");

                // The pickup did not move, and the chunk key is derived from position only, so it must
                // survive a stable-id change untouched.
                Assert.AreEqual(firstChunkKey, secondChunkKey);

                string currentStableId = ReadPrivateField<string>(pickup, "stableWorldStateId");
                Assert.AreEqual("onvalidate-invalidation-second", currentStableId);
                Assert.IsTrue(WorldPickupStateCodec.TryBuildIdentity(
                    scene.path,
                    currentStableId,
                    itemData.PersistentId,
                    pickup.transform.position,
                    out long expectedKey,
                    out long expectedChunkKey));
                Assert.AreEqual(
                    expectedKey,
                    secondKey,
                    "The rebuilt identity does not match the codec, so the pickup and the save layer disagree about its key.");
                Assert.AreEqual(expectedChunkKey, secondChunkKey);
            }
            finally
            {
                if (itemData != null)
                    UnityEngine.Object.DestroyImmediate(itemData);

                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);

                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, removeScene: true);

                AssetDatabase.DeleteAsset(tempScenePath);
            }
        }

        [Test]
        public void PickupItem_DoesNotUseGlobalObjectIdForAuthoredIdentity()
        {
            // ARCHITECTURE GUARD, deliberately still a text assertion and named as one.
            // UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow returns editor-session-scoped ids that
            // are not stable across a reimport, so it must never seed a persisted save key. There is no
            // behavioural assertion for "this API is absent from the file" - the whole point is that
            // the call must not exist, and a runtime test can only observe the API when it is already
            // being used. This is the one assertion kept from the source-text test that used to live
            // here; it is a rule about source shape, not a behaviour pretending to be tested.
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Items/PickupItem.cs"));

            Assert.That(source, Does.Not.Contain("UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow"));
        }

        [Test]
        public void WorldPickupStateAuthoringValidator_ScanFindsInactiveDuplicatesAndScopesToTheRequestedScene()
        {
            // Replaces 22 StringAssert/Does.Contain probes on the validator's own source text. Two of
            // them ("[MenuItem(\"Hecton/...") sat red for a long time against a file that had always
            // spelled the prefix Hecton8 - the failure mode of text assertions in both directions. The
            // menu paths below are read from the COMPILED MenuItem attributes, which cannot drift from
            // the menu Unity actually registers, and every other claim is replaced by driving the real
            // scan. FindObjectsInactive.Include and the requiredScenePath filter are proven by outcome:
            // a disabled duplicate is still counted, and the same duplicate pair is not counted when the
            // scan is asked about a different scene path. The repair pass is proven by the resulting IDs
            // no longer colliding, not by the presence of an AssignNewStableId call site.
            const string tempScenePath = "Assets/_Project/Tests/Editor/SaveSystem/__WorldPickupStateValidatorScanEditTests_Temp.unity";
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = default;
            ItemData itemData = null;
            try
            {
                AssertValidatorMenuItemPath("ValidateOpenScenePickupStableIds", "Hecton8/Validation/Validate World Pickup Stable IDs");
                AssertValidatorMenuItemPath("SeedOpenScenePickupStableIds", "Hecton8/Authoring/Seed World Pickup Stable IDs In Open Scenes");

                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                Assert.IsTrue(EditorSceneManager.SaveScene(scene, tempScenePath));

                itemData = ScriptableObject.CreateInstance<ItemData>();
                itemData.name = "WorldPickupStateValidatorScanEditTests.Item";

                PickupItem first = CreatePickup("WorldPickupStateValidatorScanEditTests.First");
                PickupItem second = CreatePickup("WorldPickupStateValidatorScanEditTests.Second");
                SceneManager.MoveGameObjectToScene(first.gameObject, scene);
                SceneManager.MoveGameObjectToScene(second.gameObject, scene);
                Assert.IsTrue(EditorSceneManager.SaveScene(scene, tempScenePath));

                SetPrivateField(first, "itemData", itemData);
                SetPrivateField(second, "itemData", itemData);
                SetPrivateField(first, "persistWorldState", true);
                SetPrivateField(second, "persistWorldState", true);
                SetPrivateField(first, "stableWorldStateId", "validator-scan-unique-first");
                SetPrivateField(second, "stableWorldStateId", "validator-scan-unique-second");

                Assert.AreEqual(
                    0,
                    WorldPickupStateAuthoringValidator.ScanOpenScenePickups(repair: false, requiredScenePath: tempScenePath),
                    "Two persistent pickups with distinct stable IDs must scan clean.");

                // A DISABLED duplicate must still be found. A scan that omitted
                // FindObjectsInactive.Include would report zero issues here and the build gate would
                // ship a save-key collision.
                SetPrivateField(second, "stableWorldStateId", "validator-scan-unique-first");
                second.gameObject.SetActive(false);

                WorldPickupStableIdScanResult duplicateScan =
                    WorldPickupStateAuthoringValidator.ScanOpenScenePickupStableIds(repair: false, requiredScenePath: tempScenePath);

                Assert.AreEqual(
                    1,
                    duplicateScan.IssueCount,
                    "An inactive duplicate stable ID was not reported, so the scan is not including inactive objects.");
                Assert.AreEqual(0, duplicateScan.RepairedCount, "repair:false must not mutate authoring data.");
                Assert.AreEqual(
                    "validator-scan-unique-first",
                    ReadSerializedString(second, "stableWorldStateId"),
                    "repair:false rewrote a stable ID.");

                // Scene scoping: the same duplicate pair must NOT be counted when the scan is asked for a
                // different scene path. A scan that ignored requiredScenePath would report 1 here and
                // fail an unrelated scene's build gate.
                Assert.AreEqual(
                    0,
                    WorldPickupStateAuthoringValidator.ScanOpenScenePickups(
                        repair: false,
                        requiredScenePath: "Assets/_Project/Tests/Editor/SaveSystem/__WorldPickupStateValidatorScanEditTests_NotThisScene.unity"),
                    "The scan ignored requiredScenePath and reported issues from a scene it was not asked about.");

                second.gameObject.SetActive(true);

                // repair:true must resolve the duplicate rather than leave it unresolved.
                WorldPickupStableIdScanResult repairScan =
                    WorldPickupStateAuthoringValidator.ScanOpenScenePickupStableIds(repair: true, requiredScenePath: tempScenePath);

                Assert.AreEqual(1, repairScan.IssueCount);
                Assert.AreEqual(1, repairScan.RepairedCount);
                Assert.AreEqual(0, repairScan.UnresolvedCount);
                Assert.AreNotEqual(
                    ReadSerializedString(first, "stableWorldStateId"),
                    ReadSerializedString(second, "stableWorldStateId"),
                    "repair:true reported a repair but left the two pickups sharing one save key.");
                Assert.AreEqual(
                    0,
                    WorldPickupStateAuthoringValidator.ScanOpenScenePickups(repair: false, requiredScenePath: tempScenePath),
                    "A repaired scene must scan clean on the next pass.");
            }
            finally
            {
                if (itemData != null)
                    UnityEngine.Object.DestroyImmediate(itemData);

                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);

                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, removeScene: true);

                AssetDatabase.DeleteAsset(tempScenePath);
            }
        }

        [Test]
        public void WorldStateManager_ApplyPickupStateSuppressesEveryDepletedPickupEvenWhenTheSweepMutatesTheRegistry()
        {
            // Replaces a source-text test that asserted the literal loop header
            //   "for (int i = PickupItem.WorldStateRegistryCount - 1; i >= 0; i--)"
            // in ApplyPickupStateToScene (WorldStateManager.cs:643). That assertion is green for a
            // reformat and green for a correct-looking loop with a broken body, and it is red for a
            // rename of `i` that changes nothing. The reason the descending scan exists is real:
            // PickupItem's registry is a RegistryBucket (RegistryBucket.cs:152) that removes with
            // swap-with-last, and suppressing a pickup can unregister it mid-sweep, moving the tail
            // entry down into the slot just visited. An ascending scan over a live count skips that
            // moved entry.
            //
            // This drives the real sweep with a real mid-sweep swap-removal. Middle.persistWorldState
            // is cleared AFTER its identity is cached, so ShouldRetainWorldStateRegistryWhileInactive
            // (PickupItem.cs:779) turns false and its OnDisable unregisters it (PickupItem.cs:342)
            // while ApplyPickupStateToScene is still iterating - which is exactly the shape that
            // relocates Last into Middle's slot. An ascending sweep leaves Last active and this test
            // fails; a descending sweep suppresses all three.
            const string tempScenePath = "Assets/_Project/Tests/Editor/SaveSystem/__WorldPickupStateSweepEditTests_Temp.unity";
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = default;
            GameObject managerHost = null;
            ItemData itemData = null;
            PickupItem[] pickups = null;
            try
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                Assert.IsTrue(EditorSceneManager.SaveScene(scene, tempScenePath));

                itemData = ScriptableObject.CreateInstance<ItemData>();
                itemData.name = "WorldPickupStateSweepEditTests.Item";

                int baselineRegistryCount = PickupItem.WorldStateRegistryCount;
                pickups = new PickupItem[3];
                long[] persistenceKeys = new long[3];
                long[] chunkKeys = new long[3];
                string[] names = { "First", "Middle", "Last" };

                for (int i = 0; i < pickups.Length; i++)
                {
                    PickupItem pickup = CreatePickup("WorldPickupStateSweepEditTests." + names[i]);
                    SceneManager.MoveGameObjectToScene(pickup.gameObject, scene);
                    SetPrivateField(pickup, "itemData", itemData);
                    SetPrivateField(pickup, "quantity", 2 + i);
                    SetPrivateField(pickup, "persistWorldState", true);
                    SetPrivateField(pickup, "stableWorldStateId", "sweep-stable-id-" + names[i]);
                    InvokePrivateInstance(pickup, "CaptureWorldStateRestoreBaseline");
                    InvokePrivateInstance(pickup, "InvalidateWorldStateIdentity");
                    InvokePrivateInstance(pickup, "CaptureWorldStateIdentityCold");
                    InvokePrivateInstance(pickup, "RegisterWorldStateRegistry");

                    Assert.IsTrue(
                        pickup.TryGetWorldStatePersistenceIdentity(out persistenceKeys[i], out chunkKeys[i]),
                        names[i] + " has no world-state identity, so the sweep would skip it for the wrong reason.");
                    Assert.AreNotEqual(0L, persistenceKeys[i]);
                    pickups[i] = pickup;
                }

                Assert.AreEqual(
                    baselineRegistryCount + 3,
                    PickupItem.WorldStateRegistryCount,
                    "All three pickups must be in the world-state registry before the sweep runs.");
                Assert.AreNotEqual(persistenceKeys[0], persistenceKeys[1]);
                Assert.AreNotEqual(persistenceKeys[1], persistenceKeys[2]);

                managerHost = new GameObject("WorldPickupStateSweepEditTests.WorldStateManager");
                WorldStateManager manager = managerHost.AddComponent<WorldStateManager>();
                SetPrivateField(manager, "_depletedNodeIds", new HashSet<string>());
                SetPrivateField(manager, "_depletedPickupKeys", new HashSet<long>());

                for (int i = 0; i < pickups.Length; i++)
                    manager.RegisterCollectedPickup(persistenceKeys[i], chunkKeys[i]);

                // Arm the mid-sweep swap-removal on the middle entry.
                SetPrivateField(pickups[1], "persistWorldState", false);

                for (int i = 0; i < pickups.Length; i++)
                    Assert.IsTrue(pickups[i].gameObject.activeSelf, names[i] + " must start active.");

                manager.ApplyToScene();

                for (int i = 0; i < pickups.Length; i++)
                {
                    Assert.IsFalse(
                        pickups[i].gameObject.activeSelf,
                        names[i] + " is still active after one sweep. A single ApplyToScene pass must suppress every "
                            + "depleted pickup; an ascending scan over the live registry count skips the entry that "
                            + "swap-removal relocated into the slot it just visited.");
                }

                Assert.AreEqual(
                    baselineRegistryCount + 2,
                    PickupItem.WorldStateRegistryCount,
                    "The middle pickup was expected to unregister during the sweep; without that mutation this "
                        + "fixture is not exercising the swap-removal hazard it exists to cover.");

                // Restoration side of the same sweep: clearing the depleted set must bring the still
                // registered pickups back, with the authored quantity restored rather than left at zero.
                SetPrivateField(pickups[0], "quantity", 0);
                SetPrivateField(pickups[2], "quantity", 0);
                manager.ClearAll();
                manager.ApplyToScene();

                Assert.IsTrue(pickups[0].gameObject.activeSelf, "First was not restored by the sweep.");
                Assert.IsTrue(pickups[2].gameObject.activeSelf, "Last was not restored by the sweep.");
                Assert.AreEqual(2, pickups[0].Quantity, "Restoration must replay the captured quantity baseline.");
                Assert.AreEqual(4, pickups[2].Quantity, "Restoration must replay the captured quantity baseline.");

                // The middle pickup opted out of persistence and left the registry, so the sweep can no
                // longer reach it. Asserted so that a future change of that contract shows up here
                // instead of silently altering which objects a load can restore.
                Assert.IsFalse(pickups[1].gameObject.activeSelf);
            }
            finally
            {
                if (managerHost != null)
                    UnityEngine.Object.DestroyImmediate(managerHost);

                if (pickups != null)
                {
                    for (int i = 0; i < pickups.Length; i++)
                    {
                        if (pickups[i] != null)
                            UnityEngine.Object.DestroyImmediate(pickups[i].gameObject);
                    }
                }

                if (itemData != null)
                    UnityEngine.Object.DestroyImmediate(itemData);

                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);

                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, removeScene: true);

                AssetDatabase.DeleteAsset(tempScenePath);
            }
        }

        [Test]
        public void WorldStateManager_LoadingAnEmptyWorldStateRestoresEverySuppressedNodeAndPickup()
        {
            // Replaces the worst source-text test in this fixture: 41 Does.Contain probes spread across
            // FIVE product files it does not own, plus eight Assert.Greater comparisons on raw IndexOf
            // offsets into SaveManager.cs and PersistentWorldRegistry.cs. Those offset comparisons pin
            // the ORDER OF STATEMENTS in files this fixture has no ownership of; the whole block is
            // green for a wrong implementation that happens to contain the same substrings, and red for
            // a correct one that renames a local.
            //
            // The name of that test made a behavioural claim - "restore pass does not early return when
            // loaded state is empty" - and its evidence was
            //   Does.Not.Contain("_depletedNodeIds.Count == 0")
            // which any rewrite to "< 1", "!= 0" or an extracted guard method defeats while reintroducing
            // the exact bug. Loading a save with no depletions is precisely when restoration has to run:
            // if the sweep bails out on an empty set, a player who reloads an earlier slot keeps every
            // node and pickup that a later session had already consumed.
            const string tempScenePath = "Assets/_Project/Tests/Editor/SaveSystem/__WorldPickupStateEmptyLoadEditTests_Temp.unity";
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = default;
            GameObject managerHost = null;
            GameObject nodeHost = null;
            ItemData itemData = null;
            PickupItem pickup = null;
            try
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                Assert.IsTrue(EditorSceneManager.SaveScene(scene, tempScenePath));

                itemData = ScriptableObject.CreateInstance<ItemData>();
                itemData.name = "WorldPickupStateEmptyLoadEditTests.Item";

                pickup = CreatePickup("WorldPickupStateEmptyLoadEditTests.Pickup");
                SceneManager.MoveGameObjectToScene(pickup.gameObject, scene);
                SetPrivateField(pickup, "itemData", itemData);
                SetPrivateField(pickup, "quantity", 5);
                SetPrivateField(pickup, "persistWorldState", true);
                SetPrivateField(pickup, "stableWorldStateId", "empty-load-stable-id");
                InvokePrivateInstance(pickup, "CaptureWorldStateRestoreBaseline");
                InvokePrivateInstance(pickup, "InvalidateWorldStateIdentity");
                InvokePrivateInstance(pickup, "CaptureWorldStateIdentityCold");
                InvokePrivateInstance(pickup, "RegisterWorldStateRegistry");

                Assert.IsTrue(pickup.TryGetWorldStatePersistenceIdentity(
                    out long pickupPersistenceKey,
                    out long pickupChunkKey));
                Assert.AreNotEqual(0L, pickupPersistenceKey);

                nodeHost = new GameObject("WorldPickupStateEmptyLoadEditTests.Node");
                ResourceNode node = nodeHost.AddComponent<ResourceNode>();
                node.SetUniqueId("empty-load-resource-node");

                managerHost = new GameObject("WorldPickupStateEmptyLoadEditTests.WorldStateManager");
                WorldStateManager manager = managerHost.AddComponent<WorldStateManager>();
                SetPrivateField(manager, "_depletedNodeIds", new HashSet<string>());
                SetPrivateField(manager, "_depletedPickupKeys", new HashSet<long>());

                manager.RegisterDepletedNode(node.UniqueId);
                manager.RegisterCollectedPickup(pickupPersistenceKey, pickupChunkKey);
                manager.ApplyToScene();

                Assert.IsFalse(node.gameObject.activeSelf, "Precondition: the depleted node must be suppressed first.");
                Assert.IsFalse(pickup.gameObject.activeSelf, "Precondition: the collected pickup must be suppressed first.");

                // Zero the authored quantity so a restore that only flips activeSelf is distinguishable
                // from one that replays the captured baseline.
                SetPrivateField(pickup, "quantity", 0);

                // A real save container that carries NO depletions - the exact input the dropped test
                // claimed to cover with a Does.Not.Contain on a literal count comparison.
                SaveData emptyWorldState = SaveData.CreateNew(0.0);
                Assert.AreEqual(0, emptyWorldState.worldState.depletedCount);
                Assert.AreEqual(0, emptyWorldState.worldState.depletedPickupWordCount);

                manager.LoadFromSaveData(emptyWorldState);

                Assert.AreEqual(0, manager.DepletedCount);
                Assert.AreEqual(0, manager.DepletedPickupCount);
                Assert.IsFalse(manager.IsNodeDepleted("empty-load-resource-node"));
                Assert.IsFalse(manager.IsPickupDepleted(pickupPersistenceKey));
                Assert.IsTrue(
                    node.gameObject.activeSelf,
                    "Loading a save with no depleted nodes left a previously suppressed node inactive. The restore "
                        + "pass must run when the loaded set is EMPTY - that is the only moment it can undo a "
                        + "suppression from an earlier session.");
                Assert.IsTrue(
                    pickup.gameObject.activeSelf,
                    "Loading a save with no collected pickups left a previously suppressed pickup inactive.");
                Assert.AreEqual(
                    5,
                    pickup.Quantity,
                    "The pickup was reactivated without replaying its captured quantity baseline, so the player "
                        + "gets an empty pickup back.");

                // LoadFromSaveData(null) takes a different branch (WorldStateManager.cs:301) and must
                // reach the same restore pass rather than only clearing runtime state.
                manager.RegisterDepletedNode(node.UniqueId);
                manager.RegisterCollectedPickup(pickupPersistenceKey, pickupChunkKey);
                manager.ApplyToScene();
                Assert.IsFalse(node.gameObject.activeSelf);
                Assert.IsFalse(pickup.gameObject.activeSelf);

                manager.LoadFromSaveData(null);

                Assert.AreEqual(0, manager.DepletedCount);
                Assert.AreEqual(0, manager.DepletedPickupCount);
                Assert.IsTrue(
                    node.gameObject.activeSelf,
                    "LoadFromSaveData(null) cleared runtime state without applying it to the scene.");
                Assert.IsTrue(
                    pickup.gameObject.activeSelf,
                    "LoadFromSaveData(null) cleared runtime state without applying it to the scene.");
            }
            finally
            {
                if (managerHost != null)
                    UnityEngine.Object.DestroyImmediate(managerHost);

                if (pickup != null)
                    UnityEngine.Object.DestroyImmediate(pickup.gameObject);

                if (nodeHost != null)
                    UnityEngine.Object.DestroyImmediate(nodeHost);

                if (itemData != null)
                    UnityEngine.Object.DestroyImmediate(itemData);

                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);

                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, removeScene: true);

                AssetDatabase.DeleteAsset(tempScenePath);
            }
        }

        [Test]
        public void DestructibleOrganicManager_SourceGuardsAcceptedDropsPublishItemLifecycle()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/World/DestructibleOrganicManager.cs"));
            string drainDropBuffer = ExtractMethodBody(source, "private bool DrainDropBuffer()");
            string publishLifecycle = ExtractMethodBody(source, "private static void PublishOrganicDropLifecycleCollected(");

            Assert.That(drainDropBuffer, Does.Contain("playerInventory.ScavengeAttempt(drop.ItemHashId, drop.Quantity, playerInventory.transform)"));
            Assert.That(drainDropBuffer, Does.Contain("rejectedQuantity = result.RejectedQuantity;"));
            Assert.That(drainDropBuffer, Does.Contain("PublishOrganicDropLifecycleCollected("));
            Assert.That(drainDropBuffer, Does.Contain("drop.Quantity - rejectedQuantity"));
            Assert.That(drainDropBuffer, Does.Contain("ToRuntimeVector3(drop.Position)"));
            Assert.That(publishLifecycle, Does.Contain("ItemData item = itemCatalog.FindByHash(itemHashId);"));
            Assert.That(publishLifecycle, Does.Contain("EntityId.ToULong(interactor.GetEntityId())"));
            Assert.That(publishLifecycle, Does.Contain("ItemLifecycleSignalRoute.TryPublishCollected("));
        }

        [Test]
        public void PlayerInventory_SourceGuardsDeferredScavengingAcceptedDropsPublishItemLifecycle()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/PlayerInventory.cs"));
            string captureSignals = ExtractMethodBody(source, "private void CaptureScavengingLootOracleSignals()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string populateSave = ExtractMethodBody(source, "public void PopulateSaveData(");
            string spillSignals = ExtractMethodBody(source, "private void SpillScavengingSignalsToWorldDrops(");
            string signalFilter = ExtractMethodBody(source, "private static bool IsPendingScavengingInventorySignal(");
            string mergeSignal = ExtractMethodBody(source, "private static bool TryMergePendingScavengingSignal(");
            string applyDeferred = ExtractMethodBody(source, "private void ApplyDeferredScavengingLootOracleSignals()");
            string publishLifecycle = ExtractMethodBody(source, "private void PublishPendingScavengingLifecycleCollected(");

            Assert.That(signalFilter, Does.Contain("signal.SourceKind == ItemAcquiredSignalSourceKinds.ScavengingLootOracle"));
            Assert.That(signalFilter, Does.Not.Contain("signal.SourceKind == ItemAcquiredSignalSourceKinds.DroneMining"));
            Assert.That(signalFilter, Does.Contain("signal.ItemHash != 0u"));
            Assert.That(signalFilter, Does.Contain("signal.Quantity != 0"));
            Assert.That(source, Does.Contain("private int _lastScavengingItemSignalCaptureGeneration = -1;"));
            Assert.That(captureSignals, Does.Contain("int snapshotGeneration = SignalBus<ItemAcquiredSignal>.SnapshotGeneration;"));
            Assert.That(captureSignals, Does.Contain("if (_lastScavengingItemSignalCaptureGeneration == snapshotGeneration)"));
            Assert.That(captureSignals, Does.Contain("_lastScavengingItemSignalCaptureGeneration = snapshotGeneration;"));
            Assert.That(captureSignals, Does.Contain("SpillScavengingSignalsToWorldDrops(signals);"));
            Assert.That(onDisable, Does.Contain("CaptureScavengingLootOracleSignals();"));
            AssertTextBefore(onDisable, "CaptureScavengingLootOracleSignals();", "ApplyDeferredScavengingLootOracleSignals();");
            Assert.That(onDestroy, Does.Contain("ApplyDeferredScavengingLootOracleSignals();"));
            Assert.That(onDestroy, Does.Contain("CaptureScavengingLootOracleSignals();"));
            AssertTextBefore(onDestroy, "CaptureScavengingLootOracleSignals();", "ApplyDeferredScavengingLootOracleSignals();");
            Assert.That(populateSave, Does.Contain("CaptureScavengingLootOracleSignals();"));
            AssertTextBefore(populateSave, "CaptureScavengingLootOracleSignals();", "ApplyDeferredScavengingLootOracleSignals();");
            Assert.That(captureSignals, Does.Contain("if (!IsPendingScavengingInventorySignal(in signal))"));
            Assert.That(captureSignals, Does.Contain("out int overflowQuantity"));
            Assert.That(captureSignals, Does.Contain("if (overflowQuantity > 0 &&"));
            Assert.That(captureSignals, Does.Contain("!TryRegisterPendingScavengingWorldDrop(in signal, overflowQuantity)"));
            Assert.That(captureSignals, Does.Contain("if (!TryRegisterPendingScavengingWorldDrop(in signal, signal.Quantity))"));
            Assert.That(captureSignals, Does.Contain("InventoryEvents.TryNotifyInventoryFull(unchecked((int)signal.ItemHash));"));
            Assert.That(captureSignals, Does.Contain("continue;"));
            Assert.That(captureSignals, Does.Not.Contain("if (writeIndex >= pending.Length)\r\n                    break;"));
            Assert.That(captureSignals, Does.Not.Contain("if (writeIndex >= pending.Length)\n                    break;"));
            Assert.That(spillSignals, Does.Contain("if (!IsPendingScavengingInventorySignal(in signal))"));
            Assert.That(spillSignals, Does.Contain("if (!TryRegisterPendingScavengingWorldDrop(in signal, signal.Quantity))"));
            Assert.That(spillSignals, Does.Contain("InventoryEvents.TryNotifyInventoryFull(unchecked((int)signal.ItemHash));"));
            Assert.That(mergeSignal, Does.Contain("overflowQuantity = 0;"));
            Assert.That(mergeSignal, Does.Contain("overflowQuantity = math.max(0, mergedQuantity - ushort.MaxValue);"));
            Assert.That(applyDeferred, Does.Contain("TryAddItemWithStateInternal("));
            Assert.That(applyDeferred, Does.Contain("int clampedAddedQuantity = math.clamp(addedQuantity, 0, requestedQuantity);"));
            Assert.That(applyDeferred, Does.Contain("PublishPendingScavengingLifecycleCollected(in signal, clampedAddedQuantity);"));
            Assert.That(applyDeferred, Does.Contain("int remainingQuantity = requestedQuantity - clampedAddedQuantity;"));
            Assert.That(publishLifecycle, Does.Contain("ItemData item = itemCatalog.FindByHash(unchecked((int)signal.ItemHash));"));
            Assert.That(publishLifecycle, Does.Contain("signal.PositionAup.TryToRuntimeFloat3(out float3 runtimePosition)"));
            Assert.That(publishLifecycle, Does.Contain("math.all(math.isfinite(runtimePosition))"));
            Assert.That(publishLifecycle, Does.Contain("EntityId.ToULong(gameObject.GetEntityId())"));
            Assert.That(publishLifecycle, Does.Contain("ItemLifecycleSignalRoute.TryPublishCollected("));
        }

        [Test]
        public void PlayerInventory_SourceGuardsInventoryCommandQueueOverflowPublishesFailure()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/PlayerInventory.cs"));
            string respawnJobs = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Physiology/ShinobuRespawnJobs.cs"));
            string respawnReconciliation = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs"));
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string consumeCommands = ExtractMethodBody(source, "private void ConsumeDeferredInventoryCommandSignals()");
            string captureCommands = ExtractMethodBody(source, "private void CaptureInventoryCommandSignals()");
            string dropDeferred = ExtractMethodBody(source, "private void DropDeferredInventoryCommandSignals()");
            string dropCommands = ExtractMethodBody(source, "private void DropInventoryCommandSignals(");
            string dropCommand = ExtractMethodBody(source, "private void DropInventoryCommandSignal(");
            string recordDroppedCommands = ExtractMethodBody(source, "private void RecordDroppedInventoryCommandSignals(");
            string commandFilter = ExtractMethodBody(source, "private static bool IsPendingInventoryCommandForOwner(");
            string resolveDeathAupSideband = ExtractMethodBody(source, "private bool TryResolveRespawnDeathAupSideband(");
            string emitPenalty = ExtractMethodBody(respawnJobs, "private uint EmitInventoryPenalty(");

            Assert.That(source, Does.Contain("public int DroppedInventoryCommandSignalCount => _droppedInventoryCommandSignalCount;"));
            Assert.That(onDisable, Does.Contain("DropDeferredInventoryCommandSignals();"));
            Assert.That(onDisable, Does.Not.Contain("_pendingInventoryCommandCount = 0;"));
            Assert.That(onDestroy, Does.Contain("DropDeferredInventoryCommandSignals();"));
            Assert.That(consumeCommands, Does.Contain("int safeCount = math.min(count, commands.Length);"));
            Assert.That(consumeCommands, Does.Contain("bool shouldSort = false;"));
            Assert.That(consumeCommands, Does.Contain("commands[index] = default;"));
            Assert.That(consumeCommands, Does.Contain("RecordDroppedInventoryCommandSignals(count);"));
            Assert.That(consumeCommands, Does.Contain("RecordDroppedInventoryCommandSignals(count - safeCount);"));
            Assert.That(consumeCommands, Does.Contain("if (shouldSort)"));
            Assert.That(consumeCommands, Does.Not.Contain("SortInventory();\r\n                return;"));
            Assert.That(consumeCommands, Does.Not.Contain("SortInventory();\n                return;"));
            Assert.That(captureCommands, Does.Contain("DropInventoryCommandSignals(commands, ResolveInventorySignalHash());"));
            Assert.That(captureCommands, Does.Contain("if (!IsPendingInventoryCommandForOwner(in command, inventoryHash))"));
            Assert.That(captureCommands, Does.Contain("DropInventoryCommandSignal(in command);"));
            Assert.That(captureCommands, Does.Not.Contain("if (writeIndex >= pending.Length)\r\n                    break;"));
            Assert.That(captureCommands, Does.Not.Contain("if (writeIndex >= pending.Length)\n                    break;"));
            Assert.That(dropDeferred, Does.Contain("int count = _pendingInventoryCommandCount;"));
            Assert.That(dropDeferred, Does.Contain("_pendingInventoryCommandCount = 0;"));
            Assert.That(dropDeferred, Does.Contain("RecordDroppedInventoryCommandSignals(count);"));
            Assert.That(dropDeferred, Does.Contain("int safeCount = math.min(count, pending.Length);"));
            Assert.That(dropDeferred, Does.Contain("pending[index] = default;"));
            Assert.That(dropDeferred, Does.Contain("DropInventoryCommandSignal(in command);"));
            Assert.That(dropDeferred, Does.Contain("RecordDroppedInventoryCommandSignals(count - safeCount);"));
            Assert.That(dropCommands, Does.Contain("if (!IsPendingInventoryCommandForOwner(in command, inventoryHash))"));
            Assert.That(dropCommands, Does.Contain("DropInventoryCommandSignal(in command);"));
            Assert.That(dropCommand, Does.Contain("RecordDroppedInventoryCommandSignal();"));
            Assert.That(dropCommand, Does.Contain("PublishRespawnDropPenaltyResult(in command, 0);"));
            Assert.That(recordDroppedCommands, Does.Contain("droppedCount <= 0"));
            Assert.That(recordDroppedCommands, Does.Contain("int remaining = int.MaxValue - _droppedInventoryCommandSignalCount;"));
            Assert.That(recordDroppedCommands, Does.Contain("_droppedInventoryCommandSignalCount += math.min(droppedCount, remaining);"));
            Assert.That(commandFilter, Does.Contain("command.InventoryHash == 0u || command.InventoryHash == inventoryHash"));
            Assert.That(commandFilter, Does.Contain("command.Command == InventoryCommandSignalCommands.DropNonEquippedResources"));
            Assert.That(commandFilter, Does.Contain("command.Command == InventoryCommandSignalCommands.Sort"));
            Assert.That(respawnJobs, Does.Contain("command.Command = InventoryCommandSignalCommands.DropNonEquippedResources;"));
            Assert.That(emitPenalty, Does.Contain("SignalBus<InventoryRespawnDeathAupSignal>.TryEnqueueBounded"));
            Assert.That(emitPenalty, Does.Contain("command.PayloadFlags |= InventoryCommandSignalPayloadFlags.RespawnDeathAupSideband;"));
            Assert.That(emitPenalty, Does.Contain("if ((request.Flags & ShinobuRespawnFlags.NanDetected) != 0u)"));
            Assert.That(emitPenalty, Does.Contain("sideband.Flags |= 0x80000000u;"));
            Assert.That(resolveDeathAupSideband, Does.Contain("(signal.Flags & 0x80000000u) != 0u"));
            Assert.That(emitPenalty, Does.Contain("return SignalBus<InventoryCommandSignal>.TryEnqueueBounded(InventoryCommands, InventoryCommandsBudget, command)"));
            Assert.That(emitPenalty, Does.Contain("? ShinobuRespawnFlags.PenaltyApplied"));
            Assert.That(emitPenalty, Does.Contain(": 0u;"));
            Assert.That(emitPenalty, Does.Not.Contain("return ShinobuRespawnFlags.PenaltyApplied;"));
            Assert.That(respawnReconciliation, Does.Contain("SignalBus<InventoryRespawnPenaltyResultSignal>.GetFrameSnapshot()"));
            Assert.That(respawnReconciliation, Does.Contain("TryWriteDroppedItemTelemetry(signal.DroppedCount);"));
        }

        private static void SetPrivateField<T>(WorldStateManager manager, string fieldName, T value)
        {
            FieldInfo field = typeof(WorldStateManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(manager, value);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, signature);

            int openBrace = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(openBrace, 0, signature);

            int depth = 0;
            for (int i = openBrace; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(openBrace, i - openBrace + 1);
                }
            }

            Assert.Fail("Unbalanced method body: " + signature);
            return string.Empty;
        }

        private static void AssertTextBefore(string source, string expectedEarlier, string expectedLater)
        {
            int earlierIndex = source.IndexOf(expectedEarlier, StringComparison.Ordinal);
            int laterIndex = source.IndexOf(expectedLater, StringComparison.Ordinal);
            Assert.GreaterOrEqual(earlierIndex, 0, expectedEarlier);
            Assert.GreaterOrEqual(laterIndex, 0, expectedLater);
            Assert.Less(earlierIndex, laterIndex, $"{expectedEarlier} should appear before {expectedLater}");
        }

        private static PickupItem CreatePickup(string name)
        {
            GameObject host = new GameObject(name);

            // PickupItem carries [RequireComponent(typeof(InteractionHighlighter))] and
            // [RequireComponent(typeof(Collider))] (PickupItem.cs:21-22). Collider is ABSTRACT, so
            // AddComponent<PickupItem>() cannot satisfy that dependency on its own - Unity has no
            // concrete collider to pick, refuses the add, and returns null. Every caller of this
            // helper then dereferenced that null on the very next line, which is why all five tests
            // that build a PickupItem threw NullReferenceException before reaching any product code,
            // in all three recorded batchmode runs. Adding the concrete dependencies first is what
            // lets the PickupItem add succeed; BoxCollider is the concrete collider this test suite
            // already uses for the same purpose (PlayerHealthSaveBridgeEditTests.cs:94).
            host.AddComponent<BoxCollider>();
            host.AddComponent<InteractionHighlighter>();

            PickupItem pickup = host.AddComponent<PickupItem>();
            Assert.IsNotNull(
                pickup,
                "AddComponent<PickupItem> returned null - a [RequireComponent] dependency of PickupItem is not satisfiable on this host GameObject. Satisfy the concrete dependency here instead of letting callers dereference null.");
            return pickup;
        }

        private static string ReadSerializedString(UnityEngine.Object target, string propertyName)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.UpdateIfRequiredOrScript();
            SerializedProperty property = serialized.FindProperty(propertyName);
            Assert.IsNotNull(property, propertyName);
            return property.stringValue;
        }

        private static void InvokePrivateInstance(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            method.Invoke(target, null);
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private static T ReadPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            return (T)field.GetValue(target);
        }

        /// <summary>
        /// Asserts that a validator entry point carries the expected <see cref="MenuItem"/> path on the
        /// COMPILED attribute. Unlike a substring probe on the source file this cannot disagree with the
        /// menu Unity actually registers, and it fails when the method is renamed or the attribute is
        /// dropped rather than only when the literal text moves.
        /// </summary>
        private static void AssertValidatorMenuItemPath(string methodName, string expectedMenuPath)
        {
            MethodInfo method = typeof(WorldPickupStateAuthoringValidator).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(method, "Missing WorldPickupStateAuthoringValidator method: " + methodName);

            MenuItem menuItem = (MenuItem)Attribute.GetCustomAttribute(method, typeof(MenuItem));
            Assert.IsNotNull(menuItem, methodName + " has no MenuItem attribute, so the authoring entry point is unreachable.");
            Assert.AreEqual(
                expectedMenuPath,
                menuItem.menuItem,
                "The registered menu path does not match the path this project's authoring instructions tell people to use.");
        }
    }
}
