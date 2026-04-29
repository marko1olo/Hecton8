using System.Collections.Generic;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class WorldProceduralScatterPreviewGizmoDrawer
    {
        private static readonly List<WorldProceduralScatterDirector.ScatterPreviewGizmoRecord> _records =
            new List<WorldProceduralScatterDirector.ScatterPreviewGizmoRecord>(512); // COLD ALLOC: List<ScatterPreviewGizmoRecord>[512] - SceneView scatter preview gizmo cache - owner: WorldProceduralScatterPreviewGizmoDrawer

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawScatterPreviewGizmos(WorldProceduralScatterDirector director, GizmoType gizmoType)
        {
            if (director == null)
                return;

            director.BuildScatterPreviewGizmoSnapshot(_records);
            int count = _records.Count;
            for (int i = 0; i < count; i++)
            {
                WorldProceduralScatterDirector.ScatterPreviewGizmoRecord record = _records[i];
                float radius = Mathf.Max(0.25f, record.SpacingRadius);
                Handles.color = ResolveColor(record);
                Handles.DrawWireDisc(record.Position, Vector3.up, radius);
                Handles.DrawWireDisc(record.Position, Vector3.up, Mathf.Max(0.18f, radius * 0.35f));

                Vector3 tiltStem = record.Position + (Vector3.up * Mathf.Max(0.5f, record.MaxTiltAngleDegrees * 0.02f));
                Handles.DrawLine(record.Position, tiltStem);
            }
        }

        private static Color ResolveColor(WorldProceduralScatterDirector.ScatterPreviewGizmoRecord record)
        {
            Color layerColor = record.ScatterLayer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Ground => new Color(0.26f, 0.92f, 0.71f, 0.88f),
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => new Color(0.24f, 0.71f, 1f, 0.9f),
                WorldPrefabFamilyProfile.ScatterLayer.Structure => new Color(1f, 0.66f, 0.19f, 0.9f),
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => new Color(1f, 0.33f, 0.27f, 0.92f),
                _ => new Color(0.85f, 0.85f, 0.85f, 0.8f)
            };

            if ((record.Substrate & WorldProceduralPlacementRule.FloraSubstrateMask.Rock) != 0 &&
                (record.Substrate & WorldProceduralPlacementRule.FloraSubstrateMask.Sand) == 0)
            {
                return Color.Lerp(layerColor, new Color(0.62f, 0.62f, 0.7f, layerColor.a), 0.45f);
            }

            if ((record.Substrate & WorldProceduralPlacementRule.FloraSubstrateMask.Sand) != 0 &&
                (record.Substrate & WorldProceduralPlacementRule.FloraSubstrateMask.Rock) == 0)
            {
                return Color.Lerp(layerColor, new Color(0.93f, 0.82f, 0.46f, layerColor.a), 0.35f);
            }

            return layerColor;
        }
    }
}
