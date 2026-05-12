// ============================================================================
// HECTON-8 — ModuleSocket.cs
// Canonical module socket contract for strict inverse-direction habitat snapping.
// ============================================================================

using UnityEngine;

namespace Hecton8.Building
{
    public enum ModuleSocketDirection : byte
    {
        North = 0,
        South = 1,
        East = 2,
        West = 3,
        Top = 4,
        Bottom = 5
    }

    [System.Flags]
    public enum ModuleSocketMask : byte
    {
        None = 0,
        North = 1 << 0,
        South = 1 << 1,
        East = 1 << 2,
        West = 1 << 3,
        Top = 1 << 4,
        Bottom = 1 << 5
    }

    internal static class ModuleSocketTopology
    {
        internal static ModuleSocketMask ToMask(ModuleSocketDirection direction)
        {
            switch (direction)
            {
                case ModuleSocketDirection.North: return ModuleSocketMask.North;
                case ModuleSocketDirection.South: return ModuleSocketMask.South;
                case ModuleSocketDirection.East: return ModuleSocketMask.East;
                case ModuleSocketDirection.West: return ModuleSocketMask.West;
                case ModuleSocketDirection.Top: return ModuleSocketMask.Top;
                case ModuleSocketDirection.Bottom: return ModuleSocketMask.Bottom;
                default: return ModuleSocketMask.None;
            }
        }

        internal static bool AreInverseDirections(ModuleSocketDirection lhs, ModuleSocketDirection rhs)
        {
            switch (lhs)
            {
                case ModuleSocketDirection.North: return rhs == ModuleSocketDirection.South;
                case ModuleSocketDirection.South: return rhs == ModuleSocketDirection.North;
                case ModuleSocketDirection.East: return rhs == ModuleSocketDirection.West;
                case ModuleSocketDirection.West: return rhs == ModuleSocketDirection.East;
                case ModuleSocketDirection.Top: return rhs == ModuleSocketDirection.Bottom;
                case ModuleSocketDirection.Bottom: return rhs == ModuleSocketDirection.Top;
                default: return false;
            }
        }

        internal static bool AreCompatible(
            string lhsCompatibleType,
            ModuleSocketDirection lhsDirection,
            string rhsCompatibleType,
            ModuleSocketDirection rhsDirection)
        {
            if (!AreInverseDirections(lhsDirection, rhsDirection))
                return false;

            if (string.IsNullOrEmpty(lhsCompatibleType) || string.IsNullOrEmpty(rhsCompatibleType))
                return true;

            return string.Equals(lhsCompatibleType, rhsCompatibleType, System.StringComparison.OrdinalIgnoreCase);
        }

        internal static ModuleSocketDirection QuantizeDirection(Vector3 localPosition)
        {
            if (float.IsNaN(localPosition.x) ||
                float.IsNaN(localPosition.y) ||
                float.IsNaN(localPosition.z) ||
                float.IsInfinity(localPosition.x) ||
                float.IsInfinity(localPosition.y) ||
                float.IsInfinity(localPosition.z))
            {
                localPosition = Vector3.forward;
            }

            float absX = Mathf.Abs(localPosition.x);
            float absY = Mathf.Abs(localPosition.y);
            float absZ = Mathf.Abs(localPosition.z);
            if ((absX + absY + absZ) <= 0.0001f)
            {
                localPosition = Vector3.forward;
                absX = 0f;
                absY = 0f;
                absZ = 1f;
            }

            if (absX >= absY && absX >= absZ)
                return localPosition.x >= 0f ? ModuleSocketDirection.East : ModuleSocketDirection.West;

            if (absY >= absX && absY >= absZ)
                return localPosition.y >= 0f ? ModuleSocketDirection.Top : ModuleSocketDirection.Bottom;

            return localPosition.z >= 0f ? ModuleSocketDirection.North : ModuleSocketDirection.South;
        }

        internal static Quaternion RotationFromDirection(ModuleSocketDirection direction)
        {
            switch (direction)
            {
                case ModuleSocketDirection.North: return Quaternion.LookRotation(Vector3.forward, Vector3.up);
                case ModuleSocketDirection.South: return Quaternion.LookRotation(Vector3.back, Vector3.up);
                case ModuleSocketDirection.East: return Quaternion.LookRotation(Vector3.right, Vector3.up);
                case ModuleSocketDirection.West: return Quaternion.LookRotation(Vector3.left, Vector3.up);
                case ModuleSocketDirection.Top: return Quaternion.LookRotation(Vector3.up, Vector3.forward);
                case ModuleSocketDirection.Bottom: return Quaternion.LookRotation(Vector3.down, Vector3.forward);
                default: return Quaternion.identity;
            }
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("HECTON-8/Building/Module Socket")]
    public sealed class ModuleSocket : MonoBehaviour
    {
        [Header("── Socket Settings ──────────────────")]
        [Tooltip("Semantic compatibility lane. Empty = universal socket.")]
        [SerializeField] private string compatibleType = string.Empty;

        [Tooltip("Canonical socket direction used by strict inverse-socket snapping and graph math.")]
        [SerializeField] private ModuleSocketDirection direction = ModuleSocketDirection.North;

        private bool _isOccupied;

        /// <summary>Socket is already consumed by another placed module.</summary>
        public bool IsOccupied => _isOccupied;

        /// <summary>Semantic compatibility lane. Empty = universal socket.</summary>
        public string CompatibleType => compatibleType;

        /// <summary>Canonical socket direction used by strict inverse-socket snapping.</summary>
        public ModuleSocketDirection Direction => direction;

        /// <summary>Bitmask representation of the authored socket direction.</summary>
        public ModuleSocketMask DirectionMask => ModuleSocketTopology.ToMask(direction);

        /// <summary>Marks this socket as occupied or free.</summary>
        public void SetOccupied(bool occupied)
        {
            _isOccupied = occupied;
        }

        /// <summary>Cold-path runtime initialization for generated proxy sockets.</summary>
        public void ConfigureRuntime(string runtimeCompatibleType, ModuleSocketDirection runtimeDirection, bool occupied = false)
        {
            compatibleType = runtimeCompatibleType ?? string.Empty;
            direction = runtimeDirection;
            _isOccupied = occupied;
        }

        /// <summary>
        /// Returns true when the supplied socket is the valid inverse connection target.
        /// </summary>
        public bool CanConnectTo(ModuleSocket other)
        {
            if (other == null)
                return false;

            return ModuleSocketTopology.AreCompatible(
                compatibleType,
                direction,
                other.compatibleType,
                other.direction);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            compatibleType ??= string.Empty;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = _isOccupied
                ? new Color(1f, 0.2f, 0.2f, 0.6f)
                : new Color(0f, 1f, 0.5f, 0.6f);

            Gizmos.DrawWireSphere(transform.position, 0.15f);

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.5f);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + transform.up * 0.3f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, 2f);
        }
#endif
    }
}
