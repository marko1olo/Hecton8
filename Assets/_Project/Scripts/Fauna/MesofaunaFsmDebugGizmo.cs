#if UNITY_EDITOR
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI
{
    [DisallowMultipleComponent]
    public sealed class MesofaunaFsmDebugGizmo : MonoBehaviour
    {
        private const int GizmoCapacity = 128;
        // COLD ALLOC: Vector3[GizmoCapacity] - editor-only mesofauna gizmo origins - owner: MesofaunaFsmDebugGizmo
        private static readonly Vector3[] Origins = new Vector3[GizmoCapacity];
        // COLD ALLOC: Vector3[GizmoCapacity] - editor-only mesofauna desired velocity staging - owner: MesofaunaFsmDebugGizmo
        private static readonly Vector3[] DesiredVelocities = new Vector3[GizmoCapacity];
        // COLD ALLOC: Vector3[GizmoCapacity] - editor-only mesofauna target vector staging - owner: MesofaunaFsmDebugGizmo
        private static readonly Vector3[] TargetVectors = new Vector3[GizmoCapacity];
        // COLD ALLOC: byte[GizmoCapacity] - editor-only mesofauna state staging - owner: MesofaunaFsmDebugGizmo
        private static readonly byte[] States = new byte[GizmoCapacity];
        // COLD ALLOC: uint[GizmoCapacity] - editor-only mesofauna target hash staging - owner: MesofaunaFsmDebugGizmo
        private static readonly uint[] TargetHashes = new uint[GizmoCapacity];

        [SerializeField]
        [Tooltip("Draw mesofauna finite-state vectors in the Scene view.")]
        private bool _drawGizmos = true;

        [SerializeField, Range(1, GizmoCapacity)]
        [Tooltip("Maximum predator debug vectors drawn by this editor-only hook.")]
        private int _maxPredators = 64;

        private void OnDrawGizmos()
        {
            if (!_drawGizmos)
                return;

            int count = PredatorCognitionDomain.CopyMesofaunaDebugGizmos(
                Origins,
                DesiredVelocities,
                TargetVectors,
                States,
                TargetHashes,
                math.clamp(_maxPredators, 1, GizmoCapacity));
            for (int i = 0; i < count; i++)
            {
                Vector3 origin = Origins[i];
                Gizmos.color = ResolveStateColor(States[i]);
                Gizmos.DrawLine(origin, origin + Vector3.ClampMagnitude(DesiredVelocities[i], 10f));
                Gizmos.DrawWireSphere(origin, 0.35f);

                Gizmos.color = Color.white;
                Gizmos.DrawLine(origin, origin + Vector3.ClampMagnitude(TargetVectors[i], 12f));
            }
        }

        private static Color ResolveStateColor(byte state)
        {
            switch (state)
            {
                case MesofaunaBehaviorConstants.StateHunt:
                    return Color.red;
                case MesofaunaBehaviorConstants.StateFlee:
                    return Color.cyan;
                case MesofaunaBehaviorConstants.StateTrackScent:
                    return Color.green;
                case MesofaunaBehaviorConstants.StateIdle:
                    return Color.blue;
                default:
                    return Color.yellow;
            }
        }
    }
}
#endif
