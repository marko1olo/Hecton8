// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  HectonWorldGenerator.cs — Project HECTON-8 World Engine                   ║
// ║  Unity 6 (URP) | Burst + Jobs | Async Chunk Streaming | LOD System         ║
// ║  v2.1 — Voxel volume lifecycle fix (DestroyChunk → DespawnVolume)          ║
// ║                                                                             ║
// ║  CHANGES v2.1:                                                              ║
// ║  ─────────────                                                              ║
// ║  [FIX] DestroyChunk: voxel child objects (Voxel_Cave_*, Voxel_Rift_*)      ║
// ║        now destroyed via voxelEngine.DespawnVolume() instead of direct      ║
// ║        SafeDestroy(). This ensures HectonVoxelEngine._activeVolumes is      ║
// ║        properly cleaned up, preventing null reference accumulation.         ║
// ║  [FIX] Fallback: if voxelEngine is null at destruction time, falls back     ║
// ║        to manual mesh cleanup + SafeDestroy (same as v2.0 behavior).       ║
// ║                                                                             ║
// ║  PREVIOUS (v2.0):                                                           ║
// ║  ─────────────                                                              ║
// ║  Fully Asynchronous Job Pipeline (Zero Main-Thread Blocks)                  ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
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

[System.Serializable]
public class SpineSettings
{
    [Tooltip("Max island height above sea level (Y=0).")]
    public float maxHeight = 400f;

    [Tooltip("Half-width of spine influence zone (m).")]
    public float width = 600f;

    [Tooltip("Max lateral displacement of spine centerline by domain warp (m).")]
    public float warpStrength = 1500f;

    public HectonNoiseLayer warpNoise = new HectonNoiseLayer
        { scale = 0.0003f, octaves = 3, lacunarity = 2f, persistence = 0.5f, seed = 111 };

    public HectonNoiseLayer islandNoise = new HectonNoiseLayer
        { scale = 0.0004f, octaves = 2, lacunarity = 2f, persistence = 0.5f, seed = 222 };

    [Range(0.2f, 0.8f)]
    public float islandThreshold = 0.45f;
}

[System.Serializable]
public class SlopeSettings
{
    public float westLength = 6000f;
    public float eastLength = 1500f;

    public AnimationCurve westCurve = new AnimationCurve(
        new Keyframe(0.00f, 1.00f,  0.0f,  0.0f),
        new Keyframe(0.15f, 0.92f, -0.3f, -0.3f),
        new Keyframe(0.40f, 0.65f, -0.7f, -0.7f),
        new Keyframe(0.70f, 0.20f, -0.6f, -0.6f),
        new Keyframe(0.90f, 0.05f, -0.2f, -0.1f),
        new Keyframe(1.00f, 0.00f,  0.0f,  0.0f)
    );

    public AnimationCurve eastCurve = new AnimationCurve(
        new Keyframe(0.00f, 1.00f,  0.0f,  0.0f),
        new Keyframe(0.10f, 0.70f, -4.0f, -4.0f),
        new Keyframe(0.30f, 0.10f, -1.5f, -1.5f),
        new Keyframe(0.55f, 0.02f, -0.1f, -0.1f),
        new Keyframe(1.00f, 0.00f,  0.0f,  0.0f)
    );

    public float maxDepth = 5000f;

    [Header("Terraces (West Only)")]
    [Range(0, 16)]
    public int terraceCount = 8;

    [Range(0f, 1f)]
    public float terraceStrength = 0.5f;
}

[System.Serializable]
public class BiomeSettings
{
    public HectonNoiseLayer biomeNoise = new HectonNoiseLayer
        { scale = 0.00015f, octaves = 2, lacunarity = 2f, persistence = 0.5f, seed = 555 };

    public AnimationCurve biomeRemapCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Flat Biome (Sand / Dunes)")]
    public HectonNoiseLayer flatSurfaceNoise = new HectonNoiseLayer
        { scale = 0.003f, octaves = 2, lacunarity = 2f, persistence = 0.4f, seed = 333 };
    public float flatSurfaceAmplitude = 5f;

    [Header("Aggressive Biome (Rock / Fractures)")]
    public HectonNoiseLayer aggressiveSurfaceNoise = new HectonNoiseLayer
        { scale = 0.005f, octaves = 5, lacunarity = 2f, persistence = 0.45f, seed = 444 };
    public float aggressiveSurfaceAmplitude = 40f;

    [Range(0f, 1f)]
    public float flatDisplacementFactor = 0.1f;
}

[System.Serializable]
public class DisplacementSettings
{
    public HectonNoiseLayer noise = new HectonNoiseLayer
        { scale = 0.008f, octaves = 3, lacunarity = 2f, persistence = 0.5f, seed = 666 };

    public Vector3 scale = new Vector3(20f, 15f, 20f);

    [Range(0f, 5f)]
    public float slopeWeight = 2f;
}

[System.Serializable]
public class CaveSettings
{
    public HectonNoiseLayer noise = new HectonNoiseLayer
        { scale = 0.02f, octaves = 3, lacunarity = 2.2f, persistence = 0.5f, seed = 777 };

    [Range(0.3f, 0.9f)]
    public float threshold = 0.62f;

    [Range(0.01f, 0.2f)]
    public float edgeWidth = 0.05f;

    public float minDepth = -30f;
}

public class HectonChunkData
{
    public int2 coord;
    public int lod;
    public GameObject go;
    public Mesh mesh;
}

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

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct HectonVertexJob : IJobParallelFor
{
    public int resX, resZ;
    public float originX, originZ;
    public float spacing;
    public int lodLevel;

    public float mapHalfSize;

    public float spineMaxHeight, spineWidth, spineWarpStrength, islandThreshold;
    public NoiseData warpNoise, islandNoise;

    public float westLen, eastLen, maxDepth;
    public int terraceCount;
    public float terraceStrength;

    public float flatAmp, aggrAmp, flatDispFactor;
    public NoiseData biomeNoise, flatSurfNoise, aggrSurfNoise;

    public float3 dispScale;
    public float slopeDispW;
    public NoiseData dispNoise;

    public float caveThresh, caveMinY;
    public NoiseData caveNoise;

    [ReadOnly] public NativeArray<float> westLUT, eastLUT, biomeLUT;

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

        float warpVal = HectonNoise.Fractal2D(0f, wz, warpNoise);
        float spineCX = (warpVal * 2f - 1f) * spineWarpStrength;

        float dx   = wx - spineCX;
        float absDx = math.abs(dx);
        bool  west  = dx < 0f;

        float sLen    = west ? westLen : eastLen;
        float normD   = math.saturate(absDx / math.max(sLen, 1f));
        float curveV;

        if (west) curveV = HectonNoise.SampleLUT(westLUT, normD);
        else      curveV = HectonNoise.SampleLUT(eastLUT, normD);

        if (west && terraceCount > 0 && terraceStrength > 0f)
        {
            float tc = (float)terraceCount;
            float stepped = math.round(curveV * tc) / tc;
            curveV = math.lerp(curveV, stepped, terraceStrength);
        }

        float floor = math.lerp(-maxDepth, 0f, curveV);

        float spineInf = math.saturate(1f - absDx / math.max(spineWidth, 1f));
        spineInf *= spineInf;
        float islN = HectonNoise.Fractal2D(wx * 0.1f, wz, islandNoise);
        float islF = math.saturate((islN - islandThreshold) / math.max(1f - islandThreshold, 0.01f));
        float spineElev = spineMaxHeight * spineInf * islF;

        float bRaw  = HectonNoise.Fractal2D(wx, wz, biomeNoise);
        float bVal  = HectonNoise.SampleLUT(biomeLUT, bRaw);
        float flatN = HectonNoise.Fractal2D(wx, wz, flatSurfNoise);
        float agrN  = HectonNoise.Fractal2D(wx, wz, aggrSurfNoise);
        float surfY = (math.lerp(flatN, agrN, bVal) * 2f - 1f) *
                       math.lerp(flatAmp, aggrAmp, bVal);

        float y = floor + spineElev + surfY;

        float fadeX = math.saturate((mapHalfSize - math.abs(wx)) / 1000f);
        float fadeZ = math.saturate((mapHalfSize - math.abs(wz)) / 1000f);
        y = math.lerp(-maxDepth, y, fadeX * fadeZ);

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

        float caveVal = 0f;
        byte  caveBit = 0;
        if (lodLevel == 0 && fy < caveMinY)
        {
            caveVal = HectonNoise.Fractal3D(fx, fy, fz, caveNoise, 300f);
            caveVal *= math.lerp(0.8f, 1.1f, slopeW);
            caveBit = (byte)(caveVal > caveThresh ? 1 : 0);
        }

        outCave[idx]   = caveVal;
        outIsCave[idx] = caveBit;
        outBiome[idx]  = bVal;
    }
}

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
    public Transform viewer;
    public float chunkSize = 256f;

    [Range(1f, 8f)]
    public float lod0Spacing = 2f;

    [Range(8f, 32f)]
    public float lod1Spacing = 16f;

    public float activeRadius = 500f;
    public float distantRadius = 2000f;

    [Range(1, 8)]
    public int maxChunksPerFrame = 2;

    [Header("═══ ASYNC PIPELINE ═══")]
    [Range(1, 8)]
    public int maxPendingChunks = 4;

    [Range(1, 4)]
    public int maxFinalizationsPerFrame = 2;

    [Header("═══ RENDERING ═══")]
    public Material terrainMaterial;
    public bool generateColliders = true;

    [Header("═══ VOXEL ENGINE ═══")]
    [Tooltip("Reference to the voxel engine for cave/rift generation.")]
    public HectonVoxelEngine voxelEngine;

    [Header("═══ MAP ═══")]
    public float mapSize = 15000f;

    #endregion

    // ╔═══════════════════════════════════════════════╗
    // ║             INTERNAL STATE                    ║
    // ╚═══════════════════════════════════════════════╝

    #region Internal State

    private class PendingChunk
    {
        public int2 coord;
        public int lod;
        public int resX;
        public int resZ;
        public float spacing;
        public NativeArray<Vector3> verts;
        public NativeArray<Vector3> norms;
        public NativeArray<Vector2> uvs;
        public NativeArray<Color>   cols;
        public NativeArray<float>   caveV;
        public NativeArray<byte>    caveB;
        public NativeArray<float>   biomeV;
        public JobHandle combinedHandle;

        public void DisposeArrays()
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

    readonly Dictionary<int2, HectonChunkData> _active = new Dictionary<int2, HectonChunkData>();
    readonly List<HectonChunkRequest> _queue = new List<HectonChunkRequest>();
    int _queueHead;

    private readonly List<PendingChunk> _pendingChunks = new List<PendingChunk>();

    readonly List<Mesh>       _bakeMeshes  = new List<Mesh>();
    readonly List<GameObject> _bakeObjects = new List<GameObject>();
    int _bakeHead;
    const int MAX_BAKES_PER_FRAME = 2;

    readonly Dictionary<int2, List<Vector3>> _poiBases     = new Dictionary<int2, List<Vector3>>();
    readonly Dictionary<int2, List<Vector3>> _poiResources = new Dictionary<int2, List<Vector3>>();

    NativeArray<float> _westLUT, _eastLUT, _biomeLUT;
    bool _lutsReady;

    int2 _lastChunk = new int2(int.MinValue, int.MinValue);
    bool _streaming;

    [HideInInspector] public GameObject previewObj;

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
        ProcessPendingChunks();

        if (_bakeHead < _bakeMeshes.Count)
            BakePhysicsBatch();
    }

    void StartStreaming()
    {
        if (viewer == null) { Debug.LogWarning("[Hecton] No viewer assigned."); return; }
        EnsureLUTs();
        _streaming  = true;
        _lastChunk  = new int2(int.MinValue, int.MinValue);
    }

    void StopStreaming()
    {
        _streaming = false;
        _queue.Clear();
        _queueHead = 0;

        FlushPendingChunks();

        foreach (var kvp in _active) DestroyChunk(kvp.Value);
        _active.Clear();

        _bakeMeshes.Clear(); _bakeObjects.Clear(); _bakeHead = 0;
        _poiBases.Clear();
        _poiResources.Clear();

        DisposeLUTs();
    }

    void FlushPendingChunks()
    {
        for (int i = _pendingChunks.Count - 1; i >= 0; i--)
        {
            var pc = _pendingChunks[i];
            pc.combinedHandle.Complete();
            pc.DisposeArrays();
        }
        _pendingChunks.Clear();
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

        var desired = new Dictionary<int2, int>();
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

        var remove = new List<int2>();
        foreach (var kvp in _active)
        {
            if (!desired.TryGetValue(kvp.Key, out int wantLod) || wantLod != kvp.Value.lod)
                remove.Add(kvp.Key);
        }
        foreach (var c in remove) { DestroyChunk(_active[c]); _active.Remove(c); }

        for (int i = _pendingChunks.Count - 1; i >= 0; i--)
        {
            var pc = _pendingChunks[i];
            if (!desired.TryGetValue(pc.coord, out int wantLod) || wantLod != pc.lod)
            {
                pc.combinedHandle.Complete();
                pc.DisposeArrays();
                _pendingChunks.RemoveAt(i);
            }
        }

        var pendingSet = new HashSet<int2>();
        for (int i = 0; i < _pendingChunks.Count; i++)
            pendingSet.Add(_pendingChunks[i].coord);

        var requests = new List<HectonChunkRequest>();
        foreach (var kvp in desired)
        {
            if (_active.ContainsKey(kvp.Key)) continue;
            if (pendingSet.Contains(kvp.Key)) continue;

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
        int scheduled = 0;
        while (_queueHead < _queue.Count
               && scheduled < maxChunksPerFrame
               && _pendingChunks.Count < maxPendingChunks)
        {
            var req = _queue[_queueHead++];

            if (_active.TryGetValue(req.coord, out var existing))
            {
                if (existing.lod == req.lod) continue;
                DestroyChunk(existing);
                _active.Remove(req.coord);
            }

            bool alreadyPending = false;
            for (int i = 0; i < _pendingChunks.Count; i++)
            {
                if (_pendingChunks[i].coord.Equals(req.coord))
                {
                    alreadyPending = true;
                    break;
                }
            }
            if (alreadyPending) continue;

            ScheduleChunkJob(req.coord, req.lod);
            scheduled++;
        }
    }

    void ProcessPendingChunks()
    {
        int finalized = 0;

        for (int i = _pendingChunks.Count - 1; i >= 0 && finalized < maxFinalizationsPerFrame; i--)
        {
            var pc = _pendingChunks[i];

            if (!pc.combinedHandle.IsCompleted)
                continue;

            pc.combinedHandle.Complete();

            var cd = FinalizeChunk(pc);
            if (cd != null)
                _active[pc.coord] = cd;

            _pendingChunks.RemoveAt(i);
            finalized++;
        }
    }

    #endregion

    // ╔═══════════════════════════════════════════════╗
    // ║        ASYNC CHUNK GENERATION PIPELINE        ║
    // ╚═══════════════════════════════════════════════╝

    #region Async Chunk Generation

    void ScheduleChunkJob(int2 coord, int lod)
    {
        float sp  = lod == 0 ? lod0Spacing : lod1Spacing;
        int   res = Mathf.CeilToInt(chunkSize / sp) + 1;
        int   vc  = res * res;
        float2 org = ChunkOrigin(coord);

        var verts  = new NativeArray<Vector3>(vc, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var norms  = new NativeArray<Vector3>(vc, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var uvs    = new NativeArray<Vector2>(vc, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var cols   = new NativeArray<Color>(vc,   Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var caveV  = new NativeArray<float>(vc,   Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var caveB  = new NativeArray<byte>(vc,    Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var biomeV = new NativeArray<float>(vc,   Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        var vertexJob = MakeVertexJob(res, res, org.x, org.y, sp, lod,
                                       verts, uvs, caveV, caveB, biomeV);
        JobHandle h1 = vertexJob.Schedule(vc, JOB_BATCH);

        var normalJob = new HectonNormalJob
        {
            resX = res, resZ = res,
            vertices = verts,
            normals  = norms
        };
        JobHandle h2 = normalJob.Schedule(vc, JOB_BATCH, h1);

        var colorJob = new HectonColorJob
        {
            maxDepth   = slopes.maxDepth,
            caveThresh = caves.threshold,
            caveEdge   = caves.edgeWidth,
            verts  = verts,
            norms  = norms,
            cave   = caveV,
            isCave = caveB,
            biome  = biomeV,
            colors = cols
        };
        JobHandle h3 = colorJob.Schedule(vc, JOB_BATCH, h2);

        var pc = new PendingChunk
        {
            coord          = coord,
            lod            = lod,
            resX           = res,
            resZ           = res,
            spacing        = sp,
            verts          = verts,
            norms          = norms,
            uvs            = uvs,
            cols           = cols,
            caveV          = caveV,
            caveB          = caveB,
            biomeV         = biomeV,
            combinedHandle = h3
        };

        _pendingChunks.Add(pc);
    }

    HectonChunkData FinalizeChunk(PendingChunk pc)
    {
        try
        {
            int res = pc.resX;
            int vc  = res * pc.resZ;

            int maxTri = (res - 1) * (pc.resZ - 1) * 6;
            var tris = new int[maxTri];
            int tc = 0;
            bool cutCaves = pc.lod == 0;

            for (int z = 0; z < pc.resZ - 1; z++)
            for (int x = 0; x < res - 1; x++)
            {
                int i00 = z * res + x;
                int i10 = i00 + 1;
                int i01 = i00 + res;
                int i11 = i01 + 1;

                if (!cutCaves || !(pc.caveB[i00] > 0 &&
                    pc.caveB[i01] > 0 && pc.caveB[i10] > 0))
                {
                    tris[tc++] = i00;
                    tris[tc++] = i01;
                    tris[tc++] = i10;
                }

                if (!cutCaves || !(pc.caveB[i10] > 0 && pc.caveB[i01] > 0 && pc.caveB[i11] > 0))
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

            var mesh = new Mesh();
            mesh.name = $"Hecton_{pc.coord.x}_{pc.coord.y}_L{pc.lod}";
            if (vc > 65535) mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(pc.verts);
            mesh.SetNormals(pc.norms);
            mesh.SetUVs(0, pc.uvs);
            mesh.SetColors(pc.cols);
            mesh.triangles = tris;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            var go = new GameObject(mesh.name);
            go.transform.SetParent(transform, false);
            go.isStatic = true;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial    = terrainMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows    = true;

            if (pc.lod == 0 && generateColliders)
            {
                _bakeMeshes.Add(mesh);
                _bakeObjects.Add(go);
            }

            if (pc.lod == 0)
            {
                ExtractPOI(pc.coord, pc.verts, pc.norms, pc.caveB);
                SpawnVoxelPOIs(pc.coord, pc.verts, pc.caveB, pc.resX, pc.resZ, pc.spacing);
            }

            return new HectonChunkData
            {
                coord = pc.coord,
                lod   = pc.lod,
                go    = go,
                mesh  = mesh
            };
        }
        finally
        {
            pc.DisposeArrays();
        }
    }

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

    #endregion

    // ╔═══════════════════════════════════════════════╗
    // ║            VOXEL POI SPAWNING                 ║
    // ╚═══════════════════════════════════════════════╝

    #region Voxel POI

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

        var cavePositions = new List<Vector3>(64);
        for (int i = 0; i < verts.Length; i++)
        {
            if (caveB[i] > 0)
                cavePositions.Add(verts[i]);
        }

        if (cavePositions.Count > 0)
        {
            float clusterSize = 24f;
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

            var clusterList = new List<KeyValuePair<int2, List<Vector3>>>(clusters.Count);
            foreach (var kvp in clusters)
            {
                if (kvp.Value.Count < MinClusterVertices)
                    continue;
                clusterList.Add(kvp);
            }

            clusterList.Sort((a, b) => b.Value.Count.CompareTo(a.Value.Count));

            int spawned = 0;
            for (int ci = 0; ci < clusterList.Count && spawned < MaxVoxelsPerChunk; ci++)
            {
                var cluster = clusterList[ci];

                float sumX = 0f, sumZ = 0f;
                for (int i = 0; i < cluster.Value.Count; i++)
                {
                    sumX += cluster.Value[i].x;
                    sumZ += cluster.Value[i].z;
                }
                float cx = sumX / cluster.Value.Count;
                float cz = sumZ / cluster.Value.Count;

                float floorHeight = GetWorldHeight(cx, cz);
                Vector3 spawnPos = new Vector3(cx, floorHeight - 15f, cz);

                var poiGO = new GameObject($"Voxel_Cave_{coord.x}_{coord.y}_{cluster.Key.x}_{cluster.Key.y}");
                poiGO.transform.SetParent(transform, false);
                poiGO.transform.position = spawnPos;
                poiGO.isStatic = true;

                voxelEngine.GenerateVolume(poiGO, spawnPos, VoxelPOIType.Cave);
                spawned++;
            }
        }

        NoiseData riftND = new NoiseData
        {
            scale = 0.00008f, octaves = 2, lacunarity = 2f,
            persistence = 0.5f, offset = float3.zero, seed = 9999
        };

        float riftNoise = HectonNoise.Fractal2D(chunkCenterX, chunkCenterZ, riftND);
        float riftThreshold = 0.82f;

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

        _poiBases.Remove(coord);
        _poiResources.Remove(coord);

        if (bases != null) _poiBases[coord]     = bases;
        if (res != null)   _poiResources[coord] = res;
    }

    #endregion

    // ╔═══════════════════════════════════════════════╗
    // ║       PARALLEL PHYSICS BAKING                 ║
    // ╚═══════════════════════════════════════════════╝

    #region Physics

    void BakePhysicsBatch()
    {
        int baked = 0;

        while (_bakeHead < _bakeMeshes.Count && baked < MAX_BAKES_PER_FRAME)
        {
            Mesh mesh       = _bakeMeshes[_bakeHead];
            GameObject go   = _bakeObjects[_bakeHead];
            _bakeHead++;

            if (mesh == null || go == null) continue;

            #pragma warning disable CS0618
            Physics.BakeMesh(mesh.GetInstanceID(), false);
            #pragma warning restore CS0618

            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;

            baked++;
        }

        if (_bakeHead >= _bakeMeshes.Count)
        {
            _bakeMeshes.Clear();
            _bakeObjects.Clear();
            _bakeHead = 0;
        }
    }

    #endregion

    // ╔═══════════════════════════════════════════════╗
    // ║           CHUNK LIFECYCLE (v2.1 FIX)          ║
    // ╚═══════════════════════════════════════════════╝

    #region Chunk Lifecycle

    /// <summary>
    /// Destroys a chunk and all its associated voxel volumes.
    ///
    /// v2.1 FIX: Voxel child objects are now destroyed through
    /// voxelEngine.DespawnVolume() instead of direct SafeDestroy().
    ///
    /// БЫЛО (v2.0, УТЕЧКА):
    ///   SafeDestroy(child.gameObject)
    ///   → HectonVoxelEngine._activeVolumes[i] becomes null
    ///   → null accumulates infinitely
    ///   → O(n) degradation in DespawnVolume/ClearAll
    ///
    /// СТАЛО (v2.1, КОРРЕКТНО):
    ///   voxelEngine.DespawnVolume(child.gameObject)
    ///   → removes from _activeVolumes
    ///   → cleans mesh + collider
    ///   → returns to pool or destroys
    ///   → zero null references
    ///
    /// FALLBACK: если voxelEngine == null (уничтожен раньше,
    /// смена сцены), используется прямой SafeDestroy (как в v2.0).
    /// Это безопасно — _activeVolumes тоже уничтожен вместе с engine.
    /// </summary>
    void DestroyChunk(HectonChunkData cd)
    {
        if (cd == null) return;

        // ── Destroy child voxel volumes (v2.1: через DespawnVolume) ──
        if (cd.go != null)
        {
            string cavePrefix = $"Voxel_Cave_{cd.coord.x}_{cd.coord.y}";
            string riftPrefix = $"Voxel_Rift_{cd.coord.x}_{cd.coord.y}";

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith(cavePrefix) || child.name.StartsWith(riftPrefix))
                {
                    // v2.1: Делегируем очистку VoxelEngine.
                    // DespawnVolume удаляет из _activeVolumes,
                    // чистит mesh/collider, возвращает в пул.
                    if (voxelEngine != null)
                    {
                        voxelEngine.DespawnVolume(child.gameObject);
                    }
                    else
                    {
                        // Fallback: engine уже уничтожен (смена сцены).
                        // Ручная очистка как в v2.0.
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
        }

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

    public float GetBiomeAt(float x, float z)
    {
        EnsureLUTs();
        NoiseData bioND = NoiseData.From(biomes.biomeNoise);
        float bRaw = HectonNoise.Fractal2D(x, z, bioND);
        return HectonNoise.SampleLUT(_biomeLUT, bRaw);
    }

    public float GetWorldHeight(float x, float z)
    {
        EnsureLUTs();

        NoiseData warpND = NoiseData.From(spine.warpNoise);
        NoiseData islND  = NoiseData.From(spine.islandNoise);
        NoiseData bioND  = NoiseData.From(biomes.biomeNoise);
        NoiseData flatND = NoiseData.From(biomes.flatSurfaceNoise);
        NoiseData aggrND = NoiseData.From(biomes.aggressiveSurfaceNoise);

        float hs = mapSize * 0.5f;

        float warpVal = HectonNoise.Fractal2D(0f, z, warpND);
        float spineCX = (warpVal * 2f - 1f) * spine.warpStrength;

        float dx    = x - spineCX;
        float absDx = Mathf.Abs(dx);
        bool  west  = dx < 0f;

        float sLen  = west ? slopes.westLength : slopes.eastLength;
        float normD = Mathf.Clamp01(absDx / Mathf.Max(sLen, 1f));
        float curveV;

        if (west) curveV = HectonNoise.SampleLUT(_westLUT, normD);
        else      curveV = HectonNoise.SampleLUT(_eastLUT, normD);

        if (west && slopes.terraceCount > 0 && slopes.terraceStrength > 0f)
        {
            float tc      = (float)slopes.terraceCount;
            float stepped = Mathf.Round(curveV * tc) / tc;
            curveV = Mathf.Lerp(curveV, stepped, slopes.terraceStrength);
        }

        float floor = Mathf.Lerp(-slopes.maxDepth, 0f, curveV);

        float spineInf = Mathf.Clamp01(1f - absDx / Mathf.Max(spine.width, 1f));
        spineInf *= spineInf;
        float islN = HectonNoise.Fractal2D(x * 0.1f, z, islND);
        float islF = Mathf.Clamp01(
            (islN - spine.islandThreshold) /
            Mathf.Max(1f - spine.islandThreshold, 0.01f));
        float spineElev = spine.maxHeight * spineInf * islF;

        float bRaw = HectonNoise.Fractal2D(x, z, bioND);
        float bVal = HectonNoise.SampleLUT(_biomeLUT, bRaw);
        float fltN = HectonNoise.Fractal2D(x, z, flatND);
        float agrN = HectonNoise.Fractal2D(x, z, aggrND);
        float surfY = (Mathf.Lerp(fltN, agrN, bVal) * 2f - 1f) *
                       Mathf.Lerp(biomes.flatSurfaceAmplitude,
                                  biomes.aggressiveSurfaceAmplitude, bVal);

        float y = floor + spineElev + surfY;

        float fadeX = Mathf.Clamp01((hs - Mathf.Abs(x)) / 1000f);
        float fadeZ = Mathf.Clamp01((hs - Mathf.Abs(z)) / 1000f);
        y = Mathf.Lerp(-slopes.maxDepth, y, fadeX * fadeZ);

        return y;
    }

    [ContextMenu("▶ Generate World Preview")]
    public void GenerateWorldPreview()
    {
        ClearPreview();
        EnsureLUTs();

        const float previewSpacing = 100f;
        float hs  = mapSize * 0.5f;
        int   res = Mathf.CeilToInt(mapSize / previewSpacing) + 1;
        int   vc  = res * res;

        var verts  = new NativeArray<Vector3>(vc, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var norms  = new NativeArray<Vector3>(vc, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var uvs    = new NativeArray<Vector2>(vc, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var cols   = new NativeArray<Color>(vc, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var caveV  = new NativeArray<float>(vc, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var caveB  = new NativeArray<byte>(vc, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var biomeV = new NativeArray<float>(vc, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        try
        {
#if UNITY_EDITOR
            EditorUtility.DisplayProgressBar("Hecton World Preview",
                $"Generating {res}×{res} vertices...", 0.2f);
#endif
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

    [ContextMenu("✕ Clear All")]
    public void ClearAll()
    {
        StopStreaming();
        ClearPreview();

        for (int i = transform.childCount - 1; i >= 0; i--)
            SafeDestroy(transform.GetChild(i).gameObject);
    }

    #endregion

    // ╔═══════════════════════════════════════════════╗
    // ║                 GIZMOS                        ║
    // ╚═══════════════════════════════════════════════╝

    #region Gizmos

    void OnDrawGizmosSelected()
    {
        float hs = mapSize * 0.5f;

        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = new Color(0f, 0.5f, 1f, 0.2f);
        float totalH = slopes.maxDepth + spine.maxHeight;
        Gizmos.DrawWireCube(
            new Vector3(0f, -(slopes.maxDepth - spine.maxHeight) * 0.5f, 0f),
            new Vector3(mapSize, totalH, mapSize));

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.1f);
        Gizmos.DrawCube(Vector3.zero, new Vector3(mapSize, 0.5f, mapSize));

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.3f);
        Gizmos.DrawWireCube(
            new Vector3(0f, -slopes.maxDepth, 0f),
            new Vector3(mapSize, 0.5f, mapSize));

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

    #endregion

    // ╔═══════════════════════════════════════════════╗
    // ║              UTILITIES                        ║
    // ╚═══════════════════════════════════════════════╝

    #region Utilities

    public int ActiveChunkCount => _active.Count;
    public int PendingChunkCount => _pendingChunks.Count;

    public int BaseLocationCount
    {
        get { int n = 0; foreach (var kv in _poiBases) n += kv.Value.Count; return n; }
    }

    public int ResourceNodeCount
    {
        get { int n = 0; foreach (var kv in _poiResources) n += kv.Value.Count; return n; }
    }

    public void GetAllBaseLocations(List<Vector3> result)
    {
        result.Clear();
        foreach (var kv in _poiBases)
            result.AddRange(kv.Value);
    }

    public void GetAllResourceNodes(List<Vector3> result)
    {
        result.Clear();
        foreach (var kv in _poiResources)
            result.AddRange(kv.Value);
    }

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

    void OnDestroy()
    {
        ClearAll();
    }
}

#endregion

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

        bool hasContent = gen.transform.childCount > 0;
        if (hasContent || (Application.isPlaying && gen.ActiveChunkCount > 0))
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
                $"Active chunks: {gen.ActiveChunkCount}\n" +
                $"Pending (async): {gen.PendingChunkCount}\n" : "") +
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
            "Project HECTON-8 World Engine v2.1\n" +
            "Burst + Jobs + Async Chunk Pipeline + LOD + Parallel PhysX\n" +
            "v2.1: Voxel lifecycle fix (DespawnVolume integration)\n" +
            "Zero main-thread blocks during streaming.",
            MessageType.None);
    }
}

#endif