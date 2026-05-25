namespace Hecton8.Tools.Editor
{
    using System.Globalization;
    using Hecton8.Tools;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(UpgradeMatrixDebugGizmo))]
    public sealed class UpgradeMatrixDebugGizmoEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            UpgradeMatrixDebugGizmo gizmo = (UpgradeMatrixDebugGizmo)target;
            if (!gizmo.DrawGizmo)
                return;

            Vector3 labelPosition = gizmo.transform.position + (Vector3.up * 1.85f);
            string label = "Mask 0x" + gizmo.ActiveMask.ToString("X16") +
                           "\nDepth/Range: " + gizmo.Stat0.ToString("0.###", CultureInfo.InvariantCulture) +
                           "  Spd: " + gizmo.Stat3.ToString("0.###", CultureInfo.InvariantCulture) +
                           "\nEntity: 0x" + gizmo.EntityHashId.ToString("X8") +
                           " Equip: 0x" + gizmo.EquipmentHashId.ToString("X8");
            Handles.Label(labelPosition, label);
        }
    }
}
