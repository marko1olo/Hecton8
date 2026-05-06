// ============================================================================
// HECTON-8 — HectonMeshGenerator.cs
// Editor utility for procedural sky dome mesh generation.
//
// PURPOSE:
//   Generates an inverted hemisphere mesh for use as a sky dome.
//   The mesh is saved as a persistent .asset file for reuse.
//
// MESH SPECIFICATION:
//   • Shape: Hemisphere (upper half of sphere, Y ≥ 0)
//   • Normals: Inverted (pointing INWARD) — camera sits inside the dome
//   • UV Mapping: Spherical projection, seamless at zenith
//   • Radius: 1 unit (scale via Transform in scene)
//   • Segments: 64 longitude × 32 latitude (2048 quads = 4096 tris)
//   • Bottom ring: Y = 0 (horizon), closed with a flat cap
//
// USAGE:
//   Menu: Tools → Hecton → Generate Sky Dome
//   Output: Assets/_Project/Art/Models/SkyDome_Inverted.asset
//
// SHADER COMPATIBILITY:
//   Designed for Hecton_AlienSky_Master.shader:
//     • Cull Front (shader culls front faces, our inverted normals face inward)
//     • UV.x = longitude (0→1 around dome), UV.y = latitude (0=horizon, 1=zenith)
//     • Seamless at zenith: all top-ring vertices share UV.y = 1.0
//
// NOTES:
//   • Editor-only script (#if UNITY_EDITOR)
//   • No runtime allocations — runs once in editor
//   • Creates output directory if it doesn't exist
//   • Overwrites existing asset at the same path
// ============================================================================

#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.IO;

namespace Hecton8.Editor
{
    public static class HectonMeshGenerator
    {
        // ══════════════════════════════════════════════════════════
        //  CONSTANTS
        // ══════════════════════════════════════════════════════════

        /// <summary>Number of segments around the dome (longitude).</summary>
        private const int LongitudeSegments = 64;

        /// <summary>Number of segments from horizon to zenith (latitude).</summary>
        private const int LatitudeSegments = 32;

        /// <summary>Dome radius in local space. Scale via Transform.</summary>
        private const float Radius = 1f;

        /// <summary>Output directory relative to Assets/.</summary>
        private const string OutputDirectory = "Assets/_Project/Art/Models";

        /// <summary>Output asset filename.</summary>
        private const string OutputFilename = "SkyDome_Inverted.asset";

        // ══════════════════════════════════════════════════════════
        //  MENU ITEM
        // ══════════════════════════════════════════════════════════

        [MenuItem("Tools/Hecton/Generate Sky Dome", false, 100)]
        private static void GenerateSkyDome()
        {
            Mesh mesh = CreateInvertedHemisphereMesh();

            // ── Ensure output directory exists ──
            if (!Directory.Exists(OutputDirectory))
            {
                Directory.CreateDirectory(OutputDirectory);
                AssetDatabase.Refresh();
            }

            string fullPath = Path.Combine(OutputDirectory, OutputFilename);

            // ── Check for existing asset ──
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(fullPath);

            if (existingMesh != null)
            {
                // Overwrite existing asset (preserves references in scenes)
                EditorUtility.CopySerialized(mesh, existingMesh);
                AssetDatabase.SaveAssets();

                Debug.Log(
                    $"[HectonMeshGenerator] Sky dome mesh UPDATED at: {fullPath}\n" +
                    $"  Vertices: {existingMesh.vertexCount}\n" +
                    $"  Triangles: {ResolveTriangleCount(existingMesh)}\n" +
                    $"  Existing references preserved.");
            }
            else
            {
                // Create new asset
                AssetDatabase.CreateAsset(mesh, fullPath);
                AssetDatabase.SaveAssets();

                Debug.Log(
                    $"[HectonMeshGenerator] Sky dome mesh CREATED at: {fullPath}\n" +
                    $"  Vertices: {mesh.vertexCount}\n" +
                    $"  Triangles: {ResolveTriangleCount(mesh)}");
            }

            // ── Ping in Project window ──
            EditorGUIUtility.PingObject(
                AssetDatabase.LoadAssetAtPath<Mesh>(fullPath));
        }

        // ══════════════════════════════════════════════════════════
        //  MESH GENERATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Creates an inverted hemisphere mesh.
        ///
        /// GEOMETRY LAYOUT:
        ///
        ///   Latitude rings: 0 (horizon, Y=0) to LatitudeSegments (zenith, Y=R).
        ///   Longitude slices: 0 to LongitudeSegments (wraps, last = first UV.x=1).
        ///
        ///   Vertex grid: (LongitudeSegments + 1) × (LatitudeSegments + 1)
        ///   +1 on longitude for UV seam (x=0 and x=1 are same position, different UV).
        ///
        ///   Plus 1 zenith vertex (single point at top, shared by all top triangles).
        ///   Plus (LongitudeSegments + 1) bottom ring vertices for horizon cap.
        ///   Plus 1 center vertex for bottom cap.
        ///
        /// UV MAPPING:
        ///   U = longitude / LongitudeSegments  → [0, 1] around dome
        ///   V = latitude / LatitudeSegments     → [0, 1] horizon to zenith
        ///
        ///   Zenith vertex: U = 0.5, V = 1.0 (center of texture top edge)
        ///   This prevents UV pinching at the pole.
        ///
        /// WINDING ORDER:
        ///   Inverted (CW when viewed from outside = CCW from inside).
        ///   Combined with Cull Front in shader = visible from inside.
        ///
        /// NORMALS:
        ///   All point INWARD (toward center of sphere).
        ///   normal = -normalize(position)
        /// </summary>
        private static Mesh CreateInvertedHemisphereMesh()
        {
            int lonSegments = LongitudeSegments;
            int latSegments = LatitudeSegments;

            // ── Vertex count calculation ──
            // Main grid: (lon+1) × (lat+1) — includes UV seam column
            // Bottom cap: center vertex (1)
            // Zenith is part of the grid (top ring)
            int gridVertCount = (lonSegments + 1) * (latSegments + 1);
            int bottomCenterIdx = gridVertCount; // index of cap center vertex
            int totalVerts = gridVertCount + 1;  // +1 for bottom cap center

            Vector3[] vertices = new Vector3[totalVerts];
            Vector3[] normals  = new Vector3[totalVerts];
            Vector2[] uvs      = new Vector2[totalVerts];

            // ── Triangle count calculation ──
            // Main dome: lon × lat × 2 triangles per quad × 3 indices
            // Bottom cap: lon triangles × 3 indices
            int mainTriCount = lonSegments * latSegments * 6;
            int capTriCount  = lonSegments * 3;
            int[] triangles  = new int[mainTriCount + capTriCount];

            // ══════════════════════════════════════════════
            //  GENERATE VERTICES (hemisphere grid)
            // ══════════════════════════════════════════════

            float piHalf = Mathf.PI * 0.5f;
            float pi2    = Mathf.PI * 2f;

            int vertIdx = 0;

            for (int lat = 0; lat <= latSegments; lat++)
            {
                // latFraction: 0 (horizon) → 1 (zenith)
                float latFraction = (float)lat / latSegments;

                // Polar angle: 0 (horizon, XZ plane) → π/2 (zenith, +Y axis)
                float polarAngle = latFraction * piHalf;

                float sinPolar = Mathf.Sin(polarAngle);
                float cosPolar = Mathf.Cos(polarAngle);

                // Y = sin(polar) × R → 0 at horizon, R at zenith
                float y = sinPolar * Radius;

                // Horizontal radius at this latitude
                float ringRadius = cosPolar * Radius;

                for (int lon = 0; lon <= lonSegments; lon++)
                {
                    // lonFraction: 0 → 1 (full circle)
                    float lonFraction = (float)lon / lonSegments;

                    // Azimuth angle: 0 → 2π
                    float azimuth = lonFraction * pi2;

                    float x = Mathf.Cos(azimuth) * ringRadius;
                    float z = Mathf.Sin(azimuth) * ringRadius;

                    vertices[vertIdx] = new Vector3(x, y, z);

                    // ── Inverted normal (pointing INWARD) ──
                    // Normal = -normalized(position) for inward-facing sphere
                    Vector3 outward = new Vector3(x, y, z).normalized;
                    normals[vertIdx] = -outward;

                    // ── UV mapping ──
                    // U = longitude [0..1], V = latitude [0..1]
                    // V=0 at horizon, V=1 at zenith
                    uvs[vertIdx] = new Vector2(lonFraction, latFraction);

                    vertIdx++;
                }
            }

            // ── Bottom cap center vertex (Y = 0, center of horizon ring) ──
            vertices[bottomCenterIdx] = Vector3.zero;
            normals[bottomCenterIdx]  = Vector3.up; // pointing inward (upward from below)
            uvs[bottomCenterIdx]      = new Vector2(0.5f, 0f);

            // ══════════════════════════════════════════════
            //  GENERATE TRIANGLES (inverted winding)
            // ══════════════════════════════════════════════

            int triIdx = 0;
            int rowWidth = lonSegments + 1; // vertices per latitude ring

            // ── Main dome quads ──
            // Each quad = 2 triangles between adjacent latitude rings.
            // INVERTED winding: swap triangle vertex order for inward-facing.
            //
            // Standard (outward): (A, B, C) and (C, B, D)
            // Inverted (inward):  (A, C, B) and (C, D, B)
            //
            //  A --- B     A = lat × rowWidth + lon
            //  |   / |     B = A + 1
            //  |  /  |     C = (lat+1) × rowWidth + lon
            //  | /   |     D = C + 1
            //  C --- D

            for (int lat = 0; lat < latSegments; lat++)
            {
                for (int lon = 0; lon < lonSegments; lon++)
                {
                    int a = lat * rowWidth + lon;
                    int b = a + 1;
                    int c = (lat + 1) * rowWidth + lon;
                    int d = c + 1;

                    // Triangle 1: A, C, B (inverted)
                    triangles[triIdx++] = a;
                    triangles[triIdx++] = c;
                    triangles[triIdx++] = b;

                    // Triangle 2: B, C, D (inverted)
                    triangles[triIdx++] = b;
                    triangles[triIdx++] = c;
                    triangles[triIdx++] = d;
                }
            }

            // ── Bottom cap triangles ──
            // Connects the horizon ring (lat=0) to the center vertex.
            // Fills the hole at the bottom of the hemisphere.
            //
            // For each longitude segment:
            //   Triangle: center, lon+1, lon (inverted winding)
            //
            // This creates a flat disc at Y=0, visible from above (inside dome).

            for (int lon = 0; lon < lonSegments; lon++)
            {
                int horizonA = lon;          // lat=0 ring, current
                int horizonB = lon + 1;      // lat=0 ring, next

                // Inverted winding: center, B, A
                triangles[triIdx++] = bottomCenterIdx;
                triangles[triIdx++] = horizonB;
                triangles[triIdx++] = horizonA;
            }

            // ══════════════════════════════════════════════
            //  ASSEMBLE MESH
            // ══════════════════════════════════════════════

            Mesh mesh = new Mesh();
            mesh.name = "SkyDome_Inverted";

            // Use 32-bit index buffer if vertex count exceeds 16-bit limit
            // (65535). Our mesh: ~2145 verts — 16-bit is fine.
            mesh.indexFormat = totalVerts > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);

            // ── Tangents for normal mapping (if ever needed) ──
            mesh.RecalculateTangents();

            // ── Bounds: sphere of radius 1 centered at origin ──
            mesh.bounds = new Bounds(
                new Vector3(0f, Radius * 0.5f, 0f),
                new Vector3(Radius * 2f, Radius, Radius * 2f));

            return mesh;
        }

        // ══════════════════════════════════════════════════════════
        //  VALIDATION
        // ══════════════════════════════════════════════════════════

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

        [MenuItem("Tools/Hecton/Generate Sky Dome", true)]
        private static bool ValidateGenerateSkyDome()
        {
            // Always available in editor
            return true;
        }
    }
}

#endif // UNITY_EDITOR
