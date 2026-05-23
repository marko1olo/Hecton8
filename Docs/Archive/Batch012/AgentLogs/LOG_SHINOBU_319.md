# LOG_SHINOBU_319 - STATUS_EFFECTS_FSM_ENGINE

## 2026-05-22 - Status Effects FSM Integration

What was wrong:
- Combat status truth lived in branch-heavy slow tick code with a `uint` mirror mask and scattered duration arrays.
- Poison/bleed archaeology showed no active runtime coroutine or managed `Dictionary<EffectType,float>` timer in Combat/Physiology, but there was no scanner proving the absence.
- DoT status damage risked becoming a side-owner if implemented as a new manager.
- No status-specific 300-frame blackbox dump existed for SHINOBU_319.

What was done:
- Converted `CombatDamageRuntime` to `partial` and added `CombatDamageRuntime_StatusEffects.cs`.
- Added Vault-backed 64B `CombatStatusEffectState` with `ulong StatusEffectMask` at offset 0, two `float4` timer packs, byte FSM lanes, and state hash.
- Added `CombatStatusEffectRequest` queue, `ApplyStatusEffectRequestsJob`, and Interlocked CAS OR over the status mask word.
- Added `EvaluateStatusEffectsJob` with deterministic Burst math, continuous cadence from `GlobalQualityWeight`, and `SignalBus<CombatDamageSignal>` DoT routing.
- Removed the dead private `ProcessCombatStatusJob` branch-heavy implementation.
- Added 300-entry `CombatStatusEffectTelemetryEntry` Vault ring and `Docs/AgentLogs/Dump_SHINOBU_319.bin` dump path.
- Added UI Toolkit `FSM Status Effect Tuner`, cold `status_effect_profiles.csv` span parser, live debug gizmo, and `OOP_Buff_Scanner`.
- Added BufferIDs 71260-71266 and SystemID `GameplayCombat = 74`.

Cinematic Cheats used:
- Poison bubble feedback uses `BubbleSpawnSignal`; no CPU particle system or per-status GameObject is created.
- Low-tier cadence batches status integration up to 1.0s while preserving total damage via accumulated dt.
- High/Ultra tiers use the same gameplay truth and spend extra cadence/VFX budget without binary quality branches.

Exact Microseconds saved:
- Legacy coroutine deletion: no active coroutine path found, so measured saving is 0 us; scanner prevents future >100 us swarm regressions.
- Branchy slow tick removal: static estimate 20-60 us per 2k targets on i3/MX350-class hardware from fewer branch checks and one 64B state row.
- Low-tier cadence: static estimate 0.05-0.15 ms saved when quality drops from 10Hz status evaluation to 1Hz batch integration.
- VFX fake: static estimate 200+ us saved during poison bursts versus CPU particle/component spawning.
- Telemetry cost: estimated 1 us per active status result write; profiler proof blocked by CPU rule.

Verification:
- `rg` found no edited runtime `IEnumerator`, `StartCoroutine`, `yield return`, `new Dictionary`, `float.Parse`, `ProcessCombatStatusJob`, or `StatusBatchSize`.
- Brace counts matched for `CombatDamageRuntime_StatusEffects.cs` and `StatusEffectsEditorFacade.cs`.
- `git diff --check` returned no whitespace errors for touched files.
- Compile was not launched. CPU sampled at 87-100%, and HECTON rule forbids rebuild under >50% CPU load.

<SELF_AUDIT>
  <TASK_CHECK>
    <TASK id="01" status="PASS">Combat/Physiology grep completed; real owner is Gameplay/Combat/CombatDamageRuntime.cs.</TASK>
    <TASK id="02" status="PASS">Integrated through partial CombatDamageRuntime, not a standalone manager.</TASK>
    <TASK id="03" status="PASS">DoT damage enqueues CombatDamageSignal through SignalBus writer.</TASK>
    <TASK id="04" status="PASS">No active MonoBehaviour poison/bleed coroutine found; scanner added.</TASK>
    <TASK id="05" status="PASS">No managed Dictionary status timer found; timers are flat native lanes.</TASK>
    <TASK id="06" status="PASS">GenerateMockStatusEffectsJob added.</TASK>
    <TASK id="07" status="PASS">EvaluateStatusEffectsJob added with deterministic Burst.</TASK>
    <TASK id="08" status="PASS">Stun mobility scale resolved with bit extraction and math.lerp.</TASK>
    <TASK id="09" status="PASS">Toxic bubble feedback routes through BubbleSpawnSignal.</TASK>
    <TASK id="10" status="PASS">NativeQueue requests and Interlocked CAS OR mask application added.</TASK>
    <TASK id="11" status="PASS">Cadence lerps 1.0s to 0.1s by GlobalQualityWeight.</TASK>
    <TASK id="12" status="PASS">VFX AUP uses AbsoluteUniversePosition, no absolute float truncation.</TASK>
    <TASK id="13" status="PASS">DTOs are explicit 64B layouts, deterministic Burst jobs.</TASK>
    <TASK id="14" status="PASS">Transient telemetry/counter buffers use UninitializedMemory and deterministic overwrite.</TASK>
    <TASK id="15" status="PASS">300-entry status telemetry ring and dump path added.</TASK>
    <TASK id="16" status="PASS">FSM Status Effect Tuner window added.</TASK>
    <TASK id="17" status="PASS">Span-based CSV parser added, no float.Parse.</TASK>
    <TASK id="18" status="PASS">Live debug gizmo added.</TASK>
    <TASK id="19" status="PASS">OOP_Buff_Scanner added.</TASK>
    <TASK id="20" status="PASS">Layout verifier and this audit added.</TASK>
  </TASK_CHECK>
  <ARM64_CHECK>
    CombatStatusEffectRequest size=64, StatusEffectMask offset=8.
    CombatStatusEffectState size=64, StatusEffectMask offset=0, Durations0123 offset=8, Durations4567 offset=24, byte FSM pack offset=48.
    CombatStatusEffectTuning size=64.
    CombatStatusEffectTelemetryEntry size=64, StatusEffectMask offset=8.
  </ARM64_CHECK>
  <ZERO_GC_CHECK>
    Hot path uses NativeArray, NativeQueue, SignalBus writers, Interlocked, and Burst jobs.
    No LINQ, IEnumerator, StartCoroutine, managed status dictionary, boxing closure, or per-effect class allocation in edited runtime status path.
    Editor windows/scanner allocate only in UNITY_EDITOR cold tools.
  </ZERO_GC_CHECK>
  <AUP_CHECK>
    DoT truth is mask/timer data and has no position dependency.
    Toxic VFX resolves AbsoluteUniversePosition from runtime origin before BubbleSpawnSignal; no absolute double3-to-float truncation for signal payload.
  </AUP_CHECK>
  <VAULT_IDS>
    states=71260, telemetryRing=71261, telemetryCursor=71262, tuning=71263, counters=71264, profiles=71265, scannerReport=71266.
  </VAULT_IDS>
</SELF_AUDIT>

## 2026-05-22 - Subagent Defect Closure And Phase Polish

What was wrong:
- The first atomic mask writer used `ulong*` indexing against a 64B row array. Slot 1 would have targeted byte offset 8 inside row 0 instead of `StatusEffectMask@0` in row 1.
- Request slot values from `SlotByTargetId` were trusted against five native arrays without a local bounds fence.
- Completion unlocked Vault buffers before writing owner-side solve microseconds, aggregate telemetry, anomaly state, and dump evidence.
- Telemetry ring indexing used signed `%` / `math.abs`, which fails at `int.MinValue`.
- Toxic bubble VFX was published from the simulation worker. The cleaner route is native staging in the job and SignalBus publication from owner completion.
- Signal lanes could be cold-initialized by the first scheduled status tick if global bootstrap had not already touched them.

What was done:
- Replaced the broken mask pointer math with `AtomicOrStatusMask(slot, bits)` using `base + slot * 64`.
- Added slot validation in `ApplyStatusEffectRequestsJob` and evaluation-index validation in `EvaluateStatusEffectsJob`.
- Added 64B `CombatStatusEffectVfxRequest` and Vault BufferID `71267` for exact-AUP toxic bubble staging.
- Moved VFX publication to `PublishStatusEffectVfxRequests` after the job fence; the job writes only native rows.
- Moved Vault unlock to the end of completion after counters, telemetry, anomaly, VFX publish, and dump trigger.
- Changed telemetry ring math to unsigned modulo.
- Prewarmed/configured `SignalBus<CombatDamageSignal>` and `SignalBus<BubbleSpawnSignal>` in cold status storage bootstrap and guarded scheduling on `HasNativeStorage`.
- Updated `SHINOBU_319_STATUS_EFFECTS_ROUTE_CARD.md`, `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, status, and rationale.

Cinematic Cheats used:
- Poison bubbles remain a GPU/procedural downstream lie. CPU status math emits a 64B scalar/AUP request, not particles, transforms, prefabs, or components.
- Visual density is continuous: bubble cadence lerps from 48 frames at low quality toward 8 frames at full quality.
- Damage truth remains a SignalBus combat fact; VFX is a separate staged presentation fact.

Exact Microseconds saved:
- Atomic stride fix: no speed claim; it prevents cross-entity memory corruption.
- Slot bounds fence: sub-microsecond for normal small request batches; protects against stale maps.
- Owner-completion VFX staging: keeps the original 200+ us avoided estimate versus CPU particle/component bursts.
- Cold SignalBus prewarm: avoids first-hit native lane allocation during combat; exact hitch depends on platform allocator.
- Unsigned telemetry ring: no normal-frame speed gain; prevents rare long-session negative index fault.

Verification:
- Static scans found no edited runtime coroutine, `WaitForSeconds`, managed status dictionary, LINQ marker, `float.Parse`, `DamageSourceIds`, `BubbleSignalWriter`, `math.abs`, `Pack=1`, or runtime OOP status manager pattern in the status slice.
- Braces balanced: `CombatDamageRuntime_StatusEffects.cs` 131/131 and `StatusEffectsEditorFacade.cs` 45/45.
- No trailing whitespace in the two status source files or route card.
- `git diff --check` reported only existing CRLF warnings for tracked touched files.
- Build not launched: CPU guard sampled 100%; `dotnet.exe`, `csc.exe`, and `VBCSCompiler.exe` were absent, but the project rule forbids rebuild above 50% CPU.

<SELF_AUDIT>
  <TASK_CHECK>
    <TASK id="01" status="PASS">Repository archaeology found no active runtime poison/bleed coroutine under the requested Combat/Physiology surface; real integration owner is Gameplay/Combat/CombatDamageRuntime.cs.</TASK>
    <TASK id="02" status="PASS">Implemented as `CombatDamageRuntime` partial files, not a standalone status manager.</TASK>
    <TASK id="03" status="PASS">Status DoT emits `CombatDamageSignal`; health truth remains central combat damage route.</TASK>
    <TASK id="04" status="PASS">No MonoBehaviour buff path retained in touched status runtime; editor scanner guards regression.</TASK>
    <TASK id="05" status="PASS">No managed `Dictionary<EffectType,float>` timer path retained; timers live in flat native lanes.</TASK>
    <TASK id="06" status="PASS">`GenerateMockStatusEffectsJob` creates dense synthetic masks/timers for isolated stress.</TASK>
    <TASK id="07" status="PASS">`EvaluateStatusEffectsJob` is deterministic Burst with `[NoAlias]` native lanes.</TASK>
    <TASK id="08" status="PASS">Penalty path uses mask bit extraction and math interpolation; FSM byte resolution is branchless.</TASK>
    <TASK id="09" status="PASS">Toxic bubble decision runs in Burst; exact-AUP VFX request is staged as native data and published after fence.</TASK>
    <TASK id="10" status="PASS">External requests use `NativeQueue`; mask application uses Interlocked CAS OR at correct 64B row stride.</TASK>
    <TASK id="11" status="PASS">Status cadence continuously lerps by `GlobalQualityWeight` from 1.0s to 0.1s.</TASK>
    <TASK id="12" status="PASS">Damage/VFX AUP reads target root `double3` from Vault; no absolute float truncation before signal payload.</TASK>
    <TASK id="13" status="PASS">Authoritative state/tuning/request/telemetry/counter/VFX DTOs are explicit unmanaged layouts and deterministic jobs.</TASK>
    <TASK id="14" status="PASS">Telemetry/counter/VFX lanes use uninitialized Vault storage and deterministic owner overwrite; semantic state lane is cleared.</TASK>
    <TASK id="15" status="PASS">300-entry telemetry ring records active count, request count, damage milli, VFX count, bit extraction estimate, state hash, anomaly, and solve microseconds; dump path exists.</TASK>
    <TASK id="16" status="PASS">UI Toolkit tuner reads/writes Vault-backed tuning in editor.</TASK>
    <TASK id="17" status="PASS">CSV profile parser is editor/cold only and avoids `float.Parse`/LINQ.</TASK>
    <TASK id="18" status="PASS">Scene View debug bars read raw masks from Vault mirrors, not status GameObjects.</TASK>
    <TASK id="19" status="PASS">`OOP_Buff_Scanner` emits the optimization report artifact.</TASK>
    <TASK id="20" status="PASS">Layout verifier, route card, ledger entry, rationale, status, and this audit are on disk.</TASK>
  </TASK_CHECK>
  <STRUCT_LAYOUT>
    <DTO name="CombatStatusEffectState" size="64">StatusEffectMask ulong @0 size8; Durations0123 float4 @8 size16; Durations4567 float4 @24 size16; LastAppliedFrame uint @40 size4; LastChangedFrame uint @44 size4; FSM bytes @48..55 size8; StateHash uint @56 size4; Reserved uint @60 size4.</DTO>
    <DTO name="CombatStatusEffectRequest" size="64">TargetId int @0; SourceId int @4; StatusEffectMask ulong @8; DurationSeconds float @16; Magnitude float @20; ImpactAup double3 @24 size24; Frame uint @48; DamageType uint @52; Flags/padding @56..63.</DTO>
    <DTO name="CombatStatusEffectCounterLane" size="64">Value int @0; explicit padding @4..63 prevents false sharing for Interlocked counters.</DTO>
    <DTO name="CombatStatusEffectVfxRequest" size="64">PositionAup double3 @0 size24; Intensity01 @24; RadiusMeters @28; Frame @32; SourceHash @36; EffectHash @40; Flags @44; padding @48..63.</DTO>
  </STRUCT_LAYOUT>
  <SCALABILITY>GlobalQualityWeight changes cadence, job batch size, and toxic bubble cadence continuously. Below 0.3, status integration batches toward 1.0s and bubble cadence tends toward 48 frames while preserving integrated damage. At high/ultra quality, cadence tightens toward 0.1s and bubble cadence toward 8 frames; gameplay truth, DTO layout, save identity, and authority route remain unchanged.</SCALABILITY>
  <H_PHI_VAULT_STATUS>Persistent status state, tuning, telemetry, counters, and VFX staging are Vault buffers `71260..71264` and `71267`; request ingress is a bounded native queue prewarmed by the combat owner. No status effect class/component/timer owns gameplay truth.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Consumes the combat damage job guard and armor target AUP Vault view; schedules `ApplyStatusEffectRequestsJob` then `EvaluateStatusEffectsJob`; registers the resulting JobHandle with H8Memory. `[NoAlias]` is present on non-overlapping job lanes. Parallel telemetry/counter/VFX writes use Interlocked or unique reserved slots.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling asmdef reference was added; status code sits in the existing combat partial and routes cross-domain output through Core SignalBus payloads and Vault buffer IDs.</COMPILE_GUARD>
  <DEAR_LIE>Before: object/coroutine poison visuals trend toward O(active effects) managed updates plus CPU particle/component churn. After: O(active rows) Burst scalar math plus bounded 64B VFX request rows; geometry remains downstream procedural VFX.</DEAR_LIE>
</SELF_AUDIT>

## 2026-05-22 - Build Guard Recheck

What was checked:
- Re-read status/rationale and re-extracted the `SHINOBU_319` XML prompt from `CURRENT_BATCH.md`.
- Verified `BufferID 71267` is unique in `H8Memory.cs`.
- Verified `Hecton8.Core.csproj` contains `CombatDamageRuntime_StatusEffects.cs`; runtime partial is covered by the generated project.
- Parsed `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` and confirmed `shinobu319StatusEffectsScanner`.
- Parsed `Docs/Reports/SHINOBU_319_SELF_AUDIT.xml`.

Build decision:
- No build launched. CPU later sampled at `50`, but `dotnet.exe` PID `10956` and `VBCSCompiler.exe` PID `2036` were active. Project guard forbids launching rebuild while another compiler is running.
- Second waited guard probe: CPU `85`, `VBCSCompiler.exe` PID `2036` still active. Rebuild remains forbidden.

## 2026-05-22 - NaN Vaccination Pass

What was wrong:
- Timer and DPS math assumed Vault rows and tuning rows were finite. A corrupted save/rollback/import row could produce NaN duration or damage and poison telemetry or signal payloads.

What was done:
- `EvaluateStatusEffectsJob` now sanitizes duration `float4` packs before decrement.
- DPS and raw damage are finite-guarded before damage signal emission.

Microseconds:
- Added SIMD finite masks and scalar finite checks. Expected cost is below status cadence savings; no measured profiler proof because build/profiler remain gated.

## 2026-05-22 - Compile Guard Recheck

What was checked:
- Waited another 45 seconds after the previous guard block.
- CPU sampled at `80`.
- Active compiler infrastructure remained: `dotnet.exe` PID `16376`, `VBCSCompiler.exe` PID `2036`.

Decision:
- No rebuild launched. Project guard forbids build while CPU is above 50% or another compiler process is active.

## 2026-05-22 - Second Polish Pass

What was wrong:
- `TryQueueStatusEffect` still invoked owner initialization, allowing a first-hit allocation route from a gameplay write facade.
- Request-only slow ticks scheduled the full `EvaluateStatusEffectsJob` with zero delta.
- `ResolveFsmByte` still used C# control flow instead of byte FSM select math.
- The status quality path still inherited the old binary requested-math-LOD multiplier through `_visualQualityWeight01`.
- The editor scanner could overwrite the shared physics optimization report and delete other agents' keys.
- The dedicated XML audit did not list every status DTO layout.

What was done:
- Status ingress now fails closed unless prewarmed by owner boot.
- Request-only frames schedule only `ApplyStatusEffectRequestsJob`; O(MaxTargets) evaluation waits for continuous cadence debt.
- FSM byte resolution uses `math.select` and finite clamping.
- Status cadence/batch/VFX quality now uses `SignalBusRegistry.GlobalQualityWeight01` directly.
- Counter clearing moved after Vault lock acquisition.
- `OOP_Buff_Scanner` now merges only `shinobu319StatusEffectsScanner` and writes `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_319.json`.
- `SHINOBU_319_SELF_AUDIT.xml` now includes request, state, tuning, telemetry, counter, and VFX layouts.

Cinematic Cheats used:
- Request-only frames do no DoT/VFX target sweep. The visual bubble lie remains staged until cadence says presentation work is due.

Exact Microseconds saved:
- First-hit allocation route removed: hitch avoided, platform-specific.
- Request-only frame collapse: estimated 20-60 us per 2k targets.
- Branchless byte FSM: removes two control-flow decisions per refreshed effect byte.

Verification:
- Build not launched after this pass because the latest CPU probe sampled 93%, above the project gate.

## 2026-05-22 - Runtime Fence And Damage Staging Pass

What was wrong:
- `CanMutateTargets()` could finalize a completed status job without calling `CompleteStatusEffectFrame()`, losing queued counts, VFX publication, telemetry, dump decisions, and Vault unlock discipline.
- `EvaluateStatusEffectsJob` published DoT through a worker SignalBus writer, mixing simulation math with owner publication and bypassing completion-phase backpressure.
- Status simulation read armor target AUP rows without borrowing the armor Vault lock for the lifetime of the scheduled job.
- Completion telemetry used the accumulator after it had been reset, so solve rows could report `DeltaTime=0` for real integration frames.

What was done:
- `CanMutateTargets()` now fails closed while `_statusJobScheduled` is true. Status completion remains in `LateFrameTick`/shutdown where `CompleteStatusEffectFrame()` runs.
- Added Vault buffer `71268 Shinobu319StatusEffectDamageSignals` and a ninth 64B padded counter lane for Interlocked damage-signal slot reservation.
- `EvaluateStatusEffectsJob` writes existing 64B Core `CombatDamageSignal` rows to Vault; owner completion publishes `SignalBus<CombatDamageSignal>` after the job fence.
- SignalBus damage backpressure now writes anomaly hash `0x5319D001`; missing native storage writes `0x5319D002` instead of silently losing a staged gameplay damage packet.
- Status evaluation borrows armor Vault locks when it reads `TargetRootAups`, then releases them in status completion.
- `BubbleSpawnSignal` availability no longer gates status request application or DoT simulation; missing VFX native storage suppresses only the toxic bubble presentation.
- `OOP_Buff_Scanner` report regeneration now emits the current `71268` damage-staging proof, self-audit path, route-card path, and compile-guard field instead of downgrading the sidecar schema on the next menu run.
- Completion telemetry now records `_statusLastEvaluationDeltaSeconds`, captured before accumulator reset.
- Route card, binary ledger, self-audit XML, and scanner JSON now document the `71268` staging route.

Cinematic Cheats used:
- Poison bubble visuals remain 64B scalar/AUP staging to VFX, not CPU particle or GameObject simulation. Damage truth and visual truth now both publish from owner completion after Burst math.

Exact Microseconds saved:
- No new speed claim for the fence fix; it prevents lost completion proof.
- Worker SignalBus writer removed from the hot job. Replacement cost is one contiguous 64B Vault write per active DoT row plus one owner-side publish loop over staged rows.
- Borrowed armor lock has no speed claim; it prevents cross-domain AUP race without creating a shadow target-position owner.

Verification:
- Static source pass only. Build guard not re-run yet after this patch block.

## 2026-05-22 - Static Verification After Runtime Fence Pass

What was checked:
- Forbidden runtime scan for `DamageSignalWriter`, `NativeDisableContainerSafetyRestriction`, status coroutine names, `Pack=1`, `math.abs(`, and old config calls returned only scanner string literals inside `StatusEffectsEditorFacade.cs`.
- `PHYSICS_OPTIMIZATION_REPORT.json` and `PHYSICS_OPTIMIZATION_REPORT_SHINOBU_319.json` parse as JSON; shared key `shinobu319StatusEffectsScanner` exists.
- `SHINOBU_319_SELF_AUDIT.xml` parses as XML.
- Code-aware brace scan ignoring string/comment braces: `CombatDamageRuntime_StatusEffects.cs` `142/142`; `StatusEffectsEditorFacade.cs` `51/51`.
- `git diff --check` reported line-ending warnings only for tracked touched files.

Build decision:
- No build launched. CPU sampled at `65`, and active `dotnet.exe` processes were `1716`, `5652`, `13176`, `15352`, `19416`, `21912`, `22460`. Project guard forbids rebuild above 50% CPU or while compiler processes are active.

## 2026-05-22 - Build Guard Wait Probe

What was checked:
- Waited 45 seconds.
- CPU sampled at `35`.
- Active `dotnet.exe` processes remained: `1716`, `5652`, `13176`, `15352`, `19416`, `21912`, `22460`.

Decision:
- No build launched. CPU is below threshold, but the project guard also forbids rebuild while compiler processes are active.

## 2026-05-22 - Gated Build Probe

What was checked:
- CPU guard sampled `39`.
- No active `dotnet.exe`, `csc.exe`, or `VBCSCompiler.exe` process was reported.
- Launched one compile probe: `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`.

Result:
- Build failed in unrelated gameplay files:
  - `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs`: missing `VRSomaticKinematicStateMirrorDTO` and `VRSomaticComfortDTO`.
  - `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime_HandIK.cs`: missing `PlayerHandIkConfigFlags`.
- No SHINOBU_319 runtime/editor/status file was named in the compiler errors.

Decision:
- Stop at one build attempt. This is an external compile wall outside the status-effects domain.
- Do not patch VR somatic or player kinematics symbols from SHINOBU_319.

## 2026-05-22 - Post-Wall Proof Refresh

What was checked:
- `PHYSICS_OPTIMIZATION_REPORT.json` and `PHYSICS_OPTIMIZATION_REPORT_SHINOBU_319.json` parse as JSON.
- `SHINOBU_319_SELF_AUDIT.xml` parses as XML.
- Stale CPU-gate compile-proof strings are absent from SHINOBU_319 reports and scanner generator.
- Forbidden owned-runtime scan for worker damage writers, SignalBus config ownership, `Pack=1`, `math.abs(`, and stale accumulator telemetry returned no hits.
- Scoped `git diff --check` reports only line-ending warnings in tracked files already carrying CRLF normalization drift.

Decision:
- Keep compile proof as external compile wall evidence. Do not launch a second build.

## 2026-05-22 - Request-Only Dependency Isolation

What was wrong:
- `TryScheduleStatusEffectJobs` resolved and refreshed armor `TargetRootAups` before it knew whether the frame needed `EvaluateStatusEffectsJob`.
- This let a spatial/AUP lane block pure `StatusEffectMask` request application even when cadence debt had not matured.

What was done:
- Armor view resolution, AUP refresh, and armor Vault lock now happen only inside the `hasSimulationWork` branch.
- Status Vault locking is split: request-only frames lock five required buffers; simulation frames lock seven including VFX and damage staging.
- Request-only frames keep the intended route: queue drain -> Interlocked mask OR -> timer refresh -> completion proof.

Cinematic Cheats used:
- None added. This preserves the existing cadence collapse by keeping request-only frames O(queued requests), not O(MaxTargets) plus AUP snapshot work.

Exact Microseconds saved:
- Estimated 5-25 us per request-only frame on low-end CPUs by avoiding unnecessary armor Vault resolution, target AUP refresh, and two unused Vault lock attempts.

## 2026-05-22 - Active Telemetry Counter Repair

What was wrong:
- `StatusEffectCounterActive` was updated only for rows that emitted damage/change/anomaly results.
- Stable live status rows could be missing from completion telemetry.

What was done:
- Active row counting now happens before the result early-out.
- Result counting remains separate and still tracks only rows with emitted result payloads.

Cinematic Cheats used:
- None. This is blackbox correctness.

Exact Microseconds saved:
- No speed claim. One Interlocked add per live evaluated row buys correct 300-frame forensic pressure data.

## 2026-05-22 - Owner-Folded Telemetry Ring And Reserved ID Correction

What was wrong:
- The parallel status evaluator wrote the 300-entry telemetry ring using an Interlocked cursor plus modulo wrap. A single pass with more than 300 result rows could reuse a slot inside the same `IJobParallelFor`.
- The dump writer exported raw ring index order instead of cursor-ordered chronological order.
- Docs overclaimed `71265`/`71266` as active BufferIDs even though CSV/scanner proof uses cold editor filesystem routes.

What was done:
- Removed telemetry ring writes from `EvaluateStatusEffectsJob`.
- Owner completion now folds per-result telemetry from `ResultsBySlot`/`ResultActiveBySlot` after the job fence, then writes the completion summary.
- Dump export starts at the ring cursor when wrapped.
- `ResultActiveBySlot[index]` is cleared before full evaluation-index validation to prevent stale post-fence telemetry fold rows.
- Ledger and route card now mark `71265`/`71266` reserved-only; active buffers remain `71260..71264`, `71267`, and `71268`.

Cinematic Cheats used:
- None. This is telemetry determinism and proof honesty.

Exact Microseconds saved:
- No speed claim. Parallel ring contention is removed; owner fold is bounded by emitted result rows.

Verification:
- Subagent findings were addressed: parallel ring write removed, dump order cursor-based, `71265`/`71266` documented as reserved-only.
- JSON reports parse; self-audit XML parses.
- Forbidden owned-runtime scan found no telemetry ring writer in `IJobParallelFor`, no worker damage writer, no SignalBus config ownership, no `Pack=1`, and no `math.abs(`.
- Scoped `git diff --check` reports only a ledger LF/CRLF warning.
- Rebuild not rerun: CPU sampled at `74` with no active compiler processes, above the project rebuild threshold. Previous legal build probe still documents the unrelated VR somatic/player kinematics compile wall.
