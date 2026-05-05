using UnityEngine;
using Hecton8.Core;

namespace Hecton8.World
{
    /// <summary>
    /// Unity lifecycle bridge for the static voxel navgrid runtime native containers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoxelDynamicNavGridRuntimeLifecycle : MonoBehaviour
    {
        private void OnDisable()
        {
            VoxelDynamicNavGridRuntime.DisposeAll();
        }

        private void OnDestroy()
        {
            VoxelDynamicNavGridRuntime.DisposeAll();
            VoxelDynamicNavGridRuntime.ClearLifecycleOwner(this);
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
