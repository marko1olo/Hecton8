#if UNITY_EDITOR
using System.Globalization;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor.Structures
{
    public sealed class DeepReachStationArchitectWindow : EditorWindow
    {
        private string _moduleFolder = DeepReachStationModuleLibraryBuilder.DefaultPrefabFolder;
        private string _outputFolder = "Assets/_Project/Art/Baked/Structures";
        private string _stationName = "Station_AbyssHub";
        private uint _seed = 8421u;
        private int _gridX = 9;
        private int _gridY = 3;
        private int _gridZ = 13;
        private int _maxPlacements = 100;
        private float _cellSize = 7.5f;
        private float _quality = 0.72f;
        private float _weldEpsilon = 0.0015f;
        private StationFabricationResult _lastResult;
        private TextField _moduleFolderField;
        private TextField _outputFolderField;
        private TextField _stationNameField;
        private TextField _seedField;
        private SliderInt _gridXField;
        private SliderInt _gridYField;
        private SliderInt _gridZField;
        private SliderInt _maxPlacementsField;
        private Slider _cellSizeField;
        private Slider _qualityField;
        private Slider _weldEpsilonField;
        private Label _lastBakeLabel;
        private Label _vertexCountLabel;
        private Label _triangleCountLabel;
        private Label _faultMaskLabel;

        [MenuItem("Hecton/Structures/Deep Reach Station Architect")]
        public static void Open()
        {
            GetWindow<DeepReachStationArchitectWindow>("Station Architect");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            Label title = new Label("Deep Reach Station Architect");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4f;
            root.Add(title);

            Label domain = new Label("Offline static station prefab bake");
            domain.SetEnabled(false);
            domain.style.marginBottom = 8f;
            root.Add(domain);

            _moduleFolderField = new TextField("Module Prefab Folder") { value = _moduleFolder };
            _outputFolderField = new TextField("Output Folder") { value = _outputFolder };
            _stationNameField = new TextField("Station Name") { value = _stationName };
            _seedField = new TextField("Seed") { value = _seed.ToString(CultureInfo.InvariantCulture) };
            root.Add(_moduleFolderField);
            root.Add(_outputFolderField);
            root.Add(_stationNameField);
            root.Add(_seedField);

            Label gridTitle = new Label("WFC Grid");
            gridTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            gridTitle.style.marginTop = 8f;
            root.Add(gridTitle);

            _gridXField = CreateSliderInt("X", _gridX, 3, 64);
            _gridYField = CreateSliderInt("Y", _gridY, 1, 16);
            _gridZField = CreateSliderInt("Z", _gridZ, 3, 64);
            _maxPlacementsField = CreateSliderInt("Module Cap", _maxPlacements, 1, 512);
            _cellSizeField = CreateSlider("Cell Size", _cellSize, 1f, 32f);
            _qualityField = CreateSlider("Global Quality Weight", _quality, 0f, 1f);
            _weldEpsilonField = CreateSlider("Weld Epsilon", _weldEpsilon, 0.0001f, 0.05f);
            root.Add(_gridXField);
            root.Add(_gridYField);
            root.Add(_gridZField);
            root.Add(_maxPlacementsField);
            root.Add(_cellSizeField);
            root.Add(_qualityField);
            root.Add(_weldEpsilonField);

            Button bakeButton = new Button(BakeFromUi) { text = "Bake Monolithic Station Prefab" };
            bakeButton.style.height = 32f;
            bakeButton.style.marginTop = 10f;
            root.Add(bakeButton);

            Label lastBakeTitle = new Label("Last Bake");
            lastBakeTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            lastBakeTitle.style.marginTop = 10f;
            root.Add(lastBakeTitle);

            _lastBakeLabel = new Label();
            _vertexCountLabel = new Label();
            _triangleCountLabel = new Label();
            _faultMaskLabel = new Label();
            root.Add(_lastBakeLabel);
            root.Add(_vertexCountLabel);
            root.Add(_triangleCountLabel);
            root.Add(_faultMaskLabel);
            RefreshResultLabels();
        }

        private static SliderInt CreateSliderInt(string label, int value, int low, int high)
        {
            SliderInt slider = new SliderInt(label, low, high)
            {
                value = math.clamp(value, low, high),
                showInputField = true
            };
            return slider;
        }

        private static Slider CreateSlider(string label, float value, float low, float high)
        {
            Slider slider = new Slider(label, low, high)
            {
                value = math.clamp(value, low, high),
                showInputField = true
            };
            return slider;
        }

        private void BakeFromUi()
        {
            SyncSettingsFromUi();
            Bake();
            RefreshResultLabels();
        }

        private void SyncSettingsFromUi()
        {
            _moduleFolder = _moduleFolderField.value;
            _outputFolder = _outputFolderField.value;
            _stationName = _stationNameField.value;
            _seed = ParseSeed(_seedField.value, _seed);
            _seedField.value = _seed.ToString(CultureInfo.InvariantCulture);
            _gridX = _gridXField.value;
            _gridY = _gridYField.value;
            _gridZ = _gridZField.value;
            _maxPlacements = _maxPlacementsField.value;
            _cellSize = _cellSizeField.value;
            _quality = _qualityField.value;
            _weldEpsilon = _weldEpsilonField.value;
        }

        private static uint ParseSeed(string value, uint fallback)
        {
            if (uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsed) && parsed != 0u)
                return parsed;

            return fallback == 0u ? 1u : fallback;
        }

        private void RefreshResultLabels()
        {
            if (_lastBakeLabel == null)
                return;

            bool hasResult = !string.IsNullOrEmpty(_lastResult.PrefabPath) || !string.IsNullOrEmpty(_lastResult.FailureReason);
            _lastBakeLabel.text = hasResult ? (_lastResult.Success ? _lastResult.PrefabPath : _lastResult.FailureReason) : "No bake executed in this window.";
            _vertexCountLabel.text = "Vertices: " + _lastResult.FinalVertexCount;
            _triangleCountLabel.text = "Triangles: " + _lastResult.FinalTriangleCount;
            _faultMaskLabel.text = "Fault Mask: 0x" + _lastResult.Counters.FaultFlags.ToString("X8");
        }

        private void Bake()
        {
            StationFabricationSettings settings = new StationFabricationSettings
            {
                ModulePrefabFolder = _moduleFolder,
                OutputFolder = _outputFolder,
                StationName = _stationName,
                Seed = _seed,
                GridDims = new int3(_gridX, _gridY, _gridZ),
                MaxPlacements = _maxPlacements,
                CellSize = _cellSize,
                GlobalQualityWeight = _quality,
                WeldEpsilon = _weldEpsilon
            };

            bool ok = DeepReachStationFabricator.Fabricate(settings, out _lastResult);
            if (ok)
            {
                UnityEngine.Object prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_lastResult.PrefabPath);
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
            else
            {
                Debug.LogError(_lastResult.FailureReason);
            }
        }
    }
}
#endif
