using System;
using System.Collections.Generic;
using System.IO;
using Hecton8.World.OfflineHadalArchBaker;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.World.OfflineHadalArchBaker.Editor
{
    public sealed class HadalStructureForgeWindow : EditorWindow
    {
        private const string CsvPath = "Assets/_SourceData/HadalGraphs/hadal_structure_graphs.csv";
        private readonly List<SdfShapeDTO> _shapes = new List<SdfShapeDTO>(HadalArchBakeConstants.MaxPreviewShapes);
        private TextField _assetNameField;
        private TextField _recipeField;
        private IntegerField _resolutionField;
        private FloatField _voxelSizeField;
        private Slider _qualitySlider;
        private FloatField _noiseFrequencyField;
        private FloatField _noiseAmplitudeField;
        private FloatField _cavityDistanceField;
        private IntegerField _cavityRaysField;
        private Toggle _createPrefabToggle;
        private Label _statusLabel;
        private ScrollView _shapeList;
        private bool _bakeInFlight;

        [MenuItem("Hecton8/Hadal Structure Forge/Open Forge")]
        public static void Open()
        {
            GetWindow<HadalStructureForgeWindow>("Hadal Structure Forge");
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _assetNameField = new TextField("Asset Name") { value = "GEN_Hadal_Lava_Arch" };
            _recipeField = new TextField("CSV Recipe") { value = "Abyssal_Lava_Arch" };
            _resolutionField = new IntegerField("Resolution") { value = HadalArchBakeConstants.DefaultResolution };
            _voxelSizeField = new FloatField("Voxel Size") { value = 0.75f };
            _qualitySlider = new Slider("Global Quality Weight", 0f, 1f) { value = 0.75f };
            _noiseFrequencyField = new FloatField("Noise Frequency") { value = 0.055f };
            _noiseAmplitudeField = new FloatField("Noise Amplitude") { value = 0.42f };
            _cavityDistanceField = new FloatField("Cavity Ray Distance") { value = 4.5f };
            _cavityRaysField = new IntegerField("Cavity Rays") { value = 8 };
            _createPrefabToggle = new Toggle("Create Static Prefab") { value = true };

            rootVisualElement.Add(_assetNameField);
            rootVisualElement.Add(_recipeField);
            rootVisualElement.Add(_resolutionField);
            rootVisualElement.Add(_voxelSizeField);
            rootVisualElement.Add(_qualitySlider);
            rootVisualElement.Add(_noiseFrequencyField);
            rootVisualElement.Add(_noiseAmplitudeField);
            rootVisualElement.Add(_cavityDistanceField);
            rootVisualElement.Add(_cavityRaysField);
            rootVisualElement.Add(_createPrefabToggle);

            VisualElement buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.Add(new Button(AddSphere) { text = "Add Sphere" });
            buttons.Add(new Button(AddBox) { text = "Add Box" });
            buttons.Add(new Button(AddTorus) { text = "Add Torus" });
            buttons.Add(new Button(AddCylinder) { text = "Add Cylinder" });
            buttons.Add(new Button(LoadCsv) { text = "Load CSV" });
            buttons.Add(new Button(RefreshPreview) { text = "Preview" });
            buttons.Add(new Button(BakeMonolith) { text = "BAKE MONOLITH" });
            rootVisualElement.Add(buttons);

            _shapeList = new ScrollView();
            _shapeList.style.height = 220;
            _shapeList.style.marginTop = 6;
            rootVisualElement.Add(_shapeList);

            _statusLabel = new Label("No bake run in this editor session.");
            _statusLabel.style.marginTop = 6;
            rootVisualElement.Add(_statusLabel);

            BuildDefaultGraph();
            RebuildShapeList();
            RefreshPreview();
        }

        private void OnDisable()
        {
            HadalSdfPreviewStore.Dispose();
        }

        private void AddSphere()
        {
            _shapes.Add(new SdfShapeDTO
            {
                ShapeType = (uint)SdfShapeType.Sphere,
                Operation = (uint)SdfBooleanOperation.Subtract,
                Position = new float3(0f, 2f, 0f),
                Extents = new float3(4f, 1f, 1f),
                BlendRadius = 0f,
                NoiseWeight = 0f
            });
            RebuildShapeList();
        }

        private void AddBox()
        {
            _shapes.Add(new SdfShapeDTO
            {
                ShapeType = (uint)SdfShapeType.Box,
                Operation = (uint)SdfBooleanOperation.Add,
                Position = new float3(0f, -16f, 0f),
                Extents = new float3(32f, 6f, 32f),
                BlendRadius = 0f,
                NoiseWeight = 0f
            });
            RebuildShapeList();
        }

        private void AddTorus()
        {
            _shapes.Add(new SdfShapeDTO
            {
                ShapeType = (uint)SdfShapeType.Torus,
                Operation = (uint)SdfBooleanOperation.SmoothUnion,
                Position = new float3(0f, 0f, 0f),
                Extents = new float3(22f, 5f, 0f),
                BlendRadius = 1.2f,
                NoiseWeight = 1f
            });
            RebuildShapeList();
        }

        private void AddCylinder()
        {
            _shapes.Add(new SdfShapeDTO
            {
                ShapeType = (uint)SdfShapeType.Cylinder,
                Operation = (uint)SdfBooleanOperation.Add,
                Position = new float3(0f, 0f, 0f),
                Extents = new float3(4f, 18f, 0f),
                BlendRadius = 0.5f,
                NoiseWeight = 1f
            });
            RebuildShapeList();
        }

        private void BuildDefaultGraph()
        {
            _shapes.Clear();
            AddBox();
            AddTorus();
            _shapes.Add(new SdfShapeDTO
            {
                ShapeType = (uint)SdfShapeType.Sphere,
                Operation = (uint)SdfBooleanOperation.Subtract,
                Position = new float3(-10f, -5f, 0f),
                Extents = new float3(8f, 1f, 1f)
            });
            _shapes.Add(new SdfShapeDTO
            {
                ShapeType = (uint)SdfShapeType.Sphere,
                Operation = (uint)SdfBooleanOperation.Subtract,
                Position = new float3(11f, -4f, -4f),
                Extents = new float3(7f, 1f, 1f)
            });
        }

        private void LoadCsv()
        {
            if (!HadalShapeGraphCsvParser.TryLoad(CsvPath, _recipeField.value, _shapes, out uint schemaHash))
            {
                _statusLabel.text = "CSV load failed or recipe missing: " + CsvPath;
                return;
            }

            _statusLabel.text = "Loaded CSV recipe. Schema hash 0x" + schemaHash.ToString("X8") + ". Shapes " + _shapes.Count + ".";
            RebuildShapeList();
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            if (_shapes.Count <= 0)
                return;

            HadalSdfPreviewStore.Rebuild(_shapes, new float3(42f, 28f, 42f));
            SceneView.RepaintAll();
        }

        private void BakeMonolith()
        {
            if (_bakeInFlight)
            {
                _statusLabel.text = "Bake already running.";
                return;
            }

            if (_shapes.Count <= 0)
                BuildDefaultGraph();

            HadalArchBakeConfigDTO config = BuildConfig();
            _bakeInFlight = HadalArchBakePipeline.BakeAsync(
                _assetNameField.value,
                _shapes.ToArray(),
                config,
                HadalArchBakePipeline.ResolveDefaultMaterial(),
                _createPrefabToggle.value,
                OnBakeCompleted,
                OnBakeFailed);

            _statusLabel.text = _bakeInFlight ? "Bake scheduled." : "Bake rejected; another bake is active.";
        }

        private void OnBakeCompleted(HadalArchBakeResult result)
        {
            _bakeInFlight = false;
            if (_statusLabel == null)
                return;

            _statusLabel.text =
                "Baked: LOD0 " + result.Lod0Triangles +
                " tris | LOD1 " + result.Lod1Triangles +
                " | LOD2 " + result.Lod2Triangles +
                " | warnings 0x" + result.WarningFlags.ToString("X8");
        }

        private void OnBakeFailed(Exception exception)
        {
            _bakeInFlight = false;
            if (_statusLabel == null)
                return;

            _statusLabel.text = "Bake failed: " + exception.GetType().Name;
        }

        private HadalArchBakeConfigDTO BuildConfig()
        {
            double3 centerAup = new double3(120000.0d, -7200.0d, -44000.0d);
            uint seed = HadalArchBakeMath.HashFnv1a(centerAup);
            return new HadalArchBakeConfigDTO
            {
                CenterAup = centerAup,
                VolumeOriginAup = centerAup,
                Resolution = new int3(math.max(16, _resolutionField.value)),
                VoxelSize = math.max(0.05f, _voxelSizeField.value),
                GlobalQualityWeight = math.saturate(_qualitySlider.value),
                NoiseFrequency = math.max(0.001f, _noiseFrequencyField.value),
                NoiseAmplitude = math.max(0f, _noiseAmplitudeField.value),
                CavityRayDistance = math.max(0.1f, _cavityDistanceField.value),
                CavityRayCount = math.clamp(_cavityRaysField.value, 1, 12),
                Seed = seed,
                ShapeCount = _shapes.Count,
                Lod1KeepRatio = math.lerp(0.35f, 0.65f, math.saturate(_qualitySlider.value)),
                Lod2KeepRatio = math.lerp(0.07f, 0.18f, math.saturate(_qualitySlider.value)),
                SurfaceBand = math.max(1f, _voxelSizeField.value * 5f)
            };
        }

        private void RebuildShapeList()
        {
            if (_shapeList == null)
                return;

            _shapeList.Clear();
            for (int i = 0; i < _shapes.Count; i++)
            {
                SdfShapeDTO shape = _shapes[i];
                _shapeList.Add(new Label(
                    i.ToString("00") +
                    " | type " + shape.ShapeType +
                    " | op " + shape.Operation +
                    " | pos " + shape.Position +
                    " | ext " + shape.Extents));
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class HadalSdfPreviewGizmo : MonoBehaviour
    {
        [SerializeField, Tooltip("Draws Hadal Structure Forge SDF preview hits in Scene View.")]
        private bool drawPreview = true;

        private void OnDrawGizmos()
        {
            if (!drawPreview || !HadalSdfPreviewStore.HasPreview)
                return;

            float3[] hits = HadalSdfPreviewStore.HitPositions;
            byte[] flags = HadalSdfPreviewStore.HitFlags;
            if (hits == null || flags == null)
                return;

            Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.75f);
            int count = math.min(hits.Length, flags.Length);
            for (int i = 0; i < count; i++)
            {
                if (flags[i] == 0)
                    continue;

                float3 p = hits[i];
                Gizmos.DrawCube(new Vector3(p.x, p.y, p.z), Vector3.one * 0.22f);
            }
        }
    }

    public static class HadalSdfPreviewStore
    {
        public static float3[] HitPositions;
        public static byte[] HitFlags;
        public static bool HasPreview;

        static HadalSdfPreviewStore()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting -= Dispose;
            EditorApplication.quitting += Dispose;
        }

        public static void Rebuild(List<SdfShapeDTO> shapes, float3 boundsExtents)
        {
            Dispose();
            int shapeCount = math.min(shapes.Count, HadalArchBakeConstants.MaxPreviewShapes);
            int2 grid = new int2(56, 40);
            int rayCount = grid.x * grid.y;
            NativeArray<SdfShapeDTO> nativeShapes = new NativeArray<SdfShapeDTO>(math.max(shapeCount, 1), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<float3> hitPositions = new NativeArray<float3>(rayCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<byte> hitFlags = new NativeArray<byte>(rayCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            try
            {
                for (int i = 0; i < shapeCount; i++)
                    nativeShapes[i] = shapes[i];

                new HadalSdfPreviewRaymarchJob
                {
                    Shapes = nativeShapes,
                    HitPositions = hitPositions,
                    HitFlags = hitFlags,
                    Grid = grid,
                    ShapeCount = shapeCount,
                    BoundsExtents = boundsExtents,
                    Steps = 72
                }.Schedule(rayCount, 64).Complete();
                HitPositions = new float3[rayCount];
                HitFlags = new byte[rayCount];
                for (int i = 0; i < rayCount; i++)
                {
                    HitPositions[i] = hitPositions[i];
                    HitFlags[i] = hitFlags[i];
                }

                HasPreview = true;
            }
            finally
            {
                if (nativeShapes.IsCreated)
                    nativeShapes.Dispose();
                if (hitPositions.IsCreated)
                    hitPositions.Dispose();
                if (hitFlags.IsCreated)
                    hitFlags.Dispose();
            }
        }

        public static void Dispose()
        {
            HitPositions = null;
            HitFlags = null;
            HasPreview = false;
        }
    }
}
