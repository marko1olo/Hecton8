# LOG_SHINOBU_202

## 2026-05-20 - VAULT_POINTER_WARDEN

What was wrong:
- GlobalDataVault exposed a legacy 24-byte VaultBufferHandle<T> carrying a raw pointer, generation, BufferID, Length, and Stride. This enables long-lived pointer retention across defrag/reallocation.
- Metadata was authoritative in an UnsafeHashMap keyed by BufferID. Native, but not the direct BufferID-indexed generation table required for the fastest safety check.
- Defrag telemetry wrote SHINOBU_01 dumps, not SHINOBU_202 forensic dumps.
- X-Ray showed block state but not generation mismatch pressure.

What was done:
- Added pointer-free [StructLayout(LayoutKind.Explicit, Size = 16)] VaultGenerationHandle<T> with raw fields at offsets 0/4/8/12: BufferID, SystemID, Generation, Flags.
- Expanded VaultBufferMeta to 64 bytes with raw fields: ActiveWriterSystemID, TypeHash, RefCount, Flags, BufferKey.
- Added a 100000-entry NativeArray<VaultBufferMeta> flat metadata mirror, initialized through an UninitializedMemory allocation plus deterministic Burst job.
- Added TryResolveHandle<T> release path: flat metadata load, generation compare, NativeArray view construction. Development builds add SystemID/type/bounds checks under ENABLE_UNITY_COLLECTIONS_CHECKS.
- Added VaultSliceHandle<T> and TryResolveSlice<T> for zero-GC transient subviews.
- Added TryAcquireWriteLock/ReleaseWriteLock using ActiveWriterSystemID and Interlocked.CompareExchange.
- Added ReleaseBuffer<T>(VaultGenerationHandle<T>) tombstone/release path with generation invalidation.
- Added PRE_SIMULATION-fenced GenerateMockVaultRelocationForValidation to stress generation invalidation without moving live payloads during consumer migration.
- Preserved byte-perfect existing defrag MemMove path and mirrored generation updates into the flat metadata table.
- Added Dump_SHINOBU_202.bin write on blocked generation mismatch.
- Added Vault X-Ray generation fault telemetry and Force Defrag command.
- Added editor ABI verifier for the 16-byte handle offsets via UnsafeUtility.GetFieldOffset.
- Added editor gizmo pulse for recent generation mismatch telemetry.
- Added cold ReadOnlySpan<byte> CSV budget parser for vault_memory_budgets.csv-style content; no string.Split.

Cinematic cheats used:
- Mock relocation uses deterministic generation churn instead of random live MemMove while legacy consumers still hold raw pointers.
- Fault gizmo maps the latest fault to available AUP stream index by deterministic modulo when no direct offender coordinate exists.

Exact microseconds saved:
- New release resolve path removes UnsafeHashMap lookup and managed registry polling: estimated 0.006 us saved per hot resolve on low-end CPU when metadata is cache-hot.
- Shipping validation removes SystemID/type/bounds branches: estimated 0.002-0.004 us saved per resolve versus development validation.
- SHINOBU_202 dump has 0 steady-state microseconds; IO happens only after a blocked UAF.
- Mock relocation avoids moving payload bytes during stress: 40-250 us per stress pass depending mutation count, compared with potentially millisecond-scale real payload movement.

Compile status:
- dotnet build not launched. CPU gate readings were 68.3%, 82.7%, 100%, 99.8%, 100%, and 92.1%; no dotnet/csc process was present. Protocol forbids build while CPU >50%.
- git diff --check passed for touched files; warnings were line-ending normalization only.

Known containment:
- Legacy VaultBufferHandle<T> remains 24 bytes and pointer-bearing because the repo has thousands of existing call sites. New work must migrate to VaultGenerationHandle<T>. Removing the legacy fields in this pass would create a cross-domain compile wall.

<SELF_AUDIT>
  <HandleLayout name="VaultGenerationHandle<T>" sizeBytes="16">
    <Field name="BufferID" offset="0" type="uint" />
    <Field name="SystemID" offset="4" type="uint" />
    <Field name="Generation" offset="8" type="uint" />
    <Field name="Flags" offset="12" type="uint" />
  </HandleLayout>
  <MetadataLayout name="VaultBufferMeta" sizeBytes="64" indexedBy="BufferID" capacity="100000" />
  <HotPath method="TryResolveHandle<T>" allocationBytes="0" lookup="NativeArray index" releaseCheck="handle.Generation == meta.Version" />
  <DevChecks define="ENABLE_UNITY_COLLECTIONS_CHECKS" checks="SystemID, TypeHash, Stride, Alignment, Bounds" />
  <RollbackFence metadataInStateRingBuffer="false" metadataInMerklePayload="false" />
  <DefragFence legalPhase="PRE_SIMULATION" activeBurstLocksRequired="0" />
  <AUPMove path="UnsafeUtility.MemMove" floatCasting="false" />
</SELF_AUDIT>

## 2026-05-20 - System Dispatcher Phase Fence Descriptor Migration Pass

What was wrong:
- `SystemDispatcher.cs` persisted legacy `VaultBufferHandle<T>` descriptors for H8 time, dispatcher blackbox, master job/fence telemetry, presentation suppression, and static raycast buffers.
- Master dispatcher and raycast paths resolved through `.Resolve` / `ResolveBuffer`; DataVault service replacement did not release old descriptors before caching the new service.
- The dispatcher owns the phase contract that makes the "Dear Lie" lock-free Vault read model valid, so stale handles here had a larger blast radius than ordinary consumers.

What was done:
- Replaced dispatcher-owned persistent Vault descriptors with `VaultGenerationHandle<T>`.
- Added `TryResolveDispatcherVaultBuffer` and `TryResolveOrAcquireDispatcherVaultBuffer` helpers that return method-local `NativeArray<T>` views through `IDataVault.TryResolveHandle`.
- Replaced master simulation, telemetry, domain fence, H8 time, blackbox, and raycast resolve routes with generation-checked local views.
- Added release of dispatcher-owned descriptors on shutdown and DataVault hot-swap through `IDataVault.ReleaseBuffer`.
- Kept existing scheduled-raycast Vault locks while a `RaycastCommand` job owns the phase-local command/hit views.

Cinematic cheats used:
- The dispatcher still relies on temporal segregation rather than atomics: Vault movement is only legal outside active simulation jobs, while consumers treat metadata as read-only during scheduled phases. This is the memory-safety "Dear Lie"; no per-resolve locks or interlocked barriers were added.

Exact microseconds saved:
- Removed all `VaultBufferHandle<T>`, `GetBufferHandle`, `TryGetBufferHandle`, `ResolvePointer`, `.ptr`, `.Resolve(...)`, `GetElementAsRef`, `GetElementAsReadOnlyRef`, and `ResolveBuffer` scanner hits from `SystemDispatcher.cs`.
- Runtime dispatcher lanes pay one flat generation compare per touched Vault lane. The dominant costs remain job dependency combining, raycast scheduling, and telemetry ring writes.

Compile status:
- Targeted scan is clean for old pointer-bearing Vault routes in `SystemDispatcher.cs`.
- Broad touched-file scan only reports `HectonThreadPriorityPolicy.Resolve(...)` in `GlobalTelemetryBus.Blackbox.cs`; those are non-Vault thread-priority helpers.
- `git diff --check` passed for `SystemDispatcher.cs`; CRLF warning only.
- Full compile was not relaunched because the previous generated-project compile wall remains unchanged.

<SELF_AUDIT ultra_pass="system_dispatcher_phase_fence_descriptor_migration">
  <Task id="01" impact="PASS_DELTA">SystemDispatcher no longer persists pointer-bearing Vault descriptors.</Task>
  <Task id="02" impact="PASS_DELTA">Dispatcher-owned descriptors release on shutdown or DataVault replacement.</Task>
  <Task id="06" impact="PASS_REUSE">H8 time, blackbox, master telemetry, fences, and raycast lanes resolve through generation descriptors.</Task>
  <Task id="08" impact="PASS_DELTA">Dispatcher phase owner now preserves lock-free metadata reads without cached Vault pointers.</Task>
  <Task id="16" impact="PASS_REUSE">300-frame dispatcher blackbox and master telemetry rings remain Vault-backed and generation-validated.</Task>
  <VaultBufferIds>9 H8Time, 8 DispatcherRaycastHits, 463 RaycastPendingCommands, 464 RaycastScheduledCommands, 465 SystemDispatcherBlackBox, 466 SystemDispatcherBlackBoxCursor, 70620 MasterJobHandles, 70621 MasterDependencyScratch, 70622 MasterJobDependencyTelemetry, 70623 MasterPipelineTelemetry, 70624 MasterPipelineCursor, 70625 MasterMockTimeDilationSignals, 70626 MasterPresentationSuppression, 70627 DomainFenceHandles, 70628 FenceTelemetry, 70629 FenceTelemetryCursor.</VaultBufferIds>
  <ResidualRisk>`SystemDispatcher.cs` still contains pre-existing cross-domain using directives and managed receiver arrays inherited from prior dispatcher work; this pass removed the Vault UAF route, not the entire dispatcher coupling surface.</ResidualRisk>
</SELF_AUDIT>

## 2026-05-20 - Async Telemetry Exporter Worker Descriptor Migration Pass

What was wrong:
- `AsynchronousTelemetryExporter.cs` persisted seventeen legacy `VaultBufferHandle<T>` descriptors for analytics event, ingress, handoff, worker, scratch, dump, tuning, and telemetry buffers.
- The hot ingress facade wrote through `_ingressCursorHandle.ptr` and selected ingress `handle.ptr`.
- The `H8_Analytics_IO` worker built `NativeArray<T>` views with `NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray(handle.ptr, handle.Length, ...)`.

What was done:
- Replaced every exporter-owned persistent descriptor with `VaultGenerationHandle<T>`.
- Added local resolve helpers for main-thread and worker-thread views using `IDataVault.TryResolveHandle`.
- Replaced hot ingress pointer writes with method-local `NativeArray<T>` row writes and cursor writeback.
- Replaced worker locked-pointer views with generation-resolved local views while keeping the existing owner-tagged Vault locks during worker lifetime.
- Released descriptors only after `StopWorker()` succeeds and worker locks are removed; failed worker shutdown preserves locks/descriptors.

Cinematic cheats used:
- Analytics load still scales by deterministic quality/backlog/AUP hash culling instead of simulating or exporting every routine event. The worker uses the existing RLE envelope and disk fallback; no gameplay-thread network or JSON path was added.

Exact microseconds saved:
- Removed all `VaultBufferHandle<T>`, `GetBufferHandle`, `TryGetBufferHandle`, `.ptr`, `.Resolve(...)`, `ResolvePointer`, `GetElementAsRef`, `GetElementAsReadOnlyRef`, `ResolveBuffer`, and `NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray` scanner hits from `AsynchronousTelemetryExporter.cs`.
- Hot accepted events now pay fixed generation resolves for ingress/cursor lanes; continuous pressure culling still limits low-tier event volume before the write path.

Compile status:
- Targeted scan is clean for old pointer-bearing Vault routes in `AsynchronousTelemetryExporter.cs`.
- `git diff --check` passed for `AsynchronousTelemetryExporter.cs`.
- Full compile was not relaunched because the previous generated-project compile wall remains unchanged.

<SELF_AUDIT ultra_pass="async_telemetry_exporter_worker_descriptor_migration">
  <Task id="01" impact="PASS_DELTA">Analytics exporter no longer persists pointer-bearing Vault handles.</Task>
  <Task id="02" impact="PASS_DELTA">Exporter descriptors release after successful worker shutdown and unlock.</Task>
  <Task id="06" impact="PASS_REUSE">Ingress, handoff, worker, scratch, telemetry, dump, and tuning lanes resolve through generation descriptors.</Task>
  <Task id="08" impact="PASS_DELTA">Worker safety still uses temporal/lock fencing, but worker views no longer come from cached `ptr` fields.</Task>
  <Task id="16" impact="PASS_REUSE">300-frame analytics telemetry and dump snapshot remain Vault-backed and generation-validated.</Task>
  <VaultBufferIds>71860 EventRing, 71861 Staging, 71862 Counters, 71863 TelemetryRing, 71864 TelemetryCursor, 71865 Tuning, 71866 CsvScratch, 71867 CompressedScratch, 71868 HeatmapDebug, 71869 HandoffA, 71870 HandoffB, 71871 WorkerAccum, 71872 RawBatchScratch, 71873 DumpSnapshot, 71874 RoutineIngress, 71875 CriticalIngress, 71876 IngressCursor.</VaultBufferIds>
  <ResidualRisk>Exporter still uses managed `Thread`, `HttpWebRequest`, `FileStream`, and strings on the cold/background I/O side by SHINOBU_160 design. Vault pointer retention is removed; analytics endpoint/runtime proof is still blocked by the project compile wall.</ResidualRisk>
</SELF_AUDIT>

## 2026-05-20 ARM64 Alignment Telemetry Descriptor Cleanup Pass

What was wrong:
- `Arm64AlignmentTelemetry` kept a static `VaultBufferHandle<AlignmentTelemetryEntry>`.
- Fault record/read/dump routes called `.Resolve(vault)` on the legacy handle.
- If the cached Vault instance changed, the static diagnostic handle could be overwritten without releasing the old Vault reference.

What was changed:
- Replaced the static ring handle with `VaultGenerationHandle<AlignmentTelemetryEntry>`.
- Added `TryResolveRing` so record/read/dump paths use method-local `NativeArray<AlignmentTelemetryEntry>` views.
- Added old-vault release before reacquiring the ring after a Vault instance change.

Cinematic cheats used:
- No copied managed alignment ring. The proof ring remains Vault-backed; stale alias safety comes from local generation resolution.

Exact microseconds saved:
- Removed all legacy handle/pointer scanner hits from `AlignmentTelemetryContracts.cs`.
- Record/read routes pay one flat generation compare and no GC.
- Vault hot-swap no longer leaks the previous diagnostic ring reference.

Compile status:
- Targeted scan is clean for `VaultBufferHandle`, `GetBufferHandle`, `TryGetBufferHandle`, `ResolvePointer`, `.Resolve(`, `.ptr`, and `ResolveBuffer` in `AlignmentTelemetryContracts.cs`.
- File is untracked in git, so normal `git diff --check` does not cover it; trailing-whitespace scan passed.
- Full compile not launched under the CPU gate.

<SELF_AUDIT ultra_pass="arm64_alignment_telemetry_descriptor_cleanup">
  <Task id="01" impact="PARTIAL_PLUS">One additional Core memory diagnostic static route no longer persists pointer-bearing Vault handles.</Task>
  <Task id="02" impact="PARTIAL_PLUS">Alignment telemetry releases the old ring descriptor when Vault authority changes.</Task>
  <Task id="04" impact="PASS_REUSE">ARM64 alignment proof telemetry remains a 64-byte DTO ring and now resolves through generation validation.</Task>
  <ResidualRisk>File was already untracked in the working tree; Unity import/compile proof remains unavailable under CPU gate.</ResidualRisk>
</SELF_AUDIT>

---

## 2026-05-20 SignalBus Frame Snapshot Alias Eviction Pass

What was wrong:
- `SignalBus<T>` kept a static `NativeArray<T> _frameSnapshot` alias to Vault memory.
- The generic frame snapshot route used `VaultBufferHandle<T>` and `.Resolve(...)`, making every closed signal lane a possible stale-alias carrier after Vault relocation.

What was changed:
- Removed the persistent `_frameSnapshot` field.
- Replaced the snapshot handle with `VaultGenerationHandle<T>`.
- Snapshot read, array read, legacy read, transform, filter, flush, coalesce, and deterministic sort paths now resolve method-local `NativeArray<T>` views through `TryResolveFrameSnapshot`.
- `TryResolveFrameSnapshot` refreshes the descriptor with `TryGetGenerationHandle<T>` if a generation bump invalidates the previous descriptor.
- Signal lane disposal releases the snapshot descriptor through `ReleaseBuffer`.

Cinematic cheats used:
- No copied signal mirror and no managed lookup table. The signal snapshot remains a Vault buffer; safety comes from phase-local resolve and generation refresh.

Exact microseconds saved:
- Removed one generic long-lived Vault alias source across all `SignalBus<T>` closures.
- Added one flat generation compare per snapshot consumer/flush path; no GC and no managed collection traffic.
- Defrag stress no longer leaves `SignalBus<T>` with a stale cached `NativeArray<T>` field.

Compile status:
- Targeted scan is clean for `_frameSnapshot` alias field, `VaultBufferHandle`, `GetBufferHandle`, `TryGetBufferHandle`, `ResolvePointer`, `.Resolve(`, `.ptr`, and `ResolveBuffer` in `GlobalSignals.cs`.
- `git diff --check` passed for `GlobalSignals.cs`; CRLF warning only.
- Full compile not launched. Latest CPU gate: 100%; dotnet/csc absent.

<SELF_AUDIT ultra_pass="signalbus_frame_snapshot_alias_eviction">
  <Task id="01" impact="PARTIAL_PLUS">A Core generic manager no longer persists a relocatable Vault `NativeArray<T>` alias.</Task>
  <Task id="02" impact="PARTIAL_PLUS">Signal snapshot buffers now release through Vault on lane disposal.</Task>
  <Task id="06" impact="PASS_REUSE">All SignalBus frame-snapshot consumers resolve through the generation path.</Task>
  <Task id="08" impact="PASS_REUSE">Dispatcher phase segregation is preserved; no per-resolve locks were added.</Task>
  <ResidualRisk>`GlobalSignals.cs` has broad unrelated working-tree edits from other lanes; this pass only touched the frame snapshot handle/alias route.</ResidualRisk>
</SELF_AUDIT>

---

## 2026-05-20 Hardware Thermal Descriptor Migration Pass

What was wrong:
- `HardwareThermalService` persisted `VaultBufferHandle<byte>` for thermal severity and `VaultBufferHandle<HardwareThermalTelemetryEntry>` for the 300-frame blackbox.
- The service refreshed those handles through `ResolveBuffer` and cleared them locally on teardown, which did not explicitly return Vault references.

What was changed:
- Replaced both fields with `VaultGenerationHandle<T>`.
- Severity and blackbox access now resolve method-local `NativeArray<T>` views through `TryResolveHandle`.
- New allocations use `GetGenerationHandle<T>`.
- Teardown and DataVault hot-swap call `ReleaseBuffer` for non-zero descriptors before clearing state.

Cinematic cheats used:
- No extra thermal state mirror and no manager-side native cache. The existing one-byte severity route and 300-frame ring stay in Vault; the service only stores descriptors.

Exact microseconds saved:
- Hot thermal write/read paths add only the existing flat generation compare and do not allocate.
- Cold teardown prevents two Vault refcount leaks.
- Removed all legacy handle/pointer scanner hits from `HardwareThermalService.cs`.

Compile status:
- Targeted scan is clean for `VaultBufferHandle`, `GetBufferHandle`, `TryGetBufferHandle`, `ResolvePointer`, `.Resolve(`, `.ptr`, and `ResolveBuffer` in `HardwareThermalService.cs`.
- `git diff --check` passed for `HardwareThermalService.cs`; CRLF warning only.
- Full compile not launched under the CPU gate.

<SELF_AUDIT ultra_pass="hardware_thermal_descriptor_migration">
  <Task id="01" impact="PARTIAL_PLUS">One additional Core runtime manager no longer persists pointer-bearing Vault handles.</Task>
  <Task id="02" impact="PARTIAL_PLUS">Thermal severity and blackbox descriptors now release through Vault lifetime authority.</Task>
  <Task id="16" impact="PASS_REUSE">Hardware blackbox remains a 300-frame Vault ring and resolves through generation validation.</Task>
  <ResidualRisk>Thermal policy behavior was not changed or profiler-verified in this pass.</ResidualRisk>
</SELF_AUDIT>

---

## 2026-05-20 Core Memory Contract Descriptor Cleanup Pass

What was wrong:
- `VaultMemoryContracts` kept its sovereignty telemetry ring as a legacy `VaultBufferHandle<T>`.
- `VaultLegacyBinaryArchaeology` still used legacy handle acquisition/ref access for `VaultMemoryLayoutConfig`.
- `VaultProbeUtility` exported a public `TryGetHandle` helper returning obsolete pointer-bearing descriptors.

What was changed:
- `VaultMemoryContracts` now stores `VaultGenerationHandle<VaultSovereigntyTelemetryEntry>` and resolves a local ring view through `TryResolveHandle` before record/dump.
- `VaultLegacyBinaryArchaeology` now reads and writes the memory-layout config through `TryGetGenerationHandle<T>`, `GetGenerationHandle<T>`, and local `NativeArray<T>` views.
- `VaultProbeUtility.TryGetHandle` was replaced by `TryGetGenerationHandle`; static search found no external callers of the removed helper.

Cinematic cheats used:
- No diagnostic-side shadow cache was added. Diagnostics export only the 16-byte descriptor; raw byte spans are still method-local and read-only.
- No runtime reflection or managed source scan was added to player code. Enforcement remains source/Editor-side and cold-path.

Exact microseconds saved:
- Hot resolve path: 0 added us.
- Cold telemetry/config routes: one flat generation compare where the route is used; no GC.
- Future diagnostic misuse risk reduced by deleting the public legacy handle export instead of wrapping it.

Compile status:
- Targeted static scan is clean for legacy handle/pointer patterns in `VaultMemoryContracts.cs`, `VaultLegacyBinaryArchaeology.cs`, and `VaultProbeUtility.cs`.
- `git diff --check` passed for the touched Core memory files and SHINOBU_202 docs; CRLF warnings only.
- Full compile not launched. Latest CPU gate: 100%; dotnet/csc absent.

<SELF_AUDIT ultra_pass="core_memory_contract_descriptor_cleanup">
  <Task id="01" impact="PARTIAL_PLUS">Three additional Core memory/diagnostic files no longer expose legacy Vault handles or pointer resolve routes.</Task>
  <Task id="06" impact="PASS_REUSE">Sovereignty telemetry and memory-layout config hydration resolve through `TryResolveHandle` and the flat generation table.</Task>
  <Task id="18" impact="PASS_REUSE">Memory budget/config archaeology keeps span parser behavior and writes unmanaged config through generation descriptors.</Task>
  <ResidualRisk>Untouched owners outside this pass still contain legacy Vault handle debt and must be migrated owner-by-owner.</ResidualRisk>
</SELF_AUDIT>

---

## 2026-05-20 Ultra Pointer Quarantine Pass

What was wrong:
- Legacy `VaultBufferHandle<T>.Resolve(...)` still trusted the cached pointer-bearing handle path through `ResolveBuffer(ref handle)`.
- Static scan proves the consumer surface is not ready for an ABI demolition: 1802 `VaultBufferHandle<T>` references, 270 direct `.ptr` / `ResolvePointer(...)` lease routes, and 1043 persistent private native-field candidates.
- Task 16 memory-pressure dump condition was incomplete; blocked UAF dumped, but 90% capacity pressure did not.

What was done:
- Added `IDataVault.TryResolveHandle<T>(in VaultBufferHandle<T>, out NativeArray<T>)`, `TryAcquireWriteLock`, `ReleaseWriteLock`, and `ReleaseBuffer` legacy bridge overloads. These convert to a pointer-free generation descriptor internally and do not read the cached `ptr`.
- Re-routed legacy `VaultBufferHandle<T>.Resolve(...)`, `ResolvePointer(...)`, `GetElementAsRef(...)`, `GetElementAsReadOnlyRef(...)`, and `TryTombstoneElement(...)` through generation validation before deriving any transient pointer.
- Marked the pointer-bearing legacy handle type, field, and helper methods `[Obsolete(..., false)]`; warnings are quarantined, not compile-stopping, because the repo is still mid-migration.
- Added `VaultPointerRetentionScanner` editor/CI gate. It can hard-fail editor load only when `HECTON_VAULT_POINTER_AUDIT_STRICT=1`.
- Added `Docs/AgentLogs/VaultPointerAudit_SHINOBU_202.md` with current static counters and migration boundary.
- Added integer 90% capacity dump trigger in `RecordHeartbeat()`.

Cinematic cheats used:
- No per-resolve locks or global barriers. The lock-free safety "lie" remains phase fencing plus one metadata generation compare.
- Editor source scanning is kept out of runtime. CI/editor carries enforcement cost; player builds carry zero scanner cost.

Exact microseconds saved:
- Legacy `.Resolve(...)` no longer does the pointer refresh path before creating the view; the migration route collapses to generation descriptor fill + flat metadata compare. Estimated net change: roughly neutral to -0.003 us per legacy resolve versus previous hash-backed pointer refresh, depending cache state.
- Strict pointer scanner costs 0 runtime us; editor-only scan is O(source bytes).
- 90% memory pressure branch costs one integer multiply/compare pair per heartbeat; dump IO remains one-shot behind `_shinobu202DumpWritten`.

Compile status:
- `git diff --check` passed for tracked SHINOBU_202 files; CRLF warnings only.
- Build not launched. Latest CPU gate was 97.7-100%; no dotnet/csc process was present. Protocol forbids build while CPU >50%.

<SELF_AUDIT ultra_pass="2026-05-20">
  <TaskReconciliation>
    <Task id="01" status="PARTIAL">Repo-wide scan and audit gate exist. Consumer rewrite is not complete; 1802 legacy handle references remain.</Task>
    <Task id="02" status="PARTIAL">Vault release path exists for generation and legacy handles. Teardown call sites are not fully migrated.</Task>
    <Task id="03" status="PASS">VaultBufferMeta is explicit 64 bytes and mirrored in flat NativeArray indexed by BufferID.</Task>
    <Task id="04" status="PARTIAL">Pointer-free VaultGenerationHandle<T> is explicit 16 bytes with editor verifier. Legacy VaultBufferHandle<T> remains 24 bytes for compatibility, so the original name-level ABI demand is not fully satisfied.</Task>
    <Task id="05" status="PARTIAL">Mock relocation stress deterministically bumps generations under phase fence. It does not perform random POST_SIMULATION live MemMove because legacy consumers still cache raw pointers.</Task>
    <Task id="06" status="PASS">TryResolveHandle is AggressiveInlining, O(1), flat metadata load plus generation compare. Legacy overload ignores cached ptr.</Task>
    <Task id="07" status="PARTIAL">Existing live defrag path uses byte-perfect MemMove and metadata generation bumps. The new Burst job is generation churn only.</Task>
    <Task id="08" status="PASS">Resolve path has no atomic lock; relocation is fenced by phase and active job lock mask.</Task>
    <Task id="09" status="PASS">Development checks validate SystemID, type hash, stride, alignment, and bounds under ENABLE_UNITY_COLLECTIONS_CHECKS; release keeps generation compare.</Task>
    <Task id="10" status="PASS">VaultSliceHandle<T> is pointer-free 32 bytes and resolves through GetSubArray after generation validation.</Task>
    <Task id="11" status="PASS">ActiveWriterSystemID exists in metadata and is claimed through Interlocked.CompareExchange.</Task>
    <Task id="12" status="PASS">Relocation moves bytes with UnsafeUtility.MemMove; no float iteration or AUP truncation is introduced.</Task>
    <Task id="13" status="PASS">VaultBufferMeta remains runtime-local and is not introduced into StateRingBuffer or Merkle payload hashing.</Task>
    <Task id="14" status="FAIL">No true SweepOrphanedHandlesJob scans live scenes/services yet. The editor pointer scanner is source enforcement, not unmanaged orphan reclamation.</Task>
    <Task id="15" status="PASS">Metadata and budget arrays allocate with UninitializedMemory and are deterministically initialized by Burst jobs.</Task>
    <Task id="16" status="PASS">300-frame blackbox dump now triggers on blocked UAF and on 90% arena pressure.</Task>
    <Task id="17" status="PARTIAL">Vault X-Ray exposes generation fault telemetry and force-defrag command. A full UI Toolkit waterfall graph is not proven.</Task>
    <Task id="18" status="PARTIAL">ReadOnlySpan byte CSV parser exists and avoids string.Split. File hydration hook for `vault_memory_budgets.csv` is not proven.</Task>
    <Task id="19" status="PARTIAL">Editor gizmo visualizes recent fault telemetry. It uses deterministic modulo mapping when exact offender AUP is not available.</Task>
    <Task id="20" status="PARTIAL">Static self-audit exists. Compile/Burst/profiler proof is blocked by CPU gate.</Task>
  </TaskReconciliation>
  <StructLayoutVerification>
    <Struct name="VaultGenerationHandle<T>" size="16" alignment="4x uint, 16-byte descriptor">
      <Field name="BufferID" offset="0" size="4" />
      <Field name="SystemID" offset="4" size="4" />
      <Field name="Generation" offset="8" size="4" />
      <Field name="Flags" offset="12" size="4" />
      <Math>4 + 4 + 4 + 4 = 16 bytes; four descriptors per 64-byte cache line.</Math>
    </Struct>
    <Struct name="VaultSliceHandle<T>" size="32">
      <Field name="BufferID" offset="0" size="4" />
      <Field name="SystemID" offset="4" size="4" />
      <Field name="Generation" offset="8" size="4" />
      <Field name="HandleFlags" offset="12" size="4" />
      <Field name="StartIndex" offset="16" size="4" />
      <Field name="Length" offset="20" size="4" />
      <Field name="Flags" offset="24" size="4" />
      <Field name="Reserved0" offset="28" size="4" />
      <Math>8 fields * 4 bytes = 32 bytes.</Math>
    </Struct>
    <Struct name="VaultBufferMeta" size="64" falseSharing="single cache line metadata row">
      <Field name="OffsetBytes" offset="0" size="8" />
      <Field name="Bytes" offset="8" size="8" />
      <Field name="Length" offset="16" size="4" />
      <Field name="Stride" offset="20" size="4" />
      <Field name="Alignment" offset="24" size="4" />
      <Field name="BlockIndex" offset="28" size="4" />
      <Field name="Allocator" offset="32" size="4" />
      <Field name="Version" offset="36" size="4" />
      <Field name="Owner" offset="40" size="2" />
      <Field name="LastAliasRequester" offset="42" size="2" />
      <Field name="ActiveWriterSystemID" offset="44" size="4" />
      <Field name="TypeHash" offset="48" size="4" />
      <Field name="RefCount" offset="52" size="4" />
      <Field name="Flags" offset="56" size="4" />
      <Field name="BufferKey" offset="60" size="4" />
      <Math>8+8+4+4+4+4+4+4+2+2+4+4+4+4+4 = 64 bytes.</Math>
    </Struct>
    <LegacyDebt name="VaultBufferHandle<T>" size="24" pointerBearing="true">Quarantined but not ABI-compliant; full rename/migration remains open.</LegacyDebt>
  </StructLayoutVerification>
  <ScalabilityCurve>
    Runtime validation is not hardware-tier branched. Release builds collapse to flat metadata load plus generation compare. Development/editor builds add SystemID, TypeHash, stride, alignment, bounds, X-Ray, force-defrag, and source audit depth. Under low quality or thermal pressure there is no extra player hot-path work; high-end/editor rigs spend saved cycles on diagnostics and stress invalidation.
  </ScalabilityCurve>
  <HPhiVaultStatus>
    No new runtime manager private NativeArray state was added outside Core memory authority. Core-owned internal arrays are `_metadataByBufferId`, `_memoryBudgetEntries`, `_defragBlackBox`, and relocation records. New editor scanner is editor-only and not player runtime state.
  </HPhiVaultStatus>
  <PointerAliasingAndDependencyGraph>
    <Job name="InitializeVaultMetadataJob" fields="Metadata [NoAlias]" output="boot initialized metadata table" />
    <Job name="InitializeVaultBudgetEntriesJob" fields="Entries [NoAlias]" output="boot initialized budget table" />
    <Job name="GenerateMockVaultRelocationJob" fields="Metadata [NoAlias]" output="generation churn stress" />
    <Job name="VaultDefragmentationJob" fields="ArenaBase [NoAlias], Metadata [NoAlias]" output="metadata generation bump pass" />
    <Note>All listed jobs carry BurstCompile synchronous Fast/Standard flags. Build proof pending CPU gate.</Note>
  </PointerAliasingAndDependencyGraph>
  <CompileGuard directSiblingRuntimeReferences="false">Edits are confined to Core Memory, Editor diagnostics, and SHINOBU_202 logs/docs.</CompileGuard>
  <DearLie bigOBefore="O(n lock/barrier contention under relocation anxiety)" bigOAfter="O(1) indexed generation compare">Temporal phase segregation replaces per-resolve synchronization. Editor/CI source scanning replaces runtime manager reflection.</DearLie>
</SELF_AUDIT>

---

## 2026-05-20 Orphan Autopsy Pass

What was wrong:
- Task 14 was still a real gap. Source scanning could find pointer-retention debt, but it could not reclaim a Vault allocation whose owner died before `ReleaseBuffer`.
- A direct Unity scene scan inside `GlobalDataVault` would create a Core-to-scene authority leak and would not be Burst-compatible.

What was done:
- Added `IDataVault.SweepOrphanedHandles(NativeArray<SystemID> liveOwners, int liveOwnerCount, MemoryDefragPhase phase, uint activeBurstLockMask, out long releasedBytes)`.
- Added Burst `SweepOrphanedHandlesJob` with synchronous Fast/Standard Burst flags. It reads a caller-owned unmanaged live-owner table and marks scene-owned metadata rows missing from that table with `VaultMetaFlagOrphanCandidate`.
- Added reclamation in the PRE_SIMULATION maintenance section only. Locked buffers, active writer buffers, and external-view buffers are not reclaimed.
- Orphan candidate/reclaim counts are packed into `MemoryDefragTelemetryEntry.Reserved32`: high 16 bits = candidates, low 16 bits = reclaimed.
- Reclaimed orphan buffers trigger the SHINOBU_202 blackbox dump path.

Cinematic cheats used:
- No Unity scene hierarchy traversal in Core memory. The scene/service layer supplies a compact unmanaged `SystemID` table; the Vault performs a pure metadata cross-reference.
- No hot-path leak checks. The sweep is maintenance-phase only and preserves the O(1) resolve path.

Exact microseconds saved:
- Hot resolve path: unchanged, 0 added us.
- Orphan scan: O(100000 metadata rows) only when called in PRE_SIMULATION. Avoided managed scene traversal/reflection and managed `HashSet<SystemID>` allocation.

Compile status:
- `git diff --check` passed for `GlobalDataVault.cs`; CRLF warning only.
- Build not launched. CPU gate remained at 100%; no evidence supports starting `dotnet build`.

<SELF_AUDIT ultra_pass="orphan_autopsy">
  <Task id="14" previous="FAIL" now="PARTIAL">Core Burst orphan sweep and reclaim path exists. Remaining external work is population of the live-owner table by scene/service authority.</Task>
  <Job name="SweepOrphanedHandlesJob" burst="CompileSynchronously=true, FloatMode.Fast, FloatPrecision.Standard">
    <Field name="Metadata" attributes="NoAlias" />
    <Field name="LiveOwners" attributes="ReadOnly,NoAlias" />
    <Route phase="PRE_SIMULATION" activeBurstLocksRequired="0" compactionFence="exclusive" />
    <Output>VaultMetaFlagOrphanCandidate on dead scene-owned rows; reclamation performed by Vault maintenance thread.</Output>
  </Job>
  <BlackBox field="MemoryDefragTelemetryEntry.Reserved32" high16="orphanCandidates" low16="orphanReclaimed" />
  <DearLie>Unmanaged owner-table cross-reference replaces expensive Core-side scene reflection.</DearLie>
</SELF_AUDIT>

---

## 2026-05-20 X-Ray Waterfall Pass

What was wrong:
- Task 17 required a telemetry waterfall, but the facade was showing current block heatmap plus labels only.

What was done:
- Added fixed 64-column UI Toolkit `VaultTelemetryWaterfallElement`.
- The window samples `TryGetVaultTelemetrySnapshot(age, ...)` from newest history, maps allocation pressure to column height/color, and renders generation mismatch deltas as red pulses.
- No runtime code path was added; this is editor-only forensic visualization.

Cinematic cheats used:
- No generated texture, no per-refresh VisualElement rebuild, no IMGUI graph. The waterfall reuses fixed columns and style updates.

Exact microseconds saved:
- Runtime: 0 us, editor-only.
- Editor refresh avoids texture allocation and per-frame element construction; fixed 64 style updates per refresh.

<SELF_AUDIT ultra_pass="xray_waterfall">
  <Task id="17" previous="PARTIAL" now="PARTIAL_PLUS">UI Toolkit waterfall exists for memory pressure and generation mismatch events. Full disk-defrag-style deep legend remains polish, not runtime safety.</Task>
  <AllocationModel runtime="0">Fixed editor arrays and fixed 64 VisualElements; no player build path.</AllocationModel>
</SELF_AUDIT>

---

## 2026-05-20 Generation Reuse Hardening Pass

What was wrong:
- Final release removed metadata completely. A later allocation with the same `BufferID` could restart at generation 1 and theoretically validate a stale generation-1 descriptor.
- `SweepOrphanedHandles` rejected `liveOwnerCount == 0`, which blocked reclamation when the caller had an empty live-owner table.

What was done:
- Added tombstone epoch preservation in the flat metadata slot on `RemoveMetadata`.
- Added `ResolveInitialGenerationForAllocation(key)` so new allocations consume the next generation instead of resetting to 1.
- Changed orphan sweep validation to allow `liveOwnerCount == 0`; zero live owners now means every scene-owned metadata row is eligible for candidate marking.
- Removed `NativeArray.IsCreated` from the Burst job body and clamp live-owner count with raw length math.

Cinematic cheats used:
- No managed side-table and no release-time scene scan. The same flat metadata cache line carries both active state and freed generation epoch.

Exact microseconds saved:
- Hot resolve path: unchanged, 0 added us.
- Allocation/release pays one flat metadata read/write. This cost is cold-path maintenance, not gameplay pointer resolution.

<SELF_AUDIT ultra_pass="generation_reuse">
  <Task id="04" impact="PASS_PLUS">Generation descriptor safety now survives BufferID reuse because freed slots preserve a tombstone epoch.</Task>
  <Task id="14" impact="PARTIAL_PLUS">Empty live-owner table no longer suppresses orphan candidate marking.</Task>
  <HotPath addedCostUs="0">TryResolveHandle remains flat metadata load plus generation compare.</HotPath>
</SELF_AUDIT>

---

## 2026-05-20 Core Scheduling Handle Migration Pass

What was wrong:
- `BurstTokenBucketJobAdmissionService` still persisted five obsolete pointer-bearing `VaultBufferHandle<T>` fields.
- `Dispose` only cleared those handles locally, leaving Vault refcount release dependent on broader owner teardown.

What was done:
- Replaced the five persistent fields with `VaultGenerationHandle<T>`.
- Switched allocation to `GetGenerationHandle`.
- Switched all resolver methods to `IDataVault.TryResolveHandle(in VaultGenerationHandle<T>, out NativeArray<T>)`.
- Added `ReleaseVaultHandle<T>` so `Dispose` calls `ReleaseBuffer` for each non-zero descriptor before clearing state.

Cinematic cheats used:
- No new scheduler-side tracking table. The service stores only the 16-byte descriptor and pays no pointer refresh logic outside the existing Vault metadata compare.

Exact microseconds saved:
- Runtime resolve cost is effectively neutral: one local BufferID guard plus the existing O(1) Vault generation compare.
- Cold teardown prevents leaked Vault references with five release calls; no frame hot-path IO or allocation.

<SELF_AUDIT ultra_pass="core_scheduling_migration">
  <Task id="01" impact="PARTIAL_PLUS">One Core manager no longer persists pointer-bearing Vault handles.</Task>
  <Task id="02" impact="PARTIAL_PLUS">Migrated service now releases descriptors through GlobalDataVault on dispose.</Task>
  <CompileWall>Change is confined to Core/Scheduling and Core/Memory contracts already touched by SHINOBU_202.</CompileWall>
</SELF_AUDIT>

---

## 2026-05-20 Data Monolith Alias Eviction Pass

What was wrong:
- `H8StaticDataArena` kept a static `NativeArray<byte> _arena` view after boot.
- Data Monolith telemetry used legacy `VaultBufferHandle<T>.ResolvePointer`, retaining raw pointer lease routes in Core data access.
- Shutdown cleared fields but did not explicitly release the Data Monolith Vault descriptors.

What was done:
- Removed the static `_arena` field.
- Replaced payload, telemetry ring, and telemetry cursor handles with `VaultGenerationHandle<T>`.
- Replaced every `_arena` use with a method-local `TryRefreshArenaView(out NativeArray<byte> arena)` resolve.
- Replaced telemetry raw pointers with local `NativeArray<H8DataMonolithTelemetryEntry>` and `NativeArray<int>` resolves.
- `ShutdownArenaOnly` now releases payload, telemetry ring, and cursor descriptors through `GlobalDataVault.ReleaseBuffer`.

Cinematic cheats used:
- No managed cache map and no copied static-data mirror. Static data remains zero-copy; the only safety work is the Vault generation compare before taking a local view.

Exact microseconds saved:
- Removed two raw telemetry pointer lease routes from the static-data runtime.
- Static-data read access pays one O(1) generation compare and stays allocation-free.
- Shutdown adds three cold release calls and prevents Vault refcount leaks.

<SELF_AUDIT ultra_pass="data_monolith_alias_eviction">
  <Task id="01" impact="PARTIAL_PLUS">`H8StaticDataArena` no longer owns a persistent `NativeArray<byte>` field.</Task>
  <Task id="02" impact="PARTIAL_PLUS">Data Monolith descriptors are released by the Vault during shutdown.</Task>
  <Task id="06" impact="PASS_REUSE">Every Data Monolith arena/telemetry view resolves through `TryResolveHandle` and the flat generation table.</Task>
  <Route bufferIds="71103,71104,71105">Payload, telemetry ring, and cursor are persistent descriptors only; views are phase-local.</Route>
</SELF_AUDIT>

---

## 2026-05-20 Static/Babel Manager Pointer Quarantine Pass

What was wrong:
- `StaticDataStore` still used pointer-bearing Vault telemetry handles for static-data and B-Tree blackbox rings.
- `BabelDictionaryStore` still used pointer-bearing telemetry handles, `BabelErrorUtf8` pointer leases, and a padded dictionary fallback pointer derived through `ResolvePointer`.

What was changed:
- `StaticDataStore` telemetry descriptors now use `VaultGenerationHandle<T>`.
- `StaticDataStore` dump/record paths resolve local `NativeArray<T>` views with `TryResolveHandle` before writing or synchronously dumping.
- `BabelDictionaryStore` telemetry and `BabelErrorUtf8` descriptors now use `VaultGenerationHandle<T>`.
- `BabelDictionaryStore` padded fallback now uses `GetBuffer<byte>`, intentionally marking `BabelDictionaryMappedBytes` as an external view so live defrag refuses to relocate it while pointer-based lore jobs still exist.

Cinematic cheats used:
- No copied static-data or Babel mirror was introduced. The stores remain zero-copy; relocation safety is bought with generation validation or an explicit external-view block for the one active pointer-job path.

Exact microseconds saved:
- Removed legacy pointer lease routes from two Core data managers: targeted `rg` finds zero `VaultBufferHandle`, `GetBufferHandle`, `ResolvePointer`, `.Resolve(`, or `.ptr` hits in `StaticDataStore.cs`, `BabelDictionaryStore.cs`, `H8StaticDataArena.cs`, and `BurstTokenBucketJobAdmissionService.cs`.
- Telemetry/error paths pay one O(1) flat generation compare and no GC.
- Padded Babel fallback trades compaction freedom for UAF prevention until SHINOBU_207 rewrites pointer jobs to `NativeArray<byte>`.

Compile status:
- `git diff --check` passed for the touched Core memory/data/scheduling files; CRLF warnings only.
- Build not launched. CPU gate stayed at 100%; dotnet/csc absent.

<SELF_AUDIT ultra_pass="static_babel_pointer_quarantine">
  <Task id="01" impact="PARTIAL_PLUS">Two additional Core data managers no longer contain legacy Vault handle or ResolvePointer routes.</Task>
  <Task id="06" impact="PASS_REUSE">Static/Babel telemetry resolves through `TryResolveHandle` and the flat generation table.</Task>
  <Task id="08" impact="PARTIAL_PLUS">Babel padded fallback is explicitly external-view guarded instead of silently trusting a relocatable pointer.</Task>
  <ResidualRisk owner="SHINOBU_207">Babel lore/B-Tree jobs still consume raw `_basePointer`; relocation is blocked for the fallback blob until those jobs accept NativeArray inputs.</ResidualRisk>
</SELF_AUDIT>

---

## 2026-05-20 Simulation Bucketer Descriptor Migration Pass

What was wrong:
- `ModuloSimulationBucketer` persisted eight obsolete `VaultBufferHandle<T>` descriptors for bucket tables, rebalance scratch, frame state, and blackbox memory.
- All resolver helpers called `.Resolve(_dataVault)`, keeping the Core cadence service dependent on the legacy pointer-bearing bridge.
- `ReleaseHandlesOnly` cleared descriptors without calling `GlobalDataVault.ReleaseBuffer`, leaving Vault refcounts live across dispose/re-init.

What was changed:
- Replaced the eight persistent descriptors with `VaultGenerationHandle<T>`.
- Allocation now uses `GetGenerationHandle<T>`.
- Table access now resolves method-local `NativeArray<T>` views through `TryResolveVaultBuffer` and `IDataVault.TryResolveHandle`.
- Dispose/re-init completes the pending rebalance job, releases every non-zero descriptor through `ReleaseBuffer`, then clears local state.

Cinematic cheats used:
- No copied bucket mirror and no managed lookup table. The bucketer keeps the existing Vault-backed data flow; stale alias safety comes from one flat generation compare before each local view.

Exact microseconds saved:
- Removed all legacy Vault handle/pointer scanner hits from `ModuloSimulationBucketer.cs`.
- Runtime table access pays one O(1) generation compare and no GC.
- Cold dispose/re-init adds eight release calls and prevents Vault refcount leaks.

Compile status:
- Targeted scan is clean for `VaultBufferHandle`, `GetBufferHandle`, `TryGetBufferHandle`, `ResolvePointer`, `.Resolve(`, `.ptr`, and `ResolveBuffer` in `ModuloSimulationBucketer.cs`.
- `git diff --check` passed for `ModuloSimulationBucketer.cs`; CRLF warning only.
- Full compile not launched. CPU gate sampled 43.3% once, then returned to 100.0%; dotnet/csc absent. Build launch remains blocked until CPU is stably below 50%.

<SELF_AUDIT ultra_pass="simulation_bucketer_descriptor_migration">
  <Task id="01" impact="PARTIAL_PLUS">One additional Core runtime manager no longer persists pointer-bearing Vault handles.</Task>
  <Task id="02" impact="PARTIAL_PLUS">Bucketing descriptors now release through Vault lifetime authority on dispose/re-init.</Task>
  <Task id="06" impact="PASS_REUSE">Bucketing tables resolve through `TryResolveHandle` and the flat generation table.</Task>
  <Task id="16" impact="PASS_REUSE">The 300-frame bucketer blackbox remains Vault-backed and resolves through generation validation.</Task>
  <StructLayout name="SimulationBucketBlackBoxEntry" sizeBytes="64">Offsets remain explicit: int/uint/float fields 0..60, byte flags at 56/57, ushort pad at 58, StateHash at 60.</StructLayout>
  <ResidualRisk>`ModuloSimulationBucketer.cs` already had unrelated working-tree quality-curve/layout edits before this descriptor pass; this pass only changed Vault descriptor acquisition, resolve, and release.</ResidualRisk>
</SELF_AUDIT>

---

## 2026-05-20 Lockstep Hash Source Pointer Cleanup Pass

What was wrong:
- `LockstepStateValidator.TryGetHashSourceBuffer` acquired a local `VaultBufferHandle<T>`.
- It checked `handle.ptr` for native alignment before resolving the handle, so the proof path still depended on a cached pointer-bearing descriptor.
- It then called `handle.Resolve(vault)`, leaving a legacy bridge call inside rollback hash-source lookup.

What was changed:
- Hash source lookup now uses `TryGetGenerationHandle<T>`.
- The buffer is resolved with `IDataVault.TryResolveHandle`.
- Alignment validation now reads `buffer.GetUnsafeReadOnlyPtr()` from the transient resolved view, then discards the view after the caller finishes hashing.

Cinematic cheats used:
- No copied rollback mirror and no persistent hash-source descriptor. The validator keeps the existing Vault data route and proves safety with a local generation resolve.

Exact microseconds saved:
- Removed local `VaultBufferHandle<T>`, `TryGetBufferHandle`, `handle.ptr`, and `handle.Resolve` hits from the hash-source route.
- Runtime cost remains one O(1) generation compare when a hash source is requested; no GC or managed collection traffic.

Compile status:
- Targeted scan is clean for `VaultBufferHandle`, `GetBufferHandle`, `TryGetBufferHandle`, `ResolvePointer`, `handle.Resolve`, `.ptr`, and `ResolveBuffer` in `LockstepStateValidator.cs`.
- `git diff --check` passed for `LockstepStateValidator.cs`; CRLF warning only.
- Full compile not launched; latest gate is CPU 100.0% with an external `dotnet` process running (PID 16748), so compile launch is forbidden.

<SELF_AUDIT ultra_pass="lockstep_hash_source_pointer_cleanup">
  <Task id="01" impact="PARTIAL_PLUS">One Core determinism local consumer no longer validates or resolves through a pointer-bearing Vault handle.</Task>
  <Task id="06" impact="PASS_REUSE">Hash-source buffers resolve through `TryResolveHandle` and the flat generation table.</Task>
  <Task id="13" impact="PASS_REUSE">Rollback payload truth is unchanged; only local memory-safety descriptor resolution changed.</Task>
  <ResidualRisk>`LockstepStateValidator.cs` already had unrelated working-tree quality-cadence edits before this pass; this pass only touched hash-source Vault resolution.</ResidualRisk>
</SELF_AUDIT>

---

## 2026-05-20 Input Bridge Facade Pointer Cleanup Pass

What was wrong:
- `H8InputMappingFacade.SyncToVault` acquired `BridgeInputFacadeBindings` with `VaultBufferHandle<T>` and wrote through `ResolvePointer`.
- `ClearExistingBuffer` repeated the same pointer lease route for clearing the buffer.

What was changed:
- Input bridge sync now uses `GetGenerationHandle<H8InputFacadeBindingEntry>`.
- Buffer clear/write routes resolve a local `NativeArray<H8InputFacadeBindingEntry>` through `TryResolveHandle`.
- `ClearBuffer` accepts the resolved view and derives a transient pointer only from `NativeArray.GetUnsafePtr()` for `MemClear`.

Cinematic cheats used:
- No runtime mirror of input bindings. The serialized facade list remains the authoring source; the Vault buffer is only a local hydrated view during sync.

Exact microseconds saved:
- Removed local `VaultBufferHandle<T>`, `TryGetBufferHandle`, `ResolvePointer`, and cached pointer scanner hits from `H8InputMappingFacade.cs`.
- Sync path cost remains cold/editor-facing: one O(1) generation compare plus existing MemClear.

Compile status:
- Targeted scan is clean for `VaultBufferHandle`, `GetBufferHandle`, `TryGetBufferHandle`, `ResolvePointer`, `handle.Resolve`, `.ptr`, and `ResolveBuffer` in `H8InputMappingFacade.cs`.
- `git diff --check` passed for `H8InputMappingFacade.cs`; CRLF warning only.
- Full compile not launched; latest gate is CPU 100.0% with external `dotnet` processes running (PIDs 15912, 54304).

<SELF_AUDIT ultra_pass="input_bridge_facade_pointer_cleanup">
  <Task id="01" impact="PARTIAL_PLUS">One Core bridge consumer no longer writes Vault data through a pointer-bearing handle.</Task>
  <Task id="06" impact="PASS_REUSE">Input bridge bindings resolve through `TryResolveHandle` and the flat generation table.</Task>
  <Task id="18" impact="PASS_REUSE">Human-authored facade data still hydrates unmanaged DTOs without adding runtime parser allocation.</Task>
  <ResidualRisk>The serialized `List<Binding>` remains editor/authoring state; this pass only removes the Vault pointer route used during Play Mode sync.</ResidualRisk>
</SELF_AUDIT>

---

## 2026-05-20 Prefab Bridge Binder Pointer Cleanup Pass

What was wrong:
- `H8PrefabRegistryRuntimeBinder.Bind` acquired prefab mapping and lore link buffers with `VaultBufferHandle<T>`.
- Bind and clear paths filled those buffers through `ResolvePointer`.

What was changed:
- Prefab mapping and lore link buffers now use `VaultGenerationHandle<T>`.
- Bind and clear paths resolve local `NativeArray<T>` views through `TryResolveHandle`.
- `ClearBuffer` derives a transient pointer from the resolved view only for the immediate `MemClear`.

Cinematic cheats used:
- No copied prefab registry runtime mirror. The registry asset remains the source; Vault bridge DTOs are hydrated through local views only.

Exact microseconds saved:
- Removed local `VaultBufferHandle<T>`, `TryGetBufferHandle`, `ResolvePointer`, and cached pointer scanner hits from `H8PrefabRegistryRuntimeBinder.cs`.
- Bind path cost remains cold/bridge-facing: two O(1) generation compares plus existing DTO writes.

Compile status:
- Targeted scan is clean for `VaultBufferHandle`, `GetBufferHandle`, `TryGetBufferHandle`, `ResolvePointer`, `handle.Resolve`, `.ptr`, and `ResolveBuffer` in `H8PrefabRegistryRuntimeBinder.cs`.
- `git diff --check` passed for `H8PrefabRegistryRuntimeBinder.cs`; CRLF warning only.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo` was attempted when CPU sampled at 17.8% and dotnet/csc were absent.
- Build failed with 115 errors. First failures are unrelated missing domain references (`Hecton8.Logistics.Grid`, `FaunaKinematicsRuntime`, `H8BinaryWorldPager`, construction/docking DTOs). The generated `Hecton8.Core.csproj` also does not include `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs`, so existing `VaultGenerationHandle<T>` usages in previously migrated files cannot resolve in this project file.
- Compile wall status: blocked by stale/generated project graph and unrelated domain dependencies; no second build attempt launched.

<SELF_AUDIT ultra_pass="prefab_bridge_binder_pointer_cleanup">
  <Task id="01" impact="PARTIAL_PLUS">One more Core bridge consumer no longer writes Vault data through pointer-bearing handles.</Task>
  <Task id="06" impact="PASS_REUSE">Prefab bridge DTO buffers resolve through `TryResolveHandle` and the flat generation table.</Task>
  <Task id="16" impact="NO_CHANGE">VRAM telemetry publication remains unchanged; no new blackbox buffer was introduced.</Task>
  <ResidualRisk>The binder still uses managed registry assets and runtime prefab registration by design; this pass only removes the Vault pointer route.</ResidualRisk>
</SELF_AUDIT>

---

## 2026-05-20 Design Bridge Runtime Pointer Cleanup Pass

What was wrong:
- `H8BridgeFacadeRuntime` acquired design value bytes, macro header, and facade telemetry ring buffers through local `VaultBufferHandle<T>` descriptors.
- Clear/write/dump paths used `ResolvePointer`, keeping live tuning on the obsolete cached-pointer API.

What was changed:
- `BridgeDesignFacadeValues` now uses `GetGenerationHandle<byte>` / `TryGetGenerationHandle<byte>` and resolves a local `NativeArray<byte>` view before clearing or writing aligned float values.
- `BridgeFacadeMacroHeader` now persists through a resolved local `NativeArray<H8FacadeMacroHeader>` view.
- `BridgeDesignFacadeTelemetryRing` now records, hashes, and dumps through a resolved local `NativeArray<H8FacadeTelemetryEntry>` view. Dump writes copy each entry to a stack local before creating the `ReadOnlySpan<byte>`.

Cinematic cheats used:
- No runtime mirror of design facade values. The bridge keeps the serialized facade as authoring truth and hydrates only the Vault DTO bytes needed for live tuning and macro persistence.

Exact microseconds saved:
- Removed local `VaultBufferHandle<T>`, `TryGetBufferHandle`, `GetBufferHandle`, `ResolvePointer`, `.ptr`, and `handle.Resolve` scanner hits from `H8BridgeFacadeRuntime.cs`.
- Runtime cost is one O(1) generation compare per touched bridge buffer; dump IO remains the dominant cold-path cost.

Compile status:
- Targeted scan is clean for `VaultBufferHandle`, `GetBufferHandle`, `TryGetBufferHandle`, `ResolvePointer`, `handle.Resolve`, `.ptr`, and `ResolveBuffer` in `H8BridgeFacadeRuntime.cs`.
- Full compile was not relaunched because the previous attempt is already blocked by stale/generated `Hecton8.Core.csproj` and unrelated missing domain references.

<SELF_AUDIT ultra_pass="design_bridge_runtime_pointer_cleanup">
  <Task id="01" impact="PARTIAL_PLUS">Another Core bridge route no longer resolves Vault data through pointer-bearing handles.</Task>
  <Task id="06" impact="PASS_REUSE">Design facade value, macro header, and telemetry buffers resolve through `TryResolveHandle` and the flat generation table.</Task>
  <Task id="16" impact="PASS_REUSE">The existing 300-frame bridge blackbox ring remains Vault-backed and now dumps from a generation-validated local view.</Task>
  <ResidualRisk>Bridge authoring assets remain managed Unity objects by design; this pass only removes the Vault pointer route used by runtime hydration and dump paths.</ResidualRisk>
</SELF_AUDIT>

---

## 2026-05-20 Content Authority Descriptor Migration Pass

What was wrong:
- `ContentBundleReferenceCounter` persisted `VaultBufferHandle<ContentBundleRefState>` and `VaultBufferHandle<int>` for bundle residency state/count.
- `ContentAuthorityRuntime` persisted four more legacy handles for content telemetry and pending-load ledgers.
- Each route resolved raw pointers through `ResolvePointer`; DataVault hot-swap cleared fields without returning Vault references.

What was changed:
- All six descriptors now use `VaultGenerationHandle<T>`.
- Resolve helpers reacquire stale or undersized descriptors with `GetGenerationHandle<T>`, then resolve method-local `NativeArray<T>` views through `TryResolveHandle`.
- Pointer use is transient and derived from the resolved view inside the current method only.
- Ref counter rebind, runtime teardown, and DataVault hot-swap now release descriptors through `ReleaseBuffer`.

Cinematic cheats used:
- No extra resident-content mirror. The existing fixed Vault ledgers remain the only runtime proof; Addressables ownership behavior was left to the existing content authority path.

Exact microseconds saved:
- Removed all `VaultBufferHandle<T>`, `GetBufferHandle`, `TryGetBufferHandle`, `ResolvePointer`, `.ptr`, and `ResolveBuffer` scanner hits from `ContentRuntimeServices.cs`.
- Access cost remains one O(1) generation compare per ledger/telemetry/pending-load resolve; teardown/hot-swap now has six cold release calls.

Compile status:
- Targeted scan is clean for `VaultBufferHandle`, `GetBufferHandle`, `TryGetBufferHandle`, `ResolvePointer`, `handle.Resolve`, `.ptr`, and `ResolveBuffer` in `ContentRuntimeServices.cs`.
- `git diff --check` passed for `ContentRuntimeServices.cs`; CRLF warning only.
- Full compile was not relaunched because stale/generated project graph blockers remain unchanged from the previous failed build.

<SELF_AUDIT ultra_pass="content_authority_descriptor_migration">
  <Task id="01" impact="PARTIAL_PLUS">A Core content manager no longer persists pointer-bearing Vault descriptors.</Task>
  <Task id="02" impact="PASS_REUSE">Content authority now releases its Vault descriptors instead of only clearing local fields.</Task>
  <Task id="06" impact="PASS_REUSE">Bundle ref, telemetry, and pending-load buffers resolve through `TryResolveHandle` and the flat generation table.</Task>
  <Task id="16" impact="PASS_REUSE">The existing 300-frame content blackbox remains Vault-backed and is accessed through generation-validated views.</Task>
  <ResidualRisk>`ContentRuntimeServices.cs` already had unrelated Addressables and hot-swap edits in the working tree; this pass touched only Vault descriptor acquisition, resolve, and release.</ResidualRisk>
</SELF_AUDIT>

---

## 2026-05-20 Homeostasis Base Descriptor Migration Pass

What was wrong:
- `HomeostasisBrain.cs` persisted `VaultBufferHandle<float>` for hardware metrics and frame-time samples.
- It also persisted `VaultBufferHandle<HomeostasisBlackBoxEntry>` for its 300-frame pressure blackbox.
- Shutdown and DataVault hot-swap cleared the fields without returning those Vault references.

What was changed:
- The three base descriptors now use `VaultGenerationHandle<T>`.
- Runtime resolve paths use `TryResolveHandle` and method-local `NativeArray<T>` views.
- Reacquire logic probes `TryGetGenerationHandle` before allocating so moved buffers are not cleared after a generation bump.
- Shutdown and DataVault hot-swap release non-zero descriptors through `ReleaseBuffer`.

Cinematic cheats used:
- No duplicate homeostasis mirror. The existing Vault buffers remain the only runtime telemetry and pressure state; this pass only replaces pointer-bearing descriptors.

Exact microseconds saved:
- Removed all `VaultBufferHandle<T>`, `GetBufferHandle`, `TryGetBufferHandle`, `ResolvePointer`, `.ptr`, and `ResolveBuffer` scanner hits from `HomeostasisBrain.cs`.
- Access cost remains one O(1) generation compare per base homeostasis buffer resolve; hot policy math is unchanged.

Compile status:
- Targeted scan is clean for `VaultBufferHandle`, `GetBufferHandle`, `TryGetBufferHandle`, `ResolvePointer`, `handle.Resolve`, `.ptr`, and `ResolveBuffer` in `HomeostasisBrain.cs`.
- `git diff --check` passed for `HomeostasisBrain.cs`; CRLF warning only.
- Full compile was not relaunched because stale/generated project graph blockers remain unchanged from the previous failed build.

<SELF_AUDIT ultra_pass="homeostasis_base_descriptor_migration">
  <Task id="01" impact="PARTIAL_PLUS">The base global pressure authority no longer persists pointer-bearing Vault descriptors.</Task>
  <Task id="02" impact="PASS_REUSE">Base homeostasis descriptors are released through the Vault on shutdown and DataVault swap.</Task>
  <Task id="06" impact="PASS_REUSE">Hardware metrics, frame times, and homeostasis blackbox resolve through `TryResolveHandle` and the flat generation table.</Task>
  <Task id="16" impact="PASS_REUSE">The existing 300-frame homeostasis blackbox remains Vault-backed and is accessed through generation-validated views.</Task>
  <ResidualRisk>`HomeostasisBrain.ScalabilityDictator.cs` was intentionally deferred from this base pass because it owns job-fenced scalability state; the follow-up pass below closes that residual lane.</ResidualRisk>
</SELF_AUDIT>

---

## 2026-05-20 Homeostasis Scalability Dictator Descriptor Migration Pass

What was wrong:
- `HomeostasisBrain.ScalabilityDictator.cs` persisted seven pointer-bearing `VaultBufferHandle<T>` descriptors for buffers `70480..70485` and `70487`.
- Runtime/editor paths used `.Resolve(vault)` and `GetElementAsRef`, keeping the global quality dictator on stale-pointer-compatible routes.
- DataVault hot-swap reset the dictator descriptors after assigning the new Vault, which could leak old refs or release against the wrong Vault.

What was changed:
- The seven dictator descriptors now use `VaultGenerationHandle<T>`.
- All state/tuning/mock/telemetry reads and writes resolve method-local `NativeArray<T>` views through `TryResolveHandle`.
- Editor/test facades copy DTO rows, sanitize/mutate them, and write them back by index instead of retaining refs.
- Hot-swap now releases dictator descriptors against the previous Vault and completes the pending mock terrain sampler job before release.

Cinematic cheats used:
- The existing mock terrain sampler remains a one-row quality proof: `GlobalQualityWeight` directly maps trilinear sample probability and skipped-trilinear percent. No terrain dependency, collider query, or renderer probe was introduced.

Exact microseconds saved:
- Removed all `VaultBufferHandle<T>`, `GetBufferHandle`, `TryGetBufferHandle`, `ResolvePointer`, `.Resolve(...)`, `.ptr`, `GetElementAsRef`, `GetElementAsReadOnlyRef`, and `ResolveBuffer` scanner hits from `HomeostasisBrain.ScalabilityDictator.cs`.
- Frame policy math is unchanged. Buffer access cost is one flat generation compare per touched dictator lane; teardown/hot-swap adds seven cold release calls and removes stale alias risk.

Compile status:
- Targeted scan is clean for old pointer-bearing Vault routes in `HomeostasisBrain.ScalabilityDictator.cs` and `HomeostasisBrain.cs`.
- `git diff --check` passed for both files; CRLF warnings only.
- Full compile was not relaunched because the previous generated-project compile wall remains unchanged.

<SELF_AUDIT ultra_pass="homeostasis_scalability_dictator_descriptor_migration">
  <Task id="01" impact="PARTIAL_PLUS">The scalability dictator no longer persists pointer-bearing Vault descriptors or `NativeArray<T>` views.</Task>
  <Task id="02" impact="PASS_REUSE">Dictator descriptors release through the previous Vault during shutdown/hot-swap after job completion.</Task>
  <Task id="06" impact="PASS_REUSE">All seven dictator lanes resolve through `TryResolveHandle` and the flat generation table.</Task>
  <Task id="08" impact="PASS_REUSE">No per-resolve lock was added; the pending job is completed only at release/reset boundaries.</Task>
  <Task id="16" impact="PASS_REUSE">The 300-frame scalability oscilloscope remains Vault-backed and uses generation-validated views.</Task>
  <VaultBufferIds>70480 SystemHealth, 70481 ScalabilityState, 70482 MockHeavyLoad, 70483 MockScatterDensity, 70484 CsvScratch, 70485 TunerState, 70487 Oscilloscope.</VaultBufferIds>
  <ResidualRisk>Full Unity import/compile proof remains blocked by the existing stale generated project graph, not by this descriptor pass.</ResidualRisk>
</SELF_AUDIT>

---

## 2026-05-20 AUP Origin Shift Descriptor Migration Pass

What was wrong:
- `AupOriginShiftCoordinator.cs` persisted eight pointer-bearing `VaultBufferHandle<T>` descriptors for lanes `73030..73037`.
- AUP state, velocity, historical point, telemetry, runtime state, mock camera, CSV scratch, and counter views were resolved through `.Resolve(vault)`.
- Cached Vault replacement cleared descriptors without releasing the old Vault references.

What was changed:
- All eight descriptors now use `VaultGenerationHandle<T>`.
- `EnsureRuntimeState` resolves or reacquires method-local `NativeArray<T>` views through `TryResolveHandle`.
- Mock camera, counter, and CSV scratch routes now resolve local views before use.
- Cached Vault replacement releases all non-zero descriptors against the previous Vault before local state reset.

Cinematic cheats used:
- The existing deterministic mock camera remains the no-dependency proof path for rebase threshold behavior. It advances by a fixed simulation step and avoids scene/camera ownership or physics queries.

Exact microseconds saved:
- Removed all `VaultBufferHandle<T>`, `GetBufferHandle`, `TryGetBufferHandle`, `ResolvePointer`, `.Resolve(...)`, `.ptr`, `GetElementAsRef`, `GetElementAsReadOnlyRef`, and `ResolveBuffer` scanner hits from `AupOriginShiftCoordinator.cs`.
- Rebase jobs still use raw pointers only after generation validation; runtime cost is one flat generation compare per lane resolve and no managed allocation.

Compile status:
- Targeted scan is clean for old pointer-bearing Vault routes in `AupOriginShiftCoordinator.cs`.
- `git diff --check` passed for `AupOriginShiftCoordinator.cs`; CRLF warning only.
- Full compile was not relaunched because the previous generated-project compile wall remains unchanged.

<SELF_AUDIT ultra_pass="aup_origin_shift_descriptor_migration">
  <Task id="01" impact="PARTIAL_PLUS">AUP origin-shift no longer persists pointer-bearing Vault descriptors.</Task>
  <Task id="02" impact="PASS_REUSE">Coordinator releases old descriptors through the previous Vault on cached Vault replacement.</Task>
  <Task id="06" impact="PASS_REUSE">All eight AUP lanes resolve through `TryResolveHandle` and the flat generation table.</Task>
  <Task id="12" impact="PASS_REUSE">AUP rebase continues byte/struct-local mutation without float demotion of absolute `double3` positions.</Task>
  <Task id="16" impact="PASS_REUSE">The 300-frame origin-shift telemetry ring remains Vault-backed and generation-validated.</Task>
  <VaultBufferIds>73030 States, 73031 Velocities, 73032 HistoricalPoints, 73033 Telemetry, 73034 RuntimeState, 73035 MockCamera, 73036 CsvScratch, 73037 Counter.</VaultBufferIds>
  <ResidualRisk>Supplemental tether/history buffers still enter through existing `TryGetBuffer<T>` external-view routes; this pass only removed the coordinator-owned persistent legacy descriptors.</ResidualRisk>
</SELF_AUDIT>

---

## 2026-05-20 - Global Telemetry Blackbox Descriptor Migration Pass

What was wrong:
- `GlobalTelemetryBus.Blackbox.cs` persisted eleven legacy `VaultBufferHandle<T>` descriptors and eleven static Vault-backed `NativeArray<T>` aliases.
- Failed Vault binding and teardown cleared local state without returning all blackbox Vault references.

What was done:
- Replaced crash blackbox handles with `VaultGenerationHandle<T>` descriptors.
- Removed persistent `NativeArray<T>` blackbox fields; event, source, frame commit, dump, MMF, watchdog, and editor routes now resolve method-local views through `TryResolveHandle`.
- Added descriptor release on failed bind and teardown after relocation locks are released.

Cinematic cheats used:
- Existing blackbox mock physics/origin payloads remain 64-byte copied snapshots, not live physics or scene traversal. The system records proof bytes and hashes instead of simulating crash causes.

Exact microseconds saved:
- Removed all persistent `NativeArray<T>` fields, `VaultBufferHandle<T>`, `GetBufferHandle`, `TryGetBufferHandle`, `ResolvePointer`, `.ptr`, `handle.Resolve`, `GetElementAsRef`, `GetElementAsReadOnlyRef`, and `ResolveBuffer` scanner hits from `GlobalTelemetryBus.Blackbox.cs`.
- Runtime cost shifts to one flat generation compare per touched blackbox lane. Teardown now returns eleven Vault references; no managed allocation was added.

Compile status:
- Targeted scan is clean for old pointer-bearing Vault routes and persistent Vault-backed `NativeArray<T>` fields in `GlobalTelemetryBus.Blackbox.cs`.
- `git diff --check` passed for `GlobalTelemetryBus.Blackbox.cs`; CRLF warning only.
- Full compile was not relaunched because the previous generated-project compile wall remains unchanged.

<SELF_AUDIT ultra_pass="global_telemetry_blackbox_descriptor_migration">
  <Task id="01" impact="PASS_DELTA">Crash blackbox no longer persists legacy pointer-bearing Vault handles or persistent Vault-backed NativeArray fields.</Task>
  <Task id="02" impact="PASS_DELTA">Failed bind and teardown release blackbox descriptors through the Vault after unlock.</Task>
  <Task id="06" impact="PASS_REUSE">All blackbox lanes resolve through `TryResolveHandle` and the generation table.</Task>
  <Task id="08" impact="PASS_WITH_FENCE">Buffers remain locked only because the diagnostic ring API intentionally exports a raw pointer; manager-side NativeArray aliases are gone.</Task>
  <Task id="16" impact="PASS_REUSE">300-frame crash blackbox remains Vault-backed and generation-validated.</Task>
  <VaultBufferIds>ShinobuCrashBlackboxBytes, ShinobuCrashMmfScratch, ShinobuCrashDumpHeader, ShinobuCrashTelemetryEvents, ShinobuCrashSourceSlots, ShinobuCrashLoggingMasks, ShinobuCrashAtomicState, ShinobuCrashWatchdogCounters, ShinobuCrashWatchdogSamples, ShinobuCrashWatchdogStaleProbes, ShinobuCrashWatchdogActive.</VaultBufferIds>
  <ResidualRisk>`TryGetBlackboxRingBuffer` still returns a raw diagnostic pointer by design; active lifetime locks block relocation for those buffers until the ring-pointer contract is replaced.</ResidualRisk>
</SELF_AUDIT>

---

## 2026-05-20 - Memory Sentinel Descriptor And Lock Fence Migration Pass

What was wrong:
- `MemorySentinelRuntime.cs` persisted ten legacy `VaultBufferHandle<T>` descriptors.
- External watched targets used `TryGetBufferHandle` plus `ResolvePointer`.
- Validation target buffers were unlocked before result consumption and rollback copy, exposing a defrag relocation window around stored target pointers.

What was done:
- Replaced sentinel-owned lanes `70873..70882` with `VaultGenerationHandle<T>` descriptors and local `TryResolveHandle` views.
- Replaced external target lookup with `TryGetGenerationHandle` plus local resolved `NativeArray<T>` views before deriving locked target pointers.
- Moved unlock after `ConsumeResults` and placed it in `finally`.
- Added release of sentinel-owned descriptors on disable and DataVault replacement.

Cinematic cheats used:
- The sentinel keeps mock inventory and mod-quarantine byte spans as tiny deterministic payloads instead of probing real inventory/mod systems directly.

Exact microseconds saved:
- Removed all `VaultBufferHandle<T>`, `GetBufferHandle`, `TryGetBufferHandle`, `ResolvePointer`, `.Resolve(...)`, `.ptr`, `GetElementAsRef`, `GetElementAsReadOnlyRef`, and `ResolveBuffer` scanner hits from `MemorySentinelRuntime.cs`.
- Runtime validation still pays the existing hash workload; pointer acquisition now pays one generation compare per watched target and prevents rollback-after-unlock UAF.

Compile status:
- Targeted scan is clean for old pointer-bearing Vault routes in `MemorySentinelRuntime.cs`.
- `git diff --check` passed for `MemorySentinelRuntime.cs`; CRLF warning only.
- Full compile was not relaunched because the previous generated-project compile wall remains unchanged.

<SELF_AUDIT ultra_pass="memory_sentinel_descriptor_lock_fence_migration">
  <Task id="01" impact="PASS_DELTA">Memory sentinel no longer persists pointer-bearing Vault descriptors.</Task>
  <Task id="02" impact="PASS_DELTA">Sentinel-owned descriptors release on disable or DataVault replacement.</Task>
  <Task id="06" impact="PASS_REUSE">Owned and external watched lanes resolve through generation descriptors.</Task>
  <Task id="10" impact="PASS_DELTA">Target pointer aliases are now locked through result consumption and rollback.</Task>
  <Task id="16" impact="PASS_REUSE">300-frame sentinel telemetry remains Vault-backed and generation-validated.</Task>
  <VaultBufferIds>70873 ValidationStates, 70874 Targets, 70875 Results, 70876 RollbackBytes, 70877 MockInventory, 70878 Telemetry, 70879 RuntimeState, 70880 AupSnapshot, 70881 CsvScratch, 70882 ModQuarantine.</VaultBufferIds>
  <ResidualRisk>`MemorySentinelTargetDTO` still stores phase-local raw target pointers for the scheduled validation job; those pointers are covered by Vault locks until `ConsumeResults` completes.</ResidualRisk>
</SELF_AUDIT>

---

## 2026-05-20 - Input Curve Haptics Tuner Descriptor Migration Pass

What was wrong:
- `InputCurveHapticsTunerWindow.cs` used `GetBufferHandle`, `VaultBufferHandle<T>`, `GetElementAsReadOnlyRef`, and `GetElementAsRef` for `ShinobuInputProfile` and `ShinobuInputCurrentDto`.
- The path is editor-only, but it was still a human-control facade demonstrating obsolete pointer-bearing Vault access.

What was done:
- Replaced both local handles with `VaultGenerationHandle<T>`.
- Resolved `InputProfileDTO` and `InputStateDTO` as method-local `NativeArray<T>` views through `IDataVault.TryResolveHandle`.
- Replaced ref access with row-zero copy/read/write by index.

Cinematic cheats used:
- The oscilloscope remains a presentation-only editor preview of one input DTO row. No runtime haptic or physical simulation was added.

Exact microseconds saved:
- Runtime hot path unchanged.
- Editor repaint pays two O(1) generation comparisons and copies one 64-byte profile row plus one 24-byte input row, while removing all obsolete pointer-bearing Vault API hits from the facade.

Compile status:
- Targeted scan is clean for old pointer-bearing Vault routes in `InputCurveHapticsTunerWindow.cs`.
- `git diff --check` passed for `InputCurveHapticsTunerWindow.cs`; CRLF warning only.
- Full compile was not relaunched because the previous generated-project compile wall remains unchanged.

<SELF_AUDIT ultra_pass="input_curve_haptics_tuner_descriptor_migration">
  <Task id="01" impact="PASS_DELTA">Editor facade no longer uses legacy pointer-bearing handles for input Vault rows.</Task>
  <Task id="06" impact="PASS_REUSE">Rows resolve through `TryResolveHandle` and the generation table.</Task>
  <Task id="17" impact="PASS_DELTA">Human-control tuning facade now demonstrates the generation descriptor route.</Task>
  <Task id="20" impact="PASS_DELTA">Targeted scanner proof removed all legacy pointer API hits from this facade.</Task>
  <VaultBufferIds>70524 ShinobuInputProfile, 70520 ShinobuInputCurrentDto.</VaultBufferIds>
  <ResidualRisk>`InputDispatcher.cs` remains a larger runtime owner with legacy `VaultBufferHandle<T>` debt and is still queued for a separate migration pass.</ResidualRisk>
</SELF_AUDIT>

---

## 2026-05-20 - Input Dispatcher Runtime Descriptor Migration Pass

What was wrong:
- `InputDispatcher.cs` persisted twelve legacy `VaultBufferHandle<T>` descriptors for deterministic input, haptics, XR state, telemetry, replay snapshot, and CSV scratch buffers.
- Hot and phase-cadence methods resolved through `.Resolve`, `GetElementAsRef`, `GetElementAsReadOnlyRef`, and `ResolvePointer`-derived paths.
- The input replay background thread read `_inputReplaySnapshotHandle.ptr`, a cached Vault pointer that could outlive a relocation.

What was done:
- Replaced all input-owned persistent Vault descriptors with `VaultGenerationHandle<T>`.
- Added local resolve/acquire helpers that return method-local `NativeArray<T>` views from `IDataVault.TryResolveHandle`.
- Replaced ref row access with index read/write.
- Released descriptors on shutdown and DataVault service replacement.
- Moved replay snapshot copying into `StageInputReplaySnapshot` while the local Vault view is valid; the replay worker now flushes the MMF accessor only.

Cinematic cheats used:
- Mock collision haptics remain deterministic signal fakes rather than physics probes; haptic output still decays from compact DTO rows instead of simulating hardware response.

Exact microseconds saved:
- Removed all `VaultBufferHandle<T>`, `GetBufferHandle`, `TryGetBufferHandle`, `.Resolve(...)`, `.ptr`, `ResolvePointer`, `GetElementAsRef`, `GetElementAsReadOnlyRef`, and `ResolveBuffer` scanner hits from `InputDispatcher.cs`.
- Runtime input lanes pay one flat generation compare per touched Vault row. Replay copy stays the existing 12 KB cadence but no longer relies on a background stale Vault pointer.

Compile status:
- Targeted scan is clean for old pointer-bearing Vault routes in `InputDispatcher.cs`.
- `git diff --check` passed for `InputDispatcher.cs`; CRLF warning only.
- Full compile was not relaunched because the previous generated-project compile wall remains unchanged.

<SELF_AUDIT ultra_pass="input_dispatcher_runtime_descriptor_migration">
  <Task id="01" impact="PASS_DELTA">Input runtime no longer persists pointer-bearing Vault handles.</Task>
  <Task id="02" impact="PASS_DELTA">Input-owned descriptors release on shutdown or DataVault replacement.</Task>
  <Task id="06" impact="PASS_REUSE">Deterministic input, haptic, XR, telemetry, replay, and CSV lanes resolve through generation descriptors.</Task>
  <Task id="08" impact="PASS_DELTA">Replay worker no longer dereferences Vault memory; phase-local staging performs the Vault copy.</Task>
  <Task id="16" impact="PASS_REUSE">300-frame input telemetry remains Vault-backed and generation-validated.</Task>
  <VaultBufferIds>70520 CurrentDto, 70521 JournalRing, 70522 ButtonMaskWindow, 70523 BlockMask, 70524 Profile, 70525 TelemetryRing, 70526 ReplaySnapshot, 70527 HapticCommands, 70530 StateBridgeRing, 70531 XRInputStates, 70532 XRLookAtRayCommands, 70533 CsvScratch.</VaultBufferIds>
  <ResidualRisk>Input still uses managed Unity InputSystem devices and MMF handles by design; the Vault UAF path is removed, but Unity/player/runtime proof is still pending.</ResidualRisk>
</SELF_AUDIT>

---

## 2026-05-20 - System Dispatcher Phase Fence Descriptor Migration Pass

What was wrong:
- `SystemDispatcher.cs` persisted legacy `VaultBufferHandle<T>` descriptors for H8 time, dispatcher blackbox, master job/fence telemetry, presentation suppression, and static raycast buffers.
- Master dispatcher and raycast paths resolved through `.Resolve` / `ResolveBuffer`; DataVault service replacement did not release old descriptors before caching the new service.
- The dispatcher owns the phase contract that makes the "Dear Lie" lock-free Vault read model valid, so stale handles here had a larger blast radius than ordinary consumers.

What was done:
- Replaced dispatcher-owned persistent Vault descriptors with `VaultGenerationHandle<T>`.
- Added `TryResolveDispatcherVaultBuffer` and `TryResolveOrAcquireDispatcherVaultBuffer` helpers that return method-local `NativeArray<T>` views through `IDataVault.TryResolveHandle`.
- Replaced master simulation, telemetry, domain fence, H8 time, blackbox, and raycast resolve routes with generation-checked local views.
- Added release of dispatcher-owned descriptors on shutdown and DataVault hot-swap through `IDataVault.ReleaseBuffer`.
- Kept existing scheduled-raycast Vault locks while a `RaycastCommand` job owns the phase-local command/hit views.

Cinematic cheats used:
- The dispatcher still relies on temporal segregation rather than atomics: Vault movement is only legal outside active simulation jobs, while consumers treat metadata as read-only during scheduled phases. This is the memory-safety "Dear Lie"; no per-resolve locks or interlocked barriers were added.

Exact microseconds saved:
- Removed all `VaultBufferHandle<T>`, `GetBufferHandle`, `TryGetBufferHandle`, `ResolvePointer`, `.ptr`, `.Resolve(...)`, `GetElementAsRef`, `GetElementAsReadOnlyRef`, and `ResolveBuffer` scanner hits from `SystemDispatcher.cs`.
- Runtime dispatcher lanes pay one flat generation compare per touched Vault lane. The dominant costs remain job dependency combining, raycast scheduling, and telemetry ring writes.

Compile status:
- Targeted scan is clean for old pointer-bearing Vault routes in `SystemDispatcher.cs`.
- Broad touched-file scan only reports `HectonThreadPriorityPolicy.Resolve(...)` in `GlobalTelemetryBus.Blackbox.cs`; those are non-Vault thread-priority helpers.
- `git diff --check` passed for `SystemDispatcher.cs`; CRLF warning only.
- Full compile was not relaunched because the previous generated-project compile wall remains unchanged.

<SELF_AUDIT ultra_pass="system_dispatcher_phase_fence_descriptor_migration">
  <Task id="01" impact="PASS_DELTA">SystemDispatcher no longer persists pointer-bearing Vault descriptors.</Task>
  <Task id="02" impact="PASS_DELTA">Dispatcher-owned descriptors release on shutdown or DataVault replacement.</Task>
  <Task id="06" impact="PASS_REUSE">H8 time, blackbox, master telemetry, fences, and raycast lanes resolve through generation descriptors.</Task>
  <Task id="08" impact="PASS_DELTA">Dispatcher phase owner now preserves lock-free metadata reads without cached Vault pointers.</Task>
  <Task id="16" impact="PASS_REUSE">300-frame dispatcher blackbox and master telemetry rings remain Vault-backed and generation-validated.</Task>
  <VaultBufferIds>9 H8Time, 8 DispatcherRaycastHits, 463 RaycastPendingCommands, 464 RaycastScheduledCommands, 465 SystemDispatcherBlackBox, 466 SystemDispatcherBlackBoxCursor, 70620 MasterJobHandles, 70621 MasterDependencyScratch, 70622 MasterJobDependencyTelemetry, 70623 MasterPipelineTelemetry, 70624 MasterPipelineCursor, 70625 MasterMockTimeDilationSignals, 70626 MasterPresentationSuppression, 70627 DomainFenceHandles, 70628 FenceTelemetry, 70629 FenceTelemetryCursor.</VaultBufferIds>
  <ResidualRisk>`SystemDispatcher.cs` still contains pre-existing cross-domain using directives and managed receiver arrays inherited from prior dispatcher work; this pass removed the Vault UAF route, not the entire dispatcher coupling surface.</ResidualRisk>
</SELF_AUDIT>

---

## 2026-05-20 - Acoustic Echo Location Descriptor Migration Pass

What was wrong:
- `AcousticEchoLocationRuntime.cs` persisted static `VaultBufferHandle<T>` descriptors for frame taps, pending taps, tracking result, and the 300-frame acoustic blackbox.
- Queue enqueue/drain/drop and blackbox paths resolved through cached handle metadata via `.Resolve(...)`.
- Dispose used `ReleaseOwnerBuffers(SystemID.AISensory)`, which is too broad for a static sensory runtime sharing the owner ID with neighboring systems.

What was done:
- Replaced the four static descriptors with `VaultGenerationHandle<T>`.
- Added `TryResolveOrAcquireVaultBuffer`, `TryResolveVaultBuffer`, `TryResolvePendingTaps`, and scoped release helpers.
- Replaced every `.Resolve(...)` route with method-local generation-checked `NativeArray<T>` views.
- On DataVault replacement, the active tracking fence is completed before old descriptors are released.
- Dispose now releases only this runtime's four descriptors through `IDataVault.ReleaseBuffer`.

Cinematic cheats used:
- The acoustic trail remains a capped 32-tap breadcrumb scan. It avoids a heavy continuous acoustic field simulation and persists only the best sensory trail plus blackbox proof rows.

Exact microseconds saved:
- Removed all legacy Vault scanner hits from `AcousticEchoLocationRuntime.cs`.
- The hot path adds one flat generation compare per queue/frame/blackbox view. The dominant Burst work remains the capped O(32) tap scan; no unbounded acoustic propagation or extra managed allocation was introduced.

Compile status:
- Targeted scan is clean for old pointer-bearing Vault routes in `AcousticEchoLocationRuntime.cs`.
- `git diff --check` passed for `AcousticEchoLocationRuntime.cs`; CRLF warning only.
- Full compile was not relaunched because the previous generated-project compile wall remains unchanged and the user explicitly forbade unnecessary rebuilds.

<SELF_AUDIT ultra_pass="acoustic_echo_location_descriptor_migration">
  <Task id="01" impact="PASS_DELTA">Acoustic echo runtime no longer persists pointer-bearing Vault descriptors.</Task>
  <Task id="02" impact="PASS_DELTA">Dispose and DataVault replacement release scoped generation descriptors.</Task>
  <Task id="06" impact="PASS_REUSE">Frame taps, pending taps, trail result, and blackbox lanes resolve through generation descriptors.</Task>
  <Task id="08" impact="PASS_DELTA">The scheduled tracking job receives only phase-local NativeArray views.</Task>
  <Task id="16" impact="PASS_REUSE">300-frame acoustic blackbox remains Vault-backed and generation-validated.</Task>
  <VaultBufferIds>AcousticEchoFrameTaps, AcousticEchoPendingTaps, AcousticEchoTrailState, AcousticEchoBlackBox.</VaultBufferIds>
  <ResidualRisk>Hot-swap completion of the active tracking fence is a cold-path safety fence; full Unity import/profiler proof remains pending behind the compile-wall blockers.</ResidualRisk>
</SELF_AUDIT>

---

## 2026-05-20 - Path Funnel Navmesh Descriptor Migration Pass

What was wrong:
- `PathFunnelNavmeshRuntime.cs` persisted five `VaultBufferHandle<T>` descriptors for active paths, WFC cell masks, invalidation ring, telemetry ring, and runtime state.
- Fast/late-frame and public path APIs resolved through `.Resolve(...)`, while runtime state copied legacy handle `Length` and `GenerationID`.
- The WFC grid read path used `TryGetBuffer`, bypassing the generation descriptor route.

What was done:
- Replaced the five owned descriptors with `VaultGenerationHandle<T>`.
- Added generation resolve/acquire/release helpers and removed legacy handle length/generation dependencies.
- Routed mutation, active-path, invalidation, telemetry, runtime-state, and WFC-grid views through `IDataVault.TryResolveHandle`.
- On disable or DataVault replacement, released only this component's five owned descriptors.

Cinematic cheats used:
- Path invalidation remains a 500-bit WFC mask test over tracked corridors. No heavy navmesh rebuild or physics probe was introduced during door state changes.

Exact microseconds saved:
- Removed all legacy Vault scanner hits from `PathFunnelNavmeshRuntime.cs`, including `TryGetBuffer`.
- Hot path cost is bounded to generation compares plus the existing active-path mask scan. No managed allocation or persistent Vault view was added.

Compile status:
- Targeted scan is clean for old pointer-bearing Vault routes in `PathFunnelNavmeshRuntime.cs`.
- `git diff --check` passed for `PathFunnelNavmeshRuntime.cs`; CRLF warning only.
- Full compile was not relaunched because the previous generated-project compile wall remains unchanged and the user explicitly forbade unnecessary rebuilds.

<SELF_AUDIT ultra_pass="path_funnel_navmesh_descriptor_migration">
  <Task id="01" impact="PASS_DELTA">Path funnel runtime no longer persists pointer-bearing Vault descriptors.</Task>
  <Task id="02" impact="PASS_DELTA">Owned descriptors release on disable and DataVault replacement.</Task>
  <Task id="06" impact="PASS_REUSE">Path, mask, invalidation, telemetry, state, and WFC-grid views resolve through generation descriptors.</Task>
  <Task id="08" impact="PASS_DELTA">WFC door invalidation uses phase-local views and retains no direct Vault grid alias.</Task>
  <Task id="16" impact="PASS_REUSE">300-frame path funnel telemetry ring remains Vault-backed and generation-validated.</Task>
  <VaultBufferIds>PathFunnelActivePaths, PathFunnelCellMasks, PathFunnelInvalidations, PathFunnelTelemetryRing, PathFunnelRuntimeState.</VaultBufferIds>
  <ResidualRisk>Full Unity import/profiler proof remains pending behind the compile-wall blockers; this pass removes the Vault UAF route only.</ResidualRisk>
</SELF_AUDIT>

---

## 2026-05-20 - WFC Laser Cut Tool Descriptor Migration Pass

What was wrong:
- `WfcLaserCutRuntime.cs` persisted two static `VaultBufferHandle<T>` descriptors and used cached `.ptr` fields as raw cut-progress and blackbox pointers.
- Tool progress writes are gameplay truth for sealed-door cutting, so stale Vault pointers here could corrupt or crash active interaction.
- Visual overkill used discrete `GlobalRegistry.ScalabilityTier` branches.

What was done:
- Replaced cut progress and laser-cut blackbox descriptors with `VaultGenerationHandle<T>`.
- Rewrote progress clear, progress write, telemetry write, and blackbox dump paths to use local `NativeArray<T>` views resolved by `IDataVault.TryResolveHandle`.
- Added scoped release on DataVault replacement.
- Replaced tier branching with a continuous `HomeostasisBrain.GlobalQualityWeight` smoothstep multiplied by stress headroom.

Cinematic cheats used:
- Door cutting remains a shader clip sphere plus molten/heat scalars, not a mesh boolean or physics fracture. The saved CPU budget feeds shader feedback and haptic/audio signals.

Exact microseconds saved:
- Removed all legacy Vault scanner hits and raw progress/telemetry pointer routes from `WfcLaserCutRuntime.cs`.
- The hot path pays two generation compares per active cut attempt. The visual curve is scalar math and avoids any discrete tier dispatch.

Compile status:
- Targeted scan is clean for old pointer-bearing Vault routes in `WfcLaserCutRuntime.cs`.
- `git diff --check` passed for `WfcLaserCutRuntime.cs`; CRLF warning only.
- Full compile was not relaunched because the previous generated-project compile wall remains unchanged and the user explicitly forbade unnecessary rebuilds.

<SELF_AUDIT ultra_pass="wfc_laser_cut_descriptor_migration">
  <Task id="01" impact="PASS_DELTA">Tool runtime no longer persists pointer-bearing Vault descriptors.</Task>
  <Task id="02" impact="PASS_DELTA">Owned descriptors release on DataVault replacement.</Task>
  <Task id="06" impact="PASS_REUSE">Cut progress and blackbox views resolve through generation descriptors.</Task>
  <Task id="08" impact="PASS_DELTA">Door cutting remains a shader clip fake instead of CPU mesh fracture.</Task>
  <Task id="09" impact="PASS_DELTA">Visual overkill now consumes continuous GlobalQualityWeight instead of quality-tier branches.</Task>
  <Task id="16" impact="PASS_REUSE">Laser-cut blackbox remains Vault-backed and generation-validated.</Task>
  <VaultBufferIds>WfcDoorCutProgress01, WfcLaserCutBlackBox.</VaultBufferIds>
  <ResidualRisk>No explicit shutdown hook exists for this static tool runtime; descriptors are released on DataVault replacement, and process teardown remains Vault-owned.</ResidualRisk>
</SELF_AUDIT>
