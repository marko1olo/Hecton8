using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World.ProceduralWreckage.Editor
{
    [DisallowMultipleComponent]
    public sealed class ProceduralWreckageDebugGizmo : MonoBehaviour
    {
        [SerializeField, Tooltip("Draws live WFC cell states from the GlobalDataVault in the Scene view.")]
        private bool drawDebugCells = true;

        private static ProceduralWreckageVaultHandles _handles;
        private static bool _hasHandles;

        private void OnDrawGizmos()
        {
            if (!drawDebugCells)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            if (!_hasHandles || !_handles.IsCreated())
                _hasHandles = ProceduralWreckageVault.TryResolveExisting(vault, out _handles);

            if (!_hasHandles ||
                !ProceduralWreckageVault.TryResolveViews(vault, ref _handles, out ProceduralWreckageVaultBuffers buffers) ||
                !buffers.DebugCells.IsCreated)
            {
                return;
            }

            int count = math.min(buffers.DebugCells.Length, ProceduralWreckageConstants.MaxDebugCells);
            for (int i = 0; i < count; i++)
            {
                WreckageDebugCellDTO cell = buffers.DebugCells[i];
                if (cell.SectorHash == 0u)
                    continue;

                Gizmos.color = ResolveColor(cell.State);
                Vector3 center = HectonFloatingOrigin.ToRuntimePosition(cell.CenterAUP, HectonFloatingOrigin.CurrentTotalOffsetDouble);
                Vector3 size = new Vector3(cell.Extents.x * 2f, cell.Extents.y * 2f, cell.Extents.z * 2f);
                Gizmos.DrawWireCube(center, size);
            }
        }

        private static Color ResolveColor(byte state)
        {
            if (state == 1)
                return Color.green;
            if (state == 2)
                return Color.red;
            return Color.yellow;
        }
    }
}
