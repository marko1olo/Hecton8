# Rationale_SHINOBU_01

Status: CORE COMPLETE / COMPILE BLOCKED BY EXTERNAL NON-SHINOBU DEPENDENCIES

Problem: GlobalDataVault mutation callers need stable in-place access without CS1612 struct-copy traps.
Solution: Add ref-return and raw slice accessors over the existing vault pointer with generation validation, bounds assumptions, and no managed allocation.
Rejected Alternatives: Returning NativeArray elements by value keeps C# copy semantics; class wrappers would add heap identity and virtual dispatch.
Scalability potential: Low uses ref hot/cold streams only; Middle keeps full gameplay truth; High/Ultra spends saved CPU in VISUAL_SYNC, not extra simulation.
Hardware Impact: i3/MX350 estimated save is sub-microsecond per thousand direct mutations by deleting stack-copy and bounds overhead; ARM64 avoids unaligned Pack=1 runtime access where edited.

Problem: OSHINO binary files may be missing under concurrent batch execution.
Solution: Cold archaeology parser reads legacy headers by raw offsets and falls back to aligned mock layout config.
Rejected Alternatives: Waiting for OSHINO agents blocks the batch; throwing FileNotFoundException turns optional data into a boot failure.
Scalability potential: Low receives safe small capacities; Ultra can expand arena limits through config/CSV without changing code.
Hardware Impact: Cold path only; prevents crash and preserves 60 FPS frame route.

Problem: Deleted vault entities can leave dirty data for parallel readers.
Solution: Tombstone with stable index, alive-mask bit clear, and UnsafeUtility.MemClear of the element payload.
Rejected Alternatives: O(N) array shifting during deletion invalidates peer indices and burns cache bandwidth.
Scalability potential: Low defers compaction; High/Ultra can compact more aggressively at phase boundaries.
Hardware Impact: 64B clear is cache-line cheap; avoids scanning/shift spikes on low-end silicon.

Problem: Memory starvation must not allocate in a Burst/simulation failure path.
Solution: Pre-owned emergency overflow slice plus read-only dummy pointer and warning bitmask.
Rejected Alternatives: New NativeArray on failure is a GC/native allocation spike; hard crash loses telemetry.
Scalability potential: Low drops cosmetic work; Ultra can reserve larger overflow if memory profiler permits.
Hardware Impact: Failure path remains deterministic on i3/MX350; expected gain is avoiding a frame hitch/crash, not reducing steady-state CPU.

Problem: Cache-aligned slices must not pay a whole-buffer finite scan on every hot acquisition.
Solution: Keep legacy GetBuffer/GetBufferHandle sanitation, but pass sanitizeFinite:false for TryAcquireSlice so the caller gets a 64-byte aligned raw slice and owns first-write initialization under UninitializedMemory.
Rejected Alternatives: Global removal of finite sanitation would weaken existing black-box NaN defense; full scan in slice path can exceed the 0.1 ms suspicion line.
Scalability potential: Low avoids scanning cold payloads; Middle/High/Ultra spend saved CPU on visual systems while still using validated handles where needed.
Hardware Impact: i3/MX350 saves O(N) buffer scan cost per hot slice; ARM64 receives aligned 64B block starts without Pack=1 traps.

Problem: Defrag relocation budget was too permissive for the 1024 bytes/frame task constraint.
Solution: Hard cap MaxLiveDefragMoveBytesPerSlice at 1024 bytes and keep relocation record emission after every MemMove.
Rejected Alternatives: 5 MB relocation slices risk visible frame spikes; unbounded compaction is banned under Frame Time Dictatorship.
Scalability potential: Low performs slow invisible cleanup; High/Ultra may run the same deterministic cap more often at safe phase boundaries.
Hardware Impact: i3/MX350 avoids multi-millisecond relocation spikes; high-end devices keep deterministic pointer-rebase behavior.

Problem: CSV memory overrides required key hashing, not string comparison chains.
Solution: Parse CSV as bytes, trim spans, FNV-mix lowercase ASCII keys to uint constants, and switch on hashes before writing the layout config into the vault.
Rejected Alternatives: string.Split/string keys allocate managed garbage; OSHINO-only regeneration blocks rapid QA tuning.
Scalability potential: Low can clamp to small arena/entity limits; Ultra can raise limits through data without code edits.
Hardware Impact: Cold/debug path only; eliminates avoidable managed allocations during override ingestion.

Problem: CSV override existed as a utility but needed a real human bridge for running projects.
Solution: Apply root `memory_overrides.csv` during GameBootstrapper vault registration after authored config, and expose a Vault X-Ray editor-only reload button for live vault sessions.
Rejected Alternatives: Adding a runtime Update poller would violate the dispatcher model and create repeated filesystem checks; designer-only reload belongs in the editor bridge.
Scalability potential: Low can immediately clamp capacity for toaster tests; Ultra can raise visual memory headroom without C# recompilation.
Hardware Impact: 0 us in simulation; one cold/editor file read only when booting or explicitly reloading.

Problem: Test assembly references are not safe to treat as transitive.
Solution: Add an explicit Hecton8.Core.Memory reference to Hecton8.EditModeTests so vault surgery tests bind directly to the memory contract assembly.
Rejected Alternatives: Depending on Hecton8.Core to re-export Memory creates brittle generated-project behavior.
Scalability potential: No runtime effect; compile graph stays explicit for all device tiers.
Hardware Impact: 0 us runtime.

Problem: Ultra-polish audit found SHINOBU's compile contract range had drifted into non-memory BufferID space under concurrent edits.
Solution: Restore VaultBufferContract.MaxBufferId to BufferID.VaultSharedTransformMatrices and add an editor test asserting the exact upper boundary.
Rejected Alternatives: Expanding ownership over Biolum/Save/VFX enum values would create false authority and compile graph coupling outside CORE_MEMORY_MUTABILITY_SURGEON.
Scalability potential: Low/Middle/High/Ultra all get deterministic binary contracts with no cross-domain surprise writes.
Hardware Impact: 0 us runtime; prevents memory contract drift that would cost debugging time on all hardware tiers.

Problem: DTO structs had correct sizes but still relied on implicit padding for the final ARM64 cache-line layout.
Solution: Add explicit padding fields and byte-offset constants/tests for VaultMemoryLayoutConfig, VaultAup64, VaultHotEntityData, VaultColdEntityData, and VaultTransformAlias.
Rejected Alternatives: Trusting StructLayout(Size=64) alone hides alignment mistakes until ARM64 or Burst inspection.
Scalability potential: Low devices avoid unaligned memory traps; Ultra keeps predictable cache-line traversal for higher visual budgets.
Hardware Impact: Quest/ARM64 risk reduction is the main gain; i3/MX350 estimate is 0.02-0.05us per thousand hot entity reads from more predictable cache access.

Problem: Blackbox telemetry did not record the new starvation and mutation-guard state.
Solution: Extend MemoryDefragTelemetryEntry to record ActiveMutationGuardMask, EmergencyOverflowCursorBytes, and MemoryStarvationWarnings inside the fixed 128-byte 300-frame ring.
Rejected Alternatives: Separate managed logs or strings in failure paths would violate Zero-GC and lose deterministic postmortem ordering.
Scalability potential: Low devices can prove fallback frequency; Ultra devices can correlate aggressive visual memory pressure with vault starvation.
Hardware Impact: Fixed-size write only, estimated below 0.01us per heartbeat; crash diagnosis improves without allocation.

Problem: Post-polish compile verification is blocked before SHINOBU files are fully evaluated by external Power missing-type resolution.
Solution: Record the current external compile wall and preserve SHINOBU static checks/diff checks; do not mutate Power from the memory domain.
Rejected Alternatives: Editing PowerGridManager or Power assembly coverage without assignment risks architectural sabotage and masks the real owner dependency.
Scalability potential: No runtime effect; protects integration hygiene under 20+ concurrent agents.
Hardware Impact: 0 us runtime.

Problem: Vault legacy archaeology still used full-file `File.ReadAllBytes` for CSV and legacy binary headers.
Solution: Replace full-file byte[] reads with fixed span streaming: 48-byte legacy header reads through FileStream + BinaryPrimitives, and CSV override parsing through 1024-byte stream chunks plus a 256-byte line scratch.
Rejected Alternatives: Keeping `File.ReadAllBytes` was acceptable only as cold-path convenience, not as titanium I/O discipline; memory-mapping a tiny 48-byte header would be over-engineered.
Scalability potential: Low/Steam Deck avoids unnecessary MicroSD burst reads and managed file-sized allocations; Ultra can still reload larger configs without changing code.
Hardware Impact: Simulation path remains 0 us; cold/editor reload avoids one file-sized managed byte[] allocation and caps parser scratch to 1280 stack bytes.

Problem: Project-wide Pack=1 scan found heavy Pack=1 debt outside SHINOBU ownership.
Solution: Keep SHINOBU Core.Memory clean and document non-owned debt instead of mass-editing GlobalSignals/Save/World contracts.
Rejected Alternatives: Cross-domain Pack=1 purge would change binary contracts and signal/save wire formats under concurrent agents.
Scalability potential: SHINOBU buffers are ARM64-safe now; remaining project-wide Pack debt needs owning-domain passes.
Hardware Impact: 0 us from documentation; prevents accidental ABI breakage while surfacing the ARM64 risk.

Problem: Contract readback found `VaultBufferContract.MaxBufferId` overwritten again into a peer inventory BufferID range.
Solution: Restore the upper bound to `BufferID.VaultSharedTransformMatrices`, keep the EditMode assertion, and add an inline ownership comment naming SHINOBU range 550-555.
Rejected Alternatives: Accepting the wider enum range would make the memory contract lie about ownership and invite cross-domain writes into non-vault buffers.
Scalability potential: All tiers get deterministic vault layout negotiation; peer inventory/logistics ranges remain outside this binary contract.
Hardware Impact: 0 us runtime; avoids integration-time data corruption.

Problem: AUP local resolver treated sector delta as one meter and only subtracted local doubles before float downcast.
Solution: Route sector scaling through `HectonPhysicsContract.AupSectorSizeMetersDouble` and resolve camera-relative double meters before any float cast; add an edit test proving 1 sector = 5000m plus local delta.
Rejected Alternatives: Guessing a 1m sector keeps math cheap but corrupts 100km authority; importing World `AUPMath` would create a Core.Memory -> World dependency wall.
Scalability potential: Low uses the same authority math with sparse probes; High/Ultra can spend the saved correctness headroom on denser presentation caches after the camera-relative delta is stable.
Hardware Impact: CPU cost is three double multiplies/adds per resolved entity, estimated below 0.04us per 100 entities on i3/MX350; precision correctness prevents far more expensive jitter/debug loops on ARM64 and PC.

Problem: Task 05 mock signal surface still lived in a runtime folder and could fragment the signal corridor.
Solution: Fence `VaultMockSignalBus` and `VaultMemoryAddressShiftSignal` behind `UNITY_EDITOR || UNITY_INCLUDE_TESTS`; runtime relocation now remains the existing `GlobalSignals.MemoryAddressShiftSignal` path published by `SystemDispatcher`.
Rejected Alternatives: Deleting the mock would violate the original blind-test task; keeping it in player builds creates duplicate signal semantics.
Scalability potential: Low/Middle/High/Ultra all avoid a duplicate runtime NativeQueue lane; editor/test coverage keeps isolated queue behavior available without player coupling.
Hardware Impact: 0 us in player runtime because the mock type is not compiled there; eliminates a possible cache-lane split.

Problem: Fatal vault dumps used only legacy `.bin` domain names while the active telemetry mandate also requires Agent-ID `.h8dump` evidence.
Solution: Mirror the same fixed 300-frame blackbox payload to `Docs/AgentLogs/Dump_SHINOBU_01.bin` and `Docs/AgentLogs/Dump_SHINOBU_01.h8dump` on defrag/PHI-VOD fatal state.
Rejected Alternatives: Renaming the legacy dump files would break existing postmortem scripts; writing managed string diagnostics would violate Zero-GC failure discipline.
Scalability potential: Low devices get deterministic crash forensic output; Ultra keeps identical payloads for deeper tooling without changing the runtime ring.
Hardware Impact: 0 us steady-state; extra disk writes happen only on fatal dump paths.

Problem: Current compile verification no longer reaches a clean graph, but the failing files are outside Core.Memory.
Solution: Record the restore-backed Fauna compile wall and the latest no-restore Gameplay compile wall; keep SHINOBU static gates and do not patch Fauna/Gameplay from the memory domain.
Rejected Alternatives: Chasing `PredatorCognitionDomain` or `SomaticKinematicsRuntime` would cross ownership boundaries and hide the actual integrator work.
Scalability potential: No runtime effect; protects batch isolation under concurrent agents.
Hardware Impact: 0 us runtime.

Problem: Ultra-polish layout review still found implicit compiler tail padding in SHINOBU-adjacent runtime structs.
Solution: Convert the remaining hidden holes into named reserved fields in `VaultBufferMeta`, `VaultMemoryBlockSnapshot`, and `HectonArenaAllocator.NativeArenaSlice<T>`, then assert public runtime DTO sizes and byte offsets in EditMode tests.
Rejected Alternatives: Trusting `StructLayout(Size=...)` alone would keep ARM64 correctness dependent on invisible compiler padding and make future field edits impossible to audit from source.
Scalability potential: Low/ARM64 avoids unaligned metadata reads; High/Ultra keeps deterministic cache traversal for denser vault visualization and defrag inspection.
Hardware Impact: 0 us steady-state on x64; Quest/ARM64 risk reduction is the gain. Metadata/slice reads remain 8-byte aligned and size-multiple-of-8 verified.

Problem: H8Memory sentinel fatal dumps wrote only the legacy `.bin` payload while the blackbox mandate expects `.h8dump` evidence for postmortem tooling.
Solution: Mirror the existing fatal leak blackbox payload to `Dump_SENTINEL_DISPOSAL_GUARD.h8dump` without renaming or removing the legacy `.bin` dump.
Rejected Alternatives: Replacing the legacy path would break current sentinel scripts; adding managed diagnostic strings would allocate in an already fatal path.
Scalability potential: Low devices get deterministic binary postmortem evidence; Ultra tooling can consume `.h8dump` without a separate conversion step.
Hardware Impact: 0 us steady-state; additional file write runs only on fatal leak dump.

Problem: Current compile verification is now blocked by a different external wall before a clean SHINOBU-only proof can be produced.
Solution: Record the current `GlobalTelemetryBus`, `SpatialAudioManager`, and `AI/Ecosystem/ShinobuEcosystemBalancer` errors and stop at the domain boundary; SHINOBU static gates stay clean.
Rejected Alternatives: Patching telemetry/audio/ecosystem from CORE_MEMORY_MUTABILITY_SURGEON would violate the assigned domain and hide ownership debt from the integrator.
Scalability potential: No runtime effect; preserves batch isolation under concurrent agents.
Hardware Impact: 0 us runtime.

Problem: Current-disk audit found `VaultBufferContract.MaxBufferId` drifted again into a peer buffer range (`FloraGenomeCsvScratch`), which would falsely grant SHINOBU authority over non-vault buffers.
Solution: Restore `MaxBufferId` to `BufferID.VaultSharedTransformMatrices` and preserve the inline comment that SHINOBU owns only BufferID range 550-555.
Rejected Alternatives: Covering the shared enum high-water mark would hide cross-domain ownership violations and invite foreign writers into the vault binary contract.
Scalability potential: Low/Middle/High/Ultra all get a stable vault contract that cannot silently absorb peer ranges during concurrent batch churn.
Hardware Impact: 0 us runtime; prevents data corruption and compile/debug churn rather than saving frame time.

Problem: Layout tests still did not cover internal runtime ABI structs that drive allocation metadata, defrag telemetry, and arena allocations.
Solution: Add editor-only reflection ABI tests for `VaultBufferMeta`, `VaultArenaBlock`, `MemoryDefragTelemetryEntry`, `VaultBufferHandle<byte>`, `VaultBufferSlice<byte>`, `NativeArenaSlice<byte>`, and nested `ArenaAllocation` byte offsets.
Rejected Alternatives: Exposing internals publicly would widen the runtime API; relying on public DTO tests alone misses the exact metadata fields that ARM64 reads in memory-management paths.
Scalability potential: Low/ARM64 avoids hidden unaligned metadata drift; Ultra keeps deterministic defrag/vault inspection under larger visual memory pressure.
Hardware Impact: 0 us runtime; editor-only tripwire. The target gain is preventing ARM64 regressions before they ship.

Problem: Latest compile verification moved to a different external wall while SHINOBU files remain absent from the error list.
Solution: Record the new `GlobalPhysicsStateManager` / `WakeRequestSignal` boundary and stop at the domain line instead of modifying Physics signals from the memory agent.
Rejected Alternatives: Inventing or patching `WakeRequestSignal` in Core.Memory would fragment the signal corridor and violate domain ownership.
Scalability potential: No runtime effect; preserves clean assembly ownership while still proving current SHINOBU edits are not the visible compiler blocker.
Hardware Impact: 0 us runtime.

Problem: The new ABI tests need Unity/EditMode compilation proof, but the current generated CLI surface does not contain `Hecton8.EditModeTests.csproj`.
Solution: Source-review the test file, keep the `.asmdef` reference path intact, remove the useless missing-project log, and record that Unity Editor/Test Runner proof is still absent.
Rejected Alternatives: Creating a synthetic test `.csproj` would not match Unity asmdef import semantics and could produce a fake pass.
Scalability potential: No runtime effect; preserves evidence hygiene instead of manufacturing a misleading test artifact.
Hardware Impact: 0 us runtime.

Problem: Current-disk audit found `VaultBufferContract.MaxBufferId` widened again to `FloraGenomeCsvScratch`, proving a raw enum high-water constant is too easy to corrupt under concurrent batch edits.
Solution: Define the six SHINOBU-owned BufferID constants explicitly, set `OwnedBufferCount = 6`, derive `MaxBufferId` from `MinBufferId + OwnedBufferCount - 1`, and add `OwnsBufferId(BufferID)` as a branchless ownership check.
Rejected Alternatives: Tracking the shared `BufferID` enum high-water mark would make SHINOBU claim peer memory ranges; adding a separate dependency guard in another assembly would widen the compile surface.
Scalability potential: Low/Middle/High/Ultra all receive a stable vault ABI that cannot silently include Flora/Inventory/VFX/Save ranges during concurrent merges.
Hardware Impact: 0 us runtime for constants; `OwnsBufferId` is one subtract and unsigned compare if used by callers. The gain is corruption prevention and compile-debug time, not frame-time savings.

Problem: Previous tests only asserted the final `MaxBufferId`, not every BufferID included in the contiguous vault contract.
Solution: Expand `VaultSurgeryEditTests` to assert all six SHINOBU-owned BufferID constants, the exact owned count, Min/Max, positive ownership for first/last vault IDs, and negative ownership for `FloraGenomeCsvScratch`.
Rejected Alternatives: Relying on one Max assertion let the contract comment drift without proving individual range membership; runtime reflection or editor scanners would not protect compile-time consumers.
Scalability potential: No runtime effect; all tiers benefit from deterministic binary layout and ownership checks before player/runtime proof.
Hardware Impact: 0 us runtime; editor-only tests catch ABI drift before it reaches ARM64 or Steam Deck builds.

Problem: Latest compile verification is blocked by non-SHINOBU code before a clean Core graph can be produced.
Solution: Record the current external walls: Agent 37 `GlobalPhysicsStateManager` missing physics-culling partial/state methods and `WorldChunkResidencyManager` calling missing `IAmbientBiotaService.IsApexInSector`; do not patch Physics or World from CORE_MEMORY_MUTABILITY_SURGEON.
Rejected Alternatives: Creating stubs for Agent 37 or Biota interfaces in Core.Memory would fracture ownership and hide the integrator's dependency debt.
Scalability potential: No runtime effect; preserves domain isolation for 20+ concurrent agents.
Hardware Impact: 0 us runtime.

Problem: `NativeArenaArray<T>` had natural player-build layout but still depended on compiler tail padding after `_frameSequence`.
Solution: Add explicit `_pad0` and initialize it in `Create`, while leaving the struct without a fixed `Size` because `ENABLE_UNITY_COLLECTIONS_CHECKS` injects Unity safety fields in editor/debug builds.
Rejected Alternatives: Applying a single fixed `StructLayout(Size=32)` would break debug/editor safety-handle layout; moving safety fields behind a separate wrapper would be a larger allocator refactor outside this task.
Scalability potential: Low/ARM64 gets source-auditable 8-byte multiple layout in player builds; Middle/High/Ultra retain Unity safety diagnostics in editor without ABI lies.
Hardware Impact: 0 us steady-state; this removes hidden padding ambiguity rather than changing instruction count.

Problem: Compile verification changed to a missing UI source file after the latest edit.
Solution: Record `Assets/_Project/Scripts/UI/CharBufferPool.cs` missing from `Hecton8.Core.csproj` as the current external compile wall; do not recreate a UI pool from the memory domain.
Rejected Alternatives: Inventing `CharBufferPool` would cross into UI/zero-GC text ownership and could conflict with the real owner implementation.
Scalability potential: No runtime effect; preserves ownership and makes the integrator-facing failure precise.
Hardware Impact: 0 us runtime.
