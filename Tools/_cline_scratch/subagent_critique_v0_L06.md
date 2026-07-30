# CRITIQUE - V0 playable drive honesty pass (post V0-L06)

Date: 2026-07-30
HEAD: af8fea4c5 (chore(auto): cement working tree 2026-07-30 20:28:28)
Branch: main ahead of gitlab/main by 2
Unity: not running / LOCK free (per task context)
Policy: Feature without gameplay is DECLINED. Evidence classes PLAYER / MEASURED / EDITOR / STATIC.

Artifacts read:
- Docs/PLAYTEST/V0_VERTICAL_SLICE_EVIDENCE_2026-07-30.md
- Docs/AgentLogs/h8_playprobe_v0_L06.json (MEASURED FAIL)
- Docs/AgentLogs/h8_playprobe_v0_L06.log (key markers)
- Docs/AgentLogs/H8_V0_PLAYTEST_SMOKE_GATE.json (V0-L01 KCC FAIL)
- Docs/AgentLogs/V0_L06_PROBE_RUNBOOK.md
- Probe: Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs
- Boot: Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs
- Menu: Assets/_Project/Scripts/MainMenuController.cs
- Smoke: Assets/_Project/Scripts/Editor/PlayModeSmokeTester.cs
- Shots: Assets/_Project/Scripts/Tools/H8_PlayModeScreenshotter.cs
- Allowlist: Tools/_cline_scratch/commit_v0_allowlist.bat
- Docs/Screenshots/V0_Playtest/ - EMPTY

---

## 0. Verdict in one line

V0 is not playable. Nothing on the captain checklist may be checked. V0-L06 is a useful MEASURED FAIL of the menu gate; it is not progress toward PLAYER proof. Treating it as momentum is self-deception.

---

## 1. Checklist honesty - do not mark [x] from L06 or KCC

### Temptation A: We ran a play probe, so Boot is partially done
REJECT.

V0-L06 JSON facts:
- exitCode=1, failures=3, finalPhase=LeavingPlayMode
- forceMenuLoad=false
- scene stayed on 00_BOOTSTRAP
- Boot FAIL: allSystemsReady=False gameReady=False activationStep=Not started activationCompleted=False
- WorldLoad BLOCKED: no live MainMenuController in 120s
- Swim/Tool/Resource/CraftRepairBuild/Mission/Hazard/SaveLoad = NOT_EXERCISED
- Proof PARTIAL (artifact writer only)
- worldDriver.started=false
- Screenshots: none; Docs/Screenshots/V0_Playtest/ empty

Ledger law: no [x] without PLAYER (or MEASURED where explicitly allowed). L06 is MEASURED proof the route never left bootstrap. That closes zero captain rows. It is anti-evidence for playability.

### Temptation B: KCC gate ran, movement debt is tracked, maybe swim is close
REJECT.

V0-L01: overallPass:false, flags 0x00000042 = Escape|SdfInvalid, kccFailureCount=743920, claimsWorldPlayable:false. Ledger already labels KCC as not WORLD playable. KCC headless never loads WORLD, never spawns a player on the shipping route, never proves camera/look/swim. Separate physics regression lane. Do not launder KCC FAIL into checklist progress, and do not launder KCC PASS into playability either.

### Temptation C: CONTENT-BLOCKED rows mean research done
Half-true, still no [x].

FirstExit/Hazard CONTENT-BLOCKED lines are honest STATIC/probe census findings. Backlog, not pass. They do not matter until Boot+WorldLoad pass - fixing a missing life-pod prefab while bootstrap dies in Environment is rearranging deck chairs.

### Temptation D: auto-cement / ahead-by-2 means the V0 drive is committed and real
REJECT - and reverse the moral.

HEAD af8fea4c5 cemented Tools/_cline_scratch/* launcher/poll/commit junk - the denylist. L06 JSON/log/runbook are not in that cement commit. Tracked V0 evidence still thin: ledger + KCC JSON exist; L06 route artifact is on disk but last cement did not protect the right files. Ahead-by-2 is pollution + illustration/headless noise, not a playable milestone.

Rule restated:
- MEASURED menu-gate FAIL != Boot [x]
- MEASURED KCC FAIL/PASS != Swim [x]
- EDITOR scene root ReportOnly != Boot [x]
- STATIC code exists != any [x]
- Empty V0_Playtest/ = visual proof absent for every screenshot row

Captain checklist remains 7/7 open. Correct.

---

## 2. Biggest missing thing / least confidence / what the user may not realize

### Biggest missing thing
A single human or graphics-on instrumented run of the real shipping route that reaches 02_HECTON_WORLD with a controllable player.

Not another headless ecology short-circuit. Not KCC. Not sandbox smoke. Not README illustrations (6a73392e7). Not more AgentLogs noise.

Until Boot->Menu->New Game->WORLD->spawn->move is observed once, every downstream system (tools, fauna, death, save) is speculative fanfic with unit-test cosplay.

### Least confidence
That batchmode-nographics can ever become the PLAYER-proof producer.

L06 burned ~127s wall, ~3.8M probe ticks, ~7k game frames, never left 00_BOOTSTRAP, never opened a menu, never started the world driver, wrote no PNG, produced no comparable lockstep hash (OwnerPresentBufferUnopened). The harness is sophisticated and still measured BIOS death, not a game.

Confidence that one more probe flag gets PLAYER proof: LOW. Confidence that Environment bootstrap is broken on this route right now: HIGH (see section 3A).

### What the user likely does not realize

1. Bootstrap is hard-failing in Environment before menu is even eligible.
   Log:
   - Bootstrap dependency failed. phase=Environment node=OceanKinematicsRuntimeService
   - Bootstrap phase failed. phase=Environment
   - Concurrent poison: InvalidOperationException NativeMemoryTrackingBridge registration failed for NativeFaultDumpWriter transient payload from HectonSeismicTideDirector.DumpCelestialTelemetry during LateFrameTick
   Probe end state: allSystemsReady=False, services Dispatcher/Tick/Save/Pool ok, but _isBootstrapComplete never true -> menu never produced by the game -> WorldLoad BLOCKED.
   So the menu gate fail is a SYMPTOM. The disease is Environment phase + telemetry dump side effects during boot.

2. -h8headless short-circuit is a different product path than the play probe.
   Ecology batch intentionally stays on bootstrap and MarkMainMenuReached without loading 01_MAIN_MENU. That is why V0-L05 can finish while proving zero play. L06 correctly does not pass -h8headless - good - but agents keep citing headless ecology as adjacent proof. It is not adjacent; it is a bypass.

3. PlayModeSmokeTester never loads WORLD.
   It opens 01_MAIN_MENU then 020_RENDER_SANDBOX. Ledger already says this. Anyone waving smoke-tester green at V0 is lying by omission.

4. Life-pod / hazard CONTENT-BLOCKED is real, but second-order.
   Zero LifePod/DropPod prefabs on disk. FaunaBrain GUID f97102d76d9d9d04f95ccebcd55b7079 has 0 prefab/scene references. Those block later checklist rows. They do not explain why you cannot reach WORLD today.

5. Git is already dirty in the wrong direction.
   Auto-cement committed denylist scratch. Allowlist bat exists and was designed to stage Docs/PLAYTEST + V0 logs + shots - and then cement ignored that discipline. Remote ahead-by-2 may include scratch the user never wanted on gitlab/main.

6. Illustrations and optimization JSON are not playability.
   6a73392e7 is concept art + headless runner edits. Pretty. Irrelevant to captain checklist.

---

## 3. Separation of concerns (stop mixing these lanes)

### (A) Boot integration bugs blocking ALL gameplay proof

These stop every PLAYER row and most honest MEASURED route rows:

1. Environment bootstrap FAIL on OceanKinematicsRuntimeService during L06 (MEASURED in h8_playprobe_v0_L06.log). Until Environment phase completes, AreAllSystemsReady() stays false (_isBootstrapComplete && Dispatcher && TickManager && Save && ObjectPool). SceneActivate never meaningfully starts (activationStep=Not started).

2. Celestial telemetry dump throwing in boot (HectonSeismicTideDirector -> NativeFaultDumpWriter.CreateTransientPayload -> NativeMemoryTrackingBridge registration fail). Side-effect I/O during boot that can kill or poison the boot path. Not a content problem.

3. No live MainMenuController within menu window because the game never finishes boot to present one. Probe correctly refused -h8ForceMenuLoad default (additive force-load deadlocks world activation - probe comments are right). Forcing menu over a dead bootstrap is a MOCK, not a fix.

4. Shipping route still unproven post-APPLY (d7e461e67 WORLD root lift). ReportOnly says active WORLD root exists (EDITOR/MEASURED scene graph). That is not Play Mode boot.

5. Determinism owner buffer never opens on failed boot (OwnerPresentBufferUnopened). Not the priority, but it means L06 cannot even serve as a regression hash baseline.

If you only fix one class of thing, fix (A). Everything else is theater.

### (B) Content blockers (real, but gated behind A)

- No life-pod / drop-pod prefab in project (scripts + quest asset exist; zero prefab hosts). FirstExit cannot be driven.
- No hazard placement / zero AddComponent sites for hazard types named by probe -> Hazard row content-blocked.
- Fauna not player-visible on route: FaunaBrain orphan (GUID only on own meta; 0 scene/prefab refs); VAT swarm prefab degeneracy (ledger).
- World content sockets x14 with shipping filter - live Play Mode count unproven.
- Historical screenshots pre-APPLY / near-black - cannot close V0-S0x.

### (C) Systems implemented-not-integrated (do not finish these for V0 glory)

| System | Why it is not proof |
|---|---|
| FaunaBrain (+ partials) | Code + EditMode tests; no creature hosts it |
| PlayModeSmokeTester | Menu + 020_RENDER_SANDBOX only - never 02_HECTON_WORLD |
| Headless ecology / -h8headless | Short-circuits SceneActivate on bootstrap; no Player |
| FloraGenomeVaultRuntime | Lab / zero production callers (ledger) |
| WorldChunkResidencyManager | Deliberate non-construction |
| SwimPresentationProfile lower body | Param accepted, never read |
| Isolated save/fauna/tools smoke MBs | No single WORLD player-route gate |
| Forge FBX set | On disk, not placed as live content proof |
| Quest_FirstHour_ExitLifePod.asset | Data without pod prefab / boot route |

Wiring comments in bootstrap installers != PLAYER. Stop celebrating installer graphs.

### (D) KCC debt unrelated to play route

- V0-L01 FAIL Escape|SdfInvalid, 743920 failures, PrecisionDrift clear, cone contract pass.
- Useful as physics CI debt after a player can stand in WORLD, or in parallel only if it does not steal the only Unity lock from Boot work.
- Does not unblock menu, spawn, tools, fauna, save.
- Do not sequence fix KCC then playtest as if KCC were the gate to WORLD. It is not. Bootstrap Environment is.

---

## 4. Next 5 tasks that actually open PLAYER proof

Reject: more README art, more cement, KCC mass triage as P0, FaunaBrain refactors, ecology biomass headless, forceMenuLoad cosplay, sandbox smoke greening, new ledger essays without a run.

### P0-1 - Fix Environment bootstrap hard-fail (OceanKinematicsRuntimeService + celestial dump)
- Reproduce with graphics-on editor Play Mode from 00_BOOTSTRAP (not only -nographics).
- Capture the actual exception under TryInitializeBootstrapDependencyNodeWithFallback for OceanKinematics (L06 log line is failure announcement; root exception may be earlier - pull full node init error, not only the seismic dump stack).
- Make boot survive without requiring a telemetry dump to native bridge during Environment init. Dump failures must not fail the phase.
- Exit criterion: AreAllSystemsReady()==true and BootstrapState.IsGameReady==true with active scene progressing past bootstrap toward 01_MAIN_MENU on the non-headless path.

### P0-2 - One honest Boot->Menu->New Game->WORLD run (PLAYER or graphics-on MEASURED)
- Human Play Mode or probe WITHOUT -nographics, with screenshotter allowed to write under Docs/Screenshots/V0_Playtest/.
- Do NOT pass -h8ForceMenuLoad until P0-1 is fixed; forced menu on dead boot is a mock.
- Do NOT pass -h8headless.
- Exit criterion: V0-S01 + V0-S02 on disk, analyzed non-black, checklist rows 1-2 eligible for PLAYER evidence. Update ledger with paths - still no checkbox until images inspected.

### P0-3 - Spawn usable + swim ~30s on WORLD
- Player at AUP, camera look, move underwater, no soft-lock / PrecisionDrift.
- This is the first real game moment. Collider/fall-through issues become real here - fix only what blocks this minute.
- Exit criterion: V0-S03 + log notes; checklist row 3.

### P0-4 - One tool use once
- Equip/use a single shipped tool on the player route (not a sandbox MB).
- Exit criterion: V0-S04; checklist row 4.

### P0-5 - One live fauna visible/reacting on route
- Not FaunaBrain architecture festival. Place or enable one living creature the player can see. If that requires binding FaunaBrain to a prefab, do the minimum host wiring - not a rewrite.
- Exit criterion: V0-S05; checklist row 5.

Defer explicitly: death/respawn, save/load roundtrip, life-pod prologue authorship, hazard placement, KCC Escape|SdfInvalid mass failures, headless biomass, PlayModeSmokeTester WORLD expansion - until rows 1-3 exist. Death/save are captain rows 6-7; they matter, but they are unreachable while Boot FAILs.

---

## 5. Probe design critique

### forceMenuLoad=false default - CORRECT, keep it
Probe comment and L06 behavior agree: additive force-load of menu over incomplete boot deadlocks world-scene activation and is not a state the game produces. Default false is honesty.
Do NOT fix L06 by flipping -h8ForceMenuLoad to green WorldLoad. That manufactures a menu on a corpse.

When would force be legitimate? Only as a labeled diagnostic after boot services are ready but menu scene failed to stream - still not PLAYER proof.

### hardTimeout / budget defaults - still footguns
Code defaults: timeout 240s, menu wait 300s, settle 300s. Probe emits BUDGET WARNING when windows exceed timeout - good. L06 overrides (900 / 120 / 180 / 90) are sane and fit.

Remaining issues:
- Defaulted runs without the bat still TIMEOUT mid-route and mis-teach agents.
- L06 spent the entire 120s menu window knowing boot already failed Environment early. Smarter probe would fail-fast WorldLoad/Boot when Bootstrap phase failed is observed or when allSystemsReady is impossible (phase failed + not headless), instead of politely waiting 120s. That is harness efficiency, not product pass.
- Top-level JSON gameFrames:0 while phases show 7141 frames during LoadingMenu is confusing for skimmers (top-level field is end-state after leave-play). Document or fix aggregation so agents stop misreading zero frames.

### -nographics cannot close PNG rows - HARD LAW
H8_PlayModeScreenshotter explicitly refuses pixel writes under -nographics and logs that ScreenCapture/camera produce no pixels. AGENTS.md already bans -nographics for MapMagic/compute.

L06 launch bat uses -batchmode -nographics. Therefore:
- V0-S01..S07 cannot be closed by L06-class launches. Ever.
- Any agent claiming screenshot proof from nographics is fabricating.
- Route JSON Proof moment already admits screenshot/clip have no producer this run.

### Other probe honesty (keep)
- CONTENT-BLOCKED vs NOT_EXERCISED vs BLOCKED distinctions are good.
- claimsWorldPlayable absence on KCC gate is good.
- Exit code reflecting Required Route FAIL (failures=3) is good - do not chase exit 0 with mocks.
- World driver not started when menu never came - correct.

### Probe anti-patterns to reject
- Raising menu wait to 600s on a known Environment FAIL (busywait).
- forceMenuLoad to skip Boot FAIL.
- Treating PARTIAL Proof as pass.
- Running probe under -h8headless to get further.
- Cementing probe launch scripts instead of the JSON/log evidence.

---

## 6. Git hygiene

### Allowlist (OK to commit when real)
- Docs/PLAYTEST/** ledger updates with honest open checkboxes
- Docs/AgentLogs/h8_playprobe_v0_L06.json + .log
- Docs/AgentLogs/V0_L06_PROBE_RUNBOOK.md
- Docs/AgentLogs/H8_V0_PLAYTEST_SMOKE_GATE.json + v0_kcc_gate_*.log
- Docs/Screenshots/V0_Playtest/** only when non-empty real captures exist
- Narrow product/bootstrap fixes under Assets/_Project/... that address P0-1 (real code, not scratch)

Use Tools/_cline_scratch/commit_v0_allowlist.bat intent: stage those paths, deny-scan, commit, pull --no-rebase, push. Do not git add -A.

### Denylist (never stage)
- Tools/_cline_scratch/** (launchers, polls, b64, recon outs, this critique may stay local)
- Tools/*cline* generally
- tokens, .env, credentials, remotes junk
- XR OpenXR noise / unrelated Library dirt
- Mega editor logs not needed once JSON summary exists (optional: keep one L06.log as measured fail evidence; scratch bats are not)

### Current hygiene FAIL
| Commit | Problem |
|---|---|
| af8fea4c5 cement | Staged denylist Tools/_cline_scratch/* (allowlist bat, poll status, pid, launch bat, etc.). Wrong files. |
| 6a73392e7 | Also packed extensive Tools/_cline_scratch/* plus illustrations. Ahead-by-2 vs gitlab is not V0 evidence pushed. |
| Working tree | L06 JSON/log on disk; cement did not prioritize them. Scratch continues to dirty status. |

Remediation advice (for main agent, not done by this subagent):
1. Stop auto-cement or exclude Tools/_cline_scratch from cement allow path.
2. Allowlist-commit Docs evidence only (ledger + L06 + KCC) if not already on remote.
3. Do not force-push. pull --no-rebase then push.
4. If scratch must vanish from history later, that is a conscious history rewrite - out of scope for V0 play; at minimum stop adding more.

---

## 7. Blunt closing

V0-L06 did its job: it proved the automated route cannot press New Game because bootstrap dies in Environment and never presents a menu. That is a sharp FAIL.

What would be dishonest next:
- Checking any captain box
- Calling L06 a partial win
- forceMenuLoad
- More KCC as substitute playtest
- Cementing scratch while PLAYER screenshots folder stays empty
- Integrating FaunaBrain into nothing

What would be honest next:
1. Fix OceanKinematics / Environment boot + stop telemetry dump from killing boot
2. Graphics-on Boot->WORLD with PNGs in Docs/Screenshots/V0_Playtest/
3. Swim 30s
4. One tool
5. One fauna

No feature without gameplay. No checkbox without PLAYER. L06 is MEASURED FAIL. Act like it.

---

## Relevant file paths

Docs/PLAYTEST/V0_VERTICAL_SLICE_EVIDENCE_2026-07-30.md
Docs/AgentLogs/h8_playprobe_v0_L06.json
Docs/AgentLogs/h8_playprobe_v0_L06.log
Docs/AgentLogs/H8_V0_PLAYTEST_SMOKE_GATE.json
Docs/AgentLogs/v0_kcc_gate_2026-07-30.log
Docs/AgentLogs/V0_L06_PROBE_RUNBOOK.md
Docs/Screenshots/V0_Playtest/
Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs
Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs
Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs
Assets/_Project/Scripts/MainMenuController.cs
Assets/_Project/Scripts/Editor/PlayModeSmokeTester.cs
Assets/_Project/Scripts/Tools/H8_PlayModeScreenshotter.cs
Assets/_Project/Scripts/Fauna/FaunaBrain.cs
Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs
Tools/_cline_scratch/commit_v0_allowlist.bat
Tools/_cline_scratch/launch_v0_L06_probe.bat
AGENTS.md
