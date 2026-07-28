using System;
using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Scan Log System")]
    public sealed class ScanLogSystem : MonoBehaviour, ISaveable, IScanEventListener, IScanLogService, IGlobalRegistryHotSwapListener
    {
        private static int s_x001ScanLogSystemSignalPushDropCount;
        public readonly struct ScanEntrySnapshot
        {
            public readonly string Id;
            public readonly string Title;
            public readonly string Category;
            public readonly string Summary;
            public readonly uint IdHash;
            public readonly uint TitleHash;
            public readonly uint CategoryHash;
            public readonly uint SummaryHash;

            public ScanEntrySnapshot(string id, string title, string category, string summary)
                : this(
                    id,
                    title,
                    category,
                    summary,
                    ScanEvents.ComputeEntryHash(id),
                    ScanLogSystem.ComputeContentHash(title),
                    ScanLogSystem.ComputeContentHash(category),
                    ScanLogSystem.ComputeContentHash(summary))
            {
            }

            public ScanEntrySnapshot(
                string id,
                string title,
                string category,
                string summary,
                uint idHash,
                uint titleHash,
                uint categoryHash,
                uint summaryHash)
            {
                Id = id;
                Title = title;
                Category = category;
                Summary = summary;
                IdHash = idHash;
                TitleHash = titleHash;
                CategoryHash = categoryHash;
                SummaryHash = summaryHash;
            }
        }

        private struct ScanEntryRecord
        {
            public string id;
            public string title;
            public string category;
            public string summary;
            public uint idHash;
            public uint titleHash;
            public uint categoryHash;
            public uint summaryHash;
        }

        private const string GenericResourceEntryId = "scan.resource_node";
        private const string GenericResourceTitle = "RESOURCE DEPOSIT";
        private const string GenericResourceCategory = "Resource";
        private const string GenericResourceSummary =
            "Hydroacoustic pulse returned a mineral-density signature. Mark for salvage or extraction.";
        private const string UnknownTitle = "UNKNOWN CONTACT";
        private const string UnknownCategory = "Unknown";
        private const string DefaultSummary = "Scan profile archived.";
        private const string ScanArchivedMessage = "SCAN ARCHIVED";
        private static readonly uint ScanArchivedNotificationMissWarningHash = unchecked((uint)LocHash.Compute("ScanLogSystem.NotificationMiss"));
        private static readonly uint ScanArchivedNotificationContextHash = unchecked((uint)LocHash.Compute("ScanLogSystem.ScanArchived"));

        private static readonly uint GenericResourceEntryHash = ScanEvents.ComputeEntryHash(GenericResourceEntryId);
        private static readonly uint GenericResourceTitleHash = ComputeContentHash(GenericResourceTitle);
        private static readonly uint GenericResourceCategoryHash = ComputeContentHash(GenericResourceCategory);
        private static readonly uint GenericResourceSummaryHash = ComputeContentHash(GenericResourceSummary);

        [SerializeField] private int maxTrackedEntries = 128;
        [SerializeField] private int maxRecentEntries = 6;

        private readonly Dictionary<uint, int> _entryIndexByHash = new Dictionary<uint, int>(128);
        private readonly List<ScanEntryRecord> _entries = new List<ScanEntryRecord>(64);
        private readonly List<uint> _recentEntryHashes = new List<uint>(8);
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;
        private bool _saveRegistered;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private uint _scanArchivedNotificationHash;
        private uint _signalSourceId;
        private uint _changeRevision;
        private int _scanArchivedNotificationMissCount;

        private static ScanLogSystem s_activeRuntimeInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_activeRuntimeInstance = null;
        }

        public int SavePriority => 35;
        public int LoadPriority => 35;
        public int EntryCount => _entries.Count;
        public int RecentCount => _recentEntryHashes.Count;
        public int ScanArchivedNotificationMissCount => _scanArchivedNotificationMissCount;
        public uint ChangeRevision => _changeRevision;
        public uint SourceId => _signalSourceId != 0u
            ? _signalSourceId
            : RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(GetEntityId()));

        private void Awake()
        {
            _scanArchivedNotificationHash = NotificationEvents.RegisterMessage(ScanArchivedMessage.AsSpan());
            _signalSourceId = RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(GetEntityId()));
        }

        private void OnEnable()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            TryRegisterHotSwapListener();
            TryRegisterService();
            TryRegisterSaveParticipant();
            ScanEvents.Register(this);
        }

        private void Start()
        {
            TryRegisterSaveParticipant();
        }

        private void OnDisable()
        {
            TryUnregisterSaveParticipant();
            TryUnregisterService();
            ScanEvents.Unregister(this);
            TryUnregisterHotSwapListener();
            ClearScanArchivedNotificationDiagnostics();
        }

        private void OnDestroy()
        {
            TryUnregisterSaveParticipant();
            TryUnregisterService();
            TryUnregisterHotSwapListener();
            ClearScanArchivedNotificationDiagnostics();
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterScanLogRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.ScanLog, this);
            if (_serviceRegistered)
                s_activeRuntimeInstance = this;
        }



        /// <summary>
        /// Yields to an already-usable runtime by destroying THIS COMPONENT, never its host GameObject.
        /// </summary>
        /// <remarks>
        /// ScanLogSystem is authored on the ROOT of Player.prefab, so Destroy(gameObject) here destroys the
        /// entire player - the Rigidbody, the collider, HectonPlayerMovement, every service handle the
        /// bootstrap resolves off it. That is not a hypothetical: the identical line in
        /// BeaconNetworkSystem.TryAbortForUsableExistingRuntime did exactly that, silently, and made the
        /// world unenterable across three consecutive headless runs.
        ///
        /// This type has no bootstrap-created twin today - nothing AddComponents it and its GUID appears in
        /// no asset other than Player.prefab - so it only fires against a genuine second player. That makes
        /// it a landmine rather than a live defect, and the difference is one double-spawn.
        ///
        /// Destroy(this) is the project's own precedent (PlayerActionController) and the invariant is asserted
        /// for that one component at Audio/Editor/AdvancedAcousticsSmokeTester.cs:672. It belongs to every
        /// component authored on the player root. The duplicate is the COMPONENT.
        /// </remarks>
        private bool TryAbortForUsableExistingRuntime()
        {
            ScanLogSystem active = s_activeRuntimeInstance;
            if (!ReferenceEquals(active, null) && !ReferenceEquals(active, this))
            {
                if (IsScanLogRuntimeUsable(active))
                {
                    Destroy(this);
                    return true;
                }

                if (ReferenceEquals(s_activeRuntimeInstance, active))
                    s_activeRuntimeInstance = null;
                if (ReferenceEquals(GlobalRegistry.ScanLog, active))
                    GlobalRegistry.UnregisterScanLogRuntime(active);
            }

            ScanLogSystem registered = GlobalRegistry.ScanLog;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsScanLogRuntimeUsable(registered))
            {
                s_activeRuntimeInstance = registered;

                // Same reasoning as the branch above. See the remarks on this method.
                Destroy(this);
                return true;
            }

            GlobalRegistry.UnregisterScanLogRuntime(registered);
            return false;
        }

        private static bool IsScanLogRuntimeUsable(ScanLogSystem system)
        {
            return system != null &&
                   system._serviceRegistered &&
                   system.isActiveAndEnabled;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.ScanLog, this))
                GlobalRegistry.UnregisterScanLogRuntime(this);

            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;

            _serviceRegistered = false;
        }

        public int CopyRecentEntries(ScanEntrySnapshot[] buffer)
        {
            if (buffer == null || buffer.Length == 0 || _recentEntryHashes.Count == 0)
                return 0;

            int count = math.min(buffer.Length, _recentEntryHashes.Count);
            for (int i = 0; i < count; i++)
            {
                uint entryHash = _recentEntryHashes[i];
                if (!_entryIndexByHash.TryGetValue(entryHash, out int entryIndex) || entryIndex < 0 || entryIndex >= _entries.Count)
                {
                    buffer[i] = default;
                    continue;
                }

                ScanEntryRecord entry = _entries[entryIndex];
                buffer[i] = ToSnapshot(in entry);
            }

            return count;
        }

        public bool TryGetLatestEntry(out ScanEntrySnapshot entry)
        {
            if (_recentEntryHashes.Count <= 0)
            {
                entry = default;
                return false;
            }

            uint entryHash = _recentEntryHashes[0];
            if (!_entryIndexByHash.TryGetValue(entryHash, out int entryIndex) || entryIndex < 0 || entryIndex >= _entries.Count)
            {
                entry = default;
                return false;
            }

            ScanEntryRecord record = _entries[entryIndex];
            entry = ToSnapshot(in record);
            return true;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private void TryRegisterSaveParticipant()
        {
            if (_saveRegistered || !Application.isPlaying || !isActiveAndEnabled)
                return;

            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
            }

            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
            _saveRegistered = true;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_saveRegistered && _registeredSaveService == null)
                return;

            ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredSaveService = null;
            _saveRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Save)
                return;

            TryUnregisterSaveParticipant();
            _saveService = currentService as ISaveService;
            TryRegisterSaveParticipant();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        public bool ContainsEntry(uint entryHash)
        {
            return entryHash != 0u && _entryIndexByHash.ContainsKey(entryHash);
        }

        public void ArchiveEntry(string entryId, string title, string category, string summary, bool markRecent = true)
        {
            TryAddOrUpdateEntry(
                ScanEvents.ComputeEntryHash(entryId),
                entryId,
                title,
                category,
                summary,
                titleHash: 0u,
                categoryHash: 0u,
                summaryHash: 0u,
                markRecent: markRecent,
                raiseEvents: true);
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            FlushPendingScanEventsForSave();
            data.scanLog.EnsureCapacity();
            data.scanLog.entryCount = math.min(_entries.Count, ScanLogDTO.MaxEntries);

            for (int i = 0; i < data.scanLog.entryCount; i++)
            {
                ScanEntryRecord entry = _entries[i];
                data.scanLog.entries[i] = new ScanEntryDTO
                {
                    id = entry.id,
                    title = entry.title,
                    category = entry.category,
                    summary = entry.summary
                };
            }

            for (int i = data.scanLog.entryCount; i < ScanLogDTO.MaxEntries; i++)
                data.scanLog.entries[i] = default;

            int recentCount = 0;
            for (int i = 0; i < _recentEntryHashes.Count && recentCount < ScanLogDTO.MaxRecentEntries; i++)
            {
                uint entryHash = _recentEntryHashes[i];
                if (!_entryIndexByHash.TryGetValue(entryHash, out int entryIndex) || entryIndex < 0 || entryIndex >= _entries.Count)
                    continue;

                data.scanLog.recentEntryIds[recentCount] = _entries[entryIndex].id;
                recentCount++;
            }

            data.scanLog.recentCount = recentCount;
            for (int i = recentCount; i < ScanLogDTO.MaxRecentEntries; i++)
                data.scanLog.recentEntryIds[i] = string.Empty;
        }

        private static void FlushPendingScanEventsForSave()
        {
            if (ScanEvents.PendingCount <= 0)
                return;

            ScanEvents.FlushPending();
        }

        public void LoadFromSaveData(SaveData data)
        {
            ClearRuntimeState();

            if (data == null)
                return;

            ScanLogDTO dto = data.scanLog;
            int entryCount = math.clamp(dto.entryCount, 0, dto.entries != null ? dto.entries.Length : 0);
            for (int i = 0; i < entryCount; i++)
            {
                ScanEntryDTO entry = dto.entries[i];
                TryAddOrUpdateEntry(
                    ScanEvents.ComputeEntryHash(entry.id),
                    entry.id,
                    entry.title,
                    entry.category,
                    entry.summary,
                    titleHash: 0u,
                    categoryHash: 0u,
                    summaryHash: 0u,
                    markRecent: false,
                    raiseEvents: false,
                    publishChangeSignal: false);
            }

            int recentCount = math.clamp(dto.recentCount, 0, dto.recentEntryIds != null ? dto.recentEntryIds.Length : 0);
            for (int i = 0; i < recentCount; i++)
            {
                string entryId = dto.recentEntryIds[i];
                uint entryHash = ScanEvents.ComputeEntryHash(entryId);
                if (entryHash == 0u || !_entryIndexByHash.ContainsKey(entryHash))
                    continue;

                _recentEntryHashes.Add(entryHash);
            }

            PublishScanLogChanged(0u, ScanLogChangedSignal.ReasonLoaded);
        }

        public void OnScanEvent(in ScanEventPayload payload)
        {
            switch ((ScanEventType)payload.EventType)
            {
                case ScanEventType.EntryDiscovered:
                    if (ScanEvents.TryResolveEntryMetadata(payload.EntryHash, out ScanEntryMetadata metadata))
                    {
                        HandleEntryDiscovered(in metadata);
                    }
                    break;

                case ScanEventType.NodeFound:
                    HandleNodeFound(payload.Position);
                    break;
            }
        }

        private void HandleEntryDiscovered(in ScanEntryMetadata metadata)
        {
            TryAddOrUpdateEntry(
                metadata.EntryHash,
                metadata.EntryId,
                metadata.Title,
                metadata.Category,
                metadata.Summary,
                metadata.TitleHash,
                metadata.CategoryHash,
                metadata.SummaryHash,
                markRecent: true,
                raiseEvents: true);
        }

        private void HandleNodeFound(Unity.Mathematics.float3 _)
        {
            if (ContainsEntry(GenericResourceEntryHash))
                return;

            TryAddOrUpdateEntry(
                GenericResourceEntryHash,
                GenericResourceEntryId,
                GenericResourceTitle,
                GenericResourceCategory,
                GenericResourceSummary,
                GenericResourceTitleHash,
                GenericResourceCategoryHash,
                GenericResourceSummaryHash,
                markRecent: true,
                raiseEvents: true);
        }

        private void TryAddOrUpdateEntry(
            uint entryHash,
            string entryId,
            string title,
            string category,
            string summary,
            uint titleHash,
            uint categoryHash,
            uint summaryHash,
            bool markRecent,
            bool raiseEvents,
            bool publishChangeSignal = true)
        {
            entryId = TrimOrFallback(entryId, string.Empty);
            if (entryHash == 0u || entryId.Length == 0)
                return;

            title = TrimOrFallback(title, UnknownTitle);
            category = TrimOrFallback(category, UnknownCategory);
            summary = TrimOrFallback(summary, DefaultSummary);
            titleHash = titleHash != 0u ? titleHash : ComputeContentHash(title);
            categoryHash = categoryHash != 0u ? categoryHash : ComputeContentHash(category);
            summaryHash = summaryHash != 0u ? summaryHash : ComputeContentHash(summary);

            bool added = false;
            if (_entryIndexByHash.TryGetValue(entryHash, out int existingIndex))
            {
                ScanEntryRecord updated = _entries[existingIndex];
                updated.id = entryId;
                updated.title = title;
                updated.category = category;
                updated.summary = summary;
                updated.idHash = entryHash;
                updated.titleHash = titleHash;
                updated.categoryHash = categoryHash;
                updated.summaryHash = summaryHash;
                _entries[existingIndex] = updated;
            }
            else
            {
                if (_entries.Count >= math.max(1, maxTrackedEntries))
                    return;

                existingIndex = _entries.Count;
                _entryIndexByHash.Add(entryHash, existingIndex);
                _entries.Add(new ScanEntryRecord
                {
                    id = entryId,
                    title = title,
                    category = category,
                    summary = summary,
                    idHash = entryHash,
                    titleHash = titleHash,
                    categoryHash = categoryHash,
                    summaryHash = summaryHash
                });
                added = true;
            }

            if (markRecent)
                PushRecent(entryHash);

            if (added && raiseEvents)
            {
                ShowUnlockFeedback();
            }

            if (added || markRecent)
            {
                if (publishChangeSignal)
                    PublishScanLogChanged(entryHash, added ? ScanLogChangedSignal.ReasonEntryAdded : ScanLogChangedSignal.ReasonRecentChanged, categoryHash);
            }
        }

        private void PublishScanLogChanged(uint entryHash, byte reason, uint categoryHash = 0u)
        {
            if (_signalSourceId == 0u)
                _signalSourceId = RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(GetEntityId()));

            _changeRevision = unchecked(_changeRevision + 1u);
            if (_changeRevision == 0u)
                _changeRevision = 1u;

            ScanLogChangedSignal signal = new ScanLogChangedSignal
            {
                SourceId = _signalSourceId,
                EntryHash = entryHash,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                EntryCount = (ushort)math.clamp(_entries.Count, 0, ushort.MaxValue),
                RecentCount = (ushort)math.clamp(_recentEntryHashes.Count, 0, ushort.MaxValue),
                Reason = reason,
                Flags = 0,
                Revision = _changeRevision,
                CategoryHash = categoryHash
            };

            SignalBus<ScanLogChangedSignal>.TryPushTracked(in signal, ref s_x001ScanLogSystemSignalPushDropCount);
        }

        private void PushRecent(uint entryHash)
        {
            int cap = math.clamp(maxRecentEntries, 1, ScanLogDTO.MaxRecentEntries);
            for (int i = _recentEntryHashes.Count - 1; i >= cap; i--)
                _recentEntryHashes.RemoveAt(i);

            int count = _recentEntryHashes.Count;
            int existingIndex = -1;
            for (int i = 0; i < count; i++)
            {
                if (_recentEntryHashes[i] == entryHash)
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex == 0)
                return;

            if (existingIndex > 0)
            {
                for (int i = existingIndex; i > 0; i--)
                    _recentEntryHashes[i] = _recentEntryHashes[i - 1];

                _recentEntryHashes[0] = entryHash;
                return;
            }

            if (count < cap)
            {
                _recentEntryHashes.Add(entryHash);
                count++;
            }

            for (int i = count - 1; i > 0; i--)
                _recentEntryHashes[i] = _recentEntryHashes[i - 1];

            _recentEntryHashes[0] = entryHash;
        }

        private void ClearRuntimeState()
        {
            _entryIndexByHash.Clear();
            _entries.Clear();
            _recentEntryHashes.Clear();
            ClearScanArchivedNotificationDiagnostics();
        }

        private void ClearScanArchivedNotificationDiagnostics()
        {
            _scanArchivedNotificationMissCount = 0;
        }

        private void ShowUnlockFeedback()
        {
            if (_scanArchivedNotificationHash == 0u)
                _scanArchivedNotificationHash = NotificationEvents.RegisterMessage(ScanArchivedMessage.AsSpan());

            TryPushScanArchivedNotification();
        }

        private void TryPushScanArchivedNotification()
        {
            if (_scanArchivedNotificationHash == 0u)
            {
                ReportScanArchivedNotificationMiss();
                return;
            }

            if (NotificationEvents.TryPushRegisteredInfo(_scanArchivedNotificationHash))
                return;

            ReportScanArchivedNotificationMiss();
        }

        private void ReportScanArchivedNotificationMiss()
        {
            _scanArchivedNotificationMissCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ScanArchivedNotificationMissWarningHash,
                ScanArchivedNotificationContextHash,
                math.max(1, _scanArchivedNotificationMissCount));
        }

        private static ScanEntrySnapshot ToSnapshot(in ScanEntryRecord entry)
        {
            return new ScanEntrySnapshot(
                entry.id,
                entry.title,
                entry.category,
                entry.summary,
                entry.idHash,
                entry.titleHash,
                entry.categoryHash,
                entry.summaryHash);
        }

        private static uint ComputeContentHash(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? 0u
                : unchecked((uint)LocHash.Compute(value));
        }

        private static string TrimOrFallback(string value, string fallback)
        {
            if (string.IsNullOrEmpty(value))
                return fallback;

            int start = 0;
            int end = value.Length - 1;
            while (start <= end && char.IsWhiteSpace(value[start]))
                start++;
            while (end >= start && char.IsWhiteSpace(value[end]))
                end--;

            if (start > end)
                return fallback;
            if (start == 0 && end == value.Length - 1)
                return value;

            int length = end - start + 1;
            return string.Create(length, (value, start), (buffer, state) =>
            {
                state.Item1.AsSpan(state.Item2, buffer.Length).CopyTo(buffer);
            });
        }
    }
}
