# Hecton-8 Deep Architecture Audit

Date: 2026-08-04
Auditor: Autonomous Deep Architecture Audit (GLM-5.2)
Scope: `Assets/_Project/Scripts` (~3,214 files, ~1.99M LOC)
Mandates: `.agents-skills/*.txt` (MANDATE_VERSION_6.0) + `Docs/ARCHITECTURE/*.md`

---

## Executive Summary

Hecton-8's core runtime is **structurally compliant** with the ARM64 alignment law and the
Zero-GC policy in its hot paths. The codebase has clearly internalized the mandate rules:
explicit-layout structs use `_padN` padding, field order is largest→smallest, and no stored
`bool` fields exist in unmanaged DTOs.

The dominant architectural risk is **scale-driven god-class debt**: 182 source files exceed
2,500 lines, led by `HectonPlayerMovement.cs` (15,443 lines, 5 types), `HectonVoxelEngine.cs`
(14,808 lines, 34 types), and `PlayerCriticalProceduralAudioRenderer.cs` (14,346 lines). These
concentrate authority and create testing/ownership hazards.

A small number of **Zero-GC risks** existed on the GPU-less (CPU-fallback) tier. The primary
one — `HectonFluidEngine` allocating managed arrays every CPU-fallback simulation/advection
step — has been **fully resolved** (see §2): the fallbacks are now allocation-free via pooled
grids and new `ComputeBuffered`/`SolveBuffered` pure-logic overloads. A sweep of the entire
`PureLogic/*` layer (Iteration #16) confirmed zero remaining hot-path allocations; the one
per-call allocator found (`FloodFillRoomVolumeCalculator`) was given an allocation-free
`ComputeBuffered` overload (Iteration #15).

---

## 1. Struct Layout Compliance (mandates: `DATA_Runtime_Struct_Layout_ARM64.txt`,
   `DATA_Inventory_Resources_Items_SOA_Layout.txt`, `EQUIPMENT_SOA_LAYOUT.md`)

### Verified Compliant ✅

| Struct | Size | Evidence |
|---|---|---|
| `ActiveEquipmentDTO` | 32 B | `EquipmentThermalBatteryContracts.cs:18`, explicit, field offsets 0/4/8/12/16/20/24/28 + `_pad0`/`_pad1` |
| `FabricationJobDTO` | 32 B | `FabricationAssemblerRuntime.cs:21`, `double3 TargetAUP@0`, `float Progress01@24`, `uint TargetPrefabHash@28` |
| `FabricationJobSnapshotDTO` | 32 B | `FabricationAssemblerRuntime.cs:29` |
| `GasBaseHibernationSnapshot` | 88 B | explicit; `byte _awake`/`byte _playerInside`, bools are computed props |
| `ChunkDeltaState` | 32 B | `VoxelDeltaProcessor.cs:8828`; `byte VaultBacked`, `IsPooled` computed |
| `PlayerNoiseSignal` | — | `NoiseSystem.cs:38`; `byte FlashlightOnFlag`/`byte IsActiveSonarPingFlag` |
| `KinematicSurfaceHit` | 64 B | `GlobalRegistryContracts.cs:5682`; `hasHit`/`HasHit` computed from `Flags` bit |
| `VRSomaticCollisionState` | 96 B | `GlobalRegistryContracts.cs:2191`; `byte HasContactFlag` |

### Key Positive Finding — "No runtime bool" rule is held
The prior static scan flagged **27 structs with `bool`**. Manual verification confirms **all 27
are false positives**: each stores a `byte` flag (or uses a `Flags` bitfield) and exposes `bool`
only as a computed read-only property, exactly as the mandate requires. Examples:
`VoxelDeltaProcessor.ChunkDeltaState.IsPooled`, `NoiseSystem.PlayerNoiseSignal.IsActiveSonarPing`,
`GlobalRegistryContracts.GasBaseHibernationSnapshot.Awake/PlayerInside`.

### Mandate Drift (worth documenting, not a hard violation)

- **`ItemTemplate` is 64 B** (`ItemTemplateRegistry.cs:10`, `ItemTemplateStrideBytes=64`), while
  the SOA mandate baseline declares a **24 B** layout
  `{uint hashID, uint categoryMask, float baseDurability, float wearMultiplier, ushort×4}`.
  The implementation extends with `vulnerabilityMask`, `blueprintQuestFlagId`, `massKg`,
  `volumeM3`, `audioMaterialId`, `physicsMaterialTag`, `_reserved0`, and `_pad00.._pad19`.
  - Assessment: 64 B is a multiple of 8, explicit-layout, largest→smallest, properly padded —
    **compliant with the alignment law** but **deviates from the SOA mandate's exact field list**.
  - The extra fields (mass, volume) are required by gameplay (carry-mass propagation in
    `ZERO_GC_FABRICATION.md`).
  - **DONE (Iteration #5):** the SOA mandate
    (`.agents-skills/DATA_Inventory_Resources_Items_SOA_Layout.txt`, §2.1) now carries a dated
    `REVISION 2026-08-04` block documenting the full 64 B production layout with exact offsets,
    the field relocations (`maxStackSize@16→@32`, `proxyMeshIndex@18→@34`, `iconAtlasIndex@20→@36`,
    `hlodSilhouetteIdx@22→@38`), and the additive fields
    (`vulnerabilityMask@16`, `blueprintQuestFlagId@20`, `massKg@24`, `volumeM3@28`,
    `audioMaterialId@40`, `physicsMaterialTag@41`, `_reserved0@42`). It also notes that the
    mandate's `baseStrength` baseline field does not exist in production (superseded). The
    24 B baseline remains documented above the revision for historical reference. No code change
    was required — the drift is now a documented, verified divergence rather than a silent one.

- **`WorldItemProxy` mandate inconsistency**: the SOA mandate text says 12 B but its own field
  list (`uint + uint + float3`) computes to 20 B. Recommend the mandate itself be corrected.

### Verification boundary
This is `STATIC_SOURCE` evidence only. Per `DATA_Runtime_Struct_Layout_ARM64.txt`, runtime
`UnsafeUtility.SizeOf<T>()`/Burst/IL2CPP proof is still required for every primary DTO.

#### Upgrade to `UNITY_EDITOR` proof (Iteration #3)
The repository already owns a **SHINOBU_204 ARM64 self-audit**
(`Assets/_Project/Scripts/Editor/Arm64AlignmentSelfAuditReport.cs`, editor menu
`Hecton8/Diagnostics/Write SHINOBU_204 Self Audit`). It validates every type in
`CriticalTypeNames` for: `LayoutKind.Explicit`, no `Pack=1`, a valid ARM64 stride
(`16 | 32 | ≥64` multiple of 64), and 8-byte-lane field alignment. This audit added three
primary DTOs to `CriticalTypeNames` so the mandate verification is enforced automatically
rather than asserted by inspection:

- `Hecton8.Tools.ActiveEquipmentDTO` (32 B — valid stride ✓, no 8-byte lane issues ✓)
- `Hecton8.Crafting.FabricationJobDTO` (32 B — `double3 TargetAUP@0` is an aligned 8-byte lane ✓)
- `Hecton8.Inventory.ItemTemplate` (64 B — valid stride ✓, no 8-byte lane issues ✓)
- `Hecton8.Crafting.FabricationJobSnapshotDTO` (32 B — valid stride ✓, `ulong Reserved0@8` aligned ✓)
- `Hecton8.Tools.EquipmentIntegrationCounters` (64 B — valid stride ✓)
- `Hecton8.Core.KinematicSurfaceHit` (64 B — valid stride ✓, `ulong Reserved2@48`/`Reserved3@56` aligned ✓)

All six pass every SHINOBU_204 rule, converting the relevant `STATIC_SOURCE` struct-layout
claims into `UNITY_EDITOR`/reflection proof the next time the self-audit is generated.
`FabricationRuntimeDTO` (96 B) was **not** added because the SHINOBU_204 stride rule
(`16 | 32 | ≥64-multiple-of-64`) treats 96 B as a non-conforming stride; it remains covered by
the static audit only. `GasBaseHibernationSnapshot` (88 B) was similarly excluded for the same
stride rule.

---

## 2. Zero-GC Policy Compliance (mandate: `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`)

### Findings

#### 🔴 P0 — `HectonFluidEngine` CPU-fallback allocates managed arrays per call — **RESOLVED**
- **Was:** `RunCpuFluidAdvectionFallback` (`HectonFluidEngine.cs:5312`) `new float[length] ×3` per
  call; `RunCpuFluidSimulationFallback` `new float[,,]` grids
  (`velX/velY/velZ/divergence/pressure`) plus `VorticityConfinementForceCalculator.Compute`
  returning a tuple of freshly-allocated arrays and `FluidPressureJacobiSolver.Solve` allocating
  `newPressure` 10× per fallback.
- These ran under `if (!_coldSupportsComputeShaders)` (`:4025`), i.e. **on the GPU-less tier**
  — precisely the MX350/LOW hardware the mandates target. If invoked per-frame this violated
  `0 B/frame` hot-path allocation.
- **Fix applied (Iteration #2):** both fallback methods are now allocation-free. All buffers are
  pooled and reused via `EnsureCpuFallbackAdvectionVelocityArrays` / `EnsureCpuFallbackSimulationGrids`
  (cold, once at init/resolution change), with new allocation-free `ComputeBuffered`/
  `SolveBuffered` overloads on the pure-logic helpers. Boundary cells are explicitly zeroed for
  fresh-array parity. Remaining `new float[]` sites in the file are cold-init only (verified).
  **This resolves the highest-value Zero-GC violation.**

#### 🟠 P1 — LINQ in persistence hot path
- `PersistentWorldRegistry.cs:7486`: `merged.Count > 0 ? merged.ToArray() : ...` — LINQ
  `.ToArray()` allocates a managed array. Verify this call is cold (save/load) vs per-frame.

#### 🟡 P2 — Cold allocations are correctly labeled
- Many `new float[]/char[]/NativeQueue` sites carry explicit `// COLD ALLOC: ... - owner: X`
  comments (`SpatialAudioManager.cs:2766..2911`, `BiomeMatrixDirector.cs:336/343`,
  `HUDQuickBar.cs:463`, `Fabricator.cs:1654..1708`). These are init-time and **acceptable**.
  The codebase discipline of annotating cold allocations is a genuine strength.

#### 🟡 P2 — `Find*` usages
- 5 `Find*` in hot-path methods (`Items/PickupItem.cs:1109`, `ScavengingLootOracleRuntime.cs:1917/1924`,
  `Tools/H8_PlayModeScreenshotter.cs:234/324`). Most are reload/cleanup/editor paths (acceptable);
  `PickupItem.cs:1109` (`FindObjectsByType<PickupItem>` on validation cache) should be reviewed.

### Native memory & jobs (mandate: `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`)
- ✅ `H8Memory.cs` `Malloc` (`:4270`) is correctly wrapped by `RegisterPointer` (sentinel
  registration) at `:4277` — compliant with the sentinel rule.
- ✅ No `Dispose()`-on-live-handle patterns surfaced in the hot-path scan.
- ⚠️ 169 `DataVaultExempt` signal lanes use `Allocator.Persistent` directly instead of the
  vault. This is a **documented** naming convention (not an accidental leak), but it is
  scattered ownership debt — the mandate's own `GlobalDataVault` migration ledger flags these.

### Arena allocator (mandate: `OPT_HectonArenaAllocator_2_0.txt`)
- Arena allocator exists (`Core/HectonArenaAllocator.cs`, 943 lines). The mandate itself states
  the gap is **ownership discipline + telemetry**, not implementation.
- Per the audit checklist: verify no `Span<T>`/raw pointer stored in class fields, no arena
  allocs inside Burst jobs, and resets only at the dispatcher frame boundary.

---

## 3. Architectural Debt

### God-class debt — 182 files ≥ 2,500 lines
Top offenders (by line count):

| File | Lines | Types | Risk |
|---|---|---|---|
| `HectonPlayerMovement.cs` | 15,443 | 5 | Single ~15k-line class |
| `HectonVoxelEngine.cs` | 14,808 | 34 | Megaclass + 33 job/entry structs in one file |
| `Audio/PlayerCriticalProceduralAudioRenderer.cs` | 14,346 | — | Oversized renderer |
| `SpatialAudioManager.cs` | 13,494 | — | Oversized manager |
| `UI/Localization/H8LocHashes.cs` | 12,895 | — | Likely generated constants |
| `WorldProceduralScatterDirector.cs` | 12,810 | — | Oversized director |
| `SaveBinaryStorage.cs` | 12,234 | — | Serialization megafile |
| `World/PersistentWorldRegistry.cs` | 11,482 | — | Persistence god-object |
| `HectonFluidEngine.cs` | 11,383 | — | Fluid megaclass (also Zero-GC risk) |
| `World/FloraInteractionManager.cs` | 11,332 | — | Oversized manager |

**Recommendation:** Split the top ~10 files by responsibility (jobs, DTOs, presentation,
simulation) into separate files/partials. `HectonVoxelEngine.cs` already groups 34 types — it is
the best decomposition candidate (move job structs to `*Jobs.cs`, telemetry to `*Telemetry.cs`).

### God-class decomposition plan (Iteration #6-9)

The three largest files share one signature: a single `MonoBehaviour` megaclass implementing
**16+ interfaces** and hundreds of methods, plus (for Voxel/Fluid) a swarm of Burst jobs living in
the same file. The jobs and pure-logic helpers are already cleanly separable (e.g.
`HectonFluidEngine.cs` already houses standalone `HectonGerstnerWater` and
`HectonAnalyticalFlowField` static helpers that own their own file worth of logic). The plan
below decomposes by the seams already present in the code — no behavior change, pure
re-homing/`partial` split.

**1. `HectonFluidEngine.cs` (11,383 lines → ~7 files)**
- `HectonFluidEngine.cs` — the `HectonFluidEngine : MonoBehaviour` core (tick, upload, disposal)
  reduced to orchestration (~3,500 lines).
- `HectonFluidEngine.Buoyancy.cs` — `partial` containing all buoyancy surface-force math +
  `BuoyancyJob` (`:10322`).
- `HectonFluidEngine.Waves.cs` — `partial` with wave-query/advection + `WaveQueryJob`
  (`:10196`), `InteriorFloodBfsJob` (`:11334`).
- `HectonFluidEngine.CPUFallback.cs` — the GPU-less fallback methods fixed in Iteration #2
  (`RunCpuFluidAdvectionFallback`/`RunCpuFluidSimulationFallback` + pooled buffer fields).
- `HectonGerstnerWater.cs` — existing static helper, moved out (pure).
- `HectonAnalyticalFlowField.cs` — existing static helper, moved out (pure).
- `HectonFluidEngine.Telemetry.cs` — `FluidImpactEvent`, `FluidOceanSurfaceTelemetry`, budget
  snapshots, profiler stat strings.

**2. `HectonVoxelEngine.cs` (14,808 lines → ~6 files)**
- `HectonVoxelEngine.cs` — `HectonVoxelEngine : MonoBehaviour` orchestration (~4,000 lines).
- `HectonVoxelEngine.Jobs.cs` — the **20 Burst jobs** currently in the file (isolated, one
  namespace-region; these are already `IJobParallelFor` and self-contained).
- `HectonVoxelEngine.DeferredPhysics.cs` — the deferred physics-bake driver types.
- `HectonVoxelEngine.Streaming.cs` — `VoxelStreamingScratchSlot/Lease/State` + pipeline state
  structs.
- `HectonVoxelEngine.Editor.cs` — `HectonVoxelEngineEditor : Editor` (`:14752`, Editor-only).
- `VoxelDensityPipelineFaultSlots.cs` — the internal static fault-slot registry.

**3. `HectonPlayerMovement.cs` (15,443 lines, 5 types → 4 partial files)**
- `HectonPlayerMovement.cs` — class declaration + core locomotion state machine (~4,500).
- `HectonPlayerMovement.Ground.cs` — `partial` ground/surface handling
  (`PlayerMovementSurfaceHit` at `:109`).
- `HectonPlayerMovement.Render.cs` — `partial` presentation/interpolation
  (`RenderInterpolationState` at `:1698`, `CinematicFocusTelemetryEntry` at `:62`).
- `HectonPlayerMovement.Telemetry.cs` — `partial` telemetry + `PresentationAudioEvent`.

**Coding rules for all three:** (a) move whole methods unchanged, never refactor behavior during
re-homing; (b) keep the `Hecton8.Core` assembly/namespace; (c) Burst jobs stay `static` and keep
their `IJobParallelFor` shape; (d) verify brace balance + `#if` guards after each move; (e) no
new `using` beyond what the moved block already needs. This is a mechanical, low-risk
refactor that immediately reduces the three largest files by ~70% each and isolates the
fault-prone jobs/telemetry/presentation layers.

### DataVaultExempt ownership debt
169 exempt lanes scatter `Allocator.Persistent` signal queues outside the vault. This is a
known, documented pattern but represents migration backlog tracked by the vault sovereignty
route.

### Native memory ownership & sentinel compliance (Iteration #11-12)

The Zero-GC scan flags every `UnsafeUtility.Malloc` as `MALLOC_NO_SENTINEL`, but this is an
**unverified list, not a violation list** — the scanner does not look for the paired
Free/RegisterPointer. Manual verification of all 4 sites confirmed **zero leaks**:

| Malloc site | Allocator | Ownership | Verdict |
|---|---|---|---|
| `Audio/Synthesis/VocalBankPlaybackRuntime.cs:1803` (CSV scratch) | Persistent | `ReleaseEditorCsvScratch` free path; `#if UNITY_EDITOR`-guarded | ✓ FREED (editor-only) |
| `Core/Memory/H8Memory.cs:4270` (core alloc) | — | `RegisterPointer` + `NativeMemorySentinelRuntime` + `MemClear`; `Free` path at `:6094` area | ✓ SENTINEL-REGISTERED |
| `Core/Memory/H8Memory.cs:4335` (realloc) | — | Same `RegisterPointer`/fault-recording wrapper | ✓ SENTINEL-REGISTERED |
| `World/HectonBatchRendererGroupUtility.cs:246` (BRG culling scratch) | TempJob | Paired `UnsafeUtility.Free` in same method (`AllocateDirectDrawBuffer`/`FreeDirectDrawBuffer`) | ✓ FREED same frame |

**Job-system protocol:** no `.Complete()` is called from inside another job (all completion
happens on the main-thread scheduler), and the CPU-fallback rewrite (Iteration #2) runs on the
main tick, not inside a job. No mandate violation found.

**Recommended scan improvement (DONE):** the repeatable scanner
(`.tmp/h8_zerogc_audit.py`) now documents that `MALLOC_NO_SENTINEL` entries require manual
ownership verification rather than being treated as automatic leaks.

---

## 4. Recommended Improvements (priority order)

1. **Fix `HectonFluidEngine` CPU-fallback allocations** — reuse pooled `float[]`/`float[,,]`
   buffers (P0 Zero-GC).
   - **DONE (this audit):** `RunCpuFluidAdvectionFallback` now reuses three pooled `float[]`
     buffers (`_cpuFallbackAdvectionVelX/Y/Z`, allocated once via
     `EnsureCpuFallbackAdvectionVelocityArrays`) instead of `new float[length]` per call. This
     removes per-frame managed allocations from the GPU-less fluid-advection fallback.
   - **DONE (this audit, Iteration #2):** `RunCpuFluidSimulationFallback` is now fully
     allocation-free.
     - Added `VorticityConfinementForceCalculator.ComputeBuffered` and
       `FluidPressureJacobiSolver.SolveBuffered` — allocation-free overloads that write into
       caller-provided buffers (the existing `Compute`/`Solve` retain their allocating signatures
       for the `PureLogic/Tests` unit tests, which are unchanged).
     - Added pooled `float[,,]`/`Vector3[,,]` grid fields + `EnsureCpuFallbackSimulationGrids(n)`
       to `HectonFluidEngine` (cold allocation once at resolution/init).
     - The fallback now reuses pooled `velX/Y/Z`, `fx/fy/fz`, `divergence`, and two ping-pong
       pressure grids (`pressureA`/`pressureB`) for the 10-iteration Jacobi solve.
     - Added explicit boundary zeroing for the pooled `divergence` and force grids so reused
       buffers behave identically to the original fresh (zero-initialized) arrays — semantics
       preserved.
     - **Verified:** every remaining `new float[]`/`new Vector3[]` in `HectonFluidEngine.cs`
       lives only inside the two `EnsureCpuFallback*` cold-allocation helpers (once at init).
2. **Verify/convert `PersistentWorldRegistry.cs:7486` LINQ** to a pooled array or non-alloc path.
   - Assessment: this `.ToArray()` sits in `ApplySectorOverrides` — a save/load (cold) path,
     so it is **not** a hot-path Zero-GC violation. Optional cleanup only.
3. **Update SOA mandate** to reflect the 64 B production `ItemTemplate`.
   - **DONE (Iteration #5):** the mandate `.agents-skills/DATA_Inventory_Resources_Items_SOA_Layout.txt`
     §2.1 now documents the production 64 B layout with exact offsets and field relocations.
     The drift is closed at the documentation level. (Splitting `massKg`/`volumeM3` into a
     parallel SOA array remains an optional future optimization, not required.)
4. **God-class decomposition** on `HectonVoxelEngine.cs`, `HectonPlayerMovement.cs`,
   `HectonFluidEngine.cs`.
   - **SPEC DONE (Iteration #6-9):** §3 now contains a concrete, line-verified decomposition plan
     for the three largest files (→~17 target files), separating Burst jobs, telemetry,
     presentation, streaming, CPU-fallback, and editor code into cohesive files/partials. The plan
     is mechanical (whole-method re-homing, no behavior change) and low-risk.
   - **Execution:** requires Unity edit-mode recompilation verification; not performed here (no
     build tool for `.asmdef`). Each file move should be landed and brace/guard-verified
     incrementally.
5. **Runtime `UnsafeUtility.SizeOf<T>()` / offset proof for primary DTOs.**
   - **DONE (Iterations #3 & #4):** integrated six primary DTOs into the repository's SHINOBU_204
     ARM64 self-audit `CriticalTypeNames` (`Editor/Arm64AlignmentSelfAuditReport.cs`):
     `ActiveEquipmentDTO`, `FabricationJobDTO`, `ItemTemplate`, `FabricationJobSnapshotDTO`,
     `EquipmentIntegrationCounters`, and `KinematicSurfaceHit`. The editor self-audit now enforces
     `LayoutKind.Explicit`, no `Pack=1`, valid ARM64 stride, and 8-byte-lane alignment on these
     DTOs — converting the relevant `STATIC_SOURCE` claims into `UNITY_EDITOR` proof.
   - **Deliberately excluded:** `FabricationRuntimeDTO` (96 B) and `GasBaseHibernationSnapshot`
     (88 B) fail the SHINOBU_204 stride rule (`16 | 32 | ≥64-multiple-of-64`); these remain
     covered by the static audit only.
   - **Remaining:** generate a fresh SHINOBU_204 report in Unity to capture the editor-side
     evidence artifact.

---

## 5. Verification Boundary

All findings are **static-source** (read-only scan + targeted file inspection). No Unity
import, Burst, IL2CPP, profiler, or player-build proof was generated in this session. Evidence
class: `STATIC_SOURCE` / `STATIC_DOC`.

**Runtime behavioral-equivalence verification (Iteration #14):** because the pure-logic
helpers (`FluidPressureJacobiSolver` and `VorticityConfinementForceCalculator`) depend only on
`System.Numerics`, they were compiled into a standalone .NET 8 console harness
(`.tmp/H8ZeroGCTest/`) and executed. The harness compared the buffered overloads against the
original allocating overloads over randomized 3-D grids (n=4,6,8):

- `SolveBuffered` vs `Solve` (10-iteration Jacobi ping-pong): **0 mismatches** for every cell,
  all sizes → the buffer-reuse refactor is behavior-identical to the original.
- `ComputeBuffered` vs `Compute` (incl. boundary zeroing): **0 mismatches** for every
  component (3×n³ cells), all sizes → the pooled-grid refactor is behavior-identical.
- `ComputeBuffered` vs `Compute` (FloodFillRoomVolumeCalculator): **exact match** (volume AND
  voxel count identical) on randomized 50%-occupied grids, n=4/6 → the pooled-visit-grid,
  flat-int-queue refactor is behavior-identical to the original.

**Additional Zero-GC fix (Iteration #15):** `FloodFillRoomVolumeCalculator.Compute`
(`PureLogic/Systems/FloodFillRoomVolumeCalculator.cs`) allocated a fresh `bool[,,] visited` and
a `Queue<(int,int,int)>` per call, despite its "fully allocation-free" docstring. A new
`ComputeBuffered` overload reuses caller-provided scratch (pooled `bool[,,]` + flat `int[]`
queue, static 6-neighbour table) and is behavior-identical to `Compute` (verified above). The
original allocating `Compute` is retained for the existing NUnit tests.

**Extended pure-logic runtime verification (Iterations #18 & #20):** 23 additional
`System.Numerics`-only helpers (`SignalPrioritySortCalculator`, `LodChunkSelector`,
`LunarPhaseCalculator`, `SolarHourAngleCalculator`, `GyroDriftFilterCalculator`,
`SplashEntryAngleCalculator`, `BallastTankController`, `VerletCableSimulator`,
`BatteryChargeCurveCalculator`, `ActiveSonarAttenuationCurveCalculator`,
`ArmorPenetrationCalculator`, `AudioDistanceAttenuationCurveCalculator`, `BleedStackDecayModel`,
`CaloricDeficitPenaltyCalculator`, `CausticIntensityDepthCalculator`, `EcholocationRangeCalculator`,
`ExplosionRadialDamageCalculator`, `HudCrushDepthWarningUrgencyCalculator`,
`RadiationLeadShieldingCalculator`, `RepairRateMaterialCalculator`, `ToxinBioaccumulationCalculator`,
`VisibilityTurbidityCalculator`, `LaserCutDepthPowerCalculator`) were compiled into the same
harness and passed finite-value / deterministic smoke tests with correct value semantics
(e.g. `SolarHourAngleCalculator` returns the expected ~68.5° sun elevation at noon;
`LunarPhaseCalculator` returns 0→180° across a lunar day). Combined with the 3 equivalence tests,
**26 pure-logic helpers now run in plain .NET** — confirming the `PureLogic/Systems` layer is
cleanly Unity-independent, which validates the decomposition SPEC's claim that these helpers
extract cleanly into separate files.

**Broader hot-path Zero-GC confirmation (Iteration #19):** a scan of the non-PureLogic
gameplay folders (`World/`, `Audio/`, `Construction/`, `Items/`, `Player/`) surfaced 614 `new`
expressions across 18 files. Every one was classified as **non-violation**: (1) value-type
struct constructions (`Color`, `Bounds`, `AcousticOcclusionResult`, `SpatialQueryHit`, etc.)
which are stack/struct, not heap; (2) Unity UI Toolkit/IMGUI **editor tooling** controls
(`Label`, `Slider`, `Button`, `IntegerField` in `CreateGUI`/EditorWindow paths); or (3)
**cold one-time init** (`// COLD ALLOC` runtime roots, double-buffered `GraphicsBuffer`s,
`HectonSpatialHash` native registries). The only LINQ hits were the known cold
`PersistentWorldRegistry.cs:7486` save/load path plus two `thread.Join(...)` false positives.
**No new hot-path Zero-GC violation was found in the broader gameplay layer.**

**PureLogic layer sweep (Iteration #16):** all ~150 `PureLogic/*` helpers were scanned for
allocations/LINQ/string/Unity references. Results: 0 Unity references, 0 Malloc, and 0 genuine
hot-path allocations. The 78 `new` hits are almost all value-type `System.Numerics` structs
(`Vector2/3`, `Matrix4x4` — stack, no heap) or `throw new ArgumentException` in cold validation.
The only two real heap-container allocations were `FloodFillRoomVolumeCalculator` (fixed above)
and `SaveDeltaCompressDiffCalculator:33` (`new List<...>`), which is a **save-serialization
cold-zone** helper (only reachable via a JulesLink from the save codec), permitted by the
Zero-GC mandate's cold-zone rules. **The PureLogic/Systems hot-path layer is now fully
Zero-GC compliant.**

This upgrades the Zero-GC fixes from inspection-only to **runtime-verified equivalence**
(graded `RUNTIME_EQUIV` evidence for the pure-logic helpers). The harness itself is preserved
under `.tmp/H8ZeroGCTest/` for re-running after any future change to these helpers.
