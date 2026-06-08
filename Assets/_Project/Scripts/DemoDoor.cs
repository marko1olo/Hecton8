using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace ScifiOffice
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class DemoDoor : MonoBehaviour, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static readonly int OpenTriggerHash = Animator.StringToHash("Open");

        private Animator _animator;
        private Transform _cachedTransform;
        private Collider _triggerCollider;
        private CachedTriggerVolume _cachedVolume;
        private IPlayerRuntimeContext _playerRuntime;
        private bool _registered;
        private bool _lateFrameRegistered;
        private bool _hotSwapRegistered;
        private bool _playerInside;
        private bool _openTriggerQueued;

        private void Awake()
        {
            _cachedTransform = transform;
            TryGetComponent(out _animator);
            CacheTriggerVolumeCold();
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
            TryUnregister();
            TryUnregisterHotSwapListener();
            _playerInside = false;
        }

        public void Tick(float deltaTime)
        {
            bool inside = _animator != null &&
                          TryResolvePlayerPosition(out Vector3 playerPosition) &&
                          _cachedVolume.Contains(_cachedTransform, playerPosition);
            if (inside == _playerInside)
                return;

            _playerInside = inside;
            if (inside)
                _openTriggerQueued = true;
        }

        public void LateFrameTick()
        {
            if (!_openTriggerQueued)
                return;

            _openTriggerQueued = false;
            Animator animator = _animator;
            if (animator != null)
                animator.SetTrigger(OpenTriggerHash);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
                _playerRuntime = currentService as IPlayerRuntimeContext;
        }

        private bool TryResolvePlayerPosition(out Vector3 position)
        {
            position = Vector3.zero;
            IPlayerRuntimeContext runtime = _playerRuntime;
            if (runtime == null)
                return false;

            if (runtime.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose))
            {
                float3 runtimePosition = pose.RuntimePosition;
                position = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
                return math.all(math.isfinite(runtimePosition));
            }

            Transform playerTransform = runtime.PlayerTransform;
            if (playerTransform == null)
                return false;

            position = playerTransform.position;
            return math.all(math.isfinite((float3)position));
        }

        private void CacheTriggerVolumeCold()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (_triggerCollider == null)
                TryGetComponent(out _triggerCollider);

            if (_triggerCollider != null)
            {
                _triggerCollider.isTrigger = true;
                _cachedVolume = CachedTriggerVolume.FromCollider(_triggerCollider, 1f);
            }
        }

        private void TryRegister()
        {
            if ((_registered && _lateFrameRegistered) || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registered)
                _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            if (!_lateFrameRegistered)
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }

            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registered = false;
            }
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
    }
}
