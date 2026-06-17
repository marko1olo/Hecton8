#if UNITY_EDITOR
using System.Globalization;
using Hecton8.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class ExecutionPipelineXRayWindow : EditorWindow
    {
        private const float FrameBudgetMs = 16.6667f;
        private const int MaxGraphRows = 32;
        private const int MaxGraphEdges = 256;
        private const int MaxJobRows = 12;
        private static readonly float[] _phaseMs = new float[4];
        private static readonly uint[] _bucketLoads = new uint[64];
        private static readonly uint[] _edgeSystemHashes = new uint[MaxGraphEdges];
        private static readonly uint[] _edgeDependencyHashes = new uint[MaxGraphEdges];
        private static readonly byte[] _edgePhaseIds = new byte[MaxGraphEdges];
        private static readonly uint[] _jobSystemHashes = new uint[85];
        private static readonly ulong[] _jobHandleBits = new ulong[85];
        private readonly VisualElement[] _phaseFills = new VisualElement[4];
        private readonly Label[] _phaseValueLabels = new Label[4];
        private readonly Label[] _bucketLabels = new Label[64];
        private readonly VisualElement[] _bucketCells = new VisualElement[64];
        private readonly Label[] _graphLabels = new Label[MaxGraphRows];
        private readonly Label[] _jobLabels = new Label[MaxJobRows];
        private Label _stateLabel;
        private Label _qualityLabel;
        private Label _fenceLabel;
        private Label _graphHeaderLabel;
        private Label _jobHeaderLabel;
        private double _nextRefreshTime;

        [MenuItem("Hecton8/Diagnostics/Execution Pipeline X-Ray")]
        public static void Open()
        {
            ExecutionPipelineXRayWindow window = GetWindow<ExecutionPipelineXRayWindow>();
            window.titleContent = new GUIContent("Execution Pipeline X-Ray");
            window.minSize = new Vector2(580f, 520f);
            window.Show();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            root.style.backgroundColor = new Color(0.07f, 0.08f, 0.085f, 1f);

            _qualityLabel = CreateHeaderLabel("QUALITY 0.000 | TIME SLICE 0.000 ms");
            root.Add(_qualityLabel);

            root.Add(CreatePhaseRow(0, "PRE_SIMULATION", 4.0f));
            root.Add(CreatePhaseRow(1, "SIM_WAIT", 8.0f));
            root.Add(CreatePhaseRow(2, "POST_SIMULATION", 4.0f));
            root.Add(CreatePhaseRow(3, "VISUAL_SYNC", 4.0f));

            _stateLabel = CreateSmallLabel("SystemDispatcher inactive");
            _stateLabel.style.marginTop = 10;
            root.Add(_stateLabel);

            Button aupFenceButton = new Button(SystemDispatcher.RequestDebugAupHardFence)
            {
                text = "Trigger AUP Hard Fence"
            };
            aupFenceButton.style.marginTop = 8;
            root.Add(aupFenceButton);

            _fenceLabel = CreateSmallLabel("Fence telemetry unavailable");
            _fenceLabel.style.marginTop = 6;
            root.Add(_fenceLabel);

            root.Add(CreateBucketGrid());

            _graphHeaderLabel = CreateHeaderLabel("DEPENDENCY GRAPH");
            _graphHeaderLabel.style.marginTop = 12;
            root.Add(_graphHeaderLabel);
            VisualElement graph = new VisualElement();
            graph.style.marginTop = 4;
            graph.style.borderTopColor = new Color(0.18f, 0.22f, 0.24f, 1f);
            graph.style.borderTopWidth = 1;
            for (int i = 0; i < MaxGraphRows; i++)
            {
                Label row = CreateSmallLabel(string.Empty);
                row.style.height = 18;
                row.style.display = DisplayStyle.None;
                _graphLabels[i] = row;
                graph.Add(row);
            }

            root.Add(graph);
            _jobHeaderLabel = CreateHeaderLabel("JOB FENCES");
            _jobHeaderLabel.style.marginTop = 12;
            root.Add(_jobHeaderLabel);

            VisualElement jobs = new VisualElement();
            jobs.style.marginTop = 4;
            jobs.style.borderTopColor = new Color(0.18f, 0.22f, 0.24f, 1f);
            jobs.style.borderTopWidth = 1;
            for (int i = 0; i < MaxJobRows; i++)
            {
                Label row = CreateSmallLabel(string.Empty);
                row.style.height = 18;
                row.style.display = DisplayStyle.None;
                _jobLabels[i] = row;
                jobs.Add(row);
            }

            root.Add(jobs);
            RefreshSnapshot();
        }

        private void OnEnable()
        {
            EditorApplication.update -= TickRefresh;
            EditorApplication.update += TickRefresh;
        }

        private void OnDisable()
        {
            EditorApplication.update -= TickRefresh;
        }

        private void TickRefresh()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextRefreshTime)
                return;

            _nextRefreshTime = now + 0.25d;
            RefreshSnapshot();
        }

        private void RefreshSnapshot()
        {
            if (_stateLabel == null)
                return;

            float quality = HomeostasisBrain.GlobalQualityWeight;
            _qualityLabel.text =
                "QUALITY " + quality.ToString("0.000", CultureInfo.InvariantCulture) +
                " | TIME SLICE " + TimeSliceScheduler.CurrentBudgetMs.ToString("0.000", CultureInfo.InvariantCulture) +
                " ms | USED " + TimeSliceScheduler.ConsumedMs.ToString("0.000", CultureInfo.InvariantCulture) + " ms";

            if (!SystemDispatcher.TryGetExecutionPipelineXRaySnapshot(_phaseMs, _bucketLoads, out DispatcherStateDTO state))
            {
                _stateLabel.text = "SystemDispatcher inactive";
                ClearGraphRows();
                ClearJobRows();
                return;
            }

            UpdatePhaseRow(0, _phaseMs[0], 4.0f);
            UpdatePhaseRow(1, _phaseMs[1], 8.0f);
            UpdatePhaseRow(2, _phaseMs[2], 4.0f);
            UpdatePhaseRow(3, _phaseMs[3], 4.0f);
            UpdateFenceTelemetry();
            _stateLabel.text =
                "Frame " + state.CurrentFrame +
                " | Phase " + state.CurrentPhaseId +
                " | Bucket " + state.ActiveBucket +
                " | Systems " + state.SortedSystemCount +
                " | Disabled " + state.DisabledSystemCount +
                " | Flags 0x" + state.Flags.ToString("X8", CultureInfo.InvariantCulture);

            UpdateBucketGrid();
            UpdateDependencyGraph();
            UpdateJobTelemetry();
        }

        private VisualElement CreatePhaseRow(int index, string label, float budgetMs)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = 30;
            row.style.marginTop = index == 0 ? 8 : 2;

            Label name = CreateSmallLabel(label);
            name.style.width = 148;
            row.Add(name);

            VisualElement rail = new VisualElement();
            rail.style.flexGrow = 1;
            rail.style.height = 18;
            rail.style.backgroundColor = new Color(0.11f, 0.12f, 0.125f, 1f);
            rail.tooltip = "Budget " + budgetMs.ToString("0.00", CultureInfo.InvariantCulture) + " ms";

            VisualElement fill = new VisualElement();
            fill.style.height = 18;
            fill.style.width = Length.Percent(0f);
            fill.style.backgroundColor = new Color(0.10f, 0.62f, 0.86f, 1f);
            _phaseFills[index] = fill;
            rail.Add(fill);
            row.Add(rail);

            Label value = CreateSmallLabel("0.00 ms");
            value.style.width = 76;
            value.style.unityTextAlign = TextAnchor.MiddleRight;
            _phaseValueLabels[index] = value;
            row.Add(value);
            return row;
        }

        private VisualElement CreateBucketGrid()
        {
            VisualElement grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.width = 232;
            grid.style.marginTop = 12;
            for (int i = 0; i < _bucketCells.Length; i++)
            {
                VisualElement cell = new VisualElement();
                cell.style.width = 24;
                cell.style.height = 24;
                cell.style.marginRight = 4;
                cell.style.marginBottom = 4;
                cell.style.backgroundColor = new Color(0.05f, 0.16f, 0.16f, 1f);
                cell.tooltip = "Bucket " + i.ToString(CultureInfo.InvariantCulture);

                Label label = CreateSmallLabel(i.ToString(CultureInfo.InvariantCulture));
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.fontSize = 9;
                label.style.flexGrow = 1;
                _bucketLabels[i] = label;
                _bucketCells[i] = cell;
                cell.Add(label);
                grid.Add(cell);
            }

            return grid;
        }

        private void UpdatePhaseRow(int index, float valueMs, float budgetMs)
        {
            float normalized = Mathf.Clamp01(valueMs / Mathf.Max(0.001f, FrameBudgetMs));
            _phaseFills[index].style.width = Length.Percent(normalized * 100f);
            _phaseFills[index].style.backgroundColor = valueMs > budgetMs
                ? new Color(0.95f, 0.08f, 0.04f, 1f)
                : new Color(0.10f, 0.62f, 0.86f, 1f);
            _phaseValueLabels[index].text = valueMs.ToString("0.00", CultureInfo.InvariantCulture) + " ms";
        }

        private void UpdateBucketGrid()
        {
            uint maxLoad = 1u;
            for (int i = 0; i < _bucketLoads.Length; i++)
            {
                if (_bucketLoads[i] > maxLoad)
                    maxLoad = _bucketLoads[i];
            }

            for (int i = 0; i < _bucketCells.Length; i++)
            {
                float load01 = Mathf.Clamp01(_bucketLoads[i] / (float)maxLoad);
                _bucketCells[i].style.backgroundColor = Color.Lerp(
                    new Color(0.05f, 0.16f, 0.16f, 1f),
                    new Color(0.91f, 0.42f, 0.08f, 1f),
                    load01);
                _bucketLabels[i].text = i.ToString(CultureInfo.InvariantCulture);
            }
        }

        private void UpdateDependencyGraph()
        {
            int edgeCount;
            int systemCount;
            try
            {
                if (!SystemDispatcher.TryGetDependencyGraphEdges(
                        _edgeSystemHashes,
                        _edgeDependencyHashes,
                        _edgePhaseIds,
                        out edgeCount,
                        out systemCount))
                {
                    ClearGraphRows();
                    return;
                }
            }
            catch (FatalArchitectureException exception)
            {
                ClearGraphRows();
                _graphHeaderLabel.text = "DEPENDENCY CYCLE | " + exception.Message;
                return;
            }

            int rowCount = Mathf.Min(edgeCount, MaxGraphRows);
            _graphHeaderLabel.text = "DEPENDENCY GRAPH | " + systemCount + " SYSTEMS | " + edgeCount + " EDGES";
            for (int i = 0; i < rowCount; i++)
            {
                Label label = _graphLabels[i];
                label.style.display = DisplayStyle.Flex;
                label.text =
                    "P" + _edgePhaseIds[i] +
                    " 0x" + _edgeSystemHashes[i].ToString("X8", CultureInfo.InvariantCulture) +
                    " <- 0x" + _edgeDependencyHashes[i].ToString("X8", CultureInfo.InvariantCulture);
            }

            for (int i = rowCount; i < _graphLabels.Length; i++)
                _graphLabels[i].style.display = DisplayStyle.None;
        }

        private void UpdateJobTelemetry()
        {
            if (!SystemDispatcher.TryGetJobDependencyTelemetrySnapshot(
                    _jobSystemHashes,
                    _jobHandleBits,
                    out int jobCount))
            {
                ClearJobRows();
                return;
            }

            int rowCount = Mathf.Min(jobCount, MaxJobRows);
            _jobHeaderLabel.text = "JOB FENCES | " + jobCount + " HANDLES";
            for (int i = 0; i < rowCount; i++)
            {
                Label label = _jobLabels[i];
                label.style.display = DisplayStyle.Flex;
                label.text =
                    "0x" + _jobSystemHashes[i].ToString("X8", CultureInfo.InvariantCulture) +
                    " handle 0x" + _jobHandleBits[i].ToString("X16", CultureInfo.InvariantCulture);
            }

            for (int i = rowCount; i < _jobLabels.Length; i++)
                _jobLabels[i].style.display = DisplayStyle.None;
        }

        private void UpdateFenceTelemetry()
        {
            if (_fenceLabel == null)
                return;

            if (!SystemDispatcher.TryGetLatestFenceTelemetry(out DispatcherFenceTelemetryEntry entry))
            {
                _fenceLabel.text = "Fence telemetry unavailable";
                return;
            }

            _fenceLabel.text =
                "FENCE frame " + entry.FrameId +
                " | jobs " + entry.ScheduledJobCount +
                " | sim wait " + entry.SimulationWaitMs.ToString("0.00", CultureInfo.InvariantCulture) + " ms" +
                " | fixed wait " + entry.FixedWaitMs.ToString("0.00", CultureInfo.InvariantCulture) + " ms" +
                " | AUP " + entry.AupHardFenceMs.ToString("0.00", CultureInfo.InvariantCulture) + " ms" +
                " | domains 0x" + entry.DomainMask.ToString("X2", CultureInfo.InvariantCulture);
        }

        private void ClearGraphRows()
        {
            if (_graphHeaderLabel != null)
                _graphHeaderLabel.text = "DEPENDENCY GRAPH";
            for (int i = 0; i < _graphLabels.Length; i++)
            {
                if (_graphLabels[i] != null)
                    _graphLabels[i].style.display = DisplayStyle.None;
            }
        }

        private void ClearJobRows()
        {
            if (_jobHeaderLabel != null)
                _jobHeaderLabel.text = "JOB FENCES";
            for (int i = 0; i < _jobLabels.Length; i++)
            {
                if (_jobLabels[i] != null)
                    _jobLabels[i].style.display = DisplayStyle.None;
            }
        }

        private static Label CreateHeaderLabel(string text)
        {
            Label label = new Label(text);
            label.style.color = new Color(0.78f, 0.88f, 0.90f, 1f);
            label.style.fontSize = 12;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        private static Label CreateSmallLabel(string text)
        {
            Label label = new Label(text);
            label.style.color = new Color(0.70f, 0.76f, 0.78f, 1f);
            label.style.fontSize = 11;
            return label;
        }
    }
}
#endif
