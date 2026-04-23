using System;
using System.Collections.Generic;
using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5900)]
    public sealed class WorldProceduralStateRegistry : MonoBehaviour, ISaveable
    {
        private const float FaunaStateCleanupInterval = 5f;
        private const float DiagnosticsRefreshInterval = 1f;

        [Serializable]
        private struct FaunaSpawnState
        {
            public float cooldownUntilPlayTime;
            public bool isLargeThreatZone;
            public bool blocked;
        }

        [Header("Settings")]
        [SerializeField] private int initialSuppressedPlacementCapacity = 256;
        [SerializeField] private int initialFaunaStateCapacity = 128;

        [Header("Diagnostics")]
        [SerializeField] private int _debugSuppressedPlacementCount;
        [SerializeField] private int _debugFaunaStateCount;
        [SerializeField] private int _debugBlockedFaunaCount;
        [SerializeField] private int _debugLargeThreatFaunaStateCount;
        [SerializeField] private float _debugCurrentPlayTime;
        [SerializeField] private string _debugLastPlacementStateChangeReason = "None";
        [SerializeField] private long _debugLastPlacementStateChangeRuntimeKey;

        private readonly List<long> _faunaRemovalBuffer = new List<long>(128);
        private HashSet<long> _suppressedPlacementKeys;
        private Dictionary<long, FaunaSpawnState> _faunaSpawnStates;
        private float _nextFaunaStateCleanupPlayTime;
        private float _nextDiagnosticsRefreshPlayTime;
        private bool _diagnosticsDirty;

        internal static WorldProceduralStateRegistry ActiveRuntimeInstance { get; private set; }

        public event Action PlacementStateChanged;

        public int SavePriority => 55;
        public int LoadPriority => 55;
        public int SuppressedPlacementCount => _suppressedPlacementKeys != null ? _suppressedPlacementKeys.Count : 0;
        public int FaunaStateCount => _faunaSpawnStates != null ? _faunaSpawnStates.Count : 0;

        private void Awake()
        {
            ActiveRuntimeInstance = this;
            _suppressedPlacementKeys = new HashSet<long>(Mathf.Max(32, initialSuppressedPlacementCapacity));
            _faunaSpawnStates = new Dictionary<long, FaunaSpawnState>(Mathf.Max(16, initialFaunaStateCapacity));
            UpdateDiagnostics();
        }

        private void OnEnable()
        {
            SaveManager.Instance?.Register(this);
        }

        private void OnDisable()
        {
            SaveManager.Instance?.Unregister(this);
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        public bool IsPlacementSuppressed(long runtimeKey)
        {
            return runtimeKey != 0L && _suppressedPlacementKeys != null && _suppressedPlacementKeys.Contains(runtimeKey);
        }

        public bool SuppressPlacement(long runtimeKey)
        {
            if (runtimeKey == 0L || _suppressedPlacementKeys == null || !_suppressedPlacementKeys.Add(runtimeKey))
                return false;

            UpdateDiagnostics();
            RecordPlacementStateChange("suppress", runtimeKey);
            PlacementStateChanged?.Invoke();
            return true;
        }

        public bool RestorePlacement(long runtimeKey)
        {
            if (runtimeKey == 0L || _suppressedPlacementKeys == null || !_suppressedPlacementKeys.Remove(runtimeKey))
                return false;

            UpdateDiagnostics();
            RecordPlacementStateChange("restore", runtimeKey);
            PlacementStateChanged?.Invoke();
            return true;
        }

        public bool IsFaunaAnchorAvailable(long runtimeKey, bool isLargeThreatZone)
        {
            if (runtimeKey == 0L || _faunaSpawnStates == null)
                return true;

            float currentPlayTime = GetCurrentPlayTimeSeconds();
            if (currentPlayTime >= _nextFaunaStateCleanupPlayTime)
                CleanupExpiredFaunaStates(currentPlayTime);

            if (!_faunaSpawnStates.TryGetValue(runtimeKey, out FaunaSpawnState state))
                return true;

            if (state.isLargeThreatZone != isLargeThreatZone)
                return true;

            if (state.blocked)
                return false;

            if (state.cooldownUntilPlayTime > currentPlayTime)
                return false;

            _faunaSpawnStates.Remove(runtimeKey);
            MarkDiagnosticsDirty();
            RefreshDiagnosticsIfNeeded(currentPlayTime);
            return true;
        }

        public void MarkFaunaAnchorUsed(long runtimeKey, bool isLargeThreatZone, float cooldownSeconds)
        {
            if (runtimeKey == 0L || _faunaSpawnStates == null)
                return;

            float currentPlayTime = GetCurrentPlayTimeSeconds();
            FaunaSpawnState state = _faunaSpawnStates.TryGetValue(runtimeKey, out FaunaSpawnState existing)
                ? existing
                : default;
            state.isLargeThreatZone = isLargeThreatZone;
            state.blocked = false;
            state.cooldownUntilPlayTime = currentPlayTime + Mathf.Max(0f, cooldownSeconds);
            _faunaSpawnStates[runtimeKey] = state;
            MarkDiagnosticsDirty();
            RefreshDiagnosticsIfNeeded(currentPlayTime);
        }

        public void BlockFaunaAnchor(long runtimeKey, bool isLargeThreatZone)
        {
            if (runtimeKey == 0L || _faunaSpawnStates == null)
                return;

            FaunaSpawnState state = _faunaSpawnStates.TryGetValue(runtimeKey, out FaunaSpawnState existing)
                ? existing
                : default;
            state.isLargeThreatZone = isLargeThreatZone;
            state.blocked = true;
            float currentPlayTime = GetCurrentPlayTimeSeconds();
            state.cooldownUntilPlayTime = Mathf.Max(state.cooldownUntilPlayTime, currentPlayTime);
            _faunaSpawnStates[runtimeKey] = state;
            MarkDiagnosticsDirty();
            RefreshDiagnosticsIfNeeded(currentPlayTime);
        }

        public bool RestoreFaunaAnchor(long runtimeKey)
        {
            if (runtimeKey == 0L || _faunaSpawnStates == null)
                return false;

            bool removed = _faunaSpawnStates.Remove(runtimeKey);
            if (removed)
            {
                MarkDiagnosticsDirty();
                RefreshDiagnosticsIfNeeded(GetCurrentPlayTimeSeconds());
            }

            return removed;
        }

        public void ClearAll()
        {
            bool hadPlacements = _suppressedPlacementKeys != null && _suppressedPlacementKeys.Count > 0;
            _suppressedPlacementKeys?.Clear();
            _faunaSpawnStates?.Clear();
            UpdateDiagnostics();

            if (hadPlacements)
            {
                RecordPlacementStateChange("clear-all", 0L);
                PlacementStateChanged?.Invoke();
            }
        }

        public void PopulateSaveData(SaveData data)
        {
            ref ProceduralWorldStateDTO dto = ref data.proceduralWorldState;
            dto.EnsureCapacity();
            CleanupExpiredFaunaStates();

            int suppressedIndex = 0;
            if (_suppressedPlacementKeys != null)
            {
                foreach (long runtimeKey in _suppressedPlacementKeys)
                {
                    if (suppressedIndex >= ProceduralWorldStateDTO.MaxSuppressedPlacements)
                    {
                        Debug.LogWarning($"[WorldProceduralStateRegistry] Max suppressed placements ({ProceduralWorldStateDTO.MaxSuppressedPlacements}) reached. Extra entries were not saved.");
                        break;
                    }

                    dto.suppressedPlacementKeys[suppressedIndex++] = runtimeKey;
                }
            }

            dto.suppressedPlacementCount = suppressedIndex;

            int faunaIndex = 0;
            if (_faunaSpawnStates != null)
            {
                Dictionary<long, FaunaSpawnState>.Enumerator enumerator = _faunaSpawnStates.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    KeyValuePair<long, FaunaSpawnState> pair = enumerator.Current;
                    if (faunaIndex >= ProceduralWorldStateDTO.MaxFaunaStates)
                    {
                        Debug.LogWarning($"[WorldProceduralStateRegistry] Max fauna states ({ProceduralWorldStateDTO.MaxFaunaStates}) reached. Extra entries were not saved.");
                        break;
                    }

                    dto.faunaStates[faunaIndex] = new ProceduralFaunaStateDTO
                    {
                        runtimeKey = pair.Key,
                        cooldownUntilPlayTime = pair.Value.cooldownUntilPlayTime,
                        isLargeThreatZone = pair.Value.isLargeThreatZone,
                        blocked = pair.Value.blocked
                    };
                    faunaIndex++;
                }
            }

            dto.faunaStateCount = faunaIndex;
        }

        public void LoadFromSaveData(SaveData data)
        {
            ProceduralWorldStateDTO dto = data.proceduralWorldState;
            _suppressedPlacementKeys.Clear();
            _faunaSpawnStates.Clear();

            if (dto.suppressedPlacementKeys != null)
            {
                int suppressedCount = Mathf.Min(dto.suppressedPlacementCount, dto.suppressedPlacementKeys.Length);
                for (int i = 0; i < suppressedCount; i++)
                {
                    long runtimeKey = dto.suppressedPlacementKeys[i];
                    if (runtimeKey != 0L)
                        _suppressedPlacementKeys.Add(runtimeKey);
                }
            }

            if (dto.faunaStates != null)
            {
                int faunaCount = Mathf.Min(dto.faunaStateCount, dto.faunaStates.Length);
                for (int i = 0; i < faunaCount; i++)
                {
                    ProceduralFaunaStateDTO entry = dto.faunaStates[i];
                    if (entry.runtimeKey == 0L)
                        continue;

                    _faunaSpawnStates[entry.runtimeKey] = new FaunaSpawnState
                    {
                        cooldownUntilPlayTime = entry.cooldownUntilPlayTime,
                        isLargeThreatZone = entry.isLargeThreatZone,
                        blocked = entry.blocked
                    };
                }
            }

            CleanupExpiredFaunaStates();
            UpdateDiagnostics();
            RecordPlacementStateChange("load-save", 0L);
            PlacementStateChanged?.Invoke();
        }

        public string DebugLastPlacementStateChangeReason => _debugLastPlacementStateChangeReason;
        public long DebugLastPlacementStateChangeRuntimeKey => _debugLastPlacementStateChangeRuntimeKey;

        private void CleanupExpiredFaunaStates()
        {
            CleanupExpiredFaunaStates(GetCurrentPlayTimeSeconds());
        }

        private void CleanupExpiredFaunaStates(float currentPlayTime)
        {
            if (_faunaSpawnStates == null || _faunaSpawnStates.Count == 0)
            {
                _nextFaunaStateCleanupPlayTime = currentPlayTime + FaunaStateCleanupInterval;
                return;
            }

            _nextFaunaStateCleanupPlayTime = currentPlayTime + FaunaStateCleanupInterval;
            _faunaRemovalBuffer.Clear();

            Dictionary<long, FaunaSpawnState>.Enumerator enumerator = _faunaSpawnStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<long, FaunaSpawnState> pair = enumerator.Current;
                if (pair.Value.blocked)
                    continue;

                if (pair.Value.cooldownUntilPlayTime > currentPlayTime)
                    continue;

                _faunaRemovalBuffer.Add(pair.Key);
            }
            enumerator.Dispose();

            for (int i = 0; i < _faunaRemovalBuffer.Count; i++)
                _faunaSpawnStates.Remove(_faunaRemovalBuffer[i]);

            if (_faunaRemovalBuffer.Count > 0)
            {
                MarkDiagnosticsDirty();
                RefreshDiagnosticsIfNeeded(currentPlayTime);
            }
        }

        private void MarkDiagnosticsDirty()
        {
            _diagnosticsDirty = true;
        }

        private void RefreshDiagnosticsIfNeeded(float currentPlayTime)
        {
            if (!_diagnosticsDirty || currentPlayTime < _nextDiagnosticsRefreshPlayTime)
                return;

            UpdateDiagnostics(currentPlayTime);
        }

        private float GetCurrentPlayTimeSeconds()
        {
            return SaveManager.Instance != null
                ? SaveManager.Instance.CurrentPlayTimeSeconds
                : Time.realtimeSinceStartup;
        }

        private void UpdateDiagnostics()
        {
            UpdateDiagnostics(GetCurrentPlayTimeSeconds());
        }

        private void UpdateDiagnostics(float currentPlayTime)
        {
            _debugCurrentPlayTime = currentPlayTime;
            _debugSuppressedPlacementCount = _suppressedPlacementKeys != null ? _suppressedPlacementKeys.Count : 0;
            _debugFaunaStateCount = _faunaSpawnStates != null ? _faunaSpawnStates.Count : 0;
            _debugBlockedFaunaCount = 0;
            _debugLargeThreatFaunaStateCount = 0;

            if (_faunaSpawnStates == null)
                return;

            Dictionary<long, FaunaSpawnState>.Enumerator enumerator = _faunaSpawnStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<long, FaunaSpawnState> pair = enumerator.Current;
                if (pair.Value.blocked)
                    _debugBlockedFaunaCount++;
                if (pair.Value.isLargeThreatZone)
                    _debugLargeThreatFaunaStateCount++;
            }
            enumerator.Dispose();

            _diagnosticsDirty = false;
            _nextDiagnosticsRefreshPlayTime = currentPlayTime + DiagnosticsRefreshInterval;
        }

        private void RecordPlacementStateChange(string reason, long runtimeKey)
        {
            _debugLastPlacementStateChangeReason = string.IsNullOrEmpty(reason) ? "unknown" : reason;
            _debugLastPlacementStateChangeRuntimeKey = runtimeKey;
        }
    }
}
