using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    #pragma warning disable CS0414
    /// <summary>
    /// Lightweight toxin exposure notifier that complements EnvironmentalHazard.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Hecton8/Gameplay/Toxin Hazard")]
    public sealed class ToxinHazard : MonoBehaviour, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        [Header("Toxin Settings")]
        [SerializeField] private float toxinBuildupRate = 0.3f;
        [SerializeField] private float maxToxinLevel = 50f;
        [SerializeField, TagSelector] private string playerTag = "Player";
        [SerializeField, Range(0.1f, 20f)] private float fallbackDetectionRadius = 2f;

        private int _activeExposureCount;
        private Transform _cachedTransform;
        private Collider _triggerCollider;
        private CachedTriggerVolume _cachedVolume;
        private IPlayerRuntimeContext _playerRuntime;
        private bool _registered;
        private bool _hotSwapRegistered;
        private bool _playerInside;

        private void Reset()
        {
            TryGetComponent(out _triggerCollider);
            if (_triggerCollider != null)
                _triggerCollider.isTrigger = true;
            _cachedVolume = CachedTriggerVolume.FromCollider(_triggerCollider, fallbackDetectionRadius);
        }

        private void Awake()
        {
            _cachedTransform = transform;
            TryGetComponent(out _triggerCollider);
            if (_triggerCollider != null)
                _triggerCollider.isTrigger = true;
            _cachedVolume = CachedTriggerVolume.FromCollider(_triggerCollider, fallbackDetectionRadius);
            _playerRuntime = GlobalRegistry.Player;
        }

        private void OnEnable()
        {
            _playerRuntime = GlobalRegistry.Player;
            TryRegister();
            TryRegisterHotSwapListener();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();
            ClearExposure();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();
            ClearExposure();
        }

        public void SlowTick()
        {
            bool inside = TryResolvePlayerPosition(out Vector3 playerPosition) &&
                          _cachedVolume.Contains(_cachedTransform, playerPosition);

            SetExposure(inside);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                IPlayerRuntimeContext nextPlayerRuntime = currentService as IPlayerRuntimeContext;
                if (!IsPlayerRuntimeContextBound(nextPlayerRuntime))
                {
                    _playerRuntime = null;
                    ClearExposure();
                    return;
                }

                if (!ReferenceEquals(_playerRuntime, nextPlayerRuntime))
                    ClearExposure();

                _playerRuntime = nextPlayerRuntime;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregister();
            if (currentService != null && isActiveAndEnabled)
                TryRegister();
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private bool TryResolvePlayerPosition(out Vector3 position)
        {
            position = Vector3.zero;
            IPlayerRuntimeContext runtime = _playerRuntime;
            if (!IsPlayerRuntimeContextBound(runtime))
                return false;

            if (runtime.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose))
            {
                position = new Vector3(pose.RuntimePosition.x, pose.RuntimePosition.y, pose.RuntimePosition.z);
                return IsPlayerTagAccepted(runtime.PlayerTransform);
            }

            Transform playerTransform = runtime.PlayerTransform;
            if (!IsPlayerTagAccepted(playerTransform))
                return false;

            position = playerTransform.position;
            return true;
        }

        private bool IsPlayerTagAccepted(Transform playerTransform)
        {
            return playerTransform != null &&
                   (string.IsNullOrEmpty(playerTag) || playerTransform.CompareTag(playerTag));
        }

        private static bool IsPlayerRuntimeContextBound(IPlayerRuntimeContext playerContext)
        {
            return playerContext != null &&
                   playerContext.IsInitialized &&
                   playerContext.PlayerTransform != null;
        }

        private void SetExposure(bool inside)
        {
            if (inside == _playerInside)
                return;

            _playerInside = inside;
            _activeExposureCount = inside ? 1 : 0;
            if (inside)
                HazardExposureNotifier.Enter(HazardType.Toxicity);
            else
                HazardExposureNotifier.Exit(HazardType.Toxicity);
        }

        private void ClearExposure()
        {
            if (_playerInside)
                HazardExposureNotifier.Exit(HazardType.Toxicity);

            _playerInside = false;
            _activeExposureCount = 0;
        }
    }
    #pragma warning restore CS0414
}
