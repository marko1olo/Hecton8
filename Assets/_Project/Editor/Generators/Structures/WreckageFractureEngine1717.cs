#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor.Structures
{
    public sealed class WreckageFractureEngine1717 : EditorWindow
    {
        private const string ForgeMenuPath = "Hecton8/Wreckage Forge/Open Forge";
        private const string BakeSelectionMenuPath = "Hecton8/Wreckage Forge/Bake Selected Assets";
        private const string ValidateSelectionMenuPath = "Hecton8/Wreckage Forge/Validate Selected Source Assets";
        private const string ScannerMenuPath = "Hecton8/Wreckage Forge/Scan Runtime Destruction";
        private const string OfflineLayoutMenuPath = "Hecton8/Wreckage Forge/Validate Offline Wreckage Layouts";
        private const string ProceduralLayoutMenuPath = "Hecton8/Procedural Wreckage/Validate Layouts";
        private const string MockBenchmarkMenuPath = "Hecton8/Wreckage Forge/Run Mock Benchmark";

        [MenuItem("Hecton8/Structures/Wreckage Fracture Engine 1717")]
        [MenuItem("Hecton8/Wreckage Forge/Open Fracture Engine 1717")]
        public static void Open()
        {
            if (!EditorApplication.ExecuteMenuItem(ForgeMenuPath))
                GetWindow<WreckageFractureEngine1717>("Wreckage 1717");
        }

        [MenuItem("Hecton8/Wreckage Forge/Validate Fracture Engine 1717")]
        public static void ValidateAllColdGates()
        {
            ExecuteColdGate(OfflineLayoutMenuPath);
            ExecuteColdGate(ProceduralLayoutMenuPath);
            ExecuteColdGate(ScannerMenuPath);
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            Label title = new Label("Wreckage Fracture Engine 1717");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4f;
            root.Add(title);

            Label route = new Label("Editor-only facade for the offline Wreckage Forge bake route.");
            route.SetEnabled(false);
            route.style.marginBottom = 8f;
            root.Add(route);

            Button openForge = new Button(OpenForge) { text = "Open Offline Forge" };
            root.Add(openForge);

            Button bakeSelectedAssets = new Button(BakeSelectedAssets) { text = "Bake Selected Source Assets" };
            root.Add(bakeSelectedAssets);

            Button validateSelection = new Button(ValidateSelectedSources) { text = "Validate Selected Source Assets" };
            root.Add(validateSelection);

            Button scanRuntime = new Button(ScanRuntimeDestruction) { text = "Scan Runtime Mesh Mutation" };
            root.Add(scanRuntime);

            Button validateLayouts = new Button(ValidateAllColdGates) { text = "Validate Layouts And Runtime Gate" };
            root.Add(validateLayouts);

            Button benchmark = new Button(RunMockBenchmark) { text = "Run Offline Mock Benchmark" };
            root.Add(benchmark);
        }

        private static void OpenForge()
        {
            EditorApplication.ExecuteMenuItem(ForgeMenuPath);
        }

        private static void BakeSelectedAssets()
        {
            ExecuteColdGate(BakeSelectionMenuPath);
        }

        private static void ValidateSelectedSources()
        {
            ExecuteColdGate(ValidateSelectionMenuPath);
        }

        private static void ScanRuntimeDestruction()
        {
            ExecuteColdGate(ScannerMenuPath);
        }

        private static void RunMockBenchmark()
        {
            ExecuteColdGate(MockBenchmarkMenuPath);
        }

        private static void ExecuteColdGate(string menuPath)
        {
            if (!EditorApplication.ExecuteMenuItem(menuPath))
                Debug.LogError("[WRECKAGE_1717] Missing editor gate: " + menuPath);
        }
    }
}
#endif
