using System;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Prologue.Space
{
    /// <summary>
    /// Converts the prologue ocean handoff signal into the guarded world scene transition.
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

        [SerializeField] private string targetWorldSceneName = DefaultWorldSceneName;
        [SerializeField] private uint minWhiteoutFramesBeforeWorldLoad = 2u;

        private ISceneService _sceneService;
        private IOrbitalDirector _orbitalDirector;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _handoffQueued;
        private bool _loadRequested;
        private bool _missingSceneServiceReported;
        private bool _sceneLoadBlockedReported;
        private bool _missingDispatcherReported;
        private uint _handoffFrame;
        private uint _lastProcessedCompleteFrame = uint.MaxValue;

        public void ConfigureTargetScene(string sceneName)
        {
            if (!string.IsNullOrEmpty(sceneName))
                targetWorldSceneName = sceneName;
        }

        private void OnEnable()
        {
            _sceneService = GlobalRegistry.Scene;
            _orbitalDirector = GlobalRegistry.OrbitalDirector;
            TryRegisterHotSwapListener();
            TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            TryUnregisterLateFrame();
            TryUnregisterHotSwapListener();
            _sceneService = null;
            _orbitalDirector = null;
            _handoffQueued = false;
            _loadRequested = false;
            _missingSceneServiceReported = false;
            _sceneLoadBlockedReported = false;
            _missingDispatcherReported = false;
            _lastProcessedCompleteFrame = uint.MaxValue;
        }

        public void LateFrameTick()
        {
            if (_loadRequested)
                return;

            uint frame = SystemDispatcher.CurrentFrameId;
            ConsumePrologueCompleteSignals(frame);
            TryLoadWorldIfReady(frame);
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

                _registeredLateFrame = false;
                if (currentService != null && isActiveAndEnabled)
                    TryRegisterLateFrame();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Scene)
            {
                _sceneService = currentService as ISceneService;
                if (isActiveAndEnabled)
                    TryLoadWorldIfReady(SystemDispatcher.CurrentFrameId);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.OrbitalDirectorRuntime)
                _orbitalDirector = currentService as IOrbitalDirector;
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
                if (!IsOceanHandoff(in signal) &&
                    !IsStandaloneOrbitalWhiteout(in signal))
                    continue;

                QueueWorldHandoff(frame);
                return;
            }

            if (TryResolveStandaloneOrbitalWhiteout())
                QueueWorldHandoff(frame);
        }

        private void TryLoadWorldIfReady(uint frame)
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

            _loadRequested = true;
            TryUnregisterLateFrame();
            _ = LoadWorldOnNextFrameAsync();
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

        private bool TryResolveStandaloneOrbitalWhiteout()
        {
            IOrbitalDirector orbital = _orbitalDirector;
            return orbital != null &&
                   orbital.TryGetSnapshot(out OrbitalDirectorSnapshot snapshot) &&
                   math.isfinite(snapshot.CloudWhiteout01) &&
                   math.isfinite(snapshot.PlanetDistanceMeters) &&
                   snapshot.CloudWhiteout01 >= 0.98f &&
                   snapshot.PlanetDistanceMeters <= 1f;
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

        private static void RefreshGameStartContextHandoff()
        {
            if (GameStartContextHolder.TryGetCurrentOrRestore(out GameStartContext context) &&
                context.IsValid)
            {
                GameStartContextHolder.SetCurrent(context);
            }
        }

        private async Awaitable LoadWorldOnNextFrameAsync()
        {
            await Awaitable.NextFrameAsync(destroyCancellationToken);
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                _loadRequested = false;
                return;
            }

            RefreshGameStartContextHandoff();
            string sceneName = string.IsNullOrEmpty(targetWorldSceneName) ? DefaultWorldSceneName : targetWorldSceneName;
            ISceneService sceneService = _sceneService;
            if (sceneService == null)
            {
                _loadRequested = false;
                PublishOnce(ref _missingSceneServiceReported, MissingSceneServiceHash);
                return;
            }

            if (!sceneService.CanLoadScene)
            {
                _loadRequested = false;
                PublishOnce(ref _sceneLoadBlockedReported, SceneLoadBlockedHash);
                return;
            }

            sceneService.LoadScene(sceneName);
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
        }
    }
}
