# SHINOBU_341 Rationale

State: HARDENING PASS ACTIVE / SOLAR STATIC GATES CLEAN / CORE BUILD BLOCKED BY EXTERNAL PLAYERTOOLMANAGER / UNITY EDITOR IMPORT PENDING / BUFFERID COLLISION EVICTED / EDITOR FACADE CHURN REDUCED / GIZMO LABEL DIRTY-CACHED / READONLY_PTR_EXTENSION_HARDENED / LEDGER_COMPILE_PROOF_NORMALIZED

## Pre-Code Decision 00: Authority Read Order

Problem: Solar power generation touches Power, Habitat, Voxel SDF, Celestial, Weather, and global authority routes. Direct class coupling would create compile walls with parallel agents.

Solution: Treat the XML block, `AGENTS.md`, domain file, `.agents-skills`, and architecture docs as the only authority. Implement isolated/partial files and unmanaged DTO/job kernels over existing contracts when discovered.

Rejected Alternatives: A standalone `HectonSolarManager` or `MonoBehaviour.Update` panel loop would duplicate the power owner and reintroduce managed heap/object traversal.

Scalability potential: Low uses depth-only attenuated math with cadence throttling; Middle adds sun-angle and coarse SDF sampling; High adds deterministic turbidity shaping; Ultra spends saved CPU budget on telemetry/editor visualization without changing gameplay truth.

Hardware Impact: Expected low-end i3/MX350 gain comes from removing PhysX raycasts and managed per-panel iteration; numeric estimate remains PENDING until archaeology finds the actual legacy path and profiler proof exists.

## Decision 01: Solar Authority Route

Problem: Legacy `Gameplay/SolarPanel.cs` owned sunlight, time, sky obstruction, visual state, and grid output inside one managed component, including `Physics.RaycastNonAlloc` and `RenderSettings.sun` reads.

Solution: Keep `SolarPanel` as a cold facade only. The authoritative solve now writes `SolarPanelStateDTO` rows into GlobalDataVault and schedules `EvaluateOpticalDepthJob` plus CSR injection jobs in `PowerGridSolarContracts.cs`. The facade reads back output for presentation and no longer implements the legacy `IPowerComponent`/UnityEvent/PowerNode dirty route.

Rejected Alternatives: A new scene `HectonSolarManager` was rejected because it would create another hot owner and direct object traversal route. Direct dependency on StormPropagation runtime was rejected because the storm asmdef depends on Core; the facade uses the Core `IWeatherService` intensity instead.

Scalability potential: Low uses slow cadence, rational Beer approximation, and one SDF/analytic shadow step; Middle increases cadence and trilinear SDF shadow; High/Ultra increases samples and exact exponential blend while preserving the same DTO truth.

Hardware Impact: Static estimate for 100 panels removes 100 PhysX sky probes and replaces them with a single Burst chain. Expected low-end gain is 180-450 us versus per-panel ray probes, pending Unity profiler confirmation.

## Decision 02: DTO and AUP Math

Problem: Solar depth fails if runtime floats are used before subtracting sea-level origin, especially at far AUP coordinates.

Solution: `SolarPanelStateDTO` is explicit 32 bytes: `double3 PanelAUP` at 0, `float BaseEfficiencyScalar` at 24, `uint PowerNodeHashID` at 28. `EvaluateOpticalDepthJob` subtracts `panel.PanelAUP - conditions.SeaLevelAUP` in double, then casts only the vertical delta to float for Beer-Lambert depth.

Rejected Alternatives: Storing runtime `Vector3` position or C# properties in the DTO was rejected due to precision loss and CS1612 defensive copy risk.

Scalability potential: Low/Middle/High/Ultra all consume the same DTO layout; only cadence, Beer approximation blend, and SDF sample count scale.

Hardware Impact: Two panel DTO rows fit in one 64-byte cache line. Estimate: 512 panels read in 256 cache-line fetches before output writes.

## Decision 03: Shadow and Power Injection

Problem: Mountain shadow formerly used an upward physics probe and did not route through the CSR power graph.

Solution: The solver samples `VoxelSdfTexture3D` with the published `VoxelSdfPayloadDescriptor` and falls back to an analytic ridge function when no SDF payload exists. Generated panel watts are atomically accumulated into `NodeSolarInputMilliWatts`, then applied to matching `PowerNodeDTO` rows as source potential/storage for the CSR graph.

Rejected Alternatives: RaycastCommand, Collider.Raycast, and scene-layer obstruction masks were rejected because they keep PhysX in the solar truth path. A new SignalBus lane was rejected after the interconnect matrix showed `PowerGridTelemetryEvents` already covers power telemetry while continuous generation belongs in Vault/CSR buffers.

Scalability potential: Low uses a large step and cheap occlusion; Middle uses limited trilinear SDF; High/Ultra spends saved raycast cost on more SDF samples and lower shadow floor.

Hardware Impact: Atomic add cost is bounded by active panels and target node contention. Estimate: 100 panels -> 100 atomic adds plus one node application pass; expected under 0.1 ms on i3/MX350 if node capacity stays near 1024.

## Decision 04: Continuous Cadence and Determinism

Problem: Solar output is simulation truth, but solving every visual frame wastes ALU and creates rollback variability if the math changes per platform.

Solution: The facade gates scheduling through `math.lerp(0.05f, 0.5f, 1.0f - GlobalQualityWeight)`, carries accumulated dt into the CSR injection pass, and keeps Burst jobs on `FloatMode.Deterministic` with sanitized denominators and finite checks.

Rejected Alternatives: Per-frame `MonoBehaviour.Update`, wall-clock time of day, and platform fast-math were rejected because they trade deterministic base survival for invisible oversampling.

Scalability potential: Low runs a 0.5s cadence with cheap Beer approximation and coarse SDF; Middle tightens cadence and keeps angle/shadow; High/Ultra reaches 0.05s cadence and uses the saved PhysX budget for richer editor telemetry and presentation overlays without changing the DTO route.

Hardware Impact: Low-end i3/MX350 avoids 9 of 10 solves versus a 20Hz high-quality cadence when throttled. Estimate: 35 us saved per 500 panels per skipped slow tick, plus removed PhysX sync cost.

## Decision 05: Tuning, Gizmo, and Report Proof

Problem: Designers need live optical tuning and engineers need proof without letting editor tooling become a new runtime authority path.

Solution: `PhotovoltaicThermodynamicsTunerWindow` is editor-only UI Toolkit and mutates the existing Vault tuning row through `UnsafeUtility.AsRef`. `SolarPanel.OnDrawGizmosSelected` reads raw `SolarPanelStateDTO` and output DTO rows from Vault. `OOP_Solar_Scanner` uses Roslyn AST scanning and non-destructive JSON upsert into the shared optimization report.

Rejected Alternatives: ScriptableObject tuning assets, destructive report rewrites, transform-only gizmos, and string-only scanner passes were rejected. The scanner still has a CLI rg mirror because Unity menu execution is blocked while CPU/dotnet gates are active.

Scalability potential: Low/Middle runtime receives no editor cost. High/Ultra editor sessions get live power/depth/shadow graphs and scene labels for dense panel arrays.

Hardware Impact: Runtime impact is zero in player builds due `#if UNITY_EDITOR`. Editor graph stores 300 telemetry entries in a fixed array; no per-frame report rewrite.

## Decision 06: Black Box and CSV Data Route

Problem: NaN power or solver over-budget conditions must be forensically explainable, and panel hardware profiles must load without string heap churn.

Solution: The solver records `SolarTelemetryEntry` into a 300-frame Vault ring and writes `Docs/AgentLogs/Dump_SHINOBU_341.bin` when non-finite output or >0.2ms is detected. The profile loader consumes `ReadOnlySpan<byte>`, hashes names with FNV-1a, and parses floats manually into `SolarProfileDTO`.

Rejected Alternatives: `Debug.Log` autopsy, `float.Parse`, `string.Split`, and managed dictionaries were rejected because they allocate and hide the exact fault frame.

Scalability potential: Low keeps the same dump surface; Middle/High/Ultra add richer telemetry values without changing save identity or gameplay authority.

Hardware Impact: Fixed 300*64 byte telemetry ring is 19.2 KB. CSV parser cost is cold boot only; no runtime GC.

## Decision 07: Compile Gate

Problem: The batch forbids launching dotnet build while CPU is above 50% or any `dotnet`/`csc` process is active.

Solution: Static gates were run, but compile was not launched. Early gates were already blocked by CPU policy; the current objective gate sampled CPU at 57% with 1 active `dotnet` process. Build verification is still blocked by CPU and active-process rules, not by a known compiler error in SHINOBU_341 source.

Rejected Alternatives: Ignoring the CPU gate or stacking another dotnet build was rejected because it violates the batch protection rule and risks corrupting concurrent agent work.

Scalability potential: No runtime impact.

Hardware Impact: Saved developer machine contention; compile proof remains pending until CPU <=50% and no dotnet/csc process exists.

## Decision 08: Polish Hardening Against Hidden OOP and Cache Contention

Problem: The first pass still left compatibility residue: `SolarPanel` implemented the managed power component contract, black-box dumping used a `BinaryWriter` field loop, and node milliwatt atomics wrote dense adjacent `int` rows vulnerable to false sharing.

Solution: Remove the `IPowerComponent` bridge, UnityEvents, `PowerNode` lookup, and grid dirty calls from `SolarPanel`. Replace dense node input counters with explicit 64-byte `SolarNodeInputCounter64` rows. Route panel/tuning/profile writes through Vault write locks and `UnsafeUtility.AsRef`. Replace the dump writer with a fixed 32-byte `SolarBlackBoxDumpHeaderDTO` plus raw `ReadOnlySpan<byte>` telemetry payload.

Rejected Alternatives: Keeping `IPowerComponent` as a "temporary" adapter was rejected because it keeps managed event semantics alive in the solar truth surface. Dense `int` counters were rejected because adjacent hot nodes can fight over one cache line under `Interlocked.Add`. `BinaryWriter` was rejected because Task 15 explicitly requires a raw binary span dump.

Scalability potential: Low/Middle devices avoid both PhysX and managed power-event fan-out; High/Ultra devices can run denser panel farms with less atomic cache-line contention while retaining the same Vault DTO route.

Hardware Impact: On i3/MX350 class CPUs, expected gain is contention-dependent but removes worst-case MESI ping-pong when neighboring power nodes receive solar atomics. Fault dumps now write one header plus one contiguous telemetry payload instead of 300 field-loop rows.

## Decision 09: Data Monolith Readiness Finding

Problem: Global doctrine requires `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`, but the file is absent in the current workspace.

Solution: Mark Data Monolith status as `STATIC_PAYLOAD_ABSENT_BLOCKED_BY_DATA_MONOLITH_PIPELINE` in the shared optimization report. The solar runtime still owns its Vault buffers by deterministic BufferID and fails closed when external payloads are absent.

Rejected Alternatives: Generating a fake `static_data.h8bin` was rejected because it would cross domain ownership and poison the global binary payload ledger.

Scalability potential: No direct runtime scalability change; this is an integration gate for boot-time payload sovereignty.

Hardware Impact: No frame-time gain claimed. Prevents a false readiness claim that would hide boot/integration failure on all hardware tiers.

## Decision 10: Weather Dependency Cache

Problem: Solar condition building still read `GlobalRegistry.Weather` during the leader slow-tick path. That is not inside Burst, but it is still a repeated global service lookup in an owner cadence.

Solution: Make `SolarPanel` implement `IGlobalRegistryHotSwapListener` and `IGlobalRegistryHotSwapRefListener`. The leader caches `IWeatherService` during lane registration and updates it when the Weather service slot is rebound. `ResolveStormTurbidity` reads only the cached field.

Rejected Alternatives: Reading `GlobalRegistry.Weather` every slow tick was rejected because the project doctrine says GlobalRegistry is cold identity/dependency injection, not a hot polling route. Creating a new weather SignalBus lane was rejected because solar only needs a scalar snapshot and the existing service contract already owns it.

Scalability potential: All tiers keep the same math. The change reduces authority coupling and avoids turning solar into a global registry poller as panel count grows.

Hardware Impact: No honest microsecond claim; this is compile-wall and authority hygiene. The measurable cost remains dominated by panel count, SDF samples, and CSR node application.

## Decision 11: Batched Panel State Hydration

Problem: The cold facade still wrote `PanelStates` by calling the runtime write method once per panel. That created one Vault write lock/unlock pair per active panel before every solar solve.

Solution: Add `SolarPowerGenerationRuntime.TryAcquirePanelStateWrite`/`ReleasePanelStateWrite` and make the leader slow tick acquire one write lock for the whole `SolarPanelStateDTO` array. Rows are written through `NativeArrayUnsafeUtility.GetUnsafePtr` and `UnsafeUtility.AsRef<SolarPanelStateDTO>`.

Rejected Alternatives: Keeping per-panel write calls was rejected because it scales lock transitions with panel count. Creating a new manager class to own panel transforms was rejected because it would duplicate the existing facade and widen the compile surface.

Scalability potential: Low tier still solves at reduced cadence; Middle/High/Ultra can hydrate hundreds of panels with one ownership transition before Burst jobs consume contiguous DTO rows.

Hardware Impact: At 500 panels, this removes up to 500 Vault lock/unlock transitions per solve and leaves only one contiguous DTO write pass. Actual microsecond gain depends on Vault lock implementation and contention.

## Decision 12: Runtime Status Renderer Eviction

Problem: The solar facade still carried a per-panel `Renderer`/`MaterialPropertyBlock` emission update. It did not own gameplay truth, but it kept a standard Unity presentation mutation path in the runtime class and violated the project SRP-batcher rule for standard geometry.

Solution: Remove the runtime status-indicator fields, `MaterialPropertyBlock` allocation, `Shader.PropertyToID`, `GetComponent<Renderer>()`, and property-block update calls from `SolarPanel`. Presentation proof stays in the editor gizmo and Vault output DTOs; production visuals must consume the shared shader/Vault route instead of per-object material mutation. Move the `SolarPanel[512]` facade table allocation into `SubsystemRegistration` with a cold allocation marker, keeping `OnEnable` registration as table fill only in the normal path.

Rejected Alternatives: Keeping MPB as a "small debug light" was rejected because the task is explicitly about killing object-oriented solar residue, and MPB on standard mesh renderers breaks batching. Creating per-panel material instances or prefab YAML edits was rejected because it would expand the blast radius and mutate assets outside the domain.

Scalability potential: Low tier avoids per-panel renderer state churn entirely; Middle/High/Ultra can spend solar scalar data in shaders or debug overlays without adding gameplay truth or per-panel CPU material work.

Hardware Impact: No honest normal-frame microsecond claim without Frame Debugger/profiler proof. Static impact is removal of one cold MPB allocation per panel instance and all runtime `GetPropertyBlock`/`SetPropertyBlock` calls from the solar facade; the 512-entry facade table is now boot-cold rather than first gameplay registration.

## Decision 13: Binary Payload Ledger Repair

Problem: `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` had no `SHINOBU_341` entry despite the solar route owning new Vault lanes and explicit runtime DTO ABIs.

Solution: Append a concise solar payload entry naming the first-pass draft BufferIDs and the DTO sizes, runtime job route, authority route, quality route, Dear Lie route, fault dump path, and static verification boundary. This decision is historical and was superseded by Decision 15 after collision archaeology moved current solar ownership to `73410..73418`.

Rejected Alternatives: Leaving the proof only in `LOG_SHINOBU_341.md` was rejected because the architecture ledger is the shared binary payload authority surface. Reordering the large ledger was rejected because concurrent agents are appending to the same document.

Scalability potential: No runtime change. The ledger documents that quality changes cadence/math richness only, not DTO layout or power authority.

Hardware Impact: No frame-time claim. This is integration-proof debt repayment.

## Decision 14: Vault Write-Lock Fault Release

Problem: Several write paths combined `TryAcquireWriteLock` with returned-array validity checks in one guard. If Vault returned a lock with an invalid or too-small view, the method could return before `finally` and leave the buffer locked.

Solution: Split lock acquisition from view validation in `TryWritePanelState`, `TryLoadProfilesFromCsv`, and `WriteConditionsRow`. After a successful lock, every exit path now passes through `finally` and releases the Vault write lock.

Rejected Alternatives: Assuming `TryAcquireWriteLock` always returns a valid array was rejected because stale generations and relocation faults are exactly where Vault discipline matters. Adding a broad `UnlockJobBuffers` call was rejected because these are scoped write locks, not the scheduled job lock mask.

Scalability potential: No quality-tier behavior change. The fix protects all tiers from a stuck buffer after a rare fault.

Hardware Impact: Fault-path correctness only; no normal-frame microsecond claim.

## Decision 15: BufferID Collision Eviction

Problem: The draft solar Vault range `73341..73349` overlapped existing physiology ownership. `73341` and `73342` are SHINOBU_320 metabolism suit profile lanes, and `73343` is SHINOBU_321 decompression telemetry. Keeping solar on that range would let `SystemID.PowerGrid` and `SystemID.GameplayPlayer` resolve different DTO shapes through the same numeric identity.

Solution: Move all solar-owned Vault lanes to `73410..73418` after a targeted grep confirmed no active `BufferID` constants or stable SHINOBU proof records already own that range. Update `PowerGridSolarContracts.cs`, the binary payload ledger, the shared optimization report, and live status to mark `73341..73349` as rejected draft IDs.

Rejected Alternatives: Reusing `73341..73349` with a different `SystemID` was rejected because BufferID sovereignty must be globally unambiguous for diagnostics, crash dumps, and future Data Monolith hydration. Generating a central registry edit was rejected because that would widen the blast radius into core ownership during a domain-limited polish pass.

Scalability potential: No quality-tier math changes. The eviction preserves the same low/middle/high/ultra solar cadence and SDF sample curves while preventing unrelated physiology buffers from being interpreted as solar DTOs.

Hardware Impact: No frame-time gain claimed. This prevents catastrophic cross-domain buffer corruption and false black-box evidence on every hardware tier.

## Decision 16: Stable Power Node Index Cache Fast Path

Problem: `ResolveSolarPowerNodeIndicesJob` mapped each panel `PowerNodeHashID` to the CSR node row by scanning every `PowerNodeDTO` row on every solar solve. At default capacity that is 512 panels * 1024 nodes = 524288 hash comparisons before optical math, even when topology is unchanged.

Solution: Reuse the existing Vault-backed `PanelPowerNodeIndices` row as a validated cache. The resolver first checks whether the previous index is in range and still points at a `PowerNodeDTO.NodeHash` equal to the panel hash. Only stale, missing, or topology-shifted rows fall back to the full scan.

Rejected Alternatives: Adding a new `NativeHashMap`/hash table was rejected because it would create new native ownership and a new mutation lifecycle without a route card. Trusting the cached index without hash validation was rejected because CSR rows can move after topology rebuild.

Scalability potential: Low/Middle/High/Ultra keep identical power truth and DTO layout. Stable topology reduces mapping work continuously for all quality tiers; expensive scans occur only when a panel is new, a node hash changes, or the power graph topology reorders.

Hardware Impact: Default stable case drops mapping from roughly 524288 hash comparisons to 512 direct cached-index validations at 512 panels and 1024 nodes. No profiler microsecond claim until Unity/Burst runtime proof exists.

## Decision 17: Read-Only Vault Accessor Split

Problem: Public solar read accessors returned immutable values to callers, but internally borrowed mutable Vault views through `TryReadHandle`. That violates the global doctrine that `Get*`/`TryGet*`/`Read*` routes must be pure immutable snapshots and not borrow write-capable views.

Solution: Convert `TryReadOutput`, `TryReadPanelState`, `TryCopyTelemetry`, `TryGetTuning`, and the latest-telemetry helper to `TryReadOnlyHandle`. The diagnostic raw dump path remains a private crash-only route because it needs a contiguous pointer for `ReadOnlySpan<byte>` export after the solver fence has completed.

Rejected Alternatives: Leaving mutable reads was rejected because it weakens Vault proof even if callers only receive copied DTO values. Adding copied managed arrays was rejected because it would allocate and break editor/live telemetry constraints.

Scalability potential: No math or quality change. All tiers preserve the same DTO route; read consumers now receive immutable snapshots without changing BufferIDs or solve cadence.

Hardware Impact: No honest microsecond claim. The gain is authority correctness and preventing accidental mutation through read lanes.

## Decision 18: Raw Pointer NoAlias Proof

Problem: `EvaluateOpticalDepthJob` and `ApplySolarPowerToCsrNodesJob` receive raw pointers into distinct Vault buffers, but the pointer fields were not all marked `[NoAlias]`. Burst must be allowed to assume state rows, output rows, node counters, and CSR node rows do not physically overlap.

Solution: Add `[NoAlias]` to `PanelStatesPtr`, `OutputsPtr`, `NodeSolarInputCountersPtr`, and `NodesPtr`. The runtime already locks these buffers as separate Vault lanes for the dispatcher-owned job window, so the alias claim matches ownership reality.

Rejected Alternatives: Leaving pointer aliasing implicit was rejected because it can suppress Burst vectorization and generate conservative loads/stores. Copying data into temporary local arrays was rejected because it would allocate or add memory bandwidth with no gameplay value.

Scalability potential: No quality-tier logic changes. Low-tier benefits from cheaper batch math; high/ultra can spend the same route on richer SDF samples without changing DTO identity.

Hardware Impact: No profiler-backed microsecond claim. This is a compiler-proof improvement for AVX2/NEON auto-vectorization over separated Vault lanes.

## Decision 19: Ledger Owner Correction And Teardown Fence Boundary

Problem: Independent audit found that the ledger used stale owner text `SystemID.PowerGrid`, while runtime solar handle acquisition uses `SystemID.Power`. The same audit also flagged the forced completion helper in `ResetForSubsystemRegistration` as a literal `.Complete()` bridge through `DispatcherJobFence`.

Solution: Correct the ledger owner to `SystemID.Power`. Keep the forced completion only in the subsystem-registration reset path, where the domain is being torn down and Vault locks must be released before static state is cleared. Normal scheduling, finalization, and read paths still use returned `JobHandle` plus `DispatcherJobFence.TryFinalizeCompleted`; no slow tick, late tick, or public read accessor force-completes the solar chain.

Rejected Alternatives: Renaming runtime owner to a non-existent `SystemID.PowerGrid` was rejected because the enum and existing power contracts use `SystemID.Power`. Removing teardown completion was rejected because a domain reset with a live pending job would leave locked Vault buffers or clear static handles under an active job.

Scalability potential: No quality-tier change. The correction preserves route proof and keeps frame-loop behavior non-blocking across low/middle/high/ultra.

Hardware Impact: No runtime microsecond gain. This prevents proof drift and confines the only forced completion to cold teardown.

## Decision 20: Compile Gate Refresh

Problem: The previous status entry still referenced older CPU samples and earlier compiler-process observations. The current objective gate sampled CPU at 57% with 1 active `dotnet` process.

Solution: Keep build verification blocked and update status/report/log proof to the latest objective gate. No `dotnet build` or Unity compile was launched from SHINOBU_341.

Rejected Alternatives: Launching a build anyway was rejected because the active process and CPU rules are explicit. Killing other agents' dotnet processes was rejected because they are not owned by this task.

Scalability potential: No runtime effect.

Hardware Impact: Prevents local machine contention and avoids corrupting concurrent compile work.

## Decision 21: Low-Tier Optical Collapse And Read-Only SDF Borrow

Problem: The optical solver still paid the exact `math.exp` cost even when `GlobalQualityWeight` was below the low-tier blend range, and the Voxel SDF payload was borrowed through mutable `TryReadHandle` despite being read-only job input. The analytic mountain fallback also used `sin/cos`, which is unnecessary for a Dear Lie shadow proxy and weakens cross-platform determinism proof.

Solution: Gate exact Beer-Lambert `exp` behind a smooth quality blend that is zero below `GlobalQualityWeight=0.30`; low tier returns the rational attenuation approximation only. Convert Voxel SDF descriptor/texture borrows to `TryReadOnlyHandle` and pass `NativeArray<byte>.ReadOnly` into `EvaluateOpticalDepthJob`. Make SDF sample count a fractional budget so new samples fade in without a power pop. Replace analytic ridge `sin/cos` with deterministic triangle waves.

Rejected Alternatives: Keeping exact exp in all tiers was rejected because it violates the required continuous ALU collapse on throttled devices. Keeping mutable SDF reads was rejected because the solar job never writes SDF and read accessors must not borrow write-capable views. Trig fallback was rejected because a visual fake ridge does not need transcendental math.

Scalability potential: Low uses rational Beer-Lambert, one SDF sample, nearest SDF lookup, and triangle-wave ridge. Middle fades in trilinear sampling and extra SDF samples. High/Ultra evaluate exact exponential attenuation and richer SDF sampling while preserving the same DTOs and CSR ownership.

Hardware Impact: On low-end silicon this removes exact exp and seven-to-eight SDF texture samples from the minimum-quality optical path. No profiler microsecond claim until Unity import/profiler proof exists.

## Decision 22: Append-Only Audit ABI Correction

Problem: The first SHINOBU_341 log append still contained current-looking `73341..73349` Vault IDs inside the initial self-audit block. That range is now known collision evidence, not the active solar ABI.

Solution: Patch the early log text to name current `73410..73418` lanes and mark `73341..73349` as rejected draft IDs. Keep later collision archaeology entries intact so the audit trail still explains why the range moved.

Rejected Alternatives: Leaving the stale self-audit untouched was rejected because downstream grep tools could misread it as current ABI truth. Deleting all historical `73341..73349` references was rejected because the collision proof is still useful forensic context.

Scalability potential: No runtime quality change; low/middle/high/ultra solar math remains the same. The change protects Data Monolith and Vault integration readers from a false ownership map.

Hardware Impact: No frame-time effect. This is proof-surface hygiene that prevents cross-domain buffer interpretation faults during integration.

## Decision 23: Scanner Self-Token Decontamination

Problem: The focused forbidden-token gate could match the scanner's own fallback `"Update("` literal even though the runtime solar path had no `Update` method.

Solution: Split the fallback token as `"Update" + "("`, matching the scanner's existing split-token pattern for raycast, RenderSettings, and DateTime probes.

Rejected Alternatives: Ignoring the self-hit was rejected because static proof should be grep-clean without requiring manual exception text. Removing the fallback probe was rejected because the scanner must still catch parser-damaged solar files.

Scalability potential: No runtime quality change; editor proof tooling remains outside player builds.

Hardware Impact: No frame-time effect. This is scanner fidelity only.

## Decision 24: Compile Gate Refresh After Scanner Patch

Problem: A gate after the scanner self-token patch measured CPU at 100% with 7 active `dotnet` processes. Later gates superseded those values, but the CPU/process block remains.

Solution: Keep compile verification blocked under the batch policy. The shared report now tracks the latest objective gate, currently `compileGateLastCpuPercent=57.0`, `compileGateProcessCount=1`.

Rejected Alternatives: Launching a build anyway was rejected by the explicit CPU/process gate. Terminating other dotnet processes was rejected because they are not owned by SHINOBU_341.

Scalability potential: No runtime effect.

Hardware Impact: Avoids adding compile load to an already saturated shared machine.

## Decision 25: Compile-Gate Proof Normalization

Problem: The append-only proof surface contained stale "latest" language from earlier compile gates, including the obsolete no-active-dotnet sample and later saturated-CPU samples. That creates an integrator hazard because the same SHINOBU_341 audit could appear to both permit and forbid a build.

Solution: Normalize SHINOBU_341 status/rationale/log language to the current objective gate: `CPU=57%`, 1 active `dotnet` process, no build launched. Keep older samples only as historical context where they explain sequence, not as current authority.

Rejected Alternatives: Deleting historical gates was rejected because append-only forensics must preserve why build proof is absent. Leaving contradictory "latest" claims was rejected because it undermines the compile-wall evidence.

Scalability potential: No runtime quality change. This protects production iteration by preventing a second agent from interpreting stale proof as permission to build under load.

Hardware Impact: No frame-time effect. Prevents avoidable compile contention on an already saturated workstation.

## Decision 26: Batch Block Extraction Regex Correction

Problem: A strict XML extraction pattern looking only for `<AGENT_PROMPT id="SHINOBU_341">` missed the current batch block because the tag contains additional attributes: `role="SOLAR_PANEL_POWER_GENERATION_SCALAR"` and `chat_name="SHINOBU_341"`.

Solution: Treat the parser miss as a tooling issue, not as missing task truth. Re-read the block with `rg -C` around `SHINOBU_341` and confirmed the 20-task matrix still matches the saved status ledger.

Rejected Alternatives: Assuming the assignment disappeared was rejected because `CURRENT_BATCH.md` clearly still contains the SHINOBU_341 block. Relying on chat-memory task text was rejected by the anti-amnesia rule.

Scalability potential: No runtime effect.

Hardware Impact: No frame-time effect. Prevents documentation drift from steering code changes.

## Decision 27: Voxel SDF Shadow Sign Correction

Problem: The Voxel SDF shadow proxy used `Smooth01(-0.5, 2.0, signed)` as its solid mask and began marching one step away from the panel. That inverted the intended SDF semantics: positive free-space samples produced high occlusion, while strongly negative solid/cave samples produced no occlusion. It also skipped the exact panel-local SDF cell required by Task 09.

Solution: Start the SDF occlusion walk at `i=0`, so low quality performs the single required panel-origin sample. Convert negative signed distances to high occlusion with `1 - Smooth01(-2.0, 0.5, signed)`, then apply the existing fractional sample budget as `sampleBudget - i`.

Rejected Alternatives: Keeping the old sign convention was rejected because it contradicts the assignment's "strongly negative cave" rule and can darken open water. Adding PhysX confirmation probes was rejected because the task exists to destroy that path. Adding a second dense raymarch was rejected because low quality must remain a one-sample Dear Lie.

Scalability potential: Low quality now performs exactly one panel-origin SDF sample plus rational attenuation. Middle and high quality fade in additional sun-direction samples without changing output DTOs, BufferIDs, or power authority.

Hardware Impact: No extra low-tier sample cost; the loop still evaluates one sample at quality 0. Higher tiers keep the same 1..9 fractional sample envelope, but the occlusion truth is now physically coherent.

## Decision 28: Subagent Audit Remediation

Problem: Independent static audit flagged real proof and authority debt: solar still read `GlobalRegistry.CelestialRuntimeSnapshot` during slow-tick condition build and editor gizmo draw, unsafe pointer jobs carried waiver attributes without local safety proof, `HasResolvedBuffer<T>` was public despite resolving mutable Vault views, Roslyn syntax fallback did not mark parser failure when fallback could not classify a file, and the editor tuner repainted through `EditorApplication.update`.

Solution: Solar condition build and gizmo now read celestial data through `SolarPowerGenerationRuntime.TryReadCelestialSnapshot`, which borrows SHINOBU_345 `CelestialStateDTO` and `EnvironmentStateDTO` through read-only Vault handles cached during the solar owner ensure path. Unsafe pointer fields now carry local invariant/alternative/safety comments. `HasResolvedBuffer<T>` is private to cold buffer ensure. `OOP_Solar_Scanner` increments parser failures when syntax fallback cannot classify a parse-error file. The UI Toolkit tuner uses a scheduled 200 ms editor callback instead of global editor update.

Rejected Alternatives: Keeping the GlobalRegistry snapshot read was rejected because registry is dependency injection, not hot celestial transport. Reading `CelestialStateDTO` by importing a sibling runtime service was rejected; the borrowed Vault rows are the owner-published unmanaged route. Changing `SolarConditionsDTO` from 160 bytes was rejected because the DTO is explicit, pointer-free, and aligned to 32-byte multiples; shrinking it would remove required sun/SDF/quality/tuning scalars without a runtime defect.

Scalability potential: Low/Middle/High/Ultra solar math is unchanged. Celestial truth now reaches solar through the same unmanaged row route across tiers, while editor refresh cadence is decoupled from frame-rate.

Hardware Impact: No honest frame-time claim. The authority fix removes one hot static registry read per solar solve/gizmo draw and prevents false-green scanner proof after syntax damage; editor repaint throttling reduces editor-only churn.

## Decision 29: Guarded Runtime Build Proof

Problem: Earlier SHINOBU_341 proof could not legally run a build because CPU/process gates were closed. After loop 25, the gate sampled CPU at 4% with zero active `dotnet/csc` processes, and the new `Hecton8.Environment` Vault borrow plus editor-schedule changes needed compiler validation.

Solution: Launched one guarded runtime project build: `dotnet build Hecton8.Core.csproj --no-restore -nologo -clp:ErrorsOnly -m:1 /nr:false`. It succeeded with 0 warnings and 0 errors in 37.38 seconds. No rebuild was launched while the gate was closed.

Rejected Alternatives: Skipping compile proof after adding the celestial Vault borrow was rejected because namespace/asmdef risk was real. Building all editor/player projects was rejected because new editor files are not present in generated editor csproj until Unity import regenerates source graphs, and broader project builds would expand outside the SHINOBU_341 proof scope.

Scalability potential: No runtime quality change. This validates the runtime solar route that every quality tier consumes.

Hardware Impact: No frame-time claim. The build command used single-process `/m:1` and `/nr:false` after the workstation was below the 50% CPU threshold.

## Decision 30: Editor Facade Churn Reduction

Problem: The SHINOBU_341 runtime path was clean, but the editor proof tools still carried avoidable production friction. `OOP_Solar_Scanner.RunScanner()` called `AssetDatabase.Refresh()` after writing a report under `Docs/`, even though that path is outside `Assets/` and the refresh can trigger unnecessary Unity import work. The tuner used a lambda callback and formatted/repainted on every scheduled editor tick even when telemetry did not change.

Solution: Remove the scanner asset refresh and console log. Replace the slider lambda with a named `OnSliderValueChanged` handler. Add a telemetry dirty key (`FrameIndex`, `StateHash`, `SolverMicroseconds`, `ActivePanelCount`) so the summary string and graph repaint occur only when the copied telemetry head changes. Static gates passed after the patch. The latest compile gate sampled CPU=11% and active `dotnet/csc`=0, but no rebuild was launched because the changed C# files are editor-only and still pending Unity import/sourcegraph regeneration.

Rejected Alternatives: Keeping `AssetDatabase.Refresh()` was rejected because the report file is not an asset and compile-wall protection matters more than editor convenience. Replacing UI Toolkit with a custom IMGUI polling loop was rejected because it would increase repaint pressure. Removing the editor summary string entirely was rejected because Task 16/18 require a designer-facing facade; the cost is editor-only and now change-driven.

Scalability potential: Runtime low/middle/high/ultra math is unchanged. Editor low-end laptops avoid unnecessary import refresh and unchanged telemetry repaint, while high-end editor sessions retain the same graph and live tuning controls.

Hardware Impact: No runtime microsecond claim. Static proof shows the player hot path is untouched; editor churn is reduced by removing one global asset refresh per scanner run and one scheduled summary/repaint update when telemetry is unchanged.

## Decision 31: Editor Gizmo Label Dirty Cache

Problem: `SolarPanel.OnDrawGizmosSelected` still used a C# interpolated string for the Scene View x-ray label. The code was editor-only, but it allocated and formatted on every selected repaint while Task 18 requires live visibility.

Solution: Replace the interpolated label with an editor-only static `GUIContent`, a cold `StringBuilder`, and a quantized hash over `PowerNodeHashID`, watts, depth, angle, and shadow. The managed string is rebuilt only when those quantized values change; otherwise `Handles.Label` receives the cached content. The runtime Burst/Vault solve path is untouched.

Rejected Alternatives: Removing the label was rejected because Task 18 requires the visual x-ray proof. Keeping `$"..."` was rejected because it is avoidable editor churn. Building a runtime HUD route was rejected because solar truth already lives in Vault DTOs and editor-only diagnostics must not become gameplay authority.

Scalability potential: Runtime low/middle/high/ultra behavior is unchanged. Low-end editor machines avoid repeated selected-panel label allocations when values are stable; high-end editor sessions retain the same live diagnostics across dense panel arrays.

Hardware Impact: No runtime frame-time claim. The gain is editor-only allocation reduction at the Unity string boundary; `Handles.Label` still requires managed text, but the allocation frequency is now value-change driven instead of repaint driven.

## Decision 32: Loop 27 Narrow Build Proof

Problem: The loop-27 patch lives under `#if UNITY_EDITOR` inside `SolarPanel.cs`. The generated `Hecton8.Core.csproj` defines `UNITY_EDITOR` and includes `SolarPanel.cs`, so a narrow runtime-core build is a real syntax/sourcegraph proof for this patch.

Solution: Re-sampled compile gate: CPU=49%, active `dotnet/csc`=0. Ran `dotnet build Hecton8.Core.csproj --no-restore -nologo -clp:ErrorsOnly -m:1 /nr:false`. Result: 0 warnings, 0 errors, elapsed 2.13 seconds.

Rejected Alternatives: Skipping compile proof was rejected because the guarded editor block is included in the generated core project. A broad rebuild or Unity editor import claim was rejected because the new editor-only scanner/tuner sourcegraph still depends on Unity project regeneration.

Scalability potential: No runtime quality change. This preserves the same solar math tiers and proves the modified facade syntax without expanding compile-wall blast radius.

Hardware Impact: No frame-time claim. Build used single-process `/m:1` after the CPU/process gate opened.

## Decision 33: Subagent Static Audit Remediation Pass

Problem: The second static audit found that the scanner accepted only owner text exactly `Physics`, the shared report writer had no inter-agent mutex, `LateFrameTick` read one output row per panel every frame after a solve, depth presentation recomputed `exp`, the condition row write sat before the job-lock `try/finally`, and the editor tuner repainted empty telemetry every 200 ms.

Solution: `OOP_Solar_Scanner` now catches `UnityEngine.Physics`, `.Physics` suffix owners, `using Phys = UnityEngine.Physics`, and `using static UnityEngine.Physics` calls, and writes the shared report under a named mutex with retry and JSON validation. `SolarPanel.LateFrameTick` now reads a single read-only output snapshot once per completed solver frame and skips unchanged frames. Depth presentation uses the same rational attenuation family as low-tier Beer-Lambert instead of `math.exp`. `TrySchedule` writes the condition row inside the lock-release `try/finally`. The tuner marks the empty telemetry graph dirty only on availability/count state changes.

Rejected Alternatives: Adding semantic-model compilation to the scanner was rejected because the editor sourcegraph is not imported yet and suffix/alias/static syntax coverage closes the reported false-green cases without creating new project references. Keeping per-panel `TryReadOutput` calls was rejected because the output buffer is already one contiguous Vault row set. Editing unrelated `PlayerToolManager.cs` was rejected because it is outside the SHINOBU_341 power-solar boundary.

Scalability potential: Low-end devices benefit from facade readback collapsing to one immutable snapshot per completed solve and no per-frame presentation `exp`; middle/high/ultra retain identical solar truth and can still update visual facades when the solver frame advances.

Hardware Impact: At 512 active panels, steady rendered frames after a completed solar solve no longer perform 512 output read-handle attempts or 512 presentation exponentials. This is a facade/readback estimate; authoritative Burst solver cost is unchanged.

## Decision 34: Loop 28 Compile Blocker Classification

Problem: The first guarded loop-28 build caught a SHINOBU-local `CS8156` caused by passing a `NativeArray<T>.ReadOnly` indexer directly by `in`. After fixing it with a local DTO copy, the next guarded build failed in `Assets/_Project/Scripts/PlayerToolManager.cs` with `CS0029` and `CS8121`, unrelated to the solar domain.

Solution: Fixed the local indexer-by-ref issue in `SolarPanel.LateFrameTick`. Mark the remaining core build failure as `[BLOCKED BY DEPENDENCY]` on `PlayerToolManager.cs`; do not edit or revert that file because it belongs to another workstream.

Rejected Alternatives: Forcing a third build without a dependency change was rejected because it would repeat the same external compiler errors. Editing `PlayerToolManager.cs` was rejected by the domain boundary and dirty-worktree rule.

Scalability potential: No runtime quality change.

Hardware Impact: No frame-time claim. Build gate was respected: first build at CPU=29%/0 active processes, second at CPU=8%/0 active processes; no build was launched while CPU/process gates were closed.

## Decision 35: Unchanged Solver Frame Output Borrow Skip

Problem: Loop 28 collapsed facade readback from per-panel Vault reads to one output snapshot borrow, but the leader still borrowed `PanelOutputs` every rendered frame after the solver frame stopped changing just to discover the frame was unchanged.

Solution: Add `SolarPowerGenerationRuntime.TryGetCompletedOutputFrameIndex`, a pure static read of the completed output frame flag. `SolarPanel.LateFrameTick` now checks that frame index first and only borrows the read-only `PanelOutputs` snapshot when the completed solver frame differs from `s_lastAppliedOutputFrame`.

Rejected Alternatives: Keeping the one stable-frame Vault borrow was rejected because the completed-frame scalar is already owned by the solar runtime and is enough to decide whether output readback is needed. Publishing a new signal was rejected because this is local facade state, not cross-domain gameplay truth.

Scalability potential: Low tier benefits most because solar solves are cadence-throttled and many rendered frames can share one completed output frame. Middle/high/ultra still update immediately when the solver publishes a new output frame.

Hardware Impact: Removes the remaining stable-frame `PanelOutputs` read-only handle borrow after loop 28. No authoritative solver microsecond claim; this is facade readback traffic reduction.

## Decision 36: Post-Loop Auditor Remediation

Problem: The post-loop auditor found four proof debts: scanner alias/static coverage ignored namespace-scoped `using` directives, syntax-damaged fallback could miss alias member calls, the shared report top-level `compileProof` still sounded green while current core build is externally blocked, and the black-box dump borrowed telemetry through `TryReadHandle`.

Solution: Scan all `UsingDirectiveSyntax` descendants in addition to root usings. Add conservative `.Raycast(` and `.RaycastNonAlloc(` fallback tokens for syntax-damaged solar files. Change top-level report `compileProof` to the current external `PlayerToolManager` blocker and preserve prior green builds under loop-specific fields. Change `DumpBlackBoxOnce` to borrow telemetry through `TryReadOnlyHandle`.

Rejected Alternatives: A full Roslyn semantic model was rejected for now because the editor files still await Unity project regeneration and suffix/alias/static syntax coverage addresses the audited false-negative forms. Leaving the top-level compile proof green was rejected because it can mislead integrators after loop 28. Keeping mutable dump reads was rejected because fault-path code still has to obey read-only access discipline.

Scalability potential: Runtime solar math tiers are unchanged. The changes protect proof tooling and crash autopsy routes without altering DTO identity, power authority, or quality behavior.

Hardware Impact: No frame-time claim. This is proof correctness plus fault-path read-only discipline.

## Decision 37: Read-Only Fault Dump Pointer Form

Problem: The black-box dump correctly borrowed telemetry through `TryReadOnlyHandle`, but the raw span pointer used the static `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(...)` form on a `NativeArray<T>.ReadOnly` view. Installed Collections supports the extension form, and sibling runtime code uses that shape for read-only native views.

Solution: Change the dump writer to `telemetry.GetUnsafeReadOnlyPtr()` after the read-only borrow. The route stays read-only and writes the same raw `SolarTelemetryEntry` payload after the fixed 32-byte dump header.

Rejected Alternatives: Reverting to mutable `TryReadHandle` was rejected because fault-path code still obeys read accessor discipline. Keeping the static overload was rejected because package overload shape is a compile-risk surface and the extension form matches the installed Collections API.

Scalability potential: No low/middle/high/ultra math change. The same 300-frame telemetry ring remains the proof artifact across all quality tiers.

Hardware Impact: No frame-time claim. This removes a sourcegraph risk in a cold fault path and does not touch solver ALU or memory bandwidth.

## Decision 38: Ledger Compile Proof Normalization

Problem: The binary payload ledger still presented the loop-25/27 green core build as the visible SHINOBU_341 verification state. Current objective proof is different: after the local solar `CS8156` was fixed, the guarded core build is blocked outside this domain by `PlayerToolManager.cs` `CS0029` and `CS8121`, and the current process gate has 7 active `dotnet` workers.

Solution: Update the SHINOBU_341 ledger verification to keep the previous green builds as historical proof while naming the current external blocker and the active process gate. The shared report already carried the blocker; the ledger now matches it.

Rejected Alternatives: Deleting prior green build evidence was rejected because it is still valid historical proof for the runtime solar sourcegraph before later unrelated external failure. Leaving the ledger as green-current was rejected because it would mislead the integrator.

Scalability potential: No runtime quality change. This protects Data Monolith/Vault integration readers from stale authority evidence.

Hardware Impact: No frame-time effect. It prevents an unnecessary rebuild attempt under active compiler-worker load.
