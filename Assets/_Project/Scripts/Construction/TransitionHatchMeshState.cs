using UnityEngine;

namespace Hecton8.Construction
{
    [DisallowMultipleComponent]
    internal sealed class TransitionHatchMeshState : MonoBehaviour
    {
        private const byte HasAdjacentMask = 1 << 0;
        private const byte AdjacentFloodedMask = 1 << 1;
        private const byte AdjacentRupturedMask = 1 << 2;
        private const byte EmergencyLockdownMask = 1 << 3;
        private const byte StateUnknown = byte.MaxValue;
        private const byte StateClosed = 0;
        private const byte StateOpen = 1;
        private const byte StateEmergency = 2;

        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private Mesh closedMesh;
        [SerializeField] private Mesh openMesh;
        [SerializeField] private Mesh emergencyMesh;
        [SerializeField] private GameObject closedRoot;
        [SerializeField] private GameObject openRoot;
        [SerializeField] private GameObject emergencyRoot;

        private byte _currentState = StateUnknown;

        private void Awake()
        {
            CacheMeshFilterCold();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheMeshFilterCold();
        }
#endif

        private void CacheMeshFilterCold()
        {
            if (meshFilter == null)
                TryGetComponent(out meshFilter);
        }

        internal static byte BuildAdjacentFlags(
            bool hasAdjacent,
            bool adjacentFlooded,
            bool adjacentRuptured,
            bool emergencyLockdown)
        {
            byte flags = 0;
            if (hasAdjacent)
                flags |= HasAdjacentMask;
            if (adjacentFlooded)
                flags |= AdjacentFloodedMask;
            if (adjacentRuptured)
                flags |= AdjacentRupturedMask;
            if (emergencyLockdown)
                flags |= EmergencyLockdownMask;

            return flags;
        }

        internal void ApplyAdjacentFlags(byte adjacentFlags)
        {
            byte nextState = ResolveState(adjacentFlags);
            if (_currentState == nextState)
                return;

            _currentState = nextState;
            ApplyMesh(nextState);
            ApplyRoots(nextState);
        }

        private static byte ResolveState(byte adjacentFlags)
        {
            bool hasAdjacent = (adjacentFlags & HasAdjacentMask) != 0;
            bool emergency = (adjacentFlags & (AdjacentFloodedMask | AdjacentRupturedMask | EmergencyLockdownMask)) != 0;
            if (!hasAdjacent)
                return StateClosed;

            return emergency ? StateEmergency : StateOpen;
        }

        private void ApplyMesh(byte state)
        {
            if (meshFilter == null)
                return;

            Mesh nextMesh = state == StateOpen
                ? openMesh
                : state == StateEmergency
                    ? emergencyMesh
                    : closedMesh;
            if (nextMesh != null && !ReferenceEquals(meshFilter.sharedMesh, nextMesh))
                meshFilter.sharedMesh = nextMesh;
        }

        private void ApplyRoots(byte state)
        {
            SetRootActive(closedRoot, state == StateClosed);
            SetRootActive(openRoot, state == StateOpen);
            SetRootActive(emergencyRoot, state == StateEmergency);
        }

        private static void SetRootActive(GameObject root, bool active)
        {
            if (root != null && root.activeSelf != active)
                root.SetActive(active);
        }
    }
}
