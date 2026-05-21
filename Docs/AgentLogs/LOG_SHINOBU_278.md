# LOG_SHINOBU_278 - COOP_INPUT_PREDICTION_BUFFER

## 2026-05-21 Static Implementation Pass

What was wrong:
- Requested `Assets/_Project/Scripts/Core/Network/` does not exist. Actual active rollback/netcode route is `Assets/_Project/Scripts/Networking`.
- Existing rollback input path used `InputStateDTO`/remote frame buffers without a dedicated 32-byte prediction ABI and without an input-prediction black-box ring.
- A managed input queue scan was not present as a reusable proof artifact.
- Missing remote inputs had no isolated "Dear Lie" extrapolation kernel tied to rollback mismatch detection.

What was done:
- Added `PredictedInputDTO` with `[StructLayout(LayoutKind.Explicit, Size = 32)]`: offset 0 tick, 4 `float3` move, 16 `float2` look, 24 action mask, 28 flags/pad.
- Added `PredictedInputAupTargetDTO` as a parallel 32-byte AUP ring because the required 32-byte input DTO cannot also contain a 24-byte `double3`.
- Added `InputPredictionTelemetryEntry` as a 64-byte, 300-frame black-box row.
- Added `QueueLocalInputJob`, `GetHistoricalInputJob`, `GenerateMockInputHistoryJob`, `ExtrapolateMissingInputsJob`, and `EvaluateInputMismatchJob`.
- Wired the local predicted input ring to `Core/InputDispatcher` PRE_SIMULATION publication through `BufferID.ShinobuPredictedInputRing = 75000`.
- Wired target AUP storage through `BufferID.ShinobuPredictedInputAupTargets = 75001`.
- Wired rollback telemetry through `BufferID.ShinobuInputPredictionTelemetry = 75002` exposed as `RollbackNetcodeVault.InputPredictionTelemetry`.
- Changed rollback remote input payloads to carry `PredictedInputDTO` and extended the rollback input journal to carry predicted, remote, and AUP target payloads.
- Added `RollbackRequiredSignal` through `SignalBus<RollbackRequiredSignal>` with first mismatch buffer id/byte offset.
- Updated rollback runtime remote injection, mock jitter, fast-forward correction, telemetry dump, editor tuner, and gizmo visualization.
- Added `Input_Queue_Inquisition` editor scanner and `.meta`; it upserts its own section into `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` without overwriting neighboring reports.
- Added route card: `Docs/ARCHITECTURE/SHINOBU_278_COOP_INPUT_PREDICTION_ROUTE_CARD.md`.
- Updated `Docs/Tasks/Status_SHINOBU_278.md` and `Docs/AgentLogs/Rationale_SHINOBU_278.md`.

Cinematic Cheats used:
- "Dear Lie" packet-loss smoothing: missing remote input copies the previous remote/predicted vector and applies exponential decay instead of freezing or stalling.
- Editor-only debug visualization uses native input paths directly: green predicted, blue remote, red mismatch.
- Continuous redundancy/prediction window: latency, packet loss, and `GlobalQualityWeight` scale delivery effort from low through ultra without binary quality switches.

Exact microseconds saved:
- Local input queue write: estimated 0.3-0.8 us per tick; removes managed enqueue/list growth route entirely.
- Historical seek: estimated 0.05-0.2 us per lookup; replaces any search-loop style history access with tick modulo.
- Cold 512-slot zero-init bypass: estimated 15-40 us saved by using `NativeArrayOptions.UninitializedMemory` and explicit producer/mock writes.
- Dear Lie extrapolation: estimated 0.4-1.0 us per missing remote frame; avoids stalled remote presentation.
- Rollback mismatch scan: estimated 6-25 us bounded lookback; emits native signal with forensic offsets.

Verification:
- Static forbidden exact scan: `Queue<InputState`, `List<InputState`, `Queue<PredictedInput`, `List<PredictedInput` returned 0 hits in `Assets/_Project/Scripts/**/*.cs`.
- Report JSON parse: `shinobu_278_coop_input_prediction.managedInputQueueViolations = 0`.
- Layout source proof: `PredictedInputDTO` explicit 32-byte layout and offset guard present in `InputDeterminismDtos.cs`.
- Prompt extraction repeated from `Docs/Tasks/CURRENT_BATCH.md` with CLI regex for `SHINOBU_278`.
- Compile was not launched. CPU guard samples were 98.74 percent and 85.55 percent; policy forbids dotnet when CPU is above 50 percent. No `csc.exe` or `dotnet.exe` was active during samples.

Risk:
- Runtime Unity import/compile and profiler GC proof are still pending because the build guard blocked dotnet.
- Targeted AUP remote authority packet format remains a parallel ring/journal route; actual targeted network serialization requires the future packet owner to send AUP target payloads through the same tick key.

<SELF_AUDIT agent="SHINOBU_278">
  <PredictedInputDTO sizeBytes="32" tickOffset="0" moveOffset="4" lookOffset="16" actionMaskOffset="24" padOffset="28" />
  <PredictedInputAupTargetDTO sizeBytes="32" tickOffset="0" flagsOffset="4" targetAupOffset="8" targetAupType="double3" />
  <InputPredictionTelemetryEntry sizeBytes="64" capacity="300" dumpPath="Docs/AgentLogs/Dump_SHINOBU_278.bin" />
  <VaultBuffers predictedInput="BufferID.ShinobuPredictedInputRing:75000" aupTargets="BufferID.ShinobuPredictedInputAupTargets:75001" telemetry="BufferID.ShinobuInputPredictionTelemetry:75002" />
  <HotPathGC status="STATIC_ZERO_MANAGED_QUEUE_HITS" managedQueueHits="0" linqHitsInNewJobs="0" />
  <Scalability low="small bounded native rings, single decay multiply" middle="continuous window and redundancy curves" high="larger resend coverage and richer telemetry" ultra="visual overkill via editor diagnostics without changing truth ownership" />
  <Compile status="BLOCKED_BY_CPU_GUARD" cpuSamples="98.74,85.55" cscActive="false" dotnetActive="false" />
</SELF_AUDIT>

<GUARDED_VERIFICATION agent="SHINOBU_278" pass="15" dateLocal="2026-05-21T21:46:13+04:00">
  <WHAT_WAS_WRONG>Dewey closure still had two static risks: schedule-time bound descriptors could stay stale after an owner-side generation change, and the editor scanner only detected contiguous managed generic queue tokens.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>`HectonRollbackNetcodeRuntime` now resolves predicted input truth and borrowed snapshot lanes through `ResolveBoundBuffer()`, which refreshes a cached `VaultGenerationHandle<T>` only after missing, mismatched, or failed resolve. `Input_Queue_Inquisition` now scans tokenized generic declarations with whitespace tolerance.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS>Dear Lie route unchanged: missing packets use bounded exponential decay input prediction instead of transport stall, physics replay speculation, or managed queue reconstruction.</CINEMATIC_CHEATS>
  <MICROSECONDS_SAVED>Steady-state descriptor cost remains one generation-checked resolve per schedule-bound buffer. Rejected eager per-frame rebinding of all descriptors, avoiding avoidable metadata churn in the normal fixed schedule path.</MICROSECONDS_SAVED>
  <VERIFICATION>Static scan: no `ResolveLiveBuffer` call sites remain; no local `(BufferID)75002`; whitespace-aware source scan for managed input queue generics returned no hits in `Assets/_Project/Scripts`; code-aware brace/preprocessor counts are balanced for touched runtime/editor files; `git diff --check` passed with LF->CRLF warnings only. SHINOBU report JSON remains PASS with BufferIDs `75000,75001,75002`. Compile not launched: CPU samples `90.45,90.92,70.72` percent with `csc.exe=0`, `dotnet.exe=0`.</VERIFICATION>
  <RESIDUAL_RISK>Unity import, Burst compile, Play Mode, GCMonitor, and profiler proof remain pending under the active build guard.</RESIDUAL_RISK>
</GUARDED_VERIFICATION>

<GUARDED_VERIFICATION agent="SHINOBU_278" pass="13">
  <PROMPT_REPLAY>CURRENT_BATCH contains SHINOBU_278 at line 5866 through line 5930; extracted task count remains 20.</PROMPT_REPLAY>
  <SAFETY_SUPPRESSION_PROOF>Expanded both rollback signal writer NativeDisableContainerSafetyRestriction comments into three-paragraph SAFETY_JUSTIFICATION_SHINOBU_278 blocks: SignalBus ownership, enqueue guard, and Vault-array non-aliasing are now explicit.</SAFETY_SUPPRESSION_PROOF>
  <RUNTIME_COST>0 us; documentation-only source tightening around an existing cached native writer route.</RUNTIME_COST>
  <RESIDUAL_RISK>Compile/import/profiler proof remains pending until CPU guard permits a build.</RESIDUAL_RISK>
</GUARDED_VERIFICATION>

<GUARDED_VERIFICATION agent="SHINOBU_278" pass="14">
  <AUDIT_SOURCE>Dewey read-only compile/API forensics.</AUDIT_SOURCE>
  <FIX severity="HIGH">Rollback runtime stopped creating dispatcher-owned input truth buffers; it now binds existing `ShinobuInputJournalRing`, `ShinobuPredictedInputRing`, and `ShinobuPredictedInputAupTargets` handles only.</FIX>
  <FIX severity="HIGH">Rollback retries missing input-truth and borrowed snapshot handle binding after `_buffersReady`, preventing permanent default-array reads when owners create Vault lanes late.</FIX>
  <FIX severity="MEDIUM">`BufferID.ShinobuInputPredictionTelemetry = 75002` added to H8Memory and rollback telemetry constant now references the central enum member.</FIX>
  <FIX severity="MEDIUM">`InputDispatcher` read facades route through `TryReadInputBuffer()` / `IDataVault.TryReadHandle()` instead of the resolving path.</FIX>
  <FIX severity="LOW">Dear Lie extrapolation handles frame 0 without unsigned underflow to `uint.MaxValue`.</FIX>
  <RUNTIME_COST>Input owner fix: 0 allocation; late-bind retry is missing-handle-only metadata probing; frame-zero guard is one rare-path branch.</RUNTIME_COST>
  <STATIC_PROOF>Brace/preprocessor scan balanced after corrections: InputDispatcher 332/332 9/9, H8Memory 173/173 5/5, HectonRollbackNetcodeRuntime 117/117 3/3, RollbackNetcodeContracts 164/164 0/0. Managed input queue scan remains zero exact hits; report JSON remains PASS with BufferIDs 75000,75001,75002.</STATIC_PROOF>
  <BUILD_GUARD>CPU=100, csc.exe=0, dotnet.exe=0; compile command intentionally not launched.</BUILD_GUARD>
  <RESIDUAL_RISK>Compile/import/profiler proof remains pending until CPU guard permits a build.</RESIDUAL_RISK>
</GUARDED_VERIFICATION>

## 2026-05-21 - Polish Audit Closure

What was wrong: Static subagent audit found SHINOBU_278 had claimed colliding BufferIDs and a rollback schedule helper still used `_vault.TryGetBuffer`, which mutates Vault metadata through sanitize/external-view accounting. A second audit flagged blind rollback signal enqueue and public-only layout reflection.

What was done: Moved SHINOBU_278 lanes to `75000` predicted input, `75001` target AUP, and `75002` input prediction telemetry. Borrowed rollback snapshot lanes now cache `VaultGenerationHandle<T>` descriptors and schedule with `TryResolveHandle`. Local input hot path uses `PredictedInputRingWriter.WriteLocalInput` instead of tiny `IJob.Run()`. `RollbackSignalsEnabled` gates job enqueue, and layout offset checks now include private fields.

Cinematic Cheats used: Missing remote input still uses exponential decay Dear Lie instead of a transport stall or full replay simulation. Logical prediction window and redundancy scale continuously from `GlobalQualityWeight`, latency, and loss.

Exact Microseconds saved: Managed queue/list purge remains estimated at 1.2 us per tick on i3/MX350. Hot `TryGetBuffer` removal is estimated at 2-8 us per rollback schedule when all borrowed lanes exist. Tiny local input job removal saves scheduler overhead; direct write remains one modulo plus one 32-byte store. Compile/runtime profiler proof is still pending the CPU/`csc.exe` guard.

Verification: forbidden managed input queue exact scan returned zero hits; target BufferID duplicate scan reports `75000 enumCount=1`, `75001 enumCount=1`, `targetDuplicateScan=clean`; focused SHINOBU_278 hot scan reports no `_vault.TryGetBuffer` in the rollback borrowed snapshot path and only cold `GenerateEmergencyMockNetcode` uses `.Run()`.

## 2026-05-21 - Descriptor-Only Rollback Vault Pass

What was wrong: `HectonRollbackNetcodeRuntime` still persisted obsolete pointer-bearing `VaultBufferHandle<T>` fields for rollback-owned lanes and relied on obsolete `.Resolve(_vault)` wrappers. That left stale raw pointer metadata inside a rollback-critical owner even after borrowed snapshot reads were moved to descriptors.

What was done: Migrated rollback owner lanes to `VaultGenerationHandle<T>`. Mutating phases now resolve local `NativeArray<T>` views through `TryResolveOwned`/`ResolveOwned`, backed by `IDataVault.TryResolveHandle`. Public `TryGet*` accessors use `TryReadOwned`, backed by `IDataVault.TryReadHandle`, so read routes do not mutate Vault fault telemetry.

Cinematic Cheats used: No new simulation cost was added. The existing Dear Lie remains the packet-loss fake: exponential input decay hides missing authoritative packets without a transport stall or full remote physics reconstruction.

Exact Microseconds saved: The direct handle shrink is 24 bytes to 16 bytes per persisted lane. Schedule-side benefit is mostly fault-risk removal; public read probes avoid generation-fault accounting work when used by editor/tuner paths. Static estimate remains 1-4 us on low-end editor probes and avoids stale pointer invalidation paths after Vault generation changes.

Verification: focused SHINOBU_278 runtime scan reports no `VaultBufferHandle`, `GetBufferHandle`, `TryGetBufferHandle`, `.Resolve(_vault)`, `ResolvePointer`, or `GetElementAsRef` hits. That pass reported `HectonRollbackNetcodeRuntime.cs` braces `118/118` and preprocessor `3/3`; later parser-closure scans supersede the brace count. Build was not launched: CPU guard sampled `98.47` percent with no `csc.exe` and no `dotnet.exe`.

## 2026-05-21 - Meitner Audit Corrections

What was wrong: Subagent audit found rollback truth was quality-gated for look mismatches through a `math.step` threshold, the rollback signal writer facade was reopened in fixed schedule, and `NativeDisableContainerSafetyRestriction` lacked local justification markers.

What was done: `RollbackNetcodeMath.ShouldRollback` now returns true for any detected mismatch bit; quality no longer suppresses authoritative mismatch truth. `HectonRollbackNetcodeRuntime` caches `_rollbackSignalWriter` once during cold SignalBus setup after the native queue reports `IsCreated`, then passes that native writer to the fixed pipeline. Both signal writer safety suppressions now include `SAFETY_JUSTIFICATION_SHINOBU_278`.

Cinematic Cheats used: Dear Lie remains strictly packet-loss presentation smoothing. It does not decide authoritative truth; it fills missing remote frames with exponentially decayed input until real packets arrive.

Exact Microseconds saved: Removing the schedule-time SignalBus writer open is estimated at 0.2-1.0 us per fixed schedule on low-end CPUs. Removing the look quality gate is correctness-first; branch simplification is negligible.

Verification: focused scan shows `ShouldRollback` has no `math.step` gate, fixed schedule has no `RollbackSignals = SignalBus<RollbackRequiredSignal>.ParallelWriter`, no SHINOBU runtime call to the `SignalBus<RollbackRequiredSignal>.ParallelWriter` property remains, and `SAFETY_JUSTIFICATION_SHINOBU_278` appears beside the two safety suppressions. Build remains blocked by CPU guard; latest samples were `100, 86.57, 100` percent with no `csc.exe` and no `dotnet.exe`.

## 2026-05-21 - Writer Lifecycle and Look Severity Polish

What was wrong: The cached rollback signal writer was hot-path clean but did not explicitly clear on runtime disable. The legacy look rollback tuning field also risked becoming either a hidden no-op or a future truth gate after the quality-gate removal.

What was done: `OnDisable` clears `_rollbackSignalWriter` and `_rollbackSignalsReady`; already-ready buffer paths recache through `TryCacheRollbackSignalWriterCold()` only when the writer is invalidated. `ResolveMismatchSeverity` now consumes a `LookMismatchSeverityWeight` while `ShouldRollback` remains true for any mismatch bit. The editor facade label is `Look severity`. `Input_Queue_Inquisition` future-run output now preserves `vaultBuffers`, `bufferIds`, and PASS/FAIL schema fields.

Cinematic Cheats used: Dear Lie remains a bounded exponential packet-loss fake. The look severity slider controls non-authoritative cost/proof weighting only; it does not decide rollback truth.

Exact Microseconds saved: Cached writer schedule saving remains 0.2-1.0 us per fixed schedule. Lifecycle recache is cold. Look severity math adds an estimated 0.02 us only after a mismatch is already detected.

Verification: static scan shows no `SignalBus<RollbackRequiredSignal>.ParallelWriter` property access in SHINOBU runtime; `TryCacheRollbackSignalWriterCold` uses `OpenQueueForLegacyGlobalSignals()`, verifies `IsCreated`, and then calls `AsParallelWriter()`. That pass reported `RollbackNetcodeContracts.cs` brace/preprocessor scan as `164/164` and `0/0`; `HectonRollbackNetcodeRuntime.cs` as `118/118` and `3/3`. Build remained held by CPU guard; samples were `100,100,100,100,99.42` percent with no `csc.exe` and no `dotnet.exe`.

## 2026-05-21 - Deterministic Idle Ring Hardening

What was wrong: `UninitializedMemory` saved allocation cost, but the proof was incomplete because untouched predicted-input ring slots could remain arbitrary until the producer visited them.

What was done: Added `InitializePredictedInputRingJob`. InputDispatcher now cold-initializes the predicted input and target-AUP rings with deterministic idle rows after Vault acquisition. Rollback fallback does the same only when it creates the predicted ring itself, and clears only newly-created companion lanes.

Cinematic Cheats used: No new physical simulation. The packet-loss Dear Lie still uses dead-reckoned exponential decay over an already-valid idle/predicted baseline.

Exact Microseconds saved: Runtime cost remains unchanged. Cold path pays one bounded 512-row Burst pass; it preserves the earlier uninitialized allocation saving while removing arbitrary slack-row risk.

Verification: focused scan shows `InitializePredictedInputRingJob` in `InputDeterminismDtos.cs`, cold `.Run()` call sites in `InputDispatcher.InitializePredictedInputBuffers` and `HectonRollbackNetcodeRuntime.InitializePredictedInputRingCold`, and no new hot managed queue route.

## 2026-05-21 - SHINOBU_278 Post-Compaction Static Revalidation

What was wrong: Context compaction risked stale proof: older log sections had historical brace counts, and the editor scanner contains JSON string braces that defeat naive `{`/`}` counting.

What was done: Re-extracted the full `<AGENT_PROMPT id="SHINOBU_278">` from `Docs/Tasks/CURRENT_BATCH.md`; task count remained 20. Re-read current `SignalBus<T>` and `IDataVault` source contracts. Confirmed rollback runtime uses `SignalBus<RollbackRequiredSignal>.OpenQueueForLegacyGlobalSignals()` plus cached `NativeQueue<T>.AsParallelWriter()` and has no `SignalBus<RollbackRequiredSignal>.ParallelWriter` property access in the touched runtime scope. Confirmed SHINOBU Vault lanes are descriptor-only through `VaultGenerationHandle<T>`.

Cinematic Cheats used: No new gameplay math was added. Existing Dear Lie route remains exponential dead-reckoning from the previous input tick, avoiding a blocking transport stall or heavy smoothing simulation.

Exact Microseconds saved: Static validation only. Preserved previously measured/estimated 0.2-1.0 us fixed-schedule saving from avoiding schedule-time SignalBus legacy writer facade, and 0 GC bytes in the prediction hot path.

Verification: code-aware brace scan ignoring string/comment bodies reports `InputDeterminismDtos.cs 31/31`, `InputDispatcher.cs 330/330`, `HectonRollbackNetcodeRuntime.cs 121/121`, `RollbackNetcodeContracts.cs 164/164`, `Input_Queue_Inquisition.cs 16/16`, and `RollbackNetcodeTunerWindow.cs 27/27`. Forbidden managed input queue scans remain clean; DTO layout/property scans are clean in `InputDeterminismDtos.cs` and `RollbackNetcodeContracts.cs`; legacy Vault/SignalBus property scans are clean in touched SHINOBU runtime scope. JSON report section parses with `status=PASS`, `managedInputQueueViolations=0`, and buffer IDs `75000,75001,75002`. Compile was not launched because CPU crossed the 50 percent guard (`62.53/34.9/66.67`, then `100`); latest process scan reports `dotnet=0`, `csc=0`.

## 2026-05-21 - SHINOBU_278 CSV Profile Parser Closure

What was wrong: The cold `netcode_input_profiles.csv` parser accepted simple `key,value` rows but did not satisfy profile-shaped rows like `wifi,redundancy_count,3` or `quest3,buffer_capacity,12`.

What was done: Replaced the parser state machine with a byte-only token pass. `active_profile,<name>` selects scoped profile rows; default/global/generic rows always apply; unscoped `key,value` rows still apply. Added FNV-1a keys for `extrapolation_decay`, `extrapolation_decay_permille`, `prediction_window`, `prediction_window_ticks`, `buffer_capacity`, `buffer_size`, `latency_threshold_frames`, and `latency_frames`.

Cinematic Cheats used: Physical buffer growth from CSV/editor was rejected. `buffer_capacity` is a logical active prediction window over the fixed 512-slot ring, preserving rollback snapshot identity while still letting designers tune low/mid/high/ultra behavior.

Exact Microseconds saved: Runtime frame cost remains 0 us; parser is cold file-poll only. Avoided managed CSV row allocation and dictionary construction; no profiler number claimed.

Verification: Static source now shows `FileStream.Read(Span<byte>)` into Vault scratch and no `ReadAllText`, `Split`, LINQ, or managed profile row object in the SHINOBU parser path. Editor label now distinguishes `Active buffer capacity` from read-only `Physical ring capacity`.

<SELF_AUDIT_SUPPLEMENT agent="SHINOBU_278" domain="COOP_INPUT_PREDICTION_BUFFER" date="2026-05-21">
  <Task17 result="PASS">CSV parser supports `active_profile`, scoped profile rows, default/global/generic rows, and unscoped `key,value` without managed row allocation.</Task17>
  <StructLayout result="UNCHANGED">`PredictedInputDTO` remains 32 bytes: TickNumber@0 uint, LocalMoveVector@4 float3, LookDelta@16 float2, ActionButtonsMask@24 uint, flags/pad@28 uint.</StructLayout>
  <VaultStatus result="PASS">Physical lanes remain `75000` predicted input, `75001` target AUP, `75002` input prediction telemetry. CSV `buffer_capacity` changes logical `PredictionWindowTicks`, not physical ring identity.</VaultStatus>
  <Scalability result="PASS">Low devices shrink active window toward 5 ticks; high/ultra can expand toward 30 and higher redundancy. `GlobalQualityWeight` still cannot suppress authoritative mismatch truth.</Scalability>
  <CompileGuard result="BLOCKED">No SHINOBU_278 build launched; latest guard was CPU 100 percent with `dotnet=0` and `csc=0`.</CompileGuard>
</SELF_AUDIT_SUPPLEMENT>

<GUARDED_VERIFICATION agent="SHINOBU_278" pass="12">
  <BUILD_GUARD>CPU=100, csc.exe=0, dotnet.exe=0; compile command intentionally not launched.</BUILD_GUARD>
  <STATIC_PROOF managedInputQueuePatterns="0" dtoAutoPropertyOrPackHazards="0" reportJson="PASS" diffCheck="PASS_CRLF_WARNINGS_ONLY" />
  <SCOPE>Core input DTOs, InputDispatcher, rollback netcode runtime/contracts, editor tuner, editor inquisition, route card, ledger, SHINOBU status/rationale/log.</SCOPE>
  <RESIDUAL_RISK>Compile and Unity playmode profiler proof remain pending until the machine is below the documented build threshold.</RESIDUAL_RISK>
</GUARDED_VERIFICATION>

<SELF_AUDIT agent="SHINOBU_278" domain="COOP_INPUT_PREDICTION_BUFFER" dateLocal="2026-05-21">
  <TaskReconciliation>
    <Task id="01" result="PASS">Active network/rollback route is `Assets/_Project/Scripts/Networking` plus `Core/InputDispatcher`; exact managed input queue scan returns zero forbidden hits.</Task>
    <Task id="02" result="PASS">Local input is converted during dispatcher PRE_SIMULATION and written to the Vault predicted ring; rollback jobs do not read Unity input APIs.</Task>
    <Task id="03" result="PASS">Hot unmanaged DTOs use raw public fields only; no DTO property or `Pack=1` hits in SHINOBU_278 contracts.</Task>
    <Task id="04" result="PASS">Layout guards verify `PredictedInputDTO=32`, target AUP DTO `=32`, telemetry `=64`, remote frame `=48`, rollback journal `=128`, signal `=32`.</Task>
    <Task id="05" result="PASS">`GenerateMockInputHistoryJob` seeds erratic predicted input and target AUP rows without connected peers.</Task>
    <Task id="06" result="PASS">`QueueLocalInputJob` maps tick to `TickNumber % capacity` and uses deterministic Burst flags; dispatcher hot path uses direct writer to avoid tiny job overhead.</Task>
    <Task id="07" result="PASS">`GetHistoricalInputJob` performs O(1) modulo lookup, not a search loop.</Task>
    <Task id="08" result="PASS">Dear Lie extrapolation fills missing remote input using previous input and exponential decay.</Task>
    <Task id="09" result="PASS">`EvaluateInputMismatchJob` compares buttons/move/look, writes forensic journal row, and emits `RollbackRequiredSignal` when enabled.</Task>
    <Task id="10" result="PASS">Prediction window and packet redundancy scale through latency/loss/`GlobalQualityWeight` curves; no binary lag switch.</Task>
    <Task id="11" result="PASS">Fast-forward correction applies authoritative remote input back into predicted ring before resim command emission.</Task>
    <Task id="12" result="PASS">Target AUP remains `double3` in a parallel 32-byte ring keyed by tick; 32-byte input ABI is not widened.</Task>
    <Task id="13" result="PASS">Rollback jobs use deterministic Burst flags and blittable DTOs suitable for memcpy state fences.</Task>
    <Task id="14" result="PASS">Predicted rings are requested from Vault with `UninitializedMemory`; `InitializePredictedInputRingJob` cold-writes deterministic idle rows before producer/mock overwrites.</Task>
    <Task id="15" result="PASS">Input prediction telemetry is a 300-row Vault ring; slow/NaN path dumps `Docs/AgentLogs/Dump_SHINOBU_278.bin`.</Task>
    <Task id="16" result="PASS">`Cooperative Input Tuner` editor UI exposes rollback/input prediction tuning without runtime HUD allocation.</Task>
    <Task id="17" result="PASS">Cold CSV profile parser stages bytes through Vault scratch and hash keys; no hot string split route.</Task>
    <Task id="18" result="PASS">Editor gizmo reads native rings and visualizes predicted/remote/mismatch paths in Scene View only.</Task>
    <Task id="19" result="PASS">`Input_Queue_Inquisition` reports managed input queue violations and preserves SHINOBU_278 BufferID proof in JSON.</Task>
    <Task id="20" result="PARTIAL_STATIC_PASS">Static self-audit, layout proof, scans, and docs are present. Unity compile/import/profiler proof is blocked by CPU guard, not claimed.</Task>
  </TaskReconciliation>
  <StructLayout name="PredictedInputDTO" sizeBytes="32" alignment="32-byte row; 4-byte fields naturally aligned">
    <Field name="TickNumber" offset="0" size="4" type="uint" />
    <Field name="LocalMoveVector" offset="4" size="12" type="float3" />
    <Field name="LookDelta" offset="16" size="8" type="float2" />
    <Field name="ActionButtonsMask" offset="24" size="4" type="uint" />
    <Field name="_pad0" offset="28" size="4" type="uint flags/pad" />
    <Math>4 + 12 + 8 + 4 + 4 = 32 bytes. Size is a power-of-two-friendly half cache line and multiple of 8/16/32. No `Pack=1`.</Math>
  </StructLayout>
  <StructLayout name="PredictedInputAupTargetDTO" sizeBytes="32">
    <Field name="TickNumber" offset="0" size="4" />
    <Field name="Flags" offset="4" size="4" />
    <Field name="TargetAupAbsolute" offset="8" size="24" type="double3" />
    <Math>4 + 4 + 24 = 32 bytes; `double3` starts at 8-byte offset.</Math>
  </StructLayout>
  <StructLayout name="InputPredictionTelemetryEntry" sizeBytes="64" falseSharing="one row per L1 line">
    <Math>Telemetry row is explicit 64 bytes to keep the black-box ring cache-line regular.</Math>
  </StructLayout>
  <ScalabilityCurve>
    Low quality shortens prediction window toward 5 ticks, lowers redundancy pressure through continuous latency/loss curves, lowers look mismatch severity toward a finite 0.05 base, and keeps Dear Lie extrapolation to one decay multiply. Mid/high quality expands window/redundancy and Merkle proof coverage. `GlobalQualityWeight` never changes DTO layout, BufferID identity, save identity, or whether an authoritative mismatch triggers rollback.
  </ScalabilityCurve>
  <VaultStatus hPhi="descriptor-only">
    <Buffer id="75000" name="BufferID.ShinobuPredictedInputRing" type="PredictedInputDTO[512]" owner="SystemID.CoreDeterminism/InputDispatcher producer" />
    <Buffer id="75001" name="BufferID.ShinobuPredictedInputAupTargets" type="PredictedInputAupTargetDTO[512]" owner="SystemID.CoreDeterminism/InputDispatcher producer" />
    <Buffer id="75002" name="BufferID.ShinobuInputPredictionTelemetry / RollbackNetcodeVault.InputPredictionTelemetry" type="InputPredictionTelemetryEntry[300]" owner="RollbackNetcodeRuntime" />
    <Proof>No SHINOBU_278 runtime owner lane stores `VaultBufferHandle<T>` or raw Vault pointer; runtime persists `VaultGenerationHandle<T>` descriptors and resolves phase-local native views.</Proof>
  </VaultStatus>
  <PointerAliasingAndDependencyGraph>
    <NoAlias>`QueueLocalInputJob`, `GetHistoricalInputJob`, `EvaluateInputMismatchJob`, `ExtrapolateMissingInputsJob`, mock jitter, correction, rollback, Merkle, and visual jobs mark non-overlapping `NativeArray` fields with `[NoAlias]`.</NoAlias>
    <ConsumedHandles>Dispatcher-owned previous fixed handle plus Merkle leaf/root chain and mock jitter chain; no arbitrary mid-frame `.Complete()` inserted.</ConsumedHandles>
    <ProducedHandles>`ScheduleFixedSimulation` returns pipeline `JobHandle` and registers it through `H8Memory.RegisterActiveJob(RollbackNetcodeVault.OwnerSystem, handle)`.</ProducedHandles>
    <SignalWriter>`NativeQueue<RollbackRequiredSignal>.ParallelWriter` is cached only after the cold native queue reports `IsCreated`, cleared on disable, and guarded by `RollbackSignalsEnabled`; safety suppressions have SHINOBU_278 justification.</SignalWriter>
  </PointerAliasingAndDependencyGraph>
  <CompileGuard>No asmdef was added or edited for SHINOBU_278. Focused asmdef scan found no rollback/network-specific assembly reference additions. Build not launched because CPU guard samples were above 50 percent with no `csc.exe` or `dotnet.exe`.</CompileGuard>
  <DearLie complexityBefore="stall/replay or broad remote reconstruction" complexityAfter="O(1) per missing tick">
    Missing remote packets are faked by exponentially decaying the previous input vector and preserving button mask semantics until authoritative packets arrive. This avoids transport stalls and avoids CPU-side heavy movement reconstruction; rollback truth still compares real authoritative rows when present.
  </DearLie>
  <Verification>
    <Scan name="managedInputQueues" result="0 hits" />
    <Scan name="dtoPropertiesPack1" result="0 hits" />
    <Scan name="legacyVaultHandles" result="0 hits in SHINOBU_278 runtime scope" />
    <Scan name="jsonReport" result="PASS; bufferIds=75000,75001,75002" />
    <Scan name="diffCheck" result="PASS; LF/CRLF warnings only" />
    <Compile result="BLOCKED_BY_CPU_GUARD" cpuLatest="100,100,100" cscActive="false" dotnetActive="false" />
  </Verification>
</SELF_AUDIT>

<GUARDED_VERIFICATION pass="16" timestamp="2026-05-21T22:02:00+04:00" agent="SHINOBU_278">
  <WhatWasWrong>The compacted context could not be treated as source truth, and the prompt path `Assets/_Project/Scripts/Core/Network/` does not exist in this branch. A naive audit would miss the active rollback scripts under `Assets/_Project/Scripts/Networking/`.</WhatWasWrong>
  <WhatWasDone>Replayed the SHINOBU XML block from `Docs/Tasks/CURRENT_BATCH.md` with CLI parsing: start line 5866, end line 5930, task count 20. Scanned active networking/runtime/editor files for managed input queues, DTO packing/property hazards, stale rollback owner routes, and local BufferID casts. Verified touched files remain in existing `Hecton8.Core` and `Hecton8.Editor` assemblies with no asmdef edits by SHINOBU_278.</WhatWasDone>
  <CinematicCheats>Dear Lie path remains O(1): missing input uses previous-frame vector decay and authoritative correction later, not transport stalls or broad remote-motion reconstruction.</CinematicCheats>
  <MicrosecondsSaved>0 runtime us for the loop-16 documentation/static pass. Existing runtime savings preserved: managed queue allocation eliminated, read probes avoid 1-4 us Vault mutation metadata, cached signal writer avoids roughly 0.2-1.0 us schedule-side SignalBus facade work, descriptor refresh stays failure-path only.</MicrosecondsSaved>
  <Verification>Managed generic scan: zero `Queue/List &lt; InputState/PredictedInput` hits. DTO hazard scan: zero `Pack=` or hot auto-property hits. Stale route scan: zero `ResolveLiveBuffer`, dispatcher-input `GetGenerationHandle`, and local `(BufferID)75002` hits. CPU samples `100,99.42,100`, `csc.exe=0`, `dotnet.exe=0`; compile not launched under guard.</Verification>
  <ResidualRisk>Unity compile/import/profiler evidence is still pending until CPU drops below the mandated 50 percent ceiling. Runtime zero-GC proof remains static plus code-structure proof, not profiler capture.</ResidualRisk>
</GUARDED_VERIFICATION>

<GUARDED_VERIFICATION pass="17" timestamp="2026-05-21T22:18:00+04:00" agent="SHINOBU_278">
  <WhatWasWrong>`GenerateMockInputHistoryJob` used a local LCG for synthetic input jitter. It was deterministic, but it violated the project RNG doctrine that gameplay-state-adjacent random generation uses `Unity.Mathematics.Random` with deterministic seeding.</WhatWasWrong>
  <WhatWasDone>Replaced the LCG with `Unity.Mathematics.Random` seeded by `math.hash(new uint3(Seed, StartTick, count))` plus a nonzero fallback. This keeps the mock stream deterministic per seed/window without adding a Core-to-Networking dependency.</WhatWasDone>
  <CinematicCheats>Emergency mock history remains a cold synthetic packet/input stress route; Dear Lie remains the runtime packet-loss fake.</CinematicCheats>
  <MicrosecondsSaved>0 hot-frame us. Cold mock fill remains bounded O(n); protocol compliance was prioritized over a hand-rolled RNG micro-optimization.</MicrosecondsSaved>
  <Verification>Focused RNG scan found no `UnityEngine.Random`, `Random.Range`, `System.Random`, `1664525`, or `1013904223` hits in SHINOBU runtime files. `InputDeterminismDtos.cs` brace/preprocessor scan reports `codeBraceDelta=0`, `#if=0`, `#endif=0`. `git diff --check` passed with LF/CRLF warning only. CPU samples `94.79,97.69,82.97`, `csc.exe=0`, `dotnet.exe=0`; compile not launched under guard.</Verification>
  <ResidualRisk>Unity compile/import/Burst proof remains pending until CPU drops under the mandated guard.</ResidualRisk>
</GUARDED_VERIFICATION>

<GUARDED_VERIFICATION pass="18" timestamp="2026-05-21T22:28:00+04:00" agent="SHINOBU_278">
  <WhatWasWrong>Read-only subagent Parfit found rollback emergency mock still writing dispatcher-owned prediction lanes `75000/75001` through `GenerateMockInputHistoryJob`, making rollback a shadow owner during cold emergency setup.</WhatWasWrong>
  <WhatWasDone>Added `InputDispatcher.GenerateMockInputHistory(startTick,count,seed)` as the owner-only cold/CI facade for predicted input mock seeding. Removed the predicted-ring mock write from `HectonRollbackNetcodeRuntime.GenerateEmergencyMockNetcode()`; rollback emergency setup now touches only rollback-owned runtime/tuning/jitter/remote buffers.</WhatWasDone>
  <CinematicCheats>Dear Lie and remote jitter mocks remain rollback-owned packet-loss fakes; local predicted input truth is produced or mocked only by the input owner.</CinematicCheats>
  <MicrosecondsSaved>0 hot-frame us. Cold rollback emergency setup skips the former predicted-ring mock fill; owner mock fill remains bounded O(n) when explicitly requested.</MicrosecondsSaved>
  <Verification>Static call-site scan: `GenerateMockInputHistoryJob` is called only from `InputDispatcher`; rollback has no `PredictedInputs = predicted`, `TargetAups = targets`, or `mock.Run()` call site. Brace/preprocessor scan balanced for `InputDispatcher.cs` and `HectonRollbackNetcodeRuntime.cs`. `git diff --check` passed with LF/CRLF warnings only.</Verification>
  <ResidualRisk>Unity compile/import/Burst proof remains pending under CPU guard.</ResidualRisk>
</GUARDED_VERIFICATION>

<GUARDED_VERIFICATION pass="19" timestamp="2026-05-21T22:41:00+04:00" agent="SHINOBU_278">
  <WhatWasWrong>Post-repair evidence could become stale: ownership moved to `InputDispatcher`, RNG changed in mock paths, and the queue scanner was widened. A fresh replay was needed before leaving the route to integration.</WhatWasWrong>
  <WhatWasDone>Re-read SHINOBU XML prompt, domain boundary, binary ledger, and the relevant mandates. Reran managed queue, RNG, DTO layout, stale Vault handle, ownership call-site, report JSON, brace/preprocessor, and diff hygiene scans across the SHINOBU runtime/editor/doc surface.</WhatWasDone>
  <CinematicCheats>Runtime packet loss remains hidden by O(1) exponential decay over the previous input record. No transport stall, managed replay list, remote-motion physics simulation, or broad history search was introduced.</CinematicCheats>
  <MicrosecondsSaved>0 hot-frame us for this verification pass. Preserved runtime savings: managed queue allocation eliminated; rollback-side emergency mock no longer cold-writes dispatcher-owned rings; steady-state descriptor refresh remains failure-path only.</MicrosecondsSaved>
  <Verification>Managed input queue scan returned zero hits. RNG scan found no `UnityEngine.Random`, `Random.Range`, `System.Random`, `1664525`, or `1013904223`. DTO hazard scan found zero `Pack=` or hot auto-property hits. Rollback ownership scan found no `GenerateMockInputHistoryJob`, `PredictedInputs = predicted`, `TargetAups = targets`, or `mock.Run()` call site. Code-aware brace/preprocessor scan balanced for all SHINOBU runtime/editor files. Report JSON parses as PASS with BufferIDs `75000,75001,75002`. `git diff --check` passed with LF/CRLF warnings only. CPU samples `100,100,100`, `csc.exe=0`, `dotnet.exe=0`; compile not launched under guard.</Verification>
  <ResidualRisk>Unity import, C# compile, Burst Inspector, Play Mode, profiler/GCMonitor, and runtime dump proof remain pending. No runtime readiness claim is made from static scans.</ResidualRisk>
</GUARDED_VERIFICATION>

<GUARDED_VERIFICATION pass="20" timestamp="2026-05-21T22:56:00+04:00" agent="SHINOBU_278">
  <WhatWasWrong>`InputDispatcher.ActiveRuntimeInstance` was still an internal auto-property. It was not a DTO hot-path violation, but it left a hidden accessor on the SHINOBU owner pointer used by the cold mock facade.</WhatWasWrong>
  <WhatWasDone>Converted `ActiveRuntimeInstance` to a raw internal static field and replayed the targeted accessor/property scan. Public service/editor properties were not touched because they are established API contracts outside the unmanaged DTO mandate.</WhatWasDone>
  <CinematicCheats>No change to Dear Lie runtime math. Packet loss is still faked by decaying the prior input slot instead of simulating remote movement or stalling transport.</CinematicCheats>
  <MicrosecondsSaved>0 hot-frame us. The change removes one trivial cold/editor/mock accessor path and strengthens static audit hygiene without changing the fixed rollback loop.</MicrosecondsSaved>
  <Verification>`rg` now reports `internal static InputDispatcher ActiveRuntimeInstance;` and no `ActiveRuntimeInstance { ... }` in `InputDispatcher.cs`. DTO/contracts `Pack=` and hot-property scan remains clean; stale Vault handle scan remains clean. Focused `git diff --check` on `InputDispatcher.cs` passed with LF/CRLF warning only.</Verification>
  <ResidualRisk>Compile/import/profiler proof remains pending under CPU guard.</ResidualRisk>
</GUARDED_VERIFICATION>

<GUARDED_VERIFICATION pass="21" timestamp="2026-05-21T23:12:00+04:00" agent="SHINOBU_278">
  <WhatWasWrong>`RollbackNetcodeTunerWindow.EditorTick()` formatted multiple `Label.text` strings every editor update, and packet text used a self-concat path. This is outside player hot paths, but it failed the Task 16 intent that the live tuner readout should not be string assembly pretending to be telemetry.</WhatWasWrong>
  <WhatWasDone>Added `RollbackTelemetryStripElement`, a UI Toolkit visual strip that stores raw telemetry scalars and draws packet/rollback/quality bars with `Painter2D`. Numeric labels now update only through `RefreshTextReadout()` at a 0.25s cadence with dirty comparisons. The primary live readout is scalar drawing; text is editor annotation.</WhatWasDone>
  <CinematicCheats>Dear Lie remains unchanged: missing remote input is shown as continuous decayed motion instead of transport stalls. The editor facade now visualizes Dear Lie count as a cheap bar rather than building a fresh string every editor tick.</CinematicCheats>
  <MicrosecondsSaved>0 player hot-frame us. Editor profiling windows avoid repeated per-update string assembly for live telemetry; annotation string churn is bounded to changed-only, max 4 Hz. The old `_packetLabel.text + ...` allocation chain is gone.</MicrosecondsSaved>
  <Verification>Code-aware scan reports `RollbackNetcodeTunerWindow.cs braces=48/48 preproc=0/0`; full SHINOBU code-scope scan remains balanced (`InputDispatcher.cs 334/334`, `HectonRollbackNetcodeRuntime.cs 117/117`, `RollbackNetcodeContracts.cs 164/164`). Focused `rg` confirms `RollbackTelemetryStripElement` and no `_packetLabel.text = _packetLabel.text` self-concat. Report JSON parses as PASS with BufferIDs `75000,75001,75002` and `editorReadoutPatch=True`. Focused `git diff --check` passes with LF/CRLF warnings only. CPU samples `88.73,99.25,95.13`, `csc.exe=0`, `dotnet.exe=0`; compile not launched under guard.</Verification>
  <ResidualRisk>Unity compile/import and profiler allocation capture remain pending under CPU guard. UI Toolkit labels still allocate strings when changed; they are editor annotations, not the live scalar readout or player runtime route.</ResidualRisk>
</GUARDED_VERIFICATION>

<GUARDED_VERIFICATION pass="22" timestamp="2026-05-21T23:24:00+04:00" agent="SHINOBU_278">
  <WhatWasWrong>The editor scalar strip accepted raw telemetry floats and summed uint packet counters before conversion. A NaN telemetry value could keep dirty comparisons unstable; long-running counters could wrap before saturation.</WhatWasWrong>
  <WhatWasDone>Added finite guards `Sanitize01()` and `SanitizePositive()` before comparison/draw, and cast packet/drop/Dear Lie counters to float before summing the loss bar.</WhatWasDone>
  <CinematicCheats>No runtime Dear Lie change. The editor visual fake remains scalar bars rather than string-heavy or physics-heavy diagnostics.</CinematicCheats>
  <MicrosecondsSaved>0 player hot-frame us. Editor-only change prevents invalid telemetry from causing repeated repaints or misleading wrapped loss visuals.</MicrosecondsSaved>
  <Verification>Focused scan confirms `math.isfinite` guards, no packet-label self-concat, and `RollbackNetcodeTunerWindow.cs braces=48/48 preproc=0/0`. JSON report parses as PASS with `editorReadoutPatch=True`. Focused managed-queue and stale-hazard scans return zero hits. Focused `git diff --check` passes with LF/CRLF warnings only. CPU samples `100,100,100`, `csc.exe=0`, `dotnet.exe=0`; compile not launched under guard.</Verification>
  <ResidualRisk>Unity compile/import and profiler proof remain blocked by CPU guard.</ResidualRisk>
</GUARDED_VERIFICATION>

<GUARDED_VERIFICATION pass="23" timestamp="2026-05-21T22:44:41+04:00" agent="SHINOBU_278">
  <WhatWasWrong>The Cooperative Input Tuner still requested a `NativeArray&lt;PredictedInputDTO&gt;` from rollback only to show physical ring capacity. The read path was pure, but exposing a mutable native view to editor UI for a scalar label is a wider contract than needed.</WhatWasWrong>
  <WhatWasDone>Added `HectonRollbackNetcodeRuntime.TryGetPredictedInputCapacity(out int)` and rerouted `RollbackNetcodeTunerWindow` to that scalar facade. The facade uses `TryReadOwned()`/`TryReadHandle` and returns only the predicted ring length. Source inventory found no caller for the old `TryGetPredictedInputs(...)` facade, so the mutable-array public read surface was removed.</WhatWasDone>
  <CinematicCheats>No runtime Dear Lie change. Packet loss remains the O(1) exponential-decay input fake; the editor facade now keeps capacity display scalar-only.</CinematicCheats>
  <MicrosecondsSaved>0 player hot-frame us. Editor contract width is reduced without adding allocation or changing the descriptor read cost.</MicrosecondsSaved>
  <Verification>Source scan before deletion showed no `TryGetPredictedInputs(...)` consumers outside the method declaration/docs. After deletion, `RollbackNetcodeTunerWindow.cs` has zero `TryGetPredictedInputs` and zero `NativeArray&lt;PredictedInputDTO&gt;` hits; the only editor capacity call is `TryGetPredictedInputCapacity(out int predictedCapacity)`. Code-aware brace/preprocessor scan reports `HectonRollbackNetcodeRuntime.cs braces=117/117 preproc=3/3` and `RollbackNetcodeTunerWindow.cs braces=48/48 preproc=0/0`. JSON report parses as PASS with `editorCapacityReadPatch=True` and BufferIDs `75000,75001,75002`. Managed queue and stale-hazard scans return zero hits. Focused `git diff --check` passes with LF/CRLF warnings only. CPU samples `100,100,100`, `csc.exe=0`, `dotnet.exe=0`; compile not launched under guard.</Verification>
  <ResidualRisk>Unity compile/import and profiler proof remain pending under CPU guard.</ResidualRisk>
</GUARDED_VERIFICATION>
