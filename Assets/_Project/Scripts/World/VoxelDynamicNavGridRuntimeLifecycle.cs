using UnityEngine;
using Hecton8.Core;

namespace Hecton8.World
{
    /// <summary>
    /// Unity lifecycle bridge for the static voxel navgrid runtime native containers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoxelDynamicNavGridRuntimeLifecycle : MonoBehaviour, ISlowTickable
    {
        private bool _registeredSlowTick;

        private void OnEnable()
        {
            if (_registeredSlowTick || !Application.isPlaying)
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void OnDisable()
        {
            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }

            VoxelDynamicNavGridRuntime.DisposeAll();
        }

        private void OnDestroy()
        {
            VoxelDynamicNavGridRuntime.DisposeAll();
            VoxelDynamicNavGridRuntime.ClearLifecycleOwner(this);
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
