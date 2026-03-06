// ============================================================================
//  HectonGeologyNode.cs
//  A continental-shelf geology generator for MapMagic 2 (v2.1.18+).
//  Exposes an AnimationCurve so artists can sculpt the cross-section profile
//  (Abyss → Ridge → Shelf) without touching code.
//
//  Outputs: Height (primary), Terrace Mask, Cracks Mask
//
//  Place under: Assets/MapMagic/Generators/Matrix/Runtime/
// ============================================================================

using UnityEngine;
using MapMagic.Core;
using MapMagic.Products;
using MapMagic.Nodes;
using Den.Tools;
using Den.Tools.Matrices;

namespace MapMagic.Nodes.MatrixGenerators
{
    [System.Serializable]
    [GeneratorMenu(
        menu = "Hecton",
        name = "Geology Processor",
        disengageable = true)]
    [UnityEngine.Scripting.Preserve]
    public class HectonGeologyNode : Generator, IOutlet<MatrixWorld>
    {
        // =================================================================
        //  ADDITIONAL OUTLETS (the node itself is the height outlet via
        //  IOutlet<MatrixWorld>, which preserves existing serialised graphs)
        // =================================================================

        public Outlet<MatrixWorld> terraceMaskOut = new Outlet<MatrixWorld>();
        public Outlet<MatrixWorld> cracksMaskOut  = new Outlet<MatrixWorld>();

        // =================================================================
        //  INSPECTOR FIELDS  (unchanged)
        // =================================================================

        /// <summary>
        /// The cross-section profile of the terrain.
        /// X-axis: normalised Perlin noise (0 = valley, 1 = peak).
        /// Y-axis: output height (0..1).
        /// </summary>
        public AnimationCurve terrainProfile;

        /// <summary>World-space wavelength of the main ridge/shelf pattern.</summary>
        public float globalScale = 15000f;

        /// <summary>Seed that shifts all noise layers.</summary>
        public int seed = 12345;

        /// <summary>Global height multiplier applied to the final value.</summary>
        public float intensity = 1.0f;

        /// <summary>World-space cell size for tectonic Voronoi plates.</summary>
        public float plateScale = 2000f;

        /// <summary>Normalised height where terraces begin (0..1).</summary>
        public float terraceStart = 0.4f;

        /// <summary>Normalised height where terraces end (0..1).</summary>
        public float terraceEnd = 0.7f;

        /// <summary>Number of discrete terrace steps.</summary>
        public int terraceSteps = 12;

        /// <summary>World-space wavelength of crack / fracture detail noise.</summary>
        public float crackScale = 200f;

        /// <summary>Amplitude of cracks subtracted from terrace zones.</summary>
        public float crackIntensity = 0.1f;

        // =================================================================
        //  CONSTRUCTOR – default bell-curve profile  (unchanged)
        // =================================================================
        public HectonGeologyNode()
        {
            terrainProfile = new AnimationCurve(
                new Keyframe(0.00f, 0.04f,  0.0f,  0.2f),
                new Keyframe(0.25f, 0.08f,  0.2f,  0.5f),
                new Keyframe(0.40f, 0.28f,  2.0f,  3.5f),
                new Keyframe(0.50f, 0.95f,  0.0f,  0.0f),
                new Keyframe(0.60f, 0.28f, -3.5f, -2.0f),
                new Keyframe(0.75f, 0.08f, -0.5f, -0.2f),
                new Keyframe(1.00f, 0.04f, -0.2f,  0.0f)
            );

            terrainProfile.preWrapMode  = WrapMode.Clamp;
            terrainProfile.postWrapMode = WrapMode.Clamp;
        }

        // =================================================================
        //  GENERATE
        // =================================================================
        public override void Generate(TileData data, StopToken stop)
        {
            if (stop != null && stop.stop) return;

            // ----------------------------------------------------------
            // 1.  Grab area info and build three MatrixWorld outputs
            // ----------------------------------------------------------
            CoordRect rect = data.area.full.rect;

            // worldPos and worldSize are Den.Tools.Vector2D (x, z doubles).
            // MatrixWorld constructor needs UnityEngine.Vector3.
            Vector2D wp = data.area.full.worldPos;
            Vector2D ws = data.area.full.worldSize;

            Vector3 worldPos  = new Vector3((float)wp.x, 0f, (float)wp.z);
            Vector3 worldSize = new Vector3((float)ws.x, 1f, (float)ws.z);

            MatrixWorld heightMatrix  = new MatrixWorld(rect, worldPos, worldSize);
            MatrixWorld terraceMatrix = new MatrixWorld(rect, worldPos, worldSize);
            MatrixWorld cracksMatrix  = new MatrixWorld(rect, worldPos, worldSize);

            // Pixel-to-world conversion factor
            float pixelSize = (float)ws.x / rect.size.x;

            // World-space origin of this tile
            float originX = (float)wp.x;
            float originZ = (float)wp.z;

            // ----------------------------------------------------------
            // 2.  Pre-sample AnimationCurve into a thread-safe LUT
            // ----------------------------------------------------------
            const int LUT_RES = 512;
            float[] profileLUT = new float[LUT_RES + 1];

            if (terrainProfile != null && terrainProfile.length > 0)
            {
                float invRes = 1f / LUT_RES;
                for (int i = 0; i <= LUT_RES; i++)
                    profileLUT[i] = terrainProfile.Evaluate(i * invRes);
            }
            else
            {
                float invRes = 1f / LUT_RES;
                for (int i = 0; i <= LUT_RES; i++)
                    profileLUT[i] = i * invRes;
            }

            // ----------------------------------------------------------
            // 3.  Precompute constants
            // ----------------------------------------------------------
            float invGlobalScale = 1f / Mathf.Max(globalScale, 0.001f);
            float invPlateScale  = 1f / Mathf.Max(plateScale,  0.001f);
            float invCrackScale  = 1f / Mathf.Max(crackScale,  0.001f);

            float seedFX = seed * 0.01337f;
            float seedFZ = seed * 0.02591f;

            float stepsF          = (float)Mathf.Max(terraceSteps, 1);
            float terraceRange    = Mathf.Max(terraceEnd - terraceStart, 0.0001f);
            float invTerraceRange = 1f / terraceRange;

            // ----------------------------------------------------------
            // 4.  Per-pixel generation
            // ----------------------------------------------------------
            for (int lx = 0; lx < rect.size.x; lx++)
            {
                if (stop != null && stop.stop) return;

                for (int lz = 0; lz < rect.size.z; lz++)
                {
                    // ====================================================
                    //  WORLD COORDINATES (seamless across tiles)
                    // ====================================================
                    float wx = originX + lx * pixelSize;
                    float wz = originZ + lz * pixelSize;

                    // ====================================================
                    //  PHASE 1 – SPINE & PROFILE
                    // ====================================================
                    float n = Mathf.PerlinNoise(
                        wx * invGlobalScale + seedFX,
                        wz * invGlobalScale + seedFZ);
                    n = Mathf.Clamp01(n);

                    float h = SampleLUT(profileLUT, n, LUT_RES);

                    // ====================================================
                    //  PHASE 2 – TECTONIC PLATES (Voronoi modulation)
                    // ====================================================
                    float v = CellularNoise(
                        wx * invPlateScale + seedFX * 3.71f,
                        wz * invPlateScale + seedFZ * 3.71f,
                        seed);

                    h *= 0.7f + 0.3f * v;

                    // ====================================================
                    //  PHASE 3 – TERRACES
                    // ====================================================
                    float tMask = 0f;

                    if (h > terraceStart && h < terraceEnd)
                    {
                        float t = (h - terraceStart) * invTerraceRange;
                        tMask = 1f - Mathf.Abs(t * 2f - 1f);
                        tMask = tMask * tMask * (3f - 2f * tMask); // smoothstep

                        float stepped = Mathf.Floor(h * stepsF) / stepsF;
                        h = Mathf.Lerp(h, stepped, tMask);
                    }

                    // ====================================================
                    //  PHASE 4 – CRACKS (fractures on terraces only)
                    // ====================================================
                    float crackVal = 0f;

                    if (tMask > 0.01f)
                    {
                        float c1 = Mathf.PerlinNoise(
                            wx * invCrackScale          + 347.5f + seedFX * 5.13f,
                            wz * invCrackScale          + 521.3f + seedFZ * 5.13f);

                        float c2 = Mathf.PerlinNoise(
                            wx * invCrackScale * 2.17f  + 891.2f,
                            wz * invCrackScale * 2.17f  + 233.7f);

                        float crack = (c1 + c2 * 0.5f) * 0.667f - 0.5f;
                        h -= crack * crackIntensity * tMask;

                        // Remap crack from [-0.5, 0.5] → [0, 1] for mask output
                        crackVal = Mathf.Clamp01(crack + 0.5f);
                    }

                    // ====================================================
                    //  WRITE ALL THREE OUTPUTS
                    // ====================================================
                    int ix = lx + rect.offset.x;
                    int iz = lz + rect.offset.z;

                    heightMatrix [ix, iz] = Mathf.Clamp01(h * intensity);
                    terraceMatrix[ix, iz] = tMask;
                    cracksMatrix [ix, iz] = crackVal;
                }
            }

            // ----------------------------------------------------------
            // 5.  Store all results
            //     'this' = primary height outlet (IOutlet<MatrixWorld>)
            //     outlet fields = secondary mask outlets
            // ----------------------------------------------------------
            data.StoreProduct(this,            heightMatrix);
            data.StoreProduct(terraceMaskOut,  terraceMatrix);
            data.StoreProduct(cracksMaskOut,   cracksMatrix);
        }

        // =================================================================
        //  HELPER: LUT Interpolation
        // =================================================================
        private static float SampleLUT(float[] lut, float t, int resolution)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            float fi = t * resolution;
            int   lo = (int)fi;
            if (lo >= resolution) return lut[resolution];
            float frac = fi - lo;
            return lut[lo] + (lut[lo + 1] - lut[lo]) * frac;
        }

        // =================================================================
        //  HELPER: Cellular (Voronoi) Noise – zero external dependencies
        // =================================================================
        private static float CellularNoise(float x, float z, int seed)
        {
            int   cellX = Mathf.FloorToInt(x);
            int   cellZ = Mathf.FloorToInt(z);
            float fracX = x - cellX;
            float fracZ = z - cellZ;

            float minDistSq = float.MaxValue;

            for (int ox = -1; ox <= 1; ox++)
            {
                for (int oz = -1; oz <= 1; oz++)
                {
                    int nx = cellX + ox;
                    int nz = cellZ + oz;

                    float jx = HashToFloat(nx, nz, seed);
                    float jz = HashToFloat(nx, nz, seed + 7919);

                    float dx = ox + jx - fracX;
                    float dz = oz + jz - fracZ;

                    float dSq = dx * dx + dz * dz;
                    if (dSq < minDistSq) minDistSq = dSq;
                }
            }

            return Mathf.Clamp01(Mathf.Sqrt(minDistSq) * 1.414f);
        }

        // =================================================================
        //  HELPER: Integer Hash → float [0, 1)
        // =================================================================
        private static float HashToFloat(int x, int z, int seed)
        {
            uint h = (uint)(x * 374761393 + z * 668265263 + seed * 1274126177);
            h = ((h >> 13) ^ h) * 1103515245u;
            h = (h >> 16) ^ h;
            return (h & 0x00FFFFFFu) * (1.0f / 16777216.0f);
        }
    }
}