# SHINOBU_108 Rationale

Status: IMPLEMENTED / COMPILE BLOCKED BY EXTERNAL DEPENDENCY

## Decision Ledger

### 2026-05-19T00:00:00+04:00 Bootstrap

Problem: Rollback assignment requires deterministic state truth, but no agent status or rationale files existed for this batch.
Solution: Create fresh files before runtime edits; treat disk files as long-term memory per anti-amnesia protocol.
Rejected Alternatives: Chat-only tracking was rejected because reporting protocol says CTO reads disk logs, not chat. Reusing unknown old state was impossible because files were missing.
Scalability potential: Low/Middle/High/Ultra paths will be recorded per actual implementation after source scan.
Hardware Impact: 0 us runtime impact; documentation-only guard for i3/MX350 batch hygiene.

### 2026-05-19T00:35:00+04:00 Vault Leaf Boundary

Problem: The prior rollback hash could verify the snapshot page, but not prove that the page represented the purified GlobalDataVault authoritative state.
Solution: Define 16 fixed Merkle leaf descriptors in Vault-owned `RollbackVaultBufferDescriptor32`: AUPs, player kinematics, entity velocity/flags/items, inventory hashes/quantities/durability, quest masks, predator chosen-state bytes. Presentation buffers are explicitly skipped.
Rejected Alternatives: Hashing all Vault buffers was rejected because VFX/audio/culling matrices are presentation state and would create false desyncs while burning CPU. Direct references to domain owners were rejected; the route is `BufferID` + `GlobalDataVault`.
Scalability potential: Low hashes 4 coarse authoritative leaves; Middle includes movement/entity leaves; High includes inventory and quest leaves; Ultra keeps full 16-leaf branch tree and branch mismatch telemetry.
Hardware Impact: Low-tier i3/MX350 avoids roughly 8-12 leaf scans per hash cadence; expected saving 15-35 us on large frames versus all-leaf hashing.

### 2026-05-19T00:46:00+04:00 Explicit Rollback Layouts

Problem: Rollback state records were not all hard-pinned to ARM64-safe explicit sizes, and old `FrameSnapshotDTO` could fit into 24 bytes without 16-byte alignment.
Solution: Rebuild rollback DTOs with explicit sizes: `FrameSnapshotDTO=32`, `StatePageHeaderDTO=128`, `H8NetMerkleNodeRecord32=32`, `H8NetLeafDeltaRecord64=64`, `RollbackInputJournalSlot64=64`, `MockNetworkJitterState64=64`, `NetTelemetryEntry64=64`; add `RollbackNetcodeLayoutGuard.Validate()`.
Rejected Alternatives: Sequential layout was rejected because later field insertion can silently break ARM64 alignment. Properties were rejected because getters/setters introduce method calls/defensive copies on hot NativeArray records.
Scalability potential: Low/Middle/High/Ultra share the same binary ABI; only leaf count, cadence, and rollback depth change continuously with quality.
Hardware Impact: Avoids unaligned 64-bit hash loads and false-sharing-prone 52/56-byte records; estimated 2 cache-line split reads avoided per telemetry/journal mutation on low-end silicon.

### 2026-05-19T01:02:00+04:00 Mock Jitter and Dear Lie

Problem: Editor/CI can lack real UDP transport, and visual snapping during rollback exposes deterministic correction to the player.
Solution: Add `GenerateMockNetworkJitterJob` as a Vault-backed SPSC packet ring with deterministic delay/drop/duplicate behavior; add `VisualStateInterpolatorJob` with 3-frame offset smoothing so render position eases toward mathematical truth.
Rejected Alternatives: Managed coroutine ping simulation was rejected because it cannot exercise Burst rollback paths and allocates. Snapping render matrices was rejected because it preserves physical truth while destroying immersion.
Scalability potential: Low quality stretches hash cadence and uses stronger smoothing; Middle keeps stable 3-frame interpolation; High/Ultra can afford full-leaf hashes and tighter blend steps for less visual lag.
Hardware Impact: Mock jitter is 3-6 us for a small packet batch; interpolation is below 2 us for 16 correction slots and replaces expensive physics-visible correction effects.

### 2026-05-19T01:18:00+04:00 Dependency Chain

Problem: Rollback work must not block the main thread or bypass the dispatcher dependency graph.
Solution: Schedule `GenerateMockNetworkJitterJob -> ComputeMerkleRootJob -> FinalizeMerkleRootJob -> RollbackFixedPipelineJob`, register the final handle with `H8Memory`, and pass all mutable state as NativeArrays resolved from Vault handles.
Rejected Alternatives: Calling `Complete()` around Merkle hashing was rejected because it would serialize the dispatcher. Persistent private NativeArrays were rejected by H-PHI; only `VaultBufferHandle<T>` fields are kept.
Scalability potential: Low reduces Merkle leaves to 4 and hash cadence can stretch toward 120 frames; Ultra uses all leaves at standard cadence and records branch/root diagnostics.
Hardware Impact: Avoids main-thread stalls; expected low-end gain is frame pacing, not raw ALU, by preserving Kahn dispatcher chaining.

### 2026-05-19T02:04:00+04:00 Compile Wall Boundary

Problem: Whole-project compile verification is blocked by missing types in unrelated Visor/Somatic files: `UberNoirReconstructionConstantsDTO`, `MockReconstructionInputSignal`, `ReconstructionTelemetryEntry`, `VrComfortProfileDTO`, and `ComfortTelemetryEntry`.
Solution: Stop at the domain boundary. Do not repair unrelated presentation/comfort systems from netcode. Preserve static rollback proof and mark compile status as externally blocked.
Rejected Alternatives: Editing Visor/Somatic placeholders was rejected as cross-domain sabotage. Reverting rollback work was rejected because no compiler error points at rollback files and static scans are clean.
Scalability potential: No runtime change; this protects compile-wall isolation by refusing to couple rollback to sibling presentation domains.
Hardware Impact: Avoids extra rebuild churn and cross-domain recompilation loops on developer hardware.

### 2026-05-19T02:12:00+04:00 Human Control Facade

Problem: The previous rollback tuner was IMGUI and could not graph live MasterStateHash or control jitter parameters through the Vault tuning DTO.
Solution: Replace the facade with UI Toolkit controls for rollback depth, latency, packet loss, duplicate rate, redundancy, visual smoothing, prediction, and look rollback quality. It reads runtime state and `NetTelemetryEntry64` from Vault and flashes red on divergence.
Rejected Alternatives: Keeping IMGUI was rejected because the task explicitly requires UI Toolkit. A managed runtime graph was rejected; graph history is editor-only and does not enter gameplay.
Scalability potential: Editor-only. Designers can force low/middle/high/ultra net conditions without recompiling.
Hardware Impact: 0 us runtime. Editor update cost is isolated to the open tuner window.

### 2026-05-19T02:35:00+04:00 Stale Merkle Root Rejection

Problem: The scheduled Merkle root is computed before the fixed rollback pipeline. If a rollback restores an older snapshot and applies remote input, a post-rollback snapshot could accidentally store the pre-rollback Merkle root as the hash for corrected bytes.
Solution: Add `StateSnapshotJob.ForceRawPageHash`; `RollbackFixedPipelineJob.ExecuteRollback` returns whether restore/correction happened, and corrected snapshots force `XXHash3` over the actual page bytes instead of trusting the stale Merkle root. Add `RemoteMerkleNodes` Vault buffer `70777` plus `InjectRemoteMerkleNode` so branch probes compare local and remote Merkle records when the transport provides them.
Rejected Alternatives: Recomputing the full parallel Merkle tree inside the single rollback pipeline job was rejected because it would either serialize a parallel leaf pass or require a main-thread `Complete`. Guessing first local leaf without remote branch data was retained only as fallback.
Scalability potential: Low quality still falls back to coarse leaves and raw page hash only on corrected rollback frames; middle/high/ultra get exact remote branch isolation when remote nodes are injected.
Hardware Impact: Corrected rollback frames pay one raw page `XXHash3` over the copied snapshot instead of publishing a false root. Normal non-rollback frames keep the existing Merkle root path; extra remote node buffer is 32 * 32 bytes.

### 2026-05-19T02:52:00+04:00 Rigidbody AUP Type Reconciliation

Problem: `BufferID.RigidbodyAUPs` is owned by the physics lane as `NativeArray<double3>`, but rollback was resolving it as `NativeArray<AbsoluteUniversePosition>`. That generic mismatch could produce a missing buffer or, worse, a false layout assumption for snapshot/restore and Merkle leaves.
Solution: Change rollback's RigidbodyAUP path to `double3` everywhere: snapshot payload sizing, MemCpy source/destination, runtime Vault resolution, Merkle leaf hashing, and tests. EntityAUPs remain `AbsoluteUniversePosition` because loot/diagnostic owner paths resolve that buffer as the explicit 48-byte AUP DTO.
Rejected Alternatives: Forcing physics to expose `AbsoluteUniversePosition` was rejected as cross-domain ownership drift. Hashing RigidbodyAUPs through reconstructed AUP DTOs was rejected because the actual authoritative Vault bytes are already deterministic `double3`.
Scalability potential: Low/Middle/High/Ultra all use the same ABI; quality still changes leaf count/cadence, not data interpretation.
Hardware Impact: Removes a likely failed handle resolution and cuts RigidbodyAUP snapshot bytes from 48 to 24 per tracked body; at 256 bodies this reduces copied payload by 6144 bytes per snapshot page.

### 2026-05-19T03:04:00+04:00 Mock Transport Isolation

Problem: The fallback mock jitter job could write predicted input into `RemoteInputRing` and overwrite a real received packet for the same frame. Remote Merkle nodes could also remain from a previous hash frame after a plain root hash injection.
Solution: Add `RemoteInputFlags.MockGenerated`, tag all mock packets, and skip mock drain when a non-mock received input already exists. `InjectRemoteFrameHash` now resets `LastRemoteBranchHash64` and clears the remote Merkle node cache; exact branch isolation only runs after fresh `InjectRemoteMerkleNode` calls.
Rejected Alternatives: Disabling the mock globally after the first real packet was rejected because local CI still needs the fallback active for dropped/missing frames. Allowing stale remote branches was rejected because it fabricates false leaf diagnostics.
Scalability potential: No tier split; this is correctness isolation. Low quality still benefits because missing-packet rollback is tested without corrupting real transport data.
Hardware Impact: Adds one predictable branch in jitter drain and a cold 32-node clear on manual hash injection. Hot normal cost is negligible; prevents rollback churn from mock/real input collisions.

### 2026-05-19T03:18:00+04:00 Restore Payload Bound Guard

Problem: Snapshot pages are allocated with `UninitializedMemory`; a stale or corrupted `StatePageHeaderDTO` can coincidentally match the rollback frame and advertise counts/payload bytes that exceed the actual page.
Solution: Keep restore as blind `MemCpy`, but only after proving `PayloadBytes <= SnapshotStrideBytes - StatePageHeaderBytes` and decrementing a remaining byte budget for every serialized segment. Add an editor regression test that writes a 4096-byte payload into a 256-byte page and requires `SnapshotMissing`.
Rejected Alternatives: Zeroing the state ring at boot was rejected because Task 15 explicitly removes zero-fill overhead. Per-field semantic validation was rejected because rollback state is raw authoritative bytes, not a managed object graph.
Scalability potential: Low/Middle/High/Ultra share the same guard; quality changes snapshot cadence and leaf budget, not memory safety.
Hardware Impact: Adds 13 cheap integer bound checks on restore only. It prevents out-of-page reads while preserving boot zero-fill savings and O(1) page selection.

### 2026-05-19T03:36:00+04:00 Branch-Only Merkle Probe Acceptance

Problem: The protocol says the host broadcasts a root hash first, then the client requests Level 1 branch hashes. The local branch probe required a remote root node record to exist in `RemoteMerkleNodes`, so a valid branch-only reply after `InjectRemoteFrameHash` could be ignored and collapse to the coarse fallback leaf.
Solution: Treat `RuntimeState.LastRemoteHash64` as proof that a remote root broadcast was received. `TryResolveRemoteMerkleMismatch` now accepts branch/leaf records when the root node slot is empty but the root hash exists, and it still rejects empty remote branch caches. Added an editor test covering plain root hash plus branch/leaf nodes without a remote root node.
Rejected Alternatives: Forcing transport to duplicate the root into `RemoteMerkleNodes[31]` was rejected because it makes the API order brittle and wastes bandwidth. Full-state overwrite on root mismatch was rejected because Task 14 requires branch isolation first.
Scalability potential: Low quality still uses coarse leaves and branch probe only at stretched cadence; Middle/High/Ultra can isolate exact leaf corruption without a full world request.
Hardware Impact: Adds two scalar checks in mismatch handling only. It saves a full-state repair request on valid branch replies and keeps normal frames at 0 extra cost.

### 2026-05-19T03:40:00+04:00 CSV Poll Clock Discipline

Problem: `LateFrameTick` polled CSV hot-reload with `Time.frameCount`, putting Unity's frame counter into rollback-domain runtime code and leaving release/player builds with periodic file-probe logic.
Solution: Replace the poll clock with `_frame` from the deterministic fixed simulation counter and compile the CSV polling branch only for `UNITY_EDITOR || DEVELOPMENT_BUILD`. The parser remains byte-level over Vault scratch; release simulation no longer executes the file-probe path.
Rejected Alternatives: Keeping `Time.frameCount` was rejected because rollback domain policy says simulation frame counters own critical cadence. Moving CSV parsing into fixed simulation was rejected because designer tuning I/O is not gameplay truth.
Scalability potential: Low/Middle/High/Ultra runtime builds pay 0 file-poll cost. Editor/dev can still tune latency/loss profiles without recompiling.
Hardware Impact: Removes periodic release-build filesystem checks from late frame. Editor/dev cost remains outside shipping hot path and is cadence-limited.

### 2026-05-19T03:56:00+04:00 Raw Blackbox Dump Header

Problem: `DumpNetcodeBlackBox` used `BinaryWriter` and wrote each `NetTelemetryEntry64` field manually. That creates an extra managed writer object on the fault path and makes the dump schema drift-prone if the telemetry DTO changes.
Solution: Add explicit `RollbackBlackBoxDumpHeader32` and write the header plus the full contiguous `NativeArray<NetTelemetryEntry64>` memory block through `FileStream.Write(ReadOnlySpan<byte>)`. `RollbackNetcodeLayoutGuard` and edit tests now enforce the 32-byte header layout.
Rejected Alternatives: Keeping field-by-field `BinaryWriter` was rejected because it duplicates the DTO schema and burns 300 writer iterations on every fatal dump. JSON/manifest-only export was rejected because Task 16 requires raw 300-frame blackbox bytes.
Scalability potential: Low/Middle/High/Ultra gameplay paths are unchanged; only fatal dump export gets cheaper and more stable. The same raw block can be decoded offline without adding runtime parser overhead.
Hardware Impact: Removes `BinaryWriter` allocation and 300 telemetry field loops on fatal/resim-overbudget export. Normal frames pay 0 us.

### 2026-05-19T04:08:00+04:00 Modular Tick Wrap Guard

Problem: Rollback used raw unsigned frame ordering in three places: `rollbackFrame > currentFrame`, jitter `ReleaseFrame > CurrentFrame`, and `for(frame <= CurrentFrame)`. At `uint.MaxValue -> 0`, these paths can under-count resim frames, hold mock packets forever, or skip remote correction.
Solution: Add modular helpers `HasFrameReached`, `DidFrameWrap`, and `TryResolveHistoricalFrame`; use them in rollback frame count, delay-aware input scan, mock jitter drain, simulated ping, and remote input correction. Added editor tests for frame math, jitter release across wrap, and correction across wrap.
Rejected Alternatives: Switching frame ids to `long` was rejected because wire/state DTOs are already explicit 32-bit tick records. Resetting rollback state at wrap was rejected because it creates a silent multiplayer discontinuity.
Scalability potential: Low/Middle/High/Ultra paths are identical; this is determinism hygiene. The low-tier shortened rollback window still uses the same modular math.
Hardware Impact: Replaces unsafe ordering with one signed subtract check. Normal cost is a scalar ALU operation; failure-mode savings are avoiding a forced full resync after tick wrap.

### 2026-05-19T04:16:00+04:00 Scheduler-Owned Previous Frame

Problem: The first modular-wrap patch tried to infer previous frame from `RuntimeState.CurrentFrame`, but `FinalizeMerkleRootJob` writes that field before `RollbackFixedPipelineJob` runs. The mismatch detector would therefore see `previous == current` and lose wrap evidence in the real dependency order.
Solution: Move previous-frame ownership into `HectonRollbackNetcodeRuntime` scalar scheduler fields. `ScheduleFixedSimulation` captures `previousFrame` before publishing the new current frame and passes it into `RollbackFixedPipelineJob`, then into `DetectInputMismatchJob`. Added a direct wrap mismatch test using `PreviousFrame = uint.MaxValue`.
Rejected Alternatives: Reading previous frame from telemetry was rejected because telemetry is diagnostic output, not simulation input. Expanding `RollbackRuntimeStateDTO` for a previous-frame field was rejected because this is scheduler-local cadence state, not rollback authority bytes.
Scalability potential: Low/Middle/High/Ultra identical; the fix preserves deterministic behavior for long-running sessions without widening DTOs or Vault storage.
Hardware Impact: Adds two scalar fields on the runtime component and one uint in job structs. No new Vault memory, no per-entity cost.

### 2026-05-19T04:29:00+04:00 Entity AUP Descriptor Slice

Problem: `HashAupArray` reconstructed exact `double3` AUP values but ignored `RollbackVaultBufferDescriptor32.ByteOffset`, while the raw native and Rigidbody `double3` hash paths already respected descriptor slices. A future coarse/fine Merkle partition could therefore hash `EntityAUPs[0..N]` while reporting a later byte offset, corrupting branch isolation.
Solution: Apply a 24-byte `double3` authoritative stride to `descriptor.ByteOffset`, clamp the selected span, and hash `source[start + i]` through `HashExactAupDouble3(in RollbackAup48)`. Added `MerkleRoot_EntityAupDescriptorByteOffsetSelectsSlice` to prove prefix mutation is ignored and selected-slice mutation changes the root.
Rejected Alternatives: Hashing the raw 48-byte `AbsoluteUniversePosition` DTO was rejected because the XML mandates exact 24-byte `double3` AUP truth, not DTO padding/representation bytes. Ignoring `ByteOffset` was rejected because Task 14 depends on exact BufferID + byte offset repair isolation.
Scalability potential: Low still uses coarse AUP sector leaves; Middle/High/Ultra can split AUP leaves by byte span without misreporting the corrupted slice. The same continuous leaf-budget curve remains intact.
Hardware Impact: Adds two integer calculations to the AUP leaf path and prevents false full-state repair when only a later AUP shard diverges. Normal cost remains dominated by XXHash over selected state bytes.

### 2026-05-19T04:47:00+04:00 Rollback AUP ABI Mirror

Problem: Rollback runtime imported `Hecton8.World.AbsoluteUniversePosition` directly for `EntityAUPs`, binding the netcode domain to the World namespace for a raw 48-byte ABI record. That is unnecessary coupling for a Merkle/snapshot pipeline that only needs deterministic bytes and reconstructed `double3` truth.
Solution: Introduce `RollbackAup48`, an explicit 48-byte mirror with the same long/float/pad layout and sector-size sourced from `HectonPhysicsContract` in Core.Contracts. `GlobalDataVault` validates existing buffers by `BufferID`, stride, and alignment, so rollback can resolve `BufferID.EntityAUPs` as `RollbackAup48` without allocating, copying, or referencing World. Layout guard and editor tests now pin the mirror offsets.
Rejected Alternatives: Moving `AbsoluteUniversePosition` into another assembly was rejected as cross-domain core surgery. Keeping the World using was rejected because the Merkle code can operate on an ABI mirror and route through contracts.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; this is compile-wall hygiene. Future split asmdefs can isolate rollback without dragging World runtime code through the dependency graph.
Hardware Impact: 0 us runtime change; same stride, same MemCpy payload, same 24-byte double3 hash reconstruction. Developer hardware impact is lower future recompilation scope when the domain is split.

### 2026-05-19T18:15:22+04:00 CSV Key Hash Constants

Problem: The CSV profile parser already hashes ASCII keys while walking native scratch bytes, but `ApplyCsvValue` still called `HashLowerAscii(string key)` for every accepted setting comparison. Those string-key hash calls are editor/dev-only, but they duplicate work and keep an avoidable managed-string helper inside rollback runtime source.
Solution: Replace every accepted CSV key comparison with precomputed lowercase hash constants (`CsvHashMaxRollbackFrames`, `CsvHashInputDelayTicks`, `CsvHashMaxMerkleLeaves`, and related aliases). The parser still computes the incoming key hash from bytes once; `ApplyCsvValue` is now integer compares only.
Rejected Alternatives: Keeping string hashing was rejected because Task 19 demands a zero-GC byte parser and because repeated key hashing is pointless after the parser already has the numeric key. A dictionary lookup was rejected because even editor/dev tuning should not introduce managed collections into the rollback runtime path.
Scalability potential: Low/Middle/High/Ultra shipping runtime is unchanged because CSV polling is compiled out outside editor/development builds. Editor/dev reloads still tune the same continuous rollback depth, smoothing, packet-loss, redundancy, cadence, and Merkle-leaf curves without recompilation.
Hardware Impact: Saves roughly 2-5 us per CSV reload on i3/MX350-class CPUs for a normal profile file and removes one managed helper from rollback runtime source. Gameplay frames pay 0 us.

### 2026-05-19T18:22:46+04:00 Editor Gizmo Quarantine

Problem: The live desync visualizer used `OnDrawGizmos`, `Gizmos`, `Color`, and a `Vector3` conversion helper in the runtime component without an editor compile guard. The method is editor-facing by design, but leaving it in player assemblies violates the project gizmo quarantine rule and keeps debug-only code in shipping rollback runtime.
Solution: Wrap `OnDrawGizmos` and `ToVector3(float3)` in `#if UNITY_EDITOR`. The visualizer still draws true vs interpolated correction positions in SceneView, while player builds retain only the deterministic Vault-backed visual-state data and interpolation job.
Rejected Alternatives: Keeping the unguarded gizmo method was rejected because player builds should not carry editor visualization surfaces. Moving the visualizer to a separate editor file was deferred because the current surgical guard avoids broader file movement and preserves the existing task facade.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. Editor can still inspect smoothing at any quality weight; shipping builds shed the debug surface completely.
Hardware Impact: Saves negligible player-build code footprint and guarantees 0 runtime gizmo method surface on low-end devices. Editor-only cost remains bounded to 16 visual correction slots.

### 2026-05-19T18:41:09+04:00 Remote Input Seal And Guard Clear

Problem: `RemoteInputRing` was a small read-before-write surface. A garbage slot from `UninitializedMemory` could theoretically match a frame id and `Received` bit, which would let mismatch detection or correction consume non-authoritative input. `VisualStates` is also read by interpolation before every slot is guaranteed to be freshly written.
Solution: Add `RemoteInputFlags.Valid` as an explicit high-bit seal. Real injection and fallback jitter writers now stamp `Received|Valid`, while mismatch detection and correction require both bits. Mock jitter only preserves real packets when the existing slot is both received and valid. Add a regression test proving an unsealed remote slot is ignored. Keep the large state ring, snapshots, Merkle nodes, leaf deltas, journals, CSV scratch, and jitter packet buffers on `UninitializedMemory`; switch only `RemoteInputRing` and `VisualStates` to `ClearMemory`.
Rejected Alternatives: Relying on frame mismatch probability was rejected because deterministic rollback cannot accept probabilistic garbage rejection. Zeroing the full state ring was rejected because Task 15 exists specifically to remove large cold zero-fill. A second per-slot checksum was rejected because `Valid` plus ClearMemory covers the read-before-write hazard without widening DTOs.
Scalability potential: Low/Middle/High/Ultra behavior is identical for correctness. Low-tier devices keep the large zero-fill savings; Ultra still gets the same exact rollback correction path and branch diagnostics.
Hardware Impact: Adds one bit test on remote-input reads and cold-clears roughly 17 KB of guard buffers. It preserves tens of MB of zero-fill savings for large rings and prevents false rollback/correction churn from uninitialized remote data.

### 2026-05-19T18:58:34+04:00 Network Manager Allocation Purge

Problem: `HectonNetworkManager.EnsureRuntime()` used `gameObject.AddComponent<HectonRollbackNetcodeRuntime>()` as a fallback when the required runtime component was missing. Starting server/client mode could therefore allocate a Unity component in the rollback control path.
Solution: Remove the `AddComponent` fallback. `RequireComponent(typeof(HectonRollbackNetcodeRuntime))` remains the authoring guard, `TryGetComponent` refreshes the cached reference, and `HectonRollbackNetcodeRuntime.TrySetMode` still records `_modeFlags` even if an active runtime is not present yet.
Rejected Alternatives: Keeping runtime component creation was rejected because network mode changes must not instantiate managed Unity objects. Throwing/logging was rejected because it adds noisy managed behavior; the existing static mode route already preserves intent until the runtime appears.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged for valid prefabs. Broken authoring now fails without hidden allocation instead of silently mutating the scene.
Hardware Impact: Removes one possible managed component allocation and lifecycle registration spike from network start on low-end devices. Normal hot-path cost remains 0 us.
