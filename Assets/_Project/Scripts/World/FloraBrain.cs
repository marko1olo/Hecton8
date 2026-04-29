using Hecton8.Core;
using Hecton8.Bootstrap;
using Hecton8.Gameplay;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Runtime parasite behavior owner for indirect flora instances.
    /// Applies localized oxygen drain when the player swims through parasite-hosting flora.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-118)]
    public sealed class FloraBrain : MonoBehaviour, ITickable
    {
        [Header("Runtime Wiring")]
        [SerializeField]
        [Tooltip("Organic entropy owner that evaluates runtime parasite exposure from indirect flora metadata.")]
        private DestructibleOrganicManager destructibleOrganicManager;

        [Header("Parasites")]
        [SerializeField, Min(0.01f)]
        [Tooltip("Base oxygen drain per second imposed by parasite-hosting flora before the mandatory x2 multiplier is applied.")]
        private float parasiteBaseOxygenDrainPerSecond = 0.55f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Interval between player/survival reference refresh attempts when runtime wiring is temporarily unavailable.")]
        private float playerResolveRetryIntervalSeconds = 1f;

        private Transform _playerTransform;
        private HectonSurvivalSystem _survivalSystem;
        private float _nextPlayerResolveTime;
        private bool _tickRegistered;

        private void Awake()
        {
            if (destructibleOrganicManager == null)
                destructibleOrganicManager = GetComponent<DestructibleOrganicManager>();
        }

        private void OnEnable()
        {
            if (_tickRegistered)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = true;
        }

        private void OnDisable()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = false;
        }

        /// <summary>
        /// Applies oxygen drain from parasite-hosting flora near the current player transform.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (destructibleOrganicManager == null)
                destructibleOrganicManager = GetComponent<DestructibleOrganicManager>();

            if (destructibleOrganicManager == null || !TryResolvePlayerRuntime())
                return;

            if (!destructibleOrganicManager.TryEvaluateParasiteExposure(_playerTransform.position, out float exposure01) ||
                exposure01 <= 0.0001f)
            {
                return;
            }

            float oxygenDrain = parasiteBaseOxygenDrainPerSecond * 2f * exposure01 * Mathf.Max(0f, deltaTime);
            if (oxygenDrain > 0f)
                _survivalSystem.DrainOxygen(oxygenDrain);
        }

        private bool TryResolvePlayerRuntime()
        {
            if (_playerTransform != null && _survivalSystem != null)
                return true;

            if (Time.time < _nextPlayerResolveTime)
                return false;

            _nextPlayerResolveTime = Time.time + Mathf.Max(0.1f, playerResolveRetryIntervalSeconds);
            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) || playerTransform == null)
                return false;

            if (!playerTransform.TryGetComponent(out HectonSurvivalSystem survivalSystem) || survivalSystem == null)
                return false;

            _playerTransform = playerTransform;
            _survivalSystem = survivalSystem;
            return true;
        }
    }
}
