using UnityEngine;

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
    }
}
