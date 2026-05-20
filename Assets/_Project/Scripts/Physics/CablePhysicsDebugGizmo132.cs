using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Physics/SHINOBU 132 Cable Gizmo")]
    public sealed class CablePhysicsDebugGizmo132 : MonoBehaviour
    {
        [SerializeField, Range(1, CablePhysics132Constants.MockTetherCount)]
        private int visibleCableCount = CablePhysics132Constants.MockTetherCount;

        [SerializeField, Range(0.01f, 0.5f)]
        private float nodeRadiusMeters = 0.075f;

        private void OnDrawGizmos()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            if (!vault.TryGetBufferHandle(CablePhysics132BufferIds.CableNodes, out VaultBufferHandle<CableNodeDTO> nodeHandle) ||
                !vault.TryGetBufferHandle(CablePhysics132BufferIds.CableConstraints, out VaultBufferHandle<TetherConstraintDTO> constraintHandle))
            {
                return;
            }

            NativeArray<CableNodeDTO> nodes = nodeHandle.Resolve(vault);
            NativeArray<TetherConstraintDTO> constraints = constraintHandle.Resolve(vault);
            if (!nodes.IsCreated || !constraints.IsCreated)
                return;

            int cableLimit = math.clamp(visibleCableCount, 1, CablePhysics132Constants.MockTetherCount);
            int nodeLimit = math.min(nodes.Length, cableLimit * CablePhysics132Constants.MockNodesPerTether);
            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            Gizmos.color = Color.red;
            for (int i = 0; i < nodeLimit; i++)
            {
                Gizmos.DrawSphere(ToRuntime(nodes[i].CurrentAUP, origin), nodeRadiusMeters);
            }

            Gizmos.color = Color.green;
            int constraintLimit = math.min(constraints.Length, cableLimit * (CablePhysics132Constants.MockNodesPerTether - 1));
            for (int i = 0; i < constraintLimit; i++)
            {
                TetherConstraintDTO constraint = constraints[i];
                if ((uint)constraint.NodeA >= (uint)nodeLimit || (uint)constraint.NodeB >= (uint)nodeLimit)
                    continue;

                Gizmos.DrawLine(
                    ToRuntime(nodes[constraint.NodeA].CurrentAUP, origin),
                    ToRuntime(nodes[constraint.NodeB].CurrentAUP, origin));
            }
        }

        private static Vector3 ToRuntime(double3 aup, double3 origin)
        {
            double3 local = aup - origin;
            double span = CablePhysics132Constants.SafeLocalAupSpanMeters;
            local = math.clamp(local, new double3(-span), new double3(span));
            return new Vector3((float)local.x, (float)local.y, (float)local.z);
        }
    }
}
