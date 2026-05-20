using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.World.OfflineWreckageBaker;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Hecton8.World.OfflineWreckageBaker.Editor
{
    public sealed class WreckageForgeWindow : EditorWindow
    {
        private const string OutputFolder = "Assets/_Project/BakedGeometry/Wreckage";
        private const string ProfileFileName = "wreckage_deformation_profiles.csv";
        internal const int ProfileCapacity = 16;
        private const int IndexCopyTileSize = 384;
        private static readonly VertexAttributeDescriptor[] s_vertexLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 3)
        };
        private readonly List<string> _pendingAssetPaths = new List<string>(64); // COLD ALLOC: List<string>[64] - editor batch queue - owner: WreckageForgeWindow
        private ObjectField _folderField;
        private ObjectField _meshField;
        private Slider _qualitySlider;
        private Slider _blastRadiusSlider;
        private Slider _tearThresholdSlider;
        private Slider _shearTorsionSlider;
        private Slider _scorchSlider;
        private Slider _collapseSlider;
        private DoubleField _moduleAupXField;
        private DoubleField _moduleAupYField;
        private DoubleField _moduleAupZField;
        private DoubleField _blastAupXField;
        private DoubleField _blastAupYField;
        private DoubleField _blastAupZField;
        private DropdownField _profileDropdown;
        private ProgressBar _progressBar;
        private Label _statusLabel;
        private WreckageProfileCache _profileCache;
        private int _profileCount;
        private int _batchIndex;
        private bool _batchActive;
        private BakeReportAccumulator _report;

        [MenuItem("HECTON-8/Wreckage Forge/Open Forge")]
        public static void Open()
        {
            GetWindow<WreckageForgeWindow>("Wreckage Forge");
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _folderField = new ObjectField("Pristine Prefab/Mesh Folder") { objectType = typeof(DefaultAsset), allowSceneObjects = false };
            _meshField = new ObjectField("Preview Mesh") { objectType = typeof(Mesh), allowSceneObjects = false };
            rootVisualElement.Add(_folderField);
            rootVisualElement.Add(_meshField);

            _profileDropdown = new DropdownField("Damage Profile");
            rootVisualElement.Add(_profileDropdown);

            _qualitySlider = new Slider("GlobalQualityWeight", 0f, 1f) { value = 0.65f };
            _blastRadiusSlider = new Slider("Blast Radius", 0.25f, 80f) { value = 14f };
            _tearThresholdSlider = new Slider("Tear Threshold", 0.01f, 0.95f) { value = 0.38f };
            _shearTorsionSlider = new Slider("Shear Torsion", 0f, 4f) { value = 1.15f };
            _scorchSlider = new Slider("Scorch Intensity", 0f, 2f) { value = 1f };
            _collapseSlider = new Slider("Collapse Compression", 0f, 0.9f) { value = 0.35f };
            _moduleAupXField = new DoubleField("Module AUP X");
            _moduleAupYField = new DoubleField("Module AUP Y");
            _moduleAupZField = new DoubleField("Module AUP Z");
            _blastAupXField = new DoubleField("Blast AUP X");
            _blastAupYField = new DoubleField("Blast AUP Y");
            _blastAupZField = new DoubleField("Blast AUP Z");
            rootVisualElement.Add(_qualitySlider);
            rootVisualElement.Add(_blastRadiusSlider);
            rootVisualElement.Add(_tearThresholdSlider);
            rootVisualElement.Add(_shearTorsionSlider);
            rootVisualElement.Add(_scorchSlider);
            rootVisualElement.Add(_collapseSlider);
            rootVisualElement.Add(_moduleAupXField);
            rootVisualElement.Add(_moduleAupYField);
            rootVisualElement.Add(_moduleAupZField);
            rootVisualElement.Add(_blastAupXField);
            rootVisualElement.Add(_blastAupYField);
            rootVisualElement.Add(_blastAupZField);

            Button loadProfiles = new Button(LoadProfiles) { text = "LOAD CSV PROFILES" };
            Button previewButton = new Button(PreviewSelectedMesh) { text = "PREVIEW SELECTED MESH" };
            Button bakeButton = new Button(BeginBake) { text = "BAKE DAMAGE STATES" };
            Button scanButton = new Button(RunScanner) { text = "SCAN RUNTIME DESTRUCTION" };
            rootVisualElement.Add(loadProfiles);
            rootVisualElement.Add(previewButton);
            rootVisualElement.Add(bakeButton);
            rootVisualElement.Add(scanButton);

            _progressBar = new ProgressBar { title = "Idle", lowValue = 0f, highValue = 1f, value = 0f };
            _statusLabel = new Label("No bake has run in this editor session.");
            rootVisualElement.Add(_progressBar);
            rootVisualElement.Add(_statusLabel);
            LoadProfiles();
        }

        private void OnDisable()
        {
            EditorApplication.update -= BakeTick;
            OfflineWreckageBlackBox.Dispose();
            OfflineWreckagePreviewStore.Dispose();
        }

        private void LoadProfiles()
        {
            _profileCache = default;
            string path = Path.Combine(ProjectRoot(), ProfileFileName);
            _profileCount = WreckageDeformationProfileCsvParser.TryLoad(path, ref _profileCache);
            if (_profileCount <= 0)
            {
                _profileCache.Set(0, BuildFallbackProfile(HashAscii("implosion_crush"), 0.65f, 14f, 0.38f, 1.15f, 1f, 0.35f));
                _profileCache.Set(1, BuildFallbackProfile(HashAscii("thermal_reactor_meltdown"), 0.8f, 22f, 0.46f, 0.75f, 1.7f, 0.18f));
                _profileCache.Set(2, BuildFallbackProfile(HashAscii("kinetic_tear"), 0.7f, 11f, 0.25f, 2.2f, 0.75f, 0.24f));
                _profileCount = 3;
            }

            List<string> choices = new List<string>(_profileCount); // COLD ALLOC: List<string>[profileCount] - editor dropdown labels - owner: WreckageForgeWindow
            for (int i = 0; i < _profileCount; i++)
                choices.Add("0x" + _profileCache.Get(i).ProfileHash.ToString("X8"));
            _profileDropdown.choices = choices;
            _profileDropdown.index = 0;
            ApplyProfileToSliders(_profileCache.Get(0));
        }

        private void ApplyProfileToSliders(in WreckageDeformationProfileDTO profile)
        {
            _qualitySlider.value = math.saturate(profile.GlobalQualityWeight);
            _blastRadiusSlider.value = math.max(0.25f, profile.BlastRadius);
            _tearThresholdSlider.value = math.saturate(profile.TearThreshold);
            _shearTorsionSlider.value = math.max(0f, profile.ShearTorsion);
            _scorchSlider.value = math.max(0f, profile.ScorchIntensity);
            _collapseSlider.value = math.saturate(profile.CollapseCompression);
        }

        private void BeginBake()
        {
            _pendingAssetPaths.Clear();
            UnityEngine.Object folderObject = _folderField.value;
            string folderPath = folderObject == null ? null : AssetDatabase.GetAssetPath(folderObject);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                _statusLabel.text = "Select a project folder containing pristine prefabs or meshes.";
                return;
            }

            string[] meshGuids = AssetDatabase.FindAssets("t:Mesh", new[] { folderPath });
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            AddGuids(meshGuids);
            AddGuids(prefabGuids);
            if (_pendingAssetPaths.Count <= 0)
            {
                _statusLabel.text = "No mesh or prefab assets found in selected folder.";
                return;
            }

            Directory.CreateDirectory(Path.Combine(ProjectRoot(), OutputFolder));
            _report = new BakeReportAccumulator();
            _batchIndex = 0;
            _batchActive = true;
            _progressBar.value = 0f;
            _progressBar.title = "Baking";
            EditorApplication.update -= BakeTick;
            EditorApplication.update += BakeTick;
        }

        private void BakeTick()
        {
            if (!_batchActive)
                return;

            if (_batchIndex >= _pendingAssetPaths.Count)
            {
                _batchActive = false;
                EditorApplication.update -= BakeTick;
                WriteReport(_report);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                _progressBar.value = 1f;
                _progressBar.title = "Bake pass ended - PENDING VERIFICATION";
                _statusLabel.text = "Baked " + _report.ProcessedMeshes + " mesh inputs. Report: Docs/Reports/WRECKAGE_BAKE_REPORT.json";
                return;
            }

            string path = _pendingAssetPaths[_batchIndex];
            Mesh mesh = LoadMeshFromAsset(path);
            if (mesh != null)
            {
                WreckageDeformationProfileDTO profile = CurrentProfile();
                profile.GlobalQualityWeight = _qualitySlider.value;
                profile.BlastRadius = _blastRadiusSlider.value;
                profile.TearThreshold = _tearThresholdSlider.value;
                profile.ShearTorsion = _shearTorsionSlider.value;
                profile.ScorchIntensity = _scorchSlider.value;
                profile.CollapseCompression = _collapseSlider.value;
                BakeMesh(mesh, path, profile, ref _report);
            }

            _batchIndex++;
            float progress = _pendingAssetPaths.Count <= 0 ? 1f : (float)_batchIndex / _pendingAssetPaths.Count;
            _progressBar.value = progress;
            _progressBar.title = "Baking " + _batchIndex + " / " + _pendingAssetPaths.Count;
            _statusLabel.text = path;
        }

        private void PreviewSelectedMesh()
        {
            Mesh mesh = _meshField.value as Mesh;
            if (mesh == null)
            {
                _statusLabel.text = "Assign a Preview Mesh first.";
                return;
            }

            WreckageDeformationProfileDTO profile = CurrentProfile();
            profile.GlobalQualityWeight = _qualitySlider.value;
            profile.BlastRadius = _blastRadiusSlider.value;
            profile.TearThreshold = _tearThresholdSlider.value;
            profile.ShearTorsion = _shearTorsionSlider.value;
            profile.ScorchIntensity = _scorchSlider.value;
            profile.CollapseCompression = _collapseSlider.value;
            OfflineWreckagePreviewStore.Dispose();
            if (!TryBuildBaseBuffers(mesh, out NativeArray<OfflineWreckageBakeVertexDTO> baseVertices, out NativeArray<int> baseIndices))
            {
                _statusLabel.text = "Preview mesh has no triangle data.";
                return;
            }

            NativeArray<float> tearWeights = default;
            NativeArray<OfflineWreckageBakeCounters64> counters = default;
            NativeArray<OfflineWreckageBakeVertexDTO> workingVertices = default;
            NativeArray<OfflineWreckageBakeVertexDTO> stateVertices = default;
            NativeArray<int> stateIndices = default;
            try
            {
                int vertexCapacity = baseVertices.Length + baseIndices.Length;
                workingVertices = new NativeArray<OfflineWreckageBakeVertexDTO>(baseVertices.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                stateVertices = new NativeArray<OfflineWreckageBakeVertexDTO>(vertexCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                stateIndices = new NativeArray<int>(baseIndices.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                tearWeights = new NativeArray<float>(baseVertices.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                counters = new NativeArray<OfflineWreckageBakeCounters64>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                float3 localBlast = ResolveLocalBlast();
                BakeStateToBuffers(baseVertices, baseIndices, profile, OfflineWreckageDamageState.Ruptured, localBlast, workingVertices, stateVertices, stateIndices, tearWeights, counters, out JobHandle previewHandle, out _);
                previewHandle.Complete();
                OfflineWreckageBakeCounters64 previewCounters = counters[0];
                OfflineWreckagePreviewStore.SetMesh(CreateMesh(mesh.name + "_WRECKAGE_PREVIEW", stateVertices, previewCounters.ActiveVertexCount, stateIndices));
                SceneView.RepaintAll();
                _statusLabel.text = "Preview mesh updated. Add OfflineWreckagePreviewGizmo to an editor-only scene object to view wireframe.";
            }
            finally
            {
                if (stateIndices.IsCreated)
                    stateIndices.Dispose();
                if (stateVertices.IsCreated)
                    stateVertices.Dispose();
                if (workingVertices.IsCreated)
                    workingVertices.Dispose();
                if (tearWeights.IsCreated)
                    tearWeights.Dispose();
                if (counters.IsCreated)
                    counters.Dispose();
                if (baseVertices.IsCreated)
                    baseVertices.Dispose();
                if (baseIndices.IsCreated)
                    baseIndices.Dispose();
            }
        }

        private void RunScanner()
        {
            int findings = Runtime_Destruction_Scanner.ScanAndWriteReport(ProjectRoot());
            _statusLabel.text = "Runtime destruction scan findings: " + findings + ". Report: Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";
        }

        private void BakeMesh(Mesh source, string sourcePath, WreckageDeformationProfileDTO profile, ref BakeReportAccumulator report)
        {
            if (!TryBuildBaseBuffers(source, out NativeArray<OfflineWreckageBakeVertexDTO> baseVertices, out NativeArray<int> baseIndices))
                return;

            NativeArray<float> tearWeights = default;
            NativeArray<OfflineWreckageBakeCounters64> counters = default;
            NativeArray<float3> hullPoints = default;
            NativeArray<OfflineWreckageBakeVertexDTO> workingVertices = default;
            NativeArray<OfflineWreckageBakeVertexDTO> stateVertices = default;
            NativeArray<int> stateIndices = default;
            try
            {
                int vertexCapacity = baseVertices.Length + baseIndices.Length;
                tearWeights = new NativeArray<float>(baseVertices.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                counters = new NativeArray<OfflineWreckageBakeCounters64>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                hullPoints = new NativeArray<float3>(OfflineWreckageBakeConstants.MaxCollisionHullVertices, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                workingVertices = new NativeArray<OfflineWreckageBakeVertexDTO>(baseVertices.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                stateVertices = new NativeArray<OfflineWreckageBakeVertexDTO>(vertexCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                stateIndices = new NativeArray<int>(baseIndices.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                string safeName = BuildStableSafeName(sourcePath);
                float3 localBlast = ResolveLocalBlast();
                double3 moduleAup = ResolveModuleAup();
                MeshDamageStateMappingDTO mapping = default;
                mapping.PristineMeshHash = HashAscii(sourcePath);
                mapping.StressedMeshHash = BakeStateAsset(source, safeName, baseVertices, baseIndices, profile, OfflineWreckageDamageState.Stressed, localBlast, moduleAup, workingVertices, stateVertices, stateIndices, tearWeights, counters, hullPoints, ref report);
                mapping.RupturedMeshHash = BakeStateAsset(source, safeName, baseVertices, baseIndices, profile, OfflineWreckageDamageState.Ruptured, localBlast, moduleAup, workingVertices, stateVertices, stateIndices, tearWeights, counters, hullPoints, ref report);
                mapping.CollapsedMeshHash = BakeStateAsset(source, safeName, baseVertices, baseIndices, profile, OfflineWreckageDamageState.Collapsed, localBlast, moduleAup, workingVertices, stateVertices, stateIndices, tearWeights, counters, hullPoints, ref report);
                WriteMappingBytes(safeName, mapping);
                report.ProcessedMeshes++;
                report.SourcePolygons += baseIndices.Length / 3;
            }
            finally
            {
                if (stateIndices.IsCreated)
                    stateIndices.Dispose();
                if (stateVertices.IsCreated)
                    stateVertices.Dispose();
                if (workingVertices.IsCreated)
                    workingVertices.Dispose();
                if (hullPoints.IsCreated)
                    hullPoints.Dispose();
                if (counters.IsCreated)
                    counters.Dispose();
                if (tearWeights.IsCreated)
                    tearWeights.Dispose();
                if (baseIndices.IsCreated)
                    baseIndices.Dispose();
                if (baseVertices.IsCreated)
                    baseVertices.Dispose();
            }
        }

        private uint BakeStateAsset(
            Mesh source,
            string safeName,
            NativeArray<OfflineWreckageBakeVertexDTO> baseVertices,
            NativeArray<int> baseIndices,
            WreckageDeformationProfileDTO profile,
            OfflineWreckageDamageState state,
            float3 localBlast,
            double3 moduleAup,
            NativeArray<OfflineWreckageBakeVertexDTO> workingVertices,
            NativeArray<OfflineWreckageBakeVertexDTO> stateVertices,
            NativeArray<int> stateIndices,
            NativeArray<float> tearWeights,
            NativeArray<OfflineWreckageBakeCounters64> counters,
            NativeArray<float3> hullPoints,
            ref BakeReportAccumulator report)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            BakeStateToBuffers(baseVertices, baseIndices, profile, state, localBlast, workingVertices, stateVertices, stateIndices, tearWeights, counters, out JobHandle handle, out uint warningFlags);
            GenerateConvexHullsJob hullJob = new GenerateConvexHullsJob
            {
                Vertices = stateVertices,
                Counters = counters,
                HullPoints = hullPoints
            };
            handle = hullJob.Schedule(handle);
            handle.Complete();
            stopwatch.Stop();

            string suffix = StateSuffix(state);
            string meshPath = OutputFolder + "/GEN_" + safeName + "_" + suffix + ".asset";
            OfflineWreckageBakeCounters64 stateCounters = counters[0];
            int hullCount = stateCounters.HullVertexCount;
            double burstMicroseconds = stopwatch.Elapsed.TotalMilliseconds * 1000.0;
            if (hullCount > 256)
                warningFlags |= OfflineWreckageBakeConstants.WarningHullBudgetExceeded;
            if (stateCounters.DegenerateTriangleCount > 0)
                warningFlags |= OfflineWreckageBakeConstants.WarningDegenerateTriangles;
            if (HasNonFinite(stateVertices, stateCounters.ActiveVertexCount))
                warningFlags |= OfflineWreckageBakeConstants.WarningNonFiniteFallback;

            uint meshHash = HashAscii(meshPath);
            OfflineWreckageBlackBox.Record(
                moduleAup,
                meshHash,
                (uint)state,
                stateCounters.ActiveVertexCount,
                stateIndices.Length,
                stateCounters.TornVertexCount,
                hullCount,
                burstMicroseconds,
                warningFlags);
            if ((warningFlags & OfflineWreckageBakeConstants.WarningNonFiniteFallback) != 0u)
                OfflineWreckageBlackBox.Dump(ProjectRoot());

            Mesh mesh = CreateMesh(source.name + "_" + suffix, stateVertices, stateCounters.ActiveVertexCount, stateIndices);
            PublishMeshAsset(mesh, meshPath);

            string hullPath = OutputFolder + "/GEN_" + safeName + "_" + suffix + "_COLLIDER.asset";
            Mesh hullMesh = CreateHullMesh(source.name + "_" + suffix + "_COLLIDER", hullPoints, hullCount);
            PublishMeshAsset(hullMesh, hullPath);

            report.GeneratedStates++;
            report.GeneratedPolygons += stateIndices.Length / 3;
            report.TornVertices += stateCounters.TornVertexCount;
            report.BurstMicroseconds += burstMicroseconds;
            report.MaxHullVertices = math.max(report.MaxHullVertices, hullCount);
            report.WarningFlags |= warningFlags;
            report.AppendState(meshPath, hullPath, state, stateVertices.Length, stateCounters.ActiveVertexCount, stateIndices.Length / 3, stateCounters.TornVertexCount, hullCount, burstMicroseconds, warningFlags);
            return meshHash;
        }

        private static void BakeStateToBuffers(
            NativeArray<OfflineWreckageBakeVertexDTO> baseVertices,
            NativeArray<int> baseIndices,
            WreckageDeformationProfileDTO profile,
            OfflineWreckageDamageState state,
            float3 localBlast,
            NativeArray<OfflineWreckageBakeVertexDTO> workingVertices,
            NativeArray<OfflineWreckageBakeVertexDTO> stateVertices,
            NativeArray<int> stateIndices,
            NativeArray<float> tearWeights,
            NativeArray<OfflineWreckageBakeCounters64> counters,
            out JobHandle handle,
            out uint warningFlags)
        {
            warningFlags = 0u;
            float stateScale = StateScale(state);
            CopyBaseVerticesJob copy = new CopyBaseVerticesJob
            {
                Source = baseVertices,
                Destination = workingVertices
            };
            handle = copy.Schedule(baseVertices.Length, 64);

            ApplyStructuralShearJob shear = new ApplyStructuralShearJob
            {
                Vertices = workingVertices,
                ShearAxis = math.all(math.isfinite(profile.ShearAxis)) && math.lengthsq(profile.ShearAxis) > 0.0001f ? profile.ShearAxis : new float3(0f, 1f, 0f),
                ShearTorsion = profile.ShearTorsion * stateScale,
                CollapseCompression = profile.CollapseCompression * (state == OfflineWreckageDamageState.Collapsed ? 1f : 0.25f),
                GlobalQualityWeight = profile.GlobalQualityWeight
            };
            handle = shear.Schedule(baseVertices.Length, 64, handle);

            ApplyRadialBlastJob blast = new ApplyRadialBlastJob
            {
                Vertices = workingVertices,
                TearWeights = tearWeights,
                EpicenterLocal = localBlast,
                Radius = math.max(profile.BlastRadius, 0.001f) * math.lerp(0.65f, 1.35f, stateScale),
                TearThreshold = math.saturate(profile.TearThreshold),
                DamageScale = stateScale,
                GlobalQualityWeight = profile.GlobalQualityWeight
            };
            handle = blast.Schedule(baseVertices.Length, 64, handle);

            BuildTornTrianglesJob torn = new BuildTornTrianglesJob
            {
                SourceVertices = workingVertices,
                SourceIndices = baseIndices,
                TearWeights = tearWeights,
                OutputVertices = stateVertices,
                OutputIndices = stateIndices,
                Counters = counters,
                TearThreshold = math.saturate(profile.TearThreshold) * math.lerp(1.25f, 0.75f, stateScale),
                SplitDistance = math.lerp(0.02f, 0.22f, stateScale),
                GlobalQualityWeight = profile.GlobalQualityWeight
            };
            handle = torn.Schedule(handle);

            RecalculateDeformedNormalsJob normals = new RecalculateDeformedNormalsJob
            {
                Vertices = stateVertices,
                Indices = stateIndices,
                Counters = counters
            };
            handle = normals.Schedule(handle);

            BakeDamageColorsJob colors = new BakeDamageColorsJob
            {
                Vertices = stateVertices,
                Counters = counters,
                EpicenterLocal = localBlast,
                BlastRadius = math.max(profile.BlastRadius, 0.001f),
                ScorchIntensity = profile.ScorchIntensity * stateScale,
                GlobalQualityWeight = profile.GlobalQualityWeight
            };
            handle = colors.Schedule(stateVertices.Length, 64, handle);
        }

        private static bool HasNonFinite(NativeArray<OfflineWreckageBakeVertexDTO> vertices, int vertexCount)
        {
            int count = math.clamp(vertexCount, 0, vertices.Length);
            for (int i = 0; i < count; i++)
            {
                OfflineWreckageBakeVertexDTO vertex = vertices[i];
                if (!math.all(math.isfinite(vertex.Position)) ||
                    !math.all(math.isfinite(vertex.Normal)) ||
                    !math.all(math.isfinite(vertex.Tangent)))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryBuildBaseBuffers(Mesh source, out NativeArray<OfflineWreckageBakeVertexDTO> vertices, out NativeArray<int> indices)
        {
            vertices = default;
            indices = default;
            if (source == null || source.vertexCount <= 0 || source.subMeshCount <= 0)
                return false;

            NativeArray<OfflineWreckageSubMeshIndexRangeDTO> ranges = default;
            try
            {
                using Mesh.MeshDataArray readOnly = Mesh.AcquireReadOnlyMeshData(source);
                Mesh.MeshData sourceData = readOnly[0];
                int vertexCount = sourceData.vertexCount;
                int indexCount = BuildTriangleSubMeshRanges(sourceData, out ranges, out int rangeCount);
                if (vertexCount <= 0 || indexCount <= 0 || rangeCount <= 0 || !HasFloatAttribute(sourceData, VertexAttribute.Position, 3))
                    return false;

                vertices = new NativeArray<OfflineWreckageBakeVertexDTO>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                indices = new NativeArray<int>(indexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                JobHandle handle = ScheduleExtractBaseVertices(sourceData, vertices);
                handle = ScheduleCopyIndices(sourceData, indices, ranges, rangeCount, handle);
                handle.Complete();
                return true;
            }
            catch
            {
                if (indices.IsCreated)
                    indices.Dispose();
                if (vertices.IsCreated)
                    vertices.Dispose();
                vertices = default;
                indices = default;
                return false;
            }
            finally
            {
                if (ranges.IsCreated)
                    ranges.Dispose();
            }
        }

        private static JobHandle ScheduleExtractBaseVertices(Mesh.MeshData sourceData, NativeArray<OfflineWreckageBakeVertexDTO> vertices)
        {
            int positionStream = sourceData.GetVertexAttributeStream(VertexAttribute.Position);
            int normalStream = sourceData.HasVertexAttribute(VertexAttribute.Normal) ? sourceData.GetVertexAttributeStream(VertexAttribute.Normal) : -1;
            int tangentStream = sourceData.HasVertexAttribute(VertexAttribute.Tangent) ? sourceData.GetVertexAttributeStream(VertexAttribute.Tangent) : -1;
            int uvStream = sourceData.HasVertexAttribute(VertexAttribute.TexCoord0) ? sourceData.GetVertexAttributeStream(VertexAttribute.TexCoord0) : -1;
            int colorStream = sourceData.HasVertexAttribute(VertexAttribute.Color) ? sourceData.GetVertexAttributeStream(VertexAttribute.Color) : -1;
            bool hasNormal = normalStream >= 0 && HasFloatAttribute(sourceData, VertexAttribute.Normal, 3);
            bool hasTangent = tangentStream >= 0 && HasFloatAttribute(sourceData, VertexAttribute.Tangent, 4);
            bool hasUv = uvStream >= 0 && HasFloatAttribute(sourceData, VertexAttribute.TexCoord0, 2);
            bool hasColor = colorStream >= 0 &&
                            sourceData.GetVertexAttributeFormat(VertexAttribute.Color) == VertexAttributeFormat.UNorm8 &&
                            sourceData.GetVertexAttributeDimension(VertexAttribute.Color) >= 4;

            ExtractBaseVerticesJob job = new ExtractBaseVerticesJob
            {
                PositionBytes = sourceData.GetVertexData<byte>(positionStream),
                NormalBytes = hasNormal ? sourceData.GetVertexData<byte>(normalStream) : default,
                TangentBytes = hasTangent ? sourceData.GetVertexData<byte>(tangentStream) : default,
                UvBytes = hasUv ? sourceData.GetVertexData<byte>(uvStream) : default,
                ColorBytes = hasColor ? sourceData.GetVertexData<byte>(colorStream) : default,
                Output = vertices,
                PositionOffset = sourceData.GetVertexAttributeOffset(VertexAttribute.Position),
                PositionStride = sourceData.GetVertexBufferStride(positionStream),
                NormalOffset = hasNormal ? sourceData.GetVertexAttributeOffset(VertexAttribute.Normal) : 0,
                NormalStride = hasNormal ? sourceData.GetVertexBufferStride(normalStream) : 0,
                TangentOffset = hasTangent ? sourceData.GetVertexAttributeOffset(VertexAttribute.Tangent) : 0,
                TangentStride = hasTangent ? sourceData.GetVertexBufferStride(tangentStream) : 0,
                UvOffset = hasUv ? sourceData.GetVertexAttributeOffset(VertexAttribute.TexCoord0) : 0,
                UvStride = hasUv ? sourceData.GetVertexBufferStride(uvStream) : 0,
                ColorOffset = hasColor ? sourceData.GetVertexAttributeOffset(VertexAttribute.Color) : 0,
                ColorStride = hasColor ? sourceData.GetVertexBufferStride(colorStream) : 0,
                HasNormal = hasNormal ? 1 : 0,
                HasTangent = hasTangent ? 1 : 0,
                HasUv = hasUv ? 1 : 0,
                HasColor = hasColor ? 1 : 0
            };
            return job.Schedule(vertices.Length, 64);
        }

        private static JobHandle ScheduleCopyIndices(
            Mesh.MeshData sourceData,
            NativeArray<int> indices,
            NativeArray<OfflineWreckageSubMeshIndexRangeDTO> ranges,
            int rangeCount,
            JobHandle dependency)
        {
            if (sourceData.indexFormat == IndexFormat.UInt16)
            {
                CopyIndex16RangesJob job = new CopyIndex16RangesJob
                {
                    Source = sourceData.GetIndexData<ushort>(),
                    Ranges = ranges,
                    Output = indices
                };
                return job.Schedule(rangeCount, 1, dependency);
            }

            CopyIndex32RangesJob copy32 = new CopyIndex32RangesJob
            {
                Source = sourceData.GetIndexData<uint>(),
                Ranges = ranges,
                Output = indices
            };
            return copy32.Schedule(rangeCount, 1, dependency);
        }

        private static int BuildTriangleSubMeshRanges(
            Mesh.MeshData sourceData,
            out NativeArray<OfflineWreckageSubMeshIndexRangeDTO> ranges,
            out int rangeCount)
        {
            ranges = default;
            rangeCount = 0;
            int subMeshCount = sourceData.subMeshCount;
            if (subMeshCount <= 0)
                return 0;

            int sourceIndexCapacity = sourceData.indexFormat == IndexFormat.UInt16
                ? sourceData.GetIndexData<ushort>().Length
                : sourceData.GetIndexData<uint>().Length;
            if (sourceIndexCapacity <= 0)
                return 0;

            int totalIndexCount = 0;
            int tileCapacity = 0;
            for (int i = 0; i < subMeshCount; i++)
            {
                SubMeshDescriptor descriptor = sourceData.GetSubMesh(i);
                if (descriptor.topology != MeshTopology.Triangles)
                    continue;

                int sourceStart = math.clamp(descriptor.indexStart, 0, sourceIndexCapacity);
                int available = math.max(sourceIndexCapacity - sourceStart, 0);
                int indexCount = math.min(descriptor.indexCount, available);
                indexCount -= indexCount % 3;
                if (indexCount <= 0)
                    continue;

                totalIndexCount += indexCount;
                tileCapacity += (indexCount + IndexCopyTileSize - 1) / IndexCopyTileSize;
            }

            if (tileCapacity <= 0 || totalIndexCount <= 0)
                return 0;

            ranges = new NativeArray<OfflineWreckageSubMeshIndexRangeDTO>(tileCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            int destinationStart = 0;
            for (int i = 0; i < subMeshCount; i++)
            {
                SubMeshDescriptor descriptor = sourceData.GetSubMesh(i);
                if (descriptor.topology != MeshTopology.Triangles)
                    continue;

                int sourceStart = math.clamp(descriptor.indexStart, 0, sourceIndexCapacity);
                int available = math.max(sourceIndexCapacity - sourceStart, 0);
                int remaining = math.min(descriptor.indexCount, available);
                remaining -= remaining % 3;
                if (remaining <= 0)
                    continue;

                while (remaining > 0)
                {
                    int chunk = math.min(remaining, IndexCopyTileSize);
                    ranges[rangeCount] = new OfflineWreckageSubMeshIndexRangeDTO
                    {
                        SourceIndexStart = sourceStart,
                        IndexCount = chunk,
                        DestinationIndexStart = destinationStart,
                        BaseVertex = descriptor.baseVertex
                    };
                    sourceStart += chunk;
                    destinationStart += chunk;
                    remaining -= chunk;
                    rangeCount++;
                }
            }

            return destinationStart;
        }

        private static bool HasFloatAttribute(Mesh.MeshData sourceData, VertexAttribute attribute, int minDimension)
        {
            return sourceData.HasVertexAttribute(attribute) &&
                   sourceData.GetVertexAttributeFormat(attribute) == VertexAttributeFormat.Float32 &&
                   sourceData.GetVertexAttributeDimension(attribute) >= minDimension;
        }

        private static Mesh CreateMesh(string name, NativeArray<OfflineWreckageBakeVertexDTO> vertices, int vertexCount, NativeArray<int> indices)
        {
            int safeVertexCount = math.clamp(vertexCount, 0, vertices.Length);
            Mesh mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontRecalculateNormals | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers;
            Bounds bounds = ComputeBounds(vertices, safeVertexCount);
            mesh.SetVertexBufferParams(safeVertexCount, s_vertexLayout);
            mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
            mesh.SetVertexBufferData(vertices, 0, 0, safeVertexCount, 0, flags);
            mesh.SetIndexBufferData(indices, 0, 0, indices.Length, flags);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length, MeshTopology.Triangles)
            {
                bounds = bounds,
                vertexCount = safeVertexCount
            }, flags);
            mesh.bounds = bounds;
            mesh.UploadMeshData(false);
            return mesh;
        }

        private static Mesh CreateHullMesh(string name, NativeArray<float3> hullPoints, int hullCount)
        {
            Mesh mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt16 };
            const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers;
            NativeArray<float3> fallbackVertices = default;
            NativeArray<ushort> indices = default;
            if (hullCount < 8)
            {
                try
                {
                    fallbackVertices = new NativeArray<float3>(3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    indices = new NativeArray<ushort>(3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    fallbackVertices[0] = float3.zero;
                    fallbackVertices[1] = new float3(1f, 0f, 0f);
                    fallbackVertices[2] = new float3(0f, 1f, 0f);
                    indices[0] = 0;
                    indices[1] = 1;
                    indices[2] = 2;
                    mesh.SetVertexBufferParams(3, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3));
                    mesh.SetIndexBufferParams(3, IndexFormat.UInt16);
                    mesh.SetVertexBufferData(fallbackVertices, 0, 0, 3, 0, flags);
                    mesh.SetIndexBufferData(indices, 0, 0, 3, flags);
                    mesh.subMeshCount = 1;
                    mesh.SetSubMesh(0, new SubMeshDescriptor(0, 3, MeshTopology.Triangles)
                    {
                        bounds = new Bounds(new Vector3(0.5f, 0.5f, 0f), Vector3.one),
                        vertexCount = 3
                    }, flags);
                    mesh.bounds = new Bounds(new Vector3(0.5f, 0.5f, 0f), Vector3.one);
                    return mesh;
                }
                finally
                {
                    if (indices.IsCreated)
                        indices.Dispose();
                    if (fallbackVertices.IsCreated)
                        fallbackVertices.Dispose();
                }
            }

            try
            {
                indices = new NativeArray<ushort>(36, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                FillHullIndexPattern(indices);

                Bounds bounds = ComputeFloat3Bounds(hullPoints, 8);
                mesh.SetVertexBufferParams(8, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3));
                mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt16);
                mesh.SetVertexBufferData(hullPoints, 0, 0, 8, 0, flags);
                mesh.SetIndexBufferData(indices, 0, 0, indices.Length, flags);
                mesh.subMeshCount = 1;
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length, MeshTopology.Triangles)
                {
                    bounds = bounds,
                    vertexCount = 8
                }, flags);
                mesh.bounds = bounds;
                return mesh;
            }
            finally
            {
                if (indices.IsCreated)
                    indices.Dispose();
            }
        }

        private static void FillHullIndexPattern(NativeArray<ushort> indices)
        {
            if (!indices.IsCreated || indices.Length < 36)
                return;

            indices[0] = 0; indices[1] = 2; indices[2] = 1; indices[3] = 0; indices[4] = 3; indices[5] = 2;
            indices[6] = 4; indices[7] = 5; indices[8] = 6; indices[9] = 4; indices[10] = 6; indices[11] = 7;
            indices[12] = 0; indices[13] = 1; indices[14] = 5; indices[15] = 0; indices[16] = 5; indices[17] = 4;
            indices[18] = 1; indices[19] = 2; indices[20] = 6; indices[21] = 1; indices[22] = 6; indices[23] = 5;
            indices[24] = 2; indices[25] = 3; indices[26] = 7; indices[27] = 2; indices[28] = 7; indices[29] = 6;
            indices[30] = 3; indices[31] = 0; indices[32] = 4; indices[33] = 3; indices[34] = 4; indices[35] = 7;
        }

        private static Bounds ComputeBounds(NativeArray<OfflineWreckageBakeVertexDTO> vertices, int vertexCount)
        {
            if (vertexCount <= 0)
                return new Bounds(Vector3.zero, Vector3.one);

            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);
            for (int i = 0; i < vertexCount; i++)
            {
                float3 p = vertices[i].Position;
                if (!math.all(math.isfinite(p)))
                    continue;

                min = math.min(min, p);
                max = math.max(max, p);
            }

            if (!math.all(math.isfinite(min)) || !math.all(math.isfinite(max)) || math.any(max <= min))
                return new Bounds(Vector3.zero, Vector3.one);

            float3 center = (min + max) * 0.5f;
            float3 size = math.max(max - min, new float3(0.01f));
            return new Bounds(new Vector3(center.x, center.y, center.z), new Vector3(size.x, size.y, size.z));
        }

        private static Bounds ComputeFloat3Bounds(NativeArray<float3> vertices, int vertexCount)
        {
            int count = math.clamp(vertexCount, 0, vertices.Length);
            if (count <= 0)
                return new Bounds(Vector3.zero, Vector3.one);

            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);
            for (int i = 0; i < count; i++)
            {
                float3 p = vertices[i];
                if (!math.all(math.isfinite(p)))
                    continue;

                min = math.min(min, p);
                max = math.max(max, p);
            }

            if (!math.all(math.isfinite(min)) || !math.all(math.isfinite(max)) || math.any(max <= min))
                return new Bounds(Vector3.zero, Vector3.one);

            float3 center = (min + max) * 0.5f;
            float3 size = math.max(max - min, new float3(0.01f));
            return new Bounds(new Vector3(center.x, center.y, center.z), new Vector3(size.x, size.y, size.z));
        }

        private void AddGuids(string[] guids)
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(path) && !_pendingAssetPaths.Contains(path))
                    _pendingAssetPaths.Add(path);
            }
        }

        private static Mesh LoadMeshFromAsset(string path)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
                return mesh;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                return null;

            MeshFilter filter = prefab.GetComponentInChildren<MeshFilter>(true);
            return filter == null ? null : filter.sharedMesh;
        }

        private WreckageDeformationProfileDTO CurrentProfile()
        {
            int index = math.clamp(_profileDropdown == null ? 0 : _profileDropdown.index, 0, math.max(_profileCount - 1, 0));
            return _profileCount > 0 ? _profileCache.Get(index) : BuildFallbackProfile(HashAscii("fallback"), 0.65f, 14f, 0.38f, 1.15f, 1f, 0.35f);
        }

        private float3 ResolveLocalBlast()
        {
            return OfflineWreckageBakeMath.LocalizeBlastEpicenter(
                new double3(
                    _blastAupXField == null ? 0d : _blastAupXField.value,
                    _blastAupYField == null ? 0d : _blastAupYField.value,
                    _blastAupZField == null ? 0d : _blastAupZField.value),
                new double3(
                    _moduleAupXField == null ? 0d : _moduleAupXField.value,
                    _moduleAupYField == null ? 0d : _moduleAupYField.value,
                    _moduleAupZField == null ? 0d : _moduleAupZField.value));
        }

        private double3 ResolveModuleAup()
        {
            return new double3(
                _moduleAupXField == null ? 0d : _moduleAupXField.value,
                _moduleAupYField == null ? 0d : _moduleAupYField.value,
                _moduleAupZField == null ? 0d : _moduleAupZField.value);
        }

        private static WreckageDeformationProfileDTO BuildFallbackProfile(uint hash, float q, float radius, float tear, float shear, float scorch, float collapse)
        {
            WreckageDeformationProfileDTO profile = default;
            profile.ProfileHash = hash;
            profile.GlobalQualityWeight = q;
            profile.BlastRadius = radius;
            profile.TearThreshold = tear;
            profile.ShearTorsion = shear;
            profile.ScorchIntensity = scorch;
            profile.CollapseCompression = collapse;
            profile.NoiseAmplitude = 0.12f;
            profile.ShearAxis = new float3(0f, 1f, 0f);
            return profile;
        }

        private static float StateScale(OfflineWreckageDamageState state)
        {
            if (state == OfflineWreckageDamageState.Stressed)
                return 0.32f;
            if (state == OfflineWreckageDamageState.Ruptured)
                return 0.68f;
            return 1f;
        }

        private static string StateSuffix(OfflineWreckageDamageState state)
        {
            if (state == OfflineWreckageDamageState.Stressed)
                return "STRESSED";
            if (state == OfflineWreckageDamageState.Ruptured)
                return "RUPTURED";
            return "COLLAPSED";
        }

        private static string SanitizeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder builder = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool bad = false;
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (c == invalid[j])
                    {
                        bad = true;
                        break;
                    }
                }

                builder.Append(bad ? '_' : c);
            }

            return builder.Length == 0 ? "Wreckage" : builder.ToString();
        }

        private static string BuildStableSafeName(string sourcePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(sourcePath);
            string safeName = SanitizeFileName(string.IsNullOrEmpty(fileName) ? "Wreckage" : fileName);
            return safeName + "_" + HashAscii(sourcePath).ToString("X8", CultureInfo.InvariantCulture);
        }

        private static uint HashAscii(string text)
        {
            uint hash = 2166136261u;
            if (string.IsNullOrEmpty(text))
                return hash;

            for (int i = 0; i < text.Length; i++)
                hash = OfflineWreckageBakeMath.HashBytes((byte)text[i], hash);
            return OfflineWreckageBakeMath.Hash(hash);
        }

        private static string ProjectRoot()
        {
            return Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length);
        }

        private static void WriteMappingBytes(string safeName, in MeshDamageStateMappingDTO mapping)
        {
            string assetPath = OutputFolder + "/GEN_" + safeName + "_DamageStateMap.bytes";
            string fullPath = Path.Combine(ProjectRoot(), assetPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            Span<byte> bytes = stackalloc byte[32];
            bytes.Clear();
            WriteUInt32(bytes, 0, mapping.PristineMeshHash);
            WriteUInt32(bytes, 4, mapping.StressedMeshHash);
            WriteUInt32(bytes, 8, mapping.RupturedMeshHash);
            WriteUInt32(bytes, 12, mapping.CollapsedMeshHash);
            OfflineWreckageAtomicFile.WriteBytes(fullPath, bytes);
            AssetDatabase.ImportAsset(assetPath);
        }

        private static void PublishMeshAsset(Mesh generated, string assetPath)
        {
            string fullPath = Path.Combine(ProjectRoot(), assetPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, assetPath);
                return;
            }

            EditorUtility.CopySerialized(generated, existing);
            existing.name = generated.name;
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(generated);
        }

        private static void WriteUInt32(Span<byte> bytes, int offset, uint value)
        {
            bytes[offset] = (byte)(value & 0xFFu);
            bytes[offset + 1] = (byte)((value >> 8) & 0xFFu);
            bytes[offset + 2] = (byte)((value >> 16) & 0xFFu);
            bytes[offset + 3] = (byte)((value >> 24) & 0xFFu);
        }

        private static void WriteReport(BakeReportAccumulator report)
        {
            string reportPath = Path.Combine(ProjectRoot(), "Docs", "Reports", "WRECKAGE_BAKE_REPORT.json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            StringBuilder json = new StringBuilder(16384);
            json.Append("{\n");
            json.Append("  \"agent\": \"SHINOBU_209\",\n");
            json.Append("  \"status\": \"PENDING_VERIFICATION\",\n");
            json.Append("  \"version\": ").Append(OfflineWreckageBakeConstants.BakeReportVersion).Append(",\n");
            json.Append("  \"processedMeshes\": ").Append(report.ProcessedMeshes).Append(",\n");
            json.Append("  \"generatedStates\": ").Append(report.GeneratedStates).Append(",\n");
            json.Append("  \"sourcePolygons\": ").Append(report.SourcePolygons).Append(",\n");
            json.Append("  \"generatedPolygons\": ").Append(report.GeneratedPolygons).Append(",\n");
            json.Append("  \"tornVertices\": ").Append(report.TornVertices).Append(",\n");
            json.Append("  \"maxHullVertices\": ").Append(report.MaxHullVertices).Append(",\n");
            json.Append("  \"burstMicroseconds\": ").Append(report.BurstMicroseconds.ToString("0.000", CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"warningFlags\": ").Append(report.WarningFlags).Append(",\n");
            json.Append("  \"criticalWarning\": \"").Append((report.WarningFlags & OfflineWreckageBakeConstants.WarningHullBudgetExceeded) != 0u ? "CRITICAL_WARNING" : "NONE").Append("\",\n");
            json.Append("  \"states\": [\n");
            json.Append(report.StateRows);
            json.Append("\n  ]\n");
            json.Append("}\n");
            WriteTextAtomic(reportPath, json.ToString());
        }

        private static void WriteTextAtomic(string path, string text)
        {
            OfflineWreckageAtomicFile.WriteTextUtf8(path, text);
        }

        private sealed class BakeReportAccumulator
        {
            public int ProcessedMeshes;
            public int GeneratedStates;
            public int SourcePolygons;
            public int GeneratedPolygons;
            public int TornVertices;
            public int MaxHullVertices;
            public double BurstMicroseconds;
            public uint WarningFlags;
            public readonly StringBuilder StateRows = new StringBuilder(8192);

            public void AppendState(string meshPath, string hullPath, OfflineWreckageDamageState state, int capacityVertices, int activeVertices, int polygons, int tornVertices, int hullVertices, double micros, uint warnings)
            {
                if (StateRows.Length > 0)
                    StateRows.Append(",\n");

                StateRows.Append("    { \"mesh\": \"");
                AppendEscaped(StateRows, meshPath);
                StateRows.Append("\", \"collisionHull\": \"");
                AppendEscaped(StateRows, hullPath);
                StateRows.Append("\", \"state\": \"").Append(state).Append("\", \"capacityVertices\": ").Append(capacityVertices);
                StateRows.Append(", \"activeVertices\": ").Append(activeVertices);
                StateRows.Append(", \"polygons\": ").Append(polygons);
                StateRows.Append(", \"tornVertices\": ").Append(tornVertices);
                StateRows.Append(", \"hullVertices\": ").Append(hullVertices);
                StateRows.Append(", \"burstMicroseconds\": ").Append(micros.ToString("0.000", CultureInfo.InvariantCulture));
                StateRows.Append(", \"warnings\": ").Append(warnings);
                StateRows.Append(", \"severity\": \"");
                StateRows.Append((warnings & OfflineWreckageBakeConstants.WarningHullBudgetExceeded) != 0u ? "CRITICAL_WARNING" : warnings != 0u ? "WARNING" : "OK");
                StateRows.Append("\" }");
            }
        }

        private static void AppendEscaped(StringBuilder builder, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' || c == '"')
                    builder.Append('\\');
                builder.Append(c);
            }
        }
    }

    internal struct WreckageProfileCache
    {
        public WreckageDeformationProfileDTO Profile00;
        public WreckageDeformationProfileDTO Profile01;
        public WreckageDeformationProfileDTO Profile02;
        public WreckageDeformationProfileDTO Profile03;
        public WreckageDeformationProfileDTO Profile04;
        public WreckageDeformationProfileDTO Profile05;
        public WreckageDeformationProfileDTO Profile06;
        public WreckageDeformationProfileDTO Profile07;
        public WreckageDeformationProfileDTO Profile08;
        public WreckageDeformationProfileDTO Profile09;
        public WreckageDeformationProfileDTO Profile10;
        public WreckageDeformationProfileDTO Profile11;
        public WreckageDeformationProfileDTO Profile12;
        public WreckageDeformationProfileDTO Profile13;
        public WreckageDeformationProfileDTO Profile14;
        public WreckageDeformationProfileDTO Profile15;

        public WreckageDeformationProfileDTO Get(int index)
        {
            switch (math.clamp(index, 0, WreckageForgeWindow.ProfileCapacity - 1))
            {
                case 0: return Profile00;
                case 1: return Profile01;
                case 2: return Profile02;
                case 3: return Profile03;
                case 4: return Profile04;
                case 5: return Profile05;
                case 6: return Profile06;
                case 7: return Profile07;
                case 8: return Profile08;
                case 9: return Profile09;
                case 10: return Profile10;
                case 11: return Profile11;
                case 12: return Profile12;
                case 13: return Profile13;
                case 14: return Profile14;
                default: return Profile15;
            }
        }

        public void Set(int index, in WreckageDeformationProfileDTO profile)
        {
            switch (math.clamp(index, 0, WreckageForgeWindow.ProfileCapacity - 1))
            {
                case 0: Profile00 = profile; break;
                case 1: Profile01 = profile; break;
                case 2: Profile02 = profile; break;
                case 3: Profile03 = profile; break;
                case 4: Profile04 = profile; break;
                case 5: Profile05 = profile; break;
                case 6: Profile06 = profile; break;
                case 7: Profile07 = profile; break;
                case 8: Profile08 = profile; break;
                case 9: Profile09 = profile; break;
                case 10: Profile10 = profile; break;
                case 11: Profile11 = profile; break;
                case 12: Profile12 = profile; break;
                case 13: Profile13 = profile; break;
                case 14: Profile14 = profile; break;
                default: Profile15 = profile; break;
            }
        }
    }

    internal static class WreckageDeformationProfileCsvParser
    {
        private const int MaxProfileCsvBytes = 32768;

        public static int TryLoad(string path, ref WreckageProfileCache profiles)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return 0;

            FileInfo info = new FileInfo(path);
            if (info.Length <= 0 || info.Length > MaxProfileCsvBytes)
                return 0;

            int byteCount = (int)info.Length;
            Span<byte> writableBytes = stackalloc byte[byteCount];
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int read = 0;
                while (read < byteCount)
                {
                    int chunk = stream.Read(writableBytes.Slice(read));
                    if (chunk <= 0)
                        break;
                    read += chunk;
                }

                if (read != byteCount)
                    return 0;
            }

            ReadOnlySpan<byte> bytes = writableBytes;
            int index = 0;
            int count = 0;
            SkipLine(bytes, ref index);
            while (index < bytes.Length && count < WreckageForgeWindow.ProfileCapacity)
            {
                WreckageDeformationProfileDTO profile = default;
                profile.ProfileHash = ReadKeyHash(bytes, ref index);
                if (profile.ProfileHash == 2166136261u)
                {
                    SkipLine(bytes, ref index);
                    continue;
                }

                ConsumeComma(bytes, ref index);
                TryReadFloat(bytes, ref index, out profile.GlobalQualityWeight);
                ConsumeComma(bytes, ref index);
                TryReadFloat(bytes, ref index, out profile.BlastRadius);
                ConsumeComma(bytes, ref index);
                TryReadFloat(bytes, ref index, out profile.TearThreshold);
                ConsumeComma(bytes, ref index);
                TryReadFloat(bytes, ref index, out profile.ShearTorsion);
                ConsumeComma(bytes, ref index);
                TryReadFloat(bytes, ref index, out profile.ScorchIntensity);
                ConsumeComma(bytes, ref index);
                TryReadFloat(bytes, ref index, out profile.CollapseCompression);
                ConsumeComma(bytes, ref index);
                TryReadFloat(bytes, ref index, out profile.NoiseAmplitude);
                profile.GlobalQualityWeight = math.saturate(profile.GlobalQualityWeight);
                profile.BlastRadius = math.max(profile.BlastRadius, 0.25f);
                profile.TearThreshold = math.saturate(profile.TearThreshold);
                profile.ShearTorsion = math.max(profile.ShearTorsion, 0f);
                profile.ScorchIntensity = math.max(profile.ScorchIntensity, 0f);
                profile.CollapseCompression = math.saturate(profile.CollapseCompression);
                profile.ShearAxis = new float3(0f, 1f, 0f);
                profiles.Set(count++, profile);
                SkipLine(bytes, ref index);
            }

            return count;
        }

        private static uint ReadKeyHash(ReadOnlySpan<byte> bytes, ref int index)
        {
            uint hash = 2166136261u;
            SkipValueWhitespace(bytes, ref index);
            while (index < bytes.Length)
            {
                byte c = bytes[index];
                if (c == (byte)',' || c == (byte)'\n' || c == (byte)'\r')
                    break;

                hash = OfflineWreckageBakeMath.HashBytes(c, hash);
                index++;
            }

            return hash;
        }

        private static bool TryReadFloat(ReadOnlySpan<byte> bytes, ref int index, out float value)
        {
            value = 0f;
            SkipValueWhitespace(bytes, ref index);
            if (index >= bytes.Length)
                return false;

            float sign = 1f;
            if (bytes[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (bytes[index] == (byte)'+')
            {
                index++;
            }

            bool readAny = false;
            float integer = 0f;
            while (index < bytes.Length)
            {
                byte c = bytes[index];
                if (c < (byte)'0' || c > (byte)'9')
                    break;

                integer = (integer * 10f) + (c - (byte)'0');
                readAny = true;
                index++;
            }

            float fraction = 0f;
            if (index < bytes.Length && bytes[index] == (byte)'.')
            {
                index++;
                float place = 0.1f;
                while (index < bytes.Length)
                {
                    byte c = bytes[index];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;

                    fraction += (c - (byte)'0') * place;
                    place *= 0.1f;
                    readAny = true;
                    index++;
                }
            }

            value = (integer + fraction) * sign;
            return readAny && math.isfinite(value);
        }

        private static void ConsumeComma(ReadOnlySpan<byte> bytes, ref int index)
        {
            SkipValueWhitespace(bytes, ref index);
            if (index < bytes.Length && bytes[index] == (byte)',')
                index++;
        }

        private static void SkipValueWhitespace(ReadOnlySpan<byte> bytes, ref int index)
        {
            while (index < bytes.Length)
            {
                byte c = bytes[index];
                if (c != (byte)' ' && c != (byte)'\t')
                    break;

                index++;
            }
        }

        private static void SkipLine(ReadOnlySpan<byte> bytes, ref int index)
        {
            while (index < bytes.Length && bytes[index] != (byte)'\n')
                index++;
            if (index < bytes.Length)
                index++;
        }
    }
}
