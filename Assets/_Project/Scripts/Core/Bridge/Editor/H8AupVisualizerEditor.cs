using System.Globalization;
using Hecton8.Core.Contracts;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Core.Bridge.EditorTools
{
    public sealed class H8AupVisualizerWindow : EditorWindow
    {
        private const string EnabledKey = "H8.Bridge.AUP.Enabled";
        private const string SectorXKey = "H8.Bridge.AUP.SectorX";
        private const string SectorYKey = "H8.Bridge.AUP.SectorY";
        private const string SectorZKey = "H8.Bridge.AUP.SectorZ";
        internal const int CellSizeMeters = HectonPhysicsContract.AupSectorSizeMetersInt;

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

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            UnityEngine.UIElements.Toggle enabled = new UnityEngine.UIElements.Toggle("Draw Sector Grid")
            {
                value = EditorPrefs.GetBool(EnabledKey, true)
            };
            enabled.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetBool(EnabledKey, evt.newValue);
                SceneView.RepaintAll();
            });
            root.Add(enabled);

            LongField sectorX = new LongField("Sector X")
            {
                value = GetLong(SectorXKey)
            };
            sectorX.RegisterValueChangedCallback(evt => SetLongAndRepaint(SectorXKey, evt.newValue));
            root.Add(sectorX);

            LongField sectorY = new LongField("Sector Y")
            {
                value = GetLong(SectorYKey)
            };
            sectorY.RegisterValueChangedCallback(evt => SetLongAndRepaint(SectorYKey, evt.newValue));
            root.Add(sectorY);

            LongField sectorZ = new LongField("Sector Z")
            {
                value = GetLong(SectorZKey)
            };
            sectorZ.RegisterValueChangedCallback(evt => SetLongAndRepaint(SectorZKey, evt.newValue));
            root.Add(sectorZ);

            Button zeroCameraButton = new Button(ZeroSceneCamera)
            {
                text = "Zero Scene Camera"
            };
            zeroCameraButton.style.marginTop = 6f;
            root.Add(zeroCameraButton);
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

            Vector3 pivot = ResolveScenePivot(SectorX, SectorY, SectorZ);
            view.pivot = pivot;
            view.Repaint();
        }

        internal static Vector3 ResolveScenePivot(long sectorX, long sectorY, long sectorZ)
        {
            return new Vector3(
                ClampSectorToSceneCoordinate(sectorX),
                ClampSectorToSceneCoordinate(sectorY),
                ClampSectorToSceneCoordinate(sectorZ));
        }

        private static float ClampSectorToSceneCoordinate(long sector)
        {
            const double cellSize = CellSizeMeters;
            const double maxSceneCoordinate = 10000000.0;
            double value = sector * cellSize;
            if (double.IsNaN(value))
                return 0f;

            if (value > maxSceneCoordinate)
                return (float)maxSceneCoordinate;
            if (value < -maxSceneCoordinate)
                return (float)-maxSceneCoordinate;

            return (float)value;
        }

        private static void SetLongAndRepaint(string key, long value)
        {
            SetLong(key, value);
            SceneView.RepaintAll();
        }

        private static long GetLong(string key)
        {
            return long.TryParse(
                EditorPrefs.GetString(key, "0"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long value)
                ? value
                : 0L;
        }

        private static void SetLong(string key, long value)
        {
            EditorPrefs.SetString(key, value.ToString(CultureInfo.InvariantCulture));
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

            Vector3 center = H8AupVisualizerWindow.ResolveScenePivot(sectorX, sectorY, sectorZ);
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
        }
    }
}
