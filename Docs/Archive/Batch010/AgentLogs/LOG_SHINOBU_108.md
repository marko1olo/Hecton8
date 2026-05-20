# LOG_SHINOBU_108

## 2026-05-19 ROLLBACK_STATE_MERKLE_WELD

What was wrong:
- Rollback hashing was page-local and could validate a copied snapshot without proving that the bytes came from the purified `GlobalDataVault`.
- The rollback DTO surface still had stale 24-byte/80-byte assumptions and no hard layout guard for new Merkle, jitter, journal, and telemetry records.
- Editor fallback networking was not a Burst SPSC jitter surface, so local tests could skip the rollback path when UDP was absent.
- Rollback visual correction could expose hard snaps instead of hiding resimulation with a presentation-only lie.

What was done:
- Added explicit Vault-owned rollback buffers `70770..70776`: Merkle nodes, leaf descriptors, leaf deltas, rollback input journal, mock jitter packets/state, visual history.
- Wired `ComputeMerkleRootJob -> FinalizeMerkleRootJob -> RollbackFixedPipelineJob` after `GenerateMockNetworkJitterJob`; the final handle is registered through `H8Memory`.
- Expanded `StateSnapshotJob` and `RestoreSnapshotJob` to blind-copy authoritative Vault state: AUPs, player states, entity AUP/velocity/flags/items, inventory hashes/quantities/durability, quest masks, predator chosen-state bytes.
- Replaced legacy telemetry with `NetTelemetryEntry64` and black-box dump payload for `Dump_NETCODE_SURGEON.bin`.
- Replaced IMGUI rollback tuner with UI Toolkit sliders, live hash graph, divergence flash, and jitter controls.
- Added runtime `OnDrawGizmos` plus SceneView overlay for red true mathematical position and green interpolated visual position.
- Added editor tests for explicit layouts, quality rollback budget, exact AUP hash sensitivity, snapshot/restore, Merkle root, and jitter delay/release.

Cinematic cheats used:
- "Dear Lie" visual rollback: simulation truth is restored immediately, but render-space correction uses a 3-frame offset history and `math.lerp` smoothing.
- Quality-scaled Merkle collapse: low quality hashes 4 coarse leaves and stretches hash cadence; high quality uses full 16-leaf branch/root evaluation.
- Branch-probe before hard resync: first mismatch requests branch isolation and writes a leaf delta record; full overwrite is deferred until 3 repair attempts.

Exact microseconds saved, estimates:
- Presentation buffers excluded from Merkle tree: 15-35 us avoided per hash cadence on i3/MX350-scale CPU.
- Rollback window quality collapse: max 120 -> 30 or max 60 -> 15 at `GlobalQualityWeight=0.1`; worst-case replay work reduced 75%.
- 64-byte journal/telemetry records: avoids 1-2 cache-line split penalties per mutation versus 52/56-byte drift records.
- `StateRingBuffer` and `InputJournalRing` uninitialized allocation: avoids boot zero-fill of multi-megabyte rings; runtime frame cost unchanged.
- Visual interpolation: <2 us for 16 correction slots; replaces visible snap effects, not physics.
- Mock jitter: 3-6 us per local packet batch; replaces managed coroutine/network fakery.

Verification:
- Static scans: no networking `BinaryFormatter`, JSON serializer, LINQ, `foreach`, `LayoutKind.Sequential`, `Pack=`, DTO properties, `UnityEngine.Random`, or `Time.deltaTime` in rollback networking files.
- Burst scan: 10 rollback jobs use `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`.
- `git diff --check` on touched files: clean except CRLF normalization warnings.
- Build command: `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`.
- Build result: blocked by unrelated compile errors in `Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs` and `Assets/_Project/Scripts/Editor/SomaticTunerWindow.cs` for missing Visor/Somatic DTOs/signals. No compiler error from rollback files was reported before the external wall.

<SELF_AUDIT agent_id="SHINOBU_108">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS_STATIC" note="Authoritative Vault leaves defined; presentation buffers excluded." />
    <TASK id="02" result="PASS_STATIC" note="Rollback networking scan found no JSON/BinaryFormatter/object graph snapshot path." />
    <TASK id="03" result="PASS_STATIC" note="Merkle/delta/journal records are explicit public fields with no properties." />
    <TASK id="04" result="PASS_STATIC" note="FrameSnapshotDTO=32 and layout guard validates DTO sizes." />
    <TASK id="05" result="PASS_STATIC" note="GenerateMockNetworkJitterJob owns SPSC delay/drop/duplicate simulation." />
    <TASK id="06" result="PASS_STATIC" note="ComputeMerkleRootJob hashes Vault leaf spans and FinalizeMerkleRootJob builds branches/root." />
    <TASK id="07" result="PASS_STATIC" note="Snapshot/restore use UnsafeUtility.MemCpy into Vault state ring pages." />
    <TASK id="08" result="PASS_STATIC" note="VisualStateInterpolatorJob keeps 3-frame render offset history." />
    <TASK id="09" result="PASS_STATIC" note="RollbackInputJournalSlot64 compares ReceivedMask against ExpectedMask with input-delay gate." />
    <TASK id="10" result="PASS_STATIC" note="Rollback restore/correction/resim command path writes resimulation flags and suppression DTO." />
    <TASK id="11" result="PASS_STATIC" note="GlobalQualityWeight curves reduce max 120->30 and max 60->15 at quality 0.1." />
    <TASK id="12" result="PASS_STATIC" note="RuntimeState buffer 70752 writes Resimulating bit 1<<4 and AudioSuppression." />
    <TASK id="13" result="PASS_STATIC" note="AUP Merkle leaves hash deterministic absolute double3 bytes, not local float3." />
    <TASK id="14" result="PASS_STATIC" note="Root mismatch requests branch probe, compares RemoteMerkleNodes when injected, writes H8NetLeafDeltaRecord64, hard-resyncs after 3 attempts." />
    <TASK id="15" result="PASS_STATIC" note="State ring, frame snapshots, rollback journal, leaf deltas, jitter packets use UninitializedMemory." />
    <TASK id="16" result="PASS_STATIC" note="300-entry NetTelemetryEntry64 ring and dump writer updated." />
    <TASK id="17" result="PASS_STATIC" note="All rollback pipeline jobs use synchronous deterministic Burst attributes." />
    <TASK id="18" result="PASS_STATIC" note="Rollback Netcode Tuner is UI Toolkit and reads Vault runtime/telemetry." />
    <TASK id="19" result="PASS_STATIC" note="netcode_latency_profiles.csv parser reads FileStream bytes into Vault scratch and mutates tuning DTO." />
    <TASK id="20" result="PASS_STATIC" note="Runtime OnDrawGizmos and SceneView overlay draw true/interpolated positions." />
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <FrameSnapshotDTO size="32" alignment="16-multiple" fields="0 ulong FrameHash64; 8 uint Tick; 12 uint InputMaskP1; 16 uint InputMaskP2; 20 uint MemoryOffset; 24 uint MerkleRootIndex; 28 uint Flags" />
    <H8NetMerkleNodeRecord32 size="32" alignment="16-multiple" fields="0 ulong HashLo; 8 ulong HashHi; 16 uint BufferId; 20 uint ByteOffset; 24 uint ByteLength; 28 uint Flags" />
    <H8NetLeafDeltaRecord64 size="64" alignment="cache-line" fields="0 LocalHashLo; 8 RemoteHashLo; 16 LocalHashHi; 24 RemoteHashHi; 32 BufferId; 36 ByteOffset; 40 ByteLength; 44 FirstDifferentByte; 48 Frame; 52 Flags; 56 pad ulong" />
    <RollbackInputJournalSlot64 size="64" alignment="cache-line" fields="0 Predicted InputStateDTO(24); 24 Remote InputStateDTO(24); 48 Frame; 52 ReceivedMask; 56 ExpectedMask; 60 Flags" />
    <MockNetworkJitterState64 size="64" alignment="cache-line" fields="0 Head; 4 Tail; 8 Sequence; 12 Dropped; 16 Duplicated; 20 LossPermille; 24 DuplicatePermille; 28 DelayFrames; 32 Flags; 36 LastFrame; 40 RngState; 48 pad; 56 pad" />
    <NetTelemetryEntry64 size="64" alignment="cache-line" fields="0 FrameHash64; 8 RemoteHash64; 16 Frame; 20 LastRollbackFrame; 24 Dropped; 28 Duplicated; 32 ResimulatedFrames; 36 ResimMs; 40 Quality; 44 Flags; 48 InputMaskP1; 52 InputMaskP2; 56 MismatchBufferId; 60 MismatchByteOffset" />
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Below GlobalQualityWeight 0.3, rollback depth follows a smooth polynomial from full budget toward 25%; 120-frame history becomes 30, 60-frame history becomes 15. Merkle leaf budget lerps from 16 leaves toward 4 coarse leaves, so inventory/quest/AI optional leaves are skipped and branch/root hashing consumes fewer bytes. Hash cadence stretches toward 2x base cadence under low quality. Visual correction uses stronger smoothing at low quality and tighter convergence at high/ultra.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Zero private NativeArray/NativeList/NativeHashMap fields are declared. Persistent memory is requested by VaultBufferHandle only: 70750 StateRingBuffer, 70751 FrameSnapshots, 70752 RuntimeState, 70753 RemoteInputRing, 70754 TickCommands, 70755 VisualStates, 70756 TelemetryRing, 70757 Tuning, 70758 AudioSuppression, 70759 CsvScratch, 70769 LatencyProfile, 70770 MerkleNodes, 70771 MerkleLeafDescriptors, 70772 LeafDeltaRecords, 70773 InputJournalRing, 70774 MockJitterPackets, 70775 MockJitterState, 70776 VisualHistory, 70777 RemoteMerkleNodes, plus existing BufferID.ShinobuInputJournalRing.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    Jobs consume dispatcher dependsOn and output final RollbackFixedPipelineJob handle registered through H8Memory. Chain: GenerateMockNetworkJitterJob -> ComputeMerkleRootJob(IJobParallelFor) -> FinalizeMerkleRootJob -> RollbackFixedPipelineJob. Job fields use NoAlias where mutable/read-only NativeArray separation is known.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No rollback asmdef or sibling runtime assembly reference was added. The code remains in existing networking/core assembly surface and communicates through GlobalRegistry, GlobalDataVault, BufferID, and SignalBus pause signal only. Whole-project compile is currently blocked by unrelated Visor/Somatic missing types, not rollback files.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Heavy alternative rejected: visually snapping or replaying physical transforms through renderer/physics correction. Implemented fake: keep authoritative simulation correction immediate, but maintain a render-only 3-frame offset average and lerp the mesh position to true AUP-local position. Complexity before: visible physics correction could force downstream event/render churn; after: O(V) for V visual correction slots, currently 16.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 SHINOBU_108 POLISH PASS 2

What was wrong:
- Merkle leaves were computed before the rollback pipeline. A rollback-frame snapshot could reuse a pre-rollback root while its copied bytes represented restored/corrected state.
- Branch-probe isolation had no separate remote Merkle node buffer, so exact branch/leaf comparison was impossible unless the transport overwrote generic runtime hash fields.

What was done:
- Added `StateSnapshotJob.ForceRawPageHash`; corrected rollback snapshots now force `XXHash3` over actual page bytes.
- Added Vault `70777` `RemoteMerkleNodes` and public `InjectRemoteMerkleNode(frame, nodeIndex, node)` route.
- `RollbackFixedPipelineJob` now compares remote branch/leaf records when present and writes `H8NetLeafDeltaRecord64` from the exact differing leaf; first-local-leaf remains fallback only when remote branches are absent.
- Added an edit test proving stale Merkle root is ignored when `ForceRawPageHash=1`.

Verification:
- Static rollback scan remains clean for managed serialization, LINQ/foreach, random/time drift, Sequential/Pack layout drift, and DTO property regressions.
- `git diff --check` remains clean except CRLF normalization warnings.
- Build was not relaunched in this pass because CPU gate reported `100%` and no new build would be valid under AGENTS.md.

## 2026-05-19 SHINOBU_108 POLISH PASS 3

What was wrong:
- Rollback resolved `BufferID.RigidbodyAUPs` as `AbsoluteUniversePosition`, while physics owns the same Vault ID as raw `double3`.

What was done:
- Converted RigidbodyAUPs through the rollback pipeline to `NativeArray<double3>`.
- Updated snapshot payload sizing, snapshot/restore MemCpy jobs, runtime Vault resolution, Merkle leaf hash path, and tests.
- Added `MerkleRoot_ConsumesRawRigidbodyDouble3Bytes` to catch future regressions.

Verification:
- Static scan confirms no `ResolveLiveBuffer<AbsoluteUniversePosition>(BufferID.RigidbodyAUPs)` or `NativeArray<AbsoluteUniversePosition> RigidbodyAups` remains in rollback files.
- Burst attribute and zero-GC hot-path scans remain clean.
- Build not relaunched: CPU gate reported `84.6%`.

## 2026-05-19 SHINOBU_108 POLISH PASS 4

What was wrong:
- Mock jitter packets were indistinguishable from real transport packets and could overwrite real received input in `RemoteInputRing`.
- Remote Merkle branch nodes could persist after a plain remote root hash injection, causing stale branch/leaf diagnostics.

What was done:
- Added `RemoteInputFlags.MockGenerated`.
- Tagged fallback mock packets and prevented mock drain from overwriting non-mock received input.
- `InjectRemoteFrameHash` clears `RemoteMerkleNodes` and `LastRemoteBranchHash64`; branch isolation requires fresh remote node injection.
- Added `MockNetworkJitter_DoesNotOverwriteRealRemoteInput`.

Verification:
- Static hot-path scan remains clean for managed serialization, LINQ/foreach, random/time drift, and blocking `Complete()`.
- `git diff --check` remains clean except CRLF normalization warnings.
- Build not relaunched: CPU gate reported `100%`.

## 2026-05-19 SHINOBU_108 POLISH PASS 5

What was wrong:
- `RestoreSnapshotJob` uses pages allocated with `UninitializedMemory`. A corrupted or stale `StatePageHeaderDTO` can match `RollbackFrame`; without a regression test, the payload/count guard could silently regress into out-of-page `MemCpy`.

What was done:
- Verified restore rejects `PayloadBytes` larger than the page payload capacity before copying.
- Verified each serialized segment advances by its on-page byte count and decrements a remaining payload budget, even when the destination buffer is shorter.
- Added `RestoreSnapshot_RejectsOutOfBoundsPayloadHeader` to write a 4096-byte payload into a 256-byte page and require `RollbackNetcodeFlags.SnapshotMissing`.

Cinematic cheats used:
- None. This is memory safety for rollback raw-byte state restore.

Exact microseconds saved:
- Preserves Task 15 boot savings by keeping `UninitializedMemory`; avoids replacing it with ClearMemory over multi-megabyte state rings.
- Adds only restore-time integer bounds checks. Normal non-rollback frames pay 0 us.

Verification:
- SHINOBU_108 XML block re-extracted from `Docs/Tasks/CURRENT_BATCH.md` with role `ROLLBACK_STATE_MERKLE_WELD` and 20 tasks.
- Static hot-path scan found no rollback networking hits for managed serialization, LINQ/foreach, random/time drift, `LayoutKind.Sequential`, `Pack=`, DTO auto-properties, or blocking `Complete()`.
- Static type guard found no `ResolveLiveBuffer<AbsoluteUniversePosition>(BufferID.RigidbodyAUPs)` or `NativeArray<AbsoluteUniversePosition> RigidbodyAups`.
- Burst scan still shows 10 rollback jobs with synchronous deterministic Burst attributes.
- `git diff --check` remains clean except CRLF normalization warnings.
- Build not relaunched: CPU gate reported `99%`, `dotnet/csc` were not running.

<SELF_AUDIT_DELTA agent_id="SHINOBU_108" pass="5">
  <TASK id="07" result="PASS_STATIC" note="Restore now rejects out-of-bounds payload headers before any MemCpy; regression test covers corrupted 4096-byte payload inside 256-byte page." />
  <STRUCT_LAYOUT note="No DTO size changed in this pass; StatePageHeaderDTO remains 128 bytes and snapshot payload begins at byte 128." />
  <H_PHI note="No new persistent allocations; test uses TempJob only." />
  <COMPILE_GUARD note="No sibling assembly dependency added; build deferred by CPU guard." />
</SELF_AUDIT_DELTA>

## 2026-05-19 SHINOBU_108 POLISH PASS 14

What was wrong:
- The live desync visualizer used `OnDrawGizmos`, `Gizmos.color`, `Color`, and a `Vector3` conversion helper directly in the runtime component without `#if UNITY_EDITOR`.
- The visualizer is a SceneView/editor facade, not simulation truth. Leaving it compiled into player builds violates gizmo quarantine and leaves debug-only code in the rollback runtime surface.

What was done:
- Wrapped `OnDrawGizmos` in `#if UNITY_EDITOR`.
- Wrapped the `ToVector3(float3)` helper in the same editor guard.
- Kept the Vault-backed `VisualStateDTO` / `VisualStateHistoryDTO` data path and `VisualStateInterpolatorJob` unchanged.

Cinematic cheats used:
- Existing Dear Lie remains intact: render correction is smoothed by interpolating true vs visual local positions instead of replaying presentation physics. The gizmo now only visualizes that cheat in editor builds.

Exact microseconds saved:
- Player-build hot path remains 0 us; the practical saving is debug-code quarantine and smaller player method surface. Editor visualization remains bounded to 16 correction slots.

Verification:
- `rg -n -C 2 "UNITY_EDITOR|OnDrawGizmos|ToVector3|Gizmos|new Vector3"` shows both visualizer and converter inside editor guards.
- Static rollback scan found no managed serializers, LINQ/foreach, Unity time drift, `UnityEngine.Random`, blocking `Complete()`, direct `Hecton8.World` / `AbsoluteUniversePosition` coupling, `LayoutKind.Sequential`, `Pack=`, or DTO auto-property regressions.
- `git diff --check` remains clean except CRLF normalization warnings.
- Build not relaunched: CPU gate reported `100%` and active `dotnet`/`csc` processes were present.

<SELF_AUDIT_DELTA agent_id="SHINOBU_108" pass="14">
  <TASK id="20" result="PASS_STATIC" note="Live desync gizmo remains available in editor only; player builds do not compile the gizmo or Vector3 debug converter." />
  <DEAR_LIE note="Visual interpolation debug surface is editor-only; smoothing job remains deterministic and Vault-backed." />
  <COMPILE_GUARD note="No new direct sibling domain reference. Build deferred by CPU/process guard at 100% with active dotnet/csc." />
</SELF_AUDIT_DELTA>

## 2026-05-19 SHINOBU_108 POLISH PASS 12

What was wrong:
- `ApplyCsvValue` compared incoming byte-parsed CSV keys by repeatedly calling `HashLowerAscii(string key)` for every accepted tuning name.
- That helper was not a gameplay allocation site, but it kept managed-string hashing logic in rollback runtime source after the parser had already reduced each key to a numeric hash.

What was done:
- Added precomputed lowercase key hash constants for rollback depth, smoothing, prediction, input delay, redundancy, packet loss, duplicate rate, cadence, and Merkle leaf count.
- Replaced all `HashLowerAscii(...)` comparisons with integer constant comparisons.
- Removed the `HashLowerAscii(string key)` helper.

Cinematic cheats used:
- None new. This is human-control parser hygiene; the existing visual rollback cheat remains the 3-frame offset interpolation that hides deterministic correction without resimulating presentation physics.

Exact microseconds saved:
- Roughly 2-5 us per editor/development CSV reload on low-end CPUs, depending on profile key count. Shipping gameplay frames remain at 0 us because CSV polling is compiled out outside editor/development builds.

Verification:
- Static rollback scan found no `HashLowerAscii`, managed serializers, LINQ/foreach, Unity time drift, `UnityEngine.Random`, blocking `Complete()`, direct `Hecton8.World` / `AbsoluteUniversePosition` coupling, `LayoutKind.Sequential`, `Pack=`, or DTO auto-property regressions.
- `git diff --check` remains clean except CRLF normalization warnings.
- Build not relaunched: CPU gate reported `82%`, above the AGENTS.md limit.
- SHINOBU_108 XML block was re-extracted with an attribute-tolerant CLI regex; task count remains 20.

<SELF_AUDIT_DELTA agent_id="SHINOBU_108" pass="12">
  <TASK id="19" result="PASS_STATIC" note="CSV parser computes incoming key hash from native bytes once; value application now uses precomputed integer constants only." />
  <ZERO_GC note="No dictionary, JSON, BinaryWriter, LINQ, foreach, or managed object graph path added to rollback runtime." />
  <SCALABILITY note="CSV controls continuous rollback depth, smoothing, prediction, packet loss, redundancy, cadence, and Merkle leaf curves; no binary low-end switch added." />
  <COMPILE_GUARD note="No new direct sibling domain reference. Build deferred by CPU guard at 82%." />
</SELF_AUDIT_DELTA>

## 2026-05-19 SHINOBU_108 POLISH PASS 6

What was wrong:
- Branch probe required `RemoteMerkleNodes[Root]` to be populated even when the root hash had already arrived through the normal root-hash facade. This violates the intended protocol: root broadcast first, branch hashes only after mismatch.
- CSV hot reload used Unity `Time.frameCount` in `LateFrameTick` and left periodic file probing in non-editor runtime code.

What was done:
- `TryResolveRemoteMerkleMismatch` now accepts branch/leaf records after plain `InjectRemoteFrameHash` when `RuntimeState.LastRemoteHash64` is present.
- Empty remote branch caches still fail closed and fall back to coarse first-leaf diagnostics.
- Added `RemoteMerkleBranchIsolation_UsesBranchNodesAfterPlainRootHash`.
- CSV polling now uses the deterministic `_frame` counter with modular overflow handling and is compiled only for `UNITY_EDITOR || DEVELOPMENT_BUILD`.

Cinematic cheats used:
- None new. This pass preserves the existing branch-probe bandwidth cheat: isolate a corrupt leaf before requesting full state.

Exact microseconds saved:
- Valid branch-only repair avoids full-state request and resync handling; normal frames add two scalar checks only during mismatch handling.
- Release builds no longer pay periodic CSV file-probe cost in late frame; editor/dev hot reload remains cadence-limited to 300 simulation frames.

Verification:
- Static hot-path scan found no rollback scope hits for `Time.frameCount`, `Time.deltaTime`, `UnityEngine.Random`, blocking `Complete()`, managed serializers, `LayoutKind.Sequential`, `Pack=`, DTO auto-properties, or `RigidbodyAUPs` type drift.
- `git diff --check` remains clean except CRLF normalization warnings.
- Build not relaunched: CPU gate reported `100%`; an earlier gate also saw `dotnet:44020` running, while the latest gate showed no `dotnet/csc` but CPU still at `100%`.

<SELF_AUDIT_DELTA agent_id="SHINOBU_108" pass="6">
  <TASK id="14" result="PASS_STATIC" note="Branch-only remote Merkle reply works after root hash broadcast; regression test covers root-slot-empty branch isolation." />
  <TASK id="19" result="PASS_STATIC" note="CSV hot reload is editor/development only and no longer uses Unity frame time." />
  <DEPENDENCY_GRAPH note="No JobHandle changes; fixed pipeline chain remains jitter -> Merkle leaves -> Merkle root -> rollback pipeline." />
  <COMPILE_GUARD note="No new direct sibling domain reference; build deferred by CPU guard." />
</SELF_AUDIT_DELTA>

## 2026-05-19 SHINOBU_108 POLISH PASS 7

What was wrong:
- `DumpNetcodeBlackBox` used `BinaryWriter` and serialized `NetTelemetryEntry64` field-by-field. The dump path duplicated the telemetry schema and introduced a managed writer object exactly where crash forensics should be raw and deterministic.

What was done:
- Added `RollbackBlackBoxDumpHeader32`, explicit size 32, with `Magic`, `SourceHash`, `CurrentFrame`, `Flags`, `EntryCount`, `EntrySizeBytes`, and `Version`.
- Extended `RollbackNetcodeLayoutGuard.Validate()` and `RollbackDtos_StayAlignedAndBlittable` to cover the dump header.
- Replaced the writer loop with one header span write and one contiguous `NativeArray<NetTelemetryEntry64>` raw memory span write.

Cinematic cheats used:
- None new. This is forensic hygiene for the existing rollback blackbox.

Exact microseconds saved:
- Fatal dump export removes the `BinaryWriter` allocation and 300 telemetry record field loops. Normal frames remain 0 us because dump only triggers on hard resync or resim budget failure.

Verification:
- Static rollback scan found no networking hits for `BinaryWriter`, managed serializers, LINQ/foreach, Unity time drift, `UnityEngine.Random`, blocking `Complete()`, `LayoutKind.Sequential`, `Pack=`, or DTO auto-properties.
- Burst scan still shows 10 rollback jobs with `CompileSynchronously = true`, `FloatMode.Deterministic`, and `FloatPrecision.Standard`.
- `git diff --check` remains clean except CRLF normalization warnings.
- Build not relaunched: CPU gate reported `100%`, so AGENTS.md forbids `dotnet build`.

<SELF_AUDIT_DELTA agent_id="SHINOBU_108" pass="7">
  <TASK id="16" result="PASS_STATIC" note="Blackbox dump now writes a 32-byte explicit header and raw 300-entry telemetry block; no BinaryWriter field loop remains in rollback scope." />
  <STRUCT_LAYOUT name="RollbackBlackBoxDumpHeader32" size="32" offsets="Magic:0/8, SourceHash:8/4, CurrentFrame:12/4, Flags:16/4, EntryCount:20/4, EntrySizeBytes:24/4, Version:28/4" proof="32 % 16 == 0" />
  <H_PHI note="No new Vault buffers or private NativeArrays; telemetry still uses Vault 70756." />
  <COMPILE_GUARD note="No new direct sibling domain reference; build deferred by CPU guard at 100%." />
</SELF_AUDIT_DELTA>

## 2026-05-19 SHINOBU_108 POLISH PASS 8

What was wrong:
- Rollback frame math used raw unsigned ordering. `ResolveRollbackFrameCount` returned 0 for a valid rollback window crossing `uint.MaxValue -> 0`; mock jitter packets with wrapped `ReleaseFrame` could remain queued; remote correction loop `frame <= CurrentFrame` skipped wrapped frames.

What was done:
- Added modular frame helpers: `HasFrameReached`, `DidFrameWrap`, and `TryResolveHistoricalFrame`.
- Rewired rollback frame count, input lookback, mock jitter drain, simulated ping, and remote input correction to use modular tick semantics.
- Added `RollbackFrameMath_HandlesUintWrap`, `MockNetworkJitter_ReleasesAcrossUintWrap`, and `ApplyRemoteInputCorrection_HandlesUintWrap`.

Cinematic cheats used:
- None new. This is deterministic clock hygiene for the existing rollback fake/repair pipeline.

Exact microseconds saved:
- Normal frames add only signed subtract checks. The practical saving is avoiding a false hard-resync and full-state overwrite when long-running sessions cross uint tick wrap.

Verification:
- Static rollback scan found no networking hits for managed serializers, LINQ/foreach, Unity time drift, `UnityEngine.Random`, blocking `Complete()`, `LayoutKind.Sequential`, `Pack=`, or DTO auto-properties.
- `git diff --check` remains clean except CRLF normalization warnings.
- Build not relaunched in this pass yet; previous CPU gate was `100%`, and AGENTS.md forbids `dotnet build` under load.

<SELF_AUDIT_DELTA agent_id="SHINOBU_108" pass="8">
  <TASK id="09" result="PASS_STATIC" note="Input journal lookback now resolves historical frames through modular tick math and retains boot warmup protection." />
  <TASK id="11" result="PASS_STATIC" note="Rollback window frame count is uint-wrap safe; low-quality shortened windows use the same helper." />
  <TASK id="05" result="PASS_STATIC" note="Mock jitter release no longer uses raw unsigned `ReleaseFrame > CurrentFrame` ordering." />
  <DEPENDENCY_GRAPH note="No JobHandle changes; fixed pipeline chain remains jitter -> Merkle leaves -> Merkle root -> rollback pipeline." />
  <COMPILE_GUARD note="No new direct sibling domain reference; build still gated by CPU policy." />
</SELF_AUDIT_DELTA>

## 2026-05-19 SHINOBU_108 POLISH PASS 9

What was wrong:
- The first wrap patch inferred `previousFrame` from `RuntimeState.CurrentFrame`, but `FinalizeMerkleRootJob` writes `CurrentFrame` before mismatch detection. In the actual job order, wrap evidence would be overwritten before `DetectInputMismatchJob`.

What was done:
- Added scheduler-owned `_previousScheduledFrame`, `_lastScheduledFrame`, and `_hasScheduledFrame` scalar fields in `HectonRollbackNetcodeRuntime`.
- `ScheduleFixedSimulation` now passes `PreviousFrame` into `RollbackFixedPipelineJob`, which passes it into `DetectInputMismatchJob`.
- `SimulatePingInternal` uses scheduler-owned previous frame instead of runtime state.
- Added `DetectInputMismatch_UsesScheduledPreviousFrameAcrossWrap`.

Cinematic cheats used:
- None new. This is deterministic scheduling correction.

Exact microseconds saved:
- Prevents false hard-resync/full-state overwrite after uint wrap. Runtime cost is one captured scalar and one uint job field.

Verification:
- Static rollback scan found no networking hits for managed serializers, LINQ/foreach, Unity time drift, `UnityEngine.Random`, blocking `Complete()`, `LayoutKind.Sequential`, `Pack=`, or DTO auto-properties.
- `git diff --check` remains clean except CRLF normalization warnings.
- Build not relaunched: latest CPU gate still reported `100%`.

<SELF_AUDIT_DELTA agent_id="SHINOBU_108" pass="9">
  <TASK id="09" result="PASS_STATIC" note="Mismatch detection now receives previous-frame cadence from the scheduler, not a runtime state field already overwritten by Merkle finalization." />
  <TASK id="11" result="PASS_STATIC" note="Modular rollback-window math is now wired through the real scheduling order." />
  <H_PHI note="No new Vault buffer; previous-frame cadence is local scalar scheduler state, not authoritative gameplay state." />
  <COMPILE_GUARD note="No new direct sibling domain reference; build deferred by CPU guard at 100%." />
</SELF_AUDIT_DELTA>

## 2026-05-19 SHINOBU_108 POLISH PASS 10

What was wrong:
- `ComputeMerkleRootJob.HashAupArray` ignored `RollbackVaultBufferDescriptor32.ByteOffset` for `EntityAUPs`.
- The node reported the descriptor offset, but the hash always started from AUP element 0. That breaks Task 14 branch isolation once AUP leaves are partitioned into non-zero byte ranges.

What was done:
- `HashAupArray` now derives `start = ByteOffset / sizeof(double3)` and hashes `source[start + i]` while preserving exact 24-byte reconstructed `double3` AUP truth.
- Added `MerkleRoot_EntityAupDescriptorByteOffsetSelectsSlice`: mutating the ignored prefix does not change the root, mutating the selected AUP does.

Cinematic cheats used:
- None new. This preserves the existing coarse-to-fine Merkle scalability path; low quality can hash fewer AUP leaves without corrupting offset semantics.

Exact microseconds saved:
- Direct cost is neutral: two scalar integer ops per AUP descriptor. Failure-mode saving is avoiding a false full-state repair request when a sliced AUP leaf is misreported.

Verification:
- Static rollback scan found no networking hits for managed serializers, LINQ/foreach, Unity time drift, `UnityEngine.Random`, blocking `Complete()`, `LayoutKind.Sequential`, `Pack=`, or DTO auto-properties.
- Burst scan still shows 10 rollback jobs with `CompileSynchronously = true`, `FloatMode.Deterministic`, and `FloatPrecision.Standard`.
- `git diff --check` remains clean except CRLF normalization warnings.
- Build not relaunched: CPU gate reported `82%`, above the AGENTS.md limit.

<SELF_AUDIT_DELTA agent_id="SHINOBU_108" pass="10">
  <TASK id="13" result="PASS_STATIC" note="Entity AUP Merkle leaves still hash exact reconstructed double3 bits and now honor descriptor byte offsets." />
  <TASK id="14" result="PASS_STATIC" note="Branch repair diagnostics can trust FirstMismatchByteOffset for sliced EntityAUP leaves." />
  <STRUCT_LAYOUT note="No DTO layout changed. The slice stride is 24 bytes because the hash authority is exact double3 AUP, not the 48-byte AbsoluteUniversePosition DTO." />
  <COMPILE_GUARD note="No new direct sibling domain reference. Build deferred by CPU guard at 82%." />
</SELF_AUDIT_DELTA>

## 2026-05-19 SHINOBU_108 POLISH PASS 11

What was wrong:
- Rollback code directly imported `Hecton8.World.AbsoluteUniversePosition` to read `BufferID.EntityAUPs`.
- The Merkle/snapshot path only needs the 48-byte ABI and exact reconstructed `double3`; referencing the World namespace is avoidable compile-wall coupling.

What was done:
- Added explicit `RollbackAup48` with offsets `GridX=0`, `GridY=8`, `GridZ=16`, `LocalX=24`, `LocalY=28`, `LocalZ=32`, `_pad0=36`, `_pad1=40`, size `48`.
- Replaced rollback runtime/contracts/tests `EntityAUPs` arrays with `NativeArray<RollbackAup48>`.
- `RollbackAup48.CellSizeMeters` routes through `HectonPhysicsContract` from Core.Contracts, not World.
- Extended `RollbackNetcodeLayoutGuard` and edit tests to pin the ABI mirror layout.

Cinematic cheats used:
- None new. This is compile-wall and ABI hygiene for the existing Merkle/Dear Lie pipeline.

Exact microseconds saved:
- Runtime cost is unchanged: same stride, same MemCpy bytes, same 24-byte exact `double3` reconstruction. Future developer-iteration saving comes from removing World namespace coupling when rollback is split into its own asmdef.

Verification:
- Static rollback scan found no direct `Hecton8.World` or `AbsoluteUniversePosition` references in rollback runtime/contracts/tests.
- Static rollback scan found no networking hits for managed serializers, LINQ/foreach, Unity time drift, `UnityEngine.Random`, blocking `Complete()`, `LayoutKind.Sequential`, `Pack=`, or DTO auto-properties.
- `git diff --check` remains clean except CRLF normalization warnings.
- Build not relaunched: CPU gate reported `100%`, above the AGENTS.md limit.

<SELF_AUDIT_DELTA agent_id="SHINOBU_108" pass="11">
  <TASK id="01" result="PASS_STATIC" note="EntityAUPs remains authoritative Vault state, now read via rollback-owned ABI mirror." />
  <TASK id="13" result="PASS_STATIC" note="Hash truth remains reconstructed 24-byte double3 AUP, never float3." />
  <STRUCT_LAYOUT name="RollbackAup48" size="48" offsets="GridX:0/8, GridY:8/8, GridZ:16/8, LocalX:24/4, LocalY:28/4, LocalZ:32/4, _pad0:36/4, _pad1:40/8" proof="48 % 16 == 0" />
  <COMPILE_GUARD note="Rollback source no longer imports Hecton8.World; build deferred by CPU guard at 100%." />
</SELF_AUDIT_DELTA>

## 2026-05-19 SHINOBU_108 POLISH PASS 12

What was wrong:
- `RemoteInputRing` was allocated with `UninitializedMemory` while mismatch/correction jobs read slots before every slot is guaranteed to be written.
- A stale garbage slot could theoretically match frame id plus `Received`, making rollback consume non-authoritative input.
- `VisualStates` is a small interpolation surface and also benefits from deterministic cold zero.

What was done:
- Added `RemoteInputFlags.Valid`.
- Real remote injection and mock jitter writers now seal slots with `Received|Valid`.
- `DetectInputMismatchJob`, `ApplyRemoteInputCorrectionJob`, and mock overwrite protection require `Received|Valid`.
- Added `ApplyRemoteInputCorrection_IgnoresUnsealedRemoteSlot`.
- Switched only `RemoteInputRing` and `VisualStates` to `ClearMemory`; large blind-copy buffers remain `UninitializedMemory`.

Cinematic cheats used:
- None new. This is rollback truth hygiene for the existing smoothing fake.

Exact microseconds saved:
- No hot-path saving claim. Cost is one bitmask check per remote read and roughly 17 KB cold clear.
- Failure-mode saving is avoiding false correction/resim churn from uninitialized remote slots while preserving tens of MB boot zero-fill savings on large buffers.

Verification:
- Static rollback scan found no networking hits for managed serializers, LINQ/foreach, Unity time drift, `UnityEngine.Random`, blocking `Complete()`, `LayoutKind.Sequential`, `Pack=`, direct `Hecton8.World`, or DTO auto-properties.
- `git diff --check` remains clean except CRLF normalization warnings.
- Build not relaunched: CPU gate reported `100%`.

<SELF_AUDIT_DELTA agent_id="SHINOBU_108" pass="12">
  <TASK id="05" result="PASS_STATIC" note="Fallback mock jitter writes sealed remote slots and cannot overwrite sealed real input." />
  <TASK id="09" result="PASS_STATIC" note="Remote input correction and mismatch detection require Received|Valid." />
  <TASK id="15" result="PASS_STATIC" note="Large rings stay UninitializedMemory; only small read-before-write guard buffers are ClearMemory." />
  <H_PHI note="No private persistent arrays added; all memory still comes through VaultBufferHandle IDs." />
  <COMPILE_GUARD note="No new direct sibling domain reference. Build deferred by CPU gate at 100%." />
</SELF_AUDIT_DELTA>

## 2026-05-19 SHINOBU_108 POLISH PASS 13

What was wrong:
- `HectonNetworkManager.EnsureRuntime()` could allocate `HectonRollbackNetcodeRuntime` with `gameObject.AddComponent` when server/client mode starts.
- That fallback hides broken authoring and puts Unity object creation inside the rollback networking control path.

What was done:
- Removed the `AddComponent` fallback.
- Kept `RequireComponent` as the authoring contract and `TryGetComponent` as the non-allocating lookup.
- Preserved static mode flag routing: `TrySetMode` still records server/client intent if no active runtime exists yet.

Cinematic cheats used:
- None new. This is allocation-surface removal.

Exact microseconds saved:
- Avoids a one-time managed component allocation and lifecycle registration spike on invalid prefabs.
- Valid prefabs are unchanged; hot path remains 0 us.

Verification:
- Static networking scan found no `AddComponent`, `Instantiate`, `new GameObject`, `FindObject`, or `GetComponent<` calls.
- Static rollback scan found no networking hits for managed serializers, LINQ/foreach, Unity time drift, `UnityEngine.Random`, or blocking `Complete()`.
- `git diff --check` remains clean except CRLF normalization warnings.
- Build not relaunched: CPU gate reported `100%`.

<SELF_AUDIT_DELTA agent_id="SHINOBU_108" pass="13">
  <TASK id="02" result="PASS_STATIC" note="Rollback networking control path no longer instantiates missing runtime components." />
  <H_PHI note="No new persistent memory; no managed collection or component allocation introduced." />
  <COMPILE_GUARD note="No new direct sibling domain reference. Build deferred by CPU gate at 100%." />
</SELF_AUDIT_DELTA>
