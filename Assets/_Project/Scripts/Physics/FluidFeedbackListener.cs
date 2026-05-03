using Hecton8.Core;
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
        private static int _pendingEventCount;
        private static int _lastOverflowWarningFrame = -1;

        /// <summary>
        /// Number of splash feedback payloads waiting for late-frame dispatch.
        /// </summary>
        public static int PendingCount => _pendingEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(FluidFeedbackEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            _listeners.Clear();
            _pendingEventCount = 0;
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
            Enqueue(in splashEvent);
        }

        /// <summary>
        /// Flushes deferred splash feedback payloads to listeners.
        /// </summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

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
                for (int i = count - 1; i >= 0; i--)
                    rawArray[i].OnFluidSplashQueued(in splashEvent);
            }

            if (_pendingEvents.IsEmpty())
                _pendingEventCount = 0;
        }

        private static void EnsureInitialized()
        {
            if (_pendingEvents.IsCreated)
                return;

            _pendingEvents = new NativeQueue<SplashEvent>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SplashEvent>[64] - deferred fluid splash feedback lane flushed by SystemDispatcher LateUpdate - owner: FluidFeedbackEvents
            NativeMemorySentinel.RegisterNativeQueue(
                _pendingEvents,
                PendingEventCapacity,
                nameof(FluidFeedbackEvents),
                nameof(_pendingEvents),
                NativeAllocationLifetime.Session);
        }

        private static bool Enqueue(in SplashEvent splashEvent)
        {
            if (_pendingEventCount >= PendingEventCapacity)
            {
                ReportOverflowOncePerFrame();
                return false;
            }

            EnsureInitialized();
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
    }

    /// <summary>
    /// Presentation listener that owns optional ParticleSystem and AudioSource feedback for fluid events.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Physics/Fluid Feedback Listener")]
    public sealed class FluidFeedbackListener : MonoBehaviour, IFluidSplashEventListener
    {
        [Header("Feedback")]
        [Tooltip("Optional particle system used for low-frequency hull splash events.")]
        [SerializeField] private ParticleSystem splashParticleSystem;
        [Tooltip("Optional audio source reserved for low-frequency hull splash feedback.")]
        [SerializeField] private AudioSource splashAudioSource;
        [Tooltip("Maximum particles emitted by one splash event.")]
        [SerializeField, Min(1)] private int maxParticlesPerSplash = 12;

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

            if (splashParticleSystem != null)
            {
                ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
                {
                    position = runtimePosition,
                    velocity = Vector3.up * Mathf.Max(0.1f, splashEvent.ImpactSpeedMetersPerSecond * 0.15f)
                };
                int particleCount = Mathf.Clamp(Mathf.CeilToInt(splashEvent.KineticEnergyJoules * 0.001f), 1, maxParticlesPerSplash);
                splashParticleSystem.Emit(emitParams, particleCount);
            }

            if (splashAudioSource == null)
                return;

            Transform audioTransform = splashAudioSource.transform;
            if (audioTransform != null)
                audioTransform.position = runtimePosition;

            if (!splashAudioSource.isPlaying)
                splashAudioSource.Play();
        }
    }
}
