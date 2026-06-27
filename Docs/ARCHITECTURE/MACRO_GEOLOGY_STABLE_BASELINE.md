# Зацементированный эталон базовой геологии

Этот документ фиксирует стабильное состояние макро-геологии и генерации террейна Hecton-8. Геоморфология (террасы, макро-шумы, линковка коллайдеров и генерация высот) полностью стабилизирована и защищена от разрушения.

---

## Текущий статус состояния террейна и выполненные работы
1. **Зеленый цвет террейна**: Шейдер `Hecton8/URP/Terrain_TextureArray` успешно применяется к `materialTemplate`. Текстуры `DeepSea_AlbedoArray` залинкованы напрямую, макро-вариации шума интегрированы в пиксельный пайплайн.
2. **Линии гизмо и артефакты**: Специфические белые изолинии от `WorldProceduralScatterDirector` подавлены путем уничтожения директора из тестовой сцены перед рендерингом (поскольку `[DrawGizmo]` вызывается Unity-редактором даже при выключенном компоненте).
3. **Вертикальные столбы**: Обнаружены и устранены нежелательные mesh-объекты (MeshRenderer/SkinnedMeshRenderer), создававшие геометрические вертикальные артефакты на снимках.
4. **Яркость и Освещение**: Направление основного и заполняющего света скорректировано (спрятано под террейн `(0, -99999, 0)`, чтобы убрать иконки источников), интенсивность экспозиции в объеме постобработки поднята до `3.5f`, тени отключены для чистоты PBR-снимка в batchmode.

---

## 1. Continuous Coordinate Wrapping / Domain Warping
Используется для гладкого искривления координат перед расчетом высокочастотных шумов без использования грубых разрывов типа `math.abs`.

```csharp
// WorldMacroGeologyFields.cs (Evaluated inside EvaluateHeightMeters)
float extent = math.max(MinimumWorldExtentMeters, p.WorldExtentMeters);
float half = extent * 0.5f;
float2 pos = new float2(absoluteX, absoluteZ);
float2 norm = pos / extent;
float lowWarp = (FractalNoise01(norm * 2.0f + new float2(11.7f, -3.9f), p.Seed ^ 0xB5297A4Du) * 2f - 1f) * 980f;
float midWarp = (FractalNoise01(norm * 4.4f + new float2(-2.1f, 8.6f), p.Seed ^ 0x4CF5AD43u) * 2f - 1f) * 520f;
float highWarp = (FractalNoise01(norm * 7.2f + new float2(-17.2f, 29.3f), p.Seed ^ 0x68E31DA4u) * 2f - 1f) * 240f;

// DOMAIN WARPING: To break the "plastic" value noise look, we perturb the coordinates for high-frequency noise.
float warpX = (FractalSimplexNoise01(norm * 12.0f, p.Seed ^ 0x8A1F3C4Du) * 2f - 1f) * 0.005f; 
float warpZ = (FractalSimplexNoise01(norm * 12.0f, p.Seed ^ 0x3B8E1D2Fu) * 2f - 1f) * 0.005f;
float2 warpedNorm = norm + new float2(warpX, warpZ);
float2 warpedPos = warpedNorm * extent;
```

---

## 2. Eroded Terraces
Масштабное террасирование (3-5 крупных уступов на 400 метров высоты вместо частых пиксельных полок Minecraft), использующее эрозию, наклон пластов и точечную маску распределения.

```csharp
// WorldMacroGeologyFields.cs (Tectonic Terracing section)
float terraceStrength = math.saturate(shelfBreakMask * 0.8f + ridgeMask * 0.4f + faultMask * 0.5f);
if (terraceStrength > 0.05f)
{
    // STEP 1: LARGE STEPS → only 3-5 terraces on a 400m mountain.
    // 80-180m: wide geological platforms, not pixel-height Minecraft slabs.
    float dynamicTerraceScale = math.lerp(80.0f, 180.0f,
        FractalSimplexNoise01(warpedNorm * 3.0f, p.Seed ^ 0x112233u));

    // STEP 2: STRATA TILT via pos (meters). 50m per km = 1-2 step shifts across mountain.
    float2 tiltDir = math.normalize(new float2(
        FractalSimplexNoise01(warpedNorm * 1.8f, p.Seed ^ 0xAB12CD34u) * 2f - 1f,
        FractalSimplexNoise01(warpedNorm * 1.8f, p.Seed ^ 0x56EF78ABu) * 2f - 1f
    ));
    float strataCoord = depth + math.dot(tiltDir, pos) * 0.05f;

    // STEP 3: EROSION at mountain scale. ±60m+±25m on 80-180m steps = 0.33-0.75 step shift.
    // Merges/kills whole terraces in patches rather than just wiggling edges.
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
    // smoothstep(0.60, 0.92) with NO floor: passes only top 32% of noise distribution.
    float terracePatchMask = math.smoothstep(0.60f, 0.92f,
        FractalSimplexNoise01(warpedNorm * 4.5f, p.Seed ^ 0x992211AAu));

    // STEP 6: MAX BLEND 0.55 — macro shape always reads through.
    depth = math.lerp(depth, terracedDepth, terraceStrength * terracePatchMask * 0.55f);
}
```

---

## 3. Micro-Grit & Ripples (Micro-Geology)
Микро-деформации и дюны на песчаных участках террейна, наложенные поверх макро-структур.

```csharp
// WorldTerrainDetailContracts.cs
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
```

---

## 4. Asynchronous Wait & Stable Check Loop (WaitAndRender)
Синхронизационный цикл ожидания генерации MapMagic перед созданием снимков, проверяющий готовность коллайдеров и альфамап-текстур.

```csharp
// TerrainRenderTestGoal.cs (Wait and stability loop)
// Give threads 200ms to start before we poll
System.Threading.Thread.Sleep(200);

int loops = 0;
int stableCount = 0;
while (loops < TimeoutLoops)
{
    mm.Update();
    Den.Tools.Tasks.CoroutineManager.Update();
    System.Threading.Thread.Sleep(50);
    loops++;

    int terrainCount = UnityEngine.Terrain.activeTerrains.Length;
    bool allTerrainsReady = true;
    foreach (var t in UnityEngine.Terrain.activeTerrains)
    {
        if (t == null || t.terrainData == null || t.terrainData.alphamapTextureCount == 0)
        {
            allTerrainsReady = false;
            break;
        }
        var col = t.GetComponent<TerrainCollider>();
        if (col == null || col.terrainData == null)
        {
            allTerrainsReady = false;
            break;
        }
    }

    bool isGenerating = mm.IsGenerating();

    // Log every 10 seconds (200 loops) to show explicit progress
    if (loops % 200 == 0)
    {
        float maxH = 0f;
        foreach (var t in UnityEngine.Terrain.activeTerrains)
            if (t.terrainData != null) maxH = Mathf.Max(maxH, t.terrainData.size.y);
        Debug.Log($"[TRT] loop={loops}  terrains={terrainCount}  generating={isGenerating}  stable={stableCount}  maxTerrainHeight={maxH}");
    }

    // Strict barrier: 9 terrains, MapMagic idle, all terrains have collider and alphamaps
    if (!isGenerating && terrainCount == ExpectedTerrains && allTerrainsReady)
    {
        stableCount++;
    }
    else
    {
        stableCount = 0; // Reset if MapMagic starts generating again
    }

    // Require 400 consecutive loops (20 seconds) of complete idle state to proceed safely
    if (stableCount >= 400)
    {
        Debug.Log($"[TRT] Done! Terrains={terrainCount}  stable={stableCount}  generating={isGenerating}  allTerrainsReady={allTerrainsReady}");
        break;
    }
}
```
