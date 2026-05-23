# Rationale_SHINOBU_310

Date: 2026-05-22
Agent: SHINOBU_310
Status: POLISH PASS STATIC VERIFIED / BUILD BLOCKED BY ACTIVE UNITY DOTNET

## Decision 000 - Preflight Scope

Problem: Spawn validation currently has unknown ownership. Creating an isolated manager before archaeology risks duplicate authority and compile walls.
Solution: Follow the XML block: grep first, integrate via partial class only if a spawn director already exists, otherwise create isolated contracts/jobs in the nearest existing domain assembly.
Rejected Alternatives: A standalone manager before repository scan; direct Physics replacement before knowing call sites.
Scalability potential: Low tier gets single-tap SDF validation; middle/high/ultra can pay for trilinear and Dear Lie gradient correction without changing gameplay truth.
Hardware Impact: No runtime gain claimed yet. Expected direction is avoiding PhysX broadphase sync on i3/MX350 during mass spawn validation.

## Decision 001 - Selected Mandates

Problem: The task crosses spawn AI, voxel SDF, native memory, AUP precision, jobs, and editor validation.
Solution: Use 8 mandates: Zero GC, ARM64 struct layout, AUP determinism, Voxel SDF pipeline, Native memory/job protocol, execution phases, GlobalRegistry DI, and crash telemetry.
Rejected Alternatives: Reading unrelated rendering/audio mandates.
Scalability potential: Same DTO and SDF sampler support Low, Middle, High, Ultra through continuous `GlobalQualityWeight` and not platform-specific switches.
Hardware Impact: Mandates bias implementation toward contiguous 32-byte requests and data-local Burst loops, which are the only acceptable path for MX350/i3 cache behavior.

## Decision 002 - Owner Integration

Problem: No `HectonSpawnDirectorRuntime` exists. The nearest active spawn owner is `World/ResourceDistributionDirector.cs`, which performs resource spawn placement and old voxel solid rejection.
Solution: Convert `ResourceDistributionDirector` to a partial class and inject SHINOBU_310 SDF validation in `World/SpawnZoneSdfValidation.cs`. The existing call site now routes runtime placement rejection through Vault SDF sampling instead of managed voxel `Transform.InverseTransformPoint` and density calls.
Rejected Alternatives: A standalone validator manager was rejected because it would duplicate spawn authority. Editing unrelated fauna directors was rejected because the discovered concrete call site is world resource injection.
Scalability potential: Low uses one nearest SDF byte read; middle/high/ultra enable blended trilinear and Dear Lie gradient correction without changing spawn truth ownership.
Hardware Impact: i3/MX350 avoids Transform + managed volume traversal for resource spawn rejection. Expected gain is removal of scene-object geometry traversal; no measured microseconds until Unity profiler run.

## Decision 003 - Vault Buffer Route

Problem: The XML demands flat validation requests and no heap validation queue, but the existing director still owns managed `Queue<SpawnRequest>` for complete object-pool injection state.
Solution: Scope the purge to validation requests: `SpawnValidationRequestDTO` rows are stored in `ShinobuSpawnSdfValidationRequests` with `SpawnValidationRingStateDTO`; the legacy queue remains only as post-validation spawn payload staging.
Rejected Alternatives: Replacing the full `SpawnRequest` queue in this pass was rejected because it also carries prefab/template/rotation/tombstone payload and would cross into object pool and persistence ownership. Leaving validation data only in locals was rejected because the editor gizmo and telemetry need a Vault proof artifact.
Scalability potential: Low/middle devices retain compact 32-byte validation snapshots; high/ultra can batch thousands through `ScheduleEvaluate` with sequential cache reads.
Hardware Impact: Request validation state becomes contiguous and uninitialized on allocation, avoiding a 32 KB zero-fill at 1024 capacity. Full spawn queue replacement remains a separate integration job.

## Decision 004 - SDF Convention Bridge

Problem: HECTON voxel payload encodes density with positive solid and negative open, while the SHINOBU_310 prompt describes clearance distance where values below radius fail.
Solution: `SpawnSdfGridHeaderDTO.Flags` carries `SolidPositiveDensity`; the sampler converts existing voxel density to open-water clearance via `clearance = -density`. Mock SDF omits that flag and stores positive-open clearance directly.
Rejected Alternatives: Reinterpreting existing byte SDF as positive-open without a flag was rejected because it would invert cave collision semantics. Maintaining two separate jobs was rejected because it doubles audit and compile surface.
Scalability potential: Same byte sampler supports low nearest and high trilinear modes across real and mock SDFs.
Hardware Impact: The convention branch is uniform per grid header and costs less than one managed voxel call; on low-end silicon the 1-tap path remains the dominant cost.

## Decision 005 - Timing Truth

Problem: Task 14 asks for exact Burst execution time, but this patch cannot truthfully measure Burst job duration from inside the job or while the owner has no profiler timing hook.
Solution: `SpawnValidationTelemetryReduceJob` accepts `QueryMicroseconds` supplied by the scheduling owner; the single synchronous bridge records `-1` plus `TimingUnavailable` instead of faking a number. Dump support exists in `SpawnZoneSdfForensics.WriteTelemetryDump`.
Rejected Alternatives: Estimating "exact microseconds" from request count was rejected as a fake report. Blocking the main thread to time `.Complete()` was rejected because it violates Task 10.
Scalability potential: Low/middle/high/ultra all get consistent telemetry layout; exact timings can be inserted by the PRE/POST simulation owner when it has real profiler timestamps.
Hardware Impact: No claimed timing gain. The honest state is "measurement hook present, exact runtime not captured in this pass."

## Decision 006 - Compile Guard

Problem: The project rule forbids dotnet/Unity rebuild while CPU is busy or another dotnet/csc process is running.
Solution: Static scans and diff inspection were run; full build was skipped because CPU load was 96 percent and `dotnet` PID 14108 was active.
Rejected Alternatives: Launching another build to force proof was rejected by explicit hardware-protection mandate.
Scalability potential: Avoiding compile spam preserves iteration bandwidth for all 20+ concurrent agents.
Hardware Impact: Prevents additional CPU contention on the developer machine. Compile status remains unverified until system load drops below the guard threshold.

## Decision 007 - SDF Missing Behavior

Problem: If the Vault is active but the SDF payload is missing, allowing spawn placement to proceed can place resources inside terrain with no authoritative geometry proof.
Solution: Fail closed when `VoxelSdfPayloadDescriptor` or `VoxelSdfTexture3D` is unavailable after the Vault exists. The request is still written to the SHINOBU_310 request ring and telemetry with `SdfUnavailable`.
Rejected Alternatives: Falling back to managed voxel object sampling was rejected because it reintroduces scene-object geometry traversal. Silently accepting missing SDF was rejected because it violates spawn safety.
Scalability potential: Low/middle/high/ultra behavior is identical for missing data: block unsafe injection and emit forensic state.
Hardware Impact: Blocking a spawn is cheaper than retrying a trapped spawned prefab and avoids object-pool churn on low-end CPUs.

## Decision 008 - Ultra Polish Defect Pass

Problem: Secondary audit found three concrete risks: editor diagnostics still polled `GlobalRegistry.DataVault`, CSV clearance profiles were parsed but not hydrated through Vault lookup rows, and the dump writer had no direct NaN/bounds call path.
Solution: Convert editor facade/gizmo to `GlobalDataVault.TryGetLatestCreated()` diagnostic route, allocate `ShinobuSpawnClearanceProfiles` and `ShinobuSpawnClearanceCsvScratch` through Vault, ingest `entity_clearance_profiles.csv` into unmanaged profile rows during cold setup, and call `WriteTelemetryDump` when validation telemetry records NaN/bounds flags.
Rejected Alternatives: Adding a new manager was rejected because `ResourceDistributionDirector` already owns resource spawn authority. Adding Roslyn references to the inherited editor assembly was rejected because it would mutate compile-wall metadata for a scanner whose current string parser is editor-only proof. Always dumping on SDF-missing was rejected because missing terrain payload is a recoverable bootstrap/data readiness state, not a mathematical fatality.
Scalability potential: Low/middle/high/ultra all share the same profile DTO and SDF sampler; CSV changes clearance radius only, not authority route or DTO layout.
Hardware Impact: On i3/MX350, profile lookup is a small cold/slow-tick linear scan over 64 rows and avoids overspawning trapped resource nodes. On high-end, the same table can carry larger clearance radii without a code recompile.

## Decision 009 - Quality Curve And ALU Gate

Problem: A subagent flagged the trilinear path as a hard branch. Fully branchless always-8-tap sampling would satisfy wording but would spend mobile ALU exactly where the prompt requires a 1-tap survival path.
Solution: Keep the low-quality ALU gate but replace the linear blend with `math.smoothstep(0.4, 1.0, GlobalQualityWeight)`. Below the threshold the job reads one byte. Above it, interpolation weight grows continuously toward 8-tap trilinear.
Rejected Alternatives: Always executing trilinear and lerping with zero was rejected as a false optimization that wastes seven SDF reads on thermally constrained hardware. Binary hardware-tier switches were rejected; the branch is driven by continuous quality scalar, not platform class.
Scalability potential: Minimum quality remains 1-tap; middle quality blends boundary precision smoothly; high/ultra reaches trilinear with Dear Lie gradient correction for visual overkill placement stability.
Hardware Impact: MX350/i3 avoids redundant SDF reads under pressure; desktop hardware uses the saved route to improve clearance precision and fewer visible spawn rejects.

## Decision 010 - Compile Guard Recheck

Problem: After polish, build verification was still requested implicitly by task 20, but project law forbids rebuild under CPU load or active dotnet/csc.
Solution: Re-sampled machine state. CPU was 99.6 percent and `dotnet` PID 5468 was active, so no `dotnet build` or Unity rebuild was launched.
Rejected Alternatives: Starting a build for proof was rejected by explicit hardware protection mandate.
Scalability potential: Prevents noisy compile contention across concurrent agents.
Hardware Impact: Avoids further saturation of the developer machine. Compile proof remains pending.

## Decision 011 - Final Guard Static Pass

Problem: Task 20 still needs compile proof, but the latest machine state has an active compiler process even though aggregate CPU dropped below 50 percent.
Solution: Re-ran the scoped runtime forbidden-token scan and diff whitespace check, then refused rebuild because `dotnet` PID 5468 is still active. Static scan found no exact `Physics.CheckSphere`, `Physics.OverlapCapsule`, or `NavMesh.SamplePosition` in AI/World/Fauna runtime scope.
Rejected Alternatives: Launching another build while `dotnet` is active was rejected because it violates the hardware-protection and compile-wall rules. Expanding into unrelated runtime domains was rejected because SHINOBU_310 owns spawn SDF validation, not global smoke-test cleanup.
Scalability potential: The static proof protects the hot spawn validation path; build proof remains a queued verification item after existing compiler work exits.
Hardware Impact: Avoids extra IO/CPU contention while preserving a clean audit trail. Latest guard: CPU 47.3 percent, active `dotnet` PID 5468.

## Decision 012 - BufferID Collision Repair

Problem: Static ledger and code search proved SHINOBU_310's first Vault lane range `71960..71968` collided with SHINOBU_302 cognition lanes in `UtilityAICognitionVault`. That violates one fact -> one owner -> one route and can corrupt unrelated cognition buffers.
Solution: Move SHINOBU_310 central enum lanes to `72600..72608` and update ledger/log evidence. A duplicate scan over `H8Memory.cs` reports no duplicate for the new SHINOBU_310 range.
Rejected Alternatives: Keeping the collision because C# enum values can technically duplicate was rejected; DataVault identity is numeric, not semantic. Moving SHINOBU_302 was rejected because that route already has runtime csc proof and existing dependent agents.
Scalability potential: All devices now resolve spawn validation memory independently from AI cognition. DTO layout and hot-path math are unchanged.
Hardware Impact: Prevents silent buffer aliasing that would poison both spawn validation and cognition under load. No microsecond gain is claimed; this is correctness containment.

## Decision 013 - Fail-Closed Vault Boundary

Problem: Read-only subagent audit found that `TryValidateSpawnRuntimePositionViaSdf` returned true when `_spawnSdfDataVault` was null, allowing gameplay spawn placement to bypass geometry validation entirely.
Solution: Change the null-Vault route to fail closed during Play Mode and only permit the bypass outside gameplay. Missing SDF payload after Vault boot already fails closed with `SdfUnavailable` telemetry.
Rejected Alternatives: Fail-open compatibility was rejected because the task is spawn safety, not spawn availability. Falling back to PhysX or managed voxel sampling was rejected because it reintroduces the forbidden broadphase/scene-object route.
Scalability potential: Low/middle/high/ultra devices share one safe authority boundary; quality can change sample richness only after Vault/SDF truth exists.
Hardware Impact: On i3/MX350, blocked unsafe spawn is cheaper than instantiating a trapped pooled object and correcting it later.

## Decision 014 - Shared Report Repair

Problem: `Docs/Reports/AI_OPTIMIZATION_REPORT.json` had been overwritten by another agent's current report and no longer carried the SHINOBU_310 proof section.
Solution: Reinsert `shinobu310SpawnSdfValidator` with current runtime scan, `72600..72608` BufferIDs, fail-closed proof, collision audit, timing caveat, and build guard state. Also update the stable SHINOBU_310 JSON with the same numeric lane facts.
Rejected Alternatives: Leaving only chat/log proof was rejected because the task explicitly demands report artifacts. Rerunning the editor scanner from Unity was rejected because Unity/dotnet is already active and CPU is saturated.
Scalability potential: Report repair does not affect runtime, but preserves proof that quality scaling changes sampling cost only, not authority identity.
Hardware Impact: No runtime impact. Prevents false integration by stale report data.

## Decision 015 - Latest Build Guard

Problem: After the collision/fail-closed patch, compile verification remained required but the rebuild guard had to be resampled.
Solution: Re-sampled CPU and compiler processes. CPU dropped to 48 percent, but Unity `dotnet` PID 1548 is still active, so build launch remains forbidden. Ran static substitutes: Burst attribute scan, hot DTO property/layout scan, JSON parse, scoped forbidden physics-token scan, duplicate BufferID scan, and `git diff --check`.
Rejected Alternatives: Launching `dotnet build` while Unity dotnet is active was rejected by explicit project law.
Scalability potential: No runtime change. Static checks keep the integration queue honest until build proof is legal.
Hardware Impact: Avoids competing with the active Unity compiler process.
