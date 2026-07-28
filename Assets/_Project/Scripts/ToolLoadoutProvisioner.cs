// ============================================================================
// HECTON-8 - ToolLoadoutProvisioner.cs
// Development provisioning helper for inventory + quick-slot loadouts.
// Keeps player runtime integration deterministic without hand-wiring every test.
// ============================================================================

using Hecton8.Gameplay;
using Hecton8.Bootstrap;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Tools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Tool Loadout Provisioner")]
    public sealed class ToolLoadoutProvisioner : MonoBehaviour
    {
        // Precomputed stable telemetry hashes. GlobalTelemetryBus.PublishPerformanceWarning
        // (Core/GlobalTelemetryBus.cs:365) carries no [Conditional] attribute, so unlike H8Debug these
        // survive into a shipped build - which is the point: this component's whole failure mode was
        // being silent.
        private const uint StartupLoadoutEmptyWarningHash = 0x544C4530u;        // TLE0
        private const uint StartupLoadoutSourceInertWarningHash = 0x544C5349u;  // TLSI
        private const uint DevelopmentGrantRefusedWarningHash = 0x544C4447u;    // TLDG
        private const uint QuickSlotMirrorDivergedWarningHash = 0x544C4D44u;    // TLMD
        private const uint ToolLoadoutProvisionerContextHash = 0x544C5650u;     // TLVP

        internal static ToolLoadoutProvisioner ActiveRuntimeInstance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveRuntimeForSubsystemRegistration()
        {
            ActiveRuntimeInstance = null;
        }

        [Header("References")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerToolManager toolManager;

        [Header("Startup Provisioning")]
        [SerializeField] private bool provisionInventoryOnStart = false;
        [SerializeField] private bool assignCoreLoadoutOnStart = false;
        [SerializeField] private bool holsterBeforeAssigning = true;
        [SerializeField] private bool provisionConstructionMaterialsOnStart = false;
        [SerializeField] private ToolLoadoutPreset startupPreset;

        [Header("Core Quick Slots")]
        [SerializeField] private GameObject[] coreQuickSlotPrefabs = new GameObject[4];

        [Header("Full Tool Kit")]
        [SerializeField] private ItemData[] allToolItems = new ItemData[13];

        [Header("Construction Materials")]
        [SerializeField] private ItemData[] starterConstructionItems = new ItemData[1];
        [SerializeField] private int[] starterConstructionAmounts = new int[1];

        private bool _appliedAtRuntime;

        private void Awake()
        {
            if (Application.isPlaying)
                ActiveRuntimeInstance = this;

            AutoResolveSceneReferences();
#if UNITY_EDITOR
            AutoResolveDefaultAssets();
#endif
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private void Start()
        {
            if (_appliedAtRuntime)
                return;

            _appliedAtRuntime = true;

            if (provisionInventoryOnStart)
                ProvisionFullToolKit();

            if (provisionConstructionMaterialsOnStart)
                ProvisionConstructionMaterials();

            if (assignCoreLoadoutOnStart)
            {
                // Startup provisioning FILLS GAPS. It is not the owner of quick-slot truth -
                // PlayerToolManager is, and ContentSanityValidator.cs:2490 validates its serialized
                // toolPrefabs as the production starter loadout - so an automatic pass must never
                // overwrite or clear a slot the owner already holds.
                int contributedSlots = startupPreset != null
                    ? ApplyStartupLoadout(startupPreset.slotPrefabs, overwriteAssignedSlots: false)
                    : ApplyStartupLoadout(coreQuickSlotPrefabs, overwriteAssignedSlots: false);

                if (contributedSlots <= 0)
                {
                    Hecton8.Core.GlobalTelemetryBus.PublishPerformanceWarning(
                        StartupLoadoutSourceInertWarningHash,
                        ToolLoadoutProvisionerContextHash,
                        0f);
                }
            }

            ReportStartupLoadoutOutcome();
            ReportQuickSlotMirrorDivergence();
        }

        /// <summary>
        /// Reports the only fact that matters to the first-20-minutes tool contract: how many quick
        /// slots actually hold a tool once startup provisioning has finished. Zero means the player
        /// begins the route with no tool verb at all, whichever owner was supposed to supply it.
        /// </summary>
        private void ReportStartupLoadoutOutcome()
        {
            AutoResolveSceneReferences();

            // scalarValue carries the slot count the owner exposes, or 0 when the owner could not be
            // resolved at all - both are the same player-visible outcome and both need a marker.
            int assignedSlots = 0;
            int slotCount = 0;
            if (toolManager != null)
            {
                slotCount = toolManager.SlotCount;
                for (int i = 0; i < slotCount; i++)
                {
                    if (toolManager.GetAssignedToolPrefab(i) != null)
                        assignedSlots++;
                }
            }

            if (assignedSlots > 0)
                return;

            Hecton8.Core.GlobalTelemetryBus.PublishPerformanceWarning(
                StartupLoadoutEmptyWarningHash,
                ToolLoadoutProvisionerContextHash,
                slotCount);
        }

        /// <summary>
        /// coreQuickSlotPrefabs is a duplicate of the tool owner's own assignments - on the canonical
        /// player it repeats PlayerToolManager.toolPrefabs GUID for GUID and in the same order - and the
        /// editor auto-resolver that keeps it populated only ever fills a NULL entry
        /// (TryAssignToolPrefab, :426), so it cannot correct an entry that already points somewhere
        /// else. The moment a designer re-authors the owner's list the mirror rots, and nothing
        /// anywhere noticed. Divergence is never intent: the sanctioned way to state a DIFFERENT
        /// loadout is a ToolLoadoutPreset asset, which startupPreset already takes ahead of this array
        /// (:100-102) and which the owner applies as authored data. The four shipped presets prove the
        /// distinction - Preset_Loadout_Construction holds the same four tools in a deliberately
        /// different slot order, which is an override; this array holding a different tool is rot.
        /// Published through GlobalTelemetryBus because it carries no [Conditional]
        /// (Core/GlobalTelemetryBus.cs:365) and therefore survives into a shipped build, which is the
        /// entire point - silence was the defect.
        /// </summary>
        private void ReportQuickSlotMirrorDivergence()
        {
            AutoResolveSceneReferences();
            if (toolManager == null || coreQuickSlotPrefabs == null)
                return;

            int comparedSlots = Mathf.Min(coreQuickSlotPrefabs.Length, toolManager.SlotCount);
            int divergedSlots = 0;
            for (int i = 0; i < comparedSlots; i++)
            {
                GameObject mirrored = coreQuickSlotPrefabs[i];
                GameObject assigned = toolManager.GetAssignedToolPrefab(i);

                // Only a slot where BOTH sides name a tool and the two disagree is drift. A mirror
                // entry left empty is an unauthored gap, and an owner slot left empty is a gap the
                // mirror is allowed to seed - neither is a contradiction. ReferenceEquals is the same
                // identity test the owner uses to decide a slot write is redundant (:873).
                if (mirrored != null && assigned != null && !ReferenceEquals(mirrored, assigned))
                    divergedSlots++;
            }

            if (divergedSlots <= 0)
                return;

            Hecton8.Core.GlobalTelemetryBus.PublishPerformanceWarning(
                QuickSlotMirrorDivergedWarningHash,
                ToolLoadoutProvisionerContextHash,
                divergedSlots);
        }

        [ContextMenu("Provision Full Tool Kit")]
        public void ProvisionFullToolKit()
        {
            if (!CanGrantDevelopmentInventoryInCurrentBuild(provisionInventoryOnStart))
                return;

            AutoResolveSceneReferences();
            if (playerInventory == null)
                return;

            for (int i = 0; i < allToolItems.Length; i++)
            {
                ItemData item = allToolItems[i];
                if (item == null)
                    continue;

                int itemHashId = ItemData.ResolvePersistentHashId(item);
                if (itemHashId == 0 || playerInventory.ContainsItem(itemHashId))
                    continue;

                playerInventory.TryAddItem(itemHashId, 1);
            }
        }

        [ContextMenu("Provision Construction Materials")]
        public void ProvisionConstructionMaterials()
        {
            if (!CanGrantDevelopmentInventoryInCurrentBuild(provisionConstructionMaterialsOnStart))
                return;

            AutoResolveSceneReferences();
            if (playerInventory == null)
                return;

            int count = Mathf.Min(starterConstructionItems.Length, starterConstructionAmounts.Length);
            for (int i = 0; i < count; i++)
            {
                ItemData item = starterConstructionItems[i];
                int amount = starterConstructionAmounts[i];
                if (item == null || amount <= 0)
                    continue;

                int itemHashId = ItemData.ResolvePersistentHashId(item);
                if (itemHashId != 0)
                    playerInventory.TryAddItem(itemHashId, amount);
            }
        }

        [ContextMenu("Assign Core Loadout")]
        public void AssignCoreLoadout()
        {
            // Explicit designer action, so replacing an assigned slot is what was asked for - but an
            // empty mirror entry is NOT a request to empty a slot (see ApplyStartupLoadout), and this
            // component cannot delete a tool the player prefab shipped with.
            ApplyStartupLoadout(coreQuickSlotPrefabs, overwriteAssignedSlots: true);
        }

        /// <summary>
        /// Writes a slot source into the tool owner and returns how many quick slots end up holding a
        /// tool. With <paramref name="overwriteAssignedSlots"/> false this only fills gaps: an entry the
        /// source leaves empty never clears an assigned slot, and a slot the owner already holds is left
        /// alone. That is the automatic startup contract - a provisioning pass that can leave the player
        /// with fewer tools than the prefab shipped with is worse than one that does nothing.
        /// </summary>
        private int ApplyStartupLoadout(GameObject[] slotSource, bool overwriteAssignedSlots)
        {
            AutoResolveSceneReferences();
            if (toolManager == null || slotSource == null)
                return 0;

            int count = Mathf.Min(slotSource.Length, toolManager.SlotCount);
            if (count <= 0)
                return 0;

            if (holsterBeforeAssigning && overwriteAssignedSlots)
                toolManager.Holster();

            int filledSlots = 0;
            for (int i = 0; i < count; i++)
            {
                GameObject candidate = slotSource[i];
                GameObject assigned = toolManager.GetAssignedToolPrefab(i);

                // An EMPTY source entry says nothing about that slot, in either mode. The only
                // authoring path into coreQuickSlotPrefabs is TryAssignToolPrefab (:426), which fills a
                // null and can never clear one, so an empty entry is an unauthored gap and not an
                // instruction to strip the bar. The authored override channel already obeys exactly
                // this rule - PlayerToolManager.ApplyLoadoutPreset:1128-1133 skips null preset slots
                // for the same reason - and until now this DEV mirror held MORE authority over the
                // validated starter loadout than a designer's preset asset did: one right-click on
                // "Assign Core Loadout" with a null mirror entry deleted a tool the prefab shipped
                // with. That is the same "fewer tools than the prefab shipped with" loss the
                // non-overwrite mode was already hardened against.
                if (candidate == null || (!overwriteAssignedSlots && assigned != null))
                {
                    if (assigned != null)
                        filledSlots++;

                    continue;
                }

                // Returns true both when it wrote the slot and when the slot already held this exact
                // prefab (PlayerToolManager.cs:873) - both mean the slot holds a tool, which is what
                // filledSlots counts.
                if (toolManager.SetAssignedToolPrefab(i, candidate, holsterIfCurrentInvalid: false))
                    filledSlots++;
            }

            return filledSlots;
        }

        [ContextMenu("Provision And Assign Core Loadout")]
        public void ProvisionAndAssignCoreLoadout()
        {
            ProvisionFullToolKit();
            ProvisionConstructionMaterials();
            AssignCoreLoadout();
        }

        [ContextMenu("Apply Startup Preset")]
        public void ApplyStartupPreset()
        {
            AutoResolveSceneReferences();
            if (toolManager == null || startupPreset == null)
                return;

            toolManager.ApplyLoadoutPreset(startupPreset, holsterBeforeAssigning);
        }

        private void AutoResolveSceneReferences()
        {
            if ((!playerInventory || !toolManager) &&
                GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                if (playerInventory == null)
                    playerTransform.TryGetComponent(out playerInventory);

                if (toolManager == null)
                {
                    toolManager = Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.ToolManager != null
                        ? Hecton8.Core.GlobalRegistry.Player.ToolManager
                        : null;
                    if (toolManager == null)
                        playerTransform.TryGetComponent(out toolManager);
                }
            }
        }

        /// <summary>
        /// Gates the two BULK INVENTORY GRANTS only - the 13-tool kit and the starter construction
        /// stock. Those are development conveniences, so shipping them would hand a release player free
        /// loot; that, and not a compile dependency, is the whole justification for a build gate here.
        /// The quick-slot assignment paths no longer carry it: they hold no editor-only dependency
        /// (every call they make - Holster, SetAssignedToolPrefab, ApplyLoadoutPreset,
        /// GameBootstrapper.TryGetCurrentPlayerTransform - compiles on every platform, and the only
        /// AssetDatabase code in this file sits under its own #if UNITY_EDITOR), and they are the sole
        /// route from the four authored ToolLoadoutPreset assets into a running game.
        /// The refusal is now audible when something actually asked for the grant, instead of returning
        /// false into silence.
        /// </summary>
        private static bool CanGrantDevelopmentInventoryInCurrentBuild(bool grantWasRequested)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            if (grantWasRequested)
            {
                Hecton8.Core.GlobalTelemetryBus.PublishPerformanceWarning(
                    DevelopmentGrantRefusedWarningHash,
                    ToolLoadoutProvisionerContextHash,
                    1f);
            }

            return false;
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            AutoResolveSceneReferences();
            AutoResolveDefaultAssets();
        }

        private void AutoResolveDefaultAssets()
        {
            TryAssignToolPrefab(ref coreQuickSlotPrefabs[0], "Assets/_Project/Prefabs/Tools/Held/Tool_Scanner_Held.prefab");
            TryAssignToolPrefab(ref coreQuickSlotPrefabs[1], "Assets/_Project/Prefabs/Tools/Held/Tool_Repair_Held.prefab");
            TryAssignToolPrefab(ref coreQuickSlotPrefabs[2], "Assets/_Project/Prefabs/Tools/Held/Tool_Builder_Held.prefab");
            TryAssignToolPrefab(ref coreQuickSlotPrefabs[3], "Assets/_Project/Prefabs/Tools/Held/Tool_LaserCutter_Held.prefab");

            string[] itemPaths =
            {
                "Assets/_Project/Data/Items/Tools/Item_Tool_Scanner.asset",
                "Assets/_Project/Data/Items/Tools/Item_Tool_Repair.asset",
                "Assets/_Project/Data/Items/Tools/Item_Tool_Builder.asset",
                "Assets/_Project/Data/Items/Tools/Item_Tool_LaserCutter.asset",
                "Assets/_Project/Data/Items/Tools/Item_Tool_Flashlight.asset",
                "Assets/_Project/Data/Items/Tools/Item_Tool_Propulsion.asset",
                "Assets/_Project/Data/Items/Tools/Item_Tool_SalvageSampler.asset",
                "Assets/_Project/Data/Items/Tools/Item_Tool_BeaconDeployer.asset",
                "Assets/_Project/Data/Items/Tools/Item_Tool_EnvAnalyzer.asset",
                "Assets/_Project/Data/Items/Tools/Item_Tool_Knife.asset",
                "Assets/_Project/Data/Items/Tools/Item_Tool_StunPistol.asset",
                "Assets/_Project/Data/Items/Tools/Item_Tool_HarpoonLauncher.asset",
                "Assets/_Project/Data/Items/Tools/Item_Tool_SeafloorDrill.asset",
            };

            if (allToolItems == null || allToolItems.Length != itemPaths.Length)
                System.Array.Resize(ref allToolItems, itemPaths.Length);

            for (int i = 0; i < allToolItems.Length && i < itemPaths.Length; i++)
            {
                if (allToolItems[i] != null)
                    continue;

                allToolItems[i] = AssetDatabase.LoadAssetAtPath<ItemData>(itemPaths[i]);
            }

            if (starterConstructionItems.Length > 0 && starterConstructionItems[0] == null)
                starterConstructionItems[0] = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset");

            if (starterConstructionAmounts.Length > 0 && starterConstructionAmounts[0] <= 0)
                starterConstructionAmounts[0] = 12;
        }

        private static void TryAssignToolPrefab(ref GameObject target, string path)
        {
            if (target != null)
                return;

            target = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
#endif
    }
}
