using System;
using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton8.Gameplay;
using Hecton8.Items;
using Hecton8.Modding;
using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.PDA
{
    /// <summary>
    /// Immutable PDA journal entry snapshot used by UI and debug consumers.
    /// </summary>
    public readonly struct PDALogbookEntry
    {
        public PDALogbookEntry(int sequence, int dayIndex, float dayTimeHours, float playTimeSeconds, string title, string message, string originKey)
        {
            Sequence = sequence;
            DayIndex = dayIndex;
            DayTimeHours = dayTimeHours;
            PlayTimeSeconds = playTimeSeconds;
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
            OriginKey = originKey ?? string.Empty;
        }

        /// <summary>Monotonic insertion order for stable sorting.</summary>
        public int Sequence { get; }

        /// <summary>Day number captured at the moment of the journal event.</summary>
        public int DayIndex { get; }

        /// <summary>Hour-of-day stamp captured at the moment of the journal event.</summary>
        public float DayTimeHours { get; }

        /// <summary>Total playtime in seconds when the event was recorded.</summary>
        public float PlayTimeSeconds { get; }

        /// <summary>Short journal headline.</summary>
        public string Title { get; }

        /// <summary>Long-form journal summary.</summary>
        public string Message { get; }

        /// <summary>Deduplication key owned by the source event.</summary>
        public string OriginKey { get; }
    }

    /// <summary>
    /// Event-driven auto-journal for major player milestones, discoveries, and field incidents.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/PDA/PDA Logbook Manager")]
    public sealed class PDALogbookManager : MonoBehaviour, ISaveable
    {
        private const string FirstLaserCutterPersistentId = "Item_Tool_LaserCutter";
        private const string FirstDeathOriginKey = "pda.log.death.first";
        private const string FirstLaserCutterOriginKey = "pda.log.craft.first_laser_cutter";
        private const string FirstLeviathanScanOriginKey = "pda.log.scan.first_leviathan";

        // COLD ALLOC: List<PDALogbookEntry>[64] - runtime journal history - owner: PDALogbookManager
        private readonly List<PDALogbookEntry> _entries = new List<PDALogbookEntry>(64);
        // COLD ALLOC: HashSet<string>[dynamic] - journal dedupe source keys - owner: PDALogbookManager
        private readonly HashSet<string> _seenOriginKeys = new HashSet<string>(StringComparer.Ordinal);

        private HectonEventSubscription _itemCraftedSubscription;
        private HectonEventSubscription _gameLoadedSubscription;
        private HectonEventSubscription _playerSpawnedSubscription;

        private HectonSurvivalSystem _survivalSystem;
        private ScanLogSystem _scanLogSystem;
        private HectonDiscoveryManager _discoveryManager;
        private bool _registeredToSave;
        private int _nextSequence = 1;

        /// <summary>Live singleton instance for PDA journal consumers.</summary>
        public static PDALogbookManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        /// <summary>Raised after the runtime journal state changes.</summary>
        public event Action LogbookChanged;

        /// <summary>Total number of retained journal entries.</summary>
        public int EntryCount => _entries.Count;

        /// <inheritdoc />
        public int SavePriority => 205;

        /// <inheritdoc />
        public int LoadPriority => 205;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
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

            if (Instance == this)
                Instance = null;
        }

        private void OnDestroy()
        {
            UnsubscribeFromOwners();
            UnsubscribeFromEventBus();
            UnregisterFromSaveManager();

            if (Instance == this)
                Instance = null;
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

            originKey = originKey.Trim();
            title = title.Trim();
            message = message.Trim();

            if (!_seenOriginKeys.Add(originKey))
                return false;

            PDAClockUtility.CaptureStamp(out int dayIndex, out float dayTimeHours, out float playTimeSeconds);
            PDALogbookEntry entry = new PDALogbookEntry(
                _nextSequence++,
                Mathf.Max(1, dayIndex),
                Mathf.Clamp(dayTimeHours, 0f, 24f),
                Mathf.Max(0f, playTimeSeconds),
                title,
                message,
                originKey);

            if (_entries.Count >= PDALogbookDTO.MaxEntries)
                _entries.RemoveAt(0);

            _entries.Add(entry);
            LogbookChanged?.Invoke();
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
                    title = entry.Title,
                    message = entry.Message,
                    originKey = entry.OriginKey
                };
            }

            for (int i = data.pdaLogbook.entryCount; i < PDALogbookDTO.MaxEntries; i++)
                data.pdaLogbook.entries[i] = default;

            int seenOriginCount = 0;
            HashSet<string>.Enumerator enumerator = _seenOriginKeys.GetEnumerator();
            while (enumerator.MoveNext() && seenOriginCount < PDALogbookDTO.MaxSeenOrigins)
            {
                data.pdaLogbook.seenOriginKeys[seenOriginCount] = enumerator.Current;
                seenOriginCount++;
            }

            data.pdaLogbook.seenOriginCount = seenOriginCount;
            for (int i = seenOriginCount; i < PDALogbookDTO.MaxSeenOrigins; i++)
                data.pdaLogbook.seenOriginKeys[i] = string.Empty;
        }

        /// <inheritdoc />
        public void LoadFromSaveData(SaveData data)
        {
            _entries.Clear();
            _seenOriginKeys.Clear();
            _nextSequence = 1;

            if (data == null)
                return;

            PDALogbookDTO dto = data.pdaLogbook;
            int entryCount = Mathf.Clamp(dto.entryCount, 0, dto.entries != null ? dto.entries.Length : 0);
            for (int i = 0; i < entryCount; i++)
            {
                PDALogbookEntryDTO entry = dto.entries[i];
                _entries.Add(new PDALogbookEntry(
                    entry.sequence,
                    Mathf.Max(1, entry.dayIndex),
                    Mathf.Clamp(entry.dayTimeHours, 0f, 24f),
                    Mathf.Max(0f, entry.playTimeSeconds),
                    entry.title,
                    entry.message,
                    entry.originKey));

                if (!string.IsNullOrWhiteSpace(entry.originKey))
                    _seenOriginKeys.Add(entry.originKey);
            }

            int seenOriginCount = Mathf.Clamp(dto.seenOriginCount, 0, dto.seenOriginKeys != null ? dto.seenOriginKeys.Length : 0);
            for (int i = 0; i < seenOriginCount; i++)
            {
                string originKey = dto.seenOriginKeys[i];
                if (!string.IsNullOrWhiteSpace(originKey))
                    _seenOriginKeys.Add(originKey);
            }

            _nextSequence = Mathf.Max(1, dto.nextSequence);
            LogbookChanged?.Invoke();
            RebindOwnerSubscriptions();
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
