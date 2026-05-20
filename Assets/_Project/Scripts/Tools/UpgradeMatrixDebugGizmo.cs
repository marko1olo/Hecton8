namespace Hecton8.Tools
{
    using UnityEngine;

    /// <summary>
    /// Editor-facing scene marker for branchless upgrade matrix debug state.
    /// Runtime logic does not consume this component.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Tools/Upgrade Matrix Debug Gizmo")]
    public sealed class UpgradeMatrixDebugGizmo : MonoBehaviour
    {
        [Header("Upgrade Matrix")]
        [Tooltip("Draw the editor-only matrix debug marker.")]
        [SerializeField] private bool drawGizmo = true;

        [Tooltip("Entity hash mirrored from UpgradeMaskDTO for debug visualization.")]
        [SerializeField] private uint entityHashId;

        [Tooltip("Equipment hash mirrored from UpgradeMaskDTO for debug visualization.")]
        [SerializeField] private uint equipmentHashId;

        [Tooltip("Low 32 bits of the 64-bit upgrade mask for inspector editing.")]
        [SerializeField] private uint maskLow;

        [Tooltip("High 32 bits of the 64-bit upgrade mask for inspector editing.")]
        [SerializeField] private uint maskHigh;

        [Header("Compiled Stats")]
        [Tooltip("Primary compiled stat lane, e.g. depth or range.")]
        [SerializeField] private float stat0;

        [Tooltip("Secondary compiled stat lane, e.g. speed multiplier.")]
        [SerializeField] private float stat3 = 1f;

        public bool DrawGizmo => drawGizmo;
        public uint EntityHashId => entityHashId;
        public uint EquipmentHashId => equipmentHashId;
        public ulong ActiveMask => ((ulong)maskHigh << 32) | maskLow;
        public float Stat0 => stat0;
        public float Stat3 => stat3;

        private void OnDrawGizmos()
        {
            if (!drawGizmo)
                return;

            int activeBits = UpgradeMatrixCompiler.PopCount64(ActiveMask);
            float heat = Mathf.Clamp01(activeBits * 0.0625f);
            Gizmos.color = Color.Lerp(new Color(0.1f, 0.7f, 1f, 0.8f), new Color(1f, 0.35f, 0.08f, 0.9f), heat);
            Vector3 origin = transform.position + (Vector3.up * 1.25f);
            Gizmos.DrawWireSphere(origin, 0.35f + heat * 0.2f);
            Gizmos.DrawLine(transform.position, origin);
        }
    }
}
