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
                "Both source textures must have Read/Write enabled\n" +
                "in their import settings.",
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
            else if (!_densityMap.isReadable)
            {
                EditorGUILayout.HelpBox(
                    "Density Map is not readable! Enable Read/Write in " +
                    "the texture import settings.", MessageType.Error);
                canPack = false;
            }

            if (_detailMap == null)
            {
                EditorGUILayout.HelpBox(
                    "Detail Map is required.", MessageType.Warning);
                canPack = false;
            }
            else if (!_detailMap.isReadable)
            {
                EditorGUILayout.HelpBox(
                    "Detail Map is not readable! Enable Read/Write in " +
                    "the texture import settings.", MessageType.Error);
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

            // Read source pixels
            Color[] densityPixels = _densityMap.GetPixels();
            Color[] detailPixels  = _detailMap.GetPixels(
                0, 0, _detailMap.width, _detailMap.height);

            // If detail map is different size, we need to sample it
            // at density map resolution using bilinear interpolation
            bool sizeMismatch = (_detailMap.width != width ||
                                 _detailMap.height != height);

            // Build density grayscale array for gradient computation
            // Using red channel as density value
            float[] density = new float[width * height];
            for (int i = 0; i < density.Length; i++)
            {
                density[i] = densityPixels[i].r;
            }

            // ---------------------------------------------------------
            // CURL NOISE FROM DENSITY GRADIENT
            //
            // For each pixel:
            //   1. Compute gradient via central differences (wrapping)
            //      gradX = density[x+1, y] - density[x-1, y]
            //      gradY = density[x, y+1] - density[x, y-1]
            //
            //   2. Rotate 90 degrees CCW to get curl vector
            //      curlX = -gradY
            //      curlY =  gradX
            //      This makes flow follow CONTOURS (iso-density lines)
            //      rather than flowing along the gradient (away from clouds)
            //
            //   3. Normalize, scale by _flowStrength, clamp [-1,1]
            //
            //   4. Map [-1,1] -> [0,1] for texture storage
            //      shader decodes: flowDir = tex.ba * 2 - 1
            // ---------------------------------------------------------

            Color[] atlasPixels = new Color[width * height];

            // Progress bar for large textures
            int totalPixels = width * height;
            int progressInterval = Mathf.Max(1, totalPixels / 100);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;

                    // -- R channel: density --
                    float r = density[idx];

                    // -- G channel: detail --
                    float g;
                    if (sizeMismatch)
                    {
                        // Bilinear sample from detail map at this UV
                        float u = (float)x / (width - 1);
                        float v = (float)y / (height - 1);
                        g = SampleBilinear(detailPixels,
                            _detailMap.width, _detailMap.height, u, v).r;
                    }
                    else
                    {
                        g = detailPixels[idx].r;
                    }

                    // -- B,A channels: curl noise flowmap --

                    // Central differences with wrapping (tiling textures)
                    int xP = (x + 1) % width;   // x + 1, wrapped
                    int xN = (x - 1 + width) % width; // x - 1, wrapped
                    int yP = (y + 1) % height;  // y + 1, wrapped
                    int yN = (y - 1 + height) % height; // y - 1, wrapped

                    float gradX = density[y * width + xP]
                                - density[y * width + xN];
                    float gradY = density[yP * width + x]
                                - density[yN * width + x];

                    // Rotate 90 degrees CCW: curl = (-gradY, gradX)
                    // This makes flow follow density contours
                    float curlX = -gradY;
                    float curlY =  gradX;

                    // Normalize curl vector
                    float curlLen = Mathf.Sqrt(curlX * curlX + curlY * curlY);
                    if (curlLen > 0.0001f)
                    {
                        curlX /= curlLen;
                        curlY /= curlLen;
                    }
                    else
                    {
                        // Zero gradient = no flow direction
                        curlX = 0f;
                        curlY = 0f;
                    }

                    // Scale by flow strength and clamp
                    curlX = Mathf.Clamp(curlX * _flowStrength, -1f, 1f);
                    curlY = Mathf.Clamp(curlY * _flowStrength, -1f, 1f);

                    // Map [-1, 1] -> [0, 1]
                    float b = curlX * 0.5f + 0.5f;
                    float a = curlY * 0.5f + 0.5f;

                    atlasPixels[idx] = new Color(r, g, b, a);

                    // Progress bar
                    if (idx % progressInterval == 0)
                    {
                        float progress = (float)idx / totalPixels;
                        if (EditorUtility.DisplayCancelableProgressBar(
                            "Packing Cloud Atlas",
                            $"Processing pixel {idx}/{totalPixels}",
                            progress))
                        {
                            EditorUtility.ClearProgressBar();
                            Debug.LogWarning(
                                "[HectonSkyTools] Atlas packing cancelled.");
                            return;
                        }
                    }
                }
            }

            EditorUtility.ClearProgressBar();

            // ---------------------------------------------------------
            // CREATE AND SAVE PNG
            // ---------------------------------------------------------

            Texture2D atlas = new Texture2D(width, height,
                TextureFormat.RGBA32, false, true); // linear color space
            atlas.name = "HectonSkyAtlas_RGBA";
            atlas.SetPixels(atlasPixels);
            atlas.Apply(false, false); // no mipmaps during save

            byte[] pngData = atlas.EncodeToPNG();

            // Clean up the temporary texture
            DestroyImmediate(atlas);

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
            //   - Read/Write ON (for potential runtime access)
            //   - No compression or high-quality compression
            //     (compression destroys flowmap precision)
            //   - Filter: Bilinear
            //   - Wrap: Repeat (clouds tile)
            // ---------------------------------------------------------

            TextureImporter importer = AssetImporter.GetAtPath(ATLAS_PNG_PATH)
                as TextureImporter;

            if (importer != null)
            {
                importer.sRGBTexture      = false;  // LINEAR -- flow data
                importer.isReadable       = true;
                importer.textureCompression =
                    TextureImporterCompression.Uncompressed;
                importer.filterMode       = FilterMode.Bilinear;
                importer.wrapMode         = TextureWrapMode.Repeat;
                importer.mipmapEnabled    = true;
                importer.alphaIsTransparency = false;
                importer.alphaSource      =
                    TextureImporterAlphaSource.FromInput;

                // Max size -- match source
                importer.maxTextureSize = Mathf.Max(width, height);

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
                $"  Import: Linear, Uncompressed, Repeat");
        }

        // =============================================================
        // UTILITY: Bilinear texture sampling
        //
        // Samples a pixel array at fractional UV coordinates using
        // bilinear interpolation. Used when Detail Map and Density Map
        // have different resolutions.
        // =============================================================

        private Color SampleBilinear(Color[] pixels,
            int texWidth, int texHeight, float u, float v)
        {
            // Convert UV [0,1] to pixel coordinates
            float px = u * (texWidth - 1);
            float py = v * (texHeight - 1);

            // Four nearest pixel indices
            int x0 = Mathf.FloorToInt(px);
            int y0 = Mathf.FloorToInt(py);
            int x1 = Mathf.Min(x0 + 1, texWidth - 1);
            int y1 = Mathf.Min(y0 + 1, texHeight - 1);

            // Fractional parts
            float fx = px - x0;
            float fy = py - y0;

            // Four corner samples
            Color c00 = pixels[y0 * texWidth + x0];
            Color c10 = pixels[y0 * texWidth + x1];
            Color c01 = pixels[y1 * texWidth + x0];
            Color c11 = pixels[y1 * texWidth + x1];

            // Bilinear interpolation
            Color c0 = Color.Lerp(c00, c10, fx);
            Color c1 = Color.Lerp(c01, c11, fx);

            return Color.Lerp(c0, c1, fy);
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
