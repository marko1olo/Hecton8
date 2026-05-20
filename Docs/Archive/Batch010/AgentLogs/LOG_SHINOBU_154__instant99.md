# SHINOBU_154 Entity Delta Save WAL Log

Timestamp: 2026-05-19 local
Agent: SHINOBU_154
Domain: Echelon 1 Core & Memory Infrastructure / SaveSystem Data Archivist
State: STATIC IMPLEMENTATION IN PROGRESS - UNITY/BURST RUNTIME PROOF PENDING

## What Was Wrong

- No dedicated entity delta WAL payload lane existed next to the voxel delta compressor.
- `entity_save_schema.h8bin` is absent, so CI/profile work needed a deterministic unmanaged fallback schema.
- Dynamic entity save truth was at risk of being pulled from managed/Unity-facing compatibility DTOs instead of a flat Vault lane.
- Existing native LZ4 binding surface is not callable from Burst jobs with a dictionary; managed compression or P/Invoke from a job would violate the hot-path mandate.

## What Was Done

- Added `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs`.
- Added explicit ARM64-safe DTOs: `EntityDeltaHeaderDTO` 32B, `EntityDeltaDataRecordDTO` 80B, `EntityDeltaBlockCounter64` 64B, telemetry/tuning/stats/profile/dump DTOs.
- Added Vault buffers `SaveEntityDeltaSchemaBytes` through `SaveEntityDeltaWalPayloadBytes` (`70340..70357`) under `SystemID.SavePersistence`.
- Added payload type `H8WorldPagePayloadTypes.EntityDeltaRle`.
- Added Burst jobs for deterministic mock entity state, tombstone pruning, delta extraction, dense packing, byte-RLE preconditioning, LZ4-format block compression, checksum header write, WAL payload packing, telemetry write, latency patch, and CSV profile parse.
- Added `Entity Save Tuner` editor facade with UI Toolkit tuning sliders, telemetry histogram, and SceneView sector heatmap.
- Added binary layout manifest assertions and a binary payload ledger entry for SHINOBU_154.

## Cinematic Cheats Used

- Dear Lie dehydration: save AUP sector/local offset, hashes, compact vitals, quantity, flags, and simulation tick. Do not save velocity, current target, animation frame, or AI transient state.
- Procedural rehydration contract: after load, fauna/module/resource owners reconstruct transient behavior from deterministic world state and hashes.
- RLE preconditioning before LZ4: repeated zero/hash-byte lanes from sparse deltas are compressed before the LZ4 token pass.

## Microseconds Saved

Static estimate only; profiler proof pending because build gate is closed by CPU load.

- Object graph traversal/JSON/BinaryFormatter route: rejected. Expected savings on i3/MX350 are in the millisecond class during autosave spikes.
- Full entity snapshot bytes: `entityCount * 80`; stored path targets `deltaCount * 80`, then RLE/LZ4.
- Header read alignment: 32B explicit layout, 8-byte checksum/hash alignment; prevents ARM64 unaligned-access penalty.
- Counter writes: 64B block counters to reduce false-sharing risk during parallel extraction.

<SELF_AUDIT agent_id="SHINOBU_154">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Absent schema path handled by deterministic unmanaged emergency schema.</TASK>
    <TASK id="02" status="PASS">Target entity save route now bypasses managed serializers; unrelated owner JSON/ISerialization surfaces were not mutated.</TASK>
    <TASK id="03" status="PASS">New hot DTOs expose public fields only; no properties in the save lane.</TASK>
    <TASK id="04" status="PASS">Header 32B explicit layout and manifest offset checks added.</TASK>
    <TASK id="05" status="PASS">Burst deterministic mock state generator added.</TASK>
    <TASK id="06" status="PASS">Block-parallel delta extraction added; NativeList replaced by Vault NativeArray cursors for H-PHI compliance.</TASK>
    <TASK id="07" status="PASS_WITH_DEVIATION">Burst LZ4-format encoder added. Native dictionary P/Invoke rejected inside Burst until a safe binding exists.</TASK>
    <TASK id="08" status="PASS">Dear Lie dehydration records only persistent facts.</TASK>
    <TASK id="09" status="PASS">WAL payload is handed to existing async pager service; compressor does not own synchronous file I/O.</TASK>
    <TASK id="10" status="PASS">Compression effort is continuous across quality, I/O pressure, and disk latency.</TASK>
    <TASK id="11" status="PASS">Checksum generated and verified from compressed payload bytes.</TASK>
    <TASK id="12" status="PASS">AUP sector hash uses integer sector coordinates; pager key mixes payload type without corrupting header sector truth.</TASK>
    <TASK id="13" status="PASS">Expired tombstones are pruned before serialization.</TASK>
    <TASK id="14" status="PASS">All jobs use deterministic Burst float mode and simulation-frame inputs.</TASK>
    <TASK id="15" status="PASS">Large Vault staging buffers request uninitialized memory.</TASK>
    <TASK id="16" status="PASS">300-frame telemetry ring and dump path added.</TASK>
    <TASK id="17" status="PASS">UI Toolkit editor tuner added.</TASK>
    <TASK id="18" status="PASS">Burst byte CSV parser added for tuning/profile DTOs.</TASK>
    <TASK id="19" status="PASS">SceneView sector heatmap added.</TASK>
    <TASK id="20" status="PARTIAL">Self-audit routine and static checks added; Unity import, Burst compile, profiler, WAL replay, and 99 percent runtime ratio proof pending.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <EntityDeltaHeaderDTO size="32">
      <field name="SectorHash" offset="0" size="8"/>
      <field name="CompressedSize" offset="8" size="4"/>
      <field name="UncompressedSize" offset="12" size="4"/>
      <field name="XXHash3Checksum" offset="16" size="8"/>
      <field name="_pad0" offset="24" size="4"/>
      <field name="_pad1" offset="28" size="4"/>
      <proof>32 bytes total, multiple of 16; all 64-bit fields start at 8-byte boundaries.</proof>
    </EntityDeltaHeaderDTO>
    <EntityDeltaDataRecordDTO size="80">
      <field name="SectorX" offset="0" size="8"/>
      <field name="SectorY" offset="8" size="8"/>
      <field name="SectorZ" offset="16" size="8"/>
      <field name="LocalX/Y/Z" offset="24" size="12"/>
      <field name="EntityKindHash" offset="36" size="4"/>
      <field name="StableEntityHash" offset="40" size="8"/>
      <field name="ArchetypeHash" offset="48" size="4"/>
      <field name="InventoryHash" offset="52" size="4"/>
      <field name="InstanceUid" offset="56" size="4"/>
      <field name="Quantity/Health/Hunger/Integrity" offset="60" size="8"/>
      <field name="Flags" offset="68" size="4"/>
      <field name="BaselineHash32" offset="72" size="4"/>
      <field name="SimulationTick" offset="76" size="4"/>
      <proof>80 bytes total, multiple of 16; primary 64-bit hash at offset 40 is 8-byte aligned.</proof>
    </EntityDeltaDataRecordDTO>
    <EntityDeltaBlockCounter64 size="64">
      <proof>One cache line per block counter; prevents worker false sharing on adjacent counters.</proof>
    </EntityDeltaBlockCounter64>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    GlobalQualityWeight and I/O pressure drive `ResolveCompressionEffort01`. Below 0.3 quality, LZ4 active hash slots collapse toward 512, minimum match length trends toward 8 bytes, probe step trends toward 4, write Hz trends toward the low tuning endpoint, and RLE bypass is allowed when the byte-RLE saving threshold is not met. High/Ultra increase hash coverage and probe density without changing payload schema.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    No private persistent NativeArray/NativeList/NativeHashMap is declared by the compressor.
    VaultBufferHandle IDs: 70340,70341,70342,70343,70344,70345,70346,70347,70348,70349,70350,70351,70352,70353,70354,70355,70356,70357.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    Jobs consume caller dependency and output a chained JobHandle: mock(optional) -> prune -> extract -> finalize -> dense pack -> RLE -> LZ4 -> checksum -> WAL pack -> telemetry.
    NativeArray fields in jobs use [NoAlias] and [ReadOnly] where applicable; parallel output buffers use NativeDisableParallelForRestriction only for indexed block-owned writes.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    New runtime file imports Core, Core.Contracts, Core.Memory, Core.Memory.Layout, Unity Burst/Collections/Jobs/Mathematics, and BCL only. No direct sibling gameplay/world/fauna/construction dependency was added.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    The fake is dehydrated persistence: skip transient AI/animation/velocity state and save compact persistent facts. Heavy path before: serialize object graph plus transient state, O(n objects + object graph edges) with GC risk. New path: O(n flat records) scan and O(delta bytes) payload, with worker compression and async WAL handoff.
  </DEAR_LIE_CONFIRMATION>
  <PROOF_BOUNDARY>
    Static checks passed: focused forbidden-pattern scan for new files, focused direct-sibling using scan, focused git diff --check. dotnet/Unity compile was not launched because CPU gate reported 100 percent load.
  </PROOF_BOUNDARY>
</SELF_AUDIT>

## 2026-05-19 Polish Addendum - Literal Gizmo Hook and Ref Access

What was wrong:
- Task 19 had a SceneView overlay, but the prompt explicitly required an `OnDrawGizmos` hook.
- The extraction/pruning hot loops still used `NativeArray<T>` indexers for several DTO reads/writes, which weakens the CS1612/ref-mutation proof even when structs have raw public fields.

What was done:
- Added `Assets/_Project/Scripts/SaveSystem/EntityDeltaGizmoProbe.cs` with `EntityDeltaGizmoProbe.OnDrawGizmos`.
- The gizmo reads only `SaveEntityDeltaSectorStats` from `GlobalDataVault` and draws sector wire boxes colored by `CompressedBytes / 131072`.
- The UI Toolkit tuner now reuses the same heatmap draw path for its SceneView overlay.
- Added `ElementAsRef<T>` and `ElementAsReadOnlyRef<T>` backed by `UnsafeUtility.AsRef`.
- `GenerateMockEntityStateJob`, `EntityTombstonePruneJob`, and `ExtractEntityDeltaJob` now mutate/read record and counter DTOs through ref access in their hot loops.
- Added stable Unity `.meta` files for all three new C# assets in the SHINOBU_154 lane.

Cinematic Cheats used:
- Save visuals remain an editor-only heatmap of compressed sector bloat. No runtime simulation, no object graph scan, no GameObject enumeration.

Exact Microseconds saved:
- Not measured. Static-only claim: ref access removes avoidable DTO indexer-copy risk across thousands of 80-byte records; Unity/Burst profiler proof is still pending.

Verification:
- Focused `git diff --check`: clean; only existing CRLF normalization warnings in tracked files.
- Forbidden-pattern scan on SHINOBU_154 runtime/editor files: no `Pack=1`, DTO properties, JSON, `MemoryStream`, `BinaryFormatter`, persistent native allocations, `foreach`, or arbitrary `Complete()`.
- Direct sibling namespace scan on SHINOBU_154 files: clean.
- Compiler gate: no `dotnet`/`csc` process, CPU load reported 100 percent, so build was not launched.

## 2026-05-19 Polish Addendum - Burst Compression Ratio Audit

What was wrong:
- Task 20's 99 percent smaller-than-full verification existed as a cold helper and Markdown proof, but it was not schedulable inside the same Burst/job dependency world as the compressor.

What was done:
- Added `EntityDeltaCompressionRatioAuditJob`.
- Added `ScheduleCompressionRatioSelfAudit(...)`.
- Added counters `CounterAuditSamples`, `CounterAuditSmallerPayloads`, and `CounterAuditPass`.
- Fatal telemetry samples are ignored so failed saves cannot be counted as successful compression wins.

Cinematic Cheats used:
- None. This is a bounded forensic pass over 300 telemetry entries, not a visual simulation.

Exact Microseconds saved:
- Not measured. Static cost bound is 300 telemetry records per audit call; the value is correctness proof, not frame-time savings.

Verification:
- Focused `git diff --check`: clean; only existing CRLF normalization warning in the architecture ledger.
- Forbidden-pattern scan on SHINOBU_154 runtime/editor files: clean.
- Burst directive scan: 13 jobs, all deterministic because save bytes participate in rollback/netcode hashing.
- `[NoAlias]` field scan: present across extraction, pack, compression, checksum, telemetry, audit, and CSV jobs.
- Untracked new-file hygiene: no trailing whitespace; brace counts balanced for the three new C# files; `.meta` GUIDs are valid 32-hex IDs.
- Compile/runtime proof still gated: no `dotnet`/`csc` process was visible, but the CPU telemetry command timed out under load before a safe below-50-percent reading was available.

Compile wall reality:
- SaveSystem currently sits under the existing root `Assets/_Project/Scripts/Hecton8.Core.asmdef`.
- That root asmdef already references multiple sibling runtime assemblies before SHINOBU_154.
- SHINOBU_154 did not add or edit that asmdef. A new `Hecton8.SaveSystem.asmdef` was rejected in this lane because it would require an integrator-owned split across existing SaveManager, Merkle, voxel, and layout-manifest routes.
- File-level direct sibling namespace scan for SHINOBU_154 code is clean.

## 2026-05-19 Polish Addendum - Audit Chain Integration

What was wrong:
- `EntityDeltaCompressionRatioAuditJob` existed, but a caller could forget to schedule it after the save pipeline.

What was done:
- `ScheduleCompressionPipeline` now chains `EntityDeltaCompressionRatioAuditJob` after `EntityDeltaTelemetryRecordJob`.
- The returned handle now includes the ratio audit whenever telemetry buffers are resolved.

Cinematic Cheats used:
- None. This is verification plumbing.

Exact Microseconds saved:
- None claimed. The added cost is bounded to 300 telemetry reads and 3 counter writes after autosave telemetry.

## 2026-05-19 Polish Addendum - RLE Stream Header and Replay Validation

What was wrong:
- The outer WAL header proved checksum and compressed/uncompressed byte counts, but it did not describe whether the inner preconditioned stream was RLE pairs or raw dense fallback.
- Raw dense fallback can be byte-identical to plausible `{run,value}` pair data, so replay code needed an explicit mode contract rather than inference.

What was done:
- Added `EntityDeltaRleStreamHeaderDTO`, explicit 16 bytes: `Magic=0`, `Flags=4`, `DenseBytes=8`, `StoredBytes=12`.
- `EntityRlePreconditionJob` writes the inner header before either RLE pairs or raw dense fallback.
- `ResolveRleStagingBytes` sizes RLE/compressed/WAL staging for the inner stream header plus worst-case pair expansion plus the outer entity header.
- `TryReadAndVerifyWalPayload` now validates the inner stream when the LZ4 stage stores raw bytes.
- Added `TryValidateRleStreamPayload` for load code after LZ4 decompression; it rejects missing magic, multiple mode bits, zero dense bytes, raw dense size mismatch, odd pair payloads, zero runs, and decoded-byte mismatches.
- Added layout-manifest and `RunSelfAudit` hooks for the new 16-byte stream header.
- Added short partition comments above the `NativeDisableParallelForRestriction` fields that write by owned index/range.

Cinematic Cheats used:
- None. This is binary replay hardening.

Exact Microseconds saved:
- None claimed. This patch spends a fixed 16 bytes and cold replay validation to avoid corrupt hydration. The runtime hot save path remains Vault/Burst/async WAL.

Verification:
- Focused `git diff --check`: clean; existing CRLF normalization warnings only.
- Forbidden-pattern scan on SHINOBU_154 runtime/editor files: clean.
- Burst directive scan: 13 jobs, 13 deterministic directives.
- Compile gate: no `dotnet`/`csc` process, CPU load reported 100 percent, so build was not launched.

<SELF_AUDIT agent_id="SHINOBU_154" revision="RLE_STREAM_HEADER_REPLAY_VALIDATION">
  <TASK_RECONCILIATION>
    <task id="01" status="PASS">No `entity_save_schema.h8bin` exists; deterministic Vault-backed emergency schema generator is present.</task>
    <task id="02" status="PASS">Entity save route avoids JSON/reflection/object graph serialization; unrelated owner surfaces were not rewritten in this lane.</task>
    <task id="03" status="PASS">Owned save DTOs expose public fields only and hot jobs use `UnsafeUtility.AsRef` helpers.</task>
    <task id="04" status="PASS">Outer header 32B, inner RLE stream header 16B, entity record 80B, counter 64B; manifest/self-audit offsets are wired.</task>
    <task id="05" status="PASS">`GenerateMockEntityStateJob` injects deterministic flat entity state into Vault buffers.</task>
    <task id="06" status="PASS">`ExtractEntityDeltaJob` emits changed records into block-owned dense ranges without `NativeList` growth.</task>
    <task id="07" status="PASS">Burst deterministic LZ4-block encoder is present; native dictionary binding was rejected because the project exposes no Burst-callable dictionary API.</task>
    <task id="08" status="PASS">Dehydrated record keeps AUP/hash/vitals/inventory and discards transient AI, velocity, target, and animation phase.</task>
    <task id="09" status="PASS">WAL payload is packed into Vault bytes and enqueued through `IAsyncPersistenceService`; direct synchronous file ownership rejected.</task>
    <task id="10" status="PASS">Compression effort uses `GlobalQualityWeight`, I/O pressure, disk latency, `math.lerp`, and smoothstep curves.</task>
    <task id="11" status="PASS">XXHash3-derived 64-bit checksum is written to `EntityDeltaHeaderDTO` and checked before replay.</task>
    <task id="12" status="PASS">Sector hash derives from integer AUP sector coordinates and payload-type pager mixing.</task>
    <task id="13" status="PASS">`EntityTombstonePruneJob` removes expired tombstones before dense extraction.</task>
    <task id="14" status="PASS">Jobs use deterministic Burst float mode and simulation frame/tick data, not `Time.deltaTime`.</task>
    <task id="15" status="PASS">Vault staging buffers request `UninitializedMemory` where fully overwritten by jobs.</task>
    <task id="16" status="PASS">300-entry telemetry ring and dump path `Docs/AgentLogs/Dump_ENTITY_IO_SURGEON.bin` are present.</task>
    <task id="17" status="PASS">Editor UI Toolkit tuner reads telemetry and writes Vault tuning DTO.</task>
    <task id="18" status="PASS">CSV profile ingest parses bytes into unmanaged tuning/profile DTOs without managed tokenization in the job.</task>
    <task id="19" status="PASS">`EntityDeltaGizmoProbe.OnDrawGizmos` draws sector heat boxes from Vault stats.</task>
    <task id="20" status="FAIL">Static self-audit, Burst telemetry audit, and replay validation exist; Unity import, Burst compile, profiler, WAL replay, and 99-percent runtime proof are still gated by CPU/compile policy.</task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <EntityDeltaHeaderDTO size="32">
      <field name="SectorHash" offset="0" size="8"/>
      <field name="CompressedSize" offset="8" size="4"/>
      <field name="UncompressedSize" offset="12" size="4"/>
      <field name="XXHash3Checksum" offset="16" size="8"/>
      <field name="_pad0" offset="24" size="4"/>
      <field name="_pad1" offset="28" size="4"/>
      <proof>32B total; multiple of 16; 64-bit fields at offsets 0 and 16.</proof>
    </EntityDeltaHeaderDTO>
    <EntityDeltaRleStreamHeaderDTO size="16">
      <field name="Magic" offset="0" size="4"/>
      <field name="Flags" offset="4" size="4"/>
      <field name="DenseBytes" offset="8" size="4"/>
      <field name="StoredBytes" offset="12" size="4"/>
      <proof>16B total; multiple of 16; no unaligned 64-bit fields.</proof>
    </EntityDeltaRleStreamHeaderDTO>
    <EntityDeltaDataRecordDTO size="80">
      <field name="SectorX" offset="0" size="8"/>
      <field name="SectorY" offset="8" size="8"/>
      <field name="SectorZ" offset="16" size="8"/>
      <field name="LocalX/Y/Z" offset="24" size="12"/>
      <field name="EntityKindHash" offset="36" size="4"/>
      <field name="StableEntityHash" offset="40" size="8"/>
      <field name="ArchetypeHash" offset="48" size="4"/>
      <field name="InventoryHash" offset="52" size="4"/>
      <field name="InstanceUid" offset="56" size="4"/>
      <field name="Quantity/Health/Hunger/Integrity" offset="60" size="8"/>
      <field name="Flags" offset="68" size="4"/>
      <field name="BaselineHash32" offset="72" size="4"/>
      <field name="SimulationTick" offset="76" size="4"/>
      <proof>80B total; multiple of 16; `StableEntityHash` offset 40 is 8-byte aligned.</proof>
    </EntityDeltaDataRecordDTO>
    <EntityDeltaBlockCounter64 size="64">
      <proof>Explicit 64B cache-line counter prevents false sharing between parallel block workers.</proof>
    </EntityDeltaBlockCounter64>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Below `GlobalQualityWeight` 0.3, `ResolveCompressionEffort01` collapses LZ4 hash coverage toward 512 slots, match length toward 8, probe step toward 4, and write cadence toward the low-Hz endpoint. RLE remains the cheap preconditioner, and raw dense fallback is accepted only with an explicit stream header.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    The compressor declares zero private persistent `NativeArray`, `NativeList`, or `NativeHashMap` fields. Vault IDs requested at boot/resolution: 70340..70357 (`SaveEntityDeltaSchemaBytes` through `SaveEntityDeltaWalPayloadBytes`).
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    Consumes caller dependency and returns `mock optional -> prune -> extract -> finalize -> dense pack -> RLE -> LZ4 -> checksum -> WAL pack -> telemetry -> ratio audit`. Job fields use `[NoAlias]`; block-owned parallel writes document their index/range partition.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    SHINOBU_154 added no new asmdef reference. Entity save files use Core/Core.Contracts/Core.Memory/Core.Memory.Layout and Unity packages only; root `Hecton8.Core.asmdef` sibling references pre-existed and remain integrator-owned.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Dear Lie: persist dehydrated facts, not live fish/base/object simulation state. Before: object graph plus transient state, O(n objects + graph edges) with GC risk. After: O(n flat records) scan, O(delta bytes) pack/compress, async WAL handoff.
  </DEAR_LIE_CONFIRMATION>
  <PROOF_BOUNDARY>
    Static gates passed: forbidden-pattern scan clean, 13/13 Burst directives deterministic, braces balanced, no trailing whitespace, direct sibling namespace scan clean. Build was not launched because CPU load remained 99-100 percent despite no `dotnet` or `csc` process.
  </PROOF_BOUNDARY>
</SELF_AUDIT>

## 2026-05-19 Polish Addendum - Post-Pack WAL Envelope Audit

What was wrong:
- The checksum job sealed `CompressedBytes`, but the WAL buffer is produced by a later pack job using a byte-count counter. If that counter or the copy went stale, the returned handle could still look valid until load.
- Rechecking this in `TryEnqueueEntityDeltaWalWrite` would put O(payload) checksum work back on the main-thread enqueue boundary.

What was done:
- Added `EntityWalPayloadEnvelopeAuditJob` after `EntityWalPayloadPackJob`.
- The audit rereads the serialized outer header from `WalPayloadBytes`, compares it to `Headers[0]`, validates `CompressedSize`, `UncompressedSize`, packed byte count, checksum, and raw RLE stream header when LZ4 is bypassed.
- Added `CounterWalEnvelopeAuditPass` at counter index `18`.
- No-delta header-only saves pass the audit without being enqueued as WAL writes.
- `ScheduleCompressionPipeline` now returns a handle that includes WAL pack and WAL envelope audit even when telemetry buffers are missing.
- `TryEnqueueEntityDeltaWalWrite` now requires `CounterWalEnvelopeAuditPass == 1` and rejects counter byte counts outside the WAL buffer length instead of silently clamping.

Cinematic Cheats used:
- None. This is dependency-graph hardening, not simulation.

Exact Microseconds saved:
- None claimed. This deliberately spends one worker checksum pass to prevent bad WAL bytes from escaping the pipeline. Main-thread enqueue avoids the extra rehash.

Verification:
- Focused `git diff --check`: clean.
- Forbidden-pattern scan on SHINOBU_154 runtime/editor files: clean.
- Burst directive scan: 14 jobs, 14 deterministic directives.

## 2026-05-19 Polish Addendum - Burst WAL Decode and RLE Expand Path

What was wrong:
- Save bytes could be verified by helper code, but there was no Burst load-side path that decoded a WAL payload back into flat entity records using Vault buffers.
- That left Task 11 load verification and Task 20 replay proof dependent on future caller discipline.

What was done:
- Added `ScheduleWalPayloadDecodePipeline(...)`.
- Added `EntityWalPayloadDecodeJob`: strict byte-count validation, outer header read, checksum verification, raw-RLE copy or deterministic LZ4 decode into Vault `RleBytes`.
- Added `EntityRleStreamExpandToRecordsJob`: RLE stream header validation, raw dense copy or `{run,value}` expansion into Vault `DenseBytes`, then whole-record copy into `DeltaRecords`.
- Added decode counters `CounterDecodeDenseBytes`, `CounterDecodeRecordCount`, and `CounterDecodePass`.
- Tightened cold validation helpers so caller-supplied byte counts must be in range instead of being silently clamped.

Cinematic Cheats used:
- Dear Lie remains dehydrated hydration: only persistent records are restored. AI targets, animation phase, and velocity are intentionally absent and must be recalculated by owner systems.

Exact Microseconds saved:
- Not measured. This removes future managed loader pressure; runtime proof remains pending behind the compile/CPU gate.

Verification:
- Focused `git diff --check`: clean.
- Forbidden-pattern scan on SHINOBU_154 runtime/editor files: clean.
- Burst directive scan: 16 jobs, 16 deterministic directives.

## 2026-05-19 Polish Addendum - Public Contract Guard and No-Op WAL Parity

What was wrong:
- `TryEnqueueEntityDeltaWalWrite` guarded `Counters` only through `CounterWalPayloadBytes`, then read `CounterWalEnvelopeAuditPass`. Normal Vault buffers are 32 counters, but a short integration/test buffer could throw instead of returning `false`.
- `TryReadAndVerifyWalPayload` rejected a header-only zero-delta WAL payload while the Burst decode path accepted that exact no-op contract.
- `EntityDeltaRleStreamHeaderDTO` was internal, which is legal in the current root Core asmdef but weak if SaveSystem is later split and layout tests remain outside the domain assembly.

What was done:
- `TryEnqueueEntityDeltaWalWrite` now requires `CounterCapacity` before touching counters.
- `TryReadAndVerifyWalPayload` now accepts a header-only payload only when `CompressedSize == 0`, `UncompressedSize == 0`, and `XXHash3Checksum == 0`.
- `EntityDeltaRleStreamHeaderDTO` is public so manifest and editor tests can legally assert its offsets after a future asmdef split.

Cinematic Cheats used:
- None. This is contract hardening for save/load boundaries.

Exact Microseconds saved:
- No runtime savings claimed. The benefit is removing an exception edge and false corruption reports in cold validation.

Verification:
- Brace balance: `OPEN=265 CLOSE=265`.
- Forbidden-pattern scan on SHINOBU_154 runtime/editor files: clean.
- Burst directive scan: 16 jobs, 16 deterministic directives.

## 2026-05-19 Polish Addendum - Symmetric Entity WAL Read Facade

What was wrong:
- The write path stores entity pages under the entity payload-specific pager sector hash, but no typed read helper exposed the same route.
- Without a helper, a loader could call `TryRequestChunkPageRead` with the raw AUP sector hash and miss the page even though the payload exists.

What was done:
- Added `TryRequestEntityDeltaWalRead(IAsyncPersistenceService, ulong, uint, out H8WorldPageReadTicket)`.
- Added `TryRequestEntityDeltaWalRead(IAsyncPersistenceService, int3, uint, out H8WorldPageReadTicket)`.
- Added `TryCopyCompletedEntityDeltaWalPayload(...)`, which validates `EntityDeltaRle` ticket type and copies into caller-owned `NativeArray<byte>` for `ScheduleWalPayloadDecodePipeline`.

Cinematic Cheats used:
- None. This is persistence route symmetry.

Exact Microseconds saved:
- No measured saving. It prevents failed page-read retries and managed fallback loaders.

Verification:
- Brace balance after helper addition: `OPEN=269 CLOSE=269`.
- Forbidden-pattern scan on SHINOBU_154 runtime/editor files: clean.
- Burst directive scan: 16 jobs, 16 deterministic directives.

## 2026-05-19 Polish Addendum - WAL Stream Async Flag Repair

What was wrong:
- `H8BinaryWorldPager` processed WAL writes on a background worker thread, but the WAL `FileStream` itself was opened without `FileOptions.Asynchronous`.
- The task requires asynchronous WAL semantics. The data file already had the async flag; the WAL append handle did not.

What was done:
- Updated `_walStream` open flags to `FileOptions.Asynchronous | FileOptions.WriteThrough | FileOptions.SequentialScan`.
- Left the queue, lock discipline, byte format, worker thread, and pager owner untouched.

Cinematic Cheats used:
- None. This is persistence I/O contract repair.

Exact Microseconds saved:
- No measurement claimed. The expected benefit is reducing OS-level synchronous handle behavior on the background pager thread, not changing main-thread scheduling cost.

Verification:
- Focused `git diff --check` for `H8BinaryWorldPager.cs` and entity compressor: clean, CRLF warning only.
- Forbidden-pattern scan on SHINOBU_154 runtime/editor files plus `H8BinaryWorldPager.cs`: clean.
- `H8BinaryWorldPager` WAL flag source anchor: `FileOptions.Asynchronous | FileOptions.WriteThrough | FileOptions.SequentialScan`.

## 2026-05-19 Polish Addendum - Dedicated Vault WAL Payload Buffer

What was wrong:
- `RleBytes` was being reused as the serialized WAL payload after pack.
- On load, copying completed WAL bytes into `RleBytes` and then decoding into `RleBytes` would alias source and destination. Raw copy and LZ4 decode are not guaranteed safe under overlap.

What was done:
- Added `BufferID.SaveEntityDeltaWalPayloadBytes = 70357`.
- Added `EntityDeltaCompressionVaultBufferSet.WalPayloadBytes`.
- `TryResolveVaultBuffers` now resolves `WalPayloadBytes` from the Vault using `UninitializedMemory`.
- WAL pack, post-pack audit, enqueue, typed read copy overload, and the default decode overload use `WalPayloadBytes` as the serialized WAL source.
- `RleBytes` is restored to a single job-owned role: pre-LZ4 RLE stream during save and post-WAL RLE decode destination during load.

Cinematic Cheats used:
- None. This is memory-safety separation.

Exact Microseconds saved:
- No saving claimed. This spends one pre-owned byte buffer to remove an aliasing corruption path.

Verification:
- Source anchors for `SaveEntityDeltaWalPayloadBytes` and `WalPayloadBytes` are present.
- Brace balance after buffer split: `OPEN=271 CLOSE=271`.
- Forbidden-pattern scan on SHINOBU_154 runtime/editor files plus `H8BinaryWorldPager.cs`: clean.
- Burst directive scan: 16 jobs, 16 deterministic directives.

<SELF_AUDIT revision="2026-05-19_WAL_PAYLOAD_SPLIT_CURRENT" agent_id="SHINOBU_154">
  <TASK_RECONCILIATION>
    <task id="01" status="PASS">Emergency unmanaged entity schema path exists; missing `entity_save_schema.h8bin` is not faked as shipped content.</task>
    <task id="02" status="PASS">Entity lane uses flat DTO bytes; JSON/object graph routes were not extended.</task>
    <task id="03" status="PASS">Hot DTOs expose public fields only; no `{ get; set; }` DTO properties.</task>
    <task id="04" status="PASS">Primary layouts are explicit: header 32B, RLE stream header 16B, record 80B, counter 64B.</task>
    <task id="05" status="PASS">Deterministic mock entity generator job exists for CI/profiling isolation.</task>
    <task id="06" status="PASS">Block-parallel delta extraction emits changed records into Vault-owned flat buffers.</task>
    <task id="07" status="PASS">Deterministic Burst LZ4 block encoder exists; managed compression rejected.</task>
    <task id="08" status="PASS">Dear Lie dehydration stores AUP/hash/vitals/inventory, not animation/AI transient state.</task>
    <task id="09" status="PASS">WAL bytes enqueue through `IAsyncPersistenceService`; pager WAL handle now includes `FileOptions.Asynchronous`.</task>
    <task id="10" status="PASS">Compression effort consumes `GlobalQualityWeight`, I/O pressure, and disk latency as continuous scalars.</task>
    <task id="11" status="PASS">XXHash3-derived 64-bit checksum is written and verified before decode.</task>
    <task id="12" status="PASS">Sector hash derives from integer AUP sector coordinates; typed read/write helpers share the same pager-key route.</task>
    <task id="13" status="PASS">Tombstone prune job clears expired tombstones before dense packing.</task>
    <task id="14" status="PASS">Jobs use deterministic Burst float mode and simulation frame/tick fields; DTOs are blittable.</task>
    <task id="15" status="PASS">Large staging buffers use Vault `UninitializedMemory` where fully overwritten.</task>
    <task id="16" status="PASS">300-entry telemetry ring and binary dump path exist.</task>
    <task id="17" status="PASS">UI Toolkit tuner writes Vault tuning DTO without recompiling constants.</task>
    <task id="18" status="PASS">CSV profile parser runs over byte spans/Vault scratch; no runtime token strings.</task>
    <task id="19" status="PASS">`EntityDeltaGizmoProbe.OnDrawGizmos` and tuner SceneView overlay render sector heat.</task>
    <task id="20" status="FAIL">Static self-audit, WAL replay validation, and ratio audit are embedded; Unity/Burst import, profiler GC, WAL replay artifact, and 99 percent runtime proof remain blocked by compile/CPU gate.</task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <EntityDeltaHeaderDTO size="32">
      <field name="SectorHash" offset="0" size="8"/>
      <field name="CompressedSize" offset="8" size="4"/>
      <field name="UncompressedSize" offset="12" size="4"/>
      <field name="XXHash3Checksum" offset="16" size="8"/>
      <field name="_pad0" offset="24" size="4"/>
      <field name="_pad1" offset="28" size="4"/>
      <proof>32B total; multiple of 16; 64-bit fields at 0 and 16.</proof>
    </EntityDeltaHeaderDTO>
    <EntityDeltaRleStreamHeaderDTO size="16">
      <field name="Magic" offset="0" size="4"/>
      <field name="Flags" offset="4" size="4"/>
      <field name="DenseBytes" offset="8" size="4"/>
      <field name="StoredBytes" offset="12" size="4"/>
      <proof>16B total; public for layout-manifest/test visibility after future asmdef split.</proof>
    </EntityDeltaRleStreamHeaderDTO>
    <EntityDeltaDataRecordDTO size="80">
      <field name="SectorX" offset="0" size="8"/>
      <field name="SectorY" offset="8" size="8"/>
      <field name="SectorZ" offset="16" size="8"/>
      <field name="LocalX/LocalY/LocalZ" offset="24" size="12"/>
      <field name="EntityKindHash" offset="36" size="4"/>
      <field name="StableEntityHash" offset="40" size="8"/>
      <field name="ArchetypeHash" offset="48" size="4"/>
      <field name="InventoryHash" offset="52" size="4"/>
      <field name="InstanceUid" offset="56" size="4"/>
      <field name="Quantity/Health/Hunger/Integrity" offset="60" size="8"/>
      <field name="Flags" offset="68" size="4"/>
      <field name="BaselineHash32" offset="72" size="4"/>
      <field name="SimulationTick" offset="76" size="4"/>
      <proof>80B total; multiple of 16; `StableEntityHash` offset 40 is 8-byte aligned.</proof>
    </EntityDeltaDataRecordDTO>
    <EntityDeltaBlockCounter64 size="64">
      <proof>Explicit 64B cache-line counter prevents false sharing between block workers.</proof>
    </EntityDeltaBlockCounter64>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Below `GlobalQualityWeight` 0.3, compression effort mathematically collapses LZ4 active hash slots toward 512, match length toward 8, probe step toward 4, and write cadence toward low-Hz. RLE remains the cheap preconditioner; raw dense fallback is self-described by the 16B RLE stream header.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Zero private persistent `NativeArray`, `NativeList`, or `NativeHashMap` fields are declared by the compressor. Vault buffer IDs: 70340 schema, 70341 current records, 70342 baseline records, 70343 delta records, 70344 block counters, 70345 dense bytes, 70346 RLE bytes, 70347 compressed bytes, 70348 LZ4 hash table, 70349 headers, 70350 counters, 70351 telemetry ring, 70352 telemetry cursor, 70353 tuning, 70354 sector stats, 70355 CSV scratch, 70356 profiles, 70357 WAL payload bytes.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    Save chain: optional mock -> tombstone prune -> delta extract -> finalize -> dense pack -> RLE precondition -> LZ4 -> checksum -> WAL pack -> WAL envelope audit -> telemetry -> ratio audit. Load chain: copy WAL payload into `WalPayloadBytes` -> checksum/LZ4 decode into `RleBytes` -> RLE expand into `DenseBytes` -> memcpy records. `[NoAlias]` is present on job fields; WAL payload split prevents source/destination alias during replay.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    SHINOBU_154 added no asmdef reference. Save lane remains in current root `Hecton8.Core.asmdef`; file-level SHINOBU runtime/editor direct sibling namespace scan is clean. Existing root sibling references are recorded as integrator-owned compile-wall debt.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Physical/AI simulation state is not serialized. Fish/base/entity state is dehydrated to persistent facts and owner systems recalculate transient target, velocity, and animation on load. Complexity shifts from object graph traversal with GC risk to O(n flat record scan) plus O(delta bytes) RLE/LZ4.
  </DEAR_LIE_CONFIRMATION>
  <PROOF_BOUNDARY>
    Latest static gates: `git diff --check` clean except CRLF warnings on tracked files; forbidden-pattern scan clean for SHINOBU runtime/editor files plus `H8BinaryWorldPager.cs`; brace balance `OPEN=272 CLOSE=272`; Burst directives `16/16`. Build not launched: generated Unity `.csproj` files are stale and omit new SHINOBU_154 Unity assets, so direct `dotnet build` would be false-negative until Unity import/project regeneration.
  </PROOF_BOUNDARY>
</SELF_AUDIT>

## 2026-05-19 Polish Addendum - Compile Gate Reality

What was wrong:
- CPU briefly dropped below the compile threshold, but the generated C# projects are stale.
- `Hecton8.Core.csproj` contains `H8BinaryWorldPager.cs`, but does not contain new `EntityDeltaCompressionArchitecture.cs`; editor project files also do not contain the new tuner/probe assets.
- A direct `dotnet build` would therefore fail on generated-project staleness, not necessarily on source correctness.

What was done:
- Did not run a known false-negative build.
- Recorded Unity import/project regeneration as the required next proof gate.
- Kept static source gates current.

Cinematic Cheats used:
- None. This is build hygiene.

Exact Microseconds saved:
- Avoided a meaningless compile attempt under a stale project graph. No runtime saving claimed.

Verification:
- `Select-String Hecton8.Core.csproj` found `H8BinaryWorldPager.cs` only; new entity compressor asset is absent from generated project files.
- Latest CPU sample later rose above the 50 percent gate again, so build remained barred.

## 2026-05-19 Polish Addendum - Saturating Finalize Counters

What was wrong:
- `EntityDeltaFinalizeJob` used unchecked unsigned `+=` for block aggregate counters.
- A corrupt or future oversized block counter could wrap dense bytes or entity counts back to a small value, poisoning WAL size decisions.

What was done:
- Added `SaturatingAdd(uint,uint)`.
- Applied it to aggregate delta count, active count, tombstones, dense bytes, pruned tombstones, and dehydrated count.
- Existing exported `int` counters still saturate through `SaturatingUIntToInt`.

Cinematic Cheats used:
- None. This is integer safety.

Exact Microseconds saved:
- No speed claim. Cost is a few scalar comparisons in one finalize job; the gain is eliminating silent wrap.

Verification:
- Source anchor: `SaturatingAdd` is used in `EntityDeltaFinalizeJob`.
- Brace balance after saturating counter guard: `OPEN=272 CLOSE=272`.

## 2026-05-20 Polish Addendum - Canonical Endian WAL Records

What was wrong:
- Dense entity records were still copied into the pre-RLE byte stream as raw native DTO memory.
- The outer WAL header and RLE stream header were explicitly little-endian, but the authority record payload had no endian marker. That could silently corrupt sector coordinates, hashes, float locals, and health/inventory fields if replay bytes came from a legacy or network source with a different byte order.
- `EntityLz4CompressionJob` also assumed the Vault LZ4 hash table was at least 256 slots. The normal SHINOBU_154 Vault request gives 4096 slots, but a short integration/test buffer should fail cleanly instead of indexing past its range.

What was done:
- `EntityDeltaDensePackJob` now writes every `EntityDeltaDataRecordDTO` field through fixed little-endian offsets instead of `UnsafeUtility.MemCpy` from the struct array.
- `EntityRlePreconditionJob` sets `RleStreamFlagLittleEndianRecords` in the 16B stream header.
- RLE stream validation rejects ambiguous payloads with no endian marker, both endian markers, or dense byte counts that are not whole `EntityDeltaDataRecordDTO` rows.
- `EntityRleStreamExpandToRecordsJob` hydrates records through explicit little-endian or big-endian readers and writes records via `ElementAsRef`, preserving the ref-mutation rule.
- Extraction and replay hydration reject non-finite local AUP offsets by setting the fatal path instead of writing corrupt coordinates to WAL or Vault records.
- `EntityLz4CompressionJob` now rejects a zero-slot hash table and clamps active hash slots to the actual buffer length.

Cinematic Cheats used:
- No physics was added. The Dear Lie remains dehydrated entity persistence: only stable AUP sector/local offsets, hashes, vitals, inventory, flags, and tick are saved; transient AI/animation/velocity is rebuilt by owner systems.

Exact Microseconds saved:
- No speed claim. This patch trades a bounded worker-side scalar pack/unpack cost per changed entity for deterministic WAL replay safety. Main-thread cost remains scheduling/enqueue only.

Verification:
- Static gates after the patch: forbidden-pattern scan clean for `Pack=`, JSON serializers, `MemoryStream`, `BinaryFormatter`, `foreach`, `.Complete()`, and runtime `new NativeArray/List/HashMap` across SHINOBU files plus `H8BinaryWorldPager.cs`.
- Direct sibling namespace scan clean for SHINOBU files plus pager.
- Brace balance: `OPEN=288 CLOSE=288`.
- Burst directives: `JOBS=16 BURST_DIRECTIVES=16`.
- `git diff --check` produced only existing CRLF warnings on tracked files.
- Build still not launched: generated Unity `.csproj` files remain stale and do not include `EntityDeltaCompressionArchitecture.cs`, `EntityDeltaGizmoProbe.cs`, or `EntitySaveTunerWindow.cs`; `dotnet build Hecton8.Core.csproj` would still be a known false-negative until Unity regenerates project files.

## 2026-05-20 Polish Addendum - Global Authority Route Card

What was wrong:
- The SHINOBU_154 Vault/WAL route had a rationale route note but no standalone route-card artifact matching `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`.
- That leaves ownership, phase, cadence, stale-handle, shutdown, and proof fields too easy to lose during context compression.

What was done:
- Added `Docs/Tasks/Route_SHINOBU_154_EntityDeltaCompression.md`.
- Route ID: `SAVE_ENTITY_DELTA_WAL_RLE_LZ4`.
- Instrument: `GlobalDataVault / IDataVault` plus black-box telemetry; writes/reads go through existing `IAsyncPersistenceService`.
- Review result: `YELLOW`, not `GREEN`, because Unity import/Burst/profiler/WAL replay proof is absent.

Cinematic Cheats used:
- None. This is architecture review hygiene.

Exact Microseconds saved:
- No runtime saving claimed. The route card prevents integration ambiguity and repeated architecture rediscovery.

Verification:
- Route card path exists and includes owner, instrument, producer phase, consumer phase, cadence, capacity, overflow/failure mode, telemetry fields, shutdown/disposal, stale-handle behavior, rejected alternatives, monolith-risk note, and proof requirements.

## 2026-05-20 Polish Addendum - Native Range Alias Guard

What was wrong:
- `[NoAlias]` was present on Burst jobs, but the public scheduling facade did not explicitly reject overlapping caller-provided `NativeArray` views.
- Dedicated `SaveEntityDeltaWalPayloadBytes` fixed the known replay alias case, but it did not prove the broader save/replay range contract if a stale Vault handle or bad test harness handed out overlapping memory.

What was done:
- Added stack-only native byte-range overlap guards for save and replay scheduling.
- `HasCompressionPipelineAliasViolation` checks 16 Vault-backed ranges before the save pipeline can schedule.
- `HasWalDecodeAliasViolation` checks the WAL/RLE/dense/records/header/counter replay ranges before decode can schedule.
- Added `EntityScheduleFailureJob`, a deterministic Burst job that marks fatal counters/header/stats and returns a tracked `JobHandle` instead of running no-alias jobs over overlapping ranges.
- `TryCopyCompletedEntityDeltaWalPayload` now rejects `Ready` copies shorter than the 32B WAL header or larger than the caller-owned buffer.

Cinematic Cheats used:
- None. This is pointer-contract hardening.

Exact Microseconds saved:
- No runtime saving claimed. Guard cost is bounded to 120 save-side range comparisons and 15 replay-side range comparisons at scheduling time. The gain is preventing undefined vectorized reads/writes under false `[NoAlias]`.

Verification:
- Forbidden-pattern scan remains clean for `Pack=`, JSON serializers, `MemoryStream`, `BinaryFormatter`, `foreach`, `.Complete()`, and runtime `new NativeArray/List/HashMap`.
- Direct sibling namespace scan remains clean for the SHINOBU runtime file.
- Brace balance: `OPEN=308 CLOSE=308`.
- Burst directives: `JOBS=17 BURST_DIRECTIVES=17`.
- `git diff --check` reports no whitespace errors; only the existing ledger CRLF normalization warning is present.
- Guarded build was not launched: CPU sample was 28.5 percent, but an external `dotnet` process (`Id 53260`) is running.
- Runtime proof remains pending: Unity import, Burst Inspector, profiler/GC, WAL replay, and generated project refresh are still required.

## 2026-05-20 Polish Addendum - Scheduling Profiler Anchors

What was wrong:
- The route card still listed profiler marker as pending.
- Static code had telemetry fields, but no named Unity Profiler anchors for measuring main-thread scheduling/enqueue preparation around the save/replay facades.

What was done:
- Added `Unity.Profiling.ProfilerMarker` to `EntityDeltaCompressionArchitecture`.
- `ScheduleCompressionPipeline` is wrapped by `H8.Save.EntityDelta.ScheduleCompression`.
- `ScheduleWalPayloadDecodePipeline` is wrapped by `H8.Save.EntityDelta.ScheduleDecode`.
- Markers stay outside Burst jobs; worker timings still require Unity/Burst profiler artifacts.

Cinematic Cheats used:
- None. This is measurement instrumentation.

Exact Microseconds saved:
- No speed claim. This adds named profiler spans so future profiler captures can measure actual scheduling overhead instead of guessing.

Verification:
- Static marker anchors found in source.
- Forbidden-pattern scan still has no hits.
- Brace/Burst parity after marker patch remains `OPEN=308 CLOSE=308 JOBS=17 BURST_DIRECTIVES=17`.

## 2026-05-20 Polish Addendum - Alias Range Capacity Clamp

What was wrong:
- The native alias guard stack arrays were exactly sized for the current buffer list.
- A future 17th save-side range or 7th replay-side range could overflow the stack range list before the overlap scan.

What was done:
- Added explicit `RangeCapacity` constants to the save and replay alias guards.
- `TryAddNativeRange` now returns failure when a created native range exceeds capacity, causing the caller to schedule the fatal alias path.

Cinematic Cheats used:
- None. This is correctness hardening for the `[NoAlias]` proof.

Exact Microseconds saved:
- No speed claim. Added one integer compare per range candidate during scheduling.

Verification:
- `OPEN=308 CLOSE=308 JOBS=17 BURST_DIRECTIVES=17`.
- Forbidden-pattern scan remains empty for Pack=, JSON, `MemoryStream`, `BinaryFormatter`, `foreach`, `.Complete()`, runtime Native container allocation, `throw new`, and `IEnumerable`.
- Direct sibling namespace scan remains empty.
- `git diff --check` reports no whitespace errors; only existing CRLF normalization warnings on touched tracked files.
- Build was not launched: CPU sample was 100 percent and generated `Hecton8.Core.csproj` still omits `EntityDeltaCompressionArchitecture.cs`.

## 2026-05-20 Compile Gate Attempt - Unity Dependency Wall

What was wrong:
- Static checks were insufficient for Task 20 runtime proof.
- Generated `.csproj` files still omitted new SHINOBU assets, making direct `dotnet build` invalid.

What was done:
- Ran Unity `6000.4.1f1` batchmode import/compile.
- Log written to `Docs/AgentLogs/Unity_SHINOBU_154_Compile.log`.
- Unity script asset scan includes:
  - `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs`
  - `Assets/_Project/Scripts/SaveSystem/EntityDeltaGizmoProbe.cs`
  - `Assets/_Project/Scripts/SaveSystem/Editor/EntitySaveTunerWindow.cs`
  - `Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs`

Cinematic Cheats used:
- None. This is compile-wall verification.

Exact Microseconds saved:
- No runtime saving claimed. Unity script compilation reported `40.599537s`; the run failed due unrelated compile-wall errors.

Dependency blockers:
- `Assets/_Project/Scripts/Physics/HabitatFluidIncursionJobs.cs`: syntax/member errors beginning at line 196.
- `Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs`: missing `NativeMemorySentinel` and `NativeAllocationLifetime`.
- `Assets/_Project/Scripts/World/ProceduralWreckage/*`: invalid `float4x4.Rotate` and missing `math.reversebytes`.
- `Assets/_Project/Scripts/World/ProceduralCoral/*`: readonly `in` buffer mutation, ambiguous `math.min`, missing `math.reversebytes`.
- `Hecton8.MockDomain.Runtime`: Burst ILPP internal `NullReferenceException`.

Verification:
- No SHINOBU_154 file appears in the compiler-error list.
- Task 20 remains `[BLOCKED BY DEPENDENCY]`; runtime/Burst/profiler proof must be rerun after the owning domains repair the compile wall.

## 2026-05-20 Polish Addendum - Native Safety Comment Hardening

What was wrong:
- The jobs using `NativeDisableParallelForRestriction` were partition-safe, but the source did not carry the three-part native-memory proof required at suppressed safety-check sites.

What was done:
- Added `SAFETY_JUSTIFICATION_PARAGRAPH_1..3` comments to `GenerateMockEntityStateJob`, `EntityTombstonePruneJob`, and `ExtractEntityDeltaJob`.
- The comments document the false-positive safety check, rejected alternatives, and exact write-ownership invariant for index-owned mock rows, block-owned tombstone rows/counters, and block-owned delta output ranges.

Cinematic Cheats used:
- None. This hardens evidence for the existing dehydrated entity delta route.

Exact Microseconds saved:
- No runtime microseconds claimed. The source change is comments only and does not alter the Burst job graph.

Verification:
- `OPEN=308 CLOSE=308 JOBS=17 BURST_DIRECTIVES=17`.
- Forbidden-pattern scan remains empty for `Pack=`, JSON serializers, `MemoryStream`, `BinaryFormatter`, `foreach`, `.Complete()`, runtime native allocations, `throw new`, and `IEnumerable`.
- Direct sibling namespace using scan remains empty.
- `git diff --check` is clean for `EntityDeltaCompressionArchitecture.cs`.
- Build was not launched: CPU sample was 59 percent.
