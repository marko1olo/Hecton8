using System;
using System.Collections.Generic;
using Hecton8.SaveSystem;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Scan Log System")]
    public sealed class ScanLogSystem : MonoBehaviour, ISaveable
    {
        public readonly struct ScanEntrySnapshot
        {
            public readonly string Id;
            public readonly string Title;
            public readonly string Category;
            public readonly string Summary;

            public ScanEntrySnapshot(string id, string title, string category, string summary)
            {
                Id = id;
                Title = title;
                Category = category;
                Summary = summary;
            }
        }

        private struct ScanEntryRecord
        {
            public string id;
            public string title;
            public string category;
            public string summary;
        }

        private const string GenericResourceEntryId = "scan.resource_node";
        private const string GenericResourceTitle = "RESOURCE DEPOSIT";
        private const string GenericResourceCategory = "Resource";
        private const string GenericResourceSummary =
            "Hydroacoustic pulse returned a mineral-density signature. Mark for salvage or extraction.";

        [SerializeField] private int maxTrackedEntries = 128;
        [SerializeField] private int maxRecentEntries = 6;

        private readonly Dictionary<string, int> _entryIndexById = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<ScanEntryRecord> _entries = new List<ScanEntryRecord>(64);
        private readonly List<string> _recentIds = new List<string>(8);
        private ScanEntrySnapshot[] _recentBuffer;
        private HUDNotification _hudNotification;

        public static ScanLogSystem Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        public int SavePriority => 35;
        public int LoadPriority => 35;
        public int EntryCount => _entries.Count;
        public int RecentCount => _recentIds.Count;

        public event Action ScanLogChanged;
        public event Action<ScanEntrySnapshot> EntryUnlocked;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureBuffers();
            AutoResolveHud();
        }

        private void OnEnable()
        {
            SaveManager.Instance?.Register(this);
            ScanEvents.OnEntryDiscovered += HandleEntryDiscovered;
            ScanEvents.OnNodeFound += HandleNodeFound;
        }

        private void OnDisable()
        {
            SaveManager.Instance?.Unregister(this);
            ScanEvents.OnEntryDiscovered -= HandleEntryDiscovered;
            ScanEvents.OnNodeFound -= HandleNodeFound;

            if (Instance == this)
                Instance = null;
        }

        public int CopyRecentEntries(ScanEntrySnapshot[] buffer)
        {
            if (buffer == null || buffer.Length == 0 || _recentIds.Count == 0)
                return 0;

            int count = Mathf.Min(buffer.Length, _recentIds.Count);
            for (int i = 0; i < count; i++)
            {
                string id = _recentIds[i];
                if (!_entryIndexById.TryGetValue(id, out int entryIndex) || entryIndex < 0 || entryIndex >= _entries.Count)
                {
                    buffer[i] = default;
                    continue;
                }

                ScanEntryRecord entry = _entries[entryIndex];
                buffer[i] = new ScanEntrySnapshot(entry.id, entry.title, entry.category, entry.summary);
            }

            return count;
        }

        public bool TryGetLatestEntry(out ScanEntrySnapshot entry)
        {
            if (_recentIds.Count <= 0)
            {
                entry = default;
                return false;
            }

            string id = _recentIds[0];
            if (!_entryIndexById.TryGetValue(id, out int entryIndex) || entryIndex < 0 || entryIndex >= _entries.Count)
            {
                entry = default;
                return false;
            }

            ScanEntryRecord record = _entries[entryIndex];
            entry = new ScanEntrySnapshot(record.id, record.title, record.category, record.summary);
            return true;
        }

        public bool ContainsEntry(string entryId)
        {
            return !string.IsNullOrWhiteSpace(entryId) && _entryIndexById.ContainsKey(entryId);
        }

        public void ArchiveEntry(string entryId, string title, string category, string summary, bool markRecent = true)
        {
            TryAddOrUpdateEntry(entryId, title, category, summary, markRecent, raiseEvents: true);
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.scanLog.EnsureCapacity();
            data.scanLog.entryCount = Mathf.Min(_entries.Count, ScanLogDTO.MaxEntries);
            data.scanLog.recentCount = Mathf.Min(_recentIds.Count, ScanLogDTO.MaxRecentEntries);

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

            for (int i = 0; i < data.scanLog.recentCount; i++)
                data.scanLog.recentEntryIds[i] = _recentIds[i];

            for (int i = data.scanLog.recentCount; i < ScanLogDTO.MaxRecentEntries; i++)
                data.scanLog.recentEntryIds[i] = string.Empty;
        }

        public void LoadFromSaveData(SaveData data)
        {
            ClearRuntimeState();

            if (data == null)
                return;

            ScanLogDTO dto = data.scanLog;
            int entryCount = Mathf.Clamp(dto.entryCount, 0, dto.entries != null ? dto.entries.Length : 0);
            for (int i = 0; i < entryCount; i++)
            {
                ScanEntryDTO entry = dto.entries[i];
                TryAddOrUpdateEntry(entry.id, entry.title, entry.category, entry.summary, markRecent: false, raiseEvents: false);
            }

            int recentCount = Mathf.Clamp(dto.recentCount, 0, dto.recentEntryIds != null ? dto.recentEntryIds.Length : 0);
            for (int i = 0; i < recentCount; i++)
            {
                string entryId = dto.recentEntryIds[i];
                if (string.IsNullOrWhiteSpace(entryId) || !_entryIndexById.ContainsKey(entryId))
                    continue;

                _recentIds.Add(entryId);
            }

            ScanLogChanged?.Invoke();
        }

        private void HandleEntryDiscovered(string entryId, string title, string category, string summary)
        {
            TryAddOrUpdateEntry(entryId, title, category, summary, markRecent: true, raiseEvents: true);
        }

        private void HandleNodeFound(Unity.Mathematics.float3 _)
        {
            if (ContainsEntry(GenericResourceEntryId))
                return;

            TryAddOrUpdateEntry(
                GenericResourceEntryId,
                GenericResourceTitle,
                GenericResourceCategory,
                GenericResourceSummary,
                markRecent: true,
                raiseEvents: true);
        }

        private void TryAddOrUpdateEntry(
            string entryId,
            string title,
            string category,
            string summary,
            bool markRecent,
            bool raiseEvents)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                return;

            entryId = entryId.Trim();
            title = string.IsNullOrWhiteSpace(title) ? entryId.ToUpperInvariant() : title.Trim();
            category = string.IsNullOrWhiteSpace(category) ? "Unknown" : category.Trim();
            summary = string.IsNullOrWhiteSpace(summary) ? "Scan profile archived." : summary.Trim();

            bool added = false;
            if (_entryIndexById.TryGetValue(entryId, out int existingIndex))
            {
                ScanEntryRecord updated = _entries[existingIndex];
                updated.title = title;
                updated.category = category;
                updated.summary = summary;
                _entries[existingIndex] = updated;
            }
            else
            {
                if (_entries.Count >= Mathf.Max(1, maxTrackedEntries))
                    return;

                existingIndex = _entries.Count;
                _entryIndexById.Add(entryId, existingIndex);
                _entries.Add(new ScanEntryRecord
                {
                    id = entryId,
                    title = title,
                    category = category,
                    summary = summary
                });
                added = true;
            }

            if (markRecent)
                PushRecent(entryId);

            if (added && raiseEvents)
            {
                ShowUnlockFeedback(title, category);
                EntryUnlocked?.Invoke(new ScanEntrySnapshot(entryId, title, category, summary));
            }

            if (added || markRecent)
                ScanLogChanged?.Invoke();
        }

        private void PushRecent(string entryId)
        {
            _recentIds.Remove(entryId);
            _recentIds.Insert(0, entryId);

            int cap = Mathf.Clamp(maxRecentEntries, 1, ScanLogDTO.MaxRecentEntries);
            if (_recentIds.Count > cap)
                _recentIds.RemoveRange(cap, _recentIds.Count - cap);
        }

        private void EnsureBuffers()
        {
            int cap = Mathf.Clamp(maxRecentEntries, 1, ScanLogDTO.MaxRecentEntries);
            if (_recentBuffer == null || _recentBuffer.Length != cap)
                _recentBuffer = new ScanEntrySnapshot[cap];
        }

        private void ClearRuntimeState()
        {
            _entryIndexById.Clear();
            _entries.Clear();
            _recentIds.Clear();
            EnsureBuffers();
        }

        private void AutoResolveHud()
        {
            if (_hudNotification == null)
                HUDNotification.TryGetActive(out _hudNotification);
        }

        private void ShowUnlockFeedback(string title, string category)
        {
            AutoResolveHud();
            if (_hudNotification == null)
                return;

            string resolvedTitle = string.IsNullOrWhiteSpace(title) ? "UNKNOWN CONTACT" : title.Trim().ToUpperInvariant();
            string resolvedCategory = string.IsNullOrWhiteSpace(category) ? "Unknown" : category.Trim().ToUpperInvariant();
            _hudNotification.ShowInfo($"SCAN ARCHIVED - {resolvedTitle} [{resolvedCategory}]");
        }
    }
}
