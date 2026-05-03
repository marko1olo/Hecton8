// ============================================================================
// HECTON-8 — LaserCutter.cs  v2.2
// Лазерный резак — PlayerTool с термическим менеджментом.
//
// v2.2 CHANGES (ZERO-GC REFACTOR):
//   [ZERO-GC] Diagnosis system entirely refactored to use FixedCharBuffer.
//     • Eliminated string.Format and string interpolation in diagnosis and operational summaries.
//     • Removed legacy CutterDiagnosis fields (headline/summary) in favor of persistent buffers.
//     • Consolidated state management and removed clobbered field declarations.
//
//   [OPT] Player inventory resolve moved out of hot loop (EnsurePlayerInventory)
//     to ONE-TIME initialization in Awake().
//
// ============================================================================

namespace Hecton8.Gameplay
{
    using System;
    using System.Runtime.InteropServices;
    using Hecton8.Bootstrap;
    using Hecton8.Building;
    using Hecton8.Construction;
    using Hecton8.Core;
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
        private static int _pendingEventCount;
        private static int _sourceCount;

        /// <summary>
        /// Pending payload count in the cutter event lane.
        /// </summary>
        public static int PendingCount => _pendingEvents.IsCreated ? _pendingEventCount : 0;

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
                for (int i = count - 1; i >= 0; i--)
                    rawArray[i].OnLaserCutterEvent(in payload);
            }

            if (_pendingEvents.IsEmpty())
                _pendingEventCount = 0;
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
            if (_pendingEvents.IsCreated)
                return;

            _pendingEvents = new NativeQueue<LaserCutterEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<LaserCutterEventPayload>[16] - deferred cutter event lane flushed by SystemDispatcher LateUpdate - owner: LaserCutterEvents
            NativeMemorySentinel.RegisterNativeQueue(
                _pendingEvents,
                PendingEventCapacity,
                nameof(LaserCutterEvents),
                nameof(_pendingEvents),
                NativeAllocationLifetime.Session);
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

            _listeners.Clear();
            for (int i = 0; i < _sourceCount; i++)
                _sources[i] = default;

            _sourceCount = 0;
            _pendingEventCount = 0;
        }

        private static void Enqueue(in LaserCutterEventPayload payload)
        {
            if (payload.CutterInstanceId == 0 || payload.CutterRootInstanceId == 0)
                return;

            EnsureInitialized();
            if (_pendingEventCount >= PendingEventCapacity)
                return;

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
        }

        private static void DrainWithoutDispatch()
        {
            if (!_pendingEvents.IsCreated)
                return;

            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out _))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;
            }

            if (_pendingEvents.IsEmpty())
                _pendingEventCount = 0;
        }
    }

    [DisallowMultipleComponent]
    public sealed class LaserCutter : PlayerTool, IToolModule
    {
        private const string CutterCategory = "CUTTER";
        private const int RecoveryProgressMessageCount = 101;
        private const float MaxRecoilImpulse = 12f;
        private const float MinEffectiveBeamPower = 0.02f;
        private const float LowPowerThresholdNormalized = 0.12f;
        private const float LowPowerOutputScale = 0.35f;
        private const float CutHitSphereRadiusMeters = 0.5f;
        private const int CutHitBufferCapacity = 8;
        private static int _WaterLayer = int.MinValue;
        private static int _TransparentFxLayer = int.MinValue;
        private static int _BaseModuleLayer = int.MinValue;
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

        // COLD ALLOC: String[101] — localized recovery progress HUD cache — owner: LaserCutter
        private static string[] _recoveryProgressMessages;
        private static GameLanguage _recoveryProgressLanguage = (GameLanguage)(-1);

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

        [Tooltip("Passive heat decay bonus applied while the player remains submerged.")]
        [SerializeField, Range(0f, 1.2f)] private float passiveWaterCoolingBonus = 0.45f;

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
        // COLD ALLOC: RaycastHit[8] - cutter-local spherecast hit arbitration buffer - owner: LaserCutter
        private readonly RaycastHit[] _cutHitBuffer = new RaycastHit[CutHitBufferCapacity];

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
        private readonly FixedCharBuffer _diagnosisHeadline = new FixedCharBuffer(64);
        private readonly FixedCharBuffer _diagnosisSummary = new FixedCharBuffer(256);
        private readonly FixedCharBuffer _telemetryBuffer = new FixedCharBuffer(512);

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
            profile.HeatGenerationRate = 1f / math.max(overheatTime, 0.1f);
            profile.CooldownRate = Mathf.Max(0f, cooldownRate);
            profile.RecoilImpulse = Mathf.Max(0f, recoilImpulseBase);
        }

        public bool DebugRecoverModule(BaseModule module)
        {
            if (module == null || !module.CanDeconstruct())
                return false;

            EnsurePlayerInventory();
            module.Deconstruct(_cachedInventory);
            ArchiveRecoveredModule(module);
            ToolHitUtility.ShowInfo(ResolveLocalized(LocalizationKeys.LASER_HUD_MODULE_RECOVERED, "LASER CUTTER - MODULE RECOVERED"));
            
            _telemetryBuffer.Clear();
            _telemetryBuffer.Append(ResolveLocalized(LocalizationKeys.LASER_LOG_MODULE_RECOVERY_MESSAGE_PREFIX, "Laser-assisted deconstruction completed on "));
            _telemetryBuffer.Append(module.name);
            _telemetryBuffer.Append(".");

            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.LASER_CATEGORY, CutterCategory),
                ResolveLocalized(LocalizationKeys.LASER_LOG_MODULE_RECOVERY_TITLE, "MODULE RECOVERY COMPLETED"),
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
            
            EnsurePlayerBindings();
        }

        private void OnEnable()
        {
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
            if (_BaseModuleLayer == int.MinValue)
                _BaseModuleLayer = Hecton8.Core.HectonLayerMasks.BaseModule;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
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
                    ToolHitUtility.ShowWarning(ResolveLocalized(LocalizationKeys.LASER_HUD_OVERHEAT_LOCKOUT, "LASER CUTTER - OVERHEAT LOCKOUT"));
                }
                return;
            }

            base.UsePrimary(deltaTime);
            Activate();
            _isFiring = true;
            PublishBeamState(true);

            _heatLevel += deltaTime * GetRuntimeHeatGenerationRate(1f / math.max(overheatTime, 0.1f));

            if (_heatLevel >= 1f)
            {
                _heatLevel = 1f;
                SyncModularHeat(_heatLevel);
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

            SyncModularHeat(_heatLevel);
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
                      SyncModularHeat(_heatLevel);
                      PublishHeat();
                      ToolHitUtility.ShowInfo(ResolveLocalized(LocalizationKeys.LASER_HUD_CORE_STABLE, "LASER CUTTER - CORE STABLE"));
                  }
              }

            if (!_isFiring && !_isLockedOut)
            {
                  if (_heatLevel > 0f)
                  {
                      float runtimeCooldown = GetRuntimeCooldownRate(cooldownRate);
                      _heatLevel = math.max(0f, _heatLevel - deltaTime * runtimeCooldown * (1f + ResolvePassiveCoolingBonus()));
                      EnterCooldownState();
                      SyncModularHeat(_heatLevel);
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
            if (_isLockedOut)
            {
                _telemetryBuffer.Clear();
                _telemetryBuffer.Append(ResolveLocalized(LocalizationKeys.LASER_OPERATIONAL_LOCKOUT_PREFIX, "LASER CUTTER // LOCKOUT "));
                _telemetryBuffer.AppendInt((int)(_heatLevel * 100f));
                _telemetryBuffer.Append("%");
                return _telemetryBuffer.ToString();
            }

            if (_cachedDeconstructModule != null)
            {
                float progress = Mathf.Clamp01(_deconstructProgress / math.max(0.01f, deconstructThreshold));
                _telemetryBuffer.Clear();
                _telemetryBuffer.Append(ResolveLocalized(LocalizationKeys.LASER_OPERATIONAL_RECOVERY_PREFIX, "LASER CUTTER // RECOVERY "));
                _telemetryBuffer.AppendInt((int)(progress * 100f));
                _telemetryBuffer.Append("%");
                return _telemetryBuffer.ToString();
            }

            if (!_diagnosisCached)
                ReadDiagnosisNow();
            
            if (_diagnosisHeadline.Length > 0)
            {
                _telemetryBuffer.Clear();
                _telemetryBuffer.Append(ResolveLocalized(LocalizationKeys.LASER_OPERATIONAL_DIAGNOSIS_PREFIX, "LASER CUTTER // "));
                _telemetryBuffer.Append(_diagnosisHeadline);
                return _telemetryBuffer.ToString();
            }

            if (_heatLevel > 0.01f)
            {
                _telemetryBuffer.Clear();
                _telemetryBuffer.Append(ResolveLocalized(LocalizationKeys.LASER_OPERATIONAL_HEAT_PREFIX, "LASER CUTTER // HEAT "));
                _telemetryBuffer.AppendInt((int)(_heatLevel * 100f));
                _telemetryBuffer.Append("%");
                return _telemetryBuffer.ToString();
            }

            return ResolveLocalized(LocalizationKeys.LASER_OPERATIONAL_READY, "LASER CUTTER // READY");
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
                return _diagnosisSummary.ToString();

            if (_heatLevel >= 0.75f)
                return ResolveLocalized(LocalizationKeys.LASER_DIRECTIVE_HOT, "Core is running hot. Finish the cut or vent heat before lockout.");

            return ResolveLocalized(LocalizationKeys.LASER_DIRECTIVE_READY, "Primary cuts. Secondary diagnoses and holds recovery mode on modules.");
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
            ToolHitUtility.ShowWarning(ResolveLocalized(LocalizationKeys.LASER_HUD_CORE_OVERHEATED, "LASER CUTTER - CORE OVERHEATED"));
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
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.forward;
            else
                direction.Normalize();

            Vector3 absoluteOrigin = ResolveAbsoluteUniversePoint(_cachedTransform.position);
            Vector3 absoluteHitPoint = ResolveAbsoluteUniversePoint(_hitInfo.point);
            float normalizedPower = ResolveNormalizedPower((runtimePower / math.max(damagePerSecond, 0.0001f)) * powerScale, heatMultiplier);
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
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.forward;
            else
                direction.Normalize();

            float normalizedPower = ResolveNormalizedPower((runtimePower / math.max(damagePerSecond, 0.0001f)) * powerScale, heatMultiplier);
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
                _deconstructProgress = 0f;
                _cachedDeconstructTargetId = targetId;
                _cachedDeconstructModule = _hitInfo.collider.GetComponent<BaseModule>() ?? _hitInfo.collider.GetComponentInParent<BaseModule>();
            }

            if (_hitInfo.normal.sqrMagnitude > 0.0001f)
                _cachedDeconstructAnchorNormal = _hitInfo.normal.normalized;
            else
                _cachedDeconstructAnchorNormal = Vector3.up;

            if (_cachedDeconstructModule == null)
            {
                if (!_deconstructBlockedReported)
                {
                    ToolHitUtility.ShowWarning(ResolveLocalized(LocalizationKeys.LASER_HUD_RECOVERY_NO_MODULE, "RECOVERY MODE - NO MODULE"));
                    _deconstructBlockedReported = true;
                }
                ApplyCutDamage(deltaTime);
                return;
            }

            if (!_cachedDeconstructModule.CanDeconstruct())
            {
                if (!_deconstructBlockedReported)
                {
                    ToolHitUtility.ShowWarning(ResolveLocalized(LocalizationKeys.LASER_HUD_RECOVERY_MODULE_LOCKED, "RECOVERY MODE - MODULE LOCKED"));
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
                    ToolHitUtility.ShowInfo("RECOVERY MODE - LOAD THE CUT");
                    _deconstructStartReported = true;
                }

                if (Time.time >= _nextProgressFeedbackAt)
                {
                    int tensionPercent = Mathf.RoundToInt(tension01 * 100f);
                    int pullPercent = Mathf.RoundToInt(pull01 * 100f);
                    ToolHitUtility.ShowInfo("RECOVERY MODE - PULL BACK " + tensionPercent + "/" + pullPercent);
                    _nextProgressFeedbackAt = Time.time + 0.6f;
                }
                return;
            }

            float progressGain = deltaTime * tension01 * math.lerp(0.75f, 1.25f, pull01);
            _deconstructProgress += progressGain;
            if (!_deconstructStartReported)
            {
                ToolHitUtility.ShowInfo("RECOVERY MODE - TEAR IT FREE");
                _deconstructStartReported = true;
            }

            if (Time.time >= _nextProgressFeedbackAt)
            {
                float progress01 = math.saturate(_deconstructProgress / math.max(deconstructThreshold, 0.01f));
                ToolHitUtility.ShowInfo(GetRecoveryProgressMessage(progress01));
                _nextProgressFeedbackAt = Time.time + 0.6f;
            }

            if (_deconstructProgress >= deconstructThreshold)
            {
                EnsurePlayerInventory();
                _cachedDeconstructModule.Deconstruct(_cachedInventory);
                ArchiveRecoveredModule(_cachedDeconstructModule);
                ToolHitUtility.ShowInfo(ResolveLocalized(LocalizationKeys.LASER_HUD_MODULE_RECOVERED, "LASER CUTTER - MODULE RECOVERED"));
                
                _telemetryBuffer.Clear();
                _telemetryBuffer.Append(ResolveLocalized(LocalizationKeys.LASER_LOG_MODULE_RECOVERY_MESSAGE_PREFIX, "Laser-assisted deconstruction completed on "));
                _telemetryBuffer.Append(_cachedDeconstructModule.name);
                _telemetryBuffer.Append(".");

                FieldOperationLogSystem.RecordOperation(
                    ResolveLocalized(LocalizationKeys.LASER_CATEGORY, CutterCategory),
                    ResolveLocalized(LocalizationKeys.LASER_LOG_MODULE_RECOVERY_TITLE, "MODULE RECOVERY COMPLETED"),
                    _telemetryBuffer,
                    "INFO");
                ResetDeconstructState();
            }
        }

        private void ResetDeconstructState()
        {
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

        private static string GetRecoveryProgressMessage(float progress01)
        {
            EnsureRecoveryProgressMessages();
            int percent = (int)(math.saturate(progress01) * 100f + 0.5f);
            percent = math.clamp(percent, 0, RecoveryProgressMessageCount - 1);
            return _recoveryProgressMessages[percent];
        }

        private static void EnsureRecoveryProgressMessages()
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            GameLanguage language = manager != null ? manager.CurrentLanguage : GameLanguage.English;
            if (_recoveryProgressMessages != null && _recoveryProgressMessages.Length == RecoveryProgressMessageCount && _recoveryProgressLanguage == language)
                return;

            string[] messages = new string[RecoveryProgressMessageCount];
            string template = ResolveLocalized(LocalizationKeys.LASER_RECOVERY_PROGRESS, "RECOVERY PROGRESS - {0}%");
            for (int i = 0; i < RecoveryProgressMessageCount; i++)
                messages[i] = string.Format(template, i);
            _recoveryProgressMessages = messages;
            _recoveryProgressLanguage = language;
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

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
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
            if (_cachedPlayerTransform == null)
                return 0f;

            Vector3 awayFromAnchor = _cachedPlayerTransform.position - anchorPoint;
            awayFromAnchor.y = 0f;
            float sqrMagnitude = awayFromAnchor.sqrMagnitude;
            if (sqrMagnitude <= 0.0001f)
                return 0f;

            awayFromAnchor *= 1f / Mathf.Sqrt(sqrMagnitude);
            Vector3 playerForward = _cachedPlayerTransform.forward;
            playerForward.y = 0f;
            float forwardSqrMagnitude = playerForward.sqrMagnitude;
            if (forwardSqrMagnitude > 0.0001f)
                playerForward *= 1f / Mathf.Sqrt(forwardSqrMagnitude);
            else
                playerForward = awayFromAnchor;

            float facingAway01 = Mathf.Clamp01((Vector3.Dot(playerForward, awayFromAnchor) + 1f) * 0.5f);
            float backpedal01 = 0f;
            IInputService inputService = GlobalRegistry.Input;
            PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                ? inputService.GetState()
                : default;
            backpedal01 = Mathf.Clamp01(-inputState.MoveDelta.y);

            float awayVelocity01 = 0f;
            if (_cachedPlayerRigidbody != null && heavySalvagePullVelocityForFullIntent > 0.01f)
            {
                float awayVelocity = Mathf.Max(0f, Vector3.Dot(_cachedPlayerRigidbody.linearVelocity, awayFromAnchor));
                awayVelocity01 = Mathf.Clamp01(awayVelocity / heavySalvagePullVelocityForFullIntent);
            }

            return Mathf.Max(awayVelocity01, backpedal01 * facingAway01);
        }

        private static void ArchiveRecoveredModule(BaseModule module)
        {
            if (module == null || Hecton8.Core.GlobalRegistry.ScanLog == null)
                return;

            ModuleMarker marker = module.GetComponent<ModuleMarker>();
            BuildableData data = marker != null ? marker.Data : null;
            if (data == null)
                return;

            string moduleId = data.PersistentId;
            if (string.IsNullOrWhiteSpace(moduleId))
                return;

            string entryId = $"recovery.module.{moduleId}".ToLowerInvariant();
            string title = string.Format(
                ResolveLocalized(LocalizationKeys.LASER_ARCHIVE_RECOVERY_TITLE, "{0} RECOVERY"),
                data.moduleName);
            string category = ResolveLocalized(LocalizationKeys.LASER_ARCHIVE_CATEGORY, "Construction");
            string summary = string.Format(
                ResolveLocalized(
                    LocalizationKeys.LASER_ARCHIVE_RECOVERY_SUMMARY,
                    "Laser-assisted recovery completed for {0}. Structural blueprint and salvage profile archived."),
                data.moduleName);
            Hecton8.Core.GlobalRegistry.ScanLog.ArchiveEntry(entryId, title, category, summary);
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
                    float jx = math.sin(t) * jitterAmp;
                    float jy = math.sin(t * 1.37f + 2.1f) * jitterAmp * 0.7f;
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
                sparksTransform.rotation = Quaternion.LookRotation(_hitInfo.normal);

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

        private void UpdateAudioState(bool shouldPlay)
        {
            if (cutAudio == null) return;

            if (shouldPlay)
            {
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
            _secondaryLatched = false;
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
            if (TryGetCutSphereHit(out hit))
                return true;

            IInteractionSignalService interactionService = GlobalRegistry.InteractionSignals;
            if (interactionService != null && interactionService.IsInitialized)
                return interactionService.TryRaycastPrimary(_raycastRequesterId, _cachedTransform.position, _cachedTransform.forward, GetRuntimeMaxRange(maxRange), ResolveCuttableRaycastMask(), QueryTriggerInteraction.Ignore, out hit);

            hit = default;
            return false;
        }

        private bool TryGetCutSphereHit(out RaycastHit hit)
        {
            hit = default;
            if (_cachedTransform == null)
                return false;

            Vector3 direction = _cachedTransform.forward;
            float range = GetRuntimeMaxRange(maxRange);
            if (range <= 0f || direction.sqrMagnitude <= 0.0001f)
                return false;

            int layerMask = ResolveCuttableRaycastMask();
            Vector3 origin = _cachedTransform.position;
            Vector3 normalizedDirection = direction.normalized;
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                origin,
                CutHitSphereRadiusMeters,
                normalizedDirection,
                _cutHitBuffer,
                range,
                layerMask,
                QueryTriggerInteraction.Ignore);
            if (hitCount <= 0)
                return false;

            int nearestHitIndex = -1;
            int nearestBaseHitIndex = -1;
            int nearestNonBaseHitIndex = -1;
            float nearestHitDistance = float.MaxValue;
            float nearestBaseHitDistance = float.MaxValue;
            float nearestNonBaseHitDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = _cutHitBuffer[i];
                if (!IsValidCutHit(origin, normalizedDirection, range, layerMask, in candidate))
                    continue;

                float candidateDistance = candidate.distance;
                if (candidateDistance < nearestHitDistance)
                {
                    nearestHitDistance = candidateDistance;
                    nearestHitIndex = i;
                }

                if (IsBaseModuleHit(candidate.collider))
                {
                    if (candidateDistance < nearestBaseHitDistance)
                    {
                        nearestBaseHitDistance = candidateDistance;
                        nearestBaseHitIndex = i;
                    }
                }
                else if (candidateDistance < nearestNonBaseHitDistance)
                {
                    nearestNonBaseHitDistance = candidateDistance;
                    nearestNonBaseHitIndex = i;
                }
            }

            if (nearestBaseHitIndex >= 0 && nearestNonBaseHitIndex >= 0)
            {
                RaycastHit baseHit = _cutHitBuffer[nearestBaseHitIndex];
                RaycastHit floraPriorityHit = _cutHitBuffer[nearestNonBaseHitIndex];
                if ((floraPriorityHit.point - baseHit.point).sqrMagnitude <= (CutHitSphereRadiusMeters * CutHitSphereRadiusMeters))
                {
                    hit = floraPriorityHit;
                    return true;
                }
            }

            if (nearestBaseHitIndex >= 0)
            {
                RaycastHit baseHit = _cutHitBuffer[nearestBaseHitIndex];
                if (IsBaseHitOccludedByConsumableFlora(baseHit.point))
                {
                    hit = baseHit;
                    return true;
                }
            }

            if (nearestHitIndex < 0)
                return false;

            hit = _cutHitBuffer[nearestHitIndex];
            return true;
        }

        private static bool IsValidCutHit(Vector3 origin, Vector3 direction, float range, int layerMask, in RaycastHit hit)
        {
            if (hit.collider == null || hit.distance <= 0.05f || hit.distance > range)
                return false;

            int layer = hit.collider.gameObject.layer;
            if ((layerMask & (1 << layer)) == 0)
                return false;

            Vector3 toHit = hit.point - origin;
            if (Vector3.Dot(hit.normal, direction) >= 0f)
                return false;

            return toHit.sqrMagnitude > 0.0001f;
        }

        private static bool IsBaseModuleHit(Collider collider)
        {
            EnsureLayerCache();
            return collider != null && collider.gameObject.layer == _BaseModuleLayer;
        }

        private static bool IsBaseHitOccludedByConsumableFlora(Vector3 worldHitPoint)
        {
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            if (organicManager == null)
                return false;

            Vector3 runtimeHitPoint = HectonFloatingOrigin.ToRuntimePosition(worldHitPoint);
            return organicManager.TryResolveNearestConsumableFlora(
                runtimeHitPoint,
                CutHitSphereRadiusMeters,
                out _,
                out _);
        }

        private void TryPublishBoilSignal(IInteractionSignalService interactionService, in EquipmentInteractionPacket packet, float deliveredDamage, float normalizedPower)
        {
            if (interactionService == null || _hitInfo.collider == null || _cachedPlayerMovement == null || !_cachedPlayerMovement.IsPlayerSubmerged)
                return;

            float coupledCutStrength = deliveredDamage * math.max(0f, waterHeatCouplingScale);
            if (coupledCutStrength <= 0f || normalizedPower < MinEffectiveBeamPower)
                return;

            EquipmentInteractionSignal boilSignal = new EquipmentInteractionSignal(
                packet,
                unchecked((int)EntityId.ToULong(_hitInfo.collider.GetEntityId())),
                new float3(_hitInfo.point.x, _hitInfo.point.y, _hitInfo.point.z),
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
            float normalizedPower = powerScale * (heatMultiplier / math.max(1f + heatDamageBonus, 0.0001f));
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

        private float ResolvePassiveCoolingBonus()
        {
            EnsurePlayerBindings();
            return _cachedPlayerMovement != null && _cachedPlayerMovement.IsPlayerSubmerged ? passiveWaterCoolingBonus : 0f;
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

