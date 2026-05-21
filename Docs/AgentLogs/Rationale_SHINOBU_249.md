# SHINOBU_249 Rationale - BUOYANCY_SLEEP_STATE_INTEGRATOR

Status: PENDING VERIFICATION / POLISH PASS ACTIVE

## Decision 0 - Scope Boundary
Problem: Sleep-state work crosses buoyancy, KCC state, SDF reads, wake signals, and render promotion. Editing every dependent domain would create direct coupling during a 20+ agent batch.
Solution: Keep authority in the existing physics/buoyancy Vault-backed DTOs and jobs; use existing `WakeRequestSignal`/SignalBus surfaces where available; add only bounded DTO fields and jobs required for sleep/wake state.
Rejected Alternatives: Direct `Rigidbody.Sleep()`/`WakeUp()` is rejected because custom buoyancy truth would desync from PhysX sleep. Moving bodies between active/inactive lists is rejected because it copies large structs and fragments static arrays. New global registry polling in jobs is rejected because hot registry lookup violates the global systems doctrine.
Scalability potential: Low tier increases sleep aggression and reduces current polling cadence; middle tier keeps normal thresholds; high tier keeps more stable-frame history; ultra tier can spend saved CPU on visual/static promotion telemetry without changing gameplay truth.
Hardware Impact: Expected low-end i3/MX350 gain is eliminating buoyancy force evaluation for settled debris rows. Target class is 50,000 debris records; exact microseconds require Unity profiler proof.

## Decision 1 - Existing Layout Conflict
Problem: The SHINOBU_249 prompt requires `KinematicStateDTO.Flags` at offset 52, but current `KinematicStateDTO` has `DragCoefficient` at offset 52 and no flags.
Solution: Audit first before mutation. If changed, keep the struct at 64 bytes, move drag to an aligned remaining slot, update layout validators, and avoid broad KCC behavioral changes.
Rejected Alternatives: Ignoring the prompt layout is rejected because sleep state needs rollback-visible flags. Replacing the KCC DTO with a new managed wrapper is rejected because Burst and rollback memcpy require a fixed unmanaged layout.
Scalability potential: One 64-byte cache-line state remains intact for weak devices; high/ultra tiers can use spare byte counters for longer deep-sleep thresholds.
Hardware Impact: Preserving one-cache-line state avoids extra L1 fetches; estimated per-row saving is one avoided extra state lookup when sleep flags and velocities sit in the same line.

## Decision 2 - Sleep Flag Instead Of Active/Inactive Migration
Problem: Sleeping 50,000 debris records by moving rows between active and inactive arrays would copy 64-byte DTOs and invalidate row identity for rollback/debug.
Solution: Keep rows fixed and mutate `Flags` bits in `BuoyancyStateDTO` and `KinematicStateDTO`; downstream work treats sleeping rows as force-output disabled.
Rejected Alternatives: Managed `List<T>` active sets are rejected for GC and O(N) copy cost. Native compaction every frame is rejected because row identity and debug telemetry would need remap tables.
Scalability potential: Low uses aggressive thresholds and early sleep; middle keeps moderate rest frames; high preserves stricter settling; ultra spends saved physics time on render/static promotion.
Hardware Impact: On i3/MX350 class hardware, skipping 50,000 buoyancy/drag evaluations targets multiple milliseconds saved after debris settles; exact microseconds require Unity profiler capture.

## Decision 3 - SDF Grounding As Sleep Gate
Problem: Velocity-only sleep freezes neutrally buoyant mid-water objects and violates predictable physical authority.
Solution: Require SDF/plane grounding before sleep. The SDF sample subtracts grid `double3` AUP origin from entity `double3` AUP, then casts the local delta to `float3`.
Rejected Alternatives: Absolute world-float SDF lookup is rejected due edge-of-map precision drift. Scene raycasts are rejected because hot physics jobs cannot depend on managed/PhysX queries.
Scalability potential: Low can rely mostly on nearest-cell SDF/plane fallback; middle uses configured contact epsilon; high/ultra can provide denser SDF data through the same Vault route.
Hardware Impact: Nearest-cell signed byte SDF costs one indexed read; it prevents repeated full buoyancy work for grounded rows.

## Decision 4 - Wake Via SignalBus Snapshot
Problem: Sleeping objects must wake on shockwaves/player/projectile forces without running collision checks on all dormant debris.
Solution: Read `SignalBus<WakeRequestSignal>.GetFrameSnapshotArray()` once in runtime, pass the read-only snapshot into Burst wake jobs, and clear sleep flags by row.
Rejected Alternatives: Hot `GlobalRegistry` polling is rejected by doctrine. C# events are rejected for GC and ordering ambiguity. PhysX `WakeUp()` is rejected because the custom solver owns truth.
Scalability potential: Low frames pay almost zero when no wake signals exist; high/ultra can broadcast larger wake radii without changing sleep-state ownership.
Hardware Impact: No-signal frames skip the wake job entirely; signaled frames scan bounded NativeArray snapshots.

## Decision 5 - Vault-Backed Sleep Telemetry
Problem: Generic buoyancy telemetry lacked wake/source/deep-sleep counts needed for black-box postmortem.
Solution: Add `SleepStateTelemetryEntry` ring buffer with 300 fixed 64-byte rows and raw binary dump `Docs/AgentLogs/Dump_SHINOBU_249.bin`.
Rejected Alternatives: Text logs are rejected for hot paths and poor crash fidelity. Managed queues are rejected for GC and resizing risk.
Scalability potential: Low keeps the same 19.2 KB ring; ultra can correlate telemetry with render static promotion without changing DTO layout.
Hardware Impact: One 64-byte write per frame is negligible compared with avoided buoyancy evaluations.

## Decision 6 - Editor Control Is Cold Only
Problem: Leads need live tuning and proof without dragging managed UI into runtime simulation.
Solution: Put `PhysicsSleepStateXRayWindow`, layout validator, and scanner under `#if UNITY_EDITOR`; sliders mutate Vault-backed tuning/config rows only in editor play mode.
Rejected Alternatives: Runtime overlay UI is rejected because it adds managed update churn. IMGUI-only window is rejected because the prompt required UI Toolkit.
Scalability potential: Low/middle/high/ultra can be inspected with the same telemetry ring; slider values feed continuous thresholds rather than binary quality modes.
Hardware Impact: Zero player-build cost; editor-only inspection cost is not in frame budget.

## Decision 7 - Compile Gate
Problem: The project instruction forbids launching dotnet build while CPU is above 50% or any dotnet/csc process is active.
Solution: Queried processes and CPU twice. First sample had no dotnet/csc rows but CPU was 88.39%. Second sample had `dotnet` process 29812 active and CPU at 100%. Compile was not launched. Static scans and `git diff --check` were used instead.
Rejected Alternatives: Running build anyway is rejected because it violates the explicit batch law and risks contention with 20+ agents.
Scalability potential: Verification resumes when CPU is below threshold; code changes are isolated enough for a targeted `Assembly-CSharp.csproj` build.
Hardware Impact: No extra build load added to already saturated machine.

## Decision 8 - KCC/Buoyancy Coupling Correction
Problem: The rough-pass `KinematicSleepStateJobs.cs` lived under Buoyancy and imported `Hecton8.Physics.KCC`, creating a physical domain dependency in the wrong direction and a compile-wall smell.
Solution: Move the standalone `KinematicStateDTO` sleep jobs into the KCC folder/namespace and give them a KCC-local 64-byte `KinematicSleepSdfConfigDTO`. Buoyancy keeps its own `BuoyancySleepSdfConfigDTO` for the 50,000 debris route.
Rejected Alternatives: Keeping the import is rejected because the polish mandate requires assembly isolation. Moving shared config to Core/Contracts is rejected in this pass because it mutates a broader public contract without integrator review. Duplicating a tiny layout DTO inside KCC is accepted because it avoids a sibling dependency and keeps the runtime route local.
Scalability potential: Low/middle/high/ultra behavior remains controlled by continuous `GlobalQualityWeight`; the DTO split changes no gameplay truth or quality route.
Hardware Impact: Runtime cost unchanged. Build graph risk reduced by eliminating the unnecessary Buoyancy -> KCC reference.

## Decision 9 - Shared Report Preservation
Problem: `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` is a shared report touched by multiple agents. Overwriting it with SHINOBU_249-only content destroys other proof artifacts.
Solution: Add `PHYSICS_OPTIMIZATION_REPORT_SHINOBU_249.json` as the SHINOBU_249 sidecar and insert a `shinobu249BuoyancySleep` addendum into the shared JSON.
Rejected Alternatives: Blind overwrite is rejected as cross-agent sabotage. Chat-only scanner proof is rejected because Task 19 requires a report artifact.
Scalability potential: No runtime effect; preserves auditability under 40-agent batch pressure.
Hardware Impact: Editor/report path only.

## Decision 10 - Route Card Required
Problem: New Vault buffers, telemetry rings, and SDF config lanes are global authority surfaces. Prior architecture note was descriptive but not a route card.
Solution: Add `Docs/ARCHITECTURE/SHINOBU_249_BUOYANCY_SLEEP_ROUTE_CARD.md` with owner, route, cadence, failure, telemetry, lifecycle, and proof requirements.
Rejected Alternatives: Rationale-only documentation is rejected because global authority docs require route cards for new Vault/telemetry routes.
Scalability potential: Route card documents low/middle/high/ultra quality behavior without adding binary switches.
Hardware Impact: No runtime cost.

## Decision 11 - SDF Predicate Closure
Problem: The permissive predicate `signedDistance >= -epsilon` can mark any positive SDF distance as grounded, freezing slow objects that are not in contact.
Solution: Require `math.abs(signedDistance) <= contactEpsilon` in both buoyancy and KCC sleep kernels.
Rejected Alternatives: Keeping positive-distance permissiveness is rejected because Task 07 requires contact, not merely outside-solid status. Raycasts are rejected because this route must remain Burst/SDF only.
Scalability potential: Low/middle/high/ultra quality still changes thresholds and cadence, not physical contact truth.
Hardware Impact: One `abs` compare per SDF sample; prevents false sleeps that would create later wake/debug cost.

## Decision 12 - FixedTick Cold Recovery Removal
Problem: Simulation tick could enter `EnsureColdBooted`/descriptor recovery, which may allocate Vault descriptors and run cold CSV/boot work.
Solution: `FixedTick` now only proceeds when `_coldBootCompleted` and all handles are ready; failed buffer resolution returns without recovery.
Rejected Alternatives: Lazy recovery in hot tick is rejected because 0 B/frame cannot coexist with cold descriptor growth in the frame loop. Blocking on recovery is rejected because dispatcher phases must remain non-blocking.
Scalability potential: All quality levels fail closed under missing Vault readiness rather than stuttering.
Hardware Impact: Low-end devices avoid surprise descriptor/IO spikes during physics frames.

## Decision 13 - Angular Sleep Scalar In Existing Padding
Problem: Buoyancy-integrated sleep used `angularEnergy = 0`, so material angular thresholds were not enforceable in that route.
Solution: Consume existing `BuoyancyStateDTO` padding at offset 56 for `AngularSpeedSq`, keeping the DTO exactly 64 bytes.
Rejected Alternatives: Adding a full `float3 AngularVelocity` is rejected because it would grow the 64-byte cache-line state. A sidecar angular array is rejected because it creates a second hot fetch and ownership route.
Scalability potential: Low can leave scalar zero from legacy producers; high/ultra producers can feed angular scalar for stricter debris settling without layout changes.
Hardware Impact: Uses existing cache line; no additional memory bandwidth.

## Decision 14 - Scanner Proof Bound
Problem: The scanner used text search but report language implied AST-level proof.
Solution: Mask comments/strings and report `TOKENIZED_TEXT_SCAN_NOT_AST`; keep the sidecar report pending compile/runtime proof. Superseded by Decision 37, which upgrades the scanner source to Roslyn AST while preserving the old report as the last executed proof until Unity can run it.
Rejected Alternatives: Claiming AST proof without Roslyn is rejected. Pulling Roslyn into Unity editor tooling is rejected in this pass because it changes dependencies and compile wall.
Scalability potential: No runtime impact.
Hardware Impact: Editor/report path only.

## Decision 15 - Sleep Authority Boundary
Problem: The prompt requires KCC `KinematicStateDTO` sleep kernels, but the existing 50,000 debris buoyancy route is `BuoyancyStateDTO`. Scheduling both as active owners would create a split sleep fact.
Solution: Keep the KCC sleep jobs under KCC as owner-local kernel artifacts and keep active buoyancy force bypass authority in `BuoyancyStateDTO.Flags`.
Rejected Alternatives: Buoyancy writing `KinematicStateDTO` is rejected as cross-domain state mutation. KCC writing `BuoyancyStateDTO` is rejected for the same reason. A bridge signal is deferred to integrator review because no existing route card assigns it as the single owner.
Scalability potential: Low/middle/high/ultra behavior for active debris remains one route through buoyancy quality curves.
Hardware Impact: Avoids a second hot state fetch and avoids duplicate sleep scans.

## Decision 16 - Public Surface Documentation
Problem: AGENTS requires XML docs on public members; the new KCC sleep jobs and editor surfaces exposed public members without documentation.
Solution: Add XML summaries/params/remarks to the new public SHINOBU_249-facing surfaces without changing behavior.
Rejected Alternatives: Leaving public surfaces undocumented is rejected because code review would fail before runtime proof. Adding large narrative comments inside hot math loops is rejected because it reduces scan readability.
Scalability potential: No runtime behavior impact.
Hardware Impact: Documentation only; 0 runtime cost.

## Decision 17 - Compile Gate Recheck
Problem: Static code changes still need compile proof, but project law forbids starting a build while CPU exceeds 50% or another `dotnet`/`csc` process is active.
Solution: Rechecked the gate before build. CPU returned 100 and `dotnet` PID `29148` was active, so compile was deliberately deferred. Continued with static compile-risk sweeps over KCC DTO layout, FixedTick allocation boundaries, SHINOBU hot-pattern scans, and stale symbol scans.
Rejected Alternatives: Launching `dotnet build` anyway is rejected because it violates the explicit batch rule and could collide with another agent's build. Killing another agent's `dotnet` process is rejected because this agent does not own it.
Scalability potential: No runtime behavior change. Verification resumes once the build gate is open; runtime scalability remains controlled by `GlobalQualityWeight`.
Hardware Impact: No extra compile load was added to an already saturated machine.

## Decision 18 - Unity Project File Coverage
Problem: The generated `.csproj` files are stale relative to new SHINOBU_249 scripts; a later `dotnet build` may not compile the new KCC/editor files.
Solution: Treat Unity import/compile or project-file regeneration as the required compile proof for new files. `dotnet build Hecton8.slnx` can still catch tracked generated-project errors, but it is not sufficient evidence for unlisted scripts.
Rejected Alternatives: Claiming a stale project build as full proof is rejected. Manually editing generated `.csproj` files is rejected because Unity owns them and would overwrite the change.
Scalability potential: No runtime behavior impact.
Hardware Impact: Verification-only constraint; no frame cost.

## Decision 19 - Persistent Sleeping Row Bypass
Problem: A row that was already flagged sleeping skipped force output but still flowed through material lookup, density/flow/SDF math, and reset deep-sleep counters on later evaluation passes.
Solution: Add a deterministic early return for `wasSleeping & inputFinite & hasBody` immediately after resolving quality-derived deep-sleep cadence. The row zeroes velocity/angular scalar, increments `DeepSleepTickCount`, preserves `FlagSleeping`, sets deep/static-promotion flags when due, clears the corresponding force-candidate slot, writes debug telemetry, and exits before material, flow, drag, and SDF work. Empty flow sample buffers now fall back to deterministic analytic flow instead of aborting the evaluator before candidate clearing.
Rejected Alternatives: Keeping the dormant row in the full evaluator is rejected because 50,000 settled objects would still pay meaningful ALU/cache cost. Moving sleeping rows into a compact list is rejected because it breaks fixed row identity and reintroduces active/inactive churn. Treating missing flow samples as a hard frame abort is rejected because the evaluator already has an analytic Dear Lie flow field.
Scalability potential: Low quality reaches sleeping state faster and then exits the heavy path; middle/high/ultra preserve stricter initial sleep thresholds but still bypass heavy math once the fact is owned by `FlagSleeping`.
Hardware Impact: On i3/MX350/Quest-class silicon, this removes per-row material lookup, flow sample, density/drag, SDF index math, and force packet construction for settled debris. Exact microseconds still require Unity profiler proof.

## Decision 20 - Scanner Validator API Fix
Problem: The editor scanner called `HydrodynamicKccLayoutValidator.Validate()`, but the actual KCC validator exposes `ValidateRuntimeLayout(out HydrodynamicKccLayoutReport report)`. This is a direct CS0117 compile blocker once Unity imports the new scanner script.
Solution: Patch the scanner and its import-time guard to call `ValidateRuntimeLayout(out _)`. The layout proof still checks `KinematicStateDTO` size 64 and `Flags` offset 52 locally.
Rejected Alternatives: Adding a new `Validate()` wrapper to the KCC runtime is rejected because it widens the KCC public API to hide a scanner bug. Removing KCC layout validation from the scanner is rejected because Task 04 requires an editor-time assertion.
Scalability potential: No runtime behavior change. The proof surface remains editor-only while low/middle/high/ultra runtime behavior stays driven by `GlobalQualityWeight`.
Hardware Impact: Removes an editor compile blocker with zero player-build frame cost.

## Decision 21 - Scanner Assembly Placement
Problem: The scanner imports both Buoyancy and KCC runtime symbols from an editor folder under Physics. A future domain asmdef split could make that direct editor import invalid.
Solution: Keep the scanner in its current editor-only location for this pass because the repo has no Buoyancy/KCC runtime asmdef split today, and moving it under the central `Scripts/Editor` asmdef could lose access to default Assembly-CSharp runtime types. Record the future boundary risk instead of introducing a speculative assembly move.
Rejected Alternatives: Creating or editing Physics asmdefs is rejected because it changes the compile graph for other agents. Moving the script into `Hecton8.Editor.asmdef` is rejected without verifying that assembly's references cover current default runtime scripts.
Scalability potential: No runtime behavior change; editor proof remains cold.
Hardware Impact: No player-build frame cost.

## Decision 22 - Script Meta Import Stability
Problem: The three new SHINOBU_249 script meta files had only `fileFormatVersion` and `guid`, unlike existing Unity script metas that carry a `MonoImporter` block.
Solution: Add standard `MonoImporter` sections to the new KCC sleep job, scanner, and X-Ray window metas without changing GUIDs.
Rejected Alternatives: Waiting for Unity to rewrite metas is rejected because generated churn can hide real import failures and creates avoidable cross-agent diffs.
Scalability potential: No runtime behavior change.
Hardware Impact: Editor/import stability only; 0 frame cost.

## Decision 23 - Flow Fallback Precondition Closure
Problem: `EvaluateBuoyancyJob` had an analytic flow fallback but still returned at job entry when `FlowSamples` was not created, so missing authored current data could skip force-candidate clearing and sleep evaluation.
Solution: Remove the `!FlowSamples.IsCreated` entry precondition. The evaluator now derives `flowSampleCount = 0` for absent samples and routes through `ResolveFlowVelocity`'s deterministic triangle-wave analytic fallback.
Rejected Alternatives: Creating an empty native flow buffer each frame is rejected because it adds ownership and allocation pressure. Treating authored flow data as mandatory is rejected because the Dear Lie fallback is already sufficient for deterministic low-cost wake/sleep behavior.
Scalability potential: Low devices can run without authored flow samples and still sleep settled debris. Middle/high/ultra can consume authored samples when present without changing authority or DTO layout.
Hardware Impact: Prevents stale force packet slots and missed sleep transitions under absent flow data; exact microseconds still require profiler proof.

## Decision 24 - Scanner Duplicate Pattern Removal
Problem: The scanner reported both `.sleepThreshold` and `sleepThreshold`, double-counting the same legacy PhysX threshold token.
Solution: Keep the broader `sleepThreshold` pattern and remove the narrower duplicate.
Rejected Alternatives: Keeping duplicate counts is rejected because report noise weakens audit precision.
Scalability potential: Editor/report path only.
Hardware Impact: 0 runtime cost.

## Decision 25 - Shared Report True Upsert
Problem: `TryUpsertSharedReportAddendum` inserted `shinobu249BuoyancySleep` once and returned on later runs, so the shared report could retain stale SHINOBU_249 values after scanner logic changed.
Solution: Replace the existing SHINOBU_249 JSON object by finding the property line and matching the object braces while respecting quoted strings. Other agents' JSON properties remain untouched.
Rejected Alternatives: Overwriting the whole shared report is rejected as cross-agent evidence loss. Leaving insert-only behavior is rejected because stale proof is worse than missing proof.
Scalability potential: Editor/report path only.
Hardware Impact: 0 runtime cost.

## Decision 26 - Buoyancy Editor Folder Meta Normalization
Problem: The new SHINOBU_249 `Assets/_Project/Scripts/Physics/Buoyancy/Editor.meta` folder meta contained only `fileFormatVersion` and `guid`, unlike normalized Unity folder metas in neighboring editor folders. Leaving it bare risks first-import metadata churn that can obscure real Unity import failures.
Solution: Preserve the existing GUID and add `folderAsset: yes` plus the standard `DefaultImporter` block.
Rejected Alternatives: Deleting and regenerating the meta is rejected because GUID churn can break references. Waiting for Unity to rewrite it is rejected because this batch already tracks import hygiene as a static gate.
Scalability potential: No runtime behavior change; this is editor/import hygiene only.
Hardware Impact: 0 runtime cost.

## Decision 27 - Default Material Settling CSV
Problem: Task 17 required `material_settling_profiles.csv`, and the status log claimed a default profile file existed, but `Assets/_Project/Data/Physics/material_settling_profiles.csv` was absent. The cold parser alone is not a complete human-tuning bridge.
Solution: Add a small default CSV with material hashes derived at cold boot by the existing FNV-1a parser, covering heavy metal, tools, ore, glass, rubber, plastic, and foam salvage. The file is authoring data only; Burst jobs consume the Vault table.
Rejected Alternatives: Hardcoding defaults into the job is rejected because it forces C# recompiles for designer tuning. Creating ScriptableObject profiles is rejected because Task 17 explicitly requires a `ReadOnlySpan<byte>` CSV bridge.
Scalability potential: Low quality can sleep light/foam debris aggressively via profile thresholds while heavy metal uses stricter settling; high/ultra can tune profiles without layout or code changes.
Hardware Impact: Cold boot file read only; 0 runtime frame cost.

## Decision 28 - Payload Ledger Boundary
Problem: New SHINOBU_249 BufferIDs, material profile rows, and fault telemetry were present in source/docs but the central binary payload ledger lacked a SHINOBU_249 boundary entry. That creates audit drift: save/rollback/Data Monolith reviewers cannot tell whether the CSV is runtime payload, authoring input, or save identity.
Solution: Add a SHINOBU_249 payload boundary addendum to `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, and patch the route card/architecture note with the exact CSV path and Data Monolith non-claim. The addendum lists `71643..71647`, DTO anchors, endian route, save boundary, authority owner, and dump route.
Rejected Alternatives: Leaving the ledger silent is rejected because `BufferID` drift is a compile-wall and save-format risk. Claiming Data Monolith readiness is rejected because `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is not proven present by this CSV.
Scalability potential: No runtime behavior change. Low/middle/high/ultra tuning remains profile-driven and continuous through Vault rows.
Hardware Impact: Documentation and cold source route only; 0 runtime frame cost.

## Decision 29 - Compile-Gated Static Source Sweep
Problem: Unity import/compile proof is still required, but the machine stayed above the explicit build threshold and multiple `dotnet` workers were active. Stopping at docs would leave source regressions unsearched while compile is blocked.
Solution: Re-run focused static gates over SHINOBU-owned Buoyancy/KCC sleep surfaces: forbidden PhysX/list/managed iteration patterns, stale SDF predicate, stale KCC validator calls, stale fixed-tick descriptor recovery symbols, Burst directive mode, and evaluator flow fallback. Record the CPU/dotnet gate as policy-blocked rather than pretending to have compiler proof.
Rejected Alternatives: Launching Unity or dotnet under CPU 99% with active `dotnet` workers is rejected by project law. Broad-repo findings from unrelated KCC editor work are not treated as SHINOBU_249 defects unless they touch this route.
Scalability potential: No runtime behavior change. The static sweep protects the intended low/middle/high/ultra sleep path until import/profiler proof can run.
Hardware Impact: 0 runtime frame cost; avoids adding compile contention to a saturated workstation.

## Decision 30 - CSV Meta Importer Normalization
Problem: The new `material_settling_profiles.csv.meta` had only `fileFormatVersion` and `guid`, while neighboring CSV assets use `TextScriptImporter` or `DefaultImporter`. Leaving a bare meta risks Unity rewriting it on first import and hiding real import failures.
Solution: Preserve the GUID and add a standard `TextScriptImporter` block.
Rejected Alternatives: Deleting the meta is rejected because GUID churn is avoidable. Waiting for Unity to synthesize importer metadata is rejected because this batch treats import hygiene as a static gate.
Scalability potential: No runtime behavior change.
Hardware Impact: Editor/import path only; 0 runtime frame cost.

## Decision 31 - Dedicated Self-Audit Artifact
Problem: The earlier `<SELF_AUDIT>` blocks in `LOG_SHINOBU_249.md` predated the CSV source, payload ledger, meta normalization, and static source gates. Keeping only stale log-embedded audit text weakens Task 20 evidence.
Solution: Add `Docs/Reports/SHINOBU_249_SELF_AUDIT.xml` with explicit task reconciliation, DTO layout math, Vault BufferIDs, dependency graph, compile guard, Dear Lie route, and cold CSV boundary. Mark Unity/profiler proof as pending.
Rejected Alternatives: Chat-only self-audit is rejected because the CTO reads files. Claiming runtime/profiler proof is rejected because the CPU/build gate blocked those runs.
Scalability potential: No runtime behavior change; the file documents low/middle/high/ultra behavior without binary tiers.
Hardware Impact: Documentation only; 0 runtime frame cost.

## Decision 32 - Editor Namespace Import Hygiene
Problem: `PhysicsSleepStateXRayWindow` is declared in `Hecton8.Physics.Editor` and references runtime DTOs and runtime methods by unqualified names. Unity/C# usually resolves parent namespace symbols, but relying on that during a generated-project import is unnecessary compile risk.
Solution: Add an explicit `using Hecton8.Physics;` import to the editor window.
Rejected Alternatives: Leaving it implicit is rejected because the fix is isolated and editor-only. Moving the editor window into the runtime namespace is rejected because editor-only code should remain segregated.
Scalability potential: No runtime behavior change.
Hardware Impact: Editor compile hygiene only; 0 runtime frame cost.

## Decision 33 - Shared Physics Report Addendum Repair
Problem: `PHYSICS_OPTIMIZATION_REPORT_SHINOBU_249.json` existed and parsed, but the shared `PHYSICS_OPTIMIZATION_REPORT.json` did not contain `shinobu249BuoyancySleep`. Task 19 requires a shared proof route, and sidecar-only evidence is weaker under multi-agent report churn.
Solution: Add a non-destructive top-level `shinobu249BuoyancySleep` object matching the sidecar summary, without touching other agents' report fields.
Rejected Alternatives: Re-running Unity editor scanner is rejected while Unity import is compile-gated. Overwriting the shared report is rejected because it would destroy other agents' fields.
Scalability potential: No runtime behavior change.
Hardware Impact: Report path only; 0 runtime frame cost.

## Decision 34 - Unity Import Blocked By External Compile Wall
Problem: Unity batchmode import/compile was finally allowed by the CPU gate and reached Bee/Csc, but the project failed in unrelated assemblies before producing a clean import proof. The batch process then stopped advancing after `06:00:17` with `bee_backend.exe` still owned by the launched Unity process.
Solution: Parse `Library/Bee/tundra.log.json` for `error CS` and SHINOBU symbols, confirm SHINOBU sources are present in `Hecton8.Core.rsp`, classify the blockers as external to `SHINOBU_249`, record the exact files in status/report artifacts, and stop only the owned Unity/Bee process to release the compiler lock.
Rejected Alternatives: Editing World/Core/Habitat/Rendering/Geology/Voxel files is rejected because those domains are outside SHINOBU_249 ownership and would be cross-agent sabotage. Calling the SHINOBU code compile-clean is rejected because `Hecton8.Core` did not reach a `Csc` result after upstream failures. Leaving the hung Unity/Bee process alive is rejected because it would block other agents.
Scalability potential: No runtime behavior change. SHINOBU sleep scalability remains static-verified; profiler/runtime proof stays blocked until upstream compile errors are resolved.
Hardware Impact: The attempted proof consumed compile time only. No player-build frame cost; stopping the owned process removed a stale compile lock.

## Decision 35 - Buoyancy Editor Assembly Isolation
Problem: The stale Unity response file listed `PhysicsSleepStateXRayWindow.cs` and `RigidbodySleepScanner.cs` inside `Hecton8.Core.rsp`. That means SHINOBU_249 editor-only scanner/UI code would compile with the runtime assembly until Unity sees an editor asmdef, violating compile-wall isolation and risking player/runtime assembly pollution.
Solution: Add `Assets/_Project/Scripts/Physics/Buoyancy/Editor/Hecton8.Physics.Buoyancy.Editor.asmdef` with `includePlatforms: Editor`, `autoReferenced: false`, direct references to `Hecton8.Core`, `Hecton8.Core.Memory`, `Unity.Mathematics`, and `Unity.Collections`, plus a stable asmdef meta. Loop 22 later adds Roslyn precompiled references inside the same Editor-only assembly because Task 19 was upgraded to AST parsing.
Rejected Alternatives: Leaving the editor scripts under the root runtime asmdef is rejected because the scanner imports `UnityEditor` and must not be part of runtime/player compile. Moving the scanner into a shared central editor assembly is rejected because it would widen ownership and may not reference the current default runtime symbols. Adding Roslyn to runtime/player assemblies is rejected; Roslyn belongs only to the cold editor scanner lane.
Scalability potential: No runtime behavior change. Low/middle/high/ultra sleep math remains controlled by `GlobalQualityWeight`; the change only removes editor proof surfaces from the runtime compile lane.
Hardware Impact: 0 frame cost. The expected impact is shorter/cleaner runtime assembly compile scope and no editor-only symbols in the player path after Unity reimport.

## Decision 36 - Force Packet Stale-Slot Invariant
Problem: `PhysicsApplySystem.TryPrepareBuoyancyForcePackets` resets only `Counters[0].ForcePackets` and the overflow flag. If the evaluator failed to overwrite invalid candidate lanes, stale force packets from a prior frame could be compacted and drained into Rigidbody force application after an object had entered sleep.
Solution: Prove and record the invariant instead of adding a full-buffer clear. `EvaluateBuoyancyJob` writes `default` to `ForcePackets[workIndex]` for out-of-range rows and for rows already marked sleeping; active rows write either a valid queued packet or a zero packet. `CompactBuoyancyForcePacketsJob` scans only `min(scheduledEvaluationCount, packetCapacity)` and stores the count of valid packets in `Counters[0].ForcePackets`. `DrainBuoyancyForcePackets` reads only that count.
Rejected Alternatives: Clearing all 8,192 force-packet slots every fixed tick is rejected because it burns memory bandwidth on a state that the compact count already excludes. Keeping the invariant undocumented is rejected because sleep correctness depends on stale packets never escaping the candidate range.
Scalability potential: Low quality schedules fewer candidate rows through stride and sleeps earlier; the invariant still holds because `CandidateCount` is the scheduled evaluation count, not the whole capacity. Middle/high/ultra can schedule more rows without changing the queue contract.
Hardware Impact: Avoids an 8,192 * 128-byte full-buffer clear per fixed tick on weak CPUs while retaining deterministic stale-force exclusion. Exact microseconds remain profiler-pending.

## Decision 37 - Task 19 AST Scanner Upgrade Without False Proof
Problem: Task 19 requires parsing the project AST, but the current SHINOBU scanner truthfully reported only a tokenized text scan. Leaving it as-is would satisfy the forbidden-pattern artifact only partially; relabeling the old report as AST proof would be a false audit.
Solution: Upgrade `RigidbodySleepScanner` to use Roslyn `CSharpSyntaxTree.ParseText` with `LanguageVersion.Preview`, scan `InvocationExpressionSyntax` for `Sleep`/`IsSleeping`, scan member and identifier syntax for `sleepThreshold`, and keep token fallback only when a file cannot be parsed. The editor asmdef now carries Roslyn precompiled references under an Editor-only assembly.
Rejected Alternatives: Pulling Roslyn into runtime assemblies is rejected because the scanner is cold editor proof tooling. Claiming the existing JSON as AST proof is rejected because Unity has not re-run the scanner after the source change. Running Unity import now is rejected because CPU is at 100% and project policy forbids build/import under that gate.
Scalability potential: No runtime quality behavior changes. The scanner protects the architecture by proving PhysX sleep calls do not creep back into the Physics folder; low/middle/high/ultra runtime still scales only through `GlobalQualityWeight`.
Hardware Impact: 0 player-frame cost. Editor scan cost is cold. Runtime benefit is indirect: it prevents reintroduction of managed PhysX sleep APIs that would split authority and produce debugging cost.

## Decision 38 - Wake Route Audit and Burst Directive Closure
Problem: Task 08 names force packet wake propagation, while the active SHINOBU route consumes `WakeRequestSignal`. Separately, `ProcessBuoyancyWakeTriggersJob` lacked an explicit Burst directive even though it is a PRE_SIMULATION mathematical job.
Solution: Keep Buoyancy on the first-party `SignalBus<WakeRequestSignal>` route because Cavitation already publishes `WakeRequestSignal` from shockwaves, and the signal contract lives in Core/Contracts. Add the deterministic Burst directive to `ProcessBuoyancyWakeTriggersJob`, statically scan SHINOBU job structs for missing directives, and record the Cavitation-to-WakeRequest bridge boundary in the route card.
Rejected Alternatives: Importing Cavitation `ForcePacketDTO` directly into Buoyancy is rejected as sibling-domain coupling and duplicate wake authority. Adding a second targeted wake queue is rejected because no route card assigns Buoyancy ownership of Cavitation transport packets. Leaving the wake prepass without Burst is rejected by the project Burst directive law.
Scalability potential: Low quality no-signal frames still skip wake work; signaled frames scan the bounded wake signal snapshot. Middle/high/ultra can increase wake signal counts through the existing SignalBus lane without changing DTO layout or authority ownership.
Hardware Impact: The Burst directive lets the wake prepass remain vectorizable/Burst-scheduled when Unity import succeeds. Exact runtime microseconds remain profiler-pending.

## Decision 39 - Hot Vault Mutation Lock Discipline
Problem: `FixedTick` wrote the Vault-backed tuning row before `TryLockJobBuffers`, and `FinishPendingSolverCompletion` unlocked before writing completed microsecond telemetry. That violates the owner-phase memory discipline even though the code path is deterministic and allocation-free.
Solution: Move `TryLockJobBuffers(vault)` before the hot tuning-row mutation, add an explicit unlock on the zero-active early return, and move `WriteCompletedComputeMicros` plus the fault-flag read before `UnlockJobBuffers`. The slow fault dump remains after unlock so file IO does not hold native buffer locks.
Rejected Alternatives: Leaving the order as-is is rejected because it creates a hidden write outside the Vault ownership window. Reacquiring locks only for telemetry is rejected because the existing job-buffer lock already covers the completed state until finalization. Holding locks during `DumpBlackBoxOnce` is rejected because fault file IO is slow and does not need to mutate Vault rows.
Scalability potential: Low quality still exits early on sleep/stride cadence, and high/ultra still evaluates stricter physics; the lock-order patch changes neither DTO layout nor quality math. It prevents race-prone ownership drift across all tiers.
Hardware Impact: 0 intended frame-cost increase. It may save debug time by eliminating a class of non-deterministic Vault writes; exact microseconds remain profiler-pending.
