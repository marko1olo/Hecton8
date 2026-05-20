// ============================================================================
// HECTON-8 --- SuitUpgradeManager.cs
// Menedzher apgreydov skafandra.
//
// LOR (lor1): Progressiya glubiny cherez apgreydy korpusa.
//   Tier 0 --- Tier 1: pervyy kraft v igre (rasshirennyy O2 rezervuar).
//   Tier 4: finalnyy --- do -5000m, O2 45 min.
//
// ARHITEKTURA:
//   --- Primenyaet apgreydy cherez HectonSurvivalSystem.OverrideStats().
//   --- Runtime-kopiya SurvivalStats --- ne mutiruet originalnyy SO.
//   --- ISaveable: sohranyaet spisok ustanovlennyh upgradeId.
//   --- Slushaet NarrativeEvents.OnDiscoveryMade dlya razblokirovki chertezhey.
//
// ZERO GC:
//   --- HashSet<string> dlya O(1) proverki ustanovlennyh apgreydov.
//   --- Nikakih new/LINQ v hot path.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-110)]
    public sealed class SuitUpgradeManager : MonoBehaviour, ISaveable, INarrativeEventListener, IGlobalRegistryHotSwapListener, ILateFrameTickable
    {
        // ----------------------------------------------------------
        //  INSPECTOR
        // ----------------------------------------------------------

        [Header("References")]
        [Tooltip("Bazovye parametry skafandra (Tier 0).")]
        [SerializeField] private SurvivalStats baseStats;

        [Tooltip("Sistema vyzhivaniya igroka.")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;

        [Header("Upgrades")]
        [Tooltip("Vse apgreydy v igre. Poryadok ne vazhen --- sortiruyutsya po tier.")]
        [SerializeField] private SuitUpgradeData[] allUpgrades = new SuitUpgradeData[0];

        // ----------------------------------------------------------
        //  SINGLETON
        // ----------------------------------------------------------

        public static SuitUpgradeManager Instance => GlobalRegistry.SuitUpgrades;

        // ----------------------------------------------------------
        //  PRIVATE STATE
        // ----------------------------------------------------------

        // COLD ALLOC: 32 entries --- max installed upgrades
        private const int ResolverResultLength = 1;
        private const uint ItemEquipOxygenRigT1Hash = 0xF0B55FA2u;
        private const uint ItemEquipOxygenRigT2Hash = 0xEFB55E0Fu;
        private const uint ItemEquipPressureHarnessT1Hash = 0x204A1C5Fu;
        private const uint ItemEquipPressureHarnessT2Hash = 0x214A1DF2u;
        private const uint ItemEquipThermalLinerT1Hash = 0x120ADBAFu;
        private const uint ItemEquipThermalLinerT2Hash = 0x130ADD42u;
        private const uint ItemEquipRadiationVeilHash = 0x68A0D2D9u;
        private const uint ItemEquipServiceFinsHash = 0x1367A143u;
        private const uint ItemEquipHudVisorAtlasHash = 0x7F5F6211u;
        private const int TelemetryCapacity = 300;
        private const int TelemetryEntrySizeBytes = 64;
        private const ulong TelemetryDumpMagic = 0x5250475055544953UL;
        private const string TelemetryDumpRelativePath = "Docs/AgentLogs/Dump_SUIT_UPGRADE_SYSTEM.bin";
        private const uint TelemetryFlagResolved = 1u << 0;
        private const uint TelemetryFlagNonFinite = 1u << 31;

        private readonly HashSet<string> _installedUpgrades  = new HashSet<string>(32);
        private readonly HashSet<string> _unlockedBlueprints = new HashSet<string>(32);
        private readonly HashSet<string> _brokenUpgrades = new HashSet<string>(16);

        // Runtime stats --- clone of baseStats with deltas applied
        private SurvivalStats _runtimeStats;
        private uint _breakOrdinal;
        private bool _serviceRegistered;
        private bool _lateFrameRegistered;
        private bool _inventorySyncQueued;
        private bool _inventorySyncRunning;
        private bool _hotSwapRegistered;
        private PlayerInventory _subscribedInventory;
        private uint _inventorySignalHash;
        private uint _lastInventorySignalRevision;
        private int _lastInventoryVersion = -1;
        private ulong _inventoryUpgradeMask;
        private ulong _upgradeMask;
        private ulong _effectiveUpgradeMask;
        private SuitStats _baseSuitStats;
        private SuitStats _resolvedSuitStats;
        private VaultBufferHandle<SuitStats> _resolverResultHandle;
        private NativeArray<SuitUpgradeTelemetryEntry> _telemetryRing;
        private uint _meshSignalSequence;
        private uint _telemetrySequence;
        private int _telemetryCursor;
        private bool _telemetryDumped;
        private SuitUpgradeLookupEntry[] _upgradeLookup = Array.Empty<SuitUpgradeLookupEntry>();

        private struct SuitUpgradeLookupEntry
        {
            public uint ItemHash;
            public SuitUpgrades Bit;
            public string UpgradeId;
        }

        [StructLayout(LayoutKind.Explicit, Size = TelemetryEntrySizeBytes)]
        private struct SuitUpgradeTelemetryEntry
        {
            [FieldOffset(0)] public uint FrameIndex;
            [FieldOffset(4)] public uint Sequence;
            [FieldOffset(8)] public ulong UpgradeMask;
            [FieldOffset(16)] public ulong EffectiveMask;
            [FieldOffset(24)] public ulong InventoryMask;
            [FieldOffset(32)] public uint Flags;
            [FieldOffset(36)] public uint StateHash;
            [FieldOffset(40)] public float MaxO2;
            [FieldOffset(44)] public float CrushDepth;
            [FieldOffset(48)] public float SwimSpeedMultiplier;
            [FieldOffset(52)] public float ThermalResistance;
            [FieldOffset(56)] public float MaxEnergy;
            [FieldOffset(60)] public float RadiationThreshold;
        }

        // ----------------------------------------------------------
        //  ISaveable
        // ----------------------------------------------------------

        public int SavePriority => 9;
        public int LoadPriority => 9;

        // ----------------------------------------------------------
        //  PUBLIC PROPERTIES
        // ----------------------------------------------------------

        public int InstalledCount => _installedUpgrades.Count;
        public ulong UpgradeMask => _upgradeMask;
        public ulong EffectiveUpgradeMask => _effectiveUpgradeMask;
        public ref readonly SuitStats CurrentStats => ref _resolvedSuitStats;
        public ref readonly SuitStats CurrentSuitStats => ref _resolvedSuitStats;
        public float CurrentMaxO2 => _resolvedSuitStats.MaxO2;
        public float CurrentSwimSpeedMultiplier => SuitUpgradeResolver.ResolveSwimSpeedMultiplier(in _resolvedSuitStats);

        /// <summary>Tekuschiy maksimalnyy tir ustanovlennyh apgreydov korpusa.</summary>
        public int CurrentHullTier
        {
            get
            {
                return SuitUpgradeResolver.ResolveHullTier(_effectiveUpgradeMask);
            }
        }

        public bool HasAbility(uint abilityHash)
        {
            return SuitUpgradeResolver.HasAbility(_effectiveUpgradeMask, abilityHash);
        }

        public static bool HasAbility(ulong mask, uint abilityHash)
        {
            return SuitUpgradeResolver.HasAbility(mask, abilityHash);
        }

        public static float ResolveSwimSpeedMultiplier(in SuitStats stats)
        {
            return SuitUpgradeResolver.ResolveSwimSpeedMultiplier(in stats);
        }

        // ----------------------------------------------------------
        //  LIFECYCLE
        // ----------------------------------------------------------

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
            _baseSuitStats = BuildBaselineSuitStats();
            _resolvedSuitStats = _baseSuitStats;
            _ = TryResolveResolverResultBuffer(out _);
            _telemetryRing = new NativeArray<SuitUpgradeTelemetryEntry>(
                TelemetryCapacity,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<SuitUpgradeTelemetryEntry>[300] - suit upgrade black-box ring - owner: SuitUpgradeManager
            NativeMemorySentinel.RegisterNativeArray(
                _telemetryRing,
                nameof(SuitUpgradeManager),
                nameof(_telemetryRing),
                NativeAllocationLifetime.Scene);
            RebuildUpgradeLookupCache();
            ResolveAndApplyUpgradeMask(0UL);
        }

        private void OnEnable()
        {
            if (!TryRegisterService())
                return;

            TryRegisterHotSwapListener();
            TryRegisterLateFrame();

            if (Hecton8.Core.GlobalRegistry.SaveRuntime != null)
                Hecton8.Core.GlobalRegistry.SaveRuntime.Register(this);

            NarrativeEvents.Register(this);
            TryBindInventory();
            QueueInventoryMaskRebuild();
        }

        private void OnDisable()
        {
            _inventorySyncQueued = false;
            TryUnregisterLateFrame();
            UnbindInventory();
            TryUnregisterHotSwapListener();

            if (Hecton8.Core.GlobalRegistry.SaveRuntime != null)
                Hecton8.Core.GlobalRegistry.SaveRuntime.Unregister(this);

            NarrativeEvents.Unregister(this);
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            TryUnregisterLateFrame();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            _resolverResultHandle = default;

            if (_telemetryRing.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_telemetryRing);
                _telemetryRing.Dispose();
                _telemetryRing = default;
            }

            if (_runtimeStats != null && !ReferenceEquals(_runtimeStats, baseStats))
            {
                Destroy(_runtimeStats);
                _runtimeStats = null;
            }
        }

        private bool TryRegisterService()
        {
            if (_serviceRegistered)
                return true;

            if (!Application.isPlaying)
                return false;

            SuitUpgradeManager registered = Hecton8.Core.GlobalRegistry.SuitUpgrades;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return false;
            }

            Hecton8.Core.GlobalRegistry.RegisterSuitUpgradeRuntime(this);
            _serviceRegistered = ReferenceEquals(Hecton8.Core.GlobalRegistry.SuitUpgrades, this);
            return _serviceRegistered;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            Hecton8.Core.GlobalRegistry.UnregisterSuitUpgradeRuntime(this);
            _serviceRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            TryRegisterLateFrame();

            if (serviceSlot != GlobalRegistryServiceSlot.PlayerInventory)
                return;

            PlayerInventory inventory = null;
            if (currentService is IPlayerInventoryService inventoryService)
                inventory = inventoryService.Inventory;

            BindInventory(inventory);
            QueueInventoryMaskRebuild();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = Hecton8.Core.GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            Hecton8.Core.GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void TryRegisterLateFrame()
        {
            if (_lateFrameRegistered || !Application.isPlaying || Hecton8.Core.GlobalRegistry.Dispatcher == null)
                return;

            _lateFrameRegistered = Hecton8.Core.GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_lateFrameRegistered)
                return;

            Hecton8.Core.GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _lateFrameRegistered = false;
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

        // ----------------------------------------------------------
        //  PUBLIC API
        // ----------------------------------------------------------

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

        // ----------------------------------------------------------
        //  PRIVATE
        // ----------------------------------------------------------

        public void OnNarrativeEvent(in NarrativeEventPayload payload)
        {
            if ((NarrativeEventType)payload.EventType != NarrativeEventType.DiscoveryMade ||
                payload.DiscoveryHash == 0u ||
                allUpgrades == null)
            {
                return;
            }

            // Proveryaem --- yavlyaetsya li eto chertezhom apgreyda
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
            RefreshInventoryUpgradeMask(true);
            ResolveAndApplyUpgradeMask(BuildInstalledUpgradeMask() | _inventoryUpgradeMask);
        }

        private void RebuildUpgradeLookupCache()
        {
            if (allUpgrades == null || allUpgrades.Length == 0)
            {
                _upgradeLookup = Array.Empty<SuitUpgradeLookupEntry>();
                return;
            }

            int count = 0;
            for (int i = 0; i < allUpgrades.Length; i++)
            {
                SuitUpgradeData upgrade = allUpgrades[i];
                if (upgrade == null || string.IsNullOrEmpty(upgrade.upgradeId))
                    continue;

                SuitUpgrades bit = SuitUpgradeResolver.ResolveUpgradeBit(upgrade);
                if (bit != SuitUpgrades.None)
                {
                    count += 1 + CountEquipmentItemHashAliases(bit);
                }
            }

            if (count == 0)
            {
                _upgradeLookup = Array.Empty<SuitUpgradeLookupEntry>();
                return;
            }

            SuitUpgradeLookupEntry[] lookup = new SuitUpgradeLookupEntry[count]; // COLD ALLOC: equipment hash to bit lookup - owner: SuitUpgradeManager
            int cursor = 0;
            for (int i = 0; i < allUpgrades.Length; i++)
            {
                SuitUpgradeData upgrade = allUpgrades[i];
                if (upgrade == null || string.IsNullOrEmpty(upgrade.upgradeId))
                    continue;

                SuitUpgrades bit = SuitUpgradeResolver.ResolveUpgradeBit(upgrade);
                if (bit == SuitUpgrades.None)
                    continue;

                cursor = AddUpgradeLookupEntry(
                    lookup,
                    cursor,
                    unchecked((uint)LocHash.Compute(upgrade.upgradeId)),
                    bit,
                    upgrade.upgradeId);
                cursor = AddEquipmentItemHashAliases(lookup, cursor, bit, upgrade.upgradeId);
            }

            if (lookup.Length > 1)
                SortUpgradeLookupByItemHash(lookup);

            _upgradeLookup = lookup;
        }

        private static int CountEquipmentItemHashAliases(SuitUpgrades bit)
        {
            switch (bit)
            {
                case SuitUpgrades.HighCapacityTank:
                    return 2;
                case SuitUpgrades.DepthModuleMk1:
                case SuitUpgrades.DepthModuleMk2:
                case SuitUpgrades.SwimFins:
                case SuitUpgrades.ThermalLining:
                case SuitUpgrades.ThermalGenerator:
                case SuitUpgrades.RadiationScrubber:
                case SuitUpgrades.SonarPing:
                    return 1;
                default:
                    return 0;
            }
        }

        private static int AddEquipmentItemHashAliases(
            SuitUpgradeLookupEntry[] lookup,
            int cursor,
            SuitUpgrades bit,
            string upgradeId)
        {
            switch (bit)
            {
                case SuitUpgrades.HighCapacityTank:
                    cursor = AddUpgradeLookupEntry(lookup, cursor, ItemEquipOxygenRigT1Hash, bit, upgradeId);
                    return AddUpgradeLookupEntry(lookup, cursor, ItemEquipOxygenRigT2Hash, bit, upgradeId);
                case SuitUpgrades.DepthModuleMk1:
                    return AddUpgradeLookupEntry(lookup, cursor, ItemEquipPressureHarnessT1Hash, bit, upgradeId);
                case SuitUpgrades.DepthModuleMk2:
                    return AddUpgradeLookupEntry(lookup, cursor, ItemEquipPressureHarnessT2Hash, bit, upgradeId);
                case SuitUpgrades.SwimFins:
                    return AddUpgradeLookupEntry(lookup, cursor, ItemEquipServiceFinsHash, bit, upgradeId);
                case SuitUpgrades.ThermalLining:
                    return AddUpgradeLookupEntry(lookup, cursor, ItemEquipThermalLinerT1Hash, bit, upgradeId);
                case SuitUpgrades.ThermalGenerator:
                    return AddUpgradeLookupEntry(lookup, cursor, ItemEquipThermalLinerT2Hash, bit, upgradeId);
                case SuitUpgrades.RadiationScrubber:
                    return AddUpgradeLookupEntry(lookup, cursor, ItemEquipRadiationVeilHash, bit, upgradeId);
                case SuitUpgrades.SonarPing:
                    return AddUpgradeLookupEntry(lookup, cursor, ItemEquipHudVisorAtlasHash, bit, upgradeId);
                default:
                    return cursor;
            }
        }

        private static int AddUpgradeLookupEntry(
            SuitUpgradeLookupEntry[] lookup,
            int cursor,
            uint itemHash,
            SuitUpgrades bit,
            string upgradeId)
        {
            lookup[cursor] = new SuitUpgradeLookupEntry
            {
                ItemHash = itemHash,
                Bit = bit,
                UpgradeId = upgradeId
            };
            return cursor + 1;
        }

        private static void SortUpgradeLookupByItemHash(SuitUpgradeLookupEntry[] lookup)
        {
            for (int i = 1; i < lookup.Length; i++)
            {
                SuitUpgradeLookupEntry entry = lookup[i];
                int cursor = i - 1;
                while (cursor >= 0 && lookup[cursor].ItemHash > entry.ItemHash)
                {
                    lookup[cursor + 1] = lookup[cursor];
                    cursor--;
                }

                lookup[cursor + 1] = entry;
            }
        }

        private SuitStats BuildBaselineSuitStats()
        {
            if (baseStats == null)
                return SuitUpgradeResolver.CreateBaseline();

            return new SuitStats
            {
                MaxO2 = baseStats.MaxOxygen,
                CrushDepth = baseStats.SafeDepth,
                SwimSpeedMultiplier = 1f,
                ThermalResistance = 0f,
                MaxEnergy = baseStats.MaxEnergy,
                MaxIntegrity = baseStats.MaxIntegrity,
                MinSafeTemperature = baseStats.MinSafeTemp,
                MaxSafeTemperature = baseStats.MaxSafeTemp,
                RadiationThreshold = baseStats.RadiationThreshold
            };
        }

        private void TryBindInventory()
        {
            BindInventory(GlobalRegistry.PlayerInventoryRuntime);
        }

        private void BindInventory(PlayerInventory inventory)
        {
            if (ReferenceEquals(inventory, _subscribedInventory))
                return;

            UnbindInventory();
            if (inventory == null)
                return;

            _subscribedInventory = inventory;
            _inventorySignalHash = ResolveInventorySignalHash(inventory);
            _lastInventorySignalRevision = inventory.InventoryVersion > 0
                ? unchecked((uint)inventory.InventoryVersion)
                : 0u;
            _lastInventoryVersion = -1;
        }

        private void UnbindInventory()
        {
            _subscribedInventory = null;
            _inventorySignalHash = 0u;
            _lastInventorySignalRevision = 0u;
            _lastInventoryVersion = -1;
            _inventoryUpgradeMask = 0UL;
        }

        public void LateFrameTick()
        {
            if (!isActiveAndEnabled)
                return;

            TryBindInventory();
            if (ConsumeInventoryChangedSignals())
                QueueInventoryMaskRebuild();
        }

        private bool ConsumeInventoryChangedSignals()
        {
            PlayerInventory inventory = _subscribedInventory != null ? _subscribedInventory : GlobalRegistry.PlayerInventoryRuntime;
            uint inventoryHash = ResolveInventorySignalHash(inventory);
            if (inventoryHash == 0u)
                return false;

            if (inventoryHash != _inventorySignalHash)
            {
                _inventorySignalHash = inventoryHash;
                _lastInventorySignalRevision = inventory != null && inventory.InventoryVersion > 0
                    ? unchecked((uint)inventory.InventoryVersion)
                    : 0u;
            }

            ReadOnlySpan<InventoryChangedSignal> signals = SignalBus<InventoryChangedSignal>.GetFrameSnapshot();
            bool changed = false;
            for (int i = 0; i < signals.Length; i++)
            {
                ref readonly InventoryChangedSignal signal = ref signals[i];
                if (signal.InventoryHash != inventoryHash ||
                    signal.Revision == 0u ||
                    (_lastInventorySignalRevision != 0u && signal.Revision <= _lastInventorySignalRevision))
                {
                    continue;
                }

                _lastInventorySignalRevision = signal.Revision;
                changed = true;
            }

            return changed;
        }

        private static uint ResolveInventorySignalHash(PlayerInventory inventory)
        {
            return inventory != null && inventory.gameObject != null
                ? unchecked((uint)EntityId.ToULong(inventory.gameObject.GetEntityId()))
                : 0u;
        }

        private void QueueInventoryMaskRebuild()
        {
            _inventorySyncQueued = true;
            if (_inventorySyncRunning || !isActiveAndEnabled)
                return;

            _ = RunInventoryMaskRebuildAsync(destroyCancellationToken);
        }

        private async Awaitable RunInventoryMaskRebuildAsync(CancellationToken cancellationToken)
        {
            _inventorySyncRunning = true;
            try
            {
                while (_inventorySyncQueued && !cancellationToken.IsCancellationRequested && isActiveAndEnabled)
                {
                    _inventorySyncQueued = false;
                    await AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
                    if (cancellationToken.IsCancellationRequested || !isActiveAndEnabled)
                        break;

                    TryBindInventory();
                    RefreshInventoryUpgradeMask(false);
                    ResolveAndApplyUpgradeMask(BuildInstalledUpgradeMask() | _inventoryUpgradeMask);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _inventorySyncRunning = false;
                if (_inventorySyncQueued && isActiveAndEnabled && !destroyCancellationToken.IsCancellationRequested)
                    QueueInventoryMaskRebuild();
            }
        }

        private ulong BuildInventoryMask(PlayerInventory inventory)
        {
            if (inventory == null)
                return 0UL;

            NativeArray<uint>.ReadOnly itemHashes = inventory.GetItemHashesReadOnly();
            NativeArray<ushort>.ReadOnly stackCounts = inventory.GetStackCountsReadOnly();
            if (!itemHashes.IsCreated || !stackCounts.IsCreated)
                return 0UL;

            ItemCatalog catalog = inventory.ItemCatalog;
            int count = math.min(itemHashes.Length, stackCounts.Length);
            ulong mask = 0UL;
            for (int i = 0; i < count; i++)
            {
                if (stackCounts[i] == 0)
                    continue;

                uint itemHash = itemHashes[i];
                if (itemHash == 0u)
                    continue;

                if (catalog == null ||
                    !catalog.TryGetRuntimeDescriptor(unchecked((int)itemHash), out ItemCatalog.ItemRuntimeDescriptor descriptor) ||
                    descriptor.CategoryId != (byte)ItemCategory.Equipment)
                {
                    continue;
                }

                ulong itemMask = ResolveMaskForItemHash(itemHash);
                if (itemMask == 0UL)
                    continue;

                mask |= itemMask;
            }

            return mask;
        }

        private void RefreshInventoryUpgradeMask(bool force)
        {
            PlayerInventory inventory = _subscribedInventory != null ? _subscribedInventory : GlobalRegistry.PlayerInventoryRuntime;
            if (inventory == null)
            {
                _inventoryUpgradeMask = 0UL;
                _lastInventoryVersion = -1;
                return;
            }

            int inventoryVersion = inventory.InventoryVersion;
            if (!force && inventoryVersion == _lastInventoryVersion)
                return;

            _inventoryUpgradeMask = BuildInventoryMask(inventory);
            _lastInventoryVersion = inventoryVersion;
        }

        private ulong BuildInstalledUpgradeMask()
        {
            if (allUpgrades == null || allUpgrades.Length == 0)
                return 0UL;

            ulong mask = 0UL;
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

                mask |= (ulong)SuitUpgradeResolver.ResolveUpgradeBit(upgrade);
            }

            return mask;
        }

        private ulong ResolveMaskForItemHash(uint itemHash)
        {
            SuitUpgradeLookupEntry[] lookup = _upgradeLookup;
            int low = 0;
            int high = lookup.Length;
            while (low < high)
            {
                int mid = low + ((high - low) >> 1);
                if (lookup[mid].ItemHash < itemHash)
                    low = mid + 1;
                else
                    high = mid;
            }

            ulong mask = 0UL;
            for (int i = low; i < lookup.Length && lookup[i].ItemHash == itemHash; i++)
            {
                string upgradeId = lookup[i].UpgradeId;
                if (!string.IsNullOrEmpty(upgradeId) && _brokenUpgrades.Contains(upgradeId))
                    continue;

                mask |= (ulong)lookup[i].Bit;
            }

            return mask;
        }

        private void ResolveAndApplyUpgradeMask(ulong mask)
        {
            ulong sanitized = mask & SuitUpgradeResolver.SupportedMask;
            ulong previousEffectiveMask = _effectiveUpgradeMask;
            _upgradeMask = sanitized;

            if (TryResolveResolverResultBuffer(out NativeArray<SuitStats> resolverResult))
            {
                SuitUpgradeResolverJob job = new SuitUpgradeResolverJob
                {
                    Upgrades = sanitized,
                    Baseline = _baseSuitStats,
                    Result = new NativeSlice<SuitStats>(resolverResult)
                };
                job.Execute();
                _resolvedSuitStats = resolverResult[0];
            }
            else
            {
                _resolvedSuitStats = SuitUpgradeResolver.Resolve(sanitized, in _baseSuitStats);
            }

            _effectiveUpgradeMask = SuitUpgradeResolver.NormalizeMask(sanitized);
            uint telemetryFlags = TelemetryFlagResolved;
            if (!AreSuitStatsFinite(in _resolvedSuitStats))
            {
                telemetryFlags |= TelemetryFlagNonFinite;
                RecordTelemetry(telemetryFlags);
                DumpTelemetry(telemetryFlags);
                return;
            }

            RecordTelemetry(telemetryFlags);
            ApplyResolvedSuitStatsToRuntimeStats(in _resolvedSuitStats);
            RaiseSuitMeshSignalIfChanged(previousEffectiveMask);
        }

        private bool TryResolveResolverResultBuffer(out NativeArray<SuitStats> resolverResult)
        {
            resolverResult = default;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            if (!_resolverResultHandle.IsCreated)
            {
                _resolverResultHandle = vault.GetBufferHandle<SuitStats>(
                    BufferID.SuitUpgradeResolverResult,
                    ResolverResultLength,
                    SystemID.GameplayPlayer,
                    NativeArrayOptions.UninitializedMemory);
            }

            if (!_resolverResultHandle.IsCreated)
                return false;

            resolverResult = _resolverResultHandle.Resolve(vault);
            return resolverResult.IsCreated && resolverResult.Length >= ResolverResultLength;
        }

        private void ApplyResolvedSuitStatsToRuntimeStats(in SuitStats resolvedStats)
        {
            if (_runtimeStats == null || baseStats == null)
                return;

            ApplyDeltasToRuntimeStats(
                resolvedStats.MaxO2 - _baseSuitStats.MaxO2,
                resolvedStats.MaxEnergy - _baseSuitStats.MaxEnergy,
                resolvedStats.CrushDepth - _baseSuitStats.CrushDepth,
                resolvedStats.MaxIntegrity - _baseSuitStats.MaxIntegrity,
                resolvedStats.MinSafeTemperature - _baseSuitStats.MinSafeTemperature,
                resolvedStats.MaxSafeTemperature - _baseSuitStats.MaxSafeTemperature,
                resolvedStats.RadiationThreshold - _baseSuitStats.RadiationThreshold);

            if (survivalSystem != null)
                survivalSystem.OverrideStats(_runtimeStats);
        }

        private void RaiseSuitMeshSignalIfChanged(ulong previousEffectiveMask)
        {
            if (previousEffectiveMask == _effectiveUpgradeMask)
                return;

            SuitMeshUpdateEvents.Raise(new SuitMeshUpdateSignal(_upgradeMask, _effectiveUpgradeMask, _meshSignalSequence++));
        }

        private void RecordTelemetry(uint flags)
        {
            if (!_telemetryRing.IsCreated)
                return;

            int index = _telemetryCursor;
            if ((uint)index >= TelemetryCapacity)
                index = 0;

            SuitStats stats = _resolvedSuitStats;
            uint stateHash = BuildTelemetryHash(_upgradeMask, _effectiveUpgradeMask, _inventoryUpgradeMask, in stats, flags);
            _telemetryRing[index] = new SuitUpgradeTelemetryEntry
            {
                FrameIndex = unchecked((uint)Mathf.Max(0, Time.frameCount)),
                Sequence = _telemetrySequence++,
                UpgradeMask = _upgradeMask,
                EffectiveMask = _effectiveUpgradeMask,
                InventoryMask = _inventoryUpgradeMask,
                Flags = flags,
                StateHash = stateHash,
                MaxO2 = stats.MaxO2,
                CrushDepth = stats.CrushDepth,
                SwimSpeedMultiplier = stats.SwimSpeedMultiplier,
                ThermalResistance = stats.ThermalResistance,
                MaxEnergy = stats.MaxEnergy,
                RadiationThreshold = stats.RadiationThreshold
            };

            index++;
            if (index >= TelemetryCapacity)
                index = 0;

            _telemetryCursor = index;
        }

        private void DumpTelemetry(uint reasonFlags)
        {
            if (_telemetryDumped || !_telemetryRing.IsCreated)
                return;

            _telemetryDumped = true;
            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", TelemetryDumpRelativePath));
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(TelemetryDumpMagic);
                    writer.Write((uint)TelemetryCapacity);
                    writer.Write((uint)_telemetryCursor);
                    writer.Write((uint)TelemetryEntrySizeBytes);
                    writer.Write(reasonFlags);

                    for (int i = 0; i < TelemetryCapacity; i++)
                    {
                        int index = _telemetryCursor + i;
                        if (index >= TelemetryCapacity)
                            index -= TelemetryCapacity;

                        SuitUpgradeTelemetryEntry entry = _telemetryRing[index];
                        WriteTelemetryEntry(writer, in entry);
                    }
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(exception, this);
#endif
            }
        }

        private static void WriteTelemetryEntry(BinaryWriter writer, in SuitUpgradeTelemetryEntry entry)
        {
            writer.Write(entry.FrameIndex);
            writer.Write(entry.Sequence);
            writer.Write(entry.UpgradeMask);
            writer.Write(entry.EffectiveMask);
            writer.Write(entry.InventoryMask);
            writer.Write(entry.Flags);
            writer.Write(entry.StateHash);
            writer.Write(entry.MaxO2);
            writer.Write(entry.CrushDepth);
            writer.Write(entry.SwimSpeedMultiplier);
            writer.Write(entry.ThermalResistance);
            writer.Write(entry.MaxEnergy);
            writer.Write(entry.RadiationThreshold);
        }

        private static bool AreSuitStatsFinite(in SuitStats stats)
        {
            return IsFinite(stats.MaxO2) &&
                   IsFinite(stats.CrushDepth) &&
                   IsFinite(stats.SwimSpeedMultiplier) &&
                   IsFinite(stats.ThermalResistance) &&
                   IsFinite(stats.MaxEnergy) &&
                   IsFinite(stats.MaxIntegrity) &&
                   IsFinite(stats.MinSafeTemperature) &&
                   IsFinite(stats.MaxSafeTemperature) &&
                   IsFinite(stats.RadiationThreshold);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static uint BuildTelemetryHash(
            ulong upgradeMask,
            ulong effectiveMask,
            ulong inventoryMask,
            in SuitStats stats,
            uint flags)
        {
            uint hash = 2166136261u;
            hash = HashTelemetry(hash, unchecked((uint)upgradeMask));
            hash = HashTelemetry(hash, unchecked((uint)(upgradeMask >> 32)));
            hash = HashTelemetry(hash, unchecked((uint)effectiveMask));
            hash = HashTelemetry(hash, unchecked((uint)(effectiveMask >> 32)));
            hash = HashTelemetry(hash, unchecked((uint)inventoryMask));
            hash = HashTelemetry(hash, unchecked((uint)(inventoryMask >> 32)));
            hash = HashTelemetry(hash, flags);
            hash = HashTelemetry(hash, math.asuint(stats.MaxO2));
            hash = HashTelemetry(hash, math.asuint(stats.CrushDepth));
            hash = HashTelemetry(hash, math.asuint(stats.SwimSpeedMultiplier));
            hash = HashTelemetry(hash, math.asuint(stats.ThermalResistance));
            hash = HashTelemetry(hash, math.asuint(stats.MaxEnergy));
            hash = HashTelemetry(hash, math.asuint(stats.MaxIntegrity));
            hash = HashTelemetry(hash, math.asuint(stats.MinSafeTemperature));
            hash = HashTelemetry(hash, math.asuint(stats.MaxSafeTemperature));
            hash = HashTelemetry(hash, math.asuint(stats.RadiationThreshold));
            return hash;
        }

        private static uint HashTelemetry(uint hash, uint value)
        {
            return unchecked((hash ^ value) * 16777619u);
        }

        private void AddInstalledUpgradeIdsFromMask(ulong mask)
        {
            if (mask == 0UL || allUpgrades == null || allUpgrades.Length == 0)
                return;

            ulong effectiveMask = SuitUpgradeResolver.NormalizeMask(mask);
            for (int i = 0; i < allUpgrades.Length; i++)
            {
                SuitUpgradeData upgrade = allUpgrades[i];
                if (upgrade == null || string.IsNullOrEmpty(upgrade.upgradeId))
                    continue;

                SuitUpgrades bit = SuitUpgradeResolver.ResolveUpgradeBit(upgrade);
                if (bit != SuitUpgrades.None && (effectiveMask & (ulong)bit) != 0UL)
                    _installedUpgrades.Add(upgrade.upgradeId);
            }
        }

        private void ApplyDeltasToRuntimeStats(
            float dOxygen, float dEnergy, float dDepth, float dIntegrity,
            float dMinTemp, float dMaxTemp, float dRad)
        {
            // SurvivalStats --- immutable SO s private setters.
            // Ispolzuem RuntimeSurvivalStats --- mutable wrapper.
            // Esli _runtimeStats uzhe RuntimeSurvivalStats --- obnovlyaem napryamuyu.
            if (_runtimeStats is RuntimeSurvivalStats rts)
            {
                rts.ApplyDeltas(baseStats, dOxygen, dEnergy, dDepth, dIntegrity, dMinTemp, dMaxTemp, dRad);
            }
            else
            {
                // Pervyy raz --- sozdaem RuntimeSurvivalStats
                RuntimeSurvivalStats newRts = ScriptableObject.CreateInstance<RuntimeSurvivalStats>();
                newRts.ApplyDeltas(baseStats, dOxygen, dEnergy, dDepth, dIntegrity, dMinTemp, dMaxTemp, dRad);
                if (_runtimeStats != null) Destroy(_runtimeStats);
                _runtimeStats = newRts;
            }
        }

        // ----------------------------------------------------------
        //  ISaveable
        // ----------------------------------------------------------

        public void PopulateSaveData(SaveData data)
        {
            if (data == null) return;

            RefreshInventoryUpgradeMask(true);
            ulong serializedMask = (BuildInstalledUpgradeMask() | _inventoryUpgradeMask) & SuitUpgradeResolver.SupportedMask;
            data.suitUpgradeMask = serializedMask;
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

            if (data == null)
            {
                RebuildRuntimeStats();
                return;
            }

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

            AddInstalledUpgradeIdsFromMask(data.suitUpgradeMask);

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
