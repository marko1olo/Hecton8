// ============================================================================
// HECTON-8 -- HectonSphereGenerator.cs
// Editor tool: generates a high-poly UV sphere mesh with optional
// inverted normals (for sky domes). Saves as .asset, creates GameObject.
//
// Menu: Tools > Hecton > Create High-Poly Sphere
// Output: Assets/_Project/Art/Models/HighPolySphere.asset
//
// Unity 6 | Editor only
// ============================================================================

using UnityEngine;
using UnityEditor;
using System.IO;

namespace Hecton.Editor
{
    public class HectonSphereGenerator : EditorWindow
    {
        // ---------------------------------------------------------
        // SETTINGS
        // ---------------------------------------------------------
        private int _segments = 128;
        private int _rings = 128;
        private float _radius = 500f;
        private bool _invertNormals = true;
        private string _assetName = "HighPolySphere";

        // Path constants
        private const string SAVE_FOLDER = "Assets/_Project/Art/Models";
        private const string MENU_PATH = "Tools/Hecton/Create High-Poly Sphere";

        // Limits
        private const int MIN_SEGMENTS = 8;
        private const int MAX_SEGMENTS = 512;
        private const int MIN_RINGS = 8;
        private const int MAX_RINGS = 512;
        private const int TrigLutSize = 1024;
        private const int TrigLutMask = TrigLutSize - 1;
        private const float TwoPi = 6.2831853071795864769f;
        private const float HalfTrigLutSize = TrigLutSize * 0.5f;

        // COLD ALLOC: float[1024] - editor trig lookup table - owner: HectonSphereGenerator
        private static readonly float[] s_sinLut = new float[TrigLutSize];
        // COLD ALLOC: float[1024] - editor trig lookup table - owner: HectonSphereGenerator
        private static readonly float[] s_cosLut = new float[TrigLutSize];

        static HectonSphereGenerator()
        {
            const float step = TwoPi / TrigLutSize;
            for (int i = 0; i < TrigLutSize; i++)
            {
                float angle = i * step;
                s_sinLut[i] = Mathf.Sin(angle);
                s_cosLut[i] = Mathf.Cos(angle);
            }
        }

        // ---------------------------------------------------------
        // MENU ITEM
        // ---------------------------------------------------------
        [MenuItem(MENU_PATH)]
        public static void ShowWindow()
        {
            HectonSphereGenerator window = GetWindow<HectonSphereGenerator>();
            window.titleContent = new GUIContent("Hecton Sphere Generator");
            window.minSize = new Vector2(320, 260);
            window.Show();
        }

        // ---------------------------------------------------------
        // GUI
        // ---------------------------------------------------------
        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Hecton High-Poly Sphere Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.HelpBox(
                "Generates a UV sphere mesh.\n" +
                "Invert normals = ON for sky domes (camera inside sphere).\n" +
                "Invert normals = OFF for regular objects.",
                MessageType.Info);

            EditorGUILayout.Space(8);

            _segments = EditorGUILayout.IntSlider("Segments (horizontal)", _segments, MIN_SEGMENTS, MAX_SEGMENTS);
            _rings = EditorGUILayout.IntSlider("Rings (vertical)", _rings, MIN_RINGS, MAX_RINGS);
            _radius = EditorGUILayout.FloatField("Radius", _radius);
            _invertNormals = EditorGUILayout.Toggle("Invert Normals (Sky Dome)", _invertNormals);
            _assetName = EditorGUILayout.TextField("Asset Name", _assetName);

            // Vertex/triangle count preview
            int vertCount = (_segments + 1) * (_rings + 1);
            int triCount = _segments * _rings * 2;
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Vertices: {vertCount:N0}    Triangles: {triCount:N0}", EditorStyles.miniLabel);

            // Warn if mesh is very large
            if (vertCount > 100000)
            {
                EditorGUILayout.HelpBox(
                    $"Large mesh: {vertCount:N0} vertices. This is fine for a sky dome but may be excessive.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(12);

            if (GUILayout.Button("Generate Sphere", GUILayout.Height(32)))
            {
                GenerateAndSave();
            }
        }

        // ---------------------------------------------------------
        // GENERATION PIPELINE
        // ---------------------------------------------------------
        private void GenerateAndSave()
        {
            // Validate radius
            if (_radius <= 0f)
            {
                _radius = 500f;
                Debug.LogWarning("[HectonSphereGenerator] Radius was <= 0, reset to 500.");
            }

            // Validate asset name
            if (string.IsNullOrWhiteSpace(_assetName))
            {
                _assetName = "HighPolySphere";
            }

            // 1. Generate mesh
            Mesh mesh = CreateUVSphere(_segments, _rings, _radius, _invertNormals);
            mesh.name = _assetName;

            // 2. Save as .asset
            string assetPath = SaveMeshAsset(mesh, _assetName);
            if (string.IsNullOrEmpty(assetPath))
                return;

            // 3. Load the saved asset (so the scene references the asset, not a runtime copy)
            Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);

            // 4. Create GameObject in scene
            GameObject go = CreateSceneObject(savedMesh);

            // 5. Select it
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(savedMesh);

            string normInfo = _invertNormals ? "INVERTED (sky dome)" : "standard";
            Debug.Log(
                $"[HectonSphereGenerator] Created sphere: {_segments}x{_rings}, " +
                $"radius={_radius}, normals={normInfo}\n" +
                $"Saved to: {assetPath}");
        }

        // ---------------------------------------------------------
        // UV SPHERE GENERATION
        //
        // Standard UV sphere (latitude/longitude grid).
        // Poles have converging triangles (acceptable for sky dome).
        //
        // Vertex layout:
        //   (rings+1) rows x (segments+1) columns
        //   Extra column for UV seam (u=0 and u=1 share position
        //   but have different UVs).
        //
        // When invertNormals is true:
        //   - Normals point inward (toward center)
        //   - Triangle winding is reversed (CW instead of CCW)
        //   - This makes the sphere visible from inside (sky dome)
        //     and works with Cull Front in the shader
        // ---------------------------------------------------------
        private static Mesh CreateUVSphere(int segments, int rings, float radius, bool invertNormals)
        {
            int vertCountX = segments + 1; // +1 for UV seam
            int vertCountY = rings + 1;    // +1 for top and bottom
            int totalVerts = vertCountX * vertCountY;
            int totalTris = segments * rings * 2;

            Vector3[] vertices = new Vector3[totalVerts];
            Vector3[] normals = new Vector3[totalVerts];
            Vector2[] uvs = new Vector2[totalVerts];
            int[] triangles = new int[totalTris * 3];

            // Normal direction multiplier
            float normalSign = invertNormals ? -1f : 1f;
            float invSegments = 1f / segments;
            float invRings = 1f / rings;
            float invRadius = 1f / radius;
            float normalScale = normalSign * invRadius;
            float latToLut = HalfTrigLutSize * invRings;
            float lonToLut = TrigLutSize * invSegments;

            // -----------------------------------------
            // VERTICES
            // -----------------------------------------
            for (int y = 0; y <= rings; y++)
            {
                // v goes from 0 (bottom/south pole) to 1 (top/north pole)
                float v = y * invRings;
                int polarIndex = ((int)(y * latToLut + 0.5f)) & TrigLutMask;
                float polarSin = s_sinLut[polarIndex];
                float polarCos = s_cosLut[polarIndex];

                // Y coordinate: -radius at bottom, +radius at top
                float py = -polarCos * radius;

                // Ring radius at this latitude
                float ringRadius = polarSin * radius;

                for (int x = 0; x <= segments; x++)
                {
                    float u = x * invSegments;
                    int azimuthIndex = ((int)(x * lonToLut + 0.5f)) & TrigLutMask;

                    float px = s_cosLut[azimuthIndex] * ringRadius;
                    float pz = s_sinLut[azimuthIndex] * ringRadius;

                    int idx = y * vertCountX + x;

                    vertices[idx] = new Vector3(px, py, pz);

                    // Radius is constant, so normalized(position) is position * invRadius.
                    normals[idx] = new Vector3(px * normalScale, py * normalScale, pz * normalScale);

                    uvs[idx] = new Vector2(u, v);
                }
            }

            // -----------------------------------------
            // TRIANGLES
            //
            // Two triangles per quad cell.
            // When invertNormals, winding order is reversed
            // so faces are visible from inside the sphere.
            // -----------------------------------------
            int triIdx = 0;

            for (int y = 0; y < rings; y++)
            {
                for (int x = 0; x < segments; x++)
                {
                    // Four corners of this quad
                    int bottomLeft = y * vertCountX + x;
                    int bottomRight = y * vertCountX + x + 1;
                    int topLeft = (y + 1) * vertCountX + x;
                    int topRight = (y + 1) * vertCountX + x + 1;

                    if (invertNormals)
                    {
                        // Reversed winding (CW when viewed from outside = CCW from inside)
                        // Triangle 1
                        triangles[triIdx++] = bottomLeft;
                        triangles[triIdx++] = topLeft;
                        triangles[triIdx++] = bottomRight;

                        // Triangle 2
                        triangles[triIdx++] = bottomRight;
                        triangles[triIdx++] = topLeft;
                        triangles[triIdx++] = topRight;
                    }
                    else
                    {
                        // Standard winding (CCW when viewed from outside)
                        // Triangle 1
                        triangles[triIdx++] = bottomLeft;
                        triangles[triIdx++] = bottomRight;
                        triangles[triIdx++] = topLeft;

                        // Triangle 2
                        triangles[triIdx++] = bottomRight;
                        triangles[triIdx++] = topRight;
                        triangles[triIdx++] = topLeft;
                    }
                }
            }

            // -----------------------------------------
            // BUILD MESH
            //
            // Use 32-bit index buffer if vertex count exceeds 16-bit limit.
            // 128x128 = 16641 verts (fits 16-bit), but higher settings may not.
            // -----------------------------------------
            Mesh mesh = new Mesh();

            if (totalVerts > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);

            // Tangents for potential normal mapping
            mesh.RecalculateTangents();

            // Bounds
            mesh.RecalculateBounds();

            return mesh;
        }

        // ---------------------------------------------------------
        // SAVE MESH AS .ASSET
        //
        // Creates folder structure if it does not exist.
        // Overwrites existing asset at the same path.
        // ---------------------------------------------------------
        private static string SaveMeshAsset(Mesh mesh, string assetName)
        {
            // Ensure folder exists
            EnsureFolderExists(SAVE_FOLDER);

            string assetPath = $"{SAVE_FOLDER}/{assetName}.asset";

            // Check for existing asset
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing != null)
            {
                // Overwrite: copy data into existing asset to preserve references
                EditorUtility.CopySerialized(mesh, existing);
                AssetDatabase.SaveAssets();
                Debug.Log($"[HectonSphereGenerator] Overwrote existing asset: {assetPath}");
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, assetPath);
                AssetDatabase.SaveAssets();
            }

            AssetDatabase.Refresh();
            return assetPath;
        }

        // ---------------------------------------------------------
        // ENSURE FOLDER PATH EXISTS
        //
        // Recursively creates missing folders in the path.
        // Input: "Assets/_Project/Art/Models"
        // Creates each segment if missing.
        // ---------------------------------------------------------
        private static void EnsureFolderExists(string folderPath)
        {
            // Split path into segments
            string[] parts = folderPath.Split('/');

            string currentPath = parts[0]; // "Assets"

            for (int i = 1; i < parts.Length; i++)
            {
                string nextPath = currentPath + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, parts[i]);
                }

                currentPath = nextPath;
            }
        }

        // ---------------------------------------------------------
        // CREATE SCENE OBJECT
        //
        // Creates a GameObject with MeshFilter + MeshRenderer.
        // Uses default material. Registers undo for clean workflow.
        // ---------------------------------------------------------
        private static GameObject CreateSceneObject(Mesh mesh)
        {
            GameObject go = new GameObject(mesh.name);

            Undo.RegisterCreatedObjectUndo(go, "Create Hecton Sphere");

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();

            // Try to find URP default material, fall back to built-in
            Material defaultMat = GetDefaultMaterial();
            mr.sharedMaterial = defaultMat;

            return go;
        }

        // ---------------------------------------------------------
        // GET DEFAULT MATERIAL
        //
        // Attempts to find URP Lit material, falls back gracefully.
        // ---------------------------------------------------------
        private static Material GetDefaultMaterial()
        {
            // Try URP default
            Material mat = AssetDatabase.GetBuiltinExtraResource<Material>(
                "Default-Material.mat");

            if (mat != null)
                return mat;

            // Absolute fallback
            return new Material(Shader.Find("Universal Render Pipeline/Lit"));
        }
    }
}
