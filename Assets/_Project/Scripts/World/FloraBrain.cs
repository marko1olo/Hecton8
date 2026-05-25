using Hecton8.Core;
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
    public sealed class FloraBrain : MonoBehaviour, ITickable, IGlobalRegistryHotSwapListener
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
        private bool _hotSwapListenerRegistered;

        private void Awake()
        {
            ResolveOrganicManager();
        }

        private void OnEnable()
        {
            ResolveOrganicManager();
            TryRegisterHotSwapListener();
            TryRegisterTick();
        }

        private void OnDisable()
        {
            TryUnregisterTick();
            TryUnregisterHotSwapListener();
        }

        /// <summary>
        /// Applies oxygen drain from parasite-hosting flora near the current player transform.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (destructibleOrganicManager == null || !TryResolvePlayerRuntime())
                return;

            if (!destructibleOrganicManager.TryEvaluateParasiteExposure(ResolvePlayerRuntimePosition(), out float exposure01) ||
                exposure01 <= 0.0001f)
            {
                return;
            }

            float oxygenDrain = parasiteBaseOxygenDrainPerSecond * 2f * exposure01 * Mathf.Max(0f, deltaTime);
            if (oxygenDrain > 0f)
                _survivalSystem.DrainOxygen(oxygenDrain);
        }

        private Vector3 ResolvePlayerRuntimePosition()
        {
            return _playerTransform != null ? _playerTransform.position : Vector3.zero;
        }

        private void ResolveOrganicManager()
        {
            if (destructibleOrganicManager != null)
                return;

            TryGetComponent(out destructibleOrganicManager);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher && currentService != null && isActiveAndEnabled)
                TryRegisterTick();
        }

        private void TryRegisterTick()
        {
            if (_tickRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _tickRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterTick()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private bool TryResolvePlayerRuntime()
        {
            if (_playerTransform != null && _survivalSystem != null)
                return true;

            if (Time.time < _nextPlayerResolveTime)
                return false;

            _nextPlayerResolveTime = Time.time + Mathf.Max(0.1f, playerResolveRetryIntervalSeconds);
            if (!PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) ||
                runtimeContext == null ||
                runtimeContext.PlayerTransform == null ||
                runtimeContext.SurvivalSystem == null)
            {
                return false;
            }

            _playerTransform = runtimeContext.PlayerTransform;
            _survivalSystem = runtimeContext.SurvivalSystem;
            return true;
        }
    }
}
