#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Ecosystem;
using Hecton8.Core.Contracts;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed unsafe class FaunaGeneticsTunerWindow : EditorWindow
    {
        private const int HistogramBars = 16;
        private const int MaxSceneSamples = 128;
        private static bool s_drawSceneGizmo = true;

        private Label _status;
        private Slider _baseSizeScalar;
        private Slider _hueShiftRange;
        private Slider _mutationProbability;
        private Slider _qualityWeight;
        private Toggle _drawGizmoToggle;
        private FaunaGeneticsHistogramElement _histogram;
        private bool _updatingControls;

        [MenuItem("HECTON-8/Ecosystem/Fauna Genetics Tuner")]
        public static void Open()
        {
            GetWindow<FaunaGeneticsTunerWindow>("Fauna Genetics");
        }

        private void OnEnable()
        {
            EditorApplication.update += TickEditor;
            SceneView.duringSceneGui += DrawSceneGizmo;
        }

        private void OnDisable()
        {
            EditorApplication.update -= TickEditor;
            SceneView.duringSceneGui -= DrawSceneGizmo;
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _status = new Label("Fauna genetics Vault buffers not visible.");
            rootVisualElement.Add(_status);

            _baseSizeScalar = CreateSlider("Base Size Scalar", 0.1f, 4f);
            _hueShiftRange = CreateSlider("Hue Shift Range", 0f, 1f);
            _mutationProbability = CreateSlider("Mutation Probability", 0f, 1f);
            _qualityWeight = CreateSlider("Global Quality Weight", 0f, 1f);
            rootVisualElement.Add(_baseSizeScalar);
            rootVisualElement.Add(_hueShiftRange);
            rootVisualElement.Add(_mutationProbability);
            rootVisualElement.Add(_qualityWeight);

            _drawGizmoToggle = new Toggle("Draw Live Genetic Mask Gizmo") { value = s_drawSceneGizmo };
            _drawGizmoToggle.RegisterValueChangedCallback(evt =>
            {
                s_drawSceneGizmo = evt.newValue;
                SceneView.RepaintAll();
            });
            rootVisualElement.Add(_drawGizmoToggle);

            Button scanButton = new Button(() =>
            {
                OOP_Variant_Scanner.RunAndWriteReport();
                Repaint();
            })
            {
                text = "Run OOP Variant Scanner"
            };
            rootVisualElement.Add(scanButton);

            _histogram = new FaunaGeneticsHistogramElement();
            _histogram.style.height = 120;
            _histogram.style.marginTop = 8;
            rootVisualElement.Add(_histogram);

            _baseSizeScalar.RegisterValueChangedCallback(evt => MutateTuning(0, evt.newValue));
            _hueShiftRange.RegisterValueChangedCallback(evt => MutateTuning(1, evt.newValue));
            _mutationProbability.RegisterValueChangedCallback(evt => MutateTuning(2, evt.newValue));
            _qualityWeight.RegisterValueChangedCallback(evt => MutateTuning(3, evt.newValue));
            RefreshFromVault();
        }

        private static Slider CreateSlider(string label, float min, float max)
        {
            return new Slider(label, min, max)
            {
                showInputField = true
            };
        }

        private void TickEditor()
        {
            RefreshFromVault();
            if (_histogram != null)
                _histogram.MarkDirtyRepaint();
        }

        private void RefreshFromVault()
        {
            if (!TryReadVaultView(BufferID.EcosystemFaunaGeneticsTuning, out NativeArray<FaunaGeneticsTuningDTO> tuningBuffer) ||
                !TryReadVaultView(BufferID.EcosystemFaunaGeneticsTelemetry, out NativeArray<GeneticsTelemetryEntry> telemetry))
            {
                if (_status != null)
                    _status.text = "Fauna genetics Vault buffers are not registered.";
                return;
            }

            FaunaGeneticsTuningDTO tuning = FaunaGeneticsTuningDTO.Sanitize(tuningBuffer[0]);
            _updatingControls = true;
            if (_baseSizeScalar != null) _baseSizeScalar.SetValueWithoutNotify(tuning.BaseSizeScalar);
            if (_hueShiftRange != null) _hueShiftRange.SetValueWithoutNotify(tuning.HueShiftRange);
            if (_mutationProbability != null) _mutationProbability.SetValueWithoutNotify(tuning.MutationProbability);
            if (_qualityWeight != null) _qualityWeight.SetValueWithoutNotify(tuning.GlobalQualityWeight);
            _updatingControls = false;

            GeneticsTelemetryEntry latest = FindLatestTelemetry(telemetry);
            if (_status != null)
            {
                _status.text =
                    "Frame " + latest.FrameIndex +
                    " | Active " + latest.ActiveGenomeCount +
                    " | Profiles " + tuning.ProfileCount +
                    " | Avg Hue " + latest.AverageHueShift01.ToString("0.000") +
                    " | Burst us " + latest.BurstExecutionMicroseconds.ToString("0.0");
            }
        }

        private void MutateTuning(int field, float value)
        {
            if (_updatingControls)
                return;

            if (!TryOpenVaultView(BufferID.EcosystemFaunaGeneticsTuning, out NativeArray<FaunaGeneticsTuningDTO> tuningBuffer))
                return;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr<FaunaGeneticsTuningDTO>(tuningBuffer);
            ref FaunaGeneticsTuningDTO tuning = ref UnsafeUtility.AsRef<FaunaGeneticsTuningDTO>(ptr);
            switch (field)
            {
                case 0:
                    tuning.BaseSizeScalar = value;
                    break;
                case 1:
                    tuning.HueShiftRange = value;
                    break;
                case 2:
                    tuning.MutationProbability = value;
                    break;
                case 3:
                    tuning.GlobalQualityWeight = value;
                    break;
            }

            tuning = FaunaGeneticsTuningDTO.Sanitize(tuning);
            SceneView.RepaintAll();
        }

        private static void DrawSceneGizmo(SceneView sceneView)
        {
            if (!s_drawSceneGizmo)
                return;

            if (!TryReadVaultView(BufferID.EcosystemHeadlessPositions, out NativeArray<float3> positions) ||
                !TryReadVaultView(BufferID.EcosystemHeadlessFaunaGenomes, out NativeArray<ulong> genomes))
            {
                return;
            }

            int count = math.min(MaxSceneSamples, math.min(positions.Length, genomes.Length));
            for (int i = 0; i < count; i++)
            {
                ulong mask = genomes[i];
                if (mask == 0UL)
                    continue;

                float3 position = positions[i];
                if (!math.all(math.isfinite(position)))
                    continue;

                int size = FaunaGenome64.ExtractSizeByte(mask);
                int hue = FaunaGenome64.ExtractHueByte(mask);
                float scale = 0.35f + size * (1f / 255f) * 1.2f;
                Handles.color = HueByteToColor(hue, 0.82f);
                Handles.DrawWireCube(new Vector3(position.x, position.y + scale, position.z), Vector3.one * scale);
            }
        }

        private static Color HueByteToColor(int hueByte, float alpha)
        {
            float h = (hueByte & 255) * (1f / 255f);
            float r = math.saturate(math.abs(h * 6f - 3f) - 1f);
            float g = math.saturate(2f - math.abs(h * 6f - 2f));
            float b = math.saturate(2f - math.abs(h * 6f - 4f));
            return new Color(r, g, b, alpha);
        }

        private static GeneticsTelemetryEntry FindLatestTelemetry(NativeArray<GeneticsTelemetryEntry> telemetry)
        {
            GeneticsTelemetryEntry latest = default;
            if (!telemetry.IsCreated)
                return latest;

            for (int i = 0; i < telemetry.Length; i++)
            {
                GeneticsTelemetryEntry entry = telemetry[i];
                if (entry.FrameIndex >= latest.FrameIndex)
                    latest = entry;
            }

            return latest;
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

        private sealed class FaunaGeneticsHistogramElement : VisualElement
        {
            public FaunaGeneticsHistogramElement()
            {
                generateVisualContent += OnGenerateVisualContent;
            }

            private void OnGenerateVisualContent(MeshGenerationContext context)
            {
                if (!TryReadVaultView(BufferID.EcosystemFaunaGeneticsTelemetry, out NativeArray<GeneticsTelemetryEntry> telemetry))
                    return;

                GeneticsTelemetryEntry latest = FindLatestTelemetry(telemetry);
                Rect rect = contentRect;
                if (rect.width <= 2f || rect.height <= 2f)
                    return;

                Painter2D painter = context.painter2D;
                float barWidth = rect.width / HistogramBars;
                for (int i = 0; i < HistogramBars; i++)
                {
                    uint packed = i < 8 ? latest.PatternHistogramLo : latest.PatternHistogramHi;
                    int shift = (i & 7) << 2;
                    int count = (int)((packed >> shift) & 0x0Fu);
                    float h = math.saturate(count * (1f / 15f));
                    float x = rect.xMin + i * barWidth;
                    float y = rect.yMax - math.max(4f, rect.height * h);
                    painter.fillColor = HueByteToColor(i * 17, 0.75f);
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x + 1f, rect.yMax));
                    painter.LineTo(new Vector2(x + barWidth - 1f, rect.yMax));
                    painter.LineTo(new Vector2(x + barWidth - 1f, y));
                    painter.LineTo(new Vector2(x + 1f, y));
                    painter.ClosePath();
                    painter.Fill();
                }
            }
        }
    }
}
#endif
