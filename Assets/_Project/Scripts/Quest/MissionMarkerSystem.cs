using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Quest
{
    /// <summary>
    /// Zero-allocation instanced world marker renderer for active quest objectives.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MissionMarkerSystem : MonoBehaviour, IUpdatable, IRenderable, IQuestEventListener, IGlobalRegistryHotSwapListener
    {
        private const int MaxMarkers = 32;
        private const float MinimumDistanceMeters = 3f;
        private const double MarkerRebuildMoveThresholdMetersSq = 64d;
        private const uint ActiveMarkerOverflowWarningHash = 0x4D4D4151u; // MMAQ
        private const uint MarkerCacheOverflowWarningHash = 0x4D4D4351u; // MMCQ
        private const uint MarkerContextHash = 0x4D4D4354u; // MMCT
        private static readonly uint _atlasCoreMarkerTargetHash = QuestFlagHashKernel.ComputeStableHash("atlas6_core");

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct QuestMarkerCache
        {
            [FieldOffset(0)] public uint TargetHash;
            [FieldOffset(4)] private uint _pad0;
            [FieldOffset(8)] public AbsoluteUniversePosition FallbackAup;
            [FieldOffset(56)] public float HeightOffset;
            [FieldOffset(60)] public byte HasFallbackPosition;
            [FieldOffset(61)] private byte _pad1;
            [FieldOffset(62)] private ushort _pad2;
        }

        [Header("── Appearance ───────────────────────")]
        [Tooltip("Authored instanced marker mesh. Runtime mesh synthesis is forbidden for quest presentation.")]
        [SerializeField] private Mesh markerMesh;

        [Tooltip("Authored marker material with GPU instancing enabled. Runtime material instancing is forbidden for quest presentation.")]
        [SerializeField] private Material markerMaterial;

        [Tooltip("Uniform marker scale in world meters.")]
        [SerializeField, Min(0.25f)] private float markerScaleMeters = 5f;

        [Tooltip("Maximum world-space marker range from the player.")]
        [SerializeField, Min(5f)] private float maxVisibleDistanceMeters = 6000f;

        // COLD ALLOC: uint[32] - quest hash marker cache keys - owner: MissionMarkerSystem
        private readonly uint[] _markerCacheQuestHashes = new uint[MaxMarkers];
        // COLD ALLOC: QuestMarkerCache[32] - quest hash marker cache values - owner: MissionMarkerSystem
        private readonly QuestMarkerCache[] _markerCaches = new QuestMarkerCache[MaxMarkers];
        // COLD ALLOC: uint[32] - active quest hash scan buffer - owner: MissionMarkerSystem
        private readonly uint[] _activeQuestHashes = new uint[MaxMarkers];
        // COLD ALLOC: Matrix4x4[32] - instanced quest marker matrices - owner: MissionMarkerSystem
        private readonly Matrix4x4[] _markerMatrices = new Matrix4x4[MaxMarkers];
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IQuestSystem _questManager;
        private IAtlasSignalReadModel _atlasSignalReadModel;
        private HectonPlayerMovement _playerMovement;
        private Material _runtimeMarkerMaterial;
        private Mesh _runtimeMarkerMesh;
        private int _visibleMarkerCount;
        private int _markerCacheCount;
        private int _activeQuestCount;
        private int _droppedActiveMarkerCount;
        private int _droppedMarkerCacheCount;
        private int _lastActiveMarkerOverflowTelemetryFrame = -1;
        private int _lastMarkerCacheOverflowTelemetryFrame = -1;
        private bool _activeQuestSetPrimed;
        private bool _registeredUpdatable;
        private bool _registeredRenderable;
        private bool _hotSwapRegistered;
        private bool _markerCacheDirty = true;
        private bool _hasMarkerRebuildPlayerAup;
        private float _cachedMarkerScaleMeters = -1f;
        private AbsoluteUniversePosition _lastMarkerRebuildPlayerAup;

        /// <summary>
        /// Number of active quest marker hashes rejected by the fixed marker budget.
        /// </summary>
        public int DroppedActiveMarkerCount => _droppedActiveMarkerCount;

        /// <summary>
        /// Number of quest marker presentation cache entries rejected by the fixed cache budget.
        /// </summary>
        public int DroppedMarkerCacheCount => _droppedMarkerCacheCount;

        private void Awake()
        {
            EnsureRuntimeResources();
            CachePlayerContextFromRegistryCold();
            CacheQuestRuntimeFromRegistryCold();
            CacheAtlasSignalFromRegistryCold();
        }

        private void OnEnable()
        {
            EnsureRuntimeResources();
            CachePlayerContextFromRegistryCold();
            CacheQuestRuntimeFromRegistryCold();
            CacheAtlasSignalFromRegistryCold();
            TryRegisterHotSwapListener();
            PrimeActiveQuestSet();
            QuestEvents.Register(this);
            RegisterRuntime();
        }

        private void OnDisable()
        {
            QuestEvents.Unregister(this);
            UnregisterRuntime();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            UnregisterRuntime();
            TryUnregisterHotSwapListener();

            _runtimeMarkerMaterial = null;
            _runtimeMarkerMesh = null;
        }

        /// <summary>
        /// Rebuilds the active quest marker cache for the current frame.
        /// </summary>
        /// <param name="deltaTime">Scaled frame delta supplied by the dispatcher.</param>
        public void Tick(float deltaTime)
        {
            if (!_activeQuestSetPrimed)
                PrimeActiveQuestSet();

            if (_activeQuestCount <= 0)
            {
                _visibleMarkerCount = 0;
                return;
            }

            ResolvePlayerContext();
            if (_cachedMarkerScaleMeters != markerScaleMeters)
                _markerCacheDirty = true;

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                _visibleMarkerCount = 0;
                _hasMarkerRebuildPlayerAup = false;
                return;
            }

            if (!_markerCacheDirty && _hasMarkerRebuildPlayerAup)
            {
                double movedSq = AbsoluteUniversePosition.DistanceSq(in playerAup, in _lastMarkerRebuildPlayerAup);
                if (movedSq < MarkerRebuildMoveThresholdMetersSq)
                    return;
            }

            RebuildMarkerCache(in playerAup);
            _lastMarkerRebuildPlayerAup = playerAup;
            _hasMarkerRebuildPlayerAup = true;
            _markerCacheDirty = false;
        }

        public void OnQuestEvent(in QuestEventPayload payload)
        {
            uint questHash = payload.QuestHashID;
            if (questHash == 0u)
                return;

            switch ((QuestEventType)payload.EventType)
            {
                case QuestEventType.Activated:
                case QuestEventType.RevertRequested:
                    AddActiveQuestHash(questHash);
                    break;
                case QuestEventType.Completed:
                case QuestEventType.Failed:
                    RemoveActiveQuestHash(questHash);
                    break;
            }
        }

        /// <summary>
        /// Draws the active quest marker batch for the current SRP camera callback.
        /// </summary>
        /// <param name="deltaTime">Scaled frame delta supplied by the render dispatcher.</param>
        public void Render(float deltaTime)
        {
            if (_visibleMarkerCount <= 0 || _runtimeMarkerMaterial == null || _runtimeMarkerMesh == null)
                return;

            Camera camera = GlobalRenderContext.CurrentCamera;
            if (camera == null ||
                camera.cameraType == CameraType.Preview ||
                camera.cameraType == CameraType.Reflection)
            {
                return;
            }

            UnityEngine.Graphics.DrawMeshInstanced(
                _runtimeMarkerMesh,
                0,
                _runtimeMarkerMaterial,
                _markerMatrices,
                _visibleMarkerCount,
                null,
                ShadowCastingMode.Off,
                false,
                0,
                camera,
                LightProbeUsage.Off);
        }

        private void RegisterRuntime()
        {
            if (Application.isPlaying && !_registeredUpdatable)
                _registeredUpdatable = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);

            if (Application.isPlaying && !_registeredRenderable)
                _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this);
        }

        private void UnregisterRuntime()
        {
            UnregisterDispatcherTick();

            if (_registeredRenderable)
            {
                GlobalRegistry.Renderables.Unregister(this);
                _registeredRenderable = false;
            }
        }

        private void UnregisterDispatcherTick()
        {
            if (_registeredUpdatable)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registeredUpdatable = false;
            }
        }

        private void ResolvePlayerContext()
        {
            if (_playerMovement != null)
                return;

            if (_playerRuntimeContext != null)
                _playerMovement = _playerRuntimeContext.PlayerMovement;
        }

        private void CachePlayerContextFromRegistryCold()
        {
            CachePlayerContext(GlobalRegistry.Player);
            ResolvePlayerContext();
        }

        private void CacheQuestRuntimeFromRegistryCold()
        {
            CacheQuestRuntime(GlobalRegistry.QuestSystem);
        }

        private void CacheAtlasSignalFromRegistryCold()
        {
            CacheAtlasSignal(GlobalRegistry.AtlasSignalReadModel);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    CachePlayerContext(currentService as IPlayerRuntimeContext);
                    break;
                case GlobalRegistryServiceSlot.QuestRuntime:
                case GlobalRegistryServiceSlot.QuestSystem:
                    CacheQuestRuntime(currentService as IQuestSystem);
                    PrimeActiveQuestSet();
                    break;
                case GlobalRegistryServiceSlot.AtlasSignalRuntime:
                    CacheAtlasSignal(currentService as IAtlasSignalReadModel);
                    _markerCacheDirty = true;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    UnregisterDispatcherTick();
                    if (currentService != null)
                        RegisterRuntime();
                    break;
            }
        }

        private void CachePlayerContext(IPlayerRuntimeContext playerContext)
        {
            _playerRuntimeContext = playerContext;
            _playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
        }

        private void CacheQuestRuntime(IQuestSystem questManager)
        {
            if (ReferenceEquals(_questManager, questManager))
                return;

            _questManager = questManager;
            ResetQuestMarkerState();
        }

        private void ResetQuestMarkerState()
        {
            _activeQuestSetPrimed = false;
            _activeQuestCount = 0;
            _visibleMarkerCount = 0;
            _markerCacheCount = 0;
            _markerCacheDirty = true;
            _hasMarkerRebuildPlayerAup = false;

            for (int i = 0; i < MaxMarkers; i++)
            {
                _activeQuestHashes[i] = 0u;
                _markerCacheQuestHashes[i] = 0u;
                _markerCaches[i] = default;
                _markerMatrices[i] = default;
            }
        }

        private void CacheAtlasSignal(IAtlasSignalReadModel atlasSignalReadModel)
        {
            _atlasSignalReadModel = atlasSignalReadModel;
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

        private void EnsureRuntimeResources()
        {
            bool meshValid = markerMesh != null &&
                             markerMesh.subMeshCount > 0 &&
                             markerMesh.GetIndexCount(0) > 0u;
            bool materialValid = markerMaterial != null &&
                                 markerMaterial.shader != null &&
                                 markerMaterial.enableInstancing;

            if (!meshValid || !materialValid)
            {
                _runtimeMarkerMesh = null;
                _runtimeMarkerMaterial = null;
                _visibleMarkerCount = 0;
                return;
            }

            if (!ReferenceEquals(_runtimeMarkerMesh, markerMesh))
                _runtimeMarkerMesh = markerMesh;

            if (!ReferenceEquals(_runtimeMarkerMaterial, markerMaterial))
                _runtimeMarkerMaterial = markerMaterial;
        }

        private void RebuildMarkerCache(in AbsoluteUniversePosition playerAup)
        {
            _visibleMarkerCount = 0;

            IQuestSystem questManager = _questManager;
            if (questManager == null)
                return;

            double maxDistanceSq = (double)maxVisibleDistanceMeters * maxVisibleDistanceMeters;
            double minDistanceSq = (double)MinimumDistanceMeters * MinimumDistanceMeters;
            float safeScale = math.max(0.25f, markerScaleMeters);
            Vector3 uniformScale = new Vector3(safeScale, safeScale, safeScale);
            _cachedMarkerScaleMeters = markerScaleMeters;

            for (int i = 0; i < _activeQuestCount && _visibleMarkerCount < MaxMarkers; i++)
            {
                uint questHash = _activeQuestHashes[i];
                if (questHash == 0u)
                    continue;

                if (!TryResolveMarkerPosition(
                        questManager,
                        questHash,
                        out Vector3 markerWorldPosition,
                        out AbsoluteUniversePosition markerAup))
                {
                    continue;
                }

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in markerAup, in playerAup);
                if (distanceSq < minDistanceSq || distanceSq > maxDistanceSq)
                    continue;

                int markerIndex = _visibleMarkerCount++;
                _markerMatrices[markerIndex] = Matrix4x4.TRS(markerWorldPosition, Quaternion.identity, uniformScale);
            }
        }

        private void PrimeActiveQuestSet()
        {
            _activeQuestCount = 0;
            IQuestSystem questManager = _questManager;
            if (questManager == null)
            {
                _activeQuestSetPrimed = false;
                return;
            }

            _activeQuestCount = questManager.CopyActiveQuestHashes(_activeQuestHashes);
            _activeQuestSetPrimed = true;
            _markerCacheDirty = true;
        }

        private void AddActiveQuestHash(uint questHash)
        {
            if (!_activeQuestSetPrimed)
                PrimeActiveQuestSet();

            for (int i = 0; i < _activeQuestCount; i++)
            {
                if (_activeQuestHashes[i] == questHash)
                {
                    _markerCacheDirty = true;
                    return;
                }
            }

            if (_activeQuestCount >= MaxMarkers)
            {
                ReportActiveMarkerOverflow(questHash);
                return;
            }

            _activeQuestHashes[_activeQuestCount++] = questHash;
            _markerCacheDirty = true;
        }

        private void RemoveActiveQuestHash(uint questHash)
        {
            for (int i = 0; i < _activeQuestCount; i++)
            {
                if (_activeQuestHashes[i] != questHash)
                    continue;

                int lastIndex = --_activeQuestCount;
                _activeQuestHashes[i] = _activeQuestHashes[lastIndex];
                _activeQuestHashes[lastIndex] = 0u;
                _markerCacheDirty = true;
                if (_activeQuestCount == 0)
                    _visibleMarkerCount = 0;
                return;
            }
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null)
            {
                if (!playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) ||
                    (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                    !movementState.PredictedAup.IsFinite())
                {
                    playerAup = default;
                    return false;
                }

                playerAup = movementState.PredictedAup;
                return true;
            }

            playerAup = default;
            return false;
        }

        private bool TryResolveMarkerPosition(
            IQuestSystem questManager,
            uint questHash,
            out Vector3 markerWorldPosition,
            out AbsoluteUniversePosition markerAup)
        {
            markerWorldPosition = default;
            markerAup = default;
            if (!TryResolveMarkerCache(questManager, questHash, out QuestMarkerCache cache))
                return false;

            if (cache.TargetHash == _atlasCoreMarkerTargetHash)
            {
                IAtlasSignalReadModel atlasSignalReadModel = _atlasSignalReadModel;
                if (atlasSignalReadModel == null ||
                    !atlasSignalReadModel.TryReadAtlasSignalCoreAup(out AbsoluteUniversePosition atlasCoreAup))
                {
                    return false;
                }

                markerAup = ResolveOffsetAup(in atlasCoreAup, cache.HeightOffset);
            }
            else if (cache.HasFallbackPosition != 0)
            {
                // The cached anchor already carries the authored height lift applied when the entry was built.
                markerAup = cache.FallbackAup;
            }
            else
            {
                return false;
            }

            // Runtime space is rebased by the floating origin, so the draw position is re-derived from the
            // absolute anchor on every rebuild. Reusing a runtime vector captured in an earlier origin epoch
            // draws the marker offset by the accumulated rebase while the AUP range test still passes.
            AbsoluteUniversePosition runtimeOriginAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!TryResolveMarkerRuntimePosition(in markerAup, in runtimeOriginAup, out markerWorldPosition))
            {
                markerAup = default;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Resolves a marker's runtime draw position from its absolute anchor against a supplied runtime origin.
        /// </summary>
        /// <param name="markerAup">Absolute marker anchor with the authored height lift already applied.</param>
        /// <param name="runtimeOriginAup">Runtime origin published by the floating-origin route.</param>
        /// <param name="markerWorldPosition">Resolved runtime-space draw position.</param>
        /// <returns>False when either input is non-finite, so no non-finite matrix reaches the instanced batch.</returns>
        public static bool TryResolveMarkerRuntimePosition(
            in AbsoluteUniversePosition markerAup,
            in AbsoluteUniversePosition runtimeOriginAup,
            out Vector3 markerWorldPosition)
        {
            markerWorldPosition = default;
            if (!markerAup.IsFinite() || !runtimeOriginAup.IsFinite())
                return false;

            float3 runtimePosition = AbsoluteUniversePosition.ToCameraRelativeFloat3(in markerAup, in runtimeOriginAup);
            if (!math.all(math.isfinite(runtimePosition)))
                return false;

            markerWorldPosition = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            return true;
        }

        private static AbsoluteUniversePosition ResolveOffsetAup(in AbsoluteUniversePosition sourceAup, float heightOffsetMeters)
        {
            if (heightOffsetMeters == 0f)
                return sourceAup;

            double3 absolute = sourceAup.ToAbsoluteDouble3();
            absolute.y += heightOffsetMeters;
            return AbsoluteUniversePosition.FromAbsolutePosition(absolute);
        }

        private bool TryResolveMarkerCache(IQuestSystem questManager, uint questHash, out QuestMarkerCache cache)
        {
            for (int i = 0; i < _markerCacheCount; i++)
            {
                if (_markerCacheQuestHashes[i] == questHash)
                {
                    cache = _markerCaches[i];
                    return true;
                }
            }

            if (_markerCacheCount >= MaxMarkers)
            {
                ReportMarkerCacheOverflow(questHash);
                cache = default;
                return false;
            }

            if (!questManager.TryCopyQuestPresentation(
                    questHash,
                    null,
                    out _,
                    null,
                    out _,
                    out uint markerTargetHash,
                    out Vector3 markerWorldPosition,
                    out float markerHeightOffset))
            {
                cache = default;
                return false;
            }

            float heightOffset = math.max(0f, markerHeightOffset);
            Vector3 liftedMarkerPosition = markerWorldPosition + (Vector3.up * heightOffset);
            AbsoluteUniversePosition fallbackAup = default;
            bool hasFallbackPosition = markerWorldPosition.sqrMagnitude > 0.0001f &&
                                       TryResolveAupFromRuntimeOrigin(
                                           liftedMarkerPosition,
                                           out fallbackAup);
            cache = new QuestMarkerCache
            {
                TargetHash = markerTargetHash,
                FallbackAup = hasFallbackPosition ? fallbackAup : default,
                HeightOffset = heightOffset,
                HasFallbackPosition = hasFallbackPosition ? (byte)1 : (byte)0
            };

            int cacheIndex = _markerCacheCount++;
            _markerCacheQuestHashes[cacheIndex] = questHash;
            _markerCaches[cacheIndex] = cache;
            return true;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition absoluteAup)
        {
            absoluteAup = default;
            if (!float.IsFinite(runtimePosition.x) ||
                !float.IsFinite(runtimePosition.y) ||
                !float.IsFinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            absoluteAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return absoluteAup.IsFinite();
        }

        private void ReportActiveMarkerOverflow(uint questHash)
        {
            _droppedActiveMarkerCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastActiveMarkerOverflowTelemetryFrame == frame)
                return;

            _lastActiveMarkerOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ActiveMarkerOverflowWarningHash,
                MarkerContextHash ^ questHash,
                math.max(1, _droppedActiveMarkerCount));
        }

        private void ReportMarkerCacheOverflow(uint questHash)
        {
            _droppedMarkerCacheCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastMarkerCacheOverflowTelemetryFrame == frame)
                return;

            _lastMarkerCacheOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                MarkerCacheOverflowWarningHash,
                MarkerContextHash ^ questHash,
                math.max(1, _droppedMarkerCacheCount));
        }

    }
}
