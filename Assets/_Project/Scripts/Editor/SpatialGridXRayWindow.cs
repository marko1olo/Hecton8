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
        private const int CounterDebugCellCount = 8;
        private const int RawGridScratchCapacity = 256;
        private const int DebugCellScratchCapacity = 256;
        private static readonly Color RawGridLowColor = new Color(0.1f, 0.35f, 1f, 0.22f);
        private static readonly Color RawGridHighColor = new Color(1f, 0.05f, 0.02f, 0.62f);
        private static readonly Color DebugCellLowColor = new Color(0.1f, 0.35f, 1f, 0.22f);
        private static readonly Color DebugCellHighColor = new Color(1f, 0.05f, 0.02f, 0.62f);
        private static readonly RawGridCandidate[] RawGridCandidates = new RawGridCandidate[RawGridScratchCapacity];
        private static readonly RawGridDrawCell[] RawGridDrawScratch = new RawGridDrawCell[RawGridScratchCapacity];
        private static readonly ShinobuSpatialHashDebugCell[] DebugCellScratch = new ShinobuSpatialHashDebugCell[DebugCellScratchCapacity];
        private static readonly BufferID[] TelemetryReadSet =
        {
            BufferID.ShinobuSpatialGridTelemetryRing,
            BufferID.ShinobuSpatialGridTelemetryCursor,
            BufferID.ShinobuSpatialGridTuning
        };
        private static readonly ulong RawGridTelemetryMutationGuardMask =
            SpatialGridMutationGuardBit(BufferID.ShinobuSpatialGridTelemetryCursor) |
            SpatialGridMutationGuardBit(BufferID.ShinobuSpatialGridTelemetryRing);
        private readonly Label[] _histogram = new Label[HistogramBars];
        private Label _summaryLabel;
        private Slider _cellSizeSlider;
        private IntegerField _maxResultsField;
        private IntegerField _hashXField;
        private IntegerField _hashYField;
        private IntegerField _hashZField;
        private Toggle _drawGridToggle;
        private bool _updatingControls;

        private struct RawGridCandidate
        {
            public SpatialGridBucketRangeDTO Range;
            public int EntityIndex;
            public float3 Position;
            public AmbientEntityAupDTO Aup;
        }

        private struct RawGridDrawCell
        {
            public float3 Center;
            public float CellSizeMeters;
            public int Occupancy;
        }

        [MenuItem("HECTON-8/AI/Spatial Grid X-Ray")]
        public static void Open()
        {
            GetWindow<SpatialGridXRayWindow>("Spatial Grid X-Ray");
        }

        private void OnEnable()
        {
            EditorApplication.update -= TickEditor;
            EditorApplication.update += TickEditor;
            SceneView.duringSceneGui -= DrawSceneGrid;
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

            if (!TryAcquireReadSetGuard(vault, TelemetryReadSet, out ulong readSetGuardMask))
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
                ReleaseReadSetGuard(vault, readSetGuardMask);
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
            if (!TryBuildRawGridDrawScratch(vault, out int drawCount))
                return false;

            for (int i = 0; i < drawCount; i++)
            {
                RawGridDrawCell cell = RawGridDrawScratch[i];
                float density = math.saturate(cell.Occupancy / 64f);
                Handles.color = Color.Lerp(RawGridLowColor, RawGridHighColor, density);
                Handles.DrawWireCube((Vector3)cell.Center, Vector3.one * cell.CellSizeMeters);
            }

            return drawCount > 0;
        }

        private static bool TryBuildRawGridDrawScratch(IDataVault vault, out int drawCount)
        {
            drawCount = 0;
            if (!TryReadRawGridTelemetry(vault, out SpatialGridTelemetryEntry latest) ||
                latest.Frame == 0u ||
                latest.CellSizeMeters <= 0f ||
                !TryCopyRawGridRanges(vault, latest.Frame, out int candidateCount) ||
                !TryCopyRawGridEntries(vault, candidateCount, out candidateCount) ||
                !TryCopyRawGridPositions(vault, candidateCount, out candidateCount) ||
                !TryCopyRawGridAups(vault, candidateCount, out candidateCount))
            {
                return false;
            }

            float cellSize = math.max(0.25f, latest.CellSizeMeters);
            for (int i = 0; i < candidateCount && drawCount < RawGridDrawScratch.Length; i++)
            {
                RawGridCandidate candidate = RawGridCandidates[i];
                double3 absolute = candidate.Aup.PositionAup.ToAbsoluteDouble3();
                if (!math.all(math.isfinite(absolute)))
                    continue;

                double3 centerAbsolute = absolute - (double3)candidate.Position;
                SpatialGridCell64 gridCell = ShinobuSpatialGridMath.QuantizeCell(absolute, cellSize);
                double3 absoluteCellCenter = math.double3(
                    (gridCell.X + 0.5d) * cellSize,
                    (gridCell.Y + 0.5d) * cellSize,
                    (gridCell.Z + 0.5d) * cellSize);
                float3 center = (float3)(absoluteCellCenter - centerAbsolute);
                if (!math.all(math.isfinite(center)))
                    continue;

                RawGridDrawCell drawCell = default;
                drawCell.Center = center;
                drawCell.CellSizeMeters = cellSize;
                drawCell.Occupancy = candidate.Range.Count;
                RawGridDrawScratch[drawCount++] = drawCell;
            }

            return drawCount > 0;
        }

        private static bool TryReadRawGridTelemetry(IDataVault vault, out SpatialGridTelemetryEntry latest)
        {
            latest = default;
            int current = 0;
            if (vault == null ||
                !vault.TryAcquireMutationGuard(RawGridTelemetryMutationGuardMask))
            {
                return false;
            }

            try
            {
                if (!TryRead(vault, BufferID.ShinobuSpatialGridTelemetryCursor, out NativeArray<int> cursor) ||
                    cursor.Length <= 0)
                {
                    return false;
                }

                current = math.max(0, cursor[0]);
                if (!TryRead(vault, BufferID.ShinobuSpatialGridTelemetryRing, out NativeArray<SpatialGridTelemetryEntry> telemetry) ||
                    telemetry.Length <= 0)
                {
                    return false;
                }

                int lastIndex = current > 0 ? (current - 1) % telemetry.Length : 0;
                latest = telemetry[lastIndex];
                return latest.Frame != 0u && latest.CellSizeMeters > 0f;
            }
            finally
            {
                vault.ReleaseMutationGuard(RawGridTelemetryMutationGuardMask);
            }
        }

        private static bool TryCopyRawGridRanges(IDataVault vault, uint frame, out int candidateCount)
        {
            candidateCount = 0;
            if (vault == null ||
                !TryAcquireSingleBufferGuard(vault, BufferID.ShinobuSpatialGridBucketRanges, out ulong guardMask))
            {
                return false;
            }

            try
            {
                if (!TryRead(vault, BufferID.ShinobuSpatialGridBucketRanges, out NativeArray<SpatialGridBucketRangeDTO> ranges) ||
                    ranges.Length <= 0)
                {
                    return false;
                }

                for (int i = 0; i < ranges.Length && candidateCount < RawGridCandidates.Length; i++)
                {
                    SpatialGridBucketRangeDTO range = ranges[i];
                    if (range.Flags != frame || range.CellHash == 0u || range.Count <= 0)
                        continue;

                    RawGridCandidate candidate = default;
                    candidate.Range = range;
                    RawGridCandidates[candidateCount++] = candidate;
                }

                return candidateCount > 0;
            }
            finally
            {
                ReleaseReadSetGuard(vault, guardMask);
            }
        }

        private static bool TryCopyRawGridEntries(IDataVault vault, int candidateCount, out int compactedCount)
        {
            compactedCount = 0;
            if (vault == null ||
                candidateCount <= 0 ||
                !TryAcquireSingleBufferGuard(vault, BufferID.ShinobuSpatialGridEntries, out ulong guardMask))
            {
                return false;
            }

            try
            {
                if (!TryRead(vault, BufferID.ShinobuSpatialGridEntries, out NativeArray<SpatialGridEntryDTO> entries) ||
                    entries.Length <= 0)
                {
                    return false;
                }

                for (int i = 0; i < candidateCount; i++)
                {
                    RawGridCandidate candidate = RawGridCandidates[i];
                    if ((uint)candidate.Range.StartIndex >= (uint)entries.Length)
                        continue;

                    SpatialGridEntryDTO first = entries[candidate.Range.StartIndex];
                    int entityIndex = (int)first.EntityRowIndex;
                    if (entityIndex < 0)
                        continue;

                    candidate.EntityIndex = entityIndex;
                    RawGridCandidates[compactedCount++] = candidate;
                }

                return compactedCount > 0;
            }
            finally
            {
                ReleaseReadSetGuard(vault, guardMask);
            }
        }

        private static bool TryCopyRawGridPositions(IDataVault vault, int candidateCount, out int compactedCount)
        {
            compactedCount = 0;
            if (vault == null ||
                candidateCount <= 0 ||
                !TryAcquireSingleBufferGuard(vault, BufferID.ShinobuAmbientEntitySnapshot, out ulong guardMask))
            {
                return false;
            }

            try
            {
                if (!TryRead(vault, BufferID.ShinobuAmbientEntitySnapshot, out NativeArray<AmbientEntityDTO> entities) ||
                    entities.Length <= 0)
                {
                    return false;
                }

                for (int i = 0; i < candidateCount; i++)
                {
                    RawGridCandidate candidate = RawGridCandidates[i];
                    if ((uint)candidate.EntityIndex >= (uint)entities.Length)
                        continue;

                    float3 position = entities[candidate.EntityIndex].Position;
                    if (!math.all(math.isfinite(position)))
                        continue;

                    candidate.Position = position;
                    RawGridCandidates[compactedCount++] = candidate;
                }

                return compactedCount > 0;
            }
            finally
            {
                ReleaseReadSetGuard(vault, guardMask);
            }
        }

        private static bool TryCopyRawGridAups(IDataVault vault, int candidateCount, out int compactedCount)
        {
            compactedCount = 0;
            if (vault == null ||
                candidateCount <= 0 ||
                !TryAcquireSingleBufferGuard(vault, BufferID.ShinobuAmbientAupSnapshot, out ulong guardMask))
            {
                return false;
            }

            try
            {
                if (!TryRead(vault, BufferID.ShinobuAmbientAupSnapshot, out NativeArray<AmbientEntityAupDTO> aups) ||
                    aups.Length <= 0)
                {
                    return false;
                }

                for (int i = 0; i < candidateCount; i++)
                {
                    RawGridCandidate candidate = RawGridCandidates[i];
                    if ((uint)candidate.EntityIndex >= (uint)aups.Length)
                        continue;

                    candidate.Aup = aups[candidate.EntityIndex];
                    RawGridCandidates[compactedCount++] = candidate;
                }

                return compactedCount > 0;
            }
            finally
            {
                ReleaseReadSetGuard(vault, guardMask);
            }
        }

        private static void DrawDebugCellsFallback(IDataVault vault)
        {
            int requestedCount = ReadDebugCellCount(vault);
            if (requestedCount <= 0 ||
                !TryCopyDebugCells(vault, requestedCount, out int copiedCount))
            {
                return;
            }

            for (int i = 0; i < copiedCount; i++)
            {
                ShinobuSpatialHashDebugCell cell = DebugCellScratch[i];
                if (cell.Flags == 0u || cell.Occupancy <= 0 || cell.CellSizeMeters <= 0f)
                    continue;

                float density = math.saturate(cell.Occupancy / 64f);
                Handles.color = Color.Lerp(DebugCellLowColor, DebugCellHighColor, density);
                Handles.DrawWireCube((Vector3)cell.CenterLocal, Vector3.one * cell.CellSizeMeters);
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

        private static int ReadCounter(NativeArray<int> counters, int index)
        {
            if (!counters.IsCreated || (uint)index >= (uint)counters.Length)
                return 0;

            return counters[index];
        }

        private static int ReadDebugCellCount(IDataVault vault)
        {
            if (vault == null || !TryAcquireSingleBufferGuard(vault, BufferID.ShinobuEcosystemCounters, out ulong guardMask))
                return 0;

            try
            {
                if (!TryRead(vault, BufferID.ShinobuEcosystemCounters, out NativeArray<int> counters))
                    return 0;

                return math.clamp(ReadCounter(counters, CounterDebugCellCount), 0, DebugCellScratchCapacity);
            }
            finally
            {
                ReleaseReadSetGuard(vault, guardMask);
            }
        }

        private static bool TryCopyDebugCells(IDataVault vault, int requestedCount, out int copiedCount)
        {
            copiedCount = 0;
            if (vault == null ||
                requestedCount <= 0 ||
                !TryAcquireSingleBufferGuard(vault, BufferID.ShinobuSpatialHashDebugCells, out ulong guardMask))
            {
                return false;
            }

            try
            {
                if (!TryRead(vault, BufferID.ShinobuSpatialHashDebugCells, out NativeArray<ShinobuSpatialHashDebugCell> cells))
                    return false;

                int count = math.min(math.min(requestedCount, cells.Length), DebugCellScratch.Length);
                for (int i = 0; i < count; i++)
                    DebugCellScratch[i] = cells[i];
                copiedCount = count;
                return copiedCount > 0;
            }
            finally
            {
                ReleaseReadSetGuard(vault, guardMask);
            }
        }

        private static bool TryAcquireSingleBufferGuard(IDataVault vault, BufferID bufferId, out ulong guardMask)
        {
            guardMask = SpatialGridMutationGuardBit(bufferId);
            return vault != null && vault.TryAcquireMutationGuard(guardMask);
        }

        private static bool TryAcquireReadSetGuard(IDataVault vault, BufferID[] set, out ulong guardMask)
        {
            guardMask = 0UL;
            if (vault == null || set == null)
                return false;

            for (int i = 0; i < set.Length; i++)
                guardMask |= SpatialGridMutationGuardBit(set[i]);

            return guardMask != 0UL && vault.TryAcquireMutationGuard(guardMask);
        }

        private static void ReleaseReadSetGuard(IDataVault vault, ulong guardMask)
        {
            if (vault == null || guardMask == 0UL)
                return;

            vault.ReleaseMutationGuard(guardMask);
        }

        private static ulong SpatialGridMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 31);
        }
    }
}
#endif
