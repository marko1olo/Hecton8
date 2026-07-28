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
        public void PickupItem_OnValidateRegeneratesDuplicateStableWorldStateIdsInEditorSceneScope()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Items/PickupItem.cs"));
            string onValidate = ExtractMethodBody(source, "private void OnValidate()");
            string duplicateCheck = ExtractMethodBody(
                source,
                "private bool HasDuplicateStableWorldStateIdInOpenScenes(string normalizedStableId)");

            string normalizedOnValidate = onValidate.Replace("\r\n", "\n").TrimStart();
            Assert.IsTrue(
                normalizedOnValidate.StartsWith("{\n            InvalidateWorldStateIdentity();", StringComparison.Ordinal),
                "OnValidate must invalidate cached persistence identity before early returns.");
            Assert.That(onValidate, Does.Contain("HasDuplicateStableWorldStateIdInOpenScenes(normalizedStableId)"));
            Assert.That(onValidate, Does.Contain("gameObject.scene.path.EndsWith(\".unity\", StringComparison.OrdinalIgnoreCase)"));
            Assert.That(onValidate, Does.Contain("itemData == null || string.IsNullOrWhiteSpace(itemData.PersistentId)"));
            Assert.That(onValidate, Does.Contain("Persistent scene pickup cannot seed stableWorldStateId without item persistent ID."));
            Assert.That(onValidate, Does.Contain("Guid.NewGuid().ToString(\"N\")"));
            Assert.That(onValidate, Does.Contain("UnityEditor.Undo.RecordObject(this"));
            Assert.That(onValidate, Does.Contain("UnityEditor.EditorUtility.SetDirty(this)"));
            Assert.That(onValidate, Does.Contain("UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene)"));
            Assert.That(duplicateCheck, Does.Contain("UnityEngine.Object.FindObjectsByType<PickupItem>"));
            Assert.That(duplicateCheck, Does.Contain("UnityEngine.FindObjectsInactive.Include"));
            Assert.That(duplicateCheck, Does.Contain("candidate.gameObject.scene.path"));
            Assert.That(duplicateCheck, Does.Contain("!candidate.persistWorldState"));
            Assert.That(duplicateCheck, Does.Contain("candidate.stableWorldStateId.Trim()"));
            Assert.That(duplicateCheck, Does.Contain("return true"));
            Assert.That(source, Does.Not.Contain("UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow"));
        }

        [Test]
        public void WorldPickupStateAuthoringValidator_SourceGuardsStableIdRoutingAndUnresolvedIssues()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Editor/SaveSystem/WorldPickupStateAuthoringValidator.cs"));

            // "Hecton8/", not "Hecton/". The source has always carried the Hecton8 prefix that every
            // other menu in this project uses, so "[MenuItem(\"Hecton/" was never a substring of it and
            // both of these assertions were red. The validator's own two user-facing instruction strings
            // had the same typo and told people to run a menu path that does not exist; they now say
            // Hecton8 as well. Fixing only one side would have re-broken the other.
            Assert.That(source, Does.Contain("[MenuItem(\"Hecton8/Validation/Validate World Pickup Stable IDs\")]"));
            Assert.That(source, Does.Contain("[MenuItem(\"Hecton8/Authoring/Seed World Pickup Stable IDs In Open Scenes\")]"));
            Assert.That(source, Does.Contain("WorldPickupStableIdBuildGate : IProcessSceneWithReport"));
            Assert.That(source, Does.Contain("throw new BuildFailedException"));
            Assert.That(source, Does.Contain("internal static int ScanOpenScenePickups(bool repair, string requiredScenePath)"));
            Assert.That(source, Does.Contain("internal static WorldPickupStableIdScanResult ScanOpenScenePickupStableIds(bool repair, string requiredScenePath)"));
            Assert.That(source, Does.Contain("UnityEngine.Object.FindObjectsByType<PickupItem>"));
            Assert.That(source, Does.Contain("FindObjectsInactive.Include"));
            Assert.That(source, Does.Contain("requiredScenePath"));
            Assert.That(source, Does.Contain("pickup.gameObject.scene.path.EndsWith(\".unity\", StringComparison.OrdinalIgnoreCase)"));
            Assert.That(source, Does.Contain("FindProperty(PersistWorldStateProperty)"));
            Assert.That(source, Does.Contain("FindProperty(StableWorldStateIdProperty)"));
            Assert.That(source, Does.Contain("itemData.PersistentId"));
            Assert.That(source, Does.Contain("MaxStableIdRepairAttempts"));
            Assert.That(source, Does.Contain("AssignNewStableId"));
            Assert.That(source, Does.Contain("Undo.RecordObject"));
            Assert.That(source, Does.Contain("serialized.ApplyModifiedProperties()"));
            Assert.That(source, Does.Contain("EditorSceneManager.MarkSceneDirty"));
            Assert.That(source, Does.Contain("UnresolvedCount"));
            Assert.That(source, Does.Contain("Duplicate pickup stable ID remains unresolved"));
            Assert.That(source, Does.Contain("BuildIdentityKey(pickup.gameObject.scene.path, stableId)"));
        }

        [Test]
        public void WorldStateManager_ApplyPickupStateScansRegistryBackwardsForSwapRemoval()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/WorldStateManager.cs"));
            string applyPickupState = ExtractMethodBody(source, "private void ApplyPickupStateToScene()");

            Assert.That(applyPickupState, Does.Contain("for (int i = PickupItem.WorldStateRegistryCount - 1; i >= 0; i--)"));
            Assert.That(applyPickupState, Does.Contain("TryResolveOrPromoteCollectedPickup(persistenceKey, chunkKey, legacyPersistenceKey)"));
            Assert.That(applyPickupState, Does.Contain("pickup.ApplyWorldStateSuppression();"));
            Assert.That(applyPickupState, Does.Contain("pickup.TryRestoreWorldStateSuppression()"));
            Assert.That(applyPickupState, Does.Not.Contain("pickup.gameObject.SetActive(false);"));
            Assert.That(applyPickupState, Does.Not.Contain("pickup.gameObject.SetActive(true);"));
            Assert.That(applyPickupState, Does.Not.Contain("for (int i = 0; i < pickupCount; i++)"));
        }

        [Test]
        public void WorldStateManager_SourceGuardsRestorePassDoesNotEarlyReturnWhenLoadedStateIsEmpty()
        {
            string managerSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/WorldStateManager.cs"));
            string loadFromSaveData = ExtractMethodBody(managerSource, "public void LoadFromSaveData(SaveData data)");
            string applyToScene = ExtractMethodBody(managerSource, "public void ApplyToScene()");
            string applyPickupState = ExtractMethodBody(managerSource, "private void ApplyPickupStateToScene()");

            Assert.That(loadFromSaveData, Does.Contain("if (data == null)"));
            Assert.That(loadFromSaveData, Does.Contain("ClearAll();"));
            Assert.That(loadFromSaveData, Does.Contain("ApplyToScene();"));
            Assert.That(applyToScene, Does.Not.Contain("_depletedNodeIds.Count == 0"));
            Assert.That(applyToScene, Does.Contain("node.ApplyWorldStateSuppression();"));
            Assert.That(applyToScene, Does.Contain("node.TryRestoreWorldStateSuppression()"));
            Assert.That(applyToScene, Does.Not.Contain("node.gameObject.SetActive(true);"));
            Assert.That(applyToScene, Does.Not.Contain("node.gameObject.SetActive(false);"));
            Assert.That(applyToScene, Does.Contain("for (int i = ResourceNode.WorldStateRegistryCount - 1; i >= 0; i--)"));
            Assert.That(applyPickupState, Does.Not.Contain("_depletedPickupKeys.Count == 0"));
            Assert.That(applyPickupState, Does.Contain("pickup.TryRestoreWorldStateSuppression()"));

            string pickupSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Items/PickupItem.cs"));
            Assert.That(pickupSource, Does.Contain("ShouldRetainWorldStateRegistryWhileInactive()"));
            Assert.That(pickupSource, Does.Contain("if (!ShouldRetainWorldStateRegistryWhileInactive())"));
            Assert.That(pickupSource, Does.Contain("_worldStateSuppressedByPersistence"));
            Assert.That(pickupSource, Does.Contain("_worldStateRestoreQuantity"));
            Assert.That(pickupSource, Does.Contain("internal void ApplyWorldStateSuppression()"));
            Assert.That(pickupSource, Does.Contain("internal bool TryRestoreWorldStateSuppression()"));
            Assert.That(pickupSource, Does.Contain("CaptureWorldStateRestoreBaseline()"));
            Assert.That(pickupSource, Does.Contain("PublishItemLifecycleCollectedSignal(attempt.AddedQuantity, interactor);"));
            Assert.That(pickupSource, Does.Contain("ItemLifecycleSignalRoute.TryPublishCollected"));
            Assert.That(pickupSource, Does.Contain("private static WorldStateManager s_worldStateManager;"));
            Assert.That(pickupSource, Does.Contain("case GlobalRegistryServiceSlot.WorldStateRuntime:"));
            Assert.That(pickupSource, Does.Contain("private WorldStateManager ResolveWorldStateManager()"));
            Assert.That(pickupSource, Does.Contain("ResolveWorldStateManager()?.RegisterCollectedPickup"));
            Assert.That(pickupSource, Does.Not.Contain("_worldStateManager?.RegisterCollectedPickup"));

            string hectonItemSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/HectonItem.cs"));
            Assert.That(hectonItemSource, Does.Contain("PublishItemLifecycleCollectedSignal(attempt.AddedQuantity, interactor);"));
            Assert.That(hectonItemSource, Does.Contain("ItemLifecycleSignalRoute.TryPublishCollected"));

            string resourceNodeSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/ResourceNode.cs"));
            string resourceNodeRestore = ExtractMethodBody(resourceNodeSource, "internal bool TryRestoreWorldStateSuppression()");
            Assert.That(resourceNodeSource, Does.Contain("if (IsPooledInstance())"));
            Assert.That(resourceNodeSource, Does.Contain("UnregisterWorldStateRegistry();"));
            Assert.That(resourceNodeSource, Does.Contain("_worldStateSuppressedByPersistence"));
            Assert.That(resourceNodeSource, Does.Contain("internal void ApplyWorldStateSuppression()"));
            Assert.That(resourceNodeSource, Does.Contain("internal bool TryRestoreWorldStateSuppression()"));
            Assert.That(resourceNodeSource, Does.Contain("internal static int ApplyPersistentWorldRegistryStateToRegisteredNodes()"));
            Assert.That(resourceNodeSource, Does.Contain("internal static int ApplyPersistentWorldRegistryStateToRegisteredNodes(PersistentWorldRegistry registry)"));
            Assert.That(resourceNodeSource, Does.Contain("EnsureRegistryCache();"));
            Assert.That(resourceNodeSource, Does.Contain("s_persistentWorldRegistry = registry;"));
            Assert.That(resourceNodeSource, Does.Contain("node.ShouldSuppressSpawn()"));
            Assert.That(resourceNodeSource, Does.Contain("ResetState();"));
            Assert.That(resourceNodeRestore, Does.Contain("EnsureRegistryCache();"));
            Assert.That(resourceNodeRestore, Does.Contain("RefreshPersistentIdentity();"));
            Assert.That(resourceNodeRestore, Does.Contain("if (ShouldSuppressSpawn())"));

            string saveManagerSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/SaveManager.cs"));
            int indexedRestoreIndex = saveManagerSource.IndexOf(
                "persistentWorldRegistryForLoad.RestoreFromIndexedSave",
                StringComparison.Ordinal);
            int fallbackRestoreIndex = saveManagerSource.IndexOf(
                "persistentWorldRegistryForLoad.RestoreFromLoadedRecords",
                StringComparison.Ordinal);
            int resourcePostRestorePassIndex = saveManagerSource.IndexOf(
                "ResourceNode.ApplyPersistentWorldRegistryStateToRegisteredNodes(persistentWorldRegistryForLoad)",
                StringComparison.Ordinal);

            Assert.GreaterOrEqual(indexedRestoreIndex, 0);
            Assert.GreaterOrEqual(fallbackRestoreIndex, 0);
            Assert.GreaterOrEqual(resourcePostRestorePassIndex, 0);
            Assert.Greater(resourcePostRestorePassIndex, indexedRestoreIndex);
            Assert.Greater(resourcePostRestorePassIndex, fallbackRestoreIndex);

            string persistentWorldRegistrySource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/World/PersistentWorldRegistry.cs"));
            int indexedSectorRestoreIndex = persistentWorldRegistrySource.IndexOf(
                "RestoreFromLoadedRecords(stagedRecords, scheduleHydration: false);",
                StringComparison.Ordinal);
            int indexedSectorPostRestorePassIndex = persistentWorldRegistrySource.IndexOf(
                "ResourceNode.ApplyPersistentWorldRegistryStateToRegisteredNodes(this)",
                Math.Max(0, indexedSectorRestoreIndex),
                StringComparison.Ordinal);

            Assert.GreaterOrEqual(indexedSectorRestoreIndex, 0);
            Assert.GreaterOrEqual(indexedSectorPostRestorePassIndex, 0);
            Assert.Greater(indexedSectorPostRestorePassIndex, indexedSectorRestoreIndex);
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
    }
}
