# SpaceEngine 0.9.8.0 Terrain And Noise Research

Date: 2026-05-07
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Source target: `C:\Users\danat\gemes\SpaceEngine 0.9.8.0\SpaceEngine 0.9.8.0`
Output target: `C:\hades\Hecton8\Docs\SPACE_ENGINE_RESEARCH\TERRAIN_AND_NOISE_098.md`
Mining status: MINING COMPLETE
Integration verification: PENDING VERIFICATION. This document installs no Unity runtime code and does not prove Burst compile, scene behavior, or frame-time impact.

## Mandate Gate

Project rules read before writing. Relevant HECTON-8 mandates applied:

- `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`: no direct MapMagic runtime API, no `Terrain.GetHeights`, no parallel scatter stack.
- `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt`: terrain displacement must happen before Marching Cubes; normals come from the density field.
- `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`: ALU/texture budget must be MX350-first; no shader variant explosion.
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`: Burst paths must use caller-owned `NativeArray<T>`, fixed layout, no managed refs, no mid-frame blocking.
- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`: shader translation must stay compatible with the existing HECTON visual constraints.
- `STRM_World_Streaming_Residency_Chunk_Management.txt`: noise must be sampled in stable world/AUP chunk space to avoid streaming seams.

No file under `Hecton8/Assets/` was modified for this research task.

## Source Inventory

Actual shader source was not under `system\shaders`. The readable shader bundle was:

- `data\shaders\Shaders0980.pak`

Extracted local evidence directory:

- `Docs\SPACE_ENGINE_RESEARCH\_extracted\Shaders0980`
- `Docs\SPACE_ENGINE_RESEARCH\_extracted\Catalogs0980`

Primary files mined:

- `tg_common.glsl`: shared noise, Voronoi, crater, crack, dune, mare, volcano, palette atlas helpers.
- `tg_terra_height.glsl`: Earth-like procedural height generator.
- `tg_terra_color.glsl`: Earth-like procedural color generator.
- `tg_selena_height.glsl`: airless/moon-like crater and rille variants.
- `cache\shaders\NVidia_581.83_4.6.0_4.60\glsl\planet_atm_ecl_light0.glsl`: compiled render path showing height/normal packing and patch transform use.
- `data\catalogs\Catalogs0980.pak`: `.sc` palette and surface descriptors.

## Core Terrain Pipeline

SpaceEngine 0.9.8.0 terrain is not a single fBM stack. It is a domain-composed generator:

1. Climate scalar is derived from latitude, optional tidal-lock orientation, snow line, sea level, and height.
2. Global land mask uses 3D cellular noise with distorted domain.
3. Biome domains are selected from a cellular Voronoi field. The nearest-cell random color channels become biome seed, terrace seed, and terrace-layer count.
4. Height branches select dunes, hills, canyons, or mountains.
5. Mare, ice caps, cracks, craters, pseudo-rivers, and volcanoes are layered after the branch height.
6. Surface color is selected from climate, slope, and variation using a quantized material atlas, then modified by sedimentary strata and global albedo noise.

Evidence:

- `tg_terra_height.glsl:58-67`: biome domains use `Cell3Noise2Color`; `col.r`, `col.g`, `col.b` drive biome, terrace probability, and terrace layer count.
- `tg_terra_height.glsl:105-133`: canyons and mountains use eroded ridged multifractal noise plus terracing.
- `tg_terra_color.glsl:123-133`: final surface color receives sedimentary layer darkening on steep exposed slopes.
- `tg_common.glsl:645-684`: material/color lookup is quantized by height, slope, and variation.

## Noise Kernel

### Extracted GLSL

Ridged multifractal core, `tg_common.glsl:1226-1242`:

```glsl
float RidgedMultifractal(vec3 point, float gain)
{
    float signal = 1.0;
    float summ   = 0.0;
    float frequency = 1.0;
    for (int i=0; i<noiseOctaves; ++i)
    {
        weight = saturate(signal * gain);
        signal = Noise(point * frequency);
        signal = noiseOffset - sqrt(noiseRidgeSmooth + signal*signal);
        signal *= signal * weight;
        summ += signal * pow(frequency, -noiseH);
        frequency *= noiseLacunarity;
    }
    return summ;
}
```

Derivative-eroded variant, `tg_common.glsl:1268-1289`:

```glsl
noiseDeriv = NoiseDeriv((point + warp * dsum) * frequency);
weight = saturate(signal * gain);
signal = noiseOffset - sqrt(noiseRidgeSmooth + noiseDeriv.w*noiseDeriv.w);
signal *= signal * weight;
amplitude = pow(frequency, -noiseH);
summ += signal * amplitude;
frequency *= noiseLacunarity;
dsum -= amplitude * noiseDeriv.xyz * noiseDeriv.w;
```

Constants repeatedly restored by the terrain shaders:

- `noiseLacunarity = 2.218281828459`
- `noiseH = 0.5` for general fBM/crater distortion
- `noiseOffset = 0.8`
- `noiseRidgeSmooth = 0.0001`
- Canyon branch: `noiseOctaves = 5`, `noiseH = 0.9`, `noiseLacunarity = 4.0`, `noiseOffset = montesSpiky`
- Mountain branch: `noiseOctaves = 10`, `noiseH = 1.0`, `noiseLacunarity = 2.0`, `noiseOffset = montesSpiky`

### Burst Translation

This is a translation scaffold for a HECTON-owned terrain module. It is not installed code. The original `Cell*Noise` functions sample a `NoiseSampler` texture for some random vectors; this translation replaces that texture dependency with deterministic ALU hashing to keep the CPU Burst path allocation-free and stream-safe.

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public static class SpaceEngineNoise098
{
    public const float DefaultLacunarity = 2.218281828459f;
    public const float DefaultH = 0.5f;
    public const float DefaultOffset = 0.8f;
    public const float DefaultRidgeSmooth = 0.0001f;

    public static float Saturate(float x)
    {
        return math.clamp(x, 0f, 1f);
    }

    public static float SmoothStep(float a, float b, float x)
    {
        float t = Saturate((x - a) / math.max(1e-6f, b - a));
        return t * t * (3f - 2f * t);
    }

    private static float Quintic(float x)
    {
        return x * x * x * (x * (x * 6f - 15f) + 10f);
    }

    private static uint Hash(uint x)
    {
        x ^= x >> 16;
        x *= 0x7feb352du;
        x ^= x >> 15;
        x *= 0x846ca68bu;
        x ^= x >> 16;
        return x;
    }

    private static uint Hash(int3 p, uint seed)
    {
        uint x = (uint)p.x * 0x8da6b343u;
        uint y = (uint)p.y * 0xd8163841u;
        uint z = (uint)p.z * 0xcb1ab31fu;
        return Hash(x ^ y ^ z ^ seed);
    }

    private static float Float01(uint h)
    {
        return (h & 0x00ffffffu) * (1f / 16777216f);
    }

    private static float3 Gradient(uint h)
    {
        float x = Float01(h) * 2f - 1f;
        float y = Float01(Hash(h ^ 0x68bc21ebu)) * 2f - 1f;
        float z = Float01(Hash(h ^ 0x02e5be93u)) * 2f - 1f;
        return math.normalize(new float3(x, y, z) + 1e-5f);
    }

    private static float GradDot(int3 cell, float3 f, uint seed)
    {
        return math.dot(Gradient(Hash(cell, seed)), f);
    }

    public static float Noise(float3 p, uint seed = 0)
    {
        int3 i = (int3)math.floor(p);
        float3 f = p - i;
        float3 u = new float3(Quintic(f.x), Quintic(f.y), Quintic(f.z));

        float n000 = GradDot(i + new int3(0, 0, 0), f - new float3(0, 0, 0), seed);
        float n100 = GradDot(i + new int3(1, 0, 0), f - new float3(1, 0, 0), seed);
        float n010 = GradDot(i + new int3(0, 1, 0), f - new float3(0, 1, 0), seed);
        float n110 = GradDot(i + new int3(1, 1, 0), f - new float3(1, 1, 0), seed);
        float n001 = GradDot(i + new int3(0, 0, 1), f - new float3(0, 0, 1), seed);
        float n101 = GradDot(i + new int3(1, 0, 1), f - new float3(1, 0, 1), seed);
        float n011 = GradDot(i + new int3(0, 1, 1), f - new float3(0, 1, 1), seed);
        float n111 = GradDot(i + new int3(1, 1, 1), f - new float3(1, 1, 1), seed);

        float nx00 = math.lerp(n000, n100, u.x);
        float nx10 = math.lerp(n010, n110, u.x);
        float nx01 = math.lerp(n001, n101, u.x);
        float nx11 = math.lerp(n011, n111, u.x);
        float nxy0 = math.lerp(nx00, nx10, u.y);
        float nxy1 = math.lerp(nx01, nx11, u.y);
        return math.lerp(nxy0, nxy1, u.z) * 1.1547005383792515f;
    }

    public static float4 NoiseDerivFinite(float3 p, uint seed = 0)
    {
        const float e = 0.0025f;
        float n = Noise(p, seed);
        float dx = Noise(p + new float3(e, 0f, 0f), seed) - Noise(p - new float3(e, 0f, 0f), seed);
        float dy = Noise(p + new float3(0f, e, 0f), seed) - Noise(p - new float3(0f, e, 0f), seed);
        float dz = Noise(p + new float3(0f, 0f, e), seed) - Noise(p - new float3(0f, 0f, e), seed);
        return new float4(new float3(dx, dy, dz) * (0.5f / e), n);
    }

    public static float Fbm(float3 point, int octaves, float lacunarity, float h, uint seed = 0)
    {
        float sum = 0f;
        float amp = 1f;
        float gain = math.pow(lacunarity, -h);

        for (int i = 0; i < octaves; i++)
        {
            sum += Noise(point, seed + (uint)i * 1013u) * amp;
            point *= lacunarity;
            amp *= gain;
        }

        return sum;
    }

    public static float RidgedMultifractal(
        float3 point,
        int octaves,
        float gain,
        float lacunarity,
        float h,
        float offset,
        float ridgeSmooth,
        uint seed = 0)
    {
        float signal = 1f;
        float sum = 0f;
        float frequency = 1f;

        for (int i = 0; i < octaves; i++)
        {
            float weight = Saturate(signal * gain);
            signal = Noise(point * frequency, seed + (uint)i * 4099u);
            signal = offset - math.sqrt(ridgeSmooth + signal * signal);
            signal *= signal * weight;
            sum += signal * math.pow(frequency, -h);
            frequency *= lacunarity;
        }

        return sum;
    }

    public static float RidgedMultifractalErodedDetail(
        float3 point,
        int octaves,
        float gain,
        float warp,
        float firstOctaveValue,
        float lacunarity,
        float h,
        float offset,
        float ridgeSmooth,
        uint seed = 0)
    {
        float frequency = lacunarity;
        float amplitude = math.pow(lacunarity, -h);
        float signal = firstOctaveValue;
        float sum = 0f;
        float3 dsum = new float3(0f);

        for (int i = 1; i < octaves; i++)
        {
            float4 nd = NoiseDerivFinite((point + warp * dsum) * frequency, seed + (uint)i * 4099u);
            float weight = Saturate(signal * gain);
            signal = offset - math.sqrt(ridgeSmooth + nd.w * nd.w);
            signal *= signal * weight;
            sum += signal * amplitude;
            dsum -= amplitude * nd.xyz * nd.w;
            frequency *= lacunarity;
            amplitude *= math.pow(lacunarity, -h);
        }

        return sum;
    }
}
```

Production note: `NoiseDerivFinite` is allocation-free, but it costs six extra noise calls. For a high-throughput CPU path, port `tg_common.glsl:939-1000` analytically instead of using finite differences. The finite derivative is acceptable as a reference implementation, not as the final chunk hot path.

## Biome Domain And Terrace Seeds

Extracted evidence, `tg_terra_height.glsl:58-67`:

```glsl
p = p * 2.3 + 13.5 * Fbm3D(p * 0.06);
vec4  col;
vec2  cell = Cell3Noise2Color(p, col);
float biome = col.r;
float biomeScale = saturate(2.0 * (pow(abs(cell.y - cell.x), 0.7) - 0.05));
float terrace = col.g;
float terraceLayers = max(col.b * 10.0 + 3.0, 3.0);
terraceLayers += Fbm(p * 5.41);
```

Meaning for HECTON-8:

- `F1/F2` separation is not only visual Voronoi. It is used as domain confidence.
- `col.r` is a biome selector, but SpaceEngine uses it as a continuous branch selector, not a fixed 108-biome ID.
- `col.g` is a per-domain terrace probability seed.
- `col.b` creates local terrace layer count: roughly `3..13`, then fBM perturbs it.
- HECTON must not map this directly to the existing 108-biome ScriptableObject matrix. Treat it as a geology-domain seed that can feed `BiomeMatrixDirector` indirectly through the existing field sampler path.

Burst-side Voronoi surrogate:

```csharp
public static class SpaceEngineCells098
{
    private static float3 Hash3(int3 p, uint seed)
    {
        uint h = (uint)p.x * 0x8da6b343u ^ (uint)p.y * 0xd8163841u ^ (uint)p.z * 0xcb1ab31fu ^ seed;
        h ^= h >> 16;
        h *= 0x7feb352du;
        h ^= h >> 15;
        h *= 0x846ca68bu;
        h ^= h >> 16;

        float x = (h & 1023u) * (1f / 1024f);
        float y = ((h >> 10) & 1023u) * (1f / 1024f);
        float z = ((h >> 20) & 1023u) * (1f / 1024f);
        return new float3(x, y, z);
    }

    public static float2 Cell3F1F2Color(float3 p, out float4 color, uint seed = 0)
    {
        int3 baseCell = (int3)math.floor(p);
        float f1 = 1e20f;
        float f2 = 1e20f;
        float3 winner = new float3(0f);

        for (int z = -1; z <= 1; z++)
        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            int3 c = baseCell + new int3(x, y, z);
            float3 rnd = Hash3(c, seed);
            float3 q = (float3)c + rnd;
            float d = math.lengthsq(q - p);

            if (d < f1)
            {
                f2 = f1;
                f1 = d;
                winner = rnd;
            }
            else if (d < f2)
            {
                f2 = d;
            }
        }

        color = new float4(winner, 1f);
        return math.sqrt(new float2(f1, f2));
    }
}
```

## Canyons, Rifts, And Terracing

Extracted canyon/mountain branch, `tg_terra_height.glsl:105-133`:

```glsl
height = -canyonsMagn * montRage * RidgedMultifractalErodedDetail(point * 4.0 * canyonsFreq * inv2montesSpiky + Randomize, 2.0, erosion, montBiomeScale);
terraceLayers *= 5.0;
float h = height * terraceLayers;
height = (floor(h) + smoothstep(0.1, 0.9, fract(h))) / terraceLayers;

height = montesMagn * montRage * RidgedMultifractalErodedDetail(point * montesFreq * inv2montesSpiky + Randomize, 2.0, erosion, montBiomeScale);
if (terrace < terraceProb)
{
    float h = height * terraceLayers;
    height = (floor(h) + smoothstep(0.0, 1.0, fract(h))) / terraceLayers;
    height *= 0.75;
}
```

Important difference:

- Canyons are negative eroded ridged multifractal. They are cut downward.
- Canyon terracing is effectively always applied in the Terra shader; the original `if (terrace < terraceProb)` is commented out.
- Canyon terrace layers are multiplied by `5.0`, making strata much denser than mountains.
- Mountains terrace only when the domain seed passes `terraceProb`, and then total amplitude is reduced to `0.75`.

Replacement for current basic `Terrace01`:

```csharp
public static class SpaceEngineTerrain098
{
    public static float TerraceSE(float height, float layers, float edge0, float edge1)
    {
        layers = math.max(1f, layers);
        float h = height * layers;
        return (math.floor(h) + SpaceEngineNoise098.SmoothStep(edge0, edge1, math.frac(h))) / layers;
    }

    public static float CanyonHeightSE(
        float3 worldPoint,
        float canyonsMagn,
        float canyonsFreq,
        float montesSpiky,
        float montRage,
        float erosion,
        float montBiomeScale,
        float terraceLayers,
        float3 randomize,
        uint seed)
    {
        float inv2MontesSpiky = 1f / math.max(1e-5f, montesSpiky * montesSpiky);
        float3 p = worldPoint * (4f * canyonsFreq * inv2MontesSpiky) + randomize;

        float ridged = SpaceEngineNoise098.RidgedMultifractalErodedDetail(
            p,
            octaves: 5,
            gain: 2f,
            warp: erosion,
            firstOctaveValue: montBiomeScale,
            lacunarity: 4f,
            h: 0.9f,
            offset: montesSpiky,
            ridgeSmooth: SpaceEngineNoise098.DefaultRidgeSmooth,
            seed: seed);

        float height = -canyonsMagn * montRage * ridged;
        return TerraceSE(height, terraceLayers * 5f, 0.1f, 0.9f);
    }
}
```

## Crater Math

Extracted crater profile, `tg_common.glsl:1998-2015`:

```glsl
float CraterHeightFunc(float lastlastLand, float lastLand, float height, float r)
{
    float distHeight = craterDistortion * height;
    float t = 1.0 - r/radPeak;
    float peak = heightPeak * craterDistortion * smoothstep(0.0, 1.0, t);
    t = smoothstep(0.0, 1.0, (r - radInner) / (radRim - radInner));
    float inoutMask = t*t*t;
    float innerRim = heightRim * distHeight * smoothstep(0.0, 1.0, inoutMask);
    t = smoothstep(0.0, 1.0, (radOuter - r) / (radOuter - radRim));
    float outerRim = distHeight * mix(0.05, heightRim, t*t);
    t = saturate((1.0 - r) / (1.0 - radOuter));
    float halo = 0.05 * distHeight * t;
    return mix(lastlastLand + height * heightFloor + peak + innerRim, lastLand + outerRim + halo, inoutMask);
}
```

Default old-crater setup in `CraterNoise`, `tg_common.glsl:2033-2089`:

- `radPeak = 0.03`
- `radInner = 0.15`
- `radRim = 0.2`
- `radOuter = 0.8`
- `heightFloor = -0.1`
- `heightPeak = 0.6`
- `heightRim = 1.0`
- round distortion uses `0.03 * Fbm3D(point * 2.56)`
- octave scale: frequency `* 1.81818182`, amplitude `* 0.55`, peak `* 0.25`, floor `* 1.2`, inner radius `* 0.60`

Burst crater profile:

```csharp
public struct CraterProfile098
{
    public float RadPeak;
    public float RadInner;
    public float RadRim;
    public float RadOuter;
    public float HeightFloor;
    public float HeightPeak;
    public float HeightRim;
    public float Distortion;

    public static CraterProfile098 OldDefault()
    {
        return new CraterProfile098
        {
            RadPeak = 0.03f,
            RadInner = 0.15f,
            RadRim = 0.2f,
            RadOuter = 0.8f,
            HeightFloor = -0.1f,
            HeightPeak = 0.6f,
            HeightRim = 1f,
            Distortion = 1f
        };
    }
}

public static class SpaceEngineCrater098
{
    public static float CraterHeightFuncSE(
        float lastlastLand,
        float lastLand,
        float height,
        float r,
        CraterProfile098 c)
    {
        float distHeight = c.Distortion * height;

        float t = 1f - r / math.max(1e-5f, c.RadPeak);
        float peak = c.HeightPeak * c.Distortion * SpaceEngineNoise098.SmoothStep(0f, 1f, t);

        t = SpaceEngineNoise098.SmoothStep(0f, 1f, (r - c.RadInner) / math.max(1e-5f, c.RadRim - c.RadInner));
        float inoutMask = t * t * t;
        float innerRim = c.HeightRim * distHeight * SpaceEngineNoise098.SmoothStep(0f, 1f, inoutMask);

        t = SpaceEngineNoise098.SmoothStep(0f, 1f, (c.RadOuter - r) / math.max(1e-5f, c.RadOuter - c.RadRim));
        float outerRim = distHeight * math.lerp(0.05f, c.HeightRim, t * t);

        t = SpaceEngineNoise098.Saturate((1f - r) / math.max(1e-5f, 1f - c.RadOuter));
        float halo = 0.05f * distHeight * t;

        float inside = lastlastLand + height * c.HeightFloor + peak + innerRim;
        float outside = lastLand + outerRim + halo;
        return math.lerp(inside, outside, inoutMask);
    }
}
```

Heightmap crater job:

```csharp
[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct ApplyCraterHeightJob : IJobParallelFor
{
    public NativeArray<float> Heights;

    [ReadOnly] public int Width;
    [ReadOnly] public float2 Origin;
    [ReadOnly] public float CellSize;
    [ReadOnly] public float2 CraterCenter;
    [ReadOnly] public float RadiusMeters;
    [ReadOnly] public float Amplitude;
    [ReadOnly] public CraterProfile098 Profile;

    public void Execute(int index)
    {
        int x = index % Width;
        int y = index / Width;
        float2 p = Origin + new float2(x, y) * CellSize;
        float r = math.length(p - CraterCenter) / math.max(1e-5f, RadiusMeters);

        if (r >= 1f)
        {
            return;
        }

        float old = Heights[index];
        float crater = SpaceEngineCrater098.CraterHeightFuncSE(0f, 0f, Amplitude, r, Profile);
        Heights[index] = old + crater;
    }
}
```

For production multi-crater terrain, do not scan every crater over every height sample. Use chunk-local crater bins or deterministic cell placement per chunk so each height sample tests only nearby crater candidates.

## Cracks, Canyons, And Rilles

Extracted crack/rille structure, `tg_common.glsl:2443-2486`:

```glsl
cell = Cell2Noise2(point + 0.02 * Fbm3D(1.8 * point));
r = smoothstep(0.0, 1.0, 250.0 * abs(cell.y - cell.x));
newLand = CrackHeightFunc(lastlastLand, lastLand, ampl, r, point);
point = point * 1.2 + Randomize;
ampl *= 0.8333;
mask *= smoothstep(0.6, 1.0, r);
```

Meaning:

- Rift valleys and lunar rilles are not hand-drawn splines in the shader. They are extracted from `abs(F2 - F1)` near Voronoi cell borders.
- The narrowness constant is high: `250.0 * abs(F2 - F1)`.
- Width, branching, and distortion come from fBM domain warp before the cell test.
- HECTON use case: abyssal cracks, trench shelves, hydrothermal fracture networks. Sample `F2-F1` in AUP/world space before voxel meshing.

## Dunes

`DunesNoise`, `tg_common.glsl:2523-2554`, uses:

- simplex noise to derive local wind/dune angle
- global fBM mask
- sinusoidal profile based on projected distance
- octave lacunarity around `1.17`, much lower than mountain noise

HECTON use case: silt waves and abyssal current ripples. Keep amplitude low and feed it as final micro-displacement, not primary shelf height.

## Mare, Basins, Volcanoes

SpaceEngine has explicit basin and volcano shapes:

- `MareHeightFunc`, `tg_common.glsl:2379-2406`: broad crater-like basin with bottom, inner rim, outer rim.
- `MareNoise`, `tg_common.glsl:2410-2439`: 3 octave basin placement with `Cell3Noise`, amplitude decay `0.62`, radial growth `1.2`.
- `VolcanoHeightFunc`, `tg_common.glsl:2228-2246`: shield cone, caldera depression, and turbulence ridges.
- `VolcanoNoise`, `tg_common.glsl:2268-2322`: inverse spherical Fibonacci placement, `softExpMaxMin` composition, octave frequency doubling.

HECTON translation:

- Mare logic is directly useful for submerged shelves and drowned calderas.
- Volcano logic is useful for hydrothermal mounts, but it must be masked by existing geology ownership and not create a second procedural geology stack.

## Procedural Color And Biome Palettes

### Shader Color System

Extracted surface lookup, `tg_common.glsl:645-684`:

```glsl
height = clamp(height - 0.0625, 0.0, 1.0);
slope  = clamp(slope  + 0.1250, 0.0, 1.0);
float h0 = floor(height * 8.0) * 0.125;
float s0 = floor(slope  * 4.0) * 0.25;
float v0 = floor(vary * 16.0) * 0.25;
...
return BlendSmart(surfV0, surfV1, dv);
```

Extracted sediment overlay, `tg_terra_color.glsl:125-133`:

```glsl
float layers = Fbm(vec3(height * 168.4 + 0.17 * vary, 0.43 * (p.x + p.y), 0.43 * (p.z - p.y)));
layers *= smoothstep(0.5, 0.55, slope);
layers *= step(surf.color.a, 0.01);
layers *= saturate(1.0 - 5.0 * volcMask.x);
layers *= saturate(1.0 - 5.0 * volcMask.y);
surf.color.rgb *= vec3(1.0) - vec3(0.0, 0.5, 1.0) * layers;
```

Color generator behavior:

- `height` is quantized into 8 bands.
- `slope` is quantized into 4 bands.
- `vary` selects 4 material variants from a 16-step source.
- Atlas lookup uses random tile offset/rotation and HSL adjustment from a material table.
- Sediment layers darken exposed steep rock by removing green/blue, shifting toward yellow/red bands.
- Snow/ice/lava masks block the sediment layer operation.

### Catalog Palette Keys

Catalog `.sc` files expose palette descriptors. Examples in extracted catalogs include:

- `colorSea`
- `colorShelf`
- `colorBeach`
- `colorDesert`
- `colorLowland`
- `colorUpland`
- `colorRock`
- `colorSnow`
- `colorLowPlants`
- `colorUpPlants`

Earth example in `SolarSys.sc` includes shelf/sea colors:

- `colorSea (0.04 0.10 0.20 1.00)`
- `colorShelf (0.15 0.48 0.46 1.00)`

HECTON shader directive:

- Use a small HECTON-owned palette buffer or LUT indexed by `heightBand`, `slopeBand`, `biomeClass`, and `variation`.
- Keep the SpaceEngine quantization idea; do not copy the visual palette verbatim.
- For underwater terrain, replace Earth climate bands with pressure/depth/thermal/sediment bands.
- Use triplanar material rules from the existing HECTON procedural asset pipeline. Avoid texture-heavy atlas dependence unless the material owner approves the sample budget.

ALU-side color banding scaffold:

```hlsl
float HeightBand8(float h)
{
    return floor(saturate(h - 0.0625) * 8.0);
}

float SlopeBand4(float slope)
{
    return floor(saturate(slope + 0.125) * 4.0);
}

float SedimentLayerMask(float height, float vary, float3 p, float slope, float snowMask, float lavaMask, float volcanoMask)
{
    float3 lp = float3(height * 168.4 + 0.17 * vary, 0.43 * (p.x + p.y), 0.43 * (p.z - p.y));
    float layers = Fbm4(lp);
    layers *= smoothstep(0.5, 0.55, slope);
    layers *= 1.0 - saturate(snowMask);
    layers *= saturate(1.0 - 5.0 * lavaMask);
    layers *= saturate(1.0 - 5.0 * volcanoMask);
    return layers;
}
```

## LOD And Quadtree Findings

No full quadtree LOD transition algorithm was found in the readable GLSL source. Evidence found only render-side patch data:

- Compiled planet shaders use `NodeCenter` as node center offset plus heightmap offset.
- `BumpTexTransf` transforms height/normal texture coordinates per patch.
- Height is packed in bump texture channels: `heightN = bumpData.w + bumpData.z * 0.00390625`.
- Detail normals are added from multi-octave noise texture sampling in the compiled render shader.

Conclusion:

- Terrain LOD selection, quadtree residency, crack stitching, and patch scheduling are almost certainly CPU/engine-side in `SpaceEngine.exe`, not exposed in the GLSL bundle.
- Do not claim SpaceEngine quadtree math was recovered. For HECTON, only adopt the stable coordinate rule: sample all procedural noise in world/AUP space before chunk extraction, then stream chunks with existing residency ownership.

## Thermal Weathering And Sediment Transport

No physically based thermal weathering, hydraulic sediment transport, or sediment deposition solver was found in the readable terrain shaders.

What was found:

- Erosion-like derivative warping in `RidgedMultifractalEroded`, based on Giliam de Carpentier-style procedural erosion.
- Sedimentary visual layers in `tg_terra_color.glsl`, driven by height, slope, and fBM.
- Pseudo-river carving from Voronoi `F2-F1` fields in `tg_terra_height.glsl:182-198`.

HECTON implication:

- Use SpaceEngine math as procedural shape language.
- Do not present it as a replacement for the existing hydraulic erosion engine or sediment simulation.

## HECTON Integration Contract

Allowed integration shape:

- Add these formulas only inside an existing terrain/geology generation owner.
- Feed MapMagic through the existing bridge/scatter data path. Do not call MapMagic generators directly from runtime systems.
- For voxel shelves, evaluate SpaceEngine-style height/displacement before SDF sampling and Marching Cubes.
- Store parameters in HECTON ScriptableObjects or fixed native config structs. Runtime jobs must receive blittable config only.
- Sample with AUP/world coordinates and deterministic seeds. Chunk-local coordinates alone will create seams.

Rejected integration shape:

- No new parallel biome/scatter manager.
- No runtime `Terrain.GetHeights`, `Terrain.GetAlphamaps`, or `Terrain.SampleHeight`.
- No managed dictionaries/lists/strings inside Burst jobs.
- No copying SpaceEngine palettes as game art direction.
- No direct claim of recovered CPU quadtree LOD.

## Minimal Terrain Job Composition

Reference-only shape for a chunk height pass:

```csharp
[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct SpaceEngineStyleTerrainPassJob : IJobParallelFor
{
    public NativeArray<float> Heights;

    [ReadOnly] public int Width;
    [ReadOnly] public float2 Origin;
    [ReadOnly] public float CellSize;
    [ReadOnly] public float3 Randomize;
    [ReadOnly] public uint Seed;

    [ReadOnly] public float MainFreq;
    [ReadOnly] public float CanyonMagn;
    [ReadOnly] public float CanyonFreq;
    [ReadOnly] public float MontesSpiky;
    [ReadOnly] public float Erosion;

    public void Execute(int index)
    {
        int x = index % Width;
        int y = index / Width;
        float2 wp2 = Origin + new float2(x, y) * CellSize;

        // For planetary terrain use normalized sphere point. For HECTON shelf chunks,
        // use stable AUP/world coordinates transformed by the existing terrain owner.
        float3 p = new float3(wp2.x, 0f, wp2.y) * MainFreq + Randomize;

        float4 domainColor;
        float2 f = SpaceEngineCells098.Cell3F1F2Color(p * 2.3f, out domainColor, Seed);
        float biomeScale = SpaceEngineNoise098.Saturate(2f * (math.pow(math.abs(f.y - f.x), 0.7f) - 0.05f));
        float terraceLayers = math.max(domainColor.z * 10f + 3f, 3f);

        float montRage = SpaceEngineNoise098.Saturate(SpaceEngineNoise098.Noise(p * 22.6f, Seed ^ 0x912759u) + 0.5f);
        montRage *= montRage;
        float montBiomeScale = math.min(math.pow(2.2f * biomeScale, 2.5f), 1f) * montRage;

        float h = SpaceEngineTerrain098.CanyonHeightSE(
            p,
            CanyonMagn,
            CanyonFreq,
            MontesSpiky,
            montRage,
            Erosion,
            montBiomeScale,
            terraceLayers,
            Randomize,
            Seed);

        Heights[index] = h;
    }
}
```

The sample above intentionally omits MapMagic, Unity `Terrain`, material assignment, and scene object writes. It is a math pass only.

## Regression Model

If these formulas are integrated later, required verification:

- Burst compile for the target assembly.
- Allocation recording on the terrain generation frame: 0 B GC.
- Determinism check: same seed, same chunk origin, same output hash.
- Seam check: adjacent chunks sample identical border heights/normals.
- MX350 shader budget check if the color logic moves to GPU.
- MapMagic bridge check: generated fields must flow through the current owner path, not a direct MapMagic runtime dependency.
- Voxel check: displacement applied before meshing; normals derived from density, not mesh-only smoothing.

## Final Findings

- Core SpaceEngine terrain flavor is ridged multifractal plus derivative domain erosion, not plain Perlin/fBM.
- Canyons are negative eroded ridged multifractal with dense terracing.
- Craters use a compact analytic profile: central peak, depressed floor, inner rim, outer rim, and halo, layered through octave cellular placement.
- Rilles/cracks use Voronoi border distance, specifically narrow `abs(F2-F1)` bands.
- Biome domains are cellular random domains with continuous seeds, not discrete game-biome IDs.
- Terrain color is ALU-driven by height/slope/variation quantization plus palette/material lookup and sedimentary overlays.
- Thermal weathering and full sediment transport were not present in the readable shader source.
- Quadtree LOD transition math was not present in the readable shader source.

Status: MINING COMPLETE.
