using System.Collections.Generic;
using System.Globalization;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Hecton8.Core;

namespace Hecton8.Editor.GeologyForge
{
    public sealed class GeologyForgeWindow : EditorWindow
    {
        private readonly List<GeologyBakeProfile> _profiles = new List<GeologyBakeProfile>(16);
        private readonly List<string> _profileNames = new List<string>(16);
        private readonly List<GeologyBakeProfile> _bakeRequestProfiles = new List<GeologyBakeProfile>(16);
        private DropdownField _profileDropdown;
        private SliderInt _resolution;
        private SliderInt _octaves;
        private Slider _frequency;
        private Slider _isoLevel;
        private SliderInt _aoRays;
        private Slider _qualityWeight;
        private IntegerField _variations;
        private TextField _seed;
        private Label _budgetSummary;
        private ProgressBar _progress;
        private int _selectedProfileIndex;

        [MenuItem("Hecton8/Geology Forge/Geology Forge", false, 179)]
        public static void Open()
        {
            GeologyForgeWindow window = GetWindow<GeologyForgeWindow>("Geology Forge");
            window.minSize = new Vector2(460f, 420f);
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;
            ReloadProfiles();

            _profileDropdown = new DropdownField("Profile", _profileNames, 0);
            _profileDropdown.RegisterValueChangedCallback(OnProfileDropdownChanged);
            rootVisualElement.Add(_profileDropdown);

            _seed = new TextField("Seed");
            _resolution = new SliderInt("SDF Resolution", GeologyForgeConstants.MinimumResolution, GeologyForgeConstants.MaximumResolution);
            _octaves = new SliderInt("Noise Octaves", 1, 8);
            _frequency = new Slider("Frequency", 0.1f, 5f);
            _isoLevel = new Slider("Iso-Level", -0.5f, 0.5f);
            _aoRays = new SliderInt("AO Rays", 1, GeologyForgeConstants.MaximumAoRays);
            _qualityWeight = new Slider("Global Quality Weight", 0f, 1f);
            _variations = new IntegerField("Variations");
            _budgetSummary = new Label();
            _progress = new ProgressBar { title = "Bake Progress", lowValue = 0f, highValue = 1f, value = 0f };

            rootVisualElement.Add(_seed);
            rootVisualElement.Add(_resolution);
            rootVisualElement.Add(_octaves);
            rootVisualElement.Add(_frequency);
            rootVisualElement.Add(_isoLevel);
            rootVisualElement.Add(_aoRays);
            rootVisualElement.Add(_qualityWeight);
            rootVisualElement.Add(_variations);
            rootVisualElement.Add(_budgetSummary);
            rootVisualElement.Add(_progress);

            Button reload = new Button(ReloadProfilesAndRepaint) { text = "Reload CSV" };
            Button preview = new Button(BuildPreview) { text = "Preview SDF Points" };
            Button bakeSelected = new Button(BakeSelected) { text = "BAKE SELECTED" };
            Button bakeAll = new Button(BakeAll) { text = "BAKE ALL" };
            Button cancel = new Button(GeologyForgeGenerator.CancelAsyncBake) { text = "Cancel Bake" };
            Button scan = new Button(RuntimeMeshGenerationScanner.ScanAndWriteReport) { text = "Scan Runtime Mesh Generation" };
            Button audit = new Button(GeologyForgeSelfAudit.RunAndWriteReport) { text = "Run Layout Self Audit" };
            rootVisualElement.Add(reload);
            rootVisualElement.Add(preview);
            rootVisualElement.Add(bakeSelected);
            rootVisualElement.Add(bakeAll);
            rootVisualElement.Add(cancel);
            rootVisualElement.Add(scan);
            rootVisualElement.Add(audit);

            SelectProfile(0);
        }

        private void OnProfileDropdownChanged(ChangeEvent<string> evt)
        {
            SelectProfile(_profileDropdown.index);
        }

        private void OnDisable()
        {
            GeologyForgePreview.Shutdown();
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
            GeologyForgeGenerator.TryLoadCsvProfiles(_profiles, "using 1606 validation profiles");

            if (_profiles.Count == 0)
                GeologyForgeGenerator.AddAgent1606ValidationProfiles(_profiles);

            SanitizeProfilesInPlace(_profiles);

            if (_profileNames.Capacity < _profiles.Count)
                _profileNames.Capacity = _profiles.Count;

            for (int i = 0; i < _profiles.Count; i++)
                _profileNames.Add(_profiles[i].Name.ToString());
        }

        private static void SanitizeProfilesInPlace(List<GeologyBakeProfile> profiles)
        {
            for (int i = 0; i < profiles.Count; i++)
                profiles[i] = GeologyForgeGenerator.SanitizeForEditor(profiles[i]);
        }

        private void SelectProfile(int index)
        {
            if (_profiles.Count == 0)
            {
                _selectedProfileIndex = 0;
                if (_budgetSummary != null)
                    _budgetSummary.text = "No geology profiles loaded";
                return;
            }

            _selectedProfileIndex = math.clamp(index, 0, math.max(0, _profiles.Count - 1));
            GeologyBakeProfile profile = _profiles[_selectedProfileIndex];
            profile = GeologyForgeGenerator.SanitizeForEditor(profile);
            _profiles[_selectedProfileIndex] = profile;
            _resolution?.SetValueWithoutNotify(profile.Resolution);
            _octaves?.SetValueWithoutNotify(profile.Octaves);
            _frequency?.SetValueWithoutNotify(profile.Frequency);
            _isoLevel?.SetValueWithoutNotify(profile.IsoLevel);
            _aoRays?.SetValueWithoutNotify(profile.AmbientOcclusionRays);
            _qualityWeight?.SetValueWithoutNotify(profile.GlobalQualityWeight);
            _variations?.SetValueWithoutNotify(SanitizeVariationCount(profile.Variations));
            _seed?.SetValueWithoutNotify(profile.Seed.ToString(CultureInfo.InvariantCulture));
            if (_budgetSummary != null)
                _budgetSummary.text = "LOD0 budget " + profile.Lod0Budget.ToString(CultureInfo.InvariantCulture) + " tris / COL proxy " + GeologyForgeConstants.CollisionProxyTriangleCount.ToString(CultureInfo.InvariantCulture) + " tris";
        }

        private GeologyBakeProfile ResolveProfileFromFields()
        {
            if (_profiles.Count == 0)
            {
                _profiles.Clear();
                GeologyForgeGenerator.AddAgent1606ValidationProfiles(_profiles);
            }

            GeologyBakeProfile profile = _profiles[math.clamp(_selectedProfileIndex, 0, _profiles.Count - 1)];
            profile.Resolution = _resolution != null ? _resolution.value : profile.Resolution;
            profile.Octaves = _octaves != null ? _octaves.value : profile.Octaves;
            profile.Frequency = _frequency != null ? _frequency.value : profile.Frequency;
            profile.IsoLevel = _isoLevel != null ? _isoLevel.value : profile.IsoLevel;
            profile.AmbientOcclusionRays = _aoRays != null ? _aoRays.value : profile.AmbientOcclusionRays;
            profile.GlobalQualityWeight = _qualityWeight != null ? _qualityWeight.value : profile.GlobalQualityWeight;
            profile.Variations = _variations != null ? SanitizeVariationCount(_variations.value) : SanitizeVariationCount(profile.Variations);
            profile.Seed = _seed != null ? ParseSeedOrFallback(_seed.value, profile.Seed) : profile.Seed;
            return GeologyForgeGenerator.SanitizeForEditor(profile);
        }

        private static uint ParseSeedOrFallback(string value, uint fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;
            string trimmed = value.Trim();
            if (trimmed.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
            {
                if (uint.TryParse(trimmed.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hexSeed))
                    return hexSeed;
                return fallback;
            }

            return uint.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint seed) ? seed : fallback;
        }

        private static int SanitizeVariationCount(int variations)
        {
            return math.clamp(variations <= 0 ? 1 : variations, 1, GeologyForgeConstants.MaximumVariations);
        }

        private void BuildPreview()
        {
            GeologyForgePreview.Build(ResolveProfileFromFields());
            SceneView.RepaintAll();
        }

        private void BakeSelected()
        {
            GeologyBakeProfile profile = ResolveProfileFromFields();
            _bakeRequestProfiles.Clear();
            _bakeRequestProfiles.Add(profile);
            TryStartBake(_bakeRequestProfiles);
        }

        private void BakeAll()
        {
            _bakeRequestProfiles.Clear();
            if (_bakeRequestProfiles.Capacity < _profiles.Count)
                _bakeRequestProfiles.Capacity = _profiles.Count;

            for (int i = 0; i < _profiles.Count; i++)
                _bakeRequestProfiles.Add(_profiles[i]);

            TryStartBake(_bakeRequestProfiles);
        }

        private void TryStartBake(List<GeologyBakeProfile> bakeList)
        {
            if (GeologyForgeGenerator.BakeProfilesAsync(bakeList, true, SetBakeProgress))
                return;

            SetBakeProgress(0f);
            H8Debug.LogWarning("Geology Forge async bake request ignored: no profiles loaded or a bake is already running.");
        }

        private void SetBakeProgress(float value)
        {
            if (_progress == null)
                return;

            _progress.value = math.saturate(value);
            _progress.MarkDirtyRepaint();
            Repaint();
        }
    }

    internal static class GeologyForgePreview
    {
        private const int PreviewResolution = 24;
        private const int MaxPreviewPoints = 2048;
        private const string NativeMemoryOwner = nameof(GeologyForgePreview);
        private static readonly Vector3[] _points;
        private static int _pointCount;
        private static bool _subscribed;

        static GeologyForgePreview()
        {
            // COLD ALLOC: Vector3[2048] — bounded SceneView preview point buffer — owner: GeologyForgePreview
            _points = new Vector3[MaxPreviewPoints];
        }

        public static void Build(GeologyBakeProfile profile)
        {
            profile = GeologyForgeGenerator.SanitizeForEditor(profile);
            int points = PreviewResolution;
            int count = points * points * points;
            float extent = math.max(0.5f, profile.RadiusMeters * 2.25f);
            float voxelStep = extent * math.rcp(points - 1);
            NativeArray<float> density = default;
            try
            {
                _pointCount = 0;
                // COLD ALLOC: NativeArray<float>[count] — editor preview SDF scratch — owner: GeologyForgePreview
                density = GeologyForgeNativeMemory.AllocateArray<float>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, NativeMemoryOwner, nameof(density));
                JobHandle previewHandle = new GenerateMockFractalNoiseJob
                {
                    Density = density,
                    SectorAup = profile.SectorAup,
                    Seed = profile.Seed,
                    Points = points,
                    Octaves = profile.Octaves,
                    VoxelStep = voxelStep,
                    RadiusMeters = profile.RadiusMeters,
                    HeightScale = profile.HeightScale,
                    Frequency = profile.Frequency,
                    NoiseAmplitude = profile.NoiseAmplitude,
                    RidgedWeight = profile.RidgedWeight,
                    VoronoiWeight = profile.VoronoiWeight,
                    IsoLevel = profile.IsoLevel,
                    GlobalQualityWeight = profile.GlobalQualityWeight
                }.Schedule(count, 64);
                previewHandle.Complete();

                float center = (points - 1) * 0.5f;
                float surfaceThreshold = voxelStep * 0.45f;
                int surfaceCandidateCount = 0;
                for (int i = 0; i < density.Length; i++)
                {
                    float d = density[i];
                    if (math.abs(d) <= surfaceThreshold)
                        surfaceCandidateCount++;
                }

                if (surfaceCandidateCount <= 0)
                {
                    _pointCount = 0;
                    return;
                }

                int targetCount = math.min(surfaceCandidateCount, MaxPreviewPoints);
                float candidateStride = surfaceCandidateCount * math.rcp((float)targetCount);
                float nextCandidate = 0f;
                int candidateIndex = 0;
                int previewCount = 0;
                for (int i = 0; i < density.Length && previewCount < targetCount; i++)
                {
                    float d = density[i];
                    if (math.abs(d) > surfaceThreshold)
                        continue;

                    if (candidateIndex + 0.5f < nextCandidate)
                    {
                        candidateIndex++;
                        continue;
                    }

                    int x = i % points;
                    int y = (i / points) % points;
                    int z = i / (points * points);
                    float3 p = new float3(x - center, y - center, z - center) * voxelStep;
                    _points[previewCount] = new Vector3(p.x, p.y, p.z);
                    previewCount++;
                    candidateIndex++;
                    nextCandidate += candidateStride;
                }

                _pointCount = previewCount;
                EnsureSubscribed();
            }
            finally
            {
                GeologyForgeNativeMemory.DisposeArray(ref density);
            }
        }

        public static void Clear()
        {
            _pointCount = 0;
            SceneView.RepaintAll();
        }

        public static void Shutdown()
        {
            _pointCount = 0;
            if (_subscribed)
            {
                SceneView.duringSceneGui -= DrawScenePreview;
                _subscribed = false;
            }

            SceneView.RepaintAll();
        }

        private static void EnsureSubscribed()
        {
            if (_subscribed)
                return;

            SceneView.duringSceneGui += DrawScenePreview;
            _subscribed = true;
        }

        private static void DrawScenePreview(SceneView sceneView)
        {
            if (_pointCount <= 0)
                return;

            Handles.color = new Color(0.55f, 0.8f, 1f, 0.75f);
            for (int i = 0; i < _pointCount; i++)
                Handles.DotHandleCap(0, _points[i], Quaternion.identity, 0.025f, EventType.Repaint);
        }
    }
}
