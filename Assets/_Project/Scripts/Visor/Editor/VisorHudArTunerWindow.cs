#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Visor.Editor
{
    public sealed class VisorHudArTunerWindow : EditorWindow
    {
        private Label _status;
        private Slider _curvature;
        private Slider _fontScale;
        private Slider _quality;

        [MenuItem("Hecton8/Visor/Visor HUD & AR Tuner")]
        private static void Open()
        {
            GetWindow<VisorHudArTunerWindow>("Visor HUD & AR Tuner");
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _status = new Label("Telemetry unavailable");
            _curvature = new Slider("Visor Distortion Curvature", 0f, 1f) { value = 0.48f };
            _fontScale = new Slider("Font Atlas Scale", 0.5f, 2f) { value = 1f };
            _quality = new Slider("GlobalQualityWeight Override", 0f, 1f) { value = math.saturate(HomeostasisBrain.GlobalQualityWeight) };

            _curvature.RegisterValueChangedCallback(evt => WriteProfile(evt.newValue, _fontScale.value));
            _fontScale.RegisterValueChangedCallback(evt => WriteProfile(_curvature.value, evt.newValue));
            _quality.RegisterValueChangedCallback(evt => HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(evt.newValue, true));

            Button releaseQuality = new Button(() => HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(0f, false))
            {
                text = "Release Quality Override"
            };

            rootVisualElement.Add(_status);
            rootVisualElement.Add(_curvature);
            rootVisualElement.Add(_fontScale);
            rootVisualElement.Add(_quality);
            rootVisualElement.Add(releaseQuality);
            EditorApplication.update -= RefreshTelemetry;
            EditorApplication.update += RefreshTelemetry;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RefreshTelemetry;
        }

        private void RefreshTelemetry()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle(VisorARStencilContracts.TelemetryRingBufferId, out VaultGenerationHandle<VisorTelemetryEntry> handle) ||
                !vault.TryResolveHandle(in handle, out NativeArray<VisorTelemetryEntry> telemetry) ||
                !telemetry.IsCreated ||
                telemetry.Length <= 0)
            {
                if (_status != null)
                    _status.text = "Telemetry unavailable";
                return;
            }

            int frame = Time.frameCount;
            int index = telemetry.Length > 0 ? frame % telemetry.Length : 0;
            if (index < 0)
                index = 0;
            VisorTelemetryEntry entry = telemetry[index];
            if (_status != null)
                _status.text = $"Targets {entry.TargetCount:0} | CPU {entry.ProjectionMicroseconds:0.00} us | GPU est {entry.EstimatedGpuMicroseconds:0.00} us | Q {entry.QualityWeight:0.00}";
        }

        private static void WriteProfile(float curvature, float fontScale)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return;

            VaultGenerationHandle<VisorHudProfileDTO> handle = vault.GetGenerationHandle<VisorHudProfileDTO>(
                VisorARStencilContracts.ProfileBufferId,
                VisorARStencilContracts.ProfileCapacity,
                SystemID.UI,
                NativeArrayOptions.ClearMemory);
            if (!vault.TryResolveHandle(in handle, out NativeArray<VisorHudProfileDTO> profiles) ||
                !profiles.IsCreated ||
                profiles.Length <= 0)
            {
                return;
            }

            VisorHudProfileDTO profile = profiles[0];
            profile.NameHash = 0x54554E45u;
            profile.Curvature = math.saturate(curvature);
            profile.FontAtlasScale = math.clamp(fontScale, 0.5f, 2f);
            profile.FogEdgeStrength = profile.FogEdgeStrength <= 0f ? 0.42f : profile.FogEdgeStrength;
            profiles[0] = profile;
        }
    }
}
#endif
