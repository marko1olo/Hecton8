using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.AI.Pathfinding.Editor
{
    internal sealed class AbyssalPathfindingTunerWindow : EditorWindow
    {
        private Slider _quality;
        private Slider _minHeuristic;
        private Slider _maxHeuristic;
        private Slider _sampleSpacing;
        private IntegerField _minNodes;
        private IntegerField _maxNodes;
        private IntegerField _lookAhead;
        private IntegerField _lineSamples;
        private IntegerField _maxRawPath;
        private IntegerField _maxWaypoints;
        private Label _stateLabel;
        private TelemetryGraphElement _telemetryGraph;

        [MenuItem("HECTON-8/AI/Abyssal Voxel Pathfinding Tuner")]
        private static void Open()
        {
            GetWindow<AbyssalPathfindingTunerWindow>("Voxel Pathfinding");
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _stateLabel = new Label();
            rootVisualElement.Add(_stateLabel);

            _quality = AddSlider("GlobalQualityWeight", 0f, 1f);
            _minHeuristic = AddSlider("Minimum Heuristic", 1f, 2.5f);
            _maxHeuristic = AddSlider("Maximum Heuristic", 1f, 4f);
            _sampleSpacing = AddSlider("String Pull Sample Spacing", 0.25f, 6f);
            _minNodes = AddInt("Min Nodes / Frame");
            _maxNodes = AddInt("Max Nodes / Frame");
            _lookAhead = AddInt("String Pull Lookahead");
            _lineSamples = AddInt("Line Samples / Segment");
            _maxRawPath = AddInt("Max Raw Nodes");
            _maxWaypoints = AddInt("Max Waypoints");

            _telemetryGraph = new TelemetryGraphElement();
            rootVisualElement.Add(_telemetryGraph);
            Button refresh = new Button(RefreshFromVault) { text = "Refresh From Vault" };
            rootVisualElement.Add(refresh);
            RegisterCallbacks();
            RefreshFromVault();
            rootVisualElement.schedule.Execute(RefreshGraph).Every(500);
        }

        private Slider AddSlider(string label, float low, float high)
        {
            Slider slider = new Slider(label, low, high);
            slider.showInputField = true;
            rootVisualElement.Add(slider);
            return slider;
        }

        private IntegerField AddInt(string label)
        {
            IntegerField field = new IntegerField(label);
            rootVisualElement.Add(field);
            return field;
        }

        private void RegisterCallbacks()
        {
            _quality.RegisterValueChangedCallback(evt => Mutate((ref VoxelAStarTuningDTO t) => t.GlobalQualityWeight = math.saturate(evt.newValue)));
            _minHeuristic.RegisterValueChangedCallback(evt => Mutate((ref VoxelAStarTuningDTO t) => t.MinimumHeuristicWeight = math.max(1f, evt.newValue)));
            _maxHeuristic.RegisterValueChangedCallback(evt => Mutate((ref VoxelAStarTuningDTO t) => t.MaximumHeuristicWeight = math.max(t.MinimumHeuristicWeight, evt.newValue)));
            _sampleSpacing.RegisterValueChangedCallback(evt => Mutate((ref VoxelAStarTuningDTO t) => t.SmoothingSampleSpacingMeters = math.max(0.05f, evt.newValue)));
            _minNodes.RegisterValueChangedCallback(evt => Mutate((ref VoxelAStarTuningDTO t) => t.MinNodesExpandedPerFrame = math.max(1, evt.newValue)));
            _maxNodes.RegisterValueChangedCallback(evt => Mutate((ref VoxelAStarTuningDTO t) => t.MaxNodesExpandedPerFrame = math.max(t.MinNodesExpandedPerFrame, evt.newValue)));
            _lookAhead.RegisterValueChangedCallback(evt => Mutate((ref VoxelAStarTuningDTO t) => t.MaxStringPullLookAhead = math.max(1, evt.newValue)));
            _lineSamples.RegisterValueChangedCallback(evt => Mutate((ref VoxelAStarTuningDTO t) => t.MaxLineSamplesPerSegment = math.max(1, evt.newValue)));
            _maxRawPath.RegisterValueChangedCallback(evt => Mutate((ref VoxelAStarTuningDTO t) => t.MaxRawPathNodes = math.max(2, evt.newValue)));
            _maxWaypoints.RegisterValueChangedCallback(evt => Mutate((ref VoxelAStarTuningDTO t) => t.MaxWaypoints = math.max(2, evt.newValue)));
        }

        private void RefreshFromVault()
        {
            if (!TryResolveTuning(out NativeArray<VoxelAStarTuningDTO> tuningBuffer))
            {
                _stateLabel.text = "DataVault unavailable";
                return;
            }

            VoxelAStarTuningDTO tuning = Sanitize(tuningBuffer[0]);
            _quality.SetValueWithoutNotify(tuning.GlobalQualityWeight);
            _minHeuristic.SetValueWithoutNotify(tuning.MinimumHeuristicWeight);
            _maxHeuristic.SetValueWithoutNotify(tuning.MaximumHeuristicWeight);
            _sampleSpacing.SetValueWithoutNotify(tuning.SmoothingSampleSpacingMeters);
            _minNodes.SetValueWithoutNotify(tuning.MinNodesExpandedPerFrame);
            _maxNodes.SetValueWithoutNotify(tuning.MaxNodesExpandedPerFrame);
            _lookAhead.SetValueWithoutNotify(tuning.MaxStringPullLookAhead);
            _lineSamples.SetValueWithoutNotify(tuning.MaxLineSamplesPerSegment);
            _maxRawPath.SetValueWithoutNotify(tuning.MaxRawPathNodes);
            _maxWaypoints.SetValueWithoutNotify(tuning.MaxWaypoints);
            _stateLabel.text = "Vault tuning live";
        }

        private void Mutate(TuningMutation mutation)
        {
            if (AnyVoxelAStarJobActive())
            {
                _stateLabel.text = "Voxel A* job active; tuning write deferred";
                return;
            }

            if (!TryAcquireTuningWrite(out GlobalDataVault vault, out VaultGenerationHandle<VoxelAStarTuningDTO> handle, out NativeArray<VoxelAStarTuningDTO> tuningBuffer))
            {
                _stateLabel.text = "DataVault unavailable";
                return;
            }

            try
            {
                ref VoxelAStarTuningDTO tuningRef = ref ResolveTuningRef(tuningBuffer);
                VoxelAStarTuningDTO tuning = Sanitize(tuningRef);
                mutation(ref tuning);
                tuningRef = Sanitize(tuning);
                _stateLabel.text = "Vault tuning updated";
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            }
        }

        private void RefreshGraph()
        {
            if (_telemetryGraph != null)
                _telemetryGraph.MarkDirtyRepaint();
        }

        private delegate void TuningMutation(ref VoxelAStarTuningDTO tuning);

        private static unsafe ref VoxelAStarTuningDTO ResolveTuningRef(NativeArray<VoxelAStarTuningDTO> tuning)
        {
            return ref UnsafeUtility.AsRef<VoxelAStarTuningDTO>(NativeArrayUnsafeUtility.GetUnsafePtr(tuning));
        }

        private static bool TryResolveTuning(out NativeArray<VoxelAStarTuningDTO> tuning)
        {
            tuning = default;
            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault) || vault.IsCompactionFenceActive)
                return false;

            if (!vault.TryGetGenerationHandle<VoxelAStarTuningDTO>(BufferID.ShinobuVoxelPathTuning, out VaultGenerationHandle<VoxelAStarTuningDTO> handle))
                return false;

            return vault.TryReadHandle(in handle, out tuning) && tuning.IsCreated && tuning.Length > 0;
        }

        private static bool TryResolveTelemetry(out NativeArray<PathfindingTelemetryEntry> telemetry)
        {
            telemetry = default;
            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault) || vault.IsCompactionFenceActive)
                return false;

            if (!vault.TryGetGenerationHandle<PathfindingTelemetryEntry>(BufferID.ShinobuVoxelPathTelemetryRing, out VaultGenerationHandle<PathfindingTelemetryEntry> handle))
                return false;

            return vault.TryReadHandle(in handle, out telemetry) && telemetry.IsCreated && telemetry.Length > 0;
        }

        private static bool TryAcquireTuningWrite(
            out GlobalDataVault vault,
            out VaultGenerationHandle<VoxelAStarTuningDTO> handle,
            out NativeArray<VoxelAStarTuningDTO> tuning)
        {
            vault = null;
            handle = default;
            tuning = default;
            if (!GlobalDataVault.TryGetLatestCreated(out vault) || vault.IsCompactionFenceActive)
                return false;

            if (!vault.TryGetGenerationHandle<VoxelAStarTuningDTO>(BufferID.ShinobuVoxelPathTuning, out handle) ||
                !vault.TryAcquireWriteLock(in handle, SystemID.CoreDiagnostics, out tuning))
            {
                return false;
            }

            if (tuning.IsCreated && tuning.Length > 0)
                return true;

            vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            tuning = default;
            return false;
        }

        private static bool AnyVoxelAStarJobActive()
        {
            return Application.isPlaying && PathFunnelNavmeshRuntime.IsAnyVoxelAStarJobActive();
        }

        private static VoxelAStarTuningDTO Sanitize(VoxelAStarTuningDTO tuning)
        {
            VoxelAStarTuningDTO fallback = VoxelAStarTuningDTO.Default();
            if (tuning.MinNodesExpandedPerFrame <= 0 || tuning.MaxNodesExpandedPerFrame <= 0)
                tuning = fallback;
            tuning.GlobalQualityWeight = math.saturate(math.select(fallback.GlobalQualityWeight, tuning.GlobalQualityWeight, math.isfinite(tuning.GlobalQualityWeight)));
            tuning.MinimumHeuristicWeight = math.max(1f, math.select(fallback.MinimumHeuristicWeight, tuning.MinimumHeuristicWeight, math.isfinite(tuning.MinimumHeuristicWeight)));
            tuning.MaximumHeuristicWeight = math.max(tuning.MinimumHeuristicWeight, math.select(fallback.MaximumHeuristicWeight, tuning.MaximumHeuristicWeight, math.isfinite(tuning.MaximumHeuristicWeight)));
            tuning.SmoothingSampleSpacingMeters = math.max(0.05f, math.select(fallback.SmoothingSampleSpacingMeters, tuning.SmoothingSampleSpacingMeters, math.isfinite(tuning.SmoothingSampleSpacingMeters)));
            tuning.MinNodesExpandedPerFrame = math.max(1, tuning.MinNodesExpandedPerFrame);
            tuning.MaxNodesExpandedPerFrame = math.max(tuning.MinNodesExpandedPerFrame, tuning.MaxNodesExpandedPerFrame);
            tuning.MaxStringPullLookAhead = math.max(1, tuning.MaxStringPullLookAhead);
            tuning.MaxLineSamplesPerSegment = math.max(1, tuning.MaxLineSamplesPerSegment);
            tuning.MaxRawPathNodes = math.max(2, tuning.MaxRawPathNodes);
            tuning.MaxWaypoints = math.max(2, tuning.MaxWaypoints);
            tuning.TimeSliceBudgetMs = math.max(0.05f, tuning.TimeSliceBudgetMs);
            tuning.VerticalPenalty = math.max(1f, tuning.VerticalPenalty);
            return tuning;
        }

        private sealed class TelemetryGraphElement : VisualElement
        {
            private const float GraphHeightPixels = 120f;
            private const float SamplePixels = 4f;
            private const float MaxBurstMicros = 1500f;
            private const float MaxNodesExpanded = 2048f;

            public TelemetryGraphElement()
            {
                style.height = GraphHeightPixels;
                style.marginTop = 6f;
                style.marginBottom = 6f;
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                if (rect.width <= 1f || rect.height <= 1f)
                    return;

                Painter2D painter = context.painter2D;
                DrawRect(painter, rect, new Color(0.012f, 0.016f, 0.022f, 0.96f));
                if (AbyssalPathfindingTunerWindow.AnyVoxelAStarJobActive())
                    return;

                if (!TryResolveTelemetry(out NativeArray<PathfindingTelemetryEntry> telemetry) || telemetry.Length <= 0)
                    return;

                int latestIndex = ResolveLatestIndex(telemetry);
                if (latestIndex < 0)
                    return;

                int columns = math.min(telemetry.Length, math.max(2, (int)math.floor(rect.width / SamplePixels)));
                DrawLineSeries(painter, telemetry, latestIndex, columns, rect, 0, new Color(0.18f, 0.72f, 0.94f, 0.92f));
                DrawLineSeries(painter, telemetry, latestIndex, columns, rect, 1, new Color(0.95f, 0.72f, 0.18f, 0.92f));

                float budgetY = rect.yMax - math.saturate(100f / MaxBurstMicros) * rect.height;
                painter.lineWidth = 1f;
                painter.strokeColor = new Color(0.95f, 0.18f, 0.12f, 0.78f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, budgetY));
                painter.LineTo(new Vector2(rect.xMax, budgetY));
                painter.Stroke();
            }

            private static int ResolveLatestIndex(NativeArray<PathfindingTelemetryEntry> telemetry)
            {
                int latestIndex = -1;
                uint latestFrame = 0u;
                for (int i = 0; i < telemetry.Length; i++)
                {
                    uint frame = telemetry[i].Frame;
                    if (frame == 0u)
                        continue;
                    if (latestIndex < 0 || frame >= latestFrame)
                    {
                        latestIndex = i;
                        latestFrame = frame;
                    }
                }

                return latestIndex;
            }

            private static void DrawLineSeries(
                Painter2D painter,
                NativeArray<PathfindingTelemetryEntry> telemetry,
                int latestIndex,
                int columns,
                Rect rect,
                int series,
                Color color)
            {
                painter.lineWidth = 1.5f;
                painter.strokeColor = color;
                painter.BeginPath();
                bool began = false;
                int start = latestIndex - columns + 1;
                for (int i = 0; i < columns; i++)
                {
                    int index = start + i;
                    while (index < 0)
                        index += telemetry.Length;
                    index %= telemetry.Length;

                    PathfindingTelemetryEntry entry = telemetry[index];
                    if (entry.Frame == 0u)
                        continue;

                    float x = rect.xMin + (columns <= 1 ? 0f : (rect.width * i) / (columns - 1));
                    float y = rect.yMax - ResolveSample01(in entry, series) * rect.height;
                    Vector2 point = new Vector2(x, y);
                    if (!began)
                    {
                        painter.MoveTo(point);
                        began = true;
                    }
                    else
                    {
                        painter.LineTo(point);
                    }
                }

                if (began)
                    painter.Stroke();
            }

            private static float ResolveSample01(in PathfindingTelemetryEntry entry, int series)
            {
                if (series == 0)
                    return math.saturate(entry.NodesExpanded / MaxNodesExpanded);
                return math.saturate(entry.BurstMicros / MaxBurstMicros);
            }

            private static void DrawRect(Painter2D painter, Rect rect, Color color)
            {
                painter.fillColor = color;
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                painter.ClosePath();
                painter.Fill();
            }
        }
    }
}
