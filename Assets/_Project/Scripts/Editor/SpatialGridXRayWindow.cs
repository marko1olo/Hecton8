#if UNITY_EDITOR
using Hecton8.AI.Ecosystem;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class SpatialGridXRayWindow : EditorWindow
    {
        private const int HistogramBars = 64;
        private static readonly BufferID[] TelemetryReadSet =
        {
            BufferID.ShinobuSpatialGridTelemetryRing,
            BufferID.ShinobuSpatialGridTelemetryCursor,
            BufferID.ShinobuSpatialGridTuning
        };
        private static readonly BufferID[] RawGridReadSet =
        {
            BufferID.ShinobuSpatialGridEntries,
            BufferID.ShinobuSpatialGridBucketRanges,
            BufferID.ShinobuAmbientEntitySnapshot,
            BufferID.ShinobuAmbientAupSnapshot,
            BufferID.ShinobuSpatialGridTelemetryRing,
            BufferID.ShinobuSpatialGridTelemetryCursor
        };
        private static readonly BufferID[] DebugCellsReadSet =
        {
            BufferID.ShinobuSpatialHashDebugCells
        };
        private readonly Label[] _histogram = new Label[HistogramBars];
        private Label _summaryLabel;
        private Slider _cellSizeSlider;
        private IntegerField _maxResultsField;
        private IntegerField _hashXField;
        private IntegerField _hashYField;
        private IntegerField _hashZField;
        private Toggle _drawGridToggle;
        private bool _updatingControls;

        [MenuItem("HECTON-8/AI/Spatial Grid X-Ray")]
        public static void Open()
        {
            GetWindow<SpatialGridXRayWindow>("Spatial Grid X-Ray");
        }

        private void OnEnable()
        {
            EditorApplication.update += TickEditor;
            SceneView.duringSceneGui += DrawSceneGrid;
        }

        private void OnDisable()
        {
            EditorApplication.update -= TickEditor;
            SceneView.duringSceneGui -= DrawSceneGrid;
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            _summaryLabel = new Label("No DataVault spatial grid.");
            rootVisualElement.Add(_summaryLabel);

            _cellSizeSlider = new Slider("Base Grid Cell Size", 2f, 64f) { value = 10f };
            _cellSizeSlider.RegisterValueChangedCallback(evt => MutateTuning(0, evt.newValue, 0));
            rootVisualElement.Add(_cellSizeSlider);

            _maxResultsField = new IntegerField("Max Query Results") { value = ShinobuSpatialGridConstants.DefaultMaxQueryResults };
            _maxResultsField.RegisterValueChangedCallback(evt => MutateTuning(1, 0f, evt.newValue));
            rootVisualElement.Add(_maxResultsField);

            _hashXField = new IntegerField("Hash Multiplier X") { value = (int)ShinobuSpatialGridConstants.DefaultHashMultiplierX };
            _hashXField.RegisterValueChangedCallback(evt => MutateTuning(2, 0f, evt.newValue));
            rootVisualElement.Add(_hashXField);

            _hashYField = new IntegerField("Hash Multiplier Y") { value = (int)ShinobuSpatialGridConstants.DefaultHashMultiplierY };
            _hashYField.RegisterValueChangedCallback(evt => MutateTuning(3, 0f, evt.newValue));
            rootVisualElement.Add(_hashYField);

            _hashZField = new IntegerField("Hash Multiplier Z") { value = (int)ShinobuSpatialGridConstants.DefaultHashMultiplierZ };
            _hashZField.RegisterValueChangedCallback(evt => MutateTuning(4, 0f, evt.newValue));
            rootVisualElement.Add(_hashZField);

            _drawGridToggle = new Toggle("Draw Live Buckets") { value = true };
            rootVisualElement.Add(_drawGridToggle);

            VisualElement bars = new VisualElement();
            bars.style.flexDirection = FlexDirection.Row;
            bars.style.height = 80;
            rootVisualElement.Add(bars);
            for (int i = 0; i < HistogramBars; i++)
            {
                Label bar = new Label();
                bar.style.flexGrow = 1;
                bar.style.marginRight = 1;
                bar.style.backgroundColor = new Color(0.08f, 0.22f, 0.72f, 0.55f);
                _histogram[i] = bar;
                bars.Add(bar);
            }
        }

        private void TickEditor()
        {
            RefreshTelemetry();
        }

        private void RefreshTelemetry()
        {
            if (!TryResolveDiagnosticVault(out IDataVault vault))
            {
                if (_summaryLabel != null)
                    _summaryLabel.text = "Spatial grid Vault buffers are not registered.";
                return;
            }

            if (!TryLockReadSet(vault, TelemetryReadSet, out int lockedCount))
            {
                if (_summaryLabel != null)
                    _summaryLabel.text = "Spatial grid Vault buffers are busy.";
                return;
            }

            try
            {
                if (!TryRead(vault, BufferID.ShinobuSpatialGridTelemetryRing, out NativeArray<SpatialGridTelemetryEntry> telemetry) ||
                    !TryRead(vault, BufferID.ShinobuSpatialGridTelemetryCursor, out NativeArray<int> cursor) ||
                    !TryRead(vault, BufferID.ShinobuSpatialGridTuning, out NativeArray<SpatialGridTuningDTO> tuningArray) ||
                    telemetry.Length <= 0 ||
                    cursor.Length <= 0 ||
                    tuningArray.Length <= 0)
                {
                    if (_summaryLabel != null)
                        _summaryLabel.text = "Spatial grid Vault buffers are not registered.";
                    return;
                }

                SpatialGridTuningDTO tuning = ShinobuSpatialGridMath.Sanitize(tuningArray[0]);
                _updatingControls = true;
                if (_cellSizeSlider != null) _cellSizeSlider.SetValueWithoutNotify(tuning.BaseGridCellSize);
                if (_maxResultsField != null) _maxResultsField.SetValueWithoutNotify(tuning.MaxQueryResultsLimit);
                if (_hashXField != null) _hashXField.SetValueWithoutNotify((int)tuning.HashMultiplierX);
                if (_hashYField != null) _hashYField.SetValueWithoutNotify((int)tuning.HashMultiplierY);
                if (_hashZField != null) _hashZField.SetValueWithoutNotify((int)tuning.HashMultiplierZ);
                _updatingControls = false;

                int current = math.max(0, cursor[0]);
                int lastIndex = current > 0 ? (current - 1) % telemetry.Length : 0;
                SpatialGridTelemetryEntry latest = telemetry[lastIndex];
                if (_summaryLabel != null)
                {
                    string sortText = latest.SortMicroseconds >= 0f
                        ? latest.SortMicroseconds.ToString("0.0") + "us"
                        : "N/A";
                    _summaryLabel.text =
                        "Frame " + latest.Frame +
                        " | Entities " + latest.EntityCount +
                        " | Queries " + latest.QueryCount +
                        " | Max Bucket " + latest.MaxBucketOccupancy +
                        " | Ranges " + latest.BucketRangeCount +
                        " | Cell " + latest.CellSizeMeters.ToString("0.00") +
                        "m | Sort " + sortText +
                        " | Invalid " + latest.InvalidInputCount +
                        " | Overflow " + latest.OverflowCount;
                }

                int maxBar = math.max(1, latest.MaxBucketOccupancy);
                int samples = math.min(HistogramBars, telemetry.Length);
                for (int i = 0; i < samples; i++)
                {
                    int index = current - samples + i;
                    if (index < 0)
                        index += telemetry.Length;
                    SpatialGridTelemetryEntry entry = telemetry[index % telemetry.Length];
                    float h = math.clamp(entry.MaxBucketOccupancy / (float)maxBar, 0.04f, 1f);
                    float sortHeat = entry.SortMicroseconds >= 0f ? math.saturate(entry.SortMicroseconds / 1000f) : 0f;
                    float colorHeat = math.max(h, sortHeat);
                    Label bar = _histogram[i];
                    if (bar == null)
                        continue;
                    bar.style.height = 6f + h * 72f;
                    bar.style.backgroundColor = Color.Lerp(new Color(0.05f, 0.35f, 0.95f, 0.55f), new Color(0.95f, 0.1f, 0.04f, 0.8f), colorHeat);
                }
            }
            finally
            {
                UnlockReadSet(vault, TelemetryReadSet, lockedCount);
            }
        }

        private void DrawSceneGrid(SceneView sceneView)
        {
            if (_drawGridToggle != null && !_drawGridToggle.value)
                return;

            if (!TryResolveDiagnosticVault(out IDataVault vault))
            {
                return;
            }

            if (DrawRawSpatialGrid(vault))
                return;

            DrawDebugCellsFallback(vault);
        }

        private static bool DrawRawSpatialGrid(IDataVault vault)
        {
            if (!TryLockReadSet(vault, RawGridReadSet, out int lockedCount))
            {
                return false;
            }

            try
            {
                if (!TryRead(vault, BufferID.ShinobuSpatialGridEntries, out NativeArray<SpatialGridEntryDTO> entries) ||
                    !TryRead(vault, BufferID.ShinobuSpatialGridBucketRanges, out NativeArray<SpatialGridBucketRangeDTO> ranges) ||
                    !TryRead(vault, BufferID.ShinobuAmbientEntitySnapshot, out NativeArray<AmbientEntityDTO> entities) ||
                    !TryRead(vault, BufferID.ShinobuAmbientAupSnapshot, out NativeArray<AmbientEntityAupDTO> aups) ||
                    !TryRead(vault, BufferID.ShinobuSpatialGridTelemetryRing, out NativeArray<SpatialGridTelemetryEntry> telemetry) ||
                    !TryRead(vault, BufferID.ShinobuSpatialGridTelemetryCursor, out NativeArray<int> cursor) ||
                    entries.Length <= 0 ||
                    ranges.Length <= 0 ||
                    entities.Length <= 0 ||
                    aups.Length <= 0 ||
                    telemetry.Length <= 0 ||
                    cursor.Length <= 0)
                {
                    return false;
                }

                int current = math.max(0, cursor[0]);
                int lastIndex = current > 0 ? (current - 1) % telemetry.Length : 0;
                SpatialGridTelemetryEntry latest = telemetry[lastIndex];
                if (latest.Frame == 0u || latest.CellSizeMeters <= 0f)
                    return false;

                int drawn = 0;
                int maxDrawn = math.min(256, ranges.Length);
                float cellSize = math.max(0.25f, latest.CellSizeMeters);
                for (int i = 0; i < ranges.Length && drawn < maxDrawn; i++)
                {
                    SpatialGridBucketRangeDTO range = ranges[i];
                    if (range.Flags != latest.Frame || range.CellHash == 0u || range.Count <= 0 || (uint)range.StartIndex >= (uint)entries.Length)
                        continue;

                    SpatialGridEntryDTO first = entries[range.StartIndex];
                    int entityIndex = (int)first.EntityRowIndex;
                    if ((uint)entityIndex >= (uint)entities.Length || (uint)entityIndex >= (uint)aups.Length)
                        continue;

                    float3 position = entities[entityIndex].Position;
                    if (!math.all(math.isfinite(position)))
                        continue;

                    double3 absolute = aups[entityIndex].PositionAup.ToAbsoluteDouble3();
                    if (!math.all(math.isfinite(absolute)))
                        continue;

                    double3 centerAbsolute = absolute - (double3)position;
                    SpatialGridCell64 gridCell = ShinobuSpatialGridMath.QuantizeCell(absolute, cellSize);
                    double3 absoluteCellCenter = new double3(
                        (gridCell.X + 0.5d) * cellSize,
                        (gridCell.Y + 0.5d) * cellSize,
                        (gridCell.Z + 0.5d) * cellSize);
                    float3 center = (float3)(absoluteCellCenter - centerAbsolute);
                    if (!math.all(math.isfinite(center)))
                        continue;

                    float density = math.saturate(range.Count / 64f);
                    Handles.color = Color.Lerp(new Color(0.1f, 0.35f, 1f, 0.22f), new Color(1f, 0.05f, 0.02f, 0.62f), density);
                    Handles.DrawWireCube((Vector3)center, Vector3.one * cellSize);
                    drawn++;
                }

                return drawn > 0;
            }
            finally
            {
                UnlockReadSet(vault, RawGridReadSet, lockedCount);
            }
        }

        private static void DrawDebugCellsFallback(IDataVault vault)
        {
            if (!TryLockReadSet(vault, DebugCellsReadSet, out int lockedCount))
                return;

            try
            {
                if (!TryRead(vault, BufferID.ShinobuSpatialHashDebugCells, out NativeArray<ShinobuSpatialHashDebugCell> cells))
                    return;

                for (int i = 0; i < cells.Length; i++)
                {
                    ShinobuSpatialHashDebugCell cell = cells[i];
                    if (cell.Flags == 0u || cell.Occupancy <= 0 || cell.CellSizeMeters <= 0f)
                        continue;

                    float density = math.saturate(cell.Occupancy / 64f);
                    Handles.color = Color.Lerp(new Color(0.1f, 0.35f, 1f, 0.22f), new Color(1f, 0.05f, 0.02f, 0.62f), density);
                    Handles.DrawWireCube((Vector3)cell.CenterLocal, Vector3.one * cell.CellSizeMeters);
                }
            }
            finally
            {
                UnlockReadSet(vault, DebugCellsReadSet, lockedCount);
            }
        }

        private void MutateTuning(int field, float floatValue, int intValue)
        {
            if (_updatingControls)
                return;

            if (!TryResolveDiagnosticVault(out IDataVault vault) ||
                !vault.TryGetGenerationHandle(BufferID.ShinobuSpatialGridTuning, out VaultGenerationHandle<SpatialGridTuningDTO> handle) ||
                !vault.TryAcquireWriteLock(in handle, SystemID.CoreDiagnostics, out NativeArray<SpatialGridTuningDTO> tuning))
            {
                return;
            }

            try
            {
                if (!tuning.IsCreated || tuning.Length <= 0)
                {
                    return;
                }

                SpatialGridTuningDTO dto = tuning[0];
                if (field == 0)
                    dto.BaseGridCellSize = floatValue;
                else if (field == 1)
                    dto.MaxQueryResultsLimit = math.clamp(intValue, 1, 256);
                else if (field == 2)
                    dto.HashMultiplierX = (uint)math.max(1, intValue);
                else if (field == 3)
                    dto.HashMultiplierY = (uint)math.max(1, intValue);
                else if (field == 4)
                    dto.HashMultiplierZ = (uint)math.max(1, intValue);
                tuning[0] = ShinobuSpatialGridMath.Sanitize(dto);
                Repaint();
                SceneView.RepaintAll();
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            }
        }

        private static bool TryResolveDiagnosticVault(out IDataVault vault)
        {
            vault = default;
            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latest))
                return false;

            vault = latest;
            return true;
        }

        private static bool TryRead<T>(IDataVault vault, BufferID bufferId, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) &&
                   handle.BufferID != 0u &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private static bool TryLockReadSet(IDataVault vault, BufferID[] set, out int lockedCount)
        {
            lockedCount = 0;
            if (vault == null || set == null)
                return false;

            for (int i = 0; i < set.Length; i++)
            {
                if (!vault.TryLockBuffer(set[i], SystemID.CoreDiagnostics))
                {
                    UnlockReadSet(vault, set, lockedCount);
                    lockedCount = 0;
                    return false;
                }

                lockedCount++;
            }

            return true;
        }

        private static void UnlockReadSet(IDataVault vault, BufferID[] set, int lockedCount)
        {
            if (vault == null || set == null)
                return;

            int count = math.min(lockedCount, set.Length);
            for (int i = count - 1; i >= 0; i--)
                vault.TryUnlockBuffer(set[i], SystemID.CoreDiagnostics);
        }
    }
}
#endif
