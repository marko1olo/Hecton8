# LOG_SHINOBU_101

Date: 2026-05-19
Agent: SHINOBU_101
Domain: ECHELON 1 CORE & MEMORY INFRASTRUCTURE
Status: PENDING VERIFICATION - build blocked by CPU gate

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
- Added Vault CSV scratch buffer `AddressableHeapCsvScratch` and byte-span parser for `asset_cache_profiles.csv`.
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
    <TASK id="17" status="PASS">300-entry telemetry ring dumps raw binary to `Docs/AgentLogs/Dump_MEMORY_SURGEON.bin`.</TASK>
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
    <JOB name="AssetTtlEvaluationJob" consumes="AddressableHeapTrackers, AddressableHeapTimeToLive, AddressableHeapTrackerFlags, AddressableHeapHandleMap" outputs="same buffers" attributes="[NoAlias]" />
    <DEPENDENCY input="none currently exposed" output="_ttlEvaluationHandle registered via H8Memory.RegisterActiveJob(SystemID.WorldStreaming)" />
    <LOCKS>Trackers, TTL, Flags, HandleMap are Vault-locked while TTL job is scheduled.</LOCKS>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    <STATUS>PENDING: dotnet build not launched because CPU samples were 100%, 98.7%, then 100%, which violates AGENTS.md build gate.</STATUS>
    <ASMDEF>Optimization runtime has no local asmdef in `Assets/_Project/Scripts/Optimization`; edited runtime files are in `Hecton8.Core.csproj`. Editor asmdef `Hecton8.Optimization.Editor` references only `Hecton8.Core`.</ASMDEF>
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    <BEFORE>Blocking or waiting for a real streamed asset risks unbounded I/O/driver wait.</BEFORE>
    <AFTER>Return cached cube impostor/material in O(1), then swap after async completion.</AFTER>
  </DEAR_LIE_CONFIRMATION>
  <VERIFICATION>
    <STATIC_SCAN>Target-file scans found no `Dictionary`, `List`, `Queue`, `File.ReadAllText`, `string.Split`, `Regex`, or `OnGUI` in SHINOBU_101 target files.</STATIC_SCAN>
    <BUILD>Not run due CPU gate; runtime/Unity/Profiler/GCMonitor proof absent.</BUILD>
  </VERIFICATION>
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
- Compile remains blocked by CPU gate.
