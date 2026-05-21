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
        private Slider _siltTurbulence;
        private Slider _quality;
        private Label _status;
        private VisualElement _telemetryGraph;

        [MenuItem("Hecton8/VFX/Volumetric Atmosphere Tuner")]
        private static void Open()
        {
            GetWindow<AbyssalAtmosphereTunerWindow>("Volumetric Atmosphere");
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

            _density = CreateSlider("Base Density", 0f, 0.3f);
            _scatter = CreateSlider("Scattering Coefficient", 0f, 4f);
            _extinction = CreateSlider("Extinction", 0.001f, 2f);
            _anisotropy = CreateSlider("Scattering Anisotropy", -0.95f, 0.95f);
            _siltTurbulence = CreateSlider("Silt Turbulence Multiplier", 0f, 8f);
            _quality = CreateSlider("Visual Overkill Step Limits", 0f, 1f);
            _telemetryGraph = new VisualElement();
            _telemetryGraph.style.height = 96;
            _telemetryGraph.style.marginTop = 6;
            _telemetryGraph.generateVisualContent += DrawTelemetryGraph;

            root.Add(_telemetryGraph);
            root.Add(_density);
            root.Add(_scatter);
            root.Add(_extinction);
            root.Add(_anisotropy);
            root.Add(_siltTurbulence);
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
            _siltTurbulence.RegisterValueChangedCallback(evt => ApplyFlow(evt.newValue));
            _quality.RegisterValueChangedCallback(evt => ApplyQuality(evt.newValue));
        }

        private void RefreshFromVault()
        {
            if (!VolumetricFogNativeLayout.Validate())
            {
                _status.text = "FogConstantsDTO layout invalid.";
                return;
            }

            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryAcquireParamsWriteView(vault, out VaultGenerationHandle<FogConstantsDTO> handle, out NativeArray<FogConstantsDTO> parameters))
            {
                _status.text = "GlobalDataVault unavailable.";
                return;
            }

            try
            {
                ref FogConstantsDTO dto = ref VolumetricFogParamsAccess.ElementAt(parameters, 0);
                EnsureUsableParams(ref dto);

                SetSliderWithoutNotify(_density, dto.FogColorAndDensity.w);
                SetSliderWithoutNotify(_scatter, dto.ScatteringParams.x);
                SetSliderWithoutNotify(_extinction, dto.ScatteringParams.y);
                SetSliderWithoutNotify(_anisotropy, dto.ScatteringParams.z);
                SetSliderWithoutNotify(_siltTurbulence, dto.FlowAdvection.w);
                SetSliderWithoutNotify(_quality, dto.QualityAndLimits.x);
                _status.text = "Vault sampled. DTO layout 64B explicit.";
                _telemetryGraph?.MarkDirtyRepaint();
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            }
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

        private static void EnsureUsableParams(ref FogConstantsDTO dto)
        {
            if (VolumetricFogParamsAccess.IsUsableParams(in dto))
                return;

            float quality = ClampFinite(HomeostasisBrain.GlobalQualityWeight, 0f, 1f, 0f);
            dto = VolumetricFogParamsAccess.CreateDefaultParams(quality);
        }

        private void ApplyDensity(float value)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryAcquireParamsWriteView(vault, out VaultGenerationHandle<FogConstantsDTO> handle, out NativeArray<FogConstantsDTO> parameters))
                return;

            try
            {
                ref FogConstantsDTO dto = ref VolumetricFogParamsAccess.ElementAt(parameters, 0);
                EnsureUsableParams(ref dto);
                dto.FogColorAndDensity.w = ClampFinite(value, 0f, 0.3f, 0.045f);
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            }
        }

        private void ApplyScatter(float value)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryAcquireParamsWriteView(vault, out VaultGenerationHandle<FogConstantsDTO> handle, out NativeArray<FogConstantsDTO> parameters))
                return;

            try
            {
                ref FogConstantsDTO dto = ref VolumetricFogParamsAccess.ElementAt(parameters, 0);
                EnsureUsableParams(ref dto);
                dto.ScatteringParams.x = ClampFinite(value, 0f, 4f, 0.85f);
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            }
        }

        private void ApplyExtinction(float value)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryAcquireParamsWriteView(vault, out VaultGenerationHandle<FogConstantsDTO> handle, out NativeArray<FogConstantsDTO> parameters))
                return;

            try
            {
                ref FogConstantsDTO dto = ref VolumetricFogParamsAccess.ElementAt(parameters, 0);
                EnsureUsableParams(ref dto);
                dto.ScatteringParams.y = ClampFinite(value, 0.001f, 2f, 0.12f);
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            }
        }

        private void ApplyAnisotropy(float value)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryAcquireParamsWriteView(vault, out VaultGenerationHandle<FogConstantsDTO> handle, out NativeArray<FogConstantsDTO> parameters))
                return;

            try
            {
                ref FogConstantsDTO dto = ref VolumetricFogParamsAccess.ElementAt(parameters, 0);
                EnsureUsableParams(ref dto);
                dto.ScatteringParams.z = ClampFinite(value, -0.95f, 0.95f, 0.42f);
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            }
        }

        private void ApplyFlow(float value)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryAcquireParamsWriteView(vault, out VaultGenerationHandle<FogConstantsDTO> handle, out NativeArray<FogConstantsDTO> parameters))
                return;

            try
            {
                ref FogConstantsDTO dto = ref VolumetricFogParamsAccess.ElementAt(parameters, 0);
                EnsureUsableParams(ref dto);
                dto.FlowAdvection.w = ClampFinite(value, 0f, 8f, 2.25f);
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            }
        }

        private void ApplyQuality(float value)
        {
            float quality = ClampFinite(value, 0f, 1f, 0f);
            HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(quality, true);
            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryAcquireParamsWriteView(vault, out VaultGenerationHandle<FogConstantsDTO> handle, out NativeArray<FogConstantsDTO> parameters))
                return;

            try
            {
                ref FogConstantsDTO dto = ref VolumetricFogParamsAccess.ElementAt(parameters, 0);
                EnsureUsableParams(ref dto);
                dto.QualityAndLimits.x = quality;
                dto.QualityAndLimits.y = VolumetricFogParamsAccess.ResolveRayStepsForQuality(quality);
                dto.QualityAndLimits.w = VolumetricFogParamsAccess.ResolveProxyBlendForQuality(quality);
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            }
        }

        private static bool TryAcquireParamsWriteView(
            IDataVault vault,
            out VaultGenerationHandle<FogConstantsDTO> handle,
            out NativeArray<FogConstantsDTO> parameters)
        {
            return TryAcquireEditorWriteView(
                vault,
                BufferID.ShinobuVolumetricFogParams,
                1,
                SystemID.Vfx,
                NativeArrayOptions.ClearMemory,
                out handle,
                out parameters);
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

            if (!TryAcquireEditorWriteView(
                    vault,
                    BufferID.ShinobuVolumetricFogCsvScratch,
                    VolumetricFogConstants.ExtinctionCsvScratchBytes,
                    SystemID.Vfx,
                    NativeArrayOptions.UninitializedMemory,
                    out VaultGenerationHandle<byte> scratchHandle,
                    out NativeArray<byte> scratch))
            {
                _status.text = "CSV scratch buffer unavailable.";
                return;
            }

            try
            {
                int byteCount = ReadFileIntoScratch(path, scratch);
                if (byteCount <= 0)
                {
                    _status.text = byteCount < 0 ? "Extinction CSV exceeds scratch capacity." : "Extinction CSV empty.";
                    return;
                }

                if (!TryAcquireEditorWriteView(
                        vault,
                        BufferID.ShinobuVolumetricFogExtinctionProfiles,
                        VolumetricFogConstants.ExtinctionProfileCapacity,
                        SystemID.Vfx,
                        NativeArrayOptions.ClearMemory,
                        out VaultGenerationHandle<WaterExtinctionProfileDTO> profileHandle,
                        out NativeArray<WaterExtinctionProfileDTO> profiles))
                {
                    _status.text = "Extinction profile buffer unavailable.";
                    return;
                }

                try
                {
                    unsafe
                    {
                        void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch);
                        ReadOnlySpan<byte> csvBytes = new ReadOnlySpan<byte>(source, byteCount);
                        if (!VolumetricFogExtinctionCsvParser.TryParseInto(csvBytes, profiles, out _, out _))
                        {
                            _status.text = "Extinction CSV parse produced no profiles.";
                            return;
                        }

                        _status.text = "Extinction CSV loaded into Vault profile buffer.";
                    }
                }
                finally
                {
                    vault.ReleaseWriteLock(in profileHandle, SystemID.CoreDiagnostics);
                }
            }
            finally
            {
                vault.ReleaseWriteLock(in scratchHandle, SystemID.CoreDiagnostics);
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
                !TryReadExistingVaultView(vault, BufferID.ShinobuVolumetricFogTelemetryRing, out NativeArray<VolumetricFogTelemetryEntry> telemetry) ||
                telemetry.Length <= 1)
            {
                return;
            }

            float maxMicroseconds = 2000f;
            for (int i = 0; i < telemetry.Length; i++)
            {
                float sample = ClampFinite(telemetry[i].EstimatedGpuMicroseconds, 0f, 16000f, 0f);
                maxMicroseconds = math.max(maxMicroseconds, sample);
            }

            painter.lineWidth = 1.5f;
            painter.strokeColor = new Color(0.15f, 0.86f, 0.72f, 1f);
            painter.BeginPath();
            bool hasPoint = false;
            for (int i = 0; i < telemetry.Length; i++)
            {
                float sample = ClampFinite(telemetry[i].EstimatedGpuMicroseconds, 0f, 16000f, 0f);
                float x = rect.xMin + rect.width * (i / (float)(telemetry.Length - 1));
                float y = rect.yMax - math.saturate(sample / maxMicroseconds) * rect.height;
                if (!hasPoint)
                {
                    painter.MoveTo(new Vector2(x, y));
                    hasPoint = true;
                }
                else
                {
                    painter.LineTo(new Vector2(x, y));
                }
            }
            if (hasPoint)
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

        private static bool TryReadExistingVaultView<T>(IDataVault vault, BufferID bufferId, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private static bool TryAcquireEditorWriteView<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            SystemID owner,
            NativeArrayOptions options,
            out VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer)
            where T : struct
        {
            handle = default;
            buffer = default;
            int required = math.max(1, requiredLength);
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> existing) &&
                vault.TryReadHandle(in existing, out NativeArray<T> existingBuffer) &&
                existingBuffer.IsCreated &&
                existingBuffer.Length >= required)
            {
                handle = existing;
            }
            else
            {
                if (vault.IsAllocationLocked)
                    return false;

                handle = vault.GetGenerationHandle<T>(
                    bufferId,
                    required,
                    owner,
                    options);
            }

            if (!vault.TryAcquireWriteLock(in handle, SystemID.CoreDiagnostics, out buffer))
                return false;

            if (buffer.IsCreated && buffer.Length >= required)
                return true;

            vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            buffer = default;
            return false;
        }
    }
}
#endif
