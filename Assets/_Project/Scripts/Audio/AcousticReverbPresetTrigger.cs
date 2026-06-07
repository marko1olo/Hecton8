using Hecton8.Core;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;

namespace Hecton8.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class AcousticReverbPresetTrigger : MonoBehaviour, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        public enum ReverbPreset : byte
        {
            SmallRoom = 0,
            LargeRoom = 1
        }

        [SerializeField] private ReverbPreset preset = ReverbPreset.SmallRoom;
        [SerializeField] private AudioMixerSnapshot smallRoomSnapshot;
        [SerializeField] private AudioMixerSnapshot largeRoomSnapshot;
        [SerializeField, Min(0f)] private float transitionSeconds = 0.08f;

        private Transform _cachedTransform;
        private BoxCollider _triggerCollider;
        private CachedTriggerVolume _cachedVolume;
        private IPlayerRuntimeContext _playerRuntime;
        private bool _registered;
        private bool _registeredLateFrame;
        private bool _hotSwapRegistered;
        private bool _playerInside;
        private bool _pendingPresetStateDirty;
        private bool _pendingPlayerInside;

        private void Reset()
        {
            ForceTriggerCollider();
            CacheVolumeCold();
        }

        private void Awake()
        {
            _cachedTransform = transform;
            CacheVolumeCold();
            _playerRuntime = GlobalRegistry.Player;
        }

        private void OnValidate()
        {
            ForceTriggerCollider();
            CacheVolumeCold();
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
            AcousticOcclusionUtility.ClearTriggerReverbPreset();
        }

        public void Tick(float deltaTime)
        {
            bool inside = TryResolvePlayerPosition(out Vector3 playerPosition) &&
                          _cachedVolume.Contains(_cachedTransform, playerPosition);
            if (inside == _playerInside)
                return;

            _playerInside = inside;
            _pendingPlayerInside = inside;
            _pendingPresetStateDirty = true;
        }

        public void LateFrameTick()
        {
            if (!_pendingPresetStateDirty)
                return;

            _pendingPresetStateDirty = false;
            if (_pendingPlayerInside)
                ApplyPreset();
            else
                AcousticOcclusionUtility.ClearTriggerReverbPreset();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregister(clearPendingPreset: false);
                if (currentService != null && isActiveAndEnabled)
                {
                    TryRegister();
                }

                return;
            }

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

            return false;
        }

        private void ApplyPreset()
        {
            AcousticOcclusionUtility.SetTriggerReverbPreset(
                preset == ReverbPreset.LargeRoom
                    ? AcousticReverbPresetKind.LargeRoom
                    : AcousticReverbPresetKind.SmallRoom);

            AudioMixerSnapshot snapshot = preset == ReverbPreset.LargeRoom
                ? largeRoomSnapshot
                : smallRoomSnapshot;
            if (snapshot != null)
                snapshot.TransitionTo(transitionSeconds);
        }

        private void CacheVolumeCold()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (_triggerCollider == null)
                TryGetComponent(out _triggerCollider);

            if (_triggerCollider != null)
                _cachedVolume = CachedTriggerVolume.FromCollider(_triggerCollider, 1f);
        }

        private void ForceTriggerCollider()
        {
            if (TryGetComponent(out BoxCollider boxCollider))
            {
                boxCollider.isTrigger = true;
                _triggerCollider = boxCollider;
                _cachedVolume = CachedTriggerVolume.FromCollider(boxCollider, 1f);
            }
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            TryUnregister(clearPendingPreset: true);
        }

        private void TryUnregister(bool clearPendingPreset)
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registered = false;
            }

            if (clearPendingPreset)
                _pendingPresetStateDirty = false;
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

            GlobalRegistry.UnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }
    }
}
