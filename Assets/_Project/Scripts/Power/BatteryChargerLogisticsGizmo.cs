#if UNITY_EDITOR
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Power
{
    [ExecuteAlways]
    [AddComponentMenu("Hecton/Debug/Battery Charger Logistics Gizmo")]
    public sealed class BatteryChargerLogisticsGizmo : MonoBehaviour
    {
        [SerializeField, Range(1, 512)] private int maxLinks = 128;
        [SerializeField] private bool drawWhenNotSelected = true;

        private void OnDrawGizmos()
        {
            if (!drawWhenNotSelected)
                return;

            DrawLinks();
        }

        private void OnDrawGizmosSelected()
        {
            DrawLinks();
        }

        private void DrawLinks()
        {
            if (!Application.isPlaying)
                return;

            int limit = math.max(1, maxLinks);
            for (int i = 0; i < limit; i++)
            {
                if (!BatteryChargerLogisticsRuntime.TryGetGizmoLink(i, out double3 chargerAup, out double3 nodeAup, out ChargerVisualStateDTO visual, out int count))
                {
                    if (i >= count)
                        break;
                    continue;
                }

                Vector3 a = HectonFloatingOrigin.ToRuntimePosition(chargerAup);
                Vector3 b = HectonFloatingOrigin.ToRuntimePosition(nodeAup);
                Gizmos.color = ResolveColor(in visual);
                Gizmos.DrawLine(a, b);
                Gizmos.DrawWireSphere(a, 0.08f);
            }
        }

        private static Color ResolveColor(in ChargerVisualStateDTO visual)
        {
            if ((visual.Flags & BatteryChargerLogisticsConstants.LinkFlagUnpowered) != 0u)
                return Color.red;

            if (visual.Status == 2u)
                return Color.yellow;

            if (visual.Status == 1u)
                return Color.green;

            return new Color(0.25f, 0.25f, 0.25f, 0.75f);
        }
    }
}
#endif
