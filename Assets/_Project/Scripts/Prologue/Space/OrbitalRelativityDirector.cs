using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
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
    public sealed class OrbitalRelativityDirector : MonoBehaviour, IOrbitalDirector, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IDisposable
    {
        private int _signalPushDropCount;
        private const int TelemetryCapacity = 300;
        private const int OrbitalTelemetryEntrySizeBytes = 64;
        private const int NativeDtoAlignmentBytes = 8;
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
        private const int MathLodHysteresisFrames = 3;
        private const float MinimumEclipseLightFloor01 = 0.18f;
        private const float ShaderGlobalEpsilonSq = 0.00000001f;
        private const float CameraPressureAmplitudeScale = 0.54f;
        private const float CameraPressureTranslationGain = 0.22f;
        private const float CameraPressureRotationGain = 0.76f;
        private const float CameraPressureNormalPriorityThreshold = 0.28f;
        private const float CameraPressureHighPriorityThreshold = 0.72f;
        private const string DumpFileName = "Dump_ORBITAL_MECHANICS_DIRECTOR.bin";
        private const string DumpPayloadLabel = "orbitalMechanicsDirectorDumpPayload";
        private const SystemID OwnerSystemId = SystemID.CoreBridge;
        private const BufferID TelemetryRingBufferId = (BufferID)0x4F524241; // "ORBA"

        private static readonly int _planetDistanceId = Shader.PropertyToID("_H8OrbitalPlanetDistanceMeters");
        private static readonly int _fakeRadiusId = Shader.PropertyToID("_H8OrbitalFakeRadiusMeters");
        private static readonly int _universeSpeedId = Shader.PropertyToID("_H8OrbitalUniverseSpeed");
        private static readonly int _reentryHeatId = Shader.PropertyToID("_H8OrbitalReentryHeat");
        private static readonly int _cloudWhiteoutId = Shader.PropertyToID("_H8OrbitalCloudWhiteout");
        private static readonly int _leadingEdgeDotId = Shader.PropertyToID("_H8OrbitalLeadingEdgeDot");
        private static readonly int _mathLodId = Shader.PropertyToID("_H8OrbitalMathLod");
        private static readonly int _aegirSunDirectionId = Shader.PropertyToID("_H8AegirSunDirection");
        private static readonly int _aegirPlanetCenterRadiusId = Shader.PropertyToID("_H8AegirPlanetCenterRadius");
        private static readonly int _aegirRingPlaneInnerId = Shader.PropertyToID("_H8AegirRingPlaneInner");
        private static readonly int _aegirOrbitScalarsId = Shader.PropertyToID("_H8AegirOrbitScalars");
        private static readonly int _aegirFlowPhaseId = Shader.PropertyToID("_H8AegirFlowPhase");
        private static readonly int _aegirFlowPhaseValidId = Shader.PropertyToID("_H8AegirFlowPhaseValid");
        private static readonly int _globalQualityWeightId = Shader.PropertyToID("_H8GlobalQualityWeight");
        private static readonly int _legacySunDirectionId = Shader.PropertyToID("_SunDirection");
        private static readonly int _legacyAegirDirectionId = Shader.PropertyToID("_AegirDirection");
        private static readonly int _orbitalTelemetryEntryRuntimeSizeBytes = UnsafeUtility.SizeOf<OrbitalTelemetryEntry>();
        private static readonly bool _orbitalTelemetryLayoutValid =
            _orbitalTelemetryEntryRuntimeSizeBytes == OrbitalTelemetryEntrySizeBytes &&
            (_orbitalTelemetryEntryRuntimeSizeBytes & (NativeDtoAlignmentBytes - 1)) == 0;
        private static readonly float3 s_defaultSunDirection = new float3(-0.38f, -0.72f, 0.58f);
        private static readonly float3 s_defaultAegirDirection = new float3(-0.38f, -0.18f, 0.905f);
        private static readonly float3 s_defaultRingPlaneNormal = new float3(0.16f, 0.93f, 0.33f);

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
        [SerializeField] private Transform gasGiantBackdrop;
        [SerializeField] private Renderer planetSphereRenderer;
        [SerializeField] private Renderer planetImpostorRenderer;
        [SerializeField] private Renderer cloudLayerRenderer;
        [SerializeField] private Renderer gasGiantBackdropRenderer;

        [Header("Relativity Fake")]
        [SerializeField] private float startDistanceMeters = 260000f;
        [SerializeField] private float handoffDistanceMeters = 2f;
        [SerializeField] private float meshSwapDistanceMeters = 120000f;
        [SerializeField] private float reentryStartDistanceMeters = 70000f;
        [SerializeField] private float cloudWhiteoutDistanceMeters = 5000f;
        [SerializeField] private float passiveApproachSpeedMetersPerSecond = 300f;
        [SerializeField] private float thrustAccelerationMetersPerSecondSq = 1400f;
        [SerializeField] private float scriptedReentryBurnAccelerationMetersPerSecondSq = 260f;
        [SerializeField] private float maxUniverseSpeedMetersPerSecond = 6200f;
        [SerializeField] private float planetSphereScaleMeters = 50000f;
        [SerializeField] private float fakePlanetRadiusMeters = 10000000f;

        [Header("Orbital Window Fake")]
        [SerializeField] private float gasGiantBackdropScaleMeters = 110000f;

        [Header("Aegir Sky Shader")]
        [SerializeField, Range(0.05f, 0.65f)] private float aegirAngularRadius = 0.28f;
        [SerializeField, Range(0.05f, 0.85f)] private float aegirRingInnerRadius = 0.36f;
        [SerializeField, Range(0.1f, 1.35f)] private float aegirRingOuterRadius = 0.72f;
        [SerializeField, Range(0f, 1f)] private float aegirRingShadowStrength = 0.62f;
        [SerializeField, Range(0f, 2f)] private float aegirBandFlowSpeed = 0.075f;
        [SerializeField] private Vector3 fallbackAegirDirection = new Vector3(-0.38f, -0.18f, 0.905f);
        [SerializeField] private Vector3 fallbackRingPlaneNormal = new Vector3(0.16f, 0.93f, 0.33f);

        [Header("Orbit Key Light")]
        [SerializeField] private Light orbitKeyLight;
        [SerializeField] private float orbitKeyLightBaseIntensity = 5.5f;
        [SerializeField, Range(0f, 1f)] private float eclipseLightFadeFloor = 0.05f;
        [SerializeField, Range(0.05f, 8f)] private float eclipseFadeResponseHz = 1.8f;

        [Header("Feedback")]
        [SerializeField] private float signalIntervalSeconds = 0.05f;
        [SerializeField] private float cameraJuiceIntervalSeconds = 0.08f;
        [SerializeField] private float audioIntervalSeconds = 0.12f;
        [SerializeField] private float hapticIntervalSeconds = 0.10f;

        private VaultGenerationHandle<OrbitalTelemetryEntry> _telemetryRingHandle;
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
        private float _presentationDeltaTime;
        private byte _mathLod = byte.MaxValue;
        private byte _mathLodCandidate = byte.MaxValue;
        private int _mathLodCandidateFrames;
        private float _mathLodShader;
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
        private bool _registeredLateFrame;
        private bool _pendingOrbitalPresentation;
        private bool _pendingOrbitalShaderClear;
        private bool _pendingCapsuleAuthorityLock;
        private bool _pendingRuntimeAuthorityRelease;
        private bool _pendingPlasmaAudioSignalDirty;
        private AcousticPingSignal _pendingPlasmaAudioSignal;
        private bool _pendingHapticSignalDirty;
        private HapticRequest _pendingHapticSignal;
        private bool _pendingCameraPressureSignalDirty;
        private StreamingTurbulenceSignal _pendingCameraPressureSignal;
        private byte _pendingCameraPressurePriority;
        private Quaternion _capsuleLockedRotation = Quaternion.identity;
        private float3 _capsuleLeadingEdgeLocalNormalized = new float3(0f, -1f, 0f);
        private IDataVault _dataVault;
        private IInputService _inputService;
        private IPhysicsService _physicsService;
        private ICelestialRuntimeSnapshotReadModel _celestialSnapshotReadModel;
        private PresentationShaderGlobalsDTO _presentationShaderGlobals;
        private PresentationShaderGlobalsDTO _uploadedPresentationShaderGlobals;
        private CelestialParametersDTO _celestialParameters;
        private CelestialParametersDTO _uploadedCelestialParameters;
        private bool _presentationShaderGlobalsUploaded;
        private bool _celestialParametersUploaded;
        private bool _aegirFlowPhaseUploaded;
        private float _eclipseOcclusionSmoothed;
        private float _aegirFlowPhase;
        private float _uploadedAegirFlowPhase = -1f;
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

        public void ConfigureStandaloneOrbitPacing(
            float startDistance,
            float reentryStartDistance,
            float whiteoutDistance,
            float passiveSpeed,
            float scriptedBurnAcceleration,
            float maxSpeed)
        {
            startDistanceMeters = math.max(1000f, startDistance);
            reentryStartDistanceMeters = math.clamp(
                reentryStartDistance,
                1000f,
                math.max(1000f, startDistanceMeters - 1f));
            cloudWhiteoutDistanceMeters = math.clamp(
                whiteoutDistance,
                250f,
                math.max(250f, reentryStartDistanceMeters - 1f));
            passiveApproachSpeedMetersPerSecond = math.max(1f, passiveSpeed);
            scriptedReentryBurnAccelerationMetersPerSecondSq = math.max(0f, scriptedBurnAcceleration);
            maxUniverseSpeedMetersPerSecond = math.max(passiveApproachSpeedMetersPerSecond, maxSpeed);
            planetSphereScaleMeters = math.clamp(planetSphereScaleMeters, 1000f, startDistanceMeters * 0.42f);
            fakePlanetRadiusMeters = math.max(planetSphereScaleMeters, startDistanceMeters * 80f);
            gasGiantBackdropScaleMeters = math.max(gasGiantBackdropScaleMeters, startDistanceMeters * 1.15f);
            if (Application.isPlaying && isActiveAndEnabled)
                ResetRuntimeState(applyPresentation: true);
        }

        public void ConfigureSceneBindings(
            Transform capsuleTransform,
            Transform sphereTransform,
            Transform impostorTransform,
            Transform cloudsTransform,
            Renderer sphereRenderer,
            Renderer impostorRenderer,
            Renderer cloudsRenderer)
        {
            if (capsuleTransform != null)
                capsuleAuthority = capsuleTransform;
            if (sphereTransform != null)
                planetSphere = sphereTransform;
            if (impostorTransform != null)
                planetImpostor = impostorTransform;
            if (cloudsTransform != null)
                cloudLayer = cloudsTransform;
            if (sphereRenderer != null)
                planetSphereRenderer = sphereRenderer;
            if (impostorRenderer != null)
                planetImpostorRenderer = impostorRenderer;
            if (cloudsRenderer != null)
                cloudLayerRenderer = cloudsRenderer;
        }

        public void ConfigureAegirBackdrop(Transform backdropTransform, Renderer backdropRenderer)
        {
            if (backdropTransform != null)
                gasGiantBackdrop = backdropTransform;
            if (backdropRenderer != null)
                gasGiantBackdropRenderer = backdropRenderer;
        }

        public void ConfigureOrbitKeyLight(Light keyLight, float baseIntensity)
        {
            if (keyLight != null)
                orbitKeyLight = keyLight;
            orbitKeyLightBaseIntensity = math.max(0f, baseIntensity);
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
            _originAup = ResolveCurrentRuntimeOriginAup();
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
            QueueOrbitalPresentation();
            if (GlobalRegistry.Phase == GlobalRegistry.RegistryPhase.Ready)
                GlobalRegistry.ReplaceOrbitalDirectorRuntime(this);
            else
                GlobalRegistry.RegisterOrbitalDirectorRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.OrbitalDirector, this);
            _inputService = GlobalRegistry.Input;
            _physicsService = GlobalRegistry.Physics;

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
            UnregisterDispatcherLanes();

            _pendingOrbitalPresentation = false;
            _pendingOrbitalShaderClear = false;
            _pendingCapsuleAuthorityLock = false;
            _pendingRuntimeAuthorityRelease = false;
            _pendingPlasmaAudioSignalDirty = false;
            _pendingPlasmaAudioSignal = default;
            _pendingHapticSignalDirty = false;
            _pendingHapticSignal = default;
            _pendingCameraPressureSignalDirty = false;
            _pendingCameraPressureSignal = default;
            _pendingCameraPressurePriority = CameraJuiceSignals.LowPriority;

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
            ReleaseTelemetryBuffer();
            _telemetryCursor = 0;
        }

        private static AbsoluteUniversePosition ResolveCurrentRuntimeOriginAup()
        {
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            return originAup.IsFinite()
                ? originAup
                : AbsoluteUniversePosition.FromAbsolutePosition(double3.zero);
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            ReleaseTelemetryBuffer();
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

            QueueCapsuleAuthorityLock();

            float dt = SanitizeDeltaTime(deltaTime);
            _presentationDeltaTime = dt;
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

            QueueOrbitalPresentation();
            EmitFeedback(dt);
            PublishSnapshot();
            RecordTelemetry();
            _tickCount++;
        }

        public void LateFrameTick()
        {
            if (_pendingCapsuleAuthorityLock)
            {
                _pendingCapsuleAuthorityLock = false;
                LockCapsuleAuthority();
            }

            if (_pendingOrbitalShaderClear)
            {
                _pendingOrbitalShaderClear = false;
                _pendingOrbitalPresentation = false;
                ClearShaderGlobals();
            }
            else if (_pendingOrbitalPresentation)
            {
                _pendingOrbitalPresentation = false;
                ApplyPresentation();
            }

            FlushQueuedFeedbackSignals();

            if (_pendingRuntimeAuthorityRelease)
            {
                _pendingRuntimeAuthorityRelease = false;
                ReleaseRuntimeAuthority();
            }
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

            if (gasGiantBackdropRenderer == null && gasGiantBackdrop != null)
                gasGiantBackdrop.TryGetComponent(out gasGiantBackdropRenderer);

            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            _celestialSnapshotReadModel = GlobalRegistry.CelestialRuntimeSnapshotReadModel;
        }

        private void ApplyColdSceneConfiguration()
        {
            if (planetSphere != null)
                planetSphere.localScale = Vector3.one * math.max(1f, planetSphereScaleMeters);

            if (gasGiantBackdrop != null)
                gasGiantBackdrop.localScale = Vector3.one * math.max(planetSphereScaleMeters, gasGiantBackdropScaleMeters);

            SetRendererEnabled(gasGiantBackdropRenderer, false);

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
            if ((_registeredUpdate && _registeredLateFrame) || !_serviceRegistered || _aborted || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredUpdate)
                _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void UnregisterDispatcherLanes()
        {
            if (_registeredUpdate)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredUpdate = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }
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

        private bool EnsureTelemetry()
        {
            if (!_orbitalTelemetryLayoutValid)
            {
                ClearTelemetryDescriptor();
                return false;
            }

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (vault.IsCompactionFenceActive)
            {
                ClearTelemetryDescriptor();
                return false;
            }

            if (IsVaultHandleCreated(in _telemetryRingHandle) &&
                vault.TryReadOnlyHandle(in _telemetryRingHandle, out NativeArray<OrbitalTelemetryEntry>.ReadOnly currentRing) &&
                currentRing.IsCreated &&
                currentRing.Length >= TelemetryCapacity)
            {
                return true;
            }

            ClearTelemetryDescriptor();
            if (vault.TryGetGenerationHandle(
                    TelemetryRingBufferId,
                    out VaultGenerationHandle<OrbitalTelemetryEntry> existing) &&
                vault.TryReadOnlyHandle(in existing, out NativeArray<OrbitalTelemetryEntry>.ReadOnly existingRing) &&
                existingRing.IsCreated &&
                existingRing.Length >= TelemetryCapacity)
            {
                _telemetryRingHandle = existing;
                return true;
            }

            if (vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<OrbitalTelemetryEntry> acquired = vault.EnsureGenerationHandle<OrbitalTelemetryEntry>(
                TelemetryRingBufferId,
                TelemetryCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            if (!IsVaultHandleCreated(in acquired) ||
                !vault.TryReadOnlyHandle(in acquired, out NativeArray<OrbitalTelemetryEntry>.ReadOnly acquiredRing) ||
                !acquiredRing.IsCreated ||
                acquiredRing.Length < TelemetryCapacity)
            {
                _telemetryCursor = 0;
                return false;
            }

            _telemetryRingHandle = acquired;
            return true;
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
            _presentationDeltaTime = 0f;
            _pendingPlasmaAudioSignalDirty = false;
            _pendingPlasmaAudioSignal = default;
            _pendingHapticSignalDirty = false;
            _pendingHapticSignal = default;
            _pendingCameraPressureSignalDirty = false;
            _pendingCameraPressureSignal = default;
            _pendingCameraPressurePriority = CameraJuiceSignals.LowPriority;
            _sequence = 0u;
            _handoffEmitted = false;
            _telemetryDumped = false;
            _domainExitHandled = false;
            _aborted = false;
            _velocityZeroForced = false;
            _mathLod = byte.MaxValue;
            _mathLodCandidate = byte.MaxValue;
            _mathLodCandidateFrames = 0;
            _mathLodShader = 0f;
            _eclipseOcclusionSmoothed = 0f;
            _aegirFlowPhase = 0f;
            _presentationShaderGlobalsUploaded = false;
            _uploadedPresentationShaderGlobals = default;
            _celestialParametersUploaded = false;
            _uploadedCelestialParameters = default;
            _aegirFlowPhaseUploaded = false;
            _uploadedAegirFlowPhase = -1f;
            PublishSnapshot();
            if (applyPresentation)
                QueueOrbitalPresentation();
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
            while (consumeInput && drained < ControlDrainLimit && SignalBus<ControlSignal>.TryConsumeFrame(out ControlSignal control))
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
            double scriptedBurnDelta = (double)ResolveScriptedReentryBurn01() *
                                       math.max(0f, scriptedReentryBurnAccelerationMetersPerSecondSq) *
                                       dt;

            _universeVelocity.y -= thrustDelta + scriptedBurnDelta;

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

        private float ResolveScriptedReentryBurn01()
        {
            if (_velocityZeroForced)
                return 0f;

            float reentryRange = math.max(1f, reentryStartDistanceMeters);
            float distance = (float)math.min(math.max(0d, _distanceMeters), (double)float.MaxValue);
            return 1f - math.saturate(distance * math.rcp(reentryRange));
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
            Vector3 capsuleForward = lockCapsuleTransform
                ? _capsuleLockedRotation * localLeadingEdge
                : capsuleAuthority != null
                    ? capsuleAuthority.TransformDirection(localLeadingEdge)
                    : Vector3.down;
            double3 forward = new double3(capsuleForward.x, capsuleForward.y, capsuleForward.z);
            double3 velocityNormal = speedSq > 0.00000001d ? _universeVelocity * math.rsqrt(speedSq) : new double3(0d, -1d, 0d);
            _leadingEdgeDot01 = math.saturate((float)math.abs(math.dot(velocityNormal, forward)));
        }

        private void ApplyPresentation()
        {
            float distance = (float)math.min(_distanceMeters, math.max(200000f, startDistanceMeters));

            if (universeRoot != null)
                universeRoot.localPosition = Vector3.zero;

            byte lod = ResolveStableMathLod(distance);
            _mathLod = lod;
            _mathLodShader = ResolveContinuousMathLod(distance);

            SetRendererEnabled(planetSphereRenderer, false);
            SetRendererEnabled(planetImpostorRenderer, false);
            SetRendererEnabled(cloudLayerRenderer, false);
            SetRendererEnabled(gasGiantBackdropRenderer, false);

            BuildPresentationShaderGlobals(distance);
            BuildCelestialParameters();

            UploadPresentationShaderGlobalsIfDirty();
            UploadCelestialGlobalsIfDirty();
            UploadAegirFlowPhaseIfDirty();
            ApplyEclipseLighting(_celestialParameters.SunDirection.w);
        }

        private void BuildPresentationShaderGlobals(float distance)
        {
            _presentationShaderGlobals.Primary.x = distance;
            _presentationShaderGlobals.Primary.y = math.max(planetSphereScaleMeters, fakePlanetRadiusMeters);
            _presentationShaderGlobals.Primary.z = (float)math.min(_universeSpeedMetersPerSecond, float.MaxValue);
            _presentationShaderGlobals.Primary.w = _reentryHeat01;
            _presentationShaderGlobals.Secondary.x = _cloudWhiteout01;
            _presentationShaderGlobals.Secondary.y = _leadingEdgeDot01;
            _presentationShaderGlobals.Secondary.z = _mathLodShader;
            _presentationShaderGlobals.Secondary.w = 0f;
        }

        private void BuildCelestialParameters()
        {
            CelestialRuntimeSnapshot snapshot = default;
            ICelestialRuntimeSnapshotReadModel readModel = _celestialSnapshotReadModel;
            if (readModel != null)
                snapshot = readModel.RuntimeSnapshot;

            float3 fallbackAegir = default;
            fallbackAegir.x = fallbackAegirDirection.x;
            fallbackAegir.y = fallbackAegirDirection.y;
            fallbackAegir.z = fallbackAegirDirection.z;

            float3 fallbackRing = default;
            fallbackRing.x = fallbackRingPlaneNormal.x;
            fallbackRing.y = fallbackRingPlaneNormal.y;
            fallbackRing.z = fallbackRingPlaneNormal.z;

            float3 sunDirection = ResolveUnitDirection(snapshot.SunDirection, s_defaultSunDirection);
            float3 aegirDirection = ResolveUnitDirection(snapshot.GasGiantDirection, fallbackAegir);
            if (!IsFinite(aegirDirection))
                aegirDirection = ResolveUnitDirection(snapshot.GasGiantOffset, s_defaultAegirDirection);

            float3 ringNormal = ResolveUnitDirection(fallbackRing, s_defaultRingPlaneNormal);
            float centerDistance = 1f;
            float ringInner = math.clamp(aegirRingInnerRadius, aegirAngularRadius + 0.02f, aegirRingOuterRadius - 0.01f);
            float ringOuter = math.max(ringInner + 0.01f, aegirRingOuterRadius);
            float quality = ResolveQuality01();
            float flowSpeed = math.max(0f, aegirBandFlowSpeed);
            _aegirFlowPhase = ResolveAegirFlowPhase(_aegirFlowPhase, flowSpeed, quality, _presentationDeltaTime);

            _celestialParameters.SunDirection.x = sunDirection.x;
            _celestialParameters.SunDirection.y = sunDirection.y;
            _celestialParameters.SunDirection.z = sunDirection.z;
            _celestialParameters.SunDirection.w = math.saturate(snapshot.EclipseOcclusion01);
            _celestialParameters.PlanetCenterRadius.x = aegirDirection.x * centerDistance;
            _celestialParameters.PlanetCenterRadius.y = aegirDirection.y * centerDistance;
            _celestialParameters.PlanetCenterRadius.z = aegirDirection.z * centerDistance;
            _celestialParameters.PlanetCenterRadius.w = math.clamp(aegirAngularRadius, 0.05f, 0.65f);
            _celestialParameters.RingPlaneInner.x = ringNormal.x;
            _celestialParameters.RingPlaneInner.y = ringNormal.y;
            _celestialParameters.RingPlaneInner.z = ringNormal.z;
            _celestialParameters.RingPlaneInner.w = ringInner;
            _celestialParameters.OrbitScalars.x = ringOuter;
            _celestialParameters.OrbitScalars.y = math.saturate(aegirRingShadowStrength);
            _celestialParameters.OrbitScalars.z = flowSpeed;
            _celestialParameters.OrbitScalars.w = quality;
        }

        private void UploadPresentationShaderGlobalsIfDirty()
        {
            if (_presentationShaderGlobalsUploaded && !PresentationShaderGlobalsChanged(_uploadedPresentationShaderGlobals, _presentationShaderGlobals))
                return;

            Shader.SetGlobalFloat(_planetDistanceId, _presentationShaderGlobals.Primary.x);
            Shader.SetGlobalFloat(_fakeRadiusId, _presentationShaderGlobals.Primary.y);
            Shader.SetGlobalFloat(_universeSpeedId, _presentationShaderGlobals.Primary.z);
            Shader.SetGlobalFloat(_reentryHeatId, _presentationShaderGlobals.Primary.w);
            Shader.SetGlobalFloat(_cloudWhiteoutId, _presentationShaderGlobals.Secondary.x);
            Shader.SetGlobalFloat(_leadingEdgeDotId, _presentationShaderGlobals.Secondary.y);
            Shader.SetGlobalFloat(_mathLodId, _presentationShaderGlobals.Secondary.z);

            _uploadedPresentationShaderGlobals = _presentationShaderGlobals;
            _presentationShaderGlobalsUploaded = true;
        }

        private void UploadCelestialGlobalsIfDirty()
        {
            if (_celestialParametersUploaded && !CelestialParametersChanged(_uploadedCelestialParameters, _celestialParameters))
                return;

            Shader.SetGlobalVector(_aegirSunDirectionId, _celestialParameters.SunDirection);
            Shader.SetGlobalVector(_aegirPlanetCenterRadiusId, _celestialParameters.PlanetCenterRadius);
            Shader.SetGlobalVector(_aegirRingPlaneInnerId, _celestialParameters.RingPlaneInner);
            Shader.SetGlobalVector(_aegirOrbitScalarsId, _celestialParameters.OrbitScalars);
            Shader.SetGlobalFloat(_globalQualityWeightId, _celestialParameters.OrbitScalars.w);
            Shader.SetGlobalVector(_legacySunDirectionId, _celestialParameters.SunDirection);
            Shader.SetGlobalVector(_legacyAegirDirectionId, _celestialParameters.PlanetCenterRadius);

            _uploadedCelestialParameters = _celestialParameters;
            _celestialParametersUploaded = true;
        }

        private void UploadAegirFlowPhaseIfDirty()
        {
            if (_aegirFlowPhaseUploaded && math.abs(_uploadedAegirFlowPhase - _aegirFlowPhase) <= 0.000001f)
                return;

            Shader.SetGlobalFloat(_aegirFlowPhaseId, _aegirFlowPhase);
            Shader.SetGlobalFloat(_aegirFlowPhaseValidId, 1f);
            _uploadedAegirFlowPhase = _aegirFlowPhase;
            _aegirFlowPhaseUploaded = true;
        }

        private static bool PresentationShaderGlobalsChanged(PresentationShaderGlobalsDTO lhs, PresentationShaderGlobalsDTO rhs)
        {
            return Vector4DeltaSq(lhs.Primary, rhs.Primary) > ShaderGlobalEpsilonSq ||
                   Vector4DeltaSq(lhs.Secondary, rhs.Secondary) > ShaderGlobalEpsilonSq;
        }

        private static bool CelestialParametersChanged(CelestialParametersDTO lhs, CelestialParametersDTO rhs)
        {
            return Vector4DeltaSq(lhs.SunDirection, rhs.SunDirection) > ShaderGlobalEpsilonSq ||
                   Vector4DeltaSq(lhs.PlanetCenterRadius, rhs.PlanetCenterRadius) > ShaderGlobalEpsilonSq ||
                   Vector4DeltaSq(lhs.RingPlaneInner, rhs.RingPlaneInner) > ShaderGlobalEpsilonSq ||
                   Vector4DeltaSq(lhs.OrbitScalars, rhs.OrbitScalars) > ShaderGlobalEpsilonSq;
        }

        private static float Vector4DeltaSq(Vector4 lhs, Vector4 rhs)
        {
            float dx = lhs.x - rhs.x;
            float dy = lhs.y - rhs.y;
            float dz = lhs.z - rhs.z;
            float dw = lhs.w - rhs.w;
            return dx * dx + dy * dy + dz * dz + dw * dw;
        }

        private static float3 ResolveUnitDirection(float3 candidate, float3 fallback)
        {
            if (IsFinite(candidate))
            {
                float candidateLengthSq = math.lengthsq(candidate);
                if (candidateLengthSq > 0.000001f)
                    return candidate * math.rsqrt(candidateLengthSq);
            }

            float fallbackLengthSq = math.lengthsq(fallback);
            return fallbackLengthSq > 0.000001f
                ? fallback * math.rsqrt(fallbackLengthSq)
                : s_defaultAegirDirection;
        }

        private void ApplyEclipseLighting(float eclipseOcclusion01)
        {
            if (orbitKeyLight == null)
                return;

            float targetOcclusion = math.saturate(eclipseOcclusion01);
            float response = math.max(0.05f, eclipseFadeResponseHz);
            float step = math.saturate(_presentationDeltaTime * response);
            _eclipseOcclusionSmoothed = math.lerp(_eclipseOcclusionSmoothed, targetOcclusion, step);

            float baseIntensity = math.max(0f, orbitKeyLightBaseIntensity);
            float floor = math.max(MinimumEclipseLightFloor01, math.saturate(eclipseLightFadeFloor));
            float intensity = baseIntensity * math.lerp(1f, floor, _eclipseOcclusionSmoothed);
            if (math.isfinite(intensity))
                orbitKeyLight.intensity = intensity;
        }

        private byte ResolveStableMathLod(float distance)
        {
            byte requested = ResolveMathLod(distance);
            if (_mathLod == byte.MaxValue)
            {
                _mathLodCandidate = requested;
                _mathLodCandidateFrames = 0;
                return requested;
            }

            if (requested == _mathLod)
            {
                _mathLodCandidate = requested;
                _mathLodCandidateFrames = 0;
                return _mathLod;
            }

            if (requested != _mathLodCandidate)
            {
                _mathLodCandidate = requested;
                _mathLodCandidateFrames = 1;
                return _mathLod;
            }

            if (_mathLodCandidateFrames < MathLodHysteresisFrames)
                _mathLodCandidateFrames++;

            return _mathLodCandidateFrames >= MathLodHysteresisFrames
                ? requested
                : _mathLod;
        }

        private byte ResolveMathLod(float distance)
        {
            float quality01 = ResolveQuality01();
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

        private float ResolveContinuousMathLod(float distance)
        {
            float quality01 = ResolveQuality01();
            float meshContinuity01 = math.smoothstep(0.12f, 0.45f, quality01);
            float highDetail01 = math.smoothstep(0.52f, 0.88f, quality01);
            float ultraDetail01 = math.smoothstep(0.86f, 1f, quality01);
            float swapRange = math.max(1f, meshSwapDistanceMeters * 0.35f);
            float distanceMesh01 = 1f - math.saturate((distance - meshSwapDistanceMeters) * math.rcp(swapRange));
            float baseMesh01 = math.max(meshContinuity01, distanceMesh01);
            return math.clamp(baseMesh01 + highDetail01 + ultraDetail01, 0f, 3f);
        }

        private static float ResolveQuality01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }

        private static float ResolveAegirFlowPhase(float currentPhase, float flowSpeed, float quality01, float deltaTime)
        {
            float safePhase = math.isfinite(currentPhase) ? currentPhase : 0f;
            float safeDelta = math.max(0f, math.select(0f, deltaTime, math.isfinite(deltaTime)));
            float cadence = math.max(0f, flowSpeed) * math.lerp(0.35f, 1.25f, math.saturate(quality01));
            return math.frac(safePhase + safeDelta * cadence);
        }

        private static float TriangleWaveSigned(float phase)
        {
            float t = math.frac(phase);
            return 1f - (4f * math.abs(t - 0.5f));
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
                byte cameraPriority = turbulence01 >= CameraPressureHighPriorityThreshold
                    ? CameraJuiceSignals.HighPriority
                    : (turbulence01 >= CameraPressureNormalPriorityThreshold ? CameraJuiceSignals.NormalPriority : CameraJuiceSignals.LowPriority);
                QueueCameraPressureFeedback(turbulence01, _reentryHeat01, cameraJuiceIntervalSeconds, cameraPriority);
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

            SignalBus<AtmosphericReentrySignal>.TryPushTracked(in signal, ref _signalPushDropCount);
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
            _pendingPlasmaAudioSignal = signal;
            _pendingPlasmaAudioSignalDirty = true;
        }

        private void PublishHaptics()
        {
            float intensity = math.saturate(0.35f + _reentryHeat01 * 0.65f);
            HapticRequest signal = default;
            signal.Intensity01 = intensity;
            signal.DurationSeconds = math.max(0.05f, hapticIntervalSeconds * 1.5f);
            signal.Frequency01 = math.saturate(0.45f + _reentryHeat01 * 0.55f);
            signal.SourceHash = SourceHash;
            signal.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            signal.Channel = HapticRequest.ChannelVehicleCritical;
            _pendingHapticSignal = signal;
            _pendingHapticSignalDirty = true;
        }

        private void QueueCameraPressureFeedback(
            float intensity01,
            float debt01,
            float durationSeconds,
            byte priority)
        {
            StreamingTurbulenceSignal signal = default;
            signal.Intensity01 = math.saturate(math.select(0f, intensity01, math.isfinite(intensity01)));
            signal.Debt01 = math.saturate(math.select(0f, debt01, math.isfinite(debt01)));
            signal.DurationSeconds = math.max(0.02f, math.select(0.02f, durationSeconds, math.isfinite(durationSeconds)));
            signal.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            signal.SourceHash = SourceHash;
            signal.Sequence = _sequence;
            _pendingCameraPressureSignal = signal;
            _pendingCameraPressurePriority = priority;
            _pendingCameraPressureSignalDirty = true;
        }

        private void FlushQueuedFeedbackSignals()
        {
            if (_pendingCameraPressureSignalDirty)
            {
                _pendingCameraPressureSignalDirty = false;
                StreamingTurbulenceSignal signal = _pendingCameraPressureSignal;
                CameraJuiceSignals.TryPublishImpact(
                    signal.Intensity01,
                    Vector3.zero,
                    Vector3.down,
                    CameraJuiceSignals.ContinuousPressureStressProfileHash,
                    CameraPressureAmplitudeScale,
                    _pendingCameraPressurePriority,
                    0f,
                    CameraPressureTranslationGain,
                    CameraPressureRotationGain,
                    SourceHash);
                SignalBus<StreamingTurbulenceSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
                _pendingCameraPressureSignal = default;
                _pendingCameraPressurePriority = CameraJuiceSignals.LowPriority;
            }

            if (_pendingPlasmaAudioSignalDirty)
            {
                _pendingPlasmaAudioSignalDirty = false;
                SignalBus<AcousticPingSignal>.TryPushTracked(in _pendingPlasmaAudioSignal, ref _signalPushDropCount);
                _pendingPlasmaAudioSignal = default;
            }

            if (_pendingHapticSignalDirty)
            {
                _pendingHapticSignalDirty = false;
                SignalBus<HapticRequest>.TryPushTracked(in _pendingHapticSignal, ref _signalPushDropCount);
                _pendingHapticSignal = default;
            }
        }

        private void PublishPrologueComplete()
        {
            PrologueCompleteSignal signal = default;
            signal.CapsuleAup = _originAup;
            signal.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            signal.SourceHash = SourceHash;
            signal.Sequence = unchecked((ushort)_sequence);
            signal.WhiteoutHoldSeconds = math.max(0.1f, signalIntervalSeconds * 4f);
            signal.Flags = PrologueCompleteSignal.FlagForceWhiteout;
            signal.Phase = PrologueCompleteSignal.PhaseWhiteout;
            SignalBus<PrologueCompleteSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        private void PublishTelemetryAnomaly(uint anomalyHash, byte severity)
        {
            TelemetryAnomalySignal signal = default;
            signal.SystemHash = SourceHash;
            signal.AnomalyHash = anomalyHash;
            signal.Scalar = IsFinite(_distanceMeters) ? (float)math.min(math.abs(_distanceMeters), float.MaxValue) : -1f;
            signal.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            signal.Severity = severity;
            SignalBus<TelemetryAnomalySignal>.TryPushTracked(in signal, ref _signalPushDropCount);
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
                QueueShaderGlobalClear();
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
                QueueCapsuleAuthorityLock();
                QueueRuntimeAuthorityRelease();
                return;
            }

            QueueShaderGlobalClear();
            PublishSnapshot();
            RecordTelemetry();
            QueueRuntimeAuthorityRelease();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                if (ReferenceEquals(previousService, currentService))
                    return;

                UnregisterDispatcherLanes();
                if (currentService != null && isActiveAndEnabled)
                    TryRegisterUpdateLane();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                ReleaseVaultBuffer(previousService as IDataVault ?? _dataVault, ref _telemetryRingHandle);
                _dataVault = currentService as IDataVault;
                _telemetryCursor = 0;
                EnsureTelemetry();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Input)
                _inputService = currentService as IInputService;

            if (serviceSlot == GlobalRegistryServiceSlot.Physics)
                _physicsService = currentService as IPhysicsService;

            if (serviceSlot == GlobalRegistryServiceSlot.CelestialEngineRuntime)
                _celestialSnapshotReadModel = currentService != null ? GlobalRegistry.CelestialRuntimeSnapshotReadModel : null;
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
            if (!_orbitalTelemetryLayoutValid)
                return;

            IDataVault vault = _dataVault;
            byte mathLod = _mathLod == byte.MaxValue ? MathLodImpostor : _mathLod;
            OrbitalTelemetryEntry entry = default;
            entry.UniverseVelocity = _universeVelocity;
            entry.PlanetDistanceMeters = _distanceMeters;
            entry.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            entry.StateHash = HashState(_universeVelocity, _distanceMeters, _reentryHeat01, _cloudWhiteout01);
            entry.ReentryHeat01 = _reentryHeat01;
            entry.CloudWhiteout01 = _cloudWhiteout01;
            entry.Sequence = unchecked((ushort)_sequence);
            entry.MathLod = mathLod;
            entry.Flags = (byte)((_handoffEmitted ? 1 : 0) | (_aborted ? 2 : 0));
            int telemetryIndex = math.clamp(_telemetryCursor, 0, TelemetryCapacity - 1);
            int nextTelemetryCursor = (telemetryIndex + 1) % TelemetryCapacity;
            if (vault == null ||
                !IsVaultHandleCreated(in _telemetryRingHandle) ||
                !vault.TryAcquireWriteLock(in _telemetryRingHandle, OwnerSystemId, out NativeArray<OrbitalTelemetryEntry> telemetryRing))
            {
                return;
            }

            try
            {
                if (!telemetryRing.IsCreated || telemetryRing.Length < TelemetryCapacity)
                    return;

                telemetryRing[telemetryIndex] = entry;
                _telemetryCursor = nextTelemetryCursor;
            }
            finally
            {
                vault.ReleaseWriteLock(in _telemetryRingHandle, OwnerSystemId);
            }
        }

        private void DumpTelemetry(byte reason)
        {
            if (!_orbitalTelemetryLayoutValid ||
                _telemetryDumped ||
                !TryReadTelemetryRing(out NativeArray<OrbitalTelemetryEntry>.ReadOnly telemetryRing))
            {
                return;
            }

            NativeArray<byte> payload = default;
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string path = Path.Combine(projectRoot, "Docs", "AgentLogs", DumpFileName);

            try
            {
                const int headerBytes = 13;
                const int rowBytes = 52;
                int length = telemetryRing.Length;
                int byteCount = headerBytes + length * rowBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(OrbitalRelativityDirector),
                    DumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);

                unsafe
                {
                    byte* bytes = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                    WriteUInt(bytes, 0, SourceHash);
                    bytes[4] = reason;
                    WriteInt(bytes, 5, TelemetryCapacity);
                    WriteInt(bytes, 9, _telemetryCursor);

                    int writeCursor = headerBytes;
                    for (int i = 0; i < length; i++)
                    {
                        int index = (_telemetryCursor + i) % length;
                        OrbitalTelemetryEntry entry = telemetryRing[index];
                        WriteUInt(bytes, writeCursor, entry.Frame);
                        WriteUInt(bytes, writeCursor + 4, entry.StateHash);
                        WriteDouble(bytes, writeCursor + 8, entry.UniverseVelocity.x);
                        WriteDouble(bytes, writeCursor + 16, entry.UniverseVelocity.y);
                        WriteDouble(bytes, writeCursor + 24, entry.UniverseVelocity.z);
                        WriteDouble(bytes, writeCursor + 32, entry.PlanetDistanceMeters);
                        WriteFloat(bytes, writeCursor + 40, entry.ReentryHeat01);
                        WriteFloat(bytes, writeCursor + 44, entry.CloudWhiteout01);
                        WriteUShort(bytes, writeCursor + 48, entry.Sequence);
                        bytes[writeCursor + 50] = entry.MathLod;
                        bytes[writeCursor + 51] = entry.Flags;
                        writeCursor += rowBytes;
                    }
                }

                _telemetryDumped = NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(OrbitalRelativityDirector),
                    DumpPayloadLabel);
            }
        }

        private static unsafe void WriteUInt(byte* data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        private static unsafe void WriteInt(byte* data, int offset, int value)
        {
            WriteUInt(data, offset, unchecked((uint)value));
        }

        private static unsafe void WriteUShort(byte* data, int offset, ushort value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
        }

        private static unsafe void WriteFloat(byte* data, int offset, float value)
        {
            UnsafeUtility.MemCpy(data + offset, &value, sizeof(float));
        }

        private static unsafe void WriteDouble(byte* data, int offset, double value)
        {
            ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
            data[offset] = (byte)bits;
            data[offset + 1] = (byte)(bits >> 8);
            data[offset + 2] = (byte)(bits >> 16);
            data[offset + 3] = (byte)(bits >> 24);
            data[offset + 4] = (byte)(bits >> 32);
            data[offset + 5] = (byte)(bits >> 40);
            data[offset + 6] = (byte)(bits >> 48);
            data[offset + 7] = (byte)(bits >> 56);
        }

        private bool TryReadTelemetryRing(out NativeArray<OrbitalTelemetryEntry>.ReadOnly telemetryRing)
        {
            telemetryRing = default;
            if (!_orbitalTelemetryLayoutValid)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null || !IsVaultHandleCreated(in _telemetryRingHandle))
                return false;

            return vault.TryReadOnlyHandle(in _telemetryRingHandle, out telemetryRing) &&
                   telemetryRing.IsCreated &&
                   telemetryRing.Length >= TelemetryCapacity;
        }

        private void ClearTelemetryDescriptor()
        {
            _telemetryRingHandle = default;
        }

        private void ReleaseTelemetryBuffer()
        {
            ReleaseVaultBuffer(_dataVault, ref _telemetryRingHandle);
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static void ReleaseVaultBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && IsVaultHandleCreated(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
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
            _physicsService?.QueueLinearVelocitySet(capsuleRigidbody, Vector3.zero, wake: false);
            _physicsService?.QueueAngularVelocitySet(capsuleRigidbody, Vector3.zero, wake: false);
        }

        private void QueueCapsuleAuthorityLock()
        {
            _pendingCapsuleAuthorityLock = true;
        }

        private void QueueOrbitalPresentation()
        {
            _pendingOrbitalPresentation = true;
        }

        private void QueueShaderGlobalClear()
        {
            _pendingOrbitalShaderClear = true;
        }

        private void QueueRuntimeAuthorityRelease()
        {
            _pendingRuntimeAuthorityRelease = true;
        }

        private static void SetRendererEnabled(Renderer renderer, bool enabled)
        {
            if (renderer != null && renderer.enabled != enabled)
                renderer.enabled = enabled;
        }

        private void ClearShaderGlobals()
        {
            Shader.SetGlobalFloat(_planetDistanceId, 0f);
            Shader.SetGlobalFloat(_fakeRadiusId, 0f);
            Shader.SetGlobalFloat(_universeSpeedId, 0f);
            Shader.SetGlobalFloat(_reentryHeatId, 0f);
            Shader.SetGlobalFloat(_cloudWhiteoutId, 0f);
            Shader.SetGlobalFloat(_leadingEdgeDotId, 0f);
            Shader.SetGlobalFloat(_mathLodId, MathLodImpostor);
            Shader.SetGlobalVector(_aegirSunDirectionId, Vector4.zero);
            Shader.SetGlobalVector(_aegirPlanetCenterRadiusId, Vector4.zero);
            Shader.SetGlobalVector(_aegirRingPlaneInnerId, Vector4.zero);
            Shader.SetGlobalVector(_aegirOrbitScalarsId, Vector4.zero);
            Shader.SetGlobalFloat(_aegirFlowPhaseId, 0f);
            Shader.SetGlobalFloat(_aegirFlowPhaseValidId, 0f);
            Shader.SetGlobalFloat(_globalQualityWeightId, 0f);
            Shader.SetGlobalVector(_legacySunDirectionId, Vector4.zero);
            Shader.SetGlobalVector(_legacyAegirDirectionId, Vector4.zero);
            _presentationShaderGlobalsUploaded = false;
            _uploadedPresentationShaderGlobals = default;
            _celestialParametersUploaded = false;
            _uploadedCelestialParameters = default;
            _aegirFlowPhaseUploaded = false;
            _uploadedAegirFlowPhase = -1f;
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

        private static bool IsFinite(float3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
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
            scriptedReentryBurnAccelerationMetersPerSecondSq = math.max(0f, scriptedReentryBurnAccelerationMetersPerSecondSq);
            maxUniverseSpeedMetersPerSecond = math.max(passiveApproachSpeedMetersPerSecond, maxUniverseSpeedMetersPerSecond);
            planetSphereScaleMeters = math.max(1f, planetSphereScaleMeters);
            fakePlanetRadiusMeters = math.max(planetSphereScaleMeters, fakePlanetRadiusMeters);
            gasGiantBackdropScaleMeters = math.max(planetSphereScaleMeters, gasGiantBackdropScaleMeters);
            aegirAngularRadius = math.clamp(aegirAngularRadius, 0.05f, 0.65f);
            aegirRingOuterRadius = math.clamp(aegirRingOuterRadius, 0.1f, 1.35f);
            aegirRingInnerRadius = math.clamp(aegirRingInnerRadius, 0.05f, aegirRingOuterRadius - 0.01f);
            aegirRingShadowStrength = math.saturate(aegirRingShadowStrength);
            aegirBandFlowSpeed = math.max(0f, aegirBandFlowSpeed);
            orbitKeyLightBaseIntensity = math.max(0f, orbitKeyLightBaseIntensity);
            eclipseLightFadeFloor = math.saturate(eclipseLightFadeFloor);
            eclipseFadeResponseHz = math.max(0.05f, eclipseFadeResponseHz);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CelestialParametersDTO
    {
        [FieldOffset(0)]
        public Vector4 SunDirection;
        [FieldOffset(16)]
        public Vector4 PlanetCenterRadius;
        [FieldOffset(32)]
        public Vector4 RingPlaneInner;
        [FieldOffset(48)]
        public Vector4 OrbitScalars;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PresentationShaderGlobalsDTO
    {
        [FieldOffset(0)]
        public Vector4 Primary;
        [FieldOffset(16)]
        public Vector4 Secondary;
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
