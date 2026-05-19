# Rationale_SHINOBU_118

Agent: SHINOBU_118
Role: DECOMPRESSION_SICKNESS_CALCULATOR
Domain: Echelon 5 Combat & Survival Physiology
Status: PENDING VERIFICATION

## Non-Trivial Decisions

### Decision 00 - Architecture Entry

Problem: Decompression sickness is a cross-domain physiology truth feeding UI, audio, survival, telemetry, and future rollback. A local MonoBehaviour or direct damage call would create hidden dependencies and non-deterministic behavior.

Solution: Owner-local physiology math first; expose unmanaged DTOs and typed scalar outputs only where existing architecture supports them. Use AUP-relative pressure math and a fixed 300-entry telemetry ring. New/changed global route surfaces require route-card data before acceptance.

Rejected Alternatives: Standard Unity Update/Coroutine health script was rejected because it allocates easily, depends on Transform floats, and cannot be snapshotted with blind MemCpy. Direct damage calls were rejected because the task names this system as a provider, not a damage owner.

Scalability potential: Low = 4 active compartments with same pressure truth and cheap presentation scalar. Middle = 8 compartments. High = 12 compartments. Ultra = 16 compartments plus richer VISUAL_SYNC/audio consumers without mutating gameplay truth.

Hardware Impact: On i3/MX350, 4-16 sequential 16-byte compartment reads are cache-resident and expected below 1 microsecond in Burst; runtime profiler proof is still absent.

### Route Card - Physiology State Broadcast

Route ID: SHINOBU_118_PHYSIOLOGY_STATE_SIGNAL
Date: 2026-05-19
Owner: SHINOBU_118 / Survival Physiology owner pending source integration
Owner domain: Echelon 5 Combat & Survival Physiology
Owning file/system: Pending source archaeology

Problem: DCS/narcosis scalars must be consumed by survival damage, visor, audio, and input without concrete cross-domain references.
Why owner-local data is insufficient: Multiple presentation and survival consumers require the same post-simulation scalar.
Why direct caller/owner interface is insufficient: Fan-out crosses POST_SIMULATION to VISUAL_SYNC/audio/input consumers.

Instrument:
  [ ] GlobalRegistry cold service/interface
  [x] SignalBus<T> first-party broadcast
  [ ] GlobalSignals bridge/direct queue
  [ ] HectonEventBus mod/API/cold event
  [x] GlobalDataVault / IDataVault
  [x] Black-box/telemetry route

Producer phase: POST_SIMULATION
Consumer phase: VISUAL_SYNC / survival damage consumption phase
Cadence: once per physiology tick or dirty state, max 1 player/frame
Expected max events/reads per frame: 1 player signal, 300-entry telemetry ring
GlobalQualityWeight behavior: active compartment count scales continuously from 4 to 16; presentation consumers may scale visual/audio richness, not physiology truth ownership.

Payload/data shape: unmanaged scalar signal, no managed refs, finite-clamped floats, entity id/frame id/flags.
Managed fields present: no
UnityEngine.Object fields present: no
Layout proof: pending source implementation and self-audit.
Capacity: pending existing SignalBus capacity pattern.
Overflow/failure mode: coalesce by player id, keep highest supersaturation and latest frame; telemetry increments drop/coalesce counters where supported.

Telemetry fields: frame, depth, ambient pressure, highest tissue tension, supersaturation, narcosis, flags, execution time estimate.
Black-box fields: 300-frame physiology telemetry ring; dump path Docs/AgentLogs/Dump_PHYSIOLOGY_SURGEON.bin per assignment.
Profiler marker: pending source implementation.
GC proof required: static source scan + compile; runtime GC proof absent until Unity/profiler run.

Shutdown/disposal rule: owner unregisters/releases native buffers through existing Vault/H8Memory owner if present.
Scene unload behavior: release/clear physiology buffers with generation invalidation where existing Vault supports it.
Stale-handle behavior: stale generation disables publish and writes telemetry fault hash.

Rejected alternatives:
  [x] owner-local field
  [x] cached owner interface
  [ ] existing SignalBus lane
  [ ] existing Vault buffer
  [x] cold HectonEventBus hook
  [ ] no global route needed

Why this does not increase global monolith risk: One narrow unmanaged physiology scalar lane, no registry polling, no managed event bus, no direct UI/audio/survival concrete references.
H-Phi impact expected: Possible small signal/DataVault surface increase; justified only if existing route cannot carry payload.
Runtime proof required before acceptance: compile, Unity Console, Play Mode physiology tick, Profiler/GC 0 B hot path, finite telemetry dump test.
Reviewer: Pending
Status: PROPOSED

### Decision 01 - Tasks 01-05 First Loop

Problem: Legacy survival code applied immediate decompression damage from ascent speed/depth checks, while the existing physiology path stored only aggregate decompression state and had no 16-byte tissue DTO or deterministic profile injection.

Solution: Replaced direct DCS damage in `HectonSurvivalSystem` with `PhysiologyStateSignal` emission, pressure-scaled oxygen drain by ambient ATM, added `TissueCompartmentDTO` at exact 16-byte explicit layout, registered `ShinobuTissueCompartments`/`ShinobuMockDiveProfile` Vault buffers, and added `GenerateMockDiveProfile()` backed by a Burst profile job.

Rejected Alternatives: Keeping `TakeDamage()` as a fallback was rejected because it creates two competing authorities. Zero-filled tissue buffers were rejected because task 15 requires uninitialized Vault memory plus explicit init. A hand-authored test curve in managed code was rejected because the profile must survive Burst profiling.

Scalability potential: Low = 4 evaluated compartments from the same 16-row state, mock profile still validates ascent sickness. Middle = 8 compartments. High = 12 compartments. Ultra = 16 compartments plus shader/audio richness from the same scalar.

Hardware Impact: i3/MX350 target is 16-byte sequential rows; active low-tier pass touches 4 rows/player and no managed allocations. Estimate for tasks 1-5 hot impact: O2 pressure multiply <0.05 us, tissue DTO state presence 0 us until kernel, mock profile cold-only.

### Decision 02 - Tasks 06-10 Haldanean Core

Problem: The existing decompression solver used aggregate fixed-buffer tensions and a binary low/high LOD branch. It did not emit an unmanaged DCS signal from the Burst lane and could not use habitat recompression pressure as the ambient treatment source.

Solution: Added deterministic `TissueSaturationJob` over `TissueCompartmentDTO` rows using raw pointer access and `UnsafeUtility.AsRef`, computes continuous supersaturation, emits `PhysiologyStateSignal` via `NativeQueue<PhysiologyStateSignal>.ParallelWriter`, uploads shader scalar through `HectonShaderGlobalDataVaultBridge`, and lets habitat room pressure override ocean depth when `PlayerBaseEnterSignal` plus `IGasDynamicsSolver.RoomPressure` are available.

Rejected Alternatives: MonoBehaviour-side Haldane integration was rejected because it would be non-authoritative and harder to snapshot. Particle/bubble blood VFX were rejected by Dear Lie protocol. A special "cured" branch was rejected; recompression is just higher ambient pressure feeding the same equation.

Scalability potential: Low = fastest three plus slowest tissue rows under `GlobalQualityWeight=0`, preserving ascent punishment. Middle/High/Ultra increase active rows continuously until all 16 are evaluated, while presentation scalars can grow visual/audio cost separately.

Hardware Impact: Pointer loop touches 4-16 contiguous 16-byte rows. Low tier read/write footprint is 64 bytes plus one slow row; estimated kernel <1 us/player on i3/MX350 until profiler proof is available. Shader upload is one VISUAL_SYNC vector write.

### Decision 03 - Tasks 11-15 Determinism And Scalability

Problem: The prompt rejects binary quality branches and requires AUP-derived pressure, rollback-safe tissue state, and uninitialized Vault allocation with explicit initialization.

Solution: `GlobalQualityWeight` now maps continuously to 4-16 active compartments; low weight evaluates fastest rows plus the slowest row. Depth comes from player AUP converted to `double3`, sea-level `double3` is subtracted before float depth use. Tissue buffers are Vault-owned, uninitialized on allocation, and initialized by `InitTissueCompartmentsJob` to 1 ATM. `TissueCompartmentDTO` stride constants document the rollback MemCpy footprint: 16 bytes/row, 256 bytes/entity.

Rejected Alternatives: `_MATH_LOD_LOW` binary branch was rejected. Transform/cached float depth was rejected. ClearMemory for tissue state was rejected because OS zero-fill is not an initialization policy and hides spawn invariants.

Scalability potential: Low = 256-byte authoritative state per player but only 64 active bytes plus slow sentinel touched per tick. Middle/High/Ultra spend extra rows for more accurate tissue curves without changing signal contracts.

Hardware Impact: AUP conversion is one double3 subtraction per player per tick. Tissue cold init is one scheduled job at allocation/spawn and no per-frame zeroing. Estimated hot savings versus 16-row always-on kernel on i3/MX350: ~50-75% row bandwidth at low quality.

### Decision 04 - Tasks 16-19 Instrumentation And Facades

Problem: DCS math is not acceptable without postmortem state, designer tuning, cold CSV ingestion, and a live ascent-ceiling visualizer.

Solution: Repointed physiology dump path to `Docs/AgentLogs/Dump_PHYSIOLOGY_SURGEON.bin`, records supersaturation and estimated execution microseconds in the 300-entry telemetry ring, dumps on invalid math or fatal bends, added UI Toolkit `DCS Physiology Tuner`, expanded the span-based `tissue_halftime_profiles.csv` parser to update coefficient/tissue rows by FNV-1a hashes, and added development-only `DcsAscentProfileOverlay`.

Rejected Alternatives: Debug.Log-only postmortem was rejected. IMGUI editor tuner was rejected for the requested UI Toolkit surface. LINQ/String.Split CSV parsing was rejected because designer hot reload must not allocate parsing collections. Shipping Canvas debug UI was rejected; the overlay is `UNITY_EDITOR || DEVELOPMENT_BUILD` only.

Scalability potential: Low = telemetry and scalar bars still expose the same authoritative state. Middle/High/Ultra can layer richer chart cadence and visor/audio consumers without changing the tissue math or Vault contracts.

Hardware Impact: Telemetry write is one 64-byte row per tick. CSV/file IO is cold and polling-gated. Editor/OnGUI surfaces are outside shipping hot path. Estimated hot-path added cost for telemetry: <0.05 us/player.

### Decision 05 - Task 20 Self-Audit

Problem: Completion cannot rely on chat output. The work needs disk-backed audit evidence, static scans, and an explicit compile limitation because CPU remained above the build threshold.

Solution: Appended `Docs/AgentLogs/LOG_SHINOBU_118.md` with wrong/done/cheats/microsecond estimates and a `<SELF_AUDIT>` block. Ran `git diff --check` for touched files and static `rg` scans for deterministic Burst, uninitialized tissue allocation, signal routing, shader route, and dump path.

Rejected Alternatives: Chat-only report and unverified optimism were rejected. Running `dotnet build` despite 100% CPU was rejected by project law.

Scalability potential: Self-audit documents low/middle/high/ultra tissue row behavior and exact Vault buffer IDs, so downstream integrators can validate without reading chat history.

Hardware Impact: No runtime impact. Build verification remains pending until CPU <=50%; no `dotnet`/`csc` process was active when checked.

### Decision 06 - Ultra-Think Hardening Pass

Problem: The first implementation still carried four structural liabilities: a private managed `byte[CsvMaxBytes]` scratch buffer for CSV/legacy coefficient ingestion, Burst jobs missing explicit `CompileSynchronously = true` and `[NoAlias]` proof, a development overlay performing object lookup inside `OnGUI`, and a quality model that reduced compartment count but not update cadence.

Solution: Added Vault buffer `BufferID.ShinobuTissueCsvScratch = 70237` and replaced all CSV/legacy binary reads with `Span<byte>` views over the resolved `NativeArray<byte>`. Added `CompileSynchronously = true` and `[NoAlias]` annotations to physiology jobs. Added `ShinobuPhysiologyConstants.MaxSimulationStepSeconds` and clamped runtime/job deltas to the same constant. Added a smoothed `GlobalQualityWeight` cadence curve: low quality accumulates to 0.2s physiology ticks, high quality runs effectively every frame. Moved the dev overlay runtime lookup to `OnEnable` and used a cached `GUIContent`. Replaced physiology runtime `Time.frameCount` payload writes with a local deterministic simulation frame counter.

Rejected Alternatives: Keeping the managed scratch array was rejected because the Vault law applies even to cold staging when a stable unmanaged scratch lane is cheap. Keeping per-frame `FindObjectOfType` in a development overlay was rejected because it normalizes bad Unity patterns. Hard low/high cadence switches were rejected because the project requires continuous `GlobalQualityWeight`, not binary tier flags. Using `Complete()` in gameplay jobs was rejected; the remaining `Complete()` calls are cold boot initialization and explicit mock profile generation only.

Scalability potential: Low = 4 active tissue rows, fastest rows plus slow sentinel, physiology cadence collapses toward 5 Hz while shader/audio consumers keep the last scalar. Middle = 8-10 active rows and intermediate cadence through smoothstep. High = 12-14 rows, near-frame cadence. Ultra = 16 rows, deterministic per-frame tissue truth plus richer visor/audio response fed by the same scalar, not extra CPU simulation.

Hardware Impact: On i3/MX350, low-tier cadence reduces physiology job submissions by roughly 66-91% depending on frame rate while preserving 0.25s max integration step. Removing the private scratch array saves one managed 8192-byte allocation per runtime instance and eliminates a future GC root. `[NoAlias]` exposes independent Vault lanes for Burst vectorization; exact profiler microseconds remain pending because build/profiler execution is still CPU-gated.

### Decision 07 - Timing, Endianness, And Adjacent Physiology Drift

Problem: The telemetry ring still wrote a constant `ExecutionMicroseconds = 0.82f`, which is a fake measurement. Legacy binary coefficient hydration assumed little-endian only. The UI Toolkit tuner still performed repeated object discovery when the runtime was missing. Adjacent physiology stress code used `Time.frameCount` and binary `ScalabilityTier` gating for hallucination visuals.

Solution: Captured `Stopwatch.GetTimestamp()` at job schedule and patched the latest telemetry row after job completion with schedule-to-completion microseconds, replacing the fake constant. Converted all SHINOBU physiology Burst jobs to `FloatMode.Deterministic` because the physiology domain is rollback-relevant. Added endian-aware legacy float table decoding with a manual allocation-free `ReverseUInt32` fallback. Moved DCS tuner runtime rebinding to create/focus/hierarchy events. Replaced adjacent stress signal frame values with a local slow-tick counter and converted hallucination visual spawning from a binary tier check to a continuous `HomeostasisBrain.GlobalQualityWeight` curve.

Rejected Alternatives: Keeping a constant microsecond value was rejected as false proof. Adding a managed binary reader or `BitConverter` path was rejected because cold paths still need deterministic allocation behavior. Keeping `GlobalRegistry.ScalabilityTier` in physiology was rejected because the project authority is the continuous `GlobalQualityWeight`. Running a build while CPU remained above threshold at 73-77% was rejected by the explicit build gate.

Scalability potential: Low = deterministic DCS kernel cadence still collapses toward 5 Hz; stress hallucination visuals become rare, dim, and closer to the player through a continuous quality weight. Middle = moderate hallucination cadence and tissue row count. High/Ultra = deterministic full-row physiology plus higher visual hallucination intensity without adding CPU-heavy simulations.

Hardware Impact: Removing the scheduled editor object search has no shipping runtime impact but removes an avoidable editor allocation/search pattern. Endian detection is cold-only and costs two float reads per legacy table. Stopwatch telemetry adds two timestamp reads and one telemetry row patch per scheduled physiology tick; this is explicitly measured as schedule-to-completion wall time, not claimed as pure Burst CPU time.
