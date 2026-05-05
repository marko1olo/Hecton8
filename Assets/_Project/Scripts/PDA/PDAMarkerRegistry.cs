using System;
using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Mathematics;
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
        public PDAMarkerSnapshot(uint markerHashId, string markerId, string title, Vector3 position, MarkerIconType iconType, bool visibleOnHud)
        {
            MarkerHashID = markerHashId;
            MarkerId = markerId ?? string.Empty;
            Title = title ?? string.Empty;
            Position = position;
            IconType = iconType;
            VisibleOnHud = visibleOnHud;
        }

        /// <summary>Stable hashed marker identifier used by native PDA payloads.</summary>
        public uint MarkerHashID { get; }

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
    public sealed class PDAMarkerRegistry : MonoBehaviour, ISaveable, IOriginShiftListener
    {
        private struct MarkerRecord
        {
            public uint markerHashId;
            public string markerId;
            public string title;
            public AbsoluteUniversePosition positionAup;
            public Vector3 runtimePosition;
            public MarkerIconType iconType;
            public bool visibleOnHud;
        }

        // COLD ALLOC: List<MarkerRecord>[32] - runtime PDA marker store - owner: PDAMarkerRegistry
        private readonly List<MarkerRecord> _markers = new List<MarkerRecord>(32);
        // COLD ALLOC: Dictionary<string,int>[32] - marker lookup table - owner: PDAMarkerRegistry
        private readonly Dictionary<string, int> _markerIndexById = new Dictionary<string, int>(32, StringComparer.Ordinal);
        // COLD ALLOC: Dictionary<uint,int>[32] - hash lookup for native PDA marker events - owner: PDAMarkerRegistry
        private readonly Dictionary<uint, int> _markerIndexByHash = new Dictionary<uint, int>(32);
        private bool _registeredToSave;
        private bool _serviceRegistered;
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
            TryRegisterService();
            HectonFloatingOrigin.RegisterListener(this);
        }

        private void Start()
        {
            TryRegisterWithSaveManager();
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterService();
            UnregisterFromSaveManager();

            if (Instance == this)
                Instance = null;
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterService();
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
            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(position);
            MarkerRecord record = new MarkerRecord
            {
                markerHashId = ComputeMarkerHash(markerId),
                markerId = markerId,
                title = trimmedTitle,
                positionAup = positionAup,
                runtimePosition = position,
                iconType = iconType,
                visibleOnHud = true
            };

            _markerIndexById[markerId] = _markers.Count;
            _markerIndexByHash[record.markerHashId] = _markers.Count;
            _markers.Add(record);
            marker = ToSnapshot(record);
            Hecton8.UI.PDAEvents.RaiseMarkerChanged(record.markerHashId, _markers.Count);
            MarkersChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Creates or updates a system-authored marker with a stable persisted identifier.
        /// </summary>
        public bool TryCreateOrUpdateMarker(string markerId, Vector3 position, MarkerIconType iconType, string title, out PDAMarkerSnapshot marker)
        {
            marker = default;
            if (string.IsNullOrWhiteSpace(markerId))
                return false;

            string trimmedTitle = string.IsNullOrWhiteSpace(title) ? BuildDefaultTitle(iconType) : title.Trim();
            if (_markerIndexById.TryGetValue(markerId, out int markerIndex))
            {
                MarkerRecord existing = _markers[markerIndex];
                existing.title = trimmedTitle;
                existing.positionAup = AbsoluteUniversePosition.FromRuntimePosition(position);
                existing.runtimePosition = position;
                existing.iconType = iconType;
                existing.visibleOnHud = true;
                _markers[markerIndex] = existing;
                marker = ToSnapshot(existing);
                Hecton8.UI.PDAEvents.RaiseMarkerChanged(existing.markerHashId, _markers.Count);
                MarkersChanged?.Invoke();
                return true;
            }

            if (_markers.Count >= PDAMarkerRegistryDTO.MaxEntries)
                return false;

            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(position);
            MarkerRecord record = new MarkerRecord
            {
                markerHashId = ComputeMarkerHash(markerId),
                markerId = markerId,
                title = trimmedTitle,
                positionAup = positionAup,
                runtimePosition = position,
                iconType = iconType,
                visibleOnHud = true
            };

            _markerIndexById[markerId] = _markers.Count;
            _markerIndexByHash[record.markerHashId] = _markers.Count;
            _markers.Add(record);
            marker = ToSnapshot(record);
            Hecton8.UI.PDAEvents.RaiseMarkerChanged(record.markerHashId, _markers.Count);
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

            MarkerRecord removedRecord = _markers[markerIndex];
            int lastIndex = _markers.Count - 1;
            MarkerRecord lastRecord = _markers[lastIndex];
            _markers.RemoveAt(lastIndex);
            _markerIndexById.Remove(markerId);
            _markerIndexByHash.Remove(removedRecord.markerHashId);

            if (markerIndex < _markers.Count)
            {
                _markers[markerIndex] = lastRecord;
                _markerIndexById[lastRecord.markerId] = markerIndex;
                _markerIndexByHash[lastRecord.markerHashId] = markerIndex;
            }

            Hecton8.UI.PDAEvents.RaiseMarkerChanged(removedRecord.markerHashId, _markers.Count);
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
            record.positionAup = AbsoluteUniversePosition.FromRuntimePosition(position);
            record.runtimePosition = position;
            _markers[markerIndex] = record;
            Hecton8.UI.PDAEvents.RaiseMarkerChanged(record.markerHashId, _markers.Count);
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
            Hecton8.UI.PDAEvents.RaiseMarkerChanged(record.markerHashId, _markers.Count);
            MarkersChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Resolves a native marker hash into a current snapshot.
        /// </summary>
        public bool TryGetMarkerByHash(uint markerHashId, out PDAMarkerSnapshot marker)
        {
            marker = default;
            if (markerHashId == 0u || !_markerIndexByHash.TryGetValue(markerHashId, out int markerIndex))
                return false;

            if ((uint)markerIndex >= (uint)_markers.Count)
                return false;

            marker = ToSnapshot(_markers[markerIndex]);
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

                float distanceSqr = (candidate.runtimePosition - origin).sqrMagnitude;
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
                entry.SetPosition(record.runtimePosition);
                entry.SetAup(in record.positionAup);
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
            _markerIndexByHash.Clear();
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

                AbsoluteUniversePosition positionAup = entry.HasAupPosition
                    ? entry.GetAup()
                    : AbsoluteUniversePosition.FromRuntimePosition(entry.GetPosition());
                MarkerRecord record = new MarkerRecord
                {
                    markerHashId = ComputeMarkerHash(entry.markerId),
                    markerId = entry.markerId,
                    title = string.IsNullOrWhiteSpace(entry.title) ? BuildDefaultTitle((MarkerIconType)entry.iconType) : entry.title,
                    positionAup = positionAup,
                    runtimePosition = ToRuntimePosition(in positionAup),
                    iconType = (MarkerIconType)Mathf.Clamp(entry.iconType, 0, (int)MarkerIconType.Beacon),
                    visibleOnHud = entry.visibleOnHud
                };

                _markerIndexById[record.markerId] = _markers.Count;
                _markerIndexByHash[record.markerHashId] = _markers.Count;
                _markers.Add(record);
            }

            _nextSequence = Mathf.Max(1, dto.nextSequence);
            MarkersChanged?.Invoke();
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (_markers.Count == 0)
                return;

            for (int i = 0; i < _markers.Count; i++)
            {
                MarkerRecord record = _markers[i];
                record.runtimePosition = ToRuntimePosition(in record.positionAup);
                _markers[i] = record;
                Hecton8.UI.PDAEvents.RaiseMarkerChanged(record.markerHashId, _markers.Count);
            }

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

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying || Instance != this)
                return;

            Hecton8.Core.GlobalRegistry.RegisterPDAMarkerRuntime(this);
            _serviceRegistered = ReferenceEquals(Hecton8.Core.GlobalRegistry.PDAMarkers, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            Hecton8.Core.GlobalRegistry.UnregisterPDAMarkerRuntime(this);
            _serviceRegistered = false;
        }

        private string BuildNextMarkerId()
        {
            string markerId = $"pda_marker_{_nextSequence:0000}";
            _nextSequence++;
            return markerId;
        }

        private static uint ComputeMarkerHash(string markerId)
        {
            return !string.IsNullOrWhiteSpace(markerId)
                ? unchecked((uint)LocHash.Compute(markerId))
                : 0u;
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
            return new PDAMarkerSnapshot(record.markerHashId, record.markerId, record.title, record.runtimePosition, record.iconType, record.visibleOnHud);
        }

        private static Vector3 ToRuntimePosition(in AbsoluteUniversePosition position)
        {
            float3 runtime = position.ToRuntimeFloat3();
            return new Vector3(runtime.x, runtime.y, runtime.z);
        }
    }
}
