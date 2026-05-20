using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World.ProceduralCoral.Editor
{
    [DisallowMultipleComponent]
    public sealed class ProceduralCoralDebugGizmo : MonoBehaviour
    {
        [SerializeField, Tooltip("Draws live coral branch segments from the GlobalDataVault in the Scene view.")]
        private bool drawDebugSegments = true;

        private static ProceduralCoralVaultHandles _handles;
        private static bool _hasHandles;

        private void OnDrawGizmos()
        {
            if (!drawDebugSegments)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            if (!_hasHandles || !_handles.IsCreated())
                _hasHandles = ProceduralCoralVault.TryResolveExisting(vault, out _handles);

            if (!_hasHandles ||
                !ProceduralCoralVault.TryResolveViews(vault, ref _handles, out ProceduralCoralVaultBuffers buffers) ||
                !buffers.DebugSegments.IsCreated)
            {
                return;
            }

            int count = buffers.Counters.IsCreated && buffers.Counters.Length > 0
                ? math.clamp(buffers.Counters[0].BranchCount, 0, buffers.DebugSegments.Length)
                : buffers.DebugSegments.Length;
            for (int i = 0; i < count; i++)
            {
                CoralDebugSegmentDTO segment = buffers.DebugSegments[i];
                Gizmos.color = ResolveColor(segment.StateFlags, segment.GenerationDepth);
                Vector3 start = new Vector3((float)segment.StartAUP.x, (float)segment.StartAUP.y, (float)segment.StartAUP.z);
                Vector3 end = new Vector3((float)segment.EndAUP.x, (float)segment.EndAUP.y, (float)segment.EndAUP.z);
                Gizmos.DrawLine(start, end);
                if ((segment.StateFlags & CoralBranchFlags.Tip) != 0)
                    Gizmos.DrawWireSphere(end, 0.18f);
            }
        }

        private static Color ResolveColor(uint flags, uint depth)
        {
            if ((flags & CoralBranchFlags.CollisionPruned) != 0)
                return Color.red;
            if ((flags & CoralBranchFlags.Tip) != 0)
                return Color.cyan;
            if (depth == 0u)
                return Color.green;
            return Color.yellow;
        }
    }
}
