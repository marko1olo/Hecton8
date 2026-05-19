# LOG_SHINOBU_101

Date: 2026-05-19
Agent: SHINOBU_101
Domain: ECHELON 1 CORE & MEMORY INFRASTRUCTURE
Status: PENDING VERIFICATION - external Visor/Somatic compile block

## What Was Wrong

- Addressables handle tracking depended on managed `Dictionary`/`Queue`/`List` patterns in `AssetLifecycleGovernor`.
- Native handle DTO layout did not satisfy the exact 64-byte map-entry contract requested by the batch.
- TTL was not fully mirrored into the native open-address map, and VRAM panic behavior was too broad.
- CSV tuning path used managed text ingestion instead of Vault scratch bytes.
- Heap tuner was IMGUI/`OnGUI`, not UI Toolkit.

## What Was Done

- Replaced Addressables hot residency lookup with fixed managed handle slots plus Vault buffer `AddressableHeapHandleMap`.
- Added fixed 16384-slot linear-probe map pressure behavior. At 85% occupied/tombstone usage, unused assets are marked for emergency release instead of resizing.
- Rebuilt `AssetHandleMapEntryDTO` as explicit 64 bytes:
  - offset 0: `ulong AssetHash` size 8
  - offset 8: `ulong BundlePrefixHash` size 8
  - offset 16: `int PoolSlotIndex` size 4
  - offset 20: `int RefCount` size 4
  - offset 24: `float TimeToLive` size 4
  - offset 28: `uint Flags` size 4
  - offset 32: `uint Generation` size 4
  - offsets 36,40,44,48,52,56,60: seven `uint` pads size 28
  - total: 64 bytes, one L1 cache line, multiple of 16.
- Converted TTL evaluation to Burst `IJobParallelFor` with required compile flags and `[NoAlias]` native fields.
- Added AUP eviction scoring: player `double3` is subtracted from asset `double3` before `float3` distance.
- Added continuous TTL curve: `BaseTTL * lerp(0.1, 3.0, smoothstep(0.2, 0.8, GlobalQualityWeight))`.
- Added exact panic selection for the furthest 10% of unreferenced/unpinned handles with atomic zero-ref validation.
- Consolidated direct `Addressables.Release` calls into `TryExecuteOrDeferBlindFrameRelease` overloads.
- Routed CSV parsing through Vault scratch buffer `AddressableHeapCsvScratch` and added the byte-span parser for `asset_cache_profiles.csv`.
- Replaced heap tuner with UI Toolkit live graphs, sliders, and leak banner.

## Cinematic Cheats Used

- The Dear Lie is a globally cached cube impostor mesh plus checkerboard/error material. The runtime avoids blocking simulation on disk I/O and returns an O(1) visual placeholder until the real Addressables load completes.
- Before: player-facing stream miss risk was unbounded disk/driver wait on the main flow.
- After: fallback lookup is O(1), no load wait; real asset swap is deferred to presentation flow.

## Exact Microseconds Saved

Measured proof absent. Static estimate only:

- Managed dictionary/queue/list removal: expected 50-500 us jitter avoidance during biome-boundary lookup storms on i3/MX350-class hardware.
- Burst TTL pass: moves O(n) TTL decay off main thread; exact frame delta pending Unity Profiler.
- CSV/UI changes: editor/cold path only; no runtime frame-time saving claimed.

<SELF_AUDIT>
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Managed `_registry`, `_pendingRelease`, and hot scratch lists removed from Addressables handle path.</TASK>
    <TASK id="02" status="PASS">Pending release is fixed ring plus native pending/releasable bits; no `Resources.UnloadUnusedAssets` path added.</TASK>
    <TASK id="03" status="PASS">DTO hot fields are public fields; ref mutation helper uses `UnsafeUtility.AsRef` on Vault memory.</TASK>
    <TASK id="04" status="PASS">`AssetHandleMapEntryDTO` is explicit 64B with required offsets and editor size validation.</TASK>
    <TASK id="05" status="PASS">Emergency cache profiles are deterministic 16B records in Vault cache profile buffer.</TASK>
    <TASK id="06" status="PASS">Vault open-address table is fixed 16384 slots; no resize path.</TASK>
    <TASK id="07" status="PASS">TTL job is Burst `IJobParallelFor`, `[NoAlias]`, Fast/Standard, scheduled asynchronously and registered with `H8Memory`.</TASK>
    <TASK id="08" status="PASS">Normal release route is `TryExecuteOrDeferBlindFrameRelease`; static direct `Addressables.Release` calls exist only inside overloads of that method.</TASK>
    <TASK id="09" status="PASS">Fallback cube impostor mesh and checkerboard material exist as cached Dear Lie assets.</TASK>
    <TASK id="10" status="PASS">VRAM panic selects furthest 10% unreferenced/unpinned handles and bypasses the visible-frame gate.</TASK>
    <TASK id="11" status="PASS">Continuous quality-weight TTL formula implemented exactly.</TASK>
    <TASK id="12" status="PASS">Interlocked increment/decrement and compare-exchange zero-ref checks are used.</TASK>
    <TASK id="13" status="PASS">AUP subtraction occurs before float cast; chunk residency stamps asset AUP into tracker slots.</TASK>
    <TASK id="14" status="PASS">Bundle prefix hash stored in map; shared bundle TTL receives +50% residency.</TASK>
    <TASK id="15" status="PASS">`SetHeapSanitizerPin(uint,bool)` sets a pinned flag skipped by TTL/panic paths.</TASK>
    <TASK id="16" status="PASS">Vault buffers use `UninitializedMemory`; cold Burst clear job sanitizes essential fields.</TASK>
    <TASK id="17" status="PASS">300-entry telemetry ring dumps raw binary to `Docs/AgentLogs/Dump_MEMORY_SURGEON.bin` and `Docs/AgentLogs/Dump_SHINOBU_101_Addressables.bin`.</TASK>
    <TASK id="18" status="PASS">UI Toolkit tuner replaces IMGUI and reads telemetry directly.</TASK>
    <TASK id="19" status="PASS">CSV loads through `FileStream` into Vault byte scratch and parses `ReadOnlySpan<byte>`.</TASK>
    <TASK id="20" status="PASS">Leak banner scans native map and reports `AssetHash`, `BundlePrefixHash`, and ref count.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <STRUCT name="AssetHandleMapEntryDTO" size="64" alignment="16-compatible">
      <FIELD name="AssetHash" offset="0" size="8" />
      <FIELD name="BundlePrefixHash" offset="8" size="8" />
      <FIELD name="PoolSlotIndex" offset="16" size="4" />
      <FIELD name="RefCount" offset="20" size="4" />
      <FIELD name="TimeToLive" offset="24" size="4" />
      <FIELD name="Flags" offset="28" size="4" />
      <FIELD name="Generation" offset="32" size="4" />
      <FIELD name="_pad0.._pad6" offset="36..60" size="28" />
      <PROOF>8+8+4+4+4+4+4+28 = 64 bytes.</PROOF>
    </STRUCT>
    <STRUCT name="AssetHeapTelemetryEntry" size="64" alignment="16-compatible" purpose="300-frame blackbox ring" />
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Below quality 0.3, `smoothstep(0.2,0.8,q)` drives TTL toward 10% and panic pressure accelerates TTL decay. At high quality, TTL expands to 300%, allowing more assets to stay resident for visual continuity and backtracking. There is no low/high binary branch in TTL math.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    <NOTE>Zero private persistent `NativeArray` allocations in `AssetLifecycleGovernor`. Fixed managed arrays remain only for non-blittable `AsyncOperationHandle` slots and fixed managed metadata/scratch, as Task 01 explicitly required.</NOTE>
    <BUFFER id="AddressableHeapCacheProfiles" numeric="70323" />
    <BUFFER id="AddressableHeapTelemetry" numeric="70324" />
    <BUFFER id="AddressableHeapTrackers" numeric="70325" />
    <BUFFER id="AddressableHeapTimeToLive" numeric="70326" />
    <BUFFER id="AddressableHeapTrackerFlags" numeric="70327" />
    <BUFFER id="AddressableHeapHandleMap" numeric="70328" />
    <BUFFER id="AddressableHeapCsvScratch" numeric="70329" />
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <JOB name="AssetTtlEvaluationJob" consumes="AddressableHeapTrackers, AddressableHeapTimeToLive, AddressableHeapHandleMap" outputs="same buffers" attributes="[NoAlias]" />
    <DEPENDENCY input="none currently exposed" output="_ttlEvaluationHandle registered via H8Memory.RegisterActiveJob(SystemID.WorldStreaming)" />
    <LOCKS>Trackers, TTL, Flags, HandleMap are Vault-locked while TTL job is scheduled.</LOCKS>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    <STATUS>BLOCKED_BY_DEPENDENCY: compile attempts 2-5 report no SHINOBU/Optimization errors; external Visor reconstruction DTOs/IDs, Somatic comfort DTOs, and Construction HeadlessDroneTask references still fail.</STATUS>
    <ASMDEF>Optimization runtime has no local asmdef in `Assets/_Project/Scripts/Optimization`; edited runtime files are in `Hecton8.Core.csproj`. Editor asmdef `Hecton8.Optimization.Editor` references only `Hecton8.Core`.</ASMDEF>
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    <BEFORE>Blocking or waiting for a real streamed asset risks unbounded I/O/driver wait.</BEFORE>
    <AFTER>Return cached cube impostor/material in O(1), then swap after async completion.</AFTER>
  </DEAR_LIE_CONFIRMATION>
  <VERIFICATION>
    <STATIC_SCAN>Target-file scans found no `Dictionary`, `List`, `Queue`, `File.ReadAllText`, `string.Split`, `Regex`, or `OnGUI` in SHINOBU_101 target files.</STATIC_SCAN>
    <BUILD>Compile attempt 4 has no SHINOBU/Optimization errors; runtime/Unity/Profiler/GCMonitor proof absent.</BUILD>
  </VERIFICATION>
</SELF_AUDIT>

## 2026-05-19 Post-Polish Forensic Audit R6

What was wrong:
- Release ownership still had two leak-risk edges after the native table rewrite: tracked records could clear native ownership before proving that the non-blittable `AsyncOperationHandle` was accepted by a blind-frame bridge, and no-owner registration failures could drop a valid local handle if the fixed bridge was full.
- Hard-reaper `CleanBundleCache` used the same release helper but did not retain/retry the completed handle when the blind-frame bridge could not accept it.
- Prior audit text predated these release-ownership repairs and was therefore insufficient as the final integrator-facing evidence trail.

What was done:
- `ExecuteReleaseFlow` now runs a bounded `CanAcceptAddressableRelease` preflight before clearing native state or removing the managed record. If the handle cannot execute or fit in the detached bridge, the record stays owned and the fixed pending-release ring reclaims the key.
- Hard-reaper cache-clean handle release now uses the same bounded preflight. If the bridge is full, the handle remains live and the active async cleanup window retries instead of starting a second clean operation.
- Failed `RegisterAddressableHandleSlot` after `Addressables.LoadAssetAsync` now uses a no-owner fault release path: normal blind/defer first, then a short panic-scoped release only when no durable owner exists and the fixed bridge refuses the handle.
- Existing Core Memory authority for `AddressableHeapCsvScratch = 70329` was verified. SHINOBU_101 did not claim ownership of unrelated dirty `H8Memory.cs` worktree changes.

Cinematic Cheats used:
- Cached cube/checkerboard impostor for unresolved streamed assets. This is a presentation fake, not a physics or disk wait.
- Bundle-prefix TTL inflation avoids unload/reload thrash without simulating actual bundle residency economics.
- Tombstone compaction is a fixed-buffer rebuild from active tracker slots, not a resizing allocator.

Exact Microseconds saved:
- Not measured. Static estimates remain bounded: managed dictionary rehash jitter avoided in streaming storms, TTL bookkeeping moved to Burst, tombstone probe debt repaired on pressure paths. Runtime profiler proof is blocked by external compile owners.

<SELF_AUDIT agent_id="SHINOBU_101" timestamp="2026-05-19T00:00:00+04:00" status="PENDING_VERIFICATION_EXTERNAL_COMPILE_BLOCK_R6">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Managed Addressables hot-path `Dictionary/List/Queue` tracking was replaced by fixed handle arrays plus Vault `AddressableHeapHandleMap`.</TASK>
    <TASK id="02" status="PASS">Synchronous release drain was removed from gameplay flow; release intent is staged through native/pending flags and the fixed release ring.</TASK>
    <TASK id="03" status="PASS">Hot DTOs use public fields; zero-copy mutation routes through `GetEntryAsRef` and unsafe ref access over Vault buffers.</TASK>
    <TASK id="04" status="PASS">`AssetHandleMapEntryDTO` is explicit 64B with required offsets and manual padding.</TASK>
    <TASK id="05" status="PASS">Emergency deterministic `AssetCacheProfileDTO` records are generated into Vault when the binary profile payload is absent.</TASK>
    <TASK id="06" status="PASS">Fixed 16384-slot open-address table uses bounded probing; pressure triggers eviction or in-place tombstone compaction, never resize.</TASK>
    <TASK id="07" status="PASS">Burst TTL job uses required compile flags, `[NoAlias]`, direct 64B map-entry iteration, and no parallel byte-flag writes.</TASK>
    <TASK id="08" status="PASS">`TryExecuteOrDeferBlindFrameRelease` is the only direct `Addressables.Release` route; tracked, detached, hard-reaper, and no-owner fault paths preserve ownership until execution/defer is proven.</TASK>
    <TASK id="09" status="PASS">Cached impostor mesh/material returns immediately while real Addressables load resolves asynchronously.</TASK>
    <TASK id="10" status="PASS">VRAM panic marks the furthest 10% unreferenced/unpinned handles with atomic zero-ref verification and may bypass the blind-frame gate to prevent OOM.</TASK>
    <TASK id="11" status="PASS">TTL uses `BaseTTL * math.lerp(0.1f, 3.0f, math.smoothstep(0.2f, 0.8f, GlobalQualityWeight))`.</TASK>
    <TASK id="12" status="PASS">Reference count mutation uses `Interlocked`; panic marking verifies zero ownership before release intent.</TASK>
    <TASK id="13" status="PASS">Eviction scoring subtracts player/chunk `double3` AUP before casting the local delta to `float3`.</TASK>
    <TASK id="14" status="PASS">`BundlePrefixHash` is stored in the native map; shared unreferenced bundle groups receive 50% TTL inflation and mirror flag consistency across map/byte/DTO owners.</TASK>
    <TASK id="15" status="PASS">`SetHeapSanitizerPin(uint,bool)` sets the pinned bit; TTL and panic eviction skip pinned handles.</TASK>
    <TASK id="16" status="PASS">Vault buffers use `NativeArrayOptions.UninitializedMemory`; cold Burst clear initializes essential DTO/map fields and byte mirrors clear sequentially.</TASK>
    <TASK id="17" status="PASS">300-entry 64B telemetry ring records heap state and dumps raw blackbox bytes to `Dump_MEMORY_SURGEON.bin` plus `Dump_SHINOBU_101_Addressables.bin`.</TASK>
    <TASK id="18" status="PASS">UI Toolkit tuner uses fixed graph elements and change-gated text updates instead of IMGUI churn.</TASK>
    <TASK id="19" status="PASS">CSV override ingest reads into Vault `AddressableHeapCsvScratch` and parses `ReadOnlySpan<byte>` without `string.Split`, `Regex`, or managed profile collections.</TASK>
    <TASK id="20" status="PASS">Leak detector scans the native map and exposes refcount anomalies with asset and bundle hashes in the editor tuner.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <STRUCT name="AssetHandleMapEntryDTO" size="64" alignment="8-byte fields at 8-byte offsets; one L1 cache line">
      <FIELD name="AssetHash" offset="0" size="8" />
      <FIELD name="BundlePrefixHash" offset="8" size="8" />
      <FIELD name="PoolSlotIndex" offset="16" size="4" />
      <FIELD name="RefCount" offset="20" size="4" />
      <FIELD name="TimeToLive" offset="24" size="4" />
      <FIELD name="Flags" offset="28" size="4" />
      <FIELD name="Generation" offset="32" size="4" />
      <FIELD name="_pad0.._pad6" offset="36..60" size="28" />
      <PROOF>8 + 8 + 4 + 4 + 4 + 4 + 4 + 28 = 64 bytes.</PROOF>
    </STRUCT>
    <STRUCT name="AssetTrackerDTO" size="64" alignment="8-byte handle and 24-byte AUP start at 8/16">
      <FIELD name="AssetHash" offset="0" size="4" />
      <FIELD name="ReferenceCount" offset="4" size="4" />
      <FIELD name="HandlePointer" offset="8" size="8" />
      <FIELD name="AssetAup" offset="16" size="24" />
      <FIELD name="MaxResidencyRadiusSq" offset="40" size="4" />
      <FIELD name="Flags" offset="44" size="4" />
      <FIELD name="_pad0.._pad1" offset="48..63" size="16" />
      <PROOF>4 + 4 + 8 + 24 + 4 + 4 + 16 = 64 bytes.</PROOF>
    </STRUCT>
    <STRUCT name="AssetHeapTelemetryEntry" size="64" purpose="300-frame fixed blackbox ring; no atomic counter lane" />
    <STRUCT name="AssetCacheProfileDTO" size="16" purpose="cache profile record; 4 x 4-byte fields" />
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Below `GlobalQualityWeight` 0.3, `smoothstep(0.2,0.8,q)` approaches zero and effective TTL collapses toward 10% of designer base TTL. Distant unreferenced assets beyond residency radius decay 5x faster, while VRAM panic applies a 3x pressure multiplier and can force the blind-frame bypass. At quality 1.0, TTL expands to 300%, preserving more visual continuity and reducing backtrack swaps. No binary hardware switch is used.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    <PRIVATE_NATIVE_FIELDS status="PASS">`AssetLifecycleGovernor` declares no private persistent `NativeArray`, `NativeList`, or `NativeHashMap` ownership. Fixed managed arrays exist only for non-blittable Unity handles and cold/editor metadata that cannot be stored in Burst DTOs.</PRIVATE_NATIVE_FIELDS>
    <BUFFER id="AddressableHeapCacheProfiles" numeric="70323" />
    <BUFFER id="AddressableHeapTelemetry" numeric="70324" />
    <BUFFER id="AddressableHeapTrackers" numeric="70325" />
    <BUFFER id="AddressableHeapTimeToLive" numeric="70326" />
    <BUFFER id="AddressableHeapTrackerFlags" numeric="70327" note="main-thread/editor mirror; not written by parallel jobs" />
    <BUFFER id="AddressableHeapHandleMap" numeric="70328" />
    <BUFFER id="AddressableHeapCsvScratch" numeric="70329" note="existing Core Memory authority verified" />
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <JOB name="AssetTtlEvaluationJob" attributes="[BurstCompile(CompileSynchronously=true, FloatMode=Fast, FloatPrecision=Standard)] [NoAlias]" consumes="Trackers, TimeToLiveSeconds, HandleMap" outputs="Trackers.Flags, TimeToLiveSeconds, HandleMap.RefCount/TimeToLive/Flags" iteration="direct map-entry pass, validates PoolSlotIndex and AssetHash before tracker mutation" />
    <JOB name="HeapSanitizerMemClearJob" attributes="[BurstCompile(CompileSynchronously=true, FloatMode=Fast, FloatPrecision=Standard)] [NoAlias]" consumes="Trackers, TimeToLiveSeconds, HandleMap" outputs="zeroed tracker/map/ttl essential fields" phase="cold boot/teardown only" />
    <HANDLE name="_ttlEvaluationHandle" registration="H8Memory.RegisterActiveJob(SystemID.WorldStreaming)" />
    <COMPLETE_POLICY>Runtime mutation first calls `TryPrepareTrackerMutation`; it only joins the TTL job after `IsCompleted` or during cold teardown/boot clear, then mirrors DTO flags back to the byte facade.</COMPLETE_POLICY>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    <ROUTING status="PASS">Runtime Optimization files reference Core/Core.Contracts/Core.Memory plus Unity namespaces; direct `Hecton8.World` and `Hecton8.SaveSystem` usings were removed from the governor.</ROUTING>
    <ASMDEF status="PASS">No Optimization runtime asmdef creates a sibling-domain dependency; editor asmdef remains editor-only and references `Hecton8.Core`.</ASMDEF>
    <BUILD status="BLOCKED_BY_EXTERNAL_OWNERS">`dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -v:minimal` attempts 2-5 report no SHINOBU/Optimization errors. Remaining failures are Visor reconstruction DTO/ID gaps, Somatic comfort DTO gaps, Construction `HeadlessDroneTask` gaps, and a duplicate SaveState project-file warning.</BUILD>
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    <FAKE>Requested-but-unloaded assets receive an O(1) cached cube/checkerboard impostor while Addressables resolves asynchronously.</FAKE>
    <BEFORE complexity="unbounded visible wait / blocking load risk">Simulation or presentation could stall waiting for disk/driver state.</BEFORE>
    <AFTER complexity="O(1) placeholder return + async swap">CPU does no heavy simulation and does not block the gameplay path.</AFTER>
  </DEAR_LIE_CONFIRMATION>
  <RELEASE_OWNERSHIP_PROOF>
    <TRACKED_HANDLE status="PASS">Native slot and managed record are cleared only after immediate release or detached bridge acceptance is proven; otherwise the fixed pending ring re-owns the release key.</TRACKED_HANDLE>
    <DETACHED_HANDLE status="PASS">Raw non-registered handles dedupe inside the fixed 64-slot detached bridge and drain only through blind/panic release gates.</DETACHED_HANDLE>
    <HARD_REAPER status="PASS">`CleanBundleCache` handle remains owned and retried when release cannot execute/defer.</HARD_REAPER>
    <NO_OWNER_FAULT status="PASS">Registration-failure handles use normal defer first; only no-owner bridge overflow triggers short panic-scoped forced release.</NO_OWNER_FAULT>
  </RELEASE_OWNERSHIP_PROOF>
</SELF_AUDIT>

## 2026-05-19 Polish Pass: Bundle Shared Mirror Consistency

What was wrong:
- Tombstone compaction rebuilt map entries with `BundleShared`, but the current slot's byte mirror and 64-byte tracker DTO flag could remain stale until a later mirror pass.
- Existing entries marked by `MarkBundlePrefixShared` updated the byte mirror but not the DTO flag lane used by Burst after mirroring.

What was done:
- `MarkBundlePrefixShared` now receives the tracker DTO buffer and synchronizes `AssetHandleMapEntryDTO.Flags`, `AddressableHeapTrackerFlags`, and low-byte `AssetTrackerDTO.Flags`.
- Registration, bundle recompute, and compaction all route through the synchronized helper.
- Compaction preserves the current per-slot TTL when rebuilding each map entry.

Cinematic Cheats used:
- None. This is state consistency in the native heap.

Exact Microseconds saved:
- Not measured. Static effect is preventing shared-bundle TTL loss and reload churn after tombstone cleanup. Compile attempt 4 reports no SHINOBU/Optimization errors.

## 2026-05-19 Polish Pass: Blackbox Dump Identity

What was wrong:
- Addressables heap telemetry still mirrored the agent dump to a stale prior-agent filename.

What was done:
- Retargeted the agent-owned heap dump to `Dump_SHINOBU_101_Addressables.bin`.
- Preserved the shared memory-infrastructure mirror `Dump_MEMORY_SURGEON.bin`.

Cinematic Cheats used:
- None. This is forensic ownership hygiene.

Exact Microseconds saved:
- None. Fault-path file identity only.

## 2026-05-19 Compile Attempt 4

What was wrong:
- The compaction, bundle-shared mirror, and dump identity corrections needed compiler validation.

What was done:
- Waited for the build gate: CPU below 50 and no `dotnet/csc` process.
- Ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -v:minimal`.
- Result: no SHINOBU/Optimization errors.
- Remaining errors are external `Visor/HectonVisorUberPostFeature.cs` missing reconstruction DTOs/IDs and `Editor/SomaticTunerWindow.cs` missing comfort DTOs. Existing duplicate `SaveStateMerkleTree.cs` project warning remains outside this lane.

Cinematic Cheats used:
- None. Compile boundary only.

Exact Microseconds saved:
- None. Verification evidence only; runtime profiler proof remains blocked by external compile owners.

## 2026-05-19 Polish Pass: Release Ownership Preflight

What was wrong:
- `ExecuteReleaseFlow` ignored a failed `TryExecuteOrDeferBlindFrameRelease(record.AddressableHandle)` return.
- If the frame was visible and the 64-slot detached bridge was full, the native slot and managed record could be cleared while no `Addressables.Release` had executed.

What was done:
- Added bounded `CanAcceptAddressableRelease()` preflight before clearing native ownership.
- The flow now preserves the `AssetRecord` and requeues the key if the detached bridge cannot accept the handle.
- `_orphanedHandlesReleased` increments only for immediate blind/panic-frame releases, not for staged handles.

Cinematic Cheats used:
- None. This is lifetime ownership hardening.

Exact Microseconds saved:
- None claimed. The fixed 64-slot scan is cold release-path work that prevents lost handle ownership under pressure.

## 2026-05-19 Polish Pass: Hard-Reaper Handle Retry

What was wrong:
- `Addressables.CleanBundleCache()` hard-reaper handle release also ignored a failed defer path.
- Preserving the handle on failure required a retry pump; otherwise the active hard-reaper window could stall permanently.

What was done:
- Completed cache-clean handles now pass through `CanAcceptAddressableRelease()` before release/defer.
- If the bridge cannot accept the handle, the handle remains live and `_hardReaperBundleCacheCleanComplete` stays false.
- Active hard-reaper windows now retry cache-handle release each slow tick.
- Releasing a completed cache-clean handle now returns immediately instead of starting a second clean operation.

Cinematic Cheats used:
- Scanner interference/static glitch remains the visual mask for the hard-reaper window; no simulation was added.

Exact Microseconds saved:
- None claimed. This is cold cleanup ownership hardening with a bounded 64-slot scan.

## 2026-05-19 Polish Pass: No-Owner Registration Failure Release

What was wrong:
- Failed Addressables registration could leave a valid local load handle with no Vault slot and no managed record owner.
- If the detached bridge was full, the normal defer helper returned false and the local handle was dropped.

What was done:
- Added `TryExecuteOrForceAddressableReleaseFault`.
- The helper uses the normal blind/defer path first.
- Only when there is no durable owner and the bridge refuses the handle, it raises a short VRAM-panic window and releases through the same gate.
- Normal tracked records still preserve ownership and requeue instead of forcing visible release.

Cinematic Cheats used:
- None. This is leak prevention for a fault path.

Exact Microseconds saved:
- None claimed. Fault path only; bounded bridge scan plus forced panic release prevents a Unity Addressables leak.

## 2026-05-19 Compile Attempt 5

What was wrong:
- Release-ownership helper changes needed compiler validation.

What was done:
- Ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -v:minimal` after CPU/dotnet gate opened.
- Result: no SHINOBU/Optimization errors.
- Remaining errors are external Visor reconstruction DTOs/IDs, Somatic comfort DTOs, and Construction `HeadlessDroneTask` references. Existing duplicate `SaveStateMerkleTree.cs` warning remains outside this lane.

Cinematic Cheats used:
- None. Compile boundary only.

Exact Microseconds saved:
- None. Verification evidence only; runtime profiler proof remains blocked by external compile owners.

## 2026-05-19 Core Buffer ID Boundary

What was wrong:
- `asset_cache_profiles.csv` required a Vault-owned scratch buffer, and the lane needed proof it was not using a private parser buffer or a foreign BufferID.

What was done:
- Verified existing `AddressableHeapCsvScratch = 70329` authority and routed the governor's zero-GC byte parser through that Vault buffer.
- Did not revert other pre-existing dirty `H8Memory.cs` changes from neighboring lanes.

Cinematic Cheats used:
- None. This is Vault ownership plumbing.

Exact Microseconds saved:
- None measured. Prevents a private parser buffer and keeps CSV ingestion cold/Vault-owned.

<SELF_AUDIT agent_id="SHINOBU_101" revision="R5" timestamp="2026-05-19T00:00:00+04:00" status="PENDING_VERIFICATION_EXTERNAL_COMPILE_BLOCK">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Managed Addressables handle dictionaries/queues/lists were replaced by fixed arrays plus Vault `AddressableHeapHandleMap`.</TASK>
    <TASK id="02" status="PASS">Actual `Addressables.Release` is centralized in `TryExecuteOrDeferBlindFrameRelease`; release intent is staged through native bits/fixed queues.</TASK>
    <TASK id="03" status="PASS">Hot DTO fields are public fields; map entries mutate through `UnsafeUtility.AsRef` via `GetEntryAsRef`.</TASK>
    <TASK id="04" status="PASS">`AssetHandleMapEntryDTO` is explicit 64B with offsets 0/8/16/20/24/28/32 and 28B final padding.</TASK>
    <TASK id="05" status="PASS">Emergency deterministic 16B cache profile DTOs populate the Vault profile buffer when payload data is absent.</TASK>
    <TASK id="06" status="PASS">Open-address table is fixed 16384 slots; pressure triggers eviction/compaction, never resize.</TASK>
    <TASK id="07" status="PASS">TTL evaluation is a Burst `IJobParallelFor` over map entries with required compile flags and `[NoAlias]` fields.</TASK>
    <TASK id="08" status="PASS">Safe-frame gate is enforced; no-owner registration failure uses panic release only after normal defer ownership is impossible.</TASK>
    <TASK id="09" status="PASS">Dear Lie impostor cube/material avoids blocking on unresolved async Addressables loads.</TASK>
    <TASK id="10" status="PASS">VRAM panic marks furthest 10% of unreferenced/unpinned handles with atomic zero-ref validation.</TASK>
    <TASK id="11" status="PASS">TTL curve is `BaseTTL * lerp(0.1, 3.0, smoothstep(0.2, 0.8, GlobalQualityWeight))`.</TASK>
    <TASK id="12" status="PASS">Native tracker refcount uses `Interlocked` increment/decrement/zero check.</TASK>
    <TASK id="13" status="PASS">Eviction distance subtracts player/asset `double3` AUP before `float3` distance math.</TASK>
    <TASK id="14" status="PASS">Bundle prefix hash drives shared-bundle TTL inflation and in-place tombstone compaction preserves TTL/shared flags.</TASK>
    <TASK id="15" status="PASS">`SetHeapSanitizerPin(uint,bool)` sets pinned state skipped by TTL and panic eviction.</TASK>
    <TASK id="16" status="PASS">Vault buffers use `UninitializedMemory`; cold Burst clear sanitizes DTO/map/TTL, byte mirror clears sequentially.</TASK>
    <TASK id="17" status="PASS">300-entry 64B telemetry ring dumps to `Dump_MEMORY_SURGEON.bin` and `Dump_SHINOBU_101_Addressables.bin`.</TASK>
    <TASK id="18" status="PASS">UI Toolkit tuner replaced IMGUI; graph elements are fixed and labels are change-gated.</TASK>
    <TASK id="19" status="PASS">CSV parser reads `FileStream.Read(Span<byte>)` into Vault scratch and parses `ReadOnlySpan<byte>`.</TASK>
    <TASK id="20" status="PASS">Leak banner scans native map for high refcounts and reports asset/bundle/ref proof.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <STRUCT name="AssetHandleMapEntryDTO" size="64">
      <FIELD name="AssetHash" offset="0" size="8" />
      <FIELD name="BundlePrefixHash" offset="8" size="8" />
      <FIELD name="PoolSlotIndex" offset="16" size="4" />
      <FIELD name="RefCount" offset="20" size="4" />
      <FIELD name="TimeToLive" offset="24" size="4" />
      <FIELD name="Flags" offset="28" size="4" />
      <FIELD name="Generation" offset="32" size="4" />
      <FIELD name="_pad0.._pad6" offset="36..60" size="28" />
      <PROOF>8+8+4+4+4+4+4+28 = 64 bytes, one cache line, 16-byte multiple.</PROOF>
    </STRUCT>
    <STRUCT name="AssetTrackerDTO" size="64">
      <FIELD name="AssetHash" offset="0" size="4" />
      <FIELD name="ReferenceCount" offset="4" size="4" />
      <FIELD name="HandlePointer" offset="8" size="8" />
      <FIELD name="AssetAup" offset="16" size="24" />
      <FIELD name="MaxResidencyRadiusSq" offset="40" size="4" />
      <FIELD name="Flags" offset="44" size="4" />
      <FIELD name="_pad0.._pad1" offset="48..63" size="16" />
      <PROOF>4+4+8+24+4+4+16 = 64 bytes.</PROOF>
    </STRUCT>
    <STRUCT name="AssetCacheProfileDTO" size="16" />
    <STRUCT name="AssetHeapTelemetryEntry" size="64" />
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    At quality below 0.3, the smoothstep term collapses TTL toward 10%, distant assets decay 5x faster, and panic pressure applies a 3x decay multiplier. At quality 1.0, TTL extends to 300%, spending saved CPU/memory stability on smoother backtracking and fewer visible swaps.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    <PRIVATE_NATIVE_FIELDS status="PASS">No private persistent NativeArray/NativeList/NativeHashMap fields in `AssetLifecycleGovernor`; fixed managed arrays remain only for non-blittable Unity handles and fixed cold metadata.</PRIVATE_NATIVE_FIELDS>
    <BUFFER id="AddressableHeapCacheProfiles" numeric="70323" />
    <BUFFER id="AddressableHeapTelemetry" numeric="70324" />
    <BUFFER id="AddressableHeapTrackers" numeric="70325" />
    <BUFFER id="AddressableHeapTimeToLive" numeric="70326" />
    <BUFFER id="AddressableHeapTrackerFlags" numeric="70327" note="main-thread/editor mirror only" />
    <BUFFER id="AddressableHeapHandleMap" numeric="70328" />
    <BUFFER id="AddressableHeapCsvScratch" numeric="70329" />
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <JOB name="AssetTtlEvaluationJob" consumes="Trackers, TimeToLiveSeconds, HandleMap" outputs="same" attributes="[NoAlias]" />
    <JOB name="HeapSanitizerMemClearJob" consumes="Trackers, TimeToLiveSeconds, HandleMap" outputs="same" attributes="[NoAlias]" />
    <DEPENDENCY output="_ttlEvaluationHandle registered via H8Memory.RegisterActiveJob(SystemID.WorldStreaming)" />
    <COMPLETE_POLICY>Cold boot/teardown or after `IsCompleted`; no arbitrary visible-frame complete in TTL path.</COMPLETE_POLICY>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    <USINGS status="PASS">Optimization runtime uses Core/Core.Contracts/Core.Memory plus Unity namespaces; no direct World/SaveSystem import remains.</USINGS>
    <ASMDEF status="PASS">No Optimization runtime asmdef exists; editor asmdef references only `Hecton8.Core`.</ASMDEF>
    <BUILD status="BLOCKED_BY_DEPENDENCY">Compile attempts 2-5 report no SHINOBU/Optimization errors; external Visor reconstruction, Somatic comfort, Construction HeadlessDroneTask, and duplicate SaveStateMerkleTree project hygiene remain outside this lane.</BUILD>
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    <BEFORE>Blocking for real streamed assets creates unbounded disk/driver wait.</BEFORE>
    <AFTER>O(1) cached cube impostor/material returns immediately while async Addressables load resolves.</AFTER>
    <COMPLEXITY before="unbounded blocking wait" after="O(1) placeholder return plus async completion" />
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Polish Pass: Gate Correction

What was wrong:
- The unused `MockChunkLoadSpamJob` used an unsafe pointer and modulo slot selection, allowing multiple parallel indices to write the same tracker slot.
- Raw `AsyncOperationHandle` overloads of `TryExecuteOrDeferBlindFrameRelease` directly released handles without checking blind-frame or VRAM-panic state.

What was done:
- Deleted the unused mock spam job and signal DTO.
- Added a fixed 64-slot detached release handle bridge for raw handles that fail registration or arrive from Addressables cache cleanup.
- Kept the only direct `Addressables.Release` call inside the gated helper body.

Cinematic Cheats used:
- No new simulation. The previous Dear Lie impostor path remains the cached cube/checkerboard fallback while real Addressables loads resolve asynchronously.

Exact Microseconds saved:
- No measured claim. Static correction prevents visible-frame release stalls and removes an unsafe parallel mock path; profiler proof remains absent.

Verification:
- Static scan now reports no `NativeDisableUnsafePtrRestriction`, no mock spam job, no managed `Dictionary/List/Queue`, and only one `Addressables.Release` line inside `TryExecuteOrDeferBlindFrameRelease`.
- Compile remains blocked: CPU reports 100% by CIM and perf counter; no `dotnet` or `csc` process was visible, but AGENTS.md forbids build launch above 50%.

## 2026-05-19 Polish Pass: Compile-Wall AUP Boundary

What was wrong:
- `AssetLifecycleGovernor` imported `Hecton8.World` for floating-origin fallback math.

What was done:
- Removed the direct world namespace import from the governor.
- Reconstructed player fallback AUP from `PlayerRuntimePoseSnapshot.Aup` plus `HectonPhysicsContract.AupSectorSizeMetersDouble`.
- Kept exact chunk AUP stamping in `WorldChunkResidencyManager`, the domain owner for chunk coordinates.

Verification:
- Static scan reports no `using Hecton8.World` and no `HectonFloatingOrigin` usage in the SHINOBU_101 Optimization files.
- Current compile boundary is external Visor/Somatic DTO ownership; this AUP pass itself has no SHINOBU/Optimization compile errors in later attempts.

## 2026-05-19 Polish Pass: TTL False-Sharing Boundary

What was wrong:
- `AssetTtlEvaluationJob` used `NativeArray<byte> Flags` and wrote `Flags[index]` from parallel workers. This is a cache-line false-sharing risk because adjacent handles share one 64-byte L1 line.

What was done:
- Removed `NativeArray<byte> Flags` from `AssetTtlEvaluationJob`.
- Added a deterministic mirror from byte flags into `AssetTrackerDTO.Flags` before scheduling.
- The byte-to-DTO mirror preserves future high `uint` flag bits and only replaces the low 8 runtime handle bits.
- TTL Burst now flips `PendingTtl` and `Releasable` inside `AssetTrackerDTO`, whose explicit layout is 64 bytes.
- Added guarded mirror-back after completion. `_ttlEvaluationFlagsMirrored` prevents an early-completed job from overwriting fresh main-thread flag mutations.
- Removed byte flag writes from `HeapSanitizerMemClearJob`; the cold byte mirror is cleared sequentially after the 64-byte DTO/map clear.

Cinematic Cheats used:
- No new simulation. Streaming visibility still relies on the cached impostor cube and deferred release blind-frame fake.

Exact Microseconds saved:
- Not measured. Static expectation: removes cache-line invalidation during the 1 Hz TTL pass on clustered handles; later compile attempts report no SHINOBU/Optimization errors, while profiler/runtime proof remains blocked by external compile owners.

## 2026-05-19 Polish Pass: Editor Facade Text Churn

What was wrong:
- `HeapSanitizerTunerWindow` used fixed UI Toolkit graph elements, but metric labels and tracker rows formatted strings on every 250 ms refresh even when values were unchanged.

What was done:
- Added fixed numeric caches for metrics, leak banner identity, row hashes/refcounts/slots/TTL/flags, and visible row count.
- Labels now update text only when the underlying value changes.
- Graphs remain fixed preallocated `VisualElement` arrays.

Cinematic Cheats used:
- None. This is editor-facade churn control, not simulation.

Exact Microseconds saved:
- Not measured. Runtime saving is zero by design; editor GC churn is reduced from every refresh to value-change events.

## 2026-05-19 Compile Attempt 1

What was wrong:
- Build was launched only after CPU gate opened at 27% and no `dotnet/csc` process was visible.
- `AssetRecord.cs` failed because `double3`, `float3`, and `math` require `using Unity.Mathematics;`.
- The same build failed in unrelated `Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs` due missing `UberNoirReconstructionConstantsDTO`, `MockReconstructionInputSignal`, `ReconstructionTelemetryEntry`, and `UberNoirReconstructionVaultIds`.

What was done:
- Added `using Unity.Mathematics;` to `AssetRecord.cs`.
- Removed unused `using Hecton8.SaveSystem;` from `AssetLifecycleGovernor`.

Cinematic Cheats used:
- None. Compile hygiene only.

Exact Microseconds saved:
- None claimed. Compile retry is pending because CPU returned to 100% after the first attempt.

## 2026-05-19 Compile Attempt 2

What was wrong:
- After the SHINOBU import fix, `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -v:minimal` still fails.
- The second build output contains no SHINOBU/Optimization errors.
- Remaining compile errors are external: missing reconstruction DTOs/IDs in `Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs` and missing comfort DTOs in `Assets/_Project/Scripts/Editor/SomaticTunerWindow.cs`.

What was done:
- Stopped at the domain boundary. No Visor or Somatic files were edited.
- Marked compile verification as dependency-blocked in `Docs/Tasks/Status_SHINOBU_101.md`.

Cinematic Cheats used:
- None. Compile boundary only.

Exact Microseconds saved:
- None claimed. SHINOBU static validation remains clean; full build requires external DTO owners to restore their contracts.

## 2026-05-19 Polish Pass: TTL Map-Entry Iteration

What was wrong:
- `AssetTtlEvaluationJob` iterated tracker slots and then probed `HandleMap` to mirror TTL/refcount back to the map. This creates avoidable probe work inside the Burst kernel.

What was done:
- Reoriented `AssetTtlEvaluationJob` to iterate `AddressableHeapHandleMap` entries directly.
- Each occupied 64-byte map entry resolves its `PoolSlotIndex` and updates the tracker, TTL lane, and map entry without an inner probe.
- Added an asset-hash match guard before mutating the tracker slot, so stale map entries cannot update the wrong slot.
- Scheduled the TTL job over `handleMap.Length` instead of `trackers.Length`.

Cinematic Cheats used:
- None. This is data-structure path correction.

Exact Microseconds saved:
- Not measured. Static worst-case work changes from tracker-count multiplied by probe walk to one fixed map pass; external Visor/Somatic compile errors still block runtime profiler proof.

## 2026-05-19 Polish Pass: Release Queue Idempotence

What was wrong:
- The pending release ring and detached raw Addressables handle bridge were bounded but accepted duplicates.
- Repeated blocked drains or repeated raw-handle callbacks could waste fixed slots and increase duplicate-release risk.

What was done:
- Added `FixedUIntQueue.Contains(uint)` and use it before pending-release enqueue.
- Added detached-handle equality check before storing a raw `AsyncOperationHandle`.
- No managed `HashSet`, `Dictionary`, or resize path was introduced.

Cinematic Cheats used:
- None. This is lifetime ownership hardening.

Exact Microseconds saved:
- Not measured. The tradeoff is bounded cold-path scan to prevent duplicate release pressure and ring overflow churn.

## 2026-05-19 Polish Pass: Pending Release Ownership

What was wrong:
- Some paths set `PendingRelease = true` before proving the fixed pending-release ring owned the key.
- A release popped from the queue could fail native slot clear while the TTL job was scheduled and then remain pending without being re-enqueued.

What was done:
- `PendingRelease` now becomes true only when `EnqueuePendingRelease(key)` succeeds.
- Queue-drained releases that hit a scheduled TTL job re-enqueue the key before preserving the pending state.
- Failed enqueue leaves the record non-pending and triggers the existing telemetry dump path.

Cinematic Cheats used:
- None. This is lifetime ownership correction.

Exact Microseconds saved:
- None claimed. The fix prevents silent asset-release ownership loss under queue pressure.

## 2026-05-19 Compile Attempt 3

What was wrong:
- New SHINOBU changes required another compile probe.

What was done:
- Ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -v:minimal` after CPU gate opened and no `dotnet/csc` process was present.
- Build output still contains no SHINOBU/Optimization errors.
- Remaining failures are external `Visor/HectonVisorUberPostFeature.cs` reconstruction DTOs/IDs and `Editor/SomaticTunerWindow.cs` comfort DTOs.

Cinematic Cheats used:
- None. Compile boundary only.

Exact Microseconds saved:
- None claimed. Full compile remains dependency-blocked outside SHINOBU_101 ownership.

## 2026-05-19 Polish Pass: Tombstone Defragmentation

What was wrong:
- Map pressure counted tombstones, but the pressure handler only evicted. A tombstone-heavy `AddressableHeapHandleMap` could stay above 85% pressure and keep degrading probe length.

What was done:
- Added a no-allocation in-place map compaction pass.
- When used+tombstone pressure is high but live occupancy is below threshold, the map is cleared and rebuilt from active tracker slots and existing handle-pool bundle prefix hashes.
- Shared-bundle flags are cleared during rebuild and re-applied only when the rebuilt map proves multiple active entries share a prefix.
- If live occupancy is also above threshold, the existing furthest-unused eviction pass runs, then compaction removes tombstones.

Cinematic Cheats used:
- None. This is heap defragmentation, not visual simulation.

Exact Microseconds saved:
- Not measured. Static effect is removal of accumulated open-address tombstone probe debt after streaming churn.

<SELF_AUDIT agent_id="SHINOBU_101" timestamp="2026-05-19T00:00:00+04:00" status="PENDING_VERIFICATION_EXTERNAL_COMPILE_BLOCK">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Managed Addressables hot-path `Dictionary/List/Queue` tracking replaced by fixed arrays plus Vault open-address map.</TASK>
    <TASK id="02" status="PASS">Release drain routes through `TryExecuteOrDeferBlindFrameRelease`; direct `Addressables.Release` is single gated helper line.</TASK>
    <TASK id="03" status="PASS">Hot DTO fields are public fields; map mutation uses `GetEntryAsRef`.</TASK>
    <TASK id="04" status="PASS">`AssetHandleMapEntryDTO` is explicit 64B with required offsets and padding.</TASK>
    <TASK id="05" status="PASS">Emergency deterministic cache profile DTOs generated into Vault.</TASK>
    <TASK id="06" status="PASS">Fixed 16384-slot open-address table; pressure triggers eviction, not resize.</TASK>
    <TASK id="07" status="PASS">Burst TTL job uses required Burst flags and `[NoAlias]`; byte flag writes removed from parallel path.</TASK>
    <TASK id="08" status="PASS">Safe frame release gate is the only release route; detached raw handles queue until blind/panic frame.</TASK>
    <TASK id="09" status="PASS">Cached cube/checkerboard impostor returns while Addressables load async.</TASK>
    <TASK id="10" status="PASS">VRAM panic marks furthest 10% unreferenced/unpinned handles with atomic zero-ref verification.</TASK>
    <TASK id="11" status="PASS">TTL uses `BaseTTL * lerp(0.1, 3.0, smoothstep(0.2, 0.8, GlobalQualityWeight))`.</TASK>
    <TASK id="12" status="PASS">Native ref counts use `Interlocked` and panic path verifies zero before marking.</TASK>
    <TASK id="13" status="PASS">AUP eviction subtracts player/chunk `double3` before casting to `float3`.</TASK>
    <TASK id="14" status="PASS">Bundle prefix hash inflates shared-bundle TTL by 50%.</TASK>
    <TASK id="15" status="PASS">`SetHeapSanitizerPin(uint,bool)` sets pinned bit; TTL and panic skip pinned assets.</TASK>
    <TASK id="16" status="PASS">Vault buffers use `UninitializedMemory`; sanitizer clear job handles 64B DTO/map/TTL, byte mirror clears cold sequentially.</TASK>
    <TASK id="17" status="PASS">300-entry 64B telemetry ring and raw dump path implemented.</TASK>
    <TASK id="18" status="PASS">UI Toolkit tuner uses fixed graphs and change-gated label text.</TASK>
    <TASK id="19" status="PASS">CSV parser reads via `FileStream.Read(Span<byte>)` into Vault scratch and parses `ReadOnlySpan<byte>`.</TASK>
    <TASK id="20" status="PASS">Leak banner scans native map for refcount anomalies and exposes hash/bundle proof.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <STRUCT name="AssetHandleMapEntryDTO" size="64">
      <FIELD name="AssetHash" offset="0" size="8" />
      <FIELD name="BundlePrefixHash" offset="8" size="8" />
      <FIELD name="PoolSlotIndex" offset="16" size="4" />
      <FIELD name="RefCount" offset="20" size="4" />
      <FIELD name="TimeToLive" offset="24" size="4" />
      <FIELD name="Flags" offset="28" size="4" />
      <FIELD name="Generation" offset="32" size="4" />
      <FIELD name="_pad0.._pad6" offset="36..60" size="28" />
      <PROOF>8+8+4+4+4+4+4+28 = 64 bytes.</PROOF>
    </STRUCT>
    <STRUCT name="AssetTrackerDTO" size="64">
      <FIELD name="AssetHash" offset="0" size="4" />
      <FIELD name="ReferenceCount" offset="4" size="4" />
      <FIELD name="HandlePointer" offset="8" size="8" />
      <FIELD name="AssetAup" offset="16" size="24" />
      <FIELD name="MaxResidencyRadiusSq" offset="40" size="4" />
      <FIELD name="Flags" offset="44" size="4" />
      <FIELD name="_pad0.._pad1" offset="48..63" size="16" />
      <PROOF>4+4+8+24+4+4+16 = 64 bytes.</PROOF>
    </STRUCT>
    <STRUCT name="AssetHeapTelemetryEntry" size="64" purpose="300-frame blackbox ring" />
    <STRUCT name="AssetCacheProfileDTO" size="16" purpose="cache profile Vault record" />
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Below quality 0.3, the smoothstep term approaches zero and TTL collapses toward 10%, so unused assets enter `PendingTtl` quickly and distant assets decay 5x faster. At quality 1.0 TTL expands to 300%, preserving backtracking visual continuity. VRAM panic multiplies decay by 3 and can bypass the blind-frame gate to prevent OOM.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    <PRIVATE_NATIVE_FIELDS status="PASS">No private persistent NativeArray/NativeList/NativeHashMap fields in `AssetLifecycleGovernor`.</PRIVATE_NATIVE_FIELDS>
    <BUFFER id="AddressableHeapCacheProfiles" numeric="70323" />
    <BUFFER id="AddressableHeapTelemetry" numeric="70324" />
    <BUFFER id="AddressableHeapTrackers" numeric="70325" />
    <BUFFER id="AddressableHeapTimeToLive" numeric="70326" />
    <BUFFER id="AddressableHeapTrackerFlags" numeric="70327" note="main-thread/editor byte mirror, not parallel job output" />
    <BUFFER id="AddressableHeapHandleMap" numeric="70328" />
    <BUFFER id="AddressableHeapCsvScratch" numeric="70329" />
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <JOB name="AssetTtlEvaluationJob" consumes="AddressableHeapTrackers, AddressableHeapTimeToLive, AddressableHeapHandleMap" outputs="same buffers" attributes="[NoAlias]" iteration="direct 64B HandleMap entry pass; no inner map probe" />
    <JOB name="HeapSanitizerMemClearJob" consumes="AddressableHeapTrackers, AddressableHeapTimeToLive, AddressableHeapHandleMap" outputs="same buffers" attributes="[NoAlias]" />
    <DEPENDENCY output="_ttlEvaluationHandle registered via H8Memory.RegisterActiveJob(SystemID.WorldStreaming)" />
    <COMPLETE_POLICY>Only cold boot/teardown or after `IsCompleted`; no arbitrary visible-frame wait in TTL path.</COMPLETE_POLICY>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    <USINGS status="PASS">Optimization runtime files reference only Core/Core.Contracts/Core.Memory plus Unity namespaces.</USINGS>
    <BUILD status="BLOCKED_BY_DEPENDENCY">Attempts 2-5 have no SHINOBU/Optimization errors; build still fails in Visor reconstruction DTOs/IDs, Somatic comfort DTOs, and Construction HeadlessDroneTask references.</BUILD>
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    <BEFORE>Blocking for real streamed assets risks unbounded I/O/driver wait.</BEFORE>
    <AFTER>O(1) cached impostor cube/material is returned while async Addressables load resolves; real asset swaps during visual sync.</AFTER>
    <COMPLEXITY before="unbounded blocking wait" after="O(1) placeholder return plus async completion" />
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>
