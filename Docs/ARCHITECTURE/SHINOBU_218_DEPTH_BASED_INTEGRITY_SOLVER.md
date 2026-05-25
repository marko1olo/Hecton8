Date: 2026-05-20

Owner: SHINOBU_218

Domain: Habitat & Vehicles / Structural Integrity Math

Status: Ultra-polish static pass; Habitat/Deformation generation handle route patched; continuous health-pressure quality path patched; hull job determinism patched; Unity compile/profiler proof pending CPU gate.

Route card: `Docs/ARCHITECTURE/SHINOBU_218_DEPTH_BASED_INTEGRITY_ROUTE_CARD.md` (`YELLOW`, runtime proof pending).

## Runtime Authority

`StructuralIntegrityCalculatorRuntime` owns structural pressure evaluation through Vault buffers `70488-70497`.

The hot path schedules Burst jobs over `IntegrityStateDTO`, node `double3` AUPs, CSR offsets/destinations, edge flags, tuning, material strength entries, and telemetry.

`IntegrityStateDTO`, `StructuralTuningDTO`, and `StructuralIntegrityConstants` are defined in `Hecton8.Habitat.Deformation.Contracts` so render consumers can read the Vault ABI without referencing the structural Runtime assembly.

Historical note: SHINOBU_115 originally documented `70110-70119`. SHINOBU_218 moved the active structural buffers to `70488-70497` after static audit found Environment/Celestial raw constants still using `70110-70116`.

Vault handle note: runtime stores only `VaultGenerationHandle<T>` descriptors.

It resolves phase-local `NativeArray<T>` views through `IDataVault.TryResolveHandle`. It releases descriptors through `IDataVault.ReleaseBuffer` on shutdown or failed boot.

Legacy pointer-bearing `VaultBufferHandle<T>` storage is not active.

- Habitat deformation cleanup note: `HullIntegrityRuntime` was also migrated off persistent `VaultBufferHandle<T>` storage.
- It keeps only `VaultGenerationHandle<T>` descriptors.
- Covered lanes: hull dent/deformation, breach jet, material, CSV scratch, telemetry, pressure mirror.
- It resolves method-local views, validates required lengths at boot, releases descriptors on failed boot/shutdown.
- It registers scheduled/cold clear handles with `H8Memory.RegisterActiveJob`.

Hull health-pressure note: `HullIntegrityRuntime` consumes `SystemHealthIndexSignal.Pressure01` as continuous quality ceiling input.

Warning/critical states are fallback floors. `math.smoothstep`/`math.lerp` shape the ceiling before dent capacity hysteresis.

Breach jet camera lookup reads a boot/hot-swap cached `IPlayerRuntimeContext`; `RefreshBreachJetCameraCold()` no longer polls `GlobalRegistry.Player`.

Cold/debug note:

- Player builds do not implement/register/unregister structural or hull runtime on cold dispatcher lane.
- CSV tuning hot reload and parsing/file polling are editor-only.
- Black-box dump file I/O remains available for fault capture.

Hull determinism note: every Burst job in `HullIntegrityTypes.cs` uses deterministic float mode because the lane mutates SIP, breach, deformation, pressure, indirect breach-jet, and telemetry-adjacent state.

Hull layout note: `HullIntegrityRuntime.ValidateLayouts()` keeps `UnsafeUtility.SizeOf<T>()` DTO size checks in every build. Reflection-backed field offset checks compile only under `UNITY_EDITOR`.

Structural CSV note:

- `StructuralIntegrityCalculatorRuntime` keeps deterministic default material strengths in player boot.
- CSV existence checks, last-write polling, file-open reads, span parser helpers, and cold CSV material apply are `UNITY_EDITOR` only.
- Player fault dumps remain enabled.

- Baked damage contract: SHINOBU_210 owns `HabitatDamageMeshStateResolver`.
- SHINOBU_210 keeps staged baked mesh hashes reachable.
- SHINOBU_218 structural runtime does not consume pressure-to-mesh resolver.
- Pre-collapse deformation here remains continuous `BucklingScalar` shader data.

Legacy `BaseModule.CurrentIntegrity`, `ModuleIntegrityComponent`, and `HabitatGraphManager` scalar surfaces remain compatibility APIs for save, repair, HUD, and other existing consumers. They are not the SHINOBU_218 authoritative structural solver.

- Runtime assembly route is intentionally narrow.
- `Hecton8.Habitat.Deformation.asmdef` references Core, Core.Contracts, Core.Memory, Bootstrap.Contracts, own Contracts, and Unity packages only.
- It does not directly reference Construction, Fluid, Vehicle, UI, or sibling runtime domains.

## Data Layout

`IntegrityStateDTO` is 32 bytes:

- `NodeHash` offset 0

- `BaseStrength` offset 4

- `CurrentStress` offset 8

- `AppliedPressure` offset 12

- `Flags` offset 16

- `BucklingScalar` offset 20

- explicit pad bytes offset 24-31

`StructuralIntegrityLayout.Validate()` checks DTO sizes and offsets in editor builds.

## Solver Loop

1. `StructuralDepthPressureJob` subtracts sea-level `double3` AUP from node `double3` AUP before casting depth to float.

2. `StructuralSdfAnchorJob` samples `VoxelSdfTexture3D` when present, otherwise uses deterministic mock anchors.

3. `StructuralGraphStressJob` computes stress from pressure, support damping, CSR neighbor support, and collapsed neighbor load.

4. `StructuralCollapseSignalJob` emits unmanaged `BaseIntegrityEventPayload`, `FluidIncursionSignal`, and `BaseModuleCompromisedSignal`.

5. `StructuralEdgeSeverJob` marks CSR edges severed when source or destination nodes collapse.

6. `StructuralTelemetryJob` writes the 300-frame ring and fault flags.

Final scheduled handle is stored locally for `LateFrameTick` visual sync and registered with `H8Memory.RegisterActiveJob(SystemID.HullIntegrity, handle)`.

Cold boot/mock/CSV jobs also register handles before intentional cold completion points. Player runtime does not implement/register `ColdTick`; CSV polling is editor-only after boot.

## GPU Upload Gate

- `StructuralTelemetryJob.StateHash` includes node hash, stress, pressure, buckling scalar, and flags.
- Visual sync uses two structured `GraphicsBuffer` instances as A/B upload buffers, caches the last uploaded state hash and active node count, and writes only the non-read buffer.
- When hash and count are unchanged, the runtime refreshes shader params but skips `GraphicsBuffer.LockBufferForWrite` and the structural state `MemCpy`.

Worst-case unchanged upload avoided at current capacity: `4096 nodes * 32 bytes = 131,072 bytes` per skipped pass.

The telemetry fold sanitizes stress, pressure, and buckling before max counters and `StateHash`; non-finite source values set `TelemetryFlagNonFinite` instead of entering the forensic row as NaN payloads.

If active node count resolves to zero, visual sync publishes shader count `0` and skips buffer copy rather than uploading one stale/default DTO.

## Quality Scaling

Authoritative cadence uses continuous `GlobalQualityWeight`:

- Low: frames-between-updates approaches 30, cheap anchor fallback remains valid.

- Middle: CSR pressure propagation runs regularly.

- High: buckling scalar and SDF cross taps add visual response through a `smoothstep(0.25,0.75,quality)` blend, not a binary quality switch.

- Ultra: per-frame evaluation and higher buckling visual intensity feed shader displacement.

Hull dent and breach visual capacity use the same continuous rule.

Global quality and homeostasis pressure combine into a smooth ceiling before dent cap and shader dent limit. Survival pressure converges toward minimum tracked rows without pop.

No Unity joints are part of this authority path.

Wall deformation is the Dear Lie: `BucklingScalar` and structural state upload as shader-visible data for vertex/material displacement.

CPU collision meshes, PhysX joints, and mesh swaps stay out.
