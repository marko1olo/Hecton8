#if UNITY_EDITOR
using Hecton8.Construction;
using Hecton8.Core;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class FleetAutomationTunerWindow : EditorWindow
    {
        private const int TelemetrySampleCapacity = 180;
        private static readonly DroneFleetDebugRoute[] DebugRoutes = new DroneFleetDebugRoute[DroneFleetAutomationFacade.MaxDebugRoutes];

        private readonly DroneTelemetryGraphElement _graph = new DroneTelemetryGraphElement(TelemetrySampleCapacity);

        private DroneFleetTuningConstants _tuning;
        private Slider _maxSpeedSlider;
        private Slider _maxNodesSlider;
        private Slider _heuristicSlider;
        private Slider _separationSlider;
        private TextField _csvPathField;
        private Toggle _autoMonitorToggle;
        private Toggle _drawRoutesToggle;
        private Label _statusLabel;
        private Label _activeLabel;
        private Label _pathSolvesLabel;
        private Label _pathFailuresLabel;
        private Label _pathIterationsLabel;
        private Label _averagePathMsLabel;
        private Label _steeringModuloLabel;
        private Label _avoidanceLabel;
        private Label _chassisLabel;
        private string _csvPath = "drone_navigation_profiles.csv";
        private string _status = "Fleet not sampled.";
        private long _lastCsvTicks;
        private bool _autoMonitorCsv = true;
        private bool _drawRoutes = true;
        private int _lastPathIterations;
        private int _lastAvoidanceVectors;

        [MenuItem("Hecton8/AI/Drone Fleet Navigation Tuner")]
        private static void Open()
        {
            GetWindow<FleetAutomationTunerWindow>("Drone Navigation");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawFleetRoutes;
            SceneView.duringSceneGui += DrawFleetRoutes;
            RefreshTuning();
        }

        private void CreateGUI()
        {
            BuildUi(rootVisualElement);
            RefreshTuning();
            RefreshUiFromTuning();
            RefreshStats();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawFleetRoutes;
        }

        private void OnInspectorUpdate()
        {
            if (_autoMonitorCsv && EditorApplication.isPlaying)
                TryApplyCsvIfChanged();

            RefreshStats();
            SceneView.RepaintAll();
        }

        private void BuildUi(VisualElement root)
        {
            root.Clear();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            Label title = new Label("Drone Fleet Navigation Tuner");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 8f;
            root.Add(title);

            _maxSpeedSlider = AddSlider(root, "MaxSpeed", 0.5f, 24f, OnMaxSpeedChanged);
            _maxNodesSlider = AddSlider(root, "MaxNodesExpandedPerFrame", 16f, 512f, OnMaxNodesChanged);
            _heuristicSlider = AddSlider(root, "HeuristicWeight", 1f, 4f, OnHeuristicChanged);
            _separationSlider = AddSlider(root, "SeparationForce", 0f, 24f, OnSeparationChanged);

            _csvPathField = new TextField("CSV Path");
            _csvPathField.value = _csvPath;
            _csvPathField.RegisterValueChangedCallback(evt => _csvPath = evt.newValue);
            root.Add(_csvPathField);

            _autoMonitorToggle = new Toggle("Auto Monitor CSV");
            _autoMonitorToggle.value = _autoMonitorCsv;
            _autoMonitorToggle.RegisterValueChangedCallback(evt => _autoMonitorCsv = evt.newValue);
            root.Add(_autoMonitorToggle);

            Button applyCsvButton = new Button(ApplyCsv) { text = "Apply CSV" };
            root.Add(applyCsvButton);

            _drawRoutesToggle = new Toggle("Draw Routes");
            _drawRoutesToggle.value = _drawRoutes;
            _drawRoutesToggle.RegisterValueChangedCallback(evt => _drawRoutes = evt.newValue);
            root.Add(_drawRoutesToggle);

            Button refreshButton = new Button(RefreshTuningAndUi) { text = "Refresh" };
            root.Add(refreshButton);

            _graph.style.height = 160f;
            _graph.style.marginTop = 8f;
            _graph.style.marginBottom = 8f;
            root.Add(_graph);

            _activeLabel = AddStatLabel(root);
            _pathSolvesLabel = AddStatLabel(root);
            _pathFailuresLabel = AddStatLabel(root);
            _pathIterationsLabel = AddStatLabel(root);
            _averagePathMsLabel = AddStatLabel(root);
            _steeringModuloLabel = AddStatLabel(root);
            _avoidanceLabel = AddStatLabel(root);
            _chassisLabel = AddStatLabel(root);

            _statusLabel = new Label(_status);
            _statusLabel.style.marginTop = 8f;
            root.Add(_statusLabel);
        }

        private static Slider AddSlider(VisualElement root, string label, float min, float max, EventCallback<ChangeEvent<float>> callback)
        {
            Slider slider = new Slider(label, min, max);
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(callback);
            root.Add(slider);
            return slider;
        }

        private static Label AddStatLabel(VisualElement root)
        {
            Label label = new Label();
            root.Add(label);
            return label;
        }

        private void OnMaxSpeedChanged(ChangeEvent<float> evt)
        {
            _tuning.MaxDroneSpeed = evt.newValue;
            ApplyTuning();
        }

        private void OnMaxNodesChanged(ChangeEvent<float> evt)
        {
            _tuning.OverkillSolveBudget = Mathf.Clamp(evt.newValue / 48f, 1f, 64f);
            ApplyTuning();
        }

        private void OnHeuristicChanged(ChangeEvent<float> evt)
        {
            _tuning.Reserved0 = evt.newValue;
            ApplyTuning();
        }

        private void OnSeparationChanged(ChangeEvent<float> evt)
        {
            _tuning.SdfRepulsionStrength = evt.newValue;
            ApplyTuning();
        }

        private void ApplyTuning()
        {
            if (!EditorApplication.isPlaying)
                return;

            DroneFleetAutomationFacade.ApplyTuningConstants(in _tuning);
            SetStatus("Vault tuning updated.");
        }

        private void RefreshTuningAndUi()
        {
            RefreshTuning();
            RefreshUiFromTuning();
            RefreshStats();
        }

        private void RefreshTuning()
        {
            if (DroneFleetAutomationFacade.TryGetTuningConstants(out _tuning))
                _status = "Native tuning sampled.";
            else
                _tuning = DroneFleetTuningConstants.CreateDefault();
        }

        private void RefreshUiFromTuning()
        {
            SetSliderValueWithoutNotify(_maxSpeedSlider, _tuning.MaxDroneSpeed);
            SetSliderValueWithoutNotify(_maxNodesSlider, Mathf.Clamp(_tuning.OverkillSolveBudget * 48f, 16f, 512f));
            float heuristicWeight = _tuning.Reserved0 > 0f
                ? _tuning.Reserved0
                : Mathf.Lerp(2.25f, 1.05f, Mathf.Clamp01(HomeostasisBrain.GlobalQualityWeight));
            SetSliderValueWithoutNotify(_heuristicSlider, heuristicWeight);
            SetSliderValueWithoutNotify(_separationSlider, _tuning.SdfRepulsionStrength);

            if (_csvPathField != null)
                _csvPathField.SetValueWithoutNotify(_csvPath);

            if (_autoMonitorToggle != null)
                _autoMonitorToggle.SetValueWithoutNotify(_autoMonitorCsv);

            if (_drawRoutesToggle != null)
                _drawRoutesToggle.SetValueWithoutNotify(_drawRoutes);

            SetStatus(_status);
        }

        private static void SetSliderValueWithoutNotify(Slider slider, float value)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(value);
        }

        private void RefreshStats()
        {
            if (!DroneFleetAutomationFacade.TryGetStats(out DroneFleetAutomationStats stats))
            {
                SetStatus(_status);
                return;
            }

            int avoidanceVectors = CountActiveAvoidanceVectors();
            int nodeDelta = math.max(0, stats.PathIterations - _lastPathIterations);
            _lastPathIterations = stats.PathIterations;
            _lastAvoidanceVectors = avoidanceVectors;
            _graph.Push(nodeDelta, stats.SteeringTickModulo, avoidanceVectors);

            SetText(_activeLabel, "Active: ", stats.ActiveDrones);
            SetText(_pathSolvesLabel, "PathSolves: ", stats.PathSolves);
            SetText(_pathFailuresLabel, "PathFailures: ", stats.PathFailures);
            SetText(_pathIterationsLabel, "PathIterations: ", stats.PathIterations);
            SetText(_averagePathMsLabel, "AveragePathfindingMs: ", stats.AveragePathfindingTimeMs, "0.0000");
            SetText(_steeringModuloLabel, "SteeringModulo: ", stats.SteeringTickModulo);
            SetText(_avoidanceLabel, "ActiveAvoidanceVectors: ", avoidanceVectors);
            SetText(_chassisLabel, "ChassisSpecs: ", stats.ChassisSpecCount);
            SetStatus(_status);
        }

        private int CountActiveAvoidanceVectors()
        {
            int count = DroneFleetAutomationFacade.CopyDebugRoutes(DebugRoutes);
            int active = 0;
            for (int i = 0; i < count; i++)
            {
                DroneFleetDebugRoute route = DebugRoutes[i];
                if ((route.Flags & 1) != 0 || math.lengthsq(route.SdfNormal) > 0.0001f)
                    active++;
            }

            return active;
        }

        private static void SetText(Label label, string prefix, int value)
        {
            if (label != null)
                label.text = prefix + value;
        }

        private static void SetText(Label label, string prefix, float value, string format)
        {
            if (label != null)
                label.text = prefix + value.ToString(format);
        }

        private void SetStatus(string status)
        {
            _status = status;
            if (_statusLabel != null)
                _statusLabel.text = status;
        }

        private void TryApplyCsvIfChanged()
        {
            string resolvedPath = ResolveCsvPath();
            if (!File.Exists(resolvedPath))
                return;

            long ticks = File.GetLastWriteTimeUtc(resolvedPath).Ticks;
            if (ticks == _lastCsvTicks)
                return;

            _lastCsvTicks = ticks;
            if (DroneFleetAutomationFacade.TryApplyDroneSpecsCsv(resolvedPath, out int keys))
            {
                RefreshTuning();
                RefreshUiFromTuning();
                SetStatus("CSV applied: " + keys + " keys.");
            }
        }

        private void ApplyCsv()
        {
            string resolvedPath = ResolveCsvPath();
            if (DroneFleetAutomationFacade.TryApplyDroneSpecsCsv(resolvedPath, out int keys))
            {
                _lastCsvTicks = File.Exists(resolvedPath) ? File.GetLastWriteTimeUtc(resolvedPath).Ticks : 0L;
                RefreshTuning();
                RefreshUiFromTuning();
                SetStatus("CSV applied: " + keys + " keys.");
            }
            else
            {
                SetStatus("CSV not applied.");
            }
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
                DrawClosedNode(route.ClosedPoint0, route.Reserved0, 0);
                DrawClosedNode(route.ClosedPoint1, route.Reserved0, 1);
                DrawClosedNode(route.ClosedPoint2, route.Reserved0, 2);
                DrawClosedNode(route.ClosedPoint3, route.Reserved0, 3);

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

        private static void DrawClosedNode(float3 position, int closedPointCount, int pointIndex)
        {
            if (closedPointCount <= pointIndex)
                return;

            Handles.color = new Color(1f, 0.05f, 0.05f, 0.8f);
            Handles.DrawWireDisc(ToVector3(position), Vector3.up, 0.14f);
        }

        private sealed class DroneTelemetryGraphElement : VisualElement
        {
            private readonly float[] _nodes;
            private readonly float[] _delay;
            private readonly float[] _avoidance;
            private int _cursor;
            private int _count;

            public DroneTelemetryGraphElement(int capacity)
            {
                _nodes = new float[capacity];
                _delay = new float[capacity];
                _avoidance = new float[capacity];
                generateVisualContent += DrawGraph;
            }

            public void Push(float nodesExpanded, float steeringDelay, float avoidanceVectors)
            {
                _nodes[_cursor] = math.max(0f, nodesExpanded);
                _delay[_cursor] = math.max(0f, steeringDelay);
                _avoidance[_cursor] = math.max(0f, avoidanceVectors);
                _cursor = (_cursor + 1) % _nodes.Length;
                _count = math.min(_count + 1, _nodes.Length);
                MarkDirtyRepaint();
            }

            private void DrawGraph(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                if (rect.width <= 1f || rect.height <= 1f)
                    return;

                Painter2D painter = context.painter2D;
                painter.fillColor = new Color(0.05f, 0.06f, 0.07f, 1f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                painter.ClosePath();
                painter.Fill();

                DrawSeries(painter, rect, _nodes, new Color(0.1f, 0.9f, 0.75f, 1f));
                DrawSeries(painter, rect, _delay, new Color(1f, 0.72f, 0.18f, 1f));
                DrawSeries(painter, rect, _avoidance, new Color(1f, 0.12f, 0.12f, 1f));
            }

            private void DrawSeries(Painter2D painter, Rect rect, float[] values, Color color)
            {
                if (_count <= 1)
                    return;

                float maxValue = 1f;
                for (int i = 0; i < _count; i++)
                {
                    int index = ResolveIndex(i);
                    maxValue = math.max(maxValue, values[index]);
                }

                painter.strokeColor = color;
                painter.lineWidth = 2f;
                painter.BeginPath();
                for (int i = 0; i < _count; i++)
                {
                    int index = ResolveIndex(i);
                    float x = rect.xMin + (rect.width * (i / math.max(1f, _count - 1f)));
                    float y = rect.yMax - (rect.height * math.saturate(values[index] / maxValue));
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();
            }

            private int ResolveIndex(int age)
            {
                int start = _count < _nodes.Length ? 0 : _cursor;
                return (start + age) % _nodes.Length;
            }
        }
    }
}
#endif
