#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Ecosystem;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    /// <summary>
    /// UI Toolkit facade for SHINOBU_116 macro ecosystem tuning.
    /// </summary>
    public sealed class MacroEcosystemTunerWindow : EditorWindow
    {
        private MacroEcosystemGraphElement _graph;
        private Slider _birthRate;
        private Slider _predationRate;
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
            _starvationRate = CreateSlider("Starvation Rate", 0f, 0.12f);
            root.Add(_birthRate);
            root.Add(_predationRate);
            root.Add(_starvationRate);

            _graph = new MacroEcosystemGraphElement();
            _graph.style.height = 180;
            _graph.style.marginTop = 8;
            root.Add(_graph);

            _birthRate.RegisterValueChangedCallback(OnBirthRateChanged);
            _predationRate.RegisterValueChangedCallback(OnPredationRateChanged);
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
            _starvationRate.SetValueWithoutNotify(tuning.PredatorStarvationRate);
            _status.text = "Vault-backed macro ecosystem tuning active.";
        }

        private void OnBirthRateChanged(ChangeEvent<float> evt)
        {
            MutateTuning(evt.newValue, _predationRate.value, _starvationRate.value);
        }

        private void OnPredationRateChanged(ChangeEvent<float> evt)
        {
            MutateTuning(_birthRate.value, evt.newValue, _starvationRate.value);
        }

        private void OnStarvationRateChanged(ChangeEvent<float> evt)
        {
            MutateTuning(_birthRate.value, _predationRate.value, evt.newValue);
        }

        private void MutateTuning(float birthRate, float predationRate, float starvationRate)
        {
            if (!TryOpenTuningView(out NativeArray<MacroEcosystemTuningDTO> buffer))
                return;

            MacroEcosystemTuningDTO tuning = buffer[0];
            tuning.BaseBirthRate = math.max(0f, birthRate);
            tuning.PredationRate = math.max(0f, predationRate);
            tuning.PredatorStarvationRate = math.max(0f, starvationRate);
            buffer[0] = MacroEcosystemTuningDTO.Sanitize(tuning);
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

            ulong maxValue = 1UL;
            for (int i = 0; i < telemetry.Length; i++)
            {
                MacroEcosystemTelemetryEntry entry = telemetry[i];
                if (entry.TotalPreyBiomass > maxValue)
                    maxValue = entry.TotalPreyBiomass;
                if (entry.TotalPredatorBiomass > maxValue)
                    maxValue = entry.TotalPredatorBiomass;
            }

            Painter2D painter = context.painter2D;
            DrawLine(painter, telemetry, rect, maxValue, true, new Color(0.1f, 0.35f, 1f, 1f));
            DrawLine(painter, telemetry, rect, maxValue, false, new Color(1f, 0.15f, 0.1f, 1f));
        }

        private static void DrawLine(
            Painter2D painter,
            NativeArray<MacroEcosystemTelemetryEntry> telemetry,
            Rect rect,
            ulong maxValue,
            bool prey,
            Color color)
        {
            painter.strokeColor = color;
            painter.lineWidth = 2f;
            painter.BeginPath();
            float invCount = 1f / math.max(1, telemetry.Length - 1);
            float invMax = 1f / math.max(1f, (float)maxValue);
            for (int i = 0; i < telemetry.Length; i++)
            {
                MacroEcosystemTelemetryEntry entry = telemetry[i];
                float x = rect.xMin + rect.width * (i * invCount);
                ulong raw = prey ? entry.TotalPreyBiomass : entry.TotalPredatorBiomass;
                float y = rect.yMax - rect.height * math.saturate(raw * invMax);
                if (i == 0)
                    painter.MoveTo(new Vector2(x, y));
                else
                    painter.LineTo(new Vector2(x, y));
            }
            painter.Stroke();
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
