using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Signals;
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
    public sealed class OrbitalDropReentryVfxController : MonoBehaviour, ILateFrameTickable
    {
        private const int TelemetryCapacity = 300;
        private const int TelemetryEntrySizeBytes = 48;
        private const uint DumpMagic = 0x4F525646u; // ORVF
        private const int DumpVersion = 1;
        private const uint PrologueSequenceSourceHash = PrologueSignalSourceHashes.SequenceDirector;
        private const uint PlasmaRoarHash = 0x50524F52u; // PROR
        private const uint OceanWavesHash = 0x4F574156u; // OWAV
        private const uint MassiveSplashHash = 0x4D53504Cu; // MSPL
        private const byte MassiveSplashDebrisKind = 32;
        private const float DefaultWhiteoutAltitudeMeters = 500f;
        private const float ShaderEpsilon = 0.0005f;
        private const float MaxPresentationDeltaSeconds = 0.25f;
        private const float MinimumOverlayLocalDistanceMeters = 0.02f;
        private const string DumpFileName = "Dump_ORBITAL_DROP_REENTRY_VFX.bin";

        private static readonly ProfilerMarker _lateFrameMarker = new ProfilerMarker("H8.PrologueVFX.Reentry.LateFrame");
        private static readonly int _PlasmaHeatId = Shader.PropertyToID("_PlasmaHeat");
        private static readonly int _PlasmaOpacityId = Shader.PropertyToID("_PlasmaOpacity");
        private static readonly int _PlasmaVelocityId = Shader.PropertyToID("_PlasmaVelocity");
        private static readonly int _PlasmaAltitudeId = Shader.PropertyToID("_PlasmaAltitude01");
        private static readonly int _PlasmaLowTierId = Shader.PropertyToID("_PlasmaLowTier");
        private static readonly int _PlasmaPhaseId = Shader.PropertyToID("_HectonReentryPhase");

        private enum ReentryPhase : byte
        {
            Idle = 0,
            Heating = 1,
            Whiteout = 2,
            HydratedFade = 3,
            Complete = 4
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct ReentryVfxTelemetryEntry
        {
            public uint Frame;
            public ushort Sequence;
            public ushort HydrationSequence;
            public float Heat01;
            public float Opacity01;
            public float AltitudeMeters;
            public float VelocityMetersPerSecond;
            public float AmbientBlend01;
            public float OverlayDistanceMeters;
            public byte Phase;
            public byte QualityTier;
            public byte Flags;
            public byte Reserved;
            public uint StateHash;
            public uint SectorHashLo;
            public uint Reserved2;
        }

        [Header("Bindings")]
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private Transform plasmaOverlayTransform;
        [SerializeField] private Renderer capsuleWindowRenderer;
        [SerializeField] private Renderer plasmaOverlayRenderer;
        [SerializeField] private Material plasmaMaterial;
        [SerializeField] private bool assignMaterialOnEnable = true;
        [SerializeField] private bool forceCameraLocalOverlay = true;
        [SerializeField, Range(0.02f, 0.35f)] private float overlayLocalDistanceMeters = 0.08f;

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
        [SerializeField] private Color oceanAmbientColor = new Color(0.02f, 0.52f, 0.62f, 1f);
        [SerializeField] private bool driveAmbientProbe = true;

        private NativeArray<ReentryVfxTelemetryEntry> _telemetry;
        private ITickDispatcher _tickDispatcher;
        private Material _activeMaterial;
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
        private float _lastUploadedLowTier = float.PositiveInfinity;
        private float _lastUploadedPhase = float.PositiveInfinity;
        private float _lastGlobalHeat = float.PositiveInfinity;
        private float _lastGlobalOpacity = float.PositiveInfinity;
        private float _lastGlobalLowTier = float.PositiveInfinity;
        private float _lastAppliedAmbientBlend = float.PositiveInfinity;
        private float _whiteoutHoldSecondsRemaining;
        private float _audioCrossfadeElapsedSeconds;
        private float _audioCrossfadeTimer;
        private HectonQualityTier _qualityTier = HectonQualityTier.Unknown;
        private byte _qualityTierByte;
        private bool _lowTier = true;
        private bool _registeredLateFrame;
        private bool _blackBoxDumped;
        private bool _plasmaRoarPublished;
        private bool _oceanWavesPublished;
        private bool _splashPublished;
        private bool _audioCrossfadeActive;
        private bool _hasSpatialAnchor;

        private void OnEnable()
        {
            EnsureNativeTelemetry();
            PrologueReentrySignalLanes.Warm();
            ResetTransientState();
            ResolveDependencies();
            ApplyConfiguredMaterial();
            RefreshQualityTier(force: true);
            RegisterLateFrame();
            PublishShaderState(force: true);
        }

        private void OnDisable()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            ResetTransientState();
            _lastAppliedAmbientBlend = float.PositiveInfinity;
            ApplyAmbientBlend();
            PublishShaderState(force: true);
            _tickDispatcher = null;
        }

        private void OnDestroy()
        {
            if (_telemetry.IsCreated)
                _telemetry.Dispose();
            _telemetry = default;
        }

        /// <summary>
        /// Executes the late-frame VFX seam update after simulation signal snapshots are stable.
        /// </summary>
        public void LateFrameTick()
        {
            using (_lateFrameMarker.Auto())
            {
                if (!_registeredLateFrame)
                    RegisterLateFrame();

                ResolveDependencies();
                RefreshQualityTier(force: false);

                float deltaTime = ResolveUnscaledDeltaTime();
                ConsumeAtmosphericSignals();
                ConsumePrologueCompleteSignals();
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
            if (_telemetry.IsCreated)
                return;

            _telemetry = new NativeArray<ReentryVfxTelemetryEntry>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ReentryVfxTelemetryEntry>[300] - fixed re-entry VFX blackbox ring - owner: OrbitalDropReentryVfxController
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
            _blackBoxDumped = false;
            _plasmaRoarPublished = false;
            _oceanWavesPublished = false;
            _splashPublished = false;
            _hasSpatialAnchor = false;
        }

        private void ResolveDependencies()
        {
            if (_tickDispatcher == null)
                _tickDispatcher = GlobalRegistry.TickDispatcher;

            _activeMaterial = plasmaMaterial;
            if (_activeMaterial == null && capsuleWindowRenderer != null)
                _activeMaterial = capsuleWindowRenderer.sharedMaterial;
            if (_activeMaterial == null && plasmaOverlayRenderer != null)
                _activeMaterial = plasmaOverlayRenderer.sharedMaterial;
        }

        private void RegisterLateFrame()
        {
            if (_registeredLateFrame)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
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

        private void RefreshQualityTier(bool force)
        {
            int frame = Time.frameCount;
            if (!force && frame - _qualityRefreshFrame < 60)
                return;

            _qualityRefreshFrame = frame;
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            byte profileByte = GlobalRegistry.ScalabilityTierProfileByte;
            bool lowTier = tier == HectonQualityTier.Unknown ||
                           tier == HectonQualityTier.Low ||
                           tier == HectonQualityTier.Mx350 ||
                           GlobalRegistry.H8_LOW_MEMORY_PROFILE;

            if (!force && tier == _qualityTier && profileByte == _qualityTierByte && lowTier == _lowTier)
                return;

            _qualityTier = tier;
            _qualityTierByte = profileByte;
            _lowTier = lowTier;
        }

        private void ConsumeAtmosphericSignals()
        {
            int frame = Time.frameCount;
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
            int frame = Time.frameCount;
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

            Vector3 localPosition = plasmaOverlayTransform.localPosition;
            float targetZ = ResolveOverlayLocalDistanceMeters();
            if (math.abs(localPosition.z - targetZ) > 0.0001f)
            {
                localPosition.z = targetZ;
                plasmaOverlayTransform.localPosition = localPosition;
            }

            if (cameraRoot != null && plasmaOverlayTransform.parent == cameraRoot)
                plasmaOverlayTransform.localRotation = Quaternion.identity;
        }

        private void PublishShaderState(bool force)
        {
            Material material = _activeMaterial;
            float velocityScale = PositiveFiniteOrMinimum(fullHeatVelocityMetersPerSecond, 1f);
            float velocity01 = math.saturate(_velocityMetersPerSecond * math.rcp(velocityScale));
            float altitude01 = 1f - ResolveAltitudeOpacity01(_altitudeMeters);
            float lowTier01 = _lowTier ? 1f : 0f;
            float phase = (float)_phase;

            if (material != null)
            {
                SetMaterialFloatIfChanged(material, _PlasmaHeatId, _heat01, ref _lastUploadedHeat, force);
                SetMaterialFloatIfChanged(material, _PlasmaOpacityId, _opacity01, ref _lastUploadedOpacity, force);
                SetMaterialFloatIfChanged(material, _PlasmaVelocityId, velocity01, ref _lastUploadedVelocity, force);
                SetMaterialFloatIfChanged(material, _PlasmaAltitudeId, altitude01, ref _lastUploadedAltitude, force);
                SetMaterialFloatIfChanged(material, _PlasmaLowTierId, lowTier01, ref _lastUploadedLowTier, force);
                SetMaterialFloatIfChanged(material, _PlasmaPhaseId, phase, ref _lastUploadedPhase, force);
            }

            SetGlobalFloatIfChanged(_PlasmaHeatId, _heat01, ref _lastGlobalHeat, force);
            SetGlobalFloatIfChanged(_PlasmaOpacityId, _opacity01, ref _lastGlobalOpacity, force);
            SetGlobalFloatIfChanged(_PlasmaLowTierId, lowTier01, ref _lastGlobalLowTier, force);
        }

        private void ApplyAmbientBlend()
        {
            if (math.abs(_lastAppliedAmbientBlend - _ambientBlend01) <= ShaderEpsilon)
                return;

            _lastAppliedAmbientBlend = _ambientBlend01;
            Color ambient = Color.Lerp(spaceAmbientColor, oceanAmbientColor, _ambientBlend01);
            RenderSettings.ambientLight = ambient;
            if (!driveAmbientProbe)
                return;

            SphericalHarmonicsL2 probe = default;
            probe[0, 0] = ambient.r;
            probe[1, 0] = ambient.g;
            probe[2, 0] = ambient.b;
            RenderSettings.ambientProbe = probe;
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
            GlobalSignals.Publish(in ping);
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
            GlobalSignals.Publish(in ping);
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
            GlobalSignals.Publish(in ping);
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
                Quantity = _lowTier ? (ushort)24 : (ushort)96
            };
            GlobalSignals.Publish(in debris);

            VisorDropletSignal droplets = new VisorDropletSignal
            {
                PositionAup = _lastCapsuleAup,
                Intensity01 = 1f,
                DurationSeconds = _lowTier ? 1.35f : 2.2f,
                SourceHash = MassiveSplashHash,
                DropletKind = VisorDropletSignal.DropletKindMassiveSplash,
                Flags = VisorDropletSignal.FlagExternalSplash,
                Sequence = _stateSequence
            };
            SignalBus<VisorDropletSignal>.Push(in droplets);
        }

        private void PublishStateSignal()
        {
            _stateSequence++;
            byte flags = 0;
            if (_lowTier)
                flags |= ReentryVfxStateSignal.FlagLowTier;
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
                QualityTier = _qualityTierByte,
                Reserved = 0
            };
            SignalBus<ReentryVfxStateSignal>.Push(in signal);
        }

        private void WriteTelemetry(byte extraFlags)
        {
            if (!_telemetry.IsCreated)
                return;

            byte flags = extraFlags;
            if (_lowTier)
                flags |= ReentryVfxStateSignal.FlagLowTier;
            if (_opacity01 >= 0.995f)
                flags |= ReentryVfxStateSignal.FlagWhiteout;
            if (_phase == ReentryPhase.HydratedFade || _phase == ReentryPhase.Complete)
                flags |= ReentryVfxStateSignal.FlagHydrated;
            if (_hasSpatialAnchor)
                flags |= ReentryVfxStateSignal.FlagSpatialAnchor;

            ReentryVfxTelemetryEntry entry = new ReentryVfxTelemetryEntry
            {
                Frame = (uint)math.max(0, Time.frameCount),
                Sequence = _stateSequence,
                HydrationSequence = _hydrationSequence,
                Heat01 = _heat01,
                Opacity01 = _opacity01,
                AltitudeMeters = _altitudeMeters,
                VelocityMetersPerSecond = _velocityMetersPerSecond,
                AmbientBlend01 = _ambientBlend01,
                OverlayDistanceMeters = ResolveOverlayLocalDistanceMeters(),
                Phase = (byte)_phase,
                QualityTier = _qualityTierByte,
                Flags = flags,
                Reserved = 0,
                StateHash = ResolveStateHash(),
                SectorHashLo = 0u,
                Reserved2 = 0
            };

            _telemetry[_telemetryCursor] = entry;
            _telemetryCursor = (_telemetryCursor + 1) % TelemetryCapacity;
        }

        private void DumpBlackBoxOnce()
        {
            if (_blackBoxDumped || !_telemetry.IsCreated)
                return;

            _blackBoxDumped = true;
            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", DumpFileName));
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(DumpMagic);
                    writer.Write(DumpVersion);
                    writer.Write(TelemetryEntrySizeBytes);
                    writer.Write(TelemetryCapacity);
                    writer.Write(_telemetryCursor);
                    writer.Write(_stateSequence);
                    for (int i = 0; i < _telemetry.Length; i++)
                    {
                        ReentryVfxTelemetryEntry entry = _telemetry[i];
                        writer.Write(entry.Frame);
                        writer.Write(entry.Sequence);
                        writer.Write(entry.HydrationSequence);
                        writer.Write(entry.Heat01);
                        writer.Write(entry.Opacity01);
                        writer.Write(entry.AltitudeMeters);
                        writer.Write(entry.VelocityMetersPerSecond);
                        writer.Write(entry.AmbientBlend01);
                        writer.Write(entry.OverlayDistanceMeters);
                        writer.Write(entry.Phase);
                        writer.Write(entry.QualityTier);
                        writer.Write(entry.Flags);
                        writer.Write(entry.Reserved);
                        writer.Write(entry.StateHash);
                        writer.Write(entry.SectorHashLo);
                        writer.Write(entry.Reserved2);
                    }
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[OrbitalDropReentryVfxController] Black box dump failed: " + exception.Message);
#endif
            }
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

            float fallback = Time.unscaledDeltaTime;
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
            return ClampFinite(overlayLocalDistanceMeters, MinimumOverlayLocalDistanceMeters, 0.35f);
        }

        private static void SetMaterialFloatIfChanged(Material material, int shaderId, float value, ref float cachedValue, bool force)
        {
            if (!force && math.abs(cachedValue - value) <= ShaderEpsilon)
                return;

            material.SetFloat(shaderId, value);
            cachedValue = value;
        }

        private static void SetGlobalFloatIfChanged(int shaderId, float value, ref float cachedValue, bool force)
        {
            if (!force && math.abs(cachedValue - value) <= ShaderEpsilon)
                return;

            Shader.SetGlobalFloat(shaderId, value);
            cachedValue = value;
        }

        private static float MoveTowards01(float current, float target, float maxDelta)
        {
            return math.saturate(current + math.clamp(target - current, -math.max(0f, maxDelta), math.max(0f, maxDelta)));
        }

        private static float PositiveFiniteOrMinimum(float value, float minimum)
        {
            return math.isfinite(value) && value > minimum ? value : minimum;
        }

        private static float ClampFinite(float value, float minimum, float maximum)
        {
            return math.isfinite(value) ? math.clamp(value, minimum, maximum) : minimum;
        }

        private void OnValidate()
        {
            overlayLocalDistanceMeters = ClampFinite(overlayLocalDistanceMeters, MinimumOverlayLocalDistanceMeters, 0.35f);
            fullHeatVelocityMetersPerSecond = PositiveFiniteOrMinimum(fullHeatVelocityMetersPerSecond, 1f);
            whiteoutAltitudeMeters = PositiveFiniteOrMinimum(whiteoutAltitudeMeters, 1f);
            heatRisePerSecond = ClampFinite(heatRisePerSecond, 0.05f, 8f);
            opacityRisePerSecond = ClampFinite(opacityRisePerSecond, 0.05f, 8f);
            opacityFadePerSecond = ClampFinite(opacityFadePerSecond, 0.05f, 8f);
            ambientTransitionSeconds = ClampFinite(ambientTransitionSeconds, 0.1f, 4f);
            audioCrossfadeSeconds = ClampFinite(audioCrossfadeSeconds, 0.25f, 4f);
            audioCrossfadeIntervalSeconds = ClampFinite(audioCrossfadeIntervalSeconds, 0.05f, 0.5f);
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
            if (signal.Phase == PrologueCompleteSignal.PhaseWhiteout)
                return true;

            return signal.Phase == PrologueCompleteSignal.PhaseOceanHandoff &&
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
            hash = (hash ^ (uint)_qualityTierByte) * 16777619u;
            hash = (hash ^ (uint)_hydrationSequence) * 16777619u;
            return hash;
        }
    }
}
