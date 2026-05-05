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
using System.Diagnostics;
using Hecton8.Core;
using Hecton8.World;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
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
    public MeshRenderer renderer;
    public MeshCollider collider;
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

[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

public class HectonWorldGenerator : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IWorldSeedProvider
{
    private const int WorldGenerationAlgorithmVersionId = 1;
    private static readonly ProfilerMarker _tickProfilerMarker = new ProfilerMarker("H8.WorldGenerator.Tick");
    private static readonly ProfilerMarker _physicsBakeBatchProfilerMarker = new ProfilerMarker("H8.WorldGenerator.PhysicsBakeBatch");
    public bool IsInitialized => ReferenceEquals(GlobalRegistry.WorldSeedProvider, this);
    public int RuntimeWorldSeed => ComputeRuntimeWorldSeed();
    public int RuntimeWorldGenerationVersionId => WorldGenerationAlgorithmVersionId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _deferredPhysicsBakeTeardowns.Clear();
        _deferredPhysicsBakeTeardownRegistered = false;
    }

    private int ComputeRuntimeWorldSeed()
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = MixRuntimeSeed(hash, spine != null && spine.warpNoise != null ? spine.warpNoise.seed : 0);
            hash = MixRuntimeSeed(hash, spine != null && spine.islandNoise != null ? spine.islandNoise.seed : 0);
            hash = MixRuntimeSeed(hash, biomes != null && biomes.biomeNoise != null ? biomes.biomeNoise.seed : 0);
            hash = MixRuntimeSeed(hash, biomes != null && biomes.flatSurfaceNoise != null ? biomes.flatSurfaceNoise.seed : 0);
            hash = MixRuntimeSeed(hash, biomes != null && biomes.aggressiveSurfaceNoise != null ? biomes.aggressiveSurfaceNoise.seed : 0);
            hash = MixRuntimeSeed(hash, displacement != null && displacement.noise != null ? displacement.noise.seed : 0);
            hash = MixRuntimeSeed(hash, caves != null && caves.noise != null ? caves.noise.seed : 0);
            hash = MixRuntimeSeed(hash, Mathf.RoundToInt(chunkSize));
            return hash == 0u ? 1 : (int)hash;
        }
    }

    private static uint MixRuntimeSeed(uint hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            hash *= 16777619u;
            hash ^= hash >> 13;
            return hash;
        }
    }

    // COLD ALLOC: Comparison<HectonChunkRequest>[1] - cached request sort delegate, prevents per-refresh lambda allocation - owner: HectonWorldGenerator
    private static readonly System.Comparison<HectonChunkRequest> _chunkRequestDistanceComparison = CompareChunkRequestsByDistance;
    // COLD ALLOC: Comparison<VoxelClusterAccumulator>[1] - cached POI cluster sort delegate, prevents per-finalize lambda allocation - owner: HectonWorldGenerator
    private static readonly System.Comparison<VoxelClusterAccumulator> _voxelClusterCountDescendingComparison = CompareVoxelClustersByCountDescending;

    private readonly struct Long2
    {
        public readonly long x;
        public readonly long y;

        public Long2(long x, long y)
        {
            this.x = x;
            this.y = y;
        }
    }

    struct PendingPhysicsBake
    {
        public Mesh Mesh;
        public GameObject Owner;
        public MeshRenderer Renderer;
        public MeshCollider Collider;
        public Material DefaultMaterial;
        public JobHandle Handle;
        public byte State;
    }

    private struct DeferredPhysicsBakeTeardown
    {
        public Mesh Mesh;
        public GameObject Owner;
        public MeshRenderer Renderer;
        public MeshCollider Collider;
        public Material DefaultMaterial;
        public JobHandle Handle;
    }

    private sealed class DeferredPhysicsBakeTeardownDriver : ILateFrameTickable
    {
        public void LateFrameTick()
        {
            DrainDeferredPhysicsBakeTeardowns();
        }
    }

    private struct VoxelClusterAccumulator
    {
        public int2 Cell;
        public int Count;
        public float SumX;
        public float SumZ;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    struct HectonPhysicsBakeJob : IJob
    {
        public EntityId MeshEntityId;

        public void Execute()
        {
            Physics.BakeMesh(MeshEntityId, false);
        }
    }

    const byte PhysicsBakeStatePending = 0;
    const byte PhysicsBakeStateScheduled = 1;
    const byte PhysicsBakeStateCompleted = 2;
    const byte PhysicsBakeStateCanceled = 3;
    const string RuntimeChunkObjectName = "HectonChunk";
    private const float PhysicsBakeFrameBudgetMilliseconds = 2f;
    private const int DeferredPhysicsBakeTeardownDrainBudget = 8;
    private const int DeferredPhysicsBakeTeardownCapacity = 2048;
    private static readonly double _physicsBakeTickToMilliseconds = 1000d / Stopwatch.Frequency;
    // COLD ALLOC: List<DeferredPhysicsBakeTeardown>[2048] - dispatcher-drained streamed chunk PhysX bake teardown queue - owner: HectonWorldGenerator
    private static readonly List<DeferredPhysicsBakeTeardown> _deferredPhysicsBakeTeardowns = new List<DeferredPhysicsBakeTeardown>(DeferredPhysicsBakeTeardownCapacity);
    // COLD ALLOC: DeferredPhysicsBakeTeardownDriver[1] - non-Mono late-frame drain adapter, avoids per-teardown allocation - owner: HectonWorldGenerator
    private static readonly DeferredPhysicsBakeTeardownDriver _deferredPhysicsBakeTeardownDriver = new DeferredPhysicsBakeTeardownDriver();
    private static bool _deferredPhysicsBakeTeardownRegistered;

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
    [Tooltip("Optional fallback material shown while streamed chunk collision is still baking.")]
    public Material pendingCollisionBakeMaterial;

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

    private const string NativeMemoryOwner = nameof(HectonWorldGenerator);
    private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;

    private struct PendingChunk
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
        public bool cancelRequested;

        public void RegisterArrays()
        {
            RegisterTrackedNativeArray(verts, nameof(verts));
            RegisterTrackedNativeArray(norms, nameof(norms));
            RegisterTrackedNativeArray(uvs, nameof(uvs));
            RegisterTrackedNativeArray(cols, nameof(cols));
            RegisterTrackedNativeArray(caveV, nameof(caveV));
            RegisterTrackedNativeArray(caveB, nameof(caveB));
            RegisterTrackedNativeArray(biomeV, nameof(biomeV));
        }

        public void DisposeArrays()
        {
            DisposeTrackedNativeArray(ref verts);
            DisposeTrackedNativeArray(ref norms);
            DisposeTrackedNativeArray(ref uvs);
            DisposeTrackedNativeArray(ref cols);
            DisposeTrackedNativeArray(ref caveV);
            DisposeTrackedNativeArray(ref caveB);
            DisposeTrackedNativeArray(ref biomeV);
        }

        public JobHandle DisposeArrays(JobHandle dependency)
        {
            bool scheduledDisposal = false;
            JobHandle disposeHandle = dependency;
            DisposeTrackedNativeArray(ref verts, ref disposeHandle, ref scheduledDisposal);
            DisposeTrackedNativeArray(ref norms, ref disposeHandle, ref scheduledDisposal);
            DisposeTrackedNativeArray(ref uvs, ref disposeHandle, ref scheduledDisposal);
            DisposeTrackedNativeArray(ref cols, ref disposeHandle, ref scheduledDisposal);
            DisposeTrackedNativeArray(ref caveV, ref disposeHandle, ref scheduledDisposal);
            DisposeTrackedNativeArray(ref caveB, ref disposeHandle, ref scheduledDisposal);
            DisposeTrackedNativeArray(ref biomeV, ref disposeHandle, ref scheduledDisposal);
            return scheduledDisposal ? disposeHandle : dependency;
        }
    }

    // COLD ALLOC: Dictionary<int2,HectonChunkData>[512] - active streamed chunk lookup for residency refresh - owner: HectonWorldGenerator
    readonly Dictionary<int2, HectonChunkData> _active = new Dictionary<int2, HectonChunkData>(512);
    // COLD ALLOC: List<HectonChunkRequest>[512] - active streaming request queue, reused across refreshes - owner: HectonWorldGenerator
    readonly List<HectonChunkRequest> _queue = new List<HectonChunkRequest>(512);
    // COLD ALLOC: Dictionary<int2,int>[512] — reused desired-chunk set for AUP streaming refresh — owner: HectonWorldGenerator
    readonly Dictionary<int2, int> _desiredChunks = new Dictionary<int2, int>(512);
    // COLD ALLOC: List<int2>[256] — reused active-chunk removal scratch for AUP streaming refresh — owner: HectonWorldGenerator
    readonly List<int2> _chunksToRemove = new List<int2>(256);
    // COLD ALLOC: HashSet<int2>[256] — reused pending-chunk lookup for AUP streaming refresh — owner: HectonWorldGenerator
    readonly HashSet<int2> _pendingChunkCoordSet = new HashSet<int2>(256);
    // COLD ALLOC: List<HectonChunkRequest>[512] — reused chunk-request sort scratch for AUP streaming refresh — owner: HectonWorldGenerator
    readonly List<HectonChunkRequest> _requestScratch = new List<HectonChunkRequest>(512);
    int _queueHead;

    // COLD ALLOC: List<PendingChunk>[64] - pending streamed chunk job records - owner: HectonWorldGenerator
    private readonly List<PendingChunk> _pendingChunks = new List<PendingChunk>(64);

    // COLD ALLOC: List<PendingPhysicsBake>[64] — background PhysX bake queue for streamed chunk colliders — owner: HectonWorldGenerator
    readonly List<PendingPhysicsBake> _pendingPhysicsBakes = new List<PendingPhysicsBake>(64);
    // COLD ALLOC: List<HectonChunkData>[64] - deferred chunk destruction while PhysX bake jobs finish - owner: HectonWorldGenerator
    readonly List<HectonChunkData> _deferredChunkRetirements = new List<HectonChunkData>(64);
    // COLD ALLOC: Dictionary<int2,int>[1024] - reused cave cell to accumulator index map for voxel POI finalization - owner: HectonWorldGenerator
    readonly Dictionary<int2, int> _voxelClusterIndexByCell = new Dictionary<int2, int>(1024);
    // COLD ALLOC: List<VoxelClusterAccumulator>[1024] - reused cave cluster accumulators, replaces per-chunk List/Dictionary allocations - owner: HectonWorldGenerator
    readonly List<VoxelClusterAccumulator> _voxelClusterScratch = new List<VoxelClusterAccumulator>(1024);
    int _physicsBakeScheduleHead;
    int _physicsBakeFinalizeHead;
    const int MAX_BAKES_PER_FRAME = 2;

    const int PoiListPoolCapacity = 1024;
    const int PoiListInitialCapacity = 256;
    // COLD ALLOC: Dictionary<int2,List<Vector3>>[512] - active base POI lookup by chunk - owner: HectonWorldGenerator
    readonly Dictionary<int2, List<Vector3>> _poiBases = new Dictionary<int2, List<Vector3>>(512);
    // COLD ALLOC: Dictionary<int2,List<Vector3>>[512] - active resource POI lookup by chunk - owner: HectonWorldGenerator
    readonly Dictionary<int2, List<Vector3>> _poiResources = new Dictionary<int2, List<Vector3>>(512);
    // COLD ALLOC: List<List<Vector3>>[1024] - pooled POI vector lists retained for active streamed chunks - owner: HectonWorldGenerator
    readonly List<List<Vector3>> _poiVectorListPool = new List<List<Vector3>>(PoiListPoolCapacity);
    bool _poiVectorListPoolReady;

    NativeArray<float> _westLUT, _eastLUT, _biomeLUT;
    bool _lutsReady;

    int2 _lastChunk = new int2(int.MinValue, int.MinValue);
    bool _streaming;
    bool _registeredToTickManager;
    bool _registeredToLateFrame;

    [HideInInspector] public GameObject previewObj;

    const int LUT_RES   = 1024;
    const int JOB_BATCH = 64;
    const int DefaultTriangleScratchCapacity = 393216; // 256m chunk at 1m spacing: 256 * 256 * 6 indices.
    // COLD ALLOC: List<int>[393216] (~1536 KB) - reused terrain triangle index scratch, prevents per-chunk managed int[] allocations during streaming finalization - owner: HectonWorldGenerator
    readonly List<int> _triangleScratch = new List<int>(DefaultTriangleScratchCapacity);

    private static void RegisterTrackedNativeArray<T>(NativeArray<T> array, string label) where T : struct
    {
        if (!array.IsCreated)
            return;

        NativeMemorySentinel.RegisterNativeArray(
            array,
            NativeMemoryOwner,
            label,
            NativeMemoryLifetime);
    }

    private static void DisposeTrackedNativeArray<T>(ref NativeArray<T> array) where T : struct
    {
        if (!array.IsCreated)
            return;

        NativeMemorySentinel.UnregisterNativeArray(array);
        array.Dispose();
        array = default;
    }

    private static void DisposeTrackedNativeArray<T>(
        ref NativeArray<T> array,
        ref JobHandle dependency,
        ref bool scheduledDisposal) where T : struct
    {
        if (!array.IsCreated)
            return;

        NativeMemorySentinel.UnregisterNativeArray(array);
        dependency = array.Dispose(dependency);
        array = default;
        scheduledDisposal = true;
    }

    #endregion

    // ╔═══════════════════════════════════════════════╗
    // ║              LIFECYCLE                        ║
    // ╚═══════════════════════════════════════════════╝

    #region Lifecycle

    void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        GlobalRegistry.RegisterWorldSeedProvider(this);
        StartStreaming();
        RegisterToTickManager();
    }

    void OnDisable()
    {
        GlobalRegistry.UnregisterWorldSeedProvider(this);

        UnregisterFromTickManager();

        if (Application.isPlaying || _streaming || _pendingChunks.Count > 0 || _lutsReady)
            StopStreaming();
    }

    public void Tick(float deltaTime)
    {
        using (_tickProfilerMarker.Auto())
        {
            if (!_streaming || viewer == null) return;

            int2 cur = WorldToChunk(viewer.position);
            if (!cur.Equals(_lastChunk))
            {
                _lastChunk = cur;
                RefreshChunks();
            }

            ProcessQueue();
        }
    }

    /// <summary>
    /// Drains completed chunk and physics-bake jobs in the dispatcher-owned late-frame swap window.
    /// </summary>
    public void LateFrameTick()
    {
        if (!_streaming)
            return;

        ProcessPendingChunks();

        if (_physicsBakeFinalizeHead < _pendingPhysicsBakes.Count ||
            _physicsBakeScheduleHead < _pendingPhysicsBakes.Count)
        {
            BakePhysicsBatch();
        }

        ProcessDeferredChunkRetirements(maxFinalizationsPerFrame);
    }

    void RegisterToTickManager()
    {
        if (_registeredToTickManager)
        {
            if (!_registeredToLateFrame && Application.isPlaying && GlobalRegistry.Dispatcher != null)
            {
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredToLateFrame = SystemDispatcher
                    .GetLateFrameLane(PriorityLayer.Environment)
                    .Contains(this);
            }

            return;
        }

        if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
            return;

        GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
        GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
        _registeredToTickManager = GlobalRegistry.Updatables.Contains(this);
        _registeredToLateFrame = SystemDispatcher
            .GetLateFrameLane(PriorityLayer.Environment)
            .Contains(this);
    }

    void UnregisterFromTickManager()
    {
        if (_registeredToLateFrame)
        {
            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredToLateFrame = false;
        }

        if (_registeredToTickManager)
        {
            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registeredToTickManager = false;
        }
    }

    void StartStreaming()
    {
        if (viewer == null) { UnityEngine.Debug.LogWarning("[Hecton] No viewer assigned."); return; }
        EnsureLUTs();
        EnsurePoiVectorListPool();
        EnsureTriangleScratchCapacity(ComputeConfiguredTriangleScratchRequirement());
        _streaming  = true;
        _lastChunk  = new int2(int.MinValue, int.MinValue);
    }

    void StopStreaming()
    {
        _streaming = false;
        _queue.Clear();
        _queueHead = 0;

        CompletePendingChunkJobsForTeardown();
        RetireDeferredChunksForStreamingStop();

        var activeEnumerator = _active.GetEnumerator();
        while (activeEnumerator.MoveNext())
            RetireChunkForStreamingStop(activeEnumerator.Current.Value);
        _active.Clear();

        HandOffRemainingPhysicsBakesForTeardown();
        ReleaseAllPoiLists();

        DisposeLUTs();
    }

    void FlushPendingChunks()
    {
        for (int i = _pendingChunks.Count - 1; i >= 0; i--)
        {
            var pc = _pendingChunks[i];
            if (!pc.combinedHandle.IsCompleted)
            {
                pc.cancelRequested = true;
                _pendingChunks[i] = pc;
                continue;
            }

            DispatcherJobSwap.TryComplete(ref pc.combinedHandle, forceComplete: false);
            pc.DisposeArrays();
            _pendingChunks.RemoveAt(i);
        }
    }

    void CompletePendingChunkJobsForTeardown()
    {
        JobHandle pendingChunkDependency = default;
        bool hasPendingDependency = false;
        for (int i = _pendingChunks.Count - 1; i >= 0; i--)
        {
            PendingChunk pc = _pendingChunks[i];
            pc.cancelRequested = true;
            JobHandle disposalHandle = pc.DisposeArrays(pc.combinedHandle);
            pendingChunkDependency = hasPendingDependency
                ? JobHandle.CombineDependencies(pendingChunkDependency, disposalHandle)
                : disposalHandle;
            hasPendingDependency = true;
        }

        _pendingChunks.Clear();
        if (hasPendingDependency)
        {
            DisposeLUTs(pendingChunkDependency);
            JobHandle.ScheduleBatchedJobs();
        }
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
        RegisterTrackedNativeArray(_westLUT, nameof(_westLUT));
        RegisterTrackedNativeArray(_eastLUT, nameof(_eastLUT));
        RegisterTrackedNativeArray(_biomeLUT, nameof(_biomeLUT));
        _lutsReady = true;
    }

    void DisposeLUTs()
    {
        if (!_lutsReady) return;
        DisposeTrackedNativeArray(ref _westLUT);
        DisposeTrackedNativeArray(ref _eastLUT);
        DisposeTrackedNativeArray(ref _biomeLUT);
        _lutsReady = false;
    }

    void DisposeLUTs(JobHandle dependency)
    {
        if (!_lutsReady) return;
        bool scheduledDisposal = false;
        JobHandle disposeHandle = dependency;
        DisposeTrackedNativeArray(ref _westLUT, ref disposeHandle, ref scheduledDisposal);
        DisposeTrackedNativeArray(ref _eastLUT, ref disposeHandle, ref scheduledDisposal);
        DisposeTrackedNativeArray(ref _biomeLUT, ref disposeHandle, ref scheduledDisposal);
        _lutsReady = false;
        if (scheduledDisposal)
            JobHandle.ScheduleBatchedJobs();
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

    int ComputeConfiguredTriangleScratchRequirement()
    {
        int res = Mathf.CeilToInt(chunkSize / Mathf.Max(0.001f, lod0Spacing)) + 1;
        return Mathf.Max(0, (res - 1) * (res - 1) * 6);
    }

    void EnsureTriangleScratchCapacity(int requiredTriangleIndices)
    {
        if (_triangleScratch.Capacity >= requiredTriangleIndices)
            return;

        // COLD ALLOC: List<int>.Capacity[requiredTriangleIndices] - authoring setting exceeded default streamed triangle scratch; avoids repeated runtime chunk-finalize arrays - owner: HectonWorldGenerator
        _triangleScratch.Capacity = requiredTriangleIndices;
    }

    #endregion

    // ╔═══════════════════════════════════════════════╗
    // ║             CHUNK STREAMING                   ║
    // ╚═══════════════════════════════════════════════╝

    #region Streaming

    int2 WorldToChunk(Vector3 p)
    {
        Long2 absoluteChunk = ResolveAbsoluteChunkCoord(p);
        return new int2(SaturateLongToInt(absoluteChunk.x), SaturateLongToInt(absoluteChunk.y));
    }

    Long2 ResolveAbsoluteChunkCoord(Vector3 runtimePosition)
    {
        AbsoluteUniversePosition viewerAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
        double3 absolutePosition = viewerAup.ToAbsoluteDouble3();
        double safeChunkSize = math.max(1d, (double)chunkSize);
        return new Long2(
            (long)math.floor(absolutePosition.x / safeChunkSize),
            (long)math.floor(absolutePosition.z / safeChunkSize));
    }

    double2 ResolveViewerAbsoluteXZ()
    {
        AbsoluteUniversePosition viewerAup = AbsoluteUniversePosition.FromRuntimePosition(viewer.position);
        double3 absolutePosition = viewerAup.ToAbsoluteDouble3();
        return new double2(absolutePosition.x, absolutePosition.z);
    }

    double2 ResolveChunkCenterAbsoluteXZ(int2 chunkCoord)
    {
        double safeChunkSize = math.max(1d, (double)chunkSize);
        return new double2(
            ((double)chunkCoord.x + 0.5d) * safeChunkSize,
            ((double)chunkCoord.y + 0.5d) * safeChunkSize);
    }

    float2 ChunkOrigin(int2 c)
    {
        double safeChunkSize = math.max(1d, (double)chunkSize);
        Vector3 runtimeOrigin = HectonFloatingOrigin.ToRuntimePosition(new Vector3(
            (float)(c.x * safeChunkSize),
            0f,
            (float)(c.y * safeChunkSize)));
        return new float2(runtimeOrigin.x, runtimeOrigin.z);
    }

    static int SaturateLongToInt(long value)
    {
        if (value > int.MaxValue)
            return int.MaxValue;

        if (value < int.MinValue)
            return int.MinValue;

        return (int)value;
    }

    private static int CompareChunkRequestsByDistance(HectonChunkRequest left, HectonChunkRequest right)
    {
        return left.distSq.CompareTo(right.distSq);
    }

    private static int CompareVoxelClustersByCountDescending(VoxelClusterAccumulator left, VoxelClusterAccumulator right)
    {
        return right.Count.CompareTo(left.Count);
    }

    void EnsurePoiVectorListPool()
    {
        if (_poiVectorListPoolReady)
            return;

        for (int i = _poiVectorListPool.Count; i < PoiListPoolCapacity; i++)
        {
            // COLD ALLOC: List<Vector3>[256] - pooled POI storage list, created during streaming startup only - owner: HectonWorldGenerator
            _poiVectorListPool.Add(new List<Vector3>(PoiListInitialCapacity));
        }

        _poiVectorListPoolReady = true;
    }

    List<Vector3> RentPoiList()
    {
        int lastIndex = _poiVectorListPool.Count - 1;
        if (lastIndex < 0)
            return null;

        List<Vector3> list = _poiVectorListPool[lastIndex];
        _poiVectorListPool.RemoveAt(lastIndex);
        list.Clear();
        return list;
    }

    void ReleasePoiList(List<Vector3> list)
    {
        if (list == null)
            return;

        list.Clear();
        if (_poiVectorListPool.Count < PoiListPoolCapacity)
            _poiVectorListPool.Add(list);
    }

    void ReleasePoiEntry(Dictionary<int2, List<Vector3>> store, int2 coord)
    {
        if (!store.TryGetValue(coord, out List<Vector3> list))
            return;

        store.Remove(coord);
        ReleasePoiList(list);
    }

    void ReleaseAllPoiLists()
    {
        var baseEnumerator = _poiBases.GetEnumerator();
        while (baseEnumerator.MoveNext())
            ReleasePoiList(baseEnumerator.Current.Value);
        _poiBases.Clear();

        var resourceEnumerator = _poiResources.GetEnumerator();
        while (resourceEnumerator.MoveNext())
            ReleasePoiList(resourceEnumerator.Current.Value);
        _poiResources.Clear();
    }

    void RefreshChunks()
    {
        double2 viewerAbsoluteXZ = ResolveViewerAbsoluteXZ();
        double activeRadiusSq = activeRadius * (double)activeRadius;
        double distantRadiusSq = distantRadius * (double)distantRadius;
        _desiredChunks.Clear();
        int rMax = Mathf.CeilToInt(distantRadius / chunkSize) + 1;

        for (int dz = -rMax; dz <= rMax; dz++)
        for (int dx = -rMax; dx <= rMax; dx++)
        {
            int2 c = _lastChunk + new int2(dx, dz);
            double2 center = ResolveChunkCenterAbsoluteXZ(c);
            double dSq = math.lengthsq(viewerAbsoluteXZ - center);

            if (dSq <= distantRadiusSq)
                _desiredChunks[c] = dSq <= activeRadiusSq ? 0 : 1;
        }

        _chunksToRemove.Clear();
        var activeEnumerator = _active.GetEnumerator();
        while (activeEnumerator.MoveNext())
        {
            var kvp = activeEnumerator.Current;
            if (!_desiredChunks.TryGetValue(kvp.Key, out int wantLod) || wantLod != kvp.Value.lod)
                _chunksToRemove.Add(kvp.Key);
        }
        for (int i = 0; i < _chunksToRemove.Count; i++)
        {
            int2 coord = _chunksToRemove[i];
            if (!_active.TryGetValue(coord, out HectonChunkData chunkToRemove))
                continue;

            DestroyChunk(chunkToRemove);
            _active.Remove(coord);
        }

        for (int i = _pendingChunks.Count - 1; i >= 0; i--)
        {
            var pc = _pendingChunks[i];
            if (!_desiredChunks.TryGetValue(pc.coord, out int wantLod) || wantLod != pc.lod)
            {
                pc.cancelRequested = true;
                _pendingChunks[i] = pc;
                continue;
            }

            pc.cancelRequested = false;
            _pendingChunks[i] = pc;
        }

        _pendingChunkCoordSet.Clear();
        for (int i = 0; i < _pendingChunks.Count; i++)
            _pendingChunkCoordSet.Add(_pendingChunks[i].coord);

        _requestScratch.Clear();
        var desiredEnumerator = _desiredChunks.GetEnumerator();
        while (desiredEnumerator.MoveNext())
        {
            var kvp = desiredEnumerator.Current;
            if (_active.ContainsKey(kvp.Key)) continue;
            if (_pendingChunkCoordSet.Contains(kvp.Key)) continue;

            double2 center = ResolveChunkCenterAbsoluteXZ(kvp.Key);
            _requestScratch.Add(new HectonChunkRequest
            {
                coord  = kvp.Key,
                lod    = kvp.Value,
                distSq = (float)math.lengthsq(viewerAbsoluteXZ - center)
            });
        }
        _requestScratch.Sort(_chunkRequestDistanceComparison);

        _queue.Clear();
        _queue.AddRange(_requestScratch);
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

            DispatcherJobSwap.TryComplete(ref pc.combinedHandle, forceComplete: false);

            var cd = pc.cancelRequested ? null : FinalizeChunk(pc);
            if (cd != null)
                _active[pc.coord] = cd;
            else if (pc.cancelRequested)
                pc.DisposeArrays();

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
            combinedHandle = h3,
            cancelRequested = false
        };

        pc.RegisterArrays();
        _pendingChunks.Add(pc);
    }

    HectonChunkData FinalizeChunk(PendingChunk pc)
    {
        try
        {
            int res = pc.resX;
            int vc  = res * pc.resZ;

            int maxTri = (res - 1) * (pc.resZ - 1) * 6;
            _triangleScratch.Clear();
            if (_triangleScratch.Capacity < maxTri)
                EnsureTriangleScratchCapacity(maxTri);

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
                    _triangleScratch.Add(i00);
                    _triangleScratch.Add(i01);
                    _triangleScratch.Add(i10);
                }

                if (!cutCaves || !(pc.caveB[i10] > 0 && pc.caveB[i01] > 0 && pc.caveB[i11] > 0))
                {
                    _triangleScratch.Add(i10);
                    _triangleScratch.Add(i01);
                    _triangleScratch.Add(i11);
                }
            }

            if (_triangleScratch.Count == 0) return null;

            var mesh = new Mesh();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            mesh.name = $"Hecton_{pc.coord.x}_{pc.coord.y}_L{pc.lod}";
#else
            mesh.name = RuntimeChunkObjectName;
#endif
            if (vc > 65535) mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(pc.verts);
            mesh.SetNormals(pc.norms);
            mesh.SetUVs(0, pc.uvs);
            mesh.SetColors(pc.cols);
            mesh.SetTriangles(_triangleScratch, 0, false);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var go = new GameObject(mesh.name);
#else
            var go = new GameObject(RuntimeChunkObjectName);
#endif
            go.transform.SetParent(transform, false);
            go.isStatic = true;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial    = terrainMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows    = true;

            MeshCollider meshCollider = null;
            if (pc.lod == 0 && generateColliders)
            {
                meshCollider = go.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = null;
                meshCollider.enabled = false;

                if (pendingCollisionBakeMaterial != null)
                    mr.sharedMaterial = pendingCollisionBakeMaterial;

                _pendingPhysicsBakes.Add(new PendingPhysicsBake
                {
                    Mesh = mesh,
                    Owner = go,
                    Renderer = mr,
                    Collider = meshCollider,
                    DefaultMaterial = terrainMaterial,
                    Handle = default,
                    State = PhysicsBakeStatePending
                });
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
                mesh  = mesh,
                renderer = mr,
                collider = meshCollider
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

        _voxelClusterIndexByCell.Clear();
        _voxelClusterScratch.Clear();

        float clusterSize = 24f;
        for (int i = 0; i < verts.Length; i++)
        {
            if (caveB[i] == 0)
                continue;

            Vector3 p = verts[i];
            int2 cell = new int2(
                Mathf.FloorToInt((p.x - chunkOrg.x) / clusterSize),
                Mathf.FloorToInt((p.z - chunkOrg.y) / clusterSize)
            );

            if (!_voxelClusterIndexByCell.TryGetValue(cell, out int clusterIndex))
            {
                clusterIndex = _voxelClusterScratch.Count;
                _voxelClusterIndexByCell[cell] = clusterIndex;
                _voxelClusterScratch.Add(new VoxelClusterAccumulator
                {
                    Cell = cell
                });
            }

            VoxelClusterAccumulator accumulator = _voxelClusterScratch[clusterIndex];
            accumulator.Count++;
            accumulator.SumX += p.x;
            accumulator.SumZ += p.z;
            _voxelClusterScratch[clusterIndex] = accumulator;
        }

        if (_voxelClusterScratch.Count > 0)
        {
            _voxelClusterScratch.Sort(_voxelClusterCountDescendingComparison);

            int spawned = 0;
            for (int ci = 0; ci < _voxelClusterScratch.Count && spawned < MaxVoxelsPerChunk; ci++)
            {
                VoxelClusterAccumulator cluster = _voxelClusterScratch[ci];
                if (cluster.Count < MinClusterVertices)
                    continue;

                float cx = cluster.SumX / cluster.Count;
                float cz = cluster.SumZ / cluster.Count;

                float floorHeight = GetWorldHeight(cx, cz);
                Vector3 spawnPos = new Vector3(cx, floorHeight - 15f, cz);

                uint caveSeed = (uint)(coord.x * 73856 ^ coord.y * 19349) + (uint)ci;
                _ = voxelEngine.GenerateVolumeAsync(spawnPos, caveSeed, null);
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

            uint riftSeed = (uint)(coord.x * 39384 ^ coord.y * 39483);
            _ = voxelEngine.GenerateVolumeAsync(riftCenter, riftSeed, null);
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
                if (bases == null)
                    bases = RentPoiList();

                if (bases != null)
                    bases.Add(p);
            }

            if (caveB[i] > 0)
            {
                if (res == null)
                    res = RentPoiList();

                if (res != null)
                    res.Add(p);
            }
        }

        ReleasePoiEntry(_poiBases, coord);
        ReleasePoiEntry(_poiResources, coord);

        if (bases != null && bases.Count > 0)
            _poiBases[coord] = bases;
        else
            ReleasePoiList(bases);

        if (res != null && res.Count > 0)
            _poiResources[coord] = res;
        else
            ReleasePoiList(res);
    }

    #endregion

    // ╔═══════════════════════════════════════════════╗
    // ║       PARALLEL PHYSICS BAKING                 ║
    // ╚═══════════════════════════════════════════════╝

    #region Physics

    static JobHandle ScheduleAsyncPhysicsBake(Mesh mesh)
    {
        return new HectonPhysicsBakeJob
        {
            MeshEntityId = mesh.GetEntityId()
        }.Schedule();
    }

    void BakePhysicsBatch()
    {
        using (_physicsBakeBatchProfilerMarker.Auto())
        {
            long batchStartTimestamp = Stopwatch.GetTimestamp();
            int scheduled = 0;
            while (_physicsBakeScheduleHead < _pendingPhysicsBakes.Count &&
                   scheduled < MAX_BAKES_PER_FRAME &&
                   !HasPhysicsBakeBudgetExpired(batchStartTimestamp))
            {
                PendingPhysicsBake pending = _pendingPhysicsBakes[_physicsBakeScheduleHead];
                if (pending.Mesh == null || pending.Owner == null)
                {
                    pending.State = PhysicsBakeStateCompleted;
                }
                else if (pending.State == PhysicsBakeStatePending)
                {
                    pending.Handle = ScheduleAsyncPhysicsBake(pending.Mesh);
                    pending.State = PhysicsBakeStateScheduled;
                    scheduled++;
                }

                _pendingPhysicsBakes[_physicsBakeScheduleHead] = pending;
                _physicsBakeScheduleHead++;
            }

            while (_physicsBakeFinalizeHead < _physicsBakeScheduleHead &&
                   !HasPhysicsBakeBudgetExpired(batchStartTimestamp))
            {
                PendingPhysicsBake pending = _pendingPhysicsBakes[_physicsBakeFinalizeHead];
                if (pending.State == PhysicsBakeStateCompleted)
                {
                    _physicsBakeFinalizeHead++;
                    continue;
                }

                if ((pending.State != PhysicsBakeStateScheduled && pending.State != PhysicsBakeStateCanceled) ||
                    !pending.Handle.IsCompleted)
                    break;

                DispatcherJobSwap.TryComplete(ref pending.Handle, forceComplete: false);

                if (pending.State == PhysicsBakeStateScheduled && pending.Mesh != null && pending.Owner != null)
                {
                    MeshCollider collider = pending.Collider;
                    if (collider != null)
                    {
                        collider.sharedMesh = pending.Mesh;
                        collider.enabled = true;
                    }

                    if (pending.Renderer != null && pending.DefaultMaterial != null)
                        pending.Renderer.sharedMaterial = pending.DefaultMaterial;
                }

                pending.State = PhysicsBakeStateCompleted;
                _pendingPhysicsBakes[_physicsBakeFinalizeHead] = pending;
                _physicsBakeFinalizeHead++;
            }

            if (_physicsBakeFinalizeHead >= _pendingPhysicsBakes.Count &&
                _physicsBakeScheduleHead >= _pendingPhysicsBakes.Count)
            {
                _pendingPhysicsBakes.Clear();
                _physicsBakeScheduleHead = 0;
                _physicsBakeFinalizeHead = 0;
            }
        }
    }

    void HandOffRemainingPhysicsBakesForTeardown()
    {
        for (int i = _pendingPhysicsBakes.Count - 1; i >= 0; i--)
        {
            PendingPhysicsBake pending = _pendingPhysicsBakes[i];
            if (pending.State == PhysicsBakeStateScheduled || pending.State == PhysicsBakeStateCanceled)
            {
                pending.State = PhysicsBakeStateCanceled;
                RestorePendingPhysicsBakePresentation(ref pending);
                EnqueueDeferredPhysicsBakeTeardown(in pending, pending.Mesh, pending.Owner);
            }
        }

        _pendingPhysicsBakes.Clear();
        _physicsBakeScheduleHead = 0;
        _physicsBakeFinalizeHead = 0;
    }

    bool TryCancelPendingPhysicsBake(Mesh mesh, GameObject owner)
    {
        bool hasInFlightBake = false;

        for (int i = _pendingPhysicsBakes.Count - 1; i >= 0; i--)
        {
            PendingPhysicsBake pending = _pendingPhysicsBakes[i];
            if (!MatchesPendingPhysicsBake(in pending, mesh, owner))
                continue;

            if (pending.State == PhysicsBakeStateScheduled || pending.State == PhysicsBakeStateCanceled)
            {
                pending.State = PhysicsBakeStateCanceled;
                _pendingPhysicsBakes[i] = pending;
                RestorePendingPhysicsBakePresentation(ref pending);

                hasInFlightBake = true;
                continue;
            }

            RestorePendingPhysicsBakePresentation(ref pending);
            RemovePendingPhysicsBakeAt(i);
        }

        return !hasInFlightBake;
    }

    bool HasInFlightPhysicsBake(Mesh mesh, GameObject owner)
    {
        for (int i = 0; i < _pendingPhysicsBakes.Count; i++)
        {
            PendingPhysicsBake pending = _pendingPhysicsBakes[i];
            if (!MatchesPendingPhysicsBake(in pending, mesh, owner))
                continue;

            if (pending.State == PhysicsBakeStateScheduled || pending.State == PhysicsBakeStateCanceled)
                return true;
        }

        return false;
    }

    private void RemovePendingPhysicsBakeAt(int index)
    {
        _pendingPhysicsBakes.RemoveAt(index);
        if (index < _physicsBakeScheduleHead)
            _physicsBakeScheduleHead--;
        if (index < _physicsBakeFinalizeHead)
            _physicsBakeFinalizeHead--;
        if (_physicsBakeScheduleHead < 0)
            _physicsBakeScheduleHead = 0;
        if (_physicsBakeFinalizeHead < 0)
            _physicsBakeFinalizeHead = 0;
    }

    private static void RestorePendingPhysicsBakePresentation(ref PendingPhysicsBake pending)
    {
        if (pending.Collider != null)
        {
            pending.Collider.enabled = false;
            pending.Collider.sharedMesh = null;
        }

        if (pending.Renderer != null && pending.DefaultMaterial != null)
            pending.Renderer.sharedMaterial = pending.DefaultMaterial;
    }

    private static void EnqueueDeferredPhysicsBakeTeardown(
        in PendingPhysicsBake pending,
        Mesh mesh,
        GameObject owner)
    {
        if (owner != null)
        {
            if (pending.Renderer != null)
                pending.Renderer.enabled = false;

            if (pending.Collider != null)
            {
                pending.Collider.enabled = false;
                pending.Collider.sharedMesh = null;
            }

            SystemDispatcher dispatcher = GlobalRegistry.Dispatcher;
            if (dispatcher != null)
                owner.transform.SetParent(dispatcher.transform, true);
        }

        _deferredPhysicsBakeTeardowns.Add(new DeferredPhysicsBakeTeardown
        {
            Mesh = mesh,
            Owner = owner,
            Renderer = pending.Renderer,
            Collider = pending.Collider,
            DefaultMaterial = pending.DefaultMaterial,
            Handle = pending.Handle
        });

        EnsureDeferredPhysicsBakeTeardownRegistered();
    }

    private static void EnsureDeferredPhysicsBakeTeardownRegistered()
    {
        if (_deferredPhysicsBakeTeardownRegistered ||
            !Application.isPlaying ||
            GlobalRegistry.Dispatcher == null)
            return;

        GlobalRegistry.RegisterLateFrameTickable(_deferredPhysicsBakeTeardownDriver, PriorityLayer.Environment);
        _deferredPhysicsBakeTeardownRegistered = SystemDispatcher
            .GetLateFrameLane(PriorityLayer.Environment)
            .Contains(_deferredPhysicsBakeTeardownDriver);
    }

    private static void DrainDeferredPhysicsBakeTeardowns()
    {
        int drained = 0;
        for (int i = _deferredPhysicsBakeTeardowns.Count - 1;
             i >= 0 && drained < DeferredPhysicsBakeTeardownDrainBudget;
             i--)
        {
            DeferredPhysicsBakeTeardown pending = _deferredPhysicsBakeTeardowns[i];
            if (!DispatcherJobSwap.TryComplete(ref pending.Handle, forceComplete: false))
                continue;

            if (pending.Collider != null)
            {
                pending.Collider.enabled = false;
                pending.Collider.sharedMesh = null;
            }

            if (pending.Renderer != null && pending.DefaultMaterial != null)
                pending.Renderer.sharedMaterial = pending.DefaultMaterial;

            if (pending.Mesh != null)
            {
                pending.Mesh.Clear();
                DestroyDeferredObject(pending.Mesh);
            }

            if (pending.Owner != null)
                DestroyDeferredObject(pending.Owner);

            RemoveDeferredPhysicsBakeTeardownAt(i);
            drained++;
        }

        if (_deferredPhysicsBakeTeardowns.Count == 0)
            UnregisterDeferredPhysicsBakeTeardownDriver();
    }

    private static void RemoveDeferredPhysicsBakeTeardownAt(int index)
    {
        int lastIndex = _deferredPhysicsBakeTeardowns.Count - 1;
        if (index != lastIndex)
            _deferredPhysicsBakeTeardowns[index] = _deferredPhysicsBakeTeardowns[lastIndex];

        _deferredPhysicsBakeTeardowns.RemoveAt(lastIndex);
    }

    private static void UnregisterDeferredPhysicsBakeTeardownDriver()
    {
        if (!_deferredPhysicsBakeTeardownRegistered)
            return;

        GlobalRegistry.UnregisterLateFrameTickable(_deferredPhysicsBakeTeardownDriver, PriorityLayer.Environment);
        _deferredPhysicsBakeTeardownRegistered = false;
    }

    private static void DestroyDeferredObject(Object obj)
    {
        if (obj == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(obj);
        else
            Destroy(obj);
#else
        Destroy(obj);
#endif
    }

    private static bool MatchesPendingPhysicsBake(in PendingPhysicsBake pending, Mesh mesh, GameObject owner)
    {
        bool meshMatches = mesh != null && ReferenceEquals(pending.Mesh, mesh);
        bool ownerMatches = owner != null && ReferenceEquals(pending.Owner, owner);
        return meshMatches || ownerMatches;
    }

    bool TryDeferChunkRetirement(HectonChunkData cd)
    {
        if (cd == null || !HasInFlightPhysicsBake(cd.mesh, cd.go))
            return false;

        if (!_deferredChunkRetirements.Contains(cd))
            _deferredChunkRetirements.Add(cd);

        if (cd.renderer != null)
            cd.renderer.enabled = false;

        if (cd.collider != null)
            cd.collider.enabled = false;

        return true;
    }

    void ProcessDeferredChunkRetirements(int maxRetirements)
    {
        int retired = 0;
        for (int i = _deferredChunkRetirements.Count - 1; i >= 0 && retired < maxRetirements; i--)
        {
            HectonChunkData cd = _deferredChunkRetirements[i];
            if (cd != null && HasInFlightPhysicsBake(cd.mesh, cd.go))
                continue;

            _deferredChunkRetirements.RemoveAt(i);
            DestroyChunkNow(cd);
            retired++;
        }
    }

    void ProcessAllDeferredChunkRetirements()
    {
        for (int i = _deferredChunkRetirements.Count - 1; i >= 0; i--)
        {
            HectonChunkData cd = _deferredChunkRetirements[i];
            if (cd != null && HasInFlightPhysicsBake(cd.mesh, cd.go))
                continue;

            _deferredChunkRetirements.RemoveAt(i);
            DestroyChunkNow(cd);
        }
    }

    void RetireDeferredChunksForStreamingStop()
    {
        for (int i = _deferredChunkRetirements.Count - 1; i >= 0; i--)
            RetireChunkForStreamingStop(_deferredChunkRetirements[i]);

        _deferredChunkRetirements.Clear();
    }

    void RetireChunkForStreamingStop(HectonChunkData cd)
    {
        if (cd == null)
            return;

        if (TryHandOffChunkPhysicsBakeForTeardown(cd))
        {
            ReleaseChunkWorldSideEffects(cd);
            return;
        }

        DestroyChunkNow(cd);
    }

    bool TryHandOffChunkPhysicsBakeForTeardown(HectonChunkData cd)
    {
        for (int i = _pendingPhysicsBakes.Count - 1; i >= 0; i--)
        {
            PendingPhysicsBake pending = _pendingPhysicsBakes[i];
            if (!MatchesPendingPhysicsBake(in pending, cd.mesh, cd.go))
                continue;

            if (pending.State == PhysicsBakeStateScheduled || pending.State == PhysicsBakeStateCanceled)
            {
                pending.State = PhysicsBakeStateCanceled;
                RestorePendingPhysicsBakePresentation(ref pending);
                EnqueueDeferredPhysicsBakeTeardown(in pending, cd.mesh, cd.go);
                RemovePendingPhysicsBakeAt(i);
                return true;
            }

            RestorePendingPhysicsBakePresentation(ref pending);
            RemovePendingPhysicsBakeAt(i);
            return false;
        }

        return false;
    }

    private static bool HasPhysicsBakeBudgetExpired(long batchStartTimestamp)
    {
        double elapsedMilliseconds = (Stopwatch.GetTimestamp() - batchStartTimestamp) * _physicsBakeTickToMilliseconds;
        return elapsedMilliseconds >= PhysicsBakeFrameBudgetMilliseconds;
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
    void DestroyChunk(HectonChunkData cd, bool allowDeferredRetirement = true)
    {
        if (cd == null) return;

        if (allowDeferredRetirement && TryDeferChunkRetirement(cd))
            return;

        DestroyChunkNow(cd);
    }

    void DestroyChunkNow(HectonChunkData cd)
    {
        if (cd == null) return;

        if (!TryCancelPendingPhysicsBake(cd.mesh, cd.go))
        {
            TryDeferChunkRetirement(cd);
            return;
        }

        ReleaseChunkWorldSideEffects(cd);
        DestroyChunkMeshObject(cd);
    }

    void ReleaseChunkWorldSideEffects(HectonChunkData cd)
    {
        if (cd == null)
            return;

        // ── Destroy child voxel volumes (v2.1: через DespawnVolume) ──
        if (voxelEngine != null)
        {
            float2 chunkOrigin = ChunkOrigin(cd.coord);
            voxelEngine.DespawnVolumesInsideXZ(
                chunkOrigin.x,
                chunkOrigin.x + chunkSize,
                chunkOrigin.y,
                chunkOrigin.y + chunkSize);
        }
        else if (cd.go != null)
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

        ReleasePoiEntry(_poiBases, cd.coord);
        ReleasePoiEntry(_poiResources, cd.coord);
    }

    void DestroyChunkMeshObject(HectonChunkData cd)
    {
        if (cd == null)
            return;

        if (cd.collider != null)
        {
            cd.collider.enabled = false;
            cd.collider.sharedMesh = null;
        }

        if (cd.renderer != null)
            cd.renderer.enabled = false;

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
            JobHandle previewVertexHandle = MakeVertexJob(res, res, -hs, -hs, previewSpacing, 1,
                          verts, uvs, caveV, caveB, biomeV)
                .Schedule(vc, JOB_BATCH);
            DispatcherJobSwap.TryComplete(ref previewVertexHandle, forceComplete: true);

#if UNITY_EDITOR
            EditorUtility.DisplayProgressBar("Hecton World Preview",
                "Computing normals...", 0.5f);
#endif
            JobHandle previewNormalHandle = new HectonNormalJob
            {
                resX = res, resZ = res,
                vertices = verts, normals = norms
            }.Schedule(vc, JOB_BATCH);
            DispatcherJobSwap.TryComplete(ref previewNormalHandle, forceComplete: true);

#if UNITY_EDITOR
            EditorUtility.DisplayProgressBar("Hecton World Preview",
                "Computing vertex colors...", 0.75f);
#endif
            JobHandle previewColorHandle = new HectonColorJob
            {
                maxDepth   = slopes.maxDepth,
                caveThresh = caves.threshold,
                caveEdge   = caves.edgeWidth,
                verts  = verts,  norms  = norms,
                cave   = caveV,  isCave = caveB,
                biome  = biomeV, colors = cols
            }.Schedule(vc, JOB_BATCH);
            DispatcherJobSwap.TryComplete(ref previewColorHandle, forceComplete: true);

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

            UnityEngine.Debug.Log($"[Hecton] Preview: {res}×{res} = {vc:N0} verts, " +
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

#if UNITY_EDITOR
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
#endif

    #endregion

    // ╔═══════════════════════════════════════════════╗
    // ║              UTILITIES                        ║
    // ╚═══════════════════════════════════════════════╝

    #region Utilities

    public int ActiveChunkCount => _active.Count;
    public int PendingChunkCount => _pendingChunks.Count;

    public int BaseLocationCount
    {
        get
        {
            int n = 0;
            var enumerator = _poiBases.GetEnumerator();
            while (enumerator.MoveNext())
                n += enumerator.Current.Value.Count;
            return n;
        }
    }

    public int ResourceNodeCount
    {
        get
        {
            int n = 0;
            var enumerator = _poiResources.GetEnumerator();
            while (enumerator.MoveNext())
                n += enumerator.Current.Value.Count;
            return n;
        }
    }

    public void GetAllBaseLocations(List<Vector3> result)
    {
        result.Clear();
        var enumerator = _poiBases.GetEnumerator();
        while (enumerator.MoveNext())
            result.AddRange(enumerator.Current.Value);
    }

    public void GetAllResourceNodes(List<Vector3> result)
    {
        result.Clear();
        var enumerator = _poiResources.GetEnumerator();
        while (enumerator.MoveNext())
            result.AddRange(enumerator.Current.Value);
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
        GlobalRegistry.UnregisterWorldSeedProvider(this);

        if (!Application.isPlaying && !_streaming && _pendingChunks.Count == 0 && !_lutsReady)
        {
            ClearPreview();
            DisposeLUTs();
            return;
        }

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
            UnityEngine.Debug.Log("[Hecton] LUTs rebaked from current curves.");
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
