# LOG_SHINOBU_352

## 2026-05-23 VOCAL_WARNING_SYSTEM_AUDIO_QUEUE

What was wrong:
- `VocalWarningSystem` used a `NativeArray<byte>` queue and insertion sort. Priority was enum order, not mathematical severity.
- Active playback preemption was `nextId < activeId`, so it could not prove hull/water breach beats battery by score.
- VWS published subtitle signals but also called `SubtitleManager.DisplaySubtitle(...)` with fallback managed strings.
- Pending warning data was not the mandated 16-byte `VocalWarningDTO` heap.
- Black-box dump path was generic `Dump_AUDIO_VWS_SYSTEM.bin`, not SHINOBU_352-specific.

What was done:
- Replaced the byte queue with Vault-backed `NativeMinHeap<VocalWarningDTO>`.
- Implemented `VocalWarningDTO` exact ABI: offset 0 `uint AudioBankHashID`, offset 4 `float PriorityScore`, offset 8 `float ExpirationTime`, offset 12 `uint Flags`, size 16.
- Added `EvaluateWarningPrioritiesJob`, `DispatchVoiceOverJob`, and `GenerateMockVocalThreatsJob` with `BurstCompile(CompileSynchronously=true)`.
- Added critical-first signal evaluation for flood/fluid/pipe/oxygen/crush before low-priority battery/brownout lanes.
- Set hull breach/water breach base priority to 1000 + critical boost; power low base priority is 120. Interruption requires `candidate > current + 180` plus interrupt flags.
- Dispatch now emits `VocalCueSignal` and hash-only `SubtitleCueSignal`; VWS no longer creates subtitle text or managed warning strings in the hot path.
- Added AUP direction hash using double-precision grid/local subtraction before localized float3 `atan2`.
- Added 300-entry Vault telemetry and dump path `Docs/AgentLogs/Dump_SHINOBU_352.bin`.
- Added UI Toolkit tuner, editor gizmo, OOP voice scanner, SHINOBU_352 report, and architecture route card.
- Added new Vault buffer IDs: 72430 heap state, 72431 current state, 72432 dispatch, 72433 profiles, 72434 CSV scratch.

Cinematic cheats used:
- No physical audio simulation. The queue emits one mathematical voice command; downstream DSP handles playback.
- Directional callouts store compact compass hashes, not dynamic localized strings.
- Radio distortion/spatial blend scale continuously with `GlobalQualityWeight`; gameplay truth remains unchanged.

Exact microseconds saved or spent:
- Old managed subtitle fallback removed: estimated low tens of microseconds per dispatch and 0 B hot-path string work.
- Heap insert/pop: O(log 64), estimated sub-10us per operation.
- Dense mock 50-threat insert: estimated 35-80us.
- Evaluation job: estimated 20-120us depending on `MaxEvaluations`.
- Telemetry row write: estimated under 5us per frame.
- Critical interruption decision: estimated under 10us per dispatch.
- AUP direction pass: low-tier bound is 8 directional `atan2` calls, estimated under 20us on i3/MX350.

Verification:
- `rg` found no runtime `PlayOneShot(`, `Queue<AudioClip`, `Queue<Voice`, `List<Voice`, or `DisplaySubtitle(` matches in Audio/Gameplay/Physiology with Editor excluded.
- `git diff --check` passed for SHINOBU_352 touched files; line-ending warnings only.
- First `dotnet build Hecton8.slnx --no-restore` launched legally at CPU 41 with no existing dotnet/csc. It failed on missing `Temp/obj/*/project.assets.json`, many unrelated owner errors, and SHINOBU_352 `CS8156`.
- SHINOBU_352 `CS8156` errors were fixed by copying `NativeArray` indexer values into local DTOs before `in` comparisons.
- Build-server shutdown succeeded. Retry blocked because CPU later sampled 82-93% and project law forbids starting another build above 50%.

Integrator notes:
- Unrelated compile walls remain in VRSomatic, airlock, combat status effects, PDA projector, haptic input, gyro, tether, SignalWarden, and package restore state. They are not owned by SHINOBU_352.
- `Docs/Reports/AUDIO_OPTIMIZATION_REPORT.json` already exists as SHINOBU_339 output. SHINOBU_352 wrote `Docs/Reports/AUDIO_OPTIMIZATION_REPORT_SHINOBU_352.json` to avoid destroying another agent artifact.

<SELF_AUDIT agent="SHINOBU_352">
  <TASKS>
    <TASK id="01" status="PASS" proof="rg archaeology and existing VWS owner identified" />
    <TASK id="02" status="PASS" proof="integrated existing VocalWarningSystem" />
    <TASK id="03" status="PASS" proof="existing VocalCueSignal and SubtitleCueSignal reused" />
    <TASK id="04" status="PASS" proof="runtime OOP voice scan returned zero matches in scoped dirs" />
    <TASK id="05" status="PASS" proof="pending queue moved to NativeMinHeap&lt;VocalWarningDTO&gt;" />
    <TASK id="06" status="PASS" proof="GenerateMockVocalThreatsJob implemented" />
    <TASK id="07" status="PASS" proof="EvaluateWarningPrioritiesJob implemented" />
    <TASK id="08" status="PASS" proof="heap insert/peek/pop/discard implemented" />
    <TASK id="09" status="PASS" proof="DispatchVoiceOverJob interrupt threshold and flags implemented" />
    <TASK id="10" status="PASS" proof="MaxEvaluations round(lerp(8,64,GlobalQualityWeight)) implemented" />
    <TASK id="11" status="PASS" proof="AUP double delta before float3 direction hash" />
    <TASK id="12" status="PASS" proof="rollback exclusion documented" />
    <TASK id="13" status="PASS" proof="Vault handles use UninitializedMemory" />
    <TASK id="14" status="PASS" proof="hash-only subtitle signal synchronized with voice cue" />
    <TASK id="15" status="PASS" proof="300-entry telemetry ring and Dump_SHINOBU_352.bin path" />
    <TASK id="16" status="PASS" proof="VocalWarningQueueTunerWindow UI Toolkit file" />
    <TASK id="17" status="PASS" proof="ReadOnlySpan byte CSV parser to NativeArray profile table" />
    <TASK id="18" status="PASS" proof="VocalWarningQueueDebugGizmo editor overlay" />
    <TASK id="19" status="PASS" proof="OOP_Voice_Scanner_SHINOBU_352 and report artifact" />
    <TASK id="20" status="PARTIAL" proof="layout/source/diff/build attempt done; retry blocked by CPU after local CS8156 fix" />
  </TASKS>
  <ARM64_CHECK>
    VocalWarningDTO size=16 offsets=0:uint AudioBankHashID,4:float PriorityScore,8:float ExpirationTime,12:uint Flags.
    HeapState size=16. CurrentState size=64. DispatchDTO size=80. TelemetryEntry size=64. ProfileDTO size=32.
  </ARM64_CHECK>
  <ZERO_GC_CHECK>
    VWS hot frame path uses NativeArray, SignalBus snapshots, IJob.Run, primitive structs, and no LINQ/managed queues/fallback strings/direct AudioSource playback.
  </ZERO_GC_CHECK>
  <AUP_CHECK>
    Direction hash subtracts listener/threat grid/local AUP as double meter delta before localized float3 atan2.
  </AUP_CHECK>
  <ROLLBACK_CHECK>
    Voice queue/current/dispatch/profile/telemetry lanes are presentation-only and excluded from StateRingBuffer/Merkle/save truth by architecture route card.
  </ROLLBACK_CHECK>
</SELF_AUDIT>

## 2026-05-23 VOCAL_WARNING_SYSTEM_AUDIO_QUEUE SOURCE BOUNDARY AUDIT

What was wrong:
- Task 14 uses `SubtitleCueSignal`; source review found a second same-name DTO in `ModdingAPI/FutureCommandSandboxValidator.cs`, and the project has many child Audio/UI asmdefs. This needed proof before claiming route safety.
- A fresh compile is still desirable after the subtitle route correction, but active compiler workers are present.

What was done:
- Verified `Hecton8.Modding.SubtitleCueSignal` is a separate mod sandbox payload. VWS references `Hecton8.Core.Contracts.Signals.SubtitleCueSignal`.
- Verified root `Assets/_Project/Scripts/Audio/VocalWarningSystem.cs` and root `Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs` are covered by the parent Core assembly surface, not by sibling `Hecton8.Audio.*` or `Hecton8.UI.*` asmdefs.
- Verified existing source already uses `[ReadOnly, NoAlias] NativeArray<T>.ReadOnly` job fields and `FileStream.Write(ReadOnlySpan<byte>)` raw dump paths, so VWS follows established project API patterns.
- Re-extracted the SHINOBU_352 XML block: `XML_OK length=25005`.
- Re-sampled build gate: CPU `15.3%`, then `39.8%`; active `dotnet` workers stayed at `7`; no build was launched.

Cinematic cheats used:
- No new simulation. The queue still emits one prioritized hash command and subtitle token; DSP/UI presentation remains downstream.

Exact microseconds saved or spent:
- Runtime 0us. This was a compile-wall and route-ownership audit.
- Avoided a needless contract-file move and sibling assembly churn; iteration-time risk reduced, no player frame cost.

Verification:
- `rg` proves the mod `SubtitleCueSignal` is in `namespace Hecton8.Modding`; the UI owner signal is in `namespace Hecton8.Core.Contracts.Signals`.
- `rg` proves no child Audio/UI asmdef owns the touched root runtime files.
- Post-log static checks: focused `git diff --check` passed with line-ending warnings only; `AUDIO_OPTIMIZATION_REPORT_SHINOBU_352.json` and shared `AUDIO_OPTIMIZATION_REPORT.json` parse; focused VWS forbidden-route scan returned no matches.
- No green compile is claimed. Task 20 remains pending guarded compile verification after the external dotnet worker wall clears.

## 2026-05-23 04:43 +04:00 - SubtitleCueSignal Route Correction

What was wrong:
- Task 14 names `SubtitleCueSignal`, but the current proof surface still described legacy `SubtitleSignal`.
- `VocalWarningSystem` had begun publishing `SubtitleCueSignal`, but it lacked the bounded duration/flag conversion helpers needed to keep the cue payload primitive and finite.
- VWS cannot legally read `BabelSubtitleSyncRuntime.CurrentAudioFrame`; that would create a concrete UI dependency in the audio queue owner.

What was done:
- Added `ResolveSubtitleDurationMilliseconds` and `ResolveSubtitleCueFlags` to pack `VocalWarningDispatchDTO` into the existing 16-byte `SubtitleCueSignal` payload.
- `PublishDispatchIfNeeded` now publishes `SignalBus<SubtitleCueSignal>.TryPush` directly with `StartAudioFrame=0`.
- `BabelSubtitleSyncRuntime.DrainCueSignals` now resolves `StartAudioFrame == 0` to its owner-local `s_audioFrameClock` before registration.
- Updated SHINOBU_352 status, rationale, route card, binary payload ledger, and audio optimization reports to name `SubtitleCueSignal`.

Cinematic cheats used:
- No UI string, clip object, or subtitle text is created in VWS. The audio queue emits a hash/token cue; the subtitle owner performs the visual accessibility illusion from its own clock and localization route.

Exact microseconds saved or spent:
- Subtitle duration clamp and flag pack: static estimate sub-1us per accepted warning on i3/MX350.
- Avoided concrete UI clock lookup from VWS: 0us runtime dependency cost and lower compile-wall risk.
- Legacy bridge removal remains a low single-digit microsecond path simplification per accepted warning; profiler proof is still absent.

Verification:
- Source now contains `SignalBus<SubtitleCueSignal>.TryPush` in VWS and no `SignalBus<SubtitleSignal>` in VWS.
- UI owner now contains `StartAudioFrame != 0u ? signal.StartAudioFrame : s_audioFrameClock`.
- No green compile is claimed; Task 20 remains PENDING VERIFICATION behind the existing external compile wall until a legal guarded build can run.

<SELF_AUDIT_DELTA agent="SHINOBU_352" phase="subtitle-cue-correction">
  <TASK id="03" status="PASS" proof="existing VocalCueSignal and SubtitleCueSignal are reused directly via typed SignalBus lanes" />
  <TASK id="14" status="PASS" proof="dispatch publishes synchronized VocalCueSignal and hash-only SubtitleCueSignal; subtitle owner resolves StartAudioFrame sentinel" />
  <STRUCT_LAYOUT proof="SubtitleCueSignal remains existing 16-byte UI-owned lane; VWS primary VocalWarningDTO remains explicit 16 bytes at offsets 0/4/8/12" />
  <ZERO_GC proof="VWS cue conversion uses only primitive math/bit flags; no managed strings, clips, queues, LINQ, or UI text calls" />
  <COMPILE_GUARD proof="No assembly reference was added; one small UI owner patch avoids concrete runtime dependency from Audio to UI" />
</SELF_AUDIT_DELTA>

## 2026-05-23 04:46 +04:00 - Build Gate Sample

What was wrong:
- A compile retry is still needed after the subtitle route correction, but the build gate must be checked before invoking `dotnet`.

What was done:
- Sampled CPU and build processes only. CPU was 30.5%, but seven active `dotnet` workers were present.
- No build command was launched.

Cinematic cheats used:
- None. This is compile discipline only.

Exact microseconds saved or spent:
- Runtime 0us. Avoided adding compile IO/CPU contention while another build/tool process set is active.

Verification:
- Task 20 remains PENDING VERIFICATION. Static route, JSON, and diff checks passed; compile proof is still blocked by active `dotnet` workers plus the previously observed external project/assets wall.

## 2026-05-23 04:49 +04:00 - Static Recheck After Cleanup

What was wrong:
- One heap fault line had bad indentation after prior raw-ref heap edits. It was cosmetic but made review harder.

What was done:
- Fixed the indentation in `VocalWarningHeapOps.Insert`.
- Re-ran targeted VWS forbidden-token scan: no legacy subtitle lane, `GlobalSignals.Publish`, `DisplaySubtitle`, Unity time, `BinaryWriter`, hidden `.Complete()`, managed voice queues, or `AudioSource.PlayOneShot` matches in `VocalWarningSystem.cs`.
- Re-ran `git diff --check`; it passed with CRLF warnings only.
- Re-sampled build gate: CPU 63.0%, seven active `dotnet` workers.

Cinematic cheats used:
- None beyond the existing hash-only audio/subtitle route.

Exact microseconds saved or spent:
- Runtime 0us. Cleanup only.

Verification:
- Build remains PENDING VERIFICATION. No build command was launched because the gate is forbidden by both CPU and active build workers.

## 2026-05-23 VOCAL_WARNING_SYSTEM_AUDIO_QUEUE CONTEXT-RESUME AUDIT

What was wrong:
- Context resumed with a stale risk: the first narrow XML regex failed because the `AGENT_PROMPT` tag includes `role` and `chat_name` attributes.
- Task 20 still lacks a fresh green compile because the development machine is saturated by other compiler workers.

What was done:
- Re-read `Docs/Tasks/Status_SHINOBU_352.md`, `Docs/AgentLogs/Rationale_SHINOBU_352.md`, `AGENTS.md`, and the relevant registry mandates.
- Re-extracted the full SHINOBU_352 XML block with an attribute-aware regex: `XML_OK length=25005`.
- Re-sampled build gate: CPU moved from 99.6 with seven `dotnet` workers to 58.2 with zero `dotnet/csc` workers. No build launched because CPU remains above the 50% gate.
- Re-ran static checks for runtime forbidden patterns, raw heap/state refs, direct typed `SignalBus<T>.TryPush`, JSON parse, and diff hygiene.

Cinematic cheats used:
- No new simulation added. The existing Dear Lie remains one priority comparison plus one interrupt flag; DSP/UI downstream creates the perceived audio cut and subtitle sync.

Exact microseconds saved or spent:
- Runtime delta this pass: 0us.
- Build-gate compliance avoids adding a full solution compile to a 99.6% CPU machine; iteration preservation only, not gameplay optimization.

Verification:
- `VocalWarningSystem.cs` scan found no runtime `Time.frameCount`, `Time.deltaTime`, `BinaryWriter`, `GlobalSignals.Publish`, `.Complete(`, `Pack=1`, `TryGetLatestCreated`, `AudioSource.PlayOneShot`, or `DisplaySubtitle`.
- Raw writeback scan found no `CurrentState[0] =`, `Dispatch[0] =`, `HeapState[0] =`, `Cooldowns[...] =`, `WarningFlags[...] =`, `WarningSeverity[...] =`, or `WarningSourceIds[...] =` assignments.
- `Docs/Reports/AUDIO_OPTIMIZATION_REPORT_SHINOBU_352.json` and shared `AUDIO_OPTIMIZATION_REPORT.json` parsed as JSON.
- `git diff --check` passed for SHINOBU_352 touched files; only CRLF warnings were reported.

Integrator notes:
- No fresh compile proof is claimed.
- Current blocker is policy, not a newly observed SHINOBU_352 compiler error: latest gate is CPU 58.2 and zero active `dotnet/csc` workers.

## 2026-05-23 VOCAL_WARNING_SYSTEM_AUDIO_QUEUE GUARDED COMPILE RETRY

What was wrong:
- Task 20 still needed a compile retry after the SHINOBU_352 `CS8156` fix.

What was done:
- Waited for the build gate to open: CPU 36.3, `dotnet/csc=0`.
- Ran `dotnet build Hecton8.slnx --no-restore`.
- Ran `dotnet build-server shutdown` after the failed attempt; MSBuild and VB/C# compiler servers shut down successfully.

Compile result:
- Build failed.
- External/generated project wall: missing `project.assets.json` under `Temp/obj/Assembly-CSharp-Editor-firstpass`, `Assembly-CSharp-Editor`, `Crest.Helpers.Editor`, `Crest`, `MapMagic.Settings`, and `Technie.PhysicsCreator.Updater`.
- Unrelated owner wall: `Assets/_Project/Scripts/Construction/HatchLockJobs.cs(12,45)` and `Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime_HatchLocks.cs(15,45)` both fail because namespace `Hecton8.Habitat` is not visible from `Hecton8.Core.csproj`.
- Visible build output showed no new SHINOBU_352-local compiler error.

Cinematic cheats used:
- None added in this pass. Existing route remains: scalar priority heap -> one cue hash -> downstream DSP/UI presentation.

Exact microseconds saved or spent:
- Runtime delta: 0us.
- Compile attempt time: 46.84 seconds wall clock, blocked by external project/assets and unrelated core namespace errors.

Integrator notes:
- No green compile is claimed.
- Do not fix the construction/habitat namespace error inside SHINOBU_352; it belongs to another owner.

## 2026-05-23 VOCAL_WARNING_SYSTEM_AUDIO_QUEUE DIRECT SIGNALBUS / RAW-REF POLISH

What was wrong:
- Final dispatch still used `GlobalSignals.Publish(in cue)` and `GlobalSignals.Publish(in subtitle)`. It was hash-only and guarded downstream, but it was not the cleanest proof of the first-party hot typed corridor.
- Owner state writes still had several `NativeArray` indexer writebacks for current state, dispatch row, telemetry row, cooldowns, flags, severity, source ids, and cold initialization. This left a CS1612/raw-mutation proof gap even though no managed allocation was present.

What was done:
- `PublishDispatchIfNeeded` now writes directly to `SignalBus<VocalCueSignal>.TryPush` and `SignalBus<SubtitleCueSignal>.TryPush`.
- Rejected cue/subtitle publication sets Vault heap fault bits: `FaultFlagVocalCueRejected` and `FaultFlagSubtitleRejected`.
- If the cue lane rejects a packet, current playback state is cleared and the dispatch row is discarded so telemetry does not claim an active voice line that the audio lane never accepted.
- Owner writes for initialization, clear, telemetry, dispatch, current state, cooldown, warning flags, severity, and source ids now use `NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks` plus `UnsafeUtility.AsRef`.
- `DispatchVoiceOverJob` writes `CurrentState` and `Dispatch` through raw refs. `EvaluateWarningPrioritiesJob` writes cooldown/flag/severity/source rows through raw refs.
- Route card, binary ledger, status, and rationale were updated.

Cinematic cheats used:
- The CPU still performs no audio mixing, fade curves, AudioSource arbitration, or UI text work. The sentience illusion remains one heap pop, one priority threshold, one interrupt flag, and one typed hash cue.

Exact microseconds saved or spent:
- Direct typed dispatch removes one `GlobalSignals` wrapper hop per cue/subtitle. Static estimate: 1-3us per accepted dispatch on i3/MX350 pending profiler proof.
- Raw state refs remove repeated NativeArray indexer writeback patterns in the hot owner route. Static estimate: low single-digit microseconds on dense warning frames pending Burst Inspector/profiler proof.
- Rejected-cue current-state clearing is fault-path only and prioritizes truth in telemetry over pretending playback succeeded.

Verification:
- Re-read `Status_SHINOBU_352.md`, `Rationale_SHINOBU_352.md`, and re-extracted the full SHINOBU_352 XML block from `CURRENT_BATCH.md` before this pass.
- `rg` found no `GlobalSignals.Publish(in cue)` or `GlobalSignals.Publish(in subtitle)` in `VocalWarningSystem.cs`.
- `rg` found no hot owner writeback patterns for `CurrentState[0] =`, `Dispatch[0] =`, `HeapState[0] =`, `Cooldowns[...] =`, `WarningFlags[...] =`, `WarningSeverity[...] =`, or `WarningSourceIds[...] =`.
- `rg` found no `Time.frameCount`, `Time.deltaTime`, `BinaryWriter`, `Queue<AudioClip>`, `List<Voice`, `AudioSource.PlayOneShot`, `DisplaySubtitle`, hidden `.Complete()`, `TryGetLatestCreated`, `Pack=1`, or hot DTO properties in `VocalWarningSystem.cs`.
- `git diff --check` passed for SHINOBU_352 touched files with CRLF warnings only.
- JSON reports still parse with `ConvertFrom-Json`.
- Build was not launched: gates sampled CPU 100% with no `dotnet/csc`, then CPU 10.2% with `dotnet/csc=7`, then CPU 82.5% with `dotnet/csc=7`; all are forbidden by CPU/active-worker policy.

Integrator notes:
- No core `GlobalSignals.cs`, `H8Memory.cs`, or sibling domain assembly file was edited for this pass.
- Fresh compile remains pending behind the gate. No green compile is claimed.

<SELF_AUDIT agent="SHINOBU_352" phase="direct-signalbus-raw-ref-polish">
  <TASKS>
    <TASK id="01" status="PASS" proof="source archaeology and existing owner route preserved" />
    <TASK id="02" status="PASS" proof="VocalWarningSystem remains the integrated owner" />
    <TASK id="03" status="PASS" proof="VocalCueSignal and SubtitleCueSignal reused directly via typed SignalBus lanes" />
    <TASK id="04" status="PASS" proof="runtime forbidden voice trigger scans remain clean for VWS and scoped roots" />
    <TASK id="05" status="PASS" proof="pending warnings remain Vault NativeMinHeap&lt;VocalWarningDTO&gt;" />
    <TASK id="06" status="PASS" proof="mock generator route unchanged" />
    <TASK id="07" status="PASS" proof="priority evaluation still Burst job with NoAlias snapshots and raw state writes" />
    <TASK id="08" status="PASS" proof="heap node/state mutation uses UnsafeUtility.AsRef raw refs" />
    <TASK id="09" status="PASS" proof="interruption remains scalar priority threshold plus interrupt flags" />
    <TASK id="10" status="PASS" proof="evaluation budget still uses continuous lerp(8,64,GlobalQualityWeight)" />
    <TASK id="11" status="PASS" proof="AUP-local direction math unchanged" />
    <TASK id="12" status="PASS" proof="presentation-only rollback fence unchanged" />
    <TASK id="13" status="PASS" proof="Vault allocation still uses UninitializedMemory; no MemClear route added" />
    <TASK id="14" status="PASS" proof="audio/subtitle dispatch is synchronized through typed hash signals" />
    <TASK id="15" status="PASS" proof="300-frame raw telemetry ring and span dump route unchanged" />
    <TASK id="16" status="PASS" proof="editor tuner still mutates Vault tuning row through raw ref" />
    <TASK id="17" status="PASS" proof="cold CSV parser remains ReadOnlySpan byte route" />
    <TASK id="18" status="PASS" proof="debug gizmo still reads raw top heap rows" />
    <TASK id="19" status="PASS" proof="Roslyn AST scanner and JSON reports remain valid" />
    <TASK id="20" status="PARTIAL" proof="static/source/layout/diff checks passed; guarded compile still blocked by active dotnet/csc workers" />
  </TASKS>
  <STRUCT_LAYOUT>
    VocalWarningDTO remains exactly 16 bytes: AudioBankHashID uint@0 size4, PriorityScore float@4 size4, ExpirationTime float@8 size4, Flags uint@12 size4. Total 16 = 2x8 and 1x16 alignment quantum. No Pack=1.
  </STRUCT_LAYOUT>
  <SCALABILITY>
    The polish does not alter the continuous quality curve: below 0.3 quality the evaluator trends toward 8 admitted rows with critical lanes first; ultra quality admits up to 64 rows while downstream audio/UI can spend presentation budget independently.
  </SCALABILITY>
  <H_PHI_VAULT_STATUS>
    Persistent bytes remain Vault-owned: AudioVocalWarningQueue, AudioVocalWarningFlags, AudioVocalWarningCooldowns, AudioVocalWarningSeverity, AudioVocalWarningSourceIds, AudioVocalWarningTelemetry, plus local casted lanes 72430..72435 for heap/current/dispatch/profiles/csv/tuning. No private persistent NativeArray allocation added.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    GenerateMockVocalThreatsJob, EvaluateWarningPrioritiesJob, and DispatchVoiceOverJob retain NoAlias on non-overlapping arrays. VWS consumes dispatcher PostSimulation timing and returns no scheduled handle because this presentation owner uses synchronous Burst Run for same-phase audio command availability; no hidden Complete is inserted.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No sibling runtime assembly reference or core enum edit was added. Rebuild was withheld by CPU/active-dotnet gates.
  </COMPILE_GUARD>
  <DEAR_LIE>
    Before: possible voice arbitration could devolve into clip/UI object work. After: O(log 64) heap math and one typed hash cue; DSP/UI synthesize the audible cut, static click, radio coloration, and subtitles downstream.
  </DEAR_LIE>
</SELF_AUDIT>

## 2026-05-23 VOCAL_WARNING_SYSTEM_AUDIO_QUEUE OWNER FRAME POLISH

What was wrong:
- The dispatcher route used `DispatcherTimingDTO.FrameId`, but fallback `Tick`/`SlowTick` and editor mock seed still read `Time.frameCount`.

What was done:
- Added `_ownerFrameCounter` and `NextOwnerFrameId`.
- Fallback `Tick`, `SlowTick`, and editor mock seed now use owner-local frame identity. Dispatcher execution still uses the dispatcher frame id.
- `_lastProcessedFrame` is now a `uint` initialized to `uint.MaxValue`, avoiding signed wrap artifacts from long sessions.

Cinematic cheats used:
- No gameplay simulation added. This is route hygiene: the voice queue remains presentation-only and fakes interruption through priority math.

Exact microseconds saved or spent:
- Runtime saving is negligible; the useful change is removing Unity `Time` state reads from fallback paths and keeping frame identity owned by VWS/dispatcher.

Verification:
- `rg` found no `Time.frameCount` or `Time.deltaTime` in VWS or SHINOBU_352 editor helpers.
- No dotnet rebuild was launched in this pass.

<SELF_AUDIT agent="SHINOBU_352" phase="owner-frame-polish">
  <TASKS>
    <TASK id="01" status="PASS" proof="source archaeology unchanged" />
    <TASK id="02" status="PASS" proof="existing owner unchanged" />
    <TASK id="03" status="PASS" proof="signal route unchanged" />
    <TASK id="04" status="PASS" proof="OOP voice trigger scans remain zero" />
    <TASK id="05" status="PASS" proof="Vault heap route unchanged" />
    <TASK id="06" status="PASS" proof="mock seed now owner-local frame based" />
    <TASK id="07" status="PASS" proof="priority job unchanged" />
    <TASK id="08" status="PASS" proof="raw heap ref mutation unchanged" />
    <TASK id="09" status="PASS" proof="Dear Lie interrupt math unchanged" />
    <TASK id="10" status="PASS" proof="quality admission curve unchanged" />
    <TASK id="11" status="PASS" proof="AUP local math unchanged" />
    <TASK id="12" status="PASS" proof="presentation-only rollback fence unchanged" />
    <TASK id="13" status="PASS" proof="Vault storage unchanged" />
    <TASK id="14" status="PASS" proof="subtitle/audio hash route unchanged" />
    <TASK id="15" status="PASS" proof="raw blackbox dump unchanged" />
    <TASK id="16" status="PASS" proof="editor tuner unchanged" />
    <TASK id="17" status="PASS" proof="CSV parser unchanged" />
    <TASK id="18" status="PASS" proof="debug gizmo unchanged" />
    <TASK id="19" status="PASS" proof="Roslyn scanner unchanged" />
    <TASK id="20" status="PARTIAL" proof="source verification advanced; fresh compile remains gated by build policy" />
  </TASKS>
  <STRUCT_LAYOUT>
    DTO and dump layouts unchanged. No new payload DTO was added for frame identity.
  </STRUCT_LAYOUT>
  <SCALABILITY>
    Quality curve remains continuous 8..64. Owner frame route does not change fidelity, DTO layout, save identity, or authority.
  </SCALABILITY>
  <VAULT_STATUS>
    No private native arrays added. Frame counter is scalar owner-local presentation bookkeeping only.
  </VAULT_STATUS>
  <COMPILE_GUARD>
    No sibling dependency added. No rebuild launched.
  </COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-23 VOCAL_WARNING_SYSTEM_AUDIO_QUEUE RAW DUMP AND HEAP POLISH

What was wrong:
- The blackbox fault route still used `BinaryWriter` to serialize telemetry fields. That contradicted the task's raw `ReadOnlySpan<byte>` dump requirement.
- The heap implementation was allocation-free but still mutated `NativeArray` nodes through indexers instead of a visible raw pointer/ref path.
- `ResolvePriorityScore` sanitized the already-sanitized tuning row again per queued warning.

What was done:
- Added `VwsTelemetryDumpHeader=32` and `_telemetrySamplesWritten`.
- `DumpTelemetryCold` now writes a fixed header plus oldest-to-newest raw `VwsTelemetryEntry=64` rows from the native ring through `FileStream.Write(ReadOnlySpan<byte>)`.
- The dump latch flips only after the file write succeeds.
- `VocalWarningHeapOps` now uses `NodeRef` / `StateRef` over `NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks` and `UnsafeUtility.AsRef`; insert, pop, discard, sift-up, sift-down, and swap no longer write heap nodes through `NativeArray` indexers.
- `ResolvePriorityScore` now trusts the single sanitized tuning snapshot passed into the job instead of reclamping it on every priority calculation.

Cinematic cheats used:
- No voice mixing or fade simulation was added. The interruption remains one scalar comparison plus an interrupt flag; the downstream DSP creates the audible cut illusion.
- The blackbox dump is forensic only and does not enter the presentation frame unless a fault/over-budget state is already present.

Exact microseconds saved or spent:
- Raw dump: cold fault path replaces 14 managed field writes per telemetry row with one header span write plus one or two raw row span writes; expected dump-time saving is hundreds of microseconds when emitted.
- Heap ref path: expected dense-frame saving is 3-15us by avoiding repeated indexer copy/writeback patterns during sift/swap.
- Single sanitized tuning use: expected dense-frame saving is 2-12us by removing repeated 64-byte tuning clamp blocks after the first job snapshot.

Verification:
- `rg` found no `BinaryWriter` in `VocalWarningSystem.cs`.
- `rg` shows `VwsTelemetryDumpHeader`, `_telemetrySamplesWritten`, `ReadOnlySpan<byte>(&header)`, and `GetUnsafeReadOnlyPtr(telemetryRing)`.
- `rg` shows heap mutation routed through `NodeRef`, `StateRef`, and `SwapNodes`.
- `git diff --check` passed for SHINOBU_352 tracked files; only CRLF warnings reported.
- Build gate sampled CPU 39.9 with `dotnet/csc=7`, so no dotnet rebuild was launched in this pass.

<SELF_AUDIT agent="SHINOBU_352" phase="raw-dump-heap-polish">
  <TASKS>
    <TASK id="01" status="PASS" proof="current owner route retained" />
    <TASK id="02" status="PASS" proof="existing VocalWarningSystem remains the integration owner" />
    <TASK id="03" status="PASS" proof="existing VocalCueSignal and SubtitleCueSignal route retained" />
    <TASK id="04" status="PASS" proof="runtime OOP voice trigger scans remain zero in scoped roots" />
    <TASK id="05" status="PASS" proof="pending warnings remain Vault-backed NativeMinHeap&lt;VocalWarningDTO&gt;" />
    <TASK id="06" status="PASS" proof="mock threat generator still inserts unmanaged DTO rows" />
    <TASK id="07" status="PASS" proof="priority job now uses one sanitized tuning snapshot for all insertions" />
    <TASK id="08" status="PASS" proof="heap node mutation now uses UnsafeUtility.AsRef raw pointer refs and swaps" />
    <TASK id="09" status="PASS" proof="hull breach still preempts battery through scalar threshold and flags" />
    <TASK id="10" status="PASS" proof="quality still maps admission depth continuously 8..64" />
    <TASK id="11" status="PASS" proof="AUP-local double subtraction remains in direction hash" />
    <TASK id="12" status="PASS" proof="rollback exclusion unchanged" />
    <TASK id="13" status="PASS" proof="Vault storage still uses UninitializedMemory with owner initialization" />
    <TASK id="14" status="PASS" proof="audio/subtitle route remains hash-only" />
    <TASK id="15" status="PASS" proof="blackbox dump now writes fixed header plus raw telemetry rows with no BinaryWriter" />
    <TASK id="16" status="PASS" proof="UI Toolkit tuner unchanged" />
    <TASK id="17" status="PASS" proof="ReadOnlySpan CSV profile parser unchanged" />
    <TASK id="18" status="PASS" proof="debug gizmo unchanged" />
    <TASK id="19" status="PASS" proof="Roslyn AST scanner unchanged" />
    <TASK id="20" status="PARTIAL" proof="source/static verification advanced; fresh compile still gated by build policy" />
  </TASKS>
  <STRUCT_LAYOUT>
    VocalWarningDTO remains 16 bytes: uint@0, float@4, float@8, uint@12. VwsTelemetryDumpHeader is 32 bytes: Magic@0, Version@4, EntryStride@8, Capacity@12, Cursor@16, EmittedCount@20, RingStartIndex@24, Reserved0@28.
  </STRUCT_LAYOUT>
  <SCALABILITY>
    Heap and dump polish do not alter gameplay truth. Low quality still admits 8 rows; Ultra still admits 64 rows and downstream presentation richness.
  </SCALABILITY>
  <VAULT_STATUS>
    No private persistent NativeArray ownership added. Telemetry ring remains `BufferID.AudioVocalWarningTelemetry`.
  </VAULT_STATUS>
  <POINTER_ALIASING_AND_DISPATCH>
    Runtime Burst jobs retain NoAlias fields. Heap node/state access now uses raw native refs inside the job kernels.
  </POINTER_ALIASING_AND_DISPATCH>
  <COMPILE_GUARD>
    No sibling runtime dependency added. No rebuild launched during this pass.
  </COMPILE_GUARD>
  <DEAR_LIE>
    The CPU still emits one hash command and an interrupt flag instead of simulating audio crossfades or clip mixing.
  </DEAR_LIE>
</SELF_AUDIT>

## 2026-05-23 VOCAL_WARNING_SYSTEM_AUDIO_QUEUE AST SCANNER POLISH

What was wrong:
- Task 19 asked for an AST validator. The previous SHINOBU_352 scanner had the right domain scope but still used lexical line matching.
- The shared `Docs/Reports/AUDIO_OPTIMIZATION_REPORT.json` did not contain a SHINOBU_352-owned section, so the exact requested report path lacked this agent's proof.
- `EvaluateWarningPrioritiesJob` was still resolving the same 64-byte tuning row on every `TryQueue` call inside a dense evaluation frame.

What was done:
- Replaced `OOP_Voice_Scanner_SHINOBU_352` with a Roslyn `CSharpSyntaxTree` AST-primary scanner. It detects vocal-warning regressions: `AudioSource.PlayOneShot`, `DisplaySubtitle`, `PlayWarning`, `Queue<AudioClip>`, `Queue<Voice*>`, `List<Voice*>`, and `Dictionary<string, AudioClip>`. Generic `.Play()` was intentionally excluded after source review because it catches non-vocal continuous audio owners such as thruster/breathing loops outside SHINOBU_352 authority.
- Lexical fallback now runs only when Roslyn parsing fails.
- The scanner writes `Docs/Reports/AUDIO_OPTIMIZATION_REPORT_SHINOBU_352.json` and non-destructively upserts `shinobu_352_vocal_warning_system_audio_queue` into `Docs/Reports/AUDIO_OPTIMIZATION_REPORT.json`.
- CLI source-control verification still reports zero forbidden runtime voice trigger matches in scoped Audio, Gameplay, Physiology, missing Combat root, and Player/HectonPlayer-named files with Editor excluded.
- `EvaluateWarningPrioritiesJob` now reads/sanitizes `VocalWarningTuningDTO` once per execution and passes it by `in` to all priority insertions.

Cinematic cheats used:
- No CPU audio blending or fade simulation was added. The priority queue emits one hash and an interrupt flag; DSP/UI create the audible cut/subtitle illusion downstream.
- The validator prevents future regression to object-driven voice clips before those routes can reach gameplay hot paths.

Exact microseconds saved or spent:
- Single tuning snapshot: estimated 2-12us saved on dense mock frames by eliminating repeated 64-byte tuning row reads and clamp blocks.
- AST scanner: editor-only, 0us player runtime.
- Shared report upsert: editor/CLI-only, 0us player runtime.

Verification:
- SHINOBU_352 XML was re-extracted from `Docs/Tasks/CURRENT_BATCH.md` using an attribute-aware CLI regex.
- `AUDIO_OPTIMIZATION_REPORT_SHINOBU_352.json` and shared `AUDIO_OPTIMIZATION_REPORT.json` both parse through `ConvertFrom-Json`.
- `rg` confirmed the scanner source contains `CSharpSyntaxTree`, `TryResolveAstFinding`, `TryResolveForbiddenInvocation`, and `TryResolveForbiddenType`.
- Runtime `rg` over Audio/Gameplay/Physiology with Editor excluded returned no `PlayOneShot`, voice queue/list, `DisplaySubtitle`, or `PlayWarning` matches.
- Player/HectonPlayer named vocal-warning scan returned zero matches across 64 files; broader `.Play()` loops such as thruster/breathing were reviewed as non-vocal and left outside SHINOBU_352 authority.
- No dotnet rebuild was launched in this polish pass.

<SELF_AUDIT agent="SHINOBU_352" phase="ast-scanner-polish">
  <TASKS>
    <TASK id="01" status="PASS" proof="source archaeology and current owner route confirmed" />
    <TASK id="02" status="PASS" proof="existing VocalWarningSystem remains the integration owner" />
    <TASK id="03" status="PASS" proof="existing VocalCueSignal and SubtitleCueSignal reused" />
    <TASK id="04" status="PASS" proof="runtime OOP voice trigger scans remain zero in scoped roots" />
    <TASK id="05" status="PASS" proof="pending warnings remain NativeMinHeap&lt;VocalWarningDTO&gt; over Vault memory" />
    <TASK id="06" status="PASS" proof="mock threat generator remains Burst job route" />
    <TASK id="07" status="PASS" proof="priority evaluation uses one tuning snapshot per job execution" />
    <TASK id="08" status="PASS" proof="heap insert/pop/discard path unchanged and bounded" />
    <TASK id="09" status="PASS" proof="hull breach interrupt threshold remains scalar flag math" />
    <TASK id="10" status="PASS" proof="queue depth still uses lerp(8,64,GlobalQualityWeight)" />
    <TASK id="11" status="PASS" proof="AUP double-local direction math unchanged" />
    <TASK id="12" status="PASS" proof="rollback exclusion route card unchanged" />
    <TASK id="13" status="PASS" proof="Vault memory still requests UninitializedMemory" />
    <TASK id="14" status="PASS" proof="audio and subtitle dispatch stay hash-only" />
    <TASK id="15" status="PASS" proof="300-frame telemetry and dump route unchanged" />
    <TASK id="16" status="PASS" proof="UI Toolkit tuner still mutates Vault tuning row" />
    <TASK id="17" status="PASS" proof="CSV profile parser remains ReadOnlySpan byte cold path" />
    <TASK id="18" status="PASS" proof="debug gizmo still exposes raw heap top rows" />
    <TASK id="19" status="PASS" proof="Roslyn AST scanner plus sidecar/shared report section now implemented" />
    <TASK id="20" status="PARTIAL" proof="source/static/report verification done; fresh compile still gated by build policy" />
  </TASKS>
  <STRUCT_LAYOUT>
    Primary DTO remains VocalWarningDTO size=16: uint@0, float@4, float@8, uint@12. No Pack=1. Tuning row remains 64 bytes and cache-line sized.
  </STRUCT_LAYOUT>
  <SCALABILITY>
    Below quality 0.3 the queue still trends to 8 evaluations; high quality trends to 64. AST/report polish does not alter gameplay truth or DTO layout.
  </SCALABILITY>
  <VAULT_STATUS>
    No private persistent NativeArray ownership added. Queue/current/dispatch/profile/csv/tuning/telemetry memory remains Vault-owned.
  </VAULT_STATUS>
  <POINTER_ALIASING_AND_DISPATCH>
    Burst job NoAlias annotations remain in runtime. Scanner work is editor-only and outside runtime dispatcher.
  </POINTER_ALIASING_AND_DISPATCH>
  <COMPILE_GUARD>
    No sibling runtime assembly dependency added. No rebuild launched during this pass.
  </COMPILE_GUARD>
  <DEAR_LIE>
    The queue still fakes sentient interruption through one priority comparison and an interrupt flag, avoiding CPU audio crossfade/mixing.
  </DEAR_LIE>
</SELF_AUDIT>

## 2026-05-23 VOCAL_WARNING_SYSTEM_AUDIO_QUEUE POLISH PASS

What was wrong:
- The first SHINOBU_352 implementation briefly added global `H8Memory` enum lanes for queue internals. That was unnecessary shared-core churn.
- Critical flood/fluid/pipe/oxygen/crush inputs were evaluated once in a new critical pre-pass and then again in old generic loops. Because critical warning IDs bypass cooldown, this could waste heap work or overwrite rows twice in one frame.
- The debug gizmo exposed pending count and current priority but not the top three raw heap rows requested by Task 18.
- The architecture route card omitted the Vault tuning row and still described the local lanes as global enum additions.

What was done:
- Removed SHINOBU_352-specific `H8Memory` enum additions from the route; runtime now uses local casted `BufferID` constants `72430..72435` in `VocalWarningSystem.cs`.
- Added `VocalWarningTuningDTO=64` in Vault lane `72435` for hull/crush/oxygen/radiation/power base priorities, critical boost, producer scale, severity boost, and interruption threshold.
- `VocalWarningQueueTunerWindow` now writes the Vault tuning row through editor-only `UnsafeUtility.AsRef`.
- Removed duplicate generic critical loops after the critical-first pass. Flood/fluid/pipe/oxygen/crush execute before battery once.
- Added editor-only `EditorTryGetHeapEntry` and expanded `VocalWarningQueueDebugGizmo` to show heap rows 0..2 with hash and priority.
- Expanded `OOP_Voice_Scanner_SHINOBU_352` to cover `Player*.cs` / `HectonPlayer*.cs` files outside already scanned roots because the project has no `Assets/_Project/Scripts/Player` directory.
- Added `.cs.meta` files for the three new editor scripts to avoid Unity importer GUID churn.
- Updated `Docs/ARCHITECTURE/VOCAL_WARNING_QUEUE_SHINOBU_352.md`, `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, `Status_SHINOBU_352.md`, `Rationale_SHINOBU_352.md`, and `AUDIO_OPTIMIZATION_REPORT_SHINOBU_352.json`.

Cinematic cheats used:
- Still no clip mixing, fade simulation, scene audio search, or UI text ownership in the queue. CPU emits one hash command; downstream audio/UI layers synthesize the illusion.
- Water breach priority is pure scalar math: hull base `1000` + critical boost `220` + severity curve versus battery base `120`.

Exact microseconds saved or spent:
- Shared-core enum removal: runtime 0us, compile-wall risk reduced by avoiding unnecessary `H8Memory` churn.
- Critical de-duplication: estimated 5-25us saved during dense flood/oxygen frames by removing duplicate signal traversal and duplicate heap insert attempts.
- Vault tuning row read: estimated sub-1us cache-line read per job; buys editor tuning without recompilation.
- Editor top-3 heap labels: editor-only, 0us player runtime.

Verification:
- Re-extracted the exact `SHINOBU_352` XML block from `Docs/Tasks/CURRENT_BATCH.md` with an attribute-aware CLI regex.
- `rg` shows only one pass each for `FloodSignals`, `FluidSignals`, `PipeSignals`, `OxygenSignals`, `CrushWarnings`, and later `BatterySignals`.
- Task 19 scanner scope now covers recursive Audio/Gameplay/Physiology plus Player/HectonPlayer-named files.
- Unity meta files exist for the new SHINOBU_352 editor scripts.
- Targeted hot-path scan found no runtime `NativeArrayOptions.ClearMemory`, `new NativeArray<`, private persistent `NativeArray<`, `NativeList<`, `NativeHashMap<`, `TryGetLatestCreated`, raycast, Unity random, direct AudioSource playback, subtitle text call, or hidden `.Complete()` in `VocalWarningSystem.cs`.
- Targeted `git diff --check` passed for SHINOBU_352 files; only CRLF warnings reported.
- Build retry was not launched: `dotnet/csc` absent, but CPU samples were 99.8/99.6/100, above the mandated 50% build gate.

Integrator notes:
- `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` remains dirty from other agents. SHINOBU_352 no longer depends on new enum entries there.
- No green compile is claimed. The previous legal build found external package/owner walls and SHINOBU_352 `CS8156`; the SHINOBU_352 ref issue is fixed, but a fresh build is blocked by CPU policy.

<SELF_AUDIT agent="SHINOBU_352" phase="polish">
  <TASKS>
    <TASK id="01" status="PASS" proof="rg archaeology identified existing VocalWarningSystem, VocalCueSignal, SubtitleCueSignal, and VWS owner route" />
    <TASK id="02" status="PASS" proof="integrated existing VocalWarningSystem instead of standalone manager" />
    <TASK id="03" status="PASS" proof="existing VocalCueSignal and SubtitleCueSignal reused; no PlayBettySoundSignal created" />
    <TASK id="04" status="PASS" proof="runtime OOP voice scan returned zero scoped matches outside editor scanner patterns" />
    <TASK id="05" status="PASS" proof="pending queue moved from byte array/insertion sort to NativeMinHeap&lt;VocalWarningDTO&gt;" />
    <TASK id="06" status="PASS" proof="GenerateMockVocalThreatsJob injects up to 50 synthetic warning rows" />
    <TASK id="07" status="PASS" proof="EvaluateWarningPrioritiesJob reads SignalBus snapshots and writes DTO heap rows" />
    <TASK id="08" status="PASS" proof="heap insert/peek/pop/discard implemented with bounded capacity and priority promotion" />
    <TASK id="09" status="PASS" proof="DispatchVoiceOverJob preempts current line only when candidate exceeds tuning threshold and interrupt flags permit" />
    <TASK id="10" status="PASS" proof="MaxEvaluations uses round(lerp(8,64,GlobalQualityWeight))" />
    <TASK id="11" status="PASS" proof="direction hash subtracts AUP grid/local values in double before float atan2" />
    <TASK id="12" status="PASS" proof="queue documented as presentation-only and excluded from rollback/Merkle/save truth" />
    <TASK id="13" status="PASS" proof="Vault handles request UninitializedMemory and owner initializes rows once" />
    <TASK id="14" status="PASS" proof="dispatch publishes synchronized VocalCueSignal and hash-only SubtitleCueSignal" />
    <TASK id="15" status="PASS" proof="VwsTelemetryEntry[300] ring and Dump_SHINOBU_352.bin route exist" />
    <TASK id="16" status="PASS" proof="UI Toolkit tuner reads telemetry and mutates Vault tuning row" />
    <TASK id="17" status="PASS" proof="ReadOnlySpan byte CSV parser writes unmanaged profile rows without float.Parse/split" />
    <TASK id="18" status="PASS" proof="debug gizmo displays heap rows 0..2 with hash and priority" />
    <TASK id="19" status="PASS" proof="OOP_Voice_Scanner_SHINOBU_352 and owned report artifact exist" />
    <TASK id="20" status="PARTIAL" proof="source/layout/diff/static scans passed; fresh compile blocked by CPU gate" />
  </TASKS>
  <STRUCT_LAYOUT>
    VocalWarningDTO size=16: AudioBankHashID uint@0 size4, PriorityScore float@4 size4, ExpirationTime float@8 size4, Flags uint@12 size4. Total 16, aligned to 8/16.
    VocalWarningTuningDTO size=64: ten float fields @0..36, Flags uint@40, Revision uint@44, pad ulong@48, pad ulong@56. Total one L1 cache line.
    CurrentState size=64, TelemetryEntry size=64, ProfileDTO size=32, HeapState size=16, DispatchDTO size=80.
  </STRUCT_LAYOUT>
  <SCALABILITY>
    Below quality 0.3, evaluation collapses toward 8 rows and still orders flood/fluid/pipe/oxygen/crush before battery. Middle quality admits broader noncritical signals. Ultra admits up to 64 rows and downstream radio/spatial presentation can spend the saved CPU without changing warning truth.
  </SCALABILITY>
  <VAULT_STATUS>
    Persistent queue memory is Vault-owned: AudioVocalWarningQueue plus local casted lanes 72430 HeapState, 72431 CurrentState, 72432 Dispatch, 72433 Profiles, 72434 CsvScratch, 72435 Tuning, and AudioVocalWarningTelemetry. No private persistent NativeArray ownership was added.
  </VAULT_STATUS>
  <POINTER_ALIASING_AND_DISPATCH>
    Burst jobs mark non-overlapping NativeArray fields with NoAlias. Current implementation consumes no incoming JobHandle and returns no JobHandle because the available VWS owner route executes in PostSimulation via dispatcher/fallback Run; no hidden Complete is inserted.
  </POINTER_ALIASING_AND_DISPATCH>
  <COMPILE_GUARD>
    No sibling runtime assembly reference was added. Work stayed in Audio runtime/editor and docs. Fresh build was withheld under CPU 99.8/99.6/100.
  </COMPILE_GUARD>
  <DEAR_LIE>
    Before: scattered clip playback and subtitle strings can become O(n) scene/audio/UI work with overlapping voices. After: O(log 64) heap route emits one hash command; DSP/UI create the perceptual cut and text downstream.
  </DEAR_LIE>
</SELF_AUDIT>
