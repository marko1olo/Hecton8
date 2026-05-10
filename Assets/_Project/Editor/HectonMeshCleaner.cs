// ============================================================================
// HectonMeshCleaner.cs
// Place in: Assets/Editor/HectonMeshCleaner.cs
// v4.0 — Per-triangle occlusion + Submesh fix + Hole fill
// ============================================================================
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class HectonMeshCleaner : EditorWindow
{
    // ═══════════════════════════════════════════════════════════════════
    // SETTINGS
    // ═══════════════════════════════════════════════════════════════════
    private GameObject targetObject;
    private bool processAllLODs = true;
    private float occlusionDistance = 0.3f;
    private float rayOffset = 0.005f;
    private bool enableHoleFill = true;
    private int maxHoleEdges = 50;
    private int minHoleEdges = 4;

    // Preview
    private bool showPreview = true;
    private Color hiddenColor = new Color(1f, 0f, 0f, 0.5f);

    // ═══════════════════════════════════════════════════════════════════
    // STATE
    // ═══════════════════════════════════════════════════════════════════
    private ulong analyzedObjectEntityId = 0UL;
    private string analyzedObjectPath = "";
    private Dictionary<ulong, PerMeshAnalysis> perMeshAnalysis = new Dictionary<ulong, PerMeshAnalysis>();
    private bool analysisReady = false;
    private Mesh previewSourceMesh;
    private GameObject previewTarget;
    private HashSet<int> previewHiddenTris = new HashSet<int>();
    private Vector2 scrollPos;
    private List<LODResult> lodResults = new List<LODResult>();
    private double lastTime = 0;
    private string lastStatus = "";
    private MessageType lastStatusType = MessageType.None;
    private static readonly RaycastHit[] rayBuffer = new RaycastHit[64];
    // COLD ALLOC: List<int>[196608] - editor submesh triangle collection scratch - owner: HectonMeshCleaner
    private static readonly List<int> s_CollectSubmeshTriangles = new List<int>(196608);
    // COLD ALLOC: List<Vector3>[65536] - editor occlusion analysis vertex scratch - owner: HectonMeshCleaner
    private readonly List<Vector3> analyzeLocalVerts = new List<Vector3>(65536);
    // COLD ALLOC: List<int>[196608] - editor occlusion analysis triangle scratch - owner: HectonMeshCleaner
    private readonly List<int> analyzeTriangles = new List<int>(196608);
    // COLD ALLOC: List<Vector3>[65536] - editor occlusion analysis world vertex scratch - owner: HectonMeshCleaner
    private readonly List<Vector3> analyzeWorldVerts = new List<Vector3>(65536);
    // COLD ALLOC: List<Vector3>[65536] - editor double-sided mesh vertex scratch - owner: HectonMeshCleaner
    private readonly List<Vector3> doubleSidedVerts = new List<Vector3>(65536);
    // COLD ALLOC: List<int>[393216] - editor double-sided mesh triangle scratch - owner: HectonMeshCleaner
    private readonly List<int> doubleSidedTris = new List<int>(393216);

    private struct PerMeshAnalysis
    {
        public ulong meshFilterID;
        public ulong meshID;
        public int vertCount;
        public int triCount;
        public string goName;
        public string parentName;
        public HashSet<int> hiddenTris;
        public HashSet<long> originalBoundaryEdges;
    }

    private struct LODResult
    {
        public string name;
        public int srcTris, hiddenTris, keptTris;
        public float pctRemoved;
    }

    [MenuItem("Hecton/Mesh Cleaner v4")]
    public static void ShowWindow()
    {
        var w = GetWindow<HectonMeshCleaner>("Mesh Cleaner v4");
        w.minSize = new Vector2(420, 620);
    }

    private void OnEnable() { SceneView.duringSceneGui += OnSceneGUI; EditorApplication.playModeStateChanged += OnPlayMode; }
    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.playModeStateChanged -= OnPlayMode;

        if (IsEditorTransitionBusy())
            return;

        FullReset();
        CleanupTemp();
    }

    private void OnPlayMode(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.ExitingEditMode || s == PlayModeStateChange.ExitingPlayMode)
            FullReset();
    }

    private static bool IsEditorTransitionBusy()
    {
        return EditorApplication.isCompiling ||
               EditorApplication.isUpdating ||
               EditorApplication.isPlayingOrWillChangePlaymode;
    }

    // ═══════════════════════════════════════════════════════════════════
    // STATE PROTECTION
    // ═══════════════════════════════════════════════════════════════════
    private void FullReset(string reason = "")
    {
        analysisReady = false;
        previewHiddenTris.Clear();
        perMeshAnalysis.Clear();
        lodResults.Clear();
        previewSourceMesh = null;
        previewTarget = null;
        analyzedObjectEntityId = 0UL;
        analyzedObjectPath = "";
        if (!string.IsNullOrEmpty(reason)) Debug.Log($"[HectonCleaner] Reset: {reason}");
        SceneView.RepaintAll();
        Repaint();
    }

    private bool ValidateState()
    {
        if (!analysisReady) return false;
        if (targetObject == null) { FullReset("Target null"); return false; }
        if (GetStableObjectId(targetObject) != analyzedObjectEntityId) { FullReset("Target changed"); return false; }
        if (GetHierarchyPath(targetObject) != analyzedObjectPath) { FullReset("Hierarchy changed"); return false; }
        foreach (var mf in GetMeshFilters())
        {
            ulong meshFilterId = GetStableObjectId(mf);
            if (!perMeshAnalysis.ContainsKey(meshFilterId)) continue;
            var a = perMeshAnalysis[meshFilterId];
            if (mf.sharedMesh == null || GetStableObjectId(mf.sharedMesh) != a.meshID ||
                mf.sharedMesh.vertexCount != a.vertCount || ResolveTriangleCount(mf.sharedMesh) != a.triCount)
            { FullReset("Mesh data changed"); return false; }
        }
        return true;
    }

    private static string GetHierarchyPath(GameObject go)
    {
        string p = go.name; Transform t = go.transform.parent;
        while (t != null) { p = t.name + "/" + p; t = t.parent; }
        return p;
    }

    private static ulong GetStableObjectId(UnityEngine.Object obj)
    {
        return obj != null ? EntityId.ToULong(obj.GetEntityId()) : 0UL;
    }

    private static long ResolveIndexCount(Mesh mesh)
    {
        if (mesh == null)
            return 0L;

        long indexCount = 0L;
        for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
        {
            indexCount += mesh.GetIndexCount(subMeshIndex);
        }

        return indexCount;
    }

    private static long ResolveTriangleCount(Mesh mesh)
    {
        return ResolveIndexCount(mesh) / 3L;
    }

    private static void CollectMeshTriangles(Mesh mesh, List<int> triangles)
    {
        triangles.Clear();
        if (mesh == null)
            return;

        for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
        {
            s_CollectSubmeshTriangles.Clear();
            mesh.GetTriangles(s_CollectSubmeshTriangles, subMeshIndex, true);
            triangles.AddRange(s_CollectSubmeshTriangles);
        }
    }

    private string GetUniqueSaveName(MeshFilter mf)
    {
        string parent = mf.transform.parent != null ? mf.transform.parent.name : "root";
        if (mf.transform.parent != null && mf.transform.parent.parent != null)
            parent = mf.transform.parent.parent.name + "_" + parent;
        return SanitizeName(parent + "_" + mf.gameObject.name);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GUI
    // ═══════════════════════════════════════════════════════════════════
    private void OnGUI()
    {
        if (analysisReady) ValidateState();
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // Header
        EditorGUILayout.Space(8);
        var title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        EditorGUILayout.LabelField("⛏ HECTON MESH CLEANER v4", title);
        EditorGUILayout.LabelField("Per-triangle occlusion + Submesh fix + Hole fill", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.Space(4);

        if (analysisReady && targetObject != null)
            EditorGUILayout.HelpBox($"✓ Locked to: {analyzedObjectPath}", MessageType.None);

        // Target
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("── TARGET ──", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        targetObject = (GameObject)EditorGUILayout.ObjectField("Root GameObject", targetObject, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck()) FullReset("Target changed");

        EditorGUILayout.BeginHorizontal();
        if (Selection.activeGameObject != null && GUILayout.Button("← Use Selection", GUILayout.Height(22)))
        { if (Selection.activeGameObject != targetObject) FullReset("Selection"); targetObject = Selection.activeGameObject; }
        GUI.backgroundColor = new Color(1f, 0.8f, 0.3f);
        if (GUILayout.Button("🔄 Reset", GUILayout.Width(80), GUILayout.Height(22))) FullReset("Manual");
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        processAllLODs = EditorGUILayout.Toggle("Process ALL LODs", processAllLODs);

        if (targetObject != null)
        {
            var mfs = GetMeshFilters();
            EditorGUI.indentLevel++;
            foreach (var mf in mfs)
            {
                if (mf.sharedMesh == null) continue;
                bool r = mf.sharedMesh.isReadable;
                int sc = mf.sharedMesh.subMeshCount;
                EditorGUILayout.LabelField(
                    $"  {(r ? "✓" : "✗")} {mf.gameObject.name}: {ResolveTriangleCount(mf.sharedMesh)} tris, {sc} submesh(es)" +
                    (r ? "" : " [NOT READABLE!]"), EditorStyles.miniLabel);
            }
            EditorGUI.indentLevel--;
        }

        // Settings
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("── OCCLUSION SETTINGS ──", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Dlya kazhdogo treugolnika kastuetsya luch vdol normali i protiv nee.\n" +
            "Esli OBA napravleniya zablokirovany v predelah Occlusion Distance → treugolnik vnutrenniy → udalyaetsya.\n" +
            "Treugolnik na poverhnosti vsegda imeet hotya by odno svobodnoe napravlenie → ostaetsya.",
            MessageType.Info);

        occlusionDistance = EditorGUILayout.Slider(
            new GUIContent("Occlusion Distance", "Maks. rasstoyanie dlya obnaruzheniya blokirovki. Menshe = bezopasnee."),
            occlusionDistance, 0.01f, 5.0f);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Presets:", GUILayout.Width(50));
        if (GUILayout.Button("Tight 0.1")) occlusionDistance = 0.1f;
        if (GUILayout.Button("Normal 0.3")) occlusionDistance = 0.3f;
        if (GUILayout.Button("Wide 0.7")) occlusionDistance = 0.7f;
        if (GUILayout.Button("Huge 1.5")) occlusionDistance = 1.5f;
        EditorGUILayout.EndHorizontal();

        rayOffset = EditorGUILayout.Slider("Ray Offset", rayOffset, 0.001f, 0.05f);

        // Hole fill
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("── HOLE FILL ──", EditorStyles.boldLabel);
        enableHoleFill = EditorGUILayout.Toggle("Enable Hole Fill", enableHoleFill);
        if (enableHoleFill)
        {
            minHoleEdges = EditorGUILayout.IntSlider("Min Hole Edges", minHoleEdges, 3, 10);
            maxHoleEdges = EditorGUILayout.IntSlider("Max Hole Edges", maxHoleEdges, 10, 200);
        }

        // Preview
        EditorGUILayout.Space(4);
        showPreview = EditorGUILayout.Toggle("Show Preview", showPreview);
        if (showPreview) hiddenColor = EditorGUILayout.ColorField("Hidden Color", hiddenColor);

        // Stats
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("── RESULTS ──", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (lodResults.Count > 0)
        {
            int ts = 0, th = 0, tk = 0;
            foreach (var r in lodResults)
            {
                EditorGUILayout.LabelField($"  {r.name}: {r.srcTris} → {r.keptTris} (-{r.hiddenTris}, -{r.pctRemoved:F1}%)", EditorStyles.miniLabel);
                ts += r.srcTris; th += r.hiddenTris; tk += r.keptTris;
            }
            float tp = ts > 0 ? (float)th / ts * 100f : 0;
            EditorGUILayout.LabelField($"  TOTAL: {ts} → {tk} (-{th}, -{tp:F1}%)");
            if (lastTime > 0) EditorGUILayout.LabelField($"  Time: {lastTime:F2}s", EditorStyles.miniLabel);
        }
        else EditorGUILayout.LabelField("  Run ANALYZE first.");
        EditorGUILayout.EndVertical();

        if (!string.IsNullOrEmpty(lastStatus)) EditorGUILayout.HelpBox(lastStatus, lastStatusType);

        // Buttons
        EditorGUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = targetObject != null;
        GUI.backgroundColor = new Color(0.3f, 0.7f, 0.95f);
        if (GUILayout.Button("🔍 ANALYZE\n(preview)", GUILayout.Height(50)))
        { FullReset("Fresh analyze"); AnalyzeAll(); }

        bool canApply = targetObject != null && analysisReady && perMeshAnalysis.Count > 0
            && GetStableObjectId(targetObject) == analyzedObjectEntityId;
        GUI.enabled = canApply;
        GUI.backgroundColor = new Color(0.2f, 0.85f, 0.3f);
        if (GUILayout.Button("▶ APPLY\n(modify)", GUILayout.Height(50)))
        { if (ValidateState()) ApplyCleanup(); else SetStatus("Re-run ANALYZE!", MessageType.Error); }
        GUI.backgroundColor = Color.white; GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        GUI.enabled = targetObject != null;
        EditorGUILayout.Space(4);
        GUI.backgroundColor = new Color(0.9f, 0.6f, 0.1f);
        if (GUILayout.Button("⚡ ANALYZE + APPLY", GUILayout.Height(30)))
        { FullReset("One-click"); AnalyzeAll(); if (analysisReady && ValidateState()) ApplyCleanup(); }
        GUI.backgroundColor = Color.white; GUI.enabled = true;

        EditorGUILayout.Space(8);
        EditorGUILayout.EndScrollView();
    }

    // ═══════════════════════════════════════════════════════════════════
    // ANALYZE
    // ═══════════════════════════════════════════════════════════════════
    private void AnalyzeAll()
    {
        double t0 = EditorApplication.timeSinceStartup;
        if (targetObject == null) { SetStatus("No target!", MessageType.Error); return; }
        var mfs = GetMeshFilters();
        if (mfs.Length == 0) { SetStatus("No readable MeshFilters!", MessageType.Error); return; }

        lodResults.Clear(); perMeshAnalysis.Clear(); previewHiddenTris.Clear();
        previewSourceMesh = null; previewTarget = null;

        for (int m = 0; m < mfs.Length; m++)
        {
            EditorUtility.DisplayProgressBar("Analyzing...", $"{mfs[m].gameObject.name} ({m + 1}/{mfs.Length})", (float)m / mfs.Length);
            var res = AnalyzeMesh(mfs[m]);
            lodResults.Add(res.lod);
            perMeshAnalysis[GetStableObjectId(mfs[m])] = res.data;
        }

        EditorUtility.ClearProgressBar();
        analyzedObjectEntityId = GetStableObjectId(targetObject);
        analyzedObjectPath = GetHierarchyPath(targetObject);
        analysisReady = true;
        lastTime = EditorApplication.timeSinceStartup - t0;

        int total = 0;
        for (int i = 0; i < lodResults.Count; i++)
            total += lodResults[i].hiddenTris;
        SetStatus($"Found {total} hidden tris across {lodResults.Count} meshes.", total > 0 ? MessageType.Info : MessageType.Warning);
        SceneView.RepaintAll();
    }

    private struct AnalysisResult { public LODResult lod; public PerMeshAnalysis data; }

    private AnalysisResult AnalyzeMesh(MeshFilter mf)
    {
        Mesh mesh = mf.sharedMesh;
        analyzeLocalVerts.Clear();
        analyzeTriangles.Clear();
        mesh.GetVertices(analyzeLocalVerts);
        CollectMeshTriangles(mesh, analyzeTriangles);
        int triCount = analyzeTriangles.Count / 3;

        // Build double-sided mesh for raycasting
        Mesh dsMesh = BuildDoubleSidedMesh(mesh);

        GameObject tempGO = new GameObject("_HectonTemp_Occl");
        tempGO.hideFlags = HideFlags.HideAndDontSave;
        tempGO.layer = 31;
        tempGO.transform.position = mf.transform.position;
        tempGO.transform.rotation = mf.transform.rotation;
        tempGO.transform.localScale = mf.transform.lossyScale;

        MeshCollider tempMC = tempGO.AddComponent<MeshCollider>();
        tempMC.sharedMesh = dsMesh;
        tempMC.convex = false;
        Physics.SyncTransforms();

        int layerMask = 1 << 31;

        // Pre-transform verts to world space
        analyzeWorldVerts.Clear();
        for (int i = 0; i < analyzeLocalVerts.Count; i++)
            analyzeWorldVerts.Add(mf.transform.TransformPoint(analyzeLocalVerts[i]));

        HashSet<int> hiddenTris = new HashSet<int>();

        for (int t = 0; t < triCount; t++)
        {
            if (t % 500 == 0)
                EditorUtility.DisplayProgressBar("Analyzing...",
                    $"{mf.gameObject.name}: tri {t}/{triCount}", (float)t / triCount);

            int i0 = analyzeTriangles[t * 3], i1 = analyzeTriangles[t * 3 + 1], i2 = analyzeTriangles[t * 3 + 2];
            Vector3 wv0 = analyzeWorldVerts[i0], wv1 = analyzeWorldVerts[i1], wv2 = analyzeWorldVerts[i2];
            Vector3 center = (wv0 + wv1 + wv2) / 3f;
            Vector3 cross = Vector3.Cross(wv1 - wv0, wv2 - wv0);

            // Skip degenerate
            if (cross.sqrMagnitude < 0.001f) continue;

            Vector3 normal = DominantAxisDirection(cross);

            int reversedIdx = t + triCount; // index of reversed copy in double-sided mesh

            // Forward ray: along normal
            bool forwardBlocked = IsBlocked(center + normal * rayOffset, normal, tempMC, t, reversedIdx, layerMask);
            if (!forwardBlocked) continue; // One side free = visible = keep

            // Backward ray: against normal
            bool backwardBlocked = IsBlocked(center - normal * rayOffset, -normal, tempMC, t, reversedIdx, layerMask);
            if (!backwardBlocked) continue; // One side free = visible = keep

            // Both blocked = internal = hide
            hiddenTris.Add(t);
        }

        DestroyImmediate(dsMesh);
        DestroyImmediate(tempGO);

        // Compute original boundary edges (before any removal)
        HashSet<long> origBoundary = ComputeBoundaryEdges(analyzeTriangles, triCount, null);

        // Preview
        if (previewSourceMesh == null)
        {
            previewSourceMesh = mesh;
            previewTarget = mf.gameObject;
            previewHiddenTris = new HashSet<int>(hiddenTris);
        }

        var data = new PerMeshAnalysis
        {
            meshFilterID = GetStableObjectId(mf),
            meshID = GetStableObjectId(mesh),
            vertCount = mesh.vertexCount,
            triCount = triCount,
            goName = mf.gameObject.name,
            parentName = mf.transform.parent != null ? mf.transform.parent.name : "root",
            hiddenTris = hiddenTris,
            originalBoundaryEdges = origBoundary
        };

        var lod = new LODResult
        {
            name = mf.gameObject.name,
            srcTris = triCount,
            hiddenTris = hiddenTris.Count,
            keptTris = triCount - hiddenTris.Count,
            pctRemoved = triCount > 0 ? (float)hiddenTris.Count / triCount * 100f : 0
        };

        return new AnalysisResult { lod = lod, data = data };
    }

    private bool IsBlocked(Vector3 origin, Vector3 dir, MeshCollider target, int srcTri, int srcTriReversed, int layerMask)
    {
        int count = Physics.RaycastNonAlloc(origin, dir, rayBuffer, occlusionDistance, layerMask);
        for (int i = 0; i < count; i++)
        {
            if (rayBuffer[i].collider != target) continue;
            int hitTri = rayBuffer[i].triangleIndex;
            if (hitTri == srcTri || hitTri == srcTriReversed) continue;
            if (rayBuffer[i].distance < 0.0001f) continue; // too close = self
            return true;
        }
        return false;
    }

    // ═══════════════════════════════════════════════════════════════════
    // DOUBLE-SIDED MESH
    // ═══════════════════════════════════════════════════════════════════
    private static Vector3 DominantAxisDirection(Vector3 vector)
    {
        float ax = Mathf.Abs(vector.x);
        float ay = Mathf.Abs(vector.y);
        float az = Mathf.Abs(vector.z);

        if (ax >= ay && ax >= az)
            return vector.x < 0f ? Vector3.left : Vector3.right;

        if (ay >= az)
            return vector.y < 0f ? Vector3.down : Vector3.up;

        return vector.z < 0f ? Vector3.back : Vector3.forward;
    }

    private Mesh BuildDoubleSidedMesh(Mesh src)
    {
        doubleSidedVerts.Clear();
        doubleSidedTris.Clear();
        src.GetVertices(doubleSidedVerts);
        CollectMeshTriangles(src, doubleSidedTris);
        int triCount = doubleSidedTris.Count;

        // Reversed copy
        for (int i = 0; i < triCount; i += 3)
        {
            doubleSidedTris.Add(doubleSidedTris[i + 0]);
            doubleSidedTris.Add(doubleSidedTris[i + 2]); // swap 1 and 2
            doubleSidedTris.Add(doubleSidedTris[i + 1]);
        }

        Mesh ds = new Mesh();
        ds.hideFlags = HideFlags.HideAndDontSave;
        if (doubleSidedVerts.Count > 65535) ds.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        ds.SetVertices(doubleSidedVerts);
        ds.SetTriangles(doubleSidedTris, 0, true);
        ds.RecalculateBounds();
        return ds;
    }

    // ═══════════════════════════════════════════════════════════════════
    // APPLY
    // ═══════════════════════════════════════════════════════════════════
    private void ApplyCleanup()
    {
        if (!analysisReady || !ValidateState()) { SetStatus("Re-run ANALYZE!", MessageType.Error); return; }
        var mfs = GetMeshFilters();
        if (mfs.Length == 0) return;

        Undo.SetCurrentGroupName("Hecton Mesh Cleanup v4");
        int undoGroup = Undo.GetCurrentGroup();
        int totalRemoved = 0, totalKept = 0, processed = 0, skipped = 0;

        for (int m = 0; m < mfs.Length; m++)
        {
            MeshFilter mf = mfs[m];
            ulong mfID = GetStableObjectId(mf);

            EditorUtility.DisplayProgressBar("Applying...", $"{mf.gameObject.name} ({m + 1}/{mfs.Length})", (float)m / mfs.Length);

            if (!perMeshAnalysis.ContainsKey(mfID)) { skipped++; continue; }
            var analysis = perMeshAnalysis[mfID];
            if (mf.sharedMesh == null || GetStableObjectId(mf.sharedMesh) != analysis.meshID) { skipped++; continue; }
            if (analysis.hiddenTris.Count == 0) { skipped++; continue; }

            Mesh cleaned = BuildCleanedMesh(mf.sharedMesh, analysis);
            if (cleaned == null) { skipped++; continue; }

            string name = GetUniqueSaveName(mf);
            string path = SaveMesh(cleaned, name);
            cleaned = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            Undo.RecordObject(mf, "Clean Mesh");
            mf.sharedMesh = cleaned;

            MeshCollider mc = mf.GetComponent<MeshCollider>();
            if (mc != null) { Undo.RecordObject(mc, "Update Collider"); mc.sharedMesh = cleaned; }

            // Also update MeshColliders on sibling/child objects referencing same mesh
            var renderer = mf.GetComponent<MeshRenderer>();
            if (renderer != null) EditorUtility.SetDirty(renderer);

            totalRemoved += analysis.hiddenTris.Count;
            totalKept += analysis.triCount - analysis.hiddenTris.Count;
            processed++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        FullReset("Applied");

        SetStatus($"✓ {processed} meshes cleaned. Removed {totalRemoved} tris. Kept {totalKept}. Ctrl+Z to undo.", MessageType.Info);
        SceneView.RepaintAll();
    }

    // ═══════════════════════════════════════════════════════════════════
    // BUILD CLEANED MESH (SUBMESH-AWARE + HOLE FILL)
    // ═══════════════════════════════════════════════════════════════════
    private Mesh BuildCleanedMesh(Mesh source, PerMeshAnalysis analysis)
    {
        // Read all vertex data channels
        List<Vector3> srcVerts = new List<Vector3>(source.vertexCount);
        List<Vector3> srcNormals = new List<Vector3>(source.vertexCount);
        List<Vector2> srcUV0 = new List<Vector2>(source.vertexCount);
        List<Vector2> srcUV1 = new List<Vector2>(source.vertexCount);
        List<Vector2> srcUV2 = new List<Vector2>(source.vertexCount);
        List<Vector2> srcUV3 = new List<Vector2>(source.vertexCount);
        List<Vector4> srcTangents = new List<Vector4>(source.vertexCount);
        List<Color> srcColors = new List<Color>(source.vertexCount);
        List<Color32> srcColors32 = new List<Color32>(source.vertexCount);
        source.GetVertices(srcVerts);
        source.GetNormals(srcNormals);
        source.GetUVs(0, srcUV0);
        source.GetUVs(1, srcUV1);
        source.GetUVs(2, srcUV2);
        source.GetUVs(3, srcUV3);
        source.GetTangents(srcTangents);
        source.GetColors(srcColors);
        source.GetColors(srcColors32);
        List<int> allTris = new List<int>((int)global::System.Math.Min(ResolveIndexCount(source), int.MaxValue));
        CollectMeshTriangles(source, allTris);
        int totalTriCount = allTris.Count / 3;

        bool hasNormals = srcNormals.Count == srcVerts.Count;
        bool hasUV0 = srcUV0.Count == srcVerts.Count;
        bool hasUV1 = srcUV1.Count == srcVerts.Count;
        bool hasUV2 = srcUV2.Count == srcVerts.Count;
        bool hasUV3 = srcUV3.Count == srcVerts.Count;
        bool hasTangents = srcTangents.Count == srcVerts.Count;
        bool hasColors = srcColors.Count == srcVerts.Count;
        bool hasColors32 = !hasColors && srcColors32.Count == srcVerts.Count;

        // ── Map each flat triangle index → submesh index ──
        int subMeshCount = source.subMeshCount;
        int[] triToSubmesh = new int[totalTriCount];
        for (int s = 0; s < subMeshCount; s++)
        {
            var desc = source.GetSubMesh(s);
            int startTri = desc.indexStart / 3;
            int count = desc.indexCount / 3;
            for (int t = 0; t < count; t++)
                triToSubmesh[startTri + t] = s;
        }

        // ── Collect kept triangles per submesh ──
        List<int>[] keptPerSubmesh = new List<int>[subMeshCount];
        for (int s = 0; s < subMeshCount; s++) keptPerSubmesh[s] = new List<int>();

        for (int t = 0; t < totalTriCount; t++)
        {
            if (!analysis.hiddenTris.Contains(t))
            {
                int s = triToSubmesh[t];
                keptPerSubmesh[s].Add(allTris[t * 3]);
                keptPerSubmesh[s].Add(allTris[t * 3 + 1]);
                keptPerSubmesh[s].Add(allTris[t * 3 + 2]);
            }
        }

        // ── Hole Fill ──
        List<int> fillTris = new List<int>(); // will go into submesh 0
        List<Vector3> extraVerts = new List<Vector3>();
        List<Vector3> extraNormals = new List<Vector3>();

        if (enableHoleFill)
        {
            // Find NEW boundary edges (edges that became boundary after removal)
            HashSet<long> keptBoundary = ComputeBoundaryEdgesFromSubmeshLists(keptPerSubmesh);

            // New holes = boundary edges that weren't boundary in original
            HashSet<long> newHoleEdges = new HashSet<long>();
            foreach (long e in keptBoundary)
            {
                if (!analysis.originalBoundaryEdges.Contains(e))
                    newHoleEdges.Add(e);
            }

            if (newHoleEdges.Count > 0)
            {
                // Trace loops
                List<List<int>> loops = TraceLoops(newHoleEdges);

                int extraVertBase = srcVerts.Count;

                foreach (var loop in loops)
                {
                    if (loop.Count < minHoleEdges || loop.Count > maxHoleEdges) continue;

                    // Compute centroid
                    Vector3 centroid = Vector3.zero;
                    Vector3 avgNormal = Vector3.zero;
                    foreach (int vi in loop)
                    {
                        centroid += srcVerts[vi];
                        if (hasNormals) avgNormal += srcNormals[vi];
                    }
                    centroid /= loop.Count;
                    avgNormal = DominantAxisDirection(avgNormal);

                    int centroidIdx = extraVertBase + extraVerts.Count;
                    extraVerts.Add(centroid);
                    extraNormals.Add(avgNormal);

                    // Fan triangulate
                    for (int i = 0; i < loop.Count; i++)
                    {
                        int a = loop[i];
                        int b = loop[(i + 1) % loop.Count];

                        // Create fan triangle: centroid, a, b
                        // Check winding by comparing with avgNormal
                        Vector3 triNormal = Vector3.Cross(srcVerts[a] - centroid, srcVerts[b] - centroid);
                        if (Vector3.Dot(triNormal, avgNormal) < 0)
                        {
                            fillTris.Add(centroidIdx);
                            fillTris.Add(b);
                            fillTris.Add(a);
                        }
                        else
                        {
                            fillTris.Add(centroidIdx);
                            fillTris.Add(a);
                            fillTris.Add(b);
                        }
                    }
                }
            }
        }

        // ── Compact vertices ──
        // Find all used vertex indices
        HashSet<int> usedVerts = new HashSet<int>();
        for (int s = 0; s < subMeshCount; s++)
            foreach (int idx in keptPerSubmesh[s]) usedVerts.Add(idx);
        foreach (int idx in fillTris)
        {
            if (idx < srcVerts.Count) usedVerts.Add(idx);
            // Extra verts are always "used"
        }

        // Build remap: old index → new index
        Dictionary<int, int> remap = new Dictionary<int, int>();
        List<Vector3> newVerts = new List<Vector3>();
        List<Vector3> newNormals = new List<Vector3>();
        List<Vector2> newUV0 = new List<Vector2>();
        List<Vector2> newUV1List = new List<Vector2>();
        List<Vector2> newUV2List = new List<Vector2>();
        List<Vector2> newUV3List = new List<Vector2>();
        List<Vector4> newTangents = new List<Vector4>();
        List<Color> newColors = new List<Color>();
        List<Color32> newColors32 = new List<Color32>();

        // Sort for deterministic output
        List<int> sortedUsed = new List<int>(usedVerts);
        sortedUsed.Sort();
        for (int i = 0; i < sortedUsed.Count; i++)
        {
            int old = sortedUsed[i];
            remap[old] = newVerts.Count;
            newVerts.Add(srcVerts[old]);
            if (hasNormals) newNormals.Add(srcNormals[old]);
            if (hasUV0) newUV0.Add(srcUV0[old]);
            if (hasUV1) newUV1List.Add(srcUV1[old]);
            if (hasUV2) newUV2List.Add(srcUV2[old]);
            if (hasUV3) newUV3List.Add(srcUV3[old]);
            if (hasTangents) newTangents.Add(srcTangents[old]);
            if (hasColors) newColors.Add(srcColors[old]);
            if (hasColors32) newColors32.Add(srcColors32[old]);
        }

        // Add extra verts (hole fill centroids)
        for (int i = 0; i < extraVerts.Count; i++)
        {
            int oldIdx = srcVerts.Count + i;
            remap[oldIdx] = newVerts.Count;
            newVerts.Add(extraVerts[i]);
            if (hasNormals) newNormals.Add(extraNormals[i]);
            if (hasUV0) newUV0.Add(Vector2.zero);
            if (hasUV1) newUV1List.Add(Vector2.zero);
            if (hasUV2) newUV2List.Add(Vector2.zero);
            if (hasUV3) newUV3List.Add(Vector2.zero);
            if (hasTangents) newTangents.Add(Vector4.zero);
            if (hasColors) newColors.Add(Color.white);
            if (hasColors32) newColors32.Add(new Color32(255, 255, 255, 255));
        }

        // Remap submesh triangle lists
        for (int s = 0; s < subMeshCount; s++)
        {
            for (int i = 0; i < keptPerSubmesh[s].Count; i++)
                keptPerSubmesh[s][i] = remap[keptPerSubmesh[s][i]];
        }

        // Add fill tris to submesh 0
        if (fillTris.Count > 0)
        {
            for (int i = 0; i < fillTris.Count; i++)
                fillTris[i] = remap[fillTris[i]];

            keptPerSubmesh[0].AddRange(fillTris);
        }

        // ── Build mesh ──
        Mesh result = new Mesh();
        result.name = source.name + "_cleaned";
        if (newVerts.Count > 65535) result.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        result.SetVertices(newVerts);
        if (hasNormals) result.SetNormals(newNormals);
        if (hasUV0) result.SetUVs(0, newUV0);
        if (hasUV1) result.SetUVs(1, newUV1List);
        if (hasUV2) result.SetUVs(2, newUV2List);
        if (hasUV3) result.SetUVs(3, newUV3List);
        if (hasTangents) result.SetTangents(newTangents);
        if (hasColors) result.SetColors(newColors);
        if (hasColors32) result.SetColors(newColors32);

        result.subMeshCount = subMeshCount;
        for (int s = 0; s < subMeshCount; s++)
            result.SetTriangles(keptPerSubmesh[s], s);

        result.RecalculateBounds();
        if (!hasNormals) result.RecalculateNormals();
        if (!hasTangents) result.RecalculateTangents();

        return result;
    }

    // ═══════════════════════════════════════════════════════════════════
    // BOUNDARY EDGES & HOLE FILL
    // ═══════════════════════════════════════════════════════════════════
    private static long PackEdge(int a, int b)
    {
        int lo = Mathf.Min(a, b);
        int hi = Mathf.Max(a, b);
        return ((long)lo << 32) | (long)(uint)hi;
    }

    private static void UnpackEdge(long packed, out int a, out int b)
    {
        a = (int)(packed >> 32);
        b = (int)(packed & 0xFFFFFFFF);
    }

    /// <summary>
    /// Find boundary edges: edges used by exactly 1 triangle.
    /// skipTris: set of triangle indices to exclude (hidden ones). Null = include all.
    /// </summary>
    private static HashSet<long> ComputeBoundaryEdges(IList<int> tris, int triCount, HashSet<int> skipTris)
    {
        Dictionary<long, int> edgeCount = new Dictionary<long, int>();
        for (int t = 0; t < triCount; t++)
        {
            if (skipTris != null && skipTris.Contains(t)) continue;
            int i0 = tris[t * 3], i1 = tris[t * 3 + 1], i2 = tris[t * 3 + 2];
            IncEdge(edgeCount, PackEdge(i0, i1));
            IncEdge(edgeCount, PackEdge(i1, i2));
            IncEdge(edgeCount, PackEdge(i2, i0));
        }

        HashSet<long> boundary = new HashSet<long>();
        foreach (var kvp in edgeCount)
            if (kvp.Value == 1) boundary.Add(kvp.Key);
        return boundary;
    }

    private static HashSet<long> ComputeBoundaryEdgesFromSubmeshLists(List<int>[] submeshTris)
    {
        Dictionary<long, int> edgeCount = new Dictionary<long, int>();
        foreach (var triList in submeshTris)
        {
            for (int i = 0; i < triList.Count; i += 3)
            {
                IncEdge(edgeCount, PackEdge(triList[i], triList[i + 1]));
                IncEdge(edgeCount, PackEdge(triList[i + 1], triList[i + 2]));
                IncEdge(edgeCount, PackEdge(triList[i + 2], triList[i]));
            }
        }
        HashSet<long> boundary = new HashSet<long>();
        foreach (var kvp in edgeCount)
            if (kvp.Value == 1) boundary.Add(kvp.Key);
        return boundary;
    }

    private static void IncEdge(Dictionary<long, int> dict, long key)
    {
        if (dict.ContainsKey(key)) dict[key]++;
        else dict[key] = 1;
    }

    /// <summary>
    /// Trace boundary edges into closed loops.
    /// </summary>
    private static List<List<int>> TraceLoops(HashSet<long> edges)
    {
        // Build adjacency: vertex → list of connected vertices via boundary edges
        Dictionary<int, List<int>> adj = new Dictionary<int, List<int>>();
        foreach (long e in edges)
        {
            UnpackEdge(e, out int a, out int b);
            if (!adj.ContainsKey(a)) adj[a] = new List<int>();
            if (!adj.ContainsKey(b)) adj[b] = new List<int>();
            adj[a].Add(b);
            adj[b].Add(a);
        }

        HashSet<long> visited = new HashSet<long>();
        List<List<int>> loops = new List<List<int>>();

        foreach (long startEdge in edges)
        {
            if (visited.Contains(startEdge)) continue;

            UnpackEdge(startEdge, out int startA, out int startB);
            visited.Add(startEdge);

            List<int> loop = new List<int>();
            loop.Add(startA);
            loop.Add(startB);

            int current = startB;
            int prev = startA;
            int maxSteps = 10000;
            bool closed = false;

            for (int step = 0; step < maxSteps; step++)
            {
                if (!adj.ContainsKey(current)) break;

                int next = -1;
                foreach (int neighbor in adj[current])
                {
                    if (neighbor == prev) continue;
                    long candidateEdge = PackEdge(current, neighbor);
                    if (!edges.Contains(candidateEdge)) continue;
                    if (visited.Contains(candidateEdge)) 
                    {
                        if (neighbor == startA && loop.Count >= 3)
                        {
                            visited.Add(candidateEdge);
                            closed = true;
                            break;
                        }
                        continue;
                    }
                    next = neighbor;
                    break;
                }

                if (closed) break;
                if (next == -1) break;

                visited.Add(PackEdge(current, next));
                loop.Add(next);
                prev = current;
                current = next;
            }

            if (closed && loop.Count >= 3)
                loops.Add(loop);
        }

        return loops;
    }

    // ═══════════════════════════════════════════════════════════════════
    // UTILITY
    // ═══════════════════════════════════════════════════════════════════
    private MeshFilter[] GetMeshFilters()
    {
        if (targetObject == null) return new MeshFilter[0];
        List<MeshFilter> list = new List<MeshFilter>();
        if (processAllLODs)
        {
            foreach (var mf in targetObject.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && mf.sharedMesh.isReadable) list.Add(mf);
        }
        else
        {
            var mf = targetObject.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable) list.Add(mf);
        }
        return list.ToArray();
    }

    private string SaveMesh(Mesh mesh, string name)
    {
        string folder = "Assets/_Project/Art/Meshes/Cleaned";
        EnsureFolder(folder);
        string path = $"{folder}/{name}_cleaned.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) EditorUtility.CopySerialized(mesh, existing);
        else AssetDatabase.CreateAsset(mesh, path);
        return path;
    }

    private void CleanupTemp()
    {
        foreach (var g in UnityEngine.Object.FindObjectsByType<GameObject>())
            if (g.name.StartsWith("_HectonTemp_")) DestroyImmediate(g);
    }

    private void SetStatus(string msg, MessageType type) { lastStatus = msg; lastStatusType = type; Repaint(); }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string[] parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    private static string SanitizeName(string n)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
        return n.Replace(' ', '_').Replace('.', '_');
    }

    // ═══════════════════════════════════════════════════════════════════
    // SCENE PREVIEW
    // ═══════════════════════════════════════════════════════════════════
    private void OnSceneGUI(SceneView sv)
    {
        if (!showPreview || !analysisReady || previewSourceMesh == null || previewTarget == null) return;
        if (targetObject == null || GetStableObjectId(targetObject) != analyzedObjectEntityId) return;

        List<Vector3> verts = new List<Vector3>(previewSourceMesh.vertexCount);
        List<int> tris = new List<int>((int)global::System.Math.Min(ResolveIndexCount(previewSourceMesh), int.MaxValue));
        previewSourceMesh.GetVertices(verts);
        CollectMeshTriangles(previewSourceMesh, tris);
        int triCount = tris.Count / 3;

        Handles.matrix = previewTarget.transform.localToWorldMatrix;
        Handles.color = hiddenColor;

        foreach (int t in previewHiddenTris)
        {
            if (t >= triCount) continue;
            Vector3 a = verts[tris[t * 3]], b = verts[tris[t * 3 + 1]], c = verts[tris[t * 3 + 2]];
            Handles.DrawLine(a, b);
            Handles.DrawLine(b, c);
            Handles.DrawLine(c, a);
        }

        Handles.matrix = Matrix4x4.identity;
        Handles.Label(previewTarget.transform.position + Vector3.up * 2f,
            $"Hidden: {previewHiddenTris.Count} tris (RED)\n{analyzedObjectPath}",
            EditorStyles.whiteBoldLabel);
    }
}
