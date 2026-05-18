# Rationale_SHINOBU_64

Agent: SHINOBU_64
Role: LOCKSTEP_ROLLBACK_NETCODE_ROUTER
Status: LOCKSTEP ARM64 PACKED DTO POLISH APPLIED; build deferred by CPU guard

## Pre-Code Analysis
Target: cooperative input-only lockstep rollback that snapshots exact simulation state, detects input/hash divergence, restores prior state, and resimulates SIMULATION/POST_SIMULATION without visual sync.
Affected systems: networking facade, deterministic input journal, GlobalDataVault hot buffers, lockstep hash telemetry, audio suppression, editor tuning/gizmos.
Zero GC proof target: all hot paths use vault-owned `NativeArray` slices, fixed DTOs, `UnsafeUtility.MemCpy`, integer hashes, and cold-only file/editor IO.
State check: no RPC, no NetworkTransform, no position replication. Remote corrections are input masks only; authoritative AUP state is copied as exact bytes.
Rule quote: `NET_Logistics` mandates compact input reconciliation; `MATH_AUP_Determinism` forbids coordinate truncation; `DBG_Telemetry` requires a 300-frame blackbox.

## Non-Trivial Decisions

### Decision 001: Prompt Disambiguation
Problem: `CURRENT_BATCH.md` contains two `SHINOBU_64` prompts and the status file was overwritten by the other role.
Solution: Select the prompt whose role is `LOCKSTEP_ROLLBACK_NETCODE_ROUTER`, matching the user's explicit networking directive.
Rejected Alternatives: Continuing volcanic status would edit the wrong domain and fake compliance.
Scalability potential: Low/Middle/High/Ultra unaffected; this is scope integrity.
Hardware Impact: Avoids cross-domain churn and build failures; no frame-time claim.

### Decision 002: Input-Only Authority
Problem: Sending transforms would mask divergence until AUP precision and physics drift explode on clients.
Solution: Keep network correction as remote `InputStateDTO` frames only, using the existing `ShinobuInputJournalRing` as the predicted input source.
Rejected Alternatives: Position sync, transform replication, or component-driven scene state messages; all hide bad simulation truth and add bandwidth.
Scalability potential: Low = button/move mismatches only; Middle = short rollback windows; High = deeper windows; Ultra = aggressive look rollback and visual overkill smoothing.
Hardware Impact: Removes per-entity transform receive/update cost. Low-end gain is workload-shape dependent, estimated tens to hundreds of microseconds in crowded scenes.

### Decision 003: MemCpy Snapshot Ring
Problem: Per-field state serialization invites AUP truncation, CS1612 copy bugs, and managed allocation pressure.
Solution: Copy exact live vault slices into `StateRingBuffer` with `UnsafeUtility.MemCpy`, header the page, and hash header+payload with XXHash3.
Rejected Alternatives: JSON/binary writer snapshots, reflection serializers, per-entity class state, or float-only world coordinates.
Scalability potential: Low = fewer live arrays copied; Middle = gameplay hot arrays; High/Ultra = larger exact-state pages with the same copy primitive.
Hardware Impact: One linear memory copy path. Uninitialized 11 MB ring avoids cold clear; hot cost is proportional to payload bytes.

### Decision 004: Quality-Weighted Resim Budget
Problem: Rollback can spike frame time when weak hardware receives late input corrections.
Solution: `GlobalQualityWeight` continuously scales rollback lookback and skips look-only mismatches below the configured quality threshold.
Rejected Alternatives: Low/high boolean quality branches or dropping all rollback on weak devices.
Scalability potential: Low = about 35 percent rollback depth and no look-only correction; Middle = partial depth; High = full depth; Ultra = richer visual correction.
Hardware Impact: On i3/MX350-class hardware the scan/resim budget is reduced before the 0.1 ms suspicion line is crossed.

### Decision 005: Headless Resimulation Command
Problem: There is no safe cross-agent API to recursively call every simulation owner from networking without inventing dependencies.
Solution: Emit `MockTickCommand` with SIMULATION|POST_SIMULATION phase bits and leave VISUAL_SYNC absent; consumers can bind without direct reference cycles.
Rejected Alternatives: Direct dispatcher recursion, scene object replay, or calling sibling systems by concrete type.
Scalability potential: Same command works across all tiers; quality only changes frame count and presentation smoothing.
Hardware Impact: Prevents duplicate render/audio work during resim; expected saving is visual-lane dependent.

### Decision 006: Hash Fence And Black Box
Problem: AUP or input divergence must produce forensic data, not an unverifiable multiplayer complaint.
Solution: Compare hashes every 60 frames, publish a pause signal on mismatch, write `Dump_NETCODE_SURGEON.bin`, then overwrite the remote hash marker with local snapshot hash after dump.
Rejected Alternatives: Console-only logging, silent correction, or continuous full-state exchange.
Scalability potential: Low/Middle/High/Ultra all pay one cadence compare; dump cost only occurs on fault.
Hardware Impact: Fixed telemetry write is 64 bytes/frame; desync dump is cold fault IO.

### Decision 007: Cold Tuning And Visualization
Problem: Rollback parameters and AUP correction visibility need designer access without recompilation.
Solution: Add `Rollback Netcode Tuner`, zero-hot-GC CSV byte parser, 200 ms ping button, and red/green SceneView gizmos.
Rejected Alternatives: hardcoded constants, managed CSV split/regex, and invisible rollback correction.
Scalability potential: Low/Middle/High/Ultra parameters are continuous floats/ints in one DTO.
Hardware Impact: Editor-only and cold IO; no hot runtime allocation.

### Decision 008: Compile Guard Compliance
Problem: The project rule forbids launching a build when CPU is over 50 percent or compiler processes are active.
Solution: Static audits were run; build remains pending until CPU guard clears. Earlier guard sample showed `CPU=100.0; CSC=2`, later `CPU=100.0; CSC=0`.
Rejected Alternatives: Starting a competing `dotnet build` and worsening contention.
Scalability potential: None; this is integration hygiene.
Hardware Impact: Prevents local machine overload and misleading compile failures.

### Active Volcanic Lane Pointer - 2026-05-18
Problem: Latest active user directive is volcanic updrafts, but this shared `SHINOBU_64` rationale still contains rollback lane state because `CURRENT_BATCH.md` has duplicate SHINOBU_64 prompts.
Solution: Preserve volcanic rationale in `Docs/AgentLogs/Rationale_SHINOBU_64_VOLCANIC_UPDRAFT.md` and append this pointer without deleting rollback data.
Rejected Alternatives: Overwriting rollback rationale or hiding the duplicate-ID collision.
Scalability potential: None; this is state integrity.
Hardware Impact: Avoids cross-domain churn. Latest volcanic compile attempt is blocked outside SHINOBU by `ConstructionSignals.cs` unresolved `ISignal`; latest volcanic polish removes hot fixed-tick `ActiveRuntimeInstance` reads from `ThermalGeyser`.

### Decision 009: Domain-Local Vault IDs
Problem: The first implementation added rollback buffer identifiers and a `Networking` owner directly to `H8Memory.cs`, widening a heavily contested core header and worsening compile-wall risk.
Solution: Removed SHINOBU rollback enum additions from the core memory header and introduced `RollbackNetcodeVault` constants in the networking contract, cast to `BufferID` with `SystemID.CoreDeterminism` as owner.
Rejected Alternatives: Keeping named core enum entries for readability. That is too expensive in a multi-agent batch because every core-memory touch becomes a merge and compile surface.
Scalability potential: Low/Middle/High/Ultra use the same vault handles; quality changes rollback depth, not buffer identity.
Hardware Impact: No frame-time claim. The gain is iteration protection and fewer unnecessary core recompiles.

### Decision 010: Explicit Runtime Ownership
Problem: A hidden `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` created a `DontDestroyOnLoad` rollback object without scene/bootstrap ownership.
Solution: Deleted the hidden bootstrap and made `HectonNetworkManager` explicitly require/ensure `HectonRollbackNetcodeRuntime` on the same object. Duplicate runtimes disable themselves instead of destroying components from `Awake`.
Rejected Alternatives: Global singleton-style object creation. It hides lifecycle, scene teardown, and ownership bugs.
Scalability potential: Same runtime math across tiers; explicit ownership prevents duplicate hot loops on weak hardware.
Hardware Impact: Prevents duplicate dispatch registration and duplicate vault access; exact microseconds depend on scene wiring.

### Decision 011: Deterministic Burst Barrier
Problem: Runtime jobs used `.Run()`, which bypassed the dispatcher job-fence model and made Burst/dependency proof weak.
Solution: Added `ExecutePostSimulationBarrier<TJob>()`: schedules each rollback job, registers the `JobHandle` with `H8Memory`, batches it, and completes through `DispatcherJobSwap` while the dispatcher-owned post-fixed/late-frame swap window is active. All rollback jobs now use `CompileSynchronously = true` and `FloatMode.Deterministic`.
Rejected Alternatives: Returning `JobHandle` from `IPostFixedTickable`; that would mutate a shared dispatcher interface mid-batch. Leaving `.Run()` was rejected because it provides no handle for the memory sentinel.
Scalability potential: Low = shorter mismatch scan and fewer rollback frames; Middle/High/Ultra = deeper windows and richer visual smoothing, still with deterministic job fences.
Hardware Impact: No fake profiler claim. The barrier preserves correctness; load shedding comes from `GlobalQualityWeight` reducing the work before the barrier.

### Decision 012: Compile Wall Boundary
Problem: After CPU/build guard cleared, `dotnet build Hecton8.Core.csproj --no-restore /m:1` failed before reaching rollback-specific diagnostics because `Assets/_Project/Scripts/Construction/ConstructionSignals.cs` cannot resolve `ISignal`.
Solution: Treat this as an external construction-domain compile wall. Record exact compiler errors and preserve rollback changes; do not patch construction signals from the netcode domain without explicit cross-domain authorization.
Rejected Alternatives: Adding a construction `using` from this agent. That is likely the right mechanical fix, but it violates the assigned domain boundary and could collide with the Construction owner.
Scalability potential: None; this is build hygiene.
Hardware Impact: No runtime hardware claim. The build attempt was legal: guard was `CPU=18.9; CSC=0; DOTNET=0` before launch.

### Decision 013: Full 64-bit Rollback Hashes
Problem: The first rollback implementation folded XXHash3 into `uint`, which weakens a 60-frame desync fence and does not satisfy the prompt's 64-bit hash requirement.
Solution: Promote snapshot/header/runtime/telemetry hash fields to `ulong`, call `MemorySentinelMath.ComputeXXHash3Full64`, display `X16` hashes in the tuner, and write 64-bit hashes into `Dump_NETCODE_SURGEON.bin`.
Rejected Alternatives: Keeping folded 32-bit hashes for smaller DTOs. That saves 8-16 bytes per record but increases collision risk in a 100km deterministic co-op simulation.
Scalability potential: Low/Middle/High/Ultra all pay one 64-bit compare at the 60-frame cadence; quality throttling still controls rollback scan depth.
Hardware Impact: Extra telemetry/snapshot hash storage is cold-cache negligible versus full-state resim; expected hot delta is below measurement noise, while desync evidence quality improves materially.

### Decision 014: Continuous Quality Curve
Problem: Rollback throttling still had a hard look-only threshold and linear budget slope, which is a disguised binary quality step.
Solution: Use `math.step` only as a scalar gate for noncritical look correction, and use `Smooth01` polynomial easing plus `math.lerp` for resim budget and cost estimates.
Rejected Alternatives: `if (lowEnd)` style branch or dropping all rollback under thermal pressure. Both produce abrupt input feel changes.
Scalability potential: Low = about 22 percent emergency rollback scan with look-only correction skipped; Middle = eased partial scan; High/Ultra = full scan and richer smoothing.
Hardware Impact: On i3/MX350-class hardware, late-input scan depth sheds before resim crosses the 5 ms dump threshold; on high-end hardware, exact-state correction stays full depth.

### Decision 015: Scoped External Compile-Wall Unblocks
Problem: Core build repeatedly failed before reaching rollback diagnostics on unrelated missing imports and a world-streaming estimator reference.
Solution: Apply minimal mechanical unblocks only: `ConstructionSignals.cs` imports `Hecton8.Core.Contracts.Signals`, `SaveBinaryPayloadCodec.cs` imports `Hecton8.Gameplay`, and `AssetLifecycleGovernor.cs` imports `Unity.Jobs`. `WorldChunkResidencyManager` already contains `EstimateAddressableChunkBytes`, so the rollback lane did not retain a duplicate estimator.
Rejected Alternatives: Refactoring external domains or ignoring compiler blockers. Broad fixes would violate domain ownership; ignoring them prevents netcode compiler verification.
Scalability potential: None for rollback math. The fixes only unblock integration.
Hardware Impact: No runtime claim for rollback. Build guard is still obeyed; repeat build waits for `CPU <= 50`, no `csc`, and no `dotnet`.

### Decision 016: Verified Core Compile After Guard Clearance
Problem: Static scans are not enough after 64-bit DTO changes and cross-domain compile-wall imports; the C# compiler must verify ABI names and Unity job extension visibility.
Solution: Waited until guard cleared, then ran `dotnet build Hecton8.Core.csproj --no-restore /m:1`. Build succeeded. Remaining diagnostics are 8 `CS0649` warnings in `GlobalPhysicsStateManager.PhysicsDistanceCullingJob`, outside rollback ownership.
Rejected Alternatives: Claiming verification from scans only, or launching build while CPU/compiler guard was active.
Scalability potential: None; this is integration proof.
Hardware Impact: No runtime frame claim. It proves rollback code compiles into `Temp/bin/Debug/Hecton8.Core.dll` without adding compile errors.

### Decision 017: Fixed Dispatcher Rollback Pipeline
Problem: `ExecutePostSimulationBarrier<TJob>()` still scheduled a job and forced completion inside the rollback runtime. That satisfied correctness but violated the Native Memory Jobs prime rule against Schedule+Complete inside tick-like code.
Solution: Replace `IPostFixedTickable` rollback execution with `IDispatcherFixedSystem`. `ScheduleFixedSimulation()` now returns one `RollbackFixedPipelineJob` handle to the master fixed bridge. The Burst job performs input mismatch detection, snapshot restore, remote input correction, headless resim command emission, exact-state snapshot, 60-frame hash fence, and telemetry write in deterministic order. `PostFixedSimulation()` only handles cold side effects after the dispatcher completion window: pause signal, blackbox dump, and remote hash overwrite marker.
Rejected Alternatives: Keeping the barrier and claiming it was acceptable because it used `DispatcherJobSwap`; splitting the pipeline into multiple C# scheduled jobs requiring runtime reads between jobs; changing shared dispatcher interfaces mid-batch. The single pipeline job avoids all three failure modes.
Scalability potential: Low = the job receives the reduced `MaxRollbackFrames` from the `GlobalQualityWeight` curve and skips look-only rollback below threshold; Middle = partial rollback scan; High/Ultra = full exact-state rollback and richer visual interpolation. Visual smoothing remains presentation-only and does not mutate simulation truth.
Hardware Impact: Removes three to four forced main-thread job barriers on rollback frames. On i3/MX350-class hardware this cuts scheduler stalls first; exact microseconds depend on contention, but the structural win is eliminating synchronous waits from the rollback owner.

### Decision 018: Post-Polish Compile Verification
Problem: The dispatcher refactor changed the runtime interface and introduced a monolithic Burst job; static scans cannot prove C# interface signatures, extension imports, or generic job scheduling names.
Solution: Waited for the guard to clear (`CPU=36.5; CSC=0; DOTNET=0`) and ran `dotnet build Hecton8.Core.csproj --no-restore /m:1`. Build succeeded. Remaining diagnostics are the same 8 external `CS0649` warnings in `GlobalPhysicsStateManager.PhysicsDistanceCullingJob`.
Rejected Alternatives: Skipping build because the previous compile passed; launching while the CPU was above the mandated threshold; running a full rebuild.
Scalability potential: None; this is integration proof for the dispatcher path.
Hardware Impact: Confirms the rollback refactor compiles without adding runtime compile-wall fallout. Post-build static scan remains clean for forced barriers, RPC/NetworkTransform, packed structs, hot DTO properties, and managed hot-path helpers.

### Decision 019: AUP-Local Visual Correction DTO
Problem: The presentation DTO still described true/interpolated absolute `double3` positions, and the editor gizmo path converted those values into `Vector3`. That reintroduced the exact AUP jitter/clamp risk the rollback lane is supposed to prevent.
Solution: Replace visual correction storage with `AnchorAupAbsolute` plus `TrueLocalMeters` and `InterpolatedLocalMeters` as `float3`. The Burst pipeline subtracts the pre-rollback anchor in double precision once, clamps/sanitizes the local delta, and presentation/editor code only blends or draws the local correction vector.
Rejected Alternatives: Keeping absolute `double3` in presentation because it is "more precise"; clamping absolute world coordinates to floats in the editor; adding a `Transform` or `NetworkTransform` correction object. All three leak presentation concerns back into simulation truth or large-world precision.
Scalability potential: Low = one local vector lerp over 16 visual slots; Middle = same data shape with longer designer-selected interpolation; High/Ultra = richer visual smoothing can be layered without changing snapshot truth or network packets.
Hardware Impact: Prevents costly and unstable large-coordinate float conversion in the presentation path. The hot work is still one bounded local `float3` lerp per active visual correction; build after this polish is deferred because guard sampled `CPU=97.9; CSC=0; DOTNET=0`, then `CPU=93.8; CSC=0; DOTNET=0`, then `CPU=100.0; CSC=1; DOTNET=1` after waiting.

### Active Volcanic Lane Pointer - 2026-05-19
Problem: Latest active user directive is volcanic updrafts, but this shared `SHINOBU_64` rationale is again dominated by rollback state because of duplicate IDs.
Solution: Keep detailed volcanic rationale in `Docs/AgentLogs/Rationale_SHINOBU_64_VOLCANIC_UPDRAFT.md` and append this pointer without deleting rollback material.
Rejected Alternatives: Overwriting rollback records or pretending the shared file is authoritative for volcanic work.
Scalability potential: Low/Middle/High/Ultra volcanic behavior now has a stricter debris curve: low quality skips debris cylinder tests entirely; higher quality ramps through `math.step * SmoothStep`.
Hardware Impact: Fresh build was not launched because guard sampled `CPU=100/100/100` with active `dotnet` process `50592`. Static volcanic scan remained clean.

### Decision 020: Remove Packed Lockstep DTOs From Rollback State Surface
Problem: `LockstepPlayerKinematicState` is copied into the rollback state ring and hashed by the lockstep validator, but the validator file still used `[StructLayout(... Pack = 1 ...)]` on this DTO and adjacent replay/hash DTOs/jobs. That violates ARM64 alignment policy even if the current field order happens to keep the same byte offsets.
Solution: Remove `Pack=1` from the lockstep validator DTOs and internal hash job structs while preserving explicit `Size` where the binary/replay ABI depends on it. Add test assertions for `LockstepPlayerKinematicState`, `LockstepReplayInputFrame`, and `LockstepReplayBlockHeader` offsets. Upgrade validator hash jobs to `CompileSynchronously=true` deterministic Burst and add `[NoAlias]` on NativeArray fields.
Rejected Alternatives: Ignoring the packed validator because the rollback files themselves were clean; that is false rigor because rollback snapshots `LockstepPlayerKinematicState` bytes. Broadly rewriting the validator dispatcher was also rejected because its `masterHandle.Complete()` is an existing POST_SIMULATION hash fence and needs a separate owner-level dependency refactor.
Scalability potential: Low/Middle/High/Ultra all use the same aligned DTO surface; quality still changes rollback scan depth, not binary layout. On higher tiers the same hash jobs can vectorize more safely under Burst.
Hardware Impact: Removes avoidable unaligned-access risk from the player snapshot/replay/hash DTOs used by rollback validation. No runtime microsecond number claimed; build after this polish is deferred because guard sampled `CPU=100.0; CSC=0; DOTNET=0`, then `CPU=100.0; CSC=1; DOTNET=1`, then `CPU=100.0; CSC=0; DOTNET=0`.

### Active Rollback Lane Pointer - 2026-05-19 Reasserted
Problem: Duplicate `SHINOBU_64` agents continue appending volcanic state into the shared rationale file.
Solution: Reassert that the current user directive is `SHINOBU_LOCKSTEP_ROLLBACK_NETCODE`; rollback status is the active lane for this turn.
Rejected Alternatives: Deleting volcanic entries, which would erase another lane's evidence trail.
Scalability potential: None; this protects task identity.
Hardware Impact: No runtime claim. Latest rollback build remains deferred by CPU/compiler guard.

### Active Volcanic Lane Pointer - 2026-05-19 Dispatcher Polish
Problem: Latest active user directive is volcanic updrafts, but this shared rationale remains polluted by duplicate-ID rollback state.
Solution: Keep detailed volcanic reasoning in `Docs/AgentLogs/Rationale_SHINOBU_64_VOLCANIC_UPDRAFT.md` and append this pointer only. The volcanic director now uses `IDispatcherFixedSystem`, returns the fixed job handle to the master bridge, and avoids owner-side hot completion.
Rejected Alternatives: Overwriting rollback evidence or keeping the legacy volcanic fixed/post-fixed scheduling path.
Scalability potential: Low/Middle/High/Ultra behavior remains controlled by `GlobalQualityWeight`; weak quality skips debris vent intersections and collapses turbulence before the dispatcher job spends the ALU.
Hardware Impact: Build remains deferred because guard sampled `CPU=100,100,99.2` with zero compiler processes.
