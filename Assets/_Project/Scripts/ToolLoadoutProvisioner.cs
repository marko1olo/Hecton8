// ============================================================================
// HECTON-8 - ToolLoadoutProvisioner.cs
// Safe provisioning helper for inventory + quick-slot loadouts.
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
        internal static ToolLoadoutProvisioner ActiveRuntimeInstance { get; private set; }

        [Header("References")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerToolManager toolManager;

        [Header("Startup Provisioning")]
        [SerializeField] private bool provisionInventoryOnStart = false;
        [SerializeField] private bool assignCoreLoadoutOnStart = false;
        [SerializeField] private bool holsterBeforeAssigning = true;
        [SerializeField] private bool provisionConstructionMaterialsOnStart = true;
        [SerializeField] private ToolLoadoutPreset startupPreset;

        [Header("Core Quick Slots")]
        [SerializeField] private GameObject[] coreQuickSlotPrefabs = new GameObject[4];

        [Header("Full Tool Kit")]
        [SerializeField] private ItemData[] allToolItems = new ItemData[12];

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

            if (provisionInventoryOnStart)
                ProvisionFullToolKit();

            if (provisionConstructionMaterialsOnStart)
                ProvisionConstructionMaterials();

            if (assignCoreLoadoutOnStart)
            {
                if (startupPreset != null)
                    ApplyStartupPreset();
                else
                    AssignCoreLoadout();
            }

            _appliedAtRuntime = true;
        }

        [ContextMenu("Provision Full Tool Kit")]
        public void ProvisionFullToolKit()
        {
            AutoResolveSceneReferences();
            if (playerInventory == null)
                return;

            for (int i = 0; i < allToolItems.Length; i++)
            {
                ItemData item = allToolItems[i];
                if (item == null)
                    continue;

                if (playerInventory.ContainsItem(item))
                    continue;

                playerInventory.TryAddItem(item, 1);
            }
        }

        [ContextMenu("Provision Construction Materials")]
        public void ProvisionConstructionMaterials()
        {
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

                playerInventory.TryAddItem(item, amount);
            }
        }

        [ContextMenu("Assign Core Loadout")]
        public void AssignCoreLoadout()
        {
            AutoResolveSceneReferences();
            if (toolManager == null)
                return;

            if (holsterBeforeAssigning)
                toolManager.Holster();

            for (int i = 0; i < coreQuickSlotPrefabs.Length; i++)
            {
                toolManager.SetAssignedToolPrefab(i, coreQuickSlotPrefabs[i], holsterIfCurrentInvalid: false);
            }
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
                SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                if (playerInventory == null)
                    playerInventory = playerTransform.GetComponent<PlayerInventory>();

                if (toolManager == null)
                    toolManager = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.ToolManager != null) ? Hecton8.Core.GlobalRegistry.Player.ToolManager : playerTransform.GetComponent<PlayerToolManager>());
            }
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
            };

            for (int i = 0; i < allToolItems.Length && i < itemPaths.Length; i++)
            {
                if (allToolItems[i] != null)
                    continue;

                allToolItems[i] = AssetDatabase.LoadAssetAtPath<ItemData>(itemPaths[i]);
            }

            if (starterConstructionItems.Length > 0 && starterConstructionItems[0] == null)
                starterConstructionItems[0] = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_Project/Data/Items/Data_Copper.asset");

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
