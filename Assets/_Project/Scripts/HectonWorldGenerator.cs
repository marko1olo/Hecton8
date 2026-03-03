// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  HectonWorldGenerator.cs — Project HECTON-8 World Engine                   ║
// ║  Unity 6 (URP) | Burst + Jobs | Chunk Streaming | LOD System               ║
// ║  v1.0                                                                       ║
// ║                                                                             ║
// ║  REQUIRED PACKAGES (Package Manager):                                       ║
// ║  • com.unity.burst           — Burst Compiler                               ║
// ║  • com.unity.mathematics     — Unity.Mathematics (noise, math)              ║
// ║  • com.unity.collections     — NativeArray, NativeList                      ║
// ║                                                                             ║
// ║  ARCHITECTURE:                                                              ║
// ║  ─────────────                                                              ║
// ║  1. Central Spine (archipelago) runs along Z with domain-warped centerline. ║
// ║  2. West slope (X < spine): gentle 6 km descent, stepped terraces.          ║
// ║  3. East slope (X > spine): aggressive 1.5 km cliff into the Abyss.        ║
// ║  4. LOD0 chunks (2 m spacing, 500 m radius) — full detail + colliders.     ║
// ║  5. LOD1 chunks (16 m spacing, 2000 m radius) — visual only.               ║
// ║  6. Vertex Colors encode Slope / Depth / Cave Edge / Biome for shaders.    ║
// ║  7. POI hooks: potentialBaseLocations, resourceNodes — for gameplay.        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
// using System.Threading.Tasks;  // REMOVED — Parallel.For не совместим с Unity 6
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
#if UNITY_EDITOR
using UnityEditor;
#endif

// ════════════════════════════════════════════════════════════════════════════════
//  REGION: DATA STRUCTURES
// ════════════════════════════════════════════════════════════════════════════════
#region Data Structures

// ── Managed noise layer (Inspector-friendly) ────────────────────────────────
[System.Serializable]
public class HectonNoiseLayer
{
    [Tooltip("Base noise scale. Smaller = larger patterns.")]
    public float scale = 0.005f;

    [Range(1, 8)]
    public int octaves = 4;

    public float lacunarity = 2.0f;

    [Range(0f, 1f)]
    public float persistence = 0.5f;

    public Vector3 offset;
    public int seed;
}

// ── Blittable noise data for Burst Jobs ─────────────────────────────────────
public struct NoiseData
{
    public float scale;
    public int octaves;
    public float lacunarity;
    public float persistence;
    public float3 offset;
    public int seed;

    public static NoiseData From(HectonNoiseLayer l)
    {
        if (l == null) return new NoiseData { scale = 0.005f, octaves = 1, lacunarity = 2f, persistence = 0.5f, seed = 0 };
        return new NoiseData
        {
            scale       = l.scale,
            octaves     = l.octaves,
            lacunarity  = l.lacunarity,
            persistence = l.persistence,
            offset      = new float3(l.offset.x, l.offset.y, l.offset.z),
            seed        = l.seed
        };
    }
}

// ── Settings: Spine (Central Ridge / Archipelago) ───────────────────────────
[System.Serializable]
public class SpineSettings
{
    [Tooltip("Max island height above sea level (Y=0).")]
    public float maxHeight = 400f;

    [Tooltip("Half-width of spine influence zone (m). Determines how wide the ridge is.")]
    public float width = 600f;

    [Tooltip("Max lateral displacement of spine centerline by domain warp (m).")]
    public float warpStrength = 1500f;

    [Tooltip("Noise that bends the spine left/right along its length.")]
    public HectonNoiseLayer warpNoise = new HectonNoiseLayer
        { scale = 0.0003f, octaves = 3, lacunarity = 2f, persistence = 0.5f, seed = 111 };

    [Tooltip("Noise that determines WHERE islands appear along the spine.")]
    public HectonNoiseLayer islandNoise = new HectonNoiseLayer
        { scale = 0.0004f, octaves = 2, lacunarity = 2f, persistence = 0.5f, seed = 222 };

    [Tooltip("Noise threshold for islands. Lower = more islands (0.3–0.6).")]
    [Range(0.2f, 0.8f)]
    public float islandThreshold = 0.45f;
}

// ── Settings: Slope Profiles ────────────────────────────────────────────────
[System.Serializable]
public class SlopeSettings
{
    [Tooltip("Length of western gentle slope from spine edge (m).")]
    public float westLength = 6000f;

    [Tooltip("Length of eastern cliff from spine edge (m).")]
    public float eastLength = 1500f;

    [Tooltip("West curve: X=0 (spine)→1 (far). Y=1 (sea level)→0 (max depth).")]
    public AnimationCurve westCurve = new AnimationCurve(
        new Keyframe(0.00f, 1.00f,  0.0f,  0.0f),
        new Keyframe(0.15f, 0.92f, -0.3f, -0.3f),
        new Keyframe(0.40f, 0.65f, -0.7f, -0.7f),
        new Keyframe(0.70f, 0.20f, -0.6f, -0.6f),
        new Keyframe(0.90f, 0.05f, -0.2f, -0.1f),
        new Keyframe(1.00f, 0.00f,  0.0f,  0.0f)
    );

    [Tooltip("East curve: steep cliff. Same axes as West.")]
    public AnimationCurve eastCurve = new AnimationCurve(
        new Keyframe(0.00f, 1.00f,  0.0f,  0.0f),
        new Keyframe(0.10f, 0.70f, -4.0f, -4.0f),
        new Keyframe(0.30f, 0.10f, -1.5f, -1.5f),
        new Keyframe(0.55f, 0.02f, -0.1f, -0.1f),
        new Keyframe(1.00f, 0.00f,  0.0f,  0.0f)
    );

    [Tooltip("Maximum ocean depth (m). The Abyss.")]
    public float maxDepth = 5000f;

    [Header("Terraces (West Only)")]
    [Tooltip("Number of flat terrace steps on the west slope. 0 = smooth.")]
    [Range(0, 16)]
    public int terraceCount = 8;

    [Tooltip("Strength of terrace quantization. 0 = smooth, 1 = hard steps.")]
    [Range(0f, 1f)]
    public float terraceStrength = 0.5f;
}

// ── Settings: Biome Surface Noise ───────────────────────────────────────────
[System.Serializable]
public class BiomeSettings
{
    [Tooltip("Large-scale noise mask: 0 = flat sand, 1 = aggressive rock.")]
    public HectonNoiseLayer biomeNoise = new HectonNoiseLayer
        { scale = 0.00015f, octaves = 2, lacunarity = 2f, persistence = 0.5f, seed = 555 };

    [Tooltip("Remap curve for biome mask. Linear = smooth blend, step = sharp borders.")]
    public AnimationCurve biomeRemapCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Flat Biome (Sand / Dunes)")]
    public HectonNoiseLayer flatSurfaceNoise = new HectonNoiseLayer
        { scale = 0.003f, octaves = 2, lacunarity = 2f, persistence = 0.4f, seed = 333 };
    public float flatSurfaceAmplitude = 5f;

    [Header("Aggressive Biome (Rock / Fractures)")]
    public HectonNoiseLayer aggressiveSurfaceNoise = new HectonNoiseLayer
        { scale = 0.005f, octaves = 5, lacunarity = 2f, persistence = 0.45f, seed = 444 };
    public float aggressiveSurfaceAmplitude = 40f;

    [Tooltip("Displacement multiplier in flat biome (0 = none, 1 = full).")]
    [Range(0f, 1f)]
    public float flatDisplacementFactor = 0.1f;
}

// ── Settings: 3D Displacement ───────────────────────────────────────────────
[System.Serializable]
public class DisplacementSettings
{
    public HectonNoiseLayer noise = new HectonNoiseLayer
        { scale = 0.008f, octaves = 3, lacunarity = 2f, persistence = 0.5f, seed = 666 };

    [Tooltip("Displacement strength per axis (m).")]
    public Vector3 scale = new Vector3(20f, 15f, 20f);

    [Tooltip("Displacement is stronger on steep slopes.")]
    [Range(0f, 5f)]
    public float slopeWeight = 2f;
}

// ── Settings: Caves ─────────────────────────────────────────────────────────
[System.Serializable]
public class CaveSettings
{
    public HectonNoiseLayer noise = new HectonNoiseLayer
        { scale = 0.02f, octaves = 3, lacunarity = 2.2f, persistence = 0.5f, seed = 777 };

    [Tooltip("Noise value above this = hole. Lower = more caves.")]
    [Range(0.3f, 0.9f)]
    public float threshold = 0.62f;

    [Tooltip("Soft-edge width for vertex color B channel.")]
    [Range(0.01f, 0.2f)]
    public float edgeWidth = 0.05f;

    [Tooltip("Caves only appear below this Y depth.")]
    public float minDepth = -30f;
}

// ── Runtime Chunk Data ──────────────────────────────────────────────────────
public class HectonChunkData
{
    public int2 coord;
    public int lod;
    public GameObject go;
    public Mesh mesh;
}

// ── Generation Request ──────────────────────────────────────────────────────
public struct HectonChunkRequest
{
    public int2 coord;
    public int lod;
    public float distSq;
}

#endregion

// ════════════════════════════════════════════════════════════════════════════════
//  REGION: BURST NOISE HELPERS
// ════════════════════════════════════════════════════════════════════════════════
#region Burst Noise Helpers

public static class HectonNoise
{
    public static float SampleLUT(NativeArray<float> lut, float t)
    {
        t = math.clamp(t, 0f, 1f);
        float fi = t * (lut.Length - 1);
        int i0 = (int)fi;
        int i1 = math.min(i0 + 1, lut.Length - 1);
        return math.lerp(lut[i0], lut[i1], fi - i0);
    }

    public static float Fractal2D(float x, float z, NoiseData n)
    {
        float amp = 1f, freq = 1f, val = 0f, maxA = 0f;
        float ox = n.offset.x + n.seed * 17.1f;
        float oz = n.offset.z + n.seed * 31.7f;

        for (int i = 0; i < n.octaves; i++)
        {
            float2 p = new float2((x + ox) * n.scale * freq, (z + oz) * n.scale * freq);
            val  += (noise.snoise(p) * 0.5f + 0.5f) * amp;
            maxA += amp;
            amp  *= n.persistence;
            freq *= n.lacunarity;
        }
        return val / maxA;
    }

    public static float Fractal3D(float x, float y, float z, NoiseData n, float chanOff)
    {
        float amp = 1f, freq = 1f, val = 0f, maxA = 0f;
        float ox = n.offset.x + n.seed * 17.1f + chanOff;
        float oy = n.offset.y + n.seed * 31.7f + chanOff * 1.7f;
        float oz = n.offset.z + n.seed * 47.3f + chanOff * 2.3f;

        for (int i = 0; i < n.octaves; i++)
        {
            float3 p = new float3(
                (x + ox) * n.scale * freq,
                (y + oy) * n.scale * freq,
                (z + oz) * n.scale * freq);
            val  += (noise.snoise(p) * 0.5f + 0.5f) * amp;
            maxA += amp;
            amp  *= n.persistence;
            freq *= n.lacunarity;
        }
        return val / maxA;
    }
}

#endregion

// ════════════════════════════════════════════════════════════════════════════════
//  REGION: BURST JOBS
// ════════════════════════════════════════════════════════════════════════════════
#region Jobs

// ── JOB 1: Terrain Vertex Generation ────────────────────────────────────────
[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct HectonVertexJob : IJobParallelFor
{
    // Grid
    public int resX, resZ;
    public float originX, originZ;
    public float spacing;
    public int lodLevel;          // 0 = full detail, 1 = visual only

    // Map
    public float mapHalfSize;

    // Spine
    public float spineMaxHeight, spineWidth, spineWarpStrength, islandThreshold;
    public NoiseData warpNoise, islandNoise;

    // Slopes
    public float westLen, eastLen, maxDepth;
    public int terraceCount;
    public float terraceStrength;

    // Biome
    public float flatAmp, aggrAmp, flatDispFactor;
    public NoiseData biomeNoise, flatSurfNoise, aggrSurfNoise;

    // Displacement
    public float3 dispScale;
    public float slopeDispW;
    public NoiseData dispNoise;

    // Caves
    public float caveThresh, caveMinY;
    public NoiseData caveNoise;

    // LUTs (ReadOnly)
    [ReadOnly] public NativeArray<float> westLUT, eastLUT, biomeLUT;

    // Output
    [WriteOnly] public NativeArray<Vector3> outVerts;
    [WriteOnly] public NativeArray<Vector2> outUVs;
    [WriteOnly] public NativeArray<float>   outCave;
    [WriteOnly] public NativeArray<byte>    outIsCave;
    [WriteOnly] public NativeArray<float>   outBiome;

    public void Execute(int idx)
    {
        int lx = idx % resX;
        int lz = idx / resX;
        float wx = originX + lx * spacing;
        float wz = originZ + lz * spacing;

        // ═══ 1. DOMAIN WARP — spine centerline ═══
        float warpVal = HectonNoise.Fractal2D(0f, wz, warpNoise);
        float spineCX = (warpVal * 2f - 1f) * spineWarpStrength;

        // ═══ 2. SIGNED DISTANCE from spine ═══
        float dx   = wx - spineCX;
        float absDx = math.abs(dx);
        bool  west  = dx < 0f;

        // ═══ 3. SLOPE PROFILE ═══
        float sLen    = west ? westLen : eastLen;
        float normD   = math.saturate(absDx / math.max(sLen, 1f));
        float curveV;

        if (west) curveV = HectonNoise.SampleLUT(westLUT, normD);
        else      curveV = HectonNoise.SampleLUT(eastLUT, normD);

        // ═══ 4. TERRACES (west only) ═══
        if (west && terraceCount > 0 && terraceStrength > 0f)
        {
            float tc = (float)terraceCount;
            float stepped = math.round(curveV * tc) / tc;
            curveV = math.lerp(curveV, stepped, terraceStrength);
        }

        // ═══ 5. BASE FLOOR HEIGHT ═══
        float floor = math.lerp(-maxDepth, 0f, curveV);

        // ═══ 6. SPINE ELEVATION (Islands) ═══
        float spineInf = math.saturate(1f - absDx / math.max(spineWidth, 1f));
        spineInf *= spineInf;                               // quadratic falloff
        float islN = HectonNoise.Fractal2D(wx * 0.1f, wz, islandNoise);
        float islF = math.saturate((islN - islandThreshold) / math.max(1f - islandThreshold, 0.01f));
        float spineElev = spineMaxHeight * spineInf * islF;

        // ═══ 7. BIOME-BLENDED SURFACE NOISE ═══
        float bRaw  = HectonNoise.Fractal2D(wx, wz, biomeNoise);
        float bVal  = HectonNoise.SampleLUT(biomeLUT, bRaw);
        float flatN = HectonNoise.Fractal2D(wx, wz, flatSurfNoise);
        float agrN  = HectonNoise.Fractal2D(wx, wz, aggrSurfNoise);
        float surfY = (math.lerp(flatN, agrN, bVal) * 2f - 1f) *
                       math.lerp(flatAmp, aggrAmp, bVal);

        // ═══ 8. COMBINED HEIGHT ═══
        float y = floor + spineElev + surfY;

        // ═══ 9. MAP-EDGE FADE ═══
        float fadeX = math.saturate((mapHalfSize - math.abs(wx)) / 1000f);
        float fadeZ = math.saturate((mapHalfSize - math.abs(wz)) / 1000f);
        y = math.lerp(-maxDepth, y, fadeX * fadeZ);

        // ═══ 10. SLOPE WEIGHT (curve derivative proxy) ═══
        float slopeW = 0f;
        {
            const float dd = 0.01f;
            float nP = math.saturate(normD + dd);
            float nM = math.saturate(math.max(normD - dd, 0f));
            float cP, cM;
            if (west) { cP = HectonNoise.SampleLUT(westLUT, nP); cM = HectonNoise.SampleLUT(westLUT, nM); }
            else      { cP = HectonNoise.SampleLUT(eastLUT, nP); cM = HectonNoise.SampleLUT(eastLUT, nM); }
            float deriv = math.abs(cP - cM) / (2f * dd);
            slopeW = math.saturate(deriv * slopeDispW);
        }

        // ═══ 11. 3D DISPLACEMENT (LOD0 only) ═══
        float fx = wx, fy = y, fz = wz;
        if (lodLevel == 0)
        {
            float bioDisp  = math.lerp(flatDispFactor, 1f, bVal);
            float totalMul = slopeW * bioDisp;
            fx += (HectonNoise.Fractal3D(wx, y, wz, dispNoise,   0f) * 2f - 1f) * dispScale.x * totalMul;
            fy += (HectonNoise.Fractal3D(wx, y, wz, dispNoise, 100f) * 2f - 1f) * dispScale.y * totalMul;
            fz += (HectonNoise.Fractal3D(wx, y, wz, dispNoise, 200f) * 2f - 1f) * dispScale.z * totalMul;
        }

        outVerts[idx] = new Vector3(fx, fy, fz);
        outUVs[idx]   = new Vector2(wx * 0.01f, wz * 0.01f);

        // ═══ 12. CAVES (LOD0 only) ═══
        float caveVal = 0f;
        byte  caveBit = 0;
        if (lodLevel == 0 && fy < caveMinY)
        {
            caveVal = HectonNoise.Fractal3D(fx, fy, fz, caveNoise, 300f);
            caveVal *= math.lerp(0.8f, 1.1f, slopeW);  // bias: more caves on slopes
            caveBit = (byte)(caveVal > caveThresh ? 1 : 0);
        }

        outCave[idx]   = caveVal;
        outIsCave[idx] = caveBit;
        outBiome[idx]  = bVal;
    }
}

// ── JOB 2: Normal Calculation (finite differences) ──────────────────────────
[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct HectonNormalJob : IJobParallelFor
{
    public int resX, resZ;
    [ReadOnly]  public NativeArray<Vector3> vertices;
    [WriteOnly] public NativeArray<Vector3> normals;

    public void Execute(int idx)
    {
        int lx = idx % resX;
        int lz = idx / resX;

        float3 L = vertices[lz * resX + math.max(lx - 1, 0)];
        float3 R = vertices[lz * resX + math.min(lx + 1, resX - 1)];
        float3 D = vertices[math.max(lz - 1, 0) * resX + lx];
        float3 U = vertices[math.min(lz + 1, resZ - 1) * resX + lx];

        float3 n = math.normalizesafe(math.cross(U - D, R - L), new float3(0, 1, 0));
        normals[idx] = n;
    }
}

// ── JOB 3: Vertex Colors ───────────────────────────────────────────────────
// R = Slope | G = Depth | B = Cave Edge | A = Biome
[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct HectonColorJob : IJobParallelFor
{
    public float maxDepth, caveThresh, caveEdge;

    [ReadOnly] public NativeArray<Vector3> verts;
    [ReadOnly] public NativeArray<Vector3> norms;
    [ReadOnly] public NativeArray<float>   cave;
    [ReadOnly] public NativeArray<byte>    isCave;
    [ReadOnly] public NativeArray<float>   biome;

    [WriteOnly] public NativeArray<Color> colors;

    public void Execute(int idx)
    {
        float3 p = verts[idx];
        float3 n = norms[idx];

        float slope = 1f - math.abs(math.dot(n, new float3(0, 1, 0)));
        float depth = math.saturate(-p.y / maxDepth);

        float cd = (cave[idx] - caveThresh) / math.max(caveEdge, 0.001f);
        float ce = math.saturate(1f - math.abs(cd));
        if (isCave[idx] > 0) ce = math.max(ce, 0.5f);

        colors[idx] = new Color(slope, depth, ce, biome[idx]);
    }
}

#endregion

// ════════════════════════════════════════════════════════════════════════════════
//  REGION: MAIN GENERATOR
// ════════════════════════════════════════════════════════════════════════════════
#region HectonWorldGenerator

[ExecuteAlways]
public class HectonWorldGenerator : MonoBehaviour
{
    // ╔═══════════════════════════════════════════════╗
    // ║             INSPECTOR SETTINGS                ║
    // ╚═══════════════════════════════════════════════╝

    #region Inspector Settings

    [Header("═══ SPINE (Central Ridge) ═══")]
    public SpineSettings spine = new SpineSettings();

    [Header("═══ SLOPES (West / East) ═══")]
    public SlopeSettings slopes = new SlopeSettings();

    [Header("═══ BIOMES (Surface Detail) ═══")]
    public BiomeSettings biomes = new BiomeSettings();

    [Header("═══ DISPLACEMENT (Overhangs) ═══")]
    public DisplacementSettings displacement = new DisplacementSettings();

    [Header("═══ CAVES ═══")]
    public CaveSettings caves = new CaveSettings();

    [Header("═══ STREAMING ═══")]
    [Tooltip("Transform to track (player camera or sub).")]
    public Transform viewer;

    [Tooltip("Chunk world size (m). Power of 2 recommended.")]
    public float chunkSize = 256f;

    [Tooltip("Vertex spacing for LOD0 (m). 2–4 = detailed.")]
    [Range(1f, 8f)]
    public float lod0Spacing = 2f;

    [Tooltip("Vertex spacing for LOD1 (m). 12–24 = low poly.")]
    [Range(8f, 32f)]
    public float lod1Spacing = 16f;

    [Tooltip("LOD0 radius around viewer (m).")]
    public float activeRadius = 500f;

    [Tooltip("LOD1 radius around viewer (m).")]
    public float distantRadius = 2000f;

    [Tooltip("Max chunks generated per frame.")]
    [Range(1, 8)]
    public int maxChunksPerFrame = 2;

    [Header("═══ RENDERING ═══")]
    public Material terrainMaterial;
    public bool generateColliders = true;
    [Header("═══ VOXEL ENGINE ═══")]
    [Tooltip("Reference to the voxel engine for cave/rift generation.")]
    public HectonVoxelEngine voxelEngine;
    [Header("═══ MAP ═══")]
    [Tooltip("Total map side length (m). 15000 = 15 km.")]
    public float mapSize = 15000f;

    #endregion

    // ╔═══════════════════════════════════════════════╗
    // ║             INTERNAL STATE                    ║
    // ╚═══════════════════════════════════════════════╝

    #region Internal State

    // Chunks
    readonly Dictionary<int2, HectonChunkData> _active = new Dictionary<int2, HectonChunkData>();
    readonly List<HectonChunkRequest> _queue = new List<HectonChunkRequest>();
    int _queueHead;

    // Physics batch
    readonly List<Mesh>       _bakeMeshes  = new List<Mesh>();
    readonly List<GameObject> _bakeObjects = new List<GameObject>();
    int _bakeHead;                          // индекс текущей позиции в очереди бейка
    const int MAX_BAKES_PER_FRAME = 2;      // макс. мешей за кадр (2–3 безопасно)

    // ▼▼▼ POI — ДОЛЖНЫ БЫТЬ ИМЕННО ЗДЕСЬ, рядом с _active ▼▼▼
    readonly Dictionary<int2, List<Vector3>> _poiBases     = new Dictionary<int2, List<Vector3>>();
    readonly Dictionary<int2, List<Vector3>> _poiResources = new Dictionary<int2, List<Vector3>>();
    // ▲▲▲ ────────────────────────────────────────────────── ▲▲▲

    // LUTs
    NativeArray<float> _westLUT, _eastLUT, _biomeLUT;
    bool _lutsReady;

    // Streaming state
    int2 _lastChunk = new int2(int.MinValue, int.MinValue);
    bool _streaming;

    // Preview
    [HideInInspector] public GameObject previewObj;

    // Constants
    const int LUT_RES   = 1024;
    const int JOB_BATCH = 64;

    #endregion

    // ╔═══════════════════════════════════════════════╗
    // ║              LIFECYCLE                        ║
    // ╚═══════════════════════════════════════════════╝

    #region Lifecycle

    void OnEnable()
    {
        if (Application.isPlaying) StartStreaming();
    }

    void OnDisable()
    {
        StopStreaming();
    }

    void Update()
    {
        if (!_streaming || viewer == null) return;

        int2 cur = WorldToChunk(viewer.position);
        if (!cur.Equals(_lastChunk))
        {
            _lastChunk = cur;
            RefreshChunks();
        }

        ProcessQueue();

        // Покадровый бейк физики — не блокирует, обрабатывает MAX_BAKES_PER_FRAME за кадр
        if (_bakeHead < _bakeMeshes.Count)
            BakePhysicsBatch();
    }

    void StartStreaming()
    {
        if (viewer == null) { Debug.LogWarning("[Hecton] No viewer assigned."); return; }
        EnsureLUTs();
        _streaming  = true;
        _lastChunk  = new int2(int.MinValue, int.MinValue); // force first refresh
    }

    void StopStreaming()
    {
        _streaming = false;
        _queue.Clear();
        _queueHead = 0;

        foreach (var kvp in _active) DestroyChunk(kvp.Value);
        _active.Clear();

        _bakeMeshes.Clear(); _bakeObjects.Clear(); _bakeHead = 0;
        _poiBases.Clear();
        _poiResources.Clear();

        DisposeLUTs();
    }

    #endregion

    // ╔═══════════════════════════════════════════════╗
    // ║             LUT MANAGEMENT                    ║
    // ╚═══════════════════════════════════════════════╝

    #region LUT Management

    void EnsureLUTs()
    {
        if (_lutsReady) return;
        _westLUT  = BakeLUT(slopes.westCurve);
        _eastLUT  = BakeLUT(slopes.eastCurve);
        _biomeLUT = BakeLUT(biomes.biomeRemapCurve);
        _lutsReady = true;
    }

    void DisposeLUTs()
    {
        if (!_lutsReady) return;
        if (_westLUT.IsCreated)  _westLUT.Dispose();
        if (_eastLUT.IsCreated)  _eastLUT.Dispose();
        if (_biomeLUT.IsCreated) _biomeLUT.Dispose();
        _lutsReady = false;
    }

    NativeArray<float> BakeLUT(AnimationCurve curve)
    {
        var lut = new NativeArray<float>(LUT_RES, Allocator.Persistent);
        for (int i = 0; i < LUT_RES; i++)
        {
            float t = (float)i / (LUT_RES - 1);
            lut[i] = (curve != null && curve.length > 0) ? curve.Evaluate(t) : (1f - t);
        }
        return lut;
    }

    /// <summary>Rebake LUTs after curve edits. Call from Editor or at runtime.</summary>
    public void RefreshLUTs()
    {
        DisposeLUTs();
        EnsureLUTs();
    }

    #endregion

    // ╔═══════════════════════════════════════════════╗
    // ║             CHUNK STREAMING                   ║
    // ╚═══════════════════════════════════════════════╝

    #region Streaming

    int2 WorldToChunk(Vector3 p) =>
        new int2(Mathf.FloorToInt(p.x / chunkSize), Mathf.FloorToInt(p.z / chunkSize));

    float2 ChunkOrigin(int2 c) => new float2(c.x * chunkSize, c.y * chunkSize);

    void RefreshChunks()
    {
        float3 vp = viewer.position;
        float2 vxz = new float2(vp.x, vp.z);
        float halfChunk = chunkSize * 0.5f;

        // Build desired set
        var desired = new Dictionary<int2, int>(); // coord -> lod
        int rMax = Mathf.CeilToInt(distantRadius / chunkSize) + 1;

        for (int dz = -rMax; dz <= rMax; dz++)
        for (int dx = -rMax; dx <= rMax; dx++)
        {
            int2 c = _lastChunk + new int2(dx, dz);
            float2 center = new float2((c.x + 0.5f) * chunkSize, (c.y + 0.5f) * chunkSize);
            float dSq = math.distancesq(vxz, center);

            if (dSq <= distantRadius * distantRadius)
                desired[c] = dSq <= activeRadius * activeRadius ? 0 : 1;
        }

        // Remove outdated
        var remove = new List<int2>();
        foreach (var kvp in _active)
        {
            if (!desired.TryGetValue(kvp.Key, out int wantLod) || wantLod != kvp.Value.lod)
                remove.Add(kvp.Key);
        }
        foreach (var c in remove) { DestroyChunk(_active[c]); _active.Remove(c); }

        // Queue new chunks sorted by distance
        var requests = new List<HectonChunkRequest>();
        foreach (var kvp in desired)
        {
            if (_active.ContainsKey(kvp.Key)) continue;
            float2 center = new float2((kvp.Key.x + 0.5f) * chunkSize, (kvp.Key.y + 0.5f) * chunkSize);
            requests.Add(new HectonChunkRequest
            {
                coord  = kvp.Key,
                lod    = kvp.Value,
                distSq = math.distancesq(vxz, center)
            });
        }
        requests.Sort((a, b) => a.distSq.CompareTo(b.distSq));

        _queue.Clear();
        _queue.AddRange(requests);
        _queueHead = 0;
    }

    void ProcessQueue()
    {
        int done = 0;
        while (_queueHead < _queue.Count && done < maxChunksPerFrame)
        {
            var req = _queue[_queueHead++];

            // Block generation if chunk with same coord+LOD already exists
            if (_active.TryGetValue(req.coord, out var existing))
            {
                if (existing.lod == req.lod) continue;   // exact match — skip
                DestroyChunk(existing);                   // LOD mismatch — rebuild
                _active.Remove(req.coord);
            }

            var cd = GenerateChunk(req.coord, req.lod);
            if (cd != null) _active[req.coord] = cd;
            done++;
        }
    }

    #endregion

    // ╔═══════════════════════════════════════════════╗
    // ║            CHUNK GENERATION                   ║
    // ╚═══════════════════════════════════════════════╝

    #region Chunk Generation

    HectonChunkData GenerateChunk(int2 coord, int lod)
    {
        float sp  = lod == 0 ? lod0Spacing : lod1Spacing;
        int   res = Mathf.CeilToInt(chunkSize / sp) + 1;
        int   vc  = res * res;
        float2 org = ChunkOrigin(coord);

        // Alloc
        var verts  = new NativeArray<Vector3>(vc, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var norms  = new NativeArray<Vector3>(vc, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var uvs    = new NativeArray<Vector2>(vc, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var cols   = new NativeArray<Color>(vc,   Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var caveV  = new NativeArray<float>(vc,   Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var caveB  = new NativeArray<byte>(vc,    Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var biomeV = new NativeArray<float>(vc,   Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        try
        {
            // ── Job 1: Terrain Vertices ──
            MakeVertexJob(res, res, org.x, org.y, sp, lod,
                          verts, uvs, caveV, caveB, biomeV)
                .Schedule(vc, JOB_BATCH).Complete();

            // ── Job 2: Normals ──
            new HectonNormalJob { resX = res, resZ = res, vertices = verts, normals = norms }
                .Schedule(vc, JOB_BATCH).Complete();

            // ── Job 3: Vertex Colors ──
            new HectonColorJob
            {
                maxDepth   = slopes.maxDepth,
                caveThresh = caves.threshold,
                caveEdge   = caves.edgeWidth,
                verts  = verts, norms = norms,
                cave   = caveV, isCave = caveB, biome = biomeV,
                colors = cols
            }.Schedule(vc, JOB_BATCH).Complete();

            // ── Build Triangles ──
            int maxTri = (res - 1) * (res - 1) * 6;
            var tris = new int[maxTri];
            int tc = 0;
            bool cutCaves = lod == 0;

            for (int z = 0; z < res - 1; z++)
            for (int x = 0; x < res - 1; x++)
            {
                int i00 = z * res + x;
                int i10 = i00 + 1;
                int i01 = i00 + res;
                int i11 = i01 + 1;

                if (!cutCaves || !(caveB[i00] > 0 && 
              
                    caveB[i01] > 0 && caveB[i10] > 0))
                {
                    tris[tc++] = i00;
                    tris[tc++] = i01;
                    tris[tc++] = i10;
                }

                if (!cutCaves || !(caveB[i10] > 0 && caveB[i01] > 0 && caveB[i11] > 0))
                {
                    tris[tc++] = i10;
                    tris[tc++] = i01;
                    tris[tc++] = i11;
                }
            }

            if (tc == 0) return null;

            if (tc < maxTri)
            {
                var trimmed = new int[tc];
                System.Array.Copy(tris, trimmed, tc);
                tris = trimmed;
            }

            // ── Build Mesh ──────────────────────────────
            var mesh = new Mesh();
            mesh.name = $"Hecton_{coord.x}_{coord.y}_L{lod}";
            if (vc > 65535) mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(cols);
            mesh.triangles = tris;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            // ── Create GameObject ────────────────────────
            var go = new GameObject(mesh.name);
            go.transform.SetParent(transform, false);
            go.isStatic = true;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial    = terrainMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows    = true;

            // ── Queue physics bake (LOD0 only) ──────────
            if (lod == 0 && generateColliders)
            {
                _bakeMeshes.Add(mesh);
                _bakeObjects.Add(go);
            }

            // ── Extract Points of Interest (LOD0 only) ──
            if (lod == 0)
            {
                ExtractPOI(coord, verts, norms, caveB);

                // ── Spawn voxel volumes for caves and rifts ──
                SpawnVoxelPOIs(coord, verts, caveB, res, res, sp);
            }

            return new HectonChunkData
            {
                coord = coord,
                lod   = lod,
                go    = go,
                mesh  = mesh
            };
        }
        finally
        {
            if (verts.IsCreated)  verts.Dispose();
            if (norms.IsCreated)  norms.Dispose();
            if (uvs.IsCreated)    uvs.Dispose();
            if (cols.IsCreated)   cols.Dispose();
            if (caveV.IsCreated)  caveV.Dispose();
            if (caveB.IsCreated)  caveB.Dispose();
            if (biomeV.IsCreated) biomeV.Dispose();
        }
    }

    /// <summary>Creates a fully-configured HectonVertexJob from current settings.</summary>
    HectonVertexJob MakeVertexJob(int resX, int resZ,
                                   float orgX, float orgZ,
                                   float spacing, int lod,
                                   NativeArray<Vector3> outVerts,
                                   NativeArray<Vector2> outUVs,
                                   NativeArray<float>   outCave,
                                   NativeArray<byte>    outIsCave,
                                   NativeArray<float>   outBiome)
    {
        return new HectonVertexJob
        {
            resX     = resX,
            resZ     = resZ,
            originX  = orgX,
            originZ  = orgZ,
            spacing  = spacing,
            lodLevel = lod,

            mapHalfSize = mapSize * 0.5f,

            spineMaxHeight    = spine.maxHeight,
            spineWidth        = spine.width,
            spineWarpStrength = spine.warpStrength,
            islandThreshold   = spine.islandThreshold,
            warpNoise         = NoiseData.From(spine.warpNoise),
            islandNoise       = NoiseData.From(spine.islandNoise),

            westLen         = slopes.westLength,
            eastLen         = slopes.eastLength,
            maxDepth        = slopes.maxDepth,
            terraceCount    = slopes.terraceCount,
            terraceStrength = slopes.terraceStrength,

            flatAmp        = biomes.flatSurfaceAmplitude,
            aggrAmp        = biomes.aggressiveSurfaceAmplitude,
            flatDispFactor = biomes.flatDisplacementFactor,
            biomeNoise     = NoiseData.From(biomes.biomeNoise),
            flatSurfNoise  = NoiseData.From(biomes.flatSurfaceNoise),
            aggrSurfNoise  = NoiseData.From(biomes.aggressiveSurfaceNoise),

            dispScale  = new float3(displacement.scale.x,
                                    displacement.scale.y,
                                    displacement.scale.z),
            slopeDispW = displacement.slopeWeight,
            dispNoise  = NoiseData.From(displacement.noise),

            caveThresh = caves.threshold,
            caveMinY   = caves.minDepth,
            caveNoise  = NoiseData.From(caves.noise),

            westLUT  = _westLUT,
            eastLUT  = _eastLUT,
            biomeLUT = _biomeLUT,

            outVerts  = outVerts,
            outUVs    = outUVs,
            outCave   = outCave,
            outIsCave = outIsCave,
            outBiome  = outBiome
        };
    }
    

    /// <summary>
    /// Scans cave vertices in a freshly generated LOD0 chunk.
    /// Clusters them into POI centers and spawns voxel volumes.
    /// Also checks for tectonic rift conditions at the chunk center.
    ///
    /// Fixes applied:
    /// - Ground Snapping: uses GetWorldHeight() for true terrain floor
    /// - Deep Embed: Y = floorHeight - 15f to hide top edge seal artifact
    /// - Noise Filtering: discards clusters with fewer than 20 vertices
    /// - Strict Limits: spawns at most MaxVoxelsPerChunk (3) cave volumes
    /// - Cluster Sorting: largest clusters first
    /// </summary>
    void SpawnVoxelPOIs(int2 coord,
                        NativeArray<Vector3> verts,
                        NativeArray<byte> caveB,
                        int resX, int resZ,
                        float spacing)
    {
        const int MaxVoxelsPerChunk = 3;
        const int MinClusterVertices = 20;

        if (voxelEngine == null) return;

        float2 chunkOrg = ChunkOrigin(coord);
        float chunkCenterX = chunkOrg.x + chunkSize * 0.5f;
        float chunkCenterZ = chunkOrg.y + chunkSize * 0.5f;

        // ════════════════════════════════════════
        //  CAVE POI: cluster cave vertices → spawn sphere volumes
        // ════════════════════════════════════════

        // Collect all cave vertex positions
        var cavePositions = new List<Vector3>(64);
        for (int i = 0; i < verts.Length; i++)
        {
            if (caveB[i] > 0)
                cavePositions.Add(verts[i]);
        }

        if (cavePositions.Count > 0)
        {
            // Simple grid-based clustering: divide chunk into cells,
            // each cell with cave vertices becomes one POI
            float clusterSize = 24f; // meters — one POI per 24m cell
            var clusters = new Dictionary<int2, List<Vector3>>();

            for (int i = 0; i < cavePositions.Count; i++)
            {
                Vector3 p = cavePositions[i];
                int2 cell = new int2(
                    Mathf.FloorToInt((p.x - chunkOrg.x) / clusterSize),
                    Mathf.FloorToInt((p.z - chunkOrg.y) / clusterSize)
                );

                if (!clusters.TryGetValue(cell, out var list))
                {
                    list = new List<Vector3>(16);
                    clusters[cell] = list;
                }
                list.Add(p);
            }

            // Build a flat list so we can sort by vertex count (descending)
            var clusterList = new List<KeyValuePair<int2, List<Vector3>>>(clusters.Count);
            foreach (var kvp in clusters)
            {
                // Noise filtering: discard clusters below minimum vertex count
                if (kvp.Value.Count < MinClusterVertices)
                    continue;

                clusterList.Add(kvp);
            }

            // Sort largest clusters first
            clusterList.Sort((a, b) => b.Value.Count.CompareTo(a.Value.Count));

            // Spawn at most MaxVoxelsPerChunk cave volumes
            int spawned = 0;
            for (int ci = 0; ci < clusterList.Count && spawned < MaxVoxelsPerChunk; ci++)
            {
                var cluster = clusterList[ci];

                // Compute cluster centroid (XZ only from mesh vertices)
                float sumX = 0f, sumZ = 0f;
                for (int i = 0; i < cluster.Value.Count; i++)
                {
                    sumX += cluster.Value[i].x;
                    sumZ += cluster.Value[i].z;
                }
                float cx = sumX / cluster.Value.Count;
                float cz = sumZ / cluster.Value.Count;

                // Ground Snapping: query true terrain floor, embed 15m deep
                // to fully hide the top edge seal artifact
                float floorHeight = GetWorldHeight(cx, cz);
                Vector3 spawnPos = new Vector3(cx, floorHeight - 15f, cz);

                // Create POI GameObject
                var poiGO = new GameObject($"Voxel_Cave_{coord.x}_{coord.y}_{cluster.Key.x}_{cluster.Key.y}");
                poiGO.transform.SetParent(transform, false);
                poiGO.transform.position = spawnPos;
                poiGO.isStatic = true;

                voxelEngine.GenerateVolume(poiGO, spawnPos, VoxelPOIType.Cave);
                spawned++;
            }
        }

        // ════════════════════════════════════════
        //  RIFT POI: noise-based check at chunk center
        // ════════════════════════════════════════

        // Use a dedicated low-frequency noise to determine rift locations.
        // Rifts are rare — only spawn when noise exceeds high threshold.
        NoiseData riftND = new NoiseData
        {
            scale = 0.00008f, octaves = 2, lacunarity = 2f,
            persistence = 0.5f, offset = float3.zero, seed = 9999
        };

        float riftNoise = HectonNoise.Fractal2D(chunkCenterX, chunkCenterZ, riftND);
        float riftThreshold = 0.82f; // ~top 18% of noise range

        if (riftNoise > riftThreshold)
        {
            float riftY = GetWorldHeight(chunkCenterX, chunkCenterZ);
            Vector3 riftCenter = new Vector3(chunkCenterX, riftY - 20f, chunkCenterZ);

            var riftGO = new GameObject($"Voxel_Rift_{coord.x}_{coord.y}");
            riftGO.transform.SetParent(transform, false);
            riftGO.transform.position = riftCenter;
            riftGO.isStatic = true;

            voxelEngine.GenerateVolume(riftGO, riftCenter, VoxelPOIType.DeepRift);
        }
    }

    /// <summary>
    /// Scans generated vertex data and records gameplay-relevant locations.
    /// Called once per LOD0 chunk after all jobs complete.
    /// NOTE: POI are not removed when chunks are destroyed — gameplay systems
    /// should perform spatial validation before using these positions.
    /// </summary>
    void ExtractPOI(int2 coord,
                    NativeArray<Vector3> verts,
                    NativeArray<Vector3> norms,
                    NativeArray<byte>    caveB)
    {
        int step = Mathf.Max(1, verts.Length / 256);

        List<Vector3> bases = null;
        List<Vector3> res   = null;

        for (int i = 0; i < verts.Length; i += step)
        {
            Vector3 p = verts[i];
            Vector3 n = norms[i];
            float upDot = Vector3.Dot(n, Vector3.up);

            if (upDot > 0.95f && p.y > -500f && p.y < 0f)
            {
                if (bases == null) bases = new List<Vector3>();
                bases.Add(p);
            }

            if (caveB[i] > 0)
            {
                if (res == null) res = new List<Vector3>();
                res.Add(p);
            }
        }

        // Store only if any points found; remove stale data first
        _poiBases.Remove(coord);
        _poiResources.Remove(coord);

        if (bases != null) _poiBases[coord]     = bases;
        if (res != null)   _poiResources[coord] = res;
    }

    #endregion // Chunk Generation

    // ╔═══════════════════════════════════════════════╗
    // ║       PARALLEL PHYSICS BAKING                 ║
    // ╚═══════════════════════════════════════════════╝

    #region Physics

    /// <summary>
    /// Bakes all queued MeshColliders in parallel via System.Threading.Tasks.Parallel.
    /// Physics.BakeMesh is thread-safe since Unity 2019.3.
    /// Collider assignment happens on the main thread (instant — data is pre-baked).
    /// </summary>
    /// <summary>
    /// Bakes up to MAX_BAKES_PER_FRAME meshes per frame on the main thread.
    /// After baking each mesh, immediately assigns MeshCollider.
    /// Skips null meshes/objects. Clears the queue when fully processed.
    /// </summary>
    void BakePhysicsBatch()
    {
        int baked = 0;

        while (_bakeHead < _bakeMeshes.Count && baked < MAX_BAKES_PER_FRAME)
        {
            Mesh mesh       = _bakeMeshes[_bakeHead];
            GameObject go   = _bakeObjects[_bakeHead];
            _bakeHead++;

            // Skip destroyed or null entries
            if (mesh == null || go == null) continue;

            #pragma warning disable CS0618
            Physics.BakeMesh(mesh.GetInstanceID(), false);
            #pragma warning restore CS0618

            // Assign collider immediately (mesh data is already baked, no re-cook)
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;

            baked++;
        }

        // Queue fully processed — reset
        if (_bakeHead >= _bakeMeshes.Count)
        {
            _bakeMeshes.Clear();
            _bakeObjects.Clear();
            _bakeHead = 0;
        }
    }

    #endregion

    // ╔═══════════════════════════════════════════════╗
    // ║           CHUNK LIFECYCLE                     ║
    // ╚═══════════════════════════════════════════════╝

    #region Chunk Lifecycle
 
    void DestroyChunk(HectonChunkData cd)
    {
        if (cd == null) return;

        // ── Destroy child voxel volumes ──
        if (cd.go != null)
        {
            // Voxel POI GameObjects are children of the main generator transform
            // with names starting with "Voxel_" and containing the chunk coord.
            // We destroy them when their parent chunk is unloaded.
            string prefix = $"Voxel_Cave_{cd.coord.x}_{cd.coord.y}";
            string riftPrefix = $"Voxel_Rift_{cd.coord.x}_{cd.coord.y}";

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith(prefix) || child.name.StartsWith(riftPrefix))
                {
                    var mf = child.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        mf.sharedMesh.Clear();
                        SafeDestroy(mf.sharedMesh);
                    }
                    SafeDestroy(child.gameObject);
                }
            }
        }

        // ── Original cleanup (keep as-is) ──
        _poiBases.Remove(cd.coord);
        _poiResources.Remove(cd.coord);

        if (cd.mesh != null)
        {
            cd.mesh.Clear();
            SafeDestroy(cd.mesh);
        }

        if (cd.go != null) SafeDestroy(cd.go);
    }

    void ClearPreview()
    {
        if (previewObj == null) return;
        var mf = previewObj.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
            SafeDestroy(mf.sharedMesh);
        SafeDestroy(previewObj);
        previewObj = null;
    }

    void SafeDestroy(Object obj)
    {
        if (obj == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(obj);
        else
            Destroy(obj);
#else
        Destroy(obj);
#endif
    }

    #endregion

    // ╔═══════════════════════════════════════════════╗
    // ║               PUBLIC API                      ║
    // ╚═══════════════════════════════════════════════╝

    #region Public API
    /// <summary>
    /// Returns biome mask at world (x, z). 0 = flat/sand, 1 = aggressive/rock.
    /// Used by HectonVoxelEngine for seamless vertex color blending.
    /// Automatically bakes LUTs if needed.
    /// </summary>
    public float GetBiomeAt(float x, float z)
    {
        EnsureLUTs();
        NoiseData bioND = NoiseData.From(biomes.biomeNoise);
        float bRaw = HectonNoise.Fractal2D(x, z, bioND);
        return HectonNoise.SampleLUT(_biomeLUT, bRaw);
    }
    /// <summary>
    /// Query base terrain height at any world position (x, z).
    /// Returns height WITHOUT displacement or cave carving — suitable for
    /// navigation, spawn placement, and water-depth checks.
    /// Automatically bakes LUTs if needed.
    /// </summary>
    public float GetWorldHeight(float x, float z)
    {
        EnsureLUTs();

        NoiseData warpND = NoiseData.From(spine.warpNoise);
        NoiseData islND  = NoiseData.From(spine.islandNoise);
        NoiseData bioND  = NoiseData.From(biomes.biomeNoise);
        NoiseData flatND = NoiseData.From(biomes.flatSurfaceNoise);
        NoiseData aggrND = NoiseData.From(biomes.aggressiveSurfaceNoise);

        float hs = mapSize * 0.5f;

        // 1. Domain-warped spine center
        float warpVal = HectonNoise.Fractal2D(0f, z, warpND);
        float spineCX = (warpVal * 2f - 1f) * spine.warpStrength;

        // 2. Signed distance from spine
        float dx    = x - spineCX;
        float absDx = Mathf.Abs(dx);
        bool  west  = dx < 0f;

        // 3. Slope profile
        float sLen  = west ? slopes.westLength : slopes.eastLength;
        float normD = Mathf.Clamp01(absDx / Mathf.Max(sLen, 1f));
        float curveV;

        if (west) curveV = HectonNoise.SampleLUT(_westLUT, normD);
        else      curveV = HectonNoise.SampleLUT(_eastLUT, normD);

        // 4. Terraces (west only)
        if (west && slopes.terraceCount > 0 && slopes.terraceStrength > 0f)
        {
            float tc      = (float)slopes.terraceCount;
            float stepped = Mathf.Round(curveV * tc) / tc;
            curveV = Mathf.Lerp(curveV, stepped, slopes.terraceStrength);
        }

        // 5. Base floor
        float floor = Mathf.Lerp(-slopes.maxDepth, 0f, curveV);

        // 6. Spine elevation (islands)
        float spineInf = Mathf.Clamp01(1f - absDx / Mathf.Max(spine.width, 1f));
        spineInf *= spineInf;
        float islN = HectonNoise.Fractal2D(x * 0.1f, z, islND);
        float islF = Mathf.Clamp01(
            (islN - spine.islandThreshold) /
            Mathf.Max(1f - spine.islandThreshold, 0.01f));
        float spineElev = spine.maxHeight * spineInf * islF;

        // 7. Surface noise (biome blended)
        float bRaw = HectonNoise.Fractal2D(x, z, bioND);
        float bVal = HectonNoise.SampleLUT(_biomeLUT, bRaw);
        float fltN = HectonNoise.Fractal2D(x, z, flatND);
        float agrN = HectonNoise.Fractal2D(x, z, aggrND);
        float surfY = (Mathf.Lerp(fltN, agrN, bVal) * 2f - 1f) *
                       Mathf.Lerp(biomes.flatSurfaceAmplitude,
                                  biomes.aggressiveSurfaceAmplitude, bVal);

        // 8. Combine
        float y = floor + spineElev + surfY;

        // 9. Edge fade (deep ocean at map borders)
        float fadeX = Mathf.Clamp01((hs - Mathf.Abs(x)) / 1000f);
        float fadeZ = Mathf.Clamp01((hs - Mathf.Abs(z)) / 1000f);
        y = Mathf.Lerp(-slopes.maxDepth, y, fadeX * fadeZ);

        return y;
    }

    /// <summary>
    /// Generates a single low-resolution mesh covering the entire 15×15 km world.
    /// Uses LOD1 (no caves, no displacement) for a fast shape overview.
    /// Works in Editor mode.
    /// </summary>
    [ContextMenu("▶ Generate World Preview")]
    public void GenerateWorldPreview()
    {
        ClearPreview();
        EnsureLUTs();

        const float previewSpacing = 100f;
        float hs  = mapSize * 0.5f;
        int   res = Mathf.CeilToInt(mapSize / previewSpacing) + 1;
        int   vc  = res * res;

        var verts  = new NativeArray<Vector3>(vc, Allocator.TempJob,
                         NativeArrayOptions.UninitializedMemory);
        var norms  = new NativeArray<Vector3>(vc, Allocator.TempJob,
                         NativeArrayOptions.UninitializedMemory);
        var uvs    = new NativeArray<Vector2>(vc, Allocator.TempJob,
                         NativeArrayOptions.UninitializedMemory);
        var cols   = new NativeArray<Color>(vc, Allocator.TempJob,
                         NativeArrayOptions.UninitializedMemory);
        var caveV  = new NativeArray<float>(vc, Allocator.TempJob,
                         NativeArrayOptions.UninitializedMemory);
        var caveB  = new NativeArray<byte>(vc, Allocator.TempJob,
                         NativeArrayOptions.UninitializedMemory);
        var biomeV = new NativeArray<float>(vc, Allocator.TempJob,
                         NativeArrayOptions.UninitializedMemory);

        try
        {
#if UNITY_EDITOR
            EditorUtility.DisplayProgressBar("Hecton World Preview",
                $"Generating {res}×{res} vertices...", 0.2f);
#endif
            // Vertex generation (LOD1 = no caves, no displacement)
            MakeVertexJob(res, res, -hs, -hs, previewSpacing, 1,
                          verts, uvs, caveV, caveB, biomeV)
                .Schedule(vc, JOB_BATCH).Complete();

#if UNITY_EDITOR
            EditorUtility.DisplayProgressBar("Hecton World Preview",
                "Computing normals...", 0.5f);
#endif
            new HectonNormalJob
            {
                resX = res, resZ = res,
                vertices = verts, normals = norms
            }.Schedule(vc, JOB_BATCH).Complete();

#if UNITY_EDITOR
            EditorUtility.DisplayProgressBar("Hecton World Preview",
                "Computing vertex colors...", 0.75f);
#endif
            new HectonColorJob
            {
                maxDepth   = slopes.maxDepth,
                caveThresh = caves.threshold,
                caveEdge   = caves.edgeWidth,
                verts  = verts,  norms  = norms,
                cave   = caveV,  isCave = caveB,
                biome  = biomeV, colors = cols
            }.Schedule(vc, JOB_BATCH).Complete();

            // Build triangles (no cave cutting for preview)
            int maxTri = (res - 1) * (res - 1) * 6;
            var tris   = new int[maxTri];
            int tc     = 0;

            for (int z = 0; z < res - 1; z++)
            for (int x = 0; x < res - 1; x++)
            {
                int i00 = z * res + x;
                int i10 = i00 + 1;
                int i01 = i00 + res;
                int i11 = i01 + 1;

                tris[tc++] = i00; tris[tc++] = i01; tris[tc++] = i10;
                tris[tc++] = i10; tris[tc++] = i01; tris[tc++] = i11;
            }

            // Assemble mesh
            var mesh = new Mesh();
            mesh.name = "Hecton_WorldPreview";
            if (vc > 65535) mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(cols);
            mesh.triangles = tris;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            // Create preview GameObject
            previewObj = new GameObject("_WorldPreview");
            previewObj.transform.SetParent(transform, false);

            var mf = previewObj.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = previewObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial    = terrainMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows    = true;

            Debug.Log($"[Hecton] Preview: {res}×{res} = {vc:N0} verts, " +
                      $"{tc / 3:N0} tris. Bounds: {mesh.bounds.size}");
        }
        finally
        {
            if (verts.IsCreated)  verts.Dispose();
            if (norms.IsCreated)  norms.Dispose();
            if (uvs.IsCreated)    uvs.Dispose();
            if (cols.IsCreated)   cols.Dispose();
            if (caveV.IsCreated)  caveV.Dispose();
            if (caveB.IsCreated)  caveB.Dispose();
            if (biomeV.IsCreated) biomeV.Dispose();

#if UNITY_EDITOR
            EditorUtility.ClearProgressBar();
#endif
        }
    }

    /// <summary>
    /// Destroys ALL generated content: streaming chunks, preview, POI, LUTs.
    /// Safe to call multiple times.
    /// </summary>
    [ContextMenu("✕ Clear All")]
    public void ClearAll()
    {
        StopStreaming();
        ClearPreview();

        // Safety: destroy any orphaned children
        for (int i = transform.childCount - 1; i >= 0; i--)
            SafeDestroy(transform.GetChild(i).gameObject);
    }

    #endregion // Public API

    // ╔═══════════════════════════════════════════════╗
    // ║                 GIZMOS                        ║
    // ╚═══════════════════════════════════════════════╝

    #region Gizmos

    void OnDrawGizmosSelected()
    {
        float hs = mapSize * 0.5f;

        // ── Map bounds (local space) ──
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = new Color(0f, 0.5f, 1f, 0.2f);
        float totalH = slopes.maxDepth + spine.maxHeight;
        Gizmos.DrawWireCube(
            new Vector3(0f, -(slopes.maxDepth - spine.maxHeight) * 0.5f, 0f),
            new Vector3(mapSize, totalH, mapSize));

        // Sea level plane
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.1f);
        Gizmos.DrawCube(Vector3.zero, new Vector3(mapSize, 0.5f, mapSize));

        // Abyss floor
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.3f);
        Gizmos.DrawWireCube(
            new Vector3(0f, -slopes.maxDepth, 0f),
            new Vector3(mapSize, 0.5f, mapSize));

        // ── Spine centerline ──
        if (spine?.warpNoise != null)
        {
            Gizmos.color = Color.yellow;
            NoiseData wND = NoiseData.From(spine.warpNoise);
            const int steps = 80;
            for (int i = 0; i < steps; i++)
            {
                float z0 = -hs + (float)i       / steps * mapSize;
                float z1 = -hs + (float)(i + 1) / steps * mapSize;
                float x0 = (HectonNoise.Fractal2D(0f, z0, wND) * 2f - 1f)
                            * spine.warpStrength;
                float x1 = (HectonNoise.Fractal2D(0f, z1, wND) * 2f - 1f)
                            * spine.warpStrength;

                Gizmos.DrawLine(
                    new Vector3(x0, spine.maxHeight * 0.3f, z0),
                    new Vector3(x1, spine.maxHeight * 0.3f, z1));
            }

            // Spine width indicators at a few Z slices
            Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
            for (int s = 0; s <= 4; s++)
            {
                float zs = -hs + s * 0.25f * mapSize;
                float xs = (HectonNoise.Fractal2D(0f, zs, wND) * 2f - 1f)
                            * spine.warpStrength;

                Gizmos.DrawLine(
                    new Vector3(xs - spine.width, 0f, zs),
                    new Vector3(xs + spine.width, 0f, zs));
            }
        }

        // ── Viewer radii (world space) ──
        Gizmos.matrix = Matrix4x4.identity;
        if (viewer != null)
        {
            Vector3 vp = viewer.position;
            vp.y = 0f;

            Gizmos.color = Color.green;
            DrawCircleGizmo(vp, activeRadius);

            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            DrawCircleGizmo(vp, distantRadius);
        }
    }

    static void DrawCircleGizmo(Vector3 center, float radius, int segs = 48)
    {
        float step = Mathf.PI * 2f / segs;
        for (int i = 0; i < segs; i++)
        {
            float a0 = i * step;
            float a1 = (i + 1) * step;
            Gizmos.DrawLine(
                center + new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius),
                center + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius));
        }
    }

    #endregion // Gizmos

    // ╔═══════════════════════════════════════════════╗
    // ║              UTILITIES                        ║
    // ╚═══════════════════════════════════════════════╝

    #region Utilities

    /// <summary>Returns the count of currently loaded chunks.</summary>
    public int ActiveChunkCount => _active.Count;

    /// <summary>Total base locations across all loaded chunks.</summary>
    public int BaseLocationCount
    {
        get { int n = 0; foreach (var kv in _poiBases) n += kv.Value.Count; return n; }
    }

    /// <summary>Total resource nodes across all loaded chunks.</summary>
    public int ResourceNodeCount
    {
        get { int n = 0; foreach (var kv in _poiResources) n += kv.Value.Count; return n; }
    }

    /// <summary>
    /// Copies all active base locations into the provided list. 
    /// Clears the list first. Allocation-free if list has enough capacity.
    /// </summary>
    public void GetAllBaseLocations(List<Vector3> result)
    {
        result.Clear();
        foreach (var kv in _poiBases)
            result.AddRange(kv.Value);
    }

    /// <summary>
    /// Copies all active resource nodes into the provided list.
    /// </summary>
    public void GetAllResourceNodes(List<Vector3> result)
    {
        result.Clear();
        foreach (var kv in _poiResources)
            result.AddRange(kv.Value);
    }

    /// <summary>Sums vertex count across all child meshes.</summary>
    public long CountTotalVertices()
    {
        long total = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            var mf = transform.GetChild(i).GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                total += mf.sharedMesh.vertexCount;
        }
        return total;
    }

    /// <summary>Sums triangle count across all child meshes.</summary>
    public long CountTotalTriangles()
    {
        long total = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            var mf = transform.GetChild(i).GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                total += mf.sharedMesh.triangles.Length / 3;
        }
        return total;
    }

    #endregion

    // ╔═══════════════════════════════════════════════╗
    // ║              CLEANUP                          ║
    // ╚═══════════════════════════════════════════════╝

    void OnDestroy()
    {
        ClearAll();
    }
}

#endregion // HectonWorldGenerator

// ════════════════════════════════════════════════════════════════════════════════
//  CUSTOM EDITOR
// ════════════════════════════════════════════════════════════════════════════════
#if UNITY_EDITOR

[CustomEditor(typeof(HectonWorldGenerator))]
public class HectonWorldGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        HectonWorldGenerator gen = (HectonWorldGenerator)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // ════════════════════════════════════
        //  ESTIMATED LOAD
        // ════════════════════════════════════
        int lod0Res = Mathf.CeilToInt(gen.chunkSize / gen.lod0Spacing) + 1;
        int lod1Res = Mathf.CeilToInt(gen.chunkSize / gen.lod1Spacing) + 1;
        int r0 = Mathf.CeilToInt(gen.activeRadius / gen.chunkSize);
        int r1 = Mathf.CeilToInt(gen.distantRadius / gen.chunkSize);
        int estL0 = (2 * r0 + 1) * (2 * r0 + 1);
        int estL1 = Mathf.Max(0, (2 * r1 + 1) * (2 * r1 + 1) - estL0);
        long estVerts = (long)estL0 * lod0Res * lod0Res
                      + (long)estL1 * lod1Res * lod1Res;
        float estVRAM = estVerts * 64f / (1024f * 1024f);

        EditorGUILayout.HelpBox(
            $"═══ ESTIMATED STREAMING LOAD ═══\n" +
            $"Chunk: {gen.chunkSize} m\n" +
            $"LOD0: {lod0Res}×{lod0Res} verts/chunk  (~{estL0} chunks)\n" +
            $"LOD1: {lod1Res}×{lod1Res} verts/chunk  (~{estL1} chunks)\n" +
            $"Peak vertices: ~{estVerts:N0}\n" +
            $"Est. VRAM: ~{estVRAM:F1} MB\n" +
            $"Abyss: -{gen.slopes.maxDepth} m  |  Spine: +{gen.spine.maxHeight} m",
            MessageType.Info);

        if (estVRAM > 150f)
        {
            EditorGUILayout.HelpBox(
                $"⚠ Est. VRAM ({estVRAM:F0} MB) high for 2 GB GPU.\n" +
                "Increase LOD spacing or reduce streaming radii.",
                MessageType.Warning);
        }

        EditorGUILayout.Space(5);

        // ════════════════════════════════════
        //  BUTTONS
        // ════════════════════════════════════
        GUI.backgroundColor = new Color(0.4f, 0.85f, 1f);
        if (GUILayout.Button("▶  Generate World Preview  (15 km Low-Res)",
                             GUILayout.Height(36)))
        {
            Undo.RegisterFullObjectHierarchyUndo(gen.gameObject, "Hecton Preview");
            gen.GenerateWorldPreview();
        }

        GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
        if (GUILayout.Button("🔄  Refresh LUTs  (after curve edits)",
                             GUILayout.Height(24)))
        {
            gen.RefreshLUTs();
            Debug.Log("[Hecton] LUTs rebaked from current curves.");
        }

        GUI.backgroundColor = new Color(1f, 0.5f, 0.4f);
        if (GUILayout.Button("✕  Clear All", GUILayout.Height(28)))
        {
            Undo.RegisterFullObjectHierarchyUndo(gen.gameObject, "Hecton Clear");
            gen.ClearAll();
        }

        GUI.backgroundColor = Color.white;

        // ════════════════════════════════════
        //  RUNTIME / SCENE STATISTICS
        // ════════════════════════════════════
        bool hasContent = gen.transform.childCount > 0;
        if (hasContent)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            long realVerts = gen.CountTotalVertices();
            long realTris  = gen.CountTotalTriangles();
            float realVRAM = (realVerts * 64f + realTris * 12f) / (1024f * 1024f);

            string mode = Application.isPlaying ? "RUNTIME" : "SCENE";

            EditorGUILayout.HelpBox(
                $"═══ {mode} STATS ═══\n" +
                $"Child objects: {gen.transform.childCount}\n" +
                (Application.isPlaying ?
                $"Active chunks: {gen.ActiveChunkCount}\n" : "") +
                $"Vertices: {realVerts:N0}\n" +
                $"Triangles: {realTris:N0}\n" +
                $"Est. VRAM: {realVRAM:F1} MB\n" +
                $"Base locations: {gen.BaseLocationCount}\n" +
                $"Resource nodes: {gen.ResourceNodeCount}",
                MessageType.None);

            if (realVRAM > 200f)
            {
                EditorGUILayout.HelpBox(
                    $"⚠ Active VRAM usage ({realVRAM:F0} MB) is high!",
                    MessageType.Warning);
            }
        }

        EditorGUILayout.Space(3);
        EditorGUILayout.HelpBox(
            "Project HECTON-8 World Engine v1.0\n" +
            "Burst + Jobs + Chunk Streaming + LOD + Parallel PhysX\n" +
            "Required: com.unity.burst, com.unity.mathematics, com.unity.collections",
            MessageType.None);
    }
}

#endif