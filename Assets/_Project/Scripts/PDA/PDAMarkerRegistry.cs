using Hecton8.Core;
using Hecton8.Quest;
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
        public PDAMarkerSnapshot(uint markerHashId, string markerId, uint titleHashId, string title, Vector3 position, in AbsoluteUniversePosition positionAup, MarkerIconType iconType, bool visibleOnHud)
        {
            MarkerHashID = markerHashId;
            MarkerId = markerId ?? string.Empty;
            TitleHashID = titleHashId;
            Title = title ?? string.Empty;
            Position = position;
            PositionAup = positionAup;
            IconType = iconType;
            VisibleOnHud = visibleOnHud;
        }

        /// <summary>Stable hashed marker identifier used by native PDA payloads.</summary>
        public uint MarkerHashID { get; }

        /// <summary>Stable marker identifier persisted in save data.</summary>
        public string MarkerId { get; }

        /// <summary>Stable hashed title identifier used by hot UI diffing.</summary>
        public uint TitleHashID { get; }

        /// <summary>User-authored marker label.</summary>
        public string Title { get; }

        /// <summary>World-space target position.</summary>
        public Vector3 Position { get; }

        /// <summary>Absolute universe target position used for long-range marker math.</summary>
        public AbsoluteUniversePosition PositionAup { get; }

        /// <summary>Icon classification used by map and HUD presenters.</summary>
        public MarkerIconType IconType { get; }

        /// <summary>True while the marker should be mirrored into HUD overlays.</summary>
        public bool VisibleOnHud { get; }

        /// <summary>
        /// Copies the marker title into a caller-owned buffer for zero-GC TMP writes.
        /// </summary>
        public int CopyTitleTo(char[] buffer)
        {
            if (buffer == null || buffer.Length == 0 || string.IsNullOrEmpty(Title))
                return 0;

            int copyLength = math.min(buffer.Length, Title.Length);
            Title.CopyTo(0, buffer, 0, copyLength);
            return copyLength;
        }
    }

    /// <summary>
    /// Save-backed registry for user-authored PDA map markers.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/PDA/PDA Marker Registry")]
    public sealed class PDAMarkerRegistry : MonoBehaviour, ISaveable, IOriginShiftListener
    {
        private const uint MarkerFlagVisibleOnHud = 1u << 0;

        private struct MarkerRecord
        {
            public uint markerHashId;
            public string markerId;
            public uint titleHashId;
            public string title;
            public AbsoluteUniversePosition positionAup;
            public Vector3 runtimePosition;
            public MarkerIconType iconType;
            public uint flags;
        }

        // COLD ALLOC: MarkerRecord[MaxEntries] - fixed PDA marker store without List/Dictionary churn - owner: PDAMarkerRegistry
        private readonly MarkerRecord[] _markers = new MarkerRecord[PDAMarkerRegistryDTO.MaxEntries];
        private bool _registeredToSave;
        private bool _serviceRegistered;
        private int _markerCount;
        private int _nextSequence = 1;

        /// <summary>Current number of persisted markers.</summary>
        public int MarkerCount => _markerCount;

        /// <inheritdoc />
        public int SavePriority => 210;

        /// <inheritdoc />
        public int LoadPriority => 210;

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
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterService();
            UnregisterFromSaveManager();
        }

        /// <summary>
        /// Creates a new user-authored map marker.
        /// </summary>
        public bool TryCreateMarker(Vector3 position, MarkerIconType iconType, string title, out PDAMarkerSnapshot marker)
        {
            if (_markerCount >= PDAMarkerRegistryDTO.MaxEntries)
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
                titleHashId = ComputeMarkerHash(trimmedTitle),
                title = trimmedTitle,
                positionAup = positionAup,
                runtimePosition = position,
                iconType = iconType,
                flags = MarkerFlagVisibleOnHud
            };

            _markers[_markerCount++] = record;
            marker = ToSnapshot(record);
            Hecton8.UI.PDAEvents.RaiseMarkerChanged(record.markerHashId, _markerCount);
            return true;
        }

        /// <summary>
        /// Creates or updates a system-authored marker with a stable persisted identifier.
        /// </summary>
        public bool TryCreateOrUpdateMarker(string markerId, Vector3 position, MarkerIconType iconType, string title, out PDAMarkerSnapshot marker)
        {
            uint markerHashId = ComputeMarkerHash(markerId);
            if (markerHashId == 0u)
            {
                marker = default;
                return false;
            }

            return TryCreateOrUpdateMarker(markerHashId, markerId, position, iconType, title, out marker);
        }

        /// <summary>
        /// Creates or updates a system-authored marker through its pre-resolved stable hash.
        /// </summary>
        public bool TryCreateOrUpdateMarker(uint markerHashId, string markerId, Vector3 position, MarkerIconType iconType, string title, out PDAMarkerSnapshot marker)
        {
            string trimmedTitle = string.IsNullOrWhiteSpace(title) ? BuildDefaultTitle(iconType) : title.Trim();
            return TryCreateOrUpdateMarker(
                markerHashId,
                markerId,
                position,
                iconType,
                ComputeMarkerHash(trimmedTitle),
                trimmedTitle,
                out marker);
        }

        /// <summary>
        /// Creates or updates a system-authored marker through pre-resolved stable hashes.
        /// </summary>
        public bool TryCreateOrUpdateMarker(uint markerHashId, Vector3 position, MarkerIconType iconType, uint titleHashId, string title, out PDAMarkerSnapshot marker)
        {
            return TryCreateOrUpdateMarker(markerHashId, string.Empty, position, iconType, titleHashId, title, out marker);
        }

        /// <summary>
        /// Creates or updates a system-authored marker through pre-resolved stable hashes and AUP source coordinates.
        /// </summary>
        public bool TryCreateOrUpdateMarker(uint markerHashId, in AbsoluteUniversePosition positionAup, MarkerIconType iconType, uint titleHashId, string title, out PDAMarkerSnapshot marker)
        {
            return TryCreateOrUpdateMarker(markerHashId, string.Empty, in positionAup, iconType, titleHashId, title, out marker);
        }

        private bool TryCreateOrUpdateMarker(uint markerHashId, string markerId, Vector3 position, MarkerIconType iconType, uint titleHashId, string title, out PDAMarkerSnapshot marker)
        {
            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(position);
            return TryCreateOrUpdateMarker(markerHashId, markerId, position, in positionAup, iconType, titleHashId, title, out marker);
        }

        private bool TryCreateOrUpdateMarker(uint markerHashId, string markerId, in AbsoluteUniversePosition positionAup, MarkerIconType iconType, uint titleHashId, string title, out PDAMarkerSnapshot marker)
        {
            Vector3 runtimePosition = ToRuntimePosition(in positionAup);
            return TryCreateOrUpdateMarker(markerHashId, markerId, runtimePosition, in positionAup, iconType, titleHashId, title, out marker);
        }

        private bool TryCreateOrUpdateMarker(uint markerHashId, string markerId, Vector3 runtimePosition, in AbsoluteUniversePosition positionAup, MarkerIconType iconType, uint titleHashId, string title, out PDAMarkerSnapshot marker)
        {
            marker = default;
            if (markerHashId == 0u)
                return false;

            string stableTitle = string.IsNullOrWhiteSpace(title) ? BuildDefaultTitle(iconType) : title;
            uint stableTitleHash = titleHashId != 0u ? titleHashId : ComputeMarkerHash(stableTitle);
            if (TryFindMarkerIndex(markerHashId, out int markerIndex))
            {
                MarkerRecord existing = _markers[markerIndex];
                existing.markerId = markerId ?? existing.markerId;
                existing.titleHashId = stableTitleHash;
                existing.title = stableTitle;
                existing.positionAup = positionAup;
                existing.runtimePosition = runtimePosition;
                existing.iconType = iconType;
                SetVisibleOnHud(ref existing, true);
                _markers[markerIndex] = existing;
                marker = ToSnapshot(existing);
                Hecton8.UI.PDAEvents.RaiseMarkerChanged(existing.markerHashId, _markerCount);
                return true;
            }

            if (_markerCount >= PDAMarkerRegistryDTO.MaxEntries)
                return false;

            MarkerRecord record = new MarkerRecord
            {
                markerHashId = markerHashId,
                markerId = markerId ?? string.Empty,
                titleHashId = stableTitleHash,
                title = stableTitle,
                positionAup = positionAup,
                runtimePosition = runtimePosition,
                iconType = iconType,
                flags = MarkerFlagVisibleOnHud
            };

            _markers[_markerCount++] = record;
            marker = ToSnapshot(record);
            Hecton8.UI.PDAEvents.RaiseMarkerChanged(record.markerHashId, _markerCount);
            return true;
        }

        /// <summary>
        /// Removes an existing PDA marker by stable identifier.
        /// </summary>
        public bool RemoveMarker(string markerId)
        {
            return RemoveMarker(ComputeMarkerHash(markerId));
        }

        /// <summary>
        /// Removes an existing PDA marker by stable hash.
        /// </summary>
        public bool RemoveMarker(uint markerHashId)
        {
            if (markerHashId == 0u || !TryFindMarkerIndex(markerHashId, out int markerIndex))
                return false;

            MarkerRecord removedRecord = _markers[markerIndex];
            int lastIndex = _markerCount - 1;
            MarkerRecord lastRecord = _markers[lastIndex];
            _markers[lastIndex] = default;
            _markerCount = lastIndex;

            if (markerIndex < _markerCount)
                _markers[markerIndex] = lastRecord;

            Hecton8.UI.PDAEvents.RaiseMarkerChanged(removedRecord.markerHashId, _markerCount);
            return true;
        }

        /// <summary>
        /// Updates the world-space position of an existing marker.
        /// </summary>
        public bool UpdateMarkerPosition(string markerId, Vector3 position)
        {
            return UpdateMarkerPosition(ComputeMarkerHash(markerId), position);
        }

        /// <summary>
        /// Updates the world-space position of an existing marker by stable hash.
        /// </summary>
        public bool UpdateMarkerPosition(uint markerHashId, Vector3 position)
        {
            if (markerHashId == 0u || !TryFindMarkerIndex(markerHashId, out int markerIndex))
                return false;

            MarkerRecord record = _markers[markerIndex];
            record.positionAup = AbsoluteUniversePosition.FromRuntimePosition(position);
            record.runtimePosition = position;
            _markers[markerIndex] = record;
            Hecton8.UI.PDAEvents.RaiseMarkerChanged(record.markerHashId, _markerCount);
            return true;
        }

        /// <summary>
        /// Updates the HUD visibility state of an existing marker.
        /// </summary>
        public bool SetMarkerHudVisibility(string markerId, bool visibleOnHud)
        {
            return SetMarkerHudVisibility(ComputeMarkerHash(markerId), visibleOnHud);
        }

        /// <summary>
        /// Updates HUD visibility through the stable marker hash.
        /// </summary>
        public bool SetMarkerHudVisibility(uint markerHashId, bool visibleOnHud)
        {
            if (markerHashId == 0u || !TryFindMarkerIndex(markerHashId, out int markerIndex))
                return false;

            MarkerRecord record = _markers[markerIndex];
            if (IsVisibleOnHud(in record) == visibleOnHud)
                return true;

            SetVisibleOnHud(ref record, visibleOnHud);
            _markers[markerIndex] = record;
            Hecton8.UI.PDAEvents.RaiseMarkerChanged(record.markerHashId, _markerCount);
            return true;
        }

        /// <summary>
        /// Resolves a native marker hash into a current snapshot.
        /// </summary>
        public bool TryGetMarkerByHash(uint markerHashId, out PDAMarkerSnapshot marker)
        {
            marker = default;
            if (markerHashId == 0u || !TryFindMarkerIndex(markerHashId, out int markerIndex))
                return false;

            if ((uint)markerIndex >= (uint)_markerCount)
                return false;

            marker = ToSnapshot(_markers[markerIndex]);
            return true;
        }

        /// <summary>
        /// Copies marker snapshots into a caller-owned buffer.
        /// </summary>
        public int CopyMarkers(PDAMarkerSnapshot[] buffer, bool hudOnly)
        {
            if (buffer == null || buffer.Length == 0 || _markerCount == 0)
                return 0;

            int count = 0;
            for (int i = 0; i < _markerCount && count < buffer.Length; i++)
            {
                MarkerRecord record = _markers[i];
                if (hudOnly && !IsVisibleOnHud(in record))
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
            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
            return TryGetNearestVisibleHudMarker(in originAup, out marker, out distance);
        }

        /// <summary>
        /// Returns the closest visible HUD marker to the supplied absolute universe point.
        /// </summary>
        public bool TryGetNearestVisibleHudMarker(in AbsoluteUniversePosition originAup, out PDAMarkerSnapshot marker, out float distance)
        {
            marker = default;
            distance = 0f;

            double bestDistanceSqr = double.MaxValue;
            bool found = false;
            for (int i = 0; i < _markerCount; i++)
            {
                MarkerRecord candidate = _markers[i];
                if (!IsVisibleOnHud(in candidate))
                    continue;

                double distanceSqr = AbsoluteUniversePosition.DistanceSq(in candidate.positionAup, in originAup);
                if (distanceSqr >= bestDistanceSqr)
                    continue;

                bestDistanceSqr = distanceSqr;
                marker = ToSnapshot(candidate);
                found = true;
            }

            if (!found)
                return false;

            if (bestDistanceSqr <= 0.000001d)
            {
                distance = 0f;
                return true;
            }

            float bestDistanceSqrFloat = (float)math.min(bestDistanceSqr, (double)float.MaxValue);
            distance = bestDistanceSqrFloat * math.rsqrt(bestDistanceSqrFloat);
            return true;
        }

        /// <inheritdoc />
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.pdaMarkers.EnsureCapacity();
            data.pdaMarkers.markerCount = math.min(_markerCount, PDAMarkerRegistryDTO.MaxEntries);
            data.pdaMarkers.nextSequence = math.max(1, _nextSequence);

            for (int i = 0; i < data.pdaMarkers.markerCount; i++)
            {
                MarkerRecord record = _markers[i];
                PDAMarkerEntryDTO entry = new PDAMarkerEntryDTO
                {
                    markerHashId = record.markerHashId,
                    markerId = record.markerId,
                    titleHashId = record.titleHashId,
                    title = record.title,
                    iconType = (int)record.iconType,
                    visibleOnHud = IsVisibleOnHud(in record)
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
            ClearMarkers();
            _nextSequence = 1;

            if (data == null)
                return;

            PDAMarkerRegistryDTO dto = data.pdaMarkers;
            int markerCount = math.clamp(dto.markerCount, 0, dto.entries != null ? dto.entries.Length : 0);
            for (int i = 0; i < markerCount; i++)
            {
                PDAMarkerEntryDTO entry = dto.entries[i];
                uint markerHashId = entry.markerHashId != 0u
                    ? entry.markerHashId
                    : ComputeMarkerHash(entry.markerId);
                if (markerHashId == 0u)
                    continue;

                string stableTitle = string.IsNullOrWhiteSpace(entry.title) ? BuildDefaultTitle((MarkerIconType)entry.iconType) : entry.title;
                AbsoluteUniversePosition positionAup = entry.HasAupPosition
                    ? entry.GetAup()
                    : AbsoluteUniversePosition.FromRuntimePosition(entry.GetPosition());
                MarkerRecord record = new MarkerRecord
                {
                    markerHashId = markerHashId,
                    markerId = entry.markerId ?? string.Empty,
                    titleHashId = entry.titleHashId != 0u
                        ? entry.titleHashId
                        : ComputeMarkerHash(stableTitle),
                    title = stableTitle,
                    positionAup = positionAup,
                    runtimePosition = ToRuntimePosition(in positionAup),
                    iconType = (MarkerIconType)math.clamp(entry.iconType, 0, (int)MarkerIconType.Beacon),
                    flags = ResolveMarkerFlags(entry.visibleOnHud)
                };

                if (_markerCount >= PDAMarkerRegistryDTO.MaxEntries)
                    break;

                if (TryFindMarkerIndex(record.markerHashId, out _))
                    continue;

                _markers[_markerCount++] = record;
            }

            _nextSequence = math.max(1, dto.nextSequence);
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (_markerCount == 0)
                return;

            for (int i = 0; i < _markerCount; i++)
            {
                MarkerRecord record = _markers[i];
                record.runtimePosition = ToRuntimePosition(in record.positionAup);
                _markers[i] = record;
                Hecton8.UI.PDAEvents.RaiseMarkerChanged(record.markerHashId, _markerCount);
            }
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
            if (_serviceRegistered || !Application.isPlaying)
                return;

            PDAMarkerRegistry registeredRuntime = Hecton8.Core.GlobalRegistry.PDAMarkers;
            if (registeredRuntime != null && !ReferenceEquals(registeredRuntime, this))
            {
                Destroy(this);
                return;
            }

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
            int sequence = _nextSequence;
            _nextSequence++;
            return string.Create(15, sequence, static (buffer, value) =>
            {
                buffer[0] = 'p';
                buffer[1] = 'd';
                buffer[2] = 'a';
                buffer[3] = '_';
                buffer[4] = 'm';
                buffer[5] = 'a';
                buffer[6] = 'r';
                buffer[7] = 'k';
                buffer[8] = 'e';
                buffer[9] = 'r';
                buffer[10] = '_';
                int clamped = math.clamp(value, 0, 9999);
                buffer[14] = (char)('0' + clamped % 10);
                clamped /= 10;
                buffer[13] = (char)('0' + clamped % 10);
                clamped /= 10;
                buffer[12] = (char)('0' + clamped % 10);
                clamped /= 10;
                buffer[11] = (char)('0' + clamped % 10);
            });
        }

        private static uint ComputeMarkerHash(string markerId)
        {
            return !string.IsNullOrWhiteSpace(markerId)
                ? QuestFlagHashKernel.ComputeStableHash(markerId)
                : 0u;
        }

        private bool TryFindMarkerIndex(uint markerHashId, out int markerIndex)
        {
            markerIndex = -1;
            if (markerHashId == 0u)
                return false;

            for (int i = 0; i < _markerCount; i++)
            {
                if (_markers[i].markerHashId != markerHashId)
                    continue;

                markerIndex = i;
                return true;
            }

            return false;
        }

        private void ClearMarkers()
        {
            for (int i = 0; i < _markerCount; i++)
                _markers[i] = default;

            _markerCount = 0;
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
            return new PDAMarkerSnapshot(record.markerHashId, record.markerId, record.titleHashId, record.title, record.runtimePosition, in record.positionAup, record.iconType, IsVisibleOnHud(in record));
        }

        private static bool IsVisibleOnHud(in MarkerRecord record)
        {
            return (record.flags & MarkerFlagVisibleOnHud) != 0u;
        }

        private static uint ResolveMarkerFlags(bool visibleOnHud)
        {
            return visibleOnHud ? MarkerFlagVisibleOnHud : 0u;
        }

        private static void SetVisibleOnHud(ref MarkerRecord record, bool visibleOnHud)
        {
            if (visibleOnHud)
                record.flags |= MarkerFlagVisibleOnHud;
            else
                record.flags &= ~MarkerFlagVisibleOnHud;
        }

        private static Vector3 ToRuntimePosition(in AbsoluteUniversePosition position)
        {
            float3 runtime = position.ToRuntimeFloat3();
            return new Vector3(runtime.x, runtime.y, runtime.z);
        }
    }
}
