using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Pooled collapse chunk spawned from catastrophic sargassum canopy failures.
    /// Owns rigidbody reset, optional silt trail playback, and timed despawn.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SargassumCollapseChunk : MonoBehaviour, ITickable, IPoolable
    {
        [Header("── Runtime Wiring ──────────────────")]
        [SerializeField]
        [Tooltip("Cached rigidbody used to drive the falling chunk.")]
        private Rigidbody chunkRigidbody;

        [SerializeField]
        [Tooltip("Optional looping particle trail emitted while the chunk sinks.")]
        private ParticleSystem siltTrail;

        [Header("── Defaults ────────────────────────")]
        [SerializeField, Min(0.5f)]
        [Tooltip("Fallback lifetime used when ActivateChunk receives an invalid despawn delay.")]
        private float defaultLifetime = 18f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Drag blend applied while the chunk is active. Higher values keep the fall heavy and damped.")]
        private float activeDrag = 0.18f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Angular-drag blend applied while the chunk tumbles downward.")]
        private float activeAngularDrag = 0.55f;

        private Vector3 _defaultLocalScale = Vector3.one;
        private float _defaultLinearDamping;
        private float _defaultAngularDamping;
        private float _remainingLifetime;
        private bool _registeredTick;

        private void Awake()
        {
            if (chunkRigidbody == null)
                TryGetComponent(out chunkRigidbody);

            _defaultLocalScale = transform.localScale;
            if (chunkRigidbody != null)
            {
                _defaultLinearDamping = chunkRigidbody.linearDamping;
                _defaultAngularDamping = chunkRigidbody.angularDamping;
            }
        }

        /// <summary>
        /// Arms the pooled chunk with its release velocities and lifetime.
        /// </summary>
        /// <param name="linearVelocityWS">Initial linear velocity in world space.</param>
        /// <param name="angularVelocityWS">Initial angular velocity in world space.</param>
        /// <param name="uniformScale">Uniform world scale multiplier applied to the root.</param>
        /// <param name="despawnDelay">Seconds before the chunk is returned to the pool.</param>
        public void ActivateChunk(Vector3 linearVelocityWS, Vector3 angularVelocityWS, float uniformScale, float despawnDelay)
        {
            if (chunkRigidbody != null)
            {
                chunkRigidbody.isKinematic = false;
                chunkRigidbody.WakeUp();
                chunkRigidbody.linearDamping = activeDrag;
                chunkRigidbody.angularDamping = activeAngularDrag;
                chunkRigidbody.linearVelocity = linearVelocityWS;
                chunkRigidbody.angularVelocity = angularVelocityWS;
            }

            transform.localScale = _defaultLocalScale * Mathf.Max(0.1f, uniformScale);

            if (siltTrail != null)
            {
                siltTrail.Clear(true);
                siltTrail.Play(true);
            }

            _remainingLifetime = despawnDelay > 0f ? despawnDelay : defaultLifetime;
            TryRegister();
        }

        /// <summary>
        /// Advances the pooled lifetime countdown.
        /// </summary>
        /// <param name="dt">Frame delta supplied by GameTickManager.</param>
        public void Tick(float dt)
        {
            if (_remainingLifetime <= 0f)
                return;

            _remainingLifetime -= Mathf.Max(0f, dt);
            if (_remainingLifetime > 0f)
                return;

            ObjectPoolManager poolManager = ObjectPoolManager.Instance;
            if (poolManager != null)
                poolManager.Despawn(gameObject);
        }

        /// <summary>
        /// Resets pooled state before the chunk becomes active.
        /// </summary>
        public void OnSpawn()
        {
            transform.localScale = _defaultLocalScale;
            _remainingLifetime = 0f;
            if (chunkRigidbody == null)
                return;

            chunkRigidbody.isKinematic = false;
            chunkRigidbody.linearVelocity = Vector3.zero;
            chunkRigidbody.angularVelocity = Vector3.zero;
            chunkRigidbody.linearDamping = _defaultLinearDamping;
            chunkRigidbody.angularDamping = _defaultAngularDamping;
            chunkRigidbody.Sleep();
        }

        /// <summary>
        /// Clears pooled state before the chunk returns to the inactive pool container.
        /// </summary>
        public void OnDespawn()
        {
            transform.localScale = _defaultLocalScale;
            _remainingLifetime = 0f;
            TryUnregister();
            if (chunkRigidbody != null)
            {
                chunkRigidbody.linearVelocity = Vector3.zero;
                chunkRigidbody.angularVelocity = Vector3.zero;
                chunkRigidbody.linearDamping = _defaultLinearDamping;
                chunkRigidbody.angularDamping = _defaultAngularDamping;
                chunkRigidbody.Sleep();
            }

            if (siltTrail != null)
                siltTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
        }

        private void TryRegister()
        {
            if (_registeredTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register((ITickable)this);
            _registeredTick = true;
        }

        private void TryUnregister()
        {
            if (!_registeredTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister((ITickable)this);

            _registeredTick = false;
        }
    }
}
