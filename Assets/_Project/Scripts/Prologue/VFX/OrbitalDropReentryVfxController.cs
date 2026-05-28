using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Prologue.VFX
{
    /// <summary>
    /// Camera-local orbital re-entry whiteout bridge. It hides ocean residency spikes with shader-only plasma.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6910)]
    [AddComponentMenu("Hecton/Prologue/VFX/Orbital Drop Reentry VFX Controller")]
    public sealed class OrbitalDropReentryVfxController : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private int _signalPushDropCount;
        private const int TelemetryCapacity = 300;
        private const int TelemetryEntrySizeBytes = 64;
        private const uint DumpMagic = 0x4F525646u; // ORVF
        private const int DumpVersion = 1;
        private const uint PrologueSequenceSourceHash = PrologueSignalSourceHashes.SequenceDirector;
        private const uint OrbitalRelativitySourceHash = PrologueSignalSourceHashes.OrbitalRelativityDirector;
        private const uint PlasmaRoarHash = 0x50524F52u; // PROR
        private const uint OceanWavesHash = 0x4F574156u; // OWAV
        private const uint MassiveSplashHash = 0x4D53504Cu; // MSPL
        private const byte MassiveSplashDebrisKind = 32;
        private const SystemID VaultOwnerSystemId = SystemID.Vfx;
        private const BufferID TelemetryBufferId = BufferID.OrbitalDropReentryVfxTelemetryRing;
        private const float DefaultWhiteoutAltitudeMeters = 500f;
        private const float ShaderEpsilon = 0.0005f;
        private const float MaxPresentationDeltaSeconds = 0.25f;
        private const float MinimumOverlayLocalDistanceMeters = 0.02f;
        private const float MaximumOverlayLocalDistanceMeters = 2f;
        private const float OverlayNearClipPaddingMeters = 0.03f;
        private const float OverlayOverscan = 1.08f;
        private const string DumpFileName = "Dump_ORBITAL_DROP_REENTRY_VFX.bin";

        private static readonly ProfilerMarker _lateFrameMarker = new ProfilerMarker("H8.PrologueVFX.Reentry.LateFrame");
        private static readonly int _HectonReentryPlasmaState0Id = Shader.PropertyToID("_HectonReentryPlasmaState0");
        private static readonly int _HectonReentryPlasmaState1Id = Shader.PropertyToID("_HectonReentryPlasmaState1");
        private static readonly int _HectonReentryAmbientId = Shader.PropertyToID("_HectonReentryAmbient");
        private static readonly Color _defaultOceanAmbientColor = new Color(0.02f, 0.52f, 0.62f, 1f);

        private enum ReentryPhase : byte
        {
            Idle = 0,
            Heating = 1,
            Whiteout = 2,
            HydratedFade = 3,
            Complete = 4
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        private struct ReentryVfxTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public uint Frame;
            [System.Runtime.InteropServices.FieldOffset(4)]
            public float Heat01;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public float Opacity01;
            [System.Runtime.InteropServices.FieldOffset(12)]
            public float AltitudeMeters;
            [System.Runtime.InteropServices.FieldOffset(16)]
            public float VelocityMetersPerSecond;
            [System.Runtime.InteropServices.FieldOffset(20)]
            public float AmbientBlend01;
            [System.Runtime.InteropServices.FieldOffset(24)]
            public float OverlayDistanceMeters;
            [System.Runtime.InteropServices.FieldOffset(28)]
            public uint StateHash;
            [System.Runtime.InteropServices.FieldOffset(32)]
            public uint SectorHashLo;
            [System.Runtime.InteropServices.FieldOffset(36)]
            public uint Reserved2;
            [System.Runtime.InteropServices.FieldOffset(40)]
            public ushort Sequence;
            [System.Runtime.InteropServices.FieldOffset(42)]
            public ushort HydrationSequence;
            [System.Runtime.InteropServices.FieldOffset(44)]
            public byte Phase;
            [System.Runtime.InteropServices.FieldOffset(45)]
            public byte QualityWeightByte;
            [System.Runtime.InteropServices.FieldOffset(46)]
            public byte Flags;
            [System.Runtime.InteropServices.FieldOffset(47)]
            public byte Reserved;
            [System.Runtime.InteropServices.FieldOffset(48)]
            private byte _pad0;
            [System.Runtime.InteropServices.FieldOffset(49)]
            private byte _pad1;
            [System.Runtime.InteropServices.FieldOffset(50)]
            private byte _pad2;
            [System.Runtime.InteropServices.FieldOffset(51)]
            private byte _pad3;
            [System.Runtime.InteropServices.FieldOffset(52)]
            private byte _pad4;
            [System.Runtime.InteropServices.FieldOffset(53)]
            private byte _pad5;
            [System.Runtime.InteropServices.FieldOffset(54)]
            private byte _pad6;
            [System.Runtime.InteropServices.FieldOffset(55)]
            private byte _pad7;
            [System.Runtime.InteropServices.FieldOffset(56)]
            private byte _pad8;
            [System.Runtime.InteropServices.FieldOffset(57)]
            private byte _pad9;
            [System.Runtime.InteropServices.FieldOffset(58)]
            private byte _pad10;
            [System.Runtime.InteropServices.FieldOffset(59)]
            private byte _pad11;
            [System.Runtime.InteropServices.FieldOffset(60)]
            private byte _pad12;
            [System.Runtime.InteropServices.FieldOffset(61)]
            private byte _pad13;
            [System.Runtime.InteropServices.FieldOffset(62)]
            private byte _pad14;
            [System.Runtime.InteropServices.FieldOffset(63)]
            private byte _pad15;
        }

        [Header("Bindings")]
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private Transform plasmaOverlayTransform;
        [SerializeField] private Renderer capsuleWindowRenderer;
        [SerializeField] private Renderer plasmaOverlayRenderer;
        [SerializeField] private Material plasmaMaterial;
        [SerializeField] private bool assignMaterialOnEnable = true;
        [SerializeField] private bool forceCameraLocalOverlay = true;
        [SerializeField, Range(0.02f, 2f)] private float overlayLocalDistanceMeters = 0.35f;

        [Header("Response")]
        [SerializeField, Min(1f)] private float fullHeatVelocityMetersPerSecond = 7600f;
        [SerializeField, Min(1f)] private float whiteoutAltitudeMeters = DefaultWhiteoutAltitudeMeters;
        [SerializeField, Range(0.05f, 8f)] private float heatRisePerSecond = 2.8f;
        [SerializeField, Range(0.05f, 8f)] private float opacityRisePerSecond = 2.2f;
        [SerializeField, Range(0.05f, 8f)] private float opacityFadePerSecond = 1.35f;
        [SerializeField, Range(0.1f, 4f)] private float ambientTransitionSeconds = 2f;
        [SerializeField, Range(0.25f, 4f)] private float audioCrossfadeSeconds = 2f;
        [SerializeField, Range(0.05f, 0.5f)] private float audioCrossfadeIntervalSeconds = 0.1f;

        [Header("Lighting")]
        [SerializeField] private Color spaceAmbientColor = Color.black;
        [SerializeField] private Color oceanAmbientColor = _defaultOceanAmbientColor;
        [SerializeField] private bool driveAmbientProbe = true;

        private VaultGenerationHandle<ReentryVfxTelemetryEntry> _telemetryHandle;
        private IDataVault _dataVault;
        private ITickDispatcher _tickDispatcher;
        private Camera _camera;
        private AbsoluteUniversePosition _lastCapsuleAup;
        private ReentryPhase _phase;
        private int _telemetryCursor;
        private int _lastProcessedAtmosphericFrame = int.MinValue;
        private int _lastProcessedCompleteFrame = int.MinValue;
        private int _qualityRefreshFrame = int.MinValue;
        private ushort _stateSequence;
        private ushort _hydrationSequence;
        private float _heat01;
        private float _targetHeat01;
        private float _opacity01;
        private float _targetOpacity01;
        private float _altitudeMeters = float.PositiveInfinity;
        private float _velocityMetersPerSecond;
        private float _ambientBlend01;
        private float _lastUploadedHeat = float.PositiveInfinity;
        private float _lastUploadedOpacity = float.PositiveInfinity;
        private float _lastUploadedVelocity = float.PositiveInfinity;
        private float _lastUploadedAltitude = float.PositiveInfinity;
        private float _lastUploadedQualityPressure = float.PositiveInfinity;
        private float _lastUploadedPhase = float.PositiveInfinity;
        private float _lastOverlayDistanceMeters;
        private float _lastAppliedAmbientBlend = float.PositiveInfinity;
        private float _whiteoutHoldSecondsRemaining;
        private float _audioCrossfadeElapsedSeconds;
        private float _audioCrossfadeTimer;
        private float _qualityWeight = 1f;
        private byte _qualityWeightByte = byte.MaxValue;
        private bool _registeredLateFrame;
        private bool _hotSwapRegistered;
        private bool _blackBoxDumped;
        private bool _plasmaRoarPublished;
        private bool _oceanWavesPublished;
        private bool _splashPublished;
        private bool _audioCrossfadeActive;
        private bool _hasSpatialAnchor;

        public void ConfigureSceneBindings(
            Transform cameraTransform,
            Transform overlayTransform,
            Renderer windowRenderer,
            Renderer overlayRenderer,
            Material overlayMaterial)
        {
            if (cameraTransform != null)
                cameraRoot = cameraTransform;
            if (overlayTransform != null)
                plasmaOverlayTransform = overlayTransform;
            if (windowRenderer != null)
                capsuleWindowRenderer = windowRenderer;
            if (overlayRenderer != null)
                plasmaOverlayRenderer = overlayRenderer;
            if (overlayMaterial != null)
                plasmaMaterial = overlayMaterial;
        }

        private void OnEnable()
        {
            EnsureNativeTelemetry();
            PrologueReentrySignalLanes.Warm();
            ResetTransientState();
            ResolveColdDependencies();
            ApplyConfiguredMaterial();
            RefreshQualityPolicy();
            RegisterLateFrame();
            TryRegisterHotSwap();
            if (Application.isPlaying)
                PublishShaderState(force: true);
        }

        private void OnDisable()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            TryUnregisterHotSwap();
            ResetTransientState();
            if (Application.isPlaying)
            {
                _lastAppliedAmbientBlend = float.PositiveInfinity;
                ApplyAmbientBlend();
                PublishShaderState(force: true);
            }

            _tickDispatcher = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                if (ReferenceEquals(previousService, currentService))
                {
                    _tickDispatcher = currentService as ITickDispatcher;
                    return;
                }

                _tickDispatcher = currentService as ITickDispatcher;
                _registeredLateFrame = false;
                if (_tickDispatcher != null && isActiveAndEnabled)
                    RegisterLateFrame();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                ReleaseTelemetryBuffer(previousService as IDataVault ?? _dataVault);
                _dataVault = currentService as IDataVault;
                _telemetryCursor = 0;
                EnsureNativeTelemetry();
            }
        }

        private void OnDestroy()
        {
            ReleaseTelemetryBuffer(_dataVault);
        }

        /// <summary>
        /// Executes the late-frame VFX seam update after simulation signal snapshots are stable.
        /// </summary>
        public void LateFrameTick()
        {
            using (_lateFrameMarker.Auto())
            {
                if (!_registeredLateFrame)
                    return;

                ResolveMaterialDependencies();

                float deltaTime = ResolveUnscaledDeltaTime();
                RefreshQualityPolicy();
                ConsumeAtmosphericSignals();
                ConsumePrologueCompleteSignals();
                if (!HasActivePresentationState())
                    return;

                UpdateTargetsFromPhase();
                IntegrateState(deltaTime);
                MaintainCameraLocalOverlay();
                PublishShaderState(force: false);
                PublishStateSignal();
                WriteTelemetry(0);
            }
        }

        private void EnsureNativeTelemetry()
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return;

            if (vault.IsCompactionFenceActive)
            {
                _telemetryHandle = default;
                return;
            }

            if (IsVaultHandleCreated(in _telemetryHandle) &&
                vault.TryReadOnlyHandle(in _telemetryHandle, out NativeArray<ReentryVfxTelemetryEntry>.ReadOnly telemetry) &&
                telemetry.IsCreated &&
                telemetry.Length >= TelemetryCapacity)
            {
                return;
            }

            if (IsVaultHandleCreated(in _telemetryHandle))
                vault.ReleaseBuffer(in _telemetryHandle);

            if (vault.IsAllocationLocked)
                return;

            _telemetryHandle = vault.EnsureGenerationHandle<ReentryVfxTelemetryEntry>(
                TelemetryBufferId,
                TelemetryCapacity,
                VaultOwnerSystemId,
                NativeArrayOptions.ClearMemory);
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            return _dataVault;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private void ReleaseTelemetryBuffer(IDataVault vault)
        {
            if (vault != null && IsVaultHandleCreated(in _telemetryHandle))
                vault.ReleaseBuffer(in _telemetryHandle);

            _telemetryHandle = default;
        }

        private void ResetTransientState()
        {
            _lastCapsuleAup = default;
            _phase = ReentryPhase.Idle;
            _lastProcessedAtmosphericFrame = int.MinValue;
            _lastProcessedCompleteFrame = int.MinValue;
            _hydrationSequence = 0;
            _heat01 = 0f;
            _targetHeat01 = 0f;
            _opacity01 = 0f;
            _targetOpacity01 = 0f;
            _altitudeMeters = float.PositiveInfinity;
            _velocityMetersPerSecond = 0f;
            _ambientBlend01 = 0f;
            _whiteoutHoldSecondsRemaining = 0f;
            _audioCrossfadeElapsedSeconds = 0f;
            _audioCrossfadeTimer = 0f;
            _audioCrossfadeActive = false;
            _lastOverlayDistanceMeters = 0f;
            _blackBoxDumped = false;
            _plasmaRoarPublished = false;
            _oceanWavesPublished = false;
            _splashPublished = false;
            _hasSpatialAnchor = false;
        }

        private void ResolveColdDependencies()
        {
            if (_tickDispatcher == null)
                _tickDispatcher = GlobalRegistry.TickDispatcher;

            ResolveMaterialDependencies();
        }

        private void ResolveMaterialDependencies()
        {
            if (_camera == null && cameraRoot != null)
                cameraRoot.TryGetComponent(out _camera);
        }

        private void RegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterHotSwap()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void ApplyConfiguredMaterial()
        {
            if (!assignMaterialOnEnable || plasmaMaterial == null)
                return;

            if (capsuleWindowRenderer != null)
                capsuleWindowRenderer.sharedMaterial = plasmaMaterial;
            if (plasmaOverlayRenderer != null)
                plasmaOverlayRenderer.sharedMaterial = plasmaMaterial;
        }

        private void RefreshQualityPolicy()
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_qualityRefreshFrame == frame)
                return;

            _qualityRefreshFrame = frame;
            CacheQualityPolicy(ResolveGlobalQualityWeight());
        }

        private void CacheQualityPolicy(float qualityWeight)
        {
            float safeQualityWeight = math.saturate(math.isfinite(qualityWeight) ? qualityWeight : 1f);
            byte qualityWeightByte = (byte)math.clamp((int)math.round(safeQualityWeight * 255f), 0, 255);

            if (qualityWeightByte == _qualityWeightByte &&
                math.abs(_qualityWeight - safeQualityWeight) <= ShaderEpsilon)
                return;

            _qualityWeightByte = qualityWeightByte;
            _qualityWeight = safeQualityWeight;
        }

        private void ConsumeAtmosphericSignals()
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastProcessedAtmosphericFrame == frame)
                return;

            _lastProcessedAtmosphericFrame = frame;
            ReadOnlySpan<AtmosphericReentrySignal> signals = SignalBus<AtmosphericReentrySignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                AtmosphericReentrySignal signal = signals[i];
                if (!IsFiniteAtmosphericSignal(in signal))
                {
                    WriteTelemetry(ReentryVfxStateSignal.FlagNaNGuard);
                    DumpBlackBoxOnce();
                    continue;
                }

                if (!IsRecognizedAtmosphericPhase(signal.Phase))
                    continue;

                if (!IsFiniteRuntimeAup(in signal.CapsuleAup))
                {
                    WriteTelemetry(ReentryVfxStateSignal.FlagNaNGuard);
                    DumpBlackBoxOnce();
                    continue;
                }

                _lastCapsuleAup = signal.CapsuleAup;
                _hasSpatialAnchor = true;
                if (_phase == ReentryPhase.Whiteout && !_plasmaRoarPublished)
                    PublishPlasmaRoar();

                _altitudeMeters = math.max(0f, signal.AltitudeMeters);
                _velocityMetersPerSecond = math.max(0f, signal.UniverseVelocityMetersPerSecond);
                _targetHeat01 = ResolveHeat01(in signal);
                bool plasmaOrWhiteout = signal.Phase == AtmosphericReentrySignal.PhasePlasma ||
                                         signal.Phase == AtmosphericReentrySignal.PhaseWhiteout;
                if (_phase == ReentryPhase.Idle && plasmaOrWhiteout)
                    BeginHeating();

                if (signal.Phase == AtmosphericReentrySignal.PhaseWhiteout ||
                    (signal.Flags & AtmosphericReentrySignal.FlagWhiteoutRequested) != 0 ||
                    signal.AltitudeMeters <= PositiveFiniteOrMinimum(whiteoutAltitudeMeters, 1f))
                {
                    EnterWhiteout();
                }
            }
        }

        private void ConsumePrologueCompleteSignals()
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastProcessedCompleteFrame == frame)
                return;

            _lastProcessedCompleteFrame = frame;
            ReadOnlySpan<PrologueCompleteSignal> signals = SignalBus<PrologueCompleteSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PrologueCompleteSignal signal = signals[i];
                if (!math.isfinite(signal.WhiteoutHoldSeconds))
                {
                    WriteTelemetry(ReentryVfxStateSignal.FlagNaNGuard);
                    DumpBlackBoxOnce();
                    continue;
                }

                if (signal.WhiteoutHoldSeconds < 0f)
                    continue;

                bool sequenceOceanHandoff = signal.Phase == PrologueCompleteSignal.PhaseOceanHandoff &&
                                             signal.SourceHash == PrologueSequenceSourceHash;
                if (!sequenceOceanHandoff && !IsWhiteoutOnlyComplete(in signal))
                    continue;

                if (!sequenceOceanHandoff && _phase >= ReentryPhase.HydratedFade)
                    continue;

                if (sequenceOceanHandoff && !IsFiniteRuntimeAup(in signal.CapsuleAup))
                {
                    WriteTelemetry(ReentryVfxStateSignal.FlagNaNGuard);
                    DumpBlackBoxOnce();
                    continue;
                }

                if (sequenceOceanHandoff)
                {
                    _lastCapsuleAup = signal.CapsuleAup;
                    _hasSpatialAnchor = true;
                }

                _whiteoutHoldSecondsRemaining = math.max(_whiteoutHoldSecondsRemaining, math.max(0f, signal.WhiteoutHoldSeconds));
                EnterWhiteout();
                if (sequenceOceanHandoff)
                {
                    EnterHydratedFade();
                }
            }
        }

        private void BeginHeating()
        {
            _phase = ReentryPhase.Heating;
            PublishPlasmaRoar();
        }

        private void EnterWhiteout()
        {
            if (_phase < ReentryPhase.Whiteout)
                _phase = ReentryPhase.Whiteout;

            _targetOpacity01 = 1f;
            PublishPlasmaRoar();
        }

        private void EnterHydratedFade()
        {
            if (_phase < ReentryPhase.Whiteout)
                EnterWhiteout();

            if (_phase != ReentryPhase.HydratedFade)
                _hydrationSequence++;

            _phase = ReentryPhase.HydratedFade;
            BeginAudioCrossfade();
        }

        private void UpdateTargetsFromPhase()
        {
            switch (_phase)
            {
                case ReentryPhase.Idle:
                    _targetHeat01 = 0f;
                    _targetOpacity01 = 0f;
                    break;
                case ReentryPhase.Heating:
                    _targetOpacity01 = ResolveAltitudeOpacity01(_altitudeMeters);
                    break;
                case ReentryPhase.Whiteout:
                    _targetHeat01 = math.max(_targetHeat01, 1f);
                    _targetOpacity01 = 1f;
                    break;
                case ReentryPhase.HydratedFade:
                    _targetOpacity01 = 0f;
                    break;
                case ReentryPhase.Complete:
                    _targetHeat01 = 0f;
                    _targetOpacity01 = 0f;
                    break;
            }
        }

        private void IntegrateState(float deltaTime)
        {
            float safeDeltaTime = math.clamp(deltaTime, 0f, MaxPresentationDeltaSeconds);
            if (_whiteoutHoldSecondsRemaining > 0f)
            {
                _whiteoutHoldSecondsRemaining = math.max(0f, _whiteoutHoldSecondsRemaining - safeDeltaTime);
                if (_phase == ReentryPhase.HydratedFade && _whiteoutHoldSecondsRemaining > 0f)
                    _targetOpacity01 = 1f;
            }

            float heatRate = _targetHeat01 > _heat01
                ? PositiveFiniteOrMinimum(heatRisePerSecond, 0.05f)
                : PositiveFiniteOrMinimum(opacityFadePerSecond, 0.05f);
            _heat01 = MoveTowards01(_heat01, _targetHeat01, safeDeltaTime * heatRate);
            float opacityRate = _targetOpacity01 > _opacity01
                ? PositiveFiniteOrMinimum(opacityRisePerSecond, 0.05f)
                : PositiveFiniteOrMinimum(opacityFadePerSecond, 0.05f);
            float previousOpacity = _opacity01;
            _opacity01 = MoveTowards01(_opacity01, _targetOpacity01, safeDeltaTime * opacityRate);

            if (_phase == ReentryPhase.HydratedFade && previousOpacity > 0.5f && _opacity01 <= 0.5f)
                PublishMassiveSplash();

            if (_phase == ReentryPhase.HydratedFade && _opacity01 <= ShaderEpsilon)
                _phase = ReentryPhase.Complete;

            float targetAmbient = _phase == ReentryPhase.HydratedFade || _phase == ReentryPhase.Complete ? 1f : 0f;
            float ambientRate = safeDeltaTime * math.rcp(PositiveFiniteOrMinimum(ambientTransitionSeconds, 0.1f));
            _ambientBlend01 = MoveTowards01(_ambientBlend01, targetAmbient, ambientRate);
            ApplyAmbientBlend();
            UpdateAudioCrossfade(safeDeltaTime);

            if (!IsFiniteRuntimeState())
            {
                WriteTelemetry(ReentryVfxStateSignal.FlagNaNGuard);
                DumpBlackBoxOnce();
                SanitizeRuntimeState();
            }
        }

        private void MaintainCameraLocalOverlay()
        {
            if (!forceCameraLocalOverlay || plasmaOverlayTransform == null)
                return;

            Camera activeCamera = _camera;
            if (activeCamera == null && cameraRoot != null)
            {
                cameraRoot.TryGetComponent(out activeCamera);
                _camera = activeCamera;
            }

            Vector3 localPosition = plasmaOverlayTransform.localPosition;
            float targetZ = ResolveCameraSafeOverlayLocalDistanceMeters(ResolveOverlayLocalDistanceMeters(), activeCamera);
            _lastOverlayDistanceMeters = targetZ;
            if (math.abs(localPosition.z - targetZ) > 0.0001f)
            {
                localPosition.z = targetZ;
                plasmaOverlayTransform.localPosition = localPosition;
            }

            if (cameraRoot != null && plasmaOverlayTransform.parent == cameraRoot)
                plasmaOverlayTransform.localRotation = Quaternion.identity;

            if (activeCamera == null)
                return;

            float targetHeight;
            if (activeCamera.orthographic)
            {
                targetHeight = activeCamera.orthographicSize * 2f;
            }
            else
            {
                float fieldOfView = math.isfinite(activeCamera.fieldOfView) ? activeCamera.fieldOfView : 60f;
                targetHeight = 2f * targetZ * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            }

            if (!math.isfinite(targetHeight) || targetHeight <= 0f)
                return;

            float aspect = activeCamera.aspect;
            if (!math.isfinite(aspect) || aspect <= 0.01f)
                aspect = 16f / 9f;

            float targetWidth = targetHeight * aspect;
            Vector3 localScale = plasmaOverlayTransform.localScale;
            float scaledWidth = targetWidth * OverlayOverscan;
            float scaledHeight = targetHeight * OverlayOverscan;
            if (math.abs(localScale.x - scaledWidth) > 0.0001f ||
                math.abs(localScale.y - scaledHeight) > 0.0001f ||
                math.abs(localScale.z - 1f) > 0.0001f)
            {
                plasmaOverlayTransform.localScale = new Vector3(scaledWidth, scaledHeight, 1f);
            }
        }

        private void PublishShaderState(bool force)
        {
            float velocityScale = PositiveFiniteOrMinimum(fullHeatVelocityMetersPerSecond, 1f);
            float velocity01 = math.saturate(_velocityMetersPerSecond * math.rcp(velocityScale));
            float altitude01 = 1f - ResolveAltitudeOpacity01(_altitudeMeters);
            float qualityPressure01 = ResolveSurvivalPressure01();
            float phase = (float)_phase;

            SetReentryRuntimeGlobalsIfChanged(
                _heat01,
                _opacity01,
                velocity01,
                altitude01,
                qualityPressure01,
                phase,
                force);
        }

        private void ApplyAmbientBlend()
        {
            float ambientBlend01 = math.isfinite(_ambientBlend01) ? math.saturate(_ambientBlend01) : 0f;
            float lastAppliedAmbientBlend = math.isfinite(_lastAppliedAmbientBlend)
                ? _lastAppliedAmbientBlend
                : float.PositiveInfinity;
            if (math.abs(lastAppliedAmbientBlend - ambientBlend01) <= ShaderEpsilon)
                return;

            _lastAppliedAmbientBlend = ambientBlend01;
            bool finiteSpaceAmbient = IsFiniteColor(spaceAmbientColor);
            bool finiteOceanAmbient = IsFiniteColor(oceanAmbientColor);
            Color ambient = Color.Lerp(
                finiteSpaceAmbient ? spaceAmbientColor : ResolveFiniteColor(spaceAmbientColor, Color.black),
                finiteOceanAmbient ? oceanAmbientColor : ResolveFiniteColor(oceanAmbientColor, _defaultOceanAmbientColor),
                ambientBlend01);
            if (!finiteSpaceAmbient || !finiteOceanAmbient)
            {
                WriteTelemetry(ReentryVfxStateSignal.FlagNaNGuard);
                DumpBlackBoxOnce();
            }

            RenderSettings.ambientLight = ambient;
            if (!driveAmbientProbe)
                return;

            Shader.SetGlobalColor(_HectonReentryAmbientId, ambient);
        }

        private void BeginAudioCrossfade()
        {
            if (_audioCrossfadeActive)
                return;

            _audioCrossfadeActive = true;
            _audioCrossfadeElapsedSeconds = 0f;
            _audioCrossfadeTimer = 0f;
            _oceanWavesPublished = false;
        }

        private void UpdateAudioCrossfade(float deltaTime)
        {
            if (!_audioCrossfadeActive)
                return;

            float duration = PositiveFiniteOrMinimum(audioCrossfadeSeconds, 0.25f);
            _audioCrossfadeElapsedSeconds = math.min(duration, _audioCrossfadeElapsedSeconds + deltaTime);
            _audioCrossfadeTimer -= deltaTime;

            if (_audioCrossfadeTimer <= 0f)
            {
                float blend01 = math.saturate(_audioCrossfadeElapsedSeconds * math.rcp(duration));
                PublishAcousticBlend(PlasmaRoarHash, math.lerp(250f, 80f, blend01), 1f - blend01);
                PublishAcousticBlend(OceanWavesHash, math.lerp(90f, 180f, blend01), blend01 * 0.82f);
                _audioCrossfadeTimer = PositiveFiniteOrMinimum(audioCrossfadeIntervalSeconds, 0.05f);
            }

            if (_audioCrossfadeElapsedSeconds >= duration)
            {
                _audioCrossfadeActive = false;
                _oceanWavesPublished = true;
            }
        }

        private void PublishAcousticBlend(uint sourceId, float radiusMeters, float intensity01)
        {
            if (!_hasSpatialAnchor)
                return;

            float safeIntensity01 = math.saturate(intensity01);
            if (safeIntensity01 <= 0.001f)
                return;

            float safeRadiusMeters = PositiveFiniteOrMinimum(radiusMeters, 1f);
            AcousticPingSignal ping = new AcousticPingSignal
            {
                PositionAup = _lastCapsuleAup,
                RadiusMeters = safeRadiusMeters,
                Intensity01 = safeIntensity01,
                SourceId = sourceId,
                Channel = 0,
                Flags = 0
            };
            SignalBus<AcousticPingSignal>.TryPushTracked(in ping, ref _signalPushDropCount);
        }

        private void PublishPlasmaRoar()
        {
            if (_plasmaRoarPublished || !_hasSpatialAnchor)
                return;

            _plasmaRoarPublished = true;
            AcousticPingSignal ping = new AcousticPingSignal
            {
                PositionAup = _lastCapsuleAup,
                RadiusMeters = 250f,
                Intensity01 = 1f,
                SourceId = PlasmaRoarHash,
                Channel = 0,
                Flags = 0
            };
            SignalBus<AcousticPingSignal>.TryPushTracked(in ping, ref _signalPushDropCount);
        }

        private void PublishOceanWaves()
        {
            if (_oceanWavesPublished || _audioCrossfadeActive || !_hasSpatialAnchor)
                return;

            _oceanWavesPublished = true;
            AcousticPingSignal ping = new AcousticPingSignal
            {
                PositionAup = _lastCapsuleAup,
                RadiusMeters = 160f,
                Intensity01 = 0.82f,
                SourceId = OceanWavesHash,
                Channel = 0,
                Flags = 0
            };
            SignalBus<AcousticPingSignal>.TryPushTracked(in ping, ref _signalPushDropCount);
        }

        private void PublishMassiveSplash()
        {
            if (_splashPublished || !_hasSpatialAnchor)
                return;

            _splashPublished = true;
            DebrisSpawnSignal debris = new DebrisSpawnSignal
            {
                PositionAup = _lastCapsuleAup,
                SpeciesHash = MassiveSplashHash,
                SourceEntityId = MassiveSplashHash,
                Intensity01 = 1f,
                DebrisKind = MassiveSplashDebrisKind,
                Flags = 1,
                Quantity = ResolveSplashDebrisQuantity()
            };
            SignalBus<DebrisSpawnSignal>.TryPushTracked(in debris, ref _signalPushDropCount);

            VisorDropletSignal droplets = new VisorDropletSignal
            {
                PositionAup = _lastCapsuleAup,
                Intensity01 = 1f,
                DurationSeconds = ResolveDropletDurationSeconds(),
                SourceHash = MassiveSplashHash,
                DropletKind = VisorDropletSignal.DropletKindMassiveSplash,
                Flags = VisorDropletSignal.FlagExternalSplash,
                Sequence = _stateSequence
            };
            SignalBus<VisorDropletSignal>.TryPushTracked(in droplets, ref _signalPushDropCount);
        }

        private void PublishStateSignal()
        {
            _stateSequence++;
            byte flags = 0;
            if (_opacity01 >= 0.995f)
                flags |= ReentryVfxStateSignal.FlagWhiteout;
            if (_phase == ReentryPhase.HydratedFade || _phase == ReentryPhase.Complete)
                flags |= ReentryVfxStateSignal.FlagHydrated;
            if (_hasSpatialAnchor)
                flags |= ReentryVfxStateSignal.FlagSpatialAnchor;

            ReentryVfxStateSignal signal = new ReentryVfxStateSignal
            {
                CapsuleAup = _lastCapsuleAup,
                Heat01 = _heat01,
                Opacity01 = _opacity01,
                Sequence = _stateSequence,
                HydrationSequence = _hydrationSequence,
                Phase = (byte)_phase,
                Flags = flags,
                QualityTier = _qualityWeightByte,
                Reserved = 0
            };
            SignalBus<ReentryVfxStateSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        private void WriteTelemetry(byte extraFlags)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !IsVaultHandleCreated(in _telemetryHandle))
                return;

            if (!vault.TryAcquireWriteLock(in _telemetryHandle, VaultOwnerSystemId, out NativeArray<ReentryVfxTelemetryEntry> telemetry))
                return;

            try
            {
                if (!telemetry.IsCreated || telemetry.Length < TelemetryCapacity)
                    return;

                byte flags = extraFlags;
                if (_opacity01 >= 0.995f)
                    flags |= ReentryVfxStateSignal.FlagWhiteout;
                if (_phase == ReentryPhase.HydratedFade || _phase == ReentryPhase.Complete)
                    flags |= ReentryVfxStateSignal.FlagHydrated;
                if (_hasSpatialAnchor)
                    flags |= ReentryVfxStateSignal.FlagSpatialAnchor;

                ReentryVfxTelemetryEntry entry = default;
                entry.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
                entry.Sequence = _stateSequence;
                entry.HydrationSequence = _hydrationSequence;
                entry.Heat01 = _heat01;
                entry.Opacity01 = _opacity01;
                entry.AltitudeMeters = _altitudeMeters;
                entry.VelocityMetersPerSecond = _velocityMetersPerSecond;
                entry.AmbientBlend01 = _ambientBlend01;
                entry.OverlayDistanceMeters = ResolveTelemetryOverlayDistanceMeters();
                entry.Phase = (byte)_phase;
                entry.QualityWeightByte = _qualityWeightByte;
                entry.Flags = flags;
                entry.Reserved = 0;
                entry.StateHash = ResolveStateHash();
                entry.SectorHashLo = 0u;
                entry.Reserved2 = 0;

                telemetry[_telemetryCursor] = entry;
                _telemetryCursor = (_telemetryCursor + 1) % TelemetryCapacity;
            }
            finally
            {
                vault.ReleaseWriteLock(in _telemetryHandle, VaultOwnerSystemId);
            }
        }

        private bool HasActivePresentationState()
        {
            if (_phase == ReentryPhase.Idle)
            {
                return _heat01 > ShaderEpsilon ||
                       _targetHeat01 > ShaderEpsilon ||
                       _opacity01 > ShaderEpsilon ||
                       _targetOpacity01 > ShaderEpsilon ||
                       _whiteoutHoldSecondsRemaining > 0f ||
                       _audioCrossfadeActive;
            }

            if (_phase == ReentryPhase.Complete)
            {
                return _heat01 > ShaderEpsilon ||
                       _targetHeat01 > ShaderEpsilon ||
                       _opacity01 > ShaderEpsilon ||
                       _targetOpacity01 > ShaderEpsilon ||
                       _whiteoutHoldSecondsRemaining > 0f ||
                       _audioCrossfadeActive;
            }

            return true;
        }

        private void DumpBlackBoxOnce()
        {
            IDataVault vault = _dataVault;
            if (_blackBoxDumped ||
                vault == null ||
                !IsVaultHandleCreated(in _telemetryHandle) ||
                !vault.TryReadOnlyHandle(in _telemetryHandle, out NativeArray<ReentryVfxTelemetryEntry>.ReadOnly telemetry) ||
                !telemetry.IsCreated)
            {
                return;
            }

            _blackBoxDumped = true;
            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", DumpFileName));
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    Span<byte> header = stackalloc byte[24];
                    WriteUInt32LittleEndian(header.Slice(0, 4), DumpMagic);
                    WriteInt32LittleEndian(header.Slice(4, 4), DumpVersion);
                    WriteInt32LittleEndian(header.Slice(8, 4), TelemetryEntrySizeBytes);
                    WriteInt32LittleEndian(header.Slice(12, 4), TelemetryCapacity);
                    WriteInt32LittleEndian(header.Slice(16, 4), _telemetryCursor);
                    WriteUInt16LittleEndian(header.Slice(20, 2), _stateSequence);
                    stream.Write(header);

                    Span<byte> entryBytes = stackalloc byte[TelemetryEntrySizeBytes];
                    int length = math.min(TelemetryCapacity, telemetry.Length);
                    for (int i = 0; i < length; i++)
                    {
                        ReentryVfxTelemetryEntry entry = telemetry[i];
                        WriteTelemetryEntry(entryBytes, in entry);
                        stream.Write(entryBytes);
                    }
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[OrbitalDropReentryVfxController] Black box dump failed: " + exception.Message);
#endif
            }
        }

        private static void WriteTelemetryEntry(Span<byte> destination, in ReentryVfxTelemetryEntry entry)
        {
            destination.Clear();
            WriteUInt32LittleEndian(destination.Slice(0, 4), entry.Frame);
            WriteSingleLittleEndian(destination.Slice(4, 4), entry.Heat01);
            WriteSingleLittleEndian(destination.Slice(8, 4), entry.Opacity01);
            WriteSingleLittleEndian(destination.Slice(12, 4), entry.AltitudeMeters);
            WriteSingleLittleEndian(destination.Slice(16, 4), entry.VelocityMetersPerSecond);
            WriteSingleLittleEndian(destination.Slice(20, 4), entry.AmbientBlend01);
            WriteSingleLittleEndian(destination.Slice(24, 4), entry.OverlayDistanceMeters);
            WriteUInt32LittleEndian(destination.Slice(28, 4), entry.StateHash);
            WriteUInt32LittleEndian(destination.Slice(32, 4), entry.SectorHashLo);
            WriteUInt32LittleEndian(destination.Slice(36, 4), entry.Reserved2);
            WriteUInt16LittleEndian(destination.Slice(40, 2), entry.Sequence);
            WriteUInt16LittleEndian(destination.Slice(42, 2), entry.HydrationSequence);
            destination[44] = entry.Phase;
            destination[45] = entry.QualityWeightByte;
            destination[46] = entry.Flags;
            destination[47] = entry.Reserved;
        }

        private static void WriteSingleLittleEndian(Span<byte> destination, float value)
        {
            WriteUInt32LittleEndian(destination, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(Span<byte> destination, int value)
        {
            WriteUInt32LittleEndian(destination, unchecked((uint)value));
        }

        private static void WriteUInt32LittleEndian(Span<byte> destination, uint value)
        {
            destination[0] = (byte)value;
            destination[1] = (byte)(value >> 8);
            destination[2] = (byte)(value >> 16);
            destination[3] = (byte)(value >> 24);
        }

        private static void WriteUInt16LittleEndian(Span<byte> destination, ushort value)
        {
            destination[0] = (byte)value;
            destination[1] = (byte)(value >> 8);
        }

        private float ResolveUnscaledDeltaTime()
        {
            ITickDispatcher dispatcher = _tickDispatcher;
            if (dispatcher != null)
            {
                double dispatcherDelta = dispatcher.TimeSnapshot.UnscaledDeltaTime;
                if (dispatcherDelta > 0d && double.IsFinite(dispatcherDelta))
                    return dispatcherDelta > MaxPresentationDeltaSeconds ? MaxPresentationDeltaSeconds : (float)dispatcherDelta;
            }

            float fallback = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            return math.isfinite(fallback) && fallback > 0f ? math.min(fallback, MaxPresentationDeltaSeconds) : 0f;
        }

        private float ResolveHeat01(in AtmosphericReentrySignal signal)
        {
            if ((signal.Flags & AtmosphericReentrySignal.FlagAuthoritativeHeat) != 0 && math.isfinite(signal.Heat01))
                return math.saturate(signal.Heat01);

            float velocityScale = PositiveFiniteOrMinimum(fullHeatVelocityMetersPerSecond, 1f);
            float velocity01 = signal.UniverseVelocityMetersPerSecond * math.rcp(velocityScale);
            return math.saturate(velocity01);
        }

        private float ResolveAltitudeOpacity01(float altitudeMeters)
        {
            if (!math.isfinite(altitudeMeters))
                return 0f;

            float threshold = PositiveFiniteOrMinimum(whiteoutAltitudeMeters, 1f);
            return 1f - math.saturate(altitudeMeters * math.rcp(threshold));
        }

        private float ResolveOverlayLocalDistanceMeters()
        {
            return ClampFinite(overlayLocalDistanceMeters, MinimumOverlayLocalDistanceMeters, MaximumOverlayLocalDistanceMeters);
        }

        private float ResolveTelemetryOverlayDistanceMeters()
        {
            return math.isfinite(_lastOverlayDistanceMeters) && _lastOverlayDistanceMeters > 0f
                ? _lastOverlayDistanceMeters
                : ResolveOverlayLocalDistanceMeters();
        }

        private static float ResolveCameraSafeOverlayLocalDistanceMeters(float baseDistanceMeters, Camera activeCamera)
        {
            if (activeCamera == null)
                return baseDistanceMeters;

            float nearClip = activeCamera.nearClipPlane;
            if (!math.isfinite(nearClip) || nearClip < 0f)
                return baseDistanceMeters;

            float cameraSafeDistance = math.max(baseDistanceMeters, nearClip + OverlayNearClipPaddingMeters);
            return math.max(cameraSafeDistance, MinimumOverlayLocalDistanceMeters);
        }

        private void SetReentryRuntimeGlobalsIfChanged(
            float heat01,
            float opacity01,
            float velocity01,
            float altitude01,
            float qualityPressure01,
            float phase,
            bool force)
        {
            bool changed = force ||
                           math.abs(_lastUploadedHeat - heat01) > ShaderEpsilon ||
                           math.abs(_lastUploadedOpacity - opacity01) > ShaderEpsilon ||
                           math.abs(_lastUploadedVelocity - velocity01) > ShaderEpsilon ||
                           math.abs(_lastUploadedAltitude - altitude01) > ShaderEpsilon ||
                           math.abs(_lastUploadedQualityPressure - qualityPressure01) > ShaderEpsilon ||
                           math.abs(_lastUploadedPhase - phase) > ShaderEpsilon;
            if (!changed)
                return;

            Shader.SetGlobalVector(_HectonReentryPlasmaState0Id, new Vector4(heat01, opacity01, velocity01, altitude01));
            Shader.SetGlobalVector(_HectonReentryPlasmaState1Id, new Vector4(qualityPressure01, phase, 0f, 0f));
            _lastUploadedHeat = heat01;
            _lastUploadedOpacity = opacity01;
            _lastUploadedVelocity = velocity01;
            _lastUploadedAltitude = altitude01;
            _lastUploadedQualityPressure = qualityPressure01;
            _lastUploadedPhase = phase;
        }

        private static float MoveTowards01(float current, float target, float maxDelta)
        {
            return math.saturate(current + math.clamp(target - current, -math.max(0f, maxDelta), math.max(0f, maxDelta)));
        }

        private static float ResolveGlobalQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private float ResolveQualityCurve01()
        {
            return math.smoothstep(0f, 1f, math.saturate(_qualityWeight));
        }

        private float ResolveSurvivalPressure01()
        {
            float qualityPressure = 1f - ResolveQualityCurve01();
            return math.saturate(qualityPressure);
        }

        private ushort ResolveSplashDebrisQuantity()
        {
            float quantity = math.lerp(24f, 96f, ResolveQualityCurve01());
            return (ushort)math.clamp((int)math.round(quantity), 24, 96);
        }

        private float ResolveDropletDurationSeconds()
        {
            return math.lerp(1.35f, 2.2f, ResolveQualityCurve01());
        }

        private static float PositiveFiniteOrMinimum(float value, float minimum)
        {
            return math.isfinite(value) && value > minimum ? value : minimum;
        }

        private static float ClampFinite(float value, float minimum, float maximum)
        {
            return math.isfinite(value) ? math.clamp(value, minimum, maximum) : minimum;
        }

        private static bool IsFiniteColor(Color value)
        {
            return math.isfinite(value.r) &&
                   math.isfinite(value.g) &&
                   math.isfinite(value.b) &&
                   math.isfinite(value.a);
        }

        private static Color ResolveFiniteColor(Color value, Color fallback)
        {
            return new Color(
                math.isfinite(value.r) ? value.r : fallback.r,
                math.isfinite(value.g) ? value.g : fallback.g,
                math.isfinite(value.b) ? value.b : fallback.b,
                math.isfinite(value.a) ? value.a : fallback.a);
        }

        private void OnValidate()
        {
            overlayLocalDistanceMeters = ClampFinite(overlayLocalDistanceMeters, MinimumOverlayLocalDistanceMeters, MaximumOverlayLocalDistanceMeters);
            fullHeatVelocityMetersPerSecond = PositiveFiniteOrMinimum(fullHeatVelocityMetersPerSecond, 1f);
            whiteoutAltitudeMeters = PositiveFiniteOrMinimum(whiteoutAltitudeMeters, 1f);
            heatRisePerSecond = ClampFinite(heatRisePerSecond, 0.05f, 8f);
            opacityRisePerSecond = ClampFinite(opacityRisePerSecond, 0.05f, 8f);
            opacityFadePerSecond = ClampFinite(opacityFadePerSecond, 0.05f, 8f);
            ambientTransitionSeconds = ClampFinite(ambientTransitionSeconds, 0.1f, 4f);
            audioCrossfadeSeconds = ClampFinite(audioCrossfadeSeconds, 0.25f, 4f);
            audioCrossfadeIntervalSeconds = ClampFinite(audioCrossfadeIntervalSeconds, 0.05f, 0.5f);
            spaceAmbientColor = ResolveFiniteColor(spaceAmbientColor, Color.black);
            oceanAmbientColor = ResolveFiniteColor(oceanAmbientColor, _defaultOceanAmbientColor);
        }

        private static bool IsFiniteAtmosphericSignal(in AtmosphericReentrySignal signal)
        {
            return math.isfinite(signal.AltitudeMeters) &&
                   math.isfinite(signal.UniverseVelocityMetersPerSecond) &&
                   math.isfinite(signal.Heat01);
        }

        private static bool IsRecognizedAtmosphericPhase(byte phase)
        {
            return phase == AtmosphericReentrySignal.PhaseApproach ||
                   phase == AtmosphericReentrySignal.PhasePlasma ||
                   phase == AtmosphericReentrySignal.PhaseWhiteout;
        }

        private static bool IsWhiteoutOnlyComplete(in PrologueCompleteSignal signal)
        {
            return signal.SourceHash == OrbitalRelativitySourceHash &&
                   signal.Sequence != 0 &&
                   signal.Phase == PrologueCompleteSignal.PhaseWhiteout &&
                   (signal.Flags & PrologueCompleteSignal.FlagForceWhiteout) != 0;
        }

        private static bool IsFiniteRuntimeAup(in AbsoluteUniversePosition position)
        {
            float3 runtimePosition = position.ToRuntimeFloat3();
            return math.all(math.isfinite(runtimePosition));
        }

        private bool IsFiniteRuntimeState()
        {
            return math.isfinite(_heat01) &&
                   math.isfinite(_targetHeat01) &&
                   math.isfinite(_opacity01) &&
                   math.isfinite(_targetOpacity01) &&
                   math.isfinite(_velocityMetersPerSecond) &&
                   math.isfinite(_ambientBlend01) &&
                   math.isfinite(_audioCrossfadeElapsedSeconds) &&
                   math.isfinite(_audioCrossfadeTimer);
        }

        private void SanitizeRuntimeState()
        {
            _heat01 = 0f;
            _targetHeat01 = 0f;
            _opacity01 = 1f;
            _targetOpacity01 = 1f;
            _velocityMetersPerSecond = 0f;
            _ambientBlend01 = 0f;
            _audioCrossfadeElapsedSeconds = 0f;
            _audioCrossfadeTimer = 0f;
            _audioCrossfadeActive = false;
            _phase = ReentryPhase.Whiteout;
        }

        private uint ResolveStateHash()
        {
            uint hash = 2166136261u;
            hash = (hash ^ math.asuint(_heat01)) * 16777619u;
            hash = (hash ^ math.asuint(_opacity01)) * 16777619u;
            hash = (hash ^ math.asuint(_altitudeMeters)) * 16777619u;
            hash = (hash ^ math.asuint(_velocityMetersPerSecond)) * 16777619u;
            hash = (hash ^ (uint)_phase) * 16777619u;
            hash = (hash ^ (uint)_qualityWeightByte) * 16777619u;
            hash = (hash ^ (uint)_hydrationSequence) * 16777619u;
            return hash;
        }
    }
}
