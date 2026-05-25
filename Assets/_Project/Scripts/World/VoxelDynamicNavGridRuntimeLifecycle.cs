using UnityEngine;
using Hecton8.Core;

namespace Hecton8.World
{
    /// <summary>
    /// Unity lifecycle bridge for the static voxel navgrid runtime native containers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoxelDynamicNavGridRuntimeLifecycle : MonoBehaviour, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private bool _registeredSlowTick;
        private bool _hotSwapRegistered;

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            TryRegisterSlowTick();
        }

        private void OnDisable()
        {
            TryUnregisterSlowTick();
            TryUnregisterHotSwapListener();
            VoxelDynamicNavGridRuntime.DisposeAll();
        }

        private void OnDestroy()
        {
            TryUnregisterSlowTick();
            TryUnregisterHotSwapListener();
            VoxelDynamicNavGridRuntime.DisposeAll();
            VoxelDynamicNavGridRuntime.ClearLifecycleOwner(this);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher || currentService == null || !isActiveAndEnabled)
                return;

            TryUnregisterSlowTick();
            TryRegisterSlowTick();
        }

        private void TryRegisterSlowTick()
        {
            if (_registeredSlowTick || !Application.isPlaying)
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterSlowTick()
        {
            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
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

        public void SlowTick()
        {
            VoxelDynamicNavGridRuntime.TickDeferredDirtyVolumes();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            IPlayerRuntimeContext player = GlobalRegistry.RegisteredPlayer;
            Transform playerTransform = player != null ? player.PlayerTransform : null;
            if (playerTransform == null)
                return;

            VoxelDynamicNavGridRuntime.DrawEditorOpenCellGizmos(playerTransform.position, 20f);
        }
#endif
    }
}
