#if UNITY_EDITOR
using System.IO;
using System.Text;
using Hecton8.Construction;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Construction.Editor
{
    public sealed class BulkheadContainmentTunerWindow : EditorWindow
    {
        private Label _statusLabel;
        private Slider _closeSpeed;
        private Slider _openSpeed;
        private Slider _overrideDistance;
        private Slider _catastrophicIntegrity;

        [MenuItem("HECTON-8/Construction/SHINOBU 220 Bulkhead Tuner")]
        public static void Open()
        {
            GetWindow<BulkheadContainmentTunerWindow>("SHINOBU 220 Bulkheads");
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 10;
            rootVisualElement.style.paddingRight = 10;
            rootVisualElement.style.paddingTop = 10;
            rootVisualElement.style.paddingBottom = 10;

            _statusLabel = new Label("Runtime inactive.");
            rootVisualElement.Add(_statusLabel);

            _closeSpeed = MakeSlider("Close speed", 0.05f, 8f, 2.4f);
            _openSpeed = MakeSlider("Open speed", 0.05f, 8f, 3f);
            _overrideDistance = MakeSlider("Override distance", 0.5f, 8f, 3.2f);
            _catastrophicIntegrity = MakeSlider("Catastrophic integrity", 0.01f, 0.99f, 0.18f);
            rootVisualElement.Add(_closeSpeed);
            rootVisualElement.Add(_openSpeed);
            rootVisualElement.Add(_overrideDistance);
            rootVisualElement.Add(_catastrophicIntegrity);

            Button applyButton = new Button(ApplyTuning) { text = "Apply Runtime Tuning" };
            rootVisualElement.Add(applyButton);

            Button csvButton = new Button(LoadCsvProfiles) { text = "Load bulkhead_profiles.csv" };
            rootVisualElement.Add(csvButton);

            Button inquisitionButton = new Button(DoorPhysicsInquisition.Run) { text = "Run Door Physics Inquisition" };
            rootVisualElement.Add(inquisitionButton);
        }

        private void OnInspectorUpdate()
        {
            if (_statusLabel == null)
                return;

            if (BulkheadContainmentRuntime.TryReadEditorState(out int activeCount, out float quality, out float cadenceHz, out float lastScheduleUs))
            {
                _statusLabel.text = $"Active: {activeCount} | Quality: {quality:0.00} | Cadence: {cadenceHz:0.0} Hz | Schedule: {lastScheduleUs:0.00} us";
            }
            else
            {
                _statusLabel.text = "Runtime inactive.";
            }
        }

        private static Slider MakeSlider(string label, float min, float max, float value)
        {
            Slider slider = new Slider(label, min, max) { value = value };
            slider.showInputField = true;
            return slider;
        }

        private void ApplyTuning()
        {
            BulkheadContainmentRuntime.TryApplyEditorTuning(
                _closeSpeed.value,
                _openSpeed.value,
                _overrideDistance.value,
                _catastrophicIntegrity.value);
        }

        private void LoadCsvProfiles()
        {
            string path = EditorUtility.OpenFilePanel("bulkhead_profiles.csv", Application.dataPath, "csv");
            if (string.IsNullOrEmpty(path))
                return;

            byte[] bytes = File.ReadAllBytes(path);
            BulkheadContainmentRuntime.TryLoadProfilesFromCsvBytes(bytes);
        }
    }

    public static class DoorPhysicsInquisition
    {
        private const string ReportPath = "Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json";

        [MenuItem("HECTON-8/Construction/Door Physics Inquisition")]
        public static void Run()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "_Project/Scripts"));
            int physicalDoorHits = 0;
            int colliderDoorHits = 0;
            int transformDoorMotionHits = 0;
            StringBuilder files = new StringBuilder(2048);

            ScanDirectory(Path.Combine(root, "Construction"), ref physicalDoorHits, ref colliderDoorHits, ref transformDoorMotionHits, files);
            ScanDirectory(Path.Combine(root, "Gameplay"), ref physicalDoorHits, ref colliderDoorHits, ref transformDoorMotionHits, files);

            string reportFullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ReportPath));
            Directory.CreateDirectory(Path.GetDirectoryName(reportFullPath));
            StringBuilder json = new StringBuilder(4096);
            json.AppendLine("{");
            json.AppendLine("  \"agent\": \"SHINOBU_220\",");
            json.AppendLine("  \"domain\": \"ECHELON 6 HABITAT & VEHICLES / BASE CONTAINMENT\",");
            json.AppendLine("  \"physicalDoorMentions\": " + physicalDoorHits + ",");
            json.AppendLine("  \"colliderDoorMentions\": " + colliderDoorHits + ",");
            json.AppendLine("  \"transformDoorMotionMentions\": " + transformDoorMotionHits + ",");
            json.AppendLine("  \"mathematicalBulkheadRuntime\": \"BulkheadContainmentRuntime\",");
            json.AppendLine("  \"notes\": \"Emergency BaseAirlock bulkhead state is DataVault/CSR/KCC plane driven; visual closure is shader-side.\",");
            json.AppendLine("  \"files\": [");
            json.Append(files);
            json.AppendLine();
            json.AppendLine("  ]");
            json.AppendLine("}");
            File.WriteAllText(reportFullPath, json.ToString());
            AssetDatabase.Refresh();
            Debug.Log("SHINOBU_220 Door Physics Inquisition wrote " + reportFullPath);
        }

        private static void ScanDirectory(
            string directory,
            ref int physicalDoorHits,
            ref int colliderDoorHits,
            ref int transformDoorMotionHits,
            StringBuilder files)
        {
            if (!Directory.Exists(directory))
                return;

            foreach (string file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                bool door = text.Contains("Door") || text.Contains("door") || text.Contains("Bulkhead") || text.Contains("bulkhead");
                if (!door)
                    continue;

                bool collider = text.Contains("BoxCollider") || text.Contains("MeshCollider") || text.Contains(".enabled =") && text.Contains("Collider");
                bool transformMotion = text.Contains(".localPosition") || text.Contains(".position =") || text.Contains("Animator");
                physicalDoorHits++;
                if (collider)
                    colliderDoorHits++;
                if (transformMotion)
                    transformDoorMotionHits++;

                if (files.Length > 0)
                    files.AppendLine(",");
                files.Append("    { \"path\": \"")
                    .Append(Escape(file.Replace('\\', '/')))
                    .Append("\", \"collider\": ")
                    .Append(collider ? "true" : "false")
                    .Append(", \"transformMotion\": ")
                    .Append(transformMotion ? "true" : "false")
                    .Append(" }");
            }
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif
