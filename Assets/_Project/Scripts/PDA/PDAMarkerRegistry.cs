using Hecton8.Core;
using Hecton8.Core.Contracts;
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
    public sealed class PDAMarkerRegistry : MonoBehaviour, ISaveable, IOriginShiftListener, IGlobalRegistryHotSwapListener
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
        private bool _registeredHotSwapListener;
        private bool _runtimeOwnerAborted;
        private int _markerCount;
        private int _nextSequence = 1;
        private uint _revision;
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;

        /// <summary>Current number of persisted markers.</summary>
        public int MarkerCount => _markerCount;

        /// <summary>Monotonic source-of-truth revision for marker UI consumers that miss a lossy PDA event.</summary>
        public uint Revision => _revision;

        /// <inheritdoc />
        public int SavePriority => 210;

        /// <inheritdoc />
        public int LoadPriority => 210;

        private void OnEnable()
        {
            if (_runtimeOwnerAborted || !TryRegisterService())
                return;

            _saveService = GlobalRegistry.Save;
            TryRegisterHotSwapListener();
            TryRegisterWithSaveManager();
            HectonFloatingOrigin.RegisterListener(this);
        }

        private void Start()
        {
            if (_runtimeOwnerAborted)
                return;

            TryRegisterHotSwapListener();
            TryRegisterWithSaveManager();
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterService();
            UnregisterFromSaveManager();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterService();
            UnregisterFromSaveManager();
            TryUnregisterHotSwapListener();
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

            string stableTitle = ResolveMarkerTitleOrFallback(title, iconType);
            string markerId = BuildNextMarkerId();
            if (!TryResolveAupFromRuntimeOrigin(position, out AbsoluteUniversePosition positionAup))
            {
                marker = default;
                return false;
            }

            MarkerRecord record = new MarkerRecord
            {
                markerHashId = ComputeMarkerHash(markerId),
                markerId = markerId,
                titleHashId = ComputeMarkerHash(stableTitle),
                title = stableTitle,
                positionAup = positionAup,
                runtimePosition = position,
                iconType = iconType,
                flags = MarkerFlagVisibleOnHud
            };

            _markers[_markerCount++] = record;
            marker = ToSnapshot(record);
            CommitMarkerRevision(record.markerHashId);
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
            string stableTitle = ResolveMarkerTitleOrFallback(title, iconType);
            return TryCreateOrUpdateMarker(
                markerHashId,
                markerId,
                position,
                iconType,
                ComputeMarkerHash(stableTitle),
                stableTitle,
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
        /// Creates or updates a system-authored marker through pre-resolved stable hashes with explicit HUD visibility.
        /// </summary>
        public bool TryCreateOrUpdateMarker(uint markerHashId, Vector3 position, MarkerIconType iconType, uint titleHashId, string title, bool visibleOnHud, out PDAMarkerSnapshot marker)
        {
            return TryCreateOrUpdateMarker(markerHashId, string.Empty, position, iconType, titleHashId, title, visibleOnHud, out marker);
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
            return TryCreateOrUpdateMarker(markerHashId, markerId, position, iconType, titleHashId, title, true, out marker);
        }

        private bool TryCreateOrUpdateMarker(uint markerHashId, string markerId, Vector3 position, MarkerIconType iconType, uint titleHashId, string title, bool visibleOnHud, out PDAMarkerSnapshot marker)
        {
            if (!TryResolveAupFromRuntimeOrigin(position, out AbsoluteUniversePosition positionAup))
            {
                marker = default;
                return false;
            }

            return TryCreateOrUpdateMarker(markerHashId, markerId, position, in positionAup, iconType, titleHashId, title, visibleOnHud, out marker);
        }

        private bool TryCreateOrUpdateMarker(uint markerHashId, string markerId, in AbsoluteUniversePosition positionAup, MarkerIconType iconType, uint titleHashId, string title, out PDAMarkerSnapshot marker)
        {
            if (!TryResolveRuntimePosition(in positionAup, out Vector3 runtimePosition))
            {
                marker = default;
                return false;
            }

            return TryCreateOrUpdateMarker(markerHashId, markerId, runtimePosition, in positionAup, iconType, titleHashId, title, true, out marker);
        }

        private bool TryCreateOrUpdateMarker(uint markerHashId, string markerId, Vector3 runtimePosition, in AbsoluteUniversePosition positionAup, MarkerIconType iconType, uint titleHashId, string title, bool visibleOnHud, out PDAMarkerSnapshot marker)
        {
            marker = default;
            if (markerHashId == 0u)
                return false;

            string stableTitle = ResolveMarkerTitleOrFallback(title, iconType);
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
                SetVisibleOnHud(ref existing, visibleOnHud);
                _markers[markerIndex] = existing;
                marker = ToSnapshot(existing);
                CommitMarkerRevision(existing.markerHashId);
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
                flags = visibleOnHud ? MarkerFlagVisibleOnHud : 0u
            };

            _markers[_markerCount++] = record;
            marker = ToSnapshot(record);
            CommitMarkerRevision(record.markerHashId);
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

            CommitMarkerRevision(removedRecord.markerHashId);
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

            if (!TryResolveAupFromRuntimeOrigin(position, out AbsoluteUniversePosition positionAup))
                return false;

            MarkerRecord record = _markers[markerIndex];
            record.positionAup = positionAup;
            record.runtimePosition = position;
            _markers[markerIndex] = record;
            CommitMarkerRevision(record.markerHashId);
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
            CommitMarkerRevision(record.markerHashId);
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
            if (!TryResolveAupFromRuntimeOrigin(origin, out AbsoluteUniversePosition originAup))
            {
                marker = default;
                distance = 0f;
                return false;
            }

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
                if (!IsFiniteNonNegativeDistanceSq(distanceSqr) ||
                    distanceSqr >= bestDistanceSqr)
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

            distance = ApproximateDistanceMetersFromSq(bestDistanceSqr);
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

            if (data != null)
            {
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
                    AbsoluteUniversePosition positionAup;
                    if (entry.HasAupPosition())
                    {
                        positionAup = entry.GetAup();
                    }
                    else if (!TryResolveAupFromRuntimeOrigin(entry.GetPosition(), out positionAup))
                    {
                        continue;
                    }

                    if (!TryResolveRuntimePosition(in positionAup, out Vector3 runtimePosition))
                        continue;

                    MarkerRecord record = new MarkerRecord
                    {
                        markerHashId = markerHashId,
                        markerId = entry.markerId ?? string.Empty,
                        titleHashId = entry.titleHashId != 0u
                            ? entry.titleHashId
                            : ComputeMarkerHash(stableTitle),
                        title = stableTitle,
                        positionAup = positionAup,
                        runtimePosition = runtimePosition,
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

            CommitMarkerRevision(0u);
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!IsFiniteVector(shiftOffset) || !math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= 0.000001f)
                return;

            if (_markerCount == 0)
                return;

            for (int i = 0; i < _markerCount; i++)
            {
                MarkerRecord record = _markers[i];
                if (TryResolveRuntimePosition(in record.positionAup, out Vector3 runtimePosition))
                {
                    record.runtimePosition = runtimePosition;
                }
                else
                {
                    SetVisibleOnHud(ref record, false);
                }

                _markers[i] = record;
            }

            CommitMarkerRevision(0u);
        }

        private void TryRegisterWithSaveManager()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_registeredToSave || !Application.isPlaying || !isActiveAndEnabled)
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
            _registeredToSave = true;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private void UnregisterFromSaveManager()
        {
            if (!_registeredToSave && _registeredSaveService == null)
                return;

            ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredSaveService = null;
            _registeredToSave = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

            if (serviceSlot != GlobalRegistryServiceSlot.Save)
                return;

            UnregisterFromSaveManager();
            _saveService = currentService as ISaveService;
            TryRegisterWithSaveManager();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_runtimeOwnerAborted)
                return;

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

        private bool TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return !_runtimeOwnerAborted;

            if (_runtimeOwnerAborted || TryAbortForUsableExistingRuntime())
                return false;

            PDAMarkerRegistry registeredRuntime = Hecton8.Core.GlobalRegistry.PDAMarkers;
            if (!ReferenceEquals(registeredRuntime, null) && !ReferenceEquals(registeredRuntime, this))
            {
                if (IsPdaMarkerRuntimeUsable(registeredRuntime))
                {
                    AbortDuplicateRuntimeOwner();
                    return false;
                }

                Hecton8.Core.GlobalRegistry.UnregisterPDAMarkerRuntime(registeredRuntime);
            }

            Hecton8.Core.GlobalRegistry.RegisterPDAMarkerRuntime(this);
            _serviceRegistered = ReferenceEquals(Hecton8.Core.GlobalRegistry.PDAMarkers, this);
            if (!_serviceRegistered)
                AbortDuplicateRuntimeOwner();
            return _serviceRegistered;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            Hecton8.Core.GlobalRegistry.UnregisterPDAMarkerRuntime(this);
            _serviceRegistered = false;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            PDAMarkerRegistry registeredRuntime = Hecton8.Core.GlobalRegistry.PDAMarkers;
            if (ReferenceEquals(registeredRuntime, null) || ReferenceEquals(registeredRuntime, this))
                return false;

            if (IsPdaMarkerRuntimeUsable(registeredRuntime))
            {
                AbortDuplicateRuntimeOwner();
                return true;
            }

            Hecton8.Core.GlobalRegistry.UnregisterPDAMarkerRuntime(registeredRuntime);
            return false;
        }

        private void AbortDuplicateRuntimeOwner()
        {
            _runtimeOwnerAborted = true;
            _serviceRegistered = false;
            _registeredToSave = false;
            _registeredSaveService = null;
            _registeredHotSwapListener = false;
            enabled = false;
            Destroy(this);
        }

        private static bool IsPdaMarkerRuntimeUsable(PDAMarkerRegistry registry)
        {
            return registry != null &&
                   registry.isActiveAndEnabled &&
                   registry._serviceRegistered &&
                   !registry._runtimeOwnerAborted;
        }

        private string BuildNextMarkerId()
        {
            int sequence = _nextSequence;
            _nextSequence++;
            return string.Create(15, sequence, (buffer, value) =>
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

        private void CommitMarkerRevision(uint markerHashId)
        {
            _revision++;
            if (_revision == 0u)
                _revision = 1u;

            if (Application.isPlaying)
                Hecton8.UI.PDAEvents.TryRaiseMarkerChanged(markerHashId, _markerCount);
        }

        private static string ResolveMarkerTitleOrFallback(string title, MarkerIconType iconType)
        {
            return string.IsNullOrWhiteSpace(title) ? BuildDefaultTitle(iconType) : title;
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

        private static bool TryResolveRuntimePosition(in AbsoluteUniversePosition targetAup, out Vector3 runtimePosition)
        {
            runtimePosition = default;
            if (!IsFiniteAup(in targetAup) ||
                !TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup))
            {
                return false;
            }

            double3 localDelta = AupPrecisionMath.LocalDeltaDouble(
                targetAup.ToAbsoluteDouble3(),
                originAup.ToAbsoluteDouble3());

            if (!math.all(math.isfinite(localDelta)))
                return false;

            double maxLocalCastMeters = AupPrecisionMath.DefaultMaxLocalCastMeters;
            double3 clampedDelta = math.clamp(
                localDelta,
                new double3(-maxLocalCastMeters, -maxLocalCastMeters, -maxLocalCastMeters),
                new double3(maxLocalCastMeters, maxLocalCastMeters, maxLocalCastMeters));
            float3 local = new float3((float)clampedDelta.x, (float)clampedDelta.y, (float)clampedDelta.z);
            if (!math.all(math.isfinite(local)))
                return false;

            runtimePosition = new Vector3(local.x, local.y, local.z);
            return true;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private static bool TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup)
        {
            originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            return IsFiniteAup(in originAup);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFiniteVector(runtimePosition) ||
                !TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup))
            {
                return false;
            }

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in positionAup);
        }

        private static float ApproximateDistanceMetersFromSq(double distanceSq)
        {
            if (!IsFiniteNonNegativeDistanceSq(distanceSq))
                return float.PositiveInfinity;
            if (distanceSq <= 0d)
                return 0f;

            float clampedSq = (float)math.min(distanceSq, (double)float.MaxValue);
            uint estimateBits = (math.asuint(clampedSq) >> 1) + 0x1FC00000u;
            float estimate = math.asfloat(estimateBits);
            return 0.5f * (estimate + (clampedSq / math.max(estimate, 0.0001f)));
        }

        private static bool IsFiniteNonNegativeDistanceSq(double distanceSq)
        {
            return !double.IsNaN(distanceSq) &&
                   !double.IsInfinity(distanceSq) &&
                   distanceSq >= 0d;
        }
    }
}
