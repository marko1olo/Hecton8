#if UNITY_EDITOR
using System.Collections.Generic;
using Hecton8.Building;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class HectonSurfacePainter : EditorWindow
{
    private const string WindowTitle = "Hecton Surface Painter";
    private const string SocketsContainerName = "SOCKETS_CONTAINER";

    private enum PlacementMode
    {
        Single,
        Scatter
    }

    private enum ScatterDistributionMode
    {
        Random,
        GridJitter,
        Poissonish
    }

    private Transform targetRoot;
    private bool paintModeEnabled;

    private PlacementMode placementMode = PlacementMode.Single;
    private ScatterDistributionMode scatterDistributionMode = ScatterDistributionMode.Random;

    private bool autoDetectType = true;
    private HectonSocketHelper.SocketType manualSocketType = HectonSocketHelper.SocketType.Side;

    private float minDistance = 0.5f;
    private float normalOffset = 0.001f;
    private float previewDiscRadius = 0.08f;
    private bool selectCreatedSocket = false;

    private float brushRadius = 0.5f;
    private int brushAttempts = 12;
    private float scatterRayHeight = 1.0f;
    private float distributionJitter = 0.85f;
    private int poissonRelaxationPasses = 2;

    private bool enableDeleteMode = true;
    private float deleteNearestMaxDistance = 0.75f;

    private string ignoreNameTokens = "LOD1;LOD2;proxy;collision;helper";

    private bool hasValidHit;
    private MeshRaycastHit currentHit;

    private readonly List<MeshFilter> cachedMeshFilters = new List<MeshFilter>(128);
    private readonly List<string> cachedIgnoreTokens = new List<string>(16);
    private readonly List<Vector2> scatterSamples2D = new List<Vector2>(256);

    private int cachedHierarchyHash;
    private int cachedIgnoreTokensHash;
    private double lastCacheBuildTime;

    private struct MeshRaycastHit
    {
        public bool valid;
        public float distance;
        public Vector3 point;
        public Vector3 normal;
        public Transform hitTransform;
        public MeshFilter meshFilter;
        public int triangleIndex;
    }

    [MenuItem("Hecton/Building/Surface Painter")]
    public static void OpenWindow()
    {
        HectonSurfacePainter window = GetWindow<HectonSurfacePainter>();
        window.titleContent = new GUIContent(WindowTitle);
        window.minSize = new Vector2(460f, 620f);
        window.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        Selection.selectionChanged += OnSelectionChanged;

        if (targetRoot == null && Selection.activeTransform != null)
            targetRoot = Selection.activeTransform;

        RebuildIgnoreTokenCache();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Selection.selectionChanged -= OnSelectionChanged;
    }

    private void OnSelectionChanged()
    {
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Root", EditorStyles.boldLabel);

            Transform newRoot = (Transform)EditorGUILayout.ObjectField(
                new GUIContent("Target Root", "Painter raycasts only against MeshFilters inside this hierarchy."),
                targetRoot,
                typeof(Transform),
                true);

            if (newRoot != targetRoot)
            {
                targetRoot = newRoot;
                InvalidateMeshCache();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selection as Root", GUILayout.Height(24f)))
                {
                    if (Selection.activeTransform != null)
                    {
                        targetRoot = Selection.activeTransform;
                        InvalidateMeshCache();
                        Repaint();
                        SceneView.RepaintAll();
                    }
                }

                if (GUILayout.Button("Ping Sockets", GUILayout.Height(24f)))
                {
                    Transform sockets = GetSocketsContainer(targetRoot);
                    if (sockets != null)
                    {
                        EditorGUIUtility.PingObject(sockets.gameObject);
                        Selection.activeGameObject = sockets.gameObject;
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild Mesh Cache", GUILayout.Height(22f)))
                {
                    RebuildMeshCache();
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("Focus Root", GUILayout.Height(22f)))
                {
                    if (targetRoot != null)
                    {
                        Selection.activeTransform = targetRoot;
                        SceneView.lastActiveSceneView?.FrameSelected();
                    }
                }
            }
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Paint Settings", EditorStyles.boldLabel);

            bool newPaintMode = EditorGUILayout.ToggleLeft(
                new GUIContent("Enable Paint Mode", "Shift + Left Click creates sockets. Shift + Right Click deletes nearest socket."),
                paintModeEnabled);

            if (newPaintMode != paintModeEnabled)
            {
                paintModeEnabled = newPaintMode;
                SceneView.RepaintAll();
            }

            placementMode = (PlacementMode)EditorGUILayout.EnumPopup(
                new GUIContent("Placement Mode", "Single places one socket. Scatter places multiple sockets in a brush area."),
                placementMode);

            autoDetectType = EditorGUILayout.Toggle(
                new GUIContent("Auto-Detect Type", "Top if normal.y > 0.5, Under if normal.y < -0.2, otherwise Side."),
                autoDetectType);

            using (new EditorGUI.DisabledScope(autoDetectType))
            {
                manualSocketType = (HectonSocketHelper.SocketType)EditorGUILayout.EnumPopup(
                    new GUIContent("Manual Socket Type", "Used only when Auto-Detect Type is disabled."),
                    manualSocketType);
            }

            minDistance = Mathf.Max(0f, EditorGUILayout.Slider(
                new GUIContent("Min Distance", "Do not create a socket if another socket is closer than this value."),
                minDistance,
                0f,
                5f));

            normalOffset = Mathf.Max(0f, EditorGUILayout.Slider(
                new GUIContent("Normal Offset", "Push created socket slightly along surface normal."),
                normalOffset,
                0f,
                0.1f));

            previewDiscRadius = Mathf.Max(0.005f, EditorGUILayout.Slider(
                new GUIContent("Single Preview Radius", "Radius of single placement preview disc."),
                previewDiscRadius,
                0.01f,
                1.0f));

            selectCreatedSocket = EditorGUILayout.Toggle(
                new GUIContent("Select Created Socket", "If enabled, newly created socket becomes current selection."),
                selectCreatedSocket);
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Scatter Brush", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(placementMode != PlacementMode.Scatter))
            {
                scatterDistributionMode = (ScatterDistributionMode)EditorGUILayout.EnumPopup(
                    new GUIContent("Distribution Mode", "Random, Grid Jitter or Poisson-ish distribution inside the brush."),
                    scatterDistributionMode);

                brushRadius = Mathf.Max(0.01f, EditorGUILayout.Slider(
                    new GUIContent("Brush Radius", "Radius of scatter brush."),
                    brushRadius,
                    0.01f,
                    5.0f));

                brushAttempts = Mathf.Max(1, EditorGUILayout.IntSlider(
                    new GUIContent("Brush Attempts", "How many candidate placements are generated per click."),
                    brushAttempts,
                    1,
                    128));

                scatterRayHeight = Mathf.Max(0.01f, EditorGUILayout.Slider(
                    new GUIContent("Scatter Ray Height", "How far above the surface plane scatter rays start."),
                    scatterRayHeight,
                    0.01f,
                    5.0f));

                distributionJitter = Mathf.Clamp01(EditorGUILayout.Slider(
                    new GUIContent("Distribution Jitter", "How much randomness is injected into structured distributions."),
                    distributionJitter,
                    0f,
                    1f));

                poissonRelaxationPasses = Mathf.Max(0, EditorGUILayout.IntSlider(
                    new GUIContent("Poisson Relaxation", "Extra relaxation passes for Poisson-ish distribution."),
                    poissonRelaxationPasses,
                    0,
                    6));
            }
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Delete Settings", EditorStyles.boldLabel);

            enableDeleteMode = EditorGUILayout.Toggle(
                new GUIContent("Enable Delete Mode", "Shift + Right Click removes nearest socket under max distance."),
                enableDeleteMode);

            using (new EditorGUI.DisabledScope(!enableDeleteMode))
            {
                deleteNearestMaxDistance = Mathf.Max(0.01f, EditorGUILayout.Slider(
                    new GUIContent("Delete Max Distance", "Maximum distance from cursor hit point to nearest socket to allow deletion."),
                    deleteNearestMaxDistance,
                    0.01f,
                    5.0f));
            }
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Mesh Filtering", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            ignoreNameTokens = EditorGUILayout.TextField(
                new GUIContent("Ignore Name Tokens", "Semicolon-separated substrings. Mesh objects containing these tokens are ignored."),
                ignoreNameTokens);

            if (EditorGUI.EndChangeCheck())
            {
                RebuildIgnoreTokenCache();
                InvalidateMeshCache();
            }

            EditorGUILayout.LabelField("Example:", "LOD1;LOD2;proxy;collision;helper");
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Manual Type Hotkeys", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1 = Top");
            EditorGUILayout.LabelField("2 = Side");
            EditorGUILayout.LabelField("3 = Under");
            EditorGUILayout.LabelField("Hotkeys work only when Auto-Detect Type is OFF.");
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);

            if (targetRoot == null)
            {
                EditorGUILayout.HelpBox("Assign Target Root to start painting.", MessageType.Warning);
            }
            else
            {
                Transform sockets = GetSocketsContainer(targetRoot);
                int socketCount = CountSockets(sockets);

                EditorGUILayout.LabelField("Root", targetRoot.name);
                EditorGUILayout.LabelField("Paint Meshes", GetValidPaintMeshCountPreview(targetRoot).ToString());
                EditorGUILayout.LabelField("Sockets", socketCount.ToString());
                EditorGUILayout.LabelField("Cache Size", cachedMeshFilters.Count.ToString());
                EditorGUILayout.LabelField("Placement", placementMode.ToString());

                if (placementMode == PlacementMode.Scatter)
                    EditorGUILayout.LabelField("Scatter Distribution", scatterDistributionMode.ToString());

                string typeText = autoDetectType ? "Auto" : manualSocketType.ToString();
                EditorGUILayout.LabelField("Mode", paintModeEnabled ? $"Paint Enabled ({typeText})" : "Paint Disabled");

                if (hasValidHit && currentHit.valid && currentHit.hitTransform != null)
                {
                    EditorGUILayout.LabelField("Hit Mesh", currentHit.hitTransform.name);
                    EditorGUILayout.LabelField("Hit Triangle", currentHit.triangleIndex.ToString());
                }
                else
                {
                    EditorGUILayout.LabelField("Hit Mesh", "None");
                }
            }
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Usage", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Shift + LMB = create");
            EditorGUILayout.LabelField("Shift + RMB = delete nearest");
            EditorGUILayout.LabelField("Single = one socket");
            EditorGUILayout.LabelField("Scatter = many sockets in brush area");
            EditorGUILayout.LabelField("Distribution Mode changes how scatter points are distributed");
            EditorGUILayout.LabelField("Sockets are auto-parented into SOCKETS_CONTAINER");
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!paintModeEnabled)
            return;

        Event evt = Event.current;
        if (evt == null)
            return;

        if (targetRoot == null)
            return;

        HandleHotkeys(evt);

        EnsureMeshCache();

        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlId);

        UpdateMeshRaycastHit(evt.mousePosition);
        DrawSurfacePreview();
        DrawNearestSocketPreview();
        DrawSceneOverlay();

        if (evt.alt)
            return;

        if (evt.type == EventType.MouseDown &&
            evt.shift &&
            !evt.control &&
            !evt.command)
        {
            if (evt.button == 0)
            {
                if (hasValidHit)
                {
                    if (placementMode == PlacementMode.Single)
                        TryCreateSocket(currentHit);
                    else
                        TryScatterSockets(currentHit);
                }

                evt.Use();
            }
            else if (evt.button == 1 && enableDeleteMode)
            {
                if (hasValidHit)
                    TryDeleteNearestSocket(currentHit.point);

                evt.Use();
            }
        }

        if (evt.type == EventType.MouseMove || evt.type == EventType.Layout || evt.type == EventType.Repaint)
            sceneView.Repaint();
    }

    private void HandleHotkeys(Event evt)
    {
        if (autoDetectType)
            return;

        if (evt.type != EventType.KeyDown)
            return;

        switch (evt.keyCode)
        {
            case KeyCode.Alpha1:
            case KeyCode.Keypad1:
                manualSocketType = HectonSocketHelper.SocketType.Top;
                Repaint();
                SceneView.RepaintAll();
                evt.Use();
                break;

            case KeyCode.Alpha2:
            case KeyCode.Keypad2:
                manualSocketType = HectonSocketHelper.SocketType.Side;
                Repaint();
                SceneView.RepaintAll();
                evt.Use();
                break;

            case KeyCode.Alpha3:
            case KeyCode.Keypad3:
                manualSocketType = HectonSocketHelper.SocketType.Under;
                Repaint();
                SceneView.RepaintAll();
                evt.Use();
                break;
        }
    }

    private void TryCreateSocket(MeshRaycastHit hit)
    {
        if (!hit.valid || targetRoot == null)
            return;

        Transform socketsContainer = GetOrCreateSocketsContainer(targetRoot);
        TryCreateSocketInternal(socketsContainer, hit.point, hit.normal);
    }

    private void TryScatterSockets(MeshRaycastHit centerHit)
    {
        if (!centerHit.valid || targetRoot == null)
            return;

        Transform socketsContainer = GetOrCreateSocketsContainer(targetRoot);

        Vector3 centerNormal = centerHit.normal.normalized;
        BuildSurfaceBasis(centerNormal, out Vector3 tangent, out Vector3 bitangent);

        GenerateScatterSamples2D();

        for (int i = 0; i < scatterSamples2D.Count; i++)
        {
            Vector2 sample2D = scatterSamples2D[i];

            Vector3 planeOffset =
                tangent * sample2D.x +
                bitangent * sample2D.y;

            Vector3 rayOrigin =
                centerHit.point +
                planeOffset +
                centerNormal * scatterRayHeight;

            Ray scatterRay = new Ray(rayOrigin, -centerNormal);

            if (!RaycastAgainstCachedMeshes(scatterRay, out MeshRaycastHit scatterHit))
                continue;

            TryCreateSocketInternal(socketsContainer, scatterHit.point, scatterHit.normal);
        }
    }

    private void GenerateScatterSamples2D()
    {
        scatterSamples2D.Clear();

        switch (scatterDistributionMode)
        {
            case ScatterDistributionMode.Random:
                GenerateRandomSamples2D(scatterSamples2D, brushRadius, brushAttempts);
                break;

            case ScatterDistributionMode.GridJitter:
                GenerateGridJitterSamples2D(scatterSamples2D, brushRadius, brushAttempts, distributionJitter);
                break;

            case ScatterDistributionMode.Poissonish:
                GeneratePoissonishSamples2D(scatterSamples2D, brushRadius, brushAttempts, distributionJitter, poissonRelaxationPasses);
                break;
        }
    }

    private static void GenerateRandomSamples2D(List<Vector2> results, float radius, int attempts)
    {
        for (int i = 0; i < attempts; i++)
        {
            Vector2 sample = Random.insideUnitCircle * radius;
            results.Add(sample);
        }
    }

    private static void GenerateGridJitterSamples2D(List<Vector2> results, float radius, int attempts, float jitter)
    {
        if (attempts <= 0)
            return;

        int grid = Mathf.CeilToInt(Mathf.Sqrt(attempts));
        float diameter = radius * 2f;
        float cellSize = diameter / grid;

        for (int y = 0; y < grid; y++)
        {
            for (int x = 0; x < grid; x++)
            {
                if (results.Count >= attempts)
                    return;

                float baseX = -radius + cellSize * (x + 0.5f);
                float baseY = -radius + cellSize * (y + 0.5f);

                float jitterRange = cellSize * 0.5f * jitter;

                float jitterX = Random.Range(-jitterRange, jitterRange);
                float jitterY = Random.Range(-jitterRange, jitterRange);

                Vector2 sample = new Vector2(baseX + jitterX, baseY + jitterY);

                if (sample.sqrMagnitude <= radius * radius)
                    results.Add(sample);
            }
        }

        while (results.Count < attempts)
        {
            Vector2 sample = Random.insideUnitCircle * radius;
            results.Add(sample);
        }
    }

    private static void GeneratePoissonishSamples2D(
        List<Vector2> results,
        float radius,
        int attempts,
        float jitter,
        int relaxationPasses)
    {
        if (attempts <= 0)
            return;

        // Shag 1: startuem s jitter-grid kak s nachalnogo priblizheniya
        GenerateGridJitterSamples2D(results, radius, attempts, Mathf.Lerp(0.2f, 1f, jitter));

        if (results.Count <= 1 || relaxationPasses <= 0)
            return;

        float radiusSqr = radius * radius;

        // Shag 2: neskolko ochen deshevyh iteratsiy "rasslableniya"
        // chtoby tochki chut razoshlis i byli menee klasternymi.
        for (int pass = 0; pass < relaxationPasses; pass++)
        {
            for (int i = 0; i < results.Count; i++)
            {
                Vector2 push = Vector2.zero;
                Vector2 a = results[i];

                for (int j = 0; j < results.Count; j++)
                {
                    if (i == j)
                        continue;

                    Vector2 b = results[j];
                    Vector2 delta = a - b;
                    float sqr = delta.sqrMagnitude;

                    if (sqr < 0.000001f)
                    {
                        push += Random.insideUnitCircle * 0.01f;
                        continue;
                    }

                    float dist = Mathf.Sqrt(sqr);
                    float desired = (radius * 2f) / Mathf.Sqrt(results.Count);

                    if (dist < desired)
                    {
                        float strength = (desired - dist) / desired;
                        push += (delta / dist) * strength * 0.1f;
                    }
                }

                a += push;

                // Kray kruga
                if (a.sqrMagnitude > radiusSqr)
                    a = a.normalized * radius * Random.Range(0.92f, 1f);

                results[i] = a;
            }
        }

        // Shag 3: legkiy finalnyy dzhitter, chtoby setka ne chitalas
        float finalJitter = radius * 0.05f * jitter;
        for (int i = 0; i < results.Count; i++)
        {
            Vector2 sample = results[i] + Random.insideUnitCircle * finalJitter;
            if (sample.sqrMagnitude > radiusSqr)
                sample = sample.normalized * radius * Random.Range(0.92f, 1f);

            results[i] = sample;
        }
    }

    private void TryCreateSocketInternal(Transform socketsContainer, Vector3 hitPoint, Vector3 hitNormal)
    {
        Vector3 finalPosition = hitPoint + hitNormal * normalOffset;

        if (IsTooCloseToExistingSocket(socketsContainer, finalPosition, minDistance))
            return;

        HectonSocketHelper.SocketType finalType = autoDetectType
            ? DetectSocketType(hitNormal)
            : manualSocketType;

        string socketName = GenerateNextSocketName(socketsContainer, finalType);

        GameObject socketObject = new GameObject(socketName);
        Undo.RegisterCreatedObjectUndo(socketObject, "Paint Hecton Socket");

        Transform socketTransform = socketObject.transform;
        socketTransform.position = finalPosition;
        socketTransform.rotation = Quaternion.LookRotation(hitNormal, GetStableUp(hitNormal));

        Undo.SetTransformParent(socketTransform, socketsContainer, "Parent Painted Hecton Socket");

        HectonSocketHelper helper = socketObject.AddComponent<HectonSocketHelper>();

        SerializedObject so = new SerializedObject(helper);
        SerializedProperty socketTypeProp = so.FindProperty("socketType");
        if (socketTypeProp != null)
        {
            socketTypeProp.enumValueIndex = (int)finalType;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(helper);
        EditorUtility.SetDirty(socketObject);
        EditorUtility.SetDirty(socketsContainer.gameObject);

        if (selectCreatedSocket)
            Selection.activeGameObject = socketObject;
    }

    private void TryDeleteNearestSocket(Vector3 referencePoint)
    {
        Transform socketsContainer = GetSocketsContainer(targetRoot);
        if (socketsContainer == null)
            return;

        Transform nearest = FindNearestSocket(socketsContainer, referencePoint, deleteNearestMaxDistance);
        if (nearest == null)
            return;

        Undo.DestroyObjectImmediate(nearest.gameObject);
        EditorUtility.SetDirty(socketsContainer.gameObject);
    }

    private void UpdateMeshRaycastHit(Vector2 mousePosition)
    {
        hasValidHit = false;
        currentHit = default;

        if (targetRoot == null)
            return;

        Ray worldRay = HandleUtility.GUIPointToWorldRay(mousePosition);

        if (RaycastAgainstCachedMeshes(worldRay, out MeshRaycastHit hit))
        {
            hasValidHit = true;
            currentHit = hit;
        }
    }

    private bool RaycastAgainstCachedMeshes(Ray worldRay, out MeshRaycastHit bestHit)
    {
        bestHit = default;

        float bestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < cachedMeshFilters.Count; i++)
        {
            MeshFilter meshFilter = cachedMeshFilters[i];
            if (meshFilter == null)
                continue;

            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null)
                continue;

            Transform meshTransform = meshFilter.transform;
            Matrix4x4 localToWorld = meshTransform.localToWorldMatrix;
            Matrix4x4 worldToLocal = localToWorld.inverse;

            Ray localRay = TransformRay(worldToLocal, worldRay);

            if (!IntersectRayMesh(localRay, mesh, out RaycastHit localHit, out int localTriangleIndex))
                continue;

            Vector3 worldPoint = localToWorld.MultiplyPoint3x4(localHit.point);
            float worldDistance = Vector3.Distance(worldRay.origin, worldPoint);

            if (worldDistance >= bestDistance)
                continue;

            Vector3 worldNormal = TransformNormal(localToWorld, localHit.normal).normalized;

            bestDistance = worldDistance;
            found = true;

            bestHit = new MeshRaycastHit
            {
                valid = true,
                distance = worldDistance,
                point = worldPoint,
                normal = worldNormal,
                hitTransform = meshTransform,
                meshFilter = meshFilter,
                triangleIndex = localTriangleIndex
            };
        }

        return found;
    }

    private void DrawSurfacePreview()
    {
        if (!hasValidHit || !currentHit.valid)
            return;

        HectonSocketHelper.SocketType previewType = autoDetectType
            ? DetectSocketType(currentHit.normal)
            : manualSocketType;

        Color typeColor = GetSocketTypeColor(previewType);

        Handles.zTest = CompareFunction.LessEqual;

        if (placementMode == PlacementMode.Single)
        {
            Handles.color = new Color(typeColor.r, typeColor.g, typeColor.b, 0.95f);
            Handles.DrawWireDisc(currentHit.point, currentHit.normal, previewDiscRadius);

            Handles.color = new Color(typeColor.r, typeColor.g, typeColor.b, 0.12f);
            Handles.DrawSolidDisc(currentHit.point, currentHit.normal, previewDiscRadius);
        }
        else
        {
            Handles.color = new Color(typeColor.r, typeColor.g, typeColor.b, 0.95f);
            Handles.DrawWireDisc(currentHit.point, currentHit.normal, brushRadius);

            Handles.color = new Color(typeColor.r, typeColor.g, typeColor.b, 0.08f);
            Handles.DrawSolidDisc(currentHit.point, currentHit.normal, brushRadius);

            DrawScatterDistributionPreview(currentHit, typeColor);
        }

        Handles.color = new Color(typeColor.r, typeColor.g, typeColor.b, 0.95f);
        Handles.DrawLine(currentHit.point, currentHit.point + currentHit.normal * (previewDiscRadius * 2.5f), 2f);

        Handles.zTest = CompareFunction.Always;
    }

    private void DrawScatterDistributionPreview(MeshRaycastHit centerHit, Color typeColor)
    {
        BuildSurfaceBasis(centerHit.normal.normalized, out Vector3 tangent, out Vector3 bitangent);

        GenerateScatterSamples2D();

        Handles.color = new Color(typeColor.r, typeColor.g, typeColor.b, 0.4f);

        float pointSize = Mathf.Clamp(brushRadius * 0.04f, 0.01f, 0.06f);

        for (int i = 0; i < scatterSamples2D.Count; i++)
        {
            Vector2 sample = scatterSamples2D[i];
            Vector3 world = centerHit.point + tangent * sample.x + bitangent * sample.y;
            Handles.DotHandleCap(0, world, Quaternion.identity, pointSize, EventType.Repaint);
        }
    }

    private void DrawNearestSocketPreview()
    {
        if (!enableDeleteMode || !hasValidHit || targetRoot == null)
            return;

        Transform sockets = GetSocketsContainer(targetRoot);
        if (sockets == null)
            return;

        Transform nearest = FindNearestSocket(sockets, currentHit.point, deleteNearestMaxDistance);
        if (nearest == null)
            return;

        Handles.zTest = CompareFunction.LessEqual;
        Handles.color = new Color(1f, 0.2f, 0.2f, 0.95f);
        Handles.SphereHandleCap(0, nearest.position, Quaternion.identity, previewDiscRadius * 1.2f, EventType.Repaint);

        Vector3 viewNormal = SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null
            ? SceneView.lastActiveSceneView.camera.transform.forward
            : Vector3.forward;

        Handles.DrawWireDisc(nearest.position, viewNormal, previewDiscRadius * 1.35f);
        Handles.zTest = CompareFunction.Always;
    }

    private void DrawSceneOverlay()
    {
        if (!paintModeEnabled)
            return;

        Handles.BeginGUI();

        GUILayout.BeginArea(new Rect(12f, 12f, 420f, 210f), GUI.skin.window);

        GUILayout.Label("Hecton Surface Painter", EditorStyles.boldLabel);
        GUILayout.Label($"Root: {(targetRoot != null ? targetRoot.name : "<None>")}");
        GUILayout.Label($"Placement: {placementMode}");

        string typeText = autoDetectType
            ? (hasValidHit && currentHit.valid ? DetectSocketType(currentHit.normal).ToString() : "Auto")
            : manualSocketType.ToString();

        GUILayout.Label($"Type: {typeText}");
        GUILayout.Label($"Min Distance: {minDistance:0.###}");

        if (placementMode == PlacementMode.Scatter)
        {
            GUILayout.Label($"Distribution: {scatterDistributionMode}");
            GUILayout.Label($"Brush Radius: {brushRadius:0.###}");
            GUILayout.Label($"Attempts: {brushAttempts}");
            GUILayout.Label($"Jitter: {distributionJitter:0.###}");
            GUILayout.Label($"Poisson Relax: {poissonRelaxationPasses}");
        }
        else
        {
            GUILayout.Label($"Preview Radius: {previewDiscRadius:0.###}");
        }

        if (hasValidHit && currentHit.valid && currentHit.hitTransform != null)
            GUILayout.Label($"Hit: {currentHit.hitTransform.name}");
        else
            GUILayout.Label("Hit: None");

        GUILayout.Label("Shift+LMB = Create");
        GUILayout.Label("Shift+RMB = Delete Nearest");

        GUILayout.EndArea();
        Handles.EndGUI();
    }

    private Transform GetOrCreateSocketsContainer(Transform root)
    {
        Transform existing = GetSocketsContainer(root);
        if (existing != null)
            return existing;

        GameObject container = new GameObject(SocketsContainerName);
        Undo.RegisterCreatedObjectUndo(container, "Create Sockets Container");

        Transform t = container.transform;
        Undo.SetTransformParent(t, root, "Parent Sockets Container");
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;

        EditorUtility.SetDirty(container);
        EditorUtility.SetDirty(root.gameObject);

        return t;
    }

    private Transform GetSocketsContainer(Transform root)
    {
        if (root == null)
            return null;

        return root.Find(SocketsContainerName);
    }

    private int CountSockets(Transform socketsContainer)
    {
        if (socketsContainer == null)
            return 0;

        int count = 0;
        for (int i = 0; i < socketsContainer.childCount; i++)
        {
            Transform child = socketsContainer.GetChild(i);
            if (child != null && child.TryGetComponent<HectonSocketHelper>(out _))
                count++;
        }

        return count;
    }

    private Transform FindNearestSocket(Transform socketsContainer, Vector3 worldPoint, float maxDistance)
    {
        if (socketsContainer == null)
            return null;

        float bestSqr = maxDistance * maxDistance;
        Transform best = null;

        for (int i = 0; i < socketsContainer.childCount; i++)
        {
            Transform child = socketsContainer.GetChild(i);
            if (child == null)
                continue;

            if (!child.TryGetComponent<HectonSocketHelper>(out _))
                continue;

            float sqr = (child.position - worldPoint).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = child;
            }
        }

        return best;
    }

    private bool IsTooCloseToExistingSocket(Transform socketsContainer, Vector3 worldPoint, float minAllowedDistance)
    {
        if (socketsContainer == null || minAllowedDistance <= 0f)
            return false;

        return FindNearestSocket(socketsContainer, worldPoint, minAllowedDistance) != null;
    }

    private string GenerateNextSocketName(Transform socketsContainer, HectonSocketHelper.SocketType type)
    {
        string prefix = $"SOCKET_{type}_";
        int maxIndex = 0;

        if (socketsContainer != null)
        {
            for (int i = 0; i < socketsContainer.childCount; i++)
            {
                Transform child = socketsContainer.GetChild(i);
                if (child == null)
                    continue;

                string childName = child.name;
                if (!childName.StartsWith(prefix))
                    continue;

                string numeric = childName.Substring(prefix.Length);
                if (int.TryParse(numeric, out int parsed))
                {
                    if (parsed > maxIndex)
                        maxIndex = parsed;
                }
            }
        }

        return $"{prefix}{(maxIndex + 1):000}";
    }

    private HectonSocketHelper.SocketType DetectSocketType(Vector3 normal)
    {
        float y = normal.normalized.y;

        if (y > 0.5f)
            return HectonSocketHelper.SocketType.Top;

        if (y < -0.2f)
            return HectonSocketHelper.SocketType.Under;

        return HectonSocketHelper.SocketType.Side;
    }

    private static Color GetSocketTypeColor(HectonSocketHelper.SocketType type)
    {
        return type switch
        {
            HectonSocketHelper.SocketType.Top => Color.green,
            HectonSocketHelper.SocketType.Side => Color.yellow,
            HectonSocketHelper.SocketType.Under => Color.red,
            _ => Color.cyan
        };
    }

    private static void BuildSurfaceBasis(Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
    {
        Vector3 up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.99f
            ? Vector3.right
            : Vector3.up;

        tangent = Vector3.Cross(up, normal).normalized;
        bitangent = Vector3.Cross(normal, tangent).normalized;
    }

    private void EnsureMeshCache()
    {
        if (targetRoot == null)
        {
            cachedMeshFilters.Clear();
            return;
        }

        int hierarchyHash = CalculateHierarchyHash(targetRoot);
        int ignoreHash = CalculateIgnoreTokensHash(ignoreNameTokens);

        if (cachedHierarchyHash != hierarchyHash ||
            cachedIgnoreTokensHash != ignoreHash ||
            EditorApplication.timeSinceStartup - lastCacheBuildTime > 1.0d)
        {
            RebuildMeshCache();
        }
    }

    private void RebuildMeshCache()
    {
        cachedMeshFilters.Clear();

        if (targetRoot == null)
            return;

        RebuildIgnoreTokenCache();

        MeshFilter[] all = targetRoot.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < all.Length; i++)
        {
            MeshFilter mf = all[i];
            if (mf == null)
                continue;

            if (!ShouldUseForPainting(mf))
                continue;

            cachedMeshFilters.Add(mf);
        }

        cachedHierarchyHash = CalculateHierarchyHash(targetRoot);
        cachedIgnoreTokensHash = CalculateIgnoreTokensHash(ignoreNameTokens);
        lastCacheBuildTime = EditorApplication.timeSinceStartup;

        Repaint();
    }

    private bool ShouldUseForPainting(MeshFilter mf)
    {
        if (mf == null)
            return false;

        if (mf.sharedMesh == null)
            return false;

        GameObject go = mf.gameObject;
        if (!go.activeInHierarchy)
            return false;

        if (!go.TryGetComponent(out MeshRenderer mr) || !mr.enabled)
            return false;

        Transform sockets = GetSocketsContainer(targetRoot);
        if (sockets != null && go.transform.IsChildOf(sockets))
            return false;

        string lower = go.name.ToLowerInvariant();
        for (int i = 0; i < cachedIgnoreTokens.Count; i++)
        {
            if (lower.Contains(cachedIgnoreTokens[i]))
                return false;
        }

        return true;
    }

    private int GetValidPaintMeshCountPreview(Transform root)
    {
        if (root == null)
            return 0;

        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        int count = 0;

        for (int i = 0; i < filters.Length; i++)
        {
            if (ShouldUseForPainting(filters[i]))
                count++;
        }

        return count;
    }

    private void RebuildIgnoreTokenCache()
    {
        cachedIgnoreTokens.Clear();

        if (string.IsNullOrWhiteSpace(ignoreNameTokens))
            return;

        string[] split = ignoreNameTokens.Split(';');
        for (int i = 0; i < split.Length; i++)
        {
            string token = split[i].Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(token))
                cachedIgnoreTokens.Add(token);
        }
    }

    private static int CalculateIgnoreTokensHash(string tokens)
    {
        return string.IsNullOrEmpty(tokens) ? 0 : tokens.GetHashCode();
    }

    private int CalculateHierarchyHash(Transform root)
    {
        if (root == null)
            return 0;

        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (int)EntityId.ToULong(root.GetEntityId());

            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter mf = filters[i];
                if (mf == null)
                    continue;

                hash = hash * 31 + (int)EntityId.ToULong(mf.GetEntityId());
                hash = hash * 31 + (mf.sharedMesh != null ? (int)EntityId.ToULong(mf.sharedMesh.GetEntityId()) : 0);
                hash = hash * 31 + (mf.gameObject.activeInHierarchy ? 1 : 0);
            }

            return hash;
        }
    }

    private void InvalidateMeshCache()
    {
        cachedHierarchyHash = 0;
        cachedIgnoreTokensHash = 0;
        lastCacheBuildTime = 0d;
        cachedMeshFilters.Clear();
    }

    private static Ray TransformRay(Matrix4x4 matrix, Ray worldRay)
    {
        Vector3 origin = matrix.MultiplyPoint3x4(worldRay.origin);
        Vector3 direction = matrix.MultiplyVector(worldRay.direction).normalized;
        return new Ray(origin, direction);
    }

    private static Vector3 TransformNormal(Matrix4x4 localToWorld, Vector3 localNormal)
    {
        return localToWorld.inverse.transpose.MultiplyVector(localNormal);
    }

    private static Vector3 GetStableUp(Vector3 forward)
    {
        float dot = Mathf.Abs(Vector3.Dot(forward.normalized, Vector3.up));
        return dot > 0.999f ? Vector3.right : Vector3.up;
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

    private static void CollectMeshTriangles(Mesh mesh, List<int> triangles)
    {
        triangles.Clear();
        List<int> submeshTriangles = new List<int>((int)global::System.Math.Min(ResolveIndexCount(mesh), int.MaxValue));
        for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
        {
            submeshTriangles.Clear();
            mesh.GetTriangles(submeshTriangles, subMeshIndex, true);
            triangles.AddRange(submeshTriangles);
        }
    }

    private static bool IntersectRayMesh(Ray localRay, Mesh mesh, out RaycastHit hit, out int triangleIndex)
    {
        hit = default;
        triangleIndex = -1;

        if (mesh == null)
            return false;

        List<Vector3> vertices = new List<Vector3>(mesh.vertexCount);
        List<int> triangles = new List<int>((int)global::System.Math.Min(ResolveIndexCount(mesh), int.MaxValue));
        List<Vector3> normals = new List<Vector3>(mesh.vertexCount);
        mesh.GetVertices(vertices);
        CollectMeshTriangles(mesh, triangles);
        mesh.GetNormals(normals);

        bool found = false;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < triangles.Count; i += 3)
        {
            Vector3 v0 = vertices[triangles[i]];
            Vector3 v1 = vertices[triangles[i + 1]];
            Vector3 v2 = vertices[triangles[i + 2]];

            if (!RayTriangle(localRay, v0, v1, v2, out float distance, out Vector3 point, out Vector3 bary))
                continue;

            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            found = true;
            triangleIndex = i / 3;

            Vector3 normal;
            if (normals.Count == vertices.Count)
            {
                Vector3 n0 = normals[triangles[i]];
                Vector3 n1 = normals[triangles[i + 1]];
                Vector3 n2 = normals[triangles[i + 2]];
                normal = (n0 * bary.x + n1 * bary.y + n2 * bary.z).normalized;
            }
            else
            {
                normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
            }

            hit = new RaycastHit
            {
                point = point,
                normal = normal,
                distance = distance
            };
        }

        return found;
    }

    private static bool RayTriangle(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2,
        out float distance, out Vector3 point, out Vector3 barycentric)
    {
        distance = 0f;
        point = default;
        barycentric = default;

        const float epsilon = 0.0000001f;

        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;

        Vector3 pVec = Vector3.Cross(ray.direction, edge2);
        float det = Vector3.Dot(edge1, pVec);

        if (det > -epsilon && det < epsilon)
            return false;

        float invDet = 1f / det;
        Vector3 tVec = ray.origin - v0;

        float u = Vector3.Dot(tVec, pVec) * invDet;
        if (u < 0f || u > 1f)
            return false;

        Vector3 qVec = Vector3.Cross(tVec, edge1);

        float v = Vector3.Dot(ray.direction, qVec) * invDet;
        if (v < 0f || u + v > 1f)
            return false;

        float t = Vector3.Dot(edge2, qVec) * invDet;
        if (t < epsilon)
            return false;

        distance = t;
        point = ray.origin + ray.direction * t;
        barycentric = new Vector3(1f - u - v, u, v);
        return true;
    }
}
#endif
