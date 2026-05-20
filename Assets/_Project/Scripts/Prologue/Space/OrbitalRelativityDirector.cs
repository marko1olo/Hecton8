using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Tools;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Prologue.Space
{
    /// <summary>
    /// Space prologue relativity fake: capsule stays at origin; the universe moves.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8600)]
    public sealed class OrbitalRelativityDirector : MonoBehaviour, IOrbitalDirector, IUpdatable, IGlobalRegistryHotSwapListener, IDisposable
    {
        private const int TelemetryCapacity = 300;
        private const int ControlDrainLimit = 8;
        private const uint SourceHash = PrologueSignalSourceHashes.OrbitalRelativityDirector;
        private const uint PlasmaRoarHash = 0x504C415Au; // PLAZ
        private const uint NaNHash = 0x4E414E21u; // NAN!
        private const uint AbortHash = 0x41424F52u; // ABOR
        private const uint DomainClaimFailedHash = 0x444F4D21u; // DOM!
        private const uint DomainNotSpaceHash = 0x4E535043u; // NSPC
        private const uint ServiceClaimFailedHash = 0x4F524253u; // ORBS
        private const byte MathLodImpostor = 0;
        private const byte MathLodMesh = 1;
        private const byte MathLodHigh = 2;
        private const byte MathLodUltra = 3;
        private const string DumpFileName = "Dump_ORBITAL_MECHANICS_DIRECTOR.bin";
        private const float SplashdownFluidImpulseRadiusMeters = 50f;
        private const float SplashdownFluidImpulseLifetimeSeconds = 5f;
        private const float SplashdownFluidImpulseStrengthMetersPerSecond = 20f;
        private const SystemID OwnerSystemId = SystemID.CoreBridge;
        private const BufferID TelemetryRingBufferId = (BufferID)0x4F524241; // "ORBA"

        private static readonly int _planetDistanceId = Shader.PropertyToID("_H8OrbitalPlanetDistanceMeters");
        private static readonly int _fakeRadiusId = Shader.PropertyToID("_H8OrbitalFakeRadiusMeters");
        private static readonly int _universeSpeedId = Shader.PropertyToID("_H8OrbitalUniverseSpeed");
        private static readonly int _reentryHeatId = Shader.PropertyToID("_H8OrbitalReentryHeat");
        private static readonly int _cloudWhiteoutId = Shader.PropertyToID("_H8OrbitalCloudWhiteout");
        private static readonly int _leadingEdgeDotId = Shader.PropertyToID("_H8OrbitalLeadingEdgeDot");
        private static readonly int _mathLodId = Shader.PropertyToID("_H8OrbitalMathLod");

        [Header("Authority")]
        [SerializeField] private Transform capsuleAuthority;
        [SerializeField] private Rigidbody capsuleRigidbody;
        [SerializeField] private Vector3 capsuleLeadingEdgeLocalDirection = Vector3.down;
        [SerializeField] private bool claimSpaceDomainOnEnable = true;
        [SerializeField] private bool lockCapsuleTransform = true;
        [SerializeField] private bool consumeInput = true;
        [SerializeField] private bool emitSignals = true;

        [Header("Universe Presentation")]
        [SerializeField] private Transform universeRoot;
        [SerializeField] private Transform planetSphere;
        [SerializeField] private Transform planetImpostor;
        [SerializeField] private Transform cloudLayer;
        [SerializeField] private Transform starField;
        [SerializeField] private Renderer planetSphereRenderer;
        [SerializeField] private Renderer planetImpostorRenderer;
        [SerializeField] private Renderer cloudLayerRenderer;

        [Header("Relativity Fake")]
        [SerializeField] private float startDistanceMeters = 12000f;
        [SerializeField] private float handoffDistanceMeters = 2f;
        [SerializeField] private float meshSwapDistanceMeters = 2000f;
        [SerializeField] private float reentryStartDistanceMeters = 1000f;
        [SerializeField] private float cloudWhiteoutDistanceMeters = 100f;
        [SerializeField] private float passiveApproachSpeedMetersPerSecond = 320f;
        [SerializeField] private float thrustAccelerationMetersPerSecondSq = 1400f;
        [SerializeField] private float maxUniverseSpeedMetersPerSecond = 6200f;
        [SerializeField] private float planetSphereScaleMeters = 5000f;
        [SerializeField] private float fakePlanetRadiusMeters = 10000000f;

        [Header("Feedback")]
        [SerializeField] private float signalIntervalSeconds = 0.05f;
        [SerializeField] private float cameraJuiceIntervalSeconds = 0.08f;
        [SerializeField] private float audioIntervalSeconds = 0.12f;
        [SerializeField] private float hapticIntervalSeconds = 0.10f;

        private NativeArray<OrbitalTelemetryEntry> _telemetryRing;
        private VaultBufferHandle<OrbitalTelemetryEntry> _telemetryRingHandle;
        private int _telemetryCursor;
        private int _tickCount;
        private uint _sequence;
        private double3 _universeVelocity;
        private double _universeSpeedMetersPerSecond;
        private double _distanceMeters;
        private float _universeSpeed01;
        private float _reentryHeat01;
        private float _cloudWhiteout01;
        private float _leadingEdgeDot01;
        private float _signalTimer;
        private float _cameraJuiceTimer;
        private float _audioTimer;
        private float _hapticTimer;
        private byte _mathLod = byte.MaxValue;
        private bool _domainClaimed;
        private bool _registeredUpdate;
        private bool _registeredHotSwapListener;
        private bool _serviceRegistered;
        private bool _spaceDomainActive;
        private bool _handoffEmitted;
        private bool _telemetryDumped;
        private bool _domainExitHandled;
        private bool _aborted;
        private bool _velocityZeroForced;
        private Quaternion _capsuleLockedRotation = Quaternion.identity;
        private float3 _capsuleLeadingEdgeLocalNormalized = new float3(0f, -1f, 0f);
        private IDataVault _dataVault;
        private IInputService _inputService;
        private OrbitalDirectorSnapshot _snapshot;
        private AbsoluteUniversePosition _originAup;

        public int TickCount => _tickCount;
        public double3 UniverseVelocity => _universeVelocity;
        public double PlanetDistanceMeters => _distanceMeters;
        public bool ReentryArmed => _reentryHeat01 > 0.001f;

        public bool TryGetSnapshot(out OrbitalDirectorSnapshot snapshot)
        {
            snapshot = _snapshot;
            return _serviceRegistered;
        }

        public void SetInputEnabled(bool enabled)
        {
            consumeInput = enabled;
        }

        public void ForceZeroUniverseVelocity(byte reason)
        {
            consumeInput = false;
            _velocityZeroForced = true;
            _universeVelocity = double3.zero;
            CacheUniverseSpeed(0d);
            PublishSnapshot();
            RecordTelemetry();
            if (reason != 0)
                PublishTelemetryAnomaly(AbortHash, reason);
        }

        public void ForceAbortReentry(byte reason)
        {
            AbortReentry(AbortHash, reason);
        }

        private void OnEnable()
        {
            CacheColdReferences();
            _originAup = AbsoluteUniversePosition.FromRuntimePosition(Vector3.zero);
            ResetRuntimeState(applyPresentation: false);
            _spaceDomainActive = false;

            IOrbitalDirector existingDirector = GlobalRegistry.OrbitalDirector;
            if (existingDirector != null && !ReferenceEquals(existingDirector, this))
            {
                _aborted = true;
                PublishTelemetryAnomaly(ServiceClaimFailedHash, 2);
                return;
            }

            if (claimSpaceDomainOnEnable)
            {
                _domainClaimed = GlobalRegistry.TryClaimCurrentDomain(Domain.Space, this);
                if (!_domainClaimed)
                {
                    _aborted = true;
                    PublishTelemetryAnomaly(DomainClaimFailedHash, 2);
                    return;
                }

                _spaceDomainActive = true;
            }
            else if (GlobalRegistry.CurrentDomain != Domain.Space)
            {
                _aborted = true;
                PublishTelemetryAnomaly(DomainNotSpaceHash, 2);
                return;
            }
            else
            {
                _spaceDomainActive = true;
            }

            EnsureTelemetry();
            ApplyColdSceneConfiguration();
            ApplyPresentation();
            GlobalRegistry.RegisterOrbitalDirectorRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.OrbitalDirector, this);
            GlobalRegistry.TryGet(out _inputService);

            TryRegisterHotSwapListener();
            TryRegisterUpdateLane();
        }

        private void OnDisable()
        {
            bool hadAuthority = _domainClaimed || _serviceRegistered;

            ReleaseRuntimeAuthority();

            if (hadAuthority)
            {
                LockCapsuleAuthority();
                ClearShaderGlobals();
            }
        }

        private void ReleaseRuntimeAuthority()
        {
            if (_registeredUpdate)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredUpdate = false;
            }

            TryUnregisterHotSwapListener();

            if (_serviceRegistered)
            {
                GlobalRegistry.UnregisterOrbitalDirectorRuntime(this);
                _serviceRegistered = false;
            }

            if (_domainClaimed)
            {
                GlobalRegistry.ClearCurrentDomain(Domain.Space, this);
                _domainClaimed = false;
            }

            _spaceDomainActive = false;
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            _telemetryRing = default;
            _telemetryRingHandle = default;
            _telemetryCursor = 0;
        }

        public void Tick(float deltaTime)
        {
            if (!_spaceDomainActive)
            {
                HandleDomainExit();
                return;
            }

            _domainExitHandled = false;

            LockCapsuleAuthority();

            float dt = SanitizeDeltaTime(deltaTime);
            if (_aborted || dt <= 0f)
            {
                RecordTelemetry();
                return;
            }

            float thrust01 = ResolveThrust01();
            IntegrateUniverse(thrust01, dt);
            UpdateReentryState();

            if (!IsFinite(_universeVelocity) || !IsFinite(_distanceMeters))
            {
                AbortReentry(NaNHash, 1);
                return;
            }

            ApplyPresentation();
            EmitFeedback(dt);
            PublishSnapshot();
            RecordTelemetry();
            _tickCount++;
        }

        [ContextMenu("Run Orbital Math Smoke Check")]
        public void RunOrbitalMathSmokeCheck()
        {
            OrbitalApproachJobResult result = OrbitalApproachIntegrateJob.Integrate(
                new double3(0d, -passiveApproachSpeedMetersPerSecond, 0d),
                math.max(1d, startDistanceMeters),
                0.016666666666666666d);
            if (result.Flags != 0)
                PublishTelemetryAnomaly(NaNHash, 3);
        }

        private void CacheColdReferences()
        {
            if (capsuleAuthority == null)
                capsuleAuthority = transform;

            if (capsuleRigidbody == null && capsuleAuthority != null)
                capsuleAuthority.TryGetComponent(out capsuleRigidbody);

            if (capsuleAuthority != null)
            {
                _capsuleLockedRotation = capsuleAuthority.rotation;
                _capsuleLeadingEdgeLocalNormalized = ResolveCapsuleLeadingEdgeLocal();
            }

            if (planetSphereRenderer == null && planetSphere != null)
                planetSphere.TryGetComponent(out planetSphereRenderer);

            if (planetImpostorRenderer == null && planetImpostor != null)
                planetImpostor.TryGetComponent(out planetImpostorRenderer);

            if (cloudLayerRenderer == null && cloudLayer != null)
                cloudLayer.TryGetComponent(out cloudLayerRenderer);

            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
        }

        private void ApplyColdSceneConfiguration()
        {
            if (planetSphere != null)
                planetSphere.localScale = Vector3.one * math.max(1f, planetSphereScaleMeters);

            if (capsuleRigidbody != null)
            {
                capsuleRigidbody.isKinematic = true;
                capsuleRigidbody.useGravity = false;
                capsuleRigidbody.constraints = RigidbodyConstraints.FreezeAll;
            }
        }

        private float3 ResolveCapsuleLeadingEdgeLocal()
        {
            float3 local = capsuleLeadingEdgeLocalDirection;
            double magnitudeSq = math.max(0d, math.lengthsq(local));
            return magnitudeSq > 0.000001d ? local * (float)math.rsqrt(magnitudeSq) : new float3(0f, -1f, 0f);
        }

        private void TryRegisterUpdateLane()
        {
            if (_registeredUpdate || !_serviceRegistered || _aborted || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !_serviceRegistered || _aborted || !Application.isPlaying)
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

        private void EnsureTelemetry()
        {
            if (_telemetryRing.IsCreated)
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            _telemetryRingHandle = vault.GetBufferHandle<OrbitalTelemetryEntry>(
                TelemetryRingBufferId,
                TelemetryCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _telemetryRing = _telemetryRingHandle.Resolve(vault);
            if (!_telemetryRing.IsCreated || _telemetryRing.Length < TelemetryCapacity)
            {
                _telemetryRing = default;
                _telemetryRingHandle = default;
                _telemetryCursor = 0;
            }
        }

        private void ResetRuntimeState(bool applyPresentation)
        {
            double passiveSpeed = math.max(1d, passiveApproachSpeedMetersPerSecond);
            _distanceMeters = math.max(1d, startDistanceMeters);
            _universeVelocity = new double3(0d, -passiveSpeed, 0d);
            CacheUniverseSpeed(passiveSpeed);
            _reentryHeat01 = 0f;
            _cloudWhiteout01 = 0f;
            _leadingEdgeDot01 = 0f;
            _signalTimer = 0f;
            _cameraJuiceTimer = 0f;
            _audioTimer = 0f;
            _hapticTimer = 0f;
            _sequence = 0u;
            _handoffEmitted = false;
            _telemetryDumped = false;
            _domainExitHandled = false;
            _aborted = false;
            _velocityZeroForced = false;
            _mathLod = byte.MaxValue;
            PublishSnapshot();
            if (applyPresentation)
                ApplyPresentation();
        }

        private float ResolveThrust01()
        {
            float thrust = 0f;

            if (consumeInput && _inputService != null && _inputService.IsInitialized && _inputService.IsPlayerInputEnabled)
            {
                PlayerInputState state = _inputService.GetState();
                thrust = math.max(thrust, math.saturate(state.MoveDelta.y));
                thrust = math.max(thrust, math.saturate(state.VerticalDelta));

                if (state.HasAction(PlayerInputAction.Sprint))
                    thrust = math.max(thrust, 0.75f);

                if (state.HasAction(PlayerInputAction.Dash))
                    thrust = 1f;
            }

            int drained = 0;
            while (consumeInput && drained < ControlDrainLimit && GlobalSignals.TryDequeueControl(out ControlSignal control))
            {
                thrust = math.max(thrust, math.saturate(control.Move.y));
                drained++;
            }

            return thrust;
        }

        private void IntegrateUniverse(float thrust01, float dt)
        {
            if (_velocityZeroForced)
            {
                _universeVelocity = double3.zero;
                CacheUniverseSpeed(0d);
                return;
            }

            double targetPassiveSpeed = math.max(1d, passiveApproachSpeedMetersPerSecond);
            double maxSpeed = math.max(targetPassiveSpeed, maxUniverseSpeedMetersPerSecond);
            double thrustDelta = (double)thrust01 * math.max(0f, thrustAccelerationMetersPerSecondSq) * dt;

            _universeVelocity.y -= thrustDelta;

            double speed = LengthFast(_universeVelocity);
            if (speed < targetPassiveSpeed)
            {
                _universeVelocity = new double3(0d, -targetPassiveSpeed, 0d);
                speed = targetPassiveSpeed;
            }
            else if (speed > maxSpeed)
            {
                _universeVelocity *= maxSpeed * math.rcp(speed);
                speed = maxSpeed;
            }

            CacheUniverseSpeed(speed);

            _distanceMeters = math.max(0d, _distanceMeters + _universeVelocity.y * dt);
            if (_distanceMeters <= handoffDistanceMeters)
                _distanceMeters = 0d;
        }

        private void UpdateReentryState()
        {
            double speedSq = LengthSq(_universeVelocity);
            float distance = (float)math.min(_distanceMeters, float.MaxValue);
            float reentryRange = math.max(1f, reentryStartDistanceMeters);
            float whiteoutRange = math.max(1f, cloudWhiteoutDistanceMeters);
            float distanceHeat = 1f - math.saturate(distance * math.rcp(reentryRange));
            _reentryHeat01 = math.saturate(distanceHeat * (0.35f + _universeSpeed01 * 0.65f));
            _cloudWhiteout01 = 1f - math.saturate(distance * math.rcp(whiteoutRange));

            Vector3 localLeadingEdge = new Vector3(
                _capsuleLeadingEdgeLocalNormalized.x,
                _capsuleLeadingEdgeLocalNormalized.y,
                _capsuleLeadingEdgeLocalNormalized.z);
            Vector3 capsuleForward = capsuleAuthority != null
                ? capsuleAuthority.TransformDirection(localLeadingEdge)
                : Vector3.down;
            double3 forward = new double3(capsuleForward.x, capsuleForward.y, capsuleForward.z);
            double3 velocityNormal = speedSq > 0.00000001d ? _universeVelocity * math.rsqrt(speedSq) : new double3(0d, -1d, 0d);
            _leadingEdgeDot01 = math.saturate((float)math.abs(math.dot(velocityNormal, forward)));
        }

        private void ApplyPresentation()
        {
            float distance = (float)math.min(_distanceMeters, 200000f);
            Vector3 planetPosition = new Vector3(0f, -distance, 0f);

            if (universeRoot != null)
                universeRoot.localPosition = Vector3.zero;

            if (planetSphere != null)
                planetSphere.localPosition = planetPosition;

            if (planetImpostor != null)
                planetImpostor.localPosition = planetPosition;

            if (cloudLayer != null)
                cloudLayer.localPosition = Vector3.zero;

            if (starField != null)
                starField.localPosition = new Vector3(0f, distance * 0.00025f, 0f);

            byte lod = ResolveMathLod(distance);
            if (lod != _mathLod)
            {
                _mathLod = lod;
                bool useMesh = lod != MathLodImpostor;
                SetRendererEnabled(planetSphereRenderer, useMesh);
                SetRendererEnabled(planetImpostorRenderer, !useMesh);
            }

            SetRendererEnabled(cloudLayerRenderer, _cloudWhiteout01 > 0.001f);

            Shader.SetGlobalFloat(_planetDistanceId, distance);
            Shader.SetGlobalFloat(_fakeRadiusId, math.max(planetSphereScaleMeters, fakePlanetRadiusMeters));
            Shader.SetGlobalFloat(_universeSpeedId, (float)math.min(_universeSpeedMetersPerSecond, float.MaxValue));
            Shader.SetGlobalFloat(_reentryHeatId, _reentryHeat01);
            Shader.SetGlobalFloat(_cloudWhiteoutId, _cloudWhiteout01);
            Shader.SetGlobalFloat(_leadingEdgeDotId, _leadingEdgeDot01);
            Shader.SetGlobalFloat(_mathLodId, _mathLod);
        }

        private byte ResolveMathLod(float distance)
        {
            float quality01 = math.saturate(HomeostasisBrain.GlobalQualityWeight);
            float meshContinuity01 = math.smoothstep(0.12f, 0.45f, quality01);
            float highDetail01 = math.smoothstep(0.52f, 0.88f, quality01);
            if (distance > meshSwapDistanceMeters && meshContinuity01 < 0.5f)
                return MathLodImpostor;

            if (highDetail01 >= 0.92f)
                return MathLodUltra;

            if (highDetail01 >= 0.42f)
                return MathLodHigh;

            return MathLodMesh;
        }

        private void EmitFeedback(float dt)
        {
            if (!emitSignals)
                return;

            _signalTimer -= dt;
            _cameraJuiceTimer -= dt;
            _audioTimer -= dt;
            _hapticTimer -= dt;

            bool reentry = _reentryHeat01 > 0.001f;
            if (reentry && _signalTimer <= 0f)
            {
                _signalTimer = math.max(0.01f, signalIntervalSeconds);
                PublishReentrySignal();
            }

            if (reentry && _cameraJuiceTimer <= 0f)
            {
                _cameraJuiceTimer = math.max(0.02f, cameraJuiceIntervalSeconds);
                float turbulence01 = UniverseSpeed01() * math.saturate(0.25f + _reentryHeat01 * 0.75f);
                CameraJuiceSignals.PublishImpact(turbulence01, Vector3.zero, Vector3.down);
                StreamingTurbulenceSignal turbulence = default;
                turbulence.Intensity01 = turbulence01;
                turbulence.Debt01 = _reentryHeat01;
                turbulence.DurationSeconds = cameraJuiceIntervalSeconds;
                turbulence.Frame = unchecked((uint)Time.frameCount);
                turbulence.SourceHash = SourceHash;
                turbulence.Sequence = _sequence;
                GlobalSignals.Publish(in turbulence);
            }

            if (reentry && _audioTimer <= 0f)
            {
                _audioTimer = math.max(0.02f, audioIntervalSeconds);
                PublishPlasmaAudio();
            }

            if (reentry && _hapticTimer <= 0f)
            {
                _hapticTimer = math.max(0.02f, hapticIntervalSeconds);
                PublishHaptics();
            }

            if (!_handoffEmitted && _cloudWhiteout01 >= 0.98f)
            {
                _handoffEmitted = true;
                PublishPrologueComplete();
            }
        }

        private void PublishReentrySignal()
        {
            AtmosphericReentrySignal signal = default;
            signal.CapsuleAup = _originAup;
            signal.AltitudeMeters = (float)math.min(_distanceMeters, float.MaxValue);
            signal.UniverseVelocityMetersPerSecond = (float)math.min(_universeSpeedMetersPerSecond, float.MaxValue);
            signal.Heat01 = _reentryHeat01;
            signal.Sequence = unchecked((ushort)++_sequence);
            signal.Phase = _cloudWhiteout01 > 0.001f
                ? AtmosphericReentrySignal.PhaseWhiteout
                : AtmosphericReentrySignal.PhasePlasma;
            signal.Flags = AtmosphericReentrySignal.FlagAuthoritativeHeat;
            if (_cloudWhiteout01 > 0.001f)
                signal.Flags |= AtmosphericReentrySignal.FlagWhiteoutRequested;

            GlobalSignals.Publish(in signal);
        }

        private void PublishPlasmaAudio()
        {
            AcousticPingSignal signal = default;
            float speed01 = UniverseSpeed01();
            signal.PositionAup = _originAup;
            signal.RadiusMeters = math.lerp(80f, 520f, speed01);
            signal.Intensity01 = math.saturate(_reentryHeat01 * 0.55f + speed01 * 0.45f);
            signal.SourceId = PlasmaRoarHash;
            signal.Channel = AcousticPingSignal.ChannelActiveSonar;
            signal.Flags = AcousticPingSignal.FlagActiveSonar;
            GlobalSignals.Publish(in signal);
        }

        private void PublishHaptics()
        {
            float intensity = math.saturate(0.35f + _reentryHeat01 * 0.65f);
            HapticRequest signal = default;
            signal.Intensity01 = intensity;
            signal.DurationSeconds = math.max(0.05f, hapticIntervalSeconds * 1.5f);
            signal.Frequency01 = math.saturate(0.45f + _reentryHeat01 * 0.55f);
            signal.SourceHash = SourceHash;
            signal.Frame = unchecked((uint)Time.frameCount);
            signal.Channel = HapticRequest.ChannelVehicleCritical;
            GlobalSignals.Publish(in signal);

            ToolHapticsRuntime.EnqueueSinusoidalCommand(
                intensity,
                math.saturate(intensity * 1.2f),
                signal.DurationSeconds,
                math.lerp(18f, 38f, _reentryHeat01),
                3,
                3);
        }

        private void PublishPrologueComplete()
        {
            PrologueCompleteSignal signal = default;
            signal.CapsuleAup = _originAup;
            signal.Frame = unchecked((uint)Time.frameCount);
            signal.SourceHash = SourceHash;
            signal.Sequence = unchecked((ushort)_sequence);
            signal.WhiteoutHoldSeconds = math.max(0.1f, signalIntervalSeconds * 4f);
            signal.Flags = PrologueCompleteSignal.FlagForceWhiteout;
            signal.Phase = PrologueCompleteSignal.PhaseOceanHandoff;
            GlobalSignals.Publish(in signal);
            PublishSplashdownFluidImpulse();
        }

        private void PublishSplashdownFluidImpulse()
        {
            float3 direction = ResolveSplashdownImpulseDirection();
            FluidImpulseSignal impulse = default;
            impulse.PositionAup = _originAup;
            impulse.Vector = direction * SplashdownFluidImpulseStrengthMetersPerSecond;
            impulse.Radius = SplashdownFluidImpulseRadiusMeters;
            impulse.Lifetime = SplashdownFluidImpulseLifetimeSeconds;
            impulse.Frame = unchecked((uint)Time.frameCount);
            impulse.SourceHash = SourceHash;
            impulse.Flags = 2u;
            GlobalSignals.Publish(in impulse);
        }

        private float3 ResolveSplashdownImpulseDirection()
        {
            double speedSq = math.lengthsq(_universeVelocity);
            if (speedSq <= 0.00000001d)
                return new float3(0f, -0.85f, 0.35f);

            double invSpeed = math.rsqrt(speedSq);
            double3 normal = _universeVelocity * invSpeed;
            float3 direction = new float3((float)normal.x, (float)normal.y, (float)normal.z);
            direction.y += 0.25f;
            float directionSq = math.lengthsq(direction);
            return directionSq > 0.000001f ? direction * math.rsqrt(directionSq) : new float3(0f, -0.85f, 0.35f);
        }

        private void PublishTelemetryAnomaly(uint anomalyHash, byte severity)
        {
            TelemetryAnomalySignal signal = default;
            signal.SystemHash = SourceHash;
            signal.AnomalyHash = anomalyHash;
            signal.Scalar = IsFinite(_distanceMeters) ? (float)math.min(math.abs(_distanceMeters), float.MaxValue) : -1f;
            signal.Frame = unchecked((uint)Time.frameCount);
            signal.Severity = severity;
            GlobalSignals.Publish(in signal);
        }

        private void AbortReentry(uint anomalyHash, byte reason)
        {
            _aborted = true;
            consumeInput = false;
            PublishTelemetryAnomaly(anomalyHash, reason);
            RecordTelemetry();
            DumpTelemetry(reason);

            _universeVelocity = double3.zero;
            if (!IsFinite(_distanceMeters))
                _distanceMeters = 0d;
            CacheUniverseSpeed(0d);
            _reentryHeat01 = 0f;
            _cloudWhiteout01 = 0f;
            _leadingEdgeDot01 = 0f;
            PublishSnapshot();
            if (_domainClaimed || _serviceRegistered)
                ClearShaderGlobals();
        }

        private void HandleDomainExit()
        {
            if (_domainExitHandled || (!_domainClaimed && !_serviceRegistered))
                return;

            _domainExitHandled = true;
            _spaceDomainActive = false;
            consumeInput = false;

            if (!_handoffEmitted)
            {
                AbortReentry(DomainNotSpaceHash, 2);
                ReleaseRuntimeAuthority();
                LockCapsuleAuthority();
                return;
            }

            ClearShaderGlobals();
            PublishSnapshot();
            RecordTelemetry();
            ReleaseRuntimeAuthority();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryRegisterUpdateLane();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                _dataVault = currentService as IDataVault;
                _telemetryRing = default;
                _telemetryRingHandle = default;
                _telemetryCursor = 0;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Input)
                _inputService = currentService as IInputService;
        }

        private float UniverseSpeed01()
        {
            return _universeSpeed01;
        }

        private void CacheUniverseSpeed(double speedMetersPerSecond)
        {
            if (!IsFinite(speedMetersPerSecond))
            {
                _universeSpeedMetersPerSecond = 0d;
                _universeSpeed01 = 0f;
                return;
            }

            double speed = math.max(0d, speedMetersPerSecond);
            _universeSpeedMetersPerSecond = speed;
            _universeSpeed01 = math.saturate((float)(speed * math.rcp(math.max(1d, maxUniverseSpeedMetersPerSecond))));
        }

        private void PublishSnapshot()
        {
            _snapshot = new OrbitalDirectorSnapshot(
                _universeVelocity,
                _distanceMeters,
                _reentryHeat01,
                _cloudWhiteout01,
                _sequence,
                _mathLod == byte.MaxValue ? MathLodImpostor : _mathLod,
                _handoffEmitted ? (byte)1 : (byte)0);
        }

        private void RecordTelemetry()
        {
            if (!_telemetryRing.IsCreated)
                return;

            OrbitalTelemetryEntry entry = default;
            entry.UniverseVelocity = _universeVelocity;
            entry.PlanetDistanceMeters = _distanceMeters;
            entry.Frame = unchecked((uint)Time.frameCount);
            entry.StateHash = HashState(_universeVelocity, _distanceMeters, _reentryHeat01, _cloudWhiteout01);
            entry.ReentryHeat01 = _reentryHeat01;
            entry.CloudWhiteout01 = _cloudWhiteout01;
            entry.Sequence = unchecked((ushort)_sequence);
            entry.MathLod = _mathLod == byte.MaxValue ? MathLodImpostor : _mathLod;
            entry.Flags = (byte)((_handoffEmitted ? 1 : 0) | (_aborted ? 2 : 0));
            _telemetryRing[_telemetryCursor] = entry;
            _telemetryCursor = (_telemetryCursor + 1) % _telemetryRing.Length;
        }

        private void DumpTelemetry(byte reason)
        {
            if (_telemetryDumped || !_telemetryRing.IsCreated)
                return;

            _telemetryDumped = true;
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string folder = Path.Combine(projectRoot, "Docs", "AgentLogs");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, DumpFileName);

            using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))
            {
                writer.Write(SourceHash);
                writer.Write(reason);
                writer.Write(TelemetryCapacity);
                writer.Write(_telemetryCursor);
                for (int i = 0; i < _telemetryRing.Length; i++)
                {
                    int index = (_telemetryCursor + i) % _telemetryRing.Length;
                    OrbitalTelemetryEntry entry = _telemetryRing[index];
                    writer.Write(entry.Frame);
                    writer.Write(entry.StateHash);
                    writer.Write(entry.UniverseVelocity.x);
                    writer.Write(entry.UniverseVelocity.y);
                    writer.Write(entry.UniverseVelocity.z);
                    writer.Write(entry.PlanetDistanceMeters);
                    writer.Write(entry.ReentryHeat01);
                    writer.Write(entry.CloudWhiteout01);
                    writer.Write(entry.Sequence);
                    writer.Write(entry.MathLod);
                    writer.Write(entry.Flags);
                }
            }
        }

        private void LockCapsuleAuthority()
        {
            if (!lockCapsuleTransform)
                return;

            if (capsuleAuthority != null)
                capsuleAuthority.SetPositionAndRotation(Vector3.zero, _capsuleLockedRotation);

            if (capsuleRigidbody == null)
                return;

            capsuleRigidbody.position = Vector3.zero;
            capsuleRigidbody.rotation = _capsuleLockedRotation;
            capsuleRigidbody.linearVelocity = Vector3.zero;
            capsuleRigidbody.angularVelocity = Vector3.zero;
        }

        private static void SetRendererEnabled(Renderer renderer, bool enabled)
        {
            if (renderer != null && renderer.enabled != enabled)
                renderer.enabled = enabled;
        }

        private static void ClearShaderGlobals()
        {
            Shader.SetGlobalFloat(_planetDistanceId, 0f);
            Shader.SetGlobalFloat(_fakeRadiusId, 0f);
            Shader.SetGlobalFloat(_universeSpeedId, 0f);
            Shader.SetGlobalFloat(_reentryHeatId, 0f);
            Shader.SetGlobalFloat(_cloudWhiteoutId, 0f);
            Shader.SetGlobalFloat(_leadingEdgeDotId, 0f);
            Shader.SetGlobalFloat(_mathLodId, MathLodImpostor);
        }

        private static float SanitizeDeltaTime(float deltaTime)
        {
            if (!math.isfinite(deltaTime) || deltaTime <= 0f)
                return 0f;

            return math.min(deltaTime, 0.05f);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinite(double3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static double LengthSq(double3 value)
        {
            return math.max(0d, math.lengthsq(value));
        }

        private static double LengthFast(double3 value)
        {
            return LengthFastFromSq(LengthSq(value));
        }

        private static double LengthFastFromSq(double lengthSq)
        {
            return lengthSq > 0d ? lengthSq * math.rsqrt(lengthSq) : 0d;
        }

        private static uint HashState(double3 velocity, double distance, float heat, float whiteout)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = Fold(hash, BitConverter.DoubleToInt64Bits(velocity.x));
                hash = Fold(hash, BitConverter.DoubleToInt64Bits(velocity.y));
                hash = Fold(hash, BitConverter.DoubleToInt64Bits(velocity.z));
                hash = Fold(hash, BitConverter.DoubleToInt64Bits(distance));
                hash = Fold(hash, BitConverter.SingleToInt32Bits(heat));
                hash = Fold(hash, BitConverter.SingleToInt32Bits(whiteout));
                return hash;
            }
        }

        private static uint Fold(uint hash, long bits)
        {
            unchecked
            {
                hash ^= (uint)bits;
                hash *= 16777619u;
                hash ^= (uint)((ulong)bits >> 32);
                hash *= 16777619u;
                return hash;
            }
        }

        private void OnValidate()
        {
            startDistanceMeters = math.max(1f, startDistanceMeters);
            handoffDistanceMeters = math.max(0f, handoffDistanceMeters);
            meshSwapDistanceMeters = math.max(1f, meshSwapDistanceMeters);
            reentryStartDistanceMeters = math.max(1f, reentryStartDistanceMeters);
            cloudWhiteoutDistanceMeters = math.max(1f, cloudWhiteoutDistanceMeters);
            passiveApproachSpeedMetersPerSecond = math.max(1f, passiveApproachSpeedMetersPerSecond);
            maxUniverseSpeedMetersPerSecond = math.max(passiveApproachSpeedMetersPerSecond, maxUniverseSpeedMetersPerSecond);
            planetSphereScaleMeters = math.max(1f, planetSphereScaleMeters);
            fakePlanetRadiusMeters = math.max(planetSphereScaleMeters, fakePlanetRadiusMeters);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct OrbitalTelemetryEntry
    {
        [FieldOffset(0)]
        public double3 UniverseVelocity;
        [FieldOffset(24)]
        public double PlanetDistanceMeters;
        [FieldOffset(32)]
        public uint Frame;
        [FieldOffset(36)]
        public uint StateHash;
        [FieldOffset(40)]
        public float ReentryHeat01;
        [FieldOffset(44)]
        public float CloudWhiteout01;
        [FieldOffset(48)]
        public ushort Sequence;
        [FieldOffset(50)]
        public byte MathLod;
        [FieldOffset(51)]
        public byte Flags;
        [FieldOffset(52)]
        public uint _pad0;
        [FieldOffset(56)]
        public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct OrbitalApproachJobResult
    {
        [FieldOffset(0)]
        public double3 UniverseVelocity;
        [FieldOffset(24)]
        public double DistanceMeters;
        [FieldOffset(32)]
        public byte Flags;
        [FieldOffset(33)]
        public byte _pad0;
        [FieldOffset(34)]
        public ushort _pad1;
        [FieldOffset(36)]
        public uint _pad2;
        [FieldOffset(40)]
        public ulong _pad3;
        [FieldOffset(48)]
        public ulong _pad4;
        [FieldOffset(56)]
        public ulong _pad5;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct OrbitalApproachIntegrateJob : IJob
    {
        public double3 UniverseVelocity;
        public double DistanceMeters;
        public double DeltaTime;
        [WriteOnly, NoAlias]
        public NativeArray<OrbitalApproachJobResult> Result;

        public void Execute()
        {
            Result[0] = Integrate(UniverseVelocity, DistanceMeters, DeltaTime);
        }

        public static OrbitalApproachJobResult Integrate(double3 universeVelocity, double distanceMeters, double deltaTime)
        {
            double speedSq = math.max(0d, math.lengthsq(universeVelocity));
            double speed = speedSq > 0d ? speedSq * math.rsqrt(speedSq) : 0d;
            double integratedDistance = math.max(0d, distanceMeters + universeVelocity.y * math.max(0d, deltaTime));
            byte flags = (byte)((IsFinite(speed) && IsFinite(integratedDistance)) ? 0 : 1);
            return new OrbitalApproachJobResult
            {
                DistanceMeters = flags == 0 ? integratedDistance : 0d,
                UniverseVelocity = flags == 0 ? universeVelocity : double3.zero,
                Flags = flags
            };
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
