namespace Hecton8.Gameplay
{
    using System;
    using System.Collections.Generic;
    using Hecton8.Core;
    using Hecton8.Interaction;
    using Hecton8.Tools;
    using Hecton8.Visor;
    using Hecton8.World;
    using NASAPunk.Visor;
    using TMPro;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Cheap-math LifePod crash/cold-start coordinator. It drives shader-state fakes instead of simulation.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/LifePod Tactile Prologue Controller")]
    public sealed class LifePodTactilePrologueController : MonoBehaviour, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const uint StateCrashActive = 1u << 0;
        private const uint StateStrapsLocked = 1u << 1;
        private const uint StateVented = 1u << 2;
        private const uint StateScrubberInserted = 1u << 3;
        private const uint StatePowerRestored = 1u << 4;
        private const uint StateSmokeActive = 1u << 5;
        private const uint StateFoamActive = 1u << 6;
        private const uint ColdStartReadyMask = StateStrapsLocked | StateVented | StateScrubberInserted;

        private const int BiosBufferCapacity = 512;
        private const byte HapticPriorityCritical = ToolHapticsRuntime.PriorityCritical;
        private const byte BothMotorMask = 0x03;
        private const uint DefaultImpactSeed = 0xA53C9E27u;
        private const float MaxToolHapticFrequencyHz = 60f;
        private const float ColdStartHapticFrequencyHz = MaxToolHapticFrequencyHz;
        private const float ImpactHapticFrequencyHz = MaxToolHapticFrequencyHz;
        private const float MinimumBiosRefreshSeconds = 0.05f;
        private const int SeatStrapLatchColdCapacity = 8;
        private const uint BiosLootCacheFrameMask = 0x7u;
        private const uint ColdReferenceSeatStrapCoordinator = 1u << 0;
        private const uint ColdReferenceDamageSystem = 1u << 1;
        private const uint ColdReferenceVentValve = 1u << 2;
        private const uint ColdReferenceScrubberSocket = 1u << 3;
        private const uint ColdReferenceBiosText = 1u << 4;
        private const uint ColdReferenceSeatStrapLatches = 1u << 5;
        private const uint ColdReferenceSearchAll =
            ColdReferenceSeatStrapCoordinator |
            ColdReferenceDamageSystem |
            ColdReferenceVentValve |
            ColdReferenceScrubberSocket |
            ColdReferenceBiosText |
            ColdReferenceSeatStrapLatches;
        private const float FoamFlowMinimumLengthSq = 0.000001f;
        private const float FoamFlowTrustedMinimumLengthSq = 0.64f;
        private const float FoamFlowTrustedMaximumLengthSq = 1.44f;

        private static readonly int LifePodSmokeParamsId = Shader.PropertyToID("_LifePodSmokeParams");
        private static readonly int PodGravityVectorId = Shader.PropertyToID("_PodGravityVector");
        private static readonly int LifePodVisorVibrationId = Shader.PropertyToID("_HectonLifePodVisorVibration");
        private static readonly int LifePodFoamParamsId = Shader.PropertyToID("_HectonLifePodFoamParams");

        [Header("Crash Sequence")]
        [Tooltip("Starts the crash sequence when the component is enabled.")]
        [SerializeField] private bool autoBeginOnEnable;

        [Tooltip("Deterministic seed used for impact short-circuit selection.")]
        [SerializeField] private uint impactSeed = DefaultImpactSeed;

        [Tooltip("Normalized impact force driving damage, haptics, and camera feedback.")]
        [SerializeField, Range(0f, 1f)] private float impactSeverity01 = 0.82f;

        [Tooltip("Additional PC camera shake scale. VR uses visor vibration instead.")]
        [SerializeField, Range(0f, 2f)] private float pcImpactShakeScale = 1.35f;

        [Tooltip("Screen-space visor vibration intensity used for XR comfort-safe impact feedback.")]
        [SerializeField, Range(0f, 1f)] private float vrVisorVibrationIntensity = 0.74f;

        [Tooltip("Seconds to hold the initial XR visor vibration before decay begins.")]
        [SerializeField, Min(0.01f)] private float vrVisorVibrationHoldSeconds = 0.11f;

        [Tooltip("Linear decay speed for shader-driven XR visor vibration.")]
        [SerializeField, Min(0.1f)] private float vrVisorVibrationRecoverySpeed = 7.5f;

        [Header("Physical Locks")]
        [Tooltip("Seat-strap coordinator that owns the two-strap panic latch.")]
        [SerializeField] private LifePodSeatStrapCoordinator seatStrapCoordinator;

        [Tooltip("Damage mask owner responsible for deterministic spark quads.")]
        [SerializeField] private LifePodDamageSystem damageSystem;

        [Tooltip("Physical valve handle used to purge smoke from the LifePod.")]
        [SerializeField] private VRValveWheelHandle ventValve;

        [Tooltip("Kinematic socket used as the O2 scrubber cold-start insertion point.")]
        [SerializeField] private PhysicalBatteryCompartment o2ScrubberSocket;

        [Header("Shader Fakes")]
        [Tooltip("Initial smoke opacity published to the LifePod fog overlay.")]
        [SerializeField, Range(0f, 1f)] private float initialSmoke01 = 1f;

        [Tooltip("Smoke reduction speed while the vent valve is open.")]
        [SerializeField, Min(0f)] private float smokePurgeRatePerSecond = 0.34f;

        [Tooltip("Extra smoke reduction speed driven by physical valve wheel angular motion.")]
        [SerializeField, Min(0f)] private float smokeManualTurnPurgeRatePerSecond = 0.22f;

        [Tooltip("Valve angular velocity that maps to full manual purge assist.")]
        [SerializeField, Min(1f)] private float fullManualVentAngularVelocityDegreesPerSecond = 120f;

        [Tooltip("Smoke value at or below which the pod counts as vented.")]
        [SerializeField, Range(0f, 1f)] private float ventedSmokeThreshold01 = 0.08f;

        [Tooltip("Screen-space foam mask fade speed after extinguisher input stops.")]
        [SerializeField, Min(0f)] private float foamFadeRatePerSecond = 0.42f;

        [Tooltip("Fake gravity vector used by pod interior shaders while the capsule lists.")]
        [SerializeField] private Vector3 fakePodGravityVector = new Vector3(0.22f, -0.94f, 0.18f);

        [Header("BIOS CRT")]
        [Tooltip("Monochrome CRT text sink. Updated via SetCharArray with the preallocated BIOS buffer.")]
        [SerializeField] private TMP_Text biosCrtText;

        [Tooltip("Minimum cadence for refreshing diagnostic CRT text.")]
        [SerializeField, Min(MinimumBiosRefreshSeconds)] private float biosRefreshSeconds = 0.12f;

        [Tooltip("Maximum AUP search radius for nearest scanned loot shown on the CRT.")]
        [SerializeField, Min(1f)] private float biosLootSearchRadius = 140f;

        [Tooltip("Extra radius padding added to the nearest loot AUP sphere diagnostic.")]
        [SerializeField, Min(0f)] private float biosLootRadiusPadding = 0.45f;

        private char[] _biosBuffer;
        private uint _stateBits;
        private float _smoke01;
        private float _foam01;
        private float2 _foamFlowDirection = new float2(0f, 1f);
        private float _visorVibration01;
        private float _visorVibrationHoldTimer;
        private float _cachedValveOpen01;
        private float _cachedValveAngular01;
        private float _biosRefreshTimer;
        private uint _biosLootCacheFrameCounter;
        private bool _cachedHasLootSphereAup;
        private bool _registeredTick;
        private bool _registeredLateFrame;
        private bool _registeredHotSwapListener;
        private bool _tickDormant;
        private uint _coldReferenceSearchMask;
        private IPlayerRuntimeContext _playerRuntime;
        private HectonPlayerMovement _cachedObserverMovement;
        private Vector4 _cachedLootSphereAup;
        private Vector4 _lastSmokeParams = Vector4.positiveInfinity;
        private Vector4 _lastPodGravityVector = Vector4.positiveInfinity;
        private Vector4 _lastVisorVibration = Vector4.positiveInfinity;
        private Vector4 _lastFoamParams = Vector4.positiveInfinity;
        private Vector4 _pendingSmokeParams;
        private Vector4 _pendingPodGravityVector;
        private Vector4 _pendingVisorVibration;
        private Vector4 _pendingFoamParams;
        private bool _smokeParamsDirty;
        private bool _podGravityVectorDirty;
        private bool _visorVibrationDirty;
        private bool _foamParamsDirty;
        private bool _coldStartFeedbackPending;
        private bool _biosDiagnosticDirty;
        private float3 _resolvedFakePodGravityVector = new float3(0f, -1f, 0f);
        private float _resolvedImpactSeverity01;
        private float _resolvedPcImpactShakeScale;
        private float _resolvedVrVisorVibrationIntensity;
        private float _resolvedVrVisorVibrationHoldSeconds;
        private float _resolvedVrVisorVibrationRecoverySpeed;
        private float _resolvedInitialSmoke01;
        private float _resolvedSmokePurgeRatePerSecond;
        private float _resolvedSmokeManualTurnPurgeRatePerSecond;
        private float _resolvedFullManualVentAngularVelocityDegreesPerSecond;
        private float _resolvedVentedSmokeThreshold01;
        private float _resolvedFoamFadeRatePerSecond;
        private float _resolvedBiosRefreshSeconds;
        private float _resolvedBiosLootSearchRadius;
        private float _resolvedBiosLootRadiusPadding;
        private List<LifePodSeatStrapLatch> _seatStrapLatches;

        /// <summary>
        /// Packed LifePod prologue state bits used by the crash and cold-start sequence.
        /// </summary>
        public uint StateBits => _stateBits;

        /// <summary>
        /// Current shader smoke intensity in normalized 0..1 space.
        /// </summary>
        public float Smoke01 => _smoke01;

        /// <summary>
        /// Current screen-space foam mask intensity in normalized 0..1 space.
        /// </summary>
        public float Foam01 => _foam01;

        /// <summary>
        /// True after straps, venting, and O2 scrubber insertion have completed.
        /// </summary>
        public bool PowerRestored => (_stateBits & StatePowerRestored) != 0u;

        private void Awake()
        {
            _biosBuffer = new char[BiosBufferCapacity]; // COLD ALLOC: char[512] — LifePod CRT diagnostic buffer — owner: LifePodTactilePrologueController
            EnsureSeatStrapLatchCache();
            CacheScalarConfig();
            _smoke01 = _resolvedInitialSmoke01;
            CacheFakePodGravityVector();
            ResolveColdReferences();
            RefreshColdRegistryReferences();
            RefreshValveTelemetryCache();
            RefreshBiosLootCache();
            PublishShaderState();
            FlushQueuedShaderState();
            WriteBiosDiagnostic();
        }

        private void OnEnable()
        {
            CacheScalarConfig();
            RefreshColdRegistryReferences();
            TryRegisterHotSwapListener();
            if (autoBeginOnEnable)
                BeginCrashSequence(impactSeed, _resolvedImpactSeverity01);
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            HectonBiosDiagnosticState.SetActive(false, 0f);
            if (damageSystem != null)
                damageSystem.ClearShortCircuits();

            _stateBits = 0u;
            _smoke01 = 0f;
            _foam01 = 0f;
            _visorVibration01 = 0f;
            _visorVibrationHoldTimer = 0f;
            _cachedValveOpen01 = 0f;
            _cachedValveAngular01 = 0f;
            _tickDormant = true;
            InvalidateColdReferenceCache();
            InvalidatePublishedShaderCache();
            PublishShaderState();
            FlushQueuedShaderState();
            TryUnregisterTick();
        }

        /// <summary>
        /// Starts the crash sequence with the serialized seed and impact severity.
        /// </summary>
        public void BeginCrashSequence()
        {
            CacheScalarConfig();
            BeginCrashSequence(impactSeed, _resolvedImpactSeverity01);
        }

        /// <summary>
        /// Starts the crash sequence with deterministic damage and feedback inputs.
        /// </summary>
        /// <param name="seed">Short-circuit random seed. Zero falls back to the default seed.</param>
        /// <param name="severity01">Normalized impact severity.</param>
        public void BeginCrashSequence(uint seed, float severity01)
        {
            ResolveColdReferences();
            CacheScalarConfig();
            ResetPhysicalStartState();
            RefreshValveTelemetryCache();
            _stateBits = StateCrashActive;
            _smoke01 = _resolvedInitialSmoke01;
            _foam01 = 0f;
            _foamFlowDirection = new float2(0f, 1f);
            _visorVibration01 = 0f;
            _visorVibrationHoldTimer = 0f;
            _biosRefreshTimer = 0f;
            InvalidateBiosLootCache();
            if (_smoke01 > _resolvedVentedSmokeThreshold01)
                _stateBits |= StateSmokeActive;

            HectonBiosDiagnosticState.SetActive(false, 0f);

            float safeSeverity = SaturateFinite01(severity01);
            if (damageSystem != null)
                damageSystem.TriggerWaterImpact(seed != 0u ? seed : DefaultImpactSeed, safeSeverity);

            TriggerImpactFeedback(safeSeverity);
            RefreshBiosLootCache();
            InvalidatePublishedShaderCache();
            PublishShaderState();
            WriteBiosDiagnostic();
            _tickDormant = false;
            TryRegisterTick();
        }

        /// <summary>
        /// Adds screen-space extinguisher foam to the visor/pod overlay without spawning particles.
        /// </summary>
        /// <param name="foamDelta01">Normalized foam contribution to add this frame/event.</param>
        public void ApplyExtinguisherFoam(float foamDelta01)
        {
            ApplyFoamDelta(foamDelta01);
        }

        /// <summary>
        /// Adds screen-space extinguisher foam with a normalized screen-flow direction.
        /// </summary>
        /// <param name="foamDelta01">Normalized foam contribution to add this frame/event.</param>
        /// <param name="screenFlowDirection">Approximate screen-space direction used by the visor foam shader.</param>
        public void ApplyExtinguisherFoam(float foamDelta01, Vector2 screenFlowDirection)
        {
            float delta = SaturateFinite01(foamDelta01);
            if (delta <= 0f)
                return;

            CacheFoamFlowDirection(screenFlowDirection);
            ApplyFoamDelta(delta);
        }

        /// <summary>
        /// Adds foam using a caller-owned cached/normalized screen-flow direction without repeating rsqrt work.
        /// </summary>
        /// <param name="foamDelta01">Normalized foam contribution to add this frame/event.</param>
        /// <param name="normalizedScreenFlowDirection">Approximate normalized screen-space flow direction.</param>
        public void ApplyExtinguisherFoamCachedFlow(float foamDelta01, float2 normalizedScreenFlowDirection)
        {
            float delta = SaturateFinite01(foamDelta01);
            if (delta <= 0f)
                return;

            CacheFoamFlowDirectionFast(normalizedScreenFlowDirection);
            ApplyFoamDelta(delta);
        }

        private void ApplyFoamDelta(float delta)
        {
            delta = SaturateFinite01(delta);
            if (delta <= 0f)
                return;

            _foam01 = math.saturate(_foam01 + delta);
            _stateBits |= StateFoamActive;
            PublishShaderState();
            _tickDormant = false;
            TryRegisterTick();
        }

        /// <summary>
        /// Allows dynamic prologue wiring to re-run cold hierarchy lookup once per dependency.
        /// </summary>
        public void InvalidateColdReferenceCache()
        {
            _coldReferenceSearchMask = 0u;
            if (_seatStrapLatches != null)
                _seatStrapLatches.Clear();
            InvalidateBiosLootCache();
        }

        private void InvalidateBiosLootCache()
        {
            _biosLootCacheFrameCounter = 0u;
            _cachedHasLootSphereAup = false;
            _cachedLootSphereAup = default;
            _cachedObserverMovement = null;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (_tickDormant)
                return;

            float dt = SanitizeAtLeast(deltaTime, 0f);
            RefreshValveTelemetryCache();
            RefreshStrapState();
            UpdateManualVenting(dt);
            UpdateScrubberState();
            UpdateFoam(dt);
            UpdateVisorVibration(dt);
            UpdateColdStartState();
            UpdateBiosLootCacheFrame();
            PublishShaderState();
            UpdateBios(dt);

            if (!NeedsActiveTick())
            {
                _tickDormant = true;
            }
        }

        public void LateFrameTick()
        {
            FlushQueuedShaderState();
            FlushQueuedColdStartFeedback();
            if (_biosDiagnosticDirty)
            {
                _biosDiagnosticDirty = false;
                WriteBiosDiagnostic();
            }
        }

        private void ResolveColdReferences()
        {
            EnsureSeatStrapLatchCache();
            if ((_coldReferenceSearchMask & ColdReferenceSearchAll) == ColdReferenceSearchAll)
                return;

            if ((_coldReferenceSearchMask & ColdReferenceSeatStrapCoordinator) == 0u)
            {
                if (seatStrapCoordinator == null)
                    seatStrapCoordinator = ComponentReferenceUtility.ResolveOwnedComponent<LifePodSeatStrapCoordinator>(transform);
                _coldReferenceSearchMask |= ColdReferenceSeatStrapCoordinator;
            }

            if ((_coldReferenceSearchMask & ColdReferenceDamageSystem) == 0u)
            {
                if (damageSystem == null)
                    damageSystem = ComponentReferenceUtility.ResolveOwnedComponent<LifePodDamageSystem>(transform);
                _coldReferenceSearchMask |= ColdReferenceDamageSystem;
            }

            if ((_coldReferenceSearchMask & ColdReferenceVentValve) == 0u)
            {
                if (ventValve == null)
                    ventValve = ComponentReferenceUtility.ResolveOwnedComponent<VRValveWheelHandle>(transform);
                _coldReferenceSearchMask |= ColdReferenceVentValve;
            }

            if ((_coldReferenceSearchMask & ColdReferenceScrubberSocket) == 0u)
            {
                if (o2ScrubberSocket == null)
                    o2ScrubberSocket = ComponentReferenceUtility.ResolveOwnedComponent<PhysicalBatteryCompartment>(transform);
                if (o2ScrubberSocket != null)
                    o2ScrubberSocket.RefreshBatteryToolCacheCold();
                _coldReferenceSearchMask |= ColdReferenceScrubberSocket;
            }

            if ((_coldReferenceSearchMask & ColdReferenceBiosText) == 0u)
            {
                if (biosCrtText == null)
                    biosCrtText = ComponentReferenceUtility.ResolveOwnedComponent<TMP_Text>(transform);
                _coldReferenceSearchMask |= ColdReferenceBiosText;
            }

            if ((_coldReferenceSearchMask & ColdReferenceSeatStrapLatches) == 0u)
            {
                if (_seatStrapLatches.Count == 0)
                    GetComponentsInChildren<LifePodSeatStrapLatch>(true, _seatStrapLatches);
                _coldReferenceSearchMask |= ColdReferenceSeatStrapLatches;
            }
        }

        private void EnsureSeatStrapLatchCache()
        {
            if (_seatStrapLatches != null)
                return;

            _seatStrapLatches = new List<LifePodSeatStrapLatch>(SeatStrapLatchColdCapacity); // COLD ALLOC: List<LifePodSeatStrapLatch>[8] — repeatable crash-start latch reset cache — owner: LifePodTactilePrologueController
        }

        private void ResetPhysicalStartState()
        {
            if (seatStrapCoordinator != null)
                seatStrapCoordinator.ResetLatchState();

            for (int i = 0; i < _seatStrapLatches.Count; i++)
            {
                LifePodSeatStrapLatch latch = _seatStrapLatches[i];
                if (latch != null)
                    latch.ResetLatchVisualState();
            }
        }

        private void RefreshStrapState()
        {
            if (seatStrapCoordinator != null && seatStrapCoordinator.IsSeatLockActive)
            {
                _stateBits |= StateStrapsLocked;
                return;
            }

            if ((_stateBits & StatePowerRestored) == 0u)
                _stateBits &= ~StateStrapsLocked;
        }

        private void UpdateManualVenting(float dt)
        {
            if ((_stateBits & StateSmokeActive) == 0u)
                return;

            float purgeRate = _cachedValveOpen01 * _resolvedSmokePurgeRatePerSecond +
                              _cachedValveAngular01 * _resolvedSmokeManualTurnPurgeRatePerSecond;
            if (purgeRate > 0f)
                _smoke01 = math.max(0f, _smoke01 - purgeRate * dt);

            if (_smoke01 <= _resolvedVentedSmokeThreshold01)
            {
                _smoke01 = 0f;
                _stateBits &= ~StateSmokeActive;
                _stateBits |= StateVented;
            }
        }

        private void UpdateScrubberState()
        {
            bool scrubberReady = o2ScrubberSocket != null &&
                o2ScrubberSocket.HasInstalledCell &&
                !o2ScrubberSocket.IsSnapInProgress;

            if (scrubberReady)
            {
                _stateBits |= StateScrubberInserted;
                return;
            }

            if ((_stateBits & StatePowerRestored) == 0u)
                _stateBits &= ~StateScrubberInserted;
        }

        private void UpdateFoam(float dt)
        {
            if ((_stateBits & StateFoamActive) == 0u)
                return;

            _foam01 = math.max(0f, _foam01 - _resolvedFoamFadeRatePerSecond * dt);
            if (_foam01 <= 0.001f)
            {
                _foam01 = 0f;
                _stateBits &= ~StateFoamActive;
            }
        }

        private void UpdateVisorVibration(float dt)
        {
            if (_visorVibrationHoldTimer > 0f)
            {
                _visorVibrationHoldTimer = math.max(0f, _visorVibrationHoldTimer - dt);
                return;
            }

            if (_visorVibration01 <= 0f)
                return;

            float decay = 1f - math.saturate(_resolvedVrVisorVibrationRecoverySpeed * dt);
            _visorVibration01 *= decay;
            if (_visorVibration01 <= 0.001f)
                _visorVibration01 = 0f;
        }

        private void UpdateColdStartState()
        {
            if ((_stateBits & StatePowerRestored) != 0u)
                return;

            if ((_stateBits & ColdStartReadyMask) != ColdStartReadyMask)
                return;

            _stateBits |= StatePowerRestored;
            _stateBits &= ~StateCrashActive;
            _coldStartFeedbackPending = true;
            _biosDiagnosticDirty = true;
        }

        private void TriggerImpactFeedback(float severity01)
        {
            if (severity01 <= 0f)
                return;

            bool xrActive = HectonXRRuntimeState.IsXRActive;
            if (xrActive)
            {
                _visorVibration01 = math.max(_visorVibration01, _resolvedVrVisorVibrationIntensity * severity01);
                _visorVibrationHoldTimer = math.max(_visorVibrationHoldTimer, _resolvedVrVisorVibrationHoldSeconds);
                IPlayerRuntimeContext playerContext = _playerRuntime;
                VisorHUDController visor = playerContext != null ? playerContext.VisorController : null;
                if (visor != null)
                {
                    visor.TriggerEnvironmentalDistortion(
                        _visorVibration01,
                        _resolvedVrVisorVibrationHoldSeconds,
                        _resolvedVrVisorVibrationRecoverySpeed);
                }
            }
            else
            {
                CameraJuiceSignals.TryPublishImpact(
                    severity01 * _resolvedPcImpactShakeScale,
                    transform.position,
                    -transform.forward,
                    CameraJuiceSignals.SharpKineticImpactProfileHash,
                    1.2f,
                    CameraJuiceSignals.CriticalPriority,
                    0f,
                    1.1f,
                    1.2f,
                    impactSeed != 0u ? impactSeed : DefaultImpactSeed);
            }

            ToolHapticsRuntime.TryEnqueueSinusoidalCommand(
                0.38f,
                0.72f,
                0.12f,
                ImpactHapticFrequencyHz,
                HapticPriorityCritical,
                BothMotorMask);
        }

        private void PublishShaderState()
        {
            PublishIfChanged(
                LifePodSmokeParamsId,
                new Vector4(_smoke01, _cachedValveOpen01, _cachedValveAngular01, (_stateBits & StateSmokeActive) != 0u ? 1f : 0f),
                ref _lastSmokeParams);

            float3 gravity = _resolvedFakePodGravityVector;
            PublishIfChanged(
                PodGravityVectorId,
                new Vector4(gravity.x, gravity.y, gravity.z, (_stateBits & StateCrashActive) != 0u ? 1f : 0f),
                ref _lastPodGravityVector);

            PublishIfChanged(
                LifePodVisorVibrationId,
                new Vector4(_visorVibration01, _resolvedVrVisorVibrationRecoverySpeed, 0f, 0f),
                ref _lastVisorVibration);

            PublishIfChanged(
                LifePodFoamParamsId,
                new Vector4(_foam01, (_stateBits & StateFoamActive) != 0u ? 1f : 0f, _foamFlowDirection.x, _foamFlowDirection.y),
                ref _lastFoamParams);
        }

        private void InvalidatePublishedShaderCache()
        {
            _lastSmokeParams = Vector4.positiveInfinity;
            _lastPodGravityVector = Vector4.positiveInfinity;
            _lastVisorVibration = Vector4.positiveInfinity;
            _lastFoamParams = Vector4.positiveInfinity;
        }

        private void CacheFoamFlowDirection(Vector2 screenFlowDirection)
        {
            float2 direction = new float2(screenFlowDirection.x, screenFlowDirection.y);
            float lengthSq = math.lengthsq(direction);
            if (lengthSq <= FoamFlowMinimumLengthSq || !math.all(math.isfinite(direction)))
                return;

            _foamFlowDirection = direction * math.rsqrt(lengthSq);
        }

        private void CacheFoamFlowDirectionFast(float2 normalizedDirection)
        {
            float lengthSq = math.lengthsq(normalizedDirection);
            if (lengthSq < FoamFlowTrustedMinimumLengthSq ||
                lengthSq > FoamFlowTrustedMaximumLengthSq ||
                !math.all(math.isfinite(normalizedDirection)))
            {
                return;
            }

            _foamFlowDirection = normalizedDirection;
        }

        private void CacheFakePodGravityVector()
        {
            float3 vector = new float3(fakePodGravityVector.x, fakePodGravityVector.y, fakePodGravityVector.z);
            float lengthSq = math.lengthsq(vector);
            if (lengthSq <= 0.000001f || !math.all(math.isfinite(vector)))
            {
                _resolvedFakePodGravityVector = new float3(0f, -1f, 0f);
                return;
            }

            if (math.abs(lengthSq - 1f) <= 0.0001f)
            {
                _resolvedFakePodGravityVector = vector;
                return;
            }

            _resolvedFakePodGravityVector = vector * math.rsqrt(lengthSq);
        }

        private void CacheScalarConfig()
        {
            _resolvedImpactSeverity01 = SaturateFinite01(impactSeverity01);
            _resolvedPcImpactShakeScale = SanitizeAtLeast(pcImpactShakeScale, 0f);
            _resolvedVrVisorVibrationIntensity = SaturateFinite01(vrVisorVibrationIntensity);
            _resolvedVrVisorVibrationHoldSeconds = SanitizeAtLeast(vrVisorVibrationHoldSeconds, 0.01f);
            _resolvedVrVisorVibrationRecoverySpeed = SanitizeAtLeast(vrVisorVibrationRecoverySpeed, 0.1f);
            _resolvedInitialSmoke01 = SaturateFinite01(initialSmoke01);
            _resolvedSmokePurgeRatePerSecond = SanitizeAtLeast(smokePurgeRatePerSecond, 0f);
            _resolvedSmokeManualTurnPurgeRatePerSecond = SanitizeAtLeast(smokeManualTurnPurgeRatePerSecond, 0f);
            _resolvedFullManualVentAngularVelocityDegreesPerSecond = SanitizeAtLeast(fullManualVentAngularVelocityDegreesPerSecond, 1f);
            _resolvedVentedSmokeThreshold01 = SaturateFinite01(ventedSmokeThreshold01);
            _resolvedFoamFadeRatePerSecond = SanitizeAtLeast(foamFadeRatePerSecond, 0f);
            _resolvedBiosRefreshSeconds = SanitizeAtLeast(biosRefreshSeconds, MinimumBiosRefreshSeconds);
            _resolvedBiosLootSearchRadius = SanitizeAtLeast(biosLootSearchRadius, 1f);
            _resolvedBiosLootRadiusPadding = SanitizeAtLeast(biosLootRadiusPadding, 0f);
        }

        private static float SaturateFinite01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float SanitizeAtLeast(float value, float minimum)
        {
            float resolved = math.isfinite(value) ? value : minimum;
            return math.max(minimum, resolved);
        }

        private void RefreshValveTelemetryCache()
        {
            if (ventValve == null)
            {
                _cachedValveOpen01 = 0f;
                _cachedValveAngular01 = 0f;
                return;
            }

            float fullSpeed = _resolvedFullManualVentAngularVelocityDegreesPerSecond;
            _cachedValveOpen01 = SaturateFinite01(ventValve.IsOpen01);
            float angularVelocity = math.isfinite(ventValve.AngularVelocityDegreesPerSecond)
                ? math.abs(ventValve.AngularVelocityDegreesPerSecond)
                : 0f;
            _cachedValveAngular01 = math.saturate(angularVelocity / fullSpeed);
        }

        private void UpdateBios(float dt)
        {
            if (biosCrtText == null)
                return;

            _biosRefreshTimer -= dt;
            if (_biosRefreshTimer > 0f)
                return;

            _biosRefreshTimer = _resolvedBiosRefreshSeconds;
            _biosDiagnosticDirty = true;
        }

        private void FlushQueuedColdStartFeedback()
        {
            if (!_coldStartFeedbackPending)
                return;

            _coldStartFeedbackPending = false;
            HectonBiosDiagnosticState.SetActive(true, 1f);
            ToolHapticsRuntime.TryEnqueueSinusoidalCommand(
                0.2f,
                0.46f,
                0.08f,
                ColdStartHapticFrequencyHz,
                HapticPriorityCritical,
                BothMotorMask);
        }

        private void UpdateBiosLootCacheFrame()
        {
            if (biosCrtText == null)
                return;

            if ((_biosLootCacheFrameCounter++ & BiosLootCacheFrameMask) != 0u)
                return;

            RefreshBiosLootCache();
        }

        private void RefreshBiosLootCache()
        {
            _cachedHasLootSphereAup = false;
            _cachedLootSphereAup = default;

            if (!TryResolveObserverAup(out AbsoluteUniversePosition observerAup))
                return;

            _cachedHasLootSphereAup = HectonScanRenderRegistry.TryFindNearestLootSphereAup(
                in observerAup,
                _resolvedBiosLootSearchRadius,
                _resolvedBiosLootRadiusPadding,
                out _cachedLootSphereAup);
        }

        private void WriteBiosDiagnostic()
        {
            if (biosCrtText == null || _biosBuffer == null)
                return;

            int cursor = 0;
            System.Span<char> buffer = _biosBuffer.AsSpan();
            Append(buffer, ref cursor, "SYSTEM DIAGNOSTIC".AsSpan());
            AppendNewLine(buffer, ref cursor);

            Append(buffer, ref cursor, "STRAPS ".AsSpan());
            AppendOkWait(buffer, ref cursor, (_stateBits & StateStrapsLocked) != 0u);
            Append(buffer, ref cursor, "  SMOKE ".AsSpan());
            AppendPercent(buffer, ref cursor, _smoke01);
            AppendNewLine(buffer, ref cursor);

            Append(buffer, ref cursor, "VENT ".AsSpan());
            AppendOkWait(buffer, ref cursor, (_stateBits & StateVented) != 0u);
            Append(buffer, ref cursor, "  O2 SCRUB ".AsSpan());
            AppendOkWait(buffer, ref cursor, (_stateBits & StateScrubberInserted) != 0u);
            AppendNewLine(buffer, ref cursor);

            Append(buffer, ref cursor, "POWER ".AsSpan());
            AppendOkWait(buffer, ref cursor, (_stateBits & StatePowerRestored) != 0u);
            Append(buffer, ref cursor, "  SHORT 0x".AsSpan());
            AppendHex4(buffer, ref cursor, damageSystem != null ? damageSystem.ShortCircuitMask : (ushort)0);
            AppendNewLine(buffer, ref cursor);

            WriteLootLine(buffer, ref cursor);
            biosCrtText.SetCharArray(_biosBuffer, 0, math.clamp(cursor, 0, _biosBuffer.Length));
        }

        private void WriteLootLine(System.Span<char> buffer, ref int cursor)
        {
            if (!_cachedHasLootSphereAup)
            {
                Append(buffer, ref cursor, "LOOT AUP NONE".AsSpan());
                return;
            }

            Append(buffer, ref cursor, "LOOT AUP ".AsSpan());
            AppendFloat(buffer, ref cursor, _cachedLootSphereAup.x);
            Append(buffer, ref cursor, ",".AsSpan());
            AppendFloat(buffer, ref cursor, _cachedLootSphereAup.y);
            Append(buffer, ref cursor, ",".AsSpan());
            AppendFloat(buffer, ref cursor, _cachedLootSphereAup.z);
            Append(buffer, ref cursor, " R".AsSpan());
            AppendFloat(buffer, ref cursor, _cachedLootSphereAup.w);
        }

        private bool TryResolveObserverAup(out AbsoluteUniversePosition observerAup)
        {
            if (_cachedObserverMovement == null)
            {
                IPlayerRuntimeContext playerContext = _playerRuntime;
                _cachedObserverMovement = playerContext != null ? playerContext.PlayerMovement : null;
            }

            if (_cachedObserverMovement != null)
            {
                observerAup = _cachedObserverMovement.PredictedAup;
                return true;
            }

            observerAup = default;
            return false;
        }

        private void TryRegisterTick()
        {
            if (!Application.isPlaying)
                return;

            _tickDormant = false;
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);

            if (!_registeredTick)
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterTick()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = false;
            }
        }

        private void RefreshColdRegistryReferences()
        {
            _playerRuntime = GlobalRegistry.Player;
            _cachedObserverMovement = null;
        }

        private void TryRegisterHotSwapListener()
        {
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntime = currentService as IPlayerRuntimeContext;
                    _cachedObserverMovement = null;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterTick();
                    if (currentService != null && NeedsActiveTick())
                    {
                        _tickDormant = false;
                        TryRegisterTick();
                    }
                    break;
            }
        }

        private bool NeedsActiveTick()
        {
            if ((_stateBits & (StateCrashActive | StateSmokeActive | StateFoamActive)) != 0u)
                return true;

            if (_visorVibration01 > 0.001f)
                return true;

            return (_stateBits & StatePowerRestored) != 0u && biosCrtText != null;
        }

        private void PublishIfChanged(int propertyId, Vector4 value, ref Vector4 previous)
        {
            if (Approximately(value, previous))
                return;

            previous = value;
            QueueShaderVector(propertyId, value);
        }

        private void QueueShaderVector(int propertyId, Vector4 value)
        {
            if (propertyId == LifePodSmokeParamsId)
            {
                _pendingSmokeParams = value;
                _smokeParamsDirty = true;
            }
            else if (propertyId == PodGravityVectorId)
            {
                _pendingPodGravityVector = value;
                _podGravityVectorDirty = true;
            }
            else if (propertyId == LifePodVisorVibrationId)
            {
                _pendingVisorVibration = value;
                _visorVibrationDirty = true;
            }
            else if (propertyId == LifePodFoamParamsId)
            {
                _pendingFoamParams = value;
                _foamParamsDirty = true;
            }
        }

        private void FlushQueuedShaderState()
        {
            if (_smokeParamsDirty)
            {
                _smokeParamsDirty = false;
                Shader.SetGlobalVector(LifePodSmokeParamsId, _pendingSmokeParams);
            }

            if (_podGravityVectorDirty)
            {
                _podGravityVectorDirty = false;
                Shader.SetGlobalVector(PodGravityVectorId, _pendingPodGravityVector);
            }

            if (_visorVibrationDirty)
            {
                _visorVibrationDirty = false;
                Shader.SetGlobalVector(LifePodVisorVibrationId, _pendingVisorVibration);
            }

            if (_foamParamsDirty)
            {
                _foamParamsDirty = false;
                Shader.SetGlobalVector(LifePodFoamParamsId, _pendingFoamParams);
            }
        }

        private static bool Approximately(Vector4 a, Vector4 b)
        {
            return math.abs(a.x - b.x) <= 0.0001f &&
                   math.abs(a.y - b.y) <= 0.0001f &&
                   math.abs(a.z - b.z) <= 0.0001f &&
                   math.abs(a.w - b.w) <= 0.0001f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheScalarConfig();
            CacheFakePodGravityVector();
            InvalidateColdReferenceCache();
        }
#endif

        private static void Append(System.Span<char> buffer, ref int cursor, System.ReadOnlySpan<char> value)
        {
            if ((uint)cursor >= (uint)buffer.Length || value.Length <= 0)
                return;

            int writable = buffer.Length - cursor;
            int copyLength = math.min(writable, value.Length);
            value.Slice(0, copyLength).CopyTo(buffer.Slice(cursor));
            cursor += copyLength;
        }

        private static void AppendNewLine(System.Span<char> buffer, ref int cursor)
        {
            if ((uint)cursor < (uint)buffer.Length)
                buffer[cursor++] = '\n';
        }

        private static void AppendOkWait(System.Span<char> buffer, ref int cursor, bool ok)
        {
            Append(buffer, ref cursor, ok ? "OK".AsSpan() : "WAIT".AsSpan());
        }

        private static void AppendPercent(System.Span<char> buffer, ref int cursor, float value01)
        {
            int percent = (int)math.round(SaturateFinite01(value01) * 100f);
            if ((uint)cursor < (uint)buffer.Length &&
                percent.TryFormat(buffer.Slice(cursor), out int written))
            {
                cursor += written;
            }

            Append(buffer, ref cursor, "%".AsSpan());
        }

        private static void AppendFloat(System.Span<char> buffer, ref int cursor, float value)
        {
            if ((uint)cursor >= (uint)buffer.Length)
                return;

            float safeValue = math.isfinite(value) ? value : 0f;
            if (safeValue.TryFormat(buffer.Slice(cursor), out int written, "F1".AsSpan()))
                cursor += written;
        }

        private static void AppendHex4(System.Span<char> buffer, ref int cursor, ushort value)
        {
            if ((uint)cursor >= (uint)buffer.Length)
                return;

            if (value.TryFormat(buffer.Slice(cursor), out int written, "X4".AsSpan()))
                cursor += written;
        }
    }
}
