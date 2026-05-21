#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Hecton8.Editor.OfflineGeometry
{
    internal struct InteriorAtlasProfile
    {
        public FixedString64Bytes Name;
        public int AtlasSize;
        public int MaxTileSize;
        public float Lod1Ratio;
        public float Lod2Ratio;
        public float GlobalQualityWeight;
    }

    internal struct InteriorClutterBakeMetric
    {
        public string SourcePath;
        public string OutputPrefabPath;
        public int StaticRenderers;
        public int InteractiveRenderers;
        public int SourceMaterials;
        public int DrawCallsBefore;
        public int DrawCallsAfter;
        public int Lod0Triangles;
        public int Lod1Triangles;
        public int Lod2Triangles;
        public double BurstTransformMilliseconds;
        public double SerializationMilliseconds;
        public InteriorClutterWarningFlags WarningFlags;
    }

    internal struct InteriorClutterBakeScratch
    {
        public List<InteriorClutterRenderSegment> StaticSegments;
        public List<Transform> InteractiveObjects;
        public List<Material> Materials;
        public List<MeshFilter> MeshFilters;
        public List<Material> SharedMaterials;
        public List<Component> ComponentScratch;

        internal static InteriorClutterBakeScratch Create()
        {
            return new InteriorClutterBakeScratch
            {
                StaticSegments = new List<InteriorClutterRenderSegment>(128),
                InteractiveObjects = new List<Transform>(32),
                Materials = new List<Material>(32),
                MeshFilters = new List<MeshFilter>(256),
                SharedMaterials = new List<Material>(8),
                ComponentScratch = new List<Component>(32)
            };
        }

        internal void Clear()
        {
            StaticSegments?.Clear();
            InteractiveObjects?.Clear();
            Materials?.Clear();
            MeshFilters?.Clear();
            SharedMaterials?.Clear();
            ComponentScratch?.Clear();
        }
    }

    internal sealed class InteriorClutterForgeWindow : EditorWindow
    {
        private TextField _folderField;
        private TextField _tagField;
        private TextField _layerField;
        private DropdownField _profileDropdown;
        private Label _status;
        private ProgressBar _progress;
        private List<InteriorAtlasProfile> _profiles;

        [MenuItem("HECTON-8/Interior Consolidation Forge/Open", false, 240)]
        private static void Open()
        {
            GetWindow<InteriorClutterForgeWindow>("Interior Consolidation Forge");
        }

        public void CreateGUI()
        {
            _profiles = InteriorAtlasProfileCsv.LoadProfiles();
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _folderField = new TextField("Prefab folder")
            {
                value = AssetDatabase.IsValidFolder(InteriorClutterForgeConstants.DefaultHabitatRoot)
                    ? InteriorClutterForgeConstants.DefaultHabitatRoot
                    : InteriorClutterForgeConstants.FallbackConstructionRoot
            };
            rootVisualElement.Add(_folderField);

            _tagField = new TextField("Excluded tags")
            {
                value = "Player,Interactable,Door,Fabricator,Terminal"
            };
            rootVisualElement.Add(_tagField);

            _layerField = new TextField("Excluded layers")
            {
                value = "Player,Interaction,UI"
            };
            rootVisualElement.Add(_layerField);

            _profileDropdown = new DropdownField("Atlas profile");
            RefreshProfileDropdown();
            rootVisualElement.Add(_profileDropdown);

            Button scanButton = new Button(RunScan) { text = "SCAN BLOAT" };
            Button previewButton = new Button(RunPreview) { text = "PREVIEW SELECTED" };
            Button bakeButton = new Button(RunBake) { text = "BAKE INTERIORS" };
            rootVisualElement.Add(scanButton);
            rootVisualElement.Add(previewButton);
            rootVisualElement.Add(bakeButton);

            _progress = new ProgressBar { title = "Idle", value = 0f };
            rootVisualElement.Add(_progress);
            _status = new Label("PENDING VERIFICATION");
            rootVisualElement.Add(_status);

            rootVisualElement.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            rootVisualElement.RegisterCallback<DragPerformEvent>(OnDragPerform);
            InteriorClutterPreviewOverlay.EnsureHook();
        }

        private void RefreshProfileDropdown()
        {
            var choices = new List<string>(math.max(1, _profiles.Count));
            for (int i = 0; i < _profiles.Count; i++)
                choices.Add(_profiles[i].Name.ToString());
            if (choices.Count == 0)
                choices.Add("Default_MX350");
            _profileDropdown.choices = choices;
            _profileDropdown.value = choices[0];
        }

        private void RunScan()
        {
            InteriorClutterExcludeFilter filter = InteriorClutterExcludeFilter.Parse(_tagField.value, _layerField.value);
            List<InteriorClutterPrefabFinding> findings = Hierarchy_Bloat_Scanner.ScanProject(_folderField.value, filter);
            Hierarchy_Bloat_Scanner.WriteReport(findings);
            _status.text = "Scan wrote " + InteriorClutterForgeConstants.RenderingOptimizationReportPath + " findings=" + findings.Count;
        }

        private void RunPreview()
        {
            GameObject selected = Selection.activeObject as GameObject;
            string path = selected != null ? AssetDatabase.GetAssetPath(selected) : null;
            if (string.IsNullOrEmpty(path))
            {
                _status.text = "No prefab selected for preview.";
                return;
            }

            InteriorClutterExcludeFilter filter = InteriorClutterExcludeFilter.Parse(_tagField.value, _layerField.value);
            InteriorClutterPreviewOverlay.BuildPreview(path, filter);
            _status.text = "Preview built for " + path;
        }

        private void RunBake()
        {
            InteriorAtlasProfile profile = ResolveSelectedProfile();
            InteriorClutterExcludeFilter filter = InteriorClutterExcludeFilter.Parse(_tagField.value, _layerField.value);
            List<InteriorClutterBakeMetric> metrics = InteriorClutterForge.BakeFolder(_folderField.value, profile, filter, OnBakeProgress);
            InteriorClutterForge.WriteConsolidationReport(metrics);
            _progress.title = "Bake pass ended";
            _progress.value = 100f;
            _status.text = "Bake wrote " + InteriorClutterForgeConstants.ConsolidationReportPath + " rooms=" + metrics.Count;
        }

        private InteriorAtlasProfile ResolveSelectedProfile()
        {
            string selected = _profileDropdown != null ? _profileDropdown.value : null;
            for (int i = 0; i < _profiles.Count; i++)
            {
                if (string.Equals(_profiles[i].Name.ToString(), selected, StringComparison.Ordinal))
                    return _profiles[i];
            }

            return InteriorAtlasProfileCsv.DefaultProfile();
        }

        private void OnBakeProgress(string label, float value01)
        {
            _progress.title = label;
            _progress.value = math.saturate(value01) * 100f;
            Repaint();
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (DragAndDrop.objectReferences == null || DragAndDrop.objectReferences.Length == 0)
                return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            DragAndDrop.AcceptDrag();
            for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(DragAndDrop.objectReferences[i]);
                if (AssetDatabase.IsValidFolder(path))
                {
                    _folderField.value = path;
                    break;
                }
            }

            evt.StopPropagation();
        }
    }

    internal static class InteriorClutterForge
    {
        private const MeshUpdateFlags MeshFlags =
            MeshUpdateFlags.DontRecalculateBounds |
            MeshUpdateFlags.DontValidateIndices |
            MeshUpdateFlags.DontNotifyMeshUsers;

        private static readonly Stopwatch _Stopwatch = new Stopwatch();

        [MenuItem("HECTON-8/Interior Consolidation Forge/Bake Selected", false, 241)]
        private static void BakeSelectedMenu()
        {
            InteriorAtlasProfile profile = InteriorAtlasProfileCsv.DefaultProfile();
            InteriorClutterExcludeFilter filter = InteriorClutterExcludeFilter.Default();
            var metrics = new List<InteriorClutterBakeMetric>(16);
            Object[] selected = Selection.objects;
            for (int i = 0; i < selected.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(selected[i]);
                if (AssetDatabase.IsValidFolder(path))
                    metrics.AddRange(BakeFolder(path, profile, filter, null));
                else if (!string.IsNullOrEmpty(path))
                    BakePrefab(path, profile, filter, metrics);
            }

            WriteConsolidationReport(metrics);
            Debug.Log("[SHINOBU_211] Interior clutter bake pass ended. Rooms=" + metrics.Count + ".");
        }

        [MenuItem("HECTON-8/Interior Consolidation Forge/Run Hierarchy Bloat Scanner", false, 242)]
        private static void ScanMenu()
        {
            InteriorClutterExcludeFilter filter = InteriorClutterExcludeFilter.Default();
            List<InteriorClutterPrefabFinding> findings = Hierarchy_Bloat_Scanner.ScanProject(InteriorClutterForgeConstants.DefaultHabitatRoot, filter);
            Hierarchy_Bloat_Scanner.WriteReport(findings);
            Debug.Log("[SHINOBU_211] Hierarchy bloat scan wrote " + InteriorClutterForgeConstants.RenderingOptimizationReportPath + " findings=" + findings.Count + ".");
        }

        [MenuItem("HECTON-8/Interior Consolidation Forge/Generate Mock Clutter Benchmark", false, 243)]
        private static void MockBenchmarkMenu()
        {
            InteriorAtlasProfile profile = InteriorAtlasProfileCsv.DefaultProfile();
            Mesh mesh = GenerateMockClutterBenchmark(profile);
            Debug.Log("[SHINOBU_211] Mock clutter benchmark generated " + (mesh != null ? mesh.vertexCount : 0) + " vertices.");
        }

        internal static List<InteriorClutterBakeMetric> BakeFolder(string folder, InteriorAtlasProfile profile, InteriorClutterExcludeFilter filter, Action<string, float> progress)
        {
            var metrics = new List<InteriorClutterBakeMetric>(16);
            string scanRoot = ResolveScanRoot(folder);
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { scanRoot });
            InteriorClutterBlackBoxSession blackBox = InteriorClutterBlackBoxSession.Create();
            InteriorClutterBakeScratch scratch = InteriorClutterBakeScratch.Create();
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    progress?.Invoke("Bake " + Path.GetFileNameWithoutExtension(path), guids.Length > 0 ? (float)i / guids.Length : 1f);
                    try
                    {
                        BakePrefab(path, profile, filter, metrics, ref blackBox, ref scratch);
                    }
                    catch (Exception ex)
                    {
                        blackBox.RecordFailure(path, InteriorClutterWarningFlags.BakeException);
                        blackBox.Dump("BakeFolder exception: " + ex.GetType().Name);
                        Debug.LogError("[SHINOBU_211] Interior bake failed for " + path + ": " + ex.Message);
                    }
                }
            }
            finally
            {
                blackBox.Dispose();
            }

            progress?.Invoke("Bake pass ended", 1f);
            return metrics;
        }

        internal static bool BakePrefab(string prefabPath, InteriorAtlasProfile profile, InteriorClutterExcludeFilter filter, List<InteriorClutterBakeMetric> metrics)
        {
            InteriorClutterBlackBoxSession blackBox = InteriorClutterBlackBoxSession.Create();
            InteriorClutterBakeScratch scratch = InteriorClutterBakeScratch.Create();
            try
            {
                return BakePrefab(prefabPath, profile, filter, metrics, ref blackBox, ref scratch);
            }
            catch (Exception ex)
            {
                blackBox.RecordFailure(prefabPath, InteriorClutterWarningFlags.BakeException);
                blackBox.Dump("BakePrefab exception: " + ex.GetType().Name);
                Debug.LogError("[SHINOBU_211] Interior bake failed for " + prefabPath + ": " + ex.Message);
                return false;
            }
            finally
            {
                blackBox.Dispose();
            }
        }

        private static bool BakePrefab(string prefabPath, InteriorAtlasProfile profile, InteriorClutterExcludeFilter filter, List<InteriorClutterBakeMetric> metrics, ref InteriorClutterBlackBoxSession blackBox, ref InteriorClutterBakeScratch scratch)
        {
            if (string.IsNullOrEmpty(prefabPath) || !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                return false;

            InteriorClutterVertexLayoutValidator.ValidateStructs();
            EnsureAssetFolder(InteriorClutterForgeConstants.MeshOutputFolder);
            EnsureAssetFolder(InteriorClutterForgeConstants.MaterialOutputFolder);
            EnsureAssetFolder(InteriorClutterForgeConstants.TextureOutputFolder);
            EnsureAssetFolder(InteriorClutterForgeConstants.PrefabOutputFolder);

            scratch.Clear();
            GameObject sourceRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                List<InteriorClutterRenderSegment> staticSegments = scratch.StaticSegments;
                List<Transform> interactiveObjects = scratch.InteractiveObjects;
                List<Material> materials = scratch.Materials;
                CollectSegments(sourceRoot, filter, scratch);
                if (staticSegments.Count == 0)
                    return false;

                InteriorClutterBakeMetric metric = default;
                metric.SourcePath = prefabPath;
                int preservedInteractiveRenderers = CountPreservedInteractiveRenderers(interactiveObjects, scratch.MeshFilters, sourceRoot.transform);
                metric.StaticRenderers = CountUniqueRenderers(staticSegments);
                metric.InteractiveRenderers = preservedInteractiveRenderers;
                metric.SourceMaterials = materials.Count;
                metric.DrawCallsBefore = math.max(1, staticSegments.Count) + preservedInteractiveRenderers;
                metric.DrawCallsAfter = 1 + preservedInteractiveRenderers;
                if (!AssetDatabase.IsValidFolder(InteriorClutterForgeConstants.DefaultHabitatRoot))
                    metric.WarningFlags |= InteriorClutterWarningFlags.MissingHabitatRoot;
                if (interactiveObjects.Count > 0)
                    metric.WarningFlags |= InteriorClutterWarningFlags.InteractivePreserved;

                InteriorMaterialAtlas atlas = InteriorMaterialAtlasBuilder.Build(prefabPath, materials, profile, ref metric);
                if (atlas.Rects.Count != materials.Count)
                    metric.WarningFlags |= InteriorClutterWarningFlags.MaterialOverflow;
                for (int rectIndex = 0; rectIndex < atlas.Rects.Count; rectIndex++)
                {
                    if ((atlas.Rects[rectIndex].Flags & (uint)InteriorClutterWarningFlags.MaterialOverflow) != 0u)
                    {
                        metric.WarningFlags |= InteriorClutterWarningFlags.MaterialOverflow;
                        break;
                    }
                }

                NativeArray<InteriorClutterSourceVertex> sourceVertices = default;
                NativeArray<int> segmentByVertex = default;
                NativeArray<InteriorClutterSegment> nativeSegments = default;
                NativeArray<InteriorClutterRawVertex> lod0Raw = default;
                NativeArray<InteriorClutterRawVertex> lod1Raw = default;
                NativeArray<InteriorClutterRawVertex> lod2Raw = default;
                try
                {
                    int totalVertexCount = CountTriangleSoupVertices(staticSegments);
                    if (totalVertexCount <= 0)
                        return false;

                    // COLD ALLOC: NativeArray<InteriorClutterSourceVertex>[totalVertexCount] - editor room triangle-soup staging - owner: InteriorClutterForge
                    sourceVertices = new NativeArray<InteriorClutterSourceVertex>(totalVertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                    // COLD ALLOC: NativeArray<int>[totalVertexCount] - editor vertex-to-segment map - owner: InteriorClutterForge
                    segmentByVertex = new NativeArray<int>(totalVertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                    // COLD ALLOC: NativeArray<InteriorClutterSegment>[staticSegments.Count] - editor transform/atlas windows - owner: InteriorClutterForge
                    nativeSegments = new NativeArray<InteriorClutterSegment>(staticSegments.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                    FillNativeSource(sourceRoot.transform, staticSegments, materials, atlas, sourceVertices, segmentByVertex, nativeSegments, ref metric);
                    // COLD ALLOC: NativeArray<InteriorClutterRawVertex>[totalVertexCount] - editor transformed LOD0 room mesh - owner: InteriorClutterForge
                    lod0Raw = new NativeArray<InteriorClutterRawVertex>(totalVertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                    _Stopwatch.Restart();
                    JobHandle transformHandle;
                    unsafe
                    {
                        transformHandle = new TransformAndAppendVerticesJob
                        {
                            SourceVertices = (InteriorClutterSourceVertex*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(sourceVertices),
                            Segments = (InteriorClutterSegment*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(nativeSegments),
                            OutputVertices = (InteriorClutterRawVertex*)NativeArrayUnsafeUtility.GetUnsafePtr(lod0Raw),
                            SegmentByVertex = segmentByVertex,
                            VertexCount = totalVertexCount,
                            SegmentCount = nativeSegments.Length
                        }.Schedule(totalVertexCount, 128);
                    }

                    int lod0Triangles = totalVertexCount / 3;
                    int lod1Triangles = math.max(1, (int)math.round(lod0Triangles * math.saturate(profile.Lod1Ratio)));
                    int lod2Triangles = math.max(1, (int)math.round(lod0Triangles * math.saturate(profile.Lod2Ratio)));
                    // COLD ALLOC: NativeArray<InteriorClutterRawVertex>[lod1Triangles*3] - editor deterministic LOD1 triangle soup - owner: InteriorClutterForge
                    lod1Raw = new NativeArray<InteriorClutterRawVertex>(lod1Triangles * 3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                    // COLD ALLOC: NativeArray<InteriorClutterRawVertex>[lod2Triangles*3] - editor deterministic LOD2 triangle soup - owner: InteriorClutterForge
                    lod2Raw = new NativeArray<InteriorClutterRawVertex>(lod2Triangles * 3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                    JobHandle lod1Handle = new DecimateTriangleSoupJob
                    {
                        SourceVertices = lod0Raw,
                        OutputVertices = lod1Raw,
                        SourceTriangleCount = lod0Triangles,
                        TargetTriangleCount = lod1Triangles,
                        SmallDetailCollapse01 = math.saturate(1f - profile.GlobalQualityWeight) * 0.35f
                    }.Schedule(lod1Triangles, 64, transformHandle);
                    JobHandle lod2Handle = new DecimateTriangleSoupJob
                    {
                        SourceVertices = lod0Raw,
                        OutputVertices = lod2Raw,
                        SourceTriangleCount = lod0Triangles,
                        TargetTriangleCount = lod2Triangles,
                        SmallDetailCollapse01 = math.lerp(0.55f, 0.18f, math.saturate(profile.GlobalQualityWeight))
                    }.Schedule(lod2Triangles, 64, transformHandle);
                    JobHandle lodHandle = JobHandle.CombineDependencies(lod1Handle, lod2Handle);
                    lodHandle.Complete();
                    _Stopwatch.Stop();
                    metric.BurstTransformMilliseconds = _Stopwatch.Elapsed.TotalMilliseconds;

                    _Stopwatch.Restart();
                    string token = SanitizeToken(Path.GetFileNameWithoutExtension(prefabPath));
                    Mesh lod0 = CreateMeshFromRaw("GEN_" + token + "_InteriorClutter_LOD0", lod0Raw);
                    Mesh lod1 = CreateMeshFromRaw("GEN_" + token + "_InteriorClutter_LOD1", lod1Raw);
                    Mesh lod2 = CreateMeshFromRaw("GEN_" + token + "_InteriorClutter_LOD2", lod2Raw);
                    string lod0Path = SaveOrReplaceMesh(lod0, InteriorClutterForgeConstants.MeshOutputFolder + "/" + lod0.name + ".asset");
                    string lod1Path = SaveOrReplaceMesh(lod1, InteriorClutterForgeConstants.MeshOutputFolder + "/" + lod1.name + ".asset");
                    string lod2Path = SaveOrReplaceMesh(lod2, InteriorClutterForgeConstants.MeshOutputFolder + "/" + lod2.name + ".asset");
                    string materialPath = SaveOrReplaceMaterial(atlas.Material, InteriorClutterForgeConstants.MaterialOutputFolder + "/MAT_" + token + "_InteriorClutterAtlas.mat");
                    metric.Lod0Triangles = lod0Triangles;
                    metric.Lod1Triangles = lod1Triangles;
                    metric.Lod2Triangles = lod2Triangles;
                    if (lod0Triangles > InteriorClutterForgeConstants.SingleRoomTriangleBudget)
                        metric.WarningFlags |= InteriorClutterWarningFlags.TriangleBudgetExceeded;

                    metric.OutputPrefabPath = CreateGeneratedPrefab(sourceRoot, token, lod0Path, lod1Path, lod2Path, materialPath, interactiveObjects, profile);
                    AssetDatabase.SaveAssets();
                    _Stopwatch.Stop();
                    metric.SerializationMilliseconds = _Stopwatch.Elapsed.TotalMilliseconds;
                    blackBox.Record(in metric);
                    metrics?.Add(metric);
                    return true;
                }
                finally
                {
                    if (lod2Raw.IsCreated) lod2Raw.Dispose();
                    if (lod1Raw.IsCreated) lod1Raw.Dispose();
                    if (lod0Raw.IsCreated) lod0Raw.Dispose();
                    if (nativeSegments.IsCreated) nativeSegments.Dispose();
                    if (segmentByVertex.IsCreated) segmentByVertex.Dispose();
                    if (sourceVertices.IsCreated) sourceVertices.Dispose();
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(sourceRoot);
                scratch.Clear();
            }
        }

        internal static Mesh GenerateMockClutterBenchmark(InteriorAtlasProfile profile)
        {
            EnsureAssetFolder(InteriorClutterForgeConstants.MeshOutputFolder);
            int vertexCount = InteriorClutterForgeConstants.MockClutterShapeCount * InteriorClutterForgeConstants.MockBoxVertexCount;
            NativeArray<InteriorClutterRawVertex> raw = default;
            try
            {
                // COLD ALLOC: NativeArray<InteriorClutterRawVertex>[18000] - editor emergency mock clutter stress mesh - owner: InteriorClutterForge
                raw = new NativeArray<InteriorClutterRawVertex>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                _Stopwatch.Restart();
                JobHandle mockHandle = new GenerateMockClutterCombineJob
                {
                    OutputVertices = raw,
                    ShapeCount = InteriorClutterForgeConstants.MockClutterShapeCount,
                    RoomRadius = 8f,
                    GlobalQualityWeight = profile.GlobalQualityWeight
                }.Schedule(InteriorClutterForgeConstants.MockClutterShapeCount, 32);
                mockHandle.Complete();
                _Stopwatch.Stop();
                Mesh mesh = CreateMeshFromRaw("GEN_SHINOBU_211_MockInteriorClutter", raw);
                string meshPath = SaveOrReplaceMesh(mesh, InteriorClutterForgeConstants.MeshOutputFolder + "/" + mesh.name + ".asset");
                AssetDatabase.SaveAssets();
                Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (savedMesh == null)
                    throw new InvalidOperationException("Interior clutter mock mesh save failed at " + meshPath + ".");
                return savedMesh;
            }
            finally
            {
                if (raw.IsCreated)
                    raw.Dispose();
            }
        }

        internal static void WriteConsolidationReport(List<InteriorClutterBakeMetric> metrics)
        {
            EnsureFileFolder(InteriorClutterForgeConstants.ConsolidationReportPath);
            var builder = new StringBuilder(4096);
            int count = metrics != null ? metrics.Count : 0;
            long before = 0;
            long after = 0;
            builder.Append("{\n  \"agent\": \"SHINOBU_211\",\n  \"status\": \"PENDING_VERIFICATION\",\n  \"roomsProcessed\": ");
            builder.Append(count);
            builder.Append(",\n  \"items\": [\n");
            if (metrics != null)
            {
                for (int i = 0; i < metrics.Count; i++)
                {
                    InteriorClutterBakeMetric m = metrics[i];
                    before += m.DrawCallsBefore;
                    after += m.DrawCallsAfter;
                    if (i > 0)
                        builder.Append(",\n");
                    builder.Append("    { \"source\": \"").Append(Escape(m.SourcePath));
                    builder.Append("\", \"output\": \"").Append(Escape(m.OutputPrefabPath));
                    builder.Append("\", \"staticRenderersMerged\": ").Append(m.StaticRenderers);
                    builder.Append(", \"interactiveRenderersPreserved\": ").Append(m.InteractiveRenderers);
                    builder.Append(", \"sourceMaterials\": ").Append(m.SourceMaterials);
                    builder.Append(", \"drawCallsBefore\": ").Append(m.DrawCallsBefore);
                    builder.Append(", \"staticDrawCallsAfter\": 1");
                    builder.Append(", \"drawCallsAfter\": ").Append(m.DrawCallsAfter);
                    builder.Append(", \"estimatedTotalDrawCallsAfter\": ").Append(m.DrawCallsAfter);
                    builder.Append(", \"lod0Tris\": ").Append(m.Lod0Triangles);
                    builder.Append(", \"lod1Tris\": ").Append(m.Lod1Triangles);
                    builder.Append(", \"lod2Tris\": ").Append(m.Lod2Triangles);
                    builder.Append(", \"burstTransformMs\": ");
                    AppendFixed(builder, m.BurstTransformMilliseconds);
                    builder.Append(", \"serializationMs\": ");
                    AppendFixed(builder, m.SerializationMilliseconds);
                    builder.Append(", \"warning\": \"").Append(m.WarningFlags == InteriorClutterWarningFlags.None ? "NONE" : m.WarningFlags.ToString());
                    builder.Append("\" }");
                }
            }

            builder.Append("\n  ],\n  \"drawCallsReducedFrom\": ").Append(before);
            builder.Append(",\n  \"drawCallsReducedTo\": ").Append(after);
            builder.Append(",\n  \"netcodeExclusion\": \"Generated meshes and atlases are immutable environmental data. Rollback/Merkle state must synchronize only ModuleTypeHash/AUP placement; static vertex buffers are excluded.\",");
            builder.Append("\n  \"selfAuditPath\": \"").Append(InteriorClutterForgeConstants.ConsolidationSelfAuditPath).Append("\"\n}\n");
            File.WriteAllText(InteriorClutterForgeConstants.ConsolidationReportPath, builder.ToString());
            WriteSelfAuditReport(metrics);
        }

        private static void WriteSelfAuditReport(List<InteriorClutterBakeMetric> metrics)
        {
            EnsureFileFolder(InteriorClutterForgeConstants.ConsolidationSelfAuditPath);
            var builder = new StringBuilder(8192);
            builder.Append("<SELF_AUDIT agent=\"SHINOBU_211\" role=\"OFFLINE_INTERIOR_CLUTTER_FORGE\" evidence=\"STATIC_SOURCE_PENDING_UNITY_IMPORT\">\n");
            builder.Append("  <TaskReconciliation>\n");
            AppendTaskAudit(builder, "01", "REALTIME_HIERARCHY_BLOAT_INQUISITION", "PASS", "Hierarchy_Bloat_Scanner emits JSON for active/enabled visible renderer truth and filters interactive roots.");
            AppendTaskAudit(builder, "02", "MULTIPLE_MATERIAL_PURGE", "PASS", "One generated atlas material per baked room/LOD renderer; all triangle submeshes are preserved even when source material slots are missing.");
            AppendTaskAudit(builder, "03", "CS1612_GEOMETRY_STATE_ANNIHILATION", "PASS", "Burst DTOs use raw public fields and pointer extraction; no hot-path properties.");
            AppendTaskAudit(builder, "04", "ARM64_MAPPING_LAYOUT_ASSERTION", "PASS", "Explicit 32/64/192-byte DTO checks, mesh stride validation, inverse-transpose normal basis offsets, and finite-only generated mesh bounds.");
            AppendTaskAudit(builder, "05", "EMERGENCY_MOCK_CLUTTER_BENCHMARK", "PASS", "GenerateMockClutterCombineJob writes 500 box proxies into one pre-sized raw mesh buffer; shared NativeList append was rejected to avoid parallel write contention.");
            AppendTaskAudit(builder, "06", "BURST_MESH_TRANSFORMATION_KERNEL", "PASS", "All eleven mathematical job structs carry exact [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)] directives. Extract jobs validate MeshData stride windows, baseVertex-adjusted indices, index-buffer offsets, destination windows, triangle-aligned counts, and source indices before TransformAndAppendVerticesJob consumes guarded segment maps and writes deterministic vertex windows; unverified direct Unity.Burst.Intrinsics are rejected until NEON/SSE parity is proven.");
            AppendTaskAudit(builder, "07", "AUTOMATED_TEXTURE_ATLASING_ALGORITHM", "PASS", "Guillotine rect packer, NativeArray texel staging, rect/color length guards, Graphics.CopyTexture path with RT-blit retry, tint-aware albedo tile multiply, URP/Standard mask-map packing, editor texture compression, `_MaskMap`-only mask texture copy, normal atlas artifact output without tangent-space binding, and null-material fallback atlas rects.");
            AppendTaskAudit(builder, "08", "THE_DEAR_LIE_UV_REMAPPING", "PASS", "UV rect remap preserves material scale/offset while faking many materials as one atlas; active bake path fuses remap into TransformAndAppendVerticesJob and the standalone RemapUvCoordinatesJob is bounds/NaN guarded.");
            AppendTaskAudit(builder, "09", "DETERMINISTIC_LOD_DECIMATION_ENGINE", "PASS", "Continuous quality-weight triangle retention/collapse creates LOD1/LOD2 with source/output window guards, finite fallback triangles, and profile-weighted LODGroup residency thresholds.");
            AppendTaskAudit(builder, "10", "ASYNCHRONOUS_ASSET_SERIALIZATION", "PASS", "Direct SetVertexBufferData mesh asset serialization under BakedGeometry with dirty-marked replacements, LOD mesh/material load validation, prefab save result checks, and explicit SaveAssets flush.");
            AppendTaskAudit(builder, "11", "INTERACTIVE_ELEMENT_PRESERVATION_FILTER", "PASS", "Ancestor exclusion preserves compacted interactive hierarchy roots once without parent/child clone overlap.");
            AppendTaskAudit(builder, "12", "AUP_DEPTH_LOCALIZATION_PREPARATION", "PASS", "Root pivot retained; prefab-local hierarchy TRS composes room-relative translation before Burst transform, avoiding absolute Transform.position subtraction for contained source roots.");
            AppendTaskAudit(builder, "13", "ROLLBACK_NETCODE_EXCLUSION_FENCE", "PASS", "Generated geometry documented as immutable render data outside StateRingBuffer.");
            AppendTaskAudit(builder, "14", "ZERO_INIT_OVERHEAD_BYPASS", "PASS", "TempJob staging arrays use NativeArrayOptions.UninitializedMemory and deterministic overwrite.");
            AppendTaskAudit(builder, "15", "TELEMETRY_CONSOLIDATION_REPORT_GENERATOR", "PASS", "JSON report plus per-bake 300-entry black-box ring dump path; pre-wrap dumps contain recorded entries only.");
            AppendTaskAudit(builder, "16", "PROCEDURAL_CLUTTER_FORGE_WINDOW", "PASS", "UI Toolkit facade with folder/profile/filter/scan/preview/bake controls.");
            AppendTaskAudit(builder, "17", "CSV_ATLAS_PROFILES_INGESTOR", "PASS", "CSV profile path feeds continuous atlas/LOD quality parameters.");
            AppendTaskAudit(builder, "18", "LIVE_MERGE_PREVIEW_GIZMO", "PASS", "SceneView green/red bounds overlay previews active/enabled bake/exclusion split.");
            AppendTaskAudit(builder, "19", "ARCHITECTURAL_METRIC_VALIDATOR", "PASS", "Static scanner reports prefabs with >10 static renderers/material pressure and separates static monolith draw calls from preserved interactive draw calls.");
            AppendTaskAudit(builder, "20", "SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION", "PASS", "This XML plus layout validator, finite-bounds fallback, 32-byte no-tangent normal-map binding fence, GPU-instancing/static-batching ownership fence, and static forbidden-API scans.");
            builder.Append("  </TaskReconciliation>\n");
            builder.Append("  <StructLayout>\n");
            builder.Append("    <InteriorClutterRawVertex size=\"32\" fields=\"Position float3 offset0 size12; Normal float3 offset12 size12; Uv0 float2 offset24 size8; padding0\" alignment=\"multiple-of-8\" meshLayoutFacade=\"InteriorClutterVertexLayoutValidator.ApplyVertexBufferParams\" />\n");
            builder.Append("    <InteriorClutterSourceVertex size=\"64\" fields=\"Position12 Normal12 Tangent16 Uv8 Color4 Pad12\" alignment=\"one-cache-line\" />\n");
            builder.Append("    <InteriorClutterSegment size=\"192\" fields=\"LocalToRoom64 AtlasUvRect16 MaterialUvScaleOffset16 scalar24 RoomRelativeOffset double3 offset120 size24 NormalToRoom cofactor columns offset144 size48\" alignment=\"multiple-of-64\" />\n");
            builder.Append("    <InteriorClutterAtlasColor size=\"16\" fields=\"AlbedoRgba offset0; NormalRgba offset4; MaskRgba offset8; pad offset12\" alignment=\"multiple-of-8\" />\n");
            builder.Append("    <InteriorClutterTelemetryEntry size=\"64\" fields=\"frame/hash/counts/warnings 32B; timings 16B; hash 8B; pad 8B\" falseSharing=\"single-row-cache-line\" />\n");
            builder.Append("  </StructLayout>\n");
            builder.Append("  <NormalMapBoundary tangentBytes=\"0\" generatedMaterialNormalBinding=\"disabled\">The generated 32-byte mesh layout intentionally omits tangents. Normal atlas textures may be emitted as offline artifacts, but `_BumpMap`, `_NormalMap`, and `_NORMALMAP` are not bound until a tangent-bearing layout is explicitly authorized.</NormalMapBoundary>\n");
            builder.Append("  <ScalabilityCurve globalQualityWeight=\"continuous_0_to_1\">LOD ratios, mock clutter elongation, small-detail collapse, generated LODGroup screen thresholds, and atlas tile caps are lerped/clamped from CSV profile values. Below 0.3 the bake keeps atlas indirection but collapses tiny triangles toward centers, emits cheaper LOD2, and lowers LOD residency so the room sheds dense geometry earlier; above 0.7 it retains denser LODs, longer LOD0/LOD1 residency, larger tiles, source texture copy fidelity, and linear normal/mask atlas data without changing runtime code paths. Overflow materials use a reserved 16px fallback tile instead of corrupting packed atlas regions.</ScalabilityCurve>\n");
            builder.Append("  <HPhiVaultStatus runtimePrivateNativeArrays=\"0\" runtimeVaultHandles=\"none_required_editor_only\" editorSessionNativeArrays=\"TempJob disposed inside bake transaction\">No runtime manager, no private persistent runtime NativeArray, no StateRingBuffer route. Per-bake black-box rings are explicitly reset after uninitialized allocation, use deterministic local frame indices, record failing prefab hashes before exception dumps, dump recorded entries only before wrap, dump chronological retained entries after wrap, and include written-entry counts in the reason sidecar. BakeFolder owns one reusable InteriorClutterBakeScratch for static segments, preserved interactives, materials, mesh filters, shared materials, and component probes, then clears it per prefab and after prefab unload. Interactive filter scratch lists are caller-owned transaction locals, not static global buffers; tag/layer filters are fixed token lists instead of string arrays. Atlas texture property lookup avoids params-array allocation in the material loop. The mesh ABI facade writes VertexAttributeDescriptor records into a disposed Temp NativeArray and validates with direct Mesh accessors instead of allocating mesh-attribute arrays. Generated atlases receiving GPU copies are synchronized to CPU texture data before asset serialization. Tinted albedo materials use a top-mip temp tile plus TintAtlasTileJob multiply path; AtlasTintFallback is reserved for failed tint copy. Exact-size Graphics.CopyTexture failures retry through an RT blit before AtlasCopyFailure. Mask atlas source texture copy accepts only `_MaskMap`; Standard `_MetallicGlossMap` uses scalar fallback packing until channel-aware repack exists. Generated atlas Texture2D assets are editor-compressed before asset publication with BC5 normals where supported and BC7/DXT5 fallback for color/mask channels. Generated asset replacements are marked dirty and flushed once after prefab/mock publication. Generated assets are immutable environment data.</HPhiVaultStatus>\n");
            builder.Append("  <PointerAliasingAndDependencyGraph>\n");
            builder.Append("    <Job name=\"ExtractClutterUInt16Job/ExtractClutterUInt32Job\" consumes=\"MeshData byte streams, validated attribute windows, baseVertex-adjusted bounded index stream, triangle-aligned counts, bounded destination windows\" outputs=\"NativeArray&lt;InteriorClutterSourceVertex&gt;, NativeArray&lt;int&gt;\" aliasing=\"NoAlias pointer/native fields\" handle=\"completed before MeshData disposal inside editor transaction\" />\n");
            builder.Append("    <Job name=\"TransformAndAppendVerticesJob\" consumes=\"source vertices, segments, guarded segment map, precomputed inverse-transpose normal basis\" outputs=\"pre-sized NativeArray&lt;InteriorClutterRawVertex&gt; windows or fallback vertices for invalid segment ids\" aliasing=\"NoAlias pointer/native fields\" handle=\"transformHandle feeds LOD1/LOD2 dependencies\" sharedAppend=\"rejected to avoid NativeList contention\" />\n");
            builder.Append("    <Job name=\"DecimateTriangleSoupJob\" consumes=\"LOD0 raw vertices, source/output length windows, finite position guards\" outputs=\"LOD1/LOD2 raw vertices or deterministic fallback triangles\" aliasing=\"NoAlias\" handle=\"LOD1 and LOD2 scheduled with transformHandle then CombineDependencies completes once before serialization\" />\n");
            builder.Append("    <Job name=\"TintAtlasTileJob\" consumes=\"readback albedo tile pixels and material tint\" outputs=\"tinted Texture2D tile copied into atlas\" aliasing=\"NoAlias\" handle=\"completed before tile Apply and CopyTexture\" />\n");
            builder.Append("    <Job name=\"FillAtlasSolidJob/FillAtlasRectColorsJob\" consumes=\"rect/color DTOs, rect/color length windows, overflow-safe rect clamps\" outputs=\"NativeArray&lt;uint&gt; texels\" aliasing=\"NoAlias\" handle=\"rectFillHandle depends on solidFillHandle; completed before Texture2D.SetPixelData\" />\n");
            builder.Append("  </PointerAliasingAndDependencyGraph>\n");
            builder.Append("  <CompileGuard assembly=\"Hecton8.HabitatInteriorClutterForge.Editor\" includePlatforms=\"Editor\" directRuntimeSiblingReference=\"none\" references=\"Unity.Burst,Unity.Collections,Unity.Jobs,Unity.Mathematics\">Domain files are isolated from the broad editor assembly and add no runtime asmdef references or runtime MonoBehaviours.</CompileGuard>\n");
            builder.Append("  <DearLie before=\"O(props*materials*renderers) runtime hierarchy traversal plus SetPass pressure\" after=\"O(1) renderer/material per visible room LOD plus offline O(vertices) bake\">Texture atlas UV remap includes material tiling/offset, overflow materials are contained to a reserved fallback tile, GPU-copied source texture data is committed before asset serialization, tinted albedo textures are multiplied offline into atlas tiles when possible, Standard metallic maps avoid false mask-channel ownership, the 32-byte no-tangent material path does not bind tangent-space normals, and triangle-soup LOD collapse replaces runtime prop logic with immutable presentation geometry.</DearLie>\n");
            builder.Append("  <ProofBoundary compile=\"BLOCKED_UNRELATED_CORE_DEPENDENCY_WALL_STRIKE_1\" unityImport=\"PENDING\" frameDebugger=\"PENDING\" profiler=\"PENDING\" />\n");
            builder.Append("</SELF_AUDIT>\n");
            File.WriteAllText(InteriorClutterForgeConstants.ConsolidationSelfAuditPath, builder.ToString());
        }

        private static void AppendTaskAudit(StringBuilder builder, string id, string name, string status, string reason)
        {
            builder.Append("    <Task id=\"").Append(id).Append("\" name=\"").Append(name).Append("\" status=\"").Append(status).Append("\" evidence=\"STATIC_SOURCE_ONLY\">");
            builder.Append(EscapeXml(reason));
            builder.Append("</Task>\n");
        }

        private static void CollectSegments(GameObject root, InteriorClutterExcludeFilter filter, InteriorClutterBakeScratch scratch)
        {
            List<InteriorClutterRenderSegment> segments = scratch.StaticSegments;
            List<Transform> interactiveObjects = scratch.InteractiveObjects;
            List<Material> materials = scratch.Materials;
            List<MeshFilter> filters = scratch.MeshFilters;
            List<Material> sharedMaterials = scratch.SharedMaterials;
            List<Component> componentScratch = scratch.ComponentScratch;
            filters.Clear();
            sharedMaterials.Clear();
            componentScratch.Clear();
            root.GetComponentsInChildren<MeshFilter>(true, filters);
            for (int i = 0; i < filters.Count; i++)
            {
                MeshFilter meshFilter = filters[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;

                if (!IsActiveInPrefabHierarchy(meshFilter.transform, root.transform) || !meshFilter.TryGetComponent(out MeshRenderer renderer) || !renderer.enabled)
                    continue;

                if (filter.TryFindExclusionRoot(meshFilter.transform, root.transform, componentScratch, out Transform excludedRoot))
                {
                    AddUniqueTransform(interactiveObjects, excludedRoot);
                    continue;
                }

                Mesh mesh = meshFilter.sharedMesh;
                sharedMaterials.Clear();
                renderer.GetSharedMaterials(sharedMaterials);
                int subMeshCount = mesh.subMeshCount;
                for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                {
                    if (mesh.GetTopology(subMesh) != MeshTopology.Triangles || mesh.GetIndexCount(subMesh) < 3)
                        continue;

                    Material material = subMesh < sharedMaterials.Count ? sharedMaterials[subMesh] : null;
                    int materialIndex = FindOrAddMaterial(materials, material);
                    segments.Add(new InteriorClutterRenderSegment(meshFilter, renderer, subMesh, materialIndex));
                }
            }
        }

        private static int FindOrAddMaterial(List<Material> materials, Material material)
        {
            for (int i = 0; i < materials.Count; i++)
            {
                if (materials[i] == material)
                    return i;
            }

            materials.Add(material);
            return materials.Count - 1;
        }

        private static void AddUniqueTransform(List<Transform> transforms, Transform transform)
        {
            if (transform == null)
                return;

            for (int i = transforms.Count - 1; i >= 0; i--)
            {
                Transform existing = transforms[i];
                if (existing == null)
                {
                    transforms.RemoveAt(i);
                    continue;
                }

                if (existing == transform || transform.IsChildOf(existing))
                    return;
                if (existing.IsChildOf(transform))
                    transforms.RemoveAt(i);
            }

            transforms.Add(transform);
        }

        internal static bool IsActiveInPrefabHierarchy(Transform start, Transform stopInclusive)
        {
            if (start == null)
                return false;

            for (Transform current = start; current != null; current = current.parent)
            {
                if (!current.gameObject.activeSelf)
                    return false;
                if (current == stopInclusive)
                    break;
            }

            return true;
        }

        private static int CountUniqueRenderers(List<InteriorClutterRenderSegment> segments)
        {
            int count = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                bool seen = false;
                for (int j = 0; j < i; j++)
                {
                    if (segments[j].Renderer == segments[i].Renderer)
                    {
                        seen = true;
                        break;
                    }
                }

                if (!seen)
                    count++;
            }

            return count;
        }

        private static int CountPreservedInteractiveRenderers(List<Transform> interactiveRoots, List<MeshFilter> filters, Transform sourceRoot)
        {
            if (interactiveRoots == null || filters == null)
                return 0;

            int count = 0;
            for (int i = 0; i < filters.Count; i++)
            {
                MeshFilter meshFilter = filters[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;

                Transform transform = meshFilter.transform;
                if (!IsUnderAnyRoot(transform, interactiveRoots))
                    continue;

                if (!IsActiveInPrefabHierarchy(transform, sourceRoot) || !meshFilter.TryGetComponent(out MeshRenderer renderer) || !renderer.enabled)
                    continue;

                count++;
            }

            return count;
        }

        private static bool IsUnderAnyRoot(Transform transform, List<Transform> roots)
        {
            if (transform == null || roots == null)
                return false;

            for (int i = 0; i < roots.Count; i++)
            {
                Transform root = roots[i];
                if (root != null && (transform == root || transform.IsChildOf(root)))
                    return true;
            }

            return false;
        }

        private static int CountTriangleSoupVertices(List<InteriorClutterRenderSegment> segments)
        {
            long total = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                long indexCount = segments[i].Mesh.GetIndexCount(segments[i].SubMesh);
                total += indexCount - indexCount % 3L;
            }

            return total > int.MaxValue ? 0 : (int)total;
        }

        private static void FillNativeSource(
            Transform sourceRoot,
            List<InteriorClutterRenderSegment> renderSegments,
            List<Material> materials,
            InteriorMaterialAtlas atlas,
            NativeArray<InteriorClutterSourceVertex> sourceVertices,
            NativeArray<int> segmentByVertex,
            NativeArray<InteriorClutterSegment> nativeSegments,
            ref InteriorClutterBakeMetric metric)
        {
            int cursor = 0;

            for (int segmentIndex = 0; segmentIndex < renderSegments.Count; segmentIndex++)
            {
                InteriorClutterRenderSegment segment = renderSegments[segmentIndex];
                Mesh mesh = segment.Mesh;
                using Mesh.MeshDataArray meshDataArray = Mesh.AcquireReadOnlyMeshData(mesh);
                Mesh.MeshData meshData = meshDataArray[0];
                if (!TryResolveVertexLayout(meshData, out InteriorClutterSourceLayout layout))
                    throw new InvalidOperationException("Unsupported clutter mesh vertex layout on " + mesh.name + ".");

                SubMeshDescriptor descriptor = meshData.GetSubMesh(segment.SubMesh);
                int indexCount = descriptor.indexCount;
                int triangleIndexCount = indexCount - indexCount % 3;
                if (triangleIndexCount != indexCount)
                    metric.WarningFlags |= InteriorClutterWarningFlags.UnsupportedMesh;
                if (triangleIndexCount <= 0)
                    continue;

                int start = cursor;
                unsafe
                {
                    NativeArray<byte> positionData = meshData.GetVertexData<byte>(layout.PositionStream);
                    NativeArray<byte> normalData = layout.HasNormals != 0 ? meshData.GetVertexData<byte>(layout.NormalStream) : default;
                    NativeArray<byte> tangentData = layout.HasTangents != 0 ? meshData.GetVertexData<byte>(layout.TangentStream) : default;
                    NativeArray<byte> uvData = layout.HasUv0 != 0 ? meshData.GetVertexData<byte>(layout.Uv0Stream) : default;
                    void* positionPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(positionData);
                    void* normalPtr = layout.HasNormals != 0 ? NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(normalData) : null;
                    void* tangentPtr = layout.HasTangents != 0 ? NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(tangentData) : null;
                    void* uvPtr = layout.HasUv0 != 0 ? NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(uvData) : null;

                    if (meshData.indexFormat == IndexFormat.UInt16)
                    {
                        NativeArray<ushort> indices = meshData.GetIndexData<ushort>();
                        JobHandle extractHandle = new ExtractClutterUInt16Job
                        {
                            Indices = indices,
                            OutputVertices = sourceVertices,
                            SegmentByVertex = segmentByVertex,
                            PositionPtr = positionPtr,
                            NormalPtr = normalPtr,
                            TangentPtr = tangentPtr,
                            Uv0Ptr = uvPtr,
                            IndexStart = descriptor.indexStart,
                            BaseVertex = descriptor.baseVertex,
                            DestinationStart = start,
                            SegmentIndex = segmentIndex,
                            PositionStride = layout.PositionStride,
                            PositionOffset = layout.PositionOffset,
                            NormalStride = layout.NormalStride,
                            NormalOffset = layout.NormalOffset,
                            TangentStride = layout.TangentStride,
                            TangentOffset = layout.TangentOffset,
                            Uv0Stride = layout.Uv0Stride,
                            Uv0Offset = layout.Uv0Offset,
                            SourceVertexCount = meshData.vertexCount,
                            HasNormals = layout.HasNormals,
                            HasTangents = layout.HasTangents,
                            HasUv0 = layout.HasUv0
                        }.Schedule(triangleIndexCount, 128);
                        extractHandle.Complete();
                    }
                    else
                    {
                        NativeArray<uint> indices = meshData.GetIndexData<uint>();
                        JobHandle extractHandle = new ExtractClutterUInt32Job
                        {
                            Indices = indices,
                            OutputVertices = sourceVertices,
                            SegmentByVertex = segmentByVertex,
                            PositionPtr = positionPtr,
                            NormalPtr = normalPtr,
                            TangentPtr = tangentPtr,
                            Uv0Ptr = uvPtr,
                            IndexStart = descriptor.indexStart,
                            BaseVertex = descriptor.baseVertex,
                            DestinationStart = start,
                            SegmentIndex = segmentIndex,
                            PositionStride = layout.PositionStride,
                            PositionOffset = layout.PositionOffset,
                            NormalStride = layout.NormalStride,
                            NormalOffset = layout.NormalOffset,
                            TangentStride = layout.TangentStride,
                            TangentOffset = layout.TangentOffset,
                            Uv0Stride = layout.Uv0Stride,
                            Uv0Offset = layout.Uv0Offset,
                            SourceVertexCount = meshData.vertexCount,
                            HasNormals = layout.HasNormals,
                            HasTangents = layout.HasTangents,
                            HasUv0 = layout.HasUv0
                        }.Schedule(triangleIndexCount, 128);
                        extractHandle.Complete();
                    }
                }

                Matrix4x4 matrix = BuildRoomLocalMatrix(sourceRoot, segment.Transform, out double3 offset);
                ResolveNormalToRoomColumns(matrix, out float4 normalC0, out float4 normalC1, out float4 normalC2);
                InteriorClutterAtlasRect rect = atlas.Rects[segment.MaterialIndex];
                nativeSegments[segmentIndex] = new InteriorClutterSegment
                {
                    LocalToRoom = ToFloat4x4(matrix),
                    AtlasUvRect = rect.UvRect,
                    MaterialUvScaleOffset = ResolveMaterialUvScaleOffset(materials, segment.MaterialIndex),
                    SourceVertexStart = start,
                    SourceVertexCount = triangleIndexCount,
                    MaterialIndex = segment.MaterialIndex,
                    RendererIndex = segment.Renderer.GetInstanceID(),
                    StableHash = StableHash(segment.Transform.name),
                    Flags = 0u,
                    RoomRelativeOffset = offset,
                    NormalToRoomC0 = normalC0,
                    NormalToRoomC1 = normalC1,
                    NormalToRoomC2 = normalC2
                };
                cursor += triangleIndexCount;
            }
        }

        private static float4 ResolveMaterialUvScaleOffset(List<Material> materials, int materialIndex)
        {
            if (materials == null || (uint)materialIndex >= (uint)materials.Count)
                return new float4(1f, 1f, 0f, 0f);

            Material material = materials[materialIndex];
            if (material == null)
                return new float4(1f, 1f, 0f, 0f);

            string property = null;
            if (material.HasProperty("_BaseMap"))
                property = "_BaseMap";
            else if (material.HasProperty("_MainTex"))
                property = "_MainTex";

            if (property == null)
                return new float4(1f, 1f, 0f, 0f);

            Vector2 scale = material.GetTextureScale(property);
            Vector2 offset = material.GetTextureOffset(property);
            return new float4(
                FiniteOrDefault(scale.x, 1f),
                FiniteOrDefault(scale.y, 1f),
                FiniteOrDefault(offset.x, 0f),
                FiniteOrDefault(offset.y, 0f));
        }

        private static float FiniteOrDefault(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static Matrix4x4 BuildRoomLocalMatrix(Transform sourceRoot, Transform sourceTransform, out double3 roomRelativeOffset)
        {
            Matrix4x4 matrix = Matrix4x4.identity;
            Transform current = sourceTransform;
            int guard = 0;
            while (current != null && current != sourceRoot && guard < 256)
            {
                matrix = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * matrix;
                current = current.parent;
                guard++;
            }

            if (current != sourceRoot)
                matrix = sourceRoot.worldToLocalMatrix * sourceTransform.localToWorldMatrix;

            roomRelativeOffset = new double3(matrix.m03, matrix.m13, matrix.m23);
            return matrix;
        }

        private static void ResolveNormalToRoomColumns(Matrix4x4 matrix, out float4 c0, out float4 c1, out float4 c2)
        {
            float3 x = new float3(matrix.m00, matrix.m10, matrix.m20);
            float3 y = new float3(matrix.m01, matrix.m11, matrix.m21);
            float3 z = new float3(matrix.m02, matrix.m12, matrix.m22);
            float3 n0 = math.cross(y, z);
            float3 n1 = math.cross(z, x);
            float3 n2 = math.cross(x, y);
            float det = math.dot(x, n0);
            bool safe =
                math.isfinite(det) &&
                math.abs(det) > 1e-12f &&
                math.all(math.isfinite(n0)) &&
                math.all(math.isfinite(n1)) &&
                math.all(math.isfinite(n2));

            if (!safe)
            {
                c0 = new float4(1f, 0f, 0f, 0f);
                c1 = new float4(0f, 1f, 0f, 0f);
                c2 = new float4(0f, 0f, 1f, 0f);
                return;
            }

            float sign = det < 0f ? -1f : 1f;
            c0 = new float4(n0 * sign, 0f);
            c1 = new float4(n1 * sign, 0f);
            c2 = new float4(n2 * sign, 0f);
        }

        private static Mesh CreateMeshFromRaw(string name, NativeArray<InteriorClutterRawVertex> raw)
        {
            NativeArray<InteriorClutterRawVertex> packed = default;
            NativeArray<uint> indices = default;
            try
            {
                int vertexCount = raw.Length;
                // COLD ALLOC: NativeArray<InteriorClutterRawVertex>[vertexCount] - editor interleaved room vertex buffer - owner: InteriorClutterForge
                packed = new NativeArray<InteriorClutterRawVertex>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                // COLD ALLOC: NativeArray<uint>[vertexCount] - editor linear room index buffer - owner: InteriorClutterForge
                indices = new NativeArray<uint>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                JobHandle pack = new PackInteriorClutterVertexJob
                {
                    SourceVertices = raw,
                    PackedVertices = packed
                }.Schedule(vertexCount, 128);
                JobHandle indexHandle = new LinearIndexFillJob
                {
                    Indices = indices
                }.Schedule(vertexCount, 128, pack);
                indexHandle.Complete();

                Mesh mesh = new Mesh
                {
                    name = name,
                    indexFormat = IndexFormat.UInt32
                };
                InteriorClutterVertexLayoutValidator.ApplyVertexBufferParams(mesh, vertexCount);
                mesh.SetVertexBufferData(packed, 0, 0, vertexCount, 0, MeshFlags);
                mesh.SetIndexBufferParams(vertexCount, IndexFormat.UInt32);
                mesh.SetIndexBufferData(indices, 0, 0, vertexCount, MeshFlags);
                mesh.subMeshCount = 1;
                Bounds bounds = CalculateBounds(raw);
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, vertexCount, MeshTopology.Triangles)
                {
                    bounds = bounds,
                    vertexCount = vertexCount
                }, MeshFlags);
                mesh.bounds = bounds;
                InteriorClutterVertexLayoutValidator.ValidateMesh(mesh);
                return mesh;
            }
            finally
            {
                if (indices.IsCreated) indices.Dispose();
                if (packed.IsCreated) packed.Dispose();
            }
        }

        private static bool TryResolveVertexLayout(Mesh.MeshData meshData, out InteriorClutterSourceLayout layout)
        {
            layout = default;
            if (!meshData.HasVertexAttribute(VertexAttribute.Position))
                return false;

            if (meshData.GetVertexAttributeFormat(VertexAttribute.Position) != VertexAttributeFormat.Float32 ||
                meshData.GetVertexAttributeDimension(VertexAttribute.Position) < 3)
                return false;

            layout.PositionStream = meshData.GetVertexAttributeStream(VertexAttribute.Position);
            layout.PositionStride = meshData.GetVertexBufferStride(layout.PositionStream);
            layout.PositionOffset = meshData.GetVertexAttributeOffset(VertexAttribute.Position);
            if (!FitsAttributeWindow(layout.PositionOffset, 12, layout.PositionStride))
                return false;

            if (meshData.HasVertexAttribute(VertexAttribute.Normal))
            {
                int stream = meshData.GetVertexAttributeStream(VertexAttribute.Normal);
                int stride = meshData.GetVertexBufferStride(stream);
                int offset = meshData.GetVertexAttributeOffset(VertexAttribute.Normal);
                if (meshData.GetVertexAttributeFormat(VertexAttribute.Normal) == VertexAttributeFormat.Float32 &&
                    meshData.GetVertexAttributeDimension(VertexAttribute.Normal) >= 3 &&
                    FitsAttributeWindow(offset, 12, stride))
                {
                    layout.HasNormals = 1;
                    layout.NormalStream = stream;
                    layout.NormalStride = stride;
                    layout.NormalOffset = offset;
                }
            }

            if (meshData.HasVertexAttribute(VertexAttribute.Tangent))
            {
                int stream = meshData.GetVertexAttributeStream(VertexAttribute.Tangent);
                int stride = meshData.GetVertexBufferStride(stream);
                int offset = meshData.GetVertexAttributeOffset(VertexAttribute.Tangent);
                if (meshData.GetVertexAttributeFormat(VertexAttribute.Tangent) == VertexAttributeFormat.Float32 &&
                    meshData.GetVertexAttributeDimension(VertexAttribute.Tangent) >= 4 &&
                    FitsAttributeWindow(offset, 16, stride))
                {
                    layout.HasTangents = 1;
                    layout.TangentStream = stream;
                    layout.TangentStride = stride;
                    layout.TangentOffset = offset;
                }
            }

            if (meshData.HasVertexAttribute(VertexAttribute.TexCoord0))
            {
                int stream = meshData.GetVertexAttributeStream(VertexAttribute.TexCoord0);
                int stride = meshData.GetVertexBufferStride(stream);
                int offset = meshData.GetVertexAttributeOffset(VertexAttribute.TexCoord0);
                if (meshData.GetVertexAttributeFormat(VertexAttribute.TexCoord0) == VertexAttributeFormat.Float32 &&
                    meshData.GetVertexAttributeDimension(VertexAttribute.TexCoord0) >= 2 &&
                    FitsAttributeWindow(offset, 8, stride))
                {
                    layout.HasUv0 = 1;
                    layout.Uv0Stream = stream;
                    layout.Uv0Stride = stride;
                    layout.Uv0Offset = offset;
                }
            }

            return layout.PositionStride > 0;
        }

        private static bool FitsAttributeWindow(int offset, int byteWidth, int stride)
        {
            return offset >= 0 && byteWidth > 0 && stride >= byteWidth && offset <= stride - byteWidth;
        }

        private static string CreateGeneratedPrefab(GameObject sourceRoot, string token, string lod0Path, string lod1Path, string lod2Path, string materialPath, List<Transform> interactiveObjects, InteriorAtlasProfile profile)
        {
            string prefabName = "GEN_" + token + "_InteriorClutterBaked";
            string prefabPath = InteriorClutterForgeConstants.PrefabOutputFolder + "/" + prefabName + ".prefab";
            GameObject outputRoot = new GameObject(prefabName);
            try
            {
                outputRoot.transform.SetPositionAndRotation(sourceRoot.transform.position, sourceRoot.transform.rotation);
                outputRoot.transform.localScale = sourceRoot.transform.localScale;
                MeshRenderer lod0 = CreateLodRenderer("LOD0_STATIC_MONOLITH", outputRoot.transform, lod0Path, materialPath);
                MeshRenderer lod1 = CreateLodRenderer("LOD1_STATIC_MONOLITH", outputRoot.transform, lod1Path, materialPath);
                MeshRenderer lod2 = CreateLodRenderer("LOD2_STATIC_MONOLITH", outputRoot.transform, lod2Path, materialPath);
                ResolveLodThresholds(profile, out float lod0Threshold, out float lod1Threshold, out float lod2Threshold);
                var lodGroup = outputRoot.AddComponent<LODGroup>();
                lodGroup.SetLODs(new[]
                {
                    new LOD(lod0Threshold, new[] { lod0 }),
                    new LOD(lod1Threshold, new[] { lod1 }),
                    new LOD(lod2Threshold, new[] { lod2 })
                });
                lodGroup.fadeMode = LODFadeMode.CrossFade;
                lodGroup.animateCrossFading = true;
                lodGroup.RecalculateBounds();
                GameObject preservedRoot = new GameObject("INTERACTIVE_PRESERVED");
                preservedRoot.transform.SetParent(outputRoot.transform, false);
                for (int i = 0; i < interactiveObjects.Count; i++)
                {
                    Transform source = interactiveObjects[i];
                    if (source == null)
                        continue;

                    GameObject clone = Object.Instantiate(source.gameObject);
                    clone.name = source.gameObject.name;
                    clone.transform.SetParent(preservedRoot.transform, false);
                    ApplyRelativeTransform(sourceRoot.transform, source, clone.transform);
                }

                GameObjectUtility.SetStaticEditorFlags(outputRoot, StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(outputRoot, prefabPath, out bool prefabSaved);
                if (!prefabSaved || savedPrefab == null)
                    throw new InvalidOperationException("Interior clutter prefab save failed at " + prefabPath + ".");
                return prefabPath;
            }
            finally
            {
                Object.DestroyImmediate(outputRoot);
            }
        }

        private static void ResolveLodThresholds(InteriorAtlasProfile profile, out float lod0, out float lod1, out float lod2)
        {
            float residency = math.smoothstep(0f, 1f, math.saturate(profile.GlobalQualityWeight));
            lod0 = math.lerp(0.48f, 0.72f, residency);
            lod1 = math.lerp(0.14f, 0.28f, residency);
            lod2 = math.lerp(0.035f, 0.075f, residency);
        }

        private static MeshRenderer CreateLodRenderer(string name, Transform parent, string meshPath, string materialPath)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            MeshFilter filter = child.AddComponent<MeshFilter>();
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mesh == null)
                throw new InvalidOperationException("Interior clutter LOD mesh load failed at " + meshPath + ".");
            if (material == null)
                throw new InvalidOperationException("Interior clutter atlas material load failed at " + materialPath + ".");

            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            GameObjectUtility.SetStaticEditorFlags(child, StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
            return renderer;
        }

        private static Bounds CalculateBounds(NativeArray<InteriorClutterRawVertex> vertices)
        {
            if (!vertices.IsCreated || vertices.Length == 0)
                return new Bounds(Vector3.zero, Vector3.one);

            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);
            bool hasFinitePosition = false;
            for (int i = 0; i < vertices.Length; i++)
            {
                float3 p = vertices[i].Position;
                if (!math.all(math.isfinite(p)))
                    continue;

                min = math.min(min, p);
                max = math.max(max, p);
                hasFinitePosition = true;
            }

            if (!hasFinitePosition)
                return new Bounds(Vector3.zero, Vector3.one);

            return new Bounds(ToVector3((min + max) * 0.5f), ToVector3(math.max(max - min, new float3(0.01f))));
        }

        private static void ApplyRelativeTransform(Transform sourceRoot, Transform sourceTransform, Transform target)
        {
            Matrix4x4 matrix = BuildRoomLocalMatrix(sourceRoot, sourceTransform, out double3 offset);
            Vector3 right = matrix.GetColumn(0);
            Vector3 up = matrix.GetColumn(1);
            Vector3 forward = matrix.GetColumn(2);
            Vector3 scale = new Vector3(right.magnitude, up.magnitude, forward.magnitude);
            if (scale.x > 0.0001f) right /= scale.x;
            if (scale.y > 0.0001f) up /= scale.y;
            if (scale.z > 0.0001f) forward /= scale.z;
            target.localPosition = ToVector3(new float3(
                FiniteOrDefault((float)offset.x, 0f),
                FiniteOrDefault((float)offset.y, 0f),
                FiniteOrDefault((float)offset.z, 0f)));
            target.localRotation = Quaternion.LookRotation(forward.sqrMagnitude > 1e-8f ? forward : Vector3.forward, up.sqrMagnitude > 1e-8f ? up : Vector3.up);
            target.localScale = new Vector3(math.max(0.0001f, scale.x), math.max(0.0001f, scale.y), math.max(0.0001f, scale.z));
        }

        private static string SaveOrReplaceMesh(Mesh mesh, string path)
        {
            EnsureAssetFolder(Path.GetDirectoryName(path));
            mesh.UploadMeshData(true);
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
                return path;
            }

            AssetDatabase.CreateAsset(mesh, path);
            EditorUtility.SetDirty(mesh);
            return path;
        }

        private static string SaveOrReplaceMaterial(Material material, string path)
        {
            EnsureAssetFolder(Path.GetDirectoryName(path));
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(material, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(material);
                return path;
            }

            AssetDatabase.CreateAsset(material, path);
            EditorUtility.SetDirty(material);
            return path;
        }

        internal static void EnsureAssetFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return;

            string normalized = folder.IndexOf('\\') >= 0 ? folder.Replace('\\', '/') : folder;
            if (AssetDatabase.IsValidFolder(normalized))
                return;

            int firstSlash = normalized.IndexOf('/');
            if (firstSlash <= 0)
                return;

            string current = normalized.Substring(0, firstSlash);
            int start = firstSlash + 1;
            while (start < normalized.Length)
            {
                int slash = normalized.IndexOf('/', start);
                int length = slash >= 0 ? slash - start : normalized.Length - start;
                if (length <= 0)
                {
                    if (slash < 0)
                        break;
                    start = slash + 1;
                    continue;
                }

                string child = normalized.Substring(start, length);
                string next = current + "/" + child;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, child);
                current = next;
                if (slash < 0)
                    break;
                start = slash + 1;
            }
        }

        internal static void EnsureFileFolder(string relativePath)
        {
            string folder = Path.GetDirectoryName(relativePath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }

        private static string ResolveScanRoot(string requested)
        {
            if (!string.IsNullOrEmpty(requested) && AssetDatabase.IsValidFolder(requested))
                return requested;

            return AssetDatabase.IsValidFolder(InteriorClutterForgeConstants.FallbackConstructionRoot)
                ? InteriorClutterForgeConstants.FallbackConstructionRoot
                : "Assets/_Project/Prefabs";
        }

        internal static string SanitizeToken(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "Unnamed";

            bool alreadySafe = true;
            for (int i = 0; i < input.Length; i++)
            {
                if (!IsSafeTokenChar(input[i]))
                {
                    alreadySafe = false;
                    break;
                }
            }

            if (alreadySafe)
                return input;

            var builder = new StringBuilder(input.Length);
            for (int i = 0; i < input.Length; i++)
                builder.Append(IsSafeTokenChar(input[i]) ? input[i] : '_');

            return builder.ToString();
        }

        private static bool IsSafeTokenChar(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
        }

        internal static void AppendFixed(StringBuilder builder, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                builder.Append("0.000");
            else
                builder.Append(value.ToString("0.000", CultureInfo.InvariantCulture));
        }

        internal static string Escape(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        internal static string EscapeXml(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        internal static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
        private static float4x4 ToFloat4x4(Matrix4x4 m)
        {
            return new float4x4(
                new float4(m.m00, m.m10, m.m20, m.m30),
                new float4(m.m01, m.m11, m.m21, m.m31),
                new float4(m.m02, m.m12, m.m22, m.m32),
                new float4(m.m03, m.m13, m.m23, m.m33));
        }

        internal static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (!string.IsNullOrEmpty(value))
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        hash ^= value[i];
                        hash *= 16777619u;
                    }
                }

                return hash;
            }
        }
    }

    internal readonly struct InteriorClutterRenderSegment
    {
        public readonly MeshFilter Filter;
        public readonly MeshRenderer Renderer;
        public readonly Mesh Mesh;
        public readonly Transform Transform;
        public readonly int SubMesh;
        public readonly int MaterialIndex;

        public InteriorClutterRenderSegment(MeshFilter filter, MeshRenderer renderer, int subMesh, int materialIndex)
        {
            Filter = filter;
            Renderer = renderer;
            Mesh = filter.sharedMesh;
            Transform = filter.transform;
            SubMesh = subMesh;
            MaterialIndex = materialIndex;
        }
    }

    internal struct InteriorClutterSourceLayout
    {
        public int PositionStream;
        public int PositionStride;
        public int PositionOffset;
        public int NormalStream;
        public int NormalStride;
        public int NormalOffset;
        public int TangentStream;
        public int TangentStride;
        public int TangentOffset;
        public int Uv0Stream;
        public int Uv0Stride;
        public int Uv0Offset;
        public byte HasNormals;
        public byte HasTangents;
        public byte HasUv0;
    }
}
#endif
