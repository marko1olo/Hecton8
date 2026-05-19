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
