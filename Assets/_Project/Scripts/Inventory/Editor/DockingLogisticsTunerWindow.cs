using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Inventory.Editor
{
    internal sealed unsafe class DockingLogisticsTunerWindow : EditorWindow
    {
        private const float HistogramHeightPixels = 128f;

        private Slider _qualitySlider;
        private SliderInt _maxItemsSlider;
        private Slider _overflowScatterSlider;
        private SliderInt _filterMaskSlider;
        private Label _stateLabel;
        private CargoTelemetryHistogramElement _histogram;

        [MenuItem("HECTON-8/Inventory/Docking Logistics Tuner")]
        private static void Open()
        {
            GetWindow<DockingLogisticsTunerWindow>("Docking Logistics");
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8f;
            rootVisualElement.style.paddingRight = 8f;
            rootVisualElement.style.paddingTop = 8f;
            rootVisualElement.style.paddingBottom = 8f;

            _stateLabel = new Label();
            rootVisualElement.Add(_stateLabel);

            _qualitySlider = CreateSlider("GlobalQualityWeight", 0f, 1f);
            _maxItemsSlider = CreateIntSlider("MaxItemsPerFrame", 0, 10000);
            _overflowScatterSlider = CreateSlider("OverflowScatterRadius", 0f, 32f);
            _filterMaskSlider = CreateIntSlider("FilterHashMask", 0, int.MaxValue);

            _histogram = new CargoTelemetryHistogramElement();
            rootVisualElement.Add(_histogram);

            rootVisualElement.Add(new Button(RefreshFromVault) { text = "Refresh From Vault" });
            RegisterCallbacks();
            RefreshFromVault();
            rootVisualElement.schedule.Execute(RefreshHistogram).Every(250);
        }

        private Slider CreateSlider(string label, float low, float high)
        {
            Slider slider = new Slider(label, low, high) { showInputField = true };
            rootVisualElement.Add(slider);
            return slider;
        }

        private SliderInt CreateIntSlider(string label, int low, int high)
        {
            SliderInt slider = new SliderInt(label, low, high) { showInputField = true };
            rootVisualElement.Add(slider);
            return slider;
        }

        private void RegisterCallbacks()
        {
            _qualitySlider.RegisterValueChangedCallback(evt => Mutate((ref CargoSyncTuningDTO t) => t.GlobalQualityWeight = math.saturate(evt.newValue)));
            _maxItemsSlider.RegisterValueChangedCallback(evt => Mutate((ref CargoSyncTuningDTO t) => t.DesignerMaxItemsPerFrame = math.max(0, evt.newValue)));
            _overflowScatterSlider.RegisterValueChangedCallback(evt => Mutate((ref CargoSyncTuningDTO t) => t.OverflowScatterRadiusMeters = math.max(0f, evt.newValue)));
            _filterMaskSlider.RegisterValueChangedCallback(evt => Mutate((ref CargoSyncTuningDTO t) => t.FilterHashMask = unchecked((uint)math.max(0, evt.newValue))));
        }

        private void RefreshFromVault()
        {
            if (!EnsureTuningBufferAvailable(out NativeArray<CargoSyncTuningDTO> tuningBuffer))
            {
                _stateLabel.text = "Cargo tuning buffer unavailable";
                return;
            }

            CargoSyncTuningDTO tuning = Sanitize(tuningBuffer[0]);
            _qualitySlider.SetValueWithoutNotify(tuning.GlobalQualityWeight);
            _maxItemsSlider.SetValueWithoutNotify(math.clamp(tuning.DesignerMaxItemsPerFrame, 0, 10000));
            _overflowScatterSlider.SetValueWithoutNotify(tuning.OverflowScatterRadiusMeters);
            _filterMaskSlider.SetValueWithoutNotify((int)math.min((uint)int.MaxValue, tuning.FilterHashMask));
            _stateLabel.text = "Cargo vault tuning live";
        }

        private void Mutate(TuningMutation mutation)
        {
            if (!EnsureAndAcquireTuningWrite(out GlobalDataVault vault, out VaultGenerationHandle<CargoSyncTuningDTO> handle, out NativeArray<CargoSyncTuningDTO> tuningBuffer))
            {
                _stateLabel.text = "Cargo tuning write lock unavailable";
                return;
            }

            try
            {
                ref CargoSyncTuningDTO tuningRef = ref UnsafeUtility.AsRef<CargoSyncTuningDTO>(NativeArrayUnsafeUtility.GetUnsafePtr(tuningBuffer));
                CargoSyncTuningDTO tuning = Sanitize(tuningRef);
                mutation(ref tuning);
                tuningRef = Sanitize(tuning);
                _stateLabel.text = "Cargo vault tuning updated";
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            }
        }

        private void RefreshHistogram()
        {
            _histogram?.MarkDirtyRepaint();
        }

        private delegate void TuningMutation(ref CargoSyncTuningDTO tuning);

        private static bool EnsureTuningBufferAvailable(out NativeArray<CargoSyncTuningDTO> tuning)
        {
            tuning = default;
            if (!AcquireEditorVaultSnapshot(out GlobalDataVault vault))
                return false;

            EnsureCargoBuffersForEditor(vault);
            if (!vault.TryGetGenerationHandle<CargoSyncTuningDTO>(BufferID.ShinobuCargoSyncTuning, out VaultGenerationHandle<CargoSyncTuningDTO> handle))
                return false;

            return vault.TryReadHandle(in handle, out tuning) && tuning.IsCreated && tuning.Length > 0;
        }

        private static bool TryResolveTelemetry(out NativeArray<CargoTelemetryEntry> telemetry, out int cursor)
        {
            telemetry = default;
            cursor = 0;
            if (!AcquireEditorVaultSnapshot(out GlobalDataVault vault))
                return false;

            if (!vault.TryGetGenerationHandle<CargoTelemetryEntry>(BufferID.ShinobuCargoSyncTelemetry, out VaultGenerationHandle<CargoTelemetryEntry> telemetryHandle) ||
                !vault.TryReadHandle(in telemetryHandle, out telemetry) ||
                !telemetry.IsCreated ||
                telemetry.Length == 0)
            {
                return false;
            }

            if (vault.TryGetGenerationHandle<CargoAtomicCounterDTO>(BufferID.ShinobuCargoSyncTelemetryCursor, out VaultGenerationHandle<CargoAtomicCounterDTO> cursorHandle) &&
                vault.TryReadHandle(in cursorHandle, out NativeArray<CargoAtomicCounterDTO> cursorBuffer) &&
                cursorBuffer.IsCreated &&
                cursorBuffer.Length > 0)
            {
                cursor = math.max(0, cursorBuffer[0].Value);
            }

            return true;
        }

        private static bool EnsureAndAcquireTuningWrite(
            out GlobalDataVault vault,
            out VaultGenerationHandle<CargoSyncTuningDTO> handle,
            out NativeArray<CargoSyncTuningDTO> tuning)
        {
            vault = null;
            handle = default;
            tuning = default;
            if (!AcquireEditorVaultSnapshot(out vault))
                return false;

            EnsureCargoBuffersForEditor(vault);
            if (!vault.TryGetGenerationHandle<CargoSyncTuningDTO>(BufferID.ShinobuCargoSyncTuning, out handle))
                return false;

            if (!vault.TryAcquireWriteLock(in handle, SystemID.CoreDiagnostics, out tuning))
                return false;

            if (tuning.IsCreated && tuning.Length > 0)
                return true;

            vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            tuning = default;
            return false;
        }

        private static bool AcquireEditorVaultSnapshot(out GlobalDataVault vault)
        {
            return GlobalDataVault.TryGetLatestCreated(out vault) && vault != null && !vault.IsCompactionFenceActive;
        }

        private static void EnsureCargoBuffersForEditor(GlobalDataVault vault)
        {
            SoaInventoryQueryEngine.EnsureCargoSyncVaultBuffers(
                vault,
                SoaInventoryQueryEngine.DefaultCargoTransactionCapacity,
                SoaInventoryQueryEngine.DefaultCargoLootCacheCapacity,
                SoaInventoryQueryEngine.DefaultCargoFilterProfileCapacity);
        }

        private static CargoSyncTuningDTO Sanitize(CargoSyncTuningDTO tuning)
        {
            if (tuning.ProgressVisualSeconds <= 0f)
                tuning.ProgressVisualSeconds = 1.25f;

            tuning.GlobalQualityWeight = math.saturate(math.select(1f, tuning.GlobalQualityWeight, math.isfinite(tuning.GlobalQualityWeight)));
            tuning.OverflowScatterRadiusMeters = math.clamp(math.select(2.5f, tuning.OverflowScatterRadiusMeters, math.isfinite(tuning.OverflowScatterRadiusMeters)), 0f, 32f);
            tuning.DesignerMaxItemsPerFrame = math.clamp(tuning.DesignerMaxItemsPerFrame, 0, 10000);
            tuning.ProgressVisualSeconds = math.clamp(math.select(1.25f, tuning.ProgressVisualSeconds, math.isfinite(tuning.ProgressVisualSeconds)), 0.05f, 20f);
            return tuning;
        }

        private sealed class CargoTelemetryHistogramElement : VisualElement
        {
            private const float SamplePixels = 4f;
            private const float MaxMicroseconds = 500f;
            private const float MaxOverflow = 64f;

            public CargoTelemetryHistogramElement()
            {
                style.height = HistogramHeightPixels;
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
                DrawRect(painter, rect, new Color(0.015f, 0.018f, 0.022f, 0.96f));
                if (!TryResolveTelemetry(out NativeArray<CargoTelemetryEntry> telemetry, out int cursor))
                    return;

                int columns = math.min(telemetry.Length, math.max(1, (int)math.floor(rect.width / SamplePixels)));
                int start = cursor - columns;
                for (int i = 0; i < columns; i++)
                {
                    int index = start + i;
                    while (index < 0)
                        index += telemetry.Length;
                    index %= telemetry.Length;

                    CargoTelemetryEntry entry = telemetry[index];
                    if (entry.Frame == 0u)
                        continue;

                    float x = rect.xMin + (i * SamplePixels);
                    float width = math.max(1f, SamplePixels - 1f);
                    float micros01 = math.saturate(entry.BurstExecutionMicroseconds / MaxMicroseconds);
                    float overflow01 = math.saturate(entry.OverflowLootCaches / MaxOverflow);
                    DrawRect(
                        painter,
                        new Rect(x, rect.yMax - (micros01 * rect.height), width, micros01 * rect.height),
                        new Color(0.18f, 0.68f, 0.95f, 0.90f));
                    if (overflow01 > 0f)
                    {
                        DrawRect(
                            painter,
                            new Rect(x, rect.yMax - (overflow01 * rect.height), width, overflow01 * rect.height),
                            new Color(0.95f, 0.70f, 0.14f, 0.86f));
                    }
                }

                float budgetY = rect.yMax - math.saturate(100f / MaxMicroseconds) * rect.height;
                painter.lineWidth = 1f;
                painter.strokeColor = new Color(0.95f, 0.18f, 0.12f, 0.75f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, budgetY));
                painter.LineTo(new Vector2(rect.xMax, budgetY));
                painter.Stroke();
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
