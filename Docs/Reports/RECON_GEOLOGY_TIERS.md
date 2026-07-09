# Hecton-8 Geomorphological Mathematics Dossier

This report catalogs the exact mathematical equations, algorithms, and structures used to deform the HECTON-8 seabed terrain across the first five geomorphological tiers (Macro-Macro down to Meso-Micro).

---

## TIER 1: MACRO-MACRO (Continental Scale: 10km - 50km)

### Geomorphological Function
Defines the massive tectonic bounds of the world, specifically separating the continental shelves from the deep abyssal zones, continental slope descents (ShelfBreak), and canyon cuts.

### Mathematical Implementation
Located in [WorldMacroGeologyFields.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/World/WorldMacroGeologyFields.cs#L323-L339):

```csharp
// 1. CONTINENTAL SHELF / ABYSS BLEND
float continentNoise = FractalSimplexNoise01(norm * 2.8f, p.Seed ^ 0x12345678u);
float shelfMask = math.smoothstep(0.35f, 0.65f, continentNoise);

// Steep, dramatic continental slope transition (ShelfBreak)
float shelfBreakMask = 1f - math.saturate(math.abs(continentNoise - 0.5f) * 6.0f);
shelfBreakMask = math.saturate(shelfBreakMask);

// Canyon cuts on the shelf break
float canyonNoise = FractalNoise01(warpedPos * 0.0004f, p.Seed ^ 0x0CA14405u);
float canyonDepthProfile = math.pow(math.smoothstep(0.6f, 0.95f, canyonNoise), 3f);
float canyonMask = canyonDepthProfile * math.smoothstep(0.1f, 0.9f, shelfBreakMask);

// Base depth blend
float depth = math.lerp(p.AbyssDepthMeters, p.ShelfDepthMeters, shelfMask);
depth += canyonMask * 800f; // Deep canyon cuts
```

Default parameters:
*   `AbyssDepthMeters`: `2950f`
*   `ShelfDepthMeters`: `90f`
*   `ShelfBreakWidthMeters`: `5200f`

---

## TIER 2: MACRO (Landscape Scale: 1km - 10km)

### Geomorphological Function
Constructs seamounts, guyots, volcanic calderas, radial gully flank patterns, massive trenches, mountain ranges, and tectonic plates.

### Mathematical Implementation
Located in [WorldMacroGeologyFields.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/World/WorldMacroGeologyFields.cs#L340-L416):

```csharp
// 2. MOUNTAIN RIDGES
// Use Ridged Multifractal for sharp, peaked mountain ranges
float ridgeNoise = RidgedMultifractal01(warpedNorm * 8.0f, p.Seed ^ 0x91E83B37u, 5);
float ridgeMask = math.smoothstep(0.35f, 0.85f, ridgeNoise);
depth -= ridgeMask * p.RidgeHeightMeters * (1f - shelfMask * 0.4f);

// 3. DEEP OCEANIC TRENCHES / FAULTS
float trenchNoise = RidgedMultifractal01(warpedNorm * 6.0f + new float2(0.4f, -0.6f), p.Seed ^ 0x4B3A2C1Du, 4);
float trenchMask = math.smoothstep(0.55f, 0.95f, trenchNoise) * (1f - shelfMask);
depth += trenchMask * p.TrenchDepthMeters;

// 4. FAULT LINES
float faultNoise = RidgedMultifractal01(warpedNorm * 12.0f, p.Seed ^ 0xCA97D1F3u, 3);
float faultMask = math.smoothstep(0.45f, 0.85f, faultNoise) * (1f - shelfMask * 0.5f);
depth += faultMask * 120f;

// 5. ABYSSAL BASINS
float basinMask = math.saturate((1f - shelfMask) * (1f - ridgeMask) * (1f - trenchMask));
depth += basinMask * p.BasinDepthMeters;

// 6. ABYSSAL SEAMOUNTS / GUYOTS (warpedPos deforms perfect circular shapes)
float2 seamountCell = math.floor(warpedPos * 0.0003f); // 3.3km grid
float2 frac = warpedPos * 0.0003f - seamountCell;
float minDist = 8.0f;
float2 seamountHash = new float2(0, 0);
float2 seamountCenterLocal = new float2(0, 0);

for (int y = -1; y <= 1; y++)
{
    for (int x = -1; x <= 1; x++)
    {
        float2 neighbor = new float2(x, y);
        float2 pointHash = Hash2((int)(seamountCell.x + neighbor.x), (int)(seamountCell.y + neighbor.y), p.Seed ^ 0x5EA30447u);
        float2 seamountDiff = neighbor + pointHash - frac;
        float dist = math.length(seamountDiff);
        if (dist < minDist)
        {
            minDist = dist;
            seamountHash = pointHash;
            seamountCenterLocal = seamountDiff; // vector from current warpedPos to seamount center
        }
    }
}

float seamountProfile = math.saturate(1f - minDist * 2f);
if (seamountProfile > 0f)
{
    // Volcanic exponential profile
    float volProfile = math.exp(-minDist * 6.0f);
    
    float isGuyot = HashToUnitFloat(Hash(unchecked((int)(seamountHash.x * 1000f)), unchecked((int)(seamountHash.y * 1000f)), 0x123456)) > 0.5f ? 1f : 0f;
    
    if (isGuyot > 0f)
    {
        // Guyot: Flat top
        volProfile = math.min(volProfile, 0.4f);
    }
    else
    {
        // Caldera (Depression at the very center)
        float calderaProfile = 1f - math.smoothstep(0f, 0.045f, minDist);
        volProfile -= calderaProfile * 0.3f * seamountProfile;
    }
    
    // Radial Erosional Gullies (seam-free organic branching using normalized direction and warpedPos phase shift)
    float2 dir = minDist > 0.0001f ? seamountCenterLocal / minDist : new float2(1f, 0f);
    float gullyPattern = (FractalSimplexNoise01(dir * 3.8f + warpedPos * 0.0005f, p.Seed ^ 0x901177Au) * 2f - 1f);
    float gullyProfile = 1f - math.abs(gullyPattern);
    gullyProfile = math.pow(gullyProfile, 3.0f); // Sharper cuts
    
    // Gullies only form on the flanks
    float flankMask = math.smoothstep(0.05f, 0.3f, minDist) * math.smoothstep(0.4f, 0.25f, minDist);
    volProfile -= gullyProfile * flankMask * 0.15f;
    
    depth -= math.saturate(volProfile) * basinMask * 2600f;
}
```

---

## TIER 3: MESO-MESO (Formation Scale: 100m - 1km)

### Geomorphological Function
Simulates regional strata (terracing) and impact basins (meteor craters).

### Mathematical Implementation
Terracing and craters are evaluated in [WorldMacroGeologyFields.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/World/WorldMacroGeologyFields.cs#L484-L600):

```csharp
// METEOR CRATERS PASS (with rim-warping to prevent perfect mathematical circles)
float craterDepthDelta = 0f;
float craterMask = 0f;

float craterGridSize = 2000f;
int2 craterCell = new int2((int)math.floor(warpedPos.x / craterGridSize), (int)math.floor(warpedPos.y / craterGridSize));

for (int dz = -1; dz <= 1; dz++)
{
    for (int dx = -1; dx <= 1; dx++)
    {
        int2 craterNeighborCell = craterCell + new int2(dx, dz);
        uint h = Hash(craterNeighborCell.x, craterNeighborCell.y, unchecked((int)(p.Seed ^ 0x9B3A21EFu)));
        
        // ~15% chance of a crater in this 2km cell
        float probability = HashToUnitFloat(h ^ 0x12345678u);
        if (probability > 0.15f) continue;
        
        float cx = (craterNeighborCell.x + HashToUnitFloat(h ^ 0x87654321u)) * craterGridSize;
        float cz = (craterNeighborCell.y + HashToUnitFloat(h ^ 0xA1B2C3D4u)) * craterGridSize;
        
        // Radius between 120m and 600m
        float radius = math.lerp(120f, 600f, math.pow(HashToUnitFloat(h ^ 0x1A2B3C4Du), 2.5f)); 
        
        float dist = math.length(new float2(warpedPos.x - cx, warpedPos.y - cz));
        if (dist > radius * 2.0f) continue;
        
        // rimWarp: deforms the crater radius so it is NOT a perfect circle
        float rimWarp = (FractalSimplexNoise01(warpedPos * 0.015f, h ^ 0xDEADBEEFu) * 2f - 1f) * 0.06f;
        float normalizedDist = dist / radius + rimWarp;
        
        // Crater Cavity
        float bowl = 1f - math.smoothstep(0f, 1f, normalizedDist); 
        bowl = math.pow(bowl, 1.5f); // Flatten the center due to sedimentation
        
        // Crater Rim
        float rimProfile = math.max(0f, 1f - math.abs(normalizedDist - 1f) * 2.5f);
        rimProfile = math.smoothstep(0f, 1f, rimProfile);
        
        // Central Peak (only in large craters)
        float peak = 0f;
        if (radius > 1200f) {
            float peakRadius = radius * 0.15f;
            peak = 1f - math.smoothstep(0f, peakRadius, dist);
            peak = math.smoothstep(0f, 1f, peak) * 0.4f;
        }
        
        // Rim Erosion Noise
        float angle = math.atan2(warpedPos.y - cz, warpedPos.x - cx);
        float rimErosion = FractalNoise01(new float2(angle * 4.0f, radius), h ^ 0xDEADBEEFu);
        rimProfile *= (0.4f + rimErosion * 0.6f);
        
        float maxDepth = radius * 0.18f;
        float maxRimHeight = radius * 0.08f;
        
        craterDepthDelta += bowl * maxDepth;     // Depress (add to depth)
        craterDepthDelta -= peak * maxDepth;     // Raise peak (subtract from depth)
        craterDepthDelta -= rimProfile * maxRimHeight; // Raise rim
        
        craterMask = math.max(craterMask, bowl);
    }
}
depth += craterDepthDelta;

...

// TECTONIC TERRACING — Localized Geological Strata
float terraceStrength = math.saturate(shelfBreakMask * 0.8f + ridgeMask * 0.4f + faultMask * 0.5f);
if (terraceStrength > 0.05f)
{
    // STEP 1: LARGE STEPS → only 3-5 terraces on a 400m mountain.
    float dynamicTerraceScale = math.lerp(80.0f, 180.0f,
        FractalSimplexNoise01(warpedNorm * 3.0f, p.Seed ^ 0x112233u));

    // STEP 2: STRATA TILT via pos (meters). 50m per km = 1-2 step shifts across mountain.
    float2 tiltDir = math.normalize(new float2(
        FractalSimplexNoise01(warpedNorm * 1.8f, p.Seed ^ 0xAB12CD34u) * 2f - 1f,
        FractalSimplexNoise01(warpedNorm * 1.8f, p.Seed ^ 0x56EF78ABu) * 2f - 1f
    ));
    float strataCoord = depth + math.dot(tiltDir, pos) * 0.05f;

    // STEP 3: EROSION at mountain scale. ±60m+±25m on 80-180m steps = 0.33-0.75 step shift.
    float terraceErosionC = (FractalSimplexNoise01(warpedNorm * 80.0f,  p.Seed ^ 0x99AA88BBu) * 2f - 1f) * 60.0f;
    float terraceErosionF = (FractalSimplexNoise01(warpedNorm * 250.0f, p.Seed ^ 0x77CC4411u) * 2f - 1f) * 25.0f;
    float terraceErosion  = terraceErosionC + terraceErosionF;

    // STEP 4: QUANTIZE with sharp cliff wall at top of step.
    float hPhase = (strataCoord + terraceErosion) / dynamicTerraceScale;
    float fStep  = math.frac(hPhase);
    float sStep  = math.smoothstep(0.55f, 0.88f, fStep);

    float terracedCoord = (math.floor(hPhase) + sStep) * dynamicTerraceScale - terraceErosion;
    float terracedDepth = terracedCoord - math.dot(tiltDir, pos) * 0.05f;

    // STEP 5: AGGRESSIVE PATCHINESS — only ~30% of mountain gets terracing.
    float terracePatchMask = math.smoothstep(0.60f, 0.92f,
        FractalSimplexNoise01(warpedNorm * 4.5f, p.Seed ^ 0x992211AAu));

    // STEP 6: MAX BLEND 0.55 — macro shape always reads through.
    depth = math.lerp(depth, terracedDepth, terraceStrength * terracePatchMask * 0.55f);
}
```

An additional local terrace layer is calculated in `WorldTerrainMesoDetailFields` in [WorldTerrainDetailContracts.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/World/WorldTerrainDetailContracts.cs#L479-L497):

```csharp
float terraceStep = 18f + shelfBreak * 48f + ridge * 24f;
float terraceWarp = (ValueNoise01(absoluteX, absoluteZ, p.Seed ^ 0x7D4B9143u, 910f) - 0.5f) * terraceStep * 0.42f;
float terraceHeight = macro.HeightMeters + terraceWarp;
float terraceLocal = terraceHeight / math.max(1f, terraceStep);
float terraceBase = math.floor(terraceLocal);
float terraceFrac = terraceLocal - terraceBase;
// Organic smooth-step instead of primitive rounding
float terraceSoft = terraceBase + math.smoothstep(0.25f, 0.75f, terraceFrac);
float terraceOffset = terraceSoft * terraceStep - terraceHeight;

// Mask out the terrace in steep areas to prevent texture stretching
float terraceMask = math.smoothstep(0.85f, 0.45f, slope);

float terraceDelta = (terraceOffset - terraceWarp * 0.18f) *
    terrace *
    terraceMask *
    p.TerraceStrengthMeters *
    detailGate;
```

---

## TIER 4: MESO (Obstacle Scale: 10m - 100m)

### Geomorphological Function
Handles sediment-dune crest lines, localized subsidence pits (cellular pockmarks), scree/talus slope accumulation at cliff flanks, slumps, and rubble.

### Mathematical Implementation
Evaluated in [WorldMacroGeologyFields.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/World/WorldMacroGeologyFields.cs#L455-L483):

```csharp
// SEDIMENT DUNE/RIPPLE DETAIL PASS
float duneSample = FractalSimplexNoise01(warpedPos * 0.05f, p.Seed ^ 0xD11EBA5Eu);
duneSample = 1f - math.abs(duneSample); // Create sharp ridges and wide valleys
duneSample = math.pow(duneSample, 1.8f); // Pin the valleys flatter

// Patch masking: dunes only appear in specific fields
float duneFieldMask = FractalNoise01(warpedPos * 0.0015f, p.Seed ^ 0xA8B2C41Eu);
duneFieldMask = math.smoothstep(0.4f, 0.6f, duneFieldMask); // Sharp transition into dune fields

float sedimentDepth = math.saturate(1f - math.saturate(hardRockExposure * 1.5f));

float duneAmplitude = math.lerp(4f, 1f, depth / 6000f);
float addedHeight = duneSample * duneAmplitude * sedimentDepth * duneFieldMask;

// CELLULAR PITS PASS (Craters / Pockmarks / Subsidence)
float2 cellHash;
float cellDist = CellularDistance01(warpedPos * 0.012f, p.Seed ^ 0xF131A21Eu, out cellHash);

// We want deep pits at the center (cellDist near 0).
float pitProfile = math.saturate(1f - cellDist * 3f); // Only the central 33% of the cell
pitProfile = math.pow(pitProfile, 2.5f); // Make it a bowl shape

// Pits appear in clusters
float pitFieldMask = FractalNoise01(warpedPos * 0.0008f, p.Seed ^ 0x99BBE211u);
pitFieldMask = math.smoothstep(0.5f, 0.7f, pitFieldMask);

// Pits subtract from sediment depth. Max pit depth is 6m.
float pitDepth = pitProfile * pitFieldMask * sedimentDepth * 6f;
```

And talus/scree accumulation code (lines 602 to 610):

```csharp
// TALUS / SCREE ACCUMULATION
float rockBase  = math.saturate(ridgeMask * 0.7f + faultMask * 0.4f + math.saturate((1f - shelfMask) * 1.5f) * 0.3f);
float slope01   = math.saturate(shelfBreakMask * 0.9f + ridgeMask * 0.8f + faultMask * 0.4f);
float screeMask = math.smoothstep(0.05f, 0.30f, slope01) * (1.0f - math.smoothstep(0.40f, 0.65f, slope01));
float screeC    = RidgedMultifractal01(warpedNorm * 140.0f, p.Seed ^ 0xE70D1A5Bu, 3);
float screeF    = RidgedMultifractal01(warpedNorm * 480.0f,  p.Seed ^ 0xC3F19802u, 2);
float screeRubble = ((screeC * 0.7f + screeF * 0.3f) * 2f - 1f) * 35.0f;
depth += screeRubble * screeMask * rockBase;
```

And inside local `WorldTerrainMesoDetailFields.Evaluate` (lines 508 to 522):

```csharp
float slumpLobes = math.smoothstep(0.58f, 0.91f, ValueNoise01(absoluteX, absoluteZ, p.Seed ^ 0x711CE4A9u, 476f)) * slump;
float slumpDelta = -slumpLobes * (5f + 34f * detailGate) * p.SlumpStrengthMeters;

float talusNoise = ValueNoise01(absoluteX, absoluteZ, p.Seed ^ 0xA9C3EF17u, 100f);
float talusDelta = (talusNoise - 0.5f) * (4f + 16f * detailGate) * talus * p.TalusStrengthMeters;

float rubbleNoise = ValueNoise01(absoluteX, absoluteZ, p.Seed ^ 0xC361A27Fu, 45f);
float rubbleDelta = (rubbleNoise - 0.5f) * (1.6f + 6.5f * detailGate) * rubble * p.RubbleStrengthMeters;

float reefNoise = ValueNoise01(absoluteX, absoluteZ, p.Seed ^ 0x91D4C0DEu, 29f);
float reefDelta = (reefNoise - 0.5f) *
    (0.8f + 3.8f * detailGate) *
    reefDetail *
    math.smoothstep(0f, 120f, 120f - depth) *
    p.ReefStrengthMeters;
```

---

## TIER 5: MESO-MICRO (KCC Collision Scale: 1m - 10m)

### Geomorphological Function
Applies high-frequency terrain breakup, rocky ridge details, and sediment wave ripples. This directly deforms the physical collision mesh that the player KCC walks on.

### Mathematical Implementation
Evaluated in [WorldMacroGeologyFields.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/World/WorldMacroGeologyFields.cs#L435-L454):

```csharp
// REALISTIC TECTONIC BREAKUP:
// Macro uses RidgedMultifractal to create sharp, eroded-looking mountain peaks instead of rounded value noise hills.
// Meso and Micro use Simplex for natural organic surface roughness without grid artifacts.
float macroBreakup = RidgedMultifractal01(warpedNorm * 18.0f + new float2(7.7f, 41.3f), p.Seed ^ 0x91E83B37u, 5);
float mesoBreakup = FractalSimplexNoise01(warpedNorm * 48.0f + new float2(-23.1f, 5.6f), p.Seed ^ 0x6C8E9CF5u) * 2f - 1f;
float microBreakup = FractalSimplexNoise01(warpedNorm * 220.0f + new float2(33.1f, -14.6f), p.Seed ^ 0x1A2B3C4Du) * 2f - 1f;

// Apply macro breakup as sharp peaks (subtracting depth) where relief is allowed
depth -= macroBreakup * 350f * reliefGate; 
depth += mesoBreakup * 140f * math.saturate(reliefGate + shelfBreakMask * 0.2f);
float microBreakupWeight = math.saturate(ridgeMask * 0.6f + faultMask * 0.4f + reliefGate * 0.5f);
depth += microBreakup * math.lerp(10f, 60f, microBreakupWeight);
depth += fractureMask * 60f;

// MESO/MICRO DETAIL PASS
float rockDetailNoise = (FractalNoise01(warpedNorm * 150.0f + new float2(-44.2f, 88.1f), p.Seed ^ 0x7B9C1A2Fu) * 2f - 1f);
float rockyRidgeDetail = 1f - 2f * math.abs(FractalNoise01(warpedNorm * 320.0f + new float2(11.4f, -99.3f), p.Seed ^ 0x5E8A9C1Du) - 0.5f);

float hardRockExposure = math.saturate(ridgeMask * 0.6f + faultMask * 0.4f + math.saturate(descent01 * 1.5f) * 0.20f);
float mesoDetailWeight = math.saturate(hardRockExposure * 0.8f + reliefGate * 0.3f);

depth += rockDetailNoise * 35f * mesoDetailWeight;
depth -= rockyRidgeDetail * 30f * mesoDetailWeight * math.saturate(descent01); 
```

And inside local `WorldTerrainMesoDetailFields.Evaluate` (lines 530 to 553):

```csharp
// [MICRO-GEOLOGY CALIBRATION] Add Ridged Noise for Hard Rock/Talus
float rockNoise1 = ValueNoise01(absoluteX, absoluteZ, p.Seed ^ 0x1A2B3C4Du, 15f);
float rockNoise2 = ValueNoise01(absoluteX, absoluteZ, p.Seed ^ 0x4D3C2B1Au, 6f);
float ridged1 = 1f - math.abs(rockNoise1 * 2f - 1f);
float ridged2 = 1f - math.abs(rockNoise2 * 2f - 1f);
// Sharp, aggressive erosion that bites into slopes and talus regions
float rockErosion = (ridged1 * 0.7f + ridged2 * 0.3f) * math.saturate(talus + (slope * 2f));
float rockDelta = -rockErosion * (4f + 16f * detailGate);

// [MICRO-GEOLOGY CALIBRATION] Add Sand ripples (Micro-dunes) for sediment areas
float waveScale = 12f;
float waveDir = 0.785398f; // 45 degrees
float2 waveVec = new float2(math.cos(waveDir), math.sin(waveDir));
float dotPos = absoluteX * waveVec.x + absoluteZ * waveVec.y;
float sineWave = math.sin(dotPos * (3.14159f * 2f / waveScale));
float rippleJitter = ValueNoise01(absoluteX, absoluteZ, p.Seed ^ 0xABCDEF12u, waveScale * 1.5f);
// Warped sine wave for natural looking underwater current ripples
float dunes = math.saturate((sineWave + 1f) * 0.5f + (rippleJitter - 0.5f));
float duneDelta = (dunes - 0.5f) * (1.8f + 2.5f * detailGate) * sediment;

float delta = math.clamp(
    terraceDelta + channelDelta + slumpDelta + talusDelta + rubbleDelta + reefDelta + rockDelta + duneDelta,
    -maxDelta,
    maxDelta);
```

---

## UTILITIES & NOISE KERNELS

### Noise & Hash Library (Verbatim)
Located in [WorldMacroGeologyFields.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/World/WorldMacroGeologyFields.cs#L635-L784):

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static float CellularDistance01(float2 pos, uint seed, out float2 cellHash)
{
    float2 cell = math.floor(pos);
    float2 frac = pos - cell;
    
    float minDist = 8.0f;
    cellHash = new float2(0, 0);

    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 neighbor = new float2(x, y);
            float2 pointHash = Hash2( (int)(cell.x + neighbor.x), (int)(cell.y + neighbor.y), seed);
            float2 diff = neighbor + pointHash - frac;
            float dist = math.length(diff);

            if (dist < minDist)
            {
                minDist = dist;
                cellHash = pointHash;
            }
        }
    }

    return math.saturate(minDist);
}

private static float CellularEdge01(float2 sample, uint seed)
{
    int2 baseCell = (int2)math.floor(sample);
    float first = float.MaxValue;
    float second = float.MaxValue;
    for (int dz = -1; dz <= 1; dz++)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            int2 cell = baseCell + new int2(dx, dz);
            float2 feature = new float2(cell.x, cell.y) + Hash2(cell.x, cell.y, seed);
            float dist = math.length(sample - feature);
            if (dist < first)
            {
                second = first;
                first = dist;
            }
            else if (dist < second)
            {
                second = dist;
            }
        }
    }

    return 1f - math.smoothstep(0.04f, 0.42f, math.max(0f, second - first));
}

private static float FractalNoise01(float2 sample, uint seed)
{
    float amplitude = 0.5f;
    float frequency = 1f;
    float total = 0f;
    float norm = 0f;
    for (int octave = 0; octave < 5; octave++)
    {
        total += ValueNoise01(sample * frequency, seed + (uint)octave * 0x9E3779B9u) * amplitude;
        norm += amplitude;
        amplitude *= 0.5f;
        frequency *= 2.02f;
    }

    return total / math.max(0.0001f, norm);
}

public static float FractalSimplexNoise01(float2 sample, uint seed)
{
    float amplitude = 0.5f;
    float frequency = 1f;
    float total = 0f;
    float norm = 0f;
    for (int octave = 0; octave < 5; octave++)
    {
        total += SimplexNoise01(sample * frequency, seed + (uint)octave * 0x9E3779B9u) * amplitude;
        norm += amplitude;
        amplitude *= 0.5f;
        frequency *= 2.02f;
    }

    return total / math.max(0.0001f, norm);
}

public static float RidgedMultifractal01(float2 sample, uint seed, int octaves = 5)
{
    float amplitude = 1f;
    float frequency = 1f;
    float total = 0f;
    float norm = 0f;
    float weight = 1f; // weight successive octaves by previous
    for (int octave = 0; octave < octaves; octave++)
    {
        float n = SimplexNoise01(sample * frequency, seed + (uint)octave * 0x9E3779B9u);
        // Ridged inversion: 1 - abs(noise * 2 - 1)
        n = 1f - math.abs(n * 2f - 1f);
        n = n * n; // sharpen ridges
        n *= weight;
        weight = math.saturate(n * 2f);
        
        total += n * amplitude;
        norm += amplitude;
        amplitude *= 0.5f;
        frequency *= 2.0f;
    }

    return total / math.max(0.0001f, norm);
}

private static float SimplexNoise01(float2 sample, uint seed)
{
    float2 p = math.floor(sample);
    float2 f = sample - p;
    float2 w = f * f * (3f - 2f * f);

    float a = math.dot(HashGradient(p, seed), f);
    float b = math.dot(HashGradient(p + new float2(1f, 0f), seed), f - new float2(1f, 0f));
    float c = math.dot(HashGradient(p + new float2(0f, 1f), seed), f - new float2(0f, 1f));
    float d = math.dot(HashGradient(p + new float2(1f, 1f), seed), f - new float2(1f, 1f));

    return math.lerp(math.lerp(a, b, w.x), math.lerp(c, d, w.x), w.y) * 0.5f + 0.5f;
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static float2 HashGradient(float2 p, uint seed)
{
    uint h = Hash((int)p.x, (int)p.y, (int)seed);
    float angle = HashToUnitFloat(h) * 6.283185f;
    return new float2(math.cos(angle), math.sin(angle));
}

private static float ValueNoise01(float2 sample, uint seed)
{
    float2 floorSample = math.floor(sample);
    int2 cell = (int2)floorSample;
    float2 local = sample - floorSample;
    float2 smooth = local * local * (3f - 2f * local);
    float a = Hash01(cell.x, cell.y, seed);
    float b = Hash01(cell.x + 1, cell.y, seed);
    float c = Hash01(cell.x, cell.y + 1, seed);
    float d = Hash01(cell.x + 1, cell.y + 1, seed);
    return math.lerp(math.lerp(a, b, smooth.x), math.lerp(c, d, smooth.x), smooth.y);
}
```

Hash functions:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static float2 Hash2(int x, int y, uint seed)
{
    return new float2(Hash01(x, y, seed), Hash01(x, y, seed ^ 0xA511E9B3u));
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static float Hash01(int x, int y, uint seed)
{
    return (Hash(x, y, (int)seed) & 0x00FFFFFFu) * (1f / 16777215f);
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static float HashToUnitFloat(uint value)
{
    value ^= value >> 16;
    value *= 0x7FEB352Du;
    value ^= value >> 15;
    value *= 0x846CA68Bu;
    value ^= value >> 16;
    return (value & 0x00FFFFFFu) * (1f / 16777215f);
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static uint Hash(int x, int y, int seed)
{
    uint hash = (uint)x * 0x8DA6B343u;
    hash ^= (uint)y * 0xD8163841u;
    hash ^= (uint)seed + 0x9E3779B9u + (hash << 6) + (hash >> 2);
    hash ^= hash >> 16;
    hash *= 0x7FEB352Du;
    hash ^= hash >> 15;
    hash *= 0x846CA68Bu;
    hash ^= hash >> 16;
    return hash;
}
```
