using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
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
        private static int s_x001DirectSignalPushDropCount_HectonDirectorAI;

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
        public static bool TryRaiseSpawnHordeRequested(Vector3 position)
        {
            bool musicPublished = PublishMusicSignal(SpawnHordeEventType, position, 0f, false);
            bool eventQueued = EnqueuePosition(SpawnHordeEventType, position);
            return musicPublished || eventQueued;
        }

        [Obsolete("DirectorAI producers must use TryRaiseSpawnHordeRequested and handle bounded enqueue failure.", true)]
        public static void RaiseSpawnHordeRequested(Vector3 position)
        {
            TryRaiseSpawnHordeRequested(position);
        }

        /// <summary>Queues an equipment glitch request.</summary>
        public static bool TryRaiseEquipmentGlitchRequested(float intensity)
        {
            bool musicPublished = PublishMusicSignal(EquipmentGlitchEventType, default, intensity, false);
            bool eventQueued = EnqueueValue(EquipmentGlitchEventType, intensity);
            return musicPublished || eventQueued;
        }

        [Obsolete("DirectorAI producers must use TryRaiseEquipmentGlitchRequested and handle bounded enqueue failure.", true)]
        public static void RaiseEquipmentGlitchRequested(float intensity)
        {
            TryRaiseEquipmentGlitchRequested(intensity);
        }

        /// <summary>Queues a rare-discovery request.</summary>
        public static bool TryRaiseRareDiscoveryRequested(Vector3 position)
        {
            bool musicPublished = PublishMusicSignal(RareDiscoveryEventType, position, 0f, false);
            bool eventQueued = EnqueuePosition(RareDiscoveryEventType, position);
            return musicPublished || eventQueued;
        }

        [Obsolete("DirectorAI producers must use TryRaiseRareDiscoveryRequested and handle bounded enqueue failure.", true)]
        public static void RaiseRareDiscoveryRequested(Vector3 position)
        {
            TryRaiseRareDiscoveryRequested(position);
        }

        /// <summary>Queues a weather shift request.</summary>
        public static bool TryRaiseWeatherShiftRequested(float intensity)
        {
            bool musicPublished = PublishMusicSignal(WeatherShiftEventType, default, intensity, false);
            bool eventQueued = EnqueueValue(WeatherShiftEventType, intensity);
            return musicPublished || eventQueued;
        }

        [Obsolete("DirectorAI producers must use TryRaiseWeatherShiftRequested and handle bounded enqueue failure.", true)]
        public static void RaiseWeatherShiftRequested(float intensity)
        {
            TryRaiseWeatherShiftRequested(intensity);
        }

        /// <summary>Queues a mission trigger request.</summary>
        public static bool TryRaiseMissionTriggerRequested(Vector3 position)
        {
            bool musicPublished = PublishMusicSignal(MissionTriggerEventType, position, 0f, false);
            bool eventQueued = EnqueuePosition(MissionTriggerEventType, position);
            return musicPublished || eventQueued;
        }

        [Obsolete("DirectorAI producers must use TryRaiseMissionTriggerRequested and handle bounded enqueue failure.", true)]
        public static void RaiseMissionTriggerRequested(Vector3 position)
        {
            TryRaiseMissionTriggerRequested(position);
        }

        /// <summary>Queues a predator pressure state change.</summary>
        public static bool TryRaisePredatorPressureChanged(bool pressureEnabled)
        {
            bool musicPublished = PublishMusicSignal(PredatorPressureEventType, default, pressureEnabled ? 1f : 0f, pressureEnabled);

            bool eventQueued = Enqueue(new DirectorAIEventPayload
            {
                EventType = PredatorPressureEventType,
                BoolValue = pressureEnabled ? (byte)1 : (byte)0
            });
            return musicPublished || eventQueued;
        }

        [Obsolete("DirectorAI producers must use TryRaisePredatorPressureChanged and handle bounded enqueue failure.", true)]
        public static void RaisePredatorPressureChanged(bool pressureEnabled)
        {
            TryRaisePredatorPressureChanged(pressureEnabled);
        }

        /// <summary>Queues a predator threat spike.</summary>
        public static bool TryRaiseThreatSpike(Vector3 position, float intensity)
        {
            Vector3 safePosition = SanitizeEventPosition(position);
            float clampedIntensity = SanitizeEvent01(intensity);
            bool musicPublished = PublishMusicSignal(ThreatSpikeEventType, safePosition, clampedIntensity, false);

            bool eventQueued = Enqueue(new DirectorAIEventPayload
            {
                EventType = ThreatSpikeEventType,
                Position = safePosition,
                Value = clampedIntensity
            });
            return musicPublished || eventQueued;
        }

        [Obsolete("DirectorAI producers must use TryRaiseThreatSpike and handle bounded enqueue failure.", true)]
        public static void RaiseThreatSpike(Vector3 position, float intensity)
        {
            TryRaiseThreatSpike(position, intensity);
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

        private static bool EnqueuePosition(byte eventType, Vector3 position)
        {
            return Enqueue(new DirectorAIEventPayload
            {
                EventType = eventType,
                Position = SanitizeEventPosition(position)
            });
        }

        private static bool EnqueueValue(byte eventType, float value)
        {
            return Enqueue(new DirectorAIEventPayload
            {
                EventType = eventType,
                Value = SanitizeEvent01(value)
            });
        }

        private static bool PublishMusicSignal(byte eventType, Vector3 position, float value, bool boolValue)
        {
            SignalBus<DirectorAIMusicSignal>.EnsureInitialized();
            DirectorAIMusicSignal signal = new DirectorAIMusicSignal(
                eventType,
                SanitizeEventPosition(position),
                SanitizeEvent01(value),
                boolValue);
            return SignalBus<DirectorAIMusicSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_HectonDirectorAI);
        }

        private static float SanitizeEvent01(float value)
        {
            return math.saturate(math.select(0f, value, math.isfinite(value)));
        }

        private static Vector3 SanitizeEventPosition(Vector3 position)
        {
            return float.IsFinite(position.x) && float.IsFinite(position.y) && float.IsFinite(position.z)
                ? position
                : Vector3.zero;
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

                if (payload.EventType == SpawnHordeEventType)
                    listener.OnDirectorSpawnHordeRequested(payload.Position);
                else if (payload.EventType == EquipmentGlitchEventType)
                    listener.OnDirectorEquipmentGlitchRequested(payload.Value);
                else if (payload.EventType == RareDiscoveryEventType)
                    listener.OnDirectorRareDiscoveryRequested(payload.Position);
                else if (payload.EventType == WeatherShiftEventType)
                    listener.OnDirectorWeatherShiftRequested(payload.Value);
                else if (payload.EventType == MissionTriggerEventType)
                    listener.OnDirectorMissionTriggerRequested(payload.Position);
                else if (payload.EventType == PredatorPressureEventType)
                    listener.OnDirectorPredatorPressureChanged(payload.BoolValue != 0);
                else if (payload.EventType == ThreatSpikeEventType)
                    listener.OnDirectorThreatSpike(payload.Position, payload.Value);
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

    /// <summary>
    /// Scene-facing compatibility owner for the encounter pacing director.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4500)]
    public sealed class HectonDirectorAI : MonoBehaviour, IUpdatable, ILateFrameTickable, IEncounterDirectorService, ISonarPingEventListener, IGlobalRegistryHotSwapListener
    {
        private static HectonDirectorAI s_activeRuntimeInstance;

        internal static HectonDirectorAI ActiveRuntimeInstance => s_activeRuntimeInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveRuntimeInstance()
        {
            s_activeRuntimeInstance = null;
        }

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
        private IDataVault _dataVault;
        private VaultGenerationHandle<double3> _predatorSpatialAbsolutePositionsHandle;
        private VaultGenerationHandle<int3> _predatorSpatialCellCoordsHandle;
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
        private const ushort PhysicsEventTypeElectromagneticPulse = 2;
        private const ushort PhysicsEventTypeAcousticPing = 3;
        private const ushort PhysicsEventTypeAcousticImpulse = 4;
        private const uint AcousticImpulseFlagCritical = 1u;
        private const uint AcousticImpulseFlagLarge = 1u << 3;
        private const int AcousticPingPredatorContactCapacity = 64;
        private const int PredatorSpatialHashContactCapacity = 64;
        private const BufferID PredatorSpatialAbsolutePositionsBufferId = (BufferID)73238;
        private const BufferID PredatorSpatialCellCoordsBufferId = (BufferID)73239;
        private static readonly ulong _predatorSpatialHashMutationGuardMask =
            PredatorSpatialHashMutationGuardBit(PredatorSpatialAbsolutePositionsBufferId) |
            PredatorSpatialHashMutationGuardBit(PredatorSpatialCellCoordsBufferId);
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
        private static readonly Vector2[] _eventOffsetDirectionLut = BuildEventOffsetDirectionLut(); // COLD ALLOC: Vector2[64] - deterministic director event offset directions - owner: HectonDirectorAI

        private HectonPlayerMovement _playerMovement;
        private IMetaCampaignService _metaCampaignService;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IEcosystemDirectorService _ecosystemDirector;
        private SargassumMicroFaunaBoids _sargassumMicroFauna;
        private ITerrainProvider _terrainProvider;
        private HectonMapMagicVegetationBridge _vegetationBridge;
        private IDataVault _predatorSpatialHashGuardVault;
        private bool _encounterDirectorServiceRegistered;
        private bool _dispatcherRegistered;
        private bool _lateFrameRegistered;
        private bool _hotSwapRegistered;
        private bool _physicsEventPayloadReaderActive;
        private bool _predatorSpatialHashBuffersPinned;
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
        private int _predatorSpatialContactCount;
        private int _lastPhysicsEventSnapshotGeneration;
        private int _lastEntityDeathSignalSnapshotGeneration;
        private Vector3 _lastResolvedPlayerForward = Vector3.forward;
        private bool _frustumPlanesInitialized;
        private float _recentSonarStress;
        private float _externalPeakPressure01;
        private float _externalPeakHoldSeconds;
        private float _lastPlayerStress01;
        private float _lastPlayerStressRaw01;
        private float _lastDirectorClutchFactor01;
        private int _lastPlayerStressSignalSequence;
        private int _lastPlayerStressSignalSeenFrame = int.MinValue;
        private const float HunterSquadHostilityThreshold = 0.8f;
        private const float HunterSquadCooldownSeconds = 9f;
        private const int HunterSquadSize = 3;
        private const int DirectorPlayerStressSignalFadeFrames = 45;
        private const float DirectorPlayerStressSignalFadeFrameRcp = 1f / DirectorPlayerStressSignalFadeFrames;
        private const float DirectorClutchFullHealth01 = 0.10f;
        private const float DirectorClutchHealthRamp01 = 0.15f;
        private const float DirectorClutchFullStress01 = 0.85f;
        private const float DirectorClutchStressRamp01 = 0.20f;
        private const byte DirectorDefaultPlayerArmorClass = (byte)CombatArmorClass.Suit;

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
        public bool IsInitialized => _encounterDirectorServiceRegistered &&
                                     ReferenceEquals(s_activeRuntimeInstance, this);

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
            TryRegisterDispatcherLanes();
            _encounterDirector.EnsureGpuResources();
            _encounterDirector.Reset();
            _frustumPlaneRefreshTimer = 0f;
            _frustumPlanesInitialized = false;
            _recentSonarStress = 0f;
            _externalPeakPressure01 = 0f;
            _externalPeakHoldSeconds = 0f;
            _lastPlayerStress01 = 0f;
            _lastPlayerStressRaw01 = 0f;
            _lastDirectorClutchFactor01 = 0f;
            _lastPlayerStressSignalSequence = 0;
            _lastPlayerStressSignalSeenFrame = int.MinValue;
            _hunterSquadCooldown = 0f;
            _predatorSightCooldown = 0f;
            _activeSonarPingDebounceTimer = 0f;
            _predatorSpatialHashReady = false;
            _predatorSpatialContactCount = 0;
            EnsurePredatorSpatialHashBuffersAllocated(out _, out _);
            SpectrumEvents.RegisterSonarPingListener(this);
            EnablePhysicsEventPayloadReader();
            PublishPredatorPressure(true);
        }

        private void Start()
        {
            if (!Application.isPlaying)
                return;

            EnsureEncounterDirectorServiceRegistered();
            RefreshColdRegistryReferences();
            TryRegisterHotSwapListener();
            TryRegisterDispatcherLanes();
        }

        private void EnsureEncounterDirectorServiceRegistered()
        {
            if (_encounterDirectorServiceRegistered)
                return;

            GlobalRegistry.RegisterEncounterDirectorService(this);
            _encounterDirectorServiceRegistered = true;
            s_activeRuntimeInstance = this;
        }

        private void TryRegisterDispatcherLanes()
        {
            if (!_dispatcherRegistered)
                _dispatcherRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);

            if (!_lateFrameRegistered)
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
        }

        private void TryUnregisterDispatcherLanes()
        {
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
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            if (_encounterDirectorServiceRegistered)
            {
                GlobalRegistry.UnregisterEncounterDirectorService(this);
                if (ReferenceEquals(s_activeRuntimeInstance, this))
                    s_activeRuntimeInstance = null;
                _encounterDirectorServiceRegistered = false;
            }

            _metaCampaignService = null;
            _encounterDirector.SetMetaCampaignService(null);
            _playerRuntimeContext = null;
            _ecosystemDirector = null;
            _sargassumMicroFauna = null;
            TryUnregisterHotSwapListener();
            TryUnregisterDispatcherLanes();

            SpectrumEvents.UnregisterSonarPingListener(this);
            DisablePhysicsEventPayloadReader();
            CompletePredatorSpatialHashBuild(forceComplete: true);
            _encounterDirector.ForceStopAndReset();
            _recentSonarStress = 0f;
            _externalPeakPressure01 = 0f;
            _externalPeakHoldSeconds = 0f;
            ResetPredatorSteeringControl();
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
            DisablePhysicsEventPayloadReader();
            _metaCampaignService = null;
            _encounterDirector.SetMetaCampaignService(null);
            _playerRuntimeContext = null;
            _ecosystemDirector = null;
            _sargassumMicroFauna = null;
            TryUnregisterHotSwapListener();

            if (_encounterDirectorServiceRegistered)
            {
                GlobalRegistry.UnregisterEncounterDirectorService(this);
                if (ReferenceEquals(s_activeRuntimeInstance, this))
                    s_activeRuntimeInstance = null;
                _encounterDirectorServiceRegistered = false;
            }

            TryUnregisterDispatcherLanes();

            CompletePredatorSpatialHashBuild(forceComplete: true);
            ReleasePredatorSpatialHashBuffers();
            _encounterDirector.ForceCompleteActiveJobForTeardown();
            _encounterDirector.ClearPredatorAupPublication();
            _encounterDirector.FlushPredatorAupVisualSync();
            ResetPredatorSteeringControl();
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
            if (!math.isfinite(deltaTime) || deltaTime <= 0f)
            {
                ResetPredatorSteeringControl();
                return;
            }

            long solveStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            RefreshRuntimeReferencesHot();
            DrainEntityDeathSignals();
            if (playerTransform == null)
            {
                ResetPredatorSteeringControl();
                return;
            }

            FrameTimingManager.CaptureFrameTimings();
            float averageFrameTimeMs = UpdateFrameTimeAverage(deltaTime);

            if (!TryResolvePlayerRuntimeSnapshot(
                    out Vector3 playerPosition,
                    out Vector3 playerVelocity,
                    out Vector3 playerForward,
                    out AbsoluteUniversePosition playerAup))
            {
                ResetPredatorSteeringControl();
                return;
            }

            float surfaceWorldY = ResolveSurfaceWorldY(playerPosition);
            ReadDirectorSurvivalStressInputs(
                out float healthNormalized,
                out float oxygenNormalized,
                out float pressureStress,
                out float thermalStress);
            float sonarStress = UpdateSonarStress(deltaTime);
            UpdateActiveSonarPingDebounce(deltaTime);
            float playerStress01 = ResolveLatestPlayerStress01();
            float internalStress = ResolveInternalStress(healthNormalized, oxygenNormalized, sonarStress, pressureStress, thermalStress);
            float clutchFactor01 = ResolveDirectorClutchFactor(healthNormalized, math.max(playerStress01, internalStress));
            PublishPredatorSteeringControl(clutchFactor01);
            float acousticThreatLevel = 0f;
            HectonMapMagicVegetationBridge vegetationBridge = _vegetationBridge;
            if (vegetationBridge != null)
                acousticThreatLevel = SanitizeDirector01(vegetationBridge.GetThreatLevel(playerPosition), 0f);
            acousticThreatLevel = math.max(SanitizeDirector01(acousticThreatLevel, 0f), sonarStress);
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
            DrainPhysicsEventPayloads();
            _encounterDirector.CompleteReadyOutput(faunaDirector, this, forceComplete: false);
            _encounterDirector.FlushPredatorAupVisualSync();
        }

        private void DrainPhysicsEventPayloads()
        {
            if (!_physicsEventPayloadReaderActive)
                return;

            int snapshotGeneration = SignalBus<PhysicsEventPayload>.SnapshotGeneration;
            if (snapshotGeneration == _lastPhysicsEventSnapshotGeneration)
                return;

            _lastPhysicsEventSnapshotGeneration = snapshotGeneration;
            ReadOnlySpan<PhysicsEventPayload> signals = SignalBus<PhysicsEventPayload>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PhysicsEventPayload payload = signals[i];
                ushort eventType = payload.EventType;
                if (eventType == PhysicsEventTypeAcousticPing)
                {
                    HandleAcousticPingPayload(in payload);
                }
                else if (eventType == PhysicsEventTypeElectromagneticPulse)
                {
                    float safeDuration = math.max(
                        PredatorAcousticDeafenedDurationSeconds,
                        math.max(0f, math.select(0f, payload.Scalar0, math.isfinite(payload.Scalar0))));
                    HandlePredatorAcousticDeafening(
                        payload.RuntimePosition,
                        payload.RadiusMeters,
                        safeDuration);
                }
                else if (eventType == PhysicsEventTypeAcousticImpulse)
                {
                    HandleAcousticImpulsePayload(in payload);
                }
            }
        }

        private void HandleAcousticImpulsePayload(in PhysicsEventPayload impulseEvent)
        {
            float safeRadiusMeters = math.max(0f, math.select(0f, impulseEvent.RadiusMeters, math.isfinite(impulseEvent.RadiusMeters)));
            float safeEnergyJoules = math.max(0f, math.select(0f, impulseEvent.Scalar0, math.isfinite(impulseEvent.Scalar0)));
            bool isLargeAcousticImpulse = (impulseEvent.StatusBits & AcousticImpulseFlagLarge) != 0u;
            if (isLargeAcousticImpulse)
            {
                float rangeVisibility01 = SanitizeDirector01(safeRadiusMeters * ActiveSonarLeviathanAggroInvRadiusMeters, 0f);
                HandleSonarPingSent(math.max(SanitizeDirector01(impulseEvent.Scalar1, 0f), rangeVisibility01));
                if ((impulseEvent.StatusBits & AcousticImpulseFlagCritical) == 0u)
                    return;
            }

            if (safeEnergyJoules < PredatorAcousticDeafeningImpulseEnergyJoules)
                return;

            HandlePredatorAcousticDeafening(
                impulseEvent.RuntimePosition,
                safeRadiusMeters,
                PredatorAcousticDeafenedDurationSeconds);
        }

        private void EnablePhysicsEventPayloadReader()
        {
            if (_physicsEventPayloadReaderActive || !Application.isPlaying)
                return;

            _physicsEventPayloadReaderActive = true;
        }

        private void DisablePhysicsEventPayloadReader()
        {
            if (!_physicsEventPayloadReaderActive)
                return;

            _physicsEventPayloadReaderActive = false;
            _lastPhysicsEventSnapshotGeneration = 0;
        }

        private void HandleAcousticPingPayload(in PhysicsEventPayload pingEvent)
        {
            float safeRadiusMeters = math.max(0f, math.select(0f, pingEvent.RadiusMeters, math.isfinite(pingEvent.RadiusMeters)));
            float safeIntensity01 = SanitizeDirector01(pingEvent.Scalar0, 0f);
            float safeEnergyJoules = math.max(0f, math.select(0f, pingEvent.Scalar2, math.isfinite(pingEvent.Scalar2)));
            if (safeRadiusMeters <= 0f || safeIntensity01 <= 0f)
                return;

            _activeSonarPingDebounceTimer = math.max(0f, math.select(0f, _activeSonarPingDebounceTimer, math.isfinite(_activeSonarPingDebounceTimer)));
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
                IFaunaDirectorCueSink faunaCue = _acousticPingPredatorContacts[i].Owner as IFaunaDirectorCueSink;
                if (faunaCue == null ||
                    faunaCue.IsDead ||
                    !faunaCue.IsApexPredatorContact)
                {
                    continue;
                }

                AbsoluteUniversePosition brainAup = _acousticPingPredatorContacts[i].PositionAup;
                if (AbsoluteUniversePosition.DistanceSq(in brainAup, in pingAup) > ActiveSonarLeviathanAggroRadiusMetersSqr)
                    continue;

                if (faunaCue.ShouldIgnoreAcousticPing(safeEnergyJoules, safeIntensity01))
                    continue;

                faunaCue.ApplyAcousticPingAggro(
                    pingEvent.RuntimePosition,
                    safeIntensity01,
                    ActiveSonarLeviathanAggroDurationSeconds);

                SargassumMicroFaunaBoids boidSystem = _sargassumMicroFauna;
                if (boidSystem != null)
                {
                    Vector3 direction = _acousticPingPredatorContacts[i].Position - pingEvent.RuntimePosition;
                    if (direction.sqrMagnitude <= 0.0001f)
                        direction = ResolveDeterministicOffsetDirection(unchecked((uint)faunaCue.SpeciesId));
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
                    DirectorAIEvents.TryRaiseThreatSpike(_acousticPingPredatorContacts[i].Position, safeIntensity01);
                    raisedThreatSpike = true;
                }
            }
        }

        private void HandlePredatorAcousticDeafening(Vector3 runtimePosition, float radiusMeters, float durationSeconds)
        {
            float safeRadiusMeters = math.max(0f, math.select(0f, radiusMeters, math.isfinite(radiusMeters)));
            float safeDurationSeconds = math.max(0f, math.select(0f, durationSeconds, math.isfinite(durationSeconds)));
            if (safeRadiusMeters <= 0f || safeDurationSeconds <= 0f)
                return;

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition pulseAup))
                return;

            int contactCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                in pulseAup,
                safeRadiusMeters,
                SpatialTargetKind.Bioform,
                _acousticPingPredatorContacts);

            double radiusSq = (double)safeRadiusMeters * safeRadiusMeters;
            for (int i = 0; i < contactCount; i++)
            {
                IFaunaDirectorCueSink faunaCue = _acousticPingPredatorContacts[i].Owner as IFaunaDirectorCueSink;
                if (faunaCue == null ||
                    faunaCue.IsDead ||
                    !faunaCue.IsApexPredatorContact)
                {
                    continue;
                }

                AbsoluteUniversePosition brainAup = _acousticPingPredatorContacts[i].PositionAup;
                if (AbsoluteUniversePosition.DistanceSq(in brainAup, in pulseAup) > radiusSq)
                    continue;

                faunaCue.ApplyPredatorDeafening(runtimePosition, safeDurationSeconds);
            }
        }

        private void UpdateActiveSonarPingDebounce(float deltaTime)
        {
            _activeSonarPingDebounceTimer = math.max(0f, math.select(0f, _activeSonarPingDebounceTimer, math.isfinite(_activeSonarPingDebounceTimer)));
            if (_activeSonarPingDebounceTimer <= 0f)
                return;

            float safeDeltaTime = math.max(0f, math.select(0f, deltaTime, math.isfinite(deltaTime)));
            _activeSonarPingDebounceTimer = math.max(0f, _activeSonarPingDebounceTimer - safeDeltaTime);
        }

        private void SchedulePredatorSightBatch(
            float deltaTime,
            Vector3 playerPosition,
            Vector3 playerVelocity,
            Vector3 playerForward,
            in AbsoluteUniversePosition playerAup)
        {
            CompletePredatorSpatialHashBuild(forceComplete: false);

            _predatorSightCooldown = math.max(0f, math.select(0f, _predatorSightCooldown, math.isfinite(_predatorSightCooldown)));
            if (_predatorSightCooldown > 0f)
            {
                float safeDeltaTime = math.max(0f, math.select(0f, deltaTime, math.isfinite(deltaTime)));
                _predatorSightCooldown = math.max(0f, _predatorSightCooldown - safeDeltaTime);
                return;
            }

            if (!TryResolvePredatorSpatialHashBuffers(
                    out NativeArray<double3> spatialAbsolutePositions,
                    out NativeArray<int3> spatialCellCoords))
            {
                return;
            }

            _predatorSightCooldown = PredatorSightIntervalSeconds;
            if (!_predatorSpatialHashReady)
            {
                SchedulePredatorSpatialHashRefresh(in playerAup);
                return;
            }

            int processedCount = 0;
            Vector3 safePlayerForward = playerForward.sqrMagnitude > 0.0001f ? ResolveDominantAxisDirection(playerForward) : Vector3.forward;
            Vector3 playerProbe = playerPosition + Vector3.up * PredatorSightProbeOffsetMeters;
            int3 playerCell = PredatorSpatialHashMath.ResolveCellCoord(playerAup.ToAbsoluteDouble3(), PredatorSpatialHashCellSizeMeters);
            for (int contactIndex = 0;
                 contactIndex < _predatorSpatialContactCount && processedCount < 1;
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
                    ref processedCount);
            }

            SchedulePredatorSpatialHashRefresh(in playerAup);
        }

        private void ProcessPredatorSightContact(
            int contactIndex,
            in AbsoluteUniversePosition playerAup,
            Vector3 playerPosition,
            Vector3 playerVelocity,
            Vector3 safePlayerForward,
            Vector3 playerProbe,
            ref int processedCount)
        {
            SpatialQueryHit contact = _predatorSpatialContacts[contactIndex];
            IFaunaDirectorCueSink faunaCue = contact.Owner as IFaunaDirectorCueSink;
            if (faunaCue == null ||
                faunaCue.IsDead ||
                (!faunaCue.IsAggressiveContact && !faunaCue.IsApexPredatorContact && !faunaCue.UsesPackHuntBehaviorContact))
            {
                return;
            }

            AbsoluteUniversePosition predatorAup = contact.PositionAup;
            double predatorDistanceSqr = AbsoluteUniversePosition.DistanceSq(in predatorAup, in playerAup);
            bool outsideFrustum = IsPredatorBehindPlayerViewByAup(in playerAup, in predatorAup, safePlayerForward);
            if (predatorDistanceSqr > PredatorDeadZoneCullDistanceMetersSqr && outsideFrustum)
            {
                faunaCue.ApplyDirectorColdTickCull(true);
                return;
            }

            faunaCue.ApplyDirectorColdTickCull(false);
            if (predatorDistanceSqr > (double)PredatorSightScanRadiusMeters * PredatorSightScanRadiusMeters)
                return;

            Vector3 predatorProbe = contact.Position + Vector3.up * PredatorSightProbeOffsetMeters;
            Vector3 toPlayer = playerProbe - predatorProbe;
            float distanceSqr = toPlayer.sqrMagnitude;
            if (distanceSqr <= 0.25f)
                return;

            Vector3 predatorForward = SanitizeFiniteVector(faunaCue.ResolveContactForward());
            predatorForward = predatorForward.sqrMagnitude > 0.0001f
                ? ResolveDominantAxisDirection(predatorForward)
                : Vector3.forward;
            if (!IsInsidePredatorSightCone(predatorForward, toPlayer, distanceSqr))
            {
                faunaCue.ApplyDirectorLineOfSight(false, playerPosition, safePlayerForward, playerVelocity);
                return;
            }

            if (predatorDistanceSqr <= PredatorSightImmediateRevealRadiusMetersSqr)
            {
                faunaCue.ApplyDirectorLineOfSight(true, playerPosition, safePlayerForward, playerVelocity);
                return;
            }

            if (predatorDistanceSqr >= PredatorSightRearViewFakeMinDistanceMetersSqr && outsideFrustum)
            {
                faunaCue.ApplyDirectorLineOfSight(false, playerPosition, safePlayerForward, playerVelocity);
                return;
            }

            bool hasLineOfSight = !IsPredatorSightTerrainBlocked(predatorProbe, playerProbe);
            faunaCue.ApplyDirectorLineOfSight(hasLineOfSight, playerPosition, safePlayerForward, playerVelocity);
            processedCount++;
        }

        private bool IsPredatorSightTerrainBlocked(Vector3 origin, Vector3 target)
        {
            if (!math.isfinite(origin.x) ||
                !math.isfinite(origin.y) ||
                !math.isfinite(origin.z) ||
                !math.isfinite(target.x) ||
                !math.isfinite(target.y) ||
                !math.isfinite(target.z))
            {
                return true;
            }

            ITerrainProvider terrainProvider = _terrainProvider;
            if (terrainProvider == null || !terrainProvider.IsAvailable)
                return false;

            const int sampleCount = 3;
            for (int i = 1; i <= sampleCount; i++)
            {
                float t = i * 0.25f;
                Vector3 sample = Vector3.LerpUnclamped(origin, target, t);
                if (!terrainProvider.TryGetHeight(sample.x, sample.z, out float terrainHeight) ||
                    !math.isfinite(terrainHeight))
                {
                    continue;
                }

                if (sample.y <= terrainHeight + 0.25f)
                    return true;
            }

            return false;
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
            in AbsoluteUniversePosition playerAup)
        {
            if (_predatorSpatialHashJobScheduled)
            {
                return;
            }

            if (!TryPinPredatorSpatialHashVaultBuffers(
                    out NativeArray<double3> spatialAbsolutePositions,
                    out NativeArray<int3> spatialCellCoords))
            {
                return;
            }

            try
            {
                int contactCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                    in playerAup,
                    PredatorSpatialHashActiveChunkRadiusMeters,
                    SpatialTargetKind.Bioform,
                    _predatorSpatialContacts);
                _predatorSpatialContactCount = math.min(contactCount, PredatorSpatialHashContactCapacity);
                for (int i = 0; i < _predatorSpatialContactCount; i++)
                {
                    AbsoluteUniversePosition contactAup = _predatorSpatialContacts[i].PositionAup;
                    double3 absolutePosition = contactAup.ToAbsoluteDouble3();
                    spatialAbsolutePositions[i] = absolutePosition;
                    spatialCellCoords[i] = PredatorSpatialHashMath.ResolveCellCoord(
                        absolutePosition,
                        PredatorSpatialHashCellSizeMeters);
                }

                for (int i = _predatorSpatialContactCount; i < PredatorSpatialHashContactCapacity; i++)
                {
                    _predatorSpatialContacts[i] = default;
                    spatialAbsolutePositions[i] = default;
                    spatialCellCoords[i] = default;
                }

                _predatorSpatialHashReady = _predatorSpatialContactCount > 0;
            }
            finally
            {
                ReleasePredatorSpatialHashVaultPins();
            }
        }

        private bool CompletePredatorSpatialHashBuild(bool forceComplete)
        {
            if (!_predatorSpatialHashJobScheduled)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _predatorSpatialHashBuildHandle, forceComplete))
                return false;

            _predatorSpatialHashJobScheduled = false;
            _predatorSpatialHashReady = _predatorSpatialContactCount > 0;
            ReleasePredatorSpatialHashVaultPins();
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

        private bool TryResolvePredatorSpatialHashBuffers(
            out NativeArray<double3> spatialAbsolutePositions,
            out NativeArray<int3> spatialCellCoords)
        {
            spatialAbsolutePositions = default;
            spatialCellCoords = default;
            return TryOpenDirectorVaultView(
                       in _predatorSpatialAbsolutePositionsHandle,
                       PredatorSpatialAbsolutePositionsBufferId,
                       PredatorSpatialHashContactCapacity,
                       out spatialAbsolutePositions) &&
                   TryOpenDirectorVaultView(
                       in _predatorSpatialCellCoordsHandle,
                       PredatorSpatialCellCoordsBufferId,
                       PredatorSpatialHashContactCapacity,
                       out spatialCellCoords);
        }

        private void ReleasePredatorSpatialHashBuffers()
        {
            ReleasePredatorSpatialHashBuffers(_dataVault);
        }

        private void ReleasePredatorSpatialHashBuffers(IDataVault vault)
        {
            CompletePredatorSpatialHashBuild(forceComplete: true);

            ReleasePredatorSpatialHashVaultPins();
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

            if (TryOpenDirectorVaultView(in handle, bufferId, requiredLength, out buffer))
                return true;

            if (vault.TryGetGenerationHandle(bufferId, out handle) &&
                TryOpenDirectorVaultView(in handle, bufferId, requiredLength, out buffer))
            {
                return true;
            }

            if (vault.IsAllocationLocked)
            {
                handle = default;
                buffer = default;
                return false;
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.AICognition,
                options);
            return TryOpenDirectorVaultView(in handle, bufferId, requiredLength, out buffer);
        }

        private bool TryOpenDirectorVaultView<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            return TryOpenDirectorVaultView(_dataVault, in handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenDirectorVaultView<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   IsDirectorVaultHandle(in handle, bufferId) &&
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

        private bool TryPinPredatorSpatialHashVaultBuffers(
            out NativeArray<double3> spatialAbsolutePositions,
            out NativeArray<int3> spatialCellCoords)
        {
            spatialAbsolutePositions = default;
            spatialCellCoords = default;
            if (_predatorSpatialHashBuffersPinned)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsDirectorVaultHandle(in _predatorSpatialAbsolutePositionsHandle, PredatorSpatialAbsolutePositionsBufferId) ||
                !IsDirectorVaultHandle(in _predatorSpatialCellCoordsHandle, PredatorSpatialCellCoordsBufferId))
            {
                return false;
            }

            if (!vault.TryAcquireMutationGuard(_predatorSpatialHashMutationGuardMask))
                return false;

            bool keepGuard = false;
            try
            {
                if (!TryOpenDirectorVaultView(
                        vault,
                        in _predatorSpatialAbsolutePositionsHandle,
                        PredatorSpatialAbsolutePositionsBufferId,
                        PredatorSpatialHashContactCapacity,
                        out spatialAbsolutePositions) ||
                    !TryOpenDirectorVaultView(
                        vault,
                        in _predatorSpatialCellCoordsHandle,
                        PredatorSpatialCellCoordsBufferId,
                        PredatorSpatialHashContactCapacity,
                        out spatialCellCoords))
                {
                    spatialAbsolutePositions = default;
                    spatialCellCoords = default;
                    return false;
                }

                _predatorSpatialHashGuardVault = vault;
                _predatorSpatialHashBuffersPinned = true;
                keepGuard = true;
                return true;
            }
            finally
            {
                if (!keepGuard)
                    vault.ReleaseMutationGuard(_predatorSpatialHashMutationGuardMask);
            }
        }

        private void ReleasePredatorSpatialHashVaultPins()
        {
            if (!_predatorSpatialHashBuffersPinned)
                return;

            IDataVault vault = _predatorSpatialHashGuardVault;
            vault?.ReleaseMutationGuard(_predatorSpatialHashMutationGuardMask);
            _predatorSpatialHashGuardVault = null;
            _predatorSpatialHashBuffersPinned = false;
        }

        private static bool IsDirectorVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)SystemID.AICognition &&
                   handle.Generation != 0u;
        }

        private static ulong PredatorSpatialHashMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private void PublishDirectorSolveBudgetIfNeeded(long solveStartTicks)
        {
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - solveStartTicks;
            double elapsedMilliseconds = elapsedTicks * _StopwatchTickToMilliseconds;
            _directorSolveWarningClockSeconds = math.max(
                0f,
                math.select(0f, _directorSolveWarningClockSeconds, math.isfinite(_directorSolveWarningClockSeconds)));
            _directorSolveWarningClockSeconds += ResolveSafeCurrentFrameUnscaledDeltaTime();
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
            float clampedPressure = SanitizeDirector01(pressure01, 0f);
            float safeHoldSeconds = math.select(0f, holdSeconds, math.isfinite(holdSeconds) & (holdSeconds > 0f));
            if (clampedPressure <= 0f || safeHoldSeconds <= 0f)
                return;

            _externalPeakPressure01 = math.max(SanitizeDirector01(_externalPeakPressure01, 0f), clampedPressure);
            _externalPeakHoldSeconds = math.max(math.select(0f, _externalPeakHoldSeconds, math.isfinite(_externalPeakHoldSeconds)), safeHoldSeconds);
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

            if (newPhase == EncounterPhase.Peak)
            {
                DirectorAIEvents.TryRaiseEquipmentGlitchRequested(math.lerp(0.35f, 0.85f, _encounterDirector.IntensityLevel));
                DirectorAIEvents.TryRaiseMissionTriggerRequested(eventPosition);
            }
            else if (newPhase == EncounterPhase.Decay)
            {
                DirectorAIEvents.TryRaiseWeatherShiftRequested(math.lerp(0.2f, 0.6f, _encounterDirector.StressLevel));
            }
            else if (newPhase == EncounterPhase.Relax)
            {
                DirectorAIEvents.TryRaiseRareDiscoveryRequested(eventPosition);
            }
        }

        internal void HandleThreatSpawned(EncounterThreatClass threatClass, Vector3 spawnPosition)
        {
            if (threatClass == EncounterThreatClass.Swarm)
                DirectorAIEvents.TryRaiseSpawnHordeRequested(spawnPosition);

            if (threatClass == EncounterThreatClass.Leviathan)
                DirectorAIEvents.TryRaiseThreatSpike(spawnPosition, 1f);
            else if (threatClass == EncounterThreatClass.Stalker)
                DirectorAIEvents.TryRaiseThreatSpike(spawnPosition, 0.75f);
        }

        private void RefreshRuntimeReferences(bool force)
        {
            _resolveRetryTimer = math.max(0f, math.select(0f, _resolveRetryTimer, math.isfinite(_resolveRetryTimer)));
            if (!force && _resolveRetryTimer > 0f)
            {
                _resolveRetryTimer = math.max(0f, _resolveRetryTimer - ResolveSafeCurrentFrameUnscaledDeltaTime());
                return;
            }

            _resolveRetryTimer = 1f;
            ApplyPlayerRuntimeContextReferences(_playerRuntimeContext, replaceExisting: false);

            if (!force)
                return;

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
        }

        private void RefreshRuntimeReferencesHot()
        {
            _resolveRetryTimer = math.max(0f, math.select(0f, _resolveRetryTimer, math.isfinite(_resolveRetryTimer)));
            if (_resolveRetryTimer > 0f)
            {
                _resolveRetryTimer = math.max(0f, _resolveRetryTimer - ResolveSafeCurrentFrameUnscaledDeltaTime());
                return;
            }

            _resolveRetryTimer = 1f;
            ApplyPlayerRuntimeContextReferences(_playerRuntimeContext, replaceExisting: false);
        }

        private void ApplyPlayerRuntimeContextReferences(IPlayerRuntimeContext playerContext, bool replaceExisting)
        {
            if (playerContext == null)
                return;

            Transform contextTransform = playerContext.PlayerTransform;
            if (contextTransform != null && (replaceExisting || playerTransform == null))
                playerTransform = contextTransform;

            HectonSurvivalSystem contextSurvival = playerContext.SurvivalSystem;
            if (contextSurvival != null && (replaceExisting || survivalSystem == null))
                survivalSystem = contextSurvival;

            HectonPlayerMovement contextMovement = playerContext.PlayerMovement;
            if (contextMovement != null && (replaceExisting || _playerMovement == null))
                _playerMovement = contextMovement;

            Camera contextCamera = playerContext.PlayerCamera;
            if (contextCamera != null && (replaceExisting || playerCamera == null))
                playerCamera = contextCamera;
        }

        private void RefreshColdRegistryReferences()
        {
            _playerRuntimeContext = GlobalRegistry.Player;
            _ecosystemDirector = GlobalRegistry.EcosystemDirector;
            _sargassumMicroFauna = GlobalRegistry.SargassumMicroFauna;
            _dataVault = GlobalRegistry.DataVault;
            PredatorCognitionDomain.InjectDataVault(_dataVault);
            _terrainProvider = GlobalRegistry.Terrain;
            _vegetationBridge = GlobalRegistry.MapMagicVegetation;
            BindMetaCampaignService(GlobalRegistry.MetaCampaign);
            if (Application.isPlaying)
                CameraJuiceSignals.EnsurePrewarmed();
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
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                ApplyPlayerRuntimeContextReferences(_playerRuntimeContext, replaceExisting: true);
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.EcosystemDirector)
            {
                _ecosystemDirector = currentService as IEcosystemDirectorService;
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.SargassumMicroFaunaRuntime)
            {
                _sargassumMicroFauna = currentService as SargassumMicroFaunaBoids;
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.TerrainProviderRuntime)
            {
                _terrainProvider = currentService as ITerrainProvider;
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.MapMagicVegetationRuntime)
            {
                _vegetationBridge = currentService as HectonMapMagicVegetationBridge;
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                CompletePredatorSpatialHashBuild(forceComplete: true);
                ReleasePredatorSpatialHashBuffers(_dataVault ?? (previousService as IDataVault));
                _dataVault = currentService as IDataVault;
                PredatorCognitionDomain.InjectDataVault(_dataVault);
                EnsurePredatorSpatialHashBuffersAllocated(out _, out _);
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.MetaCampaignRuntime)
            {
                BindMetaCampaignService(currentService as IMetaCampaignService);
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.EncounterDirector)
            {
                if (ReferenceEquals(currentService, this))
                {
                    _encounterDirectorServiceRegistered = true;
                    s_activeRuntimeInstance = this;
                }
                else if (ReferenceEquals(previousService, this))
                {
                    _encounterDirectorServiceRegistered = false;
                    if (ReferenceEquals(s_activeRuntimeInstance, this))
                        s_activeRuntimeInstance = null;
                }
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                if (currentService == null)
                {
                    _dispatcherRegistered = false;
                    _lateFrameRegistered = false;
                    return;
                }

                if (!isActiveAndEnabled)
                    return;

                TryUnregisterDispatcherLanes();
                TryRegisterDispatcherLanes();
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
            float safeDeltaTime = math.max(0f, math.select(0f, deltaTime, math.isfinite(deltaTime)));
            float fallbackSampleMs = safeDeltaTime * 1000f;
            uint timingCount = FrameTimingManager.GetLatestTimings(1u, _frameTimingScratch);
            float sampleMs = timingCount > 0u ? (float)_frameTimingScratch[0].cpuFrameTime : fallbackSampleMs;
            if (!math.isfinite(sampleMs) || sampleMs <= 0f)
                sampleMs = fallbackSampleMs;

            _frameTimeHistory[_frameTimeHistoryIndex] = sampleMs;
            _frameTimeHistoryIndex++;
            if (_frameTimeHistoryIndex >= _frameTimeHistory.Length)
                _frameTimeHistoryIndex = 0;

            if (_frameTimeHistoryCount < _frameTimeHistory.Length)
                _frameTimeHistoryCount++;

            float sum = 0f;
            for (int i = 0; i < _frameTimeHistoryCount; i++)
            {
                float historySample = _frameTimeHistory[i];
                sum += math.select(0f, historySample, math.isfinite(historySample) & (historySample > 0f));
            }

            return _frameTimeHistoryCount > 0 ? sum * math.rcp((float)_frameTimeHistoryCount) : sampleMs;
        }

        private bool RefreshEncounterFrustumPlanes(float deltaTime, Vector3 playerPosition, Vector3 playerForward)
        {
            if (_frustumPlanesInitialized)
            {
                float safeDeltaTime = math.max(0f, math.select(0f, deltaTime, math.isfinite(deltaTime)));
                _frustumPlaneRefreshTimer = math.max(
                    0f,
                    math.select(0f, _frustumPlaneRefreshTimer, math.isfinite(_frustumPlaneRefreshTimer)));
                _frustumPlaneRefreshTimer = math.max(0f, _frustumPlaneRefreshTimer - safeDeltaTime);
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

        private bool TryResolvePlayerRuntimePosition(out Vector3 playerPosition)
        {
            playerPosition = Vector3.zero;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext == null ||
                !playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState))
            {
                return false;
            }

            playerPosition = ToVector3(movementState.PredictedAup.ToRuntimeFloat3());
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

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext == null ||
                !playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState))
            {
                return false;
            }

            playerContext.TryGetLookRuntimeState(out PlayerLookState lookState);
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

        private void ReadDirectorSurvivalStressInputs(
            out float healthNormalized,
            out float oxygenNormalized,
            out float pressureStress,
            out float thermalStress)
        {
            healthNormalized = 1f;
            oxygenNormalized = 1f;
            pressureStress = 0f;
            thermalStress = 0f;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null && playerContext.TryGetSurvivalRuntimeState(out PlayerSurvivalRuntimeState survivalState))
            {
                healthNormalized = SanitizeDirector01(survivalState.IntegrityNormalized, 1f);
                oxygenNormalized = SanitizeDirector01(survivalState.OxygenNormalized, 1f);
                pressureStress = SanitizeDirector01(survivalState.PressureExposureSeverity01, 0f);
                thermalStress = math.max(
                    SanitizeDirector01(survivalState.ThermalStressSeverity01, 0f),
                    math.max(
                        SanitizeDirector01(survivalState.ColdStressSeverity01, 0f),
                        SanitizeDirector01(survivalState.HeatStressSeverity01, 0f)));
                return;
            }

            if (survivalSystem == null)
                return;

            healthNormalized = SanitizeDirector01(survivalSystem.IntegrityNormalized, 1f);
            oxygenNormalized = SanitizeDirector01(survivalSystem.OxygenNormalized, 1f);
            pressureStress = SanitizeDirector01(survivalSystem.PressureExposureSeverity01, 0f);
            thermalStress = math.max(
                SanitizeDirector01(survivalSystem.ThermalStressSeverity01, 0f),
                math.max(
                    SanitizeDirector01(survivalSystem.ColdStressSeverity01, 0f),
                    SanitizeDirector01(survivalSystem.HeatStressSeverity01, 0f)));
        }

        private float ResolveLatestPlayerStress01()
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            float rawStress01 = _lastPlayerStressRaw01;
            if (SignalBus<PlayerStressSignal>.TryGetLatest(out PlayerStressSignal signal, out int sequence) &&
                math.isfinite(signal.Stress01))
            {
                rawStress01 = SanitizeDirector01(signal.Stress01, rawStress01);
                if (_lastPlayerStressSignalSeenFrame == int.MinValue ||
                    sequence != _lastPlayerStressSignalSequence ||
                    frame < _lastPlayerStressSignalSeenFrame)
                {
                    _lastPlayerStressSignalSequence = sequence;
                    _lastPlayerStressSignalSeenFrame = frame;
                }
            }

            _lastPlayerStressRaw01 = rawStress01;
            float stress01 = rawStress01;
            if (_lastPlayerStressSignalSeenFrame != int.MinValue)
            {
                int age = math.max(0, frame - _lastPlayerStressSignalSeenFrame);
                float freshness01 = SanitizeDirector01(
                    (DirectorPlayerStressSignalFadeFrames - age) * DirectorPlayerStressSignalFadeFrameRcp,
                    0f);
                stress01 *= freshness01;
            }
            else
            {
                stress01 = 0f;
            }

            _lastPlayerStress01 = stress01;
            return stress01;
        }

        private float ResolveInternalStress(
            float healthNormalized,
            float oxygenNormalized,
            float sonarStress,
            float pressureStress,
            float thermalStress)
        {
            float healthStress = 1f - SanitizeDirector01(healthNormalized, 1f);
            float oxygenStress = 1f - SanitizeDirector01(oxygenNormalized, 1f);
            return SanitizeDirector01(math.max(
                math.max(SanitizeDirector01(pressureStress, 0f), SanitizeDirector01(thermalStress, 0f)),
                math.max(math.max(healthStress, oxygenStress), SanitizeDirector01(sonarStress, 0f))), 0f);
        }

        private float ResolveDirectorClutchFactor(float healthNormalized, float playerStress01)
        {
            float healthWindow = SanitizeDirector01(
                (DirectorClutchFullHealth01 + DirectorClutchHealthRamp01 - SanitizeDirector01(healthNormalized, 1f)) *
                math.rcp(DirectorClutchHealthRamp01),
                0f);
            float stressWindow = SanitizeDirector01(
                (SanitizeDirector01(playerStress01, 0f) - (DirectorClutchFullStress01 - DirectorClutchStressRamp01)) *
                math.rcp(DirectorClutchStressRamp01),
                0f);
            return healthWindow * stressWindow;
        }

        private void PublishPredatorSteeringControl(float clutchFactor01)
        {
            float quality = SanitizeDirector01(HomeostasisBrain.GlobalQualityWeight, 0f);
            int maxTokens = (int)math.max(1f, math.round(math.lerp(1f, 4f, quality)));
            _lastDirectorClutchFactor01 = SanitizeDirector01(clutchFactor01, 0f);
            PredatorCognitionDomain.SetDirectorSteeringControl(
                _lastDirectorClutchFactor01,
                maxTokens,
                DirectorDefaultPlayerArmorClass,
                ResolveDirectorFrameU32());
        }

        private void ResetPredatorSteeringControl()
        {
            _lastDirectorClutchFactor01 = 0f;
            PredatorCognitionDomain.SetDirectorSteeringControl(
                0f,
                1,
                DirectorDefaultPlayerArmorClass,
                ResolveDirectorFrameU32());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeDirector01(float value, float fallback)
        {
            return math.saturate(math.select(fallback, value, math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveDirectorFrameU32()
        {
            return unchecked((uint)math.max(0, SystemDispatcher.CurrentFrameIndex));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveSafeCurrentFrameUnscaledDeltaTime()
        {
            float deltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            return math.max(0f, math.select(0f, deltaTime, math.isfinite(deltaTime)));
        }

        private float UpdateSonarStress(float deltaTime)
        {
            _recentSonarStress = SanitizeDirector01(_recentSonarStress, 0f);
            if (_recentSonarStress <= 0f)
                return 0f;

            float safeDeltaTime = math.max(0f, math.select(0f, deltaTime, math.isfinite(deltaTime)));
            float safeDecay = math.max(0f, math.select(0f, SonarStressDecayPerSecond, math.isfinite(SonarStressDecayPerSecond)));
            _recentSonarStress = math.max(
                0f,
                _recentSonarStress - (safeDecay * safeDeltaTime));
            return _recentSonarStress;
        }

        private void HandleSonarPingSent(float intensity)
        {
            float clampedIntensity = SanitizeDirector01(intensity, 0f);
            if (clampedIntensity <= 0f)
                return;

            _recentSonarStress = math.max(SanitizeDirector01(_recentSonarStress, 0f), clampedIntensity);
        }

        void ISonarPingEventListener.OnSonarPingSent(float intensity)
        {
            HandleSonarPingSent(intensity);
        }

        private void ApplyExternalPeakPressure(float deltaTime, ref float internalStress, ref float acousticThreatLevel)
        {
            _externalPeakPressure01 = SanitizeDirector01(_externalPeakPressure01, 0f);
            _externalPeakHoldSeconds = math.select(0f, _externalPeakHoldSeconds, math.isfinite(_externalPeakHoldSeconds));
            if (_externalPeakHoldSeconds <= 0f || _externalPeakPressure01 <= 0f)
                return;

            float safeDeltaTime = math.max(0f, math.select(0f, deltaTime, math.isfinite(deltaTime)));
            _externalPeakHoldSeconds = math.max(0f, _externalPeakHoldSeconds - safeDeltaTime);
            internalStress = math.max(SanitizeDirector01(internalStress, 0f), _externalPeakPressure01);
            acousticThreatLevel = math.max(SanitizeDirector01(acousticThreatLevel, 0f), _externalPeakPressure01);

            if (_encounterDirector.CurrentPhase != EncounterPhase.Peak)
                _encounterDirector.RequestPhaseOverride(EncounterPhase.Peak);

            if (_externalPeakHoldSeconds > 0f)
                return;

            _externalPeakPressure01 = 0f;
        }

        private void UpdateHunterSquadPressure(float deltaTime)
        {
            _hunterSquadCooldown = math.max(0f, math.select(0f, _hunterSquadCooldown, math.isfinite(_hunterSquadCooldown)));
            if (_hunterSquadCooldown > 0f)
            {
                float safeDeltaTime = math.max(0f, math.select(0f, deltaTime, math.isfinite(deltaTime)));
                _hunterSquadCooldown = math.max(0f, _hunterSquadCooldown - safeDeltaTime);
            }

            IEcosystemDirectorService ecosystemDirector = _ecosystemDirector;
            float biomeHostility01 = ecosystemDirector != null
                ? SanitizeDirector01(ecosystemDirector.BiomeHostility01, 0f)
                : 0f;
            if (biomeHostility01 < HunterSquadHostilityThreshold)
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

            DirectorAIEvents.TryRaisePredatorPressureChanged(enabled);
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
            int index = slot & 0x7;
            bool diagonal = (index & 1) != 0;
            float axis = math.select(1f, 0.70710678f, diagonal);
            float x = math.select(0f, axis, index == 0 || index == 1 || index == 7);
            x = math.select(x, -axis, index == 3 || index == 4 || index == 5);
            float y = math.select(0f, axis, index == 1 || index == 2 || index == 3);
            y = math.select(y, -axis, index == 5 || index == 6 || index == 7);
            return new Vector2(x, y);
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
