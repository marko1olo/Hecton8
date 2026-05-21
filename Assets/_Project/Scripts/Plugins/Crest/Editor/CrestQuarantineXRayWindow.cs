#if UNITY_EDITOR
using System.IO;
using Hecton8.Crest.Bridge;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Environment.Fluids;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class CrestQuarantineXRayWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Hecton8/Crest/Quarantine & Adapter X-Ray";
        private const string DependencyReportPath = "Docs/Reports/ARCHITECTURE_OPTIMIZATION_REPORT.json";
        private Label _statusLabel;
        private Label _activeAdapterLabel;
        private Label _telemetryLabel;
        private TextField _reportField;

        [MenuItem(MenuPath, priority = 4200)]
        private static void Open()
        {
            CrestQuarantineXRayWindow window = GetWindow<CrestQuarantineXRayWindow>();
            window.titleContent = new GUIContent("Crest X-Ray");
            window.minSize = new Vector2(520f, 360f);
            window.Refresh();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 10f;
            root.style.paddingBottom = 10f;

            _statusLabel = new Label();
            _statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(_statusLabel);

            _activeAdapterLabel = new Label();
            root.Add(_activeAdapterLabel);

            _telemetryLabel = new Label();
            root.Add(_telemetryLabel);

            Button refresh = new Button(Refresh) { text = "Refresh" };
            root.Add(refresh);

            _reportField = new TextField("Last Dependency Wall Report")
            {
                multiline = true,
                isReadOnly = true
            };
            _reportField.style.flexGrow = 1f;
            root.Add(_reportField);

            Refresh();
        }

        private void Refresh()
        {
            if (_statusLabel == null)
                return;

            CrestOceanRuntimeAdapter[] adapters = Object.FindObjectsByType<CrestOceanRuntimeAdapter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            _statusLabel.text = "Crest bridge assembly active. Crest 5 package must remain outside Assets/Packages.";
            _activeAdapterLabel.text = "Crest runtime adapters in open scenes: " + adapters.Length;
            _telemetryLabel.text = ReadTelemetrySummary();

            string projectRoot = Directory.GetCurrentDirectory();
            string reportPath = Path.Combine(projectRoot, DependencyReportPath);
            _reportField.value = File.Exists(reportPath)
                ? File.ReadAllText(reportPath)
                : "No dependency report found. Run Tools/Crest_Dependency_Scanner.py.";
        }

        private static string ReadTelemetrySummary()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return "Vault telemetry: unavailable in editor context.";

            if (!vault.TryGetGenerationHandle<OceanAdapterTelemetryEntry>(
                    OceanAdapterVaultRoute.TelemetryRingBufferID,
                    out VaultGenerationHandle<OceanAdapterTelemetryEntry> telemetryHandle) ||
                !vault.TryResolveHandle(in telemetryHandle, out NativeArray<OceanAdapterTelemetryEntry> telemetry) ||
                !telemetry.IsCreated ||
                telemetry.Length == 0)
            {
                return "Vault telemetry: ocean ring not allocated.";
            }

            uint submitted = 0u;
            uint processed = 0u;
            uint dropped = 0u;
            int scanCount = Mathf.Min(telemetry.Length, OceanAdapterVaultRoute.TelemetryCapacity);
            for (int i = 0; i < scanCount; i++)
            {
                OceanAdapterTelemetryEntry entry = telemetry[i];
                submitted += entry.RequestsSubmitted;
                processed += entry.RequestsProcessed;
                dropped += entry.RequestsDropped;
            }

            return "Vault telemetry: frames=" + scanCount +
                   " submitted=" + submitted +
                   " processed=" + processed +
                   " dropped=" + dropped;
        }
    }
}
#endif
