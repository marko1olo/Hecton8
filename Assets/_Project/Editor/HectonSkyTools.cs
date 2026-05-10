// ============================================================================
// HectonSkyTools.cs
// Editor toolbox for Hecton sky dome generation and cloud atlas packing.
// Place in Assets/Editor/ folder.
// Unity 6 | URP 17+
//
// SECTION 1 -- SKY DOME GENERATOR
//   Generates a UV sphere mesh with inward-facing normals.
//   Standard triangle winding (front face outward) -- works with
//   Cull Front in the sky shader, which shows back faces (the interior).
//   Spherical UV mapping with duplicated seam/pole vertices to prevent
//   texture tearing and reduce pole pinching.
//
// SECTION 2 -- CLOUD ATLAS PACKER
//   Packs two source textures into a single RGBA atlas:
//     R = Cloud density   (from Density Map, red channel)
//     G = Detail noise    (from Detail Map, red channel)
//     B = Flow X          (curl of density gradient, mapped [0,1])
//     A = Flow Y          (curl of density gradient, mapped [0,1])
//
//   Curl noise algorithm:
//     1. Compute density gradient via central differences (wrapping).
//     2. Rotate gradient 90 degrees CCW to get curl vector.
//        This makes the flowmap follow density CONTOURS, not gradients.
//     3. Normalize, scale by Flow Strength, clamp to [-1,1].
//     4. Map [-1,1] -> [0,1] for texture storage.
//
//   The shader decodes flow direction as: flowDir = tex.ba * 2 - 1
// ============================================================================

using UnityEngine;
using UnityEditor;
using Unity.Collections;
using System.IO;

namespace Hecton.Editor
{
    public class HectonSkyTools : EditorWindow
    {
        // =============================================================
        // SERIALIZED STATE (survives domain reload / recompile)
        // =============================================================

        [Header("Dome Generator")]
        [SerializeField] private int   _domeSegments = 64;
        [SerializeField] private int   _domeRings    = 32;
        [SerializeField] private float _domeRadius   = 500f;

        [Header("Atlas Packer")]
        [SerializeField] private Texture2D _densityMap;
        [SerializeField] private Texture2D _detailMap;
        [SerializeField] private float     _flowStrength = 1.0f;

        // =============================================================
        // CONSTANTS
        // =============================================================

        private const string DOME_ASSET_PATH =
            "Assets/_Project/Art/Models/SkyDome_Inverted.asset";

        private const string ATLAS_PNG_PATH =
            "Assets/_Project/Art/Textures/Sky/HectonSkyAtlas_RGBA.png";

        // =============================================================
        // UI STATE
        // =============================================================

        private Vector2 _scrollPos;

        // =============================================================
        // WINDOW SETUP
        // =============================================================

        [MenuItem("Tools/Hecton/Sky Toolbox")]
        public static void ShowWindow()
        {
            var window = GetWindow<HectonSkyTools>("Hecton Sky Toolbox");
            window.minSize = new Vector2(400, 620);
        }

        // =============================================================
        // GUI
        // =============================================================

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawSectionHeader("SKY DOME GENERATOR");
            DrawDomeSection();

            EditorGUILayout.Space(24);

            DrawSectionHeader("CLOUD ATLAS PACKER");
            DrawAtlasSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSectionHeader(string title)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            Rect lineRect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(lineRect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorGUILayout.Space(4);
        }

        // =============================================================
        // SECTION 1: SKY DOME GENERATOR -- GUI
        // =============================================================

        private void DrawDomeSection()
        {
            EditorGUILayout.HelpBox(
                "Generates a sphere mesh with inward-facing normals.\n" +
                "Standard winding + Cull Front = visible from inside.\n" +
                "Spherical UVs with duplicated seam/pole vertices\n" +
                "to prevent hard pole pinching.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            _domeSegments = EditorGUILayout.IntSlider(
                "Segments (longitude)", _domeSegments, 16, 128);
            _domeRings = EditorGUILayout.IntSlider(
                "Rings (latitude)", _domeRings, 8, 64);
            _domeRadius = Mathf.Max(1f,
                EditorGUILayout.FloatField("Radius", _domeRadius));

            // Preview stats
            int verts = (_domeRings + 1) * (_domeSegments + 1);
            int tris  = _domeRings * _domeSegments * 2;
            EditorGUILayout.LabelField(
                $"Preview: {verts} vertices, {tris} triangles",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Generate Inverted Dome", GUILayout.Height(32)))
            {
                GenerateAndSaveDome();
            }
        }

        // =============================================================
        // SECTION 1: SKY DOME GENERATOR -- LOGIC
        // =============================================================

        private void GenerateAndSaveDome()
        {
            Mesh mesh = BuildSkyDomeMesh(_domeSegments, _domeRings, _domeRadius);

            EnsureDirectoryExists(DOME_ASSET_PATH);

            // Overwrite existing or create new
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(DOME_ASSET_PATH);
            if (existing != null)
            {
                // CopySerialized overwrites the asset in-place,
                // preserving references from scenes/prefabs
                EditorUtility.CopySerialized(mesh, existing);
                AssetDatabase.SaveAssets();
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, DOME_ASSET_PATH);
            }

            AssetDatabase.Refresh();

            Object saved = AssetDatabase.LoadAssetAtPath<Mesh>(DOME_ASSET_PATH);
            EditorGUIUtility.PingObject(saved);
            Selection.activeObject = saved;

            Debug.Log(
                $"[HectonSkyTools] Sky dome saved: {DOME_ASSET_PATH}\n" +
                $"  {mesh.vertexCount} vertices, " +
                $"{ResolveTriangleCount(mesh)} triangles, " +
                $"radius {_domeRadius}");
        }

        private static long ResolveTriangleCount(Mesh mesh)
        {
            if (mesh == null)
                return 0L;

            long triangles = 0L;
            for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
            {
                triangles += (long)mesh.GetIndexCount(subMeshIndex) / 3L;
            }

            return triangles;
        }

        /// <summary>
        /// Builds a UV sphere mesh configured as a sky dome.
        ///
        /// WINDING:
        ///   Standard (front face outward). Used with Cull Front in the
        ///   shader -- Cull Front discards front faces, revealing back
        ///   faces (the interior of the sphere) to the camera inside.
        ///
        /// NORMALS:
        ///   Inverted (pointing inward toward center). Correct for any
        ///   per-pixel normal-based shading from inside the dome.
        ///   The sky shader itself uses viewDirWS, not mesh normals,
        ///   but inward normals are set for correctness and future use.
        ///
        /// UVs:
        ///   Spherical mapping with one seam along lon=0 / lon=segments.
        ///   Seam vertices are duplicated (U=0 and U=1 at same position)
        ///   to prevent texture tearing across the seam.
        ///
        ///   Pole vertices are duplicated per segment -- each pole
        ///   triangle gets its own pole vertex with a unique U value
        ///   centered on that triangle's longitude span. This distributes
        ///   UV coverage evenly at the poles, reducing visible pinching.
        ///
        ///   U = longitude [0, 1] (wraps at seam)
        ///   V = latitude  [0, 1] (0 = south pole, 1 = north pole)
        /// </summary>
        private Mesh BuildSkyDomeMesh(int segments, int rings, float radius)
        {
            int vertCount = (rings + 1) * (segments + 1);

            var positions = new Vector3[vertCount];
            var normals   = new Vector3[vertCount];
            var uvs       = new Vector2[vertCount];

            int vi = 0;

            for (int lat = 0; lat <= rings; lat++)
            {
                // theta: 0 at north pole (top), PI at south pole (bottom)
                float theta    = Mathf.PI * lat / rings;
                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);

                for (int lon = 0; lon <= segments; lon++)
                {
                    // phi: 0 to 2*PI
                    // lon = segments duplicates lon = 0 position (UV seam)
                    float phi    = 2f * Mathf.PI * lon / segments;
                    float sinPhi = Mathf.Sin(phi);
                    float cosPhi = Mathf.Cos(phi);

                    // Unit sphere direction
                    float x = sinTheta * cosPhi;
                    float y = cosTheta;
                    float z = sinTheta * sinPhi;

                    positions[vi] = new Vector3(x, y, z) * radius;
                    normals[vi]   = new Vector3(-x, -y, -z); // INVERTED

                    // Spherical UV mapping
                    // U: longitude [0, 1], seam at lon=0 and lon=segments
                    // V: latitude  [0, 1], 0=south pole, 1=north pole
                    //
                    // At poles (lat=0 or lat=rings), sinTheta is ~0 so
                    // all positions collapse to one point. Each pole vertex
                    // still gets a unique U = (lon + 0.5) / segments at the
                    // pole rows? No -- we use lon/segments for all rows,
                    // including poles. The per-segment duplication at
                    // lon=segments handles the seam. Pole pinching is
                    // minimized because each pole vertex has a unique U,
                    // spreading the texture fan evenly.
                    float u = (float)lon / segments;
                    float v = 1f - (float)lat / rings; // 1 at top, 0 at bottom

                    // For pole rows, center U on the triangle span
                    // to reduce convergence artifacts
                    if (lat == 0 || lat == rings)
                    {
                        u = (lon + 0.5f) / segments;
                    }

                    uvs[vi] = new Vector2(u, v);

                    vi++;
                }
            }

            // ---------------------------------------------------------
            // TRIANGLES
            //
            // Standard winding order (CCW = front face outward).
            // The sky shader uses Cull Front, which discards front faces
            // and shows back faces -- the inside of the sphere.
            //
            // Grid layout:
            //   Each quad at (lat, lon) uses 4 vertices:
            //     TL = lat * (segments+1) + lon
            //     TR = TL + 1
            //     BL = (lat+1) * (segments+1) + lon
            //     BR = BL + 1
            //
            //   Two triangles per quad:
            //     Tri 1: TL, BL, TR  (standard CCW from outside)
            //     Tri 2: TR, BL, BR
            // ---------------------------------------------------------

            int triCount = rings * segments * 6; // 2 tris * 3 indices each
            var triangles = new int[triCount];
            int ti = 0;

            for (int lat = 0; lat < rings; lat++)
            {
                for (int lon = 0; lon < segments; lon++)
                {
                    int topLeft     = lat * (segments + 1) + lon;
                    int topRight    = topLeft + 1;
                    int bottomLeft  = (lat + 1) * (segments + 1) + lon;
                    int bottomRight = bottomLeft + 1;

                    // Triangle 1 (upper-left of quad)
                    triangles[ti++] = topLeft;
                    triangles[ti++] = bottomLeft;
                    triangles[ti++] = topRight;

                    // Triangle 2 (lower-right of quad)
                    triangles[ti++] = topRight;
                    triangles[ti++] = bottomLeft;
                    triangles[ti++] = bottomRight;
                }
            }

            // ---------------------------------------------------------
            // ASSEMBLE MESH
            // ---------------------------------------------------------

            Mesh mesh = new Mesh();
            mesh.name = "SkyDome_Inverted";

            // Use 32-bit indices if vertex count exceeds 16-bit limit
            if (vertCount > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);

            // Tangents for potential normal mapping in future
            mesh.RecalculateTangents();

            // Bounds for frustum culling (huge sphere, always visible)
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 2f);

            return mesh;
        }

        // =============================================================
        // SECTION 2: CLOUD ATLAS PACKER -- GUI
        // =============================================================

        private void DrawAtlasSection()
        {
            EditorGUILayout.HelpBox(
                "Packs two textures into one RGBA atlas:\n" +
                "  R = Density Map (cloud shapes)\n" +
                "  G = Detail Map (high-freq noise)\n" +
                "  B = Flow X (curl of density gradient)\n" +
                "  A = Flow Y (curl of density gradient)\n\n" +
                "Source textures stay Read/Write disabled; the packer\n" +
                "captures one GPU snapshot per input.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            _densityMap = (Texture2D)EditorGUILayout.ObjectField(
                "Density Map", _densityMap, typeof(Texture2D), false);

            _detailMap = (Texture2D)EditorGUILayout.ObjectField(
                "Detail Map", _detailMap, typeof(Texture2D), false);

            _flowStrength = EditorGUILayout.Slider(
                "Flow Strength", _flowStrength, 0f, 2f);

            EditorGUILayout.Space(4);

            // Validation warnings
            bool canPack = true;

            if (_densityMap == null)
            {
                EditorGUILayout.HelpBox(
                    "Density Map is required.", MessageType.Warning);
                canPack = false;
            }

            if (_detailMap == null)
            {
                EditorGUILayout.HelpBox(
                    "Detail Map is required.", MessageType.Warning);
                canPack = false;
            }

            if (_densityMap != null && _detailMap != null &&
                (_densityMap.width != _detailMap.width ||
                 _densityMap.height != _detailMap.height))
            {
                EditorGUILayout.HelpBox(
                    $"Texture size mismatch!\n" +
                    $"Density: {_densityMap.width}x{_densityMap.height}\n" +
                    $"Detail:  {_detailMap.width}x{_detailMap.height}\n" +
                    $"Detail Map will be bilinear-sampled at Density Map resolution.",
                    MessageType.Warning);
                // Not a hard error -- we handle mismatched sizes
            }

            EditorGUILayout.Space(4);

            GUI.enabled = canPack;
            if (GUILayout.Button("Pack RGBA Atlas", GUILayout.Height(32)))
            {
                PackAndSaveAtlas();
            }
            GUI.enabled = true;
        }

        // =============================================================
        // SECTION 2: CLOUD ATLAS PACKER -- LOGIC
        // =============================================================

        private void PackAndSaveAtlas()
        {
            int width  = _densityMap.width;
            int height = _densityMap.height;

            Texture2D densityReadable = null;
            Texture2D detailReadable = null;
            Texture2D atlas = null;
            byte[] pngData;

            try
            {
                densityReadable = CaptureReadableTexture(_densityMap, width, height);
                detailReadable = CaptureReadableTexture(_detailMap, width, height);
                atlas = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                atlas.name = "HectonSkyAtlas_RGBA";

                NativeArray<Color32> densityPixels = densityReadable.GetRawTextureData<Color32>();
                NativeArray<Color32> detailPixels = detailReadable.GetRawTextureData<Color32>();
                NativeArray<Color32> atlasPixels = atlas.GetRawTextureData<Color32>();

                int totalPixels = width * height;
                int progressInterval = Mathf.Max(1, totalPixels / 100);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int idx = y * width + x;
                        float r = ReadRed01(densityPixels, idx);
                        float g = ReadRed01(detailPixels, idx);

                        int xP = (x + 1) % width;
                        int xN = (x - 1 + width) % width;
                        int yP = (y + 1) % height;
                        int yN = (y - 1 + height) % height;

                        float gradX = ReadRed01(densityPixels, y * width + xP) -
                                      ReadRed01(densityPixels, y * width + xN);
                        float gradY = ReadRed01(densityPixels, yP * width + x) -
                                      ReadRed01(densityPixels, yN * width + x);

                        float curlX = -gradY;
                        float curlY = gradX;
                        float dominant = Mathf.Max(Mathf.Abs(curlX), Mathf.Abs(curlY));
                        if (dominant > 0.0001f)
                        {
                            curlX /= dominant;
                            curlY /= dominant;
                        }
                        else
                        {
                            curlX = 0f;
                            curlY = 0f;
                        }

                        curlX = Mathf.Clamp(curlX * _flowStrength, -1f, 1f);
                        curlY = Mathf.Clamp(curlY * _flowStrength, -1f, 1f);

                        atlasPixels[idx] = new Color32(
                            Encode01(r),
                            Encode01(g),
                            Encode01(curlX * 0.5f + 0.5f),
                            Encode01(curlY * 0.5f + 0.5f));

                        if (idx % progressInterval == 0)
                        {
                            float progress = (float)idx / totalPixels;
                            if (EditorUtility.DisplayCancelableProgressBar(
                                "Packing Cloud Atlas",
                                $"Processing pixel {idx}/{totalPixels}",
                                progress))
                            {
                                Debug.LogWarning(
                                    "[HectonSkyTools] Atlas packing cancelled.");
                                return;
                            }
                        }
                    }
                }

                atlas.Apply(false, false);
                pngData = atlas.EncodeToPNG();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (densityReadable != null)
                    DestroyImmediate(densityReadable);
                if (detailReadable != null)
                    DestroyImmediate(detailReadable);
                if (atlas != null)
                    DestroyImmediate(atlas);
            }

            if (pngData == null || pngData.Length == 0)
            {
                Debug.LogError(
                    "[HectonSkyTools] Failed to encode atlas to PNG!");
                return;
            }

            EnsureDirectoryExists(ATLAS_PNG_PATH);

            File.WriteAllBytes(ATLAS_PNG_PATH, pngData);
            AssetDatabase.Refresh();

            // ---------------------------------------------------------
            // CONFIGURE IMPORT SETTINGS
            //
            // The atlas needs specific import settings:
            //   - sRGB OFF (linear data -- flowmap vectors are not colors)
            //   - Read/Write OFF
            //   - BC7 compression for the HECTON-8 texture contract
            //   - Filter: Bilinear
            //   - Wrap: Repeat (clouds tile)
            // ---------------------------------------------------------

            TextureImporter importer = AssetImporter.GetAtPath(ATLAS_PNG_PATH)
                as TextureImporter;

            if (importer != null)
            {
                importer.sRGBTexture      = false;  // LINEAR -- flow data
                importer.isReadable       = false;
                importer.textureCompression =
                    TextureImporterCompression.Compressed;
                importer.filterMode       = FilterMode.Bilinear;
                importer.wrapMode         = TextureWrapMode.Repeat;
                importer.mipmapEnabled    = true;
                importer.alphaIsTransparency = false;
                importer.alphaSource      =
                    TextureImporterAlphaSource.FromInput;

                // Max size -- match source
                importer.maxTextureSize = Mathf.Max(width, height);

                TextureImporterPlatformSettings standalone =
                    importer.GetPlatformTextureSettings("Standalone");
                standalone.overridden = true;
                standalone.format = TextureImporterFormat.BC7;
                standalone.maxTextureSize = importer.maxTextureSize;
                standalone.textureCompression =
                    TextureImporterCompression.Compressed;
                standalone.crunchedCompression = false;
                importer.SetPlatformTextureSettings(standalone);

                importer.SaveAndReimport();
            }

            // Ping the saved asset
            Object saved = AssetDatabase.LoadAssetAtPath<Texture2D>(
                ATLAS_PNG_PATH);
            EditorGUIUtility.PingObject(saved);
            Selection.activeObject = saved;

            Debug.Log(
                $"[HectonSkyTools] Cloud atlas saved: {ATLAS_PNG_PATH}\n" +
                $"  Resolution: {width}x{height}\n" +
                $"  Flow Strength: {_flowStrength}\n" +
                $"  Channels: R=Density, G=Detail, BA=CurlFlowmap\n" +
                $"  Import: Linear, BC7, Read/Write Off, Repeat");
        }

        private static Texture2D CaptureReadableTexture(Texture texture, int width, int height)
        {
            RenderTexture temp = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            Texture2D readable = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            bool returned = false;

            try
            {
                Graphics.Blit(texture, temp);
                RenderTexture.active = temp;
                readable.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                readable.Apply(false, false);
                returned = true;
                return readable;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temp);
                if (!returned)
                    DestroyImmediate(readable);
            }
        }

        private static float ReadRed01(NativeArray<Color32> pixels, int index)
        {
            return pixels[index].r * (1f / 255f);
        }

        private static byte Encode01(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(value) * 255f), 0, 255);
        }

        // =============================================================
        // UTILITY: Ensure directory exists for an asset path
        //
        // Given "Assets/Foo/Bar/file.ext", creates Foo and Bar
        // directories if they don't exist, using AssetDatabase
        // so Unity tracks them properly.
        // =============================================================

        private void EnsureDirectoryExists(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath);

            if (string.IsNullOrEmpty(directory))
                return;

            // Normalize separators
            directory = directory.Replace('\\', '/');

            if (AssetDatabase.IsValidFolder(directory))
                return;

            // Split into parts and create incrementally
            // e.g. "Assets/_Project/Art/Models" ->
            //   ["Assets", "_Project", "Art", "Models"]
            string[] parts = directory.Split('/');
            string current = parts[0]; // "Assets"

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
