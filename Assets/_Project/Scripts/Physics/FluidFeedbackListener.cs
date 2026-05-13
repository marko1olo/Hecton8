using Hecton8.Core;
using Hecton8.World;
using Hecton.Localization;
using Unity.Collections;
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
    /// NativeQueue-backed feedback event bridge for submarine fluid splash payloads.
    /// </summary>
    public static class FluidFeedbackEvents
    {
        private const int ListenerCapacity = 16;
        private const int PendingEventCapacity = 64;

        private static readonly uint _overflowWarningHash = unchecked((uint)LocHash.Compute("FluidFeedbackEvents.Overflow"));
        private static readonly uint _queueHash = unchecked((uint)LocHash.Compute("FluidFeedbackEvents"));

        // COLD ALLOC: RegistryBucket<IFluidSplashEventListener>[16] - splash feedback listeners drained by SystemDispatcher LateUpdate - owner: FluidFeedbackEvents
        private static readonly RegistryBucket<IFluidSplashEventListener> _listeners = new RegistryBucket<IFluidSplashEventListener>(ListenerCapacity);

        private static NativeQueue<SplashEvent> _pendingEvents;
        private static NativeQueue<SplashEvent> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;
        private static int _lastOverflowWarningFrame = -1;

        /// <summary>
        /// Number of splash feedback payloads waiting for late-frame dispatch.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(FluidFeedbackEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(FluidFeedbackEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
            _lastOverflowWarningFrame = -1;
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
            if (!_pendingEvents.IsCreated)
                return;

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out SplashEvent splashEvent))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                IFluidSplashEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IFluidSplashEventListener listener = rawArray[i];
                        if (listener != null)
                            listener.OnFluidSplashQueued(in splashEvent);
                    }
                }
                finally
                {
                    _isDispatching = false;
                }
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<SplashEvent>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SplashEvent>[64] - deferred fluid splash feedback lane flushed by SystemDispatcher LateUpdate - owner: FluidFeedbackEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(FluidFeedbackEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<SplashEvent>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SplashEvent>[64] - next-frame fluid splash feedback lane prevents same-frame reentrant dispatch - owner: FluidFeedbackEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(FluidFeedbackEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
            }
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

        private static bool Enqueue(in SplashEvent splashEvent)
        {
            if (_listeners.Count <= 0)
                return false;

            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportOverflowOncePerFrame();
                return false;
            }

            EnsureInitialized();
            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(splashEvent);
                _nextFrameEventCount++;
                return true;
            }

            _pendingEvents.Enqueue(splashEvent);
            _pendingEventCount++;
            return true;
        }

        private static void ReportOverflowOncePerFrame()
        {
            int frame = Time.frameCount;
            if (_lastOverflowWarningFrame == frame)
                return;

            _lastOverflowWarningFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(_overflowWarningHash, _queueHash, PendingEventCapacity);
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!_pendingEvents.IsCreated ||
                !_nextFrameEvents.IsCreated ||
                _pendingEventCount > 0 ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<SplashEvent> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
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
