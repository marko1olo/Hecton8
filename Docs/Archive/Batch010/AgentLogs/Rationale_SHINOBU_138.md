# Rationale_SHINOBU_138

Status: IMPLEMENTED_STATIC_VERIFIED_BUILD_GATED

## Initial Scope

Problem: Predator scent tracking request targets replacement of object-trigger scent logic with a mathematical 3D chemical field.
Solution: Use DataVault-owned flat buffers, explicit 16-byte `ChemicalCellDTO`, AUP-local mapping, Burst jobs, front/back Jacobi buffers, and telemetry ring.
Rejected Alternatives: Unity trigger volumes, scent particle GameObjects, `Vector3.Distance` scans, local unmanaged persistent allocations outside Vault.
Scalability potential: Low uses lower grid resolution and 1 solver iteration; Middle increases resolution/iteration cadence; High uses stronger advection/occlusion fidelity; Ultra spends saved cycles on richer sensory debug and visual fog response without changing gameplay truth.
Hardware Impact: On i3/MX350, replacing PhysX broadphase scent checks and O(M*N) distance scans with O(1) sampling and O(N) flat-array solver targets stable cache-linear work; measured proof absent.

## Mandate Selection

Problem: Chemical field crosses AI, AUP, Vault, jobs, telemetry, and editor debug surfaces.
Solution: Read eight targeted mandates before coding: GlobalRegistry DI, Signal Lane Segregation, AUP Determinism, Floating Origin Precision, Zero GC, Native Memory Jobs, ARM64 Struct Layout, Crash Telemetry.
Rejected Alternatives: Reading unrelated rendering/UI mandates first or starting from invented architecture.
Scalability potential: Mandates force continuous quality weight, bounded snapshots, and hot-path allocation rejection across weak/mid/high/ultra devices.
Hardware Impact: Reduces risk of MX350 frame spikes from managed allocations, sync job completion, misaligned DTO reads, or trigger broadphase churn.

## Archaeology Decision

Problem: `ChemicalInfluenceGrid` already owns public scent APIs consumed by `PredatorCognitionDomain`, `MesofaunaBehavioralStateMachine`, `FloraInteractionManager`, `ScannerTool`, corpse resource code, and defoliant dead-zone routing. The existing implementation keeps local `NativeArray` buffers, a 2D byte scent grid, and breadcrumb distance scans.
Solution: Keep the current authority class and replace its backing storage with local Vault buffer IDs, explicit DTO layouts, and Burst jobs. Preserve public/internal API shape so sibling domains do not need direct new dependencies.
Rejected Alternatives: Creating a second scent service would violate one-fact-one-owner; routing predator AI through direct sibling references would damage compile-wall isolation; deleting breadcrumbs outright would break existing AI callers before a migration window exists.
Scalability potential: Low/middle devices sample a bounded 3D field and keep breadcrumb compatibility as a narrow fallback; high/ultra devices can consume the published `float4` volume and telemetry for richer debug and shader response.
Hardware Impact: Avoids PhysX trigger broadphase and per-predator per-breadcrumb hot loops as the authoritative path; retained breadcrumbs are capped compatibility data, not the solver truth.

## Loop 2 Tasks 01-05 Decision

Problem: The old scent lane used local persistent arrays and a 2D byte grid; XML requires a 3D AUP-local chemical field with no physics scent triggers.
Solution: Replaced the local `NativeArray` ownership with Vault handles `71150..71168`, made `ChemicalCellDTO` explicit 16 bytes, added editor-time layout validation, and added deterministic mock source generation in Burst.
Rejected Alternatives: Keeping the 2D byte scent grid would preserve O(1) blood lookup but fail vertical water-column diffusion; keeping object trigger scent would reintroduce PhysX broadphase cost and nondeterministic contact ordering.
Scalability potential: Low uses 5Hz cadence, nearest grid sampling, one mock-biased source band, and 1 Jacobi iteration; middle increases cadence and trilinear sampling; high/ultra execute richer advection and more solver passes while preserving the same data route.
Hardware Impact: On i3/MX350, the dominant win is removing collider/message dispatch and replacing scattered object reads with flat cache-linear cells; no profiler proof yet because guarded build was skipped while CPU telemetry reported 100%.

## Loop 3 Tasks 06-10 Decision

Problem: Active emitters can overlap the same grid cells, and the sliding 3D window must preserve concentration history while moving around the player AUP.
Solution: `ChemicalInjectionJob` uses atomic float CAS for concentration adds, `ChemicalDiffusionSolverJob` uses front/back Jacobi buffers, `ShiftChemicalGridJob` uses slab `UnsafeUtility.MemMove`, and predator cognition receives the published grid snapshot for O(1) scent sampling before fallback breadcrumbs.
Rejected Alternatives: Serial main-thread injection would block frame scheduling; in-place Gauss-Seidel would make worker order affect scent truth; clearing the grid on recenter would produce predator scent pops; editing AI to own scent math would split fact ownership.
Scalability potential: Low cadence/1 iteration still injects sources and samples a coherent grid; middle adds smoother spread; high/ultra increase iterations and advection fidelity without changing the AI contract.
Hardware Impact: On i3/MX350, replacing dense predator source loops with grid lookup should cap AI scent cost regardless of emitter count. Static only; profiler proof pending.

## Loop 4 Tasks 11-15 Decision

Problem: Solver cost must shed smoothly under thermal pressure while staying deterministic and AUP-correct for rollback.
Solution: `ResolveJacobiIterations` applies `(int)math.lerp(1f, 6f, Smooth01(GlobalQualityWeight))`, frame cadence lerps from 12-frame low-tier updates to per-frame high-tier updates, jobs run with `FloatMode.Deterministic`, and every AUP route subtracts the grid root before the float cast. The SDF bridge treats solid SDF samples as occlusion and zeros chemical diffusion through them. Vault buffers request `UninitializedMemory` and are cleared once by `ColdZeroVaultBuffersJob`.
Rejected Alternatives: Binary low/high switches would violate the scalability pillar; casting absolute AUP to `float3` would fail at 100km scale; collider/raycast blockers would reintroduce physics dependence; relying on allocator zero-fill would hide cold-boot cost.
Scalability potential: Low uses 5Hz-equivalent cadence, nearest sampling, one Jacobi pass, and no high-tap drift; middle raises cadence and enables trilinear smoothing; high uses more solver passes and drift; ultra spends the same authority route on visual overlays and shader-facing published fields.
Hardware Impact: On i3/MX350, 1 pass at sparse cadence is the survival mode. On RTX-class hardware, up to 6 passes plus advection preserves richer trails without changing predator sampling. Guarded build was not launched because CPU telemetry reported 100%.

## Loop 5 Tasks 16-20 Decision

Problem: Designers need a human tuning surface and QA needs postmortem data; the final report must live on disk, not in chat.
Solution: Added Vault-backed 300-frame telemetry, NaN dump path, `AbyssalScentTunerWindow`, `chemical_emitter_profiles.csv`, live gizmo slices, route card, and `LOG_SHINOBU_138.md` with `<SELF_AUDIT>`. CSV ingestion uses a Vault-backed fixed open-addressed DTO table instead of `NativeHashMap` to obey persistent-memory sovereignty.
Rejected Alternatives: Managed editor/runtime logs in hot path; `string.Split` CSV parsing; runtime-owned `NativeHashMap`; particle debug visualization; chat-only audit.
Scalability potential: Low devices get the same deterministic scent truth with sparse cadence and cheap sampling; middle devices gain smoother sampling; high/ultra can feed published scalar volumes into shader/UI overlays without increasing AI coupling.
Hardware Impact: On i3/MX350, telemetry is a 64B/frame ring write and editor visualization is inactive in player builds. Static verification only; build stayed gated by 100% CPU telemetry.

## Loop 6 Polish Decision

Problem: Static review exposed non-authoritative convenience paths: runtime scheduling still had Unity frame/time reads, focus fallback used `Camera.main`, layout offset validation used runtime reflection, editor telemetry rebuilt strings, and chemical runtime named Gameplay concrete symbols directly.
Solution: Replaced frame/time fallback with `HectonArenaAllocator.CurrentFrameSequence` and deterministic simulation seconds, removed `Camera.main` from focus AUP resolution, confined field-offset reflection to `#if UNITY_EDITOR`, moved tuner telemetry to numeric fields updated only on new telemetry frames, and routed bleeding/submarine data through `GlobalRegistry` contract access without source-level `Hecton8.Gameplay` symbols or `TryGetComponent` fallback.
Rejected Alternatives: `Time.frameCount`, `Time.time`, `Camera.main`, runtime `Marshal.OffsetOf`, per-update label string concatenation, and cached MonoBehaviour survival lookup. Each option is convenient Unity glue, not a deterministic chemical authority route.
Scalability potential: Low devices avoid hidden camera/tag lookups and editor-only string churn; middle/high/ultra keep the same deterministic grid truth while spending extra quality only inside solver iterations, sampling, and visual overlays.
Hardware Impact: On i3/MX350, this removes small but sharp hidden CPU/GC hazards around fallback paths and preserves compile-wall routing. No build was run because CPU gate remains violated.
