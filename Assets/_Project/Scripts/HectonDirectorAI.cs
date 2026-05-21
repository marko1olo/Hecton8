using System;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.Visor;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Systems.AI
{
    /// <summary>
    /// Main-thread listener for deferred director encounter events.
    /// </summary>
    public interface IDirectorAIEventListener
    {
        /// <summary>Called when the director requests a swarm/horde response.</summary>
        void OnDirectorSpawnHordeRequested(Vector3 position);

        /// <summary>Called when the director requests an equipment glitch.</summary>
        void OnDirectorEquipmentGlitchRequested(float intensity);

        /// <summary>Called when the director requests rare-discovery routing.</summary>
        void OnDirectorRareDiscoveryRequested(Vector3 position);

        /// <summary>Called when the director requests a weather-pressure shift.</summary>
        void OnDirectorWeatherShiftRequested(float intensity);

        /// <summary>Called when the director requests mission routing.</summary>
        void OnDirectorMissionTriggerRequested(Vector3 position);

        /// <summary>Called when predator pressure enters or exits active pressure.</summary>
        void OnDirectorPredatorPressureChanged(bool pressureEnabled);

        /// <summary>Called when a predator enters an aggro spike that should hard-cut the combat mix.</summary>
        void OnDirectorThreatSpike(Vector3 position, float intensity);
    }

    /// <summary>
    /// Fixed-ring event lane for DirectorAI outputs.
    /// </summary>
    public static class DirectorAIEvents
    {
        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct DirectorAIEventPayload
        {
            [FieldOffset(0)]
            public Vector3 Position;
            [FieldOffset(12)]
            public float Value;
            [FieldOffset(16)]
            public byte EventType;
            [FieldOffset(17)]
            public byte BoolValue;
            [FieldOffset(18)]
            public ushort Padding0;
            [FieldOffset(20)]
            public uint Padding1;
            [FieldOffset(24)]
            public ulong Padding2;
        }

        private const byte SpawnHordeEventType = DirectorAIMusicSignal.SpawnHordeEventType;
        private const byte EquipmentGlitchEventType = DirectorAIMusicSignal.EquipmentGlitchEventType;
        private const byte RareDiscoveryEventType = DirectorAIMusicSignal.RareDiscoveryEventType;
        private const byte WeatherShiftEventType = DirectorAIMusicSignal.WeatherShiftEventType;
        private const byte MissionTriggerEventType = DirectorAIMusicSignal.MissionTriggerEventType;
        private const byte PredatorPressureEventType = DirectorAIMusicSignal.PredatorPressureEventType;
        private const byte ThreatSpikeEventType = DirectorAIMusicSignal.ThreatSpikeEventType;
        private const int ExpectedPendingEventCapacity = 24;
        private const int ListenerCapacity = 16;

        private struct ListenerSlot
        {
            public IDirectorAIEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        // COLD ALLOC: ListenerSlot[16] - DirectorAI listeners drained without interface array dispatch - owner: DirectorAIEvents
        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: DirectorAIEventPayload[24] - fixed main-thread dispatch ring, no native queue ownership - owner: DirectorAIEvents
        private static readonly DirectorAIEventPayload[] _pendingEvents = new DirectorAIEventPayload[ExpectedPendingEventCapacity];
        // COLD ALLOC: DirectorAIEventPayload[24] - fixed listener reentry ring, no native queue ownership - owner: DirectorAIEvents
        private static readonly DirectorAIEventPayload[] _nextFrameEvents = new DirectorAIEventPayload[ExpectedPendingEventCapacity];
        private static int _listenerCount;
        private static int _pendingEventHead;
        private static int _pendingEventCount;
        private static int _nextFrameEventHead;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;

        /// <summary>
        /// Number of queued director events awaiting LateUpdate dispatch.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ClearRing(_pendingEvents);
            ClearRing(_nextFrameEvents);

            for (int i = 0; i < _listenerCount; i++)
                _listeners[i].Clear();

            _listenerCount = 0;
            _pendingEventHead = 0;
            _pendingEventCount = 0;
            _nextFrameEventHead = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
        }

        /// <summary>
        /// Registers a main-thread DirectorAI event listener.
        /// </summary>
        public static void Register(IDirectorAIEventListener listener)
        {
            if (listener != null)
                RegisterImmediate(listener);
        }

        /// <summary>
        /// Unregisters a main-thread DirectorAI event listener.
        /// </summary>
        public static void Unregister(IDirectorAIEventListener listener)
        {
            if (listener != null)
                TryUnregisterImmediate(listener);
        }

        /// <summary>Queues a horde request.</summary>
        public static void RaiseSpawnHordeRequested(Vector3 position)
        {
            PublishMusicSignal(SpawnHordeEventType, position, 0f, false);
            EnqueuePosition(SpawnHordeEventType, position);
        }

        /// <summary>Queues an equipment glitch request.</summary>
        public static void RaiseEquipmentGlitchRequested(float intensity)
        {
            PublishMusicSignal(EquipmentGlitchEventType, default, intensity, false);
            EnqueueValue(EquipmentGlitchEventType, intensity);
        }

        /// <summary>Queues a rare-discovery request.</summary>
        public static void RaiseRareDiscoveryRequested(Vector3 position)
        {
            PublishMusicSignal(RareDiscoveryEventType, position, 0f, false);
            EnqueuePosition(RareDiscoveryEventType, position);
        }

        /// <summary>Queues a weather shift request.</summary>
        public static void RaiseWeatherShiftRequested(float intensity)
        {
            PublishMusicSignal(WeatherShiftEventType, default, intensity, false);
            EnqueueValue(WeatherShiftEventType, intensity);
        }

        /// <summary>Queues a mission trigger request.</summary>
        public static void RaiseMissionTriggerRequested(Vector3 position)
        {
            PublishMusicSignal(MissionTriggerEventType, position, 0f, false);
            EnqueuePosition(MissionTriggerEventType, position);
        }

        /// <summary>Queues a predator pressure state change.</summary>
        public static void RaisePredatorPressureChanged(bool pressureEnabled)
        {
            PublishMusicSignal(PredatorPressureEventType, default, pressureEnabled ? 1f : 0f, pressureEnabled);
            if (_listenerCount <= 0)
                return;

            Enqueue(new DirectorAIEventPayload
            {
                EventType = PredatorPressureEventType,
                BoolValue = pressureEnabled ? (byte)1 : (byte)0
            });
        }

        /// <summary>Queues a predator threat spike.</summary>
        public static void RaiseThreatSpike(Vector3 position, float intensity)
        {
            float clampedIntensity = math.saturate(intensity);
            PublishMusicSignal(ThreatSpikeEventType, position, clampedIntensity, false);
            if (_listenerCount <= 0)
                return;

            Enqueue(new DirectorAIEventPayload
            {
                EventType = ThreatSpikeEventType,
                Position = position,
                Value = clampedIntensity
            });
        }

        /// <summary>
        /// Flushes queued director events on the main thread.
        /// </summary>
        public static void FlushPending()
        {
            if (_pendingEventCount <= 0)
            {
                PromoteNextFrameEvents();
                return;
            }

            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : ExpectedPendingEventCapacity;
            _isDispatching = true;
            try
            {
                while (scanBudget-- > 0 && _pendingEventCount > 0)
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return;

                    if (!TryDequeuePending(out DirectorAIEventPayload payload))
                        break;

                    Dispatch(in payload);
                }
            }
            finally
            {
                _isDispatching = false;
            }

            if (_pendingEventCount > 0)
                return;

            PromoteNextFrameEvents();
        }

        private static void EnqueuePosition(byte eventType, Vector3 position)
        {
            Enqueue(new DirectorAIEventPayload
            {
                EventType = eventType,
                Position = position
            });
        }

        private static void EnqueueValue(byte eventType, float value)
        {
            Enqueue(new DirectorAIEventPayload
            {
                EventType = eventType,
                Value = value
            });
        }

        private static void PublishMusicSignal(byte eventType, Vector3 position, float value, bool boolValue)
        {
            SignalBus<DirectorAIMusicSignal>.EnsureInitialized();
            DirectorAIMusicSignal signal = new DirectorAIMusicSignal(eventType, position, value, boolValue);
            SignalBus<DirectorAIMusicSignal>.Push(in signal);
        }

        private static bool Enqueue(in DirectorAIEventPayload payload)
        {
            if (_listenerCount <= 0)
                return false;

            if (_pendingEventCount + _nextFrameEventCount >= ExpectedPendingEventCapacity)
                return false;

            if (_isDispatching)
                return TryEnqueueNextFrame(in payload);

            return TryEnqueuePending(in payload);
        }

        private static void PromoteNextFrameEvents()
        {
            if (_nextFrameEventCount <= 0)
                return;

            while (_nextFrameEventCount > 0 && _pendingEventCount < ExpectedPendingEventCapacity)
                if (TryDequeueNextFrame(out DirectorAIEventPayload payload))
                    TryEnqueuePending(in payload);
                else
                    return;
        }

        private static void Dispatch(in DirectorAIEventPayload payload)
        {
            int listenerCount = _listenerCount;
            for (int i = listenerCount - 1; i >= 0; i--)
            {
                IDirectorAIEventListener listener = _listeners[i].Listener;
                if (listener == null)
                    continue;

                switch (payload.EventType)
                {
                    case SpawnHordeEventType:
                        listener.OnDirectorSpawnHordeRequested(payload.Position);
                        break;
                    case EquipmentGlitchEventType:
                        listener.OnDirectorEquipmentGlitchRequested(payload.Value);
                        break;
                    case RareDiscoveryEventType:
                        listener.OnDirectorRareDiscoveryRequested(payload.Position);
                        break;
                    case WeatherShiftEventType:
                        listener.OnDirectorWeatherShiftRequested(payload.Value);
                        break;
                    case MissionTriggerEventType:
                        listener.OnDirectorMissionTriggerRequested(payload.Position);
                        break;
                    case PredatorPressureEventType:
                        listener.OnDirectorPredatorPressureChanged(payload.BoolValue != 0);
                        break;
                    case ThreatSpikeEventType:
                        listener.OnDirectorThreatSpike(payload.Position, payload.Value);
                        break;
                }
            }
        }

        private static void RegisterImmediate(IDirectorAIEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return;
            }

            if (_listenerCount >= ListenerCapacity)
                return;

            _listeners[_listenerCount++].Listener = listener;
        }

        private static bool TryUnregisterImmediate(IDirectorAIEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                _listenerCount--;
                _listeners[i] = _listeners[_listenerCount];
                _listeners[_listenerCount].Clear();
                return true;
            }

            return false;
        }

        private static bool TryEnqueuePending(in DirectorAIEventPayload payload)
        {
            if (_pendingEventCount >= ExpectedPendingEventCapacity)
                return false;

            int writeIndex = RingIndex(_pendingEventHead, _pendingEventCount);
            _pendingEvents[writeIndex] = payload;
            _pendingEventCount++;
            return true;
        }

        private static bool TryEnqueueNextFrame(in DirectorAIEventPayload payload)
        {
            if (_nextFrameEventCount >= ExpectedPendingEventCapacity)
                return false;

            int writeIndex = RingIndex(_nextFrameEventHead, _nextFrameEventCount);
            _nextFrameEvents[writeIndex] = payload;
            _nextFrameEventCount++;
            return true;
        }

        private static bool TryDequeuePending(out DirectorAIEventPayload payload)
        {
            return TryDequeue(_pendingEvents, ref _pendingEventHead, ref _pendingEventCount, out payload);
        }

        private static bool TryDequeueNextFrame(out DirectorAIEventPayload payload)
        {
            return TryDequeue(_nextFrameEvents, ref _nextFrameEventHead, ref _nextFrameEventCount, out payload);
        }

        private static bool TryDequeue(
            DirectorAIEventPayload[] buffer,
            ref int head,
            ref int count,
            out DirectorAIEventPayload payload)
        {
            if (count <= 0)
            {
                payload = default;
                return false;
            }

            payload = buffer[head];
            buffer[head] = default;
            head = RingIndex(head, 1);
            count--;
            return true;
        }

        private static int RingIndex(int head, int offset)
        {
            int index = head + offset;
            return index >= ExpectedPendingEventCapacity ? index - ExpectedPendingEventCapacity : index;
        }

        private static void ClearRing(DirectorAIEventPayload[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = default;
        }
    }

    /// <summary>
    /// One Burst-built predator sight ray input. Managed brain references stay outside this native lane.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct PredatorSightRaycastInput
    {
        [FieldOffset(0)]
        public float3 Origin;
        [FieldOffset(12)]
        public float3 Target;
        [FieldOffset(24)]
        public int LayerMask;
        [FieldOffset(28)]
        public int Padding;
    }

    internal static class PredatorSpatialHashMath
    {
        public static int3 ResolveCellCoord(double3 absolutePosition, double cellSizeMeters)
        {
            double inverseCellSize = math.rcp(math.max(0.001d, cellSizeMeters));
            return (int3)math.floor(absolutePosition * inverseCellSize);
        }

        public static int HashCell(int3 cell)
        {
            unchecked
            {
                uint hash = ((uint)cell.x * 73856093u) ^
                            ((uint)cell.y * 19349663u) ^
                            ((uint)cell.z * 83492791u);
                hash ^= hash >> 16;
                return (int)(hash & 0x7FFFFFFFu);
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct PredatorSpatialHashInsertJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<double3> AbsolutePositions;
        [NoAlias] public NativeArray<int3> CellCoords;
        public int Count;
        public double CellSizeMeters;

        public void Execute(int index)
        {
            if (index >= Count)
                return;

            int3 cell = PredatorSpatialHashMath.ResolveCellCoord(AbsolutePositions[index], CellSizeMeters);
            CellCoords[index] = cell;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct PredatorSightRaycastBuildJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<PredatorSightRaycastInput> Inputs;
        [NoAlias] public NativeArray<RaycastCommand> Commands;
        public int RequestCount;

        public void Execute(int index)
        {
            if (index >= RequestCount)
            {
                Commands[index] = new RaycastCommand(
                    Vector3.zero,
                    Vector3.forward,
                    new QueryParameters(HectonLayerMasks.NoLayers, false, QueryTriggerInteraction.Ignore, false),
                    0f);
                return;
            }

            PredatorSightRaycastInput input = Inputs[index];
            float3 delta = input.Target - input.Origin;
            float3 absDelta = math.abs(delta);
            float maxAxis = math.cmax(absDelta);
            float minAxis = math.cmin(absDelta);
            float midAxis = absDelta.x + absDelta.y + absDelta.z - maxAxis - minAxis;
            // Cinematic upper-bound segment length: no sqrt/rsqrt in the Burst sight build lane.
            float length = math.max(0.001f, maxAxis + (midAxis * 0.5f) + (minAxis * 0.25f));
            float3 direction = delta * math.rcp(length);
            Commands[index] = new RaycastCommand(
                new Vector3(input.Origin.x, input.Origin.y, input.Origin.z),
                new Vector3(direction.x, direction.y, direction.z),
                new QueryParameters(input.LayerMask, false, QueryTriggerInteraction.Ignore, false),
                length);
        }
    }

    /// <summary>
    /// Scene-facing compatibility owner for the encounter pacing director.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4500)]
    public sealed class HectonDirectorAI : MonoBehaviour, IUpdatable, ILateFrameTickable, IEncounterDirectorService, ISonarPingEventListener, IAcousticPingEventListener, IElectromagneticPulseEventListener, IPhysicsAcousticImpulseEventListener, IGlobalRegistryHotSwapListener
    {
        internal static HectonDirectorAI ActiveRuntimeInstance => GlobalRegistry.EncounterDirector as HectonDirectorAI;

        [Header("References")]
        [Tooltip("Authoritative player transform. Resolved from bootstrap when left null.")]
        [SerializeField] private Transform playerTransform;
        [Tooltip("Optional explicit gameplay camera. Resolved from the player hierarchy when left null.")]
        [SerializeField] private Camera playerCamera;
        [Tooltip("Player survival system used to feed the director stress inputs.")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;
        [Tooltip("Fauna spawn owner. Resolved from the runtime reference utility when left null.")]
        [SerializeField] private FaunaDirector faunaDirector;
        [Tooltip("Optional authored encounter pacing profile. When assigned, spawn thresholds and critical-health suppression become data-driven.")]
        [SerializeField] private EncounterProfile encounterProfile;
        [Tooltip("Optional authored threat token cost table. When assigned, encounter token costs and simultaneous caps become data-driven.")]
        [SerializeField] private ThreatCostTable threatCostTable;

        [Header("Event Output")]
        [Tooltip("Deterministic offset radius used for non-spawn director event hints.")]
        [SerializeField, Range(8f, 48f)] private float eventOffsetRadius = 25f;

#if UNITY_EDITOR
        [Header("Diagnostics")]
        [SerializeField] private float _debugStressLevel;
        [SerializeField] private float _debugIntensityLevel;
        [SerializeField] private float _debugTokenBudget;
        [SerializeField] private float _debugAverageFrameTimeMs;
        [SerializeField] private int _debugActiveEnemyCount;
        [SerializeField] private string _debugPhaseName;
#endif

        // COLD ALLOC: EncounterDirector[1] - dispatcher-driven encounter kernel - owner: HectonDirectorAI
        private readonly EncounterDirector _encounterDirector = new EncounterDirector();
        // COLD ALLOC: Plane[6] - reusable frustum plane scratch for zero-allocation camera extraction - owner: HectonDirectorAI
        private readonly Plane[] _frustumPlaneScratch = new Plane[EncounterDirector.FrustumPlaneCount];
        // COLD ALLOC: FrameTiming[1] - reusable frame-timing sample buffer - owner: HectonDirectorAI
        private readonly FrameTiming[] _frameTimingScratch = new FrameTiming[1];
        // COLD ALLOC: float[8] - rolling frame-time history for shed hysteresis - owner: HectonDirectorAI
        private readonly float[] _frameTimeHistory = new float[8];
        // COLD ALLOC: SpatialQueryHit[64] - AUP-filtered active-sonar leviathan aggro contacts - owner: HectonDirectorAI
        private readonly SpatialQueryHit[] _acousticPingPredatorContacts = new SpatialQueryHit[AcousticPingPredatorContactCapacity];
        // COLD ALLOC: SpatialQueryHit[64] - director predator spatial hash contact mirror - owner: HectonDirectorAI
        private readonly SpatialQueryHit[] _predatorSpatialContacts = new SpatialQueryHit[PredatorSpatialHashContactCapacity];
        // COLD ALLOC: FaunaBrain[1] - managed owner mirror for completed predator LOS rays - owner: HectonDirectorAI
        private readonly FaunaBrain[] _predatorSightBrains = new FaunaBrain[PredatorSightMaxRaysPerFrame];
        // COLD ALLOC: Vector3[1] - player-position mirror for completed predator LOS rays - owner: HectonDirectorAI
        private readonly Vector3[] _predatorSightPlayerPositions = new Vector3[PredatorSightMaxRaysPerFrame];
        // COLD ALLOC: Vector3[1] - player-velocity mirror for dead-reckoned predator LOS guidance - owner: HectonDirectorAI
        private readonly Vector3[] _predatorSightPlayerVelocities = new Vector3[PredatorSightMaxRaysPerFrame];
        // COLD ALLOC: Vector3[1] - player-forward mirror for completed predator LOS rays - owner: HectonDirectorAI
        private readonly Vector3[] _predatorSightPlayerForwards = new Vector3[PredatorSightMaxRaysPerFrame];
        private IDataVault _dataVault;
        private VaultGenerationHandle<PredatorSightRaycastInput> _predatorSightInputsHandle;
        private VaultGenerationHandle<RaycastCommand> _predatorSightCommandsHandle;
        private VaultGenerationHandle<RaycastHit> _predatorSightHitsHandle;
        private VaultGenerationHandle<double3> _predatorSpatialAbsolutePositionsHandle;
        private VaultGenerationHandle<int3> _predatorSpatialCellCoordsHandle;
        private JobHandle _predatorSightRaycastHandle;
        private JobHandle _predatorSpatialHashBuildHandle;
        private const float SonarStressDecayPerSecond = 0.18f;
        private const float ActiveSonarLeviathanAggroRadiusMeters = 1000f;
        private const float ActiveSonarLeviathanAggroInvRadiusMeters = 1f / ActiveSonarLeviathanAggroRadiusMeters;
        private const double ActiveSonarLeviathanAggroRadiusMetersSqr =
            ActiveSonarLeviathanAggroRadiusMeters * ActiveSonarLeviathanAggroRadiusMeters;
        private const float ActiveSonarLeviathanAggroDurationSeconds = 10f;
        private const float ActiveSonarPingDebounceSeconds = 2f;
        private const float ActiveSonarBoidScatterRadiusMeters = 90f;
        private const float ActiveSonarBoidScatterDurationSeconds = 0.85f;
        private const float PredatorAcousticDeafenedDurationSeconds = 8f;
        private const float PredatorAcousticDeafeningImpulseEnergyJoules = 6400f;
        private const int AcousticPingPredatorContactCapacity = 64;
        private const int PredatorSightMaxRaysPerFrame = 1;
        private const int PredatorSpatialHashContactCapacity = 64;
        private const BufferID PredatorSightInputsBufferId = (BufferID)73235;
        private const BufferID PredatorSightCommandsBufferId = (BufferID)73236;
        private const BufferID PredatorSightHitsBufferId = (BufferID)73237;
        private const BufferID PredatorSpatialAbsolutePositionsBufferId = (BufferID)73238;
        private const BufferID PredatorSpatialCellCoordsBufferId = (BufferID)73239;
        private const float PredatorSpatialHashCellSizeMeters = 50f;
        private const float PredatorSpatialHashActiveChunkRadiusMeters = 500f;
        private const float PredatorDeadZoneCullDistanceMeters = 250f;
        private const double PredatorDeadZoneCullDistanceMetersSqr =
            PredatorDeadZoneCullDistanceMeters * PredatorDeadZoneCullDistanceMeters;
        private const float PredatorSightScanRadiusMeters = 220f;
        private const float PredatorSightProbeOffsetMeters = 0.75f;
        private const float PredatorSightIntervalSeconds = 0.5f;
        private const float PredatorSightConeDotThreshold = 0.28f;
        private const float PredatorSightConeDotThresholdSqr =
            PredatorSightConeDotThreshold * PredatorSightConeDotThreshold;
        private const float PredatorSightImmediateRevealRadiusMeters = 18f;
        private const double PredatorSightImmediateRevealRadiusMetersSqr =
            PredatorSightImmediateRevealRadiusMeters * PredatorSightImmediateRevealRadiusMeters;
        private const float PredatorSightRearViewDotThreshold = -0.2f;
        private const float PredatorSightRearViewFakeMinDistanceMeters = 24f;
        private const double PredatorSightRearViewFakeMinDistanceMetersSqr =
            PredatorSightRearViewFakeMinDistanceMeters * PredatorSightRearViewFakeMinDistanceMeters;
        private const double DirectorSolveBudgetMilliseconds = 0.2d;
        private const float DirectorSolveWarningCooldownSeconds = 1f;
        private const float DirectorFrustumPlaneRefreshIntervalSeconds = 0.1f;
        private const int EntityDeathSignalDrainLimit = 16;
        private const int EventOffsetDirectionLutSize = 64;
        private const int EventOffsetDirectionLutMask = EventOffsetDirectionLutSize - 1;
        private const int EventOffsetDistanceBucketCount = 16;
        private const int EventOffsetDistanceBucketMask = EventOffsetDistanceBucketCount - 1;
        private const float EventOffsetMinRadiusScale = 0.4f;
        private const float EventOffsetDistanceStepScale = (1f - EventOffsetMinRadiusScale) / (EventOffsetDistanceBucketCount - 1);
        private static readonly uint _DirectorSolveBudgetWarningHash =
            unchecked((uint)LocHash.Compute("DirectorAI.SolveBudgetExceeded"));
        private static readonly uint _DirectorTelemetryContextHash =
            unchecked((uint)LocHash.Compute("HectonDirectorAI"));
        private static readonly double _StopwatchTickToMilliseconds =
            1000.0d / System.Diagnostics.Stopwatch.Frequency;
        private static readonly int PredatorSightLayerMask =
            HectonLayerMasks.TerrainLayerMask |
            HectonLayerMasks.BaseModuleLayerMask |
            HectonLayerMasks.VehicleLayerMask |
            HectonLayerMasks.VoxelCaveLayerMask |
            HectonLayerMasks.DebrisLayerMask;
        private static readonly Vector2[] _eventOffsetDirectionLut = BuildEventOffsetDirectionLut(); // COLD ALLOC: Vector2[64] - deterministic director event offset directions - owner: HectonDirectorAI

        private HectonPlayerMovement _playerMovement;
        private IMetaCampaignService _metaCampaignService;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IEcosystemDirectorService _ecosystemDirector;
        private SargassumMicroFaunaBoids _sargassumMicroFauna;
        private bool _encounterDirectorServiceRegistered;
        private bool _dispatcherRegistered;
        private bool _lateFrameRegistered;
        private bool _hotSwapRegistered;
        private bool _acousticPingSubscribed;
        private bool _predatorSightVaultLocked;
        private bool _predatorSpatialHashVaultLocked;
        private bool _predatorSightJobScheduled;
        private bool _predatorSpatialHashJobScheduled;
        private bool _predatorSpatialHashReady;
        private float _resolveRetryTimer;
        private float _hunterSquadCooldown;
        private float _predatorSightCooldown;
        private float _activeSonarPingDebounceTimer;
        private float _nextDirectorSolveWarningTime;
        private float _directorSolveWarningClockSeconds;
        private float _frustumPlaneRefreshTimer;
        private int _frameTimeHistoryCount;
        private int _frameTimeHistoryIndex;
        private int _predatorSightScheduledCount;
        private int _predatorSpatialContactCount;
        private int _lastEntityDeathSignalSnapshotGeneration;
        private Vector3 _lastResolvedPlayerForward = Vector3.forward;
        private bool _frustumPlanesInitialized;
        private float _recentSonarStress;
        private float _externalPeakPressure01;
        private float _externalPeakHoldSeconds;
        private const float HunterSquadHostilityThreshold = 0.8f;
        private const float HunterSquadCooldownSeconds = 9f;
        private const int HunterSquadSize = 3;

        /// <summary>
        /// Current normalized director tension score in the legacy 0..100 presentation range.
        /// </summary>
        public float TensionScore => _encounterDirector.StressLevel * 100f;

        internal int CurrentPhaseIndex => (int)_encounterDirector.CurrentPhase;
        internal float CurrentStress01 => _encounterDirector.StressLevel;
        internal float CurrentIntensity01 => _encounterDirector.IntensityLevel;

        /// <summary>
        /// True while the director is in the Relax phase.
        /// </summary>
        public bool IsRelaxPhase => _encounterDirector.CurrentPhase == EncounterPhase.Relax;

        /// <summary>
        /// True while predator pressure is allowed to escalate.
        /// </summary>
        public bool IsPredatorPressureEnabled => _encounterDirector.CurrentPhase != EncounterPhase.Relax;

        /// <summary>
        /// Human-readable current phase name for legacy diagnostics consumers.
        /// </summary>
        public string CurrentPhaseName => _encounterDirector.CurrentPhaseName;

        /// <summary>
        /// True once the encounter director is registered in the global registry.
        /// </summary>
        public bool IsInitialized => ReferenceEquals(GlobalRegistry.EncounterDirector, this);

        /// <inheritdoc />
        public bool TryGetPredatorAupGpuBuffer(out GraphicsBuffer buffer, out int count)
        {
            return _encounterDirector.TryGetPredatorAupGpuBuffer(out buffer, out count);
        }

        private void Awake()
        {
            _encounterDirector.ApplyAuthoring(encounterProfile, threatCostTable);
            RefreshColdRegistryReferences();
            RefreshRuntimeReferences(force: true);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            EnsureEncounterDirectorServiceRegistered();
            RefreshColdRegistryReferences();
            TryRegisterHotSwapListener();
            RefreshMetaCampaignService();
            TryRegisterDispatcherLanes();
            _encounterDirector.EnsureGpuResources();
            _encounterDirector.Reset();
            _frustumPlaneRefreshTimer = 0f;
            _frustumPlanesInitialized = false;
            _recentSonarStress = 0f;
            _externalPeakPressure01 = 0f;
            _externalPeakHoldSeconds = 0f;
            _hunterSquadCooldown = 0f;
            _predatorSightCooldown = 0f;
            _activeSonarPingDebounceTimer = 0f;
            _predatorSpatialHashReady = false;
            _predatorSpatialContactCount = 0;
            EnsurePredatorSightBuffersAllocated(out _, out _, out _);
            EnsurePredatorSpatialHashBuffersAllocated(out _, out _);
            SpectrumEvents.RegisterSonarPingListener(this);
            SubscribeAcousticPingEvents();
            PublishPredatorPressure(true);
        }

        private void Start()
        {
            if (!Application.isPlaying)
                return;

            EnsureEncounterDirectorServiceRegistered();
            RefreshColdRegistryReferences();
            TryRegisterHotSwapListener();
            RefreshMetaCampaignService();
            TryRegisterDispatcherLanes();
        }

        private void EnsureEncounterDirectorServiceRegistered()
        {
            if (_encounterDirectorServiceRegistered)
                return;

            GlobalRegistry.RegisterEncounterDirectorService(this);
            _encounterDirectorServiceRegistered = ReferenceEquals(GlobalRegistry.EncounterDirector, this);
        }

        private void TryRegisterDispatcherLanes()
        {
            if (!_dispatcherRegistered)
                _dispatcherRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);

            if (!_lateFrameRegistered)
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            if (_encounterDirectorServiceRegistered)
            {
                GlobalRegistry.UnregisterEncounterDirectorService(this);
                _encounterDirectorServiceRegistered = false;
            }

            _metaCampaignService = null;
            _encounterDirector.SetMetaCampaignService(null);
            _playerRuntimeContext = null;
            _ecosystemDirector = null;
            _sargassumMicroFauna = null;
            TryUnregisterHotSwapListener();

            if (_dispatcherRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
                _dispatcherRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _lateFrameRegistered = false;
            }

            SpectrumEvents.UnregisterSonarPingListener(this);
            UnsubscribeAcousticPingEvents();
            CompletePredatorSightBatch(forceComplete: true);
            CompletePredatorSpatialHashBuild(forceComplete: true);
            _encounterDirector.ForceStopAndReset();
            _recentSonarStress = 0f;
            _externalPeakPressure01 = 0f;
            _externalPeakHoldSeconds = 0f;
            _hunterSquadCooldown = 0f;
            _predatorSightCooldown = 0f;
            _activeSonarPingDebounceTimer = 0f;
            _nextDirectorSolveWarningTime = 0f;
            _directorSolveWarningClockSeconds = 0f;
            _frustumPlaneRefreshTimer = 0f;
            _frustumPlanesInitialized = false;
            _predatorSpatialHashReady = false;
            _predatorSpatialContactCount = 0;
            _lastEntityDeathSignalSnapshotGeneration = 0;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _encounterDirector.ApplyAuthoring(encounterProfile, threatCostTable);
        }
#endif

        private void OnDestroy()
        {
            SpectrumEvents.UnregisterSonarPingListener(this);
            UnsubscribeAcousticPingEvents();
            _metaCampaignService = null;
            _encounterDirector.SetMetaCampaignService(null);
            _playerRuntimeContext = null;
            _ecosystemDirector = null;
            _sargassumMicroFauna = null;
            TryUnregisterHotSwapListener();

            if (_encounterDirectorServiceRegistered && ReferenceEquals(GlobalRegistry.EncounterDirector, this))
            {
                GlobalRegistry.UnregisterEncounterDirectorService(this);
                _encounterDirectorServiceRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _lateFrameRegistered = false;
            }

            if (_dispatcherRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
                _dispatcherRegistered = false;
            }

            CompletePredatorSightBatch(forceComplete: true);
            ReleasePredatorSightBuffers();
            CompletePredatorSpatialHashBuild(forceComplete: true);
            ReleasePredatorSpatialHashBuffers();
            _encounterDirector.ForceCompleteActiveJobForTeardown();
            _encounterDirector.ClearPredatorAupPublication();
            _encounterDirector.Dispose();
            _dataVault = null;
            _nextDirectorSolveWarningTime = 0f;
            _directorSolveWarningClockSeconds = 0f;
            _lastEntityDeathSignalSnapshotGeneration = 0;
        }

        /// <summary>
        /// Executes one dispatcher step.
        /// </summary>
        /// <param name="deltaTime">Scaled frame delta supplied by the dispatcher.</param>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            long solveStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            RefreshRuntimeReferences(force: false);
            DrainEntityDeathSignals();
            if (playerTransform == null)
                return;

            FrameTimingManager.CaptureFrameTimings();
            float averageFrameTimeMs = UpdateFrameTimeAverage(deltaTime);

            if (!TryResolvePlayerRuntimeSnapshot(
                    out Vector3 playerPosition,
                    out Vector3 playerVelocity,
                    out Vector3 playerForward,
                    out AbsoluteUniversePosition playerAup))
            {
                return;
            }

            float surfaceWorldY = ResolveSurfaceWorldY(playerPosition);
            float healthNormalized = survivalSystem != null ? math.saturate(survivalSystem.IntegrityNormalized) : 1f;
            float oxygenNormalized = survivalSystem != null ? math.saturate(survivalSystem.OxygenNormalized) : 1f;
            float sonarStress = UpdateSonarStress(deltaTime);
            UpdateActiveSonarPingDebounce(deltaTime);
            float internalStress = ResolveInternalStress(healthNormalized, oxygenNormalized, sonarStress);
            float acousticThreatLevel = 0f;
            HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (vegetationBridge != null)
                acousticThreatLevel = math.saturate(vegetationBridge.GetThreatLevel(playerPosition));
            acousticThreatLevel = math.max(acousticThreatLevel, sonarStress);
            ApplyExternalPeakPressure(deltaTime, ref internalStress, ref acousticThreatLevel);
            UpdateHunterSquadPressure(deltaTime);

            if (RefreshEncounterFrustumPlanes(deltaTime, playerPosition, playerForward))
                _encounterDirector.CopyFrustumPlanes(_frustumPlaneScratch);

            EncounterFrameContext frameContext = new EncounterFrameContext
            {
                DeltaTime = deltaTime,
                PlayerPosition = playerPosition,
                PlayerVelocity = playerVelocity,
                PlayerForward = playerForward,
                PlayerHealthNormalized = healthNormalized,
                PlayerOxygenNormalized = oxygenNormalized,
                PlayerInternalStress = internalStress,
                AcousticThreatLevel = acousticThreatLevel,
                PlayerDepth = ResolvePlayerDepth(playerPosition, surfaceWorldY),
                AvgFrameTimeMs = averageFrameTimeMs,
                SurfaceWorldY = surfaceWorldY
            };

            _encounterDirector.Advance(frameContext, faunaDirector, this);
            SchedulePredatorSightBatch(deltaTime, playerPosition, playerVelocity, playerForward, in playerAup);

#if UNITY_EDITOR
            _debugStressLevel = _encounterDirector.StressLevel;
            _debugIntensityLevel = _encounterDirector.IntensityLevel;
            _debugTokenBudget = _encounterDirector.TokenBudget;
            _debugAverageFrameTimeMs = averageFrameTimeMs;
            _debugActiveEnemyCount = _encounterDirector.ActiveEnemyCount;
            _debugPhaseName = _encounterDirector.CurrentPhaseName;
#endif
            PublishDirectorSolveBudgetIfNeeded(solveStartTicks);
        }

        private void DrainEntityDeathSignals()
        {
            if (!_encounterDirector.CanProcessEntityDeathSignals)
                return;

            int snapshotGeneration = SignalBus<EntityDeathSignal>.SnapshotGeneration;
            if (snapshotGeneration == _lastEntityDeathSignalSnapshotGeneration)
                return;

            _lastEntityDeathSignalSnapshotGeneration = snapshotGeneration;
            ReadOnlySpan<EntityDeathSignal> signals = SignalBus<EntityDeathSignal>.GetFrameSnapshot();
            int signalCount = math.min(EntityDeathSignalDrainLimit, signals.Length);
            for (int i = 0; i < signalCount; i++)
            {
                EntityDeathSignal signal = signals[i];
                _encounterDirector.HandleEntityDeathSignal(in signal);
            }
        }

        public void LateFrameTick()
        {
            _encounterDirector.CompleteReadyOutput(faunaDirector, this, forceComplete: false);
            CompletePredatorSightBatch(forceComplete: false);
        }

        public void OnAcousticPing(in AcousticPingEvent pingEvent)
        {
            HandleAcousticPing(in pingEvent);
        }

        public void OnElectromagneticPulse(in ElectromagneticPulseEvent pulseEvent)
        {
            HandlePredatorAcousticDeafening(
                pulseEvent.RuntimePosition,
                pulseEvent.RadiusMeters,
                math.max(PredatorAcousticDeafenedDurationSeconds, pulseEvent.DurationSeconds));
        }

        public void OnAcousticImpulse(in AcousticImpulseEvent impulseEvent)
        {
            bool isLargeAcousticImpulse = (impulseEvent.Flags & AcousticImpulseFlags.Large) != 0;
            if (isLargeAcousticImpulse)
            {
                float rangeVisibility01 = math.saturate(impulseEvent.RadiusMeters * ActiveSonarLeviathanAggroInvRadiusMeters);
                HandleSonarPingSent(math.max(impulseEvent.Volume01, rangeVisibility01));
                if ((impulseEvent.Flags & AcousticImpulseFlags.Critical) == 0)
                    return;
            }

            if (impulseEvent.KineticEnergyJoules < PredatorAcousticDeafeningImpulseEnergyJoules)
                return;

            HandlePredatorAcousticDeafening(
                impulseEvent.RuntimePosition,
                impulseEvent.RadiusMeters,
                PredatorAcousticDeafenedDurationSeconds);
        }

        private void SubscribeAcousticPingEvents()
        {
            if (_acousticPingSubscribed || !Application.isPlaying)
                return;

            PhysicsEventBus.Register((IAcousticPingEventListener)this);
            PhysicsEventBus.Register((IElectromagneticPulseEventListener)this);
            PhysicsEventBus.Register((IPhysicsAcousticImpulseEventListener)this);
            _acousticPingSubscribed = true;
        }

        private void UnsubscribeAcousticPingEvents()
        {
            if (!_acousticPingSubscribed)
                return;

            PhysicsEventBus.Unregister((IAcousticPingEventListener)this);
            PhysicsEventBus.Unregister((IElectromagneticPulseEventListener)this);
            PhysicsEventBus.Unregister((IPhysicsAcousticImpulseEventListener)this);
            _acousticPingSubscribed = false;
        }

        private void HandleAcousticPing(in AcousticPingEvent pingEvent)
        {
            if (pingEvent.RadiusMeters <= 0f || pingEvent.Intensity01 <= 0f)
                return;

            if (_activeSonarPingDebounceTimer > 0f)
                return;

            _activeSonarPingDebounceTimer = ActiveSonarPingDebounceSeconds;
            if (!TryResolveAupFromRuntimeOrigin(pingEvent.RuntimePosition, out AbsoluteUniversePosition pingAup))
                return;

            int contactCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                in pingAup,
                ActiveSonarLeviathanAggroRadiusMeters,
                SpatialTargetKind.Bioform,
                _acousticPingPredatorContacts);

            bool raisedThreatSpike = false;
            for (int i = 0; i < contactCount; i++)
            {
                FaunaBrain brain = _acousticPingPredatorContacts[i].Owner as FaunaBrain;
                if (brain == null ||
                    brain.IsDead ||
                    !brain.IsApexPredatorRuntime)
                {
                    continue;
                }

                AbsoluteUniversePosition brainAup = _acousticPingPredatorContacts[i].PositionAup;
                if (AbsoluteUniversePosition.DistanceSq(in brainAup, in pingAup) > ActiveSonarLeviathanAggroRadiusMetersSqr)
                    continue;

                if (brain.ShouldIgnoreAcousticPing(pingEvent.EnergyJoules, pingEvent.Intensity01))
                    continue;

                brain.ApplyAcousticPingAggro(
                    pingEvent.RuntimePosition,
                    pingEvent.Intensity01,
                    ActiveSonarLeviathanAggroDurationSeconds);

                SargassumMicroFaunaBoids boidSystem = _sargassumMicroFauna;
                if (boidSystem != null)
                {
                    Vector3 direction = _acousticPingPredatorContacts[i].Position - pingEvent.RuntimePosition;
                    if (direction.sqrMagnitude <= 0.0001f)
                        direction = ResolveDeterministicOffsetDirection(unchecked((uint)brain.SpeciesId));
                    else
                        direction = ResolveDominantAxisDirection(direction);

                    boidSystem.RegisterLeviathanThreatPulse(
                        _acousticPingPredatorContacts[i].Position,
                        direction,
                        ActiveSonarBoidScatterRadiusMeters,
                        ActiveSonarBoidScatterDurationSeconds);
                }

                if (!raisedThreatSpike)
                {
                    DirectorAIEvents.RaiseThreatSpike(_acousticPingPredatorContacts[i].Position, pingEvent.Intensity01);
                    raisedThreatSpike = true;
                }
            }
        }

        private void HandlePredatorAcousticDeafening(Vector3 runtimePosition, float radiusMeters, float durationSeconds)
        {
            if (radiusMeters <= 0f || durationSeconds <= 0f)
                return;

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition pulseAup))
                return;

            int contactCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                in pulseAup,
                radiusMeters,
                SpatialTargetKind.Bioform,
                _acousticPingPredatorContacts);

            double radiusSq = (double)radiusMeters * radiusMeters;
            for (int i = 0; i < contactCount; i++)
            {
                FaunaBrain brain = _acousticPingPredatorContacts[i].Owner as FaunaBrain;
                if (brain == null ||
                    brain.IsDead ||
                    !brain.IsApexPredatorRuntime)
                {
                    continue;
                }

                AbsoluteUniversePosition brainAup = _acousticPingPredatorContacts[i].PositionAup;
                if (AbsoluteUniversePosition.DistanceSq(in brainAup, in pulseAup) > radiusSq)
                    continue;

                brain.ApplyPredatorDeafening(runtimePosition, durationSeconds);
            }
        }

        private void UpdateActiveSonarPingDebounce(float deltaTime)
        {
            if (_activeSonarPingDebounceTimer <= 0f)
                return;

            _activeSonarPingDebounceTimer = math.max(0f, _activeSonarPingDebounceTimer - deltaTime);
        }

        private void SchedulePredatorSightBatch(
            float deltaTime,
            Vector3 playerPosition,
            Vector3 playerVelocity,
            Vector3 playerForward,
            in AbsoluteUniversePosition playerAup)
        {
            CompletePredatorSpatialHashBuild(forceComplete: false);
            if (_predatorSightJobScheduled)
                return;

            if (_predatorSightCooldown > 0f)
            {
                _predatorSightCooldown = math.max(0f, _predatorSightCooldown - deltaTime);
                return;
            }

            if (!EnsurePredatorSightBuffersAllocated(
                    out NativeArray<PredatorSightRaycastInput> sightInputs,
                    out NativeArray<RaycastCommand> sightCommands,
                    out NativeArray<RaycastHit> sightHits) ||
                !EnsurePredatorSpatialHashBuffersAllocated(
                    out NativeArray<double3> spatialAbsolutePositions,
                    out NativeArray<int3> spatialCellCoords))
            {
                return;
            }

            _predatorSightCooldown = PredatorSightIntervalSeconds;
            if (!_predatorSpatialHashReady)
            {
                SchedulePredatorSpatialHashRefresh(in playerAup, spatialAbsolutePositions, spatialCellCoords);
                return;
            }

            int requestCount = 0;
            Vector3 safePlayerForward = playerForward.sqrMagnitude > 0.0001f ? ResolveDominantAxisDirection(playerForward) : Vector3.forward;
            Vector3 playerProbe = playerPosition + Vector3.up * PredatorSightProbeOffsetMeters;
            int3 playerCell = PredatorSpatialHashMath.ResolveCellCoord(playerAup.ToAbsoluteDouble3(), PredatorSpatialHashCellSizeMeters);
            for (int contactIndex = 0;
                 contactIndex < _predatorSpatialContactCount && requestCount < PredatorSightMaxRaysPerFrame;
                 contactIndex++)
            {
                int3 cellDelta = math.abs(spatialCellCoords[contactIndex] - playerCell);
                if (math.cmax(cellDelta) > 1)
                    continue;

                ProcessPredatorSightContact(
                    contactIndex,
                    in playerAup,
                    playerPosition,
                    playerVelocity,
                    safePlayerForward,
                    playerProbe,
                    sightInputs,
                    ref requestCount);
            }

            SchedulePredatorSpatialHashRefresh(in playerAup, spatialAbsolutePositions, spatialCellCoords);

            if (requestCount <= 0)
                return;

            if (!TryLockPredatorSightVaultBuffers())
                return;

            PredatorSightRaycastBuildJob buildJob = new PredatorSightRaycastBuildJob
            {
                Inputs = sightInputs,
                Commands = sightCommands,
                RequestCount = requestCount
            };

            JobHandle buildHandle = buildJob.Schedule(PredatorSightMaxRaysPerFrame, 1);
            _predatorSightRaycastHandle = RaycastCommand.ScheduleBatch(
                sightCommands,
                sightHits,
                1,
                buildHandle);
            _predatorSightScheduledCount = requestCount;
            _predatorSightJobScheduled = true;
        }

        private void ProcessPredatorSightContact(
            int contactIndex,
            in AbsoluteUniversePosition playerAup,
            Vector3 playerPosition,
            Vector3 playerVelocity,
            Vector3 safePlayerForward,
            Vector3 playerProbe,
            NativeArray<PredatorSightRaycastInput> sightInputs,
            ref int requestCount)
        {
            SpatialQueryHit contact = _predatorSpatialContacts[contactIndex];
            FaunaBrain brain = contact.Owner as FaunaBrain;
            if (brain == null ||
                brain.IsDead ||
                (!brain.isAggressive && !brain.IsApexPredatorRuntime && !brain.UsesPackHuntBehavior))
            {
                return;
            }

            AbsoluteUniversePosition predatorAup = contact.PositionAup;
            double predatorDistanceSqr = AbsoluteUniversePosition.DistanceSq(in predatorAup, in playerAup);
            bool outsideFrustum = IsPredatorBehindPlayerViewByAup(in playerAup, in predatorAup, safePlayerForward);
            if (predatorDistanceSqr > PredatorDeadZoneCullDistanceMetersSqr && outsideFrustum)
            {
                brain.ApplyDirectorColdTickCull(true);
                return;
            }

            brain.ApplyDirectorColdTickCull(false);
            if (predatorDistanceSqr > (double)PredatorSightScanRadiusMeters * PredatorSightScanRadiusMeters)
                return;

            Vector3 predatorProbe = contact.Position + Vector3.up * PredatorSightProbeOffsetMeters;
            Vector3 toPlayer = playerProbe - predatorProbe;
            float distanceSqr = toPlayer.sqrMagnitude;
            if (distanceSqr <= 0.25f)
                return;

            Vector3 predatorForward = contact.Transform != null ? contact.Transform.forward : Vector3.forward;
            if (!IsInsidePredatorSightCone(predatorForward, toPlayer, distanceSqr))
            {
                brain.ApplyDirectorLineOfSight(false, playerPosition, safePlayerForward, playerVelocity);
                return;
            }

            if (predatorDistanceSqr <= PredatorSightImmediateRevealRadiusMetersSqr)
            {
                brain.ApplyDirectorLineOfSight(true, playerPosition, safePlayerForward, playerVelocity);
                return;
            }

            if (predatorDistanceSqr >= PredatorSightRearViewFakeMinDistanceMetersSqr && outsideFrustum)
            {
                brain.ApplyDirectorLineOfSight(false, playerPosition, safePlayerForward, playerVelocity);
                return;
            }

            sightInputs[requestCount] = new PredatorSightRaycastInput
                {
                    Origin = (float3)predatorProbe,
                    Target = (float3)playerProbe,
                    LayerMask = PredatorSightLayerMask
                };
            _predatorSightBrains[requestCount] = brain;
            _predatorSightPlayerPositions[requestCount] = playerPosition;
            _predatorSightPlayerVelocities[requestCount] = playerVelocity;
            _predatorSightPlayerForwards[requestCount] = safePlayerForward;
            requestCount++;
        }

        private static bool IsInsidePredatorSightCone(Vector3 predatorForward, Vector3 toPlayer, float distanceSqr)
        {
            float3 forward = (float3)predatorForward;
            float forwardLengthSq = math.lengthsq(forward);
            if (forwardLengthSq <= 0.0001f || distanceSqr <= 0.0001f)
                return true;

            float forwardDot = math.dot(forward, (float3)toPlayer);
            return forwardDot > 0f &&
                   (forwardDot * forwardDot) >= PredatorSightConeDotThresholdSqr * forwardLengthSq * distanceSqr;
        }

        private void SchedulePredatorSpatialHashRefresh(
            in AbsoluteUniversePosition playerAup,
            NativeArray<double3> spatialAbsolutePositions,
            NativeArray<int3> spatialCellCoords)
        {
            if (_predatorSpatialHashJobScheduled ||
                !spatialAbsolutePositions.IsCreated ||
                !spatialCellCoords.IsCreated)
            {
                return;
            }

            if (!TryLockPredatorSpatialHashVaultBuffers())
                return;

            int contactCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                in playerAup,
                PredatorSpatialHashActiveChunkRadiusMeters,
                SpatialTargetKind.Bioform,
                _predatorSpatialContacts);
            _predatorSpatialContactCount = math.min(contactCount, PredatorSpatialHashContactCapacity);
            for (int i = 0; i < _predatorSpatialContactCount; i++)
            {
                AbsoluteUniversePosition contactAup = _predatorSpatialContacts[i].PositionAup;
                spatialAbsolutePositions[i] = contactAup.ToAbsoluteDouble3();
                spatialCellCoords[i] = default;
            }

            for (int i = _predatorSpatialContactCount; i < PredatorSpatialHashContactCapacity; i++)
            {
                _predatorSpatialContacts[i] = default;
                spatialAbsolutePositions[i] = default;
                spatialCellCoords[i] = default;
            }

            PredatorSpatialHashInsertJob insertJob = new PredatorSpatialHashInsertJob
            {
                AbsolutePositions = spatialAbsolutePositions,
                CellCoords = spatialCellCoords,
                Count = _predatorSpatialContactCount,
                CellSizeMeters = PredatorSpatialHashCellSizeMeters
            };
            _predatorSpatialHashBuildHandle = insertJob.Schedule(PredatorSpatialHashContactCapacity, 16);
            _predatorSpatialHashJobScheduled = true;
            _predatorSpatialHashReady = false;
        }

        private bool CompletePredatorSpatialHashBuild(bool forceComplete)
        {
            if (!_predatorSpatialHashJobScheduled)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _predatorSpatialHashBuildHandle, forceComplete))
                return false;

            _predatorSpatialHashJobScheduled = false;
            _predatorSpatialHashReady = _predatorSpatialContactCount > 0;
            UnlockPredatorSpatialHashVaultBuffers();
            return true;
        }

        private static bool IsPredatorBehindPlayerViewByAup(
            in AbsoluteUniversePosition playerAup,
            in AbsoluteUniversePosition predatorAup,
            Vector3 playerForward)
        {
            double3 predatorDelta = AbsoluteUniversePosition.DeltaMetersClamped(in predatorAup, in playerAup);
            double distanceSqr = math.dot(predatorDelta, predatorDelta);
            if (!math.isfinite(distanceSqr) || distanceSqr <= 0.0001d)
                return false;

            float3 safeForward = (float3)ResolveDominantAxisDirection(playerForward);
            double forwardProjection =
                ((double)safeForward.x * predatorDelta.x) +
                ((double)safeForward.y * predatorDelta.y) +
                ((double)safeForward.z * predatorDelta.z);
            if (forwardProjection >= 0d)
                return false;

            double rearThresholdSq = (double)PredatorSightRearViewDotThreshold * PredatorSightRearViewDotThreshold;
            return (forwardProjection * forwardProjection) >= rearThresholdSq * distanceSqr;
        }

        private static Vector3 ResolveDominantAxisDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
                return Vector3.forward;

            float absX = math.abs(direction.x);
            float absY = math.abs(direction.y);
            float absZ = math.abs(direction.z);

            if (absX >= absY && absX >= absZ)
                return direction.x < 0f ? Vector3.left : Vector3.right;

            if (absY >= absZ)
                return direction.y < 0f ? Vector3.down : Vector3.up;

            return direction.z < 0f ? Vector3.back : Vector3.forward;
        }

        private bool CompletePredatorSightBatch(bool forceComplete)
        {
            if (!_predatorSightJobScheduled)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _predatorSightRaycastHandle, forceComplete))
                return false;

            bool hasHits = TryOpenDirectorVaultView(
                in _predatorSightHitsHandle,
                PredatorSightMaxRaysPerFrame,
                out NativeArray<RaycastHit> sightHits);
            int count = math.min(_predatorSightScheduledCount, PredatorSightMaxRaysPerFrame);
            for (int i = 0; i < count; i++)
            {
                FaunaBrain brain = _predatorSightBrains[i];
                if (brain == null || brain.IsDead)
                    continue;

                RaycastHit hit = hasHits ? sightHits[i] : default;
                bool hasLineOfSight = hit.collider == null;
                brain.ApplyDirectorLineOfSight(
                    hasLineOfSight,
                    _predatorSightPlayerPositions[i],
                    _predatorSightPlayerForwards[i],
                    _predatorSightPlayerVelocities[i]);
            }

            ClearPredatorSightMirrors(count);
            _predatorSightScheduledCount = 0;
            _predatorSightJobScheduled = false;
            UnlockPredatorSightVaultBuffers();
            return true;
        }

        private void ClearPredatorSightMirrors(int count)
        {
            int clampedCount = math.min(count, PredatorSightMaxRaysPerFrame);
            bool hasInputs = TryOpenDirectorVaultView(
                in _predatorSightInputsHandle,
                PredatorSightMaxRaysPerFrame,
                out NativeArray<PredatorSightRaycastInput> sightInputs);
            bool hasHits = TryOpenDirectorVaultView(
                in _predatorSightHitsHandle,
                PredatorSightMaxRaysPerFrame,
                out NativeArray<RaycastHit> sightHits);
            for (int i = 0; i < clampedCount; i++)
            {
                _predatorSightBrains[i] = null;
                _predatorSightPlayerPositions[i] = default;
                _predatorSightPlayerVelocities[i] = default;
                _predatorSightPlayerForwards[i] = default;
                if (hasInputs)
                    sightInputs[i] = default;
                if (hasHits)
                    sightHits[i] = default;
            }
        }

        private bool EnsurePredatorSightBuffersAllocated(
            out NativeArray<PredatorSightRaycastInput> sightInputs,
            out NativeArray<RaycastCommand> sightCommands,
            out NativeArray<RaycastHit> sightHits)
        {
            sightInputs = default;
            sightCommands = default;
            sightHits = default;
            return TryResolveOrAcquireDirectorVaultBuffer(
                       ref _predatorSightInputsHandle,
                       PredatorSightInputsBufferId,
                       PredatorSightMaxRaysPerFrame,
                       NativeArrayOptions.ClearMemory,
                       out sightInputs) &&
                   TryResolveOrAcquireDirectorVaultBuffer(
                       ref _predatorSightCommandsHandle,
                       PredatorSightCommandsBufferId,
                       PredatorSightMaxRaysPerFrame,
                       NativeArrayOptions.ClearMemory,
                       out sightCommands) &&
                   TryResolveOrAcquireDirectorVaultBuffer(
                       ref _predatorSightHitsHandle,
                       PredatorSightHitsBufferId,
                       PredatorSightMaxRaysPerFrame,
                       NativeArrayOptions.ClearMemory,
                       out sightHits);
        }

        private void ReleasePredatorSightBuffers()
        {
            ReleasePredatorSightBuffers(_dataVault);
        }

        private void ReleasePredatorSightBuffers(IDataVault vault)
        {
            UnlockPredatorSightVaultBuffers();
            ReleaseDirectorVaultHandle(vault, ref _predatorSightInputsHandle);
            ReleaseDirectorVaultHandle(vault, ref _predatorSightCommandsHandle);
            ReleaseDirectorVaultHandle(vault, ref _predatorSightHitsHandle);
        }

        private bool EnsurePredatorSpatialHashBuffersAllocated(
            out NativeArray<double3> spatialAbsolutePositions,
            out NativeArray<int3> spatialCellCoords)
        {
            spatialAbsolutePositions = default;
            spatialCellCoords = default;
            return TryResolveOrAcquireDirectorVaultBuffer(
                       ref _predatorSpatialAbsolutePositionsHandle,
                       PredatorSpatialAbsolutePositionsBufferId,
                       PredatorSpatialHashContactCapacity,
                       NativeArrayOptions.ClearMemory,
                       out spatialAbsolutePositions) &&
                   TryResolveOrAcquireDirectorVaultBuffer(
                       ref _predatorSpatialCellCoordsHandle,
                       PredatorSpatialCellCoordsBufferId,
                       PredatorSpatialHashContactCapacity,
                       NativeArrayOptions.ClearMemory,
                       out spatialCellCoords);
        }

        private void ReleasePredatorSpatialHashBuffers()
        {
            ReleasePredatorSpatialHashBuffers(_dataVault);
        }

        private void ReleasePredatorSpatialHashBuffers(IDataVault vault)
        {
            CompletePredatorSpatialHashBuild(forceComplete: true);

            UnlockPredatorSpatialHashVaultBuffers();
            ReleaseDirectorVaultHandle(vault, ref _predatorSpatialAbsolutePositionsHandle);
            ReleaseDirectorVaultHandle(vault, ref _predatorSpatialCellCoordsHandle);
            _predatorSpatialHashReady = false;
            _predatorSpatialContactCount = 0;
        }

        private bool TryResolveOrAcquireDirectorVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null || requiredLength <= 0)
                return false;

            if (TryOpenDirectorVaultView(in handle, requiredLength, out buffer))
                return true;

            if (vault.TryGetGenerationHandle(bufferId, out handle) &&
                TryOpenDirectorVaultView(in handle, requiredLength, out buffer))
            {
                return true;
            }

            if (vault.IsAllocationLocked)
            {
                handle = default;
                buffer = default;
                return false;
            }

            handle = vault.GetGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.AICognition,
                options);
            return TryOpenDirectorVaultView(in handle, requiredLength, out buffer);
        }

        private bool TryOpenDirectorVaultView<T>(
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   handle.BufferID != 0u &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static void ReleaseDirectorVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null && handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private bool TryLockPredatorSightVaultBuffers()
        {
            if (_predatorSightVaultLocked)
                return true;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !vault.TryLockBuffer(PredatorSightInputsBufferId, SystemID.AICognition))
            {
                return false;
            }

            if (!vault.TryLockBuffer(PredatorSightCommandsBufferId, SystemID.AICognition))
            {
                vault.TryUnlockBuffer(PredatorSightInputsBufferId, SystemID.AICognition);
                return false;
            }

            if (!vault.TryLockBuffer(PredatorSightHitsBufferId, SystemID.AICognition))
            {
                vault.TryUnlockBuffer(PredatorSightCommandsBufferId, SystemID.AICognition);
                vault.TryUnlockBuffer(PredatorSightInputsBufferId, SystemID.AICognition);
                return false;
            }

            _predatorSightVaultLocked = true;
            return true;
        }

        private void UnlockPredatorSightVaultBuffers()
        {
            if (!_predatorSightVaultLocked)
                return;

            IDataVault vault = _dataVault;
            if (vault != null)
            {
                vault.TryUnlockBuffer(PredatorSightHitsBufferId, SystemID.AICognition);
                vault.TryUnlockBuffer(PredatorSightCommandsBufferId, SystemID.AICognition);
                vault.TryUnlockBuffer(PredatorSightInputsBufferId, SystemID.AICognition);
            }

            _predatorSightVaultLocked = false;
        }

        private bool TryLockPredatorSpatialHashVaultBuffers()
        {
            if (_predatorSpatialHashVaultLocked)
                return true;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !vault.TryLockBuffer(PredatorSpatialAbsolutePositionsBufferId, SystemID.AICognition))
            {
                return false;
            }

            if (!vault.TryLockBuffer(PredatorSpatialCellCoordsBufferId, SystemID.AICognition))
            {
                vault.TryUnlockBuffer(PredatorSpatialAbsolutePositionsBufferId, SystemID.AICognition);
                return false;
            }

            _predatorSpatialHashVaultLocked = true;
            return true;
        }

        private void UnlockPredatorSpatialHashVaultBuffers()
        {
            if (!_predatorSpatialHashVaultLocked)
                return;

            IDataVault vault = _dataVault;
            if (vault != null)
            {
                vault.TryUnlockBuffer(PredatorSpatialCellCoordsBufferId, SystemID.AICognition);
                vault.TryUnlockBuffer(PredatorSpatialAbsolutePositionsBufferId, SystemID.AICognition);
            }

            _predatorSpatialHashVaultLocked = false;
        }

        private void PublishDirectorSolveBudgetIfNeeded(long solveStartTicks)
        {
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - solveStartTicks;
            double elapsedMilliseconds = elapsedTicks * _StopwatchTickToMilliseconds;
            _directorSolveWarningClockSeconds += math.max(0f, SystemDispatcher.CurrentFrameUnscaledDeltaTime);
            if (elapsedMilliseconds <= DirectorSolveBudgetMilliseconds)
                return;

            float now = _directorSolveWarningClockSeconds;
            if (now < _nextDirectorSolveWarningTime)
                return;

            _nextDirectorSolveWarningTime = now + DirectorSolveWarningCooldownSeconds;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _DirectorSolveBudgetWarningHash,
                _DirectorTelemetryContextHash,
                (float)math.min(elapsedMilliseconds, float.MaxValue));
        }

        /// <summary>
        /// Forces the next completed encounter tick into the Peak phase.
        /// </summary>
        public void ForcePeak()
        {
            _encounterDirector.RequestPhaseOverride(EncounterPhase.Peak);
        }

        /// <summary>
        /// Resets the runtime encounter state.
        /// </summary>
        public void ResetDirector()
        {
            _encounterDirector.RequestReset();
        }

        /// <summary>
        /// Forces the next completed encounter tick into the Relax phase.
        /// </summary>
        public void ForceRelax()
        {
            _encounterDirector.RequestPhaseOverride(EncounterPhase.Relax);
        }

        /// <summary>
        /// Applies external hostility pressure that extends Peak pacing without allocating auxiliary state machines.
        /// </summary>
        public void ApplyExternalPeakPressure(float pressure01, float holdSeconds)
        {
            float clampedPressure = math.saturate(pressure01);
            if (clampedPressure <= 0f || holdSeconds <= 0f)
                return;

            _externalPeakPressure01 = math.max(_externalPeakPressure01, clampedPressure);
            _externalPeakHoldSeconds = math.max(_externalPeakHoldSeconds, holdSeconds);
        }

        /// <summary>
        /// Legacy predator registration hook retained for compatibility.
        /// </summary>
        /// <param name="collider">Predator collider.</param>
        public static void RegisterPredator(Collider collider)
        {
        }

        /// <summary>
        /// Legacy predator unregistration hook retained for compatibility.
        /// </summary>
        /// <param name="collider">Predator collider.</param>
        public static void UnregisterPredator(Collider collider)
        {
        }

        /// <summary>
        /// Legacy global predator registration clear retained for compatibility.
        /// </summary>
        public static void ClearAllPredatorRegistrations()
        {
        }

        internal void HandleEncounterPhaseChanged(EncounterPhase previousPhase, EncounterPhase newPhase)
        {
            PublishPredatorPressure(newPhase != EncounterPhase.Relax);

            if (!TryResolvePlayerRuntimePosition(out Vector3 playerPosition))
                return;

            uint seed = EncounterDirector.BuildDeterministicSeed(playerPosition, _encounterDirector.FrameIndex, (int)newPhase, _encounterDirector.ActiveEnemyCount);
            Vector3 eventPosition = ResolveDeterministicOffsetPosition(playerPosition, seed, eventOffsetRadius);

            switch (newPhase)
            {
                case EncounterPhase.Peak:
                    DirectorAIEvents.RaiseEquipmentGlitchRequested(math.lerp(0.35f, 0.85f, _encounterDirector.IntensityLevel));
                    DirectorAIEvents.RaiseMissionTriggerRequested(eventPosition);
                    break;

                case EncounterPhase.Decay:
                    DirectorAIEvents.RaiseWeatherShiftRequested(math.lerp(0.2f, 0.6f, _encounterDirector.StressLevel));
                    break;

                case EncounterPhase.Relax:
                    DirectorAIEvents.RaiseRareDiscoveryRequested(eventPosition);
                    break;
            }
        }

        internal void HandleThreatSpawned(EncounterThreatClass threatClass, Vector3 spawnPosition)
        {
            if (threatClass == EncounterThreatClass.Swarm)
                DirectorAIEvents.RaiseSpawnHordeRequested(spawnPosition);

            if (threatClass == EncounterThreatClass.Leviathan)
                DirectorAIEvents.RaiseThreatSpike(spawnPosition, 1f);
            else if (threatClass == EncounterThreatClass.Stalker)
                DirectorAIEvents.RaiseThreatSpike(spawnPosition, 0.75f);
        }

        private void RefreshRuntimeReferences(bool force)
        {
            if (!force && _resolveRetryTimer > 0f)
            {
                _resolveRetryTimer -= SystemDispatcher.CurrentFrameUnscaledDeltaTime;
                return;
            }

            _resolveRetryTimer = 1f;

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (survivalSystem == null && playerTransform != null)
                playerTransform.TryGetComponent(out survivalSystem);

            if (_playerMovement == null && playerTransform != null)
                playerTransform.TryGetComponent(out _playerMovement);

            if (faunaDirector == null)
                WorldRuntimeReferenceUtility.TryResolveFaunaDirector(ref faunaDirector);

            if (playerCamera == null && playerTransform != null)
            {
                IPlayerRuntimeContext playerContext = _playerRuntimeContext;
                if (playerContext != null && playerContext.PlayerCamera != null)
                    playerCamera = playerContext.PlayerCamera;
                else
                    playerTransform.TryGetComponent(out playerCamera);
            }

            RefreshMetaCampaignService();
        }

        private void RefreshColdRegistryReferences()
        {
            _playerRuntimeContext = GlobalRegistry.Player;
            _ecosystemDirector = GlobalRegistry.EcosystemDirector;
            _sargassumMicroFauna = GlobalRegistry.SargassumMicroFauna;
            _dataVault = GlobalRegistry.DataVault;
            BindMetaCampaignService(GlobalRegistry.MetaCampaign);
        }

        private void RefreshMetaCampaignService()
        {
            BindMetaCampaignService(_metaCampaignService);
        }

        private void BindMetaCampaignService(IMetaCampaignService service)
        {
            if (ReferenceEquals(_metaCampaignService, service))
                return;

            _metaCampaignService = service;
            _encounterDirector.SetMetaCampaignService(service);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    if (playerCamera == null && _playerRuntimeContext != null)
                        playerCamera = _playerRuntimeContext.PlayerCamera;
                    break;
                case GlobalRegistryServiceSlot.EcosystemDirector:
                    _ecosystemDirector = currentService as IEcosystemDirectorService;
                    break;
                case GlobalRegistryServiceSlot.SargassumMicroFaunaRuntime:
                    _sargassumMicroFauna = currentService as SargassumMicroFaunaBoids;
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    CompletePredatorSightBatch(forceComplete: true);
                    CompletePredatorSpatialHashBuild(forceComplete: true);
                    ReleasePredatorSightBuffers(_dataVault ?? (previousService as IDataVault));
                    ReleasePredatorSpatialHashBuffers(_dataVault ?? (previousService as IDataVault));
                    _dataVault = currentService as IDataVault;
                    break;
                case GlobalRegistryServiceSlot.MetaCampaignRuntime:
                    BindMetaCampaignService(currentService as IMetaCampaignService);
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryRegisterDispatcherLanes();
                    break;
            }
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

        private float UpdateFrameTimeAverage(float deltaTime)
        {
            uint timingCount = FrameTimingManager.GetLatestTimings(1u, _frameTimingScratch);
            float sampleMs = timingCount > 0u ? (float)_frameTimingScratch[0].cpuFrameTime : deltaTime * 1000f;
            if (sampleMs <= 0f)
                sampleMs = deltaTime * 1000f;

            _frameTimeHistory[_frameTimeHistoryIndex] = sampleMs;
            _frameTimeHistoryIndex++;
            if (_frameTimeHistoryIndex >= _frameTimeHistory.Length)
                _frameTimeHistoryIndex = 0;

            if (_frameTimeHistoryCount < _frameTimeHistory.Length)
                _frameTimeHistoryCount++;

            float sum = 0f;
            for (int i = 0; i < _frameTimeHistoryCount; i++)
                sum += _frameTimeHistory[i];

            return _frameTimeHistoryCount > 0 ? sum * math.rcp((float)_frameTimeHistoryCount) : sampleMs;
        }

        private bool RefreshEncounterFrustumPlanes(float deltaTime, Vector3 playerPosition, Vector3 playerForward)
        {
            if (_frustumPlanesInitialized)
            {
                _frustumPlaneRefreshTimer = math.max(0f, _frustumPlaneRefreshTimer - math.max(0f, deltaTime));
                if (_frustumPlaneRefreshTimer > 0f)
                    return false;
            }

            if (playerCamera != null)
                GeometryUtility.CalculateFrustumPlanes(playerCamera, _frustumPlaneScratch);
            else
                EncounterDirector.FillFallbackFrustumPlanes(playerPosition, playerForward, _frustumPlaneScratch);

            _frustumPlanesInitialized = true;
            _frustumPlaneRefreshTimer = DirectorFrustumPlaneRefreshIntervalSeconds;
            return true;
        }

        private static bool TryResolvePlayerRuntimePosition(out Vector3 playerPosition)
        {
            playerPosition = Vector3.zero;
            if (!PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) ||
                runtimeContext == null ||
                (runtimeContext.MovementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u)
            {
                return false;
            }

            playerPosition = ToVector3(runtimeContext.MovementState.PredictedAup.ToRuntimeFloat3());
            return true;
        }

        private static Vector3 SanitizeFiniteVector(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z)
                ? value
                : Vector3.zero;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!float.IsFinite(runtimePosition.x) ||
                !float.IsFinite(runtimePosition.y) ||
                !float.IsFinite(runtimePosition.z))
            {
                return false;
            }

            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!math.all(math.isfinite(origin)))
                return false;

            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromAbsolutePosition(origin);
            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private bool TryResolvePlayerRuntimeSnapshot(
            out Vector3 playerPosition,
            out Vector3 playerVelocity,
            out Vector3 playerForward,
            out AbsoluteUniversePosition playerAup)
        {
            playerPosition = default;
            playerVelocity = default;
            playerForward = _lastResolvedPlayerForward.sqrMagnitude > 0.0001f
                ? _lastResolvedPlayerForward
                : Vector3.forward;
            playerAup = default;

            if (!PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) ||
                runtimeContext == null ||
                (runtimeContext.MovementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u)
            {
                return false;
            }

            PlayerMovementRuntimeState movementState = runtimeContext.MovementState;
            PlayerLookState lookState = runtimeContext.LookState;
            playerAup = movementState.PredictedAup;
            playerPosition = ToVector3(playerAup.ToRuntimeFloat3());
            playerVelocity = SanitizeFiniteVector(ToVector3(movementState.Velocity));

            Vector3 lookForward = ToVector3(lookState.AimForward);
            if (lookForward.sqrMagnitude > 0.0001f)
            {
                _lastResolvedPlayerForward = ResolveDominantAxisDirection(lookForward);
                playerForward = _lastResolvedPlayerForward;
                return true;
            }

            Vector3 cameraForward = ToVector3(movementState.CameraForward);
            if (cameraForward.sqrMagnitude > 0.0001f)
            {
                _lastResolvedPlayerForward = ResolveDominantAxisDirection(cameraForward);
                playerForward = _lastResolvedPlayerForward;
                return true;
            }

            Vector3 movementForward = ToVector3(movementState.Forward);
            if (movementForward.sqrMagnitude > 0.0001f)
            {
                _lastResolvedPlayerForward = ResolveDominantAxisDirection(movementForward);
                playerForward = _lastResolvedPlayerForward;
            }

            return true;
        }

        private float ResolveSurfaceWorldY(Vector3 playerPosition)
        {
            if (survivalSystem == null)
                return 0f;

            return playerPosition.y + survivalSystem.Depth;
        }

        private float ResolvePlayerDepth(Vector3 playerPosition, float surfaceWorldY)
        {
            if (survivalSystem != null)
                return math.max(0f, survivalSystem.Depth);

            return math.max(0f, surfaceWorldY - playerPosition.y);
        }

        private float ResolveInternalStress(float healthNormalized, float oxygenNormalized, float sonarStress)
        {
            if (survivalSystem == null)
                return math.saturate(math.max(math.max(1f - healthNormalized, 1f - oxygenNormalized), sonarStress));

            float pressureStress = math.saturate(survivalSystem.PressureExposureSeverity01);
            float thermalStress = math.saturate(survivalSystem.ThermalStressSeverity01);
            float healthStress = 1f - healthNormalized;
            float oxygenStress = 1f - oxygenNormalized;
            return math.saturate(math.max(math.max(pressureStress, thermalStress), math.max(math.max(healthStress, oxygenStress), sonarStress)));
        }

        private float UpdateSonarStress(float deltaTime)
        {
            if (_recentSonarStress <= 0f)
                return 0f;

            _recentSonarStress = math.max(
                0f,
                _recentSonarStress - (math.max(0f, SonarStressDecayPerSecond) * math.max(0f, deltaTime)));
            return _recentSonarStress;
        }

        private void HandleSonarPingSent(float intensity)
        {
            float clampedIntensity = math.saturate(intensity);
            if (clampedIntensity <= 0f)
                return;

            _recentSonarStress = math.max(_recentSonarStress, clampedIntensity);
        }

        void ISonarPingEventListener.OnSonarPingSent(float intensity)
        {
            HandleSonarPingSent(intensity);
        }

        private void ApplyExternalPeakPressure(float deltaTime, ref float internalStress, ref float acousticThreatLevel)
        {
            if (_externalPeakHoldSeconds <= 0f || _externalPeakPressure01 <= 0f)
                return;

            _externalPeakHoldSeconds = math.max(0f, _externalPeakHoldSeconds - deltaTime);
            internalStress = math.max(internalStress, _externalPeakPressure01);
            acousticThreatLevel = math.max(acousticThreatLevel, _externalPeakPressure01);

            if (_encounterDirector.CurrentPhase != EncounterPhase.Peak)
                _encounterDirector.RequestPhaseOverride(EncounterPhase.Peak);

            if (_externalPeakHoldSeconds > 0f)
                return;

            _externalPeakPressure01 = 0f;
        }

        private void UpdateHunterSquadPressure(float deltaTime)
        {
            if (_hunterSquadCooldown > 0f)
                _hunterSquadCooldown = math.max(0f, _hunterSquadCooldown - deltaTime);

            IEcosystemDirectorService ecosystemDirector = _ecosystemDirector;
            if (ecosystemDirector == null || ecosystemDirector.BiomeHostility01 < HunterSquadHostilityThreshold)
                return;

            if (_hunterSquadCooldown > 0f)
                return;

            _encounterDirector.RequestPhaseOverride(EncounterPhase.Peak);
            _encounterDirector.RequestForcedSquad(EncounterThreatClass.Stalker, HunterSquadSize);
            _hunterSquadCooldown = HunterSquadCooldownSeconds;
        }

        private void PublishPredatorPressure(bool enabled)
        {
            if (faunaDirector != null)
                faunaDirector.SetPredatorPressure(enabled);

            DirectorAIEvents.RaisePredatorPressureChanged(enabled);
        }

        private static Vector2[] BuildEventOffsetDirectionLut()
        {
            Vector2[] directions = new Vector2[EventOffsetDirectionLutSize];
            for (int i = 0; i < EventOffsetDirectionLutSize; i++)
                directions[i] = ResolveOctantDirection(i);

            return directions;
        }

        private static Vector3 ResolveDeterministicOffsetDirection(uint seed)
        {
            Vector2 direction = _eventOffsetDirectionLut[(int)((seed ^ 0xA511E9B3u) & EventOffsetDirectionLutMask)];
            return new Vector3(direction.x, 0f, direction.y);
        }

        private static Vector2 ResolveOctantDirection(int slot)
        {
            switch (slot & 0x7)
            {
                case 0:
                    return new Vector2(1f, 0f);
                case 1:
                    return new Vector2(0.70710678f, 0.70710678f);
                case 2:
                    return new Vector2(0f, 1f);
                case 3:
                    return new Vector2(-0.70710678f, 0.70710678f);
                case 4:
                    return new Vector2(-1f, 0f);
                case 5:
                    return new Vector2(-0.70710678f, -0.70710678f);
                case 6:
                    return new Vector2(0f, -1f);
                default:
                    return new Vector2(0.70710678f, -0.70710678f);
            }
        }

        private Vector3 ResolveDeterministicOffsetPosition(Vector3 origin, uint seed, float radius)
        {
            Vector2 direction = _eventOffsetDirectionLut[(int)((seed ^ 0xA511E9B3u) & EventOffsetDirectionLutMask)];
            int distanceBucket = (int)(((seed ^ 0x6C8E9CF5u) >> 8) & EventOffsetDistanceBucketMask);
            float distanceScale = EventOffsetMinRadiusScale + (EventOffsetDistanceStepScale * distanceBucket);
            float distance = radius * distanceScale;
            Vector3 offset = new Vector3(direction.x * distance, 0f, direction.y * distance);
            return origin + offset;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
