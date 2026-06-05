using System;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Prologue.Space
{
    /// <summary>
    /// Converts prologue whiteout and ocean handoff signals into a guarded additive world activation.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6900)]
    public sealed class PrologueWorldHandoffSceneLoader : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const string DefaultWorldSceneName = "02_HECTON_WORLD";
        private const uint SourceHash = 0x50574C44u; // PWLD
        private const uint MissingSceneServiceHash = 0x53434E4Du; // SCNM
        private const uint SceneLoadBlockedHash = 0x53434E42u; // SCNB
        private const uint MissingDispatcherHash = 0x44495350u; // DISP
        private const uint ScenePreloadFailedHash = 0x53434E46u; // SCNF
        private const uint SceneActivationHeldHash = 0x53434148u; // SCAH
        private const float AtmosphericPreloadHeatThreshold01 = 0.001f;
        private const float ActivationProgressReady01 = 0.9f;
        private const float ActivationHoldMaskDurationSeconds = 0.14f;
        private const float ActivationHoldCameraAmplitudeScale = 0.58f;
        private const float ActivationHoldCameraTranslationGain = 0.20f;
        private const float ActivationHoldCameraRotationGain = 0.74f;
        private const float ActivationHoldHighPriorityDebtThreshold = 0.58f;
        private const uint ActivationHoldMaskFrameStride = 2u;

        [SerializeField] private string targetWorldSceneName = DefaultWorldSceneName;
        [SerializeField] private uint minWhiteoutFramesBeforeWorldLoad = 2u;
        [SerializeField, Range(-100, 100)] private int additiveLoadPriority = 64;

        private ISceneService _sceneService;
        private AsyncOperation _worldLoadOperation;
        private AsyncOperation _orbitUnloadOperation;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _handoffQueued;
        private bool _loadRequested;
        private bool _activationRequested;
        private bool _activationReleased;
        private bool _worldSceneActivated;
        private bool _orbitUnloadRequested;
        private bool _missingSceneServiceReported;
        private bool _sceneLoadBlockedReported;
        private bool _missingDispatcherReported;
        private bool _scenePreloadFailedReported;
        private bool _activationHeldReported;
        private uint _handoffFrame;
        private uint _lastProcessedCompleteFrame = uint.MaxValue;
        private uint _lastProcessedAtmosphericFrame = uint.MaxValue;
        private uint _lastActivationHoldMaskFrame = uint.MaxValue;
        private uint _activationHoldMaskSequence;
        private int _signalPushDropCount;

        public void ConfigureTargetScene(string sceneName)
        {
            if (!string.IsNullOrEmpty(sceneName))
                targetWorldSceneName = sceneName;
        }

        private void OnEnable()
        {
            PrologueReentrySignalLanes.Warm();
            _sceneService = GlobalRegistry.Scene;
            TryRegisterHotSwapListener();
            TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            ReleaseHeldWorldLoadBeforeDisable();
            TryUnregisterLateFrame();
            TryUnregisterHotSwapListener();
            _sceneService = null;
            _handoffQueued = false;
            _loadRequested = false;
            _activationRequested = false;
            _activationReleased = false;
            _worldSceneActivated = false;
            _orbitUnloadRequested = false;
            _missingSceneServiceReported = false;
            _sceneLoadBlockedReported = false;
            _missingDispatcherReported = false;
            _scenePreloadFailedReported = false;
            _activationHeldReported = false;
            _worldLoadOperation = null;
            _orbitUnloadOperation = null;
            _lastProcessedCompleteFrame = uint.MaxValue;
            _lastProcessedAtmosphericFrame = uint.MaxValue;
            _lastActivationHoldMaskFrame = uint.MaxValue;
            _activationHoldMaskSequence = 0u;
            _signalPushDropCount = 0;
        }

        public void LateFrameTick()
        {
            uint frame = SystemDispatcher.CurrentFrameId;
            ConsumeAtmosphericPreloadSignals(frame);
            ConsumePrologueCompleteSignals(frame);
            TryBeginWorldPreloadIfReady(frame);
            TryReleaseWorldActivationIfReady(frame);
            TryCompleteActivatedWorldHandoff();
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

                TryUnregisterLateFrame();
                if (currentService != null && isActiveAndEnabled)
                    TryRegisterLateFrame();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Scene)
            {
                _sceneService = currentService as ISceneService;
                if (isActiveAndEnabled)
                    TryBeginWorldPreloadIfReady(SystemDispatcher.CurrentFrameId);
                return;
            }

        }

        private void ConsumeAtmosphericPreloadSignals(uint frame)
        {
            if (_handoffQueued || _lastProcessedAtmosphericFrame == frame)
                return;

            _lastProcessedAtmosphericFrame = frame;
            ReadOnlySpan<AtmosphericReentrySignal> signals = SignalBus<AtmosphericReentrySignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                AtmosphericReentrySignal signal = signals[i];
                if (!IsWorldPreloadSignal(in signal))
                    continue;

                QueueWorldHandoff(frame);
                return;
            }
        }

        private void ConsumePrologueCompleteSignals(uint frame)
        {
            if (_lastProcessedCompleteFrame == frame)
                return;

            _lastProcessedCompleteFrame = frame;
            ReadOnlySpan<PrologueCompleteSignal> signals = SignalBus<PrologueCompleteSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PrologueCompleteSignal signal = signals[i];
                if (IsOceanHandoff(in signal))
                {
                    QueueWorldHandoff(frame);
                    RequestWorldActivation();
                    return;
                }

                if (IsStandaloneOrbitalWhiteout(in signal))
                {
                    QueueWorldHandoff(frame);
                    return;
                }
            }
        }

        private void TryBeginWorldPreloadIfReady(uint frame)
        {
            if (!_handoffQueued || _loadRequested)
                return;

            if (frame - _handoffFrame < minWhiteoutFramesBeforeWorldLoad)
                return;

            ISceneService sceneService = _sceneService;
            if (sceneService == null)
            {
                PublishOnce(ref _missingSceneServiceReported, MissingSceneServiceHash);
                return;
            }

            if (!sceneService.CanLoadScene)
            {
                PublishOnce(ref _sceneLoadBlockedReported, SceneLoadBlockedHash);
                return;
            }

            RefreshGameStartContextHandoff();
            string sceneName = ResolveTargetWorldSceneName();
            Scene loadedWorldScene = SceneManager.GetSceneByName(sceneName);
            if (loadedWorldScene.IsValid() && loadedWorldScene.isLoaded)
            {
                _loadRequested = true;
                return;
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (operation == null)
            {
                PublishOnce(ref _scenePreloadFailedReported, ScenePreloadFailedHash);
                return;
            }

            operation.allowSceneActivation = false;
            operation.priority = math.clamp(additiveLoadPriority, -100, 100);
            _worldLoadOperation = operation;
            _loadRequested = true;
        }

        private void TryReleaseWorldActivationIfReady(uint frame)
        {
            if (!_activationRequested || _activationReleased || !_loadRequested)
                return;

            AsyncOperation operation = _worldLoadOperation;
            if (operation == null)
            {
                Scene loadedWorldScene = SceneManager.GetSceneByName(ResolveTargetWorldSceneName());
                if (loadedWorldScene.IsValid() && loadedWorldScene.isLoaded)
                    _activationReleased = true;
                return;
            }

            float progress01 = math.saturate(operation.progress * math.rcp(ActivationProgressReady01));
            if (progress01 < 1f)
            {
                PublishOnce(ref _activationHeldReported, SceneActivationHeldHash);
                EmitActivationHoldMask(frame, 1f - progress01);
                return;
            }

            operation.allowSceneActivation = true;
            _activationReleased = true;
        }

        private void TryCompleteActivatedWorldHandoff()
        {
            if (!_activationReleased)
                return;

            string sceneName = ResolveTargetWorldSceneName();
            Scene worldScene = SceneManager.GetSceneByName(sceneName);
            if (!_worldSceneActivated)
            {
                if (!worldScene.IsValid() || !worldScene.isLoaded)
                    return;

                SceneManager.SetActiveScene(worldScene);
                _worldSceneActivated = true;
            }

            if (!_orbitUnloadRequested)
            {
                Scene orbitScene = gameObject.scene;
                if (orbitScene.IsValid() &&
                    orbitScene.isLoaded &&
                    worldScene.IsValid() &&
                    orbitScene.handle != worldScene.handle)
                {
                    _orbitUnloadOperation = SceneManager.UnloadSceneAsync(orbitScene);
                }

                _orbitUnloadRequested = true;
            }

            if (_orbitUnloadOperation != null && !_orbitUnloadOperation.isDone)
                return;

            TryUnregisterLateFrame();
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying)
                return;

            if (GlobalRegistry.TickDispatcher == null)
            {
                PublishOnce(ref _missingDispatcherReported, MissingDispatcherHash);
                return;
            }

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private static bool IsOceanHandoff(in PrologueCompleteSignal signal)
        {
            return signal.Phase == PrologueCompleteSignal.PhaseOceanHandoff &&
                   signal.SourceHash == PrologueSignalSourceHashes.SequenceDirector &&
                   signal.Sequence != 0 &&
                   math.isfinite(signal.WhiteoutHoldSeconds) &&
                   signal.WhiteoutHoldSeconds >= 0f &&
                   (signal.Flags & PrologueCompleteSignal.FlagForceWhiteout) != 0;
        }

        private static bool IsStandaloneOrbitalWhiteout(in PrologueCompleteSignal signal)
        {
            return signal.Phase == PrologueCompleteSignal.PhaseWhiteout &&
                   signal.SourceHash == PrologueSignalSourceHashes.OrbitalRelativityDirector &&
                   signal.Sequence != 0 &&
                   math.isfinite(signal.WhiteoutHoldSeconds) &&
                   signal.WhiteoutHoldSeconds >= 0f &&
                   (signal.Flags & PrologueCompleteSignal.FlagForceWhiteout) != 0;
        }

        private static bool IsWorldPreloadSignal(in AtmosphericReentrySignal signal)
        {
            return signal.Sequence != 0 &&
                   (signal.Flags & AtmosphericReentrySignal.FlagAuthoritativeHeat) != 0 &&
                   (signal.Phase == AtmosphericReentrySignal.PhasePlasma ||
                    signal.Phase == AtmosphericReentrySignal.PhaseWhiteout) &&
                   math.isfinite(signal.AltitudeMeters) &&
                   math.isfinite(signal.UniverseVelocityMetersPerSecond) &&
                   math.isfinite(signal.Heat01) &&
                   signal.Heat01 >= AtmosphericPreloadHeatThreshold01;
        }

        private void QueueWorldHandoff(uint frame)
        {
            if (_handoffQueued)
                return;

            _handoffQueued = true;
            _handoffFrame = frame;
            _missingSceneServiceReported = false;
            _sceneLoadBlockedReported = false;
        }

        private void RequestWorldActivation()
        {
            _activationRequested = true;
            _activationHeldReported = false;
        }

        private void EmitActivationHoldMask(uint frame, float debt01)
        {
            if (_lastActivationHoldMaskFrame != uint.MaxValue &&
                frame - _lastActivationHoldMaskFrame < ActivationHoldMaskFrameStride)
            {
                return;
            }

            float safeDebt = math.saturate(math.isfinite(debt01) ? debt01 : 1f);
            float intensity01 = math.saturate(0.48f + safeDebt * 0.47f);
            _lastActivationHoldMaskFrame = frame;

            StreamingTurbulenceSignal turbulence = default;
            turbulence.Intensity01 = intensity01;
            turbulence.Debt01 = safeDebt;
            turbulence.DurationSeconds = ActivationHoldMaskDurationSeconds;
            turbulence.Frame = frame;
            turbulence.SourceHash = SourceHash;
            turbulence.Sequence = unchecked(++_activationHoldMaskSequence);
            SignalBus<StreamingTurbulenceSignal>.TryPushTracked(in turbulence, ref _signalPushDropCount);

            byte priority = safeDebt >= ActivationHoldHighPriorityDebtThreshold
                ? CameraJuiceSignals.HighPriority
                : CameraJuiceSignals.NormalPriority;
            CameraJuiceSignals.TryPublishImpact(
                intensity01,
                Vector3.zero,
                Vector3.down,
                CameraJuiceSignals.ContinuousPressureStressProfileHash,
                ActivationHoldCameraAmplitudeScale,
                priority,
                0f,
                ActivationHoldCameraTranslationGain,
                ActivationHoldCameraRotationGain,
                SourceHash);
        }

        private void ReleaseHeldWorldLoadBeforeDisable()
        {
            AsyncOperation operation = _worldLoadOperation;
            if (operation == null || _activationReleased || operation.allowSceneActivation)
                return;

            // Unity scene loads cannot be cancelled once started; releasing prevents a stalled scene queue.
            operation.allowSceneActivation = true;
        }

        private static void RefreshGameStartContextHandoff()
        {
            if (GameStartContextHolder.TryGetCurrentOrRestore(out GameStartContext context) &&
                context.IsValid)
            {
                GameStartContextHolder.SetCurrent(context);
            }
        }

        private string ResolveTargetWorldSceneName()
        {
            return string.IsNullOrEmpty(targetWorldSceneName) ? DefaultWorldSceneName : targetWorldSceneName;
        }

        private static void PublishOnce(ref bool reported, uint warningHash)
        {
            if (reported)
                return;

            reported = true;
            GlobalTelemetryBus.PublishPerformanceWarning(warningHash, SourceHash, 1f);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(targetWorldSceneName))
                targetWorldSceneName = DefaultWorldSceneName;
            if (minWhiteoutFramesBeforeWorldLoad == 0u)
                minWhiteoutFramesBeforeWorldLoad = 1u;
            additiveLoadPriority = math.clamp(additiveLoadPriority, -100, 100);
        }
    }
}
