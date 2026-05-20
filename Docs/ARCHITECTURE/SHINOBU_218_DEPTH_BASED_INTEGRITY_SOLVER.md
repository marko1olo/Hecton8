Date: 2026-05-20
Owner: SHINOBU_218
Domain: Habitat & Vehicles / Structural Integrity Math
Status: Ultra-polish static pass; Habitat/Deformation generation handle route patched; continuous health-pressure quality path patched; hull job determinism patched; Unity compile/profiler proof pending CPU gate.
Route card: `Docs/ARCHITECTURE/SHINOBU_218_DEPTH_BASED_INTEGRITY_ROUTE_CARD.md` (`YELLOW`, runtime proof pending).

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R46 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Current root/architecture boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md` as STATIC_DOC/STATIC_SOURCE/FILESYSTEM/PY_TOOL evidence. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. R42 remains the prior counter/route-boundary/proof-label correction. R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers; runtime proof remains absent.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-20 R46): `Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md` is the latest local static root/architecture interior-authority, route-field, and proof-language correction. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
## Runtime Authority

`StructuralIntegrityCalculatorRuntime` owns structural pressure evaluation through Vault buffers `70488-70497`.
The hot path schedules Burst jobs over `IntegrityStateDTO`, node `double3` AUPs, CSR offsets/destinations, edge flags, tuning, material strength entries, and telemetry.
`IntegrityStateDTO`, `StructuralTuningDTO`, and `StructuralIntegrityConstants` are defined in `Hecton8.Habitat.Deformation.Contracts` so render consumers can read the Vault ABI without referencing the structural Runtime assembly.

Historical note: SHINOBU_115 originally documented `70110-70119`. SHINOBU_218 moved the active structural buffers to `70488-70497` after static audit found Environment/Celestial raw constants still using `70110-70116`.

Vault handle note: the runtime stores only `VaultGenerationHandle<T>` descriptors for these buffers. It resolves phase-local `NativeArray<T>` views through `IDataVault.TryResolveHandle`, and releases descriptors through `IDataVault.ReleaseBuffer` on owner shutdown or failed boot. Legacy pointer-bearing `VaultBufferHandle<T>` storage is not part of the active SHINOBU_218 route.

Habitat deformation cleanup note: `HullIntegrityRuntime` was also migrated off persistent `VaultBufferHandle<T>` storage. It now keeps only `VaultGenerationHandle<T>` descriptors for hull dent/deformation, breach jet, material, CSV scratch, telemetry, and pressure mirror lanes, resolves method-local views, validates required lengths at boot, releases descriptors on failed boot/shutdown, and registers its scheduled/cold clear handles with `H8Memory.RegisterActiveJob`.

Hull health-pressure note: `HullIntegrityRuntime` consumes `SystemHealthIndexSignal.Pressure01` as a continuous quality ceiling input. Warning/critical states act only as fallback floors, and `math.smoothstep`/`math.lerp` shape the ceiling before dent capacity hysteresis. Breach jet camera lookup now reads a boot/hot-swap cached `IPlayerRuntimeContext`; `RefreshBreachJetCameraCold()` no longer polls `GlobalRegistry.Player`.

Cold/debug note: player builds do not implement, register, or unregister the structural or hull runtime on the cold dispatcher lane. CSV tuning hot reload and CSV parsing/file polling are editor-only; black-box dump file I/O remains available for fault capture.

Hull determinism note: every Burst job in `HullIntegrityTypes.cs` uses deterministic float mode because the lane mutates SIP, breach, deformation, pressure, indirect breach-jet, and telemetry-adjacent state.

Hull layout note: `HullIntegrityRuntime.ValidateLayouts()` keeps `UnsafeUtility.SizeOf<T>()` DTO size checks in every build. Reflection-backed field offset checks compile only under `UNITY_EDITOR`.

Structural CSV note: `StructuralIntegrityCalculatorRuntime` keeps deterministic default material strengths in player boot. CSV file existence checks, last-write polling, file-open reads, span parser helpers, and cold material apply from CSV are `UNITY_EDITOR` only. Player fault dumps remain enabled.

Baked damage contract note: SHINOBU_210 owns `HabitatDamageMeshStateResolver` and keeps staged baked mesh hashes reachable. SHINOBU_218 structural runtime does not consume the pressure-to-mesh resolver; pre-collapse deformation in this solver remains continuous `BucklingScalar` shader data.

Legacy `BaseModule.CurrentIntegrity`, `ModuleIntegrityComponent`, and `HabitatGraphManager` scalar surfaces remain compatibility APIs for save, repair, HUD, and other existing consumers. They are not the SHINOBU_218 authoritative structural solver.

The runtime assembly route is intentionally narrow: `Hecton8.Habitat.Deformation.asmdef` references Core, Core.Contracts, Core.Memory, Bootstrap.Contracts, its own Contracts, and Unity packages only. It does not directly reference Construction, Fluid, Vehicle, UI, or other sibling runtime domains.

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

The final scheduled handle is stored locally for `LateFrameTick` visual sync and registered with `H8Memory.RegisterActiveJob(SystemID.HullIntegrity, handle)` for owner-level memory/fence tracking. Cold boot/mock/CSV jobs also register their handles before their intentional cold completion points. Player runtime does not implement or register `ColdTick`; CSV polling is editor-only after boot.

## GPU Upload Gate

`StructuralTelemetryJob.StateHash` includes node hash, stress, pressure, buckling scalar, and flags. Visual sync uses two structured `GraphicsBuffer` instances as A/B upload buffers, caches the last uploaded state hash and active node count, and writes only the non-read buffer. When hash and count are unchanged, the runtime refreshes shader params but skips `GraphicsBuffer.LockBufferForWrite` and the structural state `MemCpy`.

Worst-case unchanged upload avoided at current capacity: `4096 nodes * 32 bytes = 131,072 bytes` per skipped pass.

The telemetry fold sanitizes stress, pressure, and buckling before max counters and `StateHash`; non-finite source values set `TelemetryFlagNonFinite` instead of entering the forensic row as raw NaN payloads.

If active node count resolves to zero, visual sync publishes shader count `0` and skips buffer copy rather than uploading one stale/default DTO.

## Quality Scaling

Authoritative cadence uses continuous `GlobalQualityWeight`:

- Low: frames-between-updates approaches 30, cheap anchor fallback remains valid.
- Middle: CSR pressure propagation runs regularly.
- High: buckling scalar and SDF cross taps add visual response through a `smoothstep(0.25,0.75,quality)` blend, not a binary quality switch.
- Ultra: per-frame evaluation and higher buckling visual intensity feed shader displacement.

Hull dent and breach visual capacity uses the same continuous rule. Global quality and homeostasis pressure combine into a smooth ceiling before dent cap and shader dent limit are resolved; survival pressure converges toward the minimum tracked rows without a warning/critical pop.

No Unity joints are part of this authority path. The wall deformation route is the Dear Lie: `BucklingScalar` and structural state are uploaded as shader-visible data for vertex/material displacement; CPU collision meshes, PhysX joints, and mesh swaps remain out of the authority path.


