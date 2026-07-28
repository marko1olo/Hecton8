// ============================================================================
// HECTON-8 — StorageEndpointAuthoring.cs
// Authors the base-logistics STORAGE ENDPOINT that twelve live consumers
// already query and never find.
//
// WHY A COMPONENT ON A MODULE PREFAB, NOT A STANDALONE PROP:
//   • BaseModule.cs:2790 / :2830 call TryGetComponent(out StorageCrate) ON
//     THEMSELVES for deconstruct-eject, and ConstructionManager.cs:2617 /
//     :2947 call module.TryGetComponent(out StorageCrate) for module save and
//     restore. A crate that is not a component of the BaseModule GameObject is
//     invisible to persistence and to deconstruction.
//   • BaseLogisticsNetwork.cs:165-167 rejects any crate whose PowerNode is
//     null, so the same GameObject (or an ancestor, StorageCrate.cs:417-418)
//     must carry a PowerNode or the endpoint never enters the registry.
//     PowerNode.cs:38-41 states that as authoring law: "Povesit PowerNode na
//     finalPrefab modulya bazy" / "ModuleMarker dolzhen byt nastroen s
//     BuildableData".
//   • StorageCrate.cs:1036-1058 ("never resizes storage at runtime") makes the
//     authored containedItems length the crate's real capacity. A crate with a
//     null or zero-length array is a permanently inert endpoint:
//     EnsureReservationCapacity (StorageCrate.cs:1214-1223) allocates zero
//     reservation slots and HasAutomatedCapacity always returns false.
//
// SCOPE DISCIPLINE:
//   Touches exactly one production prefab — HostPrefabPath — and nothing else.
//   No folder sweep, no scene write, no blanket apply. Mutation goes through
//   PrefabUtility prefab contents, the same route BaseModulePrefabIntegrity-
//   Enforcer.cs:20/44-45/50 uses, and the write is skipped entirely when the
//   prefab is already correct.
// ============================================================================

using Hecton8.Construction;
using Hecton8.Gameplay;
using Hecton8.Power;
using Hecton8.SaveSystem;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Authoring
{
    /// <summary>
    /// Attaches the habitat storage endpoint (StorageCrate + its PowerNode carrier)
    /// to the one base-module prefab that is a pressurized habitat interior.
    /// Idempotent: a second run detects the authored state and writes nothing.
    /// </summary>
    public static class StorageEndpointAuthoring
    {
        /// <summary>
        /// Host prefab. PFB_Module_Corridor is the only production prefab that is
        /// simultaneously a BaseModule carrier (census: BaseModule appears on
        /// PFB_Module_Corridor and PFB_Module_Foundation only) and a pressurized
        /// habitat interior (BaseModuleTemplate_CorridorStraight airVolumeM3 = 22,
        /// Build_Corridor_Straight family = Habitat, powerRating = -6). The
        /// foundation platform is airVolumeM3 = 0 / family Structure / powerRating 0
        /// — an exterior deck plate, not a place to author stowage.
        /// </summary>
        private const string HostPrefabPath =
            "Assets/_Project/Prefabs/Construction/Final/PFB_Module_Corridor.prefab";

        /// <summary>Serialized backing field name of StorageCrate.containedItems (StorageCrate.cs:106).</summary>
        private const string ContainedItemsPropertyName = "containedItems";

        private const string LogPrefix = "[StorageEndpointAuthoring]";

        /// <summary>
        /// Authored crate capacity, pinned to the module persistence cap.
        /// ConstructionManager writes at most ModuleDTO.MaxStorageCrateSlots entries
        /// per module (SaveData.cs:2549, ConstructionManager.cs:3085-3092), and the
        /// project already pins module container capacity to its DTO cap —
        /// CultivationManager.cs:23 does exactly this with ModuleDTO.MaxCultivationSlots.
        /// </summary>
        private const int AuthoredCrateSlotCount = ModuleDTO.MaxStorageCrateSlots;

        // ══════════════════════════════════════════════════════════
        //  ENTRY POINTS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Authoring entry point. Batch usage:
        /// -executeMethod Hecton8.Editor.Authoring.StorageEndpointAuthoring.AttachHabitatStorageEndpoint
        /// </summary>
        [MenuItem("Hecton8/Authoring/Attach Habitat Storage Endpoint", priority = 218)]
        public static void AttachHabitatStorageEndpoint()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(HostPrefabPath) == null)
            {
                Debug.LogError(
                    $"{LogPrefix} DECLINED: host prefab not found at '{HostPrefabPath}'. Nothing written.");
                return;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(HostPrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError(
                    $"{LogPrefix} DECLINED: '{HostPrefabPath}' could not be opened as prefab contents. Nothing written.");
                return;
            }

            bool wroteAsset = false;
            bool crateExisted = false;
            bool powerNodeExisted = false;
            int slotCountBefore = 0;
            int slotCountAfter = 0;

            try
            {
                if (!prefabRoot.TryGetComponent(out BaseModule _))
                {
                    Debug.LogError(
                        $"{LogPrefix} DECLINED: '{HostPrefabPath}' root has no BaseModule. " +
                        "BaseModule.cs:2790/:2830 and ConstructionManager.cs:2617/:2947 resolve the crate with " +
                        "TryGetComponent on the module itself, so a crate on a non-BaseModule root would be " +
                        "invisible to save and deconstruct. Nothing written.");
                    return;
                }

                if (!prefabRoot.TryGetComponent(out Collider hostCollider))
                {
                    Debug.LogError(
                        $"{LogPrefix} DECLINED: '{HostPrefabPath}' root has no Collider. " +
                        "StorageCrate.cs:56 declares [RequireComponent(typeof(Collider))] and Collider is abstract, " +
                        "so AddComponent cannot satisfy it. Author interaction collision on the prefab first. " +
                        "Nothing written.");
                    return;
                }

                crateExisted = prefabRoot.TryGetComponent(out StorageCrate crate);
                powerNodeExisted = prefabRoot.TryGetComponent(out PowerNode _);
                slotCountBefore = ReadAuthoredSlotCount(crate);

                Debug.Log(
                    $"{LogPrefix} BEFORE '{HostPrefabPath}': StorageCrate={(crateExisted ? "present" : "absent")}, " +
                    $"PowerNode={(powerNodeExisted ? "present" : "absent")}, crateSlots={slotCountBefore}, " +
                    $"rootCollider={hostCollider.GetType().Name}(isTrigger={hostCollider.isTrigger}), " +
                    $"moduleMarkerData={DescribeModuleMarkerData(prefabRoot)}.");

                bool changed = false;

                if (!crateExisted)
                {
                    crate = prefabRoot.AddComponent<StorageCrate>();
                    if (crate == null)
                    {
                        Debug.LogError(
                            $"{LogPrefix} DECLINED: AddComponent<StorageCrate> failed on '{HostPrefabPath}'. Nothing written.");
                        return;
                    }

                    changed = true;
                }

                if (!powerNodeExisted && prefabRoot.AddComponent<PowerNode>() == null)
                {
                    Debug.LogError(
                        $"{LogPrefix} DECLINED: AddComponent<PowerNode> failed on '{HostPrefabPath}'. " +
                        "Without a PowerNode BaseLogisticsNetwork.cs:165-167 drops the crate. Nothing written.");
                    return;
                }

                changed |= !powerNodeExisted;

                if (!TryEnsureAuthoredSlotCount(crate, out slotCountAfter, out bool slotCountChanged))
                    return;

                changed |= slotCountChanged;

                if (!changed)
                {
                    Debug.Log(
                        $"{LogPrefix} NO CHANGE: '{HostPrefabPath}' already authors StorageCrate + PowerNode with " +
                        $"{slotCountAfter} crate slots. Prefab not marked dirty, not saved.");
                    return;
                }

                EditorUtility.SetDirty(prefabRoot);
                if (PrefabUtility.SaveAsPrefabAsset(prefabRoot, HostPrefabPath) == null)
                {
                    Debug.LogError(
                        $"{LogPrefix} FAILED: SaveAsPrefabAsset returned null for '{HostPrefabPath}'. " +
                        "The prefab on disk is unchanged.");
                    return;
                }

                wroteAsset = true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            if (!wroteAsset)
                return;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"{LogPrefix} WROTE '{HostPrefabPath}': StorageCrate " +
                $"{(crateExisted ? "kept" : "added")}, PowerNode {(powerNodeExisted ? "kept" : "added")}, " +
                $"crateSlots {slotCountBefore} -> {slotCountAfter} (ModuleDTO.MaxStorageCrateSlots={AuthoredCrateSlotCount}). " +
                "One prefab touched.");
        }

        /// <summary>
        /// Read-only state report for the same host prefab. Writes nothing. Batch usage:
        /// -executeMethod Hecton8.Editor.Authoring.StorageEndpointAuthoring.ReportHabitatStorageEndpoint
        /// </summary>
        [MenuItem("Hecton8/Validation/Report Habitat Storage Endpoint", priority = 218)]
        public static void ReportHabitatStorageEndpoint()
        {
            GameObject hostAsset = AssetDatabase.LoadAssetAtPath<GameObject>(HostPrefabPath);
            if (hostAsset == null)
            {
                Debug.LogError($"{LogPrefix} REPORT: host prefab not found at '{HostPrefabPath}'.");
                return;
            }

            bool hasBaseModule = hostAsset.TryGetComponent(out BaseModule _);
            bool hasCollider = hostAsset.TryGetComponent(out Collider _);
            bool hasPowerNode = hostAsset.TryGetComponent(out PowerNode _);
            bool hasCrate = hostAsset.TryGetComponent(out StorageCrate crate);

            Debug.Log(
                $"{LogPrefix} REPORT '{HostPrefabPath}': BaseModule={hasBaseModule}, rootCollider={hasCollider}, " +
                $"StorageCrate={hasCrate}, crateSlots={ReadAuthoredSlotCount(crate)} " +
                $"(target {AuthoredCrateSlotCount}), PowerNode={hasPowerNode}, " +
                $"moduleMarkerData={DescribeModuleMarkerData(hostAsset)}. " +
                $"Logistics endpoint registration requires StorageCrate + PowerNode (BaseLogisticsNetwork.cs:165-167).");
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Grows StorageCrate.containedItems to the authored capacity without disturbing
        /// any element a designer already assigned. Never shrinks: a shorter authored
        /// array is a defect, a longer one is designer intent that this tool must not eat.
        /// </summary>
        private static bool TryEnsureAuthoredSlotCount(StorageCrate crate, out int slotCount, out bool changed)
        {
            slotCount = 0;
            changed = false;

            if (crate == null)
            {
                Debug.LogError($"{LogPrefix} DECLINED: no StorageCrate instance to configure. Nothing written.");
                return false;
            }

            SerializedObject crateObject = new SerializedObject(crate);
            SerializedProperty containedItems = crateObject.FindProperty(ContainedItemsPropertyName);
            if (containedItems == null || !containedItems.isArray)
            {
                Debug.LogError(
                    $"{LogPrefix} DECLINED: StorageCrate has no serialized array field '{ContainedItemsPropertyName}' " +
                    "(renamed or removed in StorageCrate.cs). Nothing written.");
                return false;
            }

            int authoredCount = containedItems.arraySize;
            if (authoredCount >= AuthoredCrateSlotCount)
            {
                slotCount = authoredCount;
                if (authoredCount > AuthoredCrateSlotCount)
                {
                    Debug.LogWarning(
                        $"{LogPrefix} '{HostPrefabPath}' authors {authoredCount} crate slots but module persistence " +
                        $"caps at ModuleDTO.MaxStorageCrateSlots={AuthoredCrateSlotCount} " +
                        "(StorageCrate.PopulateSaveData stops there), so surplus slots do not survive a save. " +
                        "Left as authored — shrinking would destroy authored contents.");
                }

                return true;
            }

            for (int index = authoredCount; index < AuthoredCrateSlotCount; index++)
            {
                containedItems.arraySize = index + 1;
                containedItems.GetArrayElementAtIndex(index).objectReferenceValue = null;
            }

            crateObject.ApplyModifiedPropertiesWithoutUndo();
            slotCount = AuthoredCrateSlotCount;
            changed = true;
            return true;
        }

        private static int ReadAuthoredSlotCount(StorageCrate crate)
        {
            if (crate == null)
                return 0;

            SerializedObject crateObject = new SerializedObject(crate);
            SerializedProperty containedItems = crateObject.FindProperty(ContainedItemsPropertyName);
            return containedItems != null && containedItems.isArray ? containedItems.arraySize : 0;
        }

        /// <summary>
        /// PowerNode.ReadBuildableData (PowerNode.cs:331-345) reads its rating from
        /// ModuleMarker.Data, so the marker binding state is worth naming in the log.
        /// </summary>
        private static string DescribeModuleMarkerData(GameObject root)
        {
            if (root == null || !root.TryGetComponent(out ModuleMarker marker))
                return "no ModuleMarker";

            return marker.Data != null ? marker.Data.name : "unbound (PowerNode falls back to inspector rating)";
        }
    }
}
