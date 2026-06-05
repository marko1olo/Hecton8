# ORGANIC_ENTROPY_MATH

Date: 2026-05-07

Owner domain: world/organic entropy and regrowth math

Status: PENDING VERIFICATION

## Source Anchors

Evidence: STATIC_SOURCE / FILESYSTEM.

Scope: cited local paths exist at capture time. No compile/import/Play/profiler/GC/player/save/platform/visual proof.

- `Assets/_Project/Scripts/World/EntropyYieldJob.cs`

- `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs`

- `Assets/_Project/Scripts/World/FloraRegrowthDirector.cs`

- `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs`

- `Data/Economy/Regrowth_Constants.json`

- `Tools/WorldEntropySim.py`

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.

- Historical actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json` (historical snapshot only; do not use for current counts or proof).

- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.

- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.

- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.

## Historical 2026-05-04 Boundary

- Evidence limit: deterministic entropy/yield contract only; flora final coverage, save delta correctness, and profiler-clean runtime remain unproven.

- Re-open `EntropyYieldJob`, `DestructibleOrganicManager`, and current flora assets before surgery.

## Scope

Deterministic loot and entropy math anchors:

- `Assets/_Project/Scripts/World/EntropyYieldJob.cs`

- `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs`

The system goal is deterministic, Burst-safe, zero-GC yield generation and lifecycle transitions for destroyed indirect flora.

Superseded flora-doc archive:

- `Docs/_Archive/2026-04-29_Organic_Entropy_Doc_Lifecycle/FLORA_DOC_SUPERSESSION_LOG.md`

## XorShift32 RNG

Seed:

```csharp

uint rng = organicEvent.InstanceUid ^ (uint)descriptor.StableHashId ^ 0x9E3779B9u;

```

Step function:

```csharp

state ^= state << 13;

state ^= state >> 17;

state ^= state << 5;

return (state & 0x00FFFFFFu) * (1f / 16777215f);

```

Implications:

- Same flora instance UID and same template always produce the same random stream.

- No `UnityEngine.Random`.

- Safe for Burst and replay.

## Conservation Of Mass

Yield is not arbitrary. The job first resolves recoverable mass and only then converts that mass into discrete inventory units.

Definitions:

- `parentMassKg`: source mass estimated by `DestructibleOrganicManager.ResolveParentMassKg(...)`

- `densityKgPerM3`: material density lookup

- `unitItemMassKg`: authored unit-mass lookup

- `toolPower01`: saturated tool efficiency input

- `minimumRecovery`: minimum recoverable fraction for the material

Recovered mass:

```csharp

recoveredMassKg =

    max(0.05f, parentMassKg) *

    lerp(saturate(minimumRecovery), 1f, toolPower01);

```

Yield volume:

```csharp

yieldVolumeM3 = recoveredMassKg / max(0.01f, densityKgPerM3);

```

Discrete mass contribution:

```csharp

massQuantity = max(1, floor(recoveredMassKg / unitItemMassKg));

volumeBonus = floor(yieldVolumeM3 * 0.35f);

```

This keeps output anchored to source material and source size.

## Weighted Loot Selection

Loot tables are flattened into `LootRuntimeEntry[]`.

Total weight:

```csharp

totalWeight += max(1, LootEntries[lootStart + lootIndex].Weight);

```

Resolved pick:

```csharp

weightedPick = floor(Next01(ref rng) * totalWeight);

```

The job walks the accumulated weight window and resolves exactly one authored loot entry per destroyed flora event.

## Authored Quantity Window

After a weighted entry is selected, authored min/max quantity still applies:

```csharp

authoredQuantity = authoredMin;

if (authoredMax > authoredMin)

{

    authoredQuantity += floor((authoredMax - authoredMin + 1) * Next01(ref rng));

    authoredQuantity = clamp(authoredQuantity, authoredMin, authoredMax);

}

```

## Quality And Rarity

Quality roll:

```csharp

quality01 = saturate(

    Next01(ref rng) * (0.55f + saturate(materialEntry.QualityBias)) +

    toolPower01 * 0.25f +

    saturate(organicEvent.Damage01) * 0.20f);

```

Rarity bands:

```csharp

quality01 >= 0.92f => tier 3

quality01 >= 0.72f => tier 2

quality01 >= 0.42f => tier 1

else               => tier 0

```

Rarity bonus:

```csharp

rarityBonus = rarityTier >= 3 ? 2 : rarityTier;

```

Interpretation:

- `0` = common

- `1` = improved

- `2` = rare

- `3` = exceptional

## Final Quantity

```csharp

finalQuantity =

    authoredQuantity +

    max(0, massQuantity - 1) +

    volumeBonus +

    rarityBonus;

```

Final clamp:

```csharp

Quantity = (ushort)clamp(finalQuantity, 1, ushort.MaxValue);

```

## GPU Entropy Encoding

The flora runtime does not allocate a separate damage buffer. It encodes entropy state into the streamed instance metadata:

- `HeightScale < 0` means entropy/regrowth channel is active.

- `WidthScale > 0` stores the short wilt time anchor.

- `WidthScale < 0` stores the long decomposition time anchor.

- `Variation.frac` remains the visual variation seed.

- `floor(Variation)` is reserved for runtime bit flags.

Shader decode:

```hlsl

if (encodedHeightScale >= 0.0)

    return 0.0;

float entropyDuration = encodedWidthScale < 0.0 ? 600.0 : 0.85;

float entropyStartTime = encodedWidthScale < 0.0 ? abs(encodedWidthScale) : max(0.0, encodedWidthScale);

return saturate((timeValue - entropyStartTime) / entropyDuration);

```

Partial wilt starts only below 50% HP. Full destruction drives the long entropy path over 600 seconds.

Runtime flag packing:

```csharp

encodedVariation = frac(variationSeed) + runtimeFlags;

```

Current bit use:

- bit `0` = `HAS_PARASITE`

Stable flora UID hashing uses only `frac(Variation)` so runtime flags do not change instance identity.

## Decomposition Math

Destroyed flora no longer hard-despawns after a short wilt. It enters a 10-minute decomposition phase.

Definitions:

- `t0`: decomposition start time

- `t`: current shader/runtime time

- `entropy01 = saturate((t - t0) / 600)`

- `smoothEntropy = entropy01^2 * (3 - 2 * entropy01)` (smoothstep)

- `baseHeightScale`: cached initial instance height scale

- `minHeightScale = 0.05`

- `minWidthScale = 0.12`

Runtime height collapse:

```csharp

HeightScale = -lerp(baseHeightScale, minHeightScale, smoothEntropy);

```

Shader-side width collapse:

```hlsl

widthScale = lerp(1.0, 0.12, entropyProgress);

```

World-space collapse bias:

```hlsl

animatedPositionWS = lerp(animatedPositionWS, renderOriginWS, entropyWeight * 0.72);

animatedPositionWS.y -= entropyWeight * instanceHeight * lerp(0.12, 0.58, heightMask);

animatedPositionWS.xz += currentDirection * (-entropyWeight * instanceHeight * 0.03 * heightMask);

```

This produces three visible effects:

1. silhouette shrink

2. top-heavy bend toward the seabed

3. desaturated decay tint in fragment shading

Decay color:

```hlsl

gradientLuma = dot(gradientColor, float3(0.299, 0.587, 0.114));

decayColor = lerp(float3(gradientLuma, gradientLuma, gradientLuma), float3(0.32, 0.29, 0.24), 0.55);

gradientColor = lerp(gradientColor, decayColor, entropyProgress * 0.92);

```

## Seed Dispersal Math

Large destroyed sargassum clusters emit deterministic seed trajectories. Each source flora UID spawns a fixed fan of derived seed UIDs:

```csharp

seedUid = sourceInstanceUid ^ ((seedIndex + 1) * 0x9E3779B9u);

```

Initial lateral spread uses the same XorShift32 family as the loot job:

```csharp

angle = NextSeed01(ref state) * 2pi;

radius = sqrt(NextSeed01(ref state)) * 1.65f;

offset = float3(cos(angle) * radius, lerp(0.12f, 0.45f, NextSeed01(ref state)), sin(angle) * radius);

```

Trajectory integration for `60` seconds:

```csharp

stepVelocity = (sampledAbyssalFlow * 0.72f) + (float3(0f, -0.06f, 0f));

position += stepVelocity * dt;

elapsedSeconds += dt;

```

Landing gate:

1. project to cached terrain height

2. sample terrain slope from cached height differentials

3. accept only if `slopeDegrees <= 30`

Slope approximation:

```csharp

gradientX = (heightPosX - heightNegX) / (sampleDistance * 2f);

gradientZ = (heightPosZ - heightNegZ) / (sampleDistance * 2f);

slopeDegrees = atan(sqrt(gradientX^2 + gradientZ^2)) * Rad2Deg;

```

Accepted seeds are persisted as pending flora deltas with remaining sprout time encoded in `Quantity`:

```csharp

remainingSproutSeconds = 7200;

Flags = FloraSeedPending;

Quantity = remainingSproutSeconds;

```

When the countdown reaches zero, the seed transitions to `FloraSeedReady`.

## Regrowth Contract

Regrowth does not spawn new flora. The existing indirect instance is restored by:

1. keeping the persistence tombstone alive during the regrowth blend

2. restoring matrix translation and `HeightScale` over time

3. removing the destroyed-flora delta only after regrowth completes

That keeps persistence authoritative while the visual return is gradual.

## 2026-05-15 Macro Regrowth Simulation

Implementation surface:

- `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs`

- `Tools/WorldEntropySim.py`

- `Data/Economy/Regrowth_Constants.json`

The macro regrowth model is a data-only world-economy layer; it instantiates no resource or predator GameObjects.

It stores byte lanes per macro-sector: soil nutrients, temperature, biome id, lifecycle stage, tombstone age, regrowth, ore stock, flora stock, prey, predator, apex respawn days.

Daily growth is fixed-point:

```csharp

GrowthRate = BaseGrowthProgressPerDayQ * SoilNutrientsQ * TemperatureQ / (255 * 255);

```

Mining maps a sector cell to `WorldRegrowthStage.Tombstone`, clears progress, applies nutrient penalty, and lets the tombstone decay into `Seed`.

Predator recovery is deterministic. Apex respawn days derive from a byte-quantized Lotka-Volterra projection of prey and predator biomass.

Persistence contract:

- `WorldRegrowthMacroDatabaseCodec.TryPack` writes a contiguous H8_MacroDB payload.

- `TryUnpack` verifies magic, version, width, height, cell count, fixed lane offsets, and FNV-style checksum before restoring lanes.

- Scratch diffusion memory is not serialized; it is restored from `SoilNutrients` on load, then overwritten by the next deterministic diffusion pass.

- Scene-lifetime NativeArray lanes allocate/release through `H8Memory` with `SystemID.WorldStreaming`, while `NativeMemorySentinel` keeps field-level labels.

- `Hecton8.World.Economy.asmdef` directly references `Hecton8.Core.Memory`; the regrowth backend must not rely on transitive access to `H8Memory`.

- Initialization uses allocated grid dimensions after clamp, not raw config dimensions.

- Regrowth memory lanes are scene-lifetime state and are allocated with `Allocator.Persistent`; Temp/TempJob allocation is not allowed for this data block.

- Allocation failure rolls back partially allocated SOA lanes through an H8Memory-only pre-registration release path.
- Reallocation first disposes any partial old lane set.
- Max macro-sector allocation is capped at `1,048,576` cells.

- Scheduler/codec reject regrowth memory unless:
  - `Width`, `Height`, and `CellCount` are coherent and within max-cell budget;
  - every serialized SOA lane length equals `CellCount`;
  - black box ring is exactly 300 entries.

- Scheduler entry points reject invalid coefficients before fixed-point products. Base growth caps at 255; permille coefficients cap at 1000 for the fast int path.

- Mining tombstone writes are serial and deterministic for duplicate cell indices.

- `WorldRegrowthSimulation.TryDumpBlackBox` writes the fixed 300-entry telemetry ring to `Docs/AgentLogs/Dump_ORGANIC_ENTROPY_REGENERATOR.bin` on cold diagnostic paths.

- `WorldEntropySim.py` mirrors the C# `Hash32`/rotate-left biome resolver and reads explicit seed/origin fields from `Regrowth_Constants.json`.

- Negative macro-sector origins use C# remainder semantics before local-z banding; the Python harness carries a parity test for this edge case.

- Python harness uses persistent nutrient scratch, byte-state apex respawn lookup, and row-based diffusion. Acceptance output is unchanged.

- `Tools/test_world_entropy_sim.py` locks exported constants against the C# fast-path config bounds.

- `WorldEntropySim.py` rejects non-positive `--days` values instead of silently clamping them.

- `run_sim()` rejects non-positive days, non-integer days, and any mode value except boolean `True`; test callers cannot bypass the CLI evidence guard.

- `calculate_balance()` validates constants and rejects any mode value except exact boolean `True`.
- Balance status is undefined for baseline runs or malformed acceptance metadata in this export.

- `build_initial_state()` validates constants and requires an explicit boolean mode before allocating per-cell lists, so helper calls cannot bypass the grid cap, byte-lane guards, or helper mode contract.

- Acceptance balance requires required biome recovery evidence; an absent Deep Abyss biome cannot pass total-overharvest from final mature ratio alone.

- Summary recovery days require at least one macro-sector in the biome; absent biomes remain `None` instead of reporting false day-1 recovery.

- The Python harness validates the biome constants contract before simulation: exactly four biomes with ids/names matching runtime indices `0..3`.

- Python harness validates the same fast-path config envelope as the C# scheduler before simulation.
- Bounds: base growth `1..255`, permille coefficients `0..1000`, positive lifecycle thresholds, valid apex min/max days.

- Byte-lane constants validated before simulation: passive recovery, mining penalty, and minimum nutrients must be `0..255`; biome temperature must be `1..255`.
- Biome nutrient start must be `minimumNutrientsQ..255`.

- C# `HasValidConfig` rejects biome nutrient starts below `MinimumNutrientsQ`, so invalid config cannot seed a lane below its own nutrient floor before the first diffusion pass.

- Lifecycle byte thresholds are validated before simulation: seed-to-mature progress, tombstone decay, and apex respawn day bounds must fit the C# byte-field envelope.

- The Python harness rejects grids above `1,048,576` cells before allocating per-cell lists, matching the C# backend cell-budget cap.

- The Python harness validates acceptance metadata before simulation: positive acceptance days, `total_overharvest` mode, maturity ratio in `(0, 1]`, and positive Safe/Abyss recovery ratio.

- The Python harness validates export identity before simulation: schema `H8_Regrowth_Constants`, version `1`, status `ENTROPY BALANCED`, and Unity status `PENDING_UNITY_VERIFICATION`.

- Python harness uses strict scalar validation.
- Integer fields reject bools, floats, and strings.
- Acceptance numeric fields reject bools, strings, and non-finite values.
- `entropyTestWorldSeed` must fit uint32.
- Macro-sector origins must fit int32.

- The CLI exposes only `--mode total_overharvest`; non-acceptance modes are rejected before simulation.

- Exported biome expected half-recovery days must be positive and within `acceptance.simulationDays` before the harness runs.

Entropy-test result:

- Command: `python Tools/WorldEntropySim.py --days 365 --mode total_overharvest`

- Result: `STATUS=ENTROPY BALANCED`

- Safe Shallows half recovery: `28` days.

- Deep Abyss half recovery: `88` days.

- Ratio: `3.143`, satisfying the 3x requirement.

- Seeded biome counts: Safe Shallows `1729`, Temperate Reef `996`, Thermal Vent `564`, Deep Abyss `807`.

Verification boundary: `STATIC_DOC` plus historical local command text only.

Current Python harness evidence requires artifact path, command, timestamp, environment, and output. Unity import, Play Mode, profiler, GCMonitor, and player build remain pending.

Post-hardening verification:

- Historical local command text: `python Tools/WorldEntropySim.py --constants Data/Economy/Regrowth_Constants.json --days 1000 --mode total_overharvest` reported `STATUS=ENTROPY BALANCED`; mature counts stable through day 1000.

- Historical local command text: `python Tools/WorldEntropySim.py --constants Data/Economy/Regrowth_Constants.json --days 365 --mode total_overharvest` reported `STATUS=ENTROPY BALANCED`; elapsed 68.723 s under then-current machine load.

- Historical local command text: `python Tools/WorldEntropySim.py --constants Data/Economy/Regrowth_Constants.json --days 1000 --mode total_overharvest` reported `STATUS=ENTROPY BALANCED`; mature counts stable through day 1000.

- Historical local command text: `python -m unittest Tools.test_world_entropy_sim -v` reported 25 tests passed in 107.456 s.

- Local command text: Visual Studio Roslyn C# 9 probe compile against Unity/Hecton8 stubs reported exit code `0`; re-run after config overflow guard tightening remained exit code `0`.

- Treat command lines above as historical local text unless paired with fresh artifact path and timestamp.
- Pending proof: Unity import, Burst compile, Play Mode, profiler, GCMonitor, player build.

- Static scans: no forbidden hot-path token matches and no raw `new NativeArray` or raw native dispose remains in `WorldRegrowthSimulation.cs`.

- Full Unity import and Burst compile remain `PENDING VERIFICATION` because no Unity CLI/editor route was available in-session.

Status: PENDING VERIFICATION

Verification: PENDING VERIFICATION
