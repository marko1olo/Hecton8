// ============================================================================
// HectonPhysicsSkinGenerator.cs
// Place in: Assets/Editor/HectonPhysicsSkinGenerator.cs
// ============================================================================

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Hecton8.Editor.ColliderOptimization1716;

public class HectonPhysicsSkinGenerator : EditorWindow
{
    private const int MaxProxyTriangles1716 = ColliderOptimizerEngine1716.ProxyMeshTriangleLimit;
    private const string LegacyPhysicsSkinRootName = "PHYSICS_SKIN";
    private const string ConvexProxyRootName1716 = "COL_ConvexProxy_1716";
    private const string CompoundProxyRootName1716 = "COL_CompoundProxy_1716";

    // ═══════════════════════════════════════════════════════════════════
    // UI STATE
    // ═══════════════════════════════════════════════════════════════════
    private GameObject targetObject;
    private float gridPrecision = 1.0f;
    private bool autoGridFromBounds = false;
    private int targetTriCount = 150;

    // Advanced
    private bool showAdvanced = false;
    private float weldThreshold = 0.01f;
    private float minTriangleArea = 0.0001f;
    private bool showWireframe = true;
    private Color wireColor = new Color(0f, 1f, 0.4f, 0.8f);

    // Chunking for huge meshes
    private bool enableChunking = false;
    private float chunkSize = 50f;

    // Batch
    private bool batchMode = false;

    // Stats
    private int sourceTriCount = 0;
    private int generatedTriCount = 0;
    private int sourceVertCount = 0;
    private int generatedVertCount = 0;
    private int chunksGenerated = 0;
    private string lastStatus = "";
    private MessageType lastStatusType = MessageType.None;
    private double lastGenerateTime = 0;
    private Vector3 sourceBoundsSize = Vector3.zero;

    // Preview
    private Mesh previewMesh;
    private GameObject previewTarget;

    // Scroll
    private Vector2 scrollPos;

    // COLD ALLOC: List<Vector3>[65536] - editor mesh source vertex extraction scratch - owner: HectonPhysicsSkinGenerator
    private readonly List<Vector3> _sourceVertices = new List<Vector3>(65536);
    // COLD ALLOC: List<int>[196608] - editor mesh source triangle extraction scratch - owner: HectonPhysicsSkinGenerator
    private readonly List<int> _sourceTriangles = new List<int>(196608);
    // COLD ALLOC: List<int>[196608] - editor mesh submesh triangle extraction scratch - owner: HectonPhysicsSkinGenerator
    private readonly List<int> _sourceSubmeshTriangles = new List<int>(196608);
    // COLD ALLOC: List<Vector3>[65536] - editor chunk vertex scratch - owner: HectonPhysicsSkinGenerator
    private readonly List<Vector3> _chunkVertices = new List<Vector3>(65536);
    // COLD ALLOC: List<int>[196608] - editor chunk triangle scratch - owner: HectonPhysicsSkinGenerator
    private readonly List<int> _chunkTriangles = new List<int>(196608);
    // COLD ALLOC: List<GameObject>[256] - editor batch selection scratch - owner: HectonPhysicsSkinGenerator
    private readonly List<GameObject> _batchObjects = new List<GameObject>(256);
    // COLD ALLOC: List<Vector3>[65536] - editor scene preview vertex extraction scratch - owner: HectonPhysicsSkinGenerator
    private readonly List<Vector3> _previewVertices = new List<Vector3>(65536);
    // COLD ALLOC: List<int>[196608] - editor scene preview triangle extraction scratch - owner: HectonPhysicsSkinGenerator
    private readonly List<int> _previewTriangles = new List<int>(196608);
    // COLD ALLOC: Dictionary<Vector3Int,VoxelAccumulator>[65536] - editor voxel centroid accumulator scratch - owner: HectonPhysicsSkinGenerator
    private readonly Dictionary<Vector3Int, VoxelAccumulator> _voxelAccumulators = new Dictionary<Vector3Int, VoxelAccumulator>(65536);
    // COLD ALLOC: Dictionary<Vector3Int,Vector3>[65536] - editor voxel centroid lookup scratch - owner: HectonPhysicsSkinGenerator
    private readonly Dictionary<Vector3Int, Vector3> _voxelCentroids = new Dictionary<Vector3Int, Vector3>(65536);
    // COLD ALLOC: HashSet<TriKey>[196608] - editor unique triangle scratch - owner: HectonPhysicsSkinGenerator
    private readonly HashSet<TriKey> _uniqueTriangleKeys = new HashSet<TriKey>(196608);
    // COLD ALLOC: List<Vector3Int>[196608] - editor shell voxel scratch - owner: HectonPhysicsSkinGenerator
    private readonly List<Vector3Int> _shellVoxels = new List<Vector3Int>(196608);
    // COLD ALLOC: Dictionary<Vector3Int,int>[65536] - editor voxel index remap scratch - owner: HectonPhysicsSkinGenerator
    private readonly Dictionary<Vector3Int, int> _voxelToIndex = new Dictionary<Vector3Int, int>(65536);
    // COLD ALLOC: List<Vector3>[65536] - editor generated vertex scratch - owner: HectonPhysicsSkinGenerator
    private readonly List<Vector3> _outVertices = new List<Vector3>(65536);
    // COLD ALLOC: List<int>[196608] - editor generated triangle scratch - owner: HectonPhysicsSkinGenerator
    private readonly List<int> _outTriangles = new List<int>(196608);
    // COLD ALLOC: Dictionary<int,int>[65536] - editor chunk vertex remap scratch - owner: HectonPhysicsSkinGenerator
    private readonly Dictionary<int, int> _chunkVertexRemap = new Dictionary<int, int>(65536);
    // COLD ALLOC: List<Vector3Int>[65536] - editor source vertex voxel scratch - owner: HectonPhysicsSkinGenerator
    private readonly List<Vector3Int> _vertexVoxelScratch = new List<Vector3Int>(65536);
    // COLD ALLOC: List<int>[65536] - editor weld remap scratch - owner: HectonPhysicsSkinGenerator
    private readonly List<int> _weldRemap = new List<int>(65536);
    // COLD ALLOC: List<int>[196608] - editor cleaned triangle scratch - owner: HectonPhysicsSkinGenerator
    private readonly List<int> _cleanTriangles = new List<int>(196608);
    // COLD ALLOC: List<int>[65536] - editor used vertex flag scratch - owner: HectonPhysicsSkinGenerator
    private readonly List<int> _usedVertexFlags = new List<int>(65536);
    // COLD ALLOC: List<int>[65536] - editor unused vertex remap scratch - owner: HectonPhysicsSkinGenerator
    private readonly List<int> _unusedRemap = new List<int>(65536);
    // COLD ALLOC: List<Vector3>[65536] - editor compacted vertex scratch - owner: HectonPhysicsSkinGenerator
    private readonly List<Vector3> _compactedVertices = new List<Vector3>(65536);
    // COLD ALLOC: List<Transform>[64] - editor existing physics skin removal scratch - owner: HectonPhysicsSkinGenerator
    private readonly List<Transform> _oldPhysicsSkinChildren = new List<Transform>(64);

    [MenuItem("Hecton8/Physics Skin Generator")]
    public static void ShowWindow()
    {
        var w = GetWindow<HectonPhysicsSkinGenerator>("⛏ Physics Skin");
        w.minSize = new Vector2(420, 600);
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;

        if (IsEditorTransitionBusy())
            return;

        previewMesh = null;
    }

    private static bool IsEditorTransitionBusy()
    {
        return EditorApplication.isCompiling ||
               EditorApplication.isUpdating ||
               EditorApplication.isPlayingOrWillChangePlaymode;
    }

    // ═══════════════════════════════════════════════════════════════════
    // GUI
    // ═══════════════════════════════════════════════════════════════════
    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        EditorGUILayout.Space(8);
        DrawHeader();
        EditorGUILayout.Space(6);

        DrawTargetSection();
        EditorGUILayout.Space(6);

        DrawGridSection();
        EditorGUILayout.Space(6);

        DrawChunkingSection();
        EditorGUILayout.Space(6);

        DrawAdvancedSection();
        EditorGUILayout.Space(8);

        DrawStatsSection();
        EditorGUILayout.Space(6);

        DrawStatusBox();
        EditorGUILayout.Space(8);

        DrawButtons();
        EditorGUILayout.Space(8);

        DrawBatchSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        var style = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("⛏ HECTON PHYSICS SKIN GENERATOR", style);
        EditorGUILayout.LabelField("1716-compatible convex proxy from baked meshes",
            EditorStyles.centeredGreyMiniLabel);
    }

    private void DrawTargetSection()
    {
        EditorGUILayout.LabelField("── TARGET ──", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        targetObject = (GameObject)EditorGUILayout.ObjectField(
            "LOD0 GameObject", targetObject, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            UpdateSourceStats();
        }

        // Quick-fill buttons
        EditorGUILayout.BeginHorizontal();
        if (Selection.activeGameObject != null)
        {
            if (GUILayout.Button("← Use Selection", GUILayout.Height(22)))
            {
                targetObject = Selection.activeGameObject;
                UpdateSourceStats();
            }
        }

        // Try to find LOD0 from parent
        if (Selection.activeGameObject != null)
        {
            if (Selection.activeGameObject.TryGetComponent(out LODGroup lodGroup))
            {
                if (GUILayout.Button("← Find LOD0 from LODGroup", GUILayout.Height(22)))
                {
                    var lods = lodGroup.GetLODs();
                    if (lods.Length > 0 && lods[0].renderers.Length > 0)
                    {
                        targetObject = lods[0].renderers[0].gameObject;
                        UpdateSourceStats();
                    }
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        // Show mesh info
        if (targetObject != null)
        {
            if (!targetObject.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null)
            {
                EditorGUILayout.HelpBox("⚠ No MeshFilter or mesh is null!", MessageType.Warning);
            }
            else if (!mf.sharedMesh.isReadable)
            {
                EditorGUILayout.HelpBox("⚠ Mesh is NOT Read/Write enabled! Fix in import settings.",
                    MessageType.Error);
            }
            else
            {
                EditorGUILayout.LabelField($"   Mesh: {mf.sharedMesh.name}",
                    EditorStyles.miniLabel);
                EditorGUILayout.LabelField(
                    $"   Bounds: {sourceBoundsSize.x:F1} x {sourceBoundsSize.y:F1} x {sourceBoundsSize.z:F1}m",
                    EditorStyles.miniLabel);
            }
        }
    }

    private void DrawGridSection()
    {
        EditorGUILayout.LabelField("── GRID PRECISION ──", EditorStyles.boldLabel);

        // Auto mode
        autoGridFromBounds = EditorGUILayout.Toggle("Auto-fit to target tri count", autoGridFromBounds);

        if (autoGridFromBounds)
        {
            targetTriCount = EditorGUILayout.IntSlider("Target Triangles", targetTriCount, 50, MaxProxyTriangles1716);
            EditorGUILayout.LabelField(
                $"   (will calculate grid size automatically)",
                EditorStyles.miniLabel);

            if (targetObject != null && sourceBoundsSize != Vector3.zero)
            {
                float estimated = EstimateGridPrecision(targetTriCount);
                EditorGUILayout.LabelField(
                    $"   Estimated Grid: {estimated:F2}",
                    EditorStyles.miniLabel);
            }
        }
        else
        {
            gridPrecision = EditorGUILayout.Slider("Cell Size", gridPrecision, 0.05f, 25.0f);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Quick:", GUILayout.Width(42));
            if (GUILayout.Button("0.2")) gridPrecision = 0.2f;
            if (GUILayout.Button("0.5")) gridPrecision = 0.5f;
            if (GUILayout.Button("1.0")) gridPrecision = 1.0f;
            if (GUILayout.Button("2.0")) gridPrecision = 2.0f;
            if (GUILayout.Button("5.0")) gridPrecision = 5.0f;
            EditorGUILayout.EndHorizontal();

            // Smart recommendation
            if (sourceBoundsSize != Vector3.zero)
            {
                float maxDim = Mathf.Max(sourceBoundsSize.x, sourceBoundsSize.y, sourceBoundsSize.z);
                float rec = maxDim / 15f; // ~15 divisions along longest axis
                EditorGUILayout.LabelField(
                    $"   Recommended for this mesh: ~{rec:F2}",
                    EditorStyles.miniLabel);
            }
        }
    }

    private void DrawChunkingSection()
    {
        EditorGUILayout.LabelField("── CHUNKING (BIG MESHES) ──", EditorStyles.boldLabel);
        enableChunking = EditorGUILayout.Toggle("Enable Chunking", enableChunking);

        if (enableChunking)
        {
            chunkSize = EditorGUILayout.Slider("Chunk Size (m)", chunkSize, 10f, 200f);
            EditorGUILayout.HelpBox(
                "Disabled by 1716: chunked non-convex MeshColliders are not a runtime-safe proxy route.",
                MessageType.Warning);

            if (sourceBoundsSize != Vector3.zero)
            {
                float maxDim = Mathf.Max(sourceBoundsSize.x, sourceBoundsSize.y, sourceBoundsSize.z);
                int estChunks = Mathf.Max(1, Mathf.CeilToInt(maxDim / chunkSize));
                EditorGUILayout.LabelField(
                    $"   Estimated chunks along longest axis: ~{estChunks}",
                    EditorStyles.miniLabel);
            }
        }
    }

    private void DrawAdvancedSection()
    {
        showAdvanced = EditorGUILayout.Foldout(showAdvanced, "── ADVANCED ──");
        if (!showAdvanced) return;

        EditorGUI.indentLevel++;
        weldThreshold = EditorGUILayout.Slider("Weld Threshold", weldThreshold, 0.001f, 0.5f);
        minTriangleArea = EditorGUILayout.FloatField("Min Triangle Area", minTriangleArea);
        showWireframe = EditorGUILayout.Toggle("Scene Wireframe Preview", showWireframe);
        wireColor = EditorGUILayout.ColorField("Wire Color", wireColor);
        EditorGUI.indentLevel--;
    }

    private void DrawStatsSection()
    {
        EditorGUILayout.LabelField("── STATS ──", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            $"Source:      {sourceVertCount:N0} verts  |  {sourceTriCount:N0} tris");
        EditorGUILayout.LabelField(
            $"Generated:   {generatedVertCount:N0} verts  |  {generatedTriCount:N0} tris");

        if (sourceTriCount > 0 && generatedTriCount > 0)
        {
            float ratio = (float)generatedTriCount / sourceTriCount * 100f;
            string quality = generatedTriCount <= MaxProxyTriangles1716 ? "OK" : "REJECTED - increase grid";
            EditorGUILayout.LabelField($"Reduction:   {ratio:F2}% of original  [{quality}]");
            EditorGUILayout.LabelField($"Gen Time:    {lastGenerateTime:F3}s");

            if (chunksGenerated > 1)
                EditorGUILayout.LabelField($"Chunks:      {chunksGenerated}");
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawStatusBox()
    {
        if (!string.IsNullOrEmpty(lastStatus))
        {
            EditorGUILayout.HelpBox(lastStatus, lastStatusType);
        }
    }

    private void DrawButtons()
    {
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = targetObject != null;

        // Generate
        GUI.backgroundColor = new Color(0.2f, 0.85f, 0.3f);
        if (GUILayout.Button("▶  GENERATE", GUILayout.Height(38)))
        {
            if (autoGridFromBounds)
                gridPrecision = EstimateGridPrecision(targetTriCount);

            if (enableChunking)
                GenerateChunked();
            else
                GenerateSingle(targetObject);
        }

        // Preview (dry run — no save)
        GUI.backgroundColor = new Color(0.3f, 0.7f, 0.9f);
        if (GUILayout.Button("👁 PREVIEW", GUILayout.Height(38)))
        {
            if (autoGridFromBounds)
                gridPrecision = EstimateGridPrecision(targetTriCount);

            PreviewOnly();
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // Remove
        GUI.backgroundColor = new Color(0.9f, 0.35f, 0.25f);
        if (GUILayout.Button("✖  Remove PHYSICS_SKIN", GUILayout.Height(24)))
        {
            RemoveExisting(targetObject);
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
    }

    private void DrawBatchSection()
    {
        EditorGUILayout.LabelField("── BATCH MODE ──", EditorStyles.boldLabel);
        batchMode = EditorGUILayout.Toggle("Batch Mode", batchMode);

        if (!batchMode) return;

        GameObject[] selectedObjects = Selection.gameObjects;
        int count = selectedObjects.Length;
        EditorGUILayout.LabelField($"   Selected objects: {count}");

        if (count == 0)
        {
            EditorGUILayout.HelpBox("Select multiple GameObjects with MeshFilters in Hierarchy.",
                MessageType.Info);
            return;
        }

        // Show list
        EditorGUI.indentLevel++;
        for (int i = 0; i < selectedObjects.Length; i++)
        {
            GameObject go = selectedObjects[i];
            go.TryGetComponent(out MeshFilter mf);
            string info = mf != null && mf.sharedMesh != null
                ? $"{CountMeshTriangles(mf.sharedMesh)} tris"
                : "no mesh";
            EditorGUILayout.LabelField($"• {go.name} ({info})");
        }
        EditorGUI.indentLevel--;

        EditorGUILayout.Space(4);

        GUI.backgroundColor = new Color(0.9f, 0.8f, 0.2f);
        if (GUILayout.Button($"▶▶  BATCH GENERATE ({count} objects)", GUILayout.Height(34)))
        {
            BatchGenerate();
        }
        GUI.backgroundColor = Color.white;
    }

    // ═══════════════════════════════════════════════════════════════════
    // GENERATION — SINGLE OBJECT
    // ═══════════════════════════════════════════════════════════════════
    private void GenerateSingle(GameObject target)
    {
        double startTime = EditorApplication.timeSinceStartup;
        chunksGenerated = 1;

        // Validate
        MeshFilter mf = ValidateTarget(target);
        if (mf == null) return;

        Mesh sourceMesh = mf.sharedMesh;
        ExtractMeshData(sourceMesh, _sourceVertices, _sourceTriangles);
        sourceTriCount = _sourceTriangles.Count / 3;
        sourceVertCount = _sourceVertices.Count;

        // Run pipeline
        PipelineResult result = RunPipeline(_sourceVertices, _sourceTriangles, gridPrecision);

        if (result.tris.Count < 3)
        {
            SetStatus($"Result: {result.tris.Count / 3} tris. Lower Grid Precision!", MessageType.Error);
            return;
        }

        if (!ValidateProxyTriangleBudget(result.tris.Count / 3, target.name))
            return;

        // Build mesh
        Mesh resultMesh = BuildMesh(result, $"COL_Skin_{target.name}_{gridPrecision:F1}");

        // Save asset
        string assetPath = SaveMeshAsset(resultMesh, target.name, gridPrecision);

        // Reload from disk to have proper reference
        resultMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);

        // Create/update child
        if (!ApplyToScene(target, resultMesh))
            return;

        // Stats
        generatedTriCount = result.tris.Count / 3;
        generatedVertCount = result.verts.Count;
        previewMesh = resultMesh;
        previewTarget = target;
        lastGenerateTime = EditorApplication.timeSinceStartup - startTime;

        SetStatus(
            $"✓ Done! {generatedTriCount} tris, {generatedVertCount} verts. Saved: {assetPath}",
            MessageType.Info);

        SceneView.RepaintAll();
    }

    // ═══════════════════════════════════════════════════════════════════
    // GENERATION — CHUNKED (FOR HUGE MESHES)
    // ═══════════════════════════════════════════════════════════════════
    private void GenerateChunked()
    {
        if (enableChunking)
        {
            SetStatus("Chunked non-convex MeshCollider generation is disabled by 1716. Use compound primitives for huge bases.", MessageType.Error);
            return;
        }

        double startTime = EditorApplication.timeSinceStartup;

        MeshFilter mf = ValidateTarget(targetObject);
        if (mf == null) return;

        Mesh sourceMesh = mf.sharedMesh;
        ExtractMeshData(sourceMesh, _sourceVertices, _sourceTriangles);
        sourceTriCount = _sourceTriangles.Count / 3;
        sourceVertCount = _sourceVertices.Count;

        // Calculate bounds
        Bounds bounds = sourceMesh.bounds;
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        int chunksX = Mathf.Max(1, Mathf.CeilToInt((max.x - min.x) / chunkSize));
        int chunksY = Mathf.Max(1, Mathf.CeilToInt((max.y - min.y) / chunkSize));
        int chunksZ = Mathf.Max(1, Mathf.CeilToInt((max.z - min.z) / chunkSize));

        Undo.SetCurrentGroupName("Generate Chunked Physics Skin");
        int undoGroup = Undo.GetCurrentGroup();

        // Remove old
        RemoveExisting(targetObject);

        // Parent container
        GameObject container = new GameObject(CompoundProxyRootName1716);
        Undo.RegisterCreatedObjectUndo(container, "Create Physics Skin Container");
        container.transform.SetParent(targetObject.transform, false);
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;
        container.transform.localScale = Vector3.one;

        int totalGenTris = 0;
        int totalGenVerts = 0;
        int chunkCount = 0;

        for (int cx = 0; cx < chunksX; cx++)
        {
            for (int cy = 0; cy < chunksY; cy++)
            {
                for (int cz = 0; cz < chunksZ; cz++)
                {
                    Bounds chunkBounds = new Bounds();
                    Vector3 chunkMin = new Vector3(
                        min.x + cx * chunkSize,
                        min.y + cy * chunkSize,
                        min.z + cz * chunkSize);
                    Vector3 chunkMax = new Vector3(
                        Mathf.Min(min.x + (cx + 1) * chunkSize, max.x),
                        Mathf.Min(min.y + (cy + 1) * chunkSize, max.y),
                        Mathf.Min(min.z + (cz + 1) * chunkSize, max.z));
                    chunkBounds.SetMinMax(chunkMin, chunkMax);

                    // Expand slightly to catch border triangles
                    float pad = gridPrecision * 1.5f;
                    Bounds expandedBounds = chunkBounds;
                    expandedBounds.Expand(pad);

                    // Collect triangles overlapping this chunk
                    _chunkVertices.Clear();
                    _chunkTriangles.Clear();
                    _chunkVertexRemap.Clear();

                    for (int i = 0; i < _sourceTriangles.Count; i += 3)
                    {
                        int i0 = _sourceTriangles[i], i1 = _sourceTriangles[i + 1], i2 = _sourceTriangles[i + 2];
                        Vector3 v0 = _sourceVertices[i0], v1 = _sourceVertices[i1], v2 = _sourceVertices[i2];

                        // Triangle overlaps chunk if any vertex is inside expanded bounds
                        if (expandedBounds.Contains(v0) ||
                            expandedBounds.Contains(v1) ||
                            expandedBounds.Contains(v2))
                        {
                            _chunkTriangles.Add(RemapVert(i0, _sourceVertices, _chunkVertices, _chunkVertexRemap));
                            _chunkTriangles.Add(RemapVert(i1, _sourceVertices, _chunkVertices, _chunkVertexRemap));
                            _chunkTriangles.Add(RemapVert(i2, _sourceVertices, _chunkVertices, _chunkVertexRemap));
                        }
                    }

                    if (_chunkTriangles.Count < 3) continue;

                    // Run pipeline on chunk
                    PipelineResult result = RunPipeline(_chunkVertices, _chunkTriangles, gridPrecision);

                    if (result.tris.Count < 3) continue;

                    // Clip output verts to actual chunk bounds (not expanded)
                    // to avoid overlap between chunks — we keep all since weld handles borders

                    Mesh chunkMesh = BuildMesh(result,
                        $"Skin_{targetObject.name}_chunk{cx}_{cy}_{cz}");

                    string chunkPath = SaveMeshAsset(chunkMesh, 
                        $"{targetObject.name}_c{cx}{cy}{cz}", gridPrecision);
                    chunkMesh = AssetDatabase.LoadAssetAtPath<Mesh>(chunkPath);

                    // Create child
                    GameObject chunkObj = new GameObject($"COL_chunk_{cx}_{cy}_{cz}");
                    Undo.RegisterCreatedObjectUndo(chunkObj, "Create Chunk");
                    chunkObj.transform.SetParent(container.transform, false);
                    chunkObj.transform.localPosition = Vector3.zero;
                    chunkObj.transform.localRotation = Quaternion.identity;
                    chunkObj.transform.localScale = Vector3.one;

                    BoxCollider box = chunkObj.AddComponent<BoxCollider>();
                    Bounds chunkMeshBounds = chunkMesh.bounds;
                    box.center = chunkMeshBounds.center;
                    box.size = ClampBoxSize(chunkMeshBounds.size);

                    totalGenTris += result.tris.Count / 3;
                    totalGenVerts += result.verts.Count;
                    chunkCount++;
                }
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        generatedTriCount = totalGenTris;
        generatedVertCount = totalGenVerts;
        chunksGenerated = chunkCount;
        previewMesh = null; // no single preview for chunked
        lastGenerateTime = EditorApplication.timeSinceStartup - startTime;

        SetStatus(
            $"✓ Chunked! {chunkCount} chunks, {totalGenTris} total tris.",
            MessageType.Info);

        SceneView.RepaintAll();
    }

    private static int RemapVert(int srcIdx, List<Vector3> srcVerts,
        List<Vector3> outVerts, Dictionary<int, int> remap)
    {
        if (remap.TryGetValue(srcIdx, out int mapped))
            return mapped;
        int newIdx = outVerts.Count;
        outVerts.Add(srcVerts[srcIdx]);
        remap[srcIdx] = newIdx;
        return newIdx;
    }

    // ═══════════════════════════════════════════════════════════════════
    // PREVIEW (NO SAVE)
    // ═══════════════════════════════════════════════════════════════════
    private void PreviewOnly()
    {
        MeshFilter mf = ValidateTarget(targetObject);
        if (mf == null) return;

        Mesh sourceMesh = mf.sharedMesh;
        ExtractMeshData(sourceMesh, _sourceVertices, _sourceTriangles);
        sourceTriCount = _sourceTriangles.Count / 3;
        sourceVertCount = _sourceVertices.Count;

        PipelineResult result = RunPipeline(_sourceVertices, _sourceTriangles, gridPrecision);

        generatedTriCount = result.tris.Count / 3;
        generatedVertCount = result.verts.Count;

        if (generatedTriCount > MaxProxyTriangles1716)
        {
            previewMesh = null;
            SetStatus($"Preview rejected: {generatedTriCount} tris exceeds 1716 proxy cap {MaxProxyTriangles1716}.", MessageType.Warning);
            SceneView.RepaintAll();
            return;
        }

        if (result.tris.Count >= 3)
        {
            previewMesh = BuildMesh(result, "COL_preview_temp");
            previewTarget = targetObject;
            SetStatus($"Preview: {generatedTriCount} tris. Not saved yet.", MessageType.Info);
        }
        else
        {
            previewMesh = null;
            SetStatus($"Preview: {generatedTriCount} tris — too few. Lower grid.", MessageType.Warning);
        }

        SceneView.RepaintAll();
    }

    // ═══════════════════════════════════════════════════════════════════
    // BATCH
    // ═══════════════════════════════════════════════════════════════════
    private void BatchGenerate()
    {
        _batchObjects.Clear();
        GameObject[] selection = Selection.gameObjects;
        for (int i = 0; i < selection.Length; i++)
        {
            GameObject go = selection[i];
            if (go == null)
                continue;

            if (!go.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null || !filter.sharedMesh.isReadable)
                continue;

            _batchObjects.Add(go);
        }

        if (_batchObjects.Count == 0)
        {
            SetStatus("No valid meshes in selection.", MessageType.Error);
            return;
        }

        Undo.SetCurrentGroupName("Batch Physics Skin Generation");
        int undoGroup = Undo.GetCurrentGroup();

        int successCount = 0;
        int failCount = 0;

        for (int i = 0; i < _batchObjects.Count; i++)
        {
            EditorUtility.DisplayProgressBar("Batch Generating...",
                $"Processing {_batchObjects[i].name} ({i + 1}/{_batchObjects.Count})",
                (float)i / _batchObjects.Count);

            try
            {
                targetObject = _batchObjects[i];
                UpdateSourceStats();

                if (autoGridFromBounds)
                    gridPrecision = EstimateGridPrecision(targetTriCount);

                GenerateSingle(_batchObjects[i]);
                successCount++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HectonSkin] Failed on {_batchObjects[i].name}: {e.Message}");
                failCount++;
            }
        }

        EditorUtility.ClearProgressBar();
        Undo.CollapseUndoOperations(undoGroup);

        SetStatus($"Batch done! {successCount} success, {failCount} failed.", MessageType.Info);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CORE PIPELINE
    // ═══════════════════════════════════════════════════════════════════
    private struct PipelineResult
    {
        public List<Vector3> verts;
        public List<int> tris;
    }

    private struct VoxelAccumulator
    {
        public Vector3 sum;
        public int count;

        public void Add(Vector3 position)
        {
            sum += position;
            count++;
        }

        public Vector3 ResolveCentroid()
        {
            return count > 0 ? sum / count : Vector3.zero;
        }
    }

    private PipelineResult RunPipeline(List<Vector3> srcVerts, List<int> srcTris, float cellSize)
    {
        float invCell = 1f / cellSize;
        _voxelAccumulators.Clear();
        _voxelCentroids.Clear();
        _uniqueTriangleKeys.Clear();
        _shellVoxels.Clear();
        _voxelToIndex.Clear();
        _outVertices.Clear();
        _outTriangles.Clear();

        // ── 1. Voxelize ──
        for (int i = 0; i < srcVerts.Count; i++)
        {
            Vector3Int key = ToVoxel(srcVerts[i], invCell);
            _voxelAccumulators.TryGetValue(key, out VoxelAccumulator accumulator);
            accumulator.Add(srcVerts[i]);
            _voxelAccumulators[key] = accumulator;
        }

        // ── 2. Centroid per voxel ──
        Dictionary<Vector3Int, VoxelAccumulator>.Enumerator voxelEnumerator = _voxelAccumulators.GetEnumerator();
        while (voxelEnumerator.MoveNext())
        {
            KeyValuePair<Vector3Int, VoxelAccumulator> kvp = voxelEnumerator.Current;
            _voxelCentroids[kvp.Key] = kvp.Value.ResolveCentroid();
        }

        // ── 3. Vertex → voxel lookup ──
        EnsureVector3IntListSize(_vertexVoxelScratch, srcVerts.Count);
        for (int i = 0; i < srcVerts.Count; i++)
            _vertexVoxelScratch[i] = ToVoxel(srcVerts[i], invCell);

        // ── 4. Shell extraction: only tris spanning 3 different voxels ──
        for (int i = 0; i < srcTris.Count; i += 3)
        {
            Vector3Int vA = _vertexVoxelScratch[srcTris[i]];
            Vector3Int vB = _vertexVoxelScratch[srcTris[i + 1]];
            Vector3Int vC = _vertexVoxelScratch[srcTris[i + 2]];

            if (vA == vB || vB == vC || vA == vC)
                continue;

            TriKey tk = new TriKey(vA, vB, vC);
            if (_uniqueTriangleKeys.Add(tk))
            {
                _shellVoxels.Add(vA);
                _shellVoxels.Add(vB);
                _shellVoxels.Add(vC);
            }
        }

        // ── 5. Build output arrays ──
        List<int> outTris = _outTriangles;
        List<Vector3> outVerts = _outVertices;

        for (int i = 0; i < _shellVoxels.Count; i++)
        {
            Vector3Int vk = _shellVoxels[i];
            if (!_voxelToIndex.ContainsKey(vk))
            {
                _voxelToIndex[vk] = outVerts.Count;
                outVerts.Add(_voxelCentroids[vk]);
            }
        }

        for (int i = 0; i < _shellVoxels.Count; i++)
            outTris.Add(_voxelToIndex[_shellVoxels[i]]);

        // ── 6. Weld vertices ──
        WeldVertices(ref outVerts, ref outTris, weldThreshold);

        // ── 7. Remove degenerates ──
        RemoveDegenerateTris(ref outVerts, ref outTris, minTriangleArea);

        // ── 8. Remove unused verts ──
        RemoveUnusedVertices(ref outVerts, ref outTris);

        return new PipelineResult { verts = outVerts, tris = outTris };
    }

    // ═══════════════════════════════════════════════════════════════════
    // ALGORITHM HELPERS
    // ═══════════════════════════════════════════════════════════════════
    private static Vector3Int ToVoxel(Vector3 pos, float invCellSize)
    {
        return new Vector3Int(
            Mathf.FloorToInt(pos.x * invCellSize),
            Mathf.FloorToInt(pos.y * invCellSize),
            Mathf.FloorToInt(pos.z * invCellSize));
    }

    private struct TriKey : System.IEquatable<TriKey>
    {
        public readonly Vector3Int a, b, c;

        public TriKey(Vector3Int x, Vector3Int y, Vector3Int z)
        {
            // Canonical sort
            if (Compare(x, y) > 0) Swap(ref x, ref y);
            if (Compare(y, z) > 0) Swap(ref y, ref z);
            if (Compare(x, y) > 0) Swap(ref x, ref y);
            a = x; b = y; c = z;
        }

        private static int Compare(Vector3Int p, Vector3Int q)
        {
            int c = p.x.CompareTo(q.x);
            if (c != 0) return c;
            c = p.y.CompareTo(q.y);
            if (c != 0) return c;
            return p.z.CompareTo(q.z);
        }

        private static void Swap(ref Vector3Int a, ref Vector3Int b)
        {
            var t = a; a = b; b = t;
        }

        public bool Equals(TriKey other) =>
            a == other.a && b == other.b && c == other.c;

        public override bool Equals(object obj) =>
            obj is TriKey tk && Equals(tk);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = a.GetHashCode();
                h = h * 397 ^ b.GetHashCode();
                h = h * 397 ^ c.GetHashCode();
                return h;
            }
        }
    }

    private void WeldVertices(ref List<Vector3> verts, ref List<int> tris, float threshold)
    {
        float threshSq = threshold * threshold;
        int n = verts.Count;
        EnsureIntListSize(_weldRemap, n, 0);
        for (int i = 0; i < n; i++) _weldRemap[i] = i;

        for (int i = 0; i < n; i++)
        {
            if (_weldRemap[i] != i) continue;
            for (int j = i + 1; j < n; j++)
            {
                if (_weldRemap[j] != j) continue;
                if ((verts[i] - verts[j]).sqrMagnitude < threshSq)
                    _weldRemap[j] = i;
            }
        }

        // Resolve chains
        for (int i = 0; i < n; i++)
        {
            int root = i;
            while (_weldRemap[root] != root) root = _weldRemap[root];
            _weldRemap[i] = root;
        }

        for (int i = 0; i < tris.Count; i++)
            tris[i] = _weldRemap[tris[i]];
    }

    private void RemoveDegenerateTris(ref List<Vector3> verts, ref List<int> tris, float minArea)
    {
        _cleanTriangles.Clear();
        float minAreaSq4 = minArea * minArea * 4f;

        for (int i = 0; i < tris.Count; i += 3)
        {
            int a = tris[i], b = tris[i + 1], c = tris[i + 2];
            if (a == b || b == c || a == c) continue;

            Vector3 cross = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (cross.sqrMagnitude < minAreaSq4) continue;

            _cleanTriangles.Add(a);
            _cleanTriangles.Add(b);
            _cleanTriangles.Add(c);
        }
        tris.Clear();
        tris.AddRange(_cleanTriangles);
    }

    private void RemoveUnusedVertices(ref List<Vector3> verts, ref List<int> tris)
    {
        EnsureIntListSize(_usedVertexFlags, verts.Count, 0);
        for (int i = 0; i < tris.Count; i++)
            _usedVertexFlags[tris[i]] = 1;

        EnsureIntListSize(_unusedRemap, verts.Count, 0);
        _compactedVertices.Clear();

        for (int i = 0; i < verts.Count; i++)
        {
            if (_usedVertexFlags[i] != 0)
            {
                _unusedRemap[i] = _compactedVertices.Count;
                _compactedVertices.Add(verts[i]);
            }
        }

        for (int i = 0; i < tris.Count; i++)
            tris[i] = _unusedRemap[tris[i]];

        verts.Clear();
        verts.AddRange(_compactedVertices);
    }

    private static void EnsureIntListSize(List<int> list, int size, int value)
    {
        if (list.Capacity < size)
            list.Capacity = size;
        list.Clear();
        for (int i = 0; i < size; i++)
            list.Add(value);
    }

    private static void EnsureVector3IntListSize(List<Vector3Int> list, int size)
    {
        if (list.Capacity < size)
            list.Capacity = size;
        list.Clear();
        for (int i = 0; i < size; i++)
            list.Add(Vector3Int.zero);
    }

    // ═══════════════════════════════════════════════════════════════════
    // AUTO GRID ESTIMATION
    // ═══════════════════════════════════════════════════════════════════
    private float EstimateGridPrecision(int targetTris)
    {
        // Rough heuristic: for a convex-ish shape, tri count ≈ 2 * (surfaceVoxels)
        // surfaceVoxels ≈ 6 * (dims/cellSize)^2 for a cube
        // We solve for cellSize given target tri count
        float maxDim = Mathf.Max(sourceBoundsSize.x,
            Mathf.Max(sourceBoundsSize.y, sourceBoundsSize.z));

        if (maxDim < 0.1f) return 0.5f;

        // Start with a guess and binary search would be ideal,
        // but a simple formula works for rocks:
        // tris ≈ k * (maxDim / cellSize)^2, k ≈ 3-6 for natural shapes
        float k = 4.5f;
        float cellSize = maxDim * ApproxInvSqrt(targetTris / k);
        cellSize = Mathf.Clamp(cellSize, 0.05f, 10f);
        return cellSize;
    }

    private static float ApproxInvSqrt(float value)
    {
        if (value <= 0f)
            return 1f;

        float half = 0.5f * value;
        int bits = System.BitConverter.SingleToInt32Bits(value);
        bits = 0x5f3759df - (bits >> 1);
        float estimate = System.BitConverter.Int32BitsToSingle(bits);
        return estimate * (1.5f - half * estimate * estimate);
    }

    // ═══════════════════════════════════════════════════════════════════
    // MESH BUILDING & SAVING
    // ═══════════════════════════════════════════════════════════════════
    private static Mesh BuildMesh(PipelineResult result, string name)
    {
        Mesh mesh = new Mesh();
        mesh.name = name;
        mesh.SetVertices(result.verts);
        mesh.SetTriangles(result.tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static string SaveMeshAsset(Mesh mesh, string objectName, float precision)
    {
        string folderPath = "Assets/_Project/Art/Meshes/Generated_Physics";
        EnsureFolderExists(folderPath);

        string safeName = SanitizeName(objectName);
        string assetName = $"COL_Skin_{safeName}_{precision:F1}.asset";
        string assetPath = $"{folderPath}/{assetName}";

        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            AssetDatabase.SaveAssets();
            return assetPath;
        }

        AssetDatabase.CreateAsset(mesh, assetPath);
        AssetDatabase.SaveAssets();
        return assetPath;
    }

    private bool ApplyToScene(GameObject target, Mesh resultMesh)
    {
        if (!ColliderOptimizerEngine1716.ValidateProxyMesh(resultMesh, out string proxyFailure))
        {
            SetStatus("1716 proxy rejected: " + proxyFailure, MessageType.Error);
            return false;
        }

        Undo.SetCurrentGroupName("Generate Physics Skin");
        int undoGroup = Undo.GetCurrentGroup();

        // Find or create
        Transform existing = target.transform.Find(ConvexProxyRootName1716);
        if (existing == null)
            existing = target.transform.Find(LegacyPhysicsSkinRootName);
        if (existing == null)
            existing = target.transform.Find(CompoundProxyRootName1716);

        GameObject skinObj;

        if (existing != null)
        {
            skinObj = existing.gameObject;
            Undo.RecordObject(skinObj, "Update Physics Skin");
            skinObj.name = ConvexProxyRootName1716;
        }
        else
        {
            skinObj = new GameObject(ConvexProxyRootName1716);
            Undo.RegisterCreatedObjectUndo(skinObj, "Create Physics Skin");
            skinObj.transform.SetParent(target.transform, false);
        }

        // Reset local transform
        skinObj.transform.localPosition = Vector3.zero;
        skinObj.transform.localRotation = Quaternion.identity;
        skinObj.transform.localScale = Vector3.one;

        ClearGeneratedProxyChildren(skinObj.transform);
        StripTargetVisualMeshCollider(target);

        // MeshCollider
        skinObj.TryGetComponent(out MeshCollider mc);
        if (mc == null)
            mc = Undo.AddComponent<MeshCollider>(skinObj);
        else
            Undo.RecordObject(mc, "Update MeshCollider");

        mc.sharedMesh = null; // force refresh
        mc.convex = true;
        mc.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                          | MeshColliderCookingOptions.EnableMeshCleaning
                          | MeshColliderCookingOptions.WeldColocatedVertices;
        mc.sharedMesh = resultMesh;

        // Debug visuals (disabled by default)
        skinObj.TryGetComponent(out MeshFilter skinMF);
        if (skinMF == null) skinMF = skinObj.AddComponent<MeshFilter>();
        skinMF.sharedMesh = resultMesh;

        skinObj.TryGetComponent(out MeshRenderer skinMR);
        if (skinMR == null) skinMR = skinObj.AddComponent<MeshRenderer>();
        skinMR.enabled = false;

        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.SetDirty(skinObj);

        if (!ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(target, out string failure))
        {
            Debug.LogError("[HectonSkin] 1716 topology validation failed: " + failure, target);
            SetStatus("1716 validation failed: " + failure, MessageType.Error);
            return false;
        }

        return true;
    }

    // ═══════════════════════════════════════════════════════════════════
    // VALIDATION & UTILITY
    // ═══════════════════════════════════════════════════════════════════
    private MeshFilter ValidateTarget(GameObject target)
    {
        if (target == null)
        {
            SetStatus("No target selected.", MessageType.Error);
            return null;
        }

        if (!target.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null)
        {
            SetStatus("Target has no MeshFilter or mesh is null.", MessageType.Error);
            return null;
        }

        if (!mf.sharedMesh.isReadable)
        {
            SetStatus("Mesh is NOT Read/Write enabled! Fix in import settings.", MessageType.Error);
            return null;
        }

        if (mf.sharedMesh.vertexCount < 4)
        {
            SetStatus("Mesh has fewer than 4 vertices.", MessageType.Error);
            return null;
        }

        return mf;
    }

    private void UpdateSourceStats()
    {
        sourceTriCount = 0;
        sourceVertCount = 0;
        sourceBoundsSize = Vector3.zero;

        if (targetObject == null) return;

        if (!targetObject.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null) return;

        sourceTriCount = CountMeshTriangles(mf.sharedMesh);
        sourceVertCount = mf.sharedMesh.vertexCount;
        sourceBoundsSize = mf.sharedMesh.bounds.size;
    }

    private void ExtractMeshData(Mesh mesh, List<Vector3> vertices, List<int> triangles)
    {
        vertices.Clear();
        triangles.Clear();
        if (mesh == null)
            return;

        mesh.GetVertices(vertices);
        for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
        {
            _sourceSubmeshTriangles.Clear();
            mesh.GetTriangles(_sourceSubmeshTriangles, subMeshIndex, true);
            triangles.AddRange(_sourceSubmeshTriangles);
        }
    }

    private static int CountMeshTriangles(Mesh mesh)
    {
        if (mesh == null)
            return 0;

        long indexCount = 0L;
        for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
            indexCount += mesh.GetIndexCount(subMeshIndex);

        return (int)global::System.Math.Min(indexCount / 3L, int.MaxValue);
    }

    private bool ValidateProxyTriangleBudget(int triangleCount, string sourceName)
    {
        if (triangleCount <= MaxProxyTriangles1716)
            return true;

        SetStatus(sourceName + " rejected: " + triangleCount + " tris exceeds 1716 proxy cap " + MaxProxyTriangles1716 + ".", MessageType.Error);
        return false;
    }

    private static void StripTargetVisualMeshCollider(GameObject target)
    {
        if (target == null ||
            !target.TryGetComponent(out MeshFilter meshFilter) ||
            meshFilter.sharedMesh == null ||
            !target.TryGetComponent(out MeshCollider meshCollider) ||
            meshCollider.sharedMesh != meshFilter.sharedMesh)
        {
            return;
        }

        Undo.DestroyObjectImmediate(meshCollider);
    }

    private static void ClearGeneratedProxyChildren(Transform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);
    }

    private static Vector3 ClampBoxSize(Vector3 size)
    {
        return new Vector3(
            Mathf.Max(0.025f, Mathf.Abs(size.x)),
            Mathf.Max(0.025f, Mathf.Abs(size.y)),
            Mathf.Max(0.025f, Mathf.Abs(size.z)));
    }

    private void RemoveExisting(GameObject target)
    {
        if (target == null) return;

        // Remove legacy and 1716-generated proxy roots.
        _oldPhysicsSkinChildren.Clear();
        Transform root = target.transform;
        int childCount = root.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == LegacyPhysicsSkinRootName ||
                child.name == ConvexProxyRootName1716 ||
                child.name == CompoundProxyRootName1716)
            {
                _oldPhysicsSkinChildren.Add(child);
            }
        }

        if (_oldPhysicsSkinChildren.Count > 0)
        {
            for (int i = 0; i < _oldPhysicsSkinChildren.Count; i++)
                Undo.DestroyObjectImmediate(_oldPhysicsSkinChildren[i].gameObject);
            SetStatus($"Removed {_oldPhysicsSkinChildren.Count} physics proxy object(s).", MessageType.Warning);
        }
        else
        {
            SetStatus("No physics proxy root found.", MessageType.Warning);
        }

        previewMesh = null;
        generatedTriCount = 0;
        generatedVertCount = 0;
        SceneView.RepaintAll();
    }

    private void SetStatus(string msg, MessageType type)
    {
        lastStatus = msg;
        lastStatusType = type;
        Repaint();
    }

    private static void EnsureFolderExists(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string SanitizeName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalid.Length; i++)
            name = name.Replace(invalid[i], '_');
        // Also replace spaces and dots
        name = name.Replace(' ', '_').Replace('.', '_');
        return name;
    }

    // ═══════════════════════════════════════════════════════════════════
    // SCENE VIEW WIREFRAME PREVIEW
    // ═══════════════════════════════════════════════════════════════════
    private void OnSceneGUI(SceneView sceneView)
    {
        if (!showWireframe || previewMesh == null || previewTarget == null) return;

        Handles.matrix = previewTarget.transform.localToWorldMatrix;
        Handles.color = wireColor;

        _previewVertices.Clear();
        _previewTriangles.Clear();
        previewMesh.GetVertices(_previewVertices);
        previewMesh.GetTriangles(_previewTriangles, 0, true);

        for (int i = 0; i < _previewTriangles.Count; i += 3)
        {
            Vector3 a = _previewVertices[_previewTriangles[i]];
            Vector3 b = _previewVertices[_previewTriangles[i + 1]];
            Vector3 c = _previewVertices[_previewTriangles[i + 2]];

            Handles.DrawLine(a, b);
            Handles.DrawLine(b, c);
            Handles.DrawLine(c, a);
        }

        // Draw vert count at center
        Handles.Label(previewMesh.bounds.center,
            $"  {_previewTriangles.Count / 3} tris",
            EditorStyles.whiteBoldLabel);
    }
}
