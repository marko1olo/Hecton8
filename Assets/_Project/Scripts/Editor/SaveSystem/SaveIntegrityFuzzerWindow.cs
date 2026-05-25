#if UNITY_EDITOR
using System.Globalization;
using Hecton8.SaveSystem;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.SaveSystem.Editor
{
    public sealed class SaveIntegrityFuzzerWindow : EditorWindow
    {
        private Label _summary;
        private Label _metrics;
        private WalFuzzerResultDTO _lastResult;
        private string _failureSectorLabel = "SHINOBU_256 WAL FAIL sector 0x0000000000000000";

        [MenuItem("HECTON-8/Save/Save Integrity Fuzzer")]
        public static void Open()
        {
            GetWindow<SaveIntegrityFuzzerWindow>("Save Integrity Fuzzer");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            Button run = new Button(RunFuzzer) { text = "RUN MASSIVE I/O CORRUPTION TEST" };
            run.style.height = 32f;
            root.Add(run);

            _summary = new Label("PENDING");
            _summary.style.marginTop = 8f;
            _summary.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(_summary);

            _metrics = new Label("No run.");
            _metrics.style.marginTop = 4f;
            root.Add(_metrics);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawFailureSector;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawFailureSector;
        }

        private void RunFuzzer()
        {
            bool passed = WalIntegrityFuzzerCore.RunDefaultEditorFuzzer(out _lastResult);
            _failureSectorLabel = "SHINOBU_256 WAL FAIL sector 0x" + _lastResult.FailedSectorHash.ToString("X16");
            if (_summary != null)
            {
                _summary.text = passed ? "PASS" : "FAIL";
                _summary.style.color = passed ? Color.green : Color.red;
            }

            if (_metrics != null)
            {
                float writeMb = _lastResult.BackupBytes / (1024f * 1024f);
                float writeSeconds = math.max(0.000001f, _lastResult.WriteMicros / 1000000f);
                float readMb = _lastResult.RecoveredBytes / (1024f * 1024f);
                float readSeconds = math.max(0.000001f, _lastResult.ReadMicros / 1000000f);
                _metrics.text =
                    "flags=0x" + _lastResult.ErrorFlags.ToString("X8") +
                    " code=" + _lastResult.ErrorCode +
                    " writeMBs=" + (writeMb / writeSeconds).ToString("F2", CultureInfo.InvariantCulture) +
                    " readMBs=" + (readMb / readSeconds).ToString("F2", CultureInfo.InvariantCulture) +
                    " sector=0x" + _lastResult.FailedSectorHash.ToString("X16");
            }

            Repaint();
            SceneView.RepaintAll();
        }

        private void DrawFailureSector(SceneView sceneView)
        {
            if (_lastResult.ErrorFlags == 0u)
                return;

            Handles.color = Color.red;
            Vector3 origin = Vector3.zero;
            Handles.DrawWireDisc(origin, Vector3.up, 2f);
            Handles.Label(origin + Vector3.up * 2.25f, _failureSectorLabel);
        }
    }
}
#endif
