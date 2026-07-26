# Generation Stack Contracts — terrain, voxel, PureLogic, Burst

Date: 2026-07-26
Status: `POLICY_DOC` / `STATIC_SOURCE` — derived from live source and executed numeric
verification. No Unity, profiler, or player proof is claimed here.
Owner domain: cross-layer contracts for the world-generation stack.
Route: `AGENTS.md` → `PROJECT_BIBLES.md` → `voxels.md` / `terrain.md` → this file.

## Why this file exists

`voxels.md` already owns the SDF sign convention, extraction requirements, seam rules and
the collision read model. It is a good bible and this file does not restate it.

What it does not state — because it reads as too obvious to write down — is the **surface
orientation invariant**. The clause *"deterministic edge ownership"* assumes an invariant
that is currently satisfied by two conventions cancelling each other out. Neither convention
is written anywhere, and each looks wrong in isolation. A future agent tidying either one
will invert every cave wall in the game and the bible will not stop them.

This file records the load-bearing contracts between layers: what may cross the
PureLogic/Burst boundary, which corner layout applies where, and who owns each duplicated
truth. It is the connective tissue, not a domain bible.

---

## 1. The surface orientation invariant (load-bearing, do not "fix")

### 1.1 The two conventions

**Corner layout.** `VoxelMCExtractJob.CubeDensities` (`HectonVoxelEngine.cs`) numbers cube
corners as:

| Index | Offset | | Index | Offset |
|---|---|---|---|---|
| d0 | (0,0,0) | | d4 | (0,0,1) |
| d1 | (1,0,0) | | d5 | (1,0,1) |
| d2 | (1,1,0) | | d6 | (1,1,1) |
| d3 | (0,1,0) | | d7 | (0,1,1) |

The canonical Marching Cubes (Bourke) layout that both shipped tables were authored for is
`0(0,0,0) 1(1,0,0) 2(1,0,1) 3(0,0,1) 4(0,1,0) 5(1,1,0) 6(1,1,1) 7(0,1,1)`.

**These differ: the engine layout is Bourke with y and z swapped.** Six of eight indices
disagree. Swapping two axes is a reflection — the Bourke basis
`(c1−c0, c3−c0, c4−c0)` has determinant `−1`, the engine basis has `+1`. A reflection
**flips triangle winding exactly once**.

**Set region.** `ResolveCubeIndex` sets bit *i* when `d_i < 0f`. Per `voxels.md`, negative is
void. Confirmed independently in source: `solidNeighborCount` counts neighbours with
`> 0f` as solid. So **the bit-set region is AIR, not rock** — the opposite of the textbook
formulation, where the set region is the solid being extracted.

### 1.2 Why the result is correct

The Marching Cubes triangle table winds triangles to face *away from* the set region. With
AIR as the set region that would face into the rock — wrong. The mirrored corner layout then
flips the winding a second time. **Two flips cancel: triangles face into the open cavity,
toward the player.**

Verified numerically, not asserted: simulating the engine's exact emission path (its own
`edgeTable`/`triTable` values, its corner layout, its `ResolveCubeIndex` rule) against a
sphere-cavity SDF produced **1208 of 1208 triangles winding toward the cavity interior**.
100 % consistency also proves the mesh is not torn — a layout/table mismatch would produce a
mixed orientation, not a uniform one.

The shading normal agrees. `normal = -gradient(density)`: density rises into rock, so
`-gradient` points out of rock, into the cavity. **Winding and shading normal both face the
open space.** Nothing here needs a screenshot to be trusted; it needs one only to catch a
regression elsewhere in the material or culling setup.

### 1.3 The rule

> **Corner layout and set-region polarity are a matched pair. Changing either one alone
> inverts every generated surface in the game.**

Forbidden without changing both together and re-deriving the invariant:

- renumbering `CubeDensities.d0..d7` to canonical Bourke order;
- flipping `ResolveCubeIndex` to `d > 0f`;
- replacing `edgeTable`/`triTable` with tables from a different source that assume a
  different corner numbering;
- negating the density sign convention in the SDF contract.

### 1.4 How to re-verify in ten minutes, without Unity

Do not trust this prose over source. Re-derive it:

1. Read the corner offsets from `CubeDensities` construction and the bit rule from
   `ResolveCubeIndex`.
2. Take the edge → corner-pair map the tables assume:
   `e0(0,1) e1(1,2) e2(2,3) e3(3,0) e4(4,5) e5(5,6) e6(6,7) e7(7,4) e8(0,4) e9(1,5) e10(2,6) e11(3,7)`.
3. Regenerate the 256-entry edge table from first principles — edge *e* is crossed exactly
   when its two corner bits differ — and diff it against the shipped table. It must match on
   all 256 entries. (It does today.)
4. Evaluate a sphere SDF `|p − c| − r` on a grid, emit triangles through the real tables,
   and check `dot(cross(v1−v0, v2−v0), centroid − c)`. For a cavity (negative inside) every
   triangle must point **toward** the centre.

If step 3 mismatches, the tables and the layout have drifted apart. If step 4 comes back
mixed, the mesh is torn. If step 4 comes back uniformly outward, the surface is inside-out.

---

## 2. Two corner orders exist in the voxel path — know which is which

They are not interchangeable, and they sit in the same subsystem:

| Consumer | Order | Index formula |
|---|---|---|
| Marching Cubes extraction (`CubeDensities`, `edgeTable`, `triTable`) | face-loop order, mirrored Bourke | not a bit formula — see the table in 1.1 |
| `VoxelSdfTrilinearInterpolationCalculator`, and the inline trilinear in `HectonVoxelVolume` | axis-bit order | `index = x + 2y + 4z` |

Both live callers are currently correct: `HectonVoxelVolume` builds its corner array as
`c000, c100, c010, c110, c001, c101, c011, c111`, which is the axis-bit order the
interpolator documents.

The hazard is a future author filling an eight-element corner array in Marching Cubes order
and passing it to the trilinear sampler, which is a natural mistake because both are
"the eight corners of a voxel". Measured cost of that mistake on a flat-ceiling SDF:
**9 of 27 sample points land on the wrong side of the surface** — holes and phantom walls.

**Rule:** any new API taking eight corner values must name its order in the signature
(`cornersXyzBitOrder`, `cornersMarchingCubesOrder`) or take a struct that cannot be
filled in the wrong order.

---

## 3. The PureLogic ↔ Burst boundary has no contract, and is being violated

### 3.1 The situation

`Hecton8.PureLogic.asmdef` sets `"noEngineReferences": true`. The layer is therefore pure
C#: no `Unity.Mathematics`, no `NativeArray`. That is the right call for testability and it
matches `COMMON_SENSE.md` #18.

But the layer **is called from `[BurstCompile]` jobs**. Confirmed call sites in
`HectonVoxelEngine.cs`:

| Callee | Calling job |
|---|---|
| `VoxelCellDirtystateBitHashingCalculator.Compute` | `VoxelDensityJob`, `VoxelColorJob`, `VoxelDirtyBlendJob` |
| `MarchingCubesLookupTable.Calculate` | `VoxelMCExtractJob` |

No document states which PureLogic members are Burst-callable. Nothing enforces it. A static
scan of all 199 implementation files finds **24 files carrying constructs that are illegal or
hazardous inside Burst**:

- 18 files `throw`;
- 6 allocate arrays (several are one-time `static readonly` tables, which is a different
  problem — see 3.2 — not a per-call allocation);
- 1 uses managed collections;
- `VoronoiBiomeSeedCalculator` alone carries five: `throw`, a **formatted string return**,
  `ToString`, a `Func<>` delegate parameter, and a `try/catch` inside its search loop.

`VoxelCellDirtystateBitHashingCalculator` and `LodChunkSelector` are clean — zero throws,
zero allocations, zero string work. They show the contract is achievable.

### 3.2 The concrete defect this exposed

`VoxelMCExtractJob` declares a native edge table:

```csharp
[ReadOnly, NoAlias] public NativeArray<int>.ReadOnly edgeTable;
[ReadOnly, NoAlias] public NativeArray<int>.ReadOnly triTable;
```

`triTable` is indexed normally (nine sites). **`edgeTable` is never indexed anywhere in the
file** — the single occurrence of `edgeTable[` is line 244, the initialisation loop that
fills it. It is allocated in the DataVault, populated, write-locked, shipped into the job as
payload, and validated by `HasSafeMarchingCubesInputs` — and then never read.

The lookup instead goes through `MarchingCubesLookupTable.Calculate`, which indexes a
**managed `static readonly int[]`**. Burst does not support managed array access. So the
hottest job in cave meshing either fails to Burst-compile and silently runs managed — which
would cost roughly an order of magnitude on the cave path — or errors at schedule time.

Three problems in one line:

1. Burst legality;
2. a DataVault buffer paid for and never used;
3. the same 256-entry table existing twice, free to drift.

**Fix — use the native table that is already in the job:**

```csharp
// HectonVoxelEngine.cs, the MarchingCubesLookupTable.Calculate line in VoxelMCExtractJob
int edgeBits = edgeTable[cubeIndex];
if (edgeBits == 0) return;
```

`cubeIndex` comes from `ResolveCubeIndex` and is `0..255` by construction, and
`HasSafeMarchingCubesInputs` already guarantees `edgeTable.Length >= 256`, so the index is
safe without further checks. This deletes the managed access, makes the native buffer earn
its cost, and leaves one owner for the table.

**Correction to an earlier recommendation.** A non-throwing `TryCalculate` was added to
`MarchingCubesLookupTable` and is useful for *managed* callers, but it does **not** fix this
call site: it still indexes the same managed static array, so it is no more Burst-legal than
`Calculate`. The native-table line above is the correct fix for the job. Do not substitute
one for the other.

### 3.3 Proposed contract

Add to `voxels.md` or `data.md`, and enforce it in the mandate registry lint:

> A PureLogic member called from Burst-compiled code must not throw, must not allocate, must
> not touch managed arrays, collections, strings or delegates, and must return unmanaged
> values through `out` parameters or a return value. Members that cannot meet this must be
> marked managed-only in their XML doc. Burst-callable members index only caller-supplied
> unmanaged buffers — never a static table in this layer.

Lookup tables that Burst needs belong in a `NativeArray` owned by the caller or the
DataVault. `MarchingCubesLookupTable.EdgeTable` may stay as the authoring/reference copy and
as the source the engine initialises the native table from, which is exactly what line 244
already does.

---

## 4. Duplicated truths — owner rulings needed

Each of these is one fact with two implementations. `AGENTS.md`: *one fact → one owner →
one route → one proof artifact.* None is currently broken; all are free to drift.

| Fact | Implementations | Suggested ruling |
|---|---|---|
| MC edge table | managed static in PureLogic; native copy in DataVault | Native is the runtime owner; managed is authoring/init source only. Resolved by the fix in 3.2. |
| Trilinear SDF sample | `VoxelSdfTrilinearInterpolationCalculator`; inline `math.lerp` version in `HectonVoxelVolume` | Keep both **deliberately** — PureLogic cannot use `Unity.Mathematics`, so a Burst job needs the inline one. Document the split so it is not "cleaned up". Same corner order in both today; a test should pin that. |
| Player core temperature | `CoreTempEquilibriumSolver` (orphaned, nothing calls it); `HectonSurvivalSystem.ResolveExponentialTemperatureStep` (live) | The live inline path is the owner and is numerically correct. Either delete the orphan or wire it and delete the inline copy — but not both alive. |
| Ambient water temperature | `AmbientTemperatureDepthGradientCalculator` (exists, tested); the HUD's own depth+light estimate | The calculator should be the owner. The HUD estimate is documented as temporary in `UI_HUD_V4_PROGRESS.md` and should be replaced. |

---

## 5. Terrain ↔ voxel seam: verified clean, recorded so it is not re-litigated

Checked as part of this pass, no defect found. Recorded because these are easy to
"improve" into breakage:

- **`TerrainSeamDitherAlphaCalculator`** — the 4×4 threshold matrix is the canonical Bayer
  matrix (`+1`, normalised by 17), verified by regenerating it from the recursive
  construction. Dividing by 17 rather than 16 is deliberate and correct: it keeps every
  threshold inside `(0,1)`, so `blend = 0` is fully transparent and `blend = 1` fully opaque.
  Negative coordinates are handled correctly by `x & 3` (two's complement wraps to `0..3`,
  unlike `%`). Its index is `x*4 + y`, which transposes the pattern relative to the more
  common `y*4 + x`; dispersion is preserved, so this is cosmetic and should be left alone
  unless art asks.
- **`VoxelMeshHeightSeamBlendCalculator`** — correct. Only the degenerate
  `blendWidth <= 0` branch uses exact float equality, which is fragile but unreachable in
  normal configuration.
- **`HectonTerrainLitPasses.hlsl`** — every `half3` is a direction or normal
  (`viewDirWS`, `normalWS`, `tangentWS`); `positionWS` stays full `float`. Compliant with
  `COMMON_SENSE.md` #4; there are no half-precision world positions to fix.
- **`HectonTerrainSampling.hlsl`** — correctly avoids reopening
  `CBUFFER_START(UnityPerMaterial)`, which `TerrainLitInput.hlsl` already opens and closes.
  SRP Batcher compatibility depends on this; the existing comment at the top of the file
  should not be removed.
- **`HectonTerrain.shader`** — the 31 `multi_compile` directives are the URP-required set,
  and the file already documents a deliberate `shader_feature` conversion citing
  `COMMON_SENSE.md` #16. No variant-hygiene action needed.

---

## 6. Open architectural questions, not answered here

Listed so they are not mistaken for settled:

1. **No real hazard-source producer exists.** `Thermodynamics/MockHazardGenerator.cs` seeds
   one 1000 °C source and one radiation leak when "WFC/geology hazard producers are absent",
   behind an `enableMockHazards` flag. The world therefore has no genuine thermal or
   radiation hazards from geology. `FIRST_20` lists thermal as a valid hazard, so this blocks
   that route moment. The file name also violates the Hollow System Ban's word list.
   It additionally stores a source id in `HazardSourceDTO._pad0` — a padding field used as
   data, against the ARM64 layout mandate.
2. **Scatter budget has no floor and no hysteresis.**
   `ProceduralFoliageScatterBudgetCalculator` returns `baseBudget × fpsRatio × qualityWeight`.
   At `GlobalQualityWeight = 0` it returns exactly zero instances — a barren world, where the
   scalability law calls `0.0` *"minimum survival presentation"* and forbids approximations
   that look primitive. It is also stateless and reacts to `currentFps` instantly: on a frame
   rate jittering 47–63 fps around a 60 fps target, the budget churns ~6600 instances over
   ten frames, which is visible popping. `AGENTS.md` requires a 2–3 s or 3–5 m hysteresis
   band on any scalability switch. Both fixes need a design decision (floor value, band
   width) and belong to the caller, `ScatterBudgetController`, since hysteresis needs state.
3. **`VoronoiBiomeSeedCalculator` returns a formatted string** (`"biome,0.123"`), forcing
   callers to parse it back and quantising the blend factor to three decimals. In a
   world-generation path this contradicts the zero-GC law outright. A non-allocating
   overload returning `out int biomeIndex, out float blend` would fix it additively without
   touching the existing signature or its tests.

---

*Derivation basis: `HectonVoxelEngine.cs` (corner layout, `ResolveCubeIndex`, job fields,
table init and indexing), `HectonVoxelVolume.cs` (trilinear call sites), `voxels.md`,
`terrain.md`, `Hecton8.PureLogic.asmdef`, and a full static scan of 199 PureLogic
implementation files. Table correctness and surface orientation were established by executed
numeric derivation, not by inspection. No Unity import, Play Mode, profiler or visual proof
is claimed by this document.*
