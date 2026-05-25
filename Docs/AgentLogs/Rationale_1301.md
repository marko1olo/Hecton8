# Rationale_1301

Status: STATIC PASS / BUILD BLOCKED BY EXISTING UI DEPENDENCY

## Phase 0 Preflight

Problem: User requested `C:\hades\current_batch.md`, but that file does not exist. Active batch prompt containing id `1301` is at `C:\hades\Hecton8\Docs\Tasks\CURRENT_BATCH.md`.
Solution: Used CLI regex extraction against the active batch file and verified `<AGENT_PROMPT id="1301"...>` from cover to cover.
Rejected Alternatives: Do not read archived batch files; AGENTS forbids previous-batch contamination unless ordered. Do not infer from neighboring prompt 1300.
Scalability potential: Correct prompt isolation prevents cross-domain code churn. Low/Middle/High/Ultra behavior unaffected because this is process control.
Hardware Impact: Avoids wasted compile/edit cycles; estimated 0 runtime us, prevents human-time regression.

Problem: Prompt path says `Assets/Project`, but source reality under this Unity project is `Assets/_Project`.
Solution: Treat `Assets/_Project/Scripts/AI/Ecology` as the concrete source root and include boid-related script paths found by source scan. Any code edit outside this will require explicit rationale tied to boid/flocking ownership.
Rejected Alternatives: Creating missing `Assets/Project` path or editing unrelated AI folders would be domain sabotage.
Scalability potential: Keeps work bounded; low devices do not pay for speculative architecture.
Hardware Impact: Runtime impact 0 us; reduces risk of unrelated compile churn.

Problem: Phase 0 requires native alias audit before code mutation.
Solution: Load relevant mandates first: native memory/job system, zero-GC, ARM64 layout, boid spatial hash, registry DI, SignalBus, post-mortem telemetry, fake-first, AUP.
Rejected Alternatives: Starting with broad refactor or manual edits without hit list.
Scalability potential: Low: no stale native aliases. Middle: cleaner job scheduling. High/Ultra: richer presentation can read stable snapshots without gameplay truth drift.
Hardware Impact: Audit itself runtime 0 us; projected gain depends on offenders found.

## Phase 0 Native Alias Exorcism

Problem: Static field scan found three non-job `NativeArray<T>` fields in `SpatialHashQuery`: spatial grid entries, bucket ranges, and AUP snapshot. They were public value-type fields, so any future caller could persist raw native aliases across a GlobalDataVault relocation window.
Solution: Replaced the fields with `VaultGenerationHandle<SpatialGridEntryDTO>`, `VaultGenerationHandle<SpatialGridBucketRangeDTO>`, and `VaultGenerationHandle<AmbientEntityAupDTO>`. Query methods now accept `IDataVault`, resolve read-only views with `TryReadOnlyHandle`, use them only inside the call, and fail closed when a handle is missing or stale.
Rejected Alternatives: Keeping the NativeArray fields because the struct has no current caller was rejected; public stale-pointer API is still an architectural leak. Storing `IDataVault` inside the query struct was rejected; the query object should carry descriptors, not a service reference. Passing handles into jobs was rejected by mandate.
Scalability potential: Low devices avoid relocation crash risk with no extra allocation. Middle/High/Ultra tiers keep the same query math and existing continuous quality budgets; this patch does not reduce visual density or alter gameplay truth.
Hardware Impact: Runtime microseconds saved: 0 claimed. Cost added: three `TryReadOnlyHandle` calls per actual query use. Existing in-repo call graph shows no current callers, so current frame cost is 0 us. Safety gain is stale native pointer elimination, not frame-time optimization.

Problem: Offender ownership had to map to existing vault routes without inventing new BufferIDs.
Solution: Mapped `AupSnapshotHandle` to `BufferID.ShinobuAmbientAupSnapshot` 70403, `EntriesHandle` to `BufferID.ShinobuSpatialGridEntries` 70448, and `BucketRangesHandle` to `BufferID.ShinobuSpatialGridBucketRanges` 70450, all under `SystemID.AIEcology`. Existing cold boot in `ShinobuEcosystemBalancer` already claims those buffers.
Rejected Alternatives: Creating new query-owned buffers was rejected because it would split one fact into two owners. Moving ownership to `SpatialHashQuery` was rejected because it is a query facade, not a lifecycle owner.
Scalability potential: Low/Middle/High/Ultra behavior remains continuous through existing `GlobalQualityWeight` spatial-grid cadence and query budget code.
Hardware Impact: Runtime microseconds saved: 0 claimed. Avoided duplicate native allocation and avoided extra memory pressure on low-end i3/MX350-class hardware.

Problem: DTO layout verification was required before touching spatial grid structures.
Solution: Audited affected DTOs and existing validators. `SpatialGridEntryDTO` is explicit 16 bytes, `SpatialGridBucketRangeDTO` explicit 32 bytes, `SpatialGridTelemetryEntry` explicit 64 bytes, and `AmbientEntityAupDTO` is asserted as 64 bytes in `ShinobuEcosystemBalancer`.
Rejected Alternatives: Reordering already explicit DTOs without a measured defect was rejected; it risks binary/GPU contract churn with no proof.
Scalability potential: Stable cache footprints remain: entry 16B, bucket range 32B, telemetry 64B. Weak hardware keeps tight traversal; high tiers can spend saved stability budget on denser visuals through existing quality scalars.
Hardware Impact: Runtime microseconds saved: 0 claimed; no DTO byte width changed.

Problem: Roslyn AST was requested, but the local PowerShell host could not load the bundled Roslyn assemblies.
Solution: Attempted to load `Assets/Plugins/Roslyn` assemblies and fallback Cursor Roslyn assemblies. Both failed on host/runtime dependency mismatch (`System.Memory`/`System.Runtime`). Recorded this honestly and used a brace-aware field scanner plus direct source review and `rg` call graph proof.
Rejected Alternatives: Faking a Roslyn result was rejected. Launching a dotnet build/tool while csc/dotnet were already running and CPU load was above policy threshold was rejected.
Scalability potential: No runtime effect. Process integrity prevents false proof artifacts from driving unsafe code churn.
Hardware Impact: Runtime 0 us. Tooling time lost; no gameplay cost.

Problem: Compile verification was required but environment guard blocked launching another build.
Solution: Checked process and CPU state before build. Found `csc.exe` and `dotnet.exe` active and CPU load 64%, with no root `.sln` present. Did not launch dotnet build. Static scanner returned after=0 persistent native alias offenders.
Rejected Alternatives: Violating the build guard to get a fake compile signal was rejected. Reporting compile success without running it was rejected.
Scalability potential: No runtime effect. Avoids saturating an already compiling workstation.
Hardware Impact: Runtime 0 us; developer-machine contention avoided.

Problem: Build verification remained required after the guard cleared.
Solution: Rechecked guard, found CPU load 30% and no active dotnet/csc process, then launched `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal`. Build failed in `Hecton8.Core.csproj` on 23 `FixedUiEventQueue<>` missing-type errors under UI/Visor files before ecology code was compiled.
Rejected Alternatives: Fixing UI/Visor missing type errors was rejected as out of agent 1301 domain. Claiming compile success was rejected. Reverting ecology patch because an unrelated core UI dependency fails was rejected.
Scalability potential: No runtime behavior change. Keeping domain boundaries prevents a memory-safety agent from destabilizing UI event infrastructure.
Hardware Impact: Runtime 0 us. Build wall time 66.16 seconds; no frame-time metric affected.

## Apex Override Re-Audit

Problem: The previous fail-closed proof missed a real NaN route: `CollectEntitiesInRadius` validated entity AUPs, but not `centerAup`, `radiusMeters`, or `CellSizeMeters` before spatial quantization.
Solution: Added pre-quantization finite checks and result-buffer validation. Invalid input now hashes failure class 5, writes one unmanaged telemetry entry through the existing spatial grid ring, and returns 0 before `QuantizeCell`.
Rejected Alternatives: Letting `math.floor`/long conversion absorb NaN was rejected because that hides corrupted coordinates. Throwing an exception was rejected because gameplay/fault paths must not allocate or stall on managed exception control flow.
Scalability potential: Low tier avoids undefined spatial probes on corrupted data. Middle/High/Ultra keep the same query budgets and visual density because only invalid-input branches changed.
Hardware Impact: Valid hot query cost: three finite checks plus one result-length branch. Estimated under 0.5 us per direct query on i3/MX350-class silicon; no per-entity cost added.

Problem: The failure telemetry writer could write NaN `CellSizeMeters` into `SpatialGridTelemetryEntry` when the corrupted field itself caused the failure.
Solution: Sanitized telemetry cell size to `0.25f` when `CellSizeMeters` is not finite.
Rejected Alternatives: Copying raw NaN into the blackbox was rejected because crash forensics must be binary-stable and grep-free.
Scalability potential: For all quality tiers, corrupt metadata degrades to a bounded forensic row instead of poisoning postmortem analysis.
Hardware Impact: Failure-only branch. Normal frame cost 0 us.

Problem: `TryFindRange(IDataVault, ...)` briefly became a read accessor with telemetry side effects on failed resolution, violating the global rule that `Try*` read accessors must be pure.
Solution: Removed telemetry mutation from standalone `TryFindRange`. `CollectEntitiesInRadius` still records failure telemetry because it is the query execution path, not a pure lookup accessor.
Rejected Alternatives: Keeping a write-lock in `TryFindRange` was rejected because read accessors must not mutate global state. Deleting the public method was rejected as unnecessary API churn.
Scalability potential: Presentation/audio consumers can call the lookup without hidden writer contention. Simulation queries still retain forensic failure rows.
Hardware Impact: Standalone failed lookup is cheaper by two avoided write-lock attempts. Normal successful lookup unchanged.

Problem: Task 15 proof was too weak: the existing spatial grid fault writer only targeted `Dump_SHINOBU_301.bin`, while agent 1301 requires an agent-specific dump artifact.
Solution: Added `ShinobuSpatialGridConstants.Agent1301DumpRelativePath = "Docs/AgentLogs/Dump_1301_AIEcology.bin"` and mirrored the existing editor/fault-only spatial telemetry dump to both the established SHINOBU_301 file and the 1301 file.
Rejected Alternatives: Renaming the existing SHINOBU_301 dump was rejected because that would break the existing spatial-grid owner evidence route. Adding a new hot-path dump writer was rejected because binary I/O belongs to the existing fault gate.
Scalability potential: All runtime tiers keep the same simulation cost. Postmortem tooling gets a stable agent-local artifact without changing gameplay truth.
Hardware Impact: Normal frame cost 0 us. Fault/editor dump writes a second 300-frame ring copy.

Problem: ARM64 layout proof relied partly on report prose. Existing guard asserted spatial DTO sizes and some offsets, but not the full spatial telemetry/tuning/profile/cell field map.
Solution: Extended the existing ecology layout guard with `AssertSize<SpatialGridCell64>(24)` and exact `AssertOffset` checks for all fields in `SpatialGridTelemetryEntry`, `SpatialGridTuningDTO`, `SpatialGridProfileDTO`, and `SpatialGridCell64`.
Rejected Alternatives: A Markdown-only byte map was rejected. Reordering already explicit DTOs was rejected because no measured layout defect existed.
Scalability potential: Low tier gets stable native strides; high/ultra tiers can increase presentation density without ABI drift.
Hardware Impact: Runtime hot cost 0 us; validation cost is cold guard only.

Problem: The first pass had no real Roslyn AST proof.
Solution: Used the already-built `Tools/VaultNativeAliasRoslynAudit` net10 executable. Results: `AI/Ecosystem` files=5, parseFailures=0, forbiddenPersistentCandidates=0; `AI/Ambient` files=1, forbiddenPersistentCandidates=0; `Animation/FaunaProcedural` files=4, forbiddenPersistentCandidates=0. Expanded `Assets/_Project/Scripts/AI` diagnostic found 44 candidates in out-of-scope AI/Cognition, AI/Perception, and AI/Pathfinding helper structs.
Rejected Alternatives: Faking AST proof or relying only on regex was rejected. Editing out-of-scope Cognition/Perception/Pathfinding was rejected as domain breach.
Scalability potential: Confirms 1301 ecology/boid roots are clean without spending implementation time in unrelated AI domains.
Hardware Impact: Runtime 0 us; proof-only tool execution.

Problem: Task 16 and Task 17 require executable stress/fuzzer proof, but the project currently fails before ecology compilation on unrelated UI/Visor `FixedUiEventQueue<>` errors.
Solution: Marked both tasks as `[BLOCKED BY COMPILE WALL]` instead of pretending static scans equal load/fuzzer execution.
Rejected Alternatives: Adding uncompiled stress harness code was rejected because it would increase code surface without executable proof. Fixing UI/Visor was rejected as outside 1301 domain.
Scalability potential: No runtime behavior change. Prevents false confidence in defrag-race safety until a green compile surface exists.
Hardware Impact: Runtime 0 us; verification debt only.

## Apex Override Re-Audit 2

Problem: Release/player fault dump route was structurally weak because `ShinobuSpatialGridForensics` lived under `#if UNITY_EDITOR` while `ShinobuEcosystemBalancer` called it from runtime telemetry code.
Solution: Moved `ShinobuSpatialGridForensics` outside the editor-only preprocessor block. `SpatialGridProfileCsv` remains editor-only. Runtime fault gate now calls `TryWriteTelemetryDump`, which returns `false` on specific IO/path failures instead of forcing the caller to `catch (Exception)`.
Rejected Alternatives: Keeping the class editor-only was rejected because release builds would not have the 1301 dump route. Adding hot-path file IO was rejected; the writer only runs from the existing catastrophic spatial-fault gate.
Scalability potential: Low/Middle/High/Ultra normal frames pay 0 us. Fault analysis survives outside editor.
Hardware Impact: Normal frame 0 us. Catastrophic dump path performs managed FileStream IO; this is not Zero-GC hot path. A fully native file writer would require a cross-domain crash reporter bridge outside 1301 ownership.

Problem: `CollectRange` used `range.StartIndex + Count` before clamping the count to available entries. A corrupted bucket range could overflow signed int arithmetic and produce a wrong end index.
Solution: Clamp `start` into `[0, safeCount]`, compute `available = safeCount - start`, clamp `count` to available, then compute `end = start + count`.
Rejected Alternatives: Trusting range data because it originates from our own job was rejected; defrag/corruption verification assumes metadata can be damaged.
Scalability potential: All quality tiers fail closed on corrupt ranges without expanding probes or changing visual density.
Hardware Impact: Valid range cost: three integer ops before loop. Estimated below 0.1 us per queried range on i3/MX350-class silicon.

Problem: `CollectEntitiesInRadius` sanitized `CellSizeMeters` for `QuantizeCell` but still passed raw `CellSizeMeters` into `ResolvePublicQueryCellRadius`.
Solution: Introduced `safeCellSize` and use it for both quantization and cell-radius resolution.
Rejected Alternatives: Relying on the callee clamp was rejected because one method-local invariant is clearer and prevents future drift.
Scalability potential: Weak devices avoid accidental oversized query shells if cell size is corrupted. High/Ultra behavior unchanged for valid tuning.
Hardware Impact: One local float assignment. Runtime saved: 0 us claimed; corruption risk reduced.

Problem: Diff-added token scan now contains `new FileStream` in `ShinobuSpatialGridForensics` after moving the dump writer into release-visible code.
Solution: Classified it explicitly as a catastrophic dump IO boundary, not Tick/SlowTick/query hot path. Hot-path scan `ShinobuSpatialGridSolver.cs:339-693` has 0 hits for `new`, string formatting, `ToString`, LINQ, foreach, string concat/interpolation, or `catch(Exception)`.
Rejected Alternatives: Reporting diff-added forbidden hits as zero would be false. Removing the dump writer would violate Task 15.
Scalability potential: Normal gameplay remains Zero-GC by static proof. Fault path prioritizes forensic artifact over frame continuity because the system is already corrupted.
Hardware Impact: Normal frame 0 us. Fault-only file allocation/IO cost is not claimed as GC-free.

Problem: The previous scan roots were too narrow for the user's macro-ecology/boid wording.
Solution: Added Roslyn cross-check for `Assets/_Project/Scripts/Ecosystem` and focused boid file checks. `Scripts/Ecosystem`: files=23, parseFailures=0, forbiddenPersistentCandidates=0. `SargassumMicroFaunaBoids.cs`: NativeArray fields are transient job parameters only. `HectonBoidController.cs`: no native collection field declaration, one comment mention. `BoidStructValidator.cs`: no native collection hits.
Rejected Alternatives: Editing broad `World` candidates was rejected; broad `World` diagnostic has 449 forbidden candidates unrelated to the focused boid files and outside 1301 domain.
Scalability potential: Confirms no hidden boid stale-alias route in the scanned files without destabilizing unrelated world systems.
Hardware Impact: Runtime 0 us; audit-only.

## Apex Override Re-Audit 18

Problem: The prepared runtime fault gate was clean, but legacy `WriteTelemetryDump(projectRoot, ...)` and `TryWriteTelemetryDump(projectRoot, ...)` still delegated to a projectRoot queue overload. If reused from corrupted-state code, that overload could call cold worker preparation, path combining, and directory creation.
Solution: Routed both legacy methods through the prepared-worker overload only. Removed the projectRoot queue overload and the single-argument `EnsureDumpWorker(string)` overload. Cold setup now has one explicit route: `EnsureDumpWorker(projectRoot, vault, handle)`.
Rejected Alternatives: Keeping the projectRoot queue overload for compatibility was rejected because compatibility with an unsafe fault entry point is not worth the managed path construction risk. Deleting all legacy methods was rejected because a fail-closed wrapper is safer for existing callsites while compile proof is blocked.
Scalability potential: Low/Middle/High/Ultra normal frames are unchanged. Fault behavior becomes more deterministic because path setup cannot be accidentally pulled into the failure branch.
Hardware Impact: Normal frame 0 us. Fault path removes a possible managed cold-setup branch; no measured frame-time saving claimed.

## Apex Override Re-Audit 17

Problem: The background spatial dump worker had a lifecycle truth gap. If `ShutdownDumpWorker()` requested stop but the worker did not finish within the 500 ms join window, `s_stopRequested` stayed at 1 while `s_dumpWorker.IsAlive` could remain true. The prepared-worker fault route could then report the worker as ready and queue a dump into a thread that was already exiting.
Solution: Added `s_stopRequested == 0` to the cold `EnsureDumpWorker` fast path and to `IsDumpWorkerPrepared`. Added a live-worker guard that fails closed when the existing worker is alive but stop-requested. Added the same stop-request guard to `TryQueueTelemetryDumpPrepared`. Added a typed `OutOfMemoryException` catch around cold worker creation. Replaced `FileOptions.Asynchronous` with `FileOptions.WriteThrough` because the worker uses synchronous `stream.Write` and the binary dump should prefer durable output over a false async marker.
Rejected Alternatives: Treating `worker.IsAlive` as sufficient readiness was rejected because stop-requested worker state is not a valid dump sink. Restarting a live stop-requested worker in place was rejected because it can race teardown and DataVault hot-swap. Leaving `FileOptions.Asynchronous` was rejected because no async write is used.
Scalability potential: Low/Middle/High/Ultra normal frames are unchanged. Fault export is more deterministic during teardown/hot-swap and still bounded to the 19224-byte Vault snapshot. No extra simulation fidelity or CPU loop was added.
Hardware Impact: Normal frame 0 us. Fault queue adds one volatile integer read. Worker file write uses WriteThrough on the background failure path only; no gameplay-frame cost is claimed.

## Apex Override Re-Audit 16

Problem: Apex15 proved the prepared spatial fault gate was clean, but `ShinobuEcosystemBalancer.cs` still contained nine broad `catch (Exception)` sites. Even if most were legacy or editor/cold IO boundaries, leaving them in a changed runtime file meant the paranoid scan could not honestly pass.
Solution: Narrowed each broad catch to the failure classes used by the local boundary: `InvalidOperationException` for job scheduling/GPU lifecycle, `ArgumentException` for GPU/path argument failures, and `IOException`/`UnauthorizedAccessException`/`NotSupportedException` for file/path boundaries. Evidence lines after the patch: `ShinobuEcosystemBalancer.cs:637`, `:1146/:1150`, `:1382-1398`, `:1574-1590`, `:1625-1641`, `:1721`, `:1818/:1822`, `:2591-2603`, `:2662-2678`.
Rejected Alternatives: Leaving broad catches as "pre-existing" was rejected because this file is already in the 1301 diff. Removing all catch branches was rejected because Unity job scheduling, GraphicsBuffer setup/upload, and file IO can fail through managed APIs; fail-closed behavior must remain. Catching `SystemException` or using filters over `Exception` was rejected as static-scan laundering.
Scalability potential: Low/Middle/High/Ultra normal-frame behavior is unchanged. The patch reduces failure masking without changing swarm fidelity, DataVault ownership, DTO layout, or continuous `GlobalQualityWeight`.
Hardware Impact: Normal frame 0 us. Failure paths still publish existing managed telemetry warnings; no new allocation is added to the success path. This is correctness and release hygiene, not a frame-time optimization.

Problem: Apex16 needed verification without another dotnet/build attempt.
Solution: Ran non-dotnet static scans. `rg catch(Exception)` over `ShinobuEcosystemBalancer.cs`, `ShinobuSpatialGridSolver.cs`, and `H8Memory.cs` returned zero hits. Focused token scans returned `HITS=0` for `FaultGate` `ShinobuEcosystemBalancer.cs:1940-1960`, `PreparedQueue` `ShinobuSpatialGridSolver.cs:1531-1618`, `PreparedWorkerCheck` `ShinobuSpatialGridSolver.cs:1761-1774`, and `SpatialHashQueryRuntime` `ShinobuSpatialGridSolver.cs:321-710`. Persistent native field grep returned zero hits for `AI/Ecosystem`, `Scripts/Ecosystem`, and focused boid files.
Rejected Alternatives: Running `dotnet build` again was rejected because the user explicitly ordered rare build/dotnet attempts and the known full build wall remains unrelated UI/Visor `FixedUiEventQueue<>`. Rerunning Roslyn was rejected because Apex16 changed catch clauses only, not native field declarations or job signatures.
Scalability potential: No runtime effect. The evidence tightens release gating without broad code churn.
Hardware Impact: Runtime 0 us; static verification only.

## Apex Override Re-Audit 15

Problem: Apex14 still allowed the spatial fault caller to pass `ResolveProjectRoot()` into `TryQueueTelemetryDump(projectRoot, ...)` from the NaN/overflow fault gate. Even though this branch is fault-only, it could rebuild managed path state or attempt worker preparation at the exact moment the simulation is already corrupted.
Solution: Added a prepared-worker overload: `TryQueueTelemetryDump(IDataVault, in VaultGenerationHandle<byte>, NativeArray<SpatialGridTelemetryEntry>, int)`. The spatial fault gate now calls this overload at `ShinobuEcosystemBalancer.cs:1864-1868`. The overload checks `IsDumpWorkerPrepared()` at `ShinobuSpatialGridSolver.cs:1761-1772` and returns false if the cold worker, vault, or snapshot handle are not already valid. The existing `projectRoot` overload remains for cold setup and compatibility; it now delegates to `TryQueueTelemetryDumpPrepared()` after `EnsureDumpWorker`.
Rejected Alternatives: Building paths or starting a worker from the fault gate was rejected because fault handling should copy the fixed Vault snapshot and signal only. Removing the background worker was rejected because Task 15 explicitly requires background serialization. Adding a new native plugin writer was rejected in this pass because it crosses domain ownership and cannot be verified while the project build remains blocked by unrelated UI/Visor compile errors.
Scalability potential: Low/Middle/High/Ultra normal frames remain unchanged. On fault, the route now degrades to queue failure if the cold worker is unavailable instead of allocating path/worker infrastructure during corrupted-state handling.
Hardware Impact: Normal frame 0 us. Fault gate saves managed path construction and possible worker-preparation work. Static range scans show `FaultGate`, `PreparedQueue`, and `PreparedWorkerCheck` have 0 hits for `new`, path construction, `ResolveProjectRoot`, string formatting, LINQ, `foreach`, interpolation, or `catch(Exception)`.

## Apex Override Re-Audit 14

Problem: AUP determinism needed proof beyond the public query path. The flocking neighbor helper computes distances from `AmbientEntityDTO.Position` snapshots, which looks suspicious if read without tracing the producer phase.
Solution: Traced the producer-consumer path. `UpdateShinobuFlockingJob` computes `local = AupToLocal(in meta.PositionAup, in CenterAup)` at `ShinobuEcosystemBalancer.cs:4225`; `AupToLocal` performs the grid/local subtraction in double at `:2878-2881` and casts only the delta at `:2882`. The job writes that local value into `entity.Position` and `entitySnapshot` at `:4243/:4248`. The neighbor solver reads the same local snapshot at `:4327` and passes it into `AccumulateNeighborBatch4` at `:4613/:4632`. This is a visual boid cheat over an AUP-derived local frame, not a direct absolute-AUP-to-float cast.
Rejected Alternatives: Recomputing double AUP deltas for every neighbor lane was rejected because it would spend CPU on scientific accuracy the game does not need. The approved model is one AUP-to-local conversion per entity per owner phase, then cheap local boid math inside the frame.
Scalability potential: Low tier keeps cheap local boid math. Middle/High/Ultra can raise probe and neighbor sample budgets through continuous `GlobalQualityWeight` without changing authority ownership or DTO layout.
Hardware Impact: Runtime 0 us; audit-only. Avoided adding per-neighbor double conversions.

Problem: Static proof had to distinguish real heap allocation from cold worker setup and stack-only span wrappers in the diff.
Solution: Diff-added managed-token scan over the modified source found only `new AutoResetEvent`, `new Thread`, `new FileStream`, `new Span<byte>`, and `new ReadOnlySpan<byte>`. The first three are cold/background dump-route objects; the span constructors are value-type wrappers over Vault byte memory. No `string.Format`, `.ToString()`, LINQ, `foreach`, interpolation, `catch(Exception)`, raw snapshot pointer, `UnsafeUtility.Malloc/Free`, `H8Memory.AllocateRaw/FreeRaw`, or `new SpatialGridTelemetryEntry[]` remains in the modified spatial runtime/fault route.
Rejected Alternatives: Reporting a whole-file zero-new claim was rejected because the fault worker intentionally owns managed thread/file objects outside the query/Tick hot path. Running Roslyn/build again was rejected because no native field declarations or job signatures changed after Apex13, and the user explicitly ordered rare dotnet/build execution.
Scalability potential: Normal gameplay remains Zero-GC by hot-path static proof; fault export keeps bounded postmortem evidence.
Hardware Impact: Normal frame 0 us. Fault worker allocations remain cold/background and are not claimed as Zero-GC runtime hot path.

Problem: Lock symmetry and BufferID uniqueness needed a separate pass after the dump-route rewrites.
Solution: Spatial dump route count: `TryAcquireWriteLock=1`, `ReleaseWriteLock=1`, `TryLockBuffer=1`, `TryUnlockBuffer=2`. The two unlocks are distinct paths: failed resolve unlocks immediately, successful path unlocks in the caller finally. `BufferID.ShinobuSpatialGridDumpSnapshot = 70475` appears exactly once; BufferID duplicate groups=0.
Rejected Alternatives: Counting all enum numeric duplicates across unrelated enums was rejected because `SystemID` and `BufferID` share numbers by design.
Scalability potential: No runtime behavior change.
Hardware Impact: Runtime 0 us; audit-only.

Problem: Build verification after the new patch was required.
Solution: Ran `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` after CPU/process guard cleared. It failed on 24 existing `FixedUiEventQueue<>` missing-type errors in `UI` and `Visor` files before a green ecology proof could be obtained.
Rejected Alternatives: Fixing UI/Visor was rejected as domain breach. Claiming compile success was rejected. Launching more builds after failure would be noise.
Scalability potential: No runtime behavior change.
Hardware Impact: Build wall time 52.25 seconds; gameplay runtime 0 us.

## Apex Override Re-Audit 3

Problem: `SpatialHashQuery` was no longer a native-alias offender, but it remained a runtime struct without an explicit ARM64 byte map. It carries `double3 CenterAbsolute` and multiple 16-byte vault descriptors, so leaving compiler layout implicit was weaker than the user's requested proof standard.
Solution: Converted `SpatialHashQuery` to `[StructLayout(LayoutKind.Explicit, Size = 144)]`. Put `CenterAbsolute` at offset 0, five `VaultGenerationHandle<T>` descriptors at offsets 24/40/56/72/88, scalar fields at offsets 104-136, and private `_pad0` at offset 140. Added cold-boot `AssertSize<SpatialHashQuery>(144)` and exact offset assertions.
Rejected Alternatives: Leaving it implicit was rejected because the report would depend on prose. Removing `SpatialGridEntryDTO` overlay aliases was rejected because `rg` found existing editor/report ABI documentation and deleting public fields would be public API churn without a separate interface approval.
Scalability potential: Low/Middle/High/Ultra behavior does not change. This is ABI proof, not a fidelity change. Stable query descriptor layout lets higher tiers increase presentation density without changing gameplay truth ownership.
Hardware Impact: Hot runtime cost 0 us. Cold layout assertion cost only during layout guard execution.

Problem: The Roslyn and SHA proof artifacts were stale after the explicit-layout edit.
Solution: Re-ran `Tools\VaultNativeAliasRoslynAudit` for `AI/Ecosystem`, `AI/Ambient`, `Animation/FaunaProcedural`, and macro `Scripts/Ecosystem`. Results remain parseFailures=0 and forbiddenPersistentCandidates=0 for all four roots. Regenerated `Docs/Reports/VAULT_EXORCISM_APEX_REAUDIT_1301.json` with current hashes and `SpatialHashQuery` byte map.
Rejected Alternatives: Reusing the old JSON was rejected because it would report stale SHA-256 and omit the new struct layout.
Scalability potential: No runtime effect. Prevents stale proof from being used as release evidence.
Hardware Impact: Runtime 0 us. Tool execution only.

Problem: The user's Zero-GC demand was broader than the actual modified query path. Whole-file `ShinobuEcosystemBalancer.cs` still contains pre-existing `catch (Exception)` sites, and claiming entire-file purity would be false.
Solution: Reported the boundary exactly. Modified query runtime range `ShinobuSpatialGridSolver.cs:321-693` has zero forbidden hits for `new`, `string.Format`, `.ToString`, LINQ, `foreach`, string concat/interpolation, or `catch(Exception)`. Diff-added forbidden scan has one hit: `new FileStream` in `ShinobuSpatialGridForensics` fault-only dump writer at line 1371. Existing Balancer catch sites are listed as pre-existing, not introduced.
Rejected Alternatives: Reporting zero whole-file managed patterns was rejected as false. Removing the dump writer was rejected because Task 15 requires a binary blackbox path. Moving file IO to a new cross-domain native writer was rejected in this domain pass because no approved crash-reporter bridge exists in 1301 ownership.
Scalability potential: Normal gameplay remains static Zero-GC by query-path proof. Fault path prioritizes forensic evidence after corruption is detected.
Hardware Impact: Normal frame 0 us. Fault-only file IO allocates managed FileStream and is not claimed GC-free.

Problem: Build proof had to be rerun after the layout edit.
Solution: Checked CPU/process guard, then ran `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal`. Build again failed on 24 existing `FixedUiEventQueue<>` missing-type errors in UI/Visor under `Hecton8.Core.csproj`. No `SpatialHashQuery` compile errors were emitted before the external wall stopped the build.
Rejected Alternatives: Fixing UI/Visor was rejected as 1301 domain breach. Claiming a green ecology build was rejected because no full compile success exists.
Scalability potential: No runtime behavior change.
Hardware Impact: Build wall time 55.80 seconds; gameplay runtime 0 us.

## Apex Override Re-Audit 4

Problem: `CollectEntitiesInRadius` still resolved DataVault read-only views before rejecting invalid `results`, NaN `centerAup`, NaN `radiusMeters`, or NaN `CellSizeMeters`. That was fail-closed after resolution, but not strict enough: corrupted query input should not touch spatial grid views at all.
Solution: Moved the invalid-input guard to the first branch in `CollectEntitiesInRadius`. Invalid input now hashes failure class 5, attempts only telemetry failure recording, and returns before `TryResolveViews` and before `QuantizeCell`.
Rejected Alternatives: Keeping the existing order was rejected because it consumed DataVault resolution bandwidth for a query that was already known to be corrupt. Throwing or logging was rejected; failure telemetry stays binary and unmanaged.
Scalability potential: Low tier avoids useless view resolution on corrupt query input. Middle/High/Ultra valid query behavior and visual density are unchanged.
Hardware Impact: Valid path cost unchanged relative to Apex3. Invalid path saves three read-only handle resolutions and exits earlier.

Problem: Flocking neighbor traversal had a separate corrupt bucket range path: `range.StartIndex + math.max(0, range.Count)` could overflow before clamping to `Count`.
Solution: Replaced it with `start = clamp(StartIndex, 0, Count)`, `available = Count - start`, `rangeCount = min(max(0, Count), available)`, and `end = start + rangeCount`. This matches the query facade fail-closed range pattern.
Rejected Alternatives: Trusting spatial ranges because they are generated internally was rejected; the requested defrag/corruption model assumes metadata can be damaged.
Scalability potential: Low/Middle/High/Ultra quality tiers keep the same neighbor solve budget. Corrupt ranges stop expanding scan windows or wrapping signed int arithmetic.
Hardware Impact: Valid path adds three integer ops per found bucket range. Estimated below 0.1 us per affected query on i3/MX350-class silicon.

Problem: Proof artifacts were stale after the two Apex4 code edits.
Solution: Re-ran the net10 Roslyn native-alias audits. `AI/Ecosystem` remains parseFailures=0 and forbiddenPersistentCandidates=0; hash changed to `67e813e988abe23d8864d176e9fbec13eefb16f81f490b5adbbce7f0a8a66fd8` because line numbers changed. Updated status and v4 report with new source SHA values.
Rejected Alternatives: Reusing Apex3 hashes was rejected as false evidence. The net8 audit binary was rejected because this machine only has .NET 10 runtime.
Scalability potential: No runtime effect. Prevents stale proof from being accepted as release evidence.
Hardware Impact: Runtime 0 us; tool execution only.

Problem: Full build verification after Apex4 was required but blocked by active `dotnet.exe` processes. Previous green proof also does not exist because the last build failed on unrelated UI/Visor `FixedUiEventQueue<>` errors.
Solution: Did not launch another `dotnet build` while the guard was violated. Recorded the state as pending verification, not fixed.
Rejected Alternatives: Violating the build guard was rejected. Claiming compile success was rejected.
Scalability potential: No runtime behavior change.
Hardware Impact: Runtime 0 us; verification debt remains.

## Apex Override Re-Audit 5

Problem: After Apex4, `TryResolveViews` and `TryResolveBucketRanges` could still build failure hashes from `.Length` on default or unresolved `NativeArray<T>.ReadOnly` views after a failed DataVault read. That is a fault-path native-view misuse, not a hot allocation issue.
Solution: Guard all resolved lengths with `.IsCreated ? .Length : 0` before hashing. Evidence lines: `ShinobuSpatialGridSolver.cs:587-594` for entries/AUP/buckets and `ShinobuSpatialGridSolver.cs:625-627` for bucket-only lookup failure.
Rejected Alternatives: Assuming default `ReadOnly.Length` is safe was rejected because the point of the failure branch is corrupted or stale descriptors. Removing the failure hash was rejected because postmortem rows need deterministic state evidence.
Scalability potential: Low/Middle/High/Ultra valid query behavior is unchanged. Corrupt descriptor handling degrades to deterministic zero-length hash components instead of touching invalid native views.
Hardware Impact: Valid path 0 us. Failure path adds three `IsCreated` branches before hashing; cost is below measurement relevance and only executes after handle resolution failure.

Problem: The user explicitly ordered rare dotnet/build execution. Apex5 changed only local failure-branch integer reads and documentation, not native fields or job signatures.
Solution: Did not rerun Roslyn or `dotnet build` after Apex5. Kept Apex4 Roslyn proof as the latest AST evidence and recorded Apex5 as a local-only safe-length patch with fresh SHA/static scans.
Rejected Alternatives: Launching another build/Roslyn pass after every small patch was rejected as violating the current user constraint and wasting the machine while the known UI/Visor compile wall still exists. Claiming green compile was rejected.
Scalability potential: No runtime effect. Verification discipline stays factual.
Hardware Impact: Runtime 0 us. Avoided another full-project build attempt while the known blocker is unchanged.

## Apex Override Re-Audit 6

Problem: The spatial fault dump path still had integer overflow exposure under corrupted cursor data. `ShinobuEcosystemBalancer` used `spatialCursor[0] - 1`, and `ShinobuSpatialGridForensics` used `cursor - telemetry.Length`. With `int.MinValue` or a hostile cursor, the crash-dump route could overflow before it wrote the binary ring.
Solution: Sanitize `spatialCursor[0]` to `1` before subtracting one in the runtime fault gate. Sanitize dump `cursor` to `0` before serializing the header and widen `safeCursor - telemetry.Length` to `long` before modulo traversal. Evidence lines: `ShinobuEcosystemBalancer.cs:1819-1822`, `ShinobuSpatialGridSolver.cs:1381-1394`.
Rejected Alternatives: Trusting the cursor because the writer normally owns it was rejected; the requested failure model includes damaged buffers. Catching `OverflowException` was rejected because fault paths must reduce managed exception control flow, not depend on it.
Scalability potential: Low/Middle/High/Ultra normal frames unchanged. Corrupted telemetry cursor now degrades to deterministic slot 0 traversal and still attempts the blackbox dump.
Hardware Impact: Normal frame 0 us. Fault path adds two integer comparisons and one widened subtraction; no measurable gameplay cost.

Problem: The user requested AST proof, but current CPU probes reported `CPU_LOAD=95` then `CPU_LOAD=84`, and the active instruction says dotnet/build must be rare.
Solution: Did not launch Roslyn/dotnet under that load. Used the last completed net10 Roslyn proof as the latest AST evidence and added fresh non-dotnet static scans for the Apex6 edit.
Rejected Alternatives: Starting a .NET process under 95% CPU was rejected. Reporting a new AST run without executing it was rejected.
Scalability potential: No runtime effect. Machine contention avoided.
Hardware Impact: Runtime 0 us. Verification debt is explicit.

## Apex Override Re-Audit 7

Problem: The prior status marked Task 15 as fully done, but the XML asks for background-thread serialization of `Docs/AgentLogs/Dump_1301_AIEcology.bin`. Current code mirrors the spatial telemetry ring to that path, but it does it synchronously in the fault route through managed `FileStream` and specific IO/path `catch` branches. That is a useful binary artifact, but it is not the literal background zero-GC dump bridge requested by the prompt.
Solution: Reclassified Task 15 to `[PARTIAL / BLOCKED BY CRASH_EXPORT_BRIDGE]` in `Status_1301.md` and updated the JSON report. Kept the current code because the available `GlobalTelemetryBus` background thread exports only its own global blackbox format; it does not provide an approved arbitrary 300-row ecology payload route. A correct full fix needs a Vault-owned byte snapshot buffer or native crash-export bridge, plus a route card, before a background worker may read spatial telemetry after the faulting phase.
Rejected Alternatives: Claiming the synchronous `FileStream` route as full compliance was rejected as false. Spawning a new background thread over a live `NativeArray<SpatialGridTelemetryEntry>` was rejected because DataVault compaction can relocate the ring and the worker would hold an unsafe view outside the owner phase. Copying to a managed `byte[]` was rejected because it would move the forensic path away from the Zero-GC mandate.
Scalability potential: Low/Middle/High/Ultra normal frames are unchanged. Fault artifact exists today, but release-grade asynchronous export remains blocked until a sanctioned snapshot/export bridge exists.
Hardware Impact: Normal frame 0 us. Current fault path is synchronous managed disk I/O. No runtime microsecond saving is claimed.

Problem: The prompt extraction proof used `TASK_MARKERS=20` without stating that the active batch uses textual `Task NN:` rows, not nested `<TASK>` tags.
Solution: Re-ran CLI extraction with an attribute-aware `<AGENT_PROMPT ... id="1301" ...>` regex and recorded both counters: `TEXT_TASK_COUNT=20`, `XML_TASK_TAG_COUNT=0`.
Rejected Alternatives: Keeping the ambiguous `TASK_MARKERS` label was rejected because it can be read as XML task tags.
Scalability potential: No runtime effect.
Hardware Impact: Runtime 0 us.

## Apex Override Re-Audit 8

Problem: Apex7 correctly admitted Task 15 was partial, but leaving it partial was not enough. The fault route still wrote synchronously from the caller through managed `FileStream`; that did not match the requested background-thread dump behavior.
Solution: Added a pre-owned spatial-grid dump worker. `ShinobuEcosystemBalancer.Activate()` cold-starts `ShinobuSpatialGridForensics.EnsureDumpWorker(ResolveProjectRoot())`, `Dispose()` shuts it down, and the spatial fault gate queues a bounded snapshot through `TryQueueTelemetryDump`. The fault caller now copies at most 300 `SpatialGridTelemetryEntry` rows into a fixed `UnsafeUtility.Malloc` buffer, 19200 bytes, 8-byte aligned, then signals the worker. The worker serializes both `Dump_SHINOBU_301.bin` and `Docs/AgentLogs/Dump_1301_AIEcology.bin`.
Rejected Alternatives: A worker reading a live `NativeArray<SpatialGridTelemetryEntry>` was rejected because a DataVault relocation/defrag window could stale the view outside the owner phase. A cross-frame DataVault read lock was rejected because it would block compaction and violate phase ownership. A managed `byte[]` snapshot was rejected because it would reintroduce fault-time heap pressure. A native plugin disk writer was rejected in this pass because it crosses out of 1301 code ownership and needs a separate route card/rebuild surface.
Scalability potential: Low tier pays 0 normal-frame cost; on fault it copies <=19200 bytes and returns after signaling. Middle/High/Ultra keep identical swarm/math fidelity; the fix buys better postmortem evidence, not extra realism. Visual density remains controlled by existing continuous `GlobalQualityWeight`.
Hardware Impact: Normal frame 0 us. Fault path bounded copy: 300 entries * 64B = 19200B plus `AutoResetEvent.Set()`. Disk I/O is moved to a background managed worker. The worker still uses `FileStream`; this is not a pure native crash-export plugin.

Problem: The dump bridge needed to remove the newly introduced managed snapshot array.
Solution: Replaced `SpatialGridTelemetryEntry[]` with `SpatialGridTelemetryEntry*` allocated by `UnsafeUtility.Malloc(..., 8, Allocator.Persistent)`, cleared once, and freed by `ReleaseSnapshotBuffer()` after worker shutdown. `TryWriteQueuedDumpFile` validates `s_pendingCount <= s_snapshotCapacity` before making a span over the pointer.
Rejected Alternatives: Persistent `NativeArray<T>` outside GlobalDataVault was rejected by the native collection mandate. Holding DataVault views across the worker was rejected for the same relocation reason.
Scalability potential: Weak hardware avoids an extra managed heap object in the crash bridge. High/Ultra tiers get the same forensic path with no gameplay-cost branch.
Hardware Impact: Cold unmanaged allocation: 19200B. Normal frame 0 us. Fault copy remains linear over 300 entries, not entity count.

## Apex Override Re-Audit 9

Problem: Apex8 still violated the native memory mandate. The snapshot buffer used direct `UnsafeUtility.Malloc` and `UnsafeUtility.Free`, so the pointer was not owned through the H8 memory tracker / `SystemID.AIEcology` route.
Solution: Replaced the raw allocation with `H8Memory.AllocateRaw(snapshotBytes, 8, SystemID.AIEcology, Allocator.Persistent, clearMemory: true)` and replaced release with `H8Memory.FreeRaw(s_snapshot, Allocator.Persistent, SystemID.AIEcology)`. Evidence: `ShinobuSpatialGridSolver.cs:1408-1413` and `:1550`.
Rejected Alternatives: Direct `NativeMemorySentinel.RegisterPointer` was rejected because `H8Memory.AllocateRaw` already performs the internal pointer registration and owner accounting. Persistent `NativeArray<T>` outside GlobalDataVault was rejected by the native collection mandate. Reverting to managed arrays was rejected because it reintroduces heap state in the fault bridge.
Scalability potential: Low/Middle/High/Ultra normal frames remain 0 us. The crash bridge remains bounded to a 300-row snapshot and does not alter swarm fidelity or continuous `GlobalQualityWeight`.
Hardware Impact: Normal frame 0 us. Cold allocation still reserves 19200B once; fault path still copies <=19200B and signals the worker. Gain is ownership correctness, not frame-time reduction.

## Apex Override Re-Audit 10

Problem: Apex9 still left a literal violation of the XML zero-alias rule. `ShinobuSpatialGridForensics` held a persistent `SpatialGridTelemetryEntry* s_snapshot` outside `GlobalDataVault`, even though the pointer was H8Memory-tracked. It also reported worker write failure through `GlobalTelemetryBus.PublishPerformanceWarning`, which routes into managed global telemetry from the background dump worker.
Solution: Removed the persistent raw snapshot pointer entirely. Added `BufferID.ShinobuSpatialGridDumpSnapshot = 70475` and claim it as a `GlobalDataVault` byte buffer of `DumpSnapshotBytes = 19224`. The fault caller copies the 24-byte header plus <=300 `SpatialGridTelemetryEntry` rows into that Vault buffer under `TryAcquireWriteLock`/`ReleaseWriteLock`; the worker resolves the same handle under `TryLockBuffer`/`TryUnlockBuffer` only while writing the file. Worker/fault failure reporting is now `LastDumpFailureFlags`/`TotalDumpWriteFailures`, not a GlobalTelemetryBus call.
Rejected Alternatives: Keeping the H8Memory raw pointer was rejected because the XML explicitly forbids long-lived raw unsafe pointers outside `GlobalDataVault`. Holding a DataVault lock for the whole worker lifetime was rejected because it weakens defrag compatibility. Worker reads over the live telemetry ring were rejected because compaction can relocate the ring between phases. A managed `byte[]` snapshot was rejected because it creates heap state in the forensic route. Native plugin disk IO was rejected in this pass because it needs a cross-domain crash-export bridge and compile proof.
Scalability potential: Low tier normal frames stay at 0 added cost; fault path copies <=19224B once. Middle/High/Ultra keep identical swarm behavior and `GlobalQualityWeight` scaling; the change buys safer postmortem export, not more simulation.
Hardware Impact: Normal frame 0 us. Fault caller does one bounded Vault snapshot copy and one signal. Worker blocks defrag only while reading the 19224B snapshot for file output, not across its lifecycle.

## Apex Override Re-Audit 11

Problem: Apex10 still had a lifecycle hole. `OnGlobalRegistryServiceReplaced(DataVault)` reset `_dataVault` and all Vault handles, but did not shut down the static spatial dump worker first. Because `EnsureDumpWorker` rejects a live worker bound to a different `IDataVault` or snapshot handle, a DataVault hot-swap could leave the worker alive on the old vault and make all later spatial fault dumps fail closed forever.
Solution: Added `TryEnsureSpatialGridDumpWorker(IDataVault vault)` and routed both `Activate()` and DataVault hot-swap through it. Hot-swap now completes jobs, unlocks job buffers, calls `ShinobuSpatialGridForensics.ShutdownDumpWorker()`, resets handles, reclaims Vault state, clears the spatial range table, then starts the dump worker against the new `_dataVault` and `_spatialGridDumpSnapshotHandle`. Evidence: `ShinobuEcosystemBalancer.cs:283-286`, `:292-300`, and `:332-357`.
Rejected Alternatives: Keeping the old worker across a DataVault replacement was rejected because the worker stores `s_dumpVault` and `s_dumpSnapshotHandle`. Forcing `EnsureDumpWorker` to silently overwrite a live worker was rejected because it could race a pending dump. Leaving dump restart only in `Activate()` was rejected because service hot-swap is an explicit runtime route.
Scalability potential: Low/Middle/High/Ultra normal frames remain unchanged. The fix protects postmortem export during vault replacement without changing swarm fidelity, DTO layout, or continuous `GlobalQualityWeight`.
Hardware Impact: Normal frame 0 us. Hot-swap path performs one managed worker shutdown/join and one cold worker rebind; this is not a per-frame cost. Fault path remains a bounded <=19224B Vault snapshot plus signal.

## Apex Override Re-Audit 12

Problem: Apex11 fixed worker rebinding, but `ResetVaultHandles()` still did not clear the new `_spatialGridDumpSnapshotHandle`. That left a stale `VaultGenerationHandle<byte>` in the balancer after teardown or failed DataVault hot-swap. Even if later cold boot overwrote it on success, the intermediate state violated descriptor consistency and could feed stale proof/logging paths.
Solution: Added `_spatialGridDumpSnapshotHandle = default;` to `ResetVaultHandles()` at `ShinobuEcosystemBalancer.cs:2093`. Re-ran the existing net10 Roslyn native-alias scanner once for `AI/Ecosystem`, `AI/Ambient`, `Animation/FaunaProcedural`, and macro `Scripts/Ecosystem`. All four roots returned `parseFailures=0` and `forbiddenPersistentCandidates=0`.
Rejected Alternatives: Relying on `EnsureVaultState()` to overwrite the stale handle later was rejected because fail-closed state must be clean immediately after reset. Launching a full project build was rejected because the user explicitly ordered rare build/dotnet usage and the known full build wall remains unrelated UI/Visor `FixedUiEventQueue<>`.
Scalability potential: Low/Middle/High/Ultra normal frames remain unchanged. The fix improves lifecycle determinism for the dump snapshot route without changing simulation math, visual density, or `GlobalQualityWeight`.
Hardware Impact: Normal frame 0 us. Reset path writes one extra 16-byte descriptor default only during teardown/hot-swap; no gameplay-frame cost.

## Apex Override Re-Audit 13

Problem: The dump failure flag path still used a non-atomic `Volatile.Read | Volatile.Write` update in `RecordQueueFailure()`. Concurrent queue/write failures could lose one bit and under-report blackbox failure state. The snapshot serializer also trusted `UnsafeUtility.SizeOf<SpatialGridTelemetryEntry>()` to remain 64 before slicing the fixed 19224-byte Vault dump buffer.
Solution: Added `AddDumpFailureFlags(int flags)` with a `CompareExchange` CAS loop at `ShinobuSpatialGridSolver.cs:1382-1396`. `RecordQueueFailure()` and worker write failures now route through it. `DrainPendingDump()` captures a baseline failure flag value and clears only through `CompareExchange` on all-files-success. Added an explicit `entrySize != 64` fail-closed guard before snapshot byte slicing at `ShinobuSpatialGridSolver.cs:1558-1564`.
Rejected Alternatives: Keeping read/OR/write was rejected because a failure telemetry path must not lose bits under concurrent fault pressure. Throwing on row-size drift was rejected because fault export must fail closed without managed exception control flow. Running a full build was rejected because this edit did not touch native fields/job signatures/asmdef, and the user explicitly ordered rare build/dotnet attempts while the known UI/Visor compile wall remains unrelated.
Scalability potential: Low/Middle/High/Ultra normal frames remain unchanged. Fault export keeps the same 300-row cap and fixed Vault snapshot; high-end devices do not spend extra simulation cycles here.
Hardware Impact: Normal frame 0 us. Failure-only path adds a small CAS loop for flag recording. Snapshot path adds one integer equality check before copy; fault-only cost is below measurement relevance on i3/MX350-class silicon.

Problem: Descriptor consistency proof needed a direct graph audit, not only line inspection of the new dump snapshot handle.
Solution: Ran a text graph over `ShinobuEcosystemBalancer.cs`: `VaultGenerationHandle` fields=32, `ClaimVaultHandle` assignments=32 missing=0, `TryOpenVaultView` verification opens=32 missing=0, reset defaults=33 missing=0. The extra reset is non-handle state; no handle is unclaimed, unchecked, or uncleared.
Rejected Alternatives: Trusting visual inspection was rejected. Running Roslyn again was rejected because no native field declarations changed after Apex12 and the existing net10 Roslyn AST pass is still current for native-alias ownership.
Scalability potential: No runtime effect. It proves lifecycle determinism before allowing higher-tier visual density to depend on the same Vault state.
Hardware Impact: Runtime 0 us; audit-only.

## Apex Override Re-Audit 17 Final Placement

Problem: Future context recovery reads the tail of this rationale file, but the earlier Apex17 block landed above older entries because this file already had historical ordering drift.
Solution: Repeat the current decision at the file tail: stop-requested dump workers are not valid prepared workers; the worker file output uses `FileOptions.WriteThrough`; dotnet/build were not rerun because no native fields, DTO layouts, job signatures, asmdefs, spatial query math, or hot boid solver code changed.
Rejected Alternatives: Leaving the latest decision only above older entries was rejected because it weakens disk-backed anti-amnesia recovery. Running dotnet/build again was rejected by the user's explicit rare-build constraint and the unchanged UI/Visor compile wall.
Scalability potential: Low/Middle/High/Ultra normal frames are unchanged. Fault export is more deterministic during teardown/hot-swap and remains bounded to the 19224-byte Vault snapshot.
Hardware Impact: Normal frame 0 us. Fault queue adds one volatile read; worker disk write is failure-path only.

## Apex Override Re-Audit 18 Final Placement

Problem: Legacy dump methods still accepted `projectRoot` and could be misread as a fault-time path builder even after the prepared worker route existed. The tail of this rationale file also did not contain the latest Apex18 decision, weakening disk-backed recovery.
Solution: `WriteTelemetryDump(projectRoot, ...)` and `TryWriteTelemetryDump(projectRoot, ...)` now route only through the prepared Vault snapshot worker; the projectRoot queue overload and single-arg `EnsureDumpWorker(string)` were removed. Cold worker setup remains explicit through `EnsureDumpWorker(projectRoot, vault, handle)`. Verified no `TryQueueTelemetryDump(projectRoot)`, no `EnsureDumpWorker(string projectRoot)`, no `FileOptions.Asynchronous`, no `catch(Exception)`, no raw snapshot pointer, and no `UnsafeUtility.Malloc/Free` or `H8Memory.AllocateRaw/FreeRaw` in `ShinobuSpatialGridSolver.cs`.
Rejected Alternatives: Leaving the legacy projectRoot queue overload was rejected because it allowed future callers to rebuild a managed dump path from a fault site. Removing the cold setup overload was rejected because activation/hot-swap still need one explicit, non-frame setup route. Running dotnet/build again was rejected by the user's rare-build constraint and because this patch did not touch native fields, DTO layouts, job signatures, asmdefs, spatial query math, or hot boid solver code.
Scalability potential: Low/Middle/High/Ultra normal frames stay unchanged. Fault export remains bounded to one 19224-byte Vault snapshot and a pre-existing worker signal.
Hardware Impact: Normal frame 0 us. Fault path avoids path construction and worker creation; background disk output is failure-path only.

## Apex Override Re-Audit 20

Problem: The previous report had the right source state, but the final evidence needed a stricter case-sensitive scan and an explicit AST rerun decision under the user's dotnet/build constraint.
Solution: Ran a case-sensitive diff-added token scan over the three changed source files. Added v20 JSON evidence. Diff-added source contains exactly five `new` hits: cold `AutoResetEvent`, cold `Thread`, background-worker `FileStream`, and two stack-only `Span` wrappers over a Vault byte buffer. Case-sensitive counts for `string.Format`, `.ToString(`, interpolation, `foreach`, `System.Linq`, LINQ selector/terminal calls, `Enumerable.`, and `catch(Exception)` are all zero in diff-added source. Full-file case-sensitive scans over the three changed source files also returned zero for those managed text/LINQ/format tokens; only `object previousService/currentService` remain as a pre-existing cold `GlobalRegistryServiceReplaced` callback signature.
Rejected Alternatives: Running the Roslyn executable again while `CPU_LOAD=100` was rejected by the explicit user rule against dotnet/build churn under load. Treating `math.select` as LINQ was rejected because the case-sensitive scan separates Unity.Mathematics from `.Select(`. Treating `new float3` and DTO struct literals as heap allocation was rejected; they are value-type construction in jobs or stack/local assignment, not managed heap objects.
Scalability potential: Low/Middle/High/Ultra normal frames are unchanged. Fault export remains bounded; no extra simulator or fidelity branch was introduced.
Hardware Impact: Normal frame 0 us. Audit-only pass; no runtime code changed in Apex20.

## Apex Override Re-Audit 21

Problem: The prepared dump queue could copy a valid 19224-byte Vault snapshot, set `s_pendingByteCount`, then fail to signal the background worker because `s_dumpSignal` was null or disposed. The worker drain path also returned to idle after its single write attempt without clearing the byte count. Neither route leaks per-frame memory, but both leave stale forensic state after a failed handoff or completed drain.
Solution: In `TryQueueTelemetryDumpPrepared`, clear `s_pendingByteCount` before returning false on null signal and on `ObjectDisposedException` from `signal.Set()`. In `DrainPendingDump()`, clear `s_pendingByteCount` after the write attempt and before `s_dumpState` returns to idle. Evidence: `ShinobuSpatialGridSolver.cs:1617`, `:1629`, and `:1681`.
Rejected Alternatives: Leaving stale byte count because the next successful queue overwrites it was rejected; failure state must be clean immediately. Adding a helper was rejected as unnecessary abstraction for three adjacent fail-closed cleanup writes. Running dotnet/Roslyn/build was rejected because `CPU_LOAD=100`; the first Apex21 guard also saw active `csc.exe`/`dotnet.exe`, and the final guard saw no dotnet/csc/MSBuild/VBCS process but CPU remained saturated.
Scalability potential: Low/Middle/High/Ultra normal frames are unchanged. Fault export remains one bounded Vault snapshot and a pre-existing worker signal; the patch changes only corrupted/lifecycle failure state cleanup.
Hardware Impact: Normal frame 0 us. Failed signal handoff adds one volatile write before returning false; no measurable gameplay cost.

## Apex Override Re-Audit 22

Problem: `ShutdownDumpWorker()` set `s_stopRequested=1` and called `signal.Set()` without a typed failure branch. Under concurrent teardown or a previously disposed `AutoResetEvent`, shutdown could escape via `ObjectDisposedException` instead of failing closed.
Solution: Wrapped only the shutdown `signal.Set()` call in `catch (ObjectDisposedException)` and left the worker join/drain semantics unchanged. Evidence: `ShinobuSpatialGridSolver.cs:1514-1517`.
Rejected Alternatives: Broad `catch(Exception)` was rejected because Apex16 removed that pattern. Ignoring the race because the route is cold was rejected; teardown/fault code must not rely on managed exception escape. Replacing the worker with a native crash-export bridge was rejected in this pass because it needs a cross-domain route and build proof.
Scalability potential: Low/Middle/High/Ultra normal frames are unchanged. The fix only hardens teardown/fault lifecycle; it does not alter boid density, spatial math, or continuous `GlobalQualityWeight`.
Hardware Impact: Normal frame 0 us. Shutdown-only cost is one typed catch table around a signal call; no measured gameplay cost.

## Post-Compaction Verification

Problem: Context recovery required proving the on-disk artifacts still match the active prompt and changed source without violating the user's rare dotnet/build rule.
Solution: Re-read Status/Rationale, re-extracted `<AGENT_PROMPT id="1301">`, parsed the v22 JSON report, reran the case-sensitive managed-token scan over the three changed source files, and ran `git diff --check`. A narrow Roslyn rerun was considered only after a CPU/build-process guard, then skipped because the guard returned `CPU_LOAD=72` with no build processes.
Rejected Alternatives: Running the Roslyn executable under CPU >50 was rejected by the user's explicit rule. Running full `dotnet build` was rejected because no new compile-surface change was made and the known UI/Visor `FixedUiEventQueue<>` wall is unrelated to 1301 scope.
Scalability potential: No runtime behavior change. This preserves proof integrity without spending machine load on redundant compile work.
Hardware Impact: Runtime 0 us; audit-only.

## Apex Override Re-Audit 23

Problem: The v22 byte-map proof was still too weak for ARM64 physical ordering in the 1301-touched core memory surface. `BlockDescriptor` had byte fields at offsets 36/37 before `ushort Reserved2@38`, and `H8MemoryTelemetryEntry` had `ushort Owner@56` / `Flags@58` before `uint Frame@60`. Sizes were multiples of 8, but physical order was not strict 8-byte -> 4-byte -> 2-byte -> 1-byte.
Solution: Reordered explicit offsets, not just declarations. `BlockDescriptor`: `BasePointer@0`, `OffsetBytes@8`, `Bytes@16`, `OwnerKey@24`, `Generation@28`, `Owner@32`, `Flags@34`, `Reserved2@36`, `State@38`, `Reserved@39`. `H8MemoryTelemetryEntry`: 8-byte fields @0/@8/@16, 4-byte fields @24..@56, `Owner@60`, `Flags@62`. Updated `ValidateBlockDescriptorAbiOffsets`, `ValidateTelemetryEntryAbiOffsets`, fatal dump writer order, `FatalLeakDumpVersion=5`, and `VaultSurgeryEditTests` offset expectations.
Rejected Alternatives: Leaving this as a documentation caveat was rejected because the user explicitly demanded a byte-offset map, not size-only proof. Reordering only source declarations while preserving the old offsets was rejected because it would hide the physical layout defect. Broad H8Memory refactor was rejected; this touched only the two explicit DTO offset maps and their direct validators/schema/test.
Scalability potential: Low/Middle/High/Ultra normal frames are unchanged. Fatal memory dumps write the same 64 bytes per telemetry entry; the schema now records the corrected order. High-tier visuals gain nothing here; this is crash-proofing and ARM64 memory discipline.
Hardware Impact: Normal frame 0 us. Cold ABI validation count unchanged. Fatal dump write size unchanged; order change is binary schema only, not frame-time cost. Low-end i3/MX350 gain is avoided misordered DTO access in core forensic memory records, not measured gameplay microseconds.

## Apex Override Re-Audit 24

Problem: Apex23 runtime ABI guards covered the full corrected H8 memory byte map, but `VaultSurgeryEditTests` still asserted only part of the same map. Missing editor assertions: `BlockDescriptor.Generation@28`, `BlockDescriptor.Flags@34`, `BlockDescriptor.Reserved@39`, and `H8MemoryTelemetryEntry.Flags@62`.
Solution: Added the missing editor offset assertions at `VaultSurgeryEditTests.cs:219`, `:221`, `:224`, and `:235`. Runtime DTO layout was not changed because Apex23 already fixed the physical offsets and `H8Memory` validators already check the same fields.
Rejected Alternatives: Leaving partial editor coverage was rejected because the user asked for line-backed byte proof, not trust in prose. Reordering DTOs again was rejected because no layout defect remained after Apex23. Full build was rejected because the known unrelated UI/Visor `FixedUiEventQueue<>` wall would not validate this editor assertion patch cleanly and the user ordered rare build use.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. This is proof hardening only; no new simulation, no binary quality switch, no gameplay truth route change.
Hardware Impact: Runtime 0 us. Editor-only assertion coverage change. Roslyn native-alias audit rerun under guard: `CPU_LOAD=8`, build processes `NONE`; all four 1301 roots returned parseFailures=0 and forbiddenPersistentCandidates=0.

## Apex Override Re-Audit 25

Problem: A fresh domain scan found managed proof leaks outside the already-mutated files but still inside 1301 AI/Ecosystem ownership. Three cold layout guards built exception strings with `typeof(T).Name`, `+ fieldName`, `expected=`, and `observed=`. Two sibling ecology files also kept seven broad `catch(Exception)` branches in schedule/IO/fault dump routes.
Solution: Replaced layout guard diagnostics with fixed const messages in `ShinobuEcosystemLayoutManifest`, `SymbiosisLayoutManifest`, and `EcosystemPopulationLayoutManifest`. Narrowed broad catches to typed `IOException`, `UnauthorizedAccessException`, `ArgumentException`, `NotSupportedException`, `InvalidOperationException` branches. Schedule failures now unlock buffers and record fixed-hash telemetry instead of rethrowing.
Rejected Alternatives: Leaving sibling files untouched was rejected because the scan root is AI/Ecosystem and the same violation existed in the same domain. Keeping detailed exception strings was rejected because the user's current release gate values zero managed string construction over cold diagnostic detail. Running full build/Roslyn again was rejected because no native fields, DTO layouts, job signatures, asmdefs, or AUP math changed, and the user ordered rare dotnet/build attempts.
Scalability potential: Low/Middle/High/Ultra normal frames are unchanged. This does not add simulation, quality gates, or gameplay truth changes. It removes cold managed string churn and broad exception policy debt from ecology boot/fault routes.
Hardware Impact: Normal frame 0 us. Cold layout mismatch paths allocate less diagnostic string data. Job scheduling failure paths now fail closed with one fixed telemetry publish after buffer unlock; no gameplay-frame cost.
