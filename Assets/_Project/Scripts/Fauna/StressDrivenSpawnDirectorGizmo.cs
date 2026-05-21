#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI
{
    [DisallowMultipleComponent]
    public sealed class StressDrivenSpawnDirectorGizmo : MonoBehaviour
    {
        [SerializeField]
        private bool _drawHiddenSpawnHeatmap = true;

        [SerializeField, Range(0.25f, 8f)]
        private float _radiusScale = 1f;

        private static readonly DirectorTelemetryEntry[] TelemetryScratch =
            new DirectorTelemetryEntry[StressDrivenSpawnDirector.TelemetryCapacity];

        private void OnDrawGizmos()
        {
            if (!_drawHiddenSpawnHeatmap ||
                !StressDrivenSpawnDirector.TryGetLatestSpawnDebug(out DirectorSpawnDebugDTO debug))
            {
                return;
            }

            if (StressDrivenSpawnDirector.TryGetLatestTelemetry(out DirectorTelemetryEntry latest) &&
                IsFinite(in latest.PlayerAup))
            {
                Vector3 player = ToRuntimePosition(in latest.PlayerAup);
                Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.45f);
                Gizmos.DrawWireSphere(player, Mathf.Max(1f, debug.MinHiddenRadiusMeters * _radiusScale));
                Gizmos.color = new Color(1f, 0.75f, 0.15f, 0.35f);
                Gizmos.DrawWireSphere(player, Mathf.Max(1f, debug.DespawnRadiusMeters * _radiusScale));
            }

            Vector3 center = ToRuntimePosition(in debug.SpawnAup);
            float radius = Mathf.Max(0.5f, debug.RadiusMeters * 0.08f * _radiusScale);
            Gizmos.color = ResolveColor(debug.Flags);
            Gizmos.DrawWireSphere(center, radius);
            Gizmos.DrawLine(center, center + Vector3.up * Mathf.Min(12f, radius * 0.5f));

            int count = StressDrivenSpawnDirector.CopyTelemetrySnapshot(TelemetryScratch);
            Gizmos.color = Color.red;
            for (int i = 0; i < count; i++)
            {
                DirectorTelemetryEntry entry = TelemetryScratch[i];
                if (entry.Spawned == 0 || !IsFinite(in entry.LastSpawnAup))
                    continue;

                Vector3 point = ToRuntimePosition(in entry.LastSpawnAup);
                Gizmos.DrawSphere(point, Mathf.Max(0.3f, 1.25f * _radiusScale));
            }
        }

        private static Color ResolveColor(uint flags)
        {
            if ((flags & StressDrivenSpawnDirector.SelectionFlagFault) != 0u)
                return Color.magenta;
            if ((flags & StressDrivenSpawnDirector.SelectionFlagLootMissing) != 0u)
                return new Color(1f, 0.45f, 0.12f);
            if ((flags & StressDrivenSpawnDirector.SelectionFlagSpawnHidden) != 0u)
                return new Color(0.15f, 0.9f, 0.35f);
            return Color.yellow;
        }

        private static bool IsFinite(in AbsoluteUniversePositionBlit128 value)
        {
            return math.all(math.isfinite(value.Local));
        }

        private static Vector3 ToRuntimePosition(in AbsoluteUniversePositionBlit128 aup)
        {
            const double cellSize = HectonPhysicsContract.AupSectorSizeMetersDouble;
            double3 absolute = new double3(
                (aup.GridX * cellSize) + aup.Local.x,
                (aup.GridY * cellSize) + aup.Local.y,
                (aup.GridZ * cellSize) + aup.Local.z);
            double3 delta = absolute - HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!math.all(math.isfinite(delta)))
                return Vector3.zero;

            return new Vector3((float)delta.x, (float)delta.y, (float)delta.z);
        }
    }
}
#endif
