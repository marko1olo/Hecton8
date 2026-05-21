#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor.OfflineGeometry
{
    public sealed class OfflineGeometryForgeWindow : EditorWindow
    {
        private readonly List<OfflineBakeSettings> _profiles = new List<OfflineBakeSettings>(16);
        private readonly List<string> _profileNames = new List<string>(16);
        private ObjectField _folderField;
        private DropdownField _profileDropdown;
        private Slider _lod1Ratio;
        private Slider _lod2Ratio;
        private Slider _primitiveTolerance;
        private Slider _qualityWeight;
        private Slider _depthMeters;
        private IntegerField _lod0Budget;
        private IntegerField _hullLimit;
        private ProgressBar _progress;

        [MenuItem("HECTON-8/LOD Collider Forge/LOD & Collider Forge", false, 249)]
        public static void Open()
        {
            OfflineGeometryForgeWindow window = GetWindow<OfflineGeometryForgeWindow>("LOD & Collider Forge");
            window.minSize = new Vector2(520f, 520f);
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;
            ReloadProfiles();

            _folderField = new ObjectField("Source Folder") { objectType = typeof(DefaultAsset), allowSceneObjects = false };
            _profileDropdown = new DropdownField("Profile", _profileNames, 0);
            _profileDropdown.RegisterValueChangedCallback(OnProfileChanged);
            _lod1Ratio = new Slider("LOD1 Triangle Ratio", 0.05f, 1f);
            _lod2Ratio = new Slider("LOD2 Triangle Ratio", 0.01f, 0.5f);
            _primitiveTolerance = new Slider("Primitive Fitting Tolerance", 0.01f, 0.5f);
            _qualityWeight = new Slider("Global Quality Weight", 0f, 1f);
            _depthMeters = new Slider("Spawn Depth Meters", 0f, 3000f);
            _lod0Budget = new IntegerField("LOD0 Hard Triangle Budget");
            _hullLimit = new IntegerField("Convex Hull Vertex Limit");
            _progress = new ProgressBar { title = "Bake Progress", lowValue = 0f, highValue = 1f, value = 0f };

            rootVisualElement.Add(_folderField);
            rootVisualElement.Add(_profileDropdown);
            rootVisualElement.Add(_lod1Ratio);
            rootVisualElement.Add(_lod2Ratio);
            rootVisualElement.Add(_primitiveTolerance);
            rootVisualElement.Add(_qualityWeight);
            rootVisualElement.Add(_depthMeters);
            rootVisualElement.Add(_lod0Budget);
            rootVisualElement.Add(_hullLimit);
            rootVisualElement.Add(_progress);

            rootVisualElement.Add(new Button(ReloadProfilesAndApply) { text = "Reload CSV Profiles" });
            rootVisualElement.Add(new Button(PreviewSelectedHull) { text = "Preview Selected Hull" });
            rootVisualElement.Add(new Button(BakeOptimizations) { text = "BAKE OPTIMIZATIONS" });
            rootVisualElement.Add(new Button(WriteStaticReports) { text = "Write Static Optimization Reports" });

            ApplyProfile(0);
        }

        private void OnProfileChanged(ChangeEvent<string> evt)
        {
            ApplyProfile(_profileDropdown.index);
        }

        private void OnDisable()
        {
            OfflineGeometryHullPreview.Clear();
        }

        private void ReloadProfilesAndApply()
        {
            ReloadProfiles();
            if (_profileDropdown != null)
            {
                _profileDropdown.choices = _profileNames;
                _profileDropdown.index = 0;
            }

            ApplyProfile(0);
        }

        private void ReloadProfiles()
        {
            _profiles.Clear();
            _profileNames.Clear();
            List<OfflineBakeSettings> loaded = OfflineOptimizationProfileCsv.LoadProfiles();
            for (int i = 0; i < loaded.Count; i++)
            {
                _profiles.Add(loaded[i]);
                _profileNames.Add(loaded[i].ProfileName.ToString());
            }

            if (_profiles.Count == 0)
            {
                OfflineBakeSettings fallback = OfflineOptimizationProfileCsv.DefaultSettings();
                _profiles.Add(fallback);
                _profileNames.Add(fallback.ProfileName.ToString());
            }
        }

        private void ApplyProfile(int index)
        {
            OfflineBakeSettings settings = _profiles[math.clamp(index, 0, math.max(0, _profiles.Count - 1))];
            _lod1Ratio?.SetValueWithoutNotify(settings.Lod1Ratio);
            _lod2Ratio?.SetValueWithoutNotify(settings.Lod2Ratio);
            _primitiveTolerance?.SetValueWithoutNotify(settings.PrimitiveTolerance);
            _qualityWeight?.SetValueWithoutNotify(settings.GlobalQualityWeight);
            _depthMeters?.SetValueWithoutNotify(settings.DepthMeters);
            _lod0Budget?.SetValueWithoutNotify(settings.Lod0HardBudget);
            _hullLimit?.SetValueWithoutNotify(settings.ConvexHullVertexLimit);
        }

        private OfflineBakeSettings ResolveSettings()
        {
            int index = _profileDropdown != null ? _profileDropdown.index : 0;
            OfflineBakeSettings settings = _profiles[math.clamp(index, 0, math.max(0, _profiles.Count - 1))];
            settings.Lod1Ratio = _lod1Ratio != null ? math.saturate(_lod1Ratio.value) : settings.Lod1Ratio;
            settings.Lod2Ratio = _lod2Ratio != null ? math.saturate(_lod2Ratio.value) : settings.Lod2Ratio;
            settings.PrimitiveTolerance = _primitiveTolerance != null ? math.max(0.001f, _primitiveTolerance.value) : settings.PrimitiveTolerance;
            settings.GlobalQualityWeight = _qualityWeight != null ? math.saturate(_qualityWeight.value) : settings.GlobalQualityWeight;
            settings.DepthMeters = _depthMeters != null ? math.max(0f, _depthMeters.value) : settings.DepthMeters;
            settings.Lod0HardBudget = _lod0Budget != null ? math.max(256, _lod0Budget.value) : settings.Lod0HardBudget;
            settings.ConvexHullVertexLimit = _hullLimit != null ? math.clamp(_hullLimit.value, OfflineGeometryBakerConstants.MinHullVertexCount, OfflineGeometryBakerConstants.MaxHullVertexCount) : settings.ConvexHullVertexLimit;
            return settings;
        }

        private void PreviewSelectedHull()
        {
            OfflineGeometryHullPreview.BuildFromSelection(ResolveSettings());
            SceneView.RepaintAll();
        }

        private void BakeOptimizations()
        {
            _progress.value = 0f;
            OfflineBakeSettings settings = ResolveSettings();
            var metrics = new List<OfflineBakeMetrics>(64);
            string folder = _folderField != null && _folderField.value != null ? AssetDatabase.GetAssetPath(_folderField.value) : null;
            if (!string.IsNullOrWhiteSpace(folder) && AssetDatabase.IsValidFolder(folder))
                metrics = OfflineGeometryBaker.BakeFolderBatch(folder, settings);
            else
                metrics = OfflineGeometryBaker.BakeSelection(settings);

            OfflineGeometryBaker.WriteOptimizationReport(metrics);
            _progress.value = 1f;
        }

        private void WriteStaticReports()
        {
            List<UnoptimizedMeshFinding> findings = Unoptimized_Mesh_Scanner.ScanProject();
            Unoptimized_Mesh_Scanner.WriteReport(findings);
            OfflineGeometrySelfAudit.WriteSelfAuditReport();
        }
    }

    internal static class OfflineGeometryHullPreview
    {
        private static OfflinePrimitiveFitResult _fit;
        private static FixedList512Bytes<float3> _hull;
        private static FixedList4096Bytes<ushort> _hullIndices;
        private static Matrix4x4 _matrix = Matrix4x4.identity;
        private static bool _hasPreview;

        static OfflineGeometryHullPreview()
        {
            SceneView.duringSceneGui -= DrawScenePreview;
            SceneView.duringSceneGui += DrawScenePreview;
        }

        internal static void BuildFromSelection(OfflineBakeSettings settings)
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                Clear();
                return;
            }

            MeshFilter filter = selected.GetComponentInChildren<MeshFilter>(true);
            if (filter == null || filter.sharedMesh == null)
            {
                Clear();
                return;
            }

            _matrix = filter.transform.localToWorldMatrix;
            _hasPreview = OfflineGeometryBaker.TryBuildPreviewHull(filter.sharedMesh, settings, out _fit, out _hull, out _hullIndices);
        }

        internal static void Clear()
        {
            _fit = default;
            _hull = default;
            _hullIndices = default;
            _hasPreview = false;
            SceneView.RepaintAll();
        }

        private static void DrawScenePreview(SceneView sceneView)
        {
            if (!_hasPreview)
                return;

            Matrix4x4 previous = Handles.matrix;
            Color previousColor = Handles.color;
            Handles.matrix = _matrix;
            Handles.color = new Color(0.1f, 1f, 0.35f, 0.95f);
            if (_fit.ColliderType == (byte)OfflineColliderKind.Sphere)
            {
                Vector3 center = new Vector3(_fit.Center.x, _fit.Center.y, _fit.Center.z);
                float radius = Mathf.Max(0.01f, _fit.Radius);
                Handles.DrawWireDisc(center, Vector3.up, radius);
                Handles.DrawWireDisc(center, Vector3.right, radius);
                Handles.DrawWireDisc(center, Vector3.forward, radius);
            }
            else
            {
                Vector3 center = new Vector3(_fit.Center.x, _fit.Center.y, _fit.Center.z);
                Vector3 size = new Vector3(_fit.Size.x, _fit.Size.y, _fit.Size.z);
                if (_fit.ColliderType == (byte)OfflineColliderKind.Box)
                    Handles.DrawWireCube(center, size);
                else
                    DrawHullEdges();
            }

            Handles.matrix = previous;
            Handles.color = previousColor;
        }

        private static void DrawHullEdges()
        {
            if (_hull.Length < 4 || _hullIndices.Length < 3)
                return;

            for (int i = 0; i + 2 < _hullIndices.Length; i += 3)
            {
                int a = _hullIndices[i];
                int b = _hullIndices[i + 1];
                int c = _hullIndices[i + 2];
                DrawEdge(a, b);
                DrawEdge(b, c);
                DrawEdge(c, a);
            }
        }

        private static void DrawEdge(int a, int b)
        {
            if ((uint)a >= (uint)_hull.Length || (uint)b >= (uint)_hull.Length)
                return;

            float3 pa = _hull[a];
            float3 pb = _hull[b];
            Handles.DrawLine(new Vector3(pa.x, pa.y, pa.z), new Vector3(pb.x, pb.y, pb.z));
        }
    }
}
#endif
