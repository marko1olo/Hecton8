// ============================================================================
// HECTON-8 — SuitUpgradeManager.cs
// Menedzher apgreydov skafandra.
//
// LOR (lor1): Progressiya glubiny cherez apgreydy korpusa.
//   Tier 0 → Tier 1: pervyy kraft v igre (rasshirennyy O2 rezervuar).
//   Tier 4: finalnyy — do -5000m, O2 45 min.
//
// ARHITEKTURA:
//   • Primenyaet apgreydy cherez HectonSurvivalSystem.OverrideStats().
//   • Runtime-kopiya SurvivalStats — ne mutiruet originalnyy SO.
//   • ISaveable: sohranyaet spisok ustanovlennyh upgradeId.
//   • Slushaet NarrativeEvents.OnDiscoveryMade dlya razblokirovki chertezhey.
//
// ZERO GC:
//   • HashSet<string> dlya O(1) proverki ustanovlennyh apgreydov.
//   • Nikakih new/LINQ v hot path.
// ============================================================================

using System.Collections.Generic;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-110)]
    public sealed class SuitUpgradeManager : MonoBehaviour, ISaveable, INarrativeEventListener
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── References ──────────────────────────────")]
        [Tooltip("Bazovye parametry skafandra (Tier 0).")]
        [SerializeField] private SurvivalStats baseStats;

        [Tooltip("Sistema vyzhivaniya igroka.")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;

        [Header("── Upgrades ────────────────────────────────")]
        [Tooltip("Vse apgreydy v igre. Poryadok ne vazhen — sortiruyutsya po tier.")]
        [SerializeField] private SuitUpgradeData[] allUpgrades = new SuitUpgradeData[0];

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static SuitUpgradeManager Instance => GlobalRegistry.SuitUpgrades;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        // COLD ALLOC: 32 entries — max installed upgrades
        private readonly HashSet<string> _installedUpgrades  = new HashSet<string>(32);
        private readonly HashSet<string> _unlockedBlueprints = new HashSet<string>(32);
        private readonly HashSet<string> _brokenUpgrades = new HashSet<string>(16);

        // Runtime stats — clone of baseStats with deltas applied
        private SurvivalStats _runtimeStats;
        private uint _breakOrdinal;
        private bool _serviceRegistered;

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public int SavePriority => 9;
        public int LoadPriority => 9;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public int InstalledCount => _installedUpgrades.Count;

        /// <summary>Tekuschiy maksimalnyy tir ustanovlennyh apgreydov korpusa.</summary>
        public int CurrentHullTier
        {
            get
            {
                int max = 0;
                for (int i = 0; i < allUpgrades.Length; i++)
                {
                    SuitUpgradeData u = allUpgrades[i];
                    if (u != null && u.category == SuitUpgradeCategory.Hull &&
                        _installedUpgrades.Contains(u.upgradeId) && u.tier > max)
                        max = u.tier;
                }
                return max;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            SuitUpgradeManager registered = GlobalRegistry.SuitUpgrades;
            if (Application.isPlaying && registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return;
            }

            if (baseStats == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[SuitUpgrade] baseStats not assigned. Disabling.", this);
#endif
                enabled = false;
                return;
            }

#if UNITY_EDITOR
            if (allUpgrades == null || allUpgrades.Length == 0)
                SyncUpgradeCatalogFromFolder();
#endif

            // COLD ALLOC: runtime clone of baseStats
            _runtimeStats = Instantiate(baseStats);
        }

        private void OnEnable()
        {
            TryRegisterService();

            if (Hecton8.Core.GlobalRegistry.SaveRuntime != null)
                Hecton8.Core.GlobalRegistry.SaveRuntime.Register(this);

            NarrativeEvents.Register(this);
        }

        private void OnDisable()
        {
            if (Hecton8.Core.GlobalRegistry.SaveRuntime != null)
                Hecton8.Core.GlobalRegistry.SaveRuntime.Unregister(this);

            NarrativeEvents.Unregister(this);
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            TryUnregisterService();
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            SuitUpgradeManager registered = Hecton8.Core.GlobalRegistry.SuitUpgrades;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return;
            }

            Hecton8.Core.GlobalRegistry.RegisterSuitUpgradeRuntime(this);
            _serviceRegistered = ReferenceEquals(Hecton8.Core.GlobalRegistry.SuitUpgrades, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            Hecton8.Core.GlobalRegistry.UnregisterSuitUpgradeRuntime(this);
            _serviceRegistered = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            if (allUpgrades == null || allUpgrades.Length == 0)
                SyncUpgradeCatalogFromFolder();
        }

        private void SyncUpgradeCatalogFromFolder()
        {
            string[] guids = AssetDatabase.FindAssets("t:SuitUpgradeData", new[] { "Assets/_Project/Data/Lore/SuitUpgrades" });
            if (guids == null || guids.Length == 0)
                return;

            List<SuitUpgradeData> upgrades = new List<SuitUpgradeData>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                SuitUpgradeData upgrade = AssetDatabase.LoadAssetAtPath<SuitUpgradeData>(path);
                if (upgrade != null)
                    upgrades.Add(upgrade);
            }

            if (upgrades.Count == 0)
                return;

            upgrades.Sort(CompareUpgradeCatalogEntries);
            allUpgrades = upgrades.ToArray();
            EditorUtility.SetDirty(this);
        }

        private static int CompareUpgradeCatalogEntries(SuitUpgradeData left, SuitUpgradeData right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;

            int tierCompare = left.tier.CompareTo(right.tier);
            if (tierCompare != 0)
                return tierCompare;

            int categoryCompare = ((int)left.category).CompareTo((int)right.category);
            if (categoryCompare != 0)
                return categoryCompare;

            return string.CompareOrdinal(left.upgradeId, right.upgradeId);
        }
#endif

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Proverit, mozhno li ustanovit apgreyd (chertezh razblokirovan).
        /// </summary>
        public bool CanInstall(SuitUpgradeData upgrade)
        {
            if (upgrade == null) return false;
            if (string.IsNullOrEmpty(upgrade.upgradeId)) return false;
            if (_installedUpgrades.Contains(upgrade.upgradeId)) return false;
            if (!string.IsNullOrEmpty(upgrade.requiredBlueprintId) &&
                !_unlockedBlueprints.Contains(upgrade.requiredBlueprintId))
                return false;
            return true;
        }

        /// <summary>
        /// Ustanovit apgreyd. Primenyaet delty k runtime stats.
        /// </summary>
        public bool InstallUpgrade(SuitUpgradeData upgrade)
        {
            if (!CanInstall(upgrade)) return false;

            _installedUpgrades.Add(upgrade.upgradeId);
            RebuildRuntimeStats();

            string displayName = upgrade.DisplayNameOrFallback;
            LocalizationManager localization = Hecton8.Core.GlobalRegistry.Localization;
            NotificationEvents.PushInfo(localization != null
                ? localization.GetFormatted(LocalizationKeys.SUIT_UPGRADE_INSTALLED, displayName)
                : "UPGRADE INSTALLED: " + displayName);

            LogUpgradeInstalled(upgrade.upgradeId, upgrade.tier);
            return true;
        }

        public bool IsInstalled(string upgradeId) => _installedUpgrades.Contains(upgradeId);
        public bool IsBroken(string upgradeId) => !string.IsNullOrEmpty(upgradeId) && _brokenUpgrades.Contains(upgradeId);

        public bool IsBlueprintUnlocked(string blueprintId) => _unlockedBlueprints.Contains(blueprintId);

        /// <summary>
        /// Randomly breaks one installed module and removes its runtime bonuses until repaired.
        /// </summary>
        public bool TryBreakRandomInstalledUpgrade(float chance01, out SuitUpgradeData brokenUpgrade)
        {
            brokenUpgrade = null;

            if (_installedUpgrades.Count <= 0 || chance01 <= 0f)
                return false;

            float chance = math.saturate(chance01);
            uint breakRoll = ComputeBreakRoll();
            if (HashToUnit01(breakRoll) > chance)
                return false;

            int eligibleCount = 0;
            for (int i = 0; i < allUpgrades.Length; i++)
            {
                SuitUpgradeData upgrade = allUpgrades[i];
                if (upgrade == null || string.IsNullOrEmpty(upgrade.upgradeId))
                    continue;

                if (!_installedUpgrades.Contains(upgrade.upgradeId) || _brokenUpgrades.Contains(upgrade.upgradeId))
                    continue;

                eligibleCount++;
            }

            if (eligibleCount <= 0)
                return false;

            int targetIndex = (int)(MixHash(breakRoll ^ 0xBADC0DEu) % (uint)eligibleCount);
            for (int i = 0; i < allUpgrades.Length; i++)
            {
                SuitUpgradeData upgrade = allUpgrades[i];
                if (upgrade == null || string.IsNullOrEmpty(upgrade.upgradeId))
                    continue;

                if (!_installedUpgrades.Contains(upgrade.upgradeId) || _brokenUpgrades.Contains(upgrade.upgradeId))
                    continue;

                if (targetIndex > 0)
                {
                    targetIndex--;
                    continue;
                }

                _brokenUpgrades.Add(upgrade.upgradeId);
                brokenUpgrade = upgrade;
                RebuildRuntimeStats();
                NotificationEvents.PushWarning("SUIT MODULE BROKEN: " + upgrade.DisplayNameOrFallback);
                LogUpgradeBroken(upgrade.upgradeId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Repairs a previously broken installed module and restores its runtime bonuses.
        /// </summary>
        public bool RepairUpgrade(string upgradeId)
        {
            if (string.IsNullOrEmpty(upgradeId) || !_installedUpgrades.Contains(upgradeId))
                return false;

            if (!_brokenUpgrades.Remove(upgradeId))
                return false;

            RebuildRuntimeStats();

            SuitUpgradeData upgrade = FindUpgradeById(upgradeId);
            NotificationEvents.PushInfo("SUIT MODULE REPAIRED: " + (upgrade != null ? upgrade.DisplayNameOrFallback : upgradeId));
            LogUpgradeRepaired(upgradeId);
            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        public void OnNarrativeEvent(in NarrativeEventPayload payload)
        {
            if ((NarrativeEventType)payload.EventType != NarrativeEventType.DiscoveryMade ||
                payload.DiscoveryHash == 0u ||
                allUpgrades == null)
            {
                return;
            }

            // Proveryaem — yavlyaetsya li eto chertezhom apgreyda
            for (int i = 0; i < allUpgrades.Length; i++)
            {
                SuitUpgradeData u = allUpgrades[i];
                if (u != null &&
                    !string.IsNullOrEmpty(u.requiredBlueprintId) &&
                    NarrativeEvents.ComputeDiscoveryHash(u.requiredBlueprintId) == payload.DiscoveryHash)
                {
                    if (_unlockedBlueprints.Add(u.requiredBlueprintId))
                    {
                        string displayName = u.DisplayNameOrFallback;
                        LocalizationManager localization = Hecton8.Core.GlobalRegistry.Localization;
                        NotificationEvents.PushInfo(localization != null
                            ? localization.GetFormatted(LocalizationKeys.SUIT_BLUEPRINT_UNLOCKED, displayName)
                            : "BLUEPRINT UNLOCKED: " + displayName);

                        LogBlueprintUnlocked(u.requiredBlueprintId, displayName);
                    }
                    break;
                }
            }
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogUpgradeInstalled(string upgradeId, int tier)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[SuitUpgrade] Installed: " + upgradeId + " (tier " + tier + ")");
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogBlueprintUnlocked(string discoveryId, string displayName)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[SuitUpgrade] Blueprint unlocked: " + discoveryId + " -> " + displayName);
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogUpgradeBroken(string upgradeId)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[SuitUpgrade] Broken: " + upgradeId);
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogUpgradeRepaired(string upgradeId)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[SuitUpgrade] Repaired: " + upgradeId);
#endif
        }

        private uint ComputeBreakRoll()
        {
            uint hash = 0x53554954u ^ (_breakOrdinal++ * 0x9E3779B9u);
            if (allUpgrades == null)
                return MixHash(hash);

            for (int i = 0; i < allUpgrades.Length; i++)
            {
                SuitUpgradeData upgrade = allUpgrades[i];
                if (upgrade == null ||
                    string.IsNullOrEmpty(upgrade.upgradeId) ||
                    !_installedUpgrades.Contains(upgrade.upgradeId) ||
                    _brokenUpgrades.Contains(upgrade.upgradeId))
                {
                    continue;
                }

                hash ^= unchecked((uint)LocHash.Compute(upgrade.upgradeId));
                hash = MixHash(hash);
            }

            return MixHash(hash);
        }

        private static float HashToUnit01(uint value)
        {
            return (MixHash(value) & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static uint MixHash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        /// <summary>
        /// Pereschityvaet runtime stats iz baseStats + vse ustanovlennye apgreydy.
        /// Vyzyvaetsya pri ustanovke apgreyda i pri zagruzke.
        /// </summary>
        private void RebuildRuntimeStats()
        {
            if (_runtimeStats == null || baseStats == null || allUpgrades == null) return;

            // Nakaplivaem delty
            float dOxygen    = 0f;
            float dEnergy    = 0f;
            float dDepth     = 0f;
            float dIntegrity = 0f;
            float dMinTemp   = 0f;
            float dMaxTemp   = 0f;
            float dRad       = 0f;

            for (int i = 0; i < allUpgrades.Length; i++)
            {
                SuitUpgradeData u = allUpgrades[i];
                if (u == null || !_installedUpgrades.Contains(u.upgradeId) || _brokenUpgrades.Contains(u.upgradeId)) continue;

                dOxygen    += u.deltaMaxOxygen;
                dEnergy    += u.deltaMaxEnergy;
                dDepth     += u.deltaSafeDepth;
                dIntegrity += u.deltaMaxIntegrity;
                dMinTemp   += u.deltaMinSafeTemp;
                dMaxTemp   += u.deltaMaxSafeTemp;
                dRad       += u.deltaRadiationThreshold;
            }

            // Primenyaem cherez OverrideStats — nuzhen novyy SO s izmenennymi znacheniyami
            // SurvivalStats immutable — ispolzuem Instantiate + reflection-free podhod
            // cherez otdelnyy RuntimeSurvivalStats helper
            ApplyDeltasToRuntimeStats(dOxygen, dEnergy, dDepth, dIntegrity, dMinTemp, dMaxTemp, dRad);

            // Primenyaem k sisteme vyzhivaniya
            if (survivalSystem != null)
                survivalSystem.OverrideStats(_runtimeStats);
        }

        private void ApplyDeltasToRuntimeStats(
            float dOxygen, float dEnergy, float dDepth, float dIntegrity,
            float dMinTemp, float dMaxTemp, float dRad)
        {
            // SurvivalStats — immutable SO s private setters.
            // Ispolzuem RuntimeSurvivalStats — mutable wrapper.
            // Esli _runtimeStats uzhe RuntimeSurvivalStats — obnovlyaem napryamuyu.
            if (_runtimeStats is RuntimeSurvivalStats rts)
            {
                rts.ApplyDeltas(baseStats, dOxygen, dEnergy, dDepth, dIntegrity, dMinTemp, dMaxTemp, dRad);
            }
            else
            {
                // Pervyy raz — sozdaem RuntimeSurvivalStats
                RuntimeSurvivalStats newRts = ScriptableObject.CreateInstance<RuntimeSurvivalStats>();
                newRts.ApplyDeltas(baseStats, dOxygen, dEnergy, dDepth, dIntegrity, dMinTemp, dMaxTemp, dRad);
                if (_runtimeStats != null) Destroy(_runtimeStats);
                _runtimeStats = newRts;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public void PopulateSaveData(SaveData data)
        {
            if (data == null) return;

            data.suitInstalledUpgradeIds.Clear();
            data.suitUnlockedBlueprintIds.Clear();
            data.suitBrokenUpgradeIds.Clear();

            HashSet<string>.Enumerator installedEnumerator = _installedUpgrades.GetEnumerator();
            while (installedEnumerator.MoveNext())
                data.suitInstalledUpgradeIds.Add(installedEnumerator.Current);

            HashSet<string>.Enumerator blueprintEnumerator = _unlockedBlueprints.GetEnumerator();
            while (blueprintEnumerator.MoveNext())
                data.suitUnlockedBlueprintIds.Add(blueprintEnumerator.Current);

            HashSet<string>.Enumerator brokenEnumerator = _brokenUpgrades.GetEnumerator();
            while (brokenEnumerator.MoveNext())
                data.suitBrokenUpgradeIds.Add(brokenEnumerator.Current);
        }

        public void LoadFromSaveData(SaveData data)
        {
            _installedUpgrades.Clear();
            _unlockedBlueprints.Clear();
            _brokenUpgrades.Clear();

            if (data == null) return;

            if (data.suitInstalledUpgradeIds != null)
            {
                for (int i = 0, count = data.suitInstalledUpgradeIds.Count; i < count; i++)
                {
                    string id = data.suitInstalledUpgradeIds[i];
                    if (!string.IsNullOrEmpty(id))
                        _installedUpgrades.Add(id);
                }
            }

            if (data.suitUnlockedBlueprintIds != null)
            {
                for (int i = 0, count = data.suitUnlockedBlueprintIds.Count; i < count; i++)
                {
                    string id = data.suitUnlockedBlueprintIds[i];
                    if (!string.IsNullOrEmpty(id))
                        _unlockedBlueprints.Add(id);
                }
            }

            if (data.suitBrokenUpgradeIds != null)
            {
                for (int i = 0, count = data.suitBrokenUpgradeIds.Count; i < count; i++)
                {
                    string id = data.suitBrokenUpgradeIds[i];
                    if (!string.IsNullOrEmpty(id) && _installedUpgrades.Contains(id))
                        _brokenUpgrades.Add(id);
                }
            }

            RebuildRuntimeStats();
        }

        private SuitUpgradeData FindUpgradeById(string upgradeId)
        {
            if (string.IsNullOrEmpty(upgradeId) || allUpgrades == null)
                return null;

            for (int i = 0; i < allUpgrades.Length; i++)
            {
                SuitUpgradeData upgrade = allUpgrades[i];
                if (upgrade != null && upgrade.upgradeId == upgradeId)
                    return upgrade;
            }

            return null;
        }
    }
}
