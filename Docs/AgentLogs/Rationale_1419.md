# Rationale_1419

## 2026-05-28 Session Init

Problem: Ecosystem CPU spatial grid and GPU swarm exchange may retain persistent native aliases and ad-hoc upload paths.
Solution: Start with source-backed static archaeology before mutation. Bind work to DataVault descriptor ownership, method-local native view resolution, `LockBufferForWrite` upload discipline, explicit DTO layout, fail-closed queries, and 300-frame blackbox telemetry.
Rejected Alternatives: Immediate rewrite without alias ledger; blind `dotnet build`; lowering entity caps to avoid pointer management; adding new global surfaces before proving existing contracts.
Scalability potential: Low uses reduced cadence/capacity via continuous `GlobalQualityWeight`; Middle keeps full gameplay truth with throttled spatial depth; High increases boid density and biolum/presentation cadence; Ultra spends saved CPU on visual swarm overkill without changing DTO truth ownership.
Hardware Impact: Expected gain for i3/MX350 depends on source findings. Target is reduced CPU-GPU upload copies and no managed GC in hot routes; measured proof absent.

Problem: Batch hygiene check.
Solution: `Status_1419.md`, `Rationale_1419.md`, and `LOG_1419.md` were missing before initialization, so no stale status content was present.
Rejected Alternatives: Reusing old agent logs or previous batch reports.
Scalability potential: N/A; process guard only.
Hardware Impact: Prevents wasted integration time from stale task state; runtime gain none.

## 2026-05-28 Loop 1 Decisions

Problem: Batch paths named `Assets/_Project/Scripts/AI/EcosystemDirector.cs`, `FaunaSpatialHashGrid.cs`, and `SwarmComputeDirector.cs`, but those exact files do not exist on disk.
Solution: Bound the audit to proven equivalents: `World/EcosystemDirector.cs`, `World/FaunaSpatialHashRegistry.cs`, `World/WorldSpatialHashGrid.cs`, `AI/Ecosystem/ShinobuEcosystemBalancer*.cs`, `AI/Ecosystem/ShinobuSpatialGridSolver.cs`, and the active swarm compute shaders.
Rejected Alternatives: Creating placeholder files to satisfy prompt names; editing nonexistent paths; assuming source roles without `rg --files` proof.
Scalability potential: Low/Middle/High/Ultra unchanged; this decision prevents false ownership routes.
Hardware Impact: Runtime gain none. Avoided mis-edits that would force extra integration rebuilds on i3/MX350 hosts.

Problem: The first alias parser classified method-local `NativeArray<T>` variables as persistent fields.
Solution: Replaced it with an access-modifier constrained regex parser and regenerated `Docs/Reports/ECOSYSTEM_ALIAS_LEDGER_1419.json`; result is 0 forbidden persistent native fields and 87 allowed Burst/job struct native views.
Rejected Alternatives: Trusting noisy parser output; running Roslyn through `dotnet` before CPU/build gate.
Scalability potential: Low devices avoid unnecessary churn; Ultra devices keep current high-density job buffers without cap reduction.
Hardware Impact: Runtime gain 0 us; integration CPU saved by avoiding a false purge pass.

Problem: `ShinobuEcosystemBalancer.Tick` resolved raw Vault views before pinning job buffers.
Solution: Moved owner-tagged `TryLockBuffer` before `TryResolveBuffers`/`TryResolveSpatialGridBuffers`/`TryResolveFlockingBuffers`; wrapped scheduling with `finally` so all pre-schedule exits release locks, while scheduled jobs retain locks until `FinishFrameJobCompletion`.
Rejected Alternatives: Converting the 28-buffer job graph to per-buffer `TryAcquireWriteLock`; that API is single-writer view ownership, while the existing Vault contract explicitly supports pinned aliases for cross-phase/job lifetime.
Scalability potential: Low keeps smaller budgets with stable aliases; Middle/High/Ultra can raise density without exposing stale pointers during defrag windows.
Hardware Impact: Direct frame cost approximately 0 us; removes stale-alias crash risk. Extra branch/finally overhead is below measurable budget compared with job scheduling.

Problem: Sargassum micro-fauna GPU upload still used `UploadNativeArraySetData` for spawn buffer sync and `GraphicsBuffer.SetData` for single-boid live updates.
Solution: Replaced bulk spawn sync with `GraphicsBufferUploadUtility.UploadNativeArray` and single-boid updates with `GraphicsBuffer.LockBufferForWrite<BoidData>` plus `finally UnlockBufferAfterWrite`.
Rejected Alternatives: Keeping `SetData` because the source managed array was preallocated; lowering boid counts; adding a new upload utility.
Scalability potential: Low avoids managed upload bridge costs; Middle uses the same path; High/Ultra spend saved sync cost on swarm density/visual overkill.
Hardware Impact: Static estimate on i3/MX350: bulk spawn upload avoids roughly 35-120 us per 5k boid reupload versus high-level `SetData`; single-boid update avoids roughly 2-8 us per event and removes the managed-array upload bridge.

Problem: Spatial-grid forensic dumps did not write the exact 1419 ecosystem swarm crash artifact path required by the batch.
Solution: Added `Docs/AgentLogs/Dump_1419_EcosystemSwarm.bin` as a mirrored output from `ShinobuSpatialGridForensics`, preserving existing SHINOBU and Agent1301 dump paths.
Rejected Alternatives: Renaming existing owner dump paths; replacing Agent1301 compatibility route; synchronous dump writes on the simulation thread.
Scalability potential: Low/Middle keep asynchronous dump routing; High/Ultra get the same forensic artifact without affecting visual density.
Hardware Impact: Normal frame cost 0 us; only catastrophic dump path writes an additional file.

## 2026-05-28 Loop 2 Decisions

Problem: Task 06 demanded deletion of persistent native fields, but the strict ledger found none in scoped runtime classes.
Solution: Performed descriptor substitution as a proof step instead of a fake deletion; SHINOBU runtime already stores `VaultGenerationHandle<T>` descriptors for entity/AUP/boid/flocking/spatial/render/telemetry lanes.
Rejected Alternatives: Deleting Burst job `NativeArray<T>` parameters; those are phase-local physical views required by jobs and are not persistent owner fields.
Scalability potential: Low through Ultra keep the same descriptor ownership route; density scales by `GlobalQualityWeight`, not by memory ownership changes.
Hardware Impact: Runtime gain 0 us; preserves existing Burst vectorization.

Problem: Task 07 required cold boot registration without `Allocator.Persistent`.
Solution: Verified `ClaimVaultHandle`/`EnsureGenerationHandle` registrations for all relevant BufferID lanes and made no new persistent native allocation. Existing graphics buffers remain cold GPU resources with `LockBufferForWrite` usage flags.
Rejected Alternatives: Adding duplicate BufferID lanes in the 1419000 range; that would fork truth ownership and break one fact/one owner.
Scalability potential: Low keeps existing capacities; Middle/High/Ultra scale via active budgets, cadence, and GPU visual filtering.
Hardware Impact: Runtime gain 0 us; avoids duplicated memory pools.

Problem: Task 10 required job signatures to consume resolved views, not descriptors.
Solution: Verified spatial and flocking jobs take `NativeArray<T>` views with `[NoAlias]` and `[ReadOnly]` on read-only inputs. `SpatialHashQuery` keeps handles only as a public fail-closed query descriptor outside Burst job scheduling.
Rejected Alternatives: Passing `VaultGenerationHandle<T>` into jobs and resolving inside Burst; that would either fail compile or hide unsafe global state access inside jobs.
Scalability potential: Low/Middle/High/Ultra all preserve deterministic job data locality.
Hardware Impact: Keeps Burst-friendly signatures; expected gain is preserved vectorization, no new measured us.

## 2026-05-28 Loop 3 Decisions

Problem: Public ecosystem read/copy accessors still read private Vault-backed buffers through mutable `VaultBufferView.Resolve()`/indexer paths.
Solution: Added `VaultBufferView<T>.TryResolveReadOnly` using `IDataVault.TryReadOnlyHandle`, added a read-only index probe overload, and converted public sector population, biomass, macro swarm copy, and apex-sector reads to fail closed when the Vault view is invalid or locked.
Rejected Alternatives: Returning raw `NativeArray<T>` to external domains; allocating managed snapshots; hiding the issue behind `HasPendingSimulationJob` only.
Scalability potential: Low devices can drop reads during defrag without AI crashes; Middle/High/Ultra keep the same data route while raising swarm density through `GlobalQualityWeight`.
Hardware Impact: Runtime gain approximately 0 us. Main gain is removing mutable alias exposure and crash risk under compaction.

Problem: The batch required an explicit 64-byte `EcosystemTelemetryEntry`, while the active ring used the older public name `ShinobuTelemetryEntry`.
Solution: Renamed the active 64-byte DTO and all runtime/editor consumers to `EcosystemTelemetryEntry`; preserved the exact byte layout, dump version, and BufferID route.
Rejected Alternatives: Defining an unused duplicate DTO; that would create a second telemetry fact with no ownership route.
Scalability potential: Low/Middle/High/Ultra unchanged; schema stability protects every tier.
Hardware Impact: Runtime gain 0 us. Compile-time/editor compatibility maintained by updating `AbyssalSwarmTunerWindow`.

Problem: The 1419 dump route was initially mirrored from the spatial-grid forensic writer, which could collide with the primary swarm telemetry dump format.
Solution: Assigned `Docs/AgentLogs/Dump_1419_EcosystemSwarm.bin` to the primary 300-frame `EcosystemTelemetryEntry` blackbox and moved the spatial-grid secondary mirror to `Dump_1419_EcosystemSpatialGrid.bin`.
Rejected Alternatives: Two binary schemas writing the same path; text/JSON fault dumps.
Scalability potential: Low devices pay 0 normal-path cost; High/Ultra keep richer forensic coverage without changing gameplay truth.
Hardware Impact: Normal frame cost 0 us. Fault path writes one additional binary file.

Problem: GPU swarm upload needed proof that high-volume transfer avoids managed heap traffic.
Solution: Confirmed target upload lanes use `GraphicsBuffer.LockBufferForWrite` with `UnsafeUtility.MemCpy` for matrix/custom arrays and mapped writes for scalar/single-boid cases; post-patch scan found no `SetData` or `UploadNativeArraySetData` in scoped swarm files.
Rejected Alternatives: `ComputeBuffer.SetData`; managed staging arrays; reducing boid count instead of fixing upload path.
Scalability potential: Low uses cheaper cadence/capacity; Middle keeps the same route; High/Ultra spend saved CPU sync time on density and visual overkill.
Hardware Impact: Static i3/MX350 estimate remains 35-120 us saved per 5k boid bulk upload and 2-8 us per single-boid update.

## 2026-05-28 Loop 4 Decisions

Problem: Task 15 requested a single comprehensive build, but host state violated the explicit build gate.
Solution: Sampled process table and CPU before any build; `dotnet` id 55080 was active and CPU load was 100%, so no build was launched. Marked build as `[BLOCKED_BY_CONTENTION]` and continued with static checks.
Rejected Alternatives: Running `dotnet build` during active compiler/CPU contention; repeated incremental builds after each edit.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; integration host stability preserved.
Hardware Impact: Runtime 0 us. Prevented additional CPU contention on an already saturated host.

Problem: Task 16 needed proof of 5000-boid spatial query behavior without allocating GameObjects or scene scaffolding.
Solution: Added `EcosystemSwarmVault1419EditTests.cs`, seeding 5000 `SpatialGridEntryDTO`/`AmbientEntityAupDTO` records through `GlobalDataVault` handles, running 500 warmed `SpatialHashQuery.CollectEntitiesInRadius` passes, hashing results, checking write-lock fail-closed behavior, invalid-handle empty results, and `GC.GetAllocatedBytesForCurrentThread` deltas.
Rejected Alternatives: Play-mode scene fuzzer; managed arrays; lowering entity count; relying on visual swarm observation.
Scalability potential: Low validates minimum survival query path; Middle/High/Ultra validate deterministic query capacity without changing DTO identity.
Hardware Impact: Test-only. Runtime 0 us.

Problem: Primary `EcosystemTelemetryEntry` blackbox dump still used synchronous file I/O on the fault path.
Solution: Added `BufferID.ShinobuEcosystemDumpSnapshot = 70476` and `ShinobuEcosystemTelemetryForensics`; fault code copies the 300-frame telemetry ring into a Vault-owned byte snapshot under `TryAcquireWriteLock`/`finally ReleaseWriteLock`, then wakes a background thread to write `Dump_1419_EcosystemSwarm.bin` plus owner mirrors.
Rejected Alternatives: Keeping `BinaryWriter` on the simulation thread; writing a JSON dump; reusing the spatial-grid dump path/schema.
Scalability potential: Low devices avoid fault-time main-thread disk stalls; Middle/High/Ultra keep richer blackbox data without altering gameplay truth.
Hardware Impact: Normal path 0 us. Fault path moves direct disk write off the main simulation thread; estimated 0.2-3 ms avoided on slow storage during crash handling.

Problem: Adding `ShinobuEcosystemDumpSnapshot` touched `H8Memory.cs`, outside the narrow ecosystem file list.
Solution: Treated it as a critical cross-domain interface edit because `BufferID` is the single authoritative route map for Vault ownership; no duplicate local integer ID was invented.
Rejected Alternatives: Magic-number buffer IDs in ecosystem code; hijacking spatial-grid dump snapshot; unmanaged side pool.
Scalability potential: Low/Middle/High/Ultra keep one route and one owner for dump snapshot memory.
Hardware Impact: Runtime 0 us; prevents alias collisions and duplicate memory claims.

## 2026-05-28 Loop 5 Decisions

Problem: Existing layout guard did not assert every field for the active telemetry/custom lanes touched by this pass.
Solution: Expanded `ShinobuEcosystemLayoutManifest` with field-by-field offset assertions for `BoidCustomDataDTO`, `FlockingThreatDTO`, `FlockingTelemetryEntry`, all `FlockingCounter64` padding lanes, `AmbientEntityAupDTO`, `ShinobuEcosystemTuning`, and every `EcosystemTelemetryEntry` field.
Rejected Alternatives: Relying on `[StructLayout]` declarations alone; trusting HLSL stride comments without C# boot-time proof.
Scalability potential: Low avoids ARM64 unaligned traps; Middle/High/Ultra keep stable shader/job ABI while increasing swarm density.
Hardware Impact: One-time boot guard only; normal frame cost 0 us after verification.

Problem: Manual hot-path audit could be polluted by legitimate cold allocations and value-type `new` expressions.
Solution: Scanned only modified `Tick`, upload, and read-accessor ranges for managed `new`, string formatting/concatenation, LINQ, and managed `foreach`; classified job structs, `int2/int3/float4`, and `GraphicsBuffer.IndirectDrawIndexedArgs` as value-type construction, and excluded cold `GraphicsBuffer`, `FileStream`, `Thread`, and `AutoResetEvent` setup.
Rejected Alternatives: Full-file grep count as proof; claiming Unity Profiler results without running tests; deleting cold resource creation.
Scalability potential: Low keeps zero-GC simulation loops; Middle/High/Ultra can spend saved bandwidth on visual density and overkill culling.
Hardware Impact: Preserves the static 35-120 us bulk upload and 2-8 us single-boid event savings; no measured profiler sample due build gate.

Problem: Final proof needed to be machine-verifiable, not chat text.
Solution: Wrote `Docs/Reports/ECOSYSTEM_MEMORY_OPTIMIZATION_REPORT_1419.json` with deleted-field count, layout proof, locking proof, GPU upload proof, hot-path audit, build-gate state, and SHA-256 hashes of modified C# files.
Rejected Alternatives: Markdown-only report; omitting blocked build state; excluding `H8Memory.cs` hash after BufferID edit.
Scalability potential: N/A; proof artifact only.
Hardware Impact: Runtime 0 us.

## 2026-05-28 Loop 6 APEX Decisions

Problem: APEX self-audit found `EcosystemDirector.PublishFloraPredatorAupBufferImmediate` still writing the flora/predator AUP upload staging buffer through `VaultBufferView` mutable indexer access without a writer lock.
Solution: Added `VaultBufferView<T>.TryAcquireWriteLock(SystemID, out NativeArray<T>)` and `ReleaseWriteLock(SystemID)`, then rewrote `PublishFloraPredatorAupBufferImmediate` to acquire the locked view once, fill `float4` records, upload the locked NativeArray via `GraphicsBufferUploadUtility.UploadNativeArray`, and release the lock inside `finally`.
Rejected Alternatives: Keeping the mutable indexer because the upload buffer is short-lived; resolving the view per element; copying to a managed staging array before GPU upload. All three violate Data Sovereignty or Zero-GC discipline.
Scalability potential: Low keeps fail-closed lock behavior under DataVault contention; Middle/High/Ultra keep the same AUP truth route while scaling visual diffusion and swarm density through continuous `GlobalQualityWeight`, not through a binary low-tier branch.
Hardware Impact: Estimated direct frame gain is 0 us; the value is crash/deadlock prevention under DataVault compaction. It preserves the 35-120 us MX350 upload-path saving by keeping `LockBufferForWrite`/MemCpy routes intact.

Problem: The previous `ECOSYSTEM_MEMORY_OPTIMIZATION_REPORT_1419.json` became stale after the APEX AUP-lock fix changed `EcosystemDirector.cs`; `ECOSYSTEM_PIPELINE_AUDIT_1419.json` also retained the old telemetry DTO name in documentation text.
Solution: Updated the stale `EcosystemDirector.cs` SHA-256 in the memory report, changed the pipeline audit entry to `EcosystemTelemetryEntry 64B`, and wrote canonical APEX proof to `Docs/Reports/ECOSYSTEM_APEX_FINAL_VERIFICATION_1419.json` with sidecar SHA-256.
Rejected Alternatives: Leaving contradictory proof artifacts; deleting prior reports; claiming the chat report superseded disk evidence.
Scalability potential: N/A; proof consistency only.
Hardware Impact: Runtime 0 us; reduces integrator time wasted on stale evidence.

Problem: Post-APEX scan found an unused cold managed `BoidData[1]` field in `SargassumMicroFaunaBoids` with a stale SetData-staging comment.
Solution: Removed `_singleBoidUpload`; `rg` showed no reads or writes, and the active single-boid upload path already writes through `LockBufferForWrite<BoidData>`.
Rejected Alternatives: Keeping a dead managed array because it was cold; changing it to another staging object; touching unrelated boid spawn state.
Scalability potential: Low/Middle/High/Ultra unchanged; it removes stale memory/explanation surface and preserves the mapped GPU write route.
Hardware Impact: Removes one cold managed `BoidData[1]` allocation and one misleading SetData route marker; hot-path gain 0 us.

Problem: Final compilation proof was requested, but the final host sample still violated the build throttle rule.
Solution: Sampled CPU/process state before any build. Final sample: `2026-05-28T01:40:49.9660188Z`, CPU load `100%`, csc id `67916`, dotnet id `20440`. Build was not launched because CPU was above 50% and compiler/dotnet processes were active; static checks and JSON validation were used.
Rejected Alternatives: Running `dotnet build` during a 100% CPU sample; claiming Unity/runtime GC proof from static analysis.
Scalability potential: N/A; build gate only.
Hardware Impact: Prevented additional host contention. Runtime proof remains pending until Unity/Profiler/GCMonitor can run legally.
