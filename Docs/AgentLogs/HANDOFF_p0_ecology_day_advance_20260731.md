# HANDOFF — P0 ecology day-advance / post-ready clock restore

**UTC:** 2026-07-31  
**Author session:** continued from `HANDOFF_p0_ecology_clock_20260731.md`  
**Repo:** `C:\hades\Hecton8`  
**Branch:** `main` → push `origin` + `gitlab` (**NO force**)

---

## 0. Standing rules (DO NOT VIOLATE)

- Blunt architect answers. **No mocks / temporal fakes / fake day counters / forced SUCCESS.**
- Poll smoke DoD → real product fix → pathspec commit **product + BACKLOG + evidence only**.
- `git pull --ff-only` then `git push origin main` + `git push gitlab main`. **NO force.**
- **NEVER** scratch / amend cementer / Debris commits.
- Real-game screenshots required for gameplay claims; headless JSON ≠ gameplay; feature w/o gameplay = **DECLINED**.
- Shell CWD Desktop is flaky — always absolute paths; agent scripts under repo with `os.chdir(r"C:\hades\Hecton8")`.
- `Docs/AgentLogs` is gitignored → `git add -f` for evidence.
- AgentWorkCementer auto-commits mid-run — never amend those commits.
- Tokens may appear in `git remote -v` — do not re-log secrets.
- **NEVER commit** `_agent_*` scratch scripts unless explicitly asked.

### DoD (smoke)

| Field | Required |
|---|---|
| `status` | ∉ `{ECOLOGY_UNAVAILABLE, BATCH_TIMEOUT, BOOTSTRAP_TIMEOUT}` |
| `ecologySampledDays` | `> 0` |
| `timeDilationDelivered` | `> 0` |
| compile | no `error CS` |

Standing DECLINED until real-game proof: headless ecology alone; Geology@2048 headless-only; KCC FAIL 0x42; Debris EXEMPT; RuntimeSmokeTester; README art; V0 Swim; `Docs/Screenshots/V0_Playtest` empty.

---

## 1. One-line status

**Ready gate is green; day-advance fix is ON DISK (uncommitted, unsmoked).**  
Next: BACKLOG + evidence → pathspec commit → pull --ff-only → push origin+gitlab → relaunch/poll DoD.

Prior smoke (`80b2d9764`, pid 21516): ready line present, then ~495s wall with **0 CSV day rows**, batch stub `BATCH_TIMEOUT`.

---

## 2. Mission

Close **P0 ecology-clock day-advance**: after ecology ready, headless smoke must advance simulated days under real dispatcher dilation, write daily CSV rows, and emit runtime result with `ecologySampledDays > 0` and `timeDilationDelivered > 0`.

User also required: architect gap analysis, subagent discovery, real-game screenshots or honest DECLINED, full documentation, commit/push/pull main. No mocks.

---

## 3. Git state (as of fix-on-disk session)

At session start:

```
## main...gitlab/main [ahead 2]
HEAD 54ec1ab94 chore(tools): add agent probe scripts and diagnostic logs
d002bfe0c chore(auto): cement working tree 2026-07-31 06:38:22
2e0d5e3d3 fix(core): release blackbox vault mutation guard after bind (Input publish P0)
edfcd719e chore(auto): cement working tree 2026-07-31 01:51:05
80b2d9764 fix(headless): mark ecology ready from Update wait path (Frost starve gate)  ← ready fix
e36bb3e13 chore(auto): cement working tree 2026-07-31 01:35:30
411715153 fix(fo): drain scene-rebase bootstrap lock under physics pause (P0 ecology) ← FO fix
```

**Ecology product commits already on main (proved earlier):**

| Hash | Role | Live proof |
|---|---|---|
| `411715153` | FO lock-drain under physics pause | foLock=0, foPhysicsPause=0 after GameReady |
| `80b2d9764` | `TryMarkEcologyReady` on Update wait path | `[HEADLESS] ecology ready (ecosystem initialized)` |

**This session product fix:** working tree only — `HeadlessSimulationRunner.cs` modified, **not committed yet**.

Remotes: `origin` = github marko1olo/Hecton8; `gitlab` = barsukdana/Hecton8. Push both, no force.

---

## 4. Prior smoke FAIL (context — do not re-diagnose FO/ready)

### Launch meta
- head=`80b2d9764085b2f2e3829edca0165bb491754e21`
- pid=21516 (FINISHED)
- log: `Docs/AgentLogs/headless_smoke_20260731_p0_ecology_ready_20260731_014953.log`

### Result JSON (batch stub — NOT full runtime WriteResult)
```json
{"agent":"HEADLESS_SIMULATION_RUNNER","status":"BATCH_TIMEOUT","exitCode":2,"source":"HeadlessSimulationBatchRunner"}
```
⚠️ Stub lacks `ecologySampledDays` / `timeDilationDelivered` / `days` / `simulatedSeconds`.

### CSV
`Docs/AgentLogs/HeadlessSimulationDaily_HEADLESS_SIMULATION_RUNNER.csv` — **header only** (~107 B). Zero day rows.

### HEADLESS lifecycle (only 5 lines entire run)
```
[HEADLESS] runner installed and started
[HEADLESS] waiting for dispatcher
[HEADLESS] dispatcher acquired
[HEADLESS] runtime lanes registered; dilation requested
[HEADLESS] ecology ready (ecosystem initialized)
```
No `complete` / `fail` from runtime. Wall ~495–500s post-ready = batch budget kill.

### Timing
`HeadlessSimulationBatchRunner`: TimeoutFixedSeconds=420, PessimisticDilation=4, smoke 5×60 → simulatedSpan=300 → budget=420+75=495s.

**Verdict:** zero sim advance (not “slow dilation”). Fast/Frost got dt≤0 or never delivered usable dilated time.

---

## 5. How days are supposed to advance (DO NOT MOCK)

1. **FastTick** (runner, `PriorityLayer.Core`): if `_ecologyReady`, `_simulatedSeconds` / `_dayAccumulatorSeconds` += dilated `deltaTime`.
2. **FrostTick** (runner, Core): when `_dayAccumulatorSeconds >= _daySeconds` (smoke: 60), `_pendingDayAudits++`.
3. **LateFrameTick** (runner, `PriorityLayer.Player`): `DrainPendingDayAudits` → `ExecuteDailyAudit` → CSV + biomass sample.

Dispatcher facts (`SystemDispatcher.cs`):
- `ConsumeFrameTimeDilationScalar` returns **0** if `_simulationPaused || _timeDilationScalar <= 1e-4`.
- `RequestSimulationPause(true)` zeros scalar; unpause restores `_prePauseTimeDilationScalar` (**may be 1, not headless 100**).
- `RequestHeadlessTimeDilation` clamps to 100.
- `RunFrostTick` early-returns if `deltaTime <= 0`.
- `RunFastTick` fixed 1/60, max 4 substeps/frame → delivered dilation ≈ fps/15, not nominal 100.
- `ShouldSkipLaneDuringBootstrap` skips **Player only** while `!IsGameReady` → LateFrame audits blocked if GameReady false.
- Headless short-circuit **does** `PublishGameReady(true)` at `GameBootstrapper.cs` ~3165.

Prior bug: runner called `RequestHeadlessTimeDilation(100)` **once** at lane register. No re-assert on ecology ready / GameReady / after pause.

---

## 6. Hypotheses (ranked)

| # | Hypothesis | Status | Fix direction |
|---|---|---|---|
| **H1** | paused or dilation≈0 entire post-ready window | **Primary; unproved live** until post-ready diag | unpause + re-RequestHeadlessTimeDilation on ready/GameReady + sustain |
| **H2** | GameReady false → Player LateFrame skipped; debt may queue but audits never drain | Possible secondary | Ensure GameReady; diag gameReady + pending |
| **H3** | FO frame lock / aupPreShift sticky | Unlikely (foLock=0 proved) | diag dispFrame |
| **H4** | unscaled dt=0 in batchmode | Possible | diag via dayAcc/simS growth |
| **H5** | watchdog too tight at ~4× | **Rejected** — 500s wall with 0 days is zero advance | N/A |

---

## 7. Product fix APPLIED ON DISK (this session)

**File:** `Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs`  
**Method:** Python patcher `_agent_apply_clock_fix.py` (scratch — do not commit). `replace_in_file` multi-block failed; patcher succeeded (+7194 bytes).

### 7.1 Constants / fields
```csharp
private const float TimeDilationScalar = 100f;
private const float PostReadyClockEnsureIntervalSeconds = 5f;
private const float PostReadyDiagIntervalSeconds = 15f;
// fields:
private int _postReadyDiagBucket = -1;
private double _lastClockEnsureRealtime;
```

### 7.2 EnsureHeadlessSimulationClock(string reason)
- Resolve `GlobalRegistry.TickDispatcher`.
- If `SimulationPaused` → `RequestSimulationPause(false, RunnerHash)`.
- `RequestHeadlessTimeDilation(TimeDilationScalar, RunnerHash)`.
- LogWarning lifecycle: `sim clock ensure reason=… pausedBefore=… dilBefore=… dilAfter=… pausedAfter=… gameReady=…`
- **Not a mock** — restores real dispatcher scalar only. Never writes CSV/day counters.

### 7.3 Call sites
| When | reason string |
|---|---|
| After lane register (replaces bare dilation request) | `lanes-registered` |
| First ecology ready transition in `TryMarkEcologyReady` | `ecology-ready` |
| When ecology wait clock arms on GameReady | `game-ready` |
| Sustain while ready && days==0 && (paused \|\| dil < 100-ε) every 5s | `post-ready-sustain` |

### 7.4 Update() post-ready path
Previously: `if (_ecologyReady) return;` after mark → **skipped all post-ready work**.  
Now: when ready, every frame:
- `TryArmEcologyWaitClock()` (GameReady may land after ecoInit)
- `MaybeEnsureHeadlessSimulationClockSustain()`
- `MaybeLogPostReadyProgress()`
- `HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks()`

### 7.5 MaybeLogPostReadyProgress (Warning, every 15s)
```
post-ready t=…s paused=… dil=… dayAcc=… pending=… days=… simS=…
gameReady=… frostReg=… lateReg=… fo*=… dispBoot=… dispFrame=…
```
Survives log filter (Warning). If next smoke BATCH_TIMEOUTs again, these lines diagnose H1–H4 without guessing.

### 7.6 Marker verify (post-patch)
```
EnsureHeadlessSimulationClock ×7
MaybeEnsureHeadlessSimulationClockSustain ×2
MaybeLogPostReadyProgress ×2
post-ready-sustain ×1
PostReadyClockEnsureIntervalSeconds ×2
```

### 7.7 Explicitly NOT done (anti-mock)
- No CSV rows without Frost/LateFrame
- No lowered `daySeconds`
- No forced SUCCESS
- No skipped biomass audit
- No fake `_completedDays` / `_simulatedSeconds`

---

## 8. Probe work this session

| Script | Result |
|---|---|
| `_agent_probe_pause_root.py` | RAN → `_agent_probe_pause_root_out.txt` |
| Pause push sites | PauseMenuController, SaveManager, InputDispatcher, LockstepStateValidator desync, RollbackNetcode |
| Unpause helper | SceneRuntimeService.ResetWorldEntryFreezeStateFromCache → RequestSimulationPause(false) |
| PauseMenu auto-open headless | No evidence |
| Subagents ×3 | handoff extract / code map / architect critique — all succeeded |

Scratch scripts on disk (DO NOT COMMIT): `_agent_probe_*.py`, `_agent_scan_*.py`, `_agent_apply_clock_fix.py`, `_eco_*`, `_scan_*`.

---

## 9. Architect answers (required)

### Least confident right now
**H1 is still unproved live.** Code fix matches the mechanism (pause zeros dilation; unpause restores pre-pause ≠ 100; single request at register). Until next smoke prints `sim clock ensure` + `post-ready … dil=… dayAcc=…`, we do not know whether:
- dilation was simply never re-asserted after a bootstrap pause, or
- something **re-pauses every frame** (sustain would log `post-ready-sustain` repeatedly), or
- H4 unscaled dt=0 / H3 frame-lock still starve master sim even with dil=100.

### Biggest thing missing / what you may not realize
1. **Ready gate green ≠ day machine live.** ecoInit can flip while Fast/Frost still get dt=0.
2. **Unpause alone is insufficient** — restore scalar is pre-pause (often 1). Must re-`RequestHeadlessTimeDilation(100)`.
3. **LateFrame is Player-lane** — blocked while `!IsGameReady` even if Core Fast/Frost run. Headless short-circuit should PublishGameReady; prior smoke never logged wait-clock arm (ready before GameReady path) — GameReady state post-ready was never Warning-diag'd.
4. **BATCH_TIMEOUT stub JSON is not a runtime ecology verdict** — missing ecologySampledDays/timeDilationDelivered.
5. **Headless green alone = DECLINED for gameplay.** Day machine lives in QA harness; no real-game day-advance UI/screenshot proof exists.

### Implemented but not integrated into gameplay
| Piece | Headless | Real gameplay |
|---|---|---|
| FO lock-drain | proved | N/A infrastructure |
| Ecology ready mark on Update | proved | N/A harness gate |
| Unpause + re-dilate on ready | **code on disk, unproved** | N/A harness |
| Day Fast→Frost→LateFrame→CSV | designed; was dead | **not a player-facing feature** |
| Ecology biomass over days | smoke target | no V0 screenshot / in-world proof |
| Geology@2048, KCC, V0 Swim | — | DECLINED / open |

**Feature without gameplay is DECLINED** — even if next smoke goes green, ship claim for “ecology days work in game” still needs real-game screenshots or stays DECLINED.

---

## 10. Key code map (absolute under repo)

| Area | Path | Notes |
|---|---|---|
| Runner (FIXED) | `Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs` | clock ensure + post-ready diag |
| Dispatcher | `Assets/_Project/Scripts/Core/SystemDispatcher.cs` | pause/dilation/Frost/bootstrap skip |
| FO | `Assets/_Project/Scripts/HectonFloatingOrigin.cs` | lock drain landed earlier |
| Batch editor | `Assets/_Project/Scripts/QA/Headless/Editor/HeadlessSimulationBatchRunner.cs` | 420+span/4 timeout; BATCH_TIMEOUT stub |
| Bootstrap | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` | headless PublishGameReady ~3165 |
| API | `Assets/_Project/Scripts/ITickable.cs` | ITickDispatcher pause/dilation |
| Stress pattern | `Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs` | ApplyHeadlessTimeDilation re-apply |
| BACKLOG | `BACKLOG.md` | must update on commit |
| Prior handoff | `Docs/AgentLogs/HANDOFF_p0_ecology_clock_20260731.md` | ready-gate session |

---

## 11. Commit pathspec (when committing)

```bat
cd /d C:\hades\Hecton8
git add Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs
git add BACKLOG.md
git add -f Docs/AgentLogs/HANDOFF_p0_ecology_day_advance_20260731.md
git add -f Docs/AgentLogs/p0_ecology_day_advance_clock_20260731.md
git add -f Docs/AgentLogs/headless_smoke_*.log
git add -f Docs/AgentLogs/HeadlessSimulationResult_*.json
git add -f Docs/AgentLogs/HeadlessSimulationDaily_*.csv
REM NEVER: _agent_* scratch, Tools/_cline_scratch, cementer amend
git commit -m "fix(headless): re-assert unpause+dilation on ecology ready (P0 day-advance)"
git pull --ff-only
git push origin main
git push gitlab main
```

Suggested commit message alternatives if scope includes only runner:
`fix(headless): unpause + re-RequestHeadlessTimeDilation on ready/GameReady (P0 clock)`

---

## 12. Relaunch / poll

Kill stray Unity holding project lock first.

```
"C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe"
  -batchmode -projectPath C:\hades\Hecton8 -nographics
  -executeMethod Hecton8.QA.Headless.Editor.HeadlessSimulationBatchRunner.Run
  -h8headless -h8headlessDays 5 -h8headlessDaySeconds 60 -h8headlessStartupTimeout 600
  -logFile Docs/AgentLogs/headless_smoke_p0_day_advance_<timestamp>.log
```

Or: `_agent_relaunch_ecology.py` + `_agent_poll_ecology.py` if still valid.

### Expected new lifecycle lines (success path signals)
```
[HEADLESS] sim clock ensure reason=lanes-registered …
[HEADLESS] ecology ready (ecosystem initialized)
[HEADLESS] sim clock ensure reason=ecology-ready pausedBefore=… dilAfter=100 …
[HEADLESS] ecology wait clock armed (GameReady)   // if GameReady after eco
[HEADLESS] sim clock ensure reason=game-ready …
[HEADLESS] post-ready t=15.0s paused=0 dil=100 dayAcc=… pending=… days=…
…
[HEADLESS] complete exitCode=0 status=SUCCESS
```

### Read DoD from runtime JSON (not batch stub)
`Docs/AgentLogs/HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json` must contain full WriteResult fields. If only stub `source=HeadlessSimulationBatchRunner`, runtime never finished — read log post-ready diag.

---

## 13. BACKLOG notes to record on commit

- 2026-07-31: ecology ready gate PROVED (`80b2d9764`)
- 2026-07-31: post-ready day-advance was DEAD (BATCH_TIMEOUT, 0 rows) after ready
- 2026-07-31: product fix on runner — EnsureHeadlessSimulationClock + sustain + post-ready diag (**DoD OPEN until smoke**)
- Real-game screenshots still REQUIRED (headless green alone = DECLINED)

---

## 14. What NOT to do

- Do not treat BATCH_TIMEOUT stub as runtime ecology field source
- Do not amend cementer / unrelated Input commits
- Do not force-push
- Do not claim gameplay proof from headless
- Do not re-open FO lock as primary if foLock=0 unless new evidence
- Do not fake days/CSV/SUCCESS
- Ready-mark on Update stays; problem was **after** ready — clock restore

---

## 15. Checklist for next agent

- [x] FO lock-drain `411715153` proved foLock=0
- [x] Ready-mark Update `80b2d9764` proved ecology ready line
- [x] Smoke 21516 FAIL documented (BATCH_TIMEOUT, 0 days)
- [x] Pause-root probe run
- [x] Product fix on disk: unpause + re-dilation + sustain + post-ready diag
- [x] This HANDOFF written (`HANDOFF_p0_ecology_day_advance_20260731.md`)
- [ ] Spot-verify `EnsureHeadlessSimulationClock` still in runner on disk
- [ ] Update `BACKLOG.md`
- [ ] Write evidence `Docs/AgentLogs/p0_ecology_day_advance_clock_20260731.md`
- [ ] Pathspec commit + pull --ff-only + push origin + push gitlab (**NO force**)
- [ ] Kill stray Unity; relaunch smoke; poll until result
- [ ] Require ecologySampledDays>0 AND timeDilationDelivered>0 AND status not timeout
- [ ] If FAIL: read `post-ready` / `sim clock ensure` lines; fix next root (not mocks)
- [ ] Real-game screenshots or honest DECLINED

---

## 16. One-line for chat status

**Ready green; clock-restore fix on disk uncommitted.** Commit → push both → smoke DoD. No mocks. Headless green ≠ gameplay.
