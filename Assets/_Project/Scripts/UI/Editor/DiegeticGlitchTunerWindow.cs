#if UNITY_EDITOR
using Hecton8.UI;
using UnityEditor;
using UnityEngine;

namespace Hecton8.UI.Editor
{
    public sealed class DiegeticGlitchTunerWindow : EditorWindow
    {
        private const string WindowTitle = "Diegetic Glitch Tuner";
        private const int PreviewCapacity = 128;

        // COLD ALLOC: char[128] - editor-only preview copy from vault mock text - owner: DiegeticGlitchTunerWindow
        private readonly char[] _previewBuffer = new char[PreviewCapacity];
        private DiegeticGlitchSurgeonRuntime _runtime;
        private string _previewText = string.Empty;
        private int _previewTextHash;
        private int _previewTextLength;

        [MenuItem("Tools/HECTON-8/Diegetic Glitch Tuner")]
        public static void Open()
        {
            GetWindow<DiegeticGlitchTunerWindow>(WindowTitle);
        }

        private void OnEnable()
        {
            EditorApplication.update += PollCsvOverride;
        }

        private void OnDisable()
        {
            EditorApplication.update -= PollCsvOverride;
        }

        private void OnGUI()
        {
            DrawRuntimeSelector();
            if (_runtime == null)
            {
                EditorGUILayout.HelpBox("No DiegeticGlitchSurgeonRuntime selected.", MessageType.Info);
                return;
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to read/write GlobalDataVault memory.", MessageType.Warning);
                return;
            }

            if (!_runtime.IsNativeReady)
            {
                EditorGUILayout.HelpBox("Runtime vault buffers are not ready.", MessageType.Warning);
                return;
            }

            DrawTuningSliders();
            if (_runtime.IsJobScheduled)
                EditorGUILayout.HelpBox("Runtime job in flight; state readout is paused for vault safety.", MessageType.None);
            else
                DrawStateReadout();

            DrawPreviewPanel();
        }

        private void PollCsvOverride()
        {
            if (_runtime == null || !Application.isPlaying || !_runtime.IsNativeReady)
                return;

            if (_runtime.PollCsvOverrideForEditor(EditorApplication.timeSinceStartup))
                Repaint();
        }

        private void DrawRuntimeSelector()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _runtime = (DiegeticGlitchSurgeonRuntime)EditorGUILayout.ObjectField("Runtime", _runtime, typeof(DiegeticGlitchSurgeonRuntime), true);
                if (GUILayout.Button("Find", GUILayout.Width(64f)))
                    _runtime = FindFirstObjectByType<DiegeticGlitchSurgeonRuntime>();
            }
        }

        private void DrawTuningSliders()
        {
            ref GlitchTuningDTO tuning = ref _runtime.GetTuningRef();
            EditorGUI.BeginChangeCheck();
            float master = EditorGUILayout.Slider("Master Intensity", tuning.MasterIntensity, 0f, 1f);
            float text = EditorGUILayout.Slider("Text Scramble Rate", tuning.TextScrambleRate, 0f, 1f);
            float matrix = EditorGUILayout.Slider("Matrix Shatter Strength", tuning.MatrixShatterStrength, 0f, 1f);
            float ghosts = EditorGUILayout.Slider("Ghost Blip Count", tuning.GhostBlipCount, 0f, 32f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_runtime, "Diegetic Glitch Tuning");
                _runtime.ApplyTuning(master, text, matrix, ghosts);
                EditorUtility.SetDirty(_runtime);
            }

            if (GUILayout.Button("Reload GlitchTable.bytes / CSV"))
            {
                _runtime.ReloadGlitchTableForEditor();
                _runtime.ReloadCsvForEditor();
            }
        }

        private void DrawStateReadout()
        {
            ref GlitchStateDTO state = ref _runtime.GetGlitchStateRef();
            ref GlitchTuningDTO tuning = ref _runtime.GetTuningRef();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Global Intensity", state.GlobalIntensity.ToString("0.000"));
            EditorGUILayout.LabelField("Global Quality Weight", tuning.GlobalQualityWeight.ToString("0.000"));
            EditorGUILayout.LabelField("Table Offset", state.GlitchTableOffset.ToString());
        }

        private void DrawPreviewPanel()
        {
            int count = _runtime.CopyMockTextTo(_previewBuffer);
            if (count < 0)
            {
                DrawPreviewLabel();
                Repaint();
                return;
            }

            int hash = 17;
            for (int i = 0; i < count; i++)
                hash = unchecked(hash * 31 + _previewBuffer[i]);

            if (count != _previewTextLength || hash != _previewTextHash)
            {
                _previewText = count > 0 ? new string(_previewBuffer, 0, count) : string.Empty; // EDITOR ALLOC: IMGUI GUI.Label string cache - owner: DiegeticGlitchTunerWindow
                _previewTextLength = count;
                _previewTextHash = hash;
            }

            EditorGUILayout.Space();
            DrawPreviewLabel();
            Repaint();
        }

        private void DrawPreviewLabel()
        {
            Rect previewRect = EditorGUILayout.GetControlRect(false, 54f);
            GUI.Box(previewRect, GUIContent.none);
            Rect labelRect = new Rect(previewRect.x + 8f, previewRect.y + 8f, previewRect.width - 16f, previewRect.height - 16f);
            GUI.Label(labelRect, _previewText, EditorStyles.boldLabel);
        }
    }
}
#endif
