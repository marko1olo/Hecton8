using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
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
        private const string PrefabOutputFolder = "Assets/Prefabs/Environment/Wrecks";
        private const string ProfileFileName = "wreckage_deformation_profiles.csv";
        internal const int ProfileCapacity = 16;
        private const int IndexCopyTileSize = 384;
        private const float MinTriangleArea = 0.0001f;
        private const float MinNormalLengthSq = 0.64f;
        private const float MaxNormalLengthSq = 1.44f;
        private const string EquipmentMetadataTypeName = "Hecton8.Interaction.EquipmentMetadata, Assembly-CSharp";
        private const string InteractionAnchorDataTypeName = "Hecton8.Interaction.InteractionAnchorData, Assembly-CSharp";
        private const string EquipmentMetadataFullName = "Hecton8.Interaction.EquipmentMetadata";
        private const string InteractionAnchorDataFullName = "Hecton8.Interaction.InteractionAnchorData";
        private const string WreckIndirectShaderName = "Hecton8/World/WreckIndirectLit";
        private const string DefaultWreckMaterialFolder = "Assets/_Project/Art/Materials/WorldProceduralProxy";
        private const string DefaultWreckMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_HectonWreckIndirect_Default.mat";
        private const int MaxWreckMaterialSlots = 2;
        private const uint AnchorFlagActive = 1u << 0;
        private const uint AnchorFlagTwoHanded = 1u << 1;
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
        private BakeStatsAccumulator _bakeStats;

        [MenuItem("HECTON-8/Wreckage Forge/Open Forge")]
        public static void Open()
        {
            GetWindow<WreckageForgeWindow>("Wreckage Forge");
        }

        [MenuItem("HECTON-8/Wreckage Forge/Bake Selected Assets")]
        public static void BakeSelectedAssets()
        {
            if (!HasValidSelectedBakeSources())
            {
                UnityEngine.Debug.LogError("[WRECKAGE_FORGE] Select at least one pristine mesh, prefab, or source folder outside generated wreckage outputs before baking.");
                return;
            }

            WreckageForgeWindow window = GetWindow<WreckageForgeWindow>("Wreckage Forge");
            EditorApplication.delayCall -= window.BeginBakeSelected;
            EditorApplication.delayCall += window.BeginBakeSelected;
        }

        [MenuItem("HECTON-8/Wreckage Forge/Validate Selected Source Assets")]
        public static void ValidateSelectedSourceAssets()
        {
            if (HasValidSelectedBakeSources())
                UnityEngine.Debug.Log("[WRECKAGE_FORGE] Selected source asset gate passed.");
            else
                UnityEngine.Debug.LogError("[WRECKAGE_FORGE] Select at least one pristine mesh, prefab, or source folder outside generated wreckage outputs.");
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
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
            Button bakeSelectionButton = new Button(BeginBakeSelected) { text = "BAKE SELECTED ASSETS" };
            Button scanButton = new Button(RunScanner) { text = "SCAN RUNTIME DESTRUCTION" };
            rootVisualElement.Add(loadProfiles);
            rootVisualElement.Add(previewButton);
            rootVisualElement.Add(bakeButton);
            rootVisualElement.Add(bakeSelectionButton);
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
                choices.Add("0x" + _profileCache.Get(i).ProfileHash.ToString("X8", CultureInfo.InvariantCulture));
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
            EnsureUiReady();
            if (RejectActiveBake())
                return;

            _pendingAssetPaths.Clear();
            UnityEngine.Object folderObject = _folderField.value;
            string folderPath = folderObject == null ? null : AssetDatabase.GetAssetPath(folderObject);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                SetStatus("Select a project folder containing pristine prefabs or meshes.");
                return;
            }

            string[] meshGuids = AssetDatabase.FindAssets("t:Mesh", new[] { folderPath });
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            AddGuids(meshGuids);
            AddGuids(prefabGuids);
            StartQueuedBake("No mesh or prefab assets found in selected folder.");
        }

        private void BeginBakeSelected()
        {
            EnsureUiReady();
            if (RejectActiveBake())
                return;

            _pendingAssetPaths.Clear();
            UnityEngine.Object[] selectedObjects = Selection.objects;
            for (int i = 0; i < selectedObjects.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(selectedObjects[i]);
                if (string.IsNullOrEmpty(path))
                    continue;

                if (AssetDatabase.IsValidFolder(path))
                {
                    string[] meshGuids = AssetDatabase.FindAssets("t:Mesh", new[] { path });
                    string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { path });
                    AddGuids(meshGuids);
                    AddGuids(prefabGuids);
                    continue;
                }

                QueueAssetPath(path);
            }

            StartQueuedBake("Select pristine mesh, prefab, or source folder assets before running the 1717 bake.");
        }

        private void StartQueuedBake(string emptyStatus)
        {
            if (_pendingAssetPaths.Count <= 0)
            {
                SetStatus(emptyStatus);
                return;
            }

            Directory.CreateDirectory(Path.Combine(ProjectRoot(), OutputFolder));
            Directory.CreateDirectory(Path.Combine(ProjectRoot(), PrefabOutputFolder));
            _bakeStats = new BakeStatsAccumulator();
            _batchIndex = 0;
            _batchActive = true;
            _progressBar.value = 0f;
            _progressBar.title = "Baking";
            EditorApplication.update -= BakeTick;
            EditorApplication.update += BakeTick;
        }

        private void EnsureUiReady()
        {
            if (_statusLabel != null &&
                _progressBar != null &&
                _profileDropdown != null &&
                _qualitySlider != null)
            {
                return;
            }

            CreateGUI();
        }

        private bool RejectActiveBake()
        {
            if (!_batchActive)
                return false;

            SetStatus("Bake is already running; current queue is preserved.");
            return true;
        }

        private void SetStatus(string message)
        {
            if (_statusLabel != null)
                _statusLabel.text = message;
            else
                UnityEngine.Debug.LogWarning(message);
        }

        private void BakeTick()
        {
            if (!_batchActive)
                return;

            if (_batchIndex >= _pendingAssetPaths.Count)
            {
                _batchActive = false;
                EditorApplication.update -= BakeTick;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                _progressBar.value = 1f;
                _progressBar.title = "Bake pass ended";
                _statusLabel.text = "Baked " + _bakeStats.ProcessedMeshes +
                                    " mesh inputs. States=" + _bakeStats.GeneratedStates +
                                    " tornVerts=" + _bakeStats.TornVertices +
                                    " csgHoles=" + _bakeStats.FractureHoleTriangles +
                                    " maxHullVerts=" + _bakeStats.MaxHullVertices + ".";
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
                BakeMesh(mesh, path, profile, ref _bakeStats);
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
                int extraVertexCapacity = ResolveAdditionalFractureVertexCapacity(baseIndices.Length, profile.GlobalQualityWeight);
                int extraIndexCapacity = ResolveAdditionalFractureIndexCapacity(baseIndices.Length, profile.GlobalQualityWeight);
                int vertexCapacity = baseVertices.Length + baseIndices.Length + extraVertexCapacity;
                int indexCapacity = baseIndices.Length + extraIndexCapacity;
                workingVertices = new NativeArray<OfflineWreckageBakeVertexDTO>(baseVertices.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                stateVertices = new NativeArray<OfflineWreckageBakeVertexDTO>(vertexCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                stateIndices = new NativeArray<int>(indexCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                tearWeights = new NativeArray<float>(baseVertices.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                counters = new NativeArray<OfflineWreckageBakeCounters64>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                float3 localBlast = ResolveLocalBlast();
                BakeStateToBuffers(baseVertices, baseIndices, profile, OfflineWreckageDamageState.Ruptured, localBlast, workingVertices, stateVertices, stateIndices, tearWeights, counters, out JobHandle previewHandle, out _);
                previewHandle.Complete();
                OfflineWreckageBakeCounters64 previewCounters = counters[0];
                int activeVertexCount = math.clamp(previewCounters.ActiveVertexCount <= 0 ? baseVertices.Length : previewCounters.ActiveVertexCount, 0, stateVertices.Length);
                int activeIndexCount = math.clamp(previewCounters.ActiveIndexCount, 0, stateIndices.Length);
                if (HasNonFinite(stateVertices, activeVertexCount) ||
                    !ValidateFinalTopology(stateVertices, activeVertexCount, stateIndices, activeIndexCount))
                {
                    uint warningFlags = previewCounters.WarningFlags |
                                        OfflineWreckageBakeConstants.WarningNonFiniteFallback |
                                        OfflineWreckageBakeConstants.WarningDegenerateTriangles;
                    OfflineWreckageBlackBox.Record(
                        ResolveModuleAup(),
                        HashAscii(mesh.name),
                        (uint)OfflineWreckageDamageState.Ruptured,
                        activeVertexCount,
                        activeIndexCount,
                        previewCounters.TornVertexCount,
                        previewCounters.HullVertexCount,
                        0.0,
                        warningFlags);
                    OfflineWreckageBlackBox.Dump(ProjectRoot());
                    _statusLabel.text = "Preview aborted: degenerate wreckage topology.";
                    return;
                }

                OfflineWreckagePreviewStore.SetMesh(CreateMesh(mesh.name + "_WRECKAGE_PREVIEW", stateVertices, activeVertexCount, stateIndices, activeIndexCount));
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
            int findings = Runtime_Destruction_Scanner.ScanFindings(ProjectRoot());
            _statusLabel.text = "Runtime destruction scan findings: " + findings + ". No report file written.";
        }

        private void BakeMesh(Mesh source, string sourcePath, WreckageDeformationProfileDTO profile, ref BakeStatsAccumulator stats)
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
                int extraVertexCapacity = ResolveAdditionalFractureVertexCapacity(baseIndices.Length, profile.GlobalQualityWeight);
                int extraIndexCapacity = ResolveAdditionalFractureIndexCapacity(baseIndices.Length, profile.GlobalQualityWeight);
                int vertexCapacity = baseVertices.Length + baseIndices.Length + extraVertexCapacity;
                int indexCapacity = baseIndices.Length + extraIndexCapacity;
                tearWeights = new NativeArray<float>(baseVertices.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                counters = new NativeArray<OfflineWreckageBakeCounters64>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                hullPoints = new NativeArray<float3>(OfflineWreckageBakeConstants.MaxCollisionHullVertices, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                workingVertices = new NativeArray<OfflineWreckageBakeVertexDTO>(baseVertices.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                stateVertices = new NativeArray<OfflineWreckageBakeVertexDTO>(vertexCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                stateIndices = new NativeArray<int>(indexCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                string safeName = BuildStableSafeName(sourcePath);
                float3 localBlast = ResolveLocalBlast();
                double3 moduleAup = ResolveModuleAup();
                MeshDamageStateMappingDTO mapping = default;
                mapping.PristineMeshHash = HashAscii(sourcePath);
                mapping.MappingVersion = OfflineWreckageBakeConstants.MappingLayoutVersion;
                mapping.ArtifactVersion = OfflineWreckageBakeConstants.BakeArtifactVersion;
                mapping.StressedMeshHash = BakeStateAsset(source, safeName, baseVertices, baseIndices, profile, OfflineWreckageDamageState.Stressed, localBlast, moduleAup, workingVertices, stateVertices, stateIndices, tearWeights, counters, hullPoints, ref stats);
                mapping.RupturedMeshHash = BakeStateAsset(source, safeName, baseVertices, baseIndices, profile, OfflineWreckageDamageState.Ruptured, localBlast, moduleAup, workingVertices, stateVertices, stateIndices, tearWeights, counters, hullPoints, ref stats);
                mapping.CollapsedMeshHash = BakeStateAsset(source, safeName, baseVertices, baseIndices, profile, OfflineWreckageDamageState.Collapsed, localBlast, moduleAup, workingVertices, stateVertices, stateIndices, tearWeights, counters, hullPoints, ref stats);
                if (mapping.StressedMeshHash == 0u || mapping.RupturedMeshHash == 0u || mapping.CollapsedMeshHash == 0u)
                {
                    stats.WarningFlags |= OfflineWreckageBakeConstants.WarningDegenerateTriangles;
                    return;
                }

                WriteMappingBytes(safeName, mapping);
                if (!PublishStaticWreckPrefab(sourcePath, safeName, profile, localBlast))
                {
                    stats.WarningFlags |= OfflineWreckageBakeConstants.WarningPrefabSerializationFailed;
                    return;
                }

                stats.ProcessedMeshes++;
                stats.SourcePolygons += baseIndices.Length / 3;
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
            ref BakeStatsAccumulator stats)
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
            string meshPath = BuildStateMeshPath(safeName, suffix);
            OfflineWreckageBakeCounters64 stateCounters = counters[0];
            warningFlags |= stateCounters.WarningFlags;
            int hullCount = stateCounters.HullVertexCount;
            int activeVertexCount = math.clamp(stateCounters.ActiveVertexCount <= 0 ? baseVertices.Length : stateCounters.ActiveVertexCount, 0, stateVertices.Length);
            int activeIndexCount = math.clamp(stateCounters.ActiveIndexCount, 0, stateIndices.Length);
            double burstMicroseconds = stopwatch.Elapsed.TotalMilliseconds * 1000.0;
            if (hullCount > 256)
                warningFlags |= OfflineWreckageBakeConstants.WarningHullBudgetExceeded;
            if (stateCounters.DegenerateTriangleCount > 0)
                warningFlags |= OfflineWreckageBakeConstants.WarningDegenerateTriangles;
            if (HasNonFinite(stateVertices, activeVertexCount))
                warningFlags |= OfflineWreckageBakeConstants.WarningNonFiniteFallback;
            if (!ValidateFinalTopology(stateVertices, activeVertexCount, stateIndices, activeIndexCount))
                warningFlags |= OfflineWreckageBakeConstants.WarningDegenerateTriangles;

            uint meshHash = HashAscii(meshPath);
            OfflineWreckageBlackBox.Record(
                moduleAup,
                meshHash,
                (uint)state,
                activeVertexCount,
                activeIndexCount,
                stateCounters.TornVertexCount,
                hullCount,
                burstMicroseconds,
                warningFlags);
            uint fatalGeometryWarnings = OfflineWreckageBakeConstants.WarningNonFiniteFallback |
                                         OfflineWreckageBakeConstants.WarningDegenerateTriangles;
            if ((warningFlags & fatalGeometryWarnings) != 0u)
            {
                OfflineWreckageBlackBox.Dump(ProjectRoot());
                UnityEngine.Debug.LogError("Degenerate triangle detected in Wreckage output: " + meshPath);
                stats.WarningFlags |= warningFlags;
                return 0u;
            }

            Mesh mesh = CreateMesh(source.name + "_" + suffix, stateVertices, activeVertexCount, stateIndices, activeIndexCount);
            if (!ValidateCreatedMeshBounds(mesh))
            {
                OfflineWreckageBlackBox.Dump(ProjectRoot());
                UnityEngine.Debug.LogError("Invalid wreckage mesh bounds detected: " + meshPath);
                UnityEngine.Object.DestroyImmediate(mesh);
                stats.WarningFlags |= OfflineWreckageBakeConstants.WarningNonFiniteFallback;
                return 0u;
            }

            PublishMeshAsset(mesh, meshPath);

            string hullPath = BuildStateHullPath(safeName, suffix);
            Mesh hullMesh = CreateHullMesh(source.name + "_" + suffix + "_COLLIDER", hullPoints, hullCount);
            if (!ValidateCreatedMeshBounds(hullMesh))
            {
                OfflineWreckageBlackBox.Dump(ProjectRoot());
                UnityEngine.Debug.LogError("Invalid wreckage hull bounds detected: " + hullPath);
                UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(hullMesh);
                stats.WarningFlags |= OfflineWreckageBakeConstants.WarningNonFiniteFallback;
                return 0u;
            }

            PublishMeshAsset(hullMesh, hullPath);

            stats.GeneratedStates++;
            stats.GeneratedPolygons += activeIndexCount / 3;
            stats.TornVertices += stateCounters.TornVertexCount;
            stats.FractureHoleTriangles += stateCounters.FractureHoleTriangleCount;
            stats.BurstMicroseconds += burstMicroseconds;
            stats.MaxHullVertices = math.max(stats.MaxHullVertices, hullCount);
            stats.WarningFlags |= warningFlags;
            return meshHash;
        }

        private static int ResolveAdditionalFractureVertexCapacity(int sourceIndexCount, float quality)
        {
            float q = math.saturate(quality);
            int scaled = (int)math.ceil(math.max(sourceIndexCount, 256) * math.lerp(0.10f, 0.40f, q));
            return math.max((int)math.ceil(math.lerp(160f, 512f, q)), scaled);
        }

        private static int ResolveAdditionalFractureIndexCapacity(int sourceIndexCount, float quality)
        {
            float q = math.saturate(quality);
            int scaled = (int)math.ceil(math.max(sourceIndexCount, 384) * math.lerp(0.20f, 0.72f, q));
            return math.max((int)math.ceil(math.lerp(320f, 1536f, q)), scaled);
        }

        private static string BuildStateMeshPath(string safeName, string suffix)
        {
            return OutputFolder + "/GEN_" + safeName + "_" + suffix + ".asset";
        }

        private static string BuildStateHullPath(string safeName, string suffix)
        {
            return OutputFolder + "/GEN_" + safeName + "_" + suffix + "_COLLIDER.asset";
        }

        private static bool PublishStaticWreckPrefab(
            string sourcePath,
            string safeName,
            WreckageDeformationProfileDTO profile,
            float3 localBlast)
        {
            Mesh stressed = AssetDatabase.LoadAssetAtPath<Mesh>(BuildStateMeshPath(safeName, StateSuffix(OfflineWreckageDamageState.Stressed)));
            Mesh ruptured = AssetDatabase.LoadAssetAtPath<Mesh>(BuildStateMeshPath(safeName, StateSuffix(OfflineWreckageDamageState.Ruptured)));
            Mesh collapsed = AssetDatabase.LoadAssetAtPath<Mesh>(BuildStateMeshPath(safeName, StateSuffix(OfflineWreckageDamageState.Collapsed)));
            if (stressed == null || ruptured == null || collapsed == null)
                return false;

            EnsureAssetFolder(PrefabOutputFolder);
            int worldStaticLayer = LayerMask.NameToLayer("World_Static");
            if (worldStaticLayer < 0)
                worldStaticLayer = 0;

            GameObject root = new GameObject("GEN_Wreck_" + safeName);
            try
            {
                root.layer = worldStaticLayer;
                Bounds bounds = ruptured.bounds;

                Material[] materials = ResolveSourceMaterials(sourcePath);
                AddVisualState(root.transform, worldStaticLayer, "VIS_Stressed", stressed, materials, false);
                AddVisualState(root.transform, worldStaticLayer, "VIS_Ruptured", ruptured, materials, true);
                AddVisualState(root.transform, worldStaticLayer, "VIS_Collapsed", collapsed, materials, false);
                AddPrimitiveCollision(root.transform, worldStaticLayer, bounds, localBlast, profile);
                if (!AttachWreckageSalvageMetadata(root, safeName, bounds, localBlast, profile.GlobalQualityWeight))
                {
                    UnityEngine.Debug.LogError("Wreckage prefab serialization aborted: invalid salvage metadata for " + safeName);
                    return false;
                }

                if (!ValidateStaticWreckPrefabContract(root))
                {
                    UnityEngine.Debug.LogError("Wreckage prefab serialization aborted: primitive/material contract failed for " + safeName);
                    return false;
                }

                string prefabPath = PrefabOutputFolder + "/GEN_Wreck_" + safeName + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static bool AttachWreckageSalvageMetadata(GameObject root, string safeName, Bounds bounds, float3 localBlast, float quality)
        {
            if (root == null)
                return false;

            Type metadataType = ResolveColdEditorType(EquipmentMetadataTypeName, EquipmentMetadataFullName);
            Type anchorType = ResolveColdEditorType(InteractionAnchorDataTypeName, InteractionAnchorDataFullName);
            if (metadataType == null || anchorType == null || !typeof(Component).IsAssignableFrom(metadataType))
                return false;

            MethodInfo validateLayout = metadataType.GetMethod("ValidateStaticLayout", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (validateLayout == null || !(validateLayout.Invoke(null, null) is bool validLayout) || !validLayout)
                return false;

            MethodInfo setBakeData = metadataType.GetMethod("SetEditorBakeData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (setBakeData == null)
                return false;

            MethodInfo validateAnchorSet = metadataType.GetMethod("ValidateAnchorSet", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (validateAnchorSet == null)
                return false;

            Array anchors = BuildWreckageAnchors(anchorType, bounds, localBlast); // COLD ALLOC: InteractionAnchorData[3] serialized to prefab only.
            object[] validationArgs = { anchors, string.Empty }; // COLD ALLOC: reflection bridge for out string.
            if (!(validateAnchorSet.Invoke(null, validationArgs) is bool validAnchors) || !validAnchors)
            {
                string failureReason = validationArgs[1] as string;
                UnityEngine.Debug.LogError("Wreckage salvage anchor validation failed for " + safeName + ": " + (failureReason ?? "unknown"));
                return false;
            }

            Component metadata = root.AddComponent(metadataType);
            uint safeHash = HashAscii(safeName);
            uint wreckHash = OfflineWreckageBakeMath.Hash(safeHash ^ 0x1717A11Cu);
            uint bakeHash = OfflineWreckageBakeMath.Hash(safeHash ^ 0xBACE1717u);
            object[] args = { wreckHash, bakeHash, quality, anchors }; // COLD ALLOC: reflection bridge across asmdef boundary.
            setBakeData.Invoke(metadata, args);
            return true;
        }

        private static bool ValidateStaticWreckPrefabContract(GameObject root)
        {
            if (root == null)
                return false;

            MeshCollider[] meshColliders = root.GetComponentsInChildren<MeshCollider>(true); // COLD ALLOC: editor prefab gate only.
            if (meshColliders != null && meshColliders.Length > 0)
                return false;

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true); // COLD ALLOC: editor prefab gate only.
            int solidPrimitiveCount = 0;
            for (int i = 0; colliders != null && i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider is BoxCollider || collider is CapsuleCollider || collider is SphereCollider)
                {
                    if (!collider.isTrigger)
                        solidPrimitiveCount++;
                    continue;
                }

                return false;
            }

            if (solidPrimitiveCount <= 0)
                return false;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true); // COLD ALLOC: editor prefab gate only.
            if (renderers == null || renderers.Length == 0)
                return false;

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Material[] slots = renderers[rendererIndex].sharedMaterials; // COLD ALLOC: editor prefab gate only.
                if (slots == null || slots.Length == 0 || slots.Length > MaxWreckMaterialSlots)
                    return false;

                for (int slot = 0; slot < slots.Length; slot++)
                {
                    if (!IsPreferredWreckMaterial(slots[slot]))
                        return false;
                }
            }

            return true;
        }

        private static Type ResolveColdEditorType(string assemblyQualifiedName, string fullName)
        {
            Type type = Type.GetType(assemblyQualifiedName, false);
            if (type != null)
                return type;

            global::System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies(); // COLD ALLOC: editor bake asmdef bridge only.
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(fullName, false);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static Array BuildWreckageAnchors(Type anchorType, Bounds bounds, float3 localBlast)
        {
            Vector3 centerVector = bounds.center;
            Vector3 extentsVector = bounds.extents;
            float3 center = new float3(centerVector.x, centerVector.y, centerVector.z);
            float3 extents = new float3(
                math.max(math.abs(extentsVector.x), 0.1f),
                math.max(math.abs(extentsVector.y), 0.1f),
                math.max(math.abs(extentsVector.z), 0.1f));
            float3 clampedBlast = math.clamp(localBlast, center - extents * 0.82f, center + extents * 0.82f);
            float snap = math.clamp(math.cmin(extents) * 0.35f, 0.12f, 0.55f);
            Array anchors = Array.CreateInstance(anchorType, 3);
            anchors.SetValue(CreateAnchor(
                anchorType,
                "ANCHOR_WreckCoreAccess_1717",
                clampedBlast,
                ResolveAnchorForward(center, clampedBlast),
                new float3(0f, 1f, 0f),
                snap,
                AnchorFlagActive | AnchorFlagTwoHanded,
                3,
                2), 0);
            anchors.SetValue(CreateAnchor(
                anchorType,
                "ANCHOR_WreckForeSalvage_1717",
                center + new float3(0f, extents.y * 0.12f, -extents.z * 0.72f),
                new float3(0f, 0f, -1f),
                new float3(0f, 1f, 0f),
                snap * 0.85f,
                AnchorFlagActive,
                3,
                1), 1);
            anchors.SetValue(CreateAnchor(
                anchorType,
                "ANCHOR_WreckAftSalvage_1717",
                center + new float3(0f, extents.y * 0.08f, extents.z * 0.72f),
                new float3(0f, 0f, 1f),
                new float3(0f, 1f, 0f),
                snap * 0.85f,
                AnchorFlagActive,
                3,
                1), 2);
            return anchors;
        }

        private static object CreateAnchor(Type anchorType, string key, float3 localPosition, float3 forward, float3 up, float snapRadius, uint flags, byte handMask, byte surfaceKind)
        {
            object anchor = Activator.CreateInstance(anchorType);
            SetAnchorField(anchorType, anchor, "LocalPosition", math.all(math.isfinite(localPosition)) ? localPosition : float3.zero);
            SetAnchorField(anchorType, anchor, "LocalForward", math.normalizesafe(forward, new float3(0f, 0f, 1f)));
            SetAnchorField(anchorType, anchor, "LocalUp", math.normalizesafe(up, new float3(0f, 1f, 0f)));
            SetAnchorField(anchorType, anchor, "SnapRadiusMeters", math.clamp(math.isfinite(snapRadius) ? snapRadius : 0.15f, 0.05f, 1.25f));
            SetAnchorField(anchorType, anchor, "AnchorId", HashAscii(key));
            SetAnchorField(anchorType, anchor, "Flags", flags);
            SetAnchorField(anchorType, anchor, "HandMask", handMask);
            SetAnchorField(anchorType, anchor, "SurfaceKind", surfaceKind);
            return anchor;
        }

        private static void SetAnchorField(Type anchorType, object anchor, string fieldName, object value)
        {
            FieldInfo field = anchorType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                field.SetValue(anchor, value);
        }

        private static float3 ResolveAnchorForward(float3 center, float3 anchor)
        {
            float3 outward = anchor - center;
            outward.y = 0f;
            return math.normalizesafe(outward, new float3(0f, 0f, 1f));
        }

        private static Material[] ResolveSourceMaterials(string sourcePath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (prefab == null)
                return ResolveDefaultWreckMaterialArray();

            Renderer renderer = prefab.GetComponentInChildren<Renderer>(true);
            if (renderer == null)
                return ResolveDefaultWreckMaterialArray();

            Material[] sourceMaterials = renderer.sharedMaterials; // COLD ALLOC: Unity editor material slot snapshot for prefab serialization only.
            if (sourceMaterials == null || sourceMaterials.Length == 0)
                return ResolveDefaultWreckMaterialArray();

            Material primary = null;
            Material secondary = null;
            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                Material material = sourceMaterials[i];
                if (!IsPreferredWreckMaterial(material))
                    continue;

                if (TryAssignWreckMaterialSlot(material, ref primary, ref secondary))
                    break;
            }

            if (primary == null)
                return ResolveDefaultWreckMaterialArray();
            if (secondary == null)
                return new[] { primary }; // COLD ALLOC: serialized prefab renderer material array.
            return new[] { primary, secondary }; // COLD ALLOC: hard cap prevents accidental SetPass/material-slot explosion.
        }

        private static Material[] ResolveDefaultWreckMaterialArray()
        {
            Material material = ResolveDefaultWreckMaterial();
            return material == null
                ? Array.Empty<Material>()
                : new[] { material }; // COLD ALLOC: one shared fallback material slot for prefab serialization.
        }

        private static Material ResolveDefaultWreckMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(DefaultWreckMaterialPath);
            if (material != null)
                return IsPreferredWreckMaterial(material) ? material : null;

            Shader shader = Shader.Find(WreckIndirectShaderName);
            if (shader == null)
                return null;

            EnsureAssetFolder(DefaultWreckMaterialFolder);
            material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(DefaultWreckMaterialPath),
                enableInstancing = true
            };
            SetMaterialFloatIfPresent(material, "_Metallic", 0.72f);
            SetMaterialFloatIfPresent(material, "_Smoothness", 0.47f);
            SetMaterialFloatIfPresent(material, "_WreckRustStrength", 0.92f);
            SetMaterialFloatIfPresent(material, "_WreckGrimeStrength", 0.78f);
            SetMaterialFloatIfPresent(material, "_WreckSootStrength", 0.95f);
            AssetDatabase.CreateAsset(material, DefaultWreckMaterialPath);
            return material;
        }

        private static void SetMaterialFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material != null && material.HasProperty(propertyName))
                material.SetFloat(propertyName, value);
        }

        private static bool IsPreferredWreckMaterial(Material material)
        {
            Shader shader = material != null ? material.shader : null;
            return shader != null && string.Equals(shader.name, WreckIndirectShaderName, StringComparison.Ordinal);
        }

        private static bool TryAssignWreckMaterialSlot(Material material, ref Material primary, ref Material secondary)
        {
            if (material == null || ReferenceEquals(material, primary) || ReferenceEquals(material, secondary))
                return CountResolvedWreckMaterialSlots(primary, secondary) >= MaxWreckMaterialSlots;
            if (primary == null)
            {
                primary = material;
                return false;
            }
            if (secondary == null)
            {
                secondary = material;
                return CountResolvedWreckMaterialSlots(primary, secondary) >= MaxWreckMaterialSlots;
            }
            return true;
        }

        private static int CountResolvedWreckMaterialSlots(Material primary, Material secondary)
        {
            int count = primary != null ? 1 : 0;
            return secondary != null ? count + 1 : count;
        }

        private static void EnsureAssetFolder(string folder)
        {
            string normalized = NormalizeAssetFolder(folder);
            if (string.IsNullOrEmpty(normalized))
                return;
            if (AssetDatabase.IsValidFolder(normalized))
                return;

            int firstSlash = normalized.IndexOf('/');
            if (firstSlash <= 0)
                return;

            string current = normalized.Substring(0, firstSlash);
            int segmentStart = firstSlash + 1;
            while (segmentStart < normalized.Length)
            {
                int slash = normalized.IndexOf('/', segmentStart);
                int segmentEnd = slash < 0 ? normalized.Length : slash;
                int segmentLength = segmentEnd - segmentStart;
                if (segmentLength <= 0)
                    return;

                string segment = normalized.Substring(segmentStart, segmentLength);
                string next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segment);
                current = next;
                if (slash < 0)
                    break;
                segmentStart = slash + 1;
            }
        }

        private static string NormalizeAssetFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder))
                return string.Empty;

            string normalized = folder.Replace('\\', '/').TrimEnd('/');
            if (!normalized.StartsWith("Assets", StringComparison.Ordinal))
                return string.Empty;
            if (normalized.IndexOf("..", StringComparison.Ordinal) >= 0)
                return string.Empty;
            return normalized;
        }

        private static void AddVisualState(Transform root, int layer, string name, Mesh mesh, Material[] materials, bool active)
        {
            GameObject child = new GameObject(name);
            child.layer = layer;
            child.SetActive(active);
            child.transform.SetParent(root, false);
            MeshFilter filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            if (materials != null && materials.Length > 0)
                renderer.sharedMaterials = materials;
        }

        private static void AddPrimitiveCollision(Transform root, int layer, Bounds bounds, float3 localBlast, WreckageDeformationProfileDTO profile)
        {
            GameObject collision = new GameObject("COL_WreckProxy");
            collision.layer = layer;
            collision.transform.SetParent(root, false);

            float q = math.saturate(profile.GlobalQualityWeight);
            Vector3 boundsCenter = bounds.center;
            Vector3 boundsSize = SanitizeColliderSize(bounds.size);
            Vector3 min = boundsCenter - boundsSize * 0.5f;
            Vector3 max = boundsCenter + boundsSize * 0.5f;
            float breachSize = math.max(0.25f, profile.BlastRadius * math.lerp(0.08f, 0.22f, q));
            float gapHalfX = math.min(boundsSize.x * 0.45f, math.max(breachSize * 1.35f, boundsSize.x * math.lerp(0.10f, 0.22f, q)));
            float gapHalfZ = math.min(boundsSize.z * 0.45f, math.max(breachSize * 1.35f, boundsSize.z * math.lerp(0.10f, 0.22f, q)));
            float gapCenterX = math.clamp(localBlast.x, min.x + 0.05f, max.x - 0.05f);
            float gapCenterZ = math.clamp(localBlast.z, min.z + 0.05f, max.z - 0.05f);
            float gapMinX = math.clamp(gapCenterX - gapHalfX, min.x, max.x);
            float gapMaxX = math.clamp(gapCenterX + gapHalfX, min.x, max.x);
            float gapMinZ = math.clamp(gapCenterZ - gapHalfZ, min.z, max.z);
            float gapMaxZ = math.clamp(gapCenterZ + gapHalfZ, min.z, max.z);

            AddBoxColliderSpan(collision.transform, layer, "COL_Hull_Left", min.x, gapMinX, min.y, max.y, min.z, max.z);
            AddBoxColliderSpan(collision.transform, layer, "COL_Hull_Right", gapMaxX, max.x, min.y, max.y, min.z, max.z);
            AddBoxColliderSpan(collision.transform, layer, "COL_Hull_Fore", gapMinX, gapMaxX, min.y, max.y, min.z, gapMinZ);
            AddBoxColliderSpan(collision.transform, layer, "COL_Hull_Aft", gapMinX, gapMaxX, min.y, max.y, gapMaxZ, max.z);

            float plateThickness = math.max(0.05f, math.min(boundsSize.y * 0.08f, 0.35f));
            AddBoxColliderFrame(collision.transform, layer, "COL_Floor", min.y, min.y + plateThickness, min, max, gapMinX, gapMaxX, gapMinZ, gapMaxZ);
            AddBoxColliderFrame(collision.transform, layer, "COL_Ceiling", max.y - plateThickness, max.y, min, max, gapMinX, gapMaxX, gapMinZ, gapMaxZ);

            GameObject breach = new GameObject("COL_BreachAccess");
            breach.layer = layer;
            breach.transform.SetParent(collision.transform, false);
            BoxCollider breachBox = breach.AddComponent<BoxCollider>();
            breachBox.center = new Vector3(localBlast.x, localBlast.y, localBlast.z);
            breachBox.size = new Vector3(breachSize, breachSize, breachSize);
            breachBox.isTrigger = true;

            GameObject salvagePocket = new GameObject("COL_SalvagePocket");
            salvagePocket.layer = layer;
            salvagePocket.transform.SetParent(collision.transform, false);
            SphereCollider salvageSphere = salvagePocket.AddComponent<SphereCollider>();
            float pocketMinY = math.min(min.y + plateThickness, max.y);
            float pocketMaxY = math.max(max.y - plateThickness, pocketMinY);
            float pocketY = math.clamp(localBlast.y, pocketMinY, pocketMaxY);
            float pocketPlanarLimit = math.max(0.08f, math.min(boundsSize.x, boundsSize.z) * 0.22f);
            salvageSphere.center = new Vector3(gapCenterX, pocketY, gapCenterZ);
            salvageSphere.radius = Mathf.Min(pocketPlanarLimit, Mathf.Max(0.08f, breachSize * Mathf.Lerp(0.55f, 0.95f, q)));
            salvageSphere.isTrigger = true;

            GameObject support = new GameObject("COL_SupportSpan");
            support.layer = layer;
            support.transform.SetParent(collision.transform, false);
            CapsuleCollider supportCapsule = support.AddComponent<CapsuleCollider>();
            supportCapsule.direction = 1;
            supportCapsule.center = bounds.center;
            float horizontalExtent = Mathf.Max(0.05f, Mathf.Min(Mathf.Abs(bounds.extents.x), Mathf.Abs(bounds.extents.z)));
            supportCapsule.radius = Mathf.Max(0.05f, horizontalExtent * Mathf.Lerp(0.035f, 0.09f, q));
            supportCapsule.height = Mathf.Max(supportCapsule.radius * 2f, Mathf.Abs(bounds.size.y) * Mathf.Lerp(0.35f, 0.9f, q));
        }

        private static void AddBoxColliderFrame(
            Transform parent,
            int layer,
            string namePrefix,
            float minY,
            float maxY,
            Vector3 min,
            Vector3 max,
            float gapMinX,
            float gapMaxX,
            float gapMinZ,
            float gapMaxZ)
        {
            AddBoxColliderSpan(parent, layer, namePrefix + "_Left", min.x, gapMinX, minY, maxY, min.z, max.z);
            AddBoxColliderSpan(parent, layer, namePrefix + "_Right", gapMaxX, max.x, minY, maxY, min.z, max.z);
            AddBoxColliderSpan(parent, layer, namePrefix + "_Fore", gapMinX, gapMaxX, minY, maxY, min.z, gapMinZ);
            AddBoxColliderSpan(parent, layer, namePrefix + "_Aft", gapMinX, gapMaxX, minY, maxY, gapMaxZ, max.z);
        }

        private static void AddBoxColliderSpan(
            Transform parent,
            int layer,
            string name,
            float minX,
            float maxX,
            float minY,
            float maxY,
            float minZ,
            float maxZ)
        {
            float sizeX = maxX - minX;
            float sizeY = maxY - minY;
            float sizeZ = maxZ - minZ;
            if (sizeX < 0.05f || sizeY < 0.05f || sizeZ < 0.05f)
                return;

            GameObject child = new GameObject(name);
            child.layer = layer;
            child.transform.SetParent(parent, false);
            BoxCollider box = child.AddComponent<BoxCollider>();
            box.center = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);
            box.size = new Vector3(sizeX, sizeY, sizeZ);
        }

        private static Vector3 SanitizeColliderSize(Vector3 size)
        {
            return new Vector3(
                Mathf.Max(0.05f, Mathf.Abs(size.x)),
                Mathf.Max(0.05f, Mathf.Abs(size.y)),
                Mathf.Max(0.05f, Mathf.Abs(size.z)));
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
                GlobalQualityWeight = profile.GlobalQualityWeight,
                EpicenterLocal = localBlast,
                DamageScale = stateScale
            };
            handle = torn.Schedule(handle);

            RecalculateDeformedNormalsJob normals = new RecalculateDeformedNormalsJob
            {
                Vertices = stateVertices,
                Indices = stateIndices,
                Counters = counters
            };
            handle = normals.Schedule(handle);

            BendFractureNormalsJob bentNormals = new BendFractureNormalsJob
            {
                Vertices = stateVertices,
                Counters = counters,
                EpicenterLocal = localBlast,
                BlastRadius = math.max(profile.BlastRadius, 0.001f) * math.lerp(0.65f, 1.35f, stateScale),
                DamageScale = stateScale,
                GlobalQualityWeight = profile.GlobalQualityWeight
            };
            handle = bentNormals.Schedule(stateVertices.Length, 64, handle);

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

        private static bool ValidateFinalTopology(
            NativeArray<OfflineWreckageBakeVertexDTO> vertices,
            int vertexCount,
            NativeArray<int> indices,
            int indexCount)
        {
            int safeVertexCount = math.clamp(vertexCount, 0, vertices.Length);
            int safeIndexCount = math.clamp(indexCount, 0, indices.Length);
            if (safeVertexCount <= 0 || safeIndexCount < 3 || safeIndexCount % 3 != 0)
                return false;

            for (int vertexIndex = 0; vertexIndex < safeVertexCount; vertexIndex++)
            {
                OfflineWreckageBakeVertexDTO vertex = vertices[vertexIndex];
                if (!math.all(math.isfinite(vertex.Position)) || !math.all(math.isfinite(vertex.Normal)))
                    return false;

                float normalLengthSq = math.lengthsq(vertex.Normal);
                if (!math.isfinite(normalLengthSq) || normalLengthSq < MinNormalLengthSq || normalLengthSq > MaxNormalLengthSq)
                    return false;
            }

            float minCrossLengthSq = MinTriangleArea * MinTriangleArea * 4f;
            for (int index = 0; index < safeIndexCount; index += 3)
            {
                int i0 = indices[index];
                int i1 = indices[index + 1];
                int i2 = indices[index + 2];
                if ((uint)i0 >= (uint)safeVertexCount ||
                    (uint)i1 >= (uint)safeVertexCount ||
                    (uint)i2 >= (uint)safeVertexCount)
                {
                    return false;
                }

                float3 v0 = vertices[i0].Position;
                float3 v1 = vertices[i1].Position;
                float3 v2 = vertices[i2].Position;
                float3 cross = math.cross(v1 - v0, v2 - v0);
                float crossLengthSq = math.lengthsq(cross);
                if (!math.isfinite(crossLengthSq) || crossLengthSq < minCrossLengthSq)
                    return false;
            }

            return true;
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

        private static Mesh CreateMesh(string name, NativeArray<OfflineWreckageBakeVertexDTO> vertices, int vertexCount, NativeArray<int> indices, int indexCount)
        {
            int safeVertexCount = math.clamp(vertexCount, 0, vertices.Length);
            int safeIndexCount = math.clamp(indexCount, 0, indices.Length);
            safeIndexCount -= safeIndexCount % 3;
            if (safeVertexCount <= 0)
                safeIndexCount = 0;
            Mesh mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers;
            Bounds bounds = ComputeBounds(vertices, safeVertexCount);
            mesh.SetVertexBufferParams(safeVertexCount, s_vertexLayout);
            mesh.SetIndexBufferParams(safeIndexCount, IndexFormat.UInt32);
            mesh.SetVertexBufferData(vertices, 0, 0, safeVertexCount, 0, flags);
            mesh.SetIndexBufferData(indices, 0, 0, safeIndexCount, flags);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, safeIndexCount, MeshTopology.Triangles)
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

        private static bool ValidateCreatedMeshBounds(Mesh mesh)
        {
            if (mesh == null || mesh.vertexCount <= 0)
                return false;

            mesh.RecalculateBounds();
            Bounds bounds = mesh.bounds;
            Vector3 extents = bounds.extents;
            float extentMagnitudeSq = extents.sqrMagnitude;
            return float.IsFinite(bounds.center.x) &&
                   float.IsFinite(bounds.center.y) &&
                   float.IsFinite(bounds.center.z) &&
                   float.IsFinite(extents.x) &&
                   float.IsFinite(extents.y) &&
                   float.IsFinite(extents.z) &&
                   float.IsFinite(extentMagnitudeSq) &&
                   extentMagnitudeSq > 0.000001f;
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
                QueueAssetPath(path);
            }
        }

        private void QueueAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path) ||
                !IsValidWreckageSourcePath(path) ||
                LoadMeshFromAsset(path) == null ||
                _pendingAssetPaths.Contains(path))
            {
                return;
            }

            _pendingAssetPaths.Add(path);
        }

        private static bool IsValidWreckageSourcePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            if (path.StartsWith(OutputFolder + "/", StringComparison.Ordinal) ||
                path.StartsWith(PrefabOutputFolder + "/", StringComparison.Ordinal))
            {
                return false;
            }

            string fileName = Path.GetFileNameWithoutExtension(path);
            return !fileName.StartsWith("GEN_", StringComparison.Ordinal) &&
                   fileName.IndexOf("_COLLIDER", StringComparison.Ordinal) < 0;
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

        internal static bool HasValidSelectedBakeSources()
        {
            UnityEngine.Object[] selectedObjects = Selection.objects;
            for (int i = 0; i < selectedObjects.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(selectedObjects[i]);
                if (string.IsNullOrEmpty(path))
                    continue;

                if (AssetDatabase.IsValidFolder(path))
                {
                    if (HasValidBakeGuid(AssetDatabase.FindAssets("t:Mesh", new[] { path })) ||
                        HasValidBakeGuid(AssetDatabase.FindAssets("t:Prefab", new[] { path })))
                    {
                        return true;
                    }

                    continue;
                }

                if (IsValidWreckageSourcePath(path) && LoadMeshFromAsset(path) != null)
                    return true;
            }

            return false;
        }

        private static bool HasValidBakeGuid(string[] guids)
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (IsValidWreckageSourcePath(path) && LoadMeshFromAsset(path) != null)
                    return true;
            }

            return false;
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
            WriteUInt32(bytes, 16, mapping.MappingVersion);
            WriteUInt32(bytes, 20, mapping.ArtifactVersion);
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

        private sealed class BakeStatsAccumulator
        {
            public int ProcessedMeshes;
            public int GeneratedStates;
            public int SourcePolygons;
            public int GeneratedPolygons;
            public int TornVertices;
            public int FractureHoleTriangles;
            public int MaxHullVertices;
            public double BurstMicroseconds;
            public uint WarningFlags;
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
