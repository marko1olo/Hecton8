// ============================================================================
// HECTON-8 - LaserCutter.cs v2.2
// Laser cutter - PlayerTool with thermal management.
//
// v2.2 CHANGES (ZERO-GC REFACTOR):
//   [ZERO-GC] Diagnosis system entirely refactored to use FixedCharBuffer.
//     - Eliminated managed formatting in diagnosis and operational summaries.
//     - Removed legacy CutterDiagnosis fields (headline/summary) in favor of persistent buffers.
//     - Consolidated state management and removed clobbered field declarations.
//
//   [OPT] Player inventory resolve moved out of hot loop (EnsurePlayerInventory)
//     to one-time initialization in Awake().
//
// ============================================================================

namespace Hecton8.Gameplay
{
    using System;
    using System.Runtime.InteropServices;
    using Hecton8.Audio;
    using Hecton8.Bootstrap;
    using Hecton8.Building;
    using Hecton8.Construction;
    using Hecton8.Core;
    using Hecton8.Core.Signals;
    using Hecton8.Interaction;
    using Hecton8.Inventory;
    using Hecton8.Input;
    using Hecton.Localization;
    using Hecton8.Physics;
    using Hecton8.Scavenging;
    using Hecton8.Tools;
    using Hecton8.World;
    using EquipmentInteractionPacket = Hecton8.Interaction.InteractionPacket;
    using EquipmentInteractionSignal = Hecton8.Interaction.InteractionSignal;
    using Unity.Collections;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Laser cutter event kind carried by <see cref="LaserCutterEventPayload"/>.
    /// </summary>
    public enum LaserCutterEventType : byte
    {
        /// <summary>Normalized heat value changed beyond the publish threshold.</summary>
        HeatChanged = 0,

        /// <summary>Beam activation state changed.</summary>
        BeamStateChanged = 1
    }

    /// <summary>
    /// Blittable laser cutter event payload queued by <see cref="LaserCutterEvents"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LaserCutterEventPayload
    {
        /// <summary>Normalized heat value [0, 1].</summary>
        public float Heat01;

        /// <summary>Runtime entity id hash of the cutter source.</summary>
        public int CutterInstanceId;

        /// <summary>Runtime entity id hash of the cutter root transform.</summary>
        public int CutterRootInstanceId;

        /// <summary>Serialized <see cref="LaserCutterEventType"/> value.</summary>
        public ushort EventType;

        /// <summary>Bit flags for event-specific state.</summary>
        public ushort StateFlags;
    }

    /// <summary>
    /// Listener contract for deferred laser cutter events.
    /// </summary>
    public interface ILaserCutterEventListener
    {
        /// <summary>
        /// Receives a laser cutter event during <see cref="SystemDispatcher"/> LateUpdate.
        /// </summary>
        /// <param name="payload">Blittable cutter event payload.</param>
        void OnLaserCutterEvent(in LaserCutterEventPayload payload);
    }

    /// <summary>
    /// Queue-backed laser cutter event lane with a sidecar source registry for live transform resolution.
    /// </summary>
    public static class LaserCutterEvents
    {
        private const int PendingEventCapacity = 16;
        private const int ListenerCapacity = 8;
        private const int SourceCapacity = 8;
        private const ushort BeamActiveFlag = 1;

        private struct SourceRecord
        {
            public LaserCutter Source;
            public int CutterInstanceId;
            public Transform CachedTransform;
        }

        // COLD ALLOC: RegistryBucket<ILaserCutterEventListener>[8] - cutter listeners drained by SystemDispatcher LateUpdate - owner: LaserCutterEvents
        private static readonly RegistryBucket<ILaserCutterEventListener> _listeners = new RegistryBucket<ILaserCutterEventListener>(ListenerCapacity);
        // COLD ALLOC: SourceRecord[8] - cutter source sidecar for live Transform resolution - owner: LaserCutterEvents
        private static readonly SourceRecord[] _sources = new SourceRecord[SourceCapacity];
        private static NativeQueue<LaserCutterEventPayload> _pendingEvents;
        private static NativeQueue<LaserCutterEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;
        private static int _sourceCount;

        /// <summary>
        /// Pending payload count in the cutter event lane.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        /// <summary>
        /// Registers a cutter event listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Register(ILaserCutterEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            _listeners.TryRegister(listener);
        }

        /// <summary>
        /// Unregisters a cutter event listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Unregister(ILaserCutterEventListener listener)
        {
            if (listener == null || !_listeners.Contains(listener))
                return;

            _listeners.Unregister(listener);
        }

        /// <summary>
        /// Flushes queued cutter events through registered listeners.
        /// </summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated || _listeners.Count <= 0)
            {
                DrainWithoutDispatch();
                return;
            }

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out LaserCutterEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                ILaserCutterEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        ILaserCutterEventListener listener = rawArray[i];
                        if (listener != null)
                            listener.OnLaserCutterEvent(in payload);
                    }
                }
                finally
                {
                    _isDispatching = false;
                }
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        /// <summary>
        /// Resolves a live cutter transform from the sidecar source registry.
        /// </summary>
        /// <param name="cutterInstanceId">Runtime entity id hash of the cutter source.</param>
        /// <param name="cutterTransform">Resolved live transform, if present.</param>
        /// <returns>True when the source is still registered and has a transform.</returns>
        public static bool TryResolveCutterTransform(int cutterInstanceId, out Transform cutterTransform)
        {
            for (int i = _sourceCount - 1; i >= 0; i--)
            {
                SourceRecord record = _sources[i];
                if (record.Source == null || record.CutterInstanceId != cutterInstanceId)
                    continue;

                cutterTransform = record.CachedTransform;
                return cutterTransform != null;
            }

            cutterTransform = null;
            return false;
        }

        internal static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<LaserCutterEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<LaserCutterEventPayload>[16] - deferred cutter event lane flushed by SystemDispatcher LateUpdate - owner: LaserCutterEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(LaserCutterEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<LaserCutterEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<LaserCutterEventPayload>[16] - next-frame cutter event lane prevents same-frame reentrant dispatch - owner: LaserCutterEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(LaserCutterEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
            }
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

        internal static void RegisterSource(LaserCutter source, int cutterInstanceId, Transform cachedTransform)
        {
            if (source == null || cutterInstanceId == 0)
                return;

            EnsureInitialized();
            for (int i = _sourceCount - 1; i >= 0; i--)
            {
                if (_sources[i].Source != source)
                    continue;

                _sources[i] = new SourceRecord
                {
                    Source = source,
                    CutterInstanceId = cutterInstanceId,
                    CachedTransform = cachedTransform
                };
                return;
            }

            if (_sourceCount >= SourceCapacity)
                return;

            _sources[_sourceCount] = new SourceRecord
            {
                Source = source,
                CutterInstanceId = cutterInstanceId,
                CachedTransform = cachedTransform
            };
            _sourceCount++;
        }

        internal static void UnregisterSource(LaserCutter source)
        {
            if (source == null || _sourceCount <= 0)
                return;

            for (int i = _sourceCount - 1; i >= 0; i--)
            {
                if (_sources[i].Source != source)
                    continue;

                int lastIndex = _sourceCount - 1;
                _sources[i] = _sources[lastIndex];
                _sources[lastIndex] = default;
                _sourceCount = lastIndex;
                return;
            }
        }

        internal static void RaiseHeatChanged(float heat01, int cutterInstanceId, int rootInstanceId)
        {
            Enqueue(new LaserCutterEventPayload
            {
                Heat01 = math.saturate(heat01),
                CutterInstanceId = cutterInstanceId,
                CutterRootInstanceId = rootInstanceId,
                EventType = (ushort)LaserCutterEventType.HeatChanged,
                StateFlags = 0
            });
        }

        internal static void RaiseBeamStateChanged(int cutterInstanceId, int rootInstanceId, bool isActive)
        {
            Enqueue(new LaserCutterEventPayload
            {
                Heat01 = 0f,
                CutterInstanceId = cutterInstanceId,
                CutterRootInstanceId = rootInstanceId,
                EventType = (ushort)LaserCutterEventType.BeamStateChanged,
                StateFlags = isActive ? BeamActiveFlag : (ushort)0
            });
        }

        /// <summary>
        /// Tests the beam-active flag in a cutter event payload.
        /// </summary>
        /// <param name="payload">Payload to inspect.</param>
        /// <returns>True when the payload marks the cutter beam active.</returns>
        public static bool IsBeamActive(in LaserCutterEventPayload payload)
        {
            return (payload.StateFlags & BeamActiveFlag) != 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(LaserCutterEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(LaserCutterEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            for (int i = 0; i < _sourceCount; i++)
                _sources[i] = default;

            _sourceCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
        }

        private static void Enqueue(in LaserCutterEventPayload payload)
        {
            if (payload.CutterInstanceId == 0 || payload.CutterRootInstanceId == 0)
                return;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
        }

        private static void DrainWithoutDispatch()
        {
            if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                return;

            if (_pendingEventCount <= 0)
            {
                PromoteNextFrameEventsIfFrontEmpty();
                if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                    return;
            }

            if (_nextFrameEvents.IsCreated)
                DrainQueueWithoutDispatch(ref _nextFrameEvents, ref _nextFrameEventCount);
        }

        private static bool DrainQueueWithoutDispatch(
            ref NativeQueue<LaserCutterEventPayload> queue,
            ref int pendingCount)
        {
            if (!queue.IsCreated)
                return true;

            int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !queue.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!queue.TryDequeue(out _))
                    break;

                if (pendingCount > 0)
                    pendingCount--;
            }

            if (queue.IsEmpty())
                pendingCount = 0;

            return true;
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!_pendingEvents.IsCreated ||
                !_nextFrameEvents.IsCreated ||
                _pendingEventCount > 0 ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<LaserCutterEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }

    [DisallowMultipleComponent]
    public sealed class LaserCutter : PlayerTool, IToolModule
    {
        private const string CutterCategory = "CUTTER";
        private const int RecoveryProgressMaxPercent = 100;
        private const float MaxRecoilImpulse = 12f;
        private const float MinEffectiveBeamPower = 0.02f;
        private const float LowPowerThresholdNormalized = 0.12f;
        private const float LowPowerOutputScale = 0.35f;
        private const float InvTau = 0.15915494f;
        private const float LaserJitterSecondaryScale = 1.37f;
        private const float LaserJitterSecondaryOffset = 2.1f;
        private const float QuaternionHalfSqrtTwo = 0.70710678f;
        private const float ShaderFloatPublishEpsilon = 0.0001f;
        private static int _WaterLayer = int.MinValue;
        private static int _TransparentFxLayer = int.MinValue;
        private const byte IdleState = (byte)ToolStateBits.Idle;
        private const byte ActiveState = (byte)ToolStateBits.Active;
        private const byte BusyState = (byte)ToolStateBits.Busy;
        private const byte OverheatedState = (byte)ToolStateBits.Overheated;
        private const byte LowPowerState = (byte)ToolStateBits.LowPower;
        private const byte CooldownState = (byte)ToolStateBits.Cooldown;

        private struct CutterDiagnosis
        {
            public string severity;
        }

        private static readonly int _LaserHitHeatId = Shader.PropertyToID("_LaserHitHeat");
        private static readonly Quaternion _SparkRotationForward = Quaternion.identity;
        private static readonly Quaternion _SparkRotationBack = new Quaternion(0f, 1f, 0f, 0f);
        private static readonly Quaternion _SparkRotationRight = new Quaternion(0f, QuaternionHalfSqrtTwo, 0f, QuaternionHalfSqrtTwo);
        private static readonly Quaternion _SparkRotationLeft = new Quaternion(0f, -QuaternionHalfSqrtTwo, 0f, QuaternionHalfSqrtTwo);
        private static readonly Quaternion _SparkRotationUp = new Quaternion(-QuaternionHalfSqrtTwo, 0f, 0f, QuaternionHalfSqrtTwo);
        private static readonly Quaternion _SparkRotationDown = new Quaternion(QuaternionHalfSqrtTwo, 0f, 0f, QuaternionHalfSqrtTwo);

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — LASER SETTINGS
        // ══════════════════════════════════════════════════════════

        [Header("── Laser Settings ────────────────────────────")]
        [Tooltip("Maximum beam range (meters).")]
        [SerializeField] private float maxRange = 5f;

        [Tooltip("Base damage per second when cutting.")]
        [SerializeField] private float damagePerSecond = 25f;

        [Tooltip("LayerMask for raycast targets.")]
        [SerializeField] private LayerMask cuttableLayer = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Header("── Heat Management ───────────────────────────")]
        [Tooltip("Seconds of continuous firing to reach overheat (heat 0→1).")]
        [SerializeField] private float overheatTime = 5f;

        [Tooltip("Heat units lost per second when NOT firing.\n" +
                 "0.3 = full cooldown from max in ~3.3 seconds.")]
        [SerializeField] private float cooldownRate = 0.3f;

        [Tooltip("Lockout duration after overheat (seconds).\n" +
                 "Tool is completely disabled during this time.")]
        [SerializeField] private float overheatLockoutTime = 2f;

        [Tooltip("Bonus damage multiplier at maximum heat.\n" +
                 "0.15 = 15% more damage when red-hot.\n" +
                 "Risk/reward: more efficient but lockout is the cost.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float heatDamageBonus = 0.15f;

        [Tooltip("Base recoil impulse used for deferred player-body kickback.")]
        [SerializeField, Range(0f, 12f)] private float recoilImpulseBase = 4f;

        [Tooltip("Additional recoil damping applied while submerged.")]
        [SerializeField, Range(0.1f, 1f)] private float submergedRecoilScale = 0.6f;

        [Tooltip("Thermal coupling scale that converts cutter damage units into seawater heat energy for localized boil anomalies.")]
        [SerializeField, Min(0f)] private float waterHeatCouplingScale = 250000f;

        [Header("── Beam Visual ───────────────────────────────")]
        [Tooltip("Maximum jitter amplitude at full heat (meters).\n" +
                 "Beam endpoint vibrates more as tool heats up.")]
        [SerializeField] private float maxJitterAmplitude = 0.008f;

        [Tooltip("Jitter frequency (Hz). Higher = faster vibration.")]
        [SerializeField] private float jitterFrequency = 50f;

        [Header("── Deconstruction ────────────────────────────")]
        [Tooltip("Seconds of continuous cutting to fully deconstruct a module.\n" +
                 "Progress resets if target changes or R/LKM released.")]
        [SerializeField] private float deconstructThreshold = 3f;
        [Tooltip("Normalized spring load required before salvage recovery progress can move.")]
        [SerializeField, Range(0f, 1f)] private float heavySalvageRequiredTension01 = 0.42f;
        [Tooltip("Normalized pull-back intent required to tear a heavy module free while cutting.")]
        [SerializeField, Range(0f, 1f)] private float heavySalvageRequiredPull01 = 0.36f;
        [Tooltip("Velocity away from the cut seam that counts as full pull intent.")]
        [SerializeField, Range(0.1f, 6f)] private float heavySalvagePullVelocityForFullIntent = 1.75f;
        [Tooltip("Retracts the cutter anchor slightly into the surface so the spring loads against the seam instead of hovering in open air.")]
        [SerializeField, Range(0f, 0.2f)] private float heavySalvageAnchorRetraction = 0.03f;

        [Header("── Visual References ─────────────────────────")]
        [Tooltip("LineRenderer for beam visualization.")]
        [SerializeField] private LineRenderer laserLine;

        [Tooltip("ParticleSystem for impact sparks.")]
        [SerializeField] private ParticleSystem sparksVFX;

        [Header("── Audio ─────────────────────────────────────")]
        [Tooltip("Looping AudioSource for cutting sound.")]
        [SerializeField] private AudioSource cutAudio;

        [Tooltip("Sound played when attempting to fire during overheat lockout.")]
        [SerializeField] private AudioClip overheatErrorClip;

        [Tooltip("Base pitch of cutting audio (at zero heat).")]
        [SerializeField] private float basePitch = 1.0f;

        [Tooltip("Maximum pitch of cutting audio (at full heat).")]
        [SerializeField] private float maxPitch = 1.3f;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>Raycast result (reused, zero GC).</summary>
        private RaycastHit _hitInfo;

        /// <summary>Cached diagnosis result (reused, zero GC).</summary>
        private CutterDiagnosis _cachedDiagnosis;

        /// <summary>Is cached diagnosis valid for current frame.</summary>
        private bool _diagnosisCached;

        /// <summary>Is beam active this frame.</summary>
        private bool _isFiring;

        /// <summary>Was beam active last frame (for toggle VFX).</summary>
        private bool _wasFiringLastFrame;

        /// <summary>Cached transform for ray origin/direction.</summary>
        private Transform _cachedTransform;

        // ── Heat State ──

        /// <summary>
        /// Current heat level [0..1].
        /// 0 = cold, 1 = overheated.
        /// Accumulates during firing, decays during idle.
        /// </summary>
        private float _heatLevel;

        /// <summary>Is tool currently in overheat lockout.</summary>
        private bool _isLockedOut;

        /// <summary>Remaining lockout time in seconds.</summary>
        private float _lockoutTimer;

        /// <summary>Last published heat value (for event throttling).</summary>
        private float _lastPublishedHeat;
        private float _lastPublishedLaserHitHeat = float.NaN;
        private bool _lastPublishedBeamActive;

        /// <summary>Has the error clip been played this lockout cycle.
        /// Prevents spamming the error sound every frame while locked.</summary>
        private bool _lockoutSoundPlayed;

        // ── Deconstruct State ──

        /// <summary>Accumulated deconstruct progress (seconds).</summary>
        private float _deconstructProgress;

        /// <summary>InstanceID of current deconstruct target (-1 = none).</summary>
        private int _cachedDeconstructTargetId = -1;

        /// <summary>Cached BaseModule of current deconstruct target.</summary>
        private BaseModule _cachedDeconstructModule;

        /// <summary>Cached PlayerInventory for Deconstruct calls.</summary>
        private PlayerInventory _cachedInventory;
        private Transform _cachedPlayerTransform;
        private HectonPlayerMovement _cachedPlayerMovement;
        private Rigidbody _cachedPlayerRigidbody;
        private HectonSurvivalSystem _cachedSurvivalSystem;

        // COLD ALLOC: persistent buffers for diagnosis and telemetry
        private FixedCharBuffer _diagnosisHeadline = new FixedCharBuffer(64);
        private FixedCharBuffer _diagnosisSummary = new FixedCharBuffer(256);
        private FixedCharBuffer _telemetryBuffer = new FixedCharBuffer(512);
        private FixedCharBuffer _recoveryFeedbackBuffer = new FixedCharBuffer(128); // COLD ALLOC: char[128] - cutter recovery HUD feedback scratch - owner: LaserCutter
        private bool _secondaryLatched;
        private bool _deconstructStartReported;
        private bool _deconstructBlockedReported;
        private float _nextProgressFeedbackAt;
        private Vector3 _cachedDeconstructAnchorPoint;
        private Vector3 _cachedDeconstructAnchorNormal = Vector3.up;
        private uint _cachedToolId;
        private ulong _raycastRequesterId;
        private byte _toolStateFlags = IdleState;

        // ── Sparks cache ──

        /// <summary>Cached emission module (struct, zero GC).</summary>
        private ParticleSystem.EmissionModule _sparksEmission;
        private bool _sparksEmissionCached;

        /// <summary>Base emission rate from prefab (for scaling).</summary>
        private float _baseSparksRate;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Current heat level [0..1]. Read by HUD systems.
        /// 0 = cold, 1 = overheated/locked.
        /// </summary>
        public float HeatLevel => _heatLevel;

        /// <summary>Is the tool currently in overheat lockout.</summary>
        public bool IsOverheated => _isLockedOut;

        internal override float ResolveModularHeatNormalized()
        {
            return _heatLevel;
        }

        protected override void ConfigureModularRuntimeProfile(ref ToolRuntimeProfile profile)
        {
            profile.MaxRange = Mathf.Max(0.1f, maxRange);
            profile.PowerScalar = Mathf.Max(0.1f, damagePerSecond);
            profile.HeatGenerationRate = math.rcp(math.max(overheatTime, 0.1f));
            profile.CooldownRate = Mathf.Max(0f, cooldownRate);
            profile.RecoilImpulse = Mathf.Max(0f, recoilImpulseBase);
        }

        public bool DebugRecoverModule(BaseModule module)
        {
            if (module == null || !module.CanDeconstruct())
                return false;

            EnsurePlayerInventory();
            Vector3 modulePosition = module.transform.position;
            if (!TryRequestModuleDeconstruction(module, modulePosition + Vector3.up, Vector3.down, 0f, 2))
                return false;

            PublishInfoMessage("LASER CUTTER - RECOVERY QUEUED");
            
            _telemetryBuffer.Clear();
            _telemetryBuffer.Append("Laser-assisted deconstruction queued for habitat rollback validation on ");
            _telemetryBuffer.Append(module.name);
            _telemetryBuffer.Append(".");

            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.LASER_CATEGORY, CutterCategory),
                "MODULE RECOVERY QUEUED",
                _telemetryBuffer,
                "INFO");
            ResetDeconstructState();
            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            EnsureLayerCache();
            _cachedTransform = transform;
            CacheSparksEmission();
            CacheToolId();
            CacheRaycastRequesterId();
            SetVisualsActive(false);
            TryAssignCutAudioMixerRoute();
            EnsurePlayerBindings();
        }

        private void OnEnable()
        {
            TryAssignCutAudioMixerRoute();
            if (Application.isPlaying)
                LaserCutterEvents.RegisterSource(this, ResolveEventCutterId(), ResolveEventTransform());
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            PublishBeamState(false);
            LaserCutterEvents.UnregisterSource(this);
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
                LaserCutterEvents.UnregisterSource(this);
        }

        private static void EnsureLayerCache()
        {
            if (_WaterLayer == int.MinValue)
                _WaterLayer = Hecton8.Core.HectonLayerMasks.Water;
            if (_TransparentFxLayer == int.MinValue)
                _TransparentFxLayer = Hecton8.Core.HectonLayerMasks.TransparentFX;
        }

        private void TryAssignCutAudioMixerRoute()
        {
            if (cutAudio == null || cutAudio.outputAudioMixerGroup != null)
                return;

            if (GlobalRegistry.Audio is SpatialAudioManager spatialAudioManager)
                cutAudio.outputAudioMixerGroup = spatialAudioManager.SfxGroup;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            TryAssignCutAudioMixerRoute();
            LaserCutterEvents.RegisterSource(this, ResolveEventCutterId(), ResolveEventTransform());
            CacheToolId();
            CacheRaycastRequesterId();
            ResetAllState();
            SetVisualsActive(false);
        }

        public override void OnDespawn()
        {
            base.OnDespawn();
            PublishBeamState(false);
            LaserCutterEvents.UnregisterSource(this);
            ResetAllState();
            SetVisualsActive(false);
        }

        public override void OnEquip()
        {
            base.OnEquip();
        }

        public override void OnUnequip()
        {
            CancelAction();
            ResetDeconstructState();
            SetVisualsActive(false);
            base.OnUnequip();
        }

        // ══════════════════════════════════════════════════════════
        //  TOOL ACTIONS
        // ══════════════════════════════════════════════════════════

        public override void UsePrimary(float deltaTime)
        {
            if (IsBroken)
            {
                OnToolBrokenWhileUsing();
                return;
            }

            if (_isLockedOut)
            {
                SetOverheatedState();
                if (!_lockoutSoundPlayed && overheatErrorClip != null)
                {
                    if (Hecton8.Core.GlobalRegistry.Audio != null)
                        Hecton8.Core.GlobalRegistry.Audio.PlayStatic2D(overheatErrorClip, 0.5f);
                    
                    _lockoutSoundPlayed = true;
                    PublishWarningMessage(ResolveLocalized(LocalizationKeys.LASER_HUD_OVERHEAT_LOCKOUT, "LASER CUTTER - OVERHEAT LOCKOUT"));
                }
                return;
            }

            base.UsePrimary(deltaTime);
            Activate();
            _isFiring = true;
            PublishBeamState(true);

            _heatLevel += deltaTime * math.rcp(math.max(overheatTime, 0.1f));

            if (_heatLevel >= 1f)
            {
                _heatLevel = 1f;
                SyncHeatOutputs();
                TriggerOverheatLockout();
                return;
            }

            bool didHit = TryGetCutHit(out _hitInfo);

            UpdateLaserLine(didHit);
            UpdateSparks(didHit);
            UpdateAudioState(true);

            if (didHit)
            {
                IInputService inputService = GlobalRegistry.Input;
                PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                    ? inputService.GetState()
                    : default;
                bool deconstructMode = inputState.HasAction(PlayerInputAction.SecondaryFire);

                if (deconstructMode)
                    ProcessDeconstructMode(deltaTime);
                else
                {
                    ResetDeconstructState();
                    ApplyCutDamage(deltaTime);
                }
            }
            else
            {
                ResetDeconstructState();
                ApplyOpenWaterBoil(deltaTime);
            }

            SyncHeatOutputs();
            PublishHeat();
        }

        public override void UseSecondary(float deltaTime)
        {
            if (IsBroken)
            {
                OnToolBrokenWhileUsing();
                return;
            }
            if (_secondaryLatched)
                return;

            base.UseSecondary(deltaTime);
            _secondaryLatched = true;

            RaycastHit diagHit;
            bool didHit = TryGetCutHit(out diagHit);

            BuildDiagnosisFromHit(diagHit, didHit, out string severity);
            _cachedDiagnosis.severity = severity;
            _diagnosisCached = true;
            
            PublishDiagnosis();
            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.LASER_CATEGORY, CutterCategory),
                _diagnosisHeadline,
                _diagnosisSummary,
                severity);
        }

        public override void ToolTick(float deltaTime)
        {
            if (_isLockedOut)
            {
                _lockoutTimer = math.max(0f, _lockoutTimer - deltaTime);
                if (_lockoutTimer <= 0f)
                {
                      _isLockedOut = false;
                      _lockoutSoundPlayed = false;
                      _heatLevel = math.min(_heatLevel, 0.8f);
                      ClearFlag(OverheatedState);
                      EnterCooldownState();
                      SyncHeatOutputs();
                      PublishHeat();
                      PublishInfoMessage(ResolveLocalized(LocalizationKeys.LASER_HUD_CORE_STABLE, "LASER CUTTER - CORE STABLE"));
                  }
              }

            if (!_isFiring && !_isLockedOut)
            {
                  if (_heatLevel > 0f)
                  {
                      _heatLevel = math.max(0f, _heatLevel - deltaTime * math.max(0f, cooldownRate));
                      EnterCooldownState();
                      SyncHeatOutputs();
                      PublishHeat();
                  }
                  else
                {
                    Deactivate();
                }
            }

            if (_wasFiringLastFrame && !_isFiring)
            {
                PublishBeamState(false);
                SetVisualsActive(false);
                ResetDeconstructState();
            }

            _wasFiringLastFrame = _isFiring;
            _isFiring = false;
            _diagnosisCached = false;

            IInputService inputService = GlobalRegistry.Input;
            PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                ? inputService.GetState()
                : default;
            if (!inputState.HasAction(PlayerInputAction.SecondaryFire))
                _secondaryLatched = false;
        }

        public override string GetOperationalSummary()
        {
            _telemetryBuffer.Clear();
            WriteOperationalSummary(ref _telemetryBuffer);
            return BuildStringFromBuffer(in _telemetryBuffer);
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            if (_isLockedOut)
            {
                buffer.Append(ResolveLocalized(LocalizationKeys.LASER_OPERATIONAL_LOCKOUT_PREFIX, "LASER CUTTER // LOCKOUT "));
                buffer.AppendInt((int)(_heatLevel * 100f));
                buffer.Append("%");
                return;
            }

            if (_cachedDeconstructModule != null)
            {
                float progress = math.saturate(_deconstructProgress * math.rcp(math.max(0.01f, deconstructThreshold)));
                buffer.Append(ResolveLocalized(LocalizationKeys.LASER_OPERATIONAL_RECOVERY_PREFIX, "LASER CUTTER // RECOVERY "));
                buffer.AppendInt((int)(progress * 100f));
                buffer.Append("%");
                return;
            }

            if (!_diagnosisCached)
                ReadDiagnosisNow();
            
            if (_diagnosisHeadline.Length > 0)
            {
                buffer.Append(ResolveLocalized(LocalizationKeys.LASER_OPERATIONAL_DIAGNOSIS_PREFIX, "LASER CUTTER // "));
                buffer.Append(_diagnosisHeadline);
                return;
            }

            if (_heatLevel > 0.01f)
            {
                buffer.Append(ResolveLocalized(LocalizationKeys.LASER_OPERATIONAL_HEAT_PREFIX, "LASER CUTTER // HEAT "));
                buffer.AppendInt((int)(_heatLevel * 100f));
                buffer.Append("%");
                return;
            }

            buffer.Append(ResolveLocalized(LocalizationKeys.LASER_OPERATIONAL_READY, "LASER CUTTER // READY"));
        }

        public override string GetOperationalDirective()
        {
            if (_isLockedOut)
                return ResolveLocalized(LocalizationKeys.LASER_DIRECTIVE_LOCKOUT, "Wait for the core to cool before firing again.");

            if (_cachedDeconstructModule != null)
                return ResolveLocalized(LocalizationKeys.LASER_DIRECTIVE_RECOVERY, "Hold the beam steady to finish recovery on the locked module.");

            if (!_diagnosisCached)
                ReadDiagnosisNow();
            
            if (_diagnosisSummary.Length > 0)
                return BuildStringFromBuffer(in _diagnosisSummary);

            if (_heatLevel >= 0.75f)
                return ResolveLocalized(LocalizationKeys.LASER_DIRECTIVE_HOT, "Core is running hot. Finish the cut or vent heat before lockout.");

            return ResolveLocalized(LocalizationKeys.LASER_DIRECTIVE_READY, "Primary cuts. Secondary diagnoses and holds recovery mode on modules.");
        }

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            if (_isLockedOut)
            {
                AppendText(ref buffer, ResolveLocalized(LocalizationKeys.LASER_DIRECTIVE_LOCKOUT, "Wait for the core to cool before firing again."));
                return;
            }

            if (_cachedDeconstructModule != null)
            {
                AppendText(ref buffer, ResolveLocalized(LocalizationKeys.LASER_DIRECTIVE_RECOVERY, "Hold the beam steady to finish recovery on the locked module."));
                return;
            }

            if (!_diagnosisCached)
                ReadDiagnosisNow();

            if (_diagnosisSummary.Length > 0)
            {
                buffer.Append(in _diagnosisSummary);
                return;
            }

            if (_heatLevel >= 0.75f)
            {
                AppendText(ref buffer, ResolveLocalized(LocalizationKeys.LASER_DIRECTIVE_HOT, "Core is running hot. Finish the cut or vent heat before lockout."));
                return;
            }

            AppendText(ref buffer, ResolveLocalized(LocalizationKeys.LASER_DIRECTIVE_READY, "Primary cuts. Secondary diagnoses and holds recovery mode on modules."));
        }

        // ══════════════════════════════════════════════════════════
        //  HEAT MANAGEMENT
        // ══════════════════════════════════════════════════════════

        private void TriggerOverheatLockout()
        {
            PublishBeamState(false);
            _isLockedOut = true;
            _lockoutTimer = math.max(0f, overheatLockoutTime);
            _lockoutSoundPlayed = false;
            _isFiring = false;
            SetOverheatedState();
            SetVisualsActive(false);
            ResetDeconstructState();
            PublishWarningMessage(ResolveLocalized(LocalizationKeys.LASER_HUD_CORE_OVERHEATED, "LASER CUTTER - CORE OVERHEATED"));
            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.LASER_CATEGORY, CutterCategory),
                ResolveLocalized(LocalizationKeys.LASER_LOG_OVERHEAT_TITLE, "LASER CORE OVERHEATED"),
                ResolveLocalized(LocalizationKeys.LASER_LOG_OVERHEAT_MESSAGE, "Cutter entered forced thermal lockout. Reduce sustained beam exposure before the next recovery pass."),
                "CRITICAL");
        }

        private void PublishHeat()
        {
            if (math.abs(_heatLevel - _lastPublishedHeat) > 0.02f)
            {
                _lastPublishedHeat = _heatLevel;
                LaserCutterEvents.RaiseHeatChanged(_heatLevel, ResolveEventCutterId(), ResolveEventRootInstanceId());
            }
        }

        private void SyncHeatOutputs()
        {
            SyncModularHeat(_heatLevel);
            if (!math.isfinite(_lastPublishedLaserHitHeat) ||
                math.abs(_heatLevel - _lastPublishedLaserHitHeat) > ShaderFloatPublishEpsilon)
            {
                Shader.SetGlobalFloat(_LaserHitHeatId, _heatLevel);
                _lastPublishedLaserHitHeat = _heatLevel;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  CUT DAMAGE
        // ══════════════════════════════════════════════════════════

        private void ApplyCutDamage(float deltaTime)
        {
            if (_hitInfo.collider == null)
                return;

            IInteractionSignalService interactionService = GlobalRegistry.InteractionSignals;
            if (interactionService == null || !interactionService.IsInitialized)
                return;

            float powerScale = GetEfficiency() * GetConditionPerformanceScale();
            float energyNormalized = ResolveSuitEnergyNormalized();
            if (energyNormalized < LowPowerThresholdNormalized)
            {
                powerScale *= LowPowerOutputScale;
                SetFlag(LowPowerState);
            }
            else
            {
                ClearFlag(LowPowerState);
            }

            float heatMultiplier = 1f + _heatLevel * heatDamageBonus;
            float runtimePower = GetRuntimePowerScalar(damagePerSecond);
            float damage = runtimePower * deltaTime * powerScale * heatMultiplier;
            if (damage <= 0f)
                return;

            Vector3 direction = _cachedTransform.forward;
            float directionSqrMagnitude = direction.sqrMagnitude;
            if (directionSqrMagnitude < 0.0001f)
                direction = Vector3.forward;
            else
                direction *= math.rsqrt(directionSqrMagnitude);

            Vector3 absoluteOrigin = ResolveAbsoluteUniversePoint(_cachedTransform.position);
            Vector3 absoluteHitPoint = ResolveAbsoluteUniversePoint(_hitInfo.point);
            float normalizedPower = ResolveNormalizedPower((runtimePower * math.rcp(math.max(damagePerSecond, 0.0001f))) * powerScale, heatMultiplier);
            if (normalizedPower < MinEffectiveBeamPower)
            {
                SetFlag(LowPowerState);
                return;
            }

            ClearFlag(LowPowerState);
            EquipmentInteractionPacket packet = new EquipmentInteractionPacket(
                _cachedToolId,
                new float3(absoluteOrigin.x, absoluteOrigin.y, absoluteOrigin.z),
                new float3(direction.x, direction.y, direction.z),
                normalizedPower,
                GetRuntimeMaxRange(maxRange),
                (byte)ToolActionMode.Primary,
                _toolStateFlags,
                (uint)Time.frameCount);
            EquipmentInteractionSignal signal = new EquipmentInteractionSignal(
                packet,
                unchecked((int)EntityId.ToULong(_hitInfo.collider.GetEntityId())),
                new float3(absoluteHitPoint.x, absoluteHitPoint.y, absoluteHitPoint.z),
                new float3(_hitInfo.normal.x, _hitInfo.normal.y, _hitInfo.normal.z),
                damage,
                (byte)InteractionEffectType.PlasmaCut,
                0);

            if (interactionService.Publish(signal, _hitInfo.collider))
            {
                TryPublishBoilSignal(interactionService, packet, damage, normalizedPower);

                SargassumCutManager cutManager = Hecton8.Core.GlobalRegistry.SargassumCut;
                if (cutManager != null)
                {
                    float terrainDamageRadius = math.lerp(0.2f, 0.75f, normalizedPower);
                    cutManager.RegisterExternalCut(_hitInfo.point, terrainDamageRadius, normalizedPower, direction, 0.1f);
                }

                DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
                if (organicManager != null)
                    organicManager.TryApplyToolHit(_hitInfo.point, _hitInfo.normal, direction, damage, normalizedPower, GetCapabilityMask());

                ApplyRecoilImpulse(direction, normalizedPower);
            }
        }

        private void ApplyOpenWaterBoil(float deltaTime)
        {
            if (_cachedPlayerMovement == null || !_cachedPlayerMovement.IsPlayerSubmerged)
                return;

            ISubmarineRuntimeContext submarine = GlobalRegistry.Submarine;
            SubmarineFluidDynamics fluidDynamics = submarine != null ? submarine.FluidDynamics : null;
            if (fluidDynamics == null || !fluidDynamics.isActiveAndEnabled)
                return;

            float powerScale = GetEfficiency() * GetConditionPerformanceScale();
            float energyNormalized = ResolveSuitEnergyNormalized();
            if (energyNormalized < LowPowerThresholdNormalized)
                powerScale *= LowPowerOutputScale;

            float heatMultiplier = 1f + _heatLevel * heatDamageBonus;
            float runtimePower = GetRuntimePowerScalar(damagePerSecond);
            float cutStrength = runtimePower * deltaTime * powerScale * heatMultiplier * math.max(0f, waterHeatCouplingScale);
            if (cutStrength <= 0f)
                return;

            Vector3 direction = _cachedTransform.forward;
            float directionSqrMagnitude = direction.sqrMagnitude;
            if (directionSqrMagnitude < 0.0001f)
                direction = Vector3.forward;
            else
                direction *= math.rsqrt(directionSqrMagnitude);

            float normalizedPower = ResolveNormalizedPower((runtimePower * math.rcp(math.max(damagePerSecond, 0.0001f))) * powerScale, heatMultiplier);
            if (normalizedPower < MinEffectiveBeamPower)
                return;

            float runtimeRange = GetRuntimeMaxRange(maxRange);
            Vector3 samplePoint = _cachedTransform.position + (direction * math.min(runtimeRange, 8f));
            fluidDynamics.InjectLocalizedWaterHeat(samplePoint, direction, cutStrength, normalizedPower);
        }

        // ══════════════════════════════════════════════════════════
        //  DECONSTRUCT MODE
        // ══════════════════════════════════════════════════════════

        private void ProcessDeconstructMode(float deltaTime)
        {
            if (_hitInfo.collider == null)
            {
                ResetDeconstructState();
                return;
            }

            EnsurePlayerBindings();

            int targetId = unchecked((int)EntityId.ToULong(_hitInfo.collider.GetEntityId()));

            if (targetId != _cachedDeconstructTargetId)
            {
                SetCachedDeconstructionPreview(false);
                _deconstructProgress = 0f;
                _cachedDeconstructTargetId = targetId;
                if (!_hitInfo.collider.TryGetComponent(out _cachedDeconstructModule))
                    _cachedDeconstructModule = _hitInfo.collider.GetComponentInParent<BaseModule>();

                if (_cachedDeconstructModule != null && _cachedDeconstructModule.CanDeconstruct())
                    SetCachedDeconstructionPreview(true);
            }

            float hitNormalSqrMagnitude = _hitInfo.normal.sqrMagnitude;
            if (hitNormalSqrMagnitude > 0.0001f)
                _cachedDeconstructAnchorNormal = _hitInfo.normal * math.rsqrt(hitNormalSqrMagnitude);
            else
                _cachedDeconstructAnchorNormal = Vector3.up;

            if (_cachedDeconstructModule == null)
            {
                if (!_deconstructBlockedReported)
                {
                    PublishWarningMessage(ResolveLocalized(LocalizationKeys.LASER_HUD_RECOVERY_NO_MODULE, "RECOVERY MODE - NO MODULE"));
                    _deconstructBlockedReported = true;
                }
                ApplyCutDamage(deltaTime);
                return;
            }

            if (!_cachedDeconstructModule.CanDeconstruct())
            {
                SetCachedDeconstructionPreview(false);
                if (!_deconstructBlockedReported)
                {
                    PublishWarningMessage(ResolveLocalized(LocalizationKeys.LASER_HUD_RECOVERY_MODULE_LOCKED, "RECOVERY MODE - MODULE LOCKED"));
                    _deconstructBlockedReported = true;
                }
                ApplyCutDamage(deltaTime);
                return;
            }

            _cachedDeconstructAnchorPoint = _hitInfo.point - _cachedDeconstructAnchorNormal * heavySalvageAnchorRetraction;
            if (_cachedPlayerMovement != null)
                _cachedPlayerMovement.ApplyCuttingTensionAnchor(_cachedDeconstructAnchorPoint, _cachedDeconstructAnchorNormal);

            float tension01 = ResolveCuttingTension01();
            float pull01 = ResolveDetachmentPull01(_cachedDeconstructAnchorPoint);
            if (tension01 < heavySalvageRequiredTension01 || pull01 < heavySalvageRequiredPull01)
            {
                if (!_deconstructStartReported)
                {
                    PublishInfoMessage("RECOVERY MODE - LOAD THE CUT");
                    _deconstructStartReported = true;
                }

                if (Time.time >= _nextProgressFeedbackAt)
                {
                    int tensionPercent = FastRoundPercent(tension01);
                    int pullPercent = FastRoundPercent(pull01);
                    ShowRecoveryPullBackFeedback(tensionPercent, pullPercent);
                    _nextProgressFeedbackAt = Time.time + 0.6f;
                }
                return;
            }

            float progressGain = deltaTime * tension01 * math.lerp(0.75f, 1.25f, pull01);
            _deconstructProgress += progressGain;
            if (!_deconstructStartReported)
            {
                PublishInfoMessage("RECOVERY MODE - TEAR IT FREE");
                _deconstructStartReported = true;
            }

            if (Time.time >= _nextProgressFeedbackAt)
            {
                float progress01 = math.saturate(_deconstructProgress * math.rcp(math.max(deconstructThreshold, 0.01f)));
                ShowRecoveryProgressFeedback(progress01);
                _nextProgressFeedbackAt = Time.time + 0.6f;
            }

            if (_deconstructProgress >= deconstructThreshold)
            {
                EnsurePlayerInventory();
                BaseModule recoveredModule = _cachedDeconstructModule;
                if (!TryRequestModuleDeconstruction(
                        recoveredModule,
                        _cachedTransform != null ? _cachedTransform.position : transform.position,
                        _cachedTransform != null ? _cachedTransform.forward : transform.forward,
                        GetRuntimeMaxRange(maxRange),
                        2))
                {
                    PublishWarningMessage(ResolveLocalized(LocalizationKeys.LASER_HUD_RECOVERY_MODULE_LOCKED, "RECOVERY MODE - MODULE LOCKED"));
                    ResetDeconstructState();
                    return;
                }

                PublishInfoMessage("LASER CUTTER - RECOVERY QUEUED");
                
                _telemetryBuffer.Clear();
                _telemetryBuffer.Append("Laser-assisted deconstruction queued for habitat rollback validation on ");
                _telemetryBuffer.Append(recoveredModule.name);
                _telemetryBuffer.Append(".");

                FieldOperationLogSystem.RecordOperation(
                    ResolveLocalized(LocalizationKeys.LASER_CATEGORY, CutterCategory),
                    "MODULE RECOVERY QUEUED",
                    _telemetryBuffer,
                    "INFO");
                ResetDeconstructState();
            }
        }

        private void ResetDeconstructState()
        {
            SetCachedDeconstructionPreview(false);

            if (_cachedPlayerMovement != null)
                _cachedPlayerMovement.ClearCuttingTensionAnchor();

            _deconstructProgress = 0f;
            _cachedDeconstructTargetId = -1;
            _cachedDeconstructModule = null;
            _deconstructStartReported = false;
            _deconstructBlockedReported = false;
            _nextProgressFeedbackAt = 0f;
            _cachedDeconstructAnchorPoint = Vector3.zero;
            _cachedDeconstructAnchorNormal = Vector3.up;
        }

        private void SetCachedDeconstructionPreview(bool enabled)
        {
            if (_cachedDeconstructModule == null)
                return;

            IHabitatDeconstructionSystem deconstructionSystem = GlobalRegistry.HabitatDeconstruction;
            if (deconstructionSystem == null || !deconstructionSystem.IsInitialized)
                return;

            uint targetEntityId = unchecked((uint)EntityId.ToULong(_cachedDeconstructModule.gameObject.GetEntityId()));
            deconstructionSystem.TrySetDeconstructionPreview(targetEntityId, enabled);
        }

        private bool TryRequestModuleDeconstruction(
            BaseModule module,
            Vector3 rayOrigin,
            Vector3 rayDirection,
            float maxDistance,
            byte toolKind)
        {
            if (module == null)
                return false;

            IHabitatDeconstructionSystem deconstructionSystem = GlobalRegistry.HabitatDeconstruction;
            if (deconstructionSystem == null || !deconstructionSystem.IsInitialized)
                return false;

            float directionLengthSq = rayDirection.sqrMagnitude;
            if (directionLengthSq <= 0.0001f)
                rayDirection = Vector3.down;
            else
                rayDirection *= math.rsqrt(directionLengthSq);

            Vector3 modulePosition = module.transform.position;
            DeconstructRequestSignal request = new DeconstructRequestSignal
            {
                TargetAup = AbsoluteUniversePosition.FromRuntimePosition(modulePosition),
                RayOriginAup = AbsoluteUniversePosition.FromRuntimePosition(rayOrigin),
                TargetEntityId = unchecked((uint)EntityId.ToULong(module.gameObject.GetEntityId())),
                RequesterEntityId = unchecked((uint)_raycastRequesterId),
                MaxDistance = Mathf.Max(0f, maxDistance),
                RayDirection = new float3(rayDirection.x, rayDirection.y, rayDirection.z),
                Frame = (uint)Mathf.Max(0, Time.frameCount),
                ToolKind = toolKind,
                Flags = 0
            };

            return deconstructionSystem.EnqueueDeconstruction(in request);
        }

        private void ShowRecoveryPullBackFeedback(int tensionPercent, int pullPercent)
        {
            _recoveryFeedbackBuffer.Clear();
            _recoveryFeedbackBuffer.Append("RECOVERY MODE - PULL BACK ");
            _recoveryFeedbackBuffer.AppendInt(math.clamp(tensionPercent, 0, RecoveryProgressMaxPercent));
            _recoveryFeedbackBuffer.Append("/");
            _recoveryFeedbackBuffer.AppendInt(math.clamp(pullPercent, 0, RecoveryProgressMaxPercent));
            ToolHitUtility.ShowInfo(in _recoveryFeedbackBuffer);
        }

        private void ShowRecoveryProgressFeedback(float progress01)
        {
            int percent = math.clamp((int)(math.saturate(progress01) * 100f + 0.5f), 0, RecoveryProgressMaxPercent);
            string template = ResolveLocalized(LocalizationKeys.LASER_RECOVERY_PROGRESS, "RECOVERY PROGRESS - {0}%");

            _recoveryFeedbackBuffer.Clear();
            if (!_recoveryFeedbackBuffer.AppendTemplate(template.AsSpan(), LocNumericArg.Int(percent)))
            {
                _recoveryFeedbackBuffer.Clear();
                _recoveryFeedbackBuffer.Append("RECOVERY PROGRESS - ");
                _recoveryFeedbackBuffer.AppendInt(percent);
                _recoveryFeedbackBuffer.Append("%");
            }

            ToolHitUtility.ShowInfo(in _recoveryFeedbackBuffer);
        }

        private void PublishInfoMessage(string message)
        {
            _telemetryBuffer.Clear();
            if (AppendText(ref _telemetryBuffer, message))
                ToolHitUtility.ShowInfo(in _telemetryBuffer);
        }

        private void PublishWarningMessage(string message)
        {
            _telemetryBuffer.Clear();
            if (AppendText(ref _telemetryBuffer, message))
                ToolHitUtility.ShowWarning(in _telemetryBuffer);
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value);
        }

        private static string BuildStringFromBuffer(in FixedCharBuffer buffer)
        {
            return buffer.Length > 0 ? new string(buffer.Buffer, 0, buffer.Length) : string.Empty;
        }

        private void EnsurePlayerInventory()
        {
            EnsurePlayerBindings();
        }

        private void EnsurePlayerBindings()
        {
            if (_cachedInventory != null &&
                _cachedPlayerMovement != null &&
                _cachedPlayerRigidbody != null &&
                _cachedSurvivalSystem != null &&
                _cachedPlayerTransform != null)
                return;

            if (!gameObject.scene.isLoaded)
                return;

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform))
            {
                _cachedPlayerTransform = playerTransform;
                if (_cachedInventory == null)
                    playerTransform.TryGetComponent(out _cachedInventory);
                if (_cachedPlayerMovement == null)
                    playerTransform.TryGetComponent(out _cachedPlayerMovement);
                if (_cachedPlayerRigidbody == null)
                    playerTransform.TryGetComponent(out _cachedPlayerRigidbody);
                if (_cachedSurvivalSystem == null)
                    playerTransform.TryGetComponent(out _cachedSurvivalSystem);
            }
        }

        private float ResolveSuitEnergyNormalized()
        {
            EnsurePlayerBindings();
            return _cachedSurvivalSystem != null ? math.saturate(_cachedSurvivalSystem.EnergyNormalized) : 1f;
        }

        private float ResolveCuttingTension01()
        {
            return _cachedPlayerMovement != null
                ? _cachedPlayerMovement.CurrentCuttingTensionNormalized
                : 0f;
        }

        private float ResolveDetachmentPull01(Vector3 anchorPoint)
        {
            EnsurePlayerBindings();
            if (_cachedPlayerTransform == null || !TryResolvePlayerAnchorOffset(anchorPoint, out Vector3 awayFromAnchor))
                return 0f;

            float sqrMagnitude = awayFromAnchor.sqrMagnitude;
            if (sqrMagnitude <= 0.0001f)
                return 0f;

            awayFromAnchor *= math.rsqrt(sqrMagnitude);
            Vector3 playerForward = _cachedPlayerTransform.forward;
            playerForward.y = 0f;
            float forwardSqrMagnitude = playerForward.sqrMagnitude;
            if (forwardSqrMagnitude > 0.0001f)
                playerForward *= math.rsqrt(forwardSqrMagnitude);
            else
                playerForward = awayFromAnchor;

            float facingAway01 = math.saturate((math.dot((float3)playerForward, (float3)awayFromAnchor) + 1f) * 0.5f);
            float backpedal01 = 0f;
            IInputService inputService = GlobalRegistry.Input;
            PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                ? inputService.GetState()
                : default;
            backpedal01 = math.saturate(-inputState.MoveDelta.y);

            float awayVelocity01 = 0f;
            if (_cachedPlayerRigidbody != null && heavySalvagePullVelocityForFullIntent > 0.01f)
            {
                float awayVelocity = math.max(0f, math.dot((float3)_cachedPlayerRigidbody.linearVelocity, (float3)awayFromAnchor));
                awayVelocity01 = math.saturate(awayVelocity / heavySalvagePullVelocityForFullIntent);
            }

            return math.max(awayVelocity01, backpedal01 * facingAway01);
        }

        private bool TryResolvePlayerAnchorOffset(Vector3 anchorPoint, out Vector3 awayFromAnchor)
        {
            if (_cachedPlayerMovement != null)
            {
                AbsoluteUniversePosition anchorAup = AbsoluteUniversePosition.FromRuntimePosition(anchorPoint);
                double3 delta = _cachedPlayerMovement.CurrentAup.ToAbsoluteDouble3() - anchorAup.ToAbsoluteDouble3();
                awayFromAnchor = default;
                awayFromAnchor.x = (float)delta.x;
                awayFromAnchor.z = (float)delta.z;
                return true;
            }

            if (_cachedPlayerTransform == null)
            {
                awayFromAnchor = default;
                return false;
            }

            awayFromAnchor = _cachedPlayerTransform.position - anchorPoint;
            awayFromAnchor.y = 0f;
            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  VISUALS
        // ══════════════════════════════════════════════════════════

        private void UpdateLaserLine(bool didHit)
        {
            if (laserLine == null) return;

            if (!laserLine.enabled)
                laserLine.enabled = true;

            laserLine.SetPosition(0, Vector3.zero);

            if (didHit)
            {
                Vector3 localHitPoint = _cachedTransform.InverseTransformPoint(_hitInfo.point);

                float jitterAmp = _heatLevel * maxJitterAmplitude;
                if (jitterAmp > 0.0001f)
                {
                    float t = Time.time * jitterFrequency;
                    float jx = FastTriangleSigned(t * InvTau) * jitterAmp;
                    float jy = FastTriangleSigned((t * LaserJitterSecondaryScale + LaserJitterSecondaryOffset) * InvTau) * jitterAmp * 0.7f;
                    localHitPoint.x += jx;
                    localHitPoint.y += jy;
                }

                laserLine.SetPosition(1, localHitPoint);
            }
            else
            {
                laserLine.SetPosition(1, Vector3.forward * GetRuntimeMaxRange(maxRange));
            }
        }

        private void UpdateSparks(bool didHit)
        {
            if (sparksVFX == null) return;

            if (didHit)
            {
                Transform sparksTransform = sparksVFX.transform;
                sparksTransform.position = _hitInfo.point;
                sparksTransform.rotation = ResolveDominantAxisRotation(_hitInfo.normal);

                if (_sparksEmissionCached)
                {
                    float heatScaledRate = _baseSparksRate * (1f + _heatLevel * 3f);
                    _sparksEmission.rateOverTimeMultiplier = heatScaledRate;
                }

                if (!sparksVFX.isPlaying)
                    sparksVFX.Play();
            }
            else
            {
                if (sparksVFX.isPlaying)
                    sparksVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private static float FastTriangleSigned(float phase)
        {
            float triangle01 = 1f - math.abs(math.frac(phase + 0.25f) * 2f - 1f);
            return triangle01 * 2f - 1f;
        }

        private static int FastRoundPercent(float value01)
        {
            return (int)(math.saturate(value01) * RecoveryProgressMaxPercent + 0.5f);
        }

        private static Quaternion ResolveDominantAxisRotation(Vector3 normal)
        {
            float absX = math.abs(normal.x);
            float absY = math.abs(normal.y);
            float absZ = math.abs(normal.z);

            if (absY >= absX && absY >= absZ)
                return normal.y >= 0f ? _SparkRotationUp : _SparkRotationDown;

            if (absX >= absZ)
                return normal.x >= 0f ? _SparkRotationRight : _SparkRotationLeft;

            return normal.z >= 0f ? _SparkRotationForward : _SparkRotationBack;
        }

        private void UpdateAudioState(bool shouldPlay)
        {
            if (cutAudio == null) return;

            if (shouldPlay)
            {
                TryAssignCutAudioMixerRoute();
                if (!cutAudio.isPlaying)
                    cutAudio.Play();

                cutAudio.pitch = math.lerp(basePitch, maxPitch, _heatLevel);
            }
            else
            {
                if (cutAudio.isPlaying)
                    cutAudio.Stop();

                cutAudio.pitch = basePitch;
            }
        }

        private void SetVisualsActive(bool active)
        {
            if (laserLine != null)
                laserLine.enabled = active;

            if (sparksVFX != null)
            {
                if (!active && sparksVFX.isPlaying)
                    sparksVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                if (!active && _sparksEmissionCached)
                    _sparksEmission.rateOverTimeMultiplier = _baseSparksRate;
            }

            if (!active)
                UpdateAudioState(false);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ══════════════════════════════════════════════════════════

        private void CacheSparksEmission()
        {
            if (sparksVFX != null)
            {
                _sparksEmission = sparksVFX.emission;
                _baseSparksRate = _sparksEmission.rateOverTimeMultiplier;
                _sparksEmissionCached = true;
            }
        }

        private void ResetAllState()
        {
            CancelAction();
            _heatLevel = 0f;
            _isLockedOut = false;
            _lockoutTimer = 0f;
            _lockoutSoundPlayed = false;
            _lastPublishedHeat = -1f;
            _lastPublishedLaserHitHeat = float.NaN;
            _secondaryLatched = false;
            SyncHeatOutputs();
            ResetDeconstructState();
        }

        public void Activate()
        {
            SetFlag(ActiveState);
            ClearFlag(IdleState);
            ClearFlag(CooldownState);
        }

        public void Deactivate()
        {
            SetFlag(IdleState);
            ClearFlag(ActiveState);
            ClearFlag(BusyState);
        }

        public void CancelAction()
        {
            PublishBeamState(false);
            _isFiring = false;
            _wasFiringLastFrame = false;
            _toolStateFlags = IdleState;
        }

        public uint GetCapabilityMask()
        {
            return ToolCapabilityMasks.PlasmaCut;
        }

        private void PublishBeamState(bool isActive)
        {
            if (_lastPublishedBeamActive == isActive)
                return;

            _lastPublishedBeamActive = isActive;
            LaserCutterEvents.RaiseBeamStateChanged(ResolveEventCutterId(), ResolveEventRootInstanceId(), isActive);
        }

        private int ResolveEventCutterId()
        {
            return unchecked((int)EntityId.ToULong(GetEntityId()));
        }

        private int ResolveEventRootInstanceId()
        {
            Transform cutterTransform = ResolveEventTransform();
            Transform rootTransform = cutterTransform != null ? cutterTransform.root : null;
            return rootTransform != null ? unchecked((int)EntityId.ToULong(rootTransform.GetEntityId())) : 0;
        }

        private Transform ResolveEventTransform()
        {
            return _cachedTransform != null ? _cachedTransform : transform;
        }

        private void BuildDiagnosisFromHit(RaycastHit hit, bool didHit, out string severity)
        {
            _diagnosisHeadline.Clear();
            _diagnosisSummary.Clear();

            if (!didHit)
            {
                _diagnosisHeadline.Append(ResolveLocalized(LocalizationKeys.LASER_HEADLINE_NO_CONTACT, "NO CONTACT"));
                _diagnosisSummary.Append(ResolveLocalized(LocalizationKeys.LASER_SUMMARY_NO_CONTACT, "Beam is firing into open water. No thermal resonance detected."));
                severity = "INFO";
                return;
            }

            if (hit.collider.TryGetComponent(out BaseModule module))
            {
                if (module.CanDeconstruct())
                {
                    _diagnosisHeadline.Append(ResolveLocalized(LocalizationKeys.LASER_HEADLINE_MODULE_LOCKED, "MODULE SECURED"));
                    _diagnosisSummary.Append(ResolveLocalized(LocalizationKeys.LASER_SUMMARY_MODULE_LOCKED, "Base module detected. Hold secondary beam to initialize salvage recovery."));
                    severity = "INFO";
                }
                else
                {
                    _diagnosisHeadline.Append(ResolveLocalized(LocalizationKeys.LASER_HEADLINE_MODULE_STABLE, "MODULE INTEGRITY HIGH"));
                    _diagnosisSummary.Append(ResolveLocalized(LocalizationKeys.LASER_SUMMARY_MODULE_STABLE, "Module is active or structurally reinforced. Deconstruction impossible."));
                    severity = "WARN";
                }
                return;
            }

            if (hit.collider.TryGetComponent(out ICuttable _))
            {
                _diagnosisHeadline.Append(ResolveLocalized(LocalizationKeys.LASER_HEADLINE_CUTTABLE_CONTACT, "CUTTABLE CONTACT"));
                _diagnosisSummary.Append(ResolveLocalized(LocalizationKeys.LASER_SUMMARY_CUTTABLE_CONTACT, "Target accepts thermal damage but is not recoverable as a base module."));
                severity = "INFO";
                return;
            }

            _diagnosisHeadline.Append(ResolveLocalized(LocalizationKeys.LASER_HEADLINE_INVALID_TARGET, "INVALID TARGET"));
            _diagnosisSummary.Append(ResolveLocalized(LocalizationKeys.LASER_SUMMARY_INVALID_TARGET, "Target is inside beam range but does not respond to cutter operations."));
            severity = "WARN";
        }

        private void ReadDiagnosisNow()
        {
            bool didHit = TryGetCutHit(out RaycastHit hit);
            BuildDiagnosisFromHit(hit, didHit, out string severity);
            _cachedDiagnosis.severity = severity;
            _diagnosisCached = true;
        }

        private bool TryGetCutHit(out RaycastHit hit)
        {
            IInteractionSignalService interactionService = GlobalRegistry.InteractionSignals;
            if (interactionService != null && interactionService.IsInitialized)
                return interactionService.TryRaycastPrimary(_raycastRequesterId, _cachedTransform.position, _cachedTransform.forward, GetRuntimeMaxRange(maxRange), ResolveCuttableRaycastMask(), QueryTriggerInteraction.Ignore, out hit);

            hit = default;
            return false;
        }

        private void TryPublishBoilSignal(IInteractionSignalService interactionService, in EquipmentInteractionPacket packet, float deliveredDamage, float normalizedPower)
        {
            if (interactionService == null || _hitInfo.collider == null || _cachedPlayerMovement == null || !_cachedPlayerMovement.IsPlayerSubmerged)
                return;

            float coupledCutStrength = deliveredDamage * math.max(0f, waterHeatCouplingScale);
            if (coupledCutStrength <= 0f || normalizedPower < MinEffectiveBeamPower)
                return;

            Vector3 absoluteHitPoint = ResolveAbsoluteUniversePoint(_hitInfo.point);
            EquipmentInteractionSignal boilSignal = new EquipmentInteractionSignal(
                packet,
                unchecked((int)EntityId.ToULong(_hitInfo.collider.GetEntityId())),
                new float3(absoluteHitPoint.x, absoluteHitPoint.y, absoluteHitPoint.z),
                new float3(_hitInfo.normal.x, _hitInfo.normal.y, _hitInfo.normal.z),
                coupledCutStrength,
                (byte)InteractionEffectType.Boil,
                0);

            interactionService.Publish(in boilSignal, _hitInfo.collider);
        }

        private int ResolveCuttableRaycastMask()
        {
            int mask = cuttableLayer.value;
            if (_WaterLayer >= 0)
                mask &= ~(1 << _WaterLayer);
            if (_TransparentFxLayer >= 0)
                mask &= ~(1 << _TransparentFxLayer);
            return mask;
        }

        private void PublishDiagnosis()
        {
            _telemetryBuffer.Clear();
            _telemetryBuffer.Append(ResolveLocalized(LocalizationKeys.LASER_DIAG_MESSAGE_PREFIX, "LASER DIAG - "));
            _telemetryBuffer.Append(_diagnosisHeadline);

            if (_cachedDiagnosis.severity == "WARN" || _cachedDiagnosis.severity == "CRITICAL")
                ToolHitUtility.ShowWarning(_telemetryBuffer);
            else
                ToolHitUtility.ShowInfo(_telemetryBuffer);
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback) : fallback;
        }

        private void CacheToolId()
        {
            string toolIdSource = RuntimeMetadata != null && !string.IsNullOrWhiteSpace(RuntimeMetadata.toolID) ? RuntimeMetadata.toolID : "tool_laser_cutter";
            _cachedToolId = unchecked((uint)Animator.StringToHash(toolIdSource));
        }

        private void CacheRaycastRequesterId()
        {
            _raycastRequesterId = EntityId.ToULong(gameObject.GetEntityId());
        }

        private float ResolveNormalizedPower(float powerScale, float heatMultiplier)
        {
            float normalizedPower = powerScale * (heatMultiplier * math.rcp(math.max(1f + heatDamageBonus, 0.0001f)));
            return math.saturate(normalizedPower);
        }

        private static Vector3 ResolveAbsoluteUniversePoint(Vector3 runtimePoint)
        {
            return runtimePoint + HectonFloatingOrigin.CurrentTotalOffset;
        }

        private void ApplyRecoilImpulse(Vector3 direction, float normalizedPower)
        {
            EnsurePlayerBindings();
            if (normalizedPower <= 0f)
                return;

            float mass = _cachedPlayerRigidbody != null ? Mathf.Max(_cachedPlayerRigidbody.mass, 0.1f) : 1f;
            float recoilScale = _cachedPlayerMovement != null && _cachedPlayerMovement.IsPlayerSubmerged ? submergedRecoilScale : 1f;
            float runtimeRecoil = GetRuntimeRecoilImpulse(recoilImpulseBase);
            float impulseMagnitude = Mathf.Min(MaxRecoilImpulse, (runtimeRecoil * normalizedPower * recoilScale) / mass);
            if (impulseMagnitude <= 0.0001f)
                return;

            TryQueuePlayerToolRecoil(direction, impulseMagnitude);
            QueueToolHapticFeedback(normalizedPower, 1f);
        }

        private void EnterCooldownState()
        {
            SetFlag(CooldownState);
            SetFlag(IdleState);
            ClearFlag(ActiveState);
        }

        private void SetOverheatedState()
        {
            SetFlag(OverheatedState);
            ClearFlag(ActiveState);
            ClearFlag(BusyState);
        }

        private void SetFlag(byte flag)
        {
            _toolStateFlags |= flag;
        }

        private void ClearFlag(byte flag)
        {
            _toolStateFlags &= unchecked((byte)~flag);
        }
    }
}

