using UnityEditor;
using UnityEngine;

namespace Hecton8.Core.Bridge.EditorTools
{
    public sealed class H8AupVisualizerWindow : EditorWindow
    {
        private const string EnabledKey = "H8.Bridge.AUP.Enabled";
        private const string SectorXKey = "H8.Bridge.AUP.SectorX";
        private const string SectorYKey = "H8.Bridge.AUP.SectorY";
        private const string SectorZKey = "H8.Bridge.AUP.SectorZ";
        internal const int CellSizeMeters = 5000;

        [MenuItem("Hecton-8/Bridge/AUP Visualizer")]
        public static void Open()
        {
            GetWindow<H8AupVisualizerWindow>("AUP Visualizer");
        }

        [MenuItem("Hecton-8/Bridge/AUP Visualizer/Toggle Grid")]
        public static void Toggle()
        {
            EditorPrefs.SetBool(EnabledKey, !EditorPrefs.GetBool(EnabledKey, true));
            SceneView.RepaintAll();
        }

        [MenuItem("Hecton-8/Bridge/AUP Visualizer/Zero Camera To Sector")]
        public static void ZeroCameraMenu()
        {
            ZeroSceneCamera();
        }

        private void OnGUI()
        {
            bool enabled = EditorGUILayout.Toggle("Draw Sector Grid", EditorPrefs.GetBool(EnabledKey, true));
            EditorPrefs.SetBool(EnabledKey, enabled);
            long x = EditorGUILayout.LongField("Sector X", GetLong(SectorXKey));
            long y = EditorGUILayout.LongField("Sector Y", GetLong(SectorYKey));
            long z = EditorGUILayout.LongField("Sector Z", GetLong(SectorZKey));
            SetLong(SectorXKey, x);
            SetLong(SectorYKey, y);
            SetLong(SectorZKey, z);

            if (GUILayout.Button("Zero Scene Camera"))
                ZeroSceneCamera();
        }

        internal static bool Enabled => EditorPrefs.GetBool(EnabledKey, true);
        internal static long SectorX => GetLong(SectorXKey);
        internal static long SectorY => GetLong(SectorYKey);
        internal static long SectorZ => GetLong(SectorZKey);

        internal static void ZeroSceneCamera()
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null)
                return;

            const double cellSize = CellSizeMeters;
            Vector3 pivot = new Vector3(
                (float)(SectorX * cellSize),
                (float)(SectorY * cellSize),
                (float)(SectorZ * cellSize));
            view.pivot = pivot;
            view.Repaint();
        }

        private static long GetLong(string key)
        {
            return long.TryParse(EditorPrefs.GetString(key, "0"), out long value) ? value : 0L;
        }

        private static void SetLong(string key, long value)
        {
            EditorPrefs.SetString(key, value.ToString());
        }
    }

    [InitializeOnLoad]
    internal static class H8AupSceneGridDrawer
    {
        static H8AupSceneGridDrawer()
        {
            SceneView.duringSceneGui -= Draw;
            SceneView.duringSceneGui += Draw;
        }

        private static void Draw(SceneView sceneView)
        {
            if (!H8AupVisualizerWindow.Enabled)
                return;

            const float cellSize = H8AupVisualizerWindow.CellSizeMeters;
            const int radius = 2;
            long sectorX = H8AupVisualizerWindow.SectorX;
            long sectorY = H8AupVisualizerWindow.SectorY;
            long sectorZ = H8AupVisualizerWindow.SectorZ;

            Vector3 center = new Vector3(sectorX * cellSize, sectorY * cellSize, sectorZ * cellSize);
            Handles.color = new Color(0.2f, 0.85f, 1f, 0.35f);

            for (int x = -radius; x <= radius; x++)
            {
                float worldX = center.x + (x * cellSize);
                Handles.DrawLine(
                    new Vector3(worldX, center.y, center.z - radius * cellSize),
                    new Vector3(worldX, center.y, center.z + radius * cellSize));
            }

            for (int z = -radius; z <= radius; z++)
            {
                float worldZ = center.z + (z * cellSize);
                Handles.DrawLine(
                    new Vector3(center.x - radius * cellSize, center.y, worldZ),
                    new Vector3(center.x + radius * cellSize, center.y, worldZ));
            }

            Handles.color = new Color(1f, 0.8f, 0.2f, 0.65f);
            Handles.DrawWireCube(center, new Vector3(cellSize, cellSize * 0.1f, cellSize));

            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(12f, 72f, 180f, 42f), EditorStyles.helpBox);
            if (GUILayout.Button("Zero Camera"))
                H8AupVisualizerWindow.ZeroSceneCamera();
            GUILayout.EndArea();
            Handles.EndGUI();
        }
    }
}
