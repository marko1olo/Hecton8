using System;
using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Items;
using Hecton8.Modding;
using Hecton8.SaveSystem;
using Hecton.Localization;
using UnityEngine;

namespace Hecton8.PDA
{
    /// <summary>
    /// Immutable PDA journal entry snapshot used by UI and debug consumers.
    /// </summary>
    public readonly struct PDALogbookEntry
    {
        public PDALogbookEntry(int sequence, int dayIndex, float dayTimeHours, float playTimeSeconds, int titleHash, int messageHash, int originHash)
        {
            Sequence = sequence;
            DayIndex = dayIndex;
            DayTimeHours = dayTimeHours;
            PlayTimeSeconds = playTimeSeconds;
            TitleHash = titleHash;
            MessageHash = messageHash;
            OriginHash = originHash;
        }

        /// <summary>Monotonic insertion order for stable sorting.</summary>
        public int Sequence { get; }

        /// <summary>Day number captured at the moment of the journal event.</summary>
        public int DayIndex { get; }

        /// <summary>Hour-of-day stamp captured at the moment of the journal event.</summary>
        public float DayTimeHours { get; }

        /// <summary>Total playtime in seconds when the event was recorded.</summary>
        public float PlayTimeSeconds { get; }

        /// <summary>Short journal headline localization hash.</summary>
        public int TitleHash { get; }

        /// <summary>Long-form journal summary localization hash.</summary>
        public int MessageHash { get; }

        /// <summary>Deduplication event hash owned by the source event.</summary>
        public int OriginHash { get; }

        /// <summary>Cold-path string reconstruction for legacy debug consumers.</summary>
        public string Title => LocRegistry.ResolveRaw(TitleHash).ToString();

        /// <summary>Cold-path string reconstruction for legacy debug consumers.</summary>
        public string Message => LocRegistry.ResolveRaw(MessageHash).ToString();

        /// <summary>Cold-path source identifier reconstruction is unavailable after hash compaction.</summary>
        public string OriginKey => OriginHash != 0 ? OriginHash.ToString("X8") : string.Empty;
    }

    /// <summary>
    /// Event-driven auto-journal for major player milestones, discoveries, and field incidents.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/PDA/PDA Logbook Manager")]
    public sealed class PDALogbookManager : MonoBehaviour, ISaveable, IPDALogbookService
    {
        private const string FirstLaserCutterPersistentId = "Item_Tool_LaserCutter";
        private const string FirstDeathOriginKey = "pda.log.death.first";
        private const string FirstLaserCutterOriginKey = "pda.log.craft.first_laser_cutter";
        private const string FirstLeviathanScanOriginKey = "pda.log.scan.first_leviathan";

        // COLD ALLOC: List<PDALogbookEntry>[64] - runtime journal history - owner: PDALogbookManager
        private readonly List<PDALogbookEntry> _entries = new List<PDALogbookEntry>(64);
        // COLD ALLOC: HashSet<int>[512] - journal dedupe source hashes - owner: PDALogbookManager
        private readonly HashSet<int> _seenOriginHashes = new HashSet<int>(512);

        private HectonEventSubscription _itemCraftedSubscription;
        private HectonEventSubscription _gameLoadedSubscription;
        private HectonEventSubscription _playerSpawnedSubscription;

        private HectonSurvivalSystem _survivalSystem;
        private ScanLogSystem _scanLogSystem;
        private HectonDiscoveryManager _discoveryManager;
        private bool _registeredToSave;
        private int _nextSequence = 1;

        /// <summary>Total number of retained journal entries.</summary>
        public int EntryCount => _entries.Count;

        /// <inheritdoc />
        public int SavePriority => 205;

        /// <inheritdoc />
        public int LoadPriority => 205;

        private void Awake()
        {
        }

        private void OnEnable()
        {
            TryRegisterLogbookService();
            TryRegisterWithSaveManager();
            SubscribeToEventBus();
            RebindOwnerSubscriptions();
        }

        private void Start()
        {
            TryRegisterWithSaveManager();
            RebindOwnerSubscriptions();
        }

        private void OnDisable()
        {
            UnsubscribeFromOwners();
            UnsubscribeFromEventBus();
            UnregisterFromSaveManager();
            UnregisterLogbookService();
        }

        private void OnDestroy()
        {
            UnsubscribeFromOwners();
            UnsubscribeFromEventBus();
            UnregisterFromSaveManager();
            UnregisterLogbookService();
        }

        /// <summary>
        /// Copies retained journal entries into a caller-owned buffer from newest to oldest.
        /// </summary>
        public int CopyEntries(PDALogbookEntry[] buffer)
        {
            if (buffer == null || buffer.Length == 0 || _entries.Count == 0)
                return 0;

            int count = Mathf.Min(buffer.Length, _entries.Count);
            for (int i = 0; i < count; i++)
                buffer[i] = _entries[_entries.Count - 1 - i];

            return count;
        }

        /// <summary>
        /// Returns the newest retained journal entry.
        /// </summary>
        public bool TryGetLatestEntry(out PDALogbookEntry entry)
        {
            if (_entries.Count <= 0)
            {
                entry = default;
                return false;
            }

            entry = _entries[_entries.Count - 1];
            return true;
        }

        /// <summary>
        /// Adds a deduplicated entry to the PDA journal.
        /// </summary>
        public bool TryAppendEntry(string originKey, string title, string message)
        {
            if (string.IsNullOrWhiteSpace(originKey) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
                return false;

            int originHash = LocHash.Compute(originKey);
            int titleHash = LocHash.Compute(title);
            int messageHash = LocHash.Compute(message);
            if (originHash == 0 || titleHash == 0 || messageHash == 0)
                return false;

            if (!_seenOriginHashes.Add(originHash))
                return false;

            PDAClockUtility.CaptureStamp(out int dayIndex, out float dayTimeHours, out float playTimeSeconds);
            PDALogbookEntry entry = new PDALogbookEntry(
                _nextSequence++,
                Mathf.Max(1, dayIndex),
                Mathf.Clamp(dayTimeHours, 0f, 24f),
                Mathf.Max(0f, playTimeSeconds),
                titleHash,
                messageHash,
                originHash);

            if (_entries.Count >= PDALogbookDTO.MaxEntries)
                _entries.RemoveAt(0);

            _entries.Add(entry);
            UIStateStore.AppendPDALogEventHash(unchecked((uint)originHash), playTimeSeconds);
            Hecton8.UI.PDAEvents.RaiseLogbookChanged(_entries.Count, unchecked((uint)originHash));
            return true;
        }

        /// <inheritdoc />
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.pdaLogbook.EnsureCapacity();
            data.pdaLogbook.entryCount = Mathf.Min(_entries.Count, PDALogbookDTO.MaxEntries);
            data.pdaLogbook.nextSequence = Mathf.Max(1, _nextSequence);

            int firstEntryIndex = Mathf.Max(0, _entries.Count - data.pdaLogbook.entryCount);
            for (int i = 0; i < data.pdaLogbook.entryCount; i++)
            {
                PDALogbookEntry entry = _entries[firstEntryIndex + i];
                data.pdaLogbook.entries[i] = new PDALogbookEntryDTO
                {
                    sequence = entry.Sequence,
                    dayIndex = entry.DayIndex,
                    dayTimeHours = entry.DayTimeHours,
                    playTimeSeconds = entry.PlayTimeSeconds,
                    titleHash = entry.TitleHash,
                    messageHash = entry.MessageHash,
                    originHash = entry.OriginHash,
                    title = string.Empty,
                    message = string.Empty,
                    originKey = string.Empty
                };
            }

            for (int i = data.pdaLogbook.entryCount; i < PDALogbookDTO.MaxEntries; i++)
                data.pdaLogbook.entries[i] = default;

            int seenOriginCount = 0;
            HashSet<int>.Enumerator enumerator = _seenOriginHashes.GetEnumerator();
            while (enumerator.MoveNext() && seenOriginCount < PDALogbookDTO.MaxSeenOrigins)
            {
                data.pdaLogbook.seenOriginHashes[seenOriginCount] = enumerator.Current;
                seenOriginCount++;
            }

            data.pdaLogbook.seenOriginCount = seenOriginCount;
            for (int i = seenOriginCount; i < PDALogbookDTO.MaxSeenOrigins; i++)
                data.pdaLogbook.seenOriginHashes[i] = 0;

            for (int i = 0; i < PDALogbookDTO.MaxSeenOrigins; i++)
                data.pdaLogbook.seenOriginKeys[i] = string.Empty;
        }

        /// <inheritdoc />
        public void LoadFromSaveData(SaveData data)
        {
            _entries.Clear();
            _seenOriginHashes.Clear();
            _nextSequence = 1;

            if (data == null)
                return;

            PDALogbookDTO dto = data.pdaLogbook;
            int entryCount = Mathf.Clamp(dto.entryCount, 0, dto.entries != null ? dto.entries.Length : 0);
            for (int i = 0; i < entryCount; i++)
            {
                PDALogbookEntryDTO entry = dto.entries[i];
                int titleHash = entry.titleHash != 0 ? entry.titleHash : LocHash.Compute(entry.title);
                int messageHash = entry.messageHash != 0 ? entry.messageHash : LocHash.Compute(entry.message);
                int originHash = entry.originHash != 0 ? entry.originHash : LocHash.Compute(entry.originKey);
                _entries.Add(new PDALogbookEntry(
                    entry.sequence,
                    Mathf.Max(1, entry.dayIndex),
                    Mathf.Clamp(entry.dayTimeHours, 0f, 24f),
                    Mathf.Max(0f, entry.playTimeSeconds),
                    titleHash,
                    messageHash,
                    originHash));

                if (originHash != 0)
                {
                    _seenOriginHashes.Add(originHash);
                    UIStateStore.AppendPDALogEventHash(unchecked((uint)originHash), entry.playTimeSeconds);
                }
            }

            int hashSeenCapacity = dto.seenOriginHashes != null ? dto.seenOriginHashes.Length : 0;
            int stringSeenCapacity = dto.seenOriginKeys != null ? dto.seenOriginKeys.Length : 0;
            int seenOriginCount = Mathf.Clamp(dto.seenOriginCount, 0, Mathf.Max(hashSeenCapacity, stringSeenCapacity));
            for (int i = 0; i < seenOriginCount; i++)
            {
                int originHash = i < hashSeenCapacity ? dto.seenOriginHashes[i] : 0;
                if (originHash == 0 && i < stringSeenCapacity)
                    originHash = LocHash.Compute(dto.seenOriginKeys[i]);

                if (originHash != 0)
                    _seenOriginHashes.Add(originHash);
            }

            _nextSequence = Mathf.Max(1, dto.nextSequence);
            Hecton8.UI.PDAEvents.RaiseLogbookChanged(_entries.Count, _entries.Count > 0 ? unchecked((uint)_entries[_entries.Count - 1].OriginHash) : 0u);
            RebindOwnerSubscriptions();
        }

        private void TryRegisterLogbookService()
        {
            IPDALogbookService registered = GlobalRegistry.PDALogbook;
            if (registered != null && !ReferenceEquals(registered, this))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[PDALogbookManager] Duplicate logbook service detected. Disabling duplicate.");
#endif
                enabled = false;
                return;
            }

            if (!ReferenceEquals(registered, this))
                GlobalRegistry.RegisterPDALogbookService(this);
        }

        private void UnregisterLogbookService()
        {
            if (ReferenceEquals(GlobalRegistry.PDALogbook, this))
                GlobalRegistry.UnregisterPDALogbookService(this);
        }

        private void SubscribeToEventBus()
        {
            if (_itemCraftedSubscription == null)
                _itemCraftedSubscription = HectonEventBus.Subscribe<ItemCraftedEvent>(HandleItemCrafted, "pda.logbook");

            if (_gameLoadedSubscription == null)
                _gameLoadedSubscription = HectonEventBus.Subscribe<GameLoadedEvent>(HandleGameLoaded, "pda.logbook");

            if (_playerSpawnedSubscription == null)
                _playerSpawnedSubscription = HectonEventBus.Subscribe<PlayerSpawnedEvent>(HandlePlayerSpawned, "pda.logbook");
        }

        private void UnsubscribeFromEventBus()
        {
            _itemCraftedSubscription?.Dispose();
            _itemCraftedSubscription = null;

            _gameLoadedSubscription?.Dispose();
            _gameLoadedSubscription = null;

            _playerSpawnedSubscription?.Dispose();
            _playerSpawnedSubscription = null;
        }

        private void RebindOwnerSubscriptions()
        {
            HectonSurvivalSystem resolvedSurvival = ResolveSurvivalSystem();
            if (!ReferenceEquals(_survivalSystem, resolvedSurvival))
            {
                if (_survivalSystem != null)
                    _survivalSystem.OnDeath -= HandlePlayerDeath;

                _survivalSystem = resolvedSurvival;
                if (_survivalSystem != null)
                    _survivalSystem.OnDeath += HandlePlayerDeath;
            }

            ScanLogSystem resolvedScanLog = ScanLogSystem.Instance;
            if (!ReferenceEquals(_scanLogSystem, resolvedScanLog))
            {
                if (_scanLogSystem != null)
                    _scanLogSystem.EntryUnlocked -= HandleEntryUnlocked;

                _scanLogSystem = resolvedScanLog;
                if (_scanLogSystem != null)
                    _scanLogSystem.EntryUnlocked += HandleEntryUnlocked;
            }

            HectonDiscoveryManager resolvedDiscoveryManager = HectonDiscoveryManager.Instance;
            if (!ReferenceEquals(_discoveryManager, resolvedDiscoveryManager))
            {
                if (_discoveryManager != null)
                    _discoveryManager.OnBiomeDiscovered -= HandleBiomeDiscovered;

                _discoveryManager = resolvedDiscoveryManager;
                if (_discoveryManager != null)
                    _discoveryManager.OnBiomeDiscovered += HandleBiomeDiscovered;
            }
        }

        private void UnsubscribeFromOwners()
        {
            if (_survivalSystem != null)
                _survivalSystem.OnDeath -= HandlePlayerDeath;
            if (_scanLogSystem != null)
                _scanLogSystem.EntryUnlocked -= HandleEntryUnlocked;
            if (_discoveryManager != null)
                _discoveryManager.OnBiomeDiscovered -= HandleBiomeDiscovered;

            _survivalSystem = null;
            _scanLogSystem = null;
            _discoveryManager = null;
        }

        private void HandleItemCrafted(ItemCraftedEvent craftedEvent)
        {
            ItemData item = craftedEvent != null ? craftedEvent.Item : null;
            if (item == null || !item.MatchesPersistentId(FirstLaserCutterPersistentId))
                return;

            TryAppendEntry(
                FirstLaserCutterOriginKey,
                "Daybook // First Cutter Fabricated",
                "Synthesized the first laser cutter. Heavy access routes and sealed salvage are now viable.");
        }

        private void HandleGameLoaded(GameLoadedEvent gameLoadedEvent)
        {
            RebindOwnerSubscriptions();
        }

        private void HandlePlayerSpawned(PlayerSpawnedEvent playerSpawnedEvent)
        {
            RebindOwnerSubscriptions();
        }

        private void HandlePlayerDeath()
        {
            SurvivalDeathCause cause = _survivalSystem != null ? _survivalSystem.LastDeathCause : SurvivalDeathCause.None;
            TryAppendEntry(
                FirstDeathOriginKey,
                "Daybook // First Fatality Recorded",
                BuildDeathMessage(cause));
        }

        private void HandleBiomeDiscovered(int biomeId)
        {
            HectonDiscoveryManager discoveryManager = _discoveryManager;
            string biomeLabel = discoveryManager != null ? discoveryManager.GetBiomeName(biomeId) : $"BIOME {biomeId}";
            TryAppendEntry(
                $"pda.log.biome.{biomeId}",
                "Daybook // New Biome Charted",
                $"Entered {biomeLabel}. PDA cartography now marks this biome as confirmed terrain.");
        }

        private void HandleEntryUnlocked(ScanLogSystem.ScanEntrySnapshot snapshot)
        {
            if (!LooksLikeLeviathanEntry(snapshot))
                return;

            TryAppendEntry(
                FirstLeviathanScanOriginKey,
                "Daybook // Leviathan Scan Archived",
                $"Archived first leviathan-class scan record: {snapshot.Title}. Threat doctrine needs revision.");
        }

        private static bool LooksLikeLeviathanEntry(ScanLogSystem.ScanEntrySnapshot snapshot)
        {
            return ContainsLeviathanToken(snapshot.Id) ||
                   ContainsLeviathanToken(snapshot.Title) ||
                   ContainsLeviathanToken(snapshot.Category) ||
                   ContainsLeviathanToken(snapshot.Summary);
        }

        private static bool ContainsLeviathanToken(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf("leviathan", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildDeathMessage(SurvivalDeathCause cause)
        {
            switch (cause)
            {
                case SurvivalDeathCause.OxygenDepletion:
                    return "Recorded first fatality: oxygen depletion. Reserve planning and ascent discipline were insufficient.";
                case SurvivalDeathCause.PressureCollapse:
                    return "Recorded first fatality: pressure collapse. Hull tolerance and route depth limits were exceeded.";
                case SurvivalDeathCause.ThermalFailure:
                    return "Recorded first fatality: thermal failure. Heat mitigation was not sufficient for the route.";
                case SurvivalDeathCause.RadiationExposure:
                    return "Recorded first fatality: radiation exposure. Shielding discipline failed under sustained contamination.";
                case SurvivalDeathCause.Starvation:
                    return "Recorded first fatality: starvation. Resource planning failed before the expedition ended.";
                case SurvivalDeathCause.Dehydration:
                    return "Recorded first fatality: dehydration. Water discipline collapsed before return-to-base.";
                case SurvivalDeathCause.IntegrityFailure:
                    return "Recorded first fatality: suit integrity failure. Structural damage outpaced field recovery.";
                default:
                    return "Recorded first fatality. Cause unresolved in telemetry.";
            }
        }

        private HectonSurvivalSystem ResolveSurvivalSystem()
        {
            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null &&
                playerTransform.TryGetComponent(out HectonSurvivalSystem survivalSystem))
            {
                return survivalSystem;
            }

            return null;
        }

        private void TryRegisterWithSaveManager()
        {
            if (_registeredToSave)
                return;

            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            if (saveManager == null)
                return;

            saveManager.Register(this);
            _registeredToSave = true;
        }

        private void UnregisterFromSaveManager()
        {
            if (!_registeredToSave)
                return;

            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            if (saveManager != null)
                saveManager.Unregister(this);

            _registeredToSave = false;
        }
    }
}
