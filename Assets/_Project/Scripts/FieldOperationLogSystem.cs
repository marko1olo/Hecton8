using System;
using System.Collections.Generic;
using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Field Operation Log System")]
    public sealed class FieldOperationLogSystem : MonoBehaviour, ISaveable
    {
        public readonly struct FieldOperationSnapshot
        {
            public readonly string Source;
            public readonly string Title;
            public readonly string Summary;
            public readonly string Severity;

            public FieldOperationSnapshot(string source, string title, string summary, string severity)
            {
                Source = source;
                Title = title;
                Summary = summary;
                Severity = severity;
            }
        }

        private struct FieldOperationRecord
        {
            public string source;
            public string title;
            public string summary;
            public string severity;
        }

        [SerializeField] private int maxRecentEntries = 10;
        [SerializeField] private bool verboseLogging;

        private readonly List<FieldOperationRecord> _recent = new List<FieldOperationRecord>(12);

        public static FieldOperationLogSystem Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        public int SavePriority => 36;
        public int LoadPriority => 36;
        public int RecentCount => _recent.Count;

        public event Action LogChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            SaveManager.Instance?.Register(this);
        }

        private void OnDisable()
        {
            SaveManager.Instance?.Unregister(this);
            if (Instance == this)
                Instance = null;
        }

        public static void RecordOperation(string source, string title, string summary, string severity = "INFO")
        {
            Instance?.Push(source, title, summary, severity);
        }

        public int CopyRecentEntries(FieldOperationSnapshot[] buffer)
        {
            if (buffer == null || buffer.Length == 0 || _recent.Count == 0)
                return 0;

            int count = Mathf.Min(buffer.Length, _recent.Count);
            for (int i = 0; i < count; i++)
            {
                FieldOperationRecord record = _recent[i];
                buffer[i] = new FieldOperationSnapshot(record.source, record.title, record.summary, record.severity);
            }

            return count;
        }

        public bool TryGetLatestEntry(out FieldOperationSnapshot snapshot)
        {
            if (_recent.Count <= 0)
            {
                snapshot = default;
                return false;
            }

            FieldOperationRecord record = _recent[0];
            snapshot = new FieldOperationSnapshot(record.source, record.title, record.summary, record.severity);
            return true;
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.fieldOperations.EnsureCapacity();
            data.fieldOperations.recentCount = Mathf.Min(_recent.Count, FieldOperationLogDTO.MaxRecentEntries);

            for (int i = 0; i < data.fieldOperations.recentCount; i++)
            {
                FieldOperationRecord record = _recent[i];
                data.fieldOperations.recentEntries[i] = new FieldOperationEntryDTO
                {
                    source = record.source,
                    title = record.title,
                    summary = record.summary,
                    severity = record.severity
                };
            }

            for (int i = data.fieldOperations.recentCount; i < FieldOperationLogDTO.MaxRecentEntries; i++)
                data.fieldOperations.recentEntries[i] = default;
        }

        public void LoadFromSaveData(SaveData data)
        {
            _recent.Clear();

            if (data == null)
                return;

            FieldOperationLogDTO dto = data.fieldOperations;
            int count = Mathf.Clamp(dto.recentCount, 0, dto.recentEntries != null ? dto.recentEntries.Length : 0);
            for (int i = 0; i < count; i++)
            {
                FieldOperationEntryDTO entry = dto.recentEntries[i];
                if (string.IsNullOrWhiteSpace(entry.title))
                    continue;

                _recent.Add(new FieldOperationRecord
                {
                    source = NormalizeSource(entry.source),
                    title = entry.title.Trim(),
                    summary = NormalizeSummary(entry.summary),
                    severity = NormalizeSeverity(entry.severity)
                });
            }

            LogChanged?.Invoke();
        }

        private void Push(string source, string title, string summary, string severity)
        {
            if (string.IsNullOrWhiteSpace(title))
                return;

            FieldOperationRecord record = new FieldOperationRecord
            {
                source = NormalizeSource(source),
                title = title.Trim(),
                summary = NormalizeSummary(summary),
                severity = NormalizeSeverity(severity)
            };

            if (_recent.Count > 0)
            {
                FieldOperationRecord latest = _recent[0];
                if (string.Equals(latest.title, record.title, StringComparison.Ordinal) &&
                    string.Equals(latest.summary, record.summary, StringComparison.Ordinal) &&
                    string.Equals(latest.severity, record.severity, StringComparison.Ordinal))
                {
                    return;
                }
            }

            _recent.Insert(0, record);
            int capacity = Mathf.Clamp(maxRecentEntries, 1, FieldOperationLogDTO.MaxRecentEntries);
            if (_recent.Count > capacity)
                _recent.RemoveRange(capacity, _recent.Count - capacity);

            if (verboseLogging)
                Debug.Log($"[FieldOps] {record.source} | {record.severity} | {record.title} | {record.summary}");

            LogChanged?.Invoke();
        }

        private static string NormalizeSource(string source)
        {
            return string.IsNullOrWhiteSpace(source) ? "FIELD" : source.Trim().ToUpperInvariant();
        }

        private static string NormalizeSummary(string summary)
        {
            return string.IsNullOrWhiteSpace(summary) ? "Field operation archived." : summary.Trim();
        }

        private static string NormalizeSeverity(string severity)
        {
            if (string.IsNullOrWhiteSpace(severity))
                return "INFO";

            string normalized = severity.Trim().ToUpperInvariant();
            if (normalized == "CRITICAL" || normalized == "WARN" || normalized == "WARNING" || normalized == "INFO")
                return normalized == "WARNING" ? "WARN" : normalized;

            return "INFO";
        }
    }
}
