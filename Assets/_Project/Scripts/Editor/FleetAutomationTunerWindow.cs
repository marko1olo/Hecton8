#if UNITY_EDITOR
using Hecton8.Construction;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorTools
{
    public sealed class FleetAutomationTunerWindow : EditorWindow
    {
        private static readonly DroneFleetDebugRoute[] DebugRoutes = new DroneFleetDebugRoute[DroneFleetAutomationFacade.MaxDebugRoutes];

        private DroneFleetTuningConstants _tuning;
        private string _csvPath = "drone_chassis_specs.csv";
        private string _status = "Fleet not sampled.";
        private long _lastCsvTicks;
        private bool _autoMonitorCsv = true;
        private bool _drawRoutes = true;
        private IMGUIContainer _inspectorContainer;

        [MenuItem("Hecton8/AI/Drone Fleet Tuner")]
        private static void Open()
        {
            GetWindow<FleetAutomationTunerWindow>("Drone Fleet");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawFleetRoutes;
            SceneView.duringSceneGui += DrawFleetRoutes;
            RefreshTuning();
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            _inspectorContainer = new IMGUIContainer(DrawInspector);
            _inspectorContainer.style.flexGrow = 1f;
            rootVisualElement.Add(_inspectorContainer);
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawFleetRoutes;
            _inspectorContainer = null;
        }

        private void OnInspectorUpdate()
        {
            if (!_autoMonitorCsv || !EditorApplication.isPlaying)
                return;

            string resolvedPath = ResolveCsvPath();
            if (!File.Exists(resolvedPath))
                return;

            long ticks = File.GetLastWriteTimeUtc(resolvedPath).Ticks;
            if (ticks == _lastCsvTicks)
                return;

            _lastCsvTicks = ticks;
            if (DroneFleetAutomationFacade.TryApplyDroneSpecsCsv(resolvedPath, out int keys))
            {
                _status = "CSV applied: " + keys + " keys.";
                RefreshTuning();
                SceneView.RepaintAll();
                Repaint();
            }
        }

        private void DrawInspector()
        {
            EditorGUILayout.LabelField("Fleet Automation Tuner", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                EditorGUI.BeginChangeCheck();
                _tuning.MaxDroneSpeed = EditorGUILayout.Slider("MaxDroneSpeed", _tuning.MaxDroneSpeed, 0.5f, 24f);
                _tuning.BatteryDrainRate = EditorGUILayout.Slider("BatteryDrainRate", _tuning.BatteryDrainRate, 0.01f, 25f);
                _tuning.SdfRepulsionStrength = EditorGUILayout.Slider("SdfRepulsionStrength", _tuning.SdfRepulsionStrength, 0f, 24f);
                _tuning.RepairSpeed = EditorGUILayout.Slider("RepairSpeed", _tuning.RepairSpeed, 0.05f, 8f);
                _tuning.CargoCapacity = EditorGUILayout.Slider("CargoCapacity", _tuning.CargoCapacity, 1f, 64f);
                _tuning.AStarCellSize = EditorGUILayout.Slider("AStarCellSize", _tuning.AStarCellSize, 1f, 12f);
                _tuning.LowTierSolveBudget = EditorGUILayout.Slider("LowTierSolveBudget", _tuning.LowTierSolveBudget, 1f, 12f);
                _tuning.HighTierSolveBudget = EditorGUILayout.Slider("HighTierSolveBudget", _tuning.HighTierSolveBudget, 1f, 24f);
                _tuning.UltraTierSolveBudget = EditorGUILayout.Slider("UltraTierSolveBudget", _tuning.UltraTierSolveBudget, 1f, 64f);
                if (EditorGUI.EndChangeCheck())
                {
                    DroneFleetAutomationFacade.ApplyTuningConstants(in _tuning);
                    _status = "Native tuning updated.";
                }

                EditorGUILayout.Space(6f);
                _csvPath = EditorGUILayout.TextField("CSV Path", _csvPath);
                _autoMonitorCsv = EditorGUILayout.Toggle("Auto Monitor CSV", _autoMonitorCsv);
                if (GUILayout.Button("Apply CSV"))
                {
                    string resolvedPath = ResolveCsvPath();
                    if (DroneFleetAutomationFacade.TryApplyDroneSpecsCsv(resolvedPath, out int keys))
                    {
                        _status = "CSV applied: " + keys + " keys.";
                        _lastCsvTicks = File.Exists(resolvedPath) ? File.GetLastWriteTimeUtc(resolvedPath).Ticks : 0L;
                        RefreshTuning();
                    }
                    else
                    {
                        _status = "CSV not applied.";
                    }
                }
            }

            _drawRoutes = EditorGUILayout.Toggle("Draw Routes", _drawRoutes);
            if (GUILayout.Button("Refresh"))
                RefreshTuning();

            if (DroneFleetAutomationFacade.TryGetStats(out DroneFleetAutomationStats stats))
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Active", stats.ActiveDrones.ToString());
                EditorGUILayout.LabelField("PathSolves", stats.PathSolves.ToString());
                EditorGUILayout.LabelField("PathFailures", stats.PathFailures.ToString());
                EditorGUILayout.LabelField("PathIterations", stats.PathIterations.ToString());
                EditorGUILayout.LabelField("AveragePathfindingMs", stats.AveragePathfindingTimeMs.ToString("0.0000"));
                EditorGUILayout.LabelField("TasksCompleted", stats.TasksCompleted.ToString());
                EditorGUILayout.LabelField("SteeringModulo", stats.SteeringTickModulo.ToString());
            }

            EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        private void RefreshTuning()
        {
            if (DroneFleetAutomationFacade.TryGetTuningConstants(out _tuning))
                _status = "Native tuning sampled.";
            else
                _tuning = DroneFleetTuningConstants.CreateDefault();
        }

        private string ResolveCsvPath()
        {
            if (Path.IsPathRooted(_csvPath))
                return _csvPath;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, _csvPath);
        }

        private void DrawFleetRoutes(SceneView sceneView)
        {
            if (!_drawRoutes || !EditorApplication.isPlaying)
                return;

            int count = DroneFleetAutomationFacade.CopyDebugRoutes(DebugRoutes);
            for (int i = 0; i < count; i++)
            {
                DroneFleetDebugRoute route = DebugRoutes[i];
                Vector3 origin = ToVector3(route.Position);
                Vector3 waypoint = ToVector3(route.Waypoint);
                Vector3 target = ToVector3(route.Target);
                Vector3 velocity = ToVector3(route.Velocity);

                Handles.color = Color.green;
                Handles.DrawLine(origin, target);
                Handles.DrawWireDisc(target, Vector3.up, 0.3f);

                Handles.color = route.PathStatus == 1 ? new Color(0.1f, 0.9f, 0.75f, 0.45f) : new Color(1f, 0.55f, 0.1f, 0.45f);
                Handles.DrawLine(origin, waypoint);
                Handles.DrawWireDisc(waypoint, Vector3.up, 0.25f);

                DrawRouteSegment(origin, route.RoutePoint0, route.RoutePointCount, 0);
                DrawRouteSegment(route.RoutePoint0, route.RoutePoint1, route.RoutePointCount, 1);
                DrawRouteSegment(route.RoutePoint1, route.RoutePoint2, route.RoutePointCount, 2);
                DrawRouteSegment(route.RoutePoint2, route.RoutePoint3, route.RoutePointCount, 3);

                if (velocity.sqrMagnitude > 0.0001f)
                {
                    Handles.color = Color.blue;
                    Handles.DrawLine(origin, origin + velocity);
                }

                if ((route.Flags & 1) != 0)
                {
                    Handles.color = Color.red;
                    Handles.DrawLine(origin, origin + (ToVector3(route.SdfNormal) * 2f));
                }
            }
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static void DrawRouteSegment(Vector3 from, float3 to, int routePointCount, int pointIndex)
        {
            if (routePointCount <= pointIndex)
                return;

            Vector3 target = ToVector3(to);
            Handles.color = new Color(0.1f, 0.9f, 0.75f, 0.75f);
            Handles.DrawLine(from, target);
            Handles.DrawWireCube(target, Vector3.one * 0.18f);
        }
    }
}
#endif
