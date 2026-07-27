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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Jobs;
using Unity.Burst;
using Unity.Burst.CompilerServices;
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
    public GameObject collisionProxyRoot;
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

    public static float SampleLUT(NativeArray<float>.ReadOnly lut, float t)
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

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

    [ReadOnly, NoAlias] public NativeArray<float> westLUT, eastLUT, biomeLUT;

    [WriteOnly, NoAlias] public NativeArray<Vector3> outVerts;
    [WriteOnly, NoAlias] public NativeArray<Vector2> outUVs;
    [WriteOnly, NoAlias] public NativeArray<float>   outCave;
    [WriteOnly, NoAlias] public NativeArray<byte>    outIsCave;
    [WriteOnly, NoAlias] public NativeArray<float>   outBiome;

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

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct HectonNormalJob : IJobParallelFor
{
    public int resX, resZ;
    [ReadOnly, NoAlias]  public NativeArray<Vector3> vertices;
    [WriteOnly, NoAlias] public NativeArray<Vector3> normals;

    public void Execute(int idx)
    {
        int lx = idx % resX;
        int lz = idx / resX;

        float3 L = vertices[lz * resX + math.max(lx - 1, 0)];
        float3 R = vertices[lz * resX + math.min(lx + 1, resX - 1)];
        float3 D = vertices[math.max(lz - 1, 0) * resX + lx];
        float3 U = vertices[math.min(lz + 1, resZ - 1) * resX + lx];

        float3 crossNormal = math.cross(U - D, R - L);
        float normalLengthSq = math.lengthsq(crossNormal);
        float3 resolvedNormal = crossNormal * math.rsqrt(math.max(normalLengthSq, 0.000001f));
        normals[idx] = math.select(new float3(0f, 1f, 0f), resolvedNormal, normalLengthSq > 0.000001f);
    }
}

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct HectonColorJob : IJobParallelFor
{
    public float maxDepth, caveThresh, caveEdge;

    [ReadOnly, NoAlias] public NativeArray<Vector3> verts;
    [ReadOnly, NoAlias] public NativeArray<Vector3> norms;
    [ReadOnly, NoAlias] public NativeArray<float>   cave;
    [ReadOnly, NoAlias] public NativeArray<byte>    isCave;
    [ReadOnly, NoAlias] public NativeArray<float>   biome;

    [WriteOnly, NoAlias] public NativeArray<Color> colors;

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

public class HectonWorldGenerator : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IWorldSeedProvider, IGlobalRegistryHotSwapListener
{
    private const int WorldGenerationAlgorithmVersionId = 1;
    private static readonly ProfilerMarker _tickProfilerMarker = new ProfilerMarker("H8.WorldGenerator.Tick");
    private static HectonWorldGenerator s_activeWorldSeedProvider;
    private static int s_activeRuntimeWorldSeed;
    private static bool s_activeRuntimeWorldSeedValid;
    public bool IsInitialized => _registeredWorldSeedProvider;
    public int RuntimeWorldSeed => ComputeRuntimeWorldSeed();
    public int RuntimeWorldGenerationVersionId => WorldGenerationAlgorithmVersionId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        s_activeWorldSeedProvider = null;
        s_activeRuntimeWorldSeed = 0;
        s_activeRuntimeWorldSeedValid = false;
    }

    public static bool TryGetActiveRuntimeWorldSeed(out int runtimeWorldSeed)
    {
        if (s_activeRuntimeWorldSeedValid && s_activeWorldSeedProvider != null)
        {
            runtimeWorldSeed = s_activeRuntimeWorldSeed;
            return true;
        }

        runtimeWorldSeed = 0;
        return false;
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
            hash = MixRuntimeSeed(hash, (int)math.round(chunkSize));
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

    public string SampleVoronoiBiome(Vector3 worldPos, Vector3[] biomeSeedPoints, string[] biomeTypes, float noiseBlend)
    {
        var pos = new System.Numerics.Vector3(worldPos.x, worldPos.y, worldPos.z);
        var seeds = new System.Numerics.Vector3[biomeSeedPoints != null ? biomeSeedPoints.Length : 0];
        if (biomeSeedPoints != null)
        {
            for (int i = 0; i < biomeSeedPoints.Length; i++)
            {
                seeds[i] = new System.Numerics.Vector3(biomeSeedPoints[i].x, biomeSeedPoints[i].y, biomeSeedPoints[i].z);
            }
        }
        return Hecton8.PureLogic.Systems.VoronoiBiomeSeedCalculator.Compute(pos, seeds, biomeTypes, noiseBlend);
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

    private struct VoxelClusterAccumulator
    {
        public int2 Cell;
        public int Count;
        public float SumX;
        public float SumZ;
    }

    const string RuntimeChunkObjectName = "HectonChunk";
    private const int TerrainCollisionProxyGridSegments = 4;
    private const float TerrainCollisionProxyMinSizeMeters = 1f;
    private const float TerrainCollisionProxyMinHeightMeters = 4f;
    private const float TerrainCollisionProxyMaxHeightMeters = 32f;
    private const float TerrainCollisionProxySkinMeters = 0.5f;

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
    [Tooltip("Generate coarse primitive terrain collision proxies for LOD0 streamed chunks. Runtime non-primitive collider cooking remains disabled.")]
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

    private const SystemID WorldGeneratorVaultOwner = SystemID.WorldStreaming;
    private const BufferID WestSlopeLutBufferId = BufferID.HectonWorldGeneratorWestSlopeLut;
    private const BufferID EastSlopeLutBufferId = BufferID.HectonWorldGeneratorEastSlopeLut;
    private const BufferID BiomeLutBufferId = BufferID.HectonWorldGeneratorBiomeLut;
    const int WorldStreamingQueueMaxCapacity = 512;
    const int PendingChunkMaxCapacity = 64;

    private ref struct PendingChunk
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
        public byte cancelRequested;

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

    private sealed class PendingChunkStore
    {
        private readonly int2[] _coords = new int2[PendingChunkMaxCapacity];
        private readonly int[] _lods = new int[PendingChunkMaxCapacity];
        private readonly int[] _resX = new int[PendingChunkMaxCapacity];
        private readonly int[] _resZ = new int[PendingChunkMaxCapacity];
        private readonly float[] _spacing = new float[PendingChunkMaxCapacity];
        private readonly NativeArray<Vector3>[] _verts = new NativeArray<Vector3>[PendingChunkMaxCapacity];
        private readonly NativeArray<Vector3>[] _norms = new NativeArray<Vector3>[PendingChunkMaxCapacity];
        private readonly NativeArray<Vector2>[] _uvs = new NativeArray<Vector2>[PendingChunkMaxCapacity];
        private readonly NativeArray<Color>[] _cols = new NativeArray<Color>[PendingChunkMaxCapacity];
        private readonly NativeArray<float>[] _caveV = new NativeArray<float>[PendingChunkMaxCapacity];
        private readonly NativeArray<byte>[] _caveB = new NativeArray<byte>[PendingChunkMaxCapacity];
        private readonly NativeArray<float>[] _biomeV = new NativeArray<float>[PendingChunkMaxCapacity];
        private readonly JobHandle[] _combinedHandles = new JobHandle[PendingChunkMaxCapacity];
        private readonly byte[] _cancelRequested = new byte[PendingChunkMaxCapacity];
        private int _count;

        public int Count => _count;

        public PendingChunk this[int index]
        {
            get
            {
                return new PendingChunk
                {
                    coord = _coords[index],
                    lod = _lods[index],
                    resX = _resX[index],
                    resZ = _resZ[index],
                    spacing = _spacing[index],
                    verts = _verts[index],
                    norms = _norms[index],
                    uvs = _uvs[index],
                    cols = _cols[index],
                    caveV = _caveV[index],
                    caveB = _caveB[index],
                    biomeV = _biomeV[index],
                    combinedHandle = _combinedHandles[index],
                    cancelRequested = _cancelRequested[index]
                };
            }
            set => Store(index, in value);
        }

        public bool TryAdd(PendingChunk chunk)
        {
            if (_count >= PendingChunkMaxCapacity)
                return false;

            Store(_count, in chunk);
            _count++;
            return true;
        }

        public void RemoveAt(int index)
        {
            if ((uint)index >= (uint)_count)
                return;

            int last = _count - 1;
            for (int i = index; i < last; i++)
                CopySlot(i + 1, i);

            ClearSlot(last);
            _count = last;
        }

        public void Clear()
        {
            for (int i = 0; i < _count; i++)
                ClearSlot(i);

            _count = 0;
        }

        private void Store(int index, in PendingChunk chunk)
        {
            _coords[index] = chunk.coord;
            _lods[index] = chunk.lod;
            _resX[index] = chunk.resX;
            _resZ[index] = chunk.resZ;
            _spacing[index] = chunk.spacing;
            _verts[index] = chunk.verts;
            _norms[index] = chunk.norms;
            _uvs[index] = chunk.uvs;
            _cols[index] = chunk.cols;
            _caveV[index] = chunk.caveV;
            _caveB[index] = chunk.caveB;
            _biomeV[index] = chunk.biomeV;
            _combinedHandles[index] = chunk.combinedHandle;
            _cancelRequested[index] = chunk.cancelRequested;
        }

        private void CopySlot(int source, int destination)
        {
            _coords[destination] = _coords[source];
            _lods[destination] = _lods[source];
            _resX[destination] = _resX[source];
            _resZ[destination] = _resZ[source];
            _spacing[destination] = _spacing[source];
            _verts[destination] = _verts[source];
            _norms[destination] = _norms[source];
            _uvs[destination] = _uvs[source];
            _cols[destination] = _cols[source];
            _caveV[destination] = _caveV[source];
            _caveB[destination] = _caveB[source];
            _biomeV[destination] = _biomeV[source];
            _combinedHandles[destination] = _combinedHandles[source];
            _cancelRequested[destination] = _cancelRequested[source];
        }

        private void ClearSlot(int index)
        {
            _coords[index] = default;
            _lods[index] = 0;
            _resX[index] = 0;
            _resZ[index] = 0;
            _spacing[index] = 0f;
            _verts[index] = default;
            _norms[index] = default;
            _uvs[index] = default;
            _cols[index] = default;
            _caveV[index] = default;
            _caveB[index] = default;
            _biomeV[index] = default;
            _combinedHandles[index] = default;
            _cancelRequested[index] = 0;
        }
    }

    // COLD ALLOC: Dictionary<int2,HectonChunkData>[512] - active streamed chunk lookup for residency refresh - owner: HectonWorldGenerator
    readonly Dictionary<int2, HectonChunkData> _active = new Dictionary<int2, HectonChunkData>(WorldStreamingQueueMaxCapacity);
    // COLD ALLOC: List<HectonChunkRequest>[512] - active streaming request queue, reused across refreshes - owner: HectonWorldGenerator
    readonly List<HectonChunkRequest> _queue = new List<HectonChunkRequest>(WorldStreamingQueueMaxCapacity);
    // COLD ALLOC: Dictionary<int2,int>[512] — reused desired-chunk set for AUP streaming refresh — owner: HectonWorldGenerator
    readonly Dictionary<int2, int> _desiredChunks = new Dictionary<int2, int>(WorldStreamingQueueMaxCapacity);
    // COLD ALLOC: List<int2>[256] — reused active-chunk removal scratch for AUP streaming refresh — owner: HectonWorldGenerator
    readonly List<int2> _chunksToRemove = new List<int2>(WorldStreamingQueueMaxCapacity);
    // COLD ALLOC: HashSet<int2>[256] — reused pending-chunk lookup for AUP streaming refresh — owner: HectonWorldGenerator
    readonly HashSet<int2> _pendingChunkCoordSet = new HashSet<int2>(PendingChunkMaxCapacity);
    // COLD ALLOC: List<HectonChunkRequest>[512] — reused chunk-request sort scratch for AUP streaming refresh — owner: HectonWorldGenerator
    readonly List<HectonChunkRequest> _requestScratch = new List<HectonChunkRequest>(WorldStreamingQueueMaxCapacity);
    int _queueHead;

    // COLD ALLOC: PendingChunkStore[1] - fixed SoA pending streamed chunk job store, no per-chunk managed allocation - owner: HectonWorldGenerator
    private readonly PendingChunkStore _pendingChunks = new PendingChunkStore();
    private JobHandle _pendingChunkOverflowDisposeHandle;
    private bool _pendingChunkOverflowDisposeActive;

    // COLD ALLOC: List<Renderer>[512] - renderer disable queue flushed in VISUAL_SYNC - owner: HectonWorldGenerator
    readonly List<Renderer> _pendingRendererDisables = new List<Renderer>(WorldStreamingQueueMaxCapacity);
    // COLD ALLOC: Dictionary<int2,int>[1024] - reused cave cell to accumulator index map for voxel POI finalization - owner: HectonWorldGenerator
    readonly Dictionary<int2, int> _voxelClusterIndexByCell = new Dictionary<int2, int>(1024);
    // COLD ALLOC: List<VoxelClusterAccumulator>[1024] - reused cave cluster accumulators, replaces per-chunk List/Dictionary allocations - owner: HectonWorldGenerator
    readonly List<VoxelClusterAccumulator> _voxelClusterScratch = new List<VoxelClusterAccumulator>(1024);
    const int PoiListPoolCapacity = 1024;
    const int PoiListInitialCapacity = 256;
    // COLD ALLOC: Dictionary<int2,List<Vector3>>[512] - active base POI lookup by chunk - owner: HectonWorldGenerator
    readonly Dictionary<int2, List<Vector3>> _poiBases = new Dictionary<int2, List<Vector3>>(512);
    // COLD ALLOC: Dictionary<int2,List<Vector3>>[512] - active resource POI lookup by chunk - owner: HectonWorldGenerator
    readonly Dictionary<int2, List<Vector3>> _poiResources = new Dictionary<int2, List<Vector3>>(512);
    // COLD ALLOC: List<List<Vector3>>[1024] - pooled POI vector lists retained for active streamed chunks - owner: HectonWorldGenerator
    readonly List<List<Vector3>> _poiVectorListPool = new List<List<Vector3>>(PoiListPoolCapacity);
    bool _poiVectorListPoolReady;

    IDataVault _worldGeneratorVault;
    VaultGenerationHandle<float> _westLutHandle;
    VaultGenerationHandle<float> _eastLutHandle;
    VaultGenerationHandle<float> _biomeLutHandle;
    bool _lutsReady;

    int2 _lastChunk = new int2(int.MinValue, int.MinValue);
    bool _streaming;
    bool _registeredToTickManager;
    bool _registeredToLateFrame;
    bool _registeredWorldSeedProvider;
    bool _registeredHotSwapListener;
    IPlayerRuntimeContext _playerRuntimeContext;

    [HideInInspector] public GameObject previewObj;

    const int LUT_RES   = 1024;
    const int JOB_BATCH = 64;
    const int DefaultTriangleScratchCapacity = 393216; // 256m chunk at 1m spacing: 256 * 256 * 6 indices.
    // COLD ALLOC: List<int>[393216] (~1536 KB) - reused terrain triangle index scratch, prevents per-chunk managed int[] allocations during streaming finalization - owner: HectonWorldGenerator
    readonly List<int> _triangleScratch = new List<int>(DefaultTriangleScratchCapacity);

    private static NativeArray<T> CreateTrackedNativeArray<T>(
        int length,
        string label,
        NativeArrayOptions options,
        Allocator allocator = Allocator.Persistent) where T : struct
    {
        NativeArray<T> array = H8Memory.Allocate<T>(length, WorldGeneratorVaultOwner, allocator, options);
        if (!array.IsCreated)
            throw new InvalidOperationException($"{nameof(HectonWorldGenerator)} native allocation failed for {label}.");

        return array;
    }

    private static void DisposeTrackedNativeArray<T>(ref NativeArray<T> array) where T : struct
    {
        if (!array.IsCreated)
            return;

        H8Memory.Release(ref array, WorldGeneratorVaultOwner);
    }

    private static void DisposeTrackedNativeArray<T>(
        ref NativeArray<T> array,
        ref JobHandle dependency,
        ref bool scheduledDisposal) where T : struct
    {
        if (!array.IsCreated)
            return;

        JobHandle releaseHandle = H8Memory.Release(ref array, dependency, WorldGeneratorVaultOwner);
        if (!array.IsCreated)
        {
            dependency = releaseHandle;
            scheduledDisposal = true;
        }
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

        RefreshColdRegistryReferences();
        TryRegisterHotSwapListener();
        GlobalRegistry.RegisterWorldSeedProvider(this);
        _registeredWorldSeedProvider = ReferenceEquals(GlobalRegistry.WorldSeedProvider, this);
        if (_registeredWorldSeedProvider)
            PublishActiveRuntimeWorldSeed();
        StartStreaming();
        RegisterToTickManager();
    }

    void OnDisable()
    {
        ClearActiveRuntimeWorldSeed();
        GlobalRegistry.UnregisterWorldSeedProvider(this);
        _registeredWorldSeedProvider = false;

        UnregisterFromTickManager();
        TryUnregisterHotSwapListener();

        if (Application.isPlaying || _streaming || _pendingChunks.Count > 0 || _lutsReady)
            StopStreaming();
    }

    private void PublishActiveRuntimeWorldSeed()
    {
        s_activeWorldSeedProvider = this;
        s_activeRuntimeWorldSeed = ComputeRuntimeWorldSeed();
        s_activeRuntimeWorldSeedValid = true;
    }

    private void ClearActiveRuntimeWorldSeed()
    {
        if (!ReferenceEquals(s_activeWorldSeedProvider, this))
            return;

        s_activeWorldSeedProvider = null;
        s_activeRuntimeWorldSeed = 0;
        s_activeRuntimeWorldSeedValid = false;
    }

    public void Tick(float deltaTime)
    {
        if (!IsInitialized)
            return;

        UpdateStreaming(deltaTime);
    }

    private void UpdateStreaming(float deltaTime)
    {
        using (_tickProfilerMarker.Auto())
        {
            if (!_streaming) return;

            if (!TryResolveViewerAup(out AbsoluteUniversePosition viewerAup))
                return;

            int2 cur = WorldToChunk(in viewerAup);
            if (!cur.Equals(_lastChunk))
            {
                _lastChunk = cur;
                RefreshChunks();
            }

            ProcessQueue();
        }
    }

    /// <summary>
    /// Drains completed chunk jobs in the dispatcher-owned late-frame swap window.
    /// </summary>
    public void LateFrameTick()
    {
        if (!IsInitialized)
            return;

        ProcessPendingChunks();
        DrainPendingChunkOverflowDisposals(forceComplete: false);
        FlushPendingRendererDisables();

    }

    void RegisterToTickManager()
    {
        if (!Application.isPlaying)
            return;

        if (!_registeredToTickManager)
            _registeredToTickManager = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);

        if (!_registeredToLateFrame)
            _registeredToLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
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

    void RefreshColdRegistryReferences()
    {
        _playerRuntimeContext = Hecton8.Core.GlobalRegistry.Player;
    }

    public void OnGlobalRegistryServiceReplaced(
        GlobalRegistryServiceSlot serviceSlot,
        object previousService,
        object currentService)
    {
        switch (serviceSlot)
        {
            case GlobalRegistryServiceSlot.Player:
                _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                break;
            case GlobalRegistryServiceSlot.Dispatcher:
                UnregisterFromTickManager();
                if (currentService != null)
                    RegisterToTickManager();
                break;
            case GlobalRegistryServiceSlot.DataVault:
                if (ReferenceEquals(_worldGeneratorVault, currentService))
                    break;

                CompletePendingChunkJobsForTeardown();
                DisposeLUTs();
                CacheWorldGeneratorVaultCold(currentService as IDataVault);
                if (_streaming)
                    EnsureLUTs();
                break;
        }
    }

    void TryRegisterHotSwapListener()
    {
        if (_registeredHotSwapListener || !Application.isPlaying)
            return;

        _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
    }

    void TryUnregisterHotSwapListener()
    {
        if (!_registeredHotSwapListener)
            return;

        GlobalRegistry.TryUnregisterHotSwapListener(this);
        _registeredHotSwapListener = false;
    }

    void StartStreaming()
    {
        if (viewer == null)
        {
#if UNITY_EDITOR
            Hecton8.Core.H8Debug.LogWarning("[Hecton] No viewer assigned.");
#endif
            return;
        }

        maxChunksPerFrame = math.clamp(maxChunksPerFrame, 1, PendingChunkMaxCapacity);
        maxPendingChunks = math.clamp(maxPendingChunks, 1, PendingChunkMaxCapacity);
        maxFinalizationsPerFrame = math.clamp(maxFinalizationsPerFrame, 1, PendingChunkMaxCapacity);
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
        DrainPendingChunkOverflowDisposals(forceComplete: true);

        var activeEnumerator = _active.GetEnumerator();
        while (activeEnumerator.MoveNext())
            DestroyChunkNow(activeEnumerator.Current.Value);
        _active.Clear();

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
                pc.cancelRequested = 1;
                _pendingChunks[i] = pc;
                continue;
            }

            DispatcherJobSwap.TryFinalizeCompleted(ref pc.combinedHandle);
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
            pc.cancelRequested = 1;
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
        if (_lutsReady && TryResolveLutViews(out _, out _, out _))
            return;

        DisposeLUTs();

        IDataVault vault = CacheWorldGeneratorVaultCold(GlobalRegistry.DataVault);
        if (vault == null)
            return;

        NativeArray<float> westSource = default;
        NativeArray<float> eastSource = default;
        NativeArray<float> biomeSource = default;
        try
        {
            westSource = BakeTemporaryLUT(slopes.westCurve);
            eastSource = BakeTemporaryLUT(slopes.eastCurve);
            biomeSource = BakeTemporaryLUT(biomes.biomeRemapCurve);

            bool westReady = EnsureLutBuffer(vault, WestSlopeLutBufferId, westSource, ref _westLutHandle);
            bool eastReady = EnsureLutBuffer(vault, EastSlopeLutBufferId, eastSource, ref _eastLutHandle);
            bool biomeReady = EnsureLutBuffer(vault, BiomeLutBufferId, biomeSource, ref _biomeLutHandle);
            _lutsReady = westReady && eastReady && biomeReady && TryResolveLutViews(out _, out _, out _);
            if (!_lutsReady)
                DisposeLUTs();
        }
        finally
        {
            if (westSource.IsCreated)
                westSource.Dispose();
            if (eastSource.IsCreated)
                eastSource.Dispose();
            if (biomeSource.IsCreated)
                biomeSource.Dispose();
        }
    }

    void DisposeLUTs()
    {
        if (!_lutsReady &&
            _westLutHandle.BufferID == 0u &&
            _eastLutHandle.BufferID == 0u &&
            _biomeLutHandle.BufferID == 0u)
        {
            return;
        }

        ReleaseLutHandle(ref _westLutHandle);
        ReleaseLutHandle(ref _eastLutHandle);
        ReleaseLutHandle(ref _biomeLutHandle);
        _lutsReady = false;
    }

    void DisposeLUTs(JobHandle dependency)
    {
        if (!_lutsReady &&
            _westLutHandle.BufferID == 0u &&
            _eastLutHandle.BufferID == 0u &&
            _biomeLutHandle.BufferID == 0u)
        {
            return;
        }

        JobHandle releaseDependency = dependency;
        DispatcherJobSwap.TryComplete(ref releaseDependency, forceComplete: true);
        DisposeLUTs();
    }

    private IDataVault CacheWorldGeneratorVaultCold(IDataVault vault)
    {
        if (!ReferenceEquals(_worldGeneratorVault, vault))
            _worldGeneratorVault = vault;

        return _worldGeneratorVault;
    }

    private static bool EnsureLutBuffer(
        IDataVault vault,
        BufferID bufferId,
        NativeArray<float> sourceLut,
        ref VaultGenerationHandle<float> handle)
    {
        if (!sourceLut.IsCreated || sourceLut.Length < LUT_RES)
            return false;

        handle = vault.EnsureGenerationHandle<float>(
            bufferId,
            LUT_RES,
            WorldGeneratorVaultOwner,
            NativeArrayOptions.UninitializedMemory);

        if (!vault.TryAcquireWriteLock(in handle, WorldGeneratorVaultOwner, out NativeArray<float> lut))
            return false;

        try
        {
            if (!lut.IsCreated || lut.Length < LUT_RES)
                return false;

            for (int i = 0; i < LUT_RES; i++)
                lut[i] = sourceLut[i];

            return true;
        }
        finally
        {
            vault.ReleaseWriteLock(in handle, WorldGeneratorVaultOwner);
        }
    }

    private void ReleaseLutHandle(ref VaultGenerationHandle<float> handle)
    {
        IDataVault vault = _worldGeneratorVault;
        if (vault != null && handle.BufferID != 0u)
            vault.ReleaseBuffer(in handle);

        handle = default;
    }

    private bool TryResolveLutViews(
        out NativeArray<float> westLut,
        out NativeArray<float> eastLut,
        out NativeArray<float> biomeLut)
    {
        westLut = default;
        eastLut = default;
        biomeLut = default;
        IDataVault vault = _worldGeneratorVault;
        return vault != null &&
               _westLutHandle.BufferID != 0u &&
               _eastLutHandle.BufferID != 0u &&
               _biomeLutHandle.BufferID != 0u &&
               vault.TryResolveHandle(in _westLutHandle, out westLut) &&
               westLut.IsCreated &&
               westLut.Length >= LUT_RES &&
               vault.TryResolveHandle(in _eastLutHandle, out eastLut) &&
               eastLut.IsCreated &&
               eastLut.Length >= LUT_RES &&
               vault.TryResolveHandle(in _biomeLutHandle, out biomeLut) &&
               biomeLut.IsCreated &&
               biomeLut.Length >= LUT_RES;
    }

    private bool TryReadLut(
        in VaultGenerationHandle<float> handle,
        out NativeArray<float>.ReadOnly lut)
    {
        lut = default;
        IDataVault vault = _worldGeneratorVault;
        return vault != null &&
               handle.BufferID != 0u &&
               vault.TryReadOnlyHandle(in handle, out lut) &&
               lut.Length >= LUT_RES;
    }

    private static NativeArray<float> BakeTemporaryLUT(AnimationCurve curve)
    {
        NativeArray<float> lut = CreateTrackedNativeArray<float>(
            LUT_RES,
            nameof(BakeTemporaryLUT),
            NativeArrayOptions.UninitializedMemory,
            Allocator.TempJob);
        FillLUT(lut, curve);
        return lut;
    }

    private static void FillLUT(NativeArray<float> lut, AnimationCurve curve)
    {
        for (int i = 0; i < LUT_RES; i++)
        {
            float t = (float)i / (LUT_RES - 1);
            lut[i] = (curve != null && curve.length > 0) ? curve.Evaluate(t) : (1f - t);
        }
    }

    private static float EvaluateCurve01(AnimationCurve curve, float t)
    {
        t = math.clamp(t, 0f, 1f);
        return curve != null && curve.length > 0 ? curve.Evaluate(t) : (1f - t);
    }

    public void RefreshLUTs()
    {
        DisposeLUTs();
        EnsureLUTs();
    }

    int ComputeConfiguredTriangleScratchRequirement()
    {
        float safeChunkSize = ResolveSafeChunkSize();
        float smallestSpacing = math.min(ResolveSafeLodSpacing(0), ResolveSafeLodSpacing(1));
        int res = ResolveChunkResolution(safeChunkSize, smallestSpacing);
        return ComputeTriangleIndexRequirement(res, res);
    }

    void EnsureTriangleScratchCapacity(int requiredTriangleIndices)
    {
        if (_triangleScratch.Capacity >= requiredTriangleIndices)
            return;

        if (_streaming)
            return;

        // COLD ALLOC: List<int>.Capacity[requiredTriangleIndices] - authoring setting exceeded default streamed triangle scratch; avoids repeated runtime chunk-finalize arrays - owner: HectonWorldGenerator
        _triangleScratch.Capacity = requiredTriangleIndices;
    }

    float ResolveSafeChunkSize()
    {
        return math.max(1f, chunkSize);
    }

    float ResolveSafeLodSpacing(int lod)
    {
        return math.max(0.001f, lod == 0 ? lod0Spacing : lod1Spacing);
    }

    private static int ResolveChunkResolution(float safeChunkSize, float safeSpacing)
    {
        double resolution = System.Math.Ceiling((double)math.max(1f, safeChunkSize) / (double)math.max(0.001f, safeSpacing)) + 1d;
        if (resolution > int.MaxValue)
            return int.MaxValue;
        if (resolution < 2d)
            return 2;

        return (int)resolution;
    }

    private static int ComputeTriangleIndexRequirement(int resX, int resZ)
    {
        long cellsX = math.max(0, resX - 1);
        long cellsZ = math.max(0, resZ - 1);
        return SaturateLongToInt(cellsX * cellsZ * 6L);
    }

    private static int ResolveChunkSearchRadius(double safeDistantRadius, double safeChunkSize)
    {
        double chunkRadius = System.Math.Ceiling(math.max(0d, safeDistantRadius) / math.max(1d, safeChunkSize)) + 1d;
        if (chunkRadius > WorldStreamingQueueMaxCapacity)
            return WorldStreamingQueueMaxCapacity;
        if (chunkRadius < 0d)
            return 0;

        return (int)chunkRadius;
    }

    #endregion

    // ╔═══════════════════════════════════════════════╗
    // ║             CHUNK STREAMING                   ║
    // ╚═══════════════════════════════════════════════╝

    #region Streaming

    int2 WorldToChunk(in AbsoluteUniversePosition viewerAup)
    {
        Long2 absoluteChunk = ResolveAbsoluteChunkCoord(in viewerAup);
        return new int2(SaturateLongToInt(absoluteChunk.x), SaturateLongToInt(absoluteChunk.y));
    }

    Long2 ResolveAbsoluteChunkCoord(in AbsoluteUniversePosition viewerAup)
    {
        double3 absolutePosition = viewerAup.ToAbsoluteDouble3();
        double safeChunkSize = math.max(1d, (double)chunkSize);
        return new Long2(
            (long)math.floor(absolutePosition.x / safeChunkSize),
            (long)math.floor(absolutePosition.z / safeChunkSize));
    }

    double2 ResolveViewerAbsoluteXZ()
    {
        if (!TryResolveViewerAup(out AbsoluteUniversePosition viewerAup))
            return double2.zero;

        double3 absolutePosition = viewerAup.ToAbsoluteDouble3();
        return new double2(absolutePosition.x, absolutePosition.z);
    }

    bool TryResolveViewerAup(out AbsoluteUniversePosition viewerAup)
    {
        IPlayerRuntimeContext playerContext = _playerRuntimeContext;
        if (playerContext != null &&
            playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
            snapshot.Aup.IsFinite())
        {
            viewerAup = snapshot.Aup;
            return true;
        }

        var playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
        if (playerMovement != null)
        {
            AbsoluteUniversePosition currentAup = playerMovement.CurrentAup;
            if (currentAup.IsFinite())
            {
                viewerAup = currentAup;
                return true;
            }
        }

        viewerAup = default;
        return false;
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
        double3 chunkOriginAup = new double3(
            (double)c.x * safeChunkSize,
            0d,
            (double)c.y * safeChunkSize);
        Vector3 runtimeOrigin = HectonFloatingOrigin.ToRuntimePosition(chunkOriginAup);
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
        double safeChunkSize = math.max(1d, (double)chunkSize);
        double safeActiveRadius = math.max(0d, (double)activeRadius);
        double safeDistantRadius = math.max(safeActiveRadius, math.max(0d, (double)distantRadius));
        double activeRadiusSq = safeActiveRadius * safeActiveRadius;
        double distantRadiusSq = safeDistantRadius * safeDistantRadius;
        _desiredChunks.Clear();
        int rMax = ResolveChunkSearchRadius(safeDistantRadius, safeChunkSize);

        for (int dz = -rMax; dz <= rMax; dz++)
        for (int dx = -rMax; dx <= rMax; dx++)
        {
            int2 c = _lastChunk + new int2(dx, dz);
            double2 center = ResolveChunkCenterAbsoluteXZ(c);
            double dSq = math.lengthsq(viewerAbsoluteXZ - center);

            if (dSq <= distantRadiusSq)
            {
                int lod = dSq <= activeRadiusSq ? 0 : 1;
                if (_desiredChunks.ContainsKey(c))
                    _desiredChunks[c] = lod;
                else if (_desiredChunks.Count < WorldStreamingQueueMaxCapacity)
                    _desiredChunks.Add(c, lod);
            }
        }

        _chunksToRemove.Clear();
        var activeEnumerator = _active.GetEnumerator();
        while (activeEnumerator.MoveNext())
        {
            var kvp = activeEnumerator.Current;
            if (!_desiredChunks.TryGetValue(kvp.Key, out int wantLod) || wantLod != kvp.Value.lod)
            {
                if (_chunksToRemove.Count < WorldStreamingQueueMaxCapacity)
                    _chunksToRemove.Add(kvp.Key);
            }
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
                pc.cancelRequested = 1;
                _pendingChunks[i] = pc;
                continue;
            }

            pc.cancelRequested = 0;
            _pendingChunks[i] = pc;
        }

        _pendingChunkCoordSet.Clear();
        for (int i = 0; i < _pendingChunks.Count; i++)
        {
            if (_pendingChunkCoordSet.Count < PendingChunkMaxCapacity)
                _pendingChunkCoordSet.Add(_pendingChunks[i].coord);
        }

        _requestScratch.Clear();
        var desiredEnumerator = _desiredChunks.GetEnumerator();
        while (desiredEnumerator.MoveNext())
        {
            var kvp = desiredEnumerator.Current;
            if (_active.ContainsKey(kvp.Key)) continue;
            if (_pendingChunkCoordSet.Contains(kvp.Key)) continue;

            double2 center = ResolveChunkCenterAbsoluteXZ(kvp.Key);
            if (_requestScratch.Count >= WorldStreamingQueueMaxCapacity)
                continue;

            _requestScratch.Add(new HectonChunkRequest
            {
                coord  = kvp.Key,
                lod    = kvp.Value,
                distSq = (float)math.lengthsq(viewerAbsoluteXZ - center)
            });
        }
        _requestScratch.Sort(_chunkRequestDistanceComparison);

        _queue.Clear();
        for (int i = 0; i < _requestScratch.Count && _queue.Count < WorldStreamingQueueMaxCapacity; i++)
            _queue.Add(_requestScratch[i]);
        _queueHead = 0;
    }

    void ProcessQueue()
    {
        int scheduled = 0;
        while (_queueHead < _queue.Count
               && scheduled < maxChunksPerFrame
               && _pendingChunks.Count < maxPendingChunks
               && _pendingChunks.Count < PendingChunkMaxCapacity)
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

            DispatcherJobSwap.TryFinalizeCompleted(ref pc.combinedHandle);

            var cd = pc.cancelRequested != 0 ? null : FinalizeChunk(pc);
            if (cd != null)
            {
                if (_active.ContainsKey(pc.coord) || _active.Count < WorldStreamingQueueMaxCapacity)
                    _active[pc.coord] = cd;
                else
                    DestroyChunk(cd);
            }
            else if (pc.cancelRequested != 0)
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
        if (_pendingChunks.Count >= PendingChunkMaxCapacity)
            return;

        EnsureLUTs();
        if (!TryResolveLutViews(out NativeArray<float> westLut, out NativeArray<float> eastLut, out NativeArray<float> biomeLut))
            return;

        float sp  = ResolveSafeLodSpacing(lod);
        int   res = ResolveChunkResolution(ResolveSafeChunkSize(), sp);
        long vertexCount = (long)res * res;
        if (vertexCount > int.MaxValue)
            return;

        int   vc  = (int)vertexCount;
        float2 org = ChunkOrigin(coord);

        NativeArray<Vector3> verts = default;
        NativeArray<Vector3> norms = default;
        NativeArray<Vector2> uvs = default;
        NativeArray<Color> cols = default;
        NativeArray<float> caveV = default;
        NativeArray<byte> caveB = default;
        NativeArray<float> biomeV = default;

        try
        {
            verts = CreateTrackedNativeArray<Vector3>(vc, nameof(PendingChunk.verts), NativeArrayOptions.UninitializedMemory);
            norms = CreateTrackedNativeArray<Vector3>(vc, nameof(PendingChunk.norms), NativeArrayOptions.UninitializedMemory);
            uvs = CreateTrackedNativeArray<Vector2>(vc, nameof(PendingChunk.uvs), NativeArrayOptions.UninitializedMemory);
            cols = CreateTrackedNativeArray<Color>(vc, nameof(PendingChunk.cols), NativeArrayOptions.UninitializedMemory);
            caveV = CreateTrackedNativeArray<float>(vc, nameof(PendingChunk.caveV), NativeArrayOptions.UninitializedMemory);
            caveB = CreateTrackedNativeArray<byte>(vc, nameof(PendingChunk.caveB), NativeArrayOptions.UninitializedMemory);
            biomeV = CreateTrackedNativeArray<float>(vc, nameof(PendingChunk.biomeV), NativeArrayOptions.UninitializedMemory);
        }
        catch
        {
            DisposeTrackedNativeArray(ref verts);
            DisposeTrackedNativeArray(ref norms);
            DisposeTrackedNativeArray(ref uvs);
            DisposeTrackedNativeArray(ref cols);
            DisposeTrackedNativeArray(ref caveV);
            DisposeTrackedNativeArray(ref caveB);
            DisposeTrackedNativeArray(ref biomeV);
            throw;
        }

        var vertexJob = MakeVertexJob(res, res, org.x, org.y, sp, lod,
                                       westLut, eastLut, biomeLut,
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
            cancelRequested = 0
        };

        if (_pendingChunks.Count >= PendingChunkMaxCapacity || !_pendingChunks.TryAdd(pc))
        {
            JobHandle disposalHandle = pc.DisposeArrays(pc.combinedHandle);
            AccumulatePendingChunkOverflowDisposal(disposalHandle);
            JobHandle.ScheduleBatchedJobs();
        }
    }

    private void AccumulatePendingChunkOverflowDisposal(JobHandle disposalHandle)
    {
        DrainPendingChunkOverflowDisposals(forceComplete: false);
        _pendingChunkOverflowDisposeHandle = _pendingChunkOverflowDisposeActive
            ? JobHandle.CombineDependencies(_pendingChunkOverflowDisposeHandle, disposalHandle)
            : disposalHandle;
        _pendingChunkOverflowDisposeActive = true;
    }

    private void DrainPendingChunkOverflowDisposals(bool forceComplete)
    {
        if (!_pendingChunkOverflowDisposeActive)
            return;

        bool completed = forceComplete
            ? DispatcherJobSwap.TryComplete(ref _pendingChunkOverflowDisposeHandle, forceComplete: true)
            : DispatcherJobSwap.TryFinalizeCompleted(ref _pendingChunkOverflowDisposeHandle);
        if (completed)
            _pendingChunkOverflowDisposeActive = false;
    }

    HectonChunkData FinalizeChunk(PendingChunk pc)
    {
        try
        {
            int res = pc.resX;
            int vc  = res * pc.resZ;

            int maxTri = ComputeTriangleIndexRequirement(res, pc.resZ);
            _triangleScratch.Clear();
            if (_triangleScratch.Capacity < maxTri)
            {
                return null;
            }

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
            mesh.name = RuntimeChunkObjectName;
            if (vc > 65535) mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(pc.verts);
            mesh.SetNormals(pc.norms);
            mesh.SetUVs(0, pc.uvs);
            mesh.SetColors(pc.cols);
            mesh.SetTriangles(_triangleScratch, 0, false);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            var go = new GameObject(RuntimeChunkObjectName);
            go.layer = HectonLayerMasks.Terrain;
            go.transform.SetParent(transform, false);
            go.isStatic = true;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial    = terrainMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows    = true;

            GameObject terrainCollisionProxyRoot = null;
            if (pc.lod == 0 && generateColliders)
            {
                terrainCollisionProxyRoot = CreateTerrainCollisionProxyRoot(
                    go,
                    pc.verts,
                    pc.caveB,
                    pc.resX,
                    pc.resZ,
                    mesh.bounds);
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
                collisionProxyRoot = terrainCollisionProxyRoot
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
                                   NativeArray<float> westLut,
                                   NativeArray<float> eastLut,
                                   NativeArray<float> biomeLut,
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

            westLUT  = westLut,
            eastLUT  = eastLut,
            biomeLUT = biomeLut,

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

        // Nothing ever assigned this field - not a prefab, not code - so this method has returned on
        // its first line for the whole life of the project and no voxel cave or rift has ever spawned.
        // HectonVoxelEngine self-installs and registers itself with GlobalRegistry, so resolve it the
        // way the rest of the project resolves services. Lazy and repeated on purpose: Unity reports a
        // destroyed engine as null, so a hot-swapped one is picked up on the next chunk rather than
        // leaving a dangling reference behind. The guard below still stands for the frames before the
        // engine exists, and ReleaseChunkWorldSideEffects keeps its own name-based fallback.
        if (voxelEngine == null)
            voxelEngine = GlobalRegistry.VoxelEngine;

        if (voxelEngine == null) return;

        float2 chunkOrg = ChunkOrigin(coord);
        float safeChunkSize = ResolveSafeChunkSize();
        float chunkCenterX = chunkOrg.x + safeChunkSize * 0.5f;
        float chunkCenterZ = chunkOrg.y + safeChunkSize * 0.5f;

        _voxelClusterIndexByCell.Clear();
        _voxelClusterScratch.Clear();

        float clusterSize = 24f;
        for (int i = 0; i < verts.Length; i++)
        {
            if (caveB[i] == 0)
                continue;

            Vector3 p = verts[i];
            int2 cell = new int2(
                (int)math.floor((p.x - chunkOrg.x) / clusterSize),
                (int)math.floor((p.z - chunkOrg.y) / clusterSize)
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
        int step = math.max(1, verts.Length / 256);

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
    // ║       TERRAIN COLLISION PROXIES               ║
    // ╚═══════════════════════════════════════════════╝

    #region Physics

    private static GameObject CreateTerrainCollisionProxyRoot(
        GameObject owner,
        NativeArray<Vector3> vertices,
        NativeArray<byte> caveMask,
        int resX,
        int resZ,
        Bounds fallbackBounds)
    {
        if (owner == null)
            return null;

        owner.layer = HectonLayerMasks.Terrain;
        GameObject proxyRoot = new GameObject("COL_TerrainProxy_Runtime");
        proxyRoot.layer = HectonLayerMasks.Terrain;
        proxyRoot.isStatic = true;
        proxyRoot.transform.SetParent(owner.transform, false);

        bool canTile =
            vertices.IsCreated &&
            resX >= 2 &&
            resZ >= 2 &&
            (long)vertices.Length >= (long)resX * resZ;

        if (!canTile)
        {
            AddTerrainCollisionProxyBox(proxyRoot, fallbackBounds);
            return proxyRoot;
        }

        int segmentsX = math.clamp(TerrainCollisionProxyGridSegments, 1, resX - 1);
        int segmentsZ = math.clamp(TerrainCollisionProxyGridSegments, 1, resZ - 1);
        bool hasAnyProxy = false;
        bool hasCaveMask = caveMask.IsCreated && caveMask.Length >= vertices.Length;

        for (int segmentZ = 0; segmentZ < segmentsZ; segmentZ++)
        for (int segmentX = 0; segmentX < segmentsX; segmentX++)
        {
            int x0 = (segmentX * (resX - 1)) / segmentsX;
            int x1 = ((segmentX + 1) * (resX - 1)) / segmentsX;
            int z0 = (segmentZ * (resZ - 1)) / segmentsZ;
            int z1 = ((segmentZ + 1) * (resZ - 1)) / segmentsZ;

            if (TryBuildTerrainCollisionTileBounds(
                    vertices,
                    caveMask,
                    hasCaveMask,
                    resX,
                    x0,
                    x1,
                    z0,
                    z1,
                    out Bounds tileBounds))
            {
                AddTerrainCollisionProxyBox(proxyRoot, tileBounds);
                hasAnyProxy = true;
            }
        }

        if (!hasAnyProxy)
        {
            UnityEngine.Object.Destroy(proxyRoot);
            return null;
        }

        return proxyRoot;
    }

    private static bool TryBuildTerrainCollisionTileBounds(
        NativeArray<Vector3> vertices,
        NativeArray<byte> caveMask,
        bool hasCaveMask,
        int resX,
        int x0,
        int x1,
        int z0,
        int z1,
        out Bounds bounds)
    {
        bounds = default;
        bool hasPoint = false;
        bool allCave = hasCaveMask;

        for (int z = z0; z <= z1; z++)
        for (int x = x0; x <= x1; x++)
        {
            int index = z * resX + x;
            if ((uint)index >= (uint)vertices.Length)
                continue;

            Vector3 vertex = vertices[index];
            if (!math.isfinite(vertex.x) ||
                !math.isfinite(vertex.y) ||
                !math.isfinite(vertex.z))
            {
                continue;
            }

            if (hasCaveMask && caveMask[index] == 0)
                allCave = false;

            if (!hasPoint)
            {
                bounds = new Bounds(vertex, Vector3.zero);
                hasPoint = true;
            }
            else
            {
                bounds.Encapsulate(vertex);
            }
        }

        return hasPoint && !allCave;
    }

    private static void AddTerrainCollisionProxyBox(GameObject proxyRoot, Bounds bounds)
    {
        if (proxyRoot == null)
            return;

        BoxCollider proxy = proxyRoot.AddComponent<BoxCollider>();
        Vector3 boundsSize = bounds.size;
        float sizeX = math.max(boundsSize.x + TerrainCollisionProxySkinMeters * 2f, TerrainCollisionProxyMinSizeMeters);
        float sizeZ = math.max(boundsSize.z + TerrainCollisionProxySkinMeters * 2f, TerrainCollisionProxyMinSizeMeters);
        float height = math.clamp(
            boundsSize.y + TerrainCollisionProxySkinMeters * 2f,
            TerrainCollisionProxyMinHeightMeters,
            TerrainCollisionProxyMaxHeightMeters);
        float topY = bounds.max.y + TerrainCollisionProxySkinMeters;

        proxy.center = new Vector3(bounds.center.x, topY - height * 0.5f, bounds.center.z);
        proxy.size = new Vector3(sizeX, height, sizeZ);
        proxy.isTrigger = false;
        proxy.enabled = true;
    }

    private static void DisableTerrainCollisionProxyRoot(GameObject proxyRoot)
    {
        if (proxyRoot != null)
            proxyRoot.SetActive(false);
    }

    #endregion

    // ╔═══════════════════════════════════════════════╗
    // ║           CHUNK LIFECYCLE (v2.1 FIX)          ║
    // ╚═══════════════════════════════════════════════╝

    #region Chunk Lifecycle

    /// <summary>
    /// Destroys a chunk and all its associated voxel volumes.
    ///
    /// v2.1 Patch: Voxel child objects are now destroyed through
    /// voxelEngine.DespawnVolume() instead of direct SafeDestroy().
    ///
    /// BYLO (v2.0, UTEChKA):
    ///   SafeDestroy(child.gameObject)
    ///   → HectonVoxelEngine._activeVolumes[i] becomes null
    ///   → null accumulates infinitely
    ///   → O(n) degradation in DespawnVolume/ClearAll
    ///
    /// STALO (v2.1, KORREKTNO):
    ///   voxelEngine.DespawnVolume(child.gameObject)
    ///   → removes from _activeVolumes
    ///   → cleans mesh + collider
    ///   → returns to pool or destroys
    ///   → zero null references
    ///
    /// FALLBACK: esli voxelEngine == null (unichtozhen ranshe,
    /// smena stseny), ispolzuetsya pryamoy SafeDestroy (kak v v2.0).
    /// Eto bezopasno — _activeVolumes tozhe unichtozhen vmeste s engine.
    /// </summary>
    void DestroyChunk(HectonChunkData cd)
    {
        if (cd == null) return;

        DestroyChunkNow(cd);
    }

    void DestroyChunkNow(HectonChunkData cd)
    {
        if (cd == null) return;

        ReleaseChunkWorldSideEffects(cd);
        DestroyChunkMeshObject(cd);
    }

    void ReleaseChunkWorldSideEffects(HectonChunkData cd)
    {
        if (cd == null)
            return;

        // ── Destroy child voxel volumes (v2.1: cherez DespawnVolume) ──
        if (voxelEngine != null)
        {
            double safeChunkSize = math.max(1d, (double)chunkSize);
            double chunkMinX = (double)cd.coord.x * safeChunkSize;
            double chunkMinZ = (double)cd.coord.y * safeChunkSize;
            voxelEngine.DespawnVolumesInsideAbsoluteXZ(
                chunkMinX,
                chunkMinX + safeChunkSize,
                chunkMinZ,
                chunkMinZ + safeChunkSize);
        }
        else if (cd.go != null)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                string childName = child.name;
                if (childName.StartsWith("Cave_") ||
                    childName.StartsWith("RuntimeCave") ||
                    childName.StartsWith("Voxel_Cave_") ||
                    childName.StartsWith("Voxel_Rift_"))
                {
                    // v2.1: Delegiruem ochistku VoxelEngine.
                    // DespawnVolume udalyaet iz _activeVolumes,
                    // chistit mesh/collider, vozvraschaet v pul.
                    if (voxelEngine != null)
                    {
                        voxelEngine.DespawnVolume(child.gameObject);
                    }
                    else
                    {
                        // Fallback: engine uzhe unichtozhen (smena stseny).
                        // Ruchnaya ochistka kak v v2.0.
                        child.TryGetComponent(out MeshFilter mf);
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
        DisableTerrainCollisionProxyRoot(cd.collisionProxyRoot);
    }

    void DestroyChunkMeshObject(HectonChunkData cd)
    {
        if (cd == null)
            return;

        DisableTerrainCollisionProxyRoot(cd.collisionProxyRoot);

        if (cd.renderer != null)
            QueueRendererDisable(cd.renderer);

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
        previewObj.TryGetComponent(out MeshFilter mf);
        if (mf != null && mf.sharedMesh != null)
            SafeDestroy(mf.sharedMesh);
        SafeDestroy(previewObj);
        previewObj = null;
    }

    void QueueRendererDisable(Renderer renderer)
    {
        if (renderer == null)
            return;

        if (!_pendingRendererDisables.Contains(renderer) &&
            _pendingRendererDisables.Count < WorldStreamingQueueMaxCapacity)
        {
            _pendingRendererDisables.Add(renderer);
        }
    }

    void FlushPendingRendererDisables()
    {
        for (int i = _pendingRendererDisables.Count - 1; i >= 0; i--)
        {
            Renderer renderer = _pendingRendererDisables[i];
            if (renderer != null)
                renderer.enabled = false;
        }

        _pendingRendererDisables.Clear();
    }

    void SafeDestroy(UnityEngine.Object obj)
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
        NoiseData bioND = NoiseData.From(biomes.biomeNoise);
        float bRaw = HectonNoise.Fractal2D(x, z, bioND);
        return TryReadLut(in _biomeLutHandle, out NativeArray<float>.ReadOnly biomeLut)
            ? HectonNoise.SampleLUT(biomeLut, bRaw)
            : EvaluateCurve01(biomes.biomeRemapCurve, bRaw);
    }

    public float GetWorldHeight(float x, float z)
    {
        NoiseData warpND = NoiseData.From(spine.warpNoise);
        NoiseData islND  = NoiseData.From(spine.islandNoise);
        NoiseData bioND  = NoiseData.From(biomes.biomeNoise);
        NoiseData flatND = NoiseData.From(biomes.flatSurfaceNoise);
        NoiseData aggrND = NoiseData.From(biomes.aggressiveSurfaceNoise);
        bool hasWestLut = TryReadLut(in _westLutHandle, out NativeArray<float>.ReadOnly westLut);
        bool hasEastLut = TryReadLut(in _eastLutHandle, out NativeArray<float>.ReadOnly eastLut);
        bool hasBiomeLut = TryReadLut(in _biomeLutHandle, out NativeArray<float>.ReadOnly biomeLut);

        float hs = mapSize * 0.5f;

        float warpVal = HectonNoise.Fractal2D(0f, z, warpND);
        float spineCX = (warpVal * 2f - 1f) * spine.warpStrength;

        float dx    = x - spineCX;
        float absDx = math.abs(dx);
        bool  west  = dx < 0f;

        float sLen  = west ? slopes.westLength : slopes.eastLength;
        float normD = math.saturate(absDx / math.max(sLen, 1f));
        float curveV;

        if (west)
            curveV = hasWestLut ? HectonNoise.SampleLUT(westLut, normD) : EvaluateCurve01(slopes.westCurve, normD);
        else
            curveV = hasEastLut ? HectonNoise.SampleLUT(eastLut, normD) : EvaluateCurve01(slopes.eastCurve, normD);

        if (west && slopes.terraceCount > 0 && slopes.terraceStrength > 0f)
        {
            float tc      = (float)slopes.terraceCount;
            float stepped = math.round(curveV * tc) / tc;
            curveV = math.lerp(curveV, stepped, slopes.terraceStrength);
        }

        float floor = math.lerp(-slopes.maxDepth, 0f, curveV);

        float spineInf = math.saturate(1f - absDx / math.max(spine.width, 1f));
        spineInf *= spineInf;
        float islN = HectonNoise.Fractal2D(x * 0.1f, z, islND);
        float islF = math.saturate(
            (islN - spine.islandThreshold) /
            math.max(1f - spine.islandThreshold, 0.01f));
        float spineElev = spine.maxHeight * spineInf * islF;

        float bRaw = HectonNoise.Fractal2D(x, z, bioND);
        float bVal = hasBiomeLut ? HectonNoise.SampleLUT(biomeLut, bRaw) : EvaluateCurve01(biomes.biomeRemapCurve, bRaw);
        float fltN = HectonNoise.Fractal2D(x, z, flatND);
        float agrN = HectonNoise.Fractal2D(x, z, aggrND);
        float surfY = (math.lerp(fltN, agrN, bVal) * 2f - 1f) *
                       math.lerp(biomes.flatSurfaceAmplitude,
                                 biomes.aggressiveSurfaceAmplitude, bVal);

        float y = floor + spineElev + surfY;

        float fadeX = math.saturate((hs - math.abs(x)) / 1000f);
        float fadeZ = math.saturate((hs - math.abs(z)) / 1000f);
        y = math.lerp(-slopes.maxDepth, y, fadeX * fadeZ);

        return y;
    }

    public void Initialize()
    {
        EnsureLUTs();
    }

    [ContextMenu("⟳ Generate Preview")]
    public void GenerateWorldPreview()
    {
        if (Application.isPlaying)
            return;

        ClearAll();
        Initialize();
        bool ownsPreviewLuts = false;
        NativeArray<float> westLut;
        NativeArray<float> eastLut;
        NativeArray<float> biomeLut;
        if (!TryResolveLutViews(out westLut, out eastLut, out biomeLut))
        {
            westLut = BakeTemporaryLUT(slopes.westCurve);
            eastLut = BakeTemporaryLUT(slopes.eastCurve);
            biomeLut = BakeTemporaryLUT(biomes.biomeRemapCurve);
            ownsPreviewLuts = true;
        }

        const float previewSpacing = 100f;
        float hs  = mapSize * 0.5f;
        int   res = Mathf.CeilToInt(mapSize / previewSpacing) + 1;
        int   vc  = res * res;

        NativeArray<Vector3> verts = CreateTrackedNativeArray<Vector3>(
            vc,
            nameof(PendingChunk.verts),
            NativeArrayOptions.UninitializedMemory,
            Allocator.TempJob);
        NativeArray<Vector3> norms = CreateTrackedNativeArray<Vector3>(
            vc,
            nameof(PendingChunk.norms),
            NativeArrayOptions.UninitializedMemory,
            Allocator.TempJob);
        NativeArray<Vector2> uvs = CreateTrackedNativeArray<Vector2>(
            vc,
            nameof(PendingChunk.uvs),
            NativeArrayOptions.UninitializedMemory,
            Allocator.TempJob);
        NativeArray<Color> cols = CreateTrackedNativeArray<Color>(
            vc,
            nameof(PendingChunk.cols),
            NativeArrayOptions.UninitializedMemory,
            Allocator.TempJob);
        NativeArray<float> caveV = CreateTrackedNativeArray<float>(
            vc,
            nameof(PendingChunk.caveV),
            NativeArrayOptions.UninitializedMemory,
            Allocator.TempJob);
        NativeArray<byte> caveB = CreateTrackedNativeArray<byte>(
            vc,
            nameof(PendingChunk.caveB),
            NativeArrayOptions.UninitializedMemory,
            Allocator.TempJob);
        NativeArray<float> biomeV = CreateTrackedNativeArray<float>(
            vc,
            nameof(PendingChunk.biomeV),
            NativeArrayOptions.UninitializedMemory,
            Allocator.TempJob);

        try
        {
#if UNITY_EDITOR
            EditorUtility.DisplayProgressBar("Hecton World Preview",
                $"Generating {res}×{res} vertices...", 0.2f);
#endif
            JobHandle previewVertexHandle = MakeVertexJob(res, res, -hs, -hs, previewSpacing, 1,
                          westLut, eastLut, biomeLut,
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
            mesh.SetTriangles(tris, 0);
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

#if UNITY_EDITOR
            Hecton8.Core.H8Debug.Log($"[Hecton] Preview: {res}×{res} = {vc:N0} verts, " +
                      $"{tc / 3:N0} tris. Bounds: {mesh.bounds.size}");
#endif
        }
        finally
        {
            DisposeTrackedNativeArray(ref verts);
            DisposeTrackedNativeArray(ref norms);
            DisposeTrackedNativeArray(ref uvs);
            DisposeTrackedNativeArray(ref cols);
            DisposeTrackedNativeArray(ref caveV);
            DisposeTrackedNativeArray(ref caveB);
            DisposeTrackedNativeArray(ref biomeV);
            if (ownsPreviewLuts)
            {
                DisposeTrackedNativeArray(ref westLut);
                DisposeTrackedNativeArray(ref eastLut);
                DisposeTrackedNativeArray(ref biomeLut);
            }

#if UNITY_EDITOR
            EditorUtility.ClearProgressBar();
#endif
        }
    }

    [ContextMenu("✕ Clear All")]
    public void ClearAll()
    {
        if (Application.isPlaying)
            return;

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
                center + new Vector3(MathLodApproximation.ApproxCosBhaskara(a0) * radius, 0f, MathLodApproximation.ApproxSinBhaskara(a0) * radius),
                center + new Vector3(MathLodApproximation.ApproxCosBhaskara(a1) * radius, 0f, MathLodApproximation.ApproxSinBhaskara(a1) * radius));
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
            transform.GetChild(i).TryGetComponent(out MeshFilter mf);
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
            transform.GetChild(i).TryGetComponent(out MeshFilter mf);
            if (mf != null && mf.sharedMesh != null)
            {
                Mesh mesh = mf.sharedMesh;
                for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
                    total += (long)mesh.GetIndexCount(subMeshIndex) / 3L;
            }
        }
        return total;
    }

    #endregion

    void OnDestroy()
    {
        ClearActiveRuntimeWorldSeed();
        GlobalRegistry.UnregisterWorldSeedProvider(this);
        _registeredWorldSeedProvider = false;
        TryUnregisterHotSwapListener();

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

        float safeChunkSize = Mathf.Max(1f, gen.chunkSize);
        float safeLod0Spacing = Mathf.Max(0.001f, gen.lod0Spacing);
        float safeLod1Spacing = Mathf.Max(0.001f, gen.lod1Spacing);
        float safeActiveRadius = Mathf.Max(0f, gen.activeRadius);
        float safeDistantRadius = Mathf.Max(safeActiveRadius, Mathf.Max(0f, gen.distantRadius));
        int lod0Res = Mathf.CeilToInt(safeChunkSize / safeLod0Spacing) + 1;
        int lod1Res = Mathf.CeilToInt(safeChunkSize / safeLod1Spacing) + 1;
        int r0 = Mathf.CeilToInt(safeActiveRadius / safeChunkSize);
        int r1 = Mathf.CeilToInt(safeDistantRadius / safeChunkSize);
        long r0Span = ((long)r0 * 2L) + 1L;
        long r1Span = ((long)r1 * 2L) + 1L;
        long estL0 = r0Span * r0Span;
        long estL1 = System.Math.Max(0L, (r1Span * r1Span) - estL0);
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
            Hecton8.Core.H8Debug.Log("[Hecton] LUTs rebaked from current curves.");
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
