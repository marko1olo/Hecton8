#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor.HydraulicErosionForge
{
    public sealed class HydraulicErosionForgeWindow : EditorWindow
    {
        private readonly List<WeatheringProfileDTO> _profiles = new List<WeatheringProfileDTO>(8);
        private readonly List<string> _profileNames = new List<string>(8);
        private DropdownField _profileDropdown;
        private SliderInt _droplets;
        private Slider _rainRate;
        private Slider _evaporation;
        private Slider _capacity;
        private Slider _aggression;
        private Slider _quality;
        private ProgressBar _progress;
        private Image _preview;
        private Texture2D _previewTexture;
        private int _selectedProfileIndex;
        private bool _previewQueued;

        [MenuItem("HECTON-8/Hydraulic Erosion Forge/Hydraulic Erosion Forge", false, 189)]
        public static void Open()
        {
            HydraulicErosionForgeWindow window = GetWindow<HydraulicErosionForgeWindow>("Hydraulic Erosion Forge");
            window.minSize = new Vector2(480f, 560f);
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;
            ReloadProfiles();

            _profileDropdown = new DropdownField("Weathering Profile", _profileNames, 0);
            _profileDropdown.RegisterValueChangedCallback(OnProfileChanged);
            _droplets = new SliderInt("Droplet Count", 0, HydraulicErosionForgeConstants.DefaultDropletCount) { showInputField = true };
            _rainRate = new Slider("Rain Rate", 0.05f, 4f) { showInputField = true };
            _evaporation = new Slider("Evaporation Speed", 0.001f, 0.08f) { showInputField = true };
            _capacity = new Slider("Sediment Capacity", 0.1f, 12f) { showInputField = true };
            _aggression = new Slider("Erosion Aggressiveness", 0.01f, 1f) { showInputField = true };
            _quality = new Slider("Global Quality Weight", 0f, 1f) { showInputField = true };
            _progress = new ProgressBar { title = "Bake Progress", lowValue = 0f, highValue = 1f, value = 0f };
            _preview = new Image { scaleMode = ScaleMode.ScaleToFit };
            _preview.style.height = 256;
            RegisterPreviewCallbacks();

            rootVisualElement.Add(_profileDropdown);
            rootVisualElement.Add(_droplets);
            rootVisualElement.Add(_rainRate);
            rootVisualElement.Add(_evaporation);
            rootVisualElement.Add(_capacity);
            rootVisualElement.Add(_aggression);
            rootVisualElement.Add(_quality);
            rootVisualElement.Add(_progress);
            rootVisualElement.Add(_preview);
            rootVisualElement.Add(new Button(ReloadProfilesAndRepaint) { text = "Reload CSV Profiles" });
            rootVisualElement.Add(new Button(BuildPreview) { text = "Preview Patch" });
            rootVisualElement.Add(new Button(StartBake) { text = "SIMULATE EROSION" });
            rootVisualElement.Add(new Button(Terrain_Runtime_Scanner_Erosion.ScanMenu) { text = "Scan Runtime Erosion" });
            rootVisualElement.Add(new Button(HydraulicErosionForgeSelfAudit.RunAndWriteReport) { text = "Run Self Audit" });

            SelectProfile(0);
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= RunQueuedPreview;
            if (_previewTexture != null)
                DestroyImmediate(_previewTexture);
            _previewTexture = null;
        }

        private void OnProfileChanged(ChangeEvent<string> evt)
        {
            SelectProfile(_profileDropdown.index);
        }

        private void ReloadProfilesAndRepaint()
        {
            ReloadProfiles();
            if (_profileDropdown != null)
            {
                _profileDropdown.choices = _profileNames;
                _profileDropdown.index = math.clamp(_selectedProfileIndex, 0, math.max(0, _profileNames.Count - 1));
            }

            SelectProfile(_profileDropdown != null ? _profileDropdown.index : 0);
        }

        private void ReloadProfiles()
        {
            _profiles.Clear();
            _profileNames.Clear();
            HydraulicErosionForgeBaker.LoadWeatheringProfiles(_profiles);
            if (_profileNames.Capacity < _profiles.Count)
                _profileNames.Capacity = _profiles.Count;
            for (int i = 0; i < _profiles.Count; i++)
                _profileNames.Add(_profiles[i].Name.ToString());
        }

        private void SelectProfile(int index)
        {
            if (_profiles.Count == 0)
                return;

            _selectedProfileIndex = math.clamp(index, 0, _profiles.Count - 1);
            WeatheringProfileDTO profile = _profiles[_selectedProfileIndex];
            _droplets?.SetValueWithoutNotify(HydraulicErosionForgeConstants.DefaultDropletCount);
            _rainRate?.SetValueWithoutNotify(profile.RainRate);
            _evaporation?.SetValueWithoutNotify(profile.EvaporationSpeed);
            _capacity?.SetValueWithoutNotify(profile.SedimentCapacity);
            _aggression?.SetValueWithoutNotify(profile.ErosionAggressiveness);
            _quality?.SetValueWithoutNotify(0.75f);
            QueuePreview();
        }

        private void RegisterPreviewCallbacks()
        {
            _droplets.RegisterValueChangedCallback(_ => QueuePreview());
            _rainRate.RegisterValueChangedCallback(_ => QueuePreview());
            _evaporation.RegisterValueChangedCallback(_ => QueuePreview());
            _capacity.RegisterValueChangedCallback(_ => QueuePreview());
            _aggression.RegisterValueChangedCallback(_ => QueuePreview());
            _quality.RegisterValueChangedCallback(_ => QueuePreview());
        }

        private void QueuePreview()
        {
            if (_previewQueued || HydraulicErosionForgeBaker.IsBusy)
                return;

            _previewQueued = true;
            EditorApplication.delayCall -= RunQueuedPreview;
            EditorApplication.delayCall += RunQueuedPreview;
        }

        private void RunQueuedPreview()
        {
            _previewQueued = false;
            if (this == null || HydraulicErosionForgeBaker.IsBusy)
                return;

            BuildPreview();
        }

        private WeatheringProfileDTO ResolveProfileFromFields()
        {
            WeatheringProfileDTO profile = _profiles.Count > 0 ? _profiles[math.clamp(_selectedProfileIndex, 0, _profiles.Count - 1)] : HydraulicErosionWeatheringCsv.DefaultProfile();
            profile.RainRate = _rainRate != null ? _rainRate.value : profile.RainRate;
            profile.EvaporationSpeed = _evaporation != null ? _evaporation.value : profile.EvaporationSpeed;
            profile.SedimentCapacity = _capacity != null ? _capacity.value : profile.SedimentCapacity;
            profile.ErosionAggressiveness = _aggression != null ? _aggression.value : profile.ErosionAggressiveness;
            return profile;
        }

        private HydraulicErosionSettingsDTO ResolveSettings(bool preview)
        {
            int resolution = preview ? HydraulicErosionForgeConstants.PreviewResolution : HydraulicErosionForgeConstants.MockResolution;
            int requestedDroplets = _droplets != null ? _droplets.value : HydraulicErosionForgeConstants.DefaultDropletCount;
            int dropletCount = preview ? math.min(requestedDroplets, HydraulicErosionForgeConstants.PreviewDropletCount) : requestedDroplets;
            float quality = _quality != null ? _quality.value : 0.75f;
            return HydraulicErosionForgeBaker.BuildSettingsFromProfile(ResolveProfileFromFields(), resolution, dropletCount, double3.zero, 0, 0, quality);
        }

        private void BuildPreview()
        {
            Texture2D next = HydraulicErosionForgeBaker.BuildPreviewTexture(ResolveSettings(true));
            if (_previewTexture != null)
                DestroyImmediate(_previewTexture);
            _previewTexture = next;
            if (_preview != null)
                _preview.image = _previewTexture;
        }

        private void StartBake()
        {
            if (HydraulicErosionForgeBaker.StartMockSectorBake(ResolveSettings(false), SetProgress))
                return;

            SetProgress(0f);
            Debug.LogWarning("[SHINOBU_242] Hydraulic erosion bake request ignored: another bake is active.");
        }

        private void SetProgress(float value)
        {
            if (_progress == null)
                return;

            _progress.value = math.saturate(value);
            _progress.MarkDirtyRepaint();
            Repaint();
        }
    }
}
#endif
