#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Ecosystem;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    /// <summary>
    /// UI Toolkit facade for SHINOBU_300 macro ecosystem tuning.
    /// </summary>
    public sealed class MacroEcosystemTunerWindow : EditorWindow
    {
        private MacroEcosystemGraphElement _graph;
        private Slider _birthRate;
        private Slider _predationRate;
        private Slider _predatorConversionRate;
        private Slider _starvationRate;
        private Label _status;

        [MenuItem("HECTON-8/Ecosystem/Macro Ecosystem Tuner")]
        private static void Open()
        {
            GetWindow<MacroEcosystemTunerWindow>("Macro Ecosystem");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _status = new Label("DataVault not initialized.");
            root.Add(_status);

            _birthRate = CreateSlider("Base Birth Rate", 0f, 0.15f);
            _predationRate = CreateSlider("Predation Rate", 0f, 0.00001f);
            _predatorConversionRate = CreateSlider("Predator Conversion Rate", 0f, 0.00001f);
            _starvationRate = CreateSlider("Starvation Rate", 0f, 0.12f);
            root.Add(_birthRate);
            root.Add(_predationRate);
            root.Add(_predatorConversionRate);
            root.Add(_starvationRate);

            _graph = new MacroEcosystemGraphElement();
            _graph.style.height = 180;
            _graph.style.marginTop = 8;
            root.Add(_graph);

            _birthRate.RegisterValueChangedCallback(OnBirthRateChanged);
            _predationRate.RegisterValueChangedCallback(OnPredationRateChanged);
            _predatorConversionRate.RegisterValueChangedCallback(OnPredatorConversionRateChanged);
            _starvationRate.RegisterValueChangedCallback(OnStarvationRateChanged);

            EditorApplication.update += OnEditorUpdate;
            RefreshSlidersFromVault();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private static Slider CreateSlider(string label, float min, float max)
        {
            Slider slider = new Slider(label, min, max)
            {
                showInputField = true
            };
            slider.style.marginTop = 4;
            return slider;
        }

        private void OnEditorUpdate()
        {
            if (_graph != null)
                _graph.MarkDirtyRepaint();
        }

        private void RefreshSlidersFromVault()
        {
            if (!TryReadTuning(out MacroEcosystemTuningDTO tuning))
                return;

            _birthRate.SetValueWithoutNotify(tuning.BaseBirthRate);
            _predationRate.SetValueWithoutNotify(tuning.PredationRate);
            _predatorConversionRate.SetValueWithoutNotify(tuning.PredatorConversionRate);
            _starvationRate.SetValueWithoutNotify(tuning.PredatorStarvationRate);
            _status.text = "Vault-backed macro ecosystem tuning active.";
        }

        private void OnBirthRateChanged(ChangeEvent<float> evt)
        {
            MutateTuning(evt.newValue, _predationRate.value, _predatorConversionRate.value, _starvationRate.value);
        }

        private void OnPredationRateChanged(ChangeEvent<float> evt)
        {
            MutateTuning(_birthRate.value, evt.newValue, _predatorConversionRate.value, _starvationRate.value);
        }

        private void OnPredatorConversionRateChanged(ChangeEvent<float> evt)
        {
            MutateTuning(_birthRate.value, _predationRate.value, evt.newValue, _starvationRate.value);
        }

        private void OnStarvationRateChanged(ChangeEvent<float> evt)
        {
            MutateTuning(_birthRate.value, _predationRate.value, _predatorConversionRate.value, evt.newValue);
        }

        private unsafe void MutateTuning(float birthRate, float predationRate, float conversionRate, float starvationRate)
        {
            if (!TryOpenTuningView(out NativeArray<MacroEcosystemTuningDTO> buffer))
                return;

            ref MacroEcosystemTuningDTO tuning = ref UnsafeUtility.AsRef<MacroEcosystemTuningDTO>(
                NativeArrayUnsafeUtility.GetUnsafePtr(buffer));
            tuning.BaseBirthRate = math.max(0f, birthRate);
            tuning.PredationRate = math.max(0f, predationRate);
            tuning.PredatorConversionRate = math.max(0f, conversionRate);
            tuning.PredatorStarvationRate = math.max(0f, starvationRate);
            tuning = MacroEcosystemTuningDTO.Sanitize(tuning);
            _status.text = "Tuning written to DataVault.";
        }

        private static bool TryReadTuning(out MacroEcosystemTuningDTO tuning)
        {
            tuning = default;
            if (!TryReadVaultView(
                    BufferID.ShinobuMacroEcosystemTuning,
                    out NativeArray<MacroEcosystemTuningDTO> buffer))
            {
                return false;
            }

            tuning = buffer[0];
            return true;
        }

        private static bool TryOpenTuningView(out NativeArray<MacroEcosystemTuningDTO> buffer)
        {
            return TryOpenVaultView(BufferID.ShinobuMacroEcosystemTuning, out buffer);
        }

        private static bool TryOpenVaultView<T>(BufferID bufferId, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = GlobalRegistry.DataVault;
            return vault != null &&
                   vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length > 0;
        }

        private static bool TryReadVaultView<T>(BufferID bufferId, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = GlobalRegistry.DataVault;
            return vault != null &&
                   vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length > 0;
        }
    }

    internal sealed class MacroEcosystemGraphElement : VisualElement
    {
        public MacroEcosystemGraphElement()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            if (!TryReadTelemetry(out NativeArray<MacroEcosystemTelemetryEntry> telemetry) ||
                telemetry.Length <= 1)
            {
                return;
            }

            Rect rect = contentRect;
            if (rect.width <= 2f || rect.height <= 2f)
                return;

            float maxValue = 1f;
            for (int i = 0; i < telemetry.Length; i++)
            {
                MacroEcosystemTelemetryEntry entry = telemetry[i];
                float stacked =
                    math.max(0f, entry.TotalFloraBiomass) +
                    math.max(0f, entry.TotalPreyBiomass) +
                    math.max(0f, entry.TotalPredatorBiomass);
                if (stacked > maxValue)
                    maxValue = stacked;
            }

            Painter2D painter = context.painter2D;
            DrawStackedArea(painter, telemetry, rect, maxValue, new Color(0.08f, 0.55f, 0.18f, 0.74f), 0);
            DrawStackedArea(painter, telemetry, rect, maxValue, new Color(0.08f, 0.25f, 0.82f, 0.70f), 1);
            DrawStackedArea(painter, telemetry, rect, maxValue, new Color(0.92f, 0.10f, 0.06f, 0.68f), 2);
        }

        private static void DrawStackedArea(
            Painter2D painter,
            NativeArray<MacroEcosystemTelemetryEntry> telemetry,
            Rect rect,
            float maxValue,
            Color color,
            int lane)
        {
            painter.fillColor = color;
            painter.BeginPath();
            float invCount = 1f / math.max(1, telemetry.Length - 1);
            float invMax = 1f / math.max(1f, maxValue);
            for (int i = 0; i < telemetry.Length; i++)
            {
                Vector2 point = ResolveStackPoint(telemetry[i], rect, i, invCount, invMax, lane, true);
                if (i == 0)
                    painter.MoveTo(point);
                else
                    painter.LineTo(point);
            }

            for (int i = telemetry.Length - 1; i >= 0; i--)
                painter.LineTo(ResolveStackPoint(telemetry[i], rect, i, invCount, invMax, lane, false));

            painter.ClosePath();
            painter.Fill();
        }

        private static Vector2 ResolveStackPoint(
            MacroEcosystemTelemetryEntry entry,
            Rect rect,
            int index,
            float invCount,
            float invMax,
            int lane,
            bool upper)
        {
            float x = rect.xMin + rect.width * (index * invCount);
            float raw = ResolveStackValue(entry, lane, upper);
            float y = rect.yMax - rect.height * math.saturate(raw * invMax);
            return new Vector2(x, y);
        }

        private static float ResolveStackValue(MacroEcosystemTelemetryEntry entry, int lane, bool upper)
        {
            float flora = math.max(0f, entry.TotalFloraBiomass);
            float prey = math.max(0f, entry.TotalPreyBiomass);
            float predator = math.max(0f, entry.TotalPredatorBiomass);

            if (lane == 0)
                return upper ? flora : 0f;
            if (lane == 1)
                return upper ? flora + prey : flora;
            return upper ? flora + prey + predator : flora + prey;
        }

        private static bool TryReadTelemetry(out NativeArray<MacroEcosystemTelemetryEntry> telemetry)
        {
            telemetry = default;
            IDataVault vault = GlobalRegistry.DataVault;
            return vault != null &&
                   vault.TryGetGenerationHandle(
                       BufferID.ShinobuMacroEcosystemTelemetryRing,
                       out VaultGenerationHandle<MacroEcosystemTelemetryEntry> handle) &&
                   vault.TryReadHandle(in handle, out telemetry) &&
                   telemetry.IsCreated;
        }
    }
}
#endif
