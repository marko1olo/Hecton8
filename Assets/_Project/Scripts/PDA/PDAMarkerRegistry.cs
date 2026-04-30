using System;
using System.Collections.Generic;
using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.PDA
{
    /// <summary>
    /// Marker icon contract for PDA map and HUD overlays.
    /// </summary>
    public enum MarkerIconType : byte
    {
        Generic = 0,
        Resource = 1,
        Hazard = 2,
        Shelter = 3,
        Objective = 4,
        Vehicle = 5,
        Beacon = 6
    }

    /// <summary>
    /// Immutable marker snapshot for map and HUD consumers.
    /// </summary>
    public readonly struct PDAMarkerSnapshot
    {
        public PDAMarkerSnapshot(string markerId, string title, Vector3 position, MarkerIconType iconType, bool visibleOnHud)
        {
            MarkerId = markerId ?? string.Empty;
            Title = title ?? string.Empty;
            Position = position;
            IconType = iconType;
            VisibleOnHud = visibleOnHud;
        }

        /// <summary>Stable marker identifier persisted in save data.</summary>
        public string MarkerId { get; }

        /// <summary>User-authored marker label.</summary>
        public string Title { get; }

        /// <summary>World-space target position.</summary>
        public Vector3 Position { get; }

        /// <summary>Icon classification used by map and HUD presenters.</summary>
        public MarkerIconType IconType { get; }

        /// <summary>True while the marker should be mirrored into HUD overlays.</summary>
        public bool VisibleOnHud { get; }
    }

    /// <summary>
    /// Save-backed registry for user-authored PDA map markers.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/PDA/PDA Marker Registry")]
    public sealed class PDAMarkerRegistry : MonoBehaviour, ISaveable
    {
        private struct MarkerRecord
        {
            public string markerId;
            public string title;
            public Vector3 position;
            public MarkerIconType iconType;
            public bool visibleOnHud;
        }

        // COLD ALLOC: List<MarkerRecord>[32] - runtime PDA marker store - owner: PDAMarkerRegistry
        private readonly List<MarkerRecord> _markers = new List<MarkerRecord>(32);
        // COLD ALLOC: Dictionary<string,int>[32] - marker lookup table - owner: PDAMarkerRegistry
        private readonly Dictionary<string, int> _markerIndexById = new Dictionary<string, int>(32, StringComparer.Ordinal);
        private bool _registeredToSave;
        private int _nextSequence = 1;

        /// <summary>Live singleton instance for PDA marker consumers.</summary>
        public static PDAMarkerRegistry Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        /// <summary>Raised after the marker collection changes.</summary>
        public event Action MarkersChanged;

        /// <summary>Current number of persisted markers.</summary>
        public int MarkerCount => _markers.Count;

        /// <inheritdoc />
        public int SavePriority => 210;

        /// <inheritdoc />
        public int LoadPriority => 210;

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
        }

        private void Start()
        {
            TryRegisterWithSaveManager();
        }

        private void OnDisable()
        {
            UnregisterFromSaveManager();

            if (Instance == this)
                Instance = null;
        }

        private void OnDestroy()
        {
            UnregisterFromSaveManager();

            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Creates a new user-authored map marker.
        /// </summary>
        public bool TryCreateMarker(Vector3 position, MarkerIconType iconType, string title, out PDAMarkerSnapshot marker)
        {
            if (_markers.Count >= PDAMarkerRegistryDTO.MaxEntries)
            {
                marker = default;
                return false;
            }

            string trimmedTitle = string.IsNullOrWhiteSpace(title) ? BuildDefaultTitle(iconType) : title.Trim();
            string markerId = BuildNextMarkerId();
            MarkerRecord record = new MarkerRecord
            {
                markerId = markerId,
                title = trimmedTitle,
                position = position,
                iconType = iconType,
                visibleOnHud = true
            };

            _markerIndexById[markerId] = _markers.Count;
            _markers.Add(record);
            marker = ToSnapshot(record);
            Hecton8.UI.PDAEvents.RaiseMarkerChanged(_markers.Count);
            MarkersChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Removes an existing PDA marker by stable identifier.
        /// </summary>
        public bool RemoveMarker(string markerId)
        {
            if (string.IsNullOrWhiteSpace(markerId) || !_markerIndexById.TryGetValue(markerId, out int markerIndex))
                return false;

            int lastIndex = _markers.Count - 1;
            MarkerRecord lastRecord = _markers[lastIndex];
            _markers.RemoveAt(lastIndex);
            _markerIndexById.Remove(markerId);

            if (markerIndex < _markers.Count)
            {
                _markers[markerIndex] = lastRecord;
                _markerIndexById[lastRecord.markerId] = markerIndex;
            }

            Hecton8.UI.PDAEvents.RaiseMarkerChanged(_markers.Count);
            MarkersChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Updates the world-space position of an existing marker.
        /// </summary>
        public bool UpdateMarkerPosition(string markerId, Vector3 position)
        {
            if (string.IsNullOrWhiteSpace(markerId) || !_markerIndexById.TryGetValue(markerId, out int markerIndex))
                return false;

            MarkerRecord record = _markers[markerIndex];
            record.position = position;
            _markers[markerIndex] = record;
            Hecton8.UI.PDAEvents.RaiseMarkerChanged(_markers.Count);
            MarkersChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Updates the HUD visibility state of an existing marker.
        /// </summary>
        public bool SetMarkerHudVisibility(string markerId, bool visibleOnHud)
        {
            if (string.IsNullOrWhiteSpace(markerId) || !_markerIndexById.TryGetValue(markerId, out int markerIndex))
                return false;

            MarkerRecord record = _markers[markerIndex];
            if (record.visibleOnHud == visibleOnHud)
                return true;

            record.visibleOnHud = visibleOnHud;
            _markers[markerIndex] = record;
            Hecton8.UI.PDAEvents.RaiseMarkerChanged(_markers.Count);
            MarkersChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Copies marker snapshots into a caller-owned buffer.
        /// </summary>
        public int CopyMarkers(PDAMarkerSnapshot[] buffer, bool hudOnly)
        {
            if (buffer == null || buffer.Length == 0 || _markers.Count == 0)
                return 0;

            int count = 0;
            for (int i = 0; i < _markers.Count && count < buffer.Length; i++)
            {
                MarkerRecord record = _markers[i];
                if (hudOnly && !record.visibleOnHud)
                    continue;

                buffer[count] = ToSnapshot(record);
                count++;
            }

            return count;
        }

        /// <summary>
        /// Returns the closest visible HUD marker to the supplied world-space point.
        /// </summary>
        public bool TryGetNearestVisibleHudMarker(Vector3 origin, out PDAMarkerSnapshot marker, out float distance)
        {
            marker = default;
            distance = 0f;

            float bestDistanceSqr = float.MaxValue;
            bool found = false;
            for (int i = 0; i < _markers.Count; i++)
            {
                MarkerRecord candidate = _markers[i];
                if (!candidate.visibleOnHud)
                    continue;

                float distanceSqr = (candidate.position - origin).sqrMagnitude;
                if (distanceSqr >= bestDistanceSqr)
                    continue;

                bestDistanceSqr = distanceSqr;
                marker = ToSnapshot(candidate);
                found = true;
            }

            if (!found)
                return false;

            distance = Mathf.Sqrt(bestDistanceSqr);
            return true;
        }

        /// <inheritdoc />
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.pdaMarkers.EnsureCapacity();
            data.pdaMarkers.markerCount = Mathf.Min(_markers.Count, PDAMarkerRegistryDTO.MaxEntries);
            data.pdaMarkers.nextSequence = Mathf.Max(1, _nextSequence);

            for (int i = 0; i < data.pdaMarkers.markerCount; i++)
            {
                MarkerRecord record = _markers[i];
                PDAMarkerEntryDTO entry = new PDAMarkerEntryDTO
                {
                    markerId = record.markerId,
                    title = record.title,
                    iconType = (int)record.iconType,
                    visibleOnHud = record.visibleOnHud
                };
                entry.SetPosition(record.position);
                data.pdaMarkers.entries[i] = entry;
            }

            for (int i = data.pdaMarkers.markerCount; i < PDAMarkerRegistryDTO.MaxEntries; i++)
                data.pdaMarkers.entries[i] = default;
        }

        /// <inheritdoc />
        public void LoadFromSaveData(SaveData data)
        {
            _markers.Clear();
            _markerIndexById.Clear();
            _nextSequence = 1;

            if (data == null)
                return;

            PDAMarkerRegistryDTO dto = data.pdaMarkers;
            int markerCount = Mathf.Clamp(dto.markerCount, 0, dto.entries != null ? dto.entries.Length : 0);
            for (int i = 0; i < markerCount; i++)
            {
                PDAMarkerEntryDTO entry = dto.entries[i];
                if (string.IsNullOrWhiteSpace(entry.markerId))
                    continue;

                MarkerRecord record = new MarkerRecord
                {
                    markerId = entry.markerId,
                    title = string.IsNullOrWhiteSpace(entry.title) ? BuildDefaultTitle((MarkerIconType)entry.iconType) : entry.title,
                    position = entry.GetPosition(),
                    iconType = (MarkerIconType)Mathf.Clamp(entry.iconType, 0, (int)MarkerIconType.Beacon),
                    visibleOnHud = entry.visibleOnHud
                };

                _markerIndexById[record.markerId] = _markers.Count;
                _markers.Add(record);
            }

            _nextSequence = Mathf.Max(1, dto.nextSequence);
            MarkersChanged?.Invoke();
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

        private string BuildNextMarkerId()
        {
            string markerId = $"pda_marker_{_nextSequence:0000}";
            _nextSequence++;
            return markerId;
        }

        private static string BuildDefaultTitle(MarkerIconType iconType)
        {
            switch (iconType)
            {
                case MarkerIconType.Resource:
                    return "RESOURCE MARKER";
                case MarkerIconType.Hazard:
                    return "HAZARD MARKER";
                case MarkerIconType.Shelter:
                    return "SHELTER MARKER";
                case MarkerIconType.Objective:
                    return "OBJECTIVE MARKER";
                case MarkerIconType.Vehicle:
                    return "VEHICLE MARKER";
                case MarkerIconType.Beacon:
                    return "BEACON MARKER";
                default:
                    return "PDA MARKER";
            }
        }

        private static PDAMarkerSnapshot ToSnapshot(MarkerRecord record)
        {
            return new PDAMarkerSnapshot(record.markerId, record.title, record.position, record.iconType, record.visibleOnHud);
        }
    }
}
