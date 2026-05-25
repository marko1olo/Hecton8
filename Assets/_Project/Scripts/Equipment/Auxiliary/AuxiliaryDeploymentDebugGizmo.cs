using System.Globalization;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Equipment.Auxiliary
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Equipment/Auxiliary Deployment Debug Gizmo")]
    public sealed class AuxiliaryDeploymentDebugGizmo : MonoBehaviour
    {
#if UNITY_EDITOR
        [SerializeField] private bool drawLabels = true;
        [SerializeField, Range(1, AuxiliaryEquipmentConstants.MaxDeployedAuxiliaries)] private int maxDrawn = 256;

        private void OnDrawGizmos()
        {
            if (!AuxiliaryEquipmentRouterRuntime.TryReadDeployments(out var deployments, out int activeCount) ||
                !deployments.IsCreated)
            {
                return;
            }

            int drawCount = math.min(math.min(maxDrawn, deployments.Length), math.max(activeCount, 0));
            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            for (int i = 0; i < drawCount; i++)
            {
                DeployedAuxiliaryDTO deployment = deployments[i];
                if (deployment.PrefabHashID == 0u || deployment.RemainingLifetime <= 0f)
                    continue;

                Gizmos.color = ResolveColor(deployment.PrefabHashID);
                double3 localDelta = AupPrecisionMath.LocalDeltaDouble(deployment.AUP_Position, origin);
                float3 local = AupPrecisionMath.DowncastLocalDelta(localDelta, float3.zero);
                Vector3 position = new Vector3(local.x, local.y, local.z);
                Gizmos.DrawWireSphere(position, ResolveRadius(deployment.PrefabHashID));
                if (drawLabels)
                    UnityEditor.Handles.Label(position + Vector3.up * 0.35f, deployment.RemainingLifetime.ToString("0.0", CultureInfo.InvariantCulture));
            }
        }

        private static Color ResolveColor(uint prefabHash)
        {
            if (prefabHash == AuxiliaryEquipmentConstants.FlarePrefabHash)
                return Color.red;
            if (prefabHash == AuxiliaryEquipmentConstants.SensorPingPrefabHash)
                return Color.blue;
            if (prefabHash == AuxiliaryEquipmentConstants.GravityTetherPrefabHash)
                return Color.green;
            return Color.magenta;
        }

        private static float ResolveRadius(uint prefabHash)
        {
            if (prefabHash == AuxiliaryEquipmentConstants.SensorPingPrefabHash)
                return 1.25f;
            if (prefabHash == AuxiliaryEquipmentConstants.GravityTetherPrefabHash)
                return 0.75f;
            return 0.5f;
        }
#endif
    }
}
