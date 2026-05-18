using Hecton8.Visor;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public sealed class DiegeticVisorTunerWindow : EditorWindow
    {
        private VisorStateDTO _previewState;
        private VisorLensTuningDTO _previewTuning;
        private DiegeticVisorLensGpuGlobalsDTO _previewGlobals;

        [MenuItem("Hecton8/Visor/Diegetic Visor Tuner")]
        private static void Open()
        {
            GetWindow<DiegeticVisorTunerWindow>("Diegetic Visor Tuner");
        }

        private void OnGUI()
        {
            DiegeticVisorLensRuntime runtime = FindRuntime();
            bool hasRuntime = runtime != null && TryReadRuntime(runtime);
            if (!hasRuntime)
            {
                EditorGUILayout.HelpBox("DiegeticVisorLensRuntime is not active.", MessageType.Warning);
                DrawPreview(_previewState, _previewGlobals);
                return;
            }

            EditorGUI.BeginChangeCheck();
            _previewState.CondensationLevel = EditorGUILayout.Slider("Condensation", _previewState.CondensationLevel, 0f, 1f);
            _previewState.WaterDropletIntensity = EditorGUILayout.Slider("Droplets", _previewState.WaterDropletIntensity, 0f, 1f);
            _previewState.CrackSeverity = EditorGUILayout.Slider("Cracks", _previewState.CrackSeverity, 0f, 1f);
            _previewState.DirtAccumulation = EditorGUILayout.Slider("Dirt", _previewState.DirtAccumulation, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                runtime.TryWriteState(in _previewState);
            }

            EditorGUILayout.Space(6f);
            EditorGUI.BeginChangeCheck();
            _previewTuning.FogRate = EditorGUILayout.Slider("Fog Rate", _previewTuning.FogRate, 0f, 0.4f);
            _previewTuning.FogBreathGain = EditorGUILayout.Slider("Breath Fog Gain", _previewTuning.FogBreathGain, 0f, 0.2f);
            _previewTuning.ClearingRate = EditorGUILayout.Slider("Clearing Rate", _previewTuning.ClearingRate, 0f, 1f);
            _previewTuning.DropletDrainSeconds = EditorGUILayout.Slider("Droplet Drain Seconds", _previewTuning.DropletDrainSeconds, 0.25f, 12f);
            _previewTuning.CrackPressureThreshold = EditorGUILayout.Slider("Crack Pressure Gate", _previewTuning.CrackPressureThreshold, 0f, 1f);
            _previewTuning.DirtSiltGain = EditorGUILayout.Slider("Silt Gain", _previewTuning.DirtSiltGain, 0f, 0.2f);
            _previewTuning.LowRefractionQualityCutoff = EditorGUILayout.Slider("Refraction Cutoff", _previewTuning.LowRefractionQualityCutoff, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                _previewTuning.Version++;
                runtime.TryWriteTuning(in _previewTuning);
            }

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Mock"))
                    runtime.GenerateEmergencyMockVisorData();
                if (GUILayout.Button("Reload CSV"))
                    runtime.TryReloadCsvOverrides();
                if (GUILayout.Button("Wipe"))
                    runtime.RequestWipeVisor(1f);
                if (GUILayout.Button("Breach"))
                    runtime.InjectMockExternalPressure(1f);
            }

            DrawPreview(_previewState, _previewGlobals);
            Repaint();
        }

        private bool TryReadRuntime(DiegeticVisorLensRuntime runtime)
        {
            try
            {
                return runtime.TryGetPreview(out _previewState, out _previewGlobals, out _previewTuning);
            }
            catch
            {
                return false;
            }
        }

        private static DiegeticVisorLensRuntime FindRuntime()
        {
            return UnityEngine.Object.FindFirstObjectByType<DiegeticVisorLensRuntime>(FindObjectsInactive.Include);
        }

        private static void DrawPreview(VisorStateDTO state, DiegeticVisorLensGpuGlobalsDTO globals)
        {
            Rect rect = GUILayoutUtility.GetRect(260f, 176f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.015f, 0.02f, 0.022f, 1f));

            float condensation = Mathf.Clamp01(state.CondensationLevel);
            float droplets = Mathf.Clamp01(state.WaterDropletIntensity);
            float cracks = Mathf.Clamp01(state.CrackSeverity);
            float dirt = Mathf.Clamp01(state.DirtAccumulation);
            Color fog = new Color(0.42f, 0.52f, 0.56f, condensation * 0.36f);
            EditorGUI.DrawRect(Shrink(rect, rect.width * 0.05f, rect.height * 0.08f), fog);

            Handles.BeginGUI();
            Color oldColor = Handles.color;
            Handles.color = new Color(0.5f, 0.72f, 0.78f, 0.28f + droplets * 0.48f);
            for (int i = 0; i < 14; i++)
            {
                float u = Frac(i * 0.31831f + droplets * 0.17f);
                float x = Mathf.Lerp(rect.xMin + 14f, rect.xMax - 14f, u);
                float y0 = Mathf.Lerp(rect.yMin + 10f, rect.yMax - 42f, Frac(i * 0.217f));
                float y1 = Mathf.Min(rect.yMax - 8f, y0 + 18f + droplets * 64f);
                Handles.DrawLine(new Vector3(x, y0), new Vector3(x + globals.Params0.x * 20f, y1));
            }

            Handles.color = new Color(0.8f, 0.92f, 0.95f, cracks);
            for (int i = 0; i < 5; i++)
            {
                float y = Mathf.Lerp(rect.yMin + 24f, rect.yMax - 24f, Frac(i * 0.377f + cracks * 0.13f));
                float x0 = Mathf.Lerp(rect.xMin + 18f, rect.xMax - 52f, Frac(i * 0.233f));
                Handles.DrawAAPolyLine(2f, new Vector3(x0, y), new Vector3(x0 + 38f + cracks * 74f, y + (i - 2) * 9f));
            }

            Handles.color = new Color(0.16f, 0.14f, 0.1f, 0.25f + dirt * 0.55f);
            for (int i = 0; i < 32; i++)
            {
                float x = Mathf.Lerp(rect.xMin + 6f, rect.xMax - 6f, Frac(i * 0.7548777f));
                float y = Mathf.Lerp(rect.yMin + 6f, rect.yMax - 6f, Frac(i * 0.5698403f));
                float r = 1f + dirt * 4f * Frac(i * 0.381f);
                Handles.DrawSolidDisc(new Vector3(x, y), Vector3.forward, r);
            }

            Handles.color = oldColor;
            Handles.EndGUI();
        }

        private static Rect Shrink(Rect rect, float horizontal, float vertical)
        {
            return new Rect(rect.x + horizontal, rect.y + vertical, rect.width - horizontal * 2f, rect.height - vertical * 2f);
        }

        private static float Frac(float value)
        {
            return value - Mathf.Floor(value);
        }
    }
}
