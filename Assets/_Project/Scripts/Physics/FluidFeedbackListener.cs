using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// Decoupled feedback event bridge for submarine fluid splash payloads.
    /// </summary>
    public static class FluidFeedbackEvents
    {
        public delegate void SplashQueuedHandler(in SplashEvent splashEvent);

        private static event SplashQueuedHandler SplashQueued;

        /// <summary>
        /// Subscribes a cold-path listener to splash feedback payloads.
        /// </summary>
        public static void SubscribeSplashQueued(SplashQueuedHandler handler)
        {
            SplashQueued += handler;
        }

        /// <summary>
        /// Unsubscribes a splash feedback listener.
        /// </summary>
        public static void UnsubscribeSplashQueued(SplashQueuedHandler handler)
        {
            SplashQueued -= handler;
        }

        /// <summary>
        /// Publishes one splash payload to presentation listeners.
        /// </summary>
        public static void PublishSplashQueued(in SplashEvent splashEvent)
        {
            SplashQueued?.Invoke(in splashEvent);
        }
    }

    /// <summary>
    /// Presentation listener that owns optional ParticleSystem and AudioSource feedback for fluid events.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Physics/Fluid Feedback Listener")]
    public sealed class FluidFeedbackListener : MonoBehaviour
    {
        [Header("Feedback")]
        [Tooltip("Optional particle system used for low-frequency hull splash events.")]
        [SerializeField] private ParticleSystem splashParticleSystem;
        [Tooltip("Optional audio source reserved for low-frequency hull splash feedback.")]
        [SerializeField] private AudioSource splashAudioSource;
        [Tooltip("Maximum particles emitted by one splash event.")]
        [SerializeField, Min(1)] private int maxParticlesPerSplash = 12;

        private FluidFeedbackEvents.SplashQueuedHandler _splashQueuedHandler;

        private void Awake()
        {
            _splashQueuedHandler = HandleSplashQueued;
        }

        private void OnEnable()
        {
            FluidFeedbackEvents.SubscribeSplashQueued(_splashQueuedHandler);
        }

        private void OnDisable()
        {
            FluidFeedbackEvents.UnsubscribeSplashQueued(_splashQueuedHandler);
        }

        private void HandleSplashQueued(in SplashEvent splashEvent)
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
