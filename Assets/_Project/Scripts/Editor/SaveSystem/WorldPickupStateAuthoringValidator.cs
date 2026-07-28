#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Interaction;
using Hecton8.Items;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor.SaveSystem
{
    internal static class WorldPickupStateAuthoringValidator
    {
        private const string StableWorldStateIdProperty = "stableWorldStateId";
        private const string PersistWorldStateProperty = "persistWorldState";
        private const int MaxStableIdRepairAttempts = 8;

        [MenuItem("Hecton8/Validation/Validate World Pickup Stable IDs")]
        private static void ValidateOpenScenePickupStableIds()
        {
            WorldPickupStableIdScanResult result = ScanOpenScenePickupStableIds(repair: false);
            if (result.IssueCount == 0)
                Debug.Log("[WorldPickupStateAuthoringValidator] PASS open-scene persistent pickups have non-empty unique stable IDs.");
            else
                Debug.LogError($"[WorldPickupStateAuthoringValidator] FAIL issues={result.IssueCount}. Run Hecton8/Authoring/Seed World Pickup Stable IDs In Open Scenes to repair open-scene IDs.");
        }

        [MenuItem("Hecton8/Authoring/Seed World Pickup Stable IDs In Open Scenes")]
        private static void SeedOpenScenePickupStableIds()
        {
            WorldPickupStableIdScanResult result = ScanOpenScenePickupStableIds(repair: true);
            Debug.Log($"[WorldPickupStateAuthoringValidator] COMPLETE issues={result.IssueCount} repaired={result.RepairedCount} unresolved={result.UnresolvedCount}.");
        }

        internal static int ScanOpenScenePickups(bool repair)
        {
            return ScanOpenScenePickups(repair, requiredScenePath: null);
        }

        internal static int ScanOpenScenePickups(bool repair, string requiredScenePath)
        {
            return ScanOpenScenePickupStableIds(repair, requiredScenePath).IssueCount;
        }

        internal static WorldPickupStableIdScanResult ScanOpenScenePickupStableIds(bool repair)
        {
            return ScanOpenScenePickupStableIds(repair, requiredScenePath: null);
        }

        internal static WorldPickupStableIdScanResult ScanOpenScenePickupStableIds(bool repair, string requiredScenePath)
        {
            PickupItem[] pickups = UnityEngine.Object.FindObjectsByType<PickupItem>(
                FindObjectsInactive.Include);
            Dictionary<string, PickupItem> firstByIdentity = new Dictionary<string, PickupItem>(pickups.Length, StringComparer.Ordinal);
            int issueCount = 0;
            int repairedCount = 0;
            int unresolvedCount = 0;

            for (int i = 0; i < pickups.Length; i++)
            {
                PickupItem pickup = pickups[i];
                if (!IsPersistentScenePickup(pickup))
                    continue;

                if (!string.IsNullOrEmpty(requiredScenePath) &&
                    !string.Equals(pickup.gameObject.scene.path, requiredScenePath, StringComparison.Ordinal))
                {
                    continue;
                }

                SerializedObject serialized = new SerializedObject(pickup);
                serialized.UpdateIfRequiredOrScript();
                SerializedProperty stableIdProperty = serialized.FindProperty(StableWorldStateIdProperty);
                if (stableIdProperty == null)
                {
                    issueCount++;
                    unresolvedCount++;
                    Debug.LogError("[WorldPickupStateAuthoringValidator] PickupItem missing serialized stableWorldStateId field.", pickup);
                    continue;
                }

                ItemData itemData = pickup.ItemData;
                if (itemData == null || string.IsNullOrWhiteSpace(itemData.PersistentId))
                {
                    issueCount++;
                    unresolvedCount++;
                    Debug.LogError("[WorldPickupStateAuthoringValidator] Persistent pickup has no item persistent ID.", pickup);
                    continue;
                }

                string stableId = string.IsNullOrWhiteSpace(stableIdProperty.stringValue)
                    ? string.Empty
                    : stableIdProperty.stringValue.Trim();
                if (string.IsNullOrEmpty(stableId))
                {
                    issueCount++;
                    if (!repair)
                    {
                        Debug.LogError("[WorldPickupStateAuthoringValidator] Persistent pickup has empty stableWorldStateId.", pickup);
                        continue;
                    }

                    stableId = AssignNewStableId(serialized, stableIdProperty, pickup);
                    repairedCount++;
                }
                else if (!string.Equals(stableIdProperty.stringValue, stableId, StringComparison.Ordinal))
                {
                    issueCount++;
                    if (!repair)
                    {
                        Debug.LogError("[WorldPickupStateAuthoringValidator] Persistent pickup stableWorldStateId needs trimming.", pickup);
                        continue;
                    }

                    Undo.RecordObject(pickup, "Trim World Pickup Stable ID");
                    stableIdProperty.stringValue = stableId;
                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(pickup);
                    EditorSceneManager.MarkSceneDirty(pickup.gameObject.scene);
                    repairedCount++;
                }

                string identity = BuildIdentityKey(pickup.gameObject.scene.path, stableId);
                if (!firstByIdentity.TryGetValue(identity, out PickupItem first))
                {
                    firstByIdentity.Add(identity, pickup);
                    continue;
                }

                issueCount++;
                if (!repair)
                {
                    Debug.LogError(
                        $"[WorldPickupStateAuthoringValidator] Duplicate pickup stable ID collides with {first.name}: {stableId}",
                        pickup);
                    continue;
                }

                int repairAttempts = 0;
                do
                {
                    stableId = AssignNewStableId(serialized, stableIdProperty, pickup);
                    identity = BuildIdentityKey(pickup.gameObject.scene.path, stableId);
                    repairAttempts++;
                }
                while (repairAttempts < MaxStableIdRepairAttempts && firstByIdentity.ContainsKey(identity));

                if (firstByIdentity.ContainsKey(identity))
                {
                    unresolvedCount++;
                    Debug.LogError(
                        $"[WorldPickupStateAuthoringValidator] Duplicate pickup stable ID remains unresolved after {MaxStableIdRepairAttempts} repair attempts: {stableId}",
                        pickup);
                    continue;
                }

                repairedCount++;
                firstByIdentity.Add(identity, pickup);
            }

            return new WorldPickupStableIdScanResult(issueCount, repairedCount, unresolvedCount);
        }

        private static bool IsPersistentScenePickup(PickupItem pickup)
        {
            if (pickup == null ||
                pickup.gameObject == null ||
                !pickup.gameObject.scene.IsValid() ||
                string.IsNullOrEmpty(pickup.gameObject.scene.path) ||
                !pickup.gameObject.scene.path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                pickup.TryGetComponent(out ObjectPoolManager.PoolItemMarker _))
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(pickup);
            serialized.UpdateIfRequiredOrScript();
            SerializedProperty persistProperty = serialized.FindProperty(PersistWorldStateProperty);
            return persistProperty == null || persistProperty.boolValue;
        }

        private static string AssignNewStableId(SerializedObject serialized, SerializedProperty stableIdProperty, PickupItem pickup)
        {
            string stableId = Guid.NewGuid().ToString("N");
            Undo.RecordObject(pickup, "Seed World Pickup Stable ID");
            stableIdProperty.stringValue = stableId;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(pickup);
            EditorSceneManager.MarkSceneDirty(pickup.gameObject.scene);
            return stableId;
        }

        private static string BuildIdentityKey(string scenePath, string stableId)
        {
            return scenePath + "\n" + stableId;
        }
    }

    internal sealed class WorldPickupStableIdBuildGate : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (!scene.IsValid() ||
                string.IsNullOrEmpty(scene.path) ||
                !scene.path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            WorldPickupStableIdScanResult result = WorldPickupStateAuthoringValidator.ScanOpenScenePickupStableIds(
                repair: false,
                requiredScenePath: scene.path);
            if (result.IssueCount == 0)
                return;

            Debug.LogWarning($"[WorldPickupStableIdBuildGate] Scene {scene.path} has {result.IssueCount} persistent pickup stable ID issue(s), unresolved={result.UnresolvedCount}. " +
                "Run Hecton8/Authoring/Seed World Pickup Stable IDs In Open Scenes, then fix unresolved item identity errors before building.");
        }
    }

    internal readonly struct WorldPickupStableIdScanResult
    {
        public WorldPickupStableIdScanResult(int issueCount, int repairedCount, int unresolvedCount)
        {
            IssueCount = issueCount;
            RepairedCount = repairedCount;
            UnresolvedCount = unresolvedCount;
        }

        public int IssueCount { get; }
        public int RepairedCount { get; }
        public int UnresolvedCount { get; }
    }
}
#endif
