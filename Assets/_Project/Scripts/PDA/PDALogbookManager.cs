using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.SaveSystem;
using Hecton.Localization;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.PDA
{
    /// <summary>
    /// Immutable PDA journal entry snapshot used by UI and debug consumers.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
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
            _pad0 = 0u;
        }

        /// <summary>Monotonic insertion order for stable sorting.</summary>
        [FieldOffset(0)] public readonly int Sequence;

        /// <summary>Day number captured at the moment of the journal event.</summary>
        [FieldOffset(4)] public readonly int DayIndex;

        /// <summary>Hour-of-day stamp captured at the moment of the journal event.</summary>
        [FieldOffset(8)] public readonly float DayTimeHours;

        /// <summary>Total playtime in seconds when the event was recorded.</summary>
        [FieldOffset(12)] public readonly float PlayTimeSeconds;

        /// <summary>Short journal headline localization hash.</summary>
        [FieldOffset(16)] public readonly int TitleHash;

        /// <summary>Long-form journal summary localization hash.</summary>
        [FieldOffset(20)] public readonly int MessageHash;

        /// <summary>Deduplication event hash owned by the source event.</summary>
        [FieldOffset(24)] public readonly int OriginHash;

        [FieldOffset(28)] private readonly uint _pad0;

        /// <summary>Legacy string surface disabled; use GetTitleSpan/TryGetTitleBuffer.</summary>
        public string Title
        {
            get
            {
                return string.Empty;
            }
        }

        /// <summary>Legacy string surface disabled; use GetMessageSpan/TryGetMessageBuffer.</summary>
        public string Message
        {
            get
            {
                return string.Empty;
            }
        }

        /// <summary>Legacy string surface disabled; use TryWriteOriginKey.</summary>
        public string OriginKey
        {
            get
            {
                return string.Empty;
            }
        }

        /// <summary>Resolve the localized title buffer for zero-GC TMP rendering.</summary>
        public bool TryGetTitleBuffer(out char[] buffer, out int length)
        {
            return LocRegistry.TryGetRawBuffer(TitleHash, out buffer, out length);
        }

        /// <summary>Resolve the localized message buffer for zero-GC TMP rendering.</summary>
        public bool TryGetMessageBuffer(out char[] buffer, out int length)
        {
            return LocRegistry.TryGetRawBuffer(MessageHash, out buffer, out length);
        }

        /// <summary>Resolve the localized title as a span without heap allocation.</summary>
        public ReadOnlySpan<char> GetTitleSpan()
        {
            return LocRegistry.ResolveRaw(TitleHash);
        }

        /// <summary>Resolve the localized message as a span without heap allocation.</summary>
        public ReadOnlySpan<char> GetMessageSpan()
        {
            return LocRegistry.ResolveRaw(MessageHash);
        }

        /// <summary>Write the origin hash as eight uppercase hex chars into a caller-owned buffer.</summary>
        public bool TryWriteOriginKey(Span<char> buffer, out int length)
        {
            if (OriginHash == 0 || buffer.Length < 8)
            {
                length = 0;
                return false;
            }

            uint value = unchecked((uint)OriginHash);
            for (int i = 7; i >= 0; i--)
            {
                int nibble = (int)(value & 0xFu);
                buffer[i] = (char)(nibble < 10 ? '0' + nibble : 'A' + (nibble - 10));
                value >>= 4;
            }

            length = 8;
            return true;
        }
    }

    /// <summary>
    /// Event-driven auto-journal for major player milestones, discoveries, and field incidents.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/PDA/PDA Logbook Manager")]
    public sealed class PDALogbookManager : MonoBehaviour, ISaveable, IPDALogbookService, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int FirstDeathOriginHash = unchecked((int)0xED21B4CC);
        private const int FirstLaserCutterOriginHash = unchecked((int)0x0710CD7A);
        private const int FirstLeviathanScanOriginHash = unchecked((int)0x8F046E1C);
        private const int FirstLaserCutterPersistentHash = unchecked((int)0x18070808);
        private const int BiomeLogOriginSaltHash = unchecked((int)0x46BF9270);
        private const int FirstLaserCutterTitleHash = unchecked((int)0x9AE083CA);
        private const int FirstLaserCutterMessageHash = unchecked((int)0x17333DB8);
        private const int FirstDeathTitleHash = unchecked((int)0x83578D04);
        private const int BiomeDiscoveredTitleHash = unchecked((int)0x1529CF4D);
        private const int BiomeDiscoveredMessageHash = unchecked((int)0xCCD5385A);
        private const int FirstLeviathanScanTitleHash = unchecked((int)0x5F18881D);
        private const int FirstLeviathanScanMessageHash = unchecked((int)0x8B4EF804);
        private const int DeathOxygenMessageHash = unchecked((int)0xD6EDC09F);
        private const int DeathPressureMessageHash = unchecked((int)0x1575F50E);
        private const int DeathThermalMessageHash = unchecked((int)0x7B97E92A);
        private const int DeathRadiationMessageHash = unchecked((int)0x09EC9793);
        private const int DeathStarvationMessageHash = unchecked((int)0xB4E90ED7);
        private const int DeathDehydrationMessageHash = unchecked((int)0x4DA2FC7D);
        private const int DeathIntegrityMessageHash = unchecked((int)0xC20F21A5);
        private const int DeathUnknownMessageHash = unchecked((int)0xDAC921D3);
        private const uint LeviathanCategoryHash = 0xDD349361u;
        private const uint BlackChoirLeviathanEntryHash = 0xC8D5E2B5u;
        private const uint FurnaceMawLeviathanEntryHash = 0xDDB1978Eu;
        private const uint GateWardenLeviathanEntryHash = 0x719F2909u;
        private const uint HaloCrownLeviathanEntryHash = 0x35ED9DB0u;
        private const uint RiftLancerLeviathanEntryHash = 0x5E944D3Bu;
        private const uint VoidRibbonLeviathanEntryHash = 0x83B27BCBu;

        // COLD ALLOC: PDALogbookEntry[MaxEntries] - fixed journal ring without List reallocs - owner: PDALogbookManager
        private readonly PDALogbookEntry[] _entries = new PDALogbookEntry[PDALogbookDTO.MaxEntries];
        // COLD ALLOC: int[MaxSeenOrigins] - fixed dedupe source hashes without HashSet enumerators - owner: PDALogbookManager
        private readonly int[] _seenOriginHashes = new int[PDALogbookDTO.MaxSeenOrigins];

        private HectonSurvivalSystem _survivalSystem;
        private IScanLogService _scanLogSystem;
        private bool _registeredToSave;
        private bool _registered;
        private bool _registeredHotSwapListener;
        private uint _scanLogSourceId;
        private uint _survivalSignalSourceId;
        private int _lastSurvivalDeathSignalSequence;
        private uint _lastProgressionMetaSequence;
        private uint _lastSessionLifecycleSequence;
        private bool _firstLaserCutterLogged;
        private bool _firstLeviathanScanLogged;
        private int _entryCount;
        private int _seenOriginCount;
        private int _nextSequence = 1;
        private ISaveService _saveService;

        /// <summary>Total number of retained journal entries.</summary>
        public int EntryCount => _entryCount;

        /// <inheritdoc />
        public int SavePriority => 205;

        /// <inheritdoc />
        public int LoadPriority => 205;
        private bool NeedsCraftingScanLogPump => !_firstLaserCutterLogged || !_firstLeviathanScanLogged;
        private bool NeedsLogbookSignalPump => Application.isPlaying;

        private void Awake()
        {
        }

        private void OnEnable()
        {
            TryRegisterLogbookService();
            if (!enabled)
                return;

            _saveService = GlobalRegistry.Save;
            TryRegisterHotSwapListener();
            TryRegisterWithSaveManager();
            RebindOwnerSubscriptions();
            RefreshLogbookSignalPumpRegistration();
        }

        private void Start()
        {
            TryRegisterHotSwapListener();
            TryRegisterWithSaveManager();
            RebindOwnerSubscriptions();
            RefreshLogbookSignalPumpRegistration();
        }

        private void OnDisable()
        {
            TryUnregister();
            UnsubscribeFromOwners();
            UnregisterFromSaveManager();
            TryUnregisterHotSwapListener();
            UnregisterLogbookService();
        }

        private void OnDestroy()
        {
            TryUnregister();
            UnsubscribeFromOwners();
            UnregisterFromSaveManager();
            TryUnregisterHotSwapListener();
            UnregisterLogbookService();
        }

        /// <summary>
        /// Copies retained journal entries into a caller-owned buffer from newest to oldest.
        /// </summary>
        public int CopyEntries(PDALogbookEntry[] buffer)
        {
            if (buffer == null || buffer.Length == 0 || _entryCount == 0)
                return 0;

            int count = math.min(buffer.Length, _entryCount);
            for (int i = 0; i < count; i++)
                buffer[i] = _entries[_entryCount - 1 - i];

            return count;
        }

        /// <summary>
        /// Returns the newest retained journal entry.
        /// </summary>
        public bool TryGetLatestEntry(out PDALogbookEntry entry)
        {
            if (_entryCount <= 0)
            {
                entry = default;
                return false;
            }

            entry = _entries[_entryCount - 1];
            return true;
        }

        /// <summary>
        /// Adds a deduplicated entry to the PDA journal by precomputed hashes.
        /// </summary>
        public bool TryAppendEntry(int originHash, int titleHash, int messageHash)
        {
            if (originHash == 0 || titleHash == 0 || messageHash == 0)
                return false;

            if (!TryAppendSeenOriginHash(originHash))
                return false;

            PDAClockUtility.CaptureStamp(out int dayIndex, out float dayTimeHours, out float playTimeSeconds);
            PDALogbookEntry entry = new PDALogbookEntry(
                _nextSequence++,
                math.max(1, dayIndex),
                math.clamp(dayTimeHours, 0f, 24f),
                math.max(0f, playTimeSeconds),
                titleHash,
                messageHash,
                originHash);

            if (_entryCount >= PDALogbookDTO.MaxEntries)
            {
                for (int i = 1; i < _entryCount; i++)
                    _entries[i - 1] = _entries[i];

                _entryCount--;
                _entries[_entryCount] = default;
            }

            _entries[_entryCount++] = entry;
            UIStateStore.AppendPDALogEventHash(unchecked((uint)originHash), playTimeSeconds);
            Hecton8.UI.PDAEvents.TryRaiseLogbookChanged(_entryCount, unchecked((uint)originHash));
            if (originHash == FirstLaserCutterOriginHash || originHash == FirstLeviathanScanOriginHash)
                RefreshLogbookSignalPumpRegistration();
            return true;
        }

        /// <inheritdoc />
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.pdaLogbook.EnsureCapacity();
            data.pdaLogbook.entryCount = math.min(_entryCount, PDALogbookDTO.MaxEntries);
            data.pdaLogbook.nextSequence = math.max(1, _nextSequence);

            int firstEntryIndex = math.max(0, _entryCount - data.pdaLogbook.entryCount);
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

            int seenOriginCount = math.min(_seenOriginCount, PDALogbookDTO.MaxSeenOrigins);
            for (int i = 0; i < seenOriginCount; i++)
                data.pdaLogbook.seenOriginHashes[i] = _seenOriginHashes[i];

            data.pdaLogbook.seenOriginCount = seenOriginCount;
            for (int i = seenOriginCount; i < PDALogbookDTO.MaxSeenOrigins; i++)
                data.pdaLogbook.seenOriginHashes[i] = 0;

            for (int i = 0; i < PDALogbookDTO.MaxSeenOrigins; i++)
                data.pdaLogbook.seenOriginKeys[i] = string.Empty;
        }

        /// <inheritdoc />
        public void LoadFromSaveData(SaveData data)
        {
            ClearEntries();
            ClearSeenOriginHashes();
            _nextSequence = 1;

            if (data == null)
            {
                RebindOwnerSubscriptions();
                RefreshLogbookSignalPumpRegistration();
                return;
            }

            PDALogbookDTO dto = data.pdaLogbook;
            int entryCount = math.clamp(dto.entryCount, 0, dto.entries != null ? dto.entries.Length : 0);
            for (int i = 0; i < entryCount; i++)
            {
                PDALogbookEntryDTO entry = dto.entries[i];
                int titleHash = entry.titleHash != 0 ? entry.titleHash : LocHash.Compute(entry.title);
                int messageHash = entry.messageHash != 0 ? entry.messageHash : LocHash.Compute(entry.message);
                int originHash = entry.originHash != 0 ? entry.originHash : LocHash.Compute(entry.originKey);
                AppendLoadedEntry(new PDALogbookEntry(
                    entry.sequence,
                    math.max(1, entry.dayIndex),
                    math.clamp(entry.dayTimeHours, 0f, 24f),
                    math.max(0f, entry.playTimeSeconds),
                    titleHash,
                    messageHash,
                    originHash));

                if (originHash != 0)
                {
                    TryAppendSeenOriginHash(originHash);
                    UIStateStore.AppendPDALogEventHash(unchecked((uint)originHash), entry.playTimeSeconds);
                }
            }

            int hashSeenCapacity = dto.seenOriginHashes != null ? dto.seenOriginHashes.Length : 0;
            int stringSeenCapacity = dto.seenOriginKeys != null ? dto.seenOriginKeys.Length : 0;
            int seenOriginCount = math.clamp(dto.seenOriginCount, 0, math.max(hashSeenCapacity, stringSeenCapacity));
            for (int i = 0; i < seenOriginCount; i++)
            {
                int originHash = i < hashSeenCapacity ? dto.seenOriginHashes[i] : 0;
                if (originHash == 0 && i < stringSeenCapacity)
                    originHash = LocHash.Compute(dto.seenOriginKeys[i]);

                if (originHash != 0)
                    TryAppendSeenOriginHash(originHash);
            }

            _nextSequence = math.max(1, dto.nextSequence);
            Hecton8.UI.PDAEvents.TryRaiseLogbookChanged(_entryCount, _entryCount > 0 ? unchecked((uint)_entries[_entryCount - 1].OriginHash) : 0u);
            RebindOwnerSubscriptions();
            RefreshLogbookSignalPumpRegistration();
        }

        private bool ContainsSeenOriginHash(int originHash)
        {
            for (int i = 0; i < _seenOriginCount; i++)
            {
                if (_seenOriginHashes[i] == originHash)
                    return true;
            }

            return false;
        }

        private bool TryAppendSeenOriginHash(int originHash)
        {
            if (originHash == 0)
                return false;

            if (ContainsSeenOriginHash(originHash))
            {
                if (originHash == FirstLaserCutterOriginHash)
                    _firstLaserCutterLogged = true;
                if (originHash == FirstLeviathanScanOriginHash)
                    _firstLeviathanScanLogged = true;
                return false;
            }

            if (_seenOriginCount >= PDALogbookDTO.MaxSeenOrigins)
                return false;

            _seenOriginHashes[_seenOriginCount++] = originHash;
            if (originHash == FirstLaserCutterOriginHash)
                _firstLaserCutterLogged = true;
            if (originHash == FirstLeviathanScanOriginHash)
                _firstLeviathanScanLogged = true;
            return true;
        }

        private void AppendLoadedEntry(PDALogbookEntry entry)
        {
            if (_entryCount >= PDALogbookDTO.MaxEntries)
                return;

            _entries[_entryCount++] = entry;
        }

        private void ClearEntries()
        {
            for (int i = 0; i < _entryCount; i++)
                _entries[i] = default;

            _entryCount = 0;
        }

        private void ClearSeenOriginHashes()
        {
            for (int i = 0; i < _seenOriginCount; i++)
                _seenOriginHashes[i] = 0;

            _seenOriginCount = 0;
            _firstLaserCutterLogged = false;
            _firstLeviathanScanLogged = false;
        }

        private void TryRegisterLogbookService()
        {
            IPDALogbookService registered = GlobalRegistry.PDALogbook;
            if (registered != null && !ReferenceEquals(registered, this))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[PDALogbookManager] Duplicate logbook service detected. Disabling duplicate.");
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

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (!NeedsLogbookSignalPump)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registered = false;
        }

        private void RefreshLogbookSignalPumpRegistration()
        {
            if (NeedsLogbookSignalPump)
                TryRegister();
            else
                TryUnregister();
        }

        private void RebindOwnerSubscriptions()
        {
            HectonSurvivalSystem resolvedSurvival = ResolveSurvivalSystem();
            if (!ReferenceEquals(_survivalSystem, resolvedSurvival))
            {
                _survivalSystem = resolvedSurvival;
                RefreshSurvivalSignalBinding();
            }

            IScanLogService resolvedScanLog = Hecton8.Core.GlobalRegistry.ScanLogService;
            if (!ReferenceEquals(_scanLogSystem, resolvedScanLog))
            {
                _scanLogSystem = resolvedScanLog;
                _scanLogSourceId = resolvedScanLog != null ? resolvedScanLog.SourceId : 0u;
            }

        }

        private void UnsubscribeFromOwners()
        {
            _survivalSystem = null;
            _scanLogSystem = null;
            _scanLogSourceId = 0u;
            _survivalSignalSourceId = 0u;
            _lastSurvivalDeathSignalSequence = 0;
        }

        public void LateFrameTick()
        {
            ProcessSessionLifecycleSignals();
            ProcessLogbookSignals();
        }

        private void ProcessSessionLifecycleSignals()
        {
            ReadOnlySpan<SessionLifecycleSignal> signals = SignalBus<SessionLifecycleSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                SessionLifecycleSignal signal = signals[i];
                if (!IsNewerSequence(signal.Sequence, _lastSessionLifecycleSequence))
                    continue;

                _lastSessionLifecycleSequence = signal.Sequence;
                if (signal.Kind == SessionLifecycleSignal.KindGameLoaded)
                    HandleGameLoaded();
                else if (signal.Kind == SessionLifecycleSignal.KindPlayerSpawned)
                    HandlePlayerSpawned();
            }
        }

        private void ProcessLogbookSignals()
        {
            ConsumeSurvivalDeathSignal();
            ProcessProgressionMetaSignals();

            if (NeedsCraftingScanLogPump)
            {
                ProcessCraftingSignals();
                ProcessScanLogSignals();
            }
        }

        private void ProcessCraftingSignals()
        {
            if (_firstLaserCutterLogged)
                return;

            ReadOnlySpan<CraftingCompletedSignal> signals = SignalBus<CraftingCompletedSignal>.GetFrameSnapshot();
            uint requiredItemHash = unchecked((uint)FirstLaserCutterPersistentHash);
            for (int i = 0; i < signals.Length; i++)
            {
                if (signals[i].ResultItemHash != requiredItemHash || signals[i].Quantity == 0)
                    continue;

                TryAppendEntry(FirstLaserCutterOriginHash, FirstLaserCutterTitleHash, FirstLaserCutterMessageHash);
                break;
            }
        }

        private void ProcessScanLogSignals()
        {
            if (_firstLeviathanScanLogged)
                return;

            RefreshScanLogSignalBinding();
            uint sourceId = _scanLogSourceId;
            if (sourceId == 0u)
                return;

            ReadOnlySpan<ScanLogChangedSignal> signals = SignalBus<ScanLogChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ref readonly ScanLogChangedSignal signal = ref signals[i];
                if (signal.SourceId != sourceId ||
                    signal.Reason != ScanLogChangedSignal.ReasonEntryAdded ||
                    !LooksLikeLeviathanEntry(in signal))
                {
                    continue;
                }

                TryAppendEntry(FirstLeviathanScanOriginHash, FirstLeviathanScanTitleHash, FirstLeviathanScanMessageHash);
                break;
            }
        }

        private void RefreshScanLogSignalBinding()
        {
            IScanLogService resolvedScanLog = Hecton8.Core.GlobalRegistry.ScanLogService;
            if (ReferenceEquals(_scanLogSystem, resolvedScanLog))
                return;

            _scanLogSystem = resolvedScanLog;
            _scanLogSourceId = resolvedScanLog != null ? resolvedScanLog.SourceId : 0u;
        }

        private void RefreshSurvivalSignalBinding()
        {
            uint sourceId = ResolveSurvivalSignalSourceId(_survivalSystem);
            if (_survivalSignalSourceId == sourceId)
                return;

            _survivalSignalSourceId = sourceId;
            _lastSurvivalDeathSignalSequence = SurvivalSignalRoute.TryGetLatestDeath(out _, out int sequence)
                ? sequence
                : 0;
        }

        private void ConsumeSurvivalDeathSignal()
        {
            if (_survivalSystem == null)
            {
                _survivalSystem = ResolveSurvivalSystem();
                RefreshSurvivalSignalBinding();
            }

            if (!SurvivalSignalRoute.TryGetLatestDeath(out SurvivalVitalsChangedSignal signal, out int sequence))
                return;

            if (sequence == _lastSurvivalDeathSignalSequence)
                return;

            _lastSurvivalDeathSignalSequence = sequence;
            uint sourceId = _survivalSignalSourceId;
            if (sourceId != 0u && signal.SourceId != sourceId)
                return;

            if ((signal.Flags & SurvivalVitalsChangedSignalFlags.Death) == 0u)
                return;

            HandlePlayerDeath((SurvivalDeathCause)signal.DeathCause);
        }

        private void ProcessProgressionMetaSignals()
        {
            ReadOnlySpan<ProgressionMetaSignal> signals = SignalBus<ProgressionMetaSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ProgressionMetaSignal signal = signals[i];
                if (!IsNewerSequence(signal.Sequence, _lastProgressionMetaSequence))
                    continue;

                _lastProgressionMetaSequence = signal.Sequence;
                if (signal.Kind == ProgressionMetaSignal.KindBiomeDiscovered)
                    HandleBiomeDiscovered(unchecked((int)signal.ContextHash));
            }
        }

        private static uint ResolveSurvivalSignalSourceId(HectonSurvivalSystem system)
        {
            return system != null
                ? RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(system.GetEntityId()))
                : 0u;
        }

        private static bool IsNewerSequence(uint candidate, uint lastProcessed)
        {
            return candidate != 0u && (lastProcessed == 0u || unchecked((int)(candidate - lastProcessed)) > 0);
        }

        private void HandleGameLoaded()
        {
            RebindOwnerSubscriptions();
            RefreshLogbookSignalPumpRegistration();
        }

        private void HandlePlayerSpawned()
        {
            RebindOwnerSubscriptions();
            RefreshLogbookSignalPumpRegistration();
        }

        private void HandlePlayerDeath(SurvivalDeathCause cause)
        {
            TryAppendEntry(FirstDeathOriginHash, FirstDeathTitleHash, ResolveDeathMessageHash(cause));
        }

        private void HandleBiomeDiscovered(int biomeId)
        {
            TryAppendEntry(ResolveBiomeOriginHash(biomeId), BiomeDiscoveredTitleHash, BiomeDiscoveredMessageHash);
        }

        private static bool LooksLikeLeviathanEntry(in ScanLogChangedSignal signal)
        {
            return signal.CategoryHash == LeviathanCategoryHash ||
                   signal.EntryHash == BlackChoirLeviathanEntryHash ||
                   signal.EntryHash == FurnaceMawLeviathanEntryHash ||
                   signal.EntryHash == GateWardenLeviathanEntryHash ||
                   signal.EntryHash == HaloCrownLeviathanEntryHash ||
                   signal.EntryHash == RiftLancerLeviathanEntryHash ||
                   signal.EntryHash == VoidRibbonLeviathanEntryHash;
        }

        private static int ResolveBiomeOriginHash(int biomeId)
        {
            unchecked
            {
                int originHash = (BiomeLogOriginSaltHash * 16777619) ^ biomeId;
                return originHash != 0 ? originHash : BiomeLogOriginSaltHash;
            }
        }

        private static int ResolveDeathMessageHash(SurvivalDeathCause cause)
        {
            switch (cause)
            {
                case SurvivalDeathCause.OxygenDepletion:
                    return DeathOxygenMessageHash;
                case SurvivalDeathCause.PressureCollapse:
                    return DeathPressureMessageHash;
                case SurvivalDeathCause.ThermalFailure:
                    return DeathThermalMessageHash;
                case SurvivalDeathCause.RadiationExposure:
                    return DeathRadiationMessageHash;
                case SurvivalDeathCause.Starvation:
                    return DeathStarvationMessageHash;
                case SurvivalDeathCause.Dehydration:
                    return DeathDehydrationMessageHash;
                case SurvivalDeathCause.IntegrityFailure:
                    return DeathIntegrityMessageHash;
                default:
                    return DeathUnknownMessageHash;
            }
        }

        private HectonSurvivalSystem ResolveSurvivalSystem()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null &&
                playerContext.PlayerObject != null &&
                playerContext.PlayerObject.TryGetComponent(out HectonSurvivalSystem survivalSystem))
            {
                return survivalSystem;
            }

            return null;
        }

        private void TryRegisterWithSaveManager()
        {
            if (_registeredToSave || !Application.isPlaying || !isActiveAndEnabled)
                return;

            if (_saveService == null)
                _saveService = GlobalRegistry.Save;

            if (_saveService == null)
                return;

            _saveService.Register(this);
            _registeredToSave = true;
        }

        private void UnregisterFromSaveManager()
        {
            if (!_registeredToSave)
                return;

            ISaveService saveService = _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredToSave = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Save)
                return;

            UnregisterFromSaveManager();
            _saveService = currentService as ISaveService;
            TryRegisterWithSaveManager();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }
    }
}
