#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.VFX.Parasites;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.VFX.Parasites.Editor
{
    public sealed class AbyssalParasiteTunerWindow : EditorWindow
    {
        private Slider _thermalAttraction;
        private Slider _curlFrequency;
        private Slider _maxSpeed;
        private Slider _qualityOverride;
        private Toggle _useQualityOverride;
        private Toggle _mockTargets;
        private Toggle _drawDebugTargets;
        private TelemetryGraphElement _graph;

        [MenuItem("Hecton8/VFX/Abyssal Parasite Tuner")]
        public static void Open()
        {
            GetWindow<AbyssalParasiteTunerWindow>("Abyssal Parasites");
        }

        private void OnEnable()
        {
            EditorApplication.update -= Repaint;
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;

            _thermalAttraction = AddSlider(root, "Thermal Attraction", 0f, 64f);
            _curlFrequency = AddSlider(root, "Curl Frequency", 0.001f, 4f);
            _maxSpeed = AddSlider(root, "Swarm Max Speed", 0.1f, 48f);
            _qualityOverride = AddSlider(root, "GlobalQuality Override", 0f, 1f);
            _useQualityOverride = AddToggle(root, "Use Quality Override");
            _mockTargets = AddToggle(root, "Mock Thermal Targets");
            _drawDebugTargets = AddToggle(root, "Draw Attraction Gizmo");
            Button reloadProfiles = new Button(ReloadProfilesFromCsv) { text = "Reload CSV Profiles" };
            root.Add(reloadProfiles);
            _graph = new TelemetryGraphElement();
            root.Add(_graph);

            _thermalAttraction.RegisterValueChangedCallback(evt => MutateTuning((ref ParasiteSwarmTuningDTO t) => t.ThermalAttractionMultiplier = math.max(0f, evt.newValue)));
            _curlFrequency.RegisterValueChangedCallback(evt => MutateTuning((ref ParasiteSwarmTuningDTO t) => t.CurlNoiseFrequency = math.max(0.001f, evt.newValue)));
            _maxSpeed.RegisterValueChangedCallback(evt => MutateTuning((ref ParasiteSwarmTuningDTO t) => t.SwarmMaxSpeed = math.max(0.1f, evt.newValue)));
            _qualityOverride.RegisterValueChangedCallback(evt => MutateTuning((ref ParasiteSwarmTuningDTO t) => t.GlobalQualityOverride = math.saturate(evt.newValue)));
            _useQualityOverride.RegisterValueChangedCallback(evt => MutateTuning((ref ParasiteSwarmTuningDTO t) =>
            {
                t.Flags = evt.newValue ? t.Flags | ParasiteSwarmContracts.TuningFlagUseQualityOverride : t.Flags & ~ParasiteSwarmContracts.TuningFlagUseQualityOverride;
            }));
            _mockTargets.RegisterValueChangedCallback(evt => MutateTuning((ref ParasiteSwarmTuningDTO t) =>
            {
                t.Flags = evt.newValue ? t.Flags | ParasiteSwarmContracts.TuningFlagMockTargets : t.Flags & ~ParasiteSwarmContracts.TuningFlagMockTargets;
            }));
            _drawDebugTargets.RegisterValueChangedCallback(evt => ParasiteAttractionDebugGizmo.DrawTargets = evt.newValue);
            SyncFromVault();
        }

        private static Slider AddSlider(VisualElement root, string label, float low, float high)
        {
            Slider slider = new Slider(label, low, high);
            slider.showInputField = true;
            root.Add(slider);
            return slider;
        }

        private static Toggle AddToggle(VisualElement root, string label)
        {
            Toggle toggle = new Toggle(label);
            root.Add(toggle);
            return toggle;
        }

        private void SyncFromVault()
        {
            IDataVault vault = ResolveVault();
            if (vault == null ||
                !vault.TryGetGenerationHandle(BufferID.ShinobuParasiteTuning, out VaultGenerationHandle<ParasiteSwarmTuningDTO> handle) ||
                !vault.TryReadHandle(in handle, out NativeArray<ParasiteSwarmTuningDTO> tuning) ||
                !tuning.IsCreated ||
                tuning.Length <= 0)
            {
                ParasiteSwarmTuningDTO fallback = ParasiteSwarmContracts.DefaultTuning();
                ApplyTuningToControls(fallback);
                return;
            }

            ApplyTuningToControls(ParasiteSwarmContracts.Sanitize(tuning[0]));
        }

        private void ApplyTuningToControls(ParasiteSwarmTuningDTO tuning)
        {
            _thermalAttraction.SetValueWithoutNotify(tuning.ThermalAttractionMultiplier);
            _curlFrequency.SetValueWithoutNotify(tuning.CurlNoiseFrequency);
            _maxSpeed.SetValueWithoutNotify(tuning.SwarmMaxSpeed);
            _qualityOverride.SetValueWithoutNotify(tuning.GlobalQualityOverride);
            _useQualityOverride.SetValueWithoutNotify((tuning.Flags & ParasiteSwarmContracts.TuningFlagUseQualityOverride) != 0u);
            _mockTargets.SetValueWithoutNotify((tuning.Flags & ParasiteSwarmContracts.TuningFlagMockTargets) != 0u);
            _drawDebugTargets.SetValueWithoutNotify(ParasiteAttractionDebugGizmo.DrawTargets);
        }

        private delegate void TuningMutation(ref ParasiteSwarmTuningDTO tuning);

        private static void MutateTuning(TuningMutation mutation)
        {
            IDataVault vault = ResolveVault();
            if (vault == null)
                return;

            ParasiteSwarmContracts.EnsureVaultBuffers(vault);
            if (!vault.TryGetGenerationHandle(BufferID.ShinobuParasiteTuning, out VaultGenerationHandle<ParasiteSwarmTuningDTO> handle) ||
                !vault.TryAcquireWriteLock(in handle, SystemID.Vfx, out NativeArray<ParasiteSwarmTuningDTO> tuning))
            {
                return;
            }

            try
            {
                if (!tuning.IsCreated || tuning.Length <= 0)
                    return;

                ParasiteSwarmTuningDTO value = tuning[0].Version == 0u ? ParasiteSwarmContracts.DefaultTuning() : tuning[0];
                mutation(ref value);
                value.Version++;
                tuning[0] = ParasiteSwarmContracts.Sanitize(value);
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.Vfx);
            }
        }

        private static void ReloadProfilesFromCsv()
        {
            IDataVault vault = ResolveVault();
            if (vault == null)
                return;

            ParasiteSwarmGpuRuntime.TryLoadProfilesFromDisk(vault);
        }

        private static IDataVault ResolveVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault != null)
                return vault;

            return GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latest) ? latest : null;
        }

        private sealed class TelemetryGraphElement : VisualElement
        {
            public TelemetryGraphElement()
            {
                style.height = 140f;
                style.marginTop = 8f;
                generateVisualContent += Generate;
            }

            private void Generate(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                if (rect.width <= 1f || rect.height <= 1f)
                    return;

                Painter2D painter = context.painter2D;
                painter.fillColor = new Color(0.05f, 0.07f, 0.075f, 1f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                painter.ClosePath();
                painter.Fill();

                IDataVault vault = ResolveVault();
                if (vault == null ||
                    !vault.TryGetGenerationHandle(BufferID.ShinobuParasiteTelemetryRing, out VaultGenerationHandle<SwarmTelemetryEntry> handle) ||
                    !vault.TryReadHandle(in handle, out NativeArray<SwarmTelemetryEntry> telemetry) ||
                    !telemetry.IsCreated ||
                    telemetry.Length <= 0)
                {
                    return;
                }

                int length = math.min(telemetry.Length, ParasiteSwarmContracts.TelemetryCapacity);
                if (length <= 1)
                    return;

                float widthStep = rect.width / math.max(1, length - 1);
                DrawSeries(painter, telemetry, length, widthStep, rect, true);
                DrawSeries(painter, telemetry, length, widthStep, rect, false);
            }

            private static void DrawSeries(
                Painter2D painter,
                NativeArray<SwarmTelemetryEntry> telemetry,
                int length,
                float widthStep,
                Rect rect,
                bool targetSeries)
            {
                painter.strokeColor = targetSeries ? new Color(0.1f, 0.9f, 0.55f, 1f) : new Color(1f, 0.22f, 0.08f, 1f);
                painter.lineWidth = 1.35f;
                painter.BeginPath();
                for (int i = 0; i < length; i++)
                {
                    SwarmTelemetryEntry entry = telemetry[i];
                    float x = rect.x + i * widthStep;
                    float normalized = targetSeries
                        ? math.saturate(entry.TargetCount / (float)ParasiteSwarmContracts.MaxTargetCount)
                        : math.saturate(entry.EstimatedGpuMicroseconds / 1500f);
                    float y = rect.yMax - normalized * rect.height;
                    Vector2 point = new Vector2(x, y);
                    if (i == 0)
                        painter.MoveTo(point);
                    else
                        painter.LineTo(point);
                }

                painter.Stroke();
            }
        }
    }
}
#endif
