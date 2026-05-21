using System;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Hecton.Localization;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// Listener for deferred fluid splash feedback payloads.
    /// </summary>
    public interface IFluidSplashEventListener
    {
        /// <summary>
        /// Receives one late-frame splash feedback payload.
        /// </summary>
        /// <param name="splashEvent">Sequential, unmanaged splash payload.</param>
        void OnFluidSplashQueued(in SplashEvent splashEvent);
    }

    /// <summary>
    /// Typed signal-lane feedback bridge for submarine fluid splash payloads.
    /// </summary>
    public static class FluidFeedbackEvents
    {
        private const int ListenerCapacity = 16;
        private const int PendingEventCapacity = 64;

        private static readonly uint _overflowWarningHash = unchecked((uint)LocHash.Compute("FluidFeedbackEvents.Overflow"));
        private static readonly uint _queueHash = unchecked((uint)LocHash.Compute(nameof(SplashEvent)));

        private struct ListenerSlot
        {
            public IFluidSplashEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        // COLD ALLOC: ListenerSlot[16] - splash feedback listeners drained by SystemDispatcher LateUpdate - owner: FluidFeedbackEvents
        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity];

        private static int _listenerCount;
        private static int _snapshotReadGeneration = -1;
        private static int _snapshotReadCursor;
        private static int _lastOverflowWarningGeneration = -1;
        private static bool _initialized;

        /// <summary>
        /// Number of splash feedback payloads waiting for late-frame dispatch.
        /// </summary>
        public static int PendingCount
        {
            get
            {
                int snapshotCount = SignalBus<SplashEvent>.SnapshotCount;
                if (snapshotCount <= 0)
                    return 0;

                return Math.Max(0, snapshotCount - _snapshotReadCursor);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < ListenerCapacity; i++)
                _listeners[i].Clear();

            _listenerCount = 0;
            _snapshotReadGeneration = -1;
            _snapshotReadCursor = 0;
            _lastOverflowWarningGeneration = -1;
            _initialized = false;
        }

        /// <summary>
        /// Registers a splash feedback listener.
        /// </summary>
        /// <param name="listener">Listener registered during component enable.</param>
        public static void Register(IFluidSplashEventListener listener)
        {
            if (listener == null)
                return;

            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return;
            }

            if (_listenerCount >= ListenerCapacity)
                return;

            _listeners[_listenerCount++].Listener = listener;
        }

        /// <summary>
        /// Unregisters a splash feedback listener.
        /// </summary>
        /// <param name="listener">Listener removed during component disable.</param>
        public static void Unregister(IFluidSplashEventListener listener)
        {
            if (listener == null)
                return;

            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                int lastIndex = --_listenerCount;
                if (i != lastIndex)
                    _listeners[i].Listener = _listeners[lastIndex].Listener;

                _listeners[lastIndex].Clear();
                return;
            }
        }

        /// <summary>
        /// Publishes one splash payload to the deferred presentation lane.
        /// </summary>
        public static void PublishSplashQueued(in SplashEvent splashEvent)
        {
            if (_listenerCount <= 0)
                return;

            Enqueue(in splashEvent);
        }

        /// <summary>
        /// Flushes deferred splash feedback payloads to listeners.
        /// </summary>
        public static void FlushPending()
        {
            if (_listenerCount <= 0)
                return;

            EnsureInitialized();
            if (SignalBus<SplashEvent>.DroppedLastFlush > 0)
                ReportOverflowOncePerSnapshot();

            int currentGeneration = SignalBus<SplashEvent>.SnapshotGeneration;
            if (_snapshotReadGeneration != currentGeneration)
            {
                _snapshotReadGeneration = currentGeneration;
                _snapshotReadCursor = 0;
            }

            ReadOnlySpan<SplashEvent> snapshot = SignalBus<SplashEvent>.GetFrameSnapshot();
            while (_snapshotReadCursor < snapshot.Length)
            {
                int signalIndex = _snapshotReadCursor;
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                {
                    RequeueRemainingSnapshot(snapshot, signalIndex);
                    _snapshotReadCursor = snapshot.Length;
                    return;
                }

                SplashEvent splashEvent = snapshot[signalIndex];
                _snapshotReadCursor = signalIndex + 1;
                DispatchToListeners(in splashEvent);
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            SignalBus<SplashEvent>.EnsureInitialized();
            _initialized = true;
        }

        private static bool Enqueue(in SplashEvent splashEvent)
        {
            if (_listenerCount <= 0)
                return false;

            EnsureInitialized();
            SignalBus<SplashEvent>.Push(in splashEvent);
            return true;
        }

        private static void DispatchToListeners(in SplashEvent splashEvent)
        {
            int count = _listenerCount;
            for (int i = count - 1; i >= 0; i--)
            {
                IFluidSplashEventListener listener = _listeners[i].Listener;
                if (listener != null)
                    listener.OnFluidSplashQueued(in splashEvent);
            }
        }

        private static void RequeueRemainingSnapshot(ReadOnlySpan<SplashEvent> snapshot, int startIndex)
        {
            for (int i = startIndex; i < snapshot.Length; i++)
            {
                SplashEvent splashEvent = snapshot[i];
                SignalBus<SplashEvent>.Push(in splashEvent);
            }
        }

        private static void ReportOverflowOncePerSnapshot()
        {
            int snapshotGeneration = SignalBus<SplashEvent>.SnapshotGeneration;
            if (_lastOverflowWarningGeneration == snapshotGeneration)
                return;

            _lastOverflowWarningGeneration = snapshotGeneration;
            GlobalTelemetryBus.PublishPerformanceWarning(_overflowWarningHash, _queueHash, PendingEventCapacity);
        }

    }

    /// <summary>
    /// Presentation listener that owns flat decal and optional AudioSource feedback for fluid events.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Physics/Fluid Feedback Listener")]
    public sealed class FluidFeedbackListener : MonoBehaviour, IFluidSplashEventListener
    {
        [Header("Feedback")]
        [Tooltip("Optional audio source reserved for low-frequency hull splash feedback.")]
        [SerializeField] private AudioSource splashAudioSource;

        private AbyssalFluidDecalManager _fluidDecals;
        private IAudioService _audio;

        private void OnEnable()
        {
            _fluidDecals = GlobalRegistry.AbyssalFluidDecals;
            _audio = GlobalRegistry.Audio;
            FluidFeedbackEvents.Register(this);
        }

        private void OnDisable()
        {
            FluidFeedbackEvents.Unregister(this);
            _fluidDecals = null;
            _audio = null;
        }

        /// <inheritdoc />
        public void OnFluidSplashQueued(in SplashEvent splashEvent)
        {
            Vector3 runtimePosition = new Vector3(
                splashEvent.RuntimePosition.x,
                splashEvent.RuntimePosition.y,
                splashEvent.RuntimePosition.z);

            if (_fluidDecals != null)
            {
                Vector3 decalVelocity = Vector3.up * math.max(0.1f, splashEvent.ImpactSpeedMetersPerSecond);
                float intensity = math.saturate(
                    splashEvent.SubmersionFactor * 0.45f +
                    splashEvent.ImpactSpeedMetersPerSecond * 0.055f +
                    splashEvent.KineticEnergyJoules * 0.00008f);
                _fluidDecals.RegisterWaterSplash(runtimePosition, decalVelocity, intensity);
            }

            if (splashAudioSource == null)
                return;

            Transform audioTransform = splashAudioSource.transform;
            if (audioTransform != null)
                audioTransform.position = runtimePosition;

            AudioClip clip = splashAudioSource.clip;
            if (clip != null && _audio != null)
            {
                _audio.PlayAtPoint(
                    clip,
                    runtimePosition,
                    splashAudioSource.volume,
                    splashAudioSource.pitch,
                    splashAudioSource.outputAudioMixerGroup);
            }
        }
    }
}
