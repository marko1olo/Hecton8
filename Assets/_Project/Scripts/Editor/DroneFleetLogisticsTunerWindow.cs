#if UNITY_EDITOR
using Hecton8.Construction;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorTools
{
    public sealed class DroneFleetLogisticsTunerWindow : EditorWindow
    {
        private const int TelemetryCapacity = 300;
        private const int DebugTaskCapacity = 64;
        private const int HistogramVisibleRows = 32;
        private const int HistogramEventClamp = 48;
        private const float HistogramPixelsPerEvent = 5f;
        private const double TelemetryRefreshIntervalSeconds = 0.25d;
        private static readonly DroneTransactionTelemetrySnapshot[] Telemetry = new DroneTransactionTelemetrySnapshot[TelemetryCapacity];
        private static readonly DroneTransactionDebugTask[] DebugTasks = new DroneTransactionDebugTask[DebugTaskCapacity];

        private readonly VisualElement[] _completionBars = new VisualElement[HistogramVisibleRows];
        private readonly VisualElement[] _conflictBars = new VisualElement[HistogramVisibleRows];
        private Label _summary;
        private VisualElement _histogram;
        private Slider _miningSpeed;
        private Slider _repairEfficiency;
        private Slider _qualityOverride;
        private Toggle _qualityOverrideEnabled;
        private Toggle _drawGizmos;
        private double _nextTelemetryRefreshTime;
        private uint _lastSummaryFrame = uint.MaxValue;
        private bool _syncing;

        [MenuItem("Hecton8/AI/Drone Fleet Logistics Tuner")]
        public static void Open()
        {
            GetWindow<DroneFleetLogisticsTunerWindow>("Drone Logistics");
        }

        private void OnEnable()
        {
            BuildUi();
            SceneView.duringSceneGui -= DrawSceneGizmos;
            SceneView.duringSceneGui += DrawSceneGizmos;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmos;
            EditorApplication.update -= Tick;
        }

        public void CreateGUI()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _summary = new Label("Drone transaction telemetry: pending");
            _histogram = new VisualElement();
            _histogram.style.marginTop = 8;
            _histogram.style.flexDirection = FlexDirection.Column;
            BuildHistogramRows(_histogram);
            _miningSpeed = CreateSlider("BaseMiningSpeed", 0.1f, 8f, 1f);
            _repairEfficiency = CreateSlider("BaseRepairEfficiency", 0.05f, 8f, 1f);
            _qualityOverride = CreateSlider("GlobalQualityWeight Override", 0f, 1f, HomeostasisBrain.GlobalQualityWeight);
            _qualityOverrideEnabled = new Toggle("Force Quality Override");
            _drawGizmos = new Toggle("Draw Transaction X-Ray") { value = true };

            Button refresh = new Button(RefreshFromRuntime) { text = "Refresh Runtime DTOs" };
            Button scan = new Button(RunScanner) { text = "Run OOP Interaction Scanner" };
            _miningSpeed.RegisterValueChangedCallback(_ => ApplyTuning());
            _repairEfficiency.RegisterValueChangedCallback(_ => ApplyTuning());
            _qualityOverride.RegisterValueChangedCallback(_ => ApplyQualityOverride());
            _qualityOverrideEnabled.RegisterValueChangedCallback(_ => ApplyQualityOverride());

            rootVisualElement.Add(_summary);
            rootVisualElement.Add(_miningSpeed);
            rootVisualElement.Add(_repairEfficiency);
            rootVisualElement.Add(_qualityOverrideEnabled);
            rootVisualElement.Add(_qualityOverride);
            rootVisualElement.Add(_drawGizmos);
            rootVisualElement.Add(refresh);
            rootVisualElement.Add(scan);
            rootVisualElement.Add(_histogram);
            RefreshFromRuntime();
        }

        private static Slider CreateSlider(string label, float min, float max, float value)
        {
            return new Slider(label, min, max)
            {
                value = value,
                showInputField = true
            };
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextTelemetryRefreshTime)
                return;

            _nextTelemetryRefreshTime = now + TelemetryRefreshIntervalSeconds;
            RefreshTelemetry();
        }

        private void RefreshFromRuntime()
        {
            _syncing = true;
            if (DroneFleetAutomationFacade.TryGetTuningConstants(out DroneFleetTuningConstants tuning))
            {
                float miningSpeed = 1f / math.max(0.01f, tuning.MiningHoldSeconds);
                _miningSpeed.SetValueWithoutNotify(math.clamp(miningSpeed, 0.1f, 8f));
                _repairEfficiency.SetValueWithoutNotify(math.clamp(tuning.RepairSpeed, 0.05f, 8f));
            }

            _qualityOverride.SetValueWithoutNotify(math.saturate(HomeostasisBrain.GlobalQualityWeight));
            _syncing = false;
            RefreshTelemetry();
        }

        private void ApplyTuning()
        {
            if (_syncing || !EditorApplication.isPlaying)
                return;

            if (!DroneFleetAutomationFacade.TryGetTuningConstants(out DroneFleetTuningConstants tuning))
                tuning = DroneFleetTuningConstants.CreateDefault();

            tuning.MiningHoldSeconds = 1f / math.max(0.1f, _miningSpeed.value);
            tuning.RepairSpeed = math.max(0.05f, _repairEfficiency.value);
            DroneFleetAutomationFacade.ApplyTuningConstants(in tuning);
        }

        private void ApplyQualityOverride()
        {
            if (_syncing)
                return;

            bool enabled = _qualityOverrideEnabled != null && _qualityOverrideEnabled.value;
            HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(_qualityOverride.value, enabled);
        }

        private void RefreshTelemetry()
        {
            if (_summary == null || _histogram == null)
                return;

            if (!DroneFleetAutomationFacade.TryGetLatestTransactionTelemetry(out DroneTransactionTelemetrySnapshot latest))
            {
                _summary.text = "Drone transaction telemetry: no owner-phase frame written.";
                ClearHistogramBars();
                return;
            }

            if (latest.Frame != _lastSummaryFrame)
            {
                _summary.text = "Frame " + latest.Frame +
                                " | tasks " + latest.TransactionCount +
                                " | repairs " + latest.RepairCount +
                                " | mined " + latest.InventoryAdds +
                                " | conflicts " + latest.AtomicConflicts +
                                " | vfx " + latest.VfxSignals +
                                " | est " + latest.EstimatedMicroseconds.ToString("0.000") + " us";
                _lastSummaryFrame = latest.Frame;
            }

            int count = DroneFleetAutomationFacade.CopyTransactionTelemetry(Telemetry);
            BuildHistogram(count);
            Repaint();
        }

        private void BuildHistogramRows(VisualElement root)
        {
            root.Add(new Label("Histogram: completions / atomic conflicts"));
            for (int i = 0; i < HistogramVisibleRows; i++)
            {
                VisualElement row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.height = 7;
                row.style.marginBottom = 1;

                VisualElement completion = CreateHistogramBar(new Color(0.12f, 0.78f, 0.42f, 0.9f));
                VisualElement conflict = CreateHistogramBar(new Color(1f, 0.2f, 0.12f, 0.9f));
                row.Add(completion);
                row.Add(conflict);
                root.Add(row);
                _completionBars[i] = completion;
                _conflictBars[i] = conflict;
            }
        }

        private static VisualElement CreateHistogramBar(Color color)
        {
            VisualElement bar = new VisualElement();
            bar.style.width = 0f;
            bar.style.height = 6;
            bar.style.marginRight = 2;
            bar.style.backgroundColor = color;
            return bar;
        }

        private void BuildHistogram(int count)
        {
            int row = 0;
            int start = math.max(0, count - HistogramVisibleRows);
            for (int i = start; i < count && row < HistogramVisibleRows; i++, row++)
            {
                DroneTransactionTelemetrySnapshot entry = Telemetry[i];
                int completions = math.max(0, entry.InventoryAdds + entry.RepairCount);
                int conflicts = math.max(0, entry.AtomicConflicts);
                SetHistogramWidths(row, completions, conflicts);
            }

            for (; row < HistogramVisibleRows; row++)
                SetHistogramWidths(row, 0, 0);
        }

        private void ClearHistogramBars()
        {
            for (int i = 0; i < HistogramVisibleRows; i++)
                SetHistogramWidths(i, 0, 0);
        }

        private void SetHistogramWidths(int row, int completions, int conflicts)
        {
            _completionBars[row].style.width = math.min(completions, HistogramEventClamp) * HistogramPixelsPerEvent;
            _conflictBars[row].style.width = math.min(conflicts, HistogramEventClamp) * HistogramPixelsPerEvent;
        }

        private void DrawSceneGizmos(SceneView sceneView)
        {
            if (_drawGizmos == null || !_drawGizmos.value || !EditorApplication.isPlaying)
                return;

            int count = DroneFleetAutomationFacade.CopyTransactionDebugTasks(DebugTasks);
            for (int i = 0; i < count; i++)
            {
                DroneTransactionDebugTask task = DebugTasks[i];
                Vector3 position = ToVector3(task.Position);
                Vector3 target = ToVector3(task.Target);
                bool repair = task.TaskTypeHash == 0x44525250u;
                Handles.color = (task.Flags & (1u << 4)) != 0u
                    ? new Color(1f, 0.15f, 0.1f, 0.85f)
                    : repair ? new Color(0.1f, 0.45f, 1f, 0.85f) : new Color(0.1f, 0.9f, 0.35f, 0.85f);
                Handles.DrawLine(position, target);
                float size = math.lerp(0.12f, 0.65f, math.saturate(task.Progress01));
                Handles.DrawWireCube(position + Vector3.up * (0.25f + size), Vector3.one * size);
                Handles.DrawWireDisc(target, Vector3.up, math.max(0.2f, size * 0.8f));
            }
        }

        private static void RunScanner()
        {
            Debug.Log(Hecton8.Construction.Editor.OOP_Interaction_Scanner.RunAndWriteReport());
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
#endif
