using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.Meta
{
    /// <summary>
    /// Applies permanent global upgrade buffs from the global profile into the active run.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6340)]
    [AddComponentMenu("Hecton8/Meta/Meta Buff Injector")]
    public sealed class MetaBuffInjector : MonoBehaviour, IGameBootstrapperEventListener
    {
        private const string StarterStructuralMetalId = "Data_TitaniumScrap";
        private const string StarterElectronicsMetalId = "Data_Copper";
        private const string StarterChemicalId = "Data_ElectrolyteSalts";

        private HectonSurvivalSystem _survivalSystem;
        private HectonPlayerMovement _playerMovement;
        private PlayerInventory _inventory;
        private bool _runtimeBuffsApplied;
        private bool _starterResourcesApplied;

        private void OnEnable()
        {
            GameBootstrapper.Register(this);
        }

        private void OnDisable()
        {
            GameBootstrapper.Unregister(this);
            _survivalSystem = null;
            _playerMovement = null;
            _inventory = null;
            _runtimeBuffsApplied = false;
            _starterResourcesApplied = false;
        }

        public void OnGameBootstrapperEvent(in GameBootstrapperEventPayload payload)
        {
            if ((GameBootstrapperEventType)payload.EventType == GameBootstrapperEventType.GameReady)
                HandleGameReady();
        }

        private void HandleGameReady()
        {
            TryApplyProfileBuffs();
        }

        private void TryApplyProfileBuffs()
        {
            if (!ResolveOwners())
                return;

            GlobalProfileData profile;
            if (!TryResolveProfile(out profile))
                return;

            float oxygenMultiplier = 1f + 0.10f * ResolveUpgradeLevel(profile, MetaUpgradeRegistry.BaseOxygenCapacityId);
            float swimMultiplier = 1f + 0.05f * ResolveUpgradeLevel(profile, MetaUpgradeRegistry.SwimSpeedBoostId);

            if (!_runtimeBuffsApplied)
            {
                _survivalSystem.SetRuntimeOxygenCapacityMultiplier(oxygenMultiplier);
                _playerMovement.SetRuntimeSwimSpeedMultiplier(swimMultiplier);
                _runtimeBuffsApplied = true;
            }

            if (_starterResourcesApplied)
                return;

            GameStartContext context = GameStartContextHolder.Current;
            if (context.StartMode != GameStartMode.NewGame)
                return;

            int starterCacheLevel = ResolveUpgradeLevel(profile, MetaUpgradeRegistry.StartingResourceCacheId);
            if (starterCacheLevel <= 0)
                return;

            InjectStarterCache(starterCacheLevel);
            _starterResourcesApplied = true;
        }

        private bool ResolveOwners()
        {
            GameObject playerObject = GameBootstrapper.CurrentPlayerObject;
            if (playerObject == null)
                return false;

            if (_survivalSystem == null)
                playerObject.TryGetComponent(out _survivalSystem);

            if (_playerMovement == null)
                playerObject.TryGetComponent(out _playerMovement);

            if (_inventory == null)
                playerObject.TryGetComponent(out _inventory);

            return _survivalSystem != null && _playerMovement != null && _inventory != null;
        }

        private static bool TryResolveProfile(out GlobalProfileData profile)
        {
            IProfileService profileService = GlobalRegistry.Profile;
            if (profileService != null)
            {
                profile = profileService.GetSnapshot();
                return true;
            }

            return GlobalProfileManager.TryLoadSnapshot(out profile);
        }

        private static int ResolveUpgradeLevel(GlobalProfileData profile, string upgradeId)
        {
            if (profile == null || string.IsNullOrWhiteSpace(upgradeId) ||
                profile.purchasedUpgradeLevels == null)
            {
                return 0;
            }

            uint upgradeHash = QuestFlagHashKernel.ComputeStableHash(upgradeId);
            if (upgradeHash == 0u)
                return 0;

            int count = Mathf.Clamp(profile.purchasedUpgradeCount, 0, profile.purchasedUpgradeLevels.Length);
            for (int i = 0; i < count; i++)
            {
                MetaUpgradeLevelRecord record = profile.purchasedUpgradeLevels[i];
                uint recordHash = record.upgradeHash != 0u ? record.upgradeHash : QuestFlagHashKernel.ComputeStableHash(record.upgradeId);
                if (recordHash == upgradeHash)
                    return Mathf.Max(0, record.level);
            }

            return 0;
        }

        private void InjectStarterCache(int level)
        {
            ItemCatalog catalog = _inventory != null ? _inventory.ItemCatalog : null;
            if (catalog == null)
                return;

            GrantStarterResource(catalog, StarterStructuralMetalId, Mathf.Clamp(2 * level, 2, 8));
            GrantStarterResource(catalog, StarterElectronicsMetalId, Mathf.Clamp(level, 1, 4));
            GrantStarterResource(catalog, StarterChemicalId, Mathf.Clamp(level, 1, 3));
        }

        private void GrantStarterResource(ItemCatalog catalog, string itemId, int quantity)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
                return;

            ItemData item = catalog.FindById(itemId);
            if (item == null)
                return;

            int itemHashId = ItemData.ResolvePersistentHashId(item);
            if (itemHashId != 0)
                _inventory.TryAddItem(itemHashId, quantity);
        }
    }
}
