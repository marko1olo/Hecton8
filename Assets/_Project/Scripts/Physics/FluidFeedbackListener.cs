using System;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Hecton.Localization;
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

        // COLD ALLOC: RegistryBucket<IFluidSplashEventListener>[16] - splash feedback listeners drained by SystemDispatcher LateUpdate - owner: FluidFeedbackEvents
        private static readonly RegistryBucket<IFluidSplashEventListener> _listeners = new RegistryBucket<IFluidSplashEventListener>(ListenerCapacity);

        private static int _snapshotReadFrame = -1;
        private static int _snapshotReadCursor;
        private static int _lastOverflowWarningFrame = -1;
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
            _listeners.Clear();
            _snapshotReadFrame = -1;
            _snapshotReadCursor = 0;
            _lastOverflowWarningFrame = -1;
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

            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        /// <summary>
        /// Unregisters a splash feedback listener.
        /// </summary>
        /// <param name="listener">Listener removed during component disable.</param>
        public static void Unregister(IFluidSplashEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        /// <summary>
        /// Publishes one splash payload to the deferred presentation lane.
        /// </summary>
        public static void PublishSplashQueued(in SplashEvent splashEvent)
        {
            if (_listeners.Count <= 0)
                return;

            Enqueue(in splashEvent);
        }

        /// <summary>
        /// Flushes deferred splash feedback payloads to listeners.
        /// </summary>
        public static void FlushPending()
        {
            if (_listeners.Count <= 0)
                return;

            EnsureInitialized();
            if (SignalBus<SplashEvent>.DroppedLastFlush > 0)
                ReportOverflowOncePerFrame();

            int currentFrame = Time.frameCount;
            if (_snapshotReadFrame != currentFrame)
            {
                _snapshotReadFrame = currentFrame;
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

            GlobalSignals.InitializeAllQueues();
            SignalBus<SplashEvent>.EnsureInitialized();
            _initialized = true;
        }

        private static bool Enqueue(in SplashEvent splashEvent)
        {
            if (_listeners.Count <= 0)
                return false;

            EnsureInitialized();
            SignalBus<SplashEvent>.Push(in splashEvent);
            return true;
        }

        private static void DispatchToListeners(in SplashEvent splashEvent)
        {
            IFluidSplashEventListener[] rawArray = _listeners.RawArray;
            int count = _listeners.Count;
            for (int i = count - 1; i >= 0; i--)
            {
                IFluidSplashEventListener listener = rawArray[i];
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

        private static void ReportOverflowOncePerFrame()
        {
            int frame = Time.frameCount;
            if (_lastOverflowWarningFrame == frame)
                return;

            _lastOverflowWarningFrame = frame;
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

        private void OnEnable()
        {
            FluidFeedbackEvents.Register(this);
        }

        private void OnDisable()
        {
            FluidFeedbackEvents.Unregister(this);
        }

        /// <inheritdoc />
        public void OnFluidSplashQueued(in SplashEvent splashEvent)
        {
            Vector3 runtimePosition = new Vector3(
                splashEvent.RuntimePosition.x,
                splashEvent.RuntimePosition.y,
                splashEvent.RuntimePosition.z);

            AbyssalFluidDecalManager fluidDecals = GlobalRegistry.AbyssalFluidDecals;
            if (fluidDecals != null)
            {
                Vector3 decalVelocity = Vector3.up * Mathf.Max(0.1f, splashEvent.ImpactSpeedMetersPerSecond);
                float intensity = Mathf.Clamp01(
                    splashEvent.SubmersionFactor * 0.45f +
                    splashEvent.ImpactSpeedMetersPerSecond * 0.055f +
                    splashEvent.KineticEnergyJoules * 0.00008f);
                fluidDecals.RegisterWaterSplash(runtimePosition, decalVelocity, intensity);
            }

            if (splashAudioSource == null)
                return;

            Transform audioTransform = splashAudioSource.transform;
            if (audioTransform != null)
                audioTransform.position = runtimePosition;

            AudioClip clip = splashAudioSource.clip;
            IAudioService audio = GlobalRegistry.Audio;
            if (clip != null && audio != null)
            {
                audio.PlayAtPoint(
                    clip,
                    runtimePosition,
                    splashAudioSource.volume,
                    splashAudioSource.pitch,
                    splashAudioSource.outputAudioMixerGroup);
            }
        }
    }
}
