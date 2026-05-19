#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.VFX;
using System;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorTools
{
    public sealed class AbyssalAtmosphereTunerWindow : EditorWindow
    {
        private Slider _density;
        private Slider _scatter;
        private Slider _extinction;
        private Slider _anisotropy;
        private Slider _flowStrength;
        private Slider _quality;
        private Label _status;
        private VisualElement _telemetryGraph;

        [MenuItem("Hecton8/VFX/Abyssal Atmosphere Tuner")]
        private static void Open()
        {
            GetWindow<AbyssalAtmosphereTunerWindow>("Abyssal Atmosphere");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _status = new Label("Vault not sampled.");
            root.Add(_status);

            _density = CreateSlider("Density", 0f, 0.3f);
            _scatter = CreateSlider("Scatter", 0f, 4f);
            _extinction = CreateSlider("Extinction", 0.001f, 2f);
            _anisotropy = CreateSlider("Anisotropy", -0.95f, 0.95f);
            _flowStrength = CreateSlider("Flow", 0f, 8f);
            _quality = CreateSlider("Quality", 0f, 1f);
            _telemetryGraph = new VisualElement();
            _telemetryGraph.style.height = 96;
            _telemetryGraph.style.marginTop = 6;
            _telemetryGraph.generateVisualContent += DrawTelemetryGraph;

            root.Add(_telemetryGraph);
            root.Add(_density);
            root.Add(_scatter);
            root.Add(_extinction);
            root.Add(_anisotropy);
            root.Add(_flowStrength);
            root.Add(_quality);

            Button refreshButton = new Button(RefreshFromVault) { text = "Refresh" };
            root.Add(refreshButton);
            Button loadCsvButton = new Button(LoadExtinctionCsv) { text = "Load Extinction CSV" };
            root.Add(loadCsvButton);

            RegisterSliderCallbacks();
            RefreshFromVault();
            root.schedule.Execute(RefreshFromVault).Every(500);
        }

        private static Slider CreateSlider(string label, float low, float high)
        {
            Slider slider = new Slider(label, low, high)
            {
                showInputField = true
            };
            return slider;
        }

        private void RegisterSliderCallbacks()
        {
            _density.RegisterValueChangedCallback(evt => ApplyDensity(evt.newValue));
            _scatter.RegisterValueChangedCallback(evt => ApplyScatter(evt.newValue));
            _extinction.RegisterValueChangedCallback(evt => ApplyExtinction(evt.newValue));
            _anisotropy.RegisterValueChangedCallback(evt => ApplyAnisotropy(evt.newValue));
            _flowStrength.RegisterValueChangedCallback(evt => ApplyFlow(evt.newValue));
            _quality.RegisterValueChangedCallback(evt => ApplyQuality(evt.newValue));
        }

        private void RefreshFromVault()
        {
            if (!TryResolveParams(out NativeArray<VolumetricFogParamsDTO> parameters))
            {
                _status.text = "GlobalDataVault unavailable.";
                return;
            }

            if (!VolumetricFogNativeLayout.Validate())
            {
                _status.text = "VolumetricFogParamsDTO layout invalid.";
                return;
            }

            ref VolumetricFogParamsDTO dto = ref VolumetricFogParamsAccess.ElementAt(parameters, 0);
            if (dto.QualityAndLimits.y <= 0f)
            {
                dto.FogColorAndDensity = new float4(0.015f, 0.045f, 0.065f, 0.045f);
                dto.ScatteringParams = new float4(0.85f, 0.12f, 0.42f, 0.97f);
                dto.FlowAdvection = new float4(0f, 0f, 0f, 2.25f);
                dto.QualityAndLimits = new float4(ClampFinite(HomeostasisBrain.GlobalQualityWeight, 0f, 1f, 0f), 4f, 70f, 1f);
            }

            SetSliderWithoutNotify(_density, dto.FogColorAndDensity.w);
            SetSliderWithoutNotify(_scatter, dto.ScatteringParams.x);
            SetSliderWithoutNotify(_extinction, dto.ScatteringParams.y);
            SetSliderWithoutNotify(_anisotropy, dto.ScatteringParams.z);
            SetSliderWithoutNotify(_flowStrength, dto.FlowAdvection.w);
            SetSliderWithoutNotify(_quality, dto.QualityAndLimits.x);
            _status.text = "Vault sampled. DTO layout 64B explicit.";
            _telemetryGraph?.MarkDirtyRepaint();
        }

        private static void SetSliderWithoutNotify(Slider slider, float value)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(math.isfinite(value) ? value : 0f);
        }

        private static float ClampFinite(float value, float min, float max, float fallback)
        {
            return math.isfinite(value) ? math.clamp(value, min, max) : fallback;
        }

        private void ApplyDensity(float value)
        {
            if (!TryResolveParams(out NativeArray<VolumetricFogParamsDTO> parameters))
                return;

            ref VolumetricFogParamsDTO dto = ref VolumetricFogParamsAccess.ElementAt(parameters, 0);
            dto.FogColorAndDensity.w = ClampFinite(value, 0f, 0.3f, 0.045f);
        }

        private void ApplyScatter(float value)
        {
            if (!TryResolveParams(out NativeArray<VolumetricFogParamsDTO> parameters))
                return;

            ref VolumetricFogParamsDTO dto = ref VolumetricFogParamsAccess.ElementAt(parameters, 0);
            dto.ScatteringParams.x = ClampFinite(value, 0f, 4f, 0.85f);
        }

        private void ApplyExtinction(float value)
        {
            if (!TryResolveParams(out NativeArray<VolumetricFogParamsDTO> parameters))
                return;

            ref VolumetricFogParamsDTO dto = ref VolumetricFogParamsAccess.ElementAt(parameters, 0);
            dto.ScatteringParams.y = ClampFinite(value, 0.001f, 2f, 0.12f);
        }

        private void ApplyAnisotropy(float value)
        {
            if (!TryResolveParams(out NativeArray<VolumetricFogParamsDTO> parameters))
                return;

            ref VolumetricFogParamsDTO dto = ref VolumetricFogParamsAccess.ElementAt(parameters, 0);
            dto.ScatteringParams.z = ClampFinite(value, -0.95f, 0.95f, 0.42f);
        }

        private void ApplyFlow(float value)
        {
            if (!TryResolveParams(out NativeArray<VolumetricFogParamsDTO> parameters))
                return;

            ref VolumetricFogParamsDTO dto = ref VolumetricFogParamsAccess.ElementAt(parameters, 0);
            dto.FlowAdvection.w = ClampFinite(value, 0f, 8f, 2.25f);
        }

        private void ApplyQuality(float value)
        {
            float quality = ClampFinite(value, 0f, 1f, 0f);
            HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(quality, true);
            if (!TryResolveParams(out NativeArray<VolumetricFogParamsDTO> parameters))
                return;

            ref VolumetricFogParamsDTO dto = ref VolumetricFogParamsAccess.ElementAt(parameters, 0);
            dto.QualityAndLimits.x = quality;
            dto.QualityAndLimits.y = math.round(math.lerp(VolumetricFogConstants.MinRaySteps, VolumetricFogConstants.MaxRaySteps, quality));
        }

        private static bool TryResolveParams(out NativeArray<VolumetricFogParamsDTO> parameters)
        {
            parameters = default;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            parameters = vault.GetBuffer<VolumetricFogParamsDTO>(
                BufferID.ShinobuVolumetricFogParams,
                1,
                SystemID.Vfx,
                NativeArrayOptions.ClearMemory);
            return parameters.IsCreated && parameters.Length > 0;
        }

        private void LoadExtinctionCsv()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                _status.text = "GlobalDataVault unavailable.";
                return;
            }

            string path = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath, "Docs", "water_extinction_profiles.csv");
            if (!File.Exists(path))
            {
                _status.text = "water_extinction_profiles.csv missing.";
                return;
            }

            NativeArray<WaterExtinctionProfileDTO> profiles = vault.GetBuffer<WaterExtinctionProfileDTO>(
                BufferID.ShinobuVolumetricFogExtinctionProfiles,
                VolumetricFogConstants.ExtinctionProfileCapacity,
                SystemID.Vfx,
                NativeArrayOptions.ClearMemory);
            if (!profiles.IsCreated || profiles.Length <= 0)
            {
                _status.text = "Extinction profile buffer unavailable.";
                return;
            }

            NativeArray<byte> scratch = vault.GetBuffer<byte>(
                    BufferID.ShinobuVolumetricFogCsvScratch,
                    VolumetricFogConstants.ExtinctionCsvScratchBytes,
                    SystemID.Vfx,
                    NativeArrayOptions.UninitializedMemory);
            if (!scratch.IsCreated || scratch.Length <= 0)
            {
                _status.text = "CSV scratch buffer unavailable.";
                return;
            }

            int byteCount = ReadFileIntoScratch(path, scratch);
            if (byteCount <= 0)
            {
                _status.text = byteCount < 0 ? "Extinction CSV exceeds scratch capacity." : "Extinction CSV empty.";
                return;
            }

            unsafe
            {
                void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch);
                ReadOnlySpan<byte> csvBytes = new ReadOnlySpan<byte>(source, byteCount);
                if (!VolumetricFogExtinctionCsvParser.TryParseInto(csvBytes, profiles, out int profileCount, out uint fileHash))
                {
                    _status.text = "Extinction CSV parse produced no profiles.";
                    return;
                }

                _status.text = "Extinction CSV loaded. Hash 0x" + fileHash.ToString("X8") + " Profiles " + profileCount;
            }
        }

        private static unsafe int ReadFileIntoScratch(string path, NativeArray<byte> scratch)
        {
            using (FileStream stream = File.OpenRead(path))
            {
                if (stream.Length > scratch.Length)
                    return -1;

                int length = (int)stream.Length;
                void* destination = NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                Span<byte> target = new Span<byte>(destination, length);
                int totalRead = 0;
                while (totalRead < length)
                {
                    int read = stream.Read(target.Slice(totalRead));
                    if (read <= 0)
                        break;

                    totalRead += read;
                }

                return totalRead;
            }
        }

        private void DrawTelemetryGraph(MeshGenerationContext context)
        {
            Rect rect = _telemetryGraph.contentRect;
            if (rect.width <= 1f || rect.height <= 1f)
                return;

            Painter2D painter = context.painter2D;
            DrawRect(painter, rect, new Color(0.015f, 0.025f, 0.03f, 1f));
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetBuffer<VolumetricFogTelemetryEntry>(BufferID.ShinobuVolumetricFogTelemetryRing, out NativeArray<VolumetricFogTelemetryEntry> telemetry) ||
                !telemetry.IsCreated ||
                telemetry.Length <= 1)
            {
                return;
            }

            float maxMicroseconds = 2000f;
            for (int i = 0; i < telemetry.Length; i++)
                maxMicroseconds = math.max(maxMicroseconds, telemetry[i].EstimatedGpuMicroseconds);

            painter.lineWidth = 1.5f;
            painter.strokeColor = new Color(0.15f, 0.86f, 0.72f, 1f);
            painter.BeginPath();
            for (int i = 0; i < telemetry.Length; i++)
            {
                float x = rect.xMin + rect.width * (i / (float)(telemetry.Length - 1));
                float y = rect.yMax - math.saturate(telemetry[i].EstimatedGpuMicroseconds / maxMicroseconds) * rect.height;
                if (i == 0)
                    painter.MoveTo(new Vector2(x, y));
                else
                    painter.LineTo(new Vector2(x, y));
            }
            painter.Stroke();

            float thresholdY = rect.yMax - math.saturate(2000f / maxMicroseconds) * rect.height;
            painter.lineWidth = 1f;
            painter.strokeColor = new Color(0.9f, 0.18f, 0.08f, 1f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, thresholdY));
            painter.LineTo(new Vector2(rect.xMax, thresholdY));
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
#endif
