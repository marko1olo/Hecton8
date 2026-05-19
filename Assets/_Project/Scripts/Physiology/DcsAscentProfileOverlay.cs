#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physiology
{
    [DisallowMultipleComponent]
    public sealed class DcsAscentProfileOverlay : MonoBehaviour
    {
        private static readonly GUIContent DiveComputerTitle = new GUIContent("DCS Dive Computer"); // COLD ALLOC: dev-only IMGUI label.

        [SerializeField] private bool showOverlay = true;
        [SerializeField] private Rect screenRect = new Rect(16f, 96f, 300f, 160f);

        private ShinobuPhysiologyRuntime _runtime;

        private void OnEnable()
        {
            _runtime = UnityEngine.Object.FindFirstObjectByType<ShinobuPhysiologyRuntime>();
        }

        private void OnGUI()
        {
            if (!showOverlay)
                return;

            if (_runtime == null || !_runtime.TryGetVitalsExport(out VitalsExportDTO export))
                return;

            GUI.Box(screenRect, DiveComputerTitle);
            Rect graph = new Rect(screenRect.x + 10f, screenRect.y + 24f, screenRect.width - 20f, screenRect.height - 36f);
            DrawLine(graph.xMin, graph.yMax, graph.xMax, graph.yMax, Color.gray);
            DrawLine(graph.xMin, graph.yMin, graph.xMin, graph.yMax, Color.gray);

            float currentDepth = math.max(0f, export.DepthMeters);
            float maxDepth = math.max(120f, currentDepth + 20f);
            float currentY = graph.yMax - math.saturate(currentDepth / maxDepth) * graph.height;
            DrawLine(graph.xMin, currentY, graph.xMax, currentY, Color.cyan);

            float worstCeiling = 0f;
            float ambient = 1f + currentDepth * 0.1f;
            for (int i = 0; i < ShinobuPhysiologyConstants.TissueCompartmentCount; i++)
            {
                if (!_runtime.TryGetTissueTension(0, i, out float tension, out float mValue))
                    continue;

                float ratio = math.max(1.01f, mValue * math.rcp(math.max(0.5f, ambient)));
                float ceilingAtm = tension * math.rcp(ratio);
                float ceilingDepth = math.max(0f, (ceilingAtm - 1f) * 10f);
                worstCeiling = math.max(worstCeiling, ceilingDepth);
                float x = graph.xMin + (i + 0.5f) * (graph.width / ShinobuPhysiologyConstants.TissueCompartmentCount);
                float y = graph.yMax - math.saturate(ceilingDepth / maxDepth) * graph.height;
                DrawLine(x, graph.yMax, x, y, Color.red);
            }

            float ceilingY = graph.yMax - math.saturate(worstCeiling / maxDepth) * graph.height;
            DrawLine(graph.xMin, ceilingY, graph.xMax, ceilingY, Color.yellow);
        }

        private static void DrawLine(float x0, float y0, float x1, float y1, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            Matrix4x4 matrix = GUI.matrix;
            float angle = math.degrees(math.atan2(y1 - y0, x1 - x0));
            float length = math.distance(new float2(x0, y0), new float2(x1, y1));
            GUIUtility.RotateAroundPivot(angle, new Vector2(x0, y0));
            GUI.DrawTexture(new Rect(x0, y0, length, 2f), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = previous;
        }
    }
}
#endif
