# HECTON-8 Build / Playtest Issues

Date: 2026-06-09
Status: PENDING VERIFICATION
Owner: build/playtest issue anchor
Evidence: STATIC_DOC only unless a build/playtest artifact is cited

## Authority

This file tracks current player-facing blockers only. Historical full ledger copy:

- `Docs/DEPRECATED/Root_Bloat_X_012_2026-05-23/BUILD_PLAYTEST_ISSUES.md`

Do not mark `[x]` without current player build, Play Mode, user confirmation, profiler, GCMonitor, or visual artifact as appropriate.

## Current Build Evidence

Last recorded full-solution CLI PASS — `ARTIFACT MISSING`, so the claim below is
`PENDING VERIFICATION`, not evidence:

- Cited artifact: `Docs/Reports/BUILD_UNKNOWN_RUNTIME_API_TRAP_CLEANUP_20260526.log`
- Command recorded: `dotnet build .\Hecton8.slnx -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false`
- Recorded result: exit `0`, proof lines `66 Build succeeded.`, `67 0 Warning(s)`, `68 0 Error(s)`
- Evidence class: CLI_COMPILE only
- **Status, verified 2026-07-28: the log file does not exist anywhere in the repository.** A
  repo-wide search for that basename returns nothing, and `Docs/Reports/` holds nine other
  `.log` files but not this one. Per `AGENTS.md` `Evidence Law`, a recorded exit code whose
  artifact is gone is not proof, so the full-solution CLI compile status reverts to
  `PENDING VERIFICATION` until a build is re-run and its log committed.

Nearest surviving artifact, and why it does **not** substitute:
`Docs/Reports/Compile_20260726.log` (2026-07-26, exit code 0, `Exiting batchmode successfully now!`)
is a Unity batchmode run, not a `dotnet build` of `Hecton8.slnx`. It carries no MSBuild
`N Warning(s)` / `N Error(s)` summary because it is a different proof class. Substituting it here
would be fabricated evidence. Whoever re-runs the CLI build should replace this whole block with
the new log path and its real summary lines.

The historical record above supersedes older root-doc statements for that dated source state only.
It does not authorize a new build attempt by itself, and it never proved Unity import, Play Mode,
player build, profiler, GC, scene wiring, or visual quality.

Before any new `dotnet`, Unity import, Play Mode, profiler, player build, asset reimport, or equivalent heavy proof action, apply the current process gate from `AGENTS.md` and `performance.md`: sample CPU plus active Unity/compiler/import/build processes. If CPU is above `50%`, `dotnet`/`csc`/Unity import/build is active, or the Unity slot is contested, report `BUILD_GATE_BLOCKED: <reason>` and continue with static/scoped work only.

Not proven by that log:

- Unity import
- Unity Console
- Play Mode
- player build
- profiler/GCMonitor
- save/load
- scene wiring
- visual quality
- platform readiness

## Open Product Blockers

| Blocker | Status | Proof Needed |
|---|---|---|
| Surface transition hitch | `[c]` | player/build swim while crossing surface and rotating camera |
| Surface oxygen refill | `[c]` | depleted-O2 surfacing test in build |
| Pause cursor and button focus | `[c]` | build check for cursor, lock state, Esc flow, button actions |
| Surface/interior/underwater audio | `[~]` | snapshot assets, runtime transition proof, player ambient source verification |
| Menu -> world start context | `[c]` | clean new/load/resume path in build |
| Save/load return route | `[~]` | current write/read/migration/corruption artifact |
| First 20 Minutes Copper Wire route | `[~]` | full route clip plus profiler/GC/memory capture |
| Data Monolith runtime boot | `[~]` | Unity import/player boot/checksum proof for `static_data.h8bin` |
| RT/VRAM retained owner set | `[!]` | Memory Profiler / Frame Debugger owner isolation |
| ~~Tool durability does not persist~~ WITHDRAWN — the codec persists it | `[?]` | none; the premise was wrong, see the correction below |
| ~~The content vacuum, measured: 4 items, 3 creatures, 0 quests~~ WITHDRAWN — wrong artifact measured | `[?]` | none; the blob has no runtime item reader, see `The content is not missing — the wiring is` |
| ~~Crafting is unreachable~~ RETRACTED — `Fabricator` IS in the binary world scene; my grep was text-only | `[?]` | none; see the retraction note |
| ~~Nine authoring buttons were never pressed~~ RETRACTED — the world scene is binary and was saved | `[?]` | none; see the retraction note |
| Four scenes are BINARY, so every text GUID search in this repo silently under-reports | `[~]` | `Tools/SceneGuidReachability.py` added `46625dc38`; still owed: the older docs re-tested with it |
| The authored swim profile is dropped for the entire lower body — accepted parameter, never read | `[!]` | a designer retunes `SwimPresentationProfile` and the legs/fins respond |
| No creature carries `FaunaBrain` — its guid occurs in exactly one file, its own `.cs.meta` | `[!]` | a creature that moves under its own brain in a build |
| World-content sockets are Editor-authored only, and `WorldShippingContentFilter` drops 10 of the 14 | `[~]` | settle whether `Tool_TrialRange` ships, then port or press |
| A failed save is invisible in the GAMEPLAY HUD (the main menu shows a real modal) | `[!]` | force a save write failure in a build and watch the gameplay HUD |
| ~~Notifications never reach the player: `HUDNotification` had zero instances~~ FIXED 2026-07-29, `5caea2a5e` | `[~]` | Play Mode: a warning visible on screen once |
| ~~Every notification delivered twice (two drains, different hashes, suppressor never matched)~~ FIXED `cc377a985` | `[~]` | Play Mode: exactly one toast per event |
| ~~Headless world sim could not finish its own default run~~ FIXED `60a7ed08d` — **and the run has now actually happened**, see the section below | `[x]` | RUN EXISTS 2026-07-29: `[ECOLOGY_UNAVAILABLE]`, 1 of 5 days, JSON on disk |
| ~~The headless sim runs and the ecology inside it is empty: prey `0.000`, predator `0.000`~~ **RETRACTED — those zeros were never measured.** They are `default(EcosystemBiomassAuditSample)` written by the failure branch itself | `[?]` | none; the premise was wrong, see the correction below |
| **`-h8headless` skips `BootstrapPhase.Player` entirely, so the ecology is never told where to look** — the harness's success condition is structurally unreachable in the mode it runs in | `[!]` | one CSV day row with non-zero biomass, from a run whose ecology was actually asked a question it could answer |
| **The harness's own verdict line is filtered out by its own log policy.** `filterLogType = LogType.Warning` (`HeadlessSimulationRunner.cs:483`) eats every `Debug.Log` — including `[HEADLESS] fail` and all `[GameBootstrapper]` node progress | `[!]` | a run whose log contains its own terminal verdict |
| Default headless config still cannot finish: 100 days x 3600 s at the measured 3.5x needs **28.6 h** against a 6 h `TimeoutCeilingSeconds` (`HeadlessSimulationBatchRunner.cs:62`) | `[!]` | a raised ceiling, or a documented maximum day count |
| **Measured time dilation is 3.5x against a nominal 100x** — below the 4x floor the batch runner's own comment calls pessimistic (`HeadlessSimulationBatchRunner.cs:61`) | `[x]` | measured: `timeDilationDelivered: 3.500491` |
| A missing asmdef reference cost a whole batchmode run, and neither the lock-free gate nor the unit tests could see it | `[x]` | fixed; see `The build break that ate the first headless run` |

### The first headless simulation run that ever produced a verdict — 2026-07-29

Everything in this section is **runtime proof**, not inspection. It is the first time this harness has
written a result file with a status in it. Command line, and every part of it is load-bearing:

```
cd C:\hades\Hecton8
"C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" -batchmode -h8headless \
  -h8headlessDays 5 -h8headlessDaySeconds 60 \
  -executeMethod Hecton8.QA.Headless.Editor.HeadlessSimulationBatchRunner.Run \
  -logFile Docs/AgentLogs/headless_run_unity.log
```

- `-h8headless` is **mandatory, not optional**. `HeadlessSimulationRunner.ShouldRunStatic` accepts argv OR
  the env var OR the `Temp/H8_HEADLESS_SIMULATION.flag` file (`:1358-1365`), but
  `GameBootstrapper._headlessBootMode` comes only from `IsHeadlessBootRequested()`, which is **argv-only**
  (`GameBootstrapper.cs:6649`, assigned `:2585`). Calling `HeadlessSimulationBatchRunner.Run` on its own
  therefore starts the runtime runner while the bootstrapper boots a **full player** and loads
  `01_MAIN_MENU` — which is exactly the "play mode simply carried on running the main menu for 45 minutes"
  symptom already described in a comment at `HeadlessSimulationBatchRunner.cs:28-30`.
- `cd` to the project root first. The editor side resolves paths from `Directory.GetCurrentDirectory()`
  (`HeadlessSimulationBatchRunner.cs:462`), the runtime side from `Application.dataPath/..`
  (`HeadlessSimulationRunner.cs:1490-1494`). A `-projectPath` launch from a foreign CWD splits the flag,
  the result JSON and the poll loop across two trees and always ends in `BATCH_TIMEOUT`.
- No `-nographics`, and this is not caution — see the ecology finding below.
- Staying in `00_BOOTSTRAP` is the DESIGNED state, not a hang: `GameBootstrapper.cs:3120-3123` marks the
  main menu reached and returns. `02_HECTON_WORLD` is never loaded, and the runner does not need it.

`Docs/AgentLogs/HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json`, verbatim:

```json
{"agent":"HEADLESS_SIMULATION_RUNNER","status":"[ECOLOGY_UNAVAILABLE]","exitCode":1,"days":1,
 "targetDays":5,"simulatedSeconds":62.6500032674521,"timeDilationNominal":100,
 "timeDilationDelivered":3.500491,"progressionSignals":0,"crashSignalsConsumed":0,
 "lastProgressionHash":0,"lastCrashReasonHash":0,"syntheticAupShifts":130,"actualOriginShifts":10,
 "nativeBytes":128435600,"h8Bytes":248407616,"gasInvalidRoomId":-1,"logSpamSuppressed":18,
 "evidenceFailureFlags":0}
```

`Docs/AgentLogs/HeadlessSimulationDaily_HEADLESS_SIMULATION_RUNNER.csv`, verbatim — one row, and the three
numbers that matter are all zero:

```
Day,PreyBiomass,PredatorBiomass,CarryingCapacity,NativeBytes,H8Bytes,NativeAllocations,H8Allocations,Flags
1,0.000,0.000,0.000,128435600,248407616,116,61,1
```

What this proves, and what it does not:

1. **The harness works.** `[HEADLESS] runner installed and started` then `[HEADLESS] waiting for dispatcher`
   appear in the log, the bootstrap reaches `TryInitializeBootstrapDependencyNodeWithFallback for node
   SystemDispatcher`, a day completes, and a verdict is written. None of that had ever been observed.
2. **The ecology was never empty. It was never asked a question it could answer — and the zeros in the CSV
   are not measurements at all.** This entry has now been wrong twice in two different ways, so the
   corrections are kept in order rather than tidied away.

   **Wrong prediction 1 (pre-run, static).** That the cause sat in
   `EcosystemDirector.AllocateRuntimeState` — either the silent `vault == null` bail at
   `EcosystemDirector.cs:4308-4310`, or a throw in its graphics tail (`:4382-4383`, `:4388-4389`) leaving
   `IsInitialized == true` while service registration never ran.

   **Wrong prediction 2 (mine, post-run).** That the bootstrap dependency chain *stalled* at
   `ModWorldPersistenceManager` because the log shows exactly eight nodes and then goes silent. It does show
   that. It is not a stall. Those eight are the complete `BootstrapPhase.CoreServices` set —
   `ResolveBootstrapNodePhase` (`GameBootstrapper.cs:5603-5615`) assigns that phase to exactly those eight,
   in exactly the logged order, and `:5424-5428` filters one global topological order by phase and
   `continue`s past everything else. The loop ended because it ran out of nodes for the phase. The next node
   in global order, `HectonFloatingOrigin`, is `BootstrapPhase.Environment` and ran later.

   **Why the log goes dark, which is what made both of us misread it.**
   `ForceHeadlessRuntimePolicy` sets `Debug.unityLogger.filterLogType = LogType.Warning`
   (`HeadlessSimulationRunner.cs:483`), called at `:337` the moment the dispatcher wait succeeds. Unity
   drops `LogType.Log` at the managed `Logger` before it reaches the log file *or*
   `Application.logMessageReceived`. So the filter ate the rest of the boot trace **and the harness's own
   verdict line**: `FailAndQuit` writes the result JSON at `:939` and logs at `:940`, the JSON is on disk,
   and `[HEADLESS] fail` appears **zero times** in all 27,107 log lines. Native engine output kept flowing
   the whole time, which is the tell — managed-only silence, exactly as a managed-only filter predicts.
   The `logSpamSuppressed: 18` field in the JSON above was also misnamed — it counts `LogType.Log`
   messages *delivered*, not suppressed, and 18 reconciles exactly with the 19 `Debug:Log` frames in the
   play-mode window minus the one that fired before the hook was installed. **Renamed to
   `debugLogMessagesDelivered`.** The JSON quoted above keeps the old key because that is verbatim what the
   run wrote; anything newer will carry the new one. Note the reading is inverted from what the old name
   suggested: a LOW number here means the filter was already active, not that little was suppressed.

   **The ecosystem was alive and registered.** `_simulatedSeconds` only accrues while `_ecologyReady`
   (`HeadlessSimulationRunner.cs:213-217`), and `_ecologyReady` is `ecosystem != null &&
   ecosystem.IsInitialized`, re-evaluated every tick including the one that wrote the verdict. The JSON says
   `simulatedSeconds: 62.65`. So `GlobalRegistry.EcosystemDirector` was non-null and `IsInitialized` true,
   which — since `IsInitialized` is a 19-term `IsCreated` conjunction over the buffers
   `AllocateRuntimeState` creates — also proves that method ran past its graphics tail without throwing.
   Both earlier hypotheses are dead on the same evidence.

   **The actual cause.** `-h8headless` skips `BootstrapPhase.Player` outright
   (`GameBootstrapper.cs:2417-2418`) and parks the boot in `00_BOOTSTRAP` (`:3120-3124`). No player is ever
   created, so `TryResolvePlayerAup` cannot succeed, so `EnsurePlayerSectorRegistered` returns before
   seeding anything, so `_activeBiomassCellCount` stays `0` — and `TryGetGlobalBiomassAudit` fails its
   `count <= 0` gate (`EcosystemDirector.cs:3415-3417`), which the runner turns into a fatal
   `[ECOLOGY_UNAVAILABLE]` (`:588-594`). **The ecology only ever seeds biomass cells from an observer
   position, and headless never gives it one. The harness's success condition is structurally unreachable
   in the mode the harness runs in.**

   **Therefore the CSV zeros are not data.** `1,0.000,0.000,0.000,...,1` is
   `default(EcosystemBiomassAuditSample)` plus the literal `flags: 1u` hard-coded in that failure branch. No
   biomass was sampled and none was reported as zero — the row records that no sample exists. Reading it as
   "the world has no life in it" is reading a null as a measurement, and that is what the retracted blocker
   row above did.

   `-nographics` remains banned regardless, on the independent grounds that `AllocateRuntimeState`'s
   graphics tail runs after the last `IsInitialized` term — that reasoning survives even though it is not
   what fired here.

   **UNPROVEN, and worth stating precisely.** `count <= 0` is *forced* by source — with no player there is
   no seeding path — but it was never *observed*, because the log filter above hid it and the result file
   surfaces no ecology telemetry. `TryGetGlobalBiomassAudit` in fact has **six** false branches, not four —
   the four commonly quoted are the clauses of its first compound condition only — and one,
   `HasPendingSimulationJob()`, is transient in a way that is **deterministic rather than unlucky**: within
   one `SystemDispatcher.RunDispatcherUpdate`, `RunSlowTick` runs before `RunFrostTick`, the ecology's
   `SlowTick` schedules the sector solve, and the flag is cleared only in a later player-loop phase
   (`RunDispatcherLateFrame`). So on any frame where a slow tick scheduled the solve, every `FrostTick`
   after it in that frame — including the runner's, which is exactly where the day boundary is evaluated —
   is *guaranteed* the unavailable answer. Settling which branch fired costs one `Debug.LogWarning` of
   `_activeBiomassCellCount` and about a minute of Unity. Also unproven: whether a *patched* run passes at
   all. Making the ecology answerable is not the same as making it healthy, and whether the solver keeps
   predator biomass above zero for five simulated days has never been measured once.

### Spending the Unity slot while other sessions are active: 3 attempts, 0 usable runs — measured 2026-07-29

Four batchmode runs were attempted in one day. **One produced a verdict; three produced nothing**, and no
attempt failed for a reason inside the code under test. Recorded as a planning input, not a complaint:
budget the slot as *contested*, and never let an UNCOMPILED label be upgraded just because a run happened.

| # | Outcome | Cause |
|---|---|---|
| 1 | died in 2 s | `GeologyAtlasTask.cs` CS0103 — foreign mid-edit; repair landed `+2m41s` after the log ended |
| 2 | **produced the verdict** | `[ECOLOGY_UNAVAILABLE]`, 1/5 days — the run this whole section documents |
| 3 | died in ~20 s | `ForgeGeneratedMaterialAuthoring.cs` CS0103 — foreign mid-edit; method existed `+4m34s` later |
| 4 | died in ~2 s | lost the lock race: argv echoed, exit code 1, no project load. Another session's `Temp/UnityLockfile` appeared 76 s after this run started |

Attempt 4 is worth recognising by shape, because it looks like nothing: the log is **42 lines**, ends
immediately after the `COMMAND LINE ARGUMENTS:` block, and the only warnings are licensing noise that the
successful run carried too. No project load, no Bee output, no compile. That is contention, not a defect —
do not go looking for one.

Before spending the slot: check `Temp/UnityLockfile` AND the Unity process count AND the newest `.cs` mtime
under `Assets/_Project`. Ten minutes of quiet was not enough for attempt 4. And if the goal is to prove a
specific assembly, `touch` its `.asmdef` first — otherwise Bee serves a cache hit and the run proves nothing
about your files even when it succeeds.

### Why a batchmode run does not upgrade an UNCOMPILED claim — measured 2026-07-29

Three batchmode runs were launched today. **Two died on another session's transient mid-edit compile break,
neither of them in a file this session touched.** This is not bad luck to be waited out; it is a cost to
plan around, and the evidence names it exactly.

- Run 1 died on `GeologyAtlasTask.cs(134,31): error CS0103` — a missing `Hecton8.Editor.asmdef` reference.
  The repair appeared in the working tree at `08:41:28`, **2m41s after** the compile died at `08:38:47`.
- Run 3 died on `ForgeGeneratedMaterialAuthoring.cs(1214,17): error CS0103: The name 'ApplyOrganicRole'
  does not exist`. That method exists now, at `:1425`. The file's mtime is `11:13:37`, **4m34s after** the
  log ended at `11:09:03`.

Bee says so outright rather than leaving it to be inferred, and this line is the one to look for:

```
Modification date of `Assets\_Project\Scripts\Editor\Authoring\ForgeGeneratedMaterialAuthoring.cs`
changed while running `Csc Library/Bee/artifacts/1900b0aE.dag/Hecton8.Editor.dll (+2 others)`.
```

It logged that twice with different timestamps, and a `Tundra build success (19.94 seconds), 5 items
updated` in between — the compile was restarted under it as the file kept being saved. **Diagnosis
protocol: before blaming your own edit for a CS0103, compare the offending file's mtime against your log's
last line, and grep the log for `changed while running`.** Both runs would otherwise have been charged to
the wrong author, and run 1 nearly was.

**The related trap, which cuts the other way.** Run 3's build reached `[3921/3925]` with only
`Hecton8.Editor.dll` rebuilt — `5 items updated`, everything else a cache hit. `Hecton8.Core`,
`Hecton8.QA.Headless` and `Hecton8.QA.Headless.Editor` did **not** recompile, even though files in all
three had been edited. So a batchmode run that ends in a compile error tells you nothing about your
assembly, and one that *succeeds* tells you nothing either unless the log shows Bee actually rebuilt the
target. `.claude\rules\hecton8-runtime-source.md` already states that rule; this is the measurement behind
it. On a cache hit, delete `Library/Bee/artifacts` or touch the asmdef and re-run.
3. **Time dilation is 3.5x, not the 4x-13x I estimated.** `timeDilationDelivered: 3.500491` against
   `timeDilationNominal: 100`. My own watchdog arithmetic in `60a7ed08d` reasoned from an optimistic floor
   of 4x; the real floor is below it. The budget `420 s + span/4` still held for this run because the run
   aborted early, so it has NOT been tested against a full 5-day span.
4. Other systems are demonstrably live: `syntheticAupShifts: 130` and `actualOriginShifts: 10` mean the
   origin-shift path ran, `nativeBytes: 128,435,600` and `h8Bytes: 248,407,616` are real allocations, and
   `evidenceFailureFlags: 0` means no evidence channel self-reported broken.
5. `progressionSignals: 0` — nothing progressed. With no ecology that is expected, and it is not
   independent evidence of a second defect.

Do not read a zero-byte CSV mid-run as "no days completed": `HeadlessCsvWriter.Flush` uses
`_stream.Flush()` (`HeadlessSimulationRunner.cs:1752`), not `Flush(true)`, so it never calls
FlushFileBuffers and Windows will not update the visible directory-entry size while Unity holds the handle.
The comment at `:1747-1751` claims the evidence survives a killed run and is visible mid-run; it is not.

### The build break that ate the first headless run

The run above was the SECOND attempt. The first died in 2 seconds with
`Scripts have compiler errors.` and Unity exit code 1:

```
Assets\_Project\Scripts\Editor\Diagnostics\GeologyAtlasTask.cs(134,31): error CS0103:
The name 'WorldWaterLevelCalibrationMath' does not exist in the current context
```

Commit `105d27df6` introduced that line and did not have a compiler over it.
`WorldWaterLevelCalibrationMath` lives in assembly `Hecton8.World.Contracts`; the type directly above it in
the same method, `WorldMacroGeologyParams`, lives in `Hecton8.Core`. **Both are `namespace Hecton8.World`**,
so the single `using Hecton8.World;` at the top of the file resolves one and not the other, and the compiler
reports it as CS0103 "does not exist in the current context" rather than as the missing assembly reference
it actually is. The fix is one line in `Assets/_Project/Scripts/Editor/Hecton8.Editor.asmdef`.

Three things could not have caught this, and each is worth knowing:

- **The lock-free compile gate.** `CONTRIBUTING.md` records that it emits FALSE `CS0433`/`CS0656` against
  `Hecton8.Editor`, so it is untrustworthy for precisely the assembly that broke.
- **The unit tests.** `Assets/_Project/Tests/Editor/WorldWaterLevelCalibrationEditTests.cs` "references"
  `WorldWaterLevelCalibrationMath` only through `StringAssert.Contains` on source text. A source-text
  assertion compiles whether or not the reference resolves — it is not a compile.
- **Exit code alone.** The first attempt returned **0** from the shell despite `Scripts have compiler
  errors` and an internal exit 1. Reading the log was the only way to see it.

The one-line asmdef repair is **not mine** — another session wrote it into the working tree at 08:41:28,
two minutes and forty-one seconds after my compile died at 08:38:47, and the cement job then swallowed it
into `7a1747361 chore(auto): cement working tree` with no rationale attached. Established by comparing the
file mtime against the log timestamp, not by asking. The reasoning now lives in a comment at
`GeologyAtlasTask.cs:134` so that the next snapshot commit cannot launder it away again.

### The Data Monolith content census — measured from the shipped blob, 2026-07-29

> **SUPERSEDED IN ITS CONCLUSION, 2026-07-29. The numbers below are correct; what I concluded from them was
> not.** I read a placeholder-tier blob and called it a content vacuum. The blob is real and the counts hold,
> but the blob is **not what the game loads for items**, so its row counts do not measure the game's content.
> The corrected finding is the next entry, `The content is not missing — the wiring is`. Keep this section for
> the byte-level method and the counts; take the verdict from the successor. Getting this wrong the first time
> is instructive: a census of the wrong artifact reads exactly like a census of the right one.

The vacuum is no longer an impression. `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`
(7,457,664 bytes) was parsed byte-wise and every count below comes out of the section table, not out of a
document. Two self-checks passed before any number was believed: the first non-empty section starts at 576,
which is exactly `AlignUp(HeaderSizeBytes 64 + DirectorySizeBytes 64 + 28*16, 64)`, and the last section ends
at 7,457,664 — the file size to the byte. Field order taken from `H8DataSectionEntry`
(`H8DataMonolithTypes.cs:249-255`): `SectionId(0), RecordSize(4), Count(8), OffsetBytes(12)`. Names from
`H8DataSectionId` (`:87-117`). My first attempt had `Count` and `OffsetBytes` transposed and the self-check
caught it — without that check the numbers below would have been fiction.

**Empty — zero rows, offset zero:**
`QuestNodes` (6), `QuestEdges` (7), `NarrativeTriggers` (15), `RadiationIntensityMap` (18).
The quest graph has no nodes AND no edges. The narrative trigger table is empty.

**Authored gameplay tables, all stubs:**
`Items` 4, `Creatures` 3, `Recipes` 3, `Biomes` 2, `VoxelMaterials` 2, `LootCdf` 4, `AudioClipRegistry` 3,
`VfxScalars` 2, `ToolHeatCapacity` 2, `SubmarineHullConstants` 2, `PhysicsMaterials` 3, `GhostModules` 2,
`SpawnCreditCosts` 3, `SopErrors` 2, `HudLayouts` 2, `SectorPageDirectory` 2, `Economy` 3,
`PhysicsConstants` 3.

**Genuinely populated — and note what they have in common:**
`BiomeHeatmap` 65536, `DepthPressureCurve` 256, `LightAttenuationCurve` 256, `LocalizationUtf8` 5,444,599
bytes, `AppliedLorePackets` 6960, `AppliedLoreRoutes` 458.

The pattern is the finding. **Every machine-generated grid or curve is full, the lore corpus is full, and
every hand-authored gameplay table is a 2-to-4-row stub.** That is the shape of a pipeline where the
generators for procedural data and the lore lane both ran, and the authoring lane never did. A survival game
with four items and three creatures has no economy, no crafting tree and no bestiary to balance, regardless
of how good the systems consuming them are.

**Why boot never told anyone.** A zero-length section is structurally legal, through two independent
bypasses rather than one — patching either alone would not help:
- `H8StaticDataArena.cs:3388` — `if (section.Count == 0u) return section.OffsetBytes == 0u;` so range
  validation short-circuits.
- `:3255` — the contiguity walk in `IsDirectoryValid` is wrapped in `if (section.Count != 0u)`, so an empty
  section consumes no layout budget and breaks no adjacency.
Then `:1009` closes it: every public accessor is a `TryGet*`/`TryFind*` returning false on an empty span, so
an empty section never crashes and never logs — it fails every lookup, forever, in silence. Only
`IsAppliedLoreContractValid` (`:3280`, floor check at `:3291`) enforces a minimum count, and only for the two
AppliedLore sections, which is exactly why those two are the populated ones.

**The census must NOT abort boot, and this is load-bearing.** A minimum-count floor of 1 applied to all 28
sections would reject the blob that is on disk right now — ids 6, 7, 15 and 18 are empty — turning
`InvalidSectionTable` into a dead boot with a misattributed cause, since that is the same status code a
genuinely corrupt table produces. Fail-closed belongs in the editor bake and the test suite, where a bad
build is never produced in the first place; the runtime gets a diagnostic. That split is also why the
minimum-count table belongs in `H8DataLayoutAudit` (`H8DataMonolithTypes.cs`, beside `GetExpectedRecordSize`
at `:749-783`) rather than in the arena — that class is already the shared audit surface for tests, editor
bakes and boot guards, so the baker's idea of "required" cannot drift from the loader's.

**Emission trap, worth naming before someone trips it.** `H8StaticDataArena.cs` contains zero `Debug.Log`,
`LogError` or `LogWarning` calls across all 4,162 lines; its only outward channel is
`H8DataBlobLoadStatus`. Do not add a `LoadedWithEmptySections` member to that enum — it is consumed as
pass/fail and every `status == Loaded` comparison in the codebase would silently stop matching. The 28-bit
deficit mask fits a single `uint`, and `H8DataMonolithTelemetryEntry` (`:716`) has spare reserved uints at a
fixed 64-byte size, so it can carry the mask with no allocation and no string.

**Two smaller findings from the same parse.** The world-seed and app-version bindings at `:3157` and `:3163`
are guarded by `expected != 0u && _directory.X != 0u`, so a blob baked with `AppVersionHash` 0 silently
matches every app version. And `visual_tuning.h8bin`, the sibling artifact, is 64 bytes — header and
directory only, no payload.
### The content is not missing — the wiring is. Verified 2026-07-29

This entry replaces the verdict of the census above. Every number here I measured or GUID-checked myself.

**The item lane already works, and that alone refutes "the game has 4 items."** `PlayerInventory` holds
`[SerializeField] private ItemCatalog itemCatalog` (`PlayerInventory.cs:632`), and the shipping
`Assets/_Project/Prefabs/Player.prefab:1552` assigns it:
`itemCatalog: {fileID: 11400000, guid: e3e4f9b6922abcc44b85e1d8a6d8f46c, type: 2}`. I read that guid out of
`Assets/_Project/Data/Items/ItemCatalog.asset.meta` and it is byte-identical — checked by GUID, not by class
name, because scenes bind by GUID. That catalog registers **73** `ItemData` ScriptableObjects (73 `.asset`
files carry script guid `a49e6475ccf8054419a7fba4c7a78a5c`). Every id resolution in the inventory goes through
`itemCatalog.FindByHash(...)`, and `ItemCatalog.cs` has zero references to the monolith, so there is no blob
fallback inside it. The player has 73 items today.

Meanwhile the blob's `Items` section has **no runtime reader at all**: `TryFindItemRecordByHash`
(`H8StaticDataArena.cs:1002`), `TryGetItemRecord` (`:966`) and `H8ItemSoAReconstructJob` each occur exactly
once in the whole tree — at their own declaration. Its only live consumer is a private localization-string
enumerator (`:1936-1956`) that reads `HashId` and the name offsets and nothing else. So the four blob ids
(`scrap_metal`, `pressure_gasket`, `oxygen_cell`, `sonar_crystal`) — which I confirmed appear in **neither**
the ScriptableObject lane nor the generated economy lane — are content nothing can ever resolve.
`FindByHash` would return null for all four, and the call sites already null-guard.

**Crafting is the opposite case, and it is the real blocker.** 42 `RecipeData` ScriptableObjects exist under
`Assets/_Project/Data/Crafting/Recipes/`. `Fabricator` is the runtime crafting owner and reads only its own
`[SerializeField] List<RecipeData> availableRecipes` (`Fabricator.cs:101`). Its script guid
`65748c03d0baf8a4a95eca4dd9cfa4c4` appears in **zero** `.unity` and `.prefab` files under `Assets` — I ran
that search myself and it returned nothing. There is also no `RecipeCatalog.asset`; `Data/Crafting/` contains
only `Recipes/` and its `.meta`, so recipes have no aggregate registration the way items do.

And here is the part that turns this from a mystery into a work item. The scene-absence does **not** mean the
lane was never built. Its construction site exists and is an authoring tool with a button:
`Assets/_Project/Scripts/Editor/FabricationBootstrapAuthoring.cs:437` does
`fabricator = station.AddComponent<Fabricator>();`, reached from
`[MenuItem("Hecton8/Authoring/Rebuild Starter Fabrication Kit", priority = 170)]` at `:60` — and there is a
companion `[MenuItem("Hecton8/Validation/Validate Starter Fabrication Kit", priority = 171)]` at `:244`. There
is no `AddComponent<Fabricator>` anywhere outside `Scripts/Editor/`. So a complete authoring tool, with its own
validator, sits behind a menu item nobody has pressed. That is a one-session fix, not a content programme, and
it is the single highest-leverage action available on this axis.

**The blob is compiled from the smallest of at least three authoring lanes.** The compiler's source roots are
exactly two: `Assets/_SourceData/DataMonolith` (`H8DataMonolithCompiler.cs:30`) and `Data/Balance` (`:31`).
I measured `Data/Balance`: 20 hand-typed CSVs, **58 data rows total**, largest 449 bytes. I then measured what
those roots exclude: `Data/Economy` 30 files, `Data/Precomputed` 14, `Data/Visuals` 24, `Data/Audio` 5,
`Data/System` 7 — **7.75 MB**, including already-compiled `.h8bin` blobs, manifests and preview images. So the
generators did run and their output is on disk. `Data/Economy/Items.csv` holds 55 rows, of which 33 ids are
also ScriptableObject names, so a baker consumed part of the SO lane to produce it. The compiler has zero
matches for `ScriptableObject`, `AssetDatabase.Load/Find`, or any of those directory names — the 7.75 MB is
structurally unreachable from it. The split is systemic rather than item-specific: `Assets/_Project/Data/Biomes`
alone carries hundreds of assets against `Biomes.csv`'s 2 rows, and `Assets/_Project/Data/Fauna` carries 22
against `Fauna.csv`'s 3.

**Do not "just add the directories to the source roots."** The two `Items.csv` files have incompatible schemas
— the generated one is `item_id, item_hash32, display_name, item_kind, source_recipe_id, source_recipe_hash32,
category_id, category_hash32, ...`, the hand-typed one is `Id, version_id, Name, Description, CategoryId,
Cost, StackMax, MassKg, IconIndex, AccessFrequency`. `ParseItem` expects the second. Repointing the compiler
would mis-map or throw. And on the item axis it would fix nothing player-visible anyway, because no runtime
code reads the result.

**Why no gate caught any of this.** `ValidateProductionSectionCoverage` is a *not-empty* gate, not a
*not-placeholder* gate: 22 of its 23 checklist entries compare `rowCount > 0` (`AppendMissingSection`,
`:2595`), and the lone exception is `BiomeHeatmap`'s `rowCount == 65536` (`:2607`). One row passes. `Items=4`
can never fail it. The four 0-row sections pass because they are **not on the checklist** — and that is
provable by construction rather than by correlation: the gate runs before `BuildBlob` and aborts on any
checked section being zero, a blob exists on disk, therefore every checked section was non-zero at bake time,
therefore only omitted sections can read 0. The gate prints *"A structurally valid sparse static_data.h8bin is
not production payload proof."* while passing exactly such a blob.

`FIRST_20_MINUTES`: pressing the fabrication authoring button is the shortest path to a craftable first
twenty minutes; today the crafting station is not in any scene the player can load.

**Still unproven, stated plainly.** All of the above is static and GUID-level: no Unity run, no build, no
player session. The zero-caller claims rest on tree-wide symbol searches, which would not catch reflection or
a string-keyed dispatch table; I saw no such data layer but did not audit for one. The item verdict is scoped
to `Items` — I did not trace `Creatures`, `Biomes` or `LootCdf` readers, so those sections may well be
load-bearing and must not inherit this conclusion.

### The authored swim profile never reaches the lower body — verified 2026-07-29

`Assets/_Project/Scripts/Gameplay/PlayerSwimBlockoutRig.Body.cs:457` declares
`SwimPresentationProfile profile` as the second parameter of `ApplyFullBodyPose`, and the identifier `profile`
appears **exactly once in the whole file** — that declaration. I ran that search myself. The call site at
`PlayerSwimBlockoutRig.cs:481` passes a real profile, so the parameter is supplied and discarded.

Consequence: torso, pelvis, thighs, calves and fins are posed from hardcoded literals and mode-keyed poses,
while the designer-authored ScriptableObject that is supposed to tune them is thrown away. The arms and hands
path consumes that same asset heavily, so a designer retuning it sees the upper body respond and the lower body
refuse — which reads as a broken asset rather than as a dropped parameter, and is why this survives.

This is the second signature the project's own rules name as its dominant failure mode: *"a parameter accepted
then ignored (one made every creature of a species share an identical genome)"*. Found by looking for that
signature deliberately: 847 mechanical candidates across the tree, 846 benign, this one real. The other named
signature — a min/max fold seeded with a sentinel that cannot lose — came back **verified clean** across 83
candidates, which is worth recording as a result rather than a non-event.

**Deliberately NOT fixed here, and the reason is the rule rather than caution.** Mapping `StrokeVerticalAmplitude`
or `StrokePitchAmplitude` onto a hardcoded torso literal means choosing a multiplier and a blend, which is
player-visible visual judgement. `TASTE.md` and the Visual Reference Parity Gate reserve that for someone with
the reference images open, and inventing an animation curve to close a ticket is how a plausible-looking wrong
fix ships. The gap is filed with the exact line so whoever owns swim presentation answers it in minutes.

`FIRST_20_MINUTES`: swim is stage 4 of the route chain, so this is on the critical path for how the first
twenty minutes *feel* rather than for whether they function.

### RETRACTED 2026-07-29 — the world scene is BINARY and every GUID grep below was blind to it

> **The entry that follows is substantially WRONG and I am leaving it in place rather than deleting it, because
> the method error is more instructive than the conclusion was.**
>
> `Assets/_Project/Scenes/02_HECTON_WORLD.unity` is **6,270,260 bytes of BINARY serialization**. It does not
> begin with `%YAML`, and it contains the string `m_Script` **zero** times. Four scenes in this project are
> binary: `02_HECTON_WORLD.unity`, `010_TEST.unity`, `020_RENDER_SANDBOX.unity` (60 MB) and
> `020_RENDER_SANDBOX_V2.unity`. The other 995 scenes/prefabs are text.
>
> Every reachability claim below rests on `rg` for a hex GUID against `*.unity`/`*.prefab`. In a binary scene a
> script reference is stored as raw bytes in **nibble-swapped** order, so a text search for the hex string
> cannot match it. My searches were therefore blind to the one scene the player actually loads.
>
> **Re-tested with a binary-aware search (nibble-swap plus raw bytes, on the file bytes rather than as text):**
>
> | Type | filed as | actually in `02_HECTON_WORLD.unity` |
> |---|---|---|
> | `Fabricator` | zero scenes | **PRESENT** (also `010_TEST.unity`) |
> | `BiomeMatrixDirector` | zero scenes | **PRESENT** |
> | `WorldContentSocket` | zero scenes | **PRESENT** |
> | `WorldContentDirector` | zero scenes | **PRESENT** |
> | `ScavengePopulator` | zero scenes | **PRESENT** |
> | `WorldCaveDirector` | zero scenes | **PRESENT** |
> | `FaunaBrain` | zero files but its own `.cs.meta` | **still zero — that finding HOLDS** |
> | `HUDNotification` | zero instances | **still zero — that finding HOLDS** |
>
> Independent corroboration, because a byte match alone deserves a second source: the binary type tree embeds
> assembly-qualified type names, and the scene contains `Fabricator` 3 times and `BiomeMatrixDirector` 6 times
> as literal type-name strings. A 16-byte GUID sequence matching by chance in 6 MB is impossible, and the type
> names agree with the GUIDs.
>
> **So the authoring buttons WERE pressed and the scene WAS saved.** "Nine buttons never pressed" and "crafting
> is unreachable" are both retracted. What remains true is narrower and still worth having: `FaunaBrain` really
> is attached to nothing, `HUDNotification` really had no instances (now fixed), and the socket lane really is
> filtered down to 4 live kinds out of 11 by `WorldShippingContentFilter`.
>
> **A second blind spot in the same searches, for the record.** The four text hits my re-test found for those
> GUIDs are all in `Assets/_Recovery/` — recovery copies, gitignored, which is why `rg` skipped them by default
> too. So the original search missed live content one way and dead content the other.
>
> **The method rule this establishes, and it belongs in the rules rather than only here:** header-test every
> scene and prefab for `%YAML` BEFORE searching it, and validate the search against a control GUID known to be
> present. I had that exact method earlier in this session, in a script that header-tested and nibble-swapped —
> and then dropped it for a one-line `rg` when the question felt simple. The scene did not get less binary
> because the question got easier.

### The wiring boundary is a 38-entry list in one file, and nine authoring buttons were never pressed

This generalizes the fabrication finding, and the fabricator turns out to be the mildest case. `Fabricator`
was one stranded lane; there are at least nine, and three of them are load-bearing for what the player sees.

**The boundary, and it is worth internalising because it explains every case below.**
`Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` contains exactly **38** `AddComponent<>` calls. I
counted them myself. That list is the project's real wiring boundary: a MonoBehaviour reaches the running
game either by sitting in a scene, or by being on that list. Anything on neither must be placed by an Editor
authoring button — and that is precisely where the content is stranded. `ConstructionManager` is on the list
(hence its scene-absence is harmless, which is why the pairing method matters). `BiomeMatrixDirector`,
`WorldContentDirector`, `ScavengePopulator`, `WorldCaveDirector`, `Fabricator` and `HectonRockManager` are all
**not** — I checked each one individually against that file.

**Worst case: the biome matrix. 108 authored assets, one uninstantiated director, five blind consumers.**
`Assets/_Project/Data/Biomes/MatrixProfiles/` holds **108** `.asset` files — I counted them — plus 13 family
and 13 atmosphere profiles. `BiomeMatrixDirector`'s script guid `5edcfefa47837a147a16a78401507398` appears in
**zero** `.unity` and `.prefab` files, and it is not on the bootstrapper's list, so nothing instantiates it.
Five runtime systems declare it as `[SerializeField]`: `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:147`,
`FaunaDirector.cs:247`, `AcousticZoneController.cs:421`, plus `HectonAtmosphereManager.cs:806` and
`HectonUnderwaterVisuals.cs:308`. Music, acoustics, fauna, atmosphere and underwater visuals all read a biome
layer that is fully authored and never built.

**Corrected: those null fields are not the defect, and saying they were made the problem sound bigger and
more diffuse than it is.** The nulls are optional by design and the consumers are already right. The tooltip
at `AcousticZoneController.cs:420` says so in as many words — *"Optional BiomeMatrixDirector reference. If
unassigned, the controller lazily resolves the runtime owner."* — and the lazy resolution is real:
`WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref ...)` is called from
`AcousticZoneController.cs:1894`, `HectonMusicDirector.cs:1462`, `AtlasSignalSystem.cs:583` and
`BeaconDeployerTool.cs:680`, resolving through `BiomeMatrixDirector.ActiveRuntimeInstance`, which the director
sets in its own `Awake` (`BiomeMatrixDirector.cs:892`, with `GlobalRegistry.RegisterBiomeMatrixRuntime` at
`:894`). One enabled director in a loaded scene binds all five with no per-consumer wiring.

So the defect is exactly one thing and it is smaller than I first wrote: **nothing instantiates the director,
so the lazy resolver has nothing to find.** The fix is scene placement alone.

**What pressing `[MenuItem]` at `Assets/_Project/Scripts/Editor/BiomeMatrixBootstrapAuthoring.cs:36` actually
does — read before pressing, because three of these are not what you would assume.**
1. It writes into **whatever scene is currently active** — `SceneManager.GetActiveScene()` at `:107`, guarded
   only by `IsValid() && isLoaded`. No name check, it never opens a scene, and an untitled empty scene passes
   that guard. The hazard is not "silently skipped", it is "director written into the wrong scene, silently".
2. **The two halves have opposite durability.** `AssetDatabase.SaveAssets()` at `:125` commits the asset half
   to disk immediately, while the scene half is only `EditorSceneManager.MarkSceneDirty` at `:122` with no
   `SaveScene` anywhere in the method. Close without saving and you keep 108 rewritten profiles and lose the
   director. This is the inverse of the "irreversible in a dirty scene" risk I assumed.
3. **Idempotent for asset identity, not for asset content.** It reuses existing files by path
   (`LoadAssetAtPath` … `if (profile == null) CreateAsset`, `:70-75`), so GUIDs and inbound references survive
   — but `:77-92` then overwrite every generated field unconditionally from a hard-coded seed table, and the
   catalog array is forced to exactly 108 with every slot reassigned (`:101`). Both writes use
   `ApplyModifiedPropertiesWithoutUndo` (`:104`, `:120`) and the new GameObject gets no
   `Undo.RegisterCreatedObjectUndo`, so **Ctrl+Z reverts neither half**. Any hand tuning applied to those 108
   profiles since they were last generated is destroyed. Whether such tuning exists is unknown and is the one
   thing to check first — recovery is version control, not the tool.
4. **Placement matters more than the button.** `[MANAGERS]`, the root it attaches to, appears in **zero** of
   the 7 scenes under `Assets/_Project/Scenes`, so it is always created fresh, it is scene-owned rather than
   `DontDestroyOnLoad`, and `GameObject.Find` only sees active objects. The director must therefore end up in
   the scene that is loaded during play, not in `00_BOOTSTRAP`, or it dies at scene handoff and
   `ActiveRuntimeInstance` goes null again (`BiomeMatrixDirector.cs:946-947`).
5. Every input loader fails silently — `LoadItemData` (`:1991`), `LoadWorldFamilyProfile` (`:1981`),
   `LoadZonePlanProfile` (`:1986`), `LoadToolLoadoutPreset` (`:2083`) all return null without logging, while
   `:127` prints *"108-biome matrix rebuilt."* unconditionally. A renamed input yields profiles with null
   resources and a success message. The detector for exactly this already exists and is itself unpressed:
   `Hecton8/Validation/Validate 108 Biome Matrix` at `:130`.

**No creature in the game has a brain.** `FaunaBrain`'s script guid `f97102d76d9d9d04f95ccebcd55b7079` occurs
in exactly **one** file in the entire `Assets` tree — its own `.cs.meta`. I ran that search myself and there is
no second hit: not a prefab, not a scene, not a `.cs`. The six generated proxy prefabs under
`Data/AI/GeneratedProxies` exist, but `DroneProxy.prefab` reportedly carries zero `m_Script` lines at all,
i.e. it is a geometry shell. `[MenuItem]` at `CreatureProxyPrefabAuthoring.cs:22`.

**Widest case: the world runtime stack. — RETRACTED IN FULL, 2026-07-29. All six of its premises are false.**

> The paragraph below is kept verbatim because the shape of the error is worth more than the claim was.
> `260f3a4a6` retracted the table version of this and **missed this prose copy**, which named two types the
> retraction never covered. Re-tested with `Tools/SceneGuidReachability.py`, control
> `WorldStreamingDirector` firing on 2 files so the search is validated: **`SeamRegistry`
> (guid `1200263adb6511e4e9502bda36d49ba5`) and `FloorBiolumZone` (guid
> `0c37648b7c41d4547aeca6871aa726f6`) are each PRESENT in both `02_HECTON_WORLD.unity` and
> `010_TEST.unity`.** The other four were already retracted. So "there is no world content, no scavenge
> loot and no caves" has zero surviving premises.
>
> The lesson is about retraction hygiene, not about scenes: I corrected the table and did not grep my own
> document for the same claim stated in prose 235 lines further down. A retraction that fixes one
> presentation of a claim and leaves another standing reads, to the next person, as two independent
> sources agreeing.

`WorldRuntimeBootstrapAuthoring.cs:55` places roughly sixteen world
managers, and the reported guid checks put `WorldContentDirector`, `ScavengePopulator`, `WorldCaveDirector`,
`SeamRegistry`, `FloorBiolumZone` and `WorldContentSocket` each in zero scenes, with consumers holding
`[SerializeField]` nulls at `WorldProceduralFillDirector.cs:16`, `WorldPopulationDirector.cs:17` and
`ScatterBudgetController.cs:53`. If that holds, there is no world content, no scavenge loot and no caves — I
verified the bootstrapper omission for three of those six types but did not re-verify all six guids myself.

The `[SerializeField]` null observation in that paragraph is also weaker evidence than it reads as, and this
generalises past this one entry: **a component carried by a prefab INSTANCE emits no scene entry unless the
value is overridden.** So for anything prefab-borne, scene-absence is the expected signature of a *correct*
instance, not evidence against one. Any audit that treats such an absence as a defect has inverted its own
observation.

**Also stranded, lower player impact:** the rock runtime stack
(`HectonRockRuntimeBootstrapAuthoring.cs:32`), two flora topology packs whose output directories do not exist
(`FloraTopologyStudio1604.cs:149`, `FloraTopologyStudio1711.cs:74` — and five sibling buttons gate on output
that was never generated), the procedural interior/colony finals
(`WorldProceduralInteriorColonyFinalAuthoring.cs:20`, while sibling lanes for geology, support and organic-misc
all landed), and the flora template thumbnails (`FloraThumbnailGenerator.cs:16`, with 35 templates waiting).

**Two rows a naive scene search would have filed wrongly**, which is why scene-absence must always be paired
with a construction-site search: `ConstructionManager` (guid in zero scenes, but
`GameBootstrapper.cs:6378` constructs it) and `WorldProceduralProxyInstance` (guid in zero scenes, but
`WorldProceduralScatterDirector.cs:8425` constructs it). Both are live. Filing either as dead would have been
a false blocker, and the same trap already cost one near-miss earlier in this session.

**Why nothing flagged any of it.** There are 863 `[MenuItem("Hecton8/` declarations across 646 files, 52 of
them under `Hecton8/Authoring/`, 78 under `Validation/` and 55 under `Diagnostics/`. Several stranded lanes
ship a companion validator button — the fabrication kit has one at
`FabricationBootstrapAuthoring.cs:244`. So the project has validators for buttons nobody pressed, and a
validator that is never run cannot report that its subject is missing. Nothing in the build gates asserts
"every authoring lane's output is present in a scene", so an empty world is indistinguishable from a full one
at CI time.

`FIRST_20_MINUTES`: the biome matrix and world runtime stack sit directly on the route — atmosphere, audio,
fauna and any world content at all. Pressing those two buttons plausibly changes the first twenty minutes more
than any code change currently on the board.

**Proof status, stated exactly.** Static and GUID-level. I verified myself: the 38-count and each of the seven
bootstrapper membership checks, `BiomeMatrixDirector`'s guid absence and its 108 profile assets, three of its
five null consumers, and `FaunaBrain`'s single-file occurrence. The remaining rows — the flora and interior
output directories, `DroneProxy.prefab`'s missing `m_Script`, and three of the six world-stack guids — are
reported and not independently re-checked by me. No Unity run, no build, no player session. Pressing any of
these buttons is an authoring action with scene consequences and needs the owner's go-ahead, not a subagent's.

| A failed save is shown to the player as a completed save | `[!]` | force a save write failure in a build and watch the HUD |

### A failed save is rendered as success — verified 2026-07-29

This is the worst-shaped defect found so far: it does not hide a failure, it reports it as a success, in a
survival game where the player's decision to keep playing depends on believing the save landed.

**The publisher is not at fault.** `SaveManager` raises the failure lane on every failure path — the
verified-pipeline failure at `:5701-5729` (failureCode 3) and the catch-all at `:5758-5762` (failureCode 1)
both funnel into `HandleSaveFailure`, which calls `SaveEvents.TryRaiseSaveFailed` at `SaveManager.cs:5476`
next to the `LogError`. Eleven more preflight and reject paths raise it too (`:2196`, `:2205`, `:2216`,
`:2292`, `:2303`, `:5046`, `:5486`, `:5495`, `:5507`, `:5520`, `:5536`). It does not merely log.

**Corrected 2026-07-29: the defect is scene-scoped, not global — I first wrote "three surfaces, none works"
and that was wrong.** There are seven runtime `ISaveEventListener` implementations, and two of them handle a
failure correctly:
- `MainMenuController` (`MainMenuController.cs:24`) is genuinely reachable — its script guid
  `759f3087469a99f40ab0dc8c4a3b6fb3` sits at `01_MAIN_MENU.unity:394`, and that scene is `enabled: 1` in
  `ProjectSettings/EditorBuildSettings.asset:12`. On `SaveFailed` it calls `OnSaveFailed` (`:2347`), clears
  `_isSaveLoadBusy`, re-enables the buttons and raises a real
  `ModalWindow.ShowWithCustomLabels("Save Failed", ...)` with the localized `ERROR_SAVE_FAILED_MESSAGE`
  (`:2354-2360`). Verified by hand, all three facts.
- `ModWorldPersistenceManager` is reachable through `GameBootstrapper.cs:6043`.

So a save that fails **in the main menu** — load, delete, slot management — does tell the player. What is
broken is the **gameplay scene**: the player saving mid-run gets nothing, and worse, gets a false success.
That narrows the blast radius and it also makes the defect sharper, because mid-run is exactly when a lost
save costs the most.

**The gameplay-scene surfaces, and why each fails:**

1. `HUDSaveNotificationLink` is the ONLY component in the repository that renders a save failure to the
   HUD — `notificationSystem.ShowCritical` for `SaveFailed`/`LoadFailed` at `:88-92`, building the literal
   `"SAVE FAILED"` at `:142`. Its script guid `473b7a7cc5029354e85995ce5c763e8f` appears in ZERO `.unity`
   and ZERO `.prefab` files, including zero nibble-swapped hits in the binary scenes, and it has no
   `AddComponent` site in any `.cs` — the only matches are two editor smoke testers that read its source as
   text. So `SaveEvents.Register(this)` at `:43` never runs and the component never exists.
2. `PauseMenuController.HandleSaveFailed` (`:579-581`) is real code, but the controller's only construction
   site is `PauseMenuHost.cs:38`, and `PauseMenuHost` (guid `99b935f9beb2c9d48a71477cfadfbaea`) is itself in
   zero scenes. Same one-link-too-short chain as `PDAMapTab` behind `PDASpectrumTab`.
3. `SuitHUDV4CanvasOverlay` IS reachable — and it is the one that lies. `OnSaveEvent` at
   `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:1747-1748` reads:
   `if (eventType == SaveEventType.SaveCompleted || eventType == SaveEventType.SaveFailed)`
   `    RequestSavingProgressHide();`
   One branch for both outcomes. `RequestSavingProgressHide` (`:1792`) sets
   `_savingProgressTargetAlpha = 0f`, so on a failed write the saving indicator fades out exactly as it does
   on a successful one. Visually indistinguishable from "saved".

**Why this is filed rather than fixed by me.** Checked directly: `SuitHUDV4CanvasOverlay` owns no
notification API at all — zero matches for `ShowCritical`, `ShowWarning` or `Notification` in the file. It
owns only the saving-progress indicator (`_savingProgressRoot`, `_savingProgressDataLamp`,
`_savingProgressTargetAlpha` and siblings). So every way of making a failure visible from inside that class
is a new player-visible visual state, which `AGENTS.md` puts behind the Visual Reference Parity Gate and
`TASTE.md`, and that gate needs the reference folder plus a capture I cannot produce without a Unity slot.
Inventing a failure visual and calling it done would be exactly the unverified visual claim the rules reject.

**The fix that needs no visual judgement, and is the recommended one.** Wire up
`HUDSaveNotificationLink`. Its presentation is already authored inside it, so activating it invents nothing.

**Host choice, and one of the two obvious candidates is a trap.** `SuitHUDV4CanvasOverlay` is the correct
owner, but NOT at `:633`, and NOT `SuitHUDPresentationController.cs:705`. That site is
`CreateProjectionSourceOverlay()`, which builds the *duplicate* projection-source canvas for the visor, not
the player's HUD: it sets `go.layer = ProjectionSourceLayer` (`:698`), is reached only when `projectedMode`
is true and `visorProjectionCamera != null` (`:657-666`), only on a cold pass (`:669`, and the COLD ALLOC
comment at `:691` says tick calls disallow creation), and its layout is a mirror —
`projectionSourceOverlay.CopyConfigurationFrom(canvasOverlay)` (`:684`). Hosting the link there breaks in at
least three ways: in flat or non-visor mode the surface is never created, so a save failure is invisible
exactly when the player is not in projected mode; when projected mode flips off,
`SetOverlayCanvasVisible` → `SetBehaviourEnabledIfChanged` (`:759`) hides the canvas through its CanvasGroup
and would suppress the notification mid-display; and it renders into the visor projection render texture
rather than the player's screen. Picking the plausible-looking site would have shipped a fix that appears to
work in exactly one camera mode.

Open questions still to settle before wiring: whether the link resolves its notification system itself or
needs it injected, and whether its `Register`/`Unregister` pair survives a host created and destroyed per
scene load.

**Second, independent fix, also low risk:** split the `SaveFailed` branch out of `OnSaveEvent:1747` so a
failure stops sharing a code path with a completion. Keep `RequestSavingProgressHide` for `SaveCompleted`
only. What the failure branch should then do is the visual decision above.

**Third:** `SaveStation` reports nothing about the save the player explicitly asked for. It already has
`ShowWarning` plumbing at `:316` and a lazy HUD notification resolve at `:194`, but implements no
`ISaveEventListener` and contains no failure branch.
| Two MonoBehaviour services cannot reach a scene | `[!]` | a pre-Ready bootstrap lane, then Play Mode proof that both slots resolve non-null |

### Two sole-implementation services are unreachable at runtime — verified 2026-07-28

Not a lead: every step below was checked by hand, and the scan method was validated against a control
before any negative result was accepted.

**1. `WorldChunkResidencyManager`** — `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:590`,
6536 lines, `MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, IBaseAirlockEventListener,
IStreamingBackpressureService, IGlobalRegistryHotSwapListener, IDisposable`.

- It is the ONLY type implementing `IStreamingBackpressureService`.
- Zero construction sites: no `new WorldChunkResidencyManager(` and no
  `AddComponent<WorldChunkResidencyManager>` anywhere under `Assets`.
- Absent from all 999 `.unity` and `.prefab` files, by class name AND by script GUID
  (`8de4f944c53c4f448bff65e8fd01a4db`).
- It is deliberate, and the reason is written down at `WorldRuntimeInstaller.cs:116-125`: the slot
  `GlobalRegistryServiceSlot.StreamingBackpressureRuntime` is HARD-DENIED by
  `GlobalRegistry.IsSceneRuntimeHotSwapSlot` (`GlobalRegistry.cs:7182`), the publication gate cannot issue
  a token for a denied slot, `OnEnable` registration at `:2491` would throw `CriticalBootException`, and
  installers run in sequence with no `try`/`catch` — so adding it there would abort every installer after
  it. That comment names the fix itself: "It needs a pre-Ready bootstrap lane, not this one."
- Consumers hold a field that can only ever be null — `PrologueSequenceRegistryBridge.cs:56`, `:324`, `:608`
  and `PDAMapTab.cs:187`. **Corrected 2026-07-29: neither consumer is reachable either, so both null reads
  are LATENT, not live.** I first wrote "live consumers" and that was an overstatement.
  `PrologueSequenceRegistryBridge` has no construction site anywhere and guid
  `45870ac22097485c8af3756f9b82f96f` returns 0 hits across all 999 scenes and prefabs, so `_service` is null,
  `OnEnable` bails at `:136-140` publishing `MissingServiceHash`, and `CacheRuntimeServices()` is never
  reached. `PDAMapTab` is created only at `PDASpectrumTab.cs:390`, and `PDASpectrumTab` itself has no
  construction site and is in no scene — so the chain stops one link earlier than I claimed. The defect is
  real and still worth fixing; it is queued behind whoever instantiates those two lanes.

**Third instance of the same shape, found 2026-07-29.** `IOrbitalDirector` is also permanently null in
`PrologueSequenceRegistryBridge` (`:55`, cached at `:607`). Its sole implementation
`Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs:23` is a MonoBehaviour with no
construction site and guid `a157e4fc116ddcb47959dc414b43d02c` returns 0 hits across all 999 files. Unlike the
streaming case the registry is NOT the obstacle: the component self-registers at `:354` and
`OrbitalDirectorRuntime` is absent from the deny list at `GlobalRegistry.cs:7160-7186`, so registration would
succeed the moment the component exists. Cost inside the bridge: `TryGetOrbitalSnapshot` (`:194-212`) always
returns false so the prologue never receives universe velocity, planet distance, reentry heat or cloud
whiteout; `ZeroUniverseVelocity` (`:499-503`) is a silent no-op through `orbital?.`; and
`TryConsumeOrbitalWhiteoutFallback` (`:825-861`) bails at `:830`, removing one of three prologue-complete
fallback paths. Also absent from every scene in that lane:
`Prologue/Space/PrologueWorldHandoffSceneLoader.cs` and `Prologue/VFX/OrbitalDropReentryVfxController.cs`;
only `Prologue/Space/PrologueOrbitSceneBootstrap.cs` is wired.

**Two services in that same file are FINE, checked so nobody re-audits them.** `ITickDispatcher` (`:609`):
`SystemDispatcher` is `AddComponent`ed at `GameBootstrapper.cs:5874` and `GameBootstrapper` drives itself
from `[RuntimeInitializeOnLoadMethod]`, so it needs no scene presence; the bridge also degrades to
`SystemDispatcher.CurrentFrameDeltaTime` when it is null (`:722-724`, `:1080-1082`). `IInputService`
(`:657`) structurally cannot be null — the property returns a `NoOpInputService` null object at
`GlobalRegistry.cs:936`.

**The PDA is NOT a dead feature, and recording that is the point.** All 21 `PDA*` MonoBehaviours are absent
from every one of the 999 scenes and prefabs, which reads as a whole handheld device written and never
wired. It is not: the PDA is built programmatically. `PDARuntimeInstaller.EnsurePlayerSystems(playerObject)`
is invoked from `GameBootstrapper.cs:8031`, `ProgressionRuntimeInstaller.cs:49` adds `PDADeathMemoryDump`,
and tabs build their own sub-panels — `PDAInventoryTab.cs:883` and `:1531`, `PDAAtlasSignalTab.cs:496`.
Scene absence proves nothing for a code-constructed UI, exactly as it proves nothing for a
non-MonoBehaviour service. The one genuine gap in that lane is `PDASpectrumTab`: no construction site, no
scene, and it is the only creator of `PDAMapTab`, so both are unreachable.

**Method, improved on the earlier pass.** Header-test all 999 scene and prefab files for `%YAML` first:
exactly 4 are binary (`02_HECTON_WORLD.unity`, `010_TEST.unity`, `020_RENDER_SANDBOX.unity`,
`020_RENDER_SANDBOX_V2.unity`). For the 995 text files a plain guid grep is exact; only the 4 binary ones
need the nibble-swapped byte order. Control `547a39a8034a57a47b65413eb12885d2` (WorldStreamingDirector)
returns 6 hits, including two independent binary scenes in swapped form and 0 in raw form — that is what
makes every negative above meaningful. And always pair a scene negative with a construction-site search: an
absent MonoBehaviour that something `AddComponent`s is not a finding.
- Player consequence: no chunk residency, no load/unload radius, no streaming backpressure, no far-field
  HLOD impostors, and the `BufferID.WorldChunkResidencyManager_ActiveImpostor*` DataVault entries are never
  populated. `AGENTS.md` `Memory Management & Chunk Dispose` has no owner on this route.

**2. `AssetLifecycleGovernor`** — `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:27`,
`MonoBehaviour, ITickable, IUpdatable, ISlowTickable, ILateFrameTickable, IAssetLifecyclePressureSink,
IGlobalRegistryHotSwapListener`. Sole implementation of `IAssetLifecyclePressureSink`. Its only two
construction sites are its own tests — `Tests/PlayMode/Optimization/AssetLifecycleGovernorTickTests.cs:17`
and `Tests/Editor/Optimization/AssetLifecycleGovernorDumpTests.cs:18`. Absent by name and by GUID
(`0e7cdf0573f867d4983dde747e5c4c22`) from all 999 scene and prefab files. Its slot
`AssetLifecycleRuntime` sits in the same hard-denied switch block.

**The negative result matters as much, and is recorded so nobody repeats the near-miss.** The denied block
at `GlobalRegistry.cs:7178-7184` covers SEVEN slots, and all seven have exactly one implementation each
that is absent from every scene — which looks like seven blocked subsystems and is not. Five of them are
plain classes, not MonoBehaviours, with real construction sites in runtime code: `H8MacroDatabaseService`
(4 sites), `BurstTokenBucketJobAdmissionService` (4), `AssetLoadDispatcher` (1), `ModuloSimulationBucketer`
(1), `HardwareThermalService` (1). For a non-MonoBehaviour service, construction in code is the correct
pattern and scene absence means nothing. Only the two MonoBehaviours above need a scene or an
`AddComponent` they never get.

**Method, stated so the next audit can repeat it.** Scene absence was checked by byte-scanning all 999
`.unity` and `.prefab` files for the class name as ASCII and UTF-16LE, and separately for the script GUID
in four forms — ASCII lower, ASCII upper, raw 16 bytes, and the nibble-swapped byte order Unity's binary
serialiser uses. `02_HECTON_WORLD.unity` is binary, so a text grep alone proves nothing: the control
`WorldStreamingDirector` (guid `547a39a8034a57a47b65413eb12885d2`) was found in 6 files, and in the
production scene only in the nibble-swapped form. A negative result without that control is worthless.

Deliberately not fixed here. Building a pre-Ready bootstrap lane changes boot ordering and registry
publication policy, which needs `Docs/SYSTEMS_CONTRACTS.md`, the global-authority route card, and Unity
Play Mode proof that both slots resolve non-null. Filed as a `BLOCKER` with the exact missing condition,
per `AGENTS.md` Deliverable class lock.

### Tool durability does not persist — static evidence, 2026-07-28

Found by auditing first-party runtime against the bans `AGENTS.md` states, rather than by playing.
This one is a functional save defect, not a style violation:

- `Assets/_Project/Scripts/SaveData.cs:153` `public Dictionary<string, float> toolDurabilityMap`
- `Assets/_Project/Scripts/SaveData.cs:156` `public Dictionary<string, bool> toolBrokenMap`
- `Assets/_Project/Scripts/SaveData.cs:365` `public Dictionary<string, string> CustomModData`

These are public fields on the ROOT `SaveData` type, which is exactly what `AGENTS.md`
`Concrete Project Contracts` bans: "Managed-collections with dynamic allocations (e.g.
`Dictionary<string, T>` or `HashSet<string>`) in the root structures of `SaveData.cs` are banned;
serialization must rely on `ISerializationCallbackReceiver` and parallel flat lists."

Measured, not assumed: `SaveData.cs` does **not** implement `ISerializationCallbackReceiver`, has no
`OnBeforeSerialize` or `OnAfterDeserialize`, and carries no parallel flat lists for these three maps.
Unity serializes no `Dictionary` field, so the consequence is that tool durability and broken-tool state
are silently dropped on save and come back empty on load. `Assets/_Project/Scripts/SaveDataMigration.cs`
lines 232 and 254 hold `HashSet<string>` under the same ban.

Deliberately NOT fixed here. Changing root save structures touches save identity and needs
`persistence.md`, the save mandates and `SaveManager.cs` read as owner files first, plus a migration
decision and a real load-after-save artifact. `SaveManager.cs` also had concurrent edits in flight at the
time of this audit. Classified `BLOCKER` with the exact missing condition, per `AGENTS.md` Deliverable
class lock.

Clean in the same audit, across 2078 first-party runtime `.cs` files with comments and string literals
stripped: zero `Camera.main`, `FindObjectOfType`/`FindObjectsOfType`, `GameObject.Find`,
`Resources.Load`, `Resources.UnloadUnusedAssets`, `async void`, `BinaryFormatter`, `renderer.material`
and `renderer.materials`. The four `OnGUI` hits are all inside `#if UNITY_EDITOR`, so they are not
violations. `DontDestroyOnLoad` appears 7 times and `Time.deltaTime` 3 times outside Dev tooling — both
need an owner-route ruling rather than a blanket verdict, and neither is claimed as a defect here.

#### WITHDRAWN 2026-07-29 — tool durability does persist, and "fixing" it would have broken a working path

The style violation is real; the functional consequence claimed from it is not. The reasoning above went
"`AGENTS.md` bans `Dictionary` in save roots" + "Unity serializes no `Dictionary` field" -> "therefore
durability is silently dropped". The second premise does not apply here: **this save system does not use
Unity serialization at all.** It has its own binary codec, and that codec handles every one of the fields.

`Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs`, write side:

- `:583` `WriteStringFloatDictionary(ref writer, data.toolDurabilityMap, SaveData.MaxToolDurabilityRecords)`
- `:584` `WriteStringBoolDictionary(ref writer, data.toolBrokenMap, SaveData.MaxToolDurabilityRecords)`
- `:586` `WriteDiscoveredBiomeBitWords(ref writer, data.discoveredBiomeBitWords)`
- `:654` `WriteStringStringDictionary(ref writer, data.CustomModData, SaveData.MaxCustomModDataEntries)`

Read side, each with a `nameof()` diagnostic: `:757` `toolDurabilityMap`, `:762` `toolBrokenMap`,
`:770` `discoveredBiomeBitWords`, `:876` `CustomModData`. Null-safe defaults at `:899-901`.

Round-trip coverage exists and is not a text assertion:
`Tests/Editor/SaveSystem/HazardZoneRuntimeSaveEditTests.cs:7653-7654` assert on
`restored.toolDurabilityMap` and `restored.toolBrokenMap`, and `:7850-7853` assert the key-trimming
contract survives the round trip.

**A fourth banned-looking field, and why it is the counter-example rather than a fifth defect.**
`SaveData.cs:159` `public HashSet<int> discoveredBiomeIds` is not in the list above, and it is the one
that shows the design is deliberate: the persisted form is the parallel flat array
`discoveredBiomeBitWords`, packed by `BiomeDiscoveryBitMask.Pack` from
`HectonDiscoveryManager.cs:211`. That is exactly the "parallel flat lists" shape `AGENTS.md` prescribes,
already implemented. Better still, `SaveBinaryPayloadCodec.cs:906-907` carries a compatibility bridge —
if the bit words are empty but the set has entries, it re-packs — so a save written before the bitmask
existed still restores. Someone converting these maps to flat lists "per the ban" would have deleted a
working, tested, migration-aware path.

**What is actually left, and it is smaller.** The `Dictionary` fields in the root type are a genuine
`AGENTS.md` style violation with a real cost — allocation and GC churn during save staging, which the ban
exists to prevent — but they are not a data-loss defect and they are not a player-facing blocker. Moving
them to flat lists is optimization work behind a GC measurement, not a save-integrity fix, and it must
keep the `:906-907` bridge and the round-trip tests green. `SaveDataMigration.cs:232,254` `HashSet<string>`
falls in the same class.

Evidence class: STATIC_SOURCE, verified by reading the codec and the tests. No Unity run, no save/load
artifact — so the positive claim here is "the write and read paths exist and are covered by an EditMode
round-trip test", not "durability provably survives a real player save".

## Entry Template

```md
## Build Entry - YYYY-MM-DD - Build Name
- Artifact:
- Hardware:
- Scene:
- Status: [ ] / [~] / [c] / [x] / [!] / [?]
- Evidence class:
- Main blocker:
- Change tested:
- Result:
- Failed:
- Next proof:
```

## Rules

- `[c]` means implementation/static-doc work closed, proof pending.
- `[x]` means current artifact proves the claim.
- Build feel beats editor feel.
- Player route proof beats subsystem count.
- Static source, H-Phi, route cards, and compile logs do not prove runtime quality.
- Visual tasks need screenshot/clip proof.
- Performance tasks need profiler/GC/memory evidence.
- Save tasks need write/read/corruption/migration evidence.
