# ORGANIC_ENTROPY_MATH

Status: REFERENCE
Verification: PENDING VERIFICATION

## 2026-05-01 Current-State Boundary

- Read `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using this math reference as current runtime truth.
- This document is the intended deterministic entropy/yield contract, not proof of complete flora final coverage, save delta correctness, or profiler-clean runtime.
- Re-open `EntropyYieldJob`, `DestructibleOrganicManager`, and current flora assets before surgery.

## Scope

This document captures the deterministic loot and entropy math implemented in:

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
