# AI Fauna Docs

Date: 2026-05-18
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

Purpose: active fauna planning and coverage reference moved out of repo root.

## 2026-05-18 R11 Active Evidence Boundary

- Historical data boundary snapshot: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Historical actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json`.
- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Current documentation/source orientation: `Docs/Reports/2026-05-18_DOCUMENTATION_R15_NAVIGATION_SUPERSESSION_R16_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_ACTIVE_ENTRYPOINT_NAVIGATION_R15_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_BATCH008_BINARY_HYGIENE_R14_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_GENERIC_REPORT_BOUNDARIES_R13_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_ACTIVE_REMAINDER_R11_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_LONGTAIL_INTERIOR_R10_LOCAL.md`, and `Docs/Reports/2026-05-18_DOCUMENTATION_EVIDENCE_LANGUAGE_AND_COUNTERS_R9_LOCAL.md`.
- R11 capture-time static counters supersede older broad source-count text in this bundle where exact values differ: `1742` project C# files, `1689` script C# files, `1725` non-test C# files, `1138660` project source lines, `1119546` script source lines, `1134363` non-test source lines, `296` project-wide interface declaration hits, `294` script interface hits, `63` direct public interfaces in `GlobalRegistryContracts.cs`, and `107` first-party asmdefs. These counts are volatile under concurrent agents; rerun `rg` before treating exact values as current.
- Visual-realistic-fake doctrine snapshot: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`; re-check `.agents-skills` for newer mandates before implementation.
- May 14/R43 CLI compile notes are historical `CLI_COMPILE` evidence only. They do not certify Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, scene wiring, frame-time, memory, or visual quality under the current dirty workspace.
- May 13 DOC_AUDIT R15 fauna override: current static asset coverage is real but not runtime spawn proof. Recursive filesystem scan found `22` creature archetype assets under `Assets/_Project/Data/AI/CreatureArchetypes`, `22` fauna data templates under `Assets/_Project/Data/Fauna`, `108` fauna biome datasets, `13` fauna family profiles, and `6` generated proxy prefabs.
- R15 wiring boundary: `GameBootstrapper` calls `EcosystemRuntimeInstaller.EnsureRuntimeSystems()`, which creates `FaunaGeneticsManager`, `EcosystemHealthDirector`, and `MigrationDirector`; it does not create `FaunaDirector` or `WorldFaunaSpawnRegistry`. If no active `FaunaDirector` registers `IFaunaSim`, bootstrap registers `DemiurgeFaunaSimulationService.Shared`, a headless data-only fallback with `ResidentSlotCapacity = 0`.
- R15 scene/proof boundary: static script-GUID search found no serialized `FaunaDirector`, `WorldFaunaSpawnRegistry`, `FaunaRuntimeSmokeTester`, or `EcosystemRuntimeInstaller` hits in `Assets` scenes/prefabs/assets. Editor authoring code can configure a `WorldFaunaSpawnRegistry` and an existing `FaunaDirector`, but that is authoring capability, not production-scene proof.
- R15 smoke boundary: `.codex-artifacts/fauna-omega-smoke-2026-05-05.log` is not a usable PASS artifact. It reports `.codex-artifacts is not a valid directory name` and ends with Unity return code `1`; no `FAUNA_OMEGA_SMOKE_RESULT` PASS line was visible in the current file.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
## Historical May 4 Boundary

- Read current stable docs and the DOC_GLOBAL R16/R15/R14/R13/R11/R10/R9 reports before using May 4 / May 6 / May 11 reports as context. The older reports are historical unless current source or a fresh evidence artifact revalidates a claim.
- This bundle is fauna concept/planning/coverage reference, not proof that all species, prefabs, biome spawns, or runtime directors are wired in current scenes.
- `AI_CREATURE_ROSTER_ENTERPRISE.md` contains encoding-damaged prose. Use stable IDs and family links as pointers only until the prose is re-authored.
- Runtime fauna ownership remains in source/domain maps: `FaunaBrain`, `FaunaDirector`, `EcosystemDirector`, and fauna registry assets must be reopened before surgery.

## Files

- `AI_CREATURE_ROSTER_ENTERPRISE.md` - species/archetype reference set.
- `AI_FAUNA_WORLD_INTEGRATION_REPORT.md` - biome coverage and world-integration snapshot.

These remain active references, not archive material.

## 2026-05-12 DOC_VULCAN Technical Requirements

Status: SOURCE-SCANNED, RUNTIME PENDING VERIFICATION.

[SOURCE] Fauna cognition must align with `PredatorCognitionDomain.cs` and `EcosystemDirector.cs`. Source contains packed utility scoring, headless outputs, 1000 m sector constants, and Burst jobs for population and migration work.

[REQ] Fauna AI must use Utility AI scoring. Hunger, fear, threat, patrol, flee, and hunt choices must reduce to cheap polynomial or scalar utility comparisons. Per-creature MonoBehaviour decision trees are forbidden for swarm, far-field, or background ecology.

[REQ] The 1 km headless mode must keep distant fauna as packed positions, species IDs, hunger, sector, migration, and population state. The system must not animate, path, or tick full cognition for entities outside the high-interest radius.

[REQ] Headless output must preserve flags such as eco-headless state so render, audio, and POI systems can decide whether to spawn visible proxies. This is a contract boundary; do not invent direct dependencies on unowned scene objects.

### Navigation And Seams

[REQ] Navigation must use A* over voxel/nav-grid data, then run funnel smoothing on the corridor. The smoother must respect Voxel SDF clearance and terrain-page seam boundaries before emitting steering points.

[REQ] The path system must store sector or AUP-relative positions, not raw long-distance world floats. Seam fixes must happen at graph or corridor level; physics raycast smoothing is not a scalable pathfinding replacement.

[REQ] Low devices may run coarse headless migration and delayed path refresh. High and Ultra devices may buy denser local steering, richer avoidance, and extra visible scavenger behavior around POIs.

### Boids Compute

[SOURCE] `Assets/_Project/Scripts/BoidSimulation.compute` uses ping-pong buffers, spatial grid counts, spatial grid cells, and kernels for grid clear, grid build, and flock simulation.

[REQ] Flocking must use GPU spatial hashing. Each boid writes to a grid cell, reads local neighbor buckets, and emits the next state to the write buffer. CPU transforms may receive only compact render-facing output.

[REQ] Grid overflow must degrade visually, not break determinism. The kernel must cap bucket writes and preserve a validity mask so group barriers remain legal.

### Whale Falls And AUP POIs

[REQ] Whale falls must register as AUP-aware points of interest. Scavenger weights must derive from hunger, distance, species role, and decay age. The renderer should sell the feeding event with density, wobble, glow, and bone-decay material states.

[REQ] The POI system must avoid hard references to future systems. Use registry/event seams already present in the project.

[SOURCE] `EcosystemDirector.cs` sets whale-fall scavenger spawn pressure to 50x and acoustic lifetime to 7200 seconds. `MigrationDirector.cs` keeps blood-cloud POIs for 7200 game seconds, applies falloff-squared scavenger population pressure, and clamps whale-fall population multiplier to 50x. `SargassumMicroFaunaBoids.cs` renders whale-fall scavenger visuals through a 96-boid, 14 m ground-hugging ring when swarm LOD is Full.

[REQ] Minimum-budget whale falls must remain presentation fakes. If `SargassumMicroFaunaBoids` is not in Full LOD, the system must skip individual scavenger boid patching and rely on fear burst, acoustic POI, and `_DecayAmount` corpse crawl/bone reveal. Do not spawn crabs/eels as fallback GameObjects.

[REQ] Food-chain GPU buffer edits must use `GraphicsBuffer.LockBufferForWrite` for single-boid consumed/scavenger patches. `BoidKillSignal` must stay a bounded native queue with an 8-signal drain cap. The 300-entry food-chain telemetry ring must dump `Docs/AgentLogs/Dump_ECOSYSTEM_FOOD_CHAIN.bin` on non-finite/anomaly state.

### Kinematic Docking Contract

[SOURCE] `VehicleDockingModule.cs` contains fixed black-box telemetry capacity, docking trajectory cache, S-curve smoothing, and docked-relative AUP storage.

[REQ] Docking must move with a deterministic S-curve lerp into a parent-space anchor. After docking, the vehicle must transfer to AUP-relative parent coordinates so long-distance float drift does not corrupt pose.

[REQ] Joint stacks are forbidden for cinematic docking unless a profiler capture proves they are cheaper and more predictable than the kinematic path.

### Atmosphere Fake

[SOURCE] `BaseAtmosphereMath.cs` exposes `ResolveDaltonPressureFake`, `ResolvePlayerOxygenConsumption`, `ResolveSolveMode`, and byte-packed CO2 lanes. `BaseAtmosphereEngine.cs` forwards `playerStressMultiplier`.

[REQ] O2 and pressure stress must use the Dalton scalar fake from survival systems. Stress must scale oxygen consumption through `ResolvePlayerOxygenConsumption`. Pressure must resolve from O2, CO2, and nitrogen scalars through `ResolveDaltonPressureFake`. The game must not simulate gas particles or room chemistry.

[REQ] Low devices must solve active compartments at coarse cadence. High and Ultra may raise solve cadence and presentation intensity, but they must keep the same scalar fake.

### Troubleshooting

[FAIL] Predator jitter: inspect packed utility scores first, then clamp hysteresis between hunt/flee/patrol. Do not add more state machines.

[FAIL] Distant fauna costs frame time: verify 1 km sector headless mode, packed SoA migration, and render proxy thresholds.

[FAIL] Boids explode or vanish: check spatial grid cell size, bucket overflow rollback, ping-pong buffer parity, and validity masks before changing flock weights.

[FAIL] Whale fall spawns no visible scavengers: check swarm LOD first. Minimum-quality weights intentionally use shader/acoustic fakes; maximum-quality weights may patch up to 96 boids. Then verify AUP POI registration, source UID, MigrationDirector POI lifetime, and `LockBufferForWrite` patch path.

[FAIL] Path crosses a terrain seam: repair voxel/nav-grid seam metadata and rerun funnel smoothing. Do not patch it with per-agent physics raycasts.

[FAIL] Docked vehicle drifts: verify parent-space anchor, AUP-relative transfer, black-box telemetry, and smoothstep parameter bounds.
