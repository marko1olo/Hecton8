// ============================================================================
// HECTON-8 - HectonDiscoveryManager.cs
// Otslezhivaet otkrytye biomy i sohranyaet poslednee korrektno podtverzhdennoe
// otkrytie dlya PDA i drugih sistem progressii.
//
// VERSIYa: production pass s vosstanovleniem latest biome i keshirovaniem HUD
// ============================================================================

using System;
using System.Collections.Generic;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton8.AI;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Narrative;
using Hecton8.SaveSystem;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Tsentralizovannyy reestr otkrytyh igrokom biomov.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Hecton Discovery Manager")]
    public sealed class HectonDiscoveryManager : MonoBehaviour, ISaveable, IScanEventListener
    {
        private const int MinBiomeId = BiomeDiscoveryBitMask.MinBiomeId;
        private const int MaxBiomeId = BiomeDiscoveryBitMask.MaxBiomeId;
        private const int InvalidBiomeId = BiomeDiscoveryBitMask.InvalidBiomeId;
        private const byte FaunaBestiaryBehaviorThreshold = 1;
        private const byte FaunaBestiaryDietThreshold = 5;
        private const byte FaunaBestiaryVulnerabilityThreshold = 10;
        private const int DiscoveredBiomeCapacity = MaxBiomeId - MinBiomeId + 1;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR - REFERENCES
        // ══════════════════════════════════════════════════════════

        [Header("── References ──────────────────────────────")]
        [Tooltip("Reestr vseh 108 biomov dlya imenovaniya i PDA-predstavleniya.")]
        [SerializeField] private HectonBiomeRegistry _registry;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        // COLD ALLOC: HashSet<int>[DiscoveredBiomeCapacity] - discovered biome ids keyed by biome registry id - owner: HectonDiscoveryManager
        private readonly HashSet<int> _discoveredBiomeIds = new HashSet<int>(DiscoveredBiomeCapacity);
        // COLD ALLOC: Dictionary<uint,byte>[64] — runtime fauna bestiary observation counters keyed by scan entry hash — owner: HectonDiscoveryManager
        private readonly Dictionary<uint, byte> _faunaInteractionCounts = new Dictionary<uint, byte>(64);
        private bool _registeredWithSaveManager;
        private bool _registeredWithScanEvents;
        private bool _serviceRegistered;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Posledniy korrektno podtverzhdennyy ID otkrytogo bioma.
        /// </summary>
        public int LastDiscoveredId { get; private set; } = InvalidBiomeId;

        /// <summary>
        /// Kolichestvo otkrytyh biomov.
        /// </summary>
        public int TotalDiscovered => _discoveredBiomeIds.Count;

        /// <inheritdoc />
        public int SavePriority => 20;

        /// <inheritdoc />
        public int LoadPriority => 20;

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Vyzyvaetsya odin raz pri pervom otkrytii novogo bioma.
        /// </summary>
        public event Action<int> OnBiomeDiscovered;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            TryRegisterService();
            TryRegisterWithSaveManager();
            TryRegisterWithScanEvents();
        }

        private void Start()
        {
            TryRegisterWithSaveManager();
            TryRegisterWithScanEvents();
        }

        private void OnDisable()
        {
            UnregisterFromSaveManager();
            UnregisterFromScanEvents();
            TryUnregisterService();

        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Pomechaet biom kak otkrytyy, esli igrok zashel v nego vpervye.
        /// </summary>
        /// <param name="biomeId">Identifikator bioma iz matritsy 1..108.</param>
        public void DiscoverBiome(int biomeId)
        {
            if (!IsValidBiomeId(biomeId))
                return;

            if (!_discoveredBiomeIds.Add(biomeId))
                return;

            LastDiscoveredId = biomeId;

            string biomeName = GetBiomeName(biomeId);
            LogBiomeDiscovered(biomeName, biomeId, this);

            OnBiomeDiscovered?.Invoke(biomeId);
            NotificationEvents.PushInfo(string.Format(
                ResolveLocalized(LocalizationKeys.DISCOVERY_NEW_BIOME, "NEW BIOME DISCOVERED: {0}"),
                biomeName));
        }

        /// <summary>
        /// Proveryaet, otkryt li ukazannyy biom.
        /// </summary>
        public bool IsDiscovered(int biomeId)
        {
            return _discoveredBiomeIds.Contains(biomeId);
        }

        /// <summary>
        /// Vozvraschaet otobrazhaemoe imya bioma.
        /// </summary>
        public string GetBiomeName(int id)
        {
            if (!IsValidBiomeId(id))
                return "NO RECENT BIOME";

            if (_registry != null)
            {
                HectonBiomeRegistry.BiomeEntry entry = _registry.GetBiome(id);
                if (!string.IsNullOrEmpty(entry.name))
                    return entry.name.ToUpperInvariant();
            }

            return $"BIOME {id}";
        }

        /// <summary>
        /// Vozvraschaet dannye bioma iz reestra.
        /// </summary>
        public HectonBiomeRegistry.BiomeEntry GetBiomeData(int id)
        {
            if (_registry == null || !IsValidBiomeId(id))
                return default;

            return _registry.GetBiome(id);
        }

        public void OnScanEvent(in ScanEventPayload payload)
        {
            ScanEventType eventType = (ScanEventType)payload.EventType;
            if (payload.EntryHash == 0u ||
                (eventType != ScanEventType.FaunaFeedingObserved &&
                 eventType != ScanEventType.FaunaMatingObserved))
            {
                return;
            }

            TryRecordFaunaObservation(payload.EntryHash);
        }

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        /// <inheritdoc />
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            BiomeDiscoveryBitMask.EnsureCapacity(ref data.discoveredBiomeBitWords);
            BiomeDiscoveryBitMask.Pack(_discoveredBiomeIds, data.discoveredBiomeBitWords);
            data.discoveredBiomeIds = null;
            data.lastDiscoveredBiomeId = IsValidBiomeId(LastDiscoveredId) &&
                                         _discoveredBiomeIds.Contains(LastDiscoveredId)
                ? LastDiscoveredId
                : ResolveFallbackLastDiscoveredId();
        }

        /// <inheritdoc />
        public void LoadFromSaveData(SaveData data)
        {
            _discoveredBiomeIds.Clear();
            LastDiscoveredId = InvalidBiomeId;

            if (data == null)
                return;

            if (BiomeDiscoveryBitMask.HasAnySet(data.discoveredBiomeBitWords))
            {
                BiomeDiscoveryBitMask.Unpack(data.discoveredBiomeBitWords, _discoveredBiomeIds);
            }
            else if (data.discoveredBiomeIds != null)
            {
                foreach (int biomeId in data.discoveredBiomeIds)
                {
                    if (IsValidBiomeId(biomeId))
                        _discoveredBiomeIds.Add(biomeId);
                }
            }

            if (IsValidBiomeId(data.lastDiscoveredBiomeId) &&
                _discoveredBiomeIds.Contains(data.lastDiscoveredBiomeId))
            {
                LastDiscoveredId = data.lastDiscoveredBiomeId;
                return;
            }

            LastDiscoveredId = ResolveFallbackLastDiscoveredId();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE METHODS
        // ══════════════════════════════════════════════════════════

        private static bool IsValidBiomeId(int biomeId)
        {
            return biomeId >= MinBiomeId && biomeId <= MaxBiomeId;
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }

        private int ResolveFallbackLastDiscoveredId()
        {
            for (int biomeId = MinBiomeId; biomeId <= MaxBiomeId; biomeId++)
            {
                if (_discoveredBiomeIds.Contains(biomeId))
                    return biomeId;
            }

            return InvalidBiomeId;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogBiomeDiscovered(string biomeName, int biomeId, UnityEngine.Object context)
        {
            UnityEngine.Debug.Log($"[Discovery] New biome discovered: {biomeName} (ID {biomeId}).", context);
        }


        private void TryRegisterWithSaveManager()
        {
            if (_registeredWithSaveManager)
                return;

            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            if (saveManager == null)
                return;

            saveManager.Register(this);
            _registeredWithSaveManager = true;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterDiscoveryRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Discovery, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterDiscoveryRuntime(this);
            _serviceRegistered = false;
        }

        private void TryRegisterWithScanEvents()
        {
            if (_registeredWithScanEvents)
                return;

            ScanEvents.Register(this);
            _registeredWithScanEvents = true;
        }

        private void UnregisterFromSaveManager()
        {
            if (!_registeredWithSaveManager)
                return;

            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            if (saveManager != null)
                saveManager.Unregister(this);

            _registeredWithSaveManager = false;
        }

        private void UnregisterFromScanEvents()
        {
            if (!_registeredWithScanEvents)
                return;

            ScanEvents.Unregister(this);
            _registeredWithScanEvents = false;
        }

        private void TryRecordFaunaObservation(uint entryHash)
        {
            if (!FaunaScanRuntimeRegistry.TryGetScanMetadata(entryHash, out FaunaScanRuntimeRegistry.FaunaScanMetadata metadata))
                return;

            byte previousCount = ResolveFaunaObservationCountFloor(entryHash, in metadata);
            byte nextCount = previousCount < byte.MaxValue
                ? (byte)(previousCount + 1)
                : byte.MaxValue;
            _faunaInteractionCounts[entryHash] = nextCount;
            TryUnlockFaunaBestiaryMilestone(in metadata, previousCount, nextCount, FaunaBestiaryBehaviorThreshold, 0);
            TryUnlockFaunaBestiaryMilestone(in metadata, previousCount, nextCount, FaunaBestiaryDietThreshold, 1);
            TryUnlockFaunaBestiaryMilestone(in metadata, previousCount, nextCount, FaunaBestiaryVulnerabilityThreshold, 2);
        }

        private byte ResolveFaunaObservationCountFloor(uint entryHash, in FaunaScanRuntimeRegistry.FaunaScanMetadata metadata)
        {
            byte currentCount = 0;
            if (_faunaInteractionCounts.TryGetValue(entryHash, out byte storedCount))
                currentCount = storedCount;

            LoreDatabaseManager loreDatabase = Hecton8.Core.GlobalRegistry.LoreDatabase;
            if (loreDatabase == null)
                return currentCount;

            if (TryResolveFaunaBestiaryLoreHash(in metadata, 2, out uint vulnerabilityHash) && loreDatabase.IsUnlocked(vulnerabilityHash))
                return currentCount < FaunaBestiaryVulnerabilityThreshold ? FaunaBestiaryVulnerabilityThreshold : currentCount;

            if (TryResolveFaunaBestiaryLoreHash(in metadata, 1, out uint dietHash) && loreDatabase.IsUnlocked(dietHash))
                return currentCount < FaunaBestiaryDietThreshold ? FaunaBestiaryDietThreshold : currentCount;

            if (TryResolveFaunaBestiaryLoreHash(in metadata, 0, out uint behaviorHash) && loreDatabase.IsUnlocked(behaviorHash))
                return currentCount < FaunaBestiaryBehaviorThreshold ? FaunaBestiaryBehaviorThreshold : currentCount;

            return currentCount;
        }

        private static bool TryResolveFaunaBestiaryLoreHash(in FaunaScanRuntimeRegistry.FaunaScanMetadata metadata, int milestoneIndex, out uint loreHash)
        {
            uint[] authoredLoreHashes = metadata.LoreUnlockHashes;
            if (authoredLoreHashes != null &&
                milestoneIndex >= 0 &&
                milestoneIndex < authoredLoreHashes.Length &&
                authoredLoreHashes[milestoneIndex] != 0u)
            {
                loreHash = authoredLoreHashes[milestoneIndex];
                return true;
            }

            if (milestoneIndex == 0 && metadata.FullLoreHash != 0u)
            {
                loreHash = metadata.FullLoreHash;
                return true;
            }

            loreHash = 0u;
            return false;
        }

        private static void TryUnlockFaunaBestiaryMilestone(
            in FaunaScanRuntimeRegistry.FaunaScanMetadata metadata,
            byte previousCount,
            byte nextCount,
            byte threshold,
            int milestoneIndex)
        {
            if (previousCount >= threshold || nextCount < threshold || !TryResolveFaunaBestiaryLoreHash(in metadata, milestoneIndex, out uint loreHash))
                return;

            LoreDatabaseManager loreDatabase = Hecton8.Core.GlobalRegistry.LoreDatabase;
            if (loreDatabase != null)
            {
                loreDatabase.TryUnlockByHash(loreHash);
                return;
            }

        }
    }
}
