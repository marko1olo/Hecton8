# CRITIQUE - V0 Boot P0 Applied (adversarial)
**Agent:** Subagent CRITIQUE
**Repo:** C:/hades/Hecton8
**HEAD at critique:** df139d50f cement 2026-07-31 - ahead gitlab/main by 4
**Prior HEAD named in brief:** e08089409 (ahead 3)
**Verdict: P0 INSUFFICIENT + PARTIALLY DISHONEST. Keep V0-L06 MEASURED FAIL. Do not green. Do not checklist.**

---

## 1. What was actually patched (read of diffs)

### CoreLowLevelUtilities.cs / NativeFaultDumpWriter
- CreateTransientPayload: if !NativeMemoryTrackingBridge.IsInstalled, return untracked NativeArray (no register).
- DisposeTransientPayload: if bridge down OR registration id missing -> dispose + return (no throw).
- STILL THROWS when bridge IS installed but TryRegister returns false (same InvalidOperationException message).

### HectonSeismicTideDirector.cs / DumpCelestialTelemetry
- Added catch InvalidOperationException + catch Exception around dump (DEV warning only).
- Telemetry dump can no longer escape LateFrameTick for any managed failure.

### GameBootstrapper.cs
- Dependency-exception log now includes node= + exception.ToString() (was bare label - L06 diagnosability hole).
- Ocean node: TryEnsureDeferredCausticsRegistered wrapped in try/catch (log + continue).
- Caustics reflection EnsureRuntimeInstance/InitializeService Invoke wrapped to LogDeferredCausticsWiringFailure.

### Ledger V0-L06 section
- Correctly records MEASURED FAIL, forceMenuLoad=false, zero screenshots, checklist still open.
- Correctly rejects forceMenuLoad / -h8headless as play proof / EmergencyMockOcean / checklist without PLAYER.
- Claims P0 product fix applied and frames dump throw as root cause that could poison boot.
- Next proof gate after recompile - NO post-fix playprobe artifact exists.

---

## 2. L06 evidence (measured, pre-P0)

**Artifacts:** Docs/AgentLogs/h8_playprobe_v0_L06.json, Tools/_cline_scratch/v0_L06_exc_slice.txt, v0_L06_boot_errors.txt

| Fact | Value |
| --- | --- |
| exitCode | 1 |
| failures | 3 |
| forceMenuLoad | false (correct) |
| scene | stayed 00_BOOTSTRAP |
| Boot | FAIL - allSystemsReady=False gameReady=False activationStep=Not started |
| WorldLoad | BLOCKED - no MainMenuController in 120s |
| worldDriver.started | false |
| PLAYER PNGs | zero (Docs/Screenshots/V0_Playtest empty) |
| gameFrames during menu wait | ~7141 (alive but not progressing product boot) |

### Two concurrent events - NOT the same stack

**A. Bootstrap path (Environment node)** log ~2099-2184:
- TryInitialize OceanKinematicsRuntimeService
- [GameBootstrapper] Bootstrap dependency exception.  <- NO exception text (pre-P0 logger)
- Bootstrap dependency failed phase=Environment node=OceanKinematicsRuntimeService
- Bootstrap phase failed phase=Environment
Stack is entirely inside TryInitializeBootstrapDependencyNodeWithFallback. Exception payload was swallowed. We still do NOT know type/message of Ocean node throw.

**B. LateFrameTick path** log ~2227-2237 (separate):
- InvalidOperationException: NativeMemoryTrackingBridge registration failed for NativeFaultDumpWriter transient payload.
- at NativeFaultDumpWriter.CreateTransientPayload
- at HectonSeismicTideDirector.WriteCelestialTelemetryDump ... LateFrameTick
- at SystemDispatcher.RunDispatcherLateFrame
This is dispatcher late-frame, not the Ocean dependency initializer. EnvironmentRuntimeContextService already succeeded earlier (seismic created + ticking) while Ocean node reported dependency exception.

### Causal honesty
Ledger language that the dump throw could poison boot is a HYPOTHESIS, not a measured causal edge. Chronology is concurrent, stacks are disjoint. P0 treats the dump as if it were the Ocean failure. That is the core honesty failure.

---

## 3. Architect questions

### Least confident about boot->player after this fix?
That OceanKinematicsRuntimeService will initialize on the next playprobe.
We never captured the Ocean exception body. P0 patches:
1. a side-channel (celestial telemetry dump), and
2. a speculative caustics reflection path with ZERO L06 stack frames naming caustics / AbyssalDeferredCaustics / Invoke.

Ocean node body is still: EnsureRuntimeInstance -> PersistRuntimeService -> InitializeService -> (optional caustics) -> IsReady(GlobalRegistry.OceanKinematics).
OceanKinematicsRuntimeService.InitializeService is mostly registry/provider refresh - if throw was elsewhere (Persist, registry contract, missing provider policy, plugin validation earlier), P0 misses it. Next probe may still die on Environment with newly-visible exception text - progress, not PASS.

### Biggest thing missing about the situation?
NO post-P0 re-measure.
Code changed + ledger wrote fix applied + cement committed 32k lines of scratch/bak - and there is still:
- no new h8_playprobe after df139d50f
- no Boot moment PASS
- no menu
- no WORLD
- no PLAYER PNG
Fix applied without a second MEASURED run is green theater adjacent, even when ledger still says FAIL.

### What do we not realize?
1. L06 dump message does NOT prove !IsInstalled. CreateTransientPayload throws the same TransientPayloadRegistrationFailureMessage whenever TryRegister returns false - including IsInstalled && RegisterNativeArrayInstance <= 0 and remember-slot full. P0 only early-returns on !IsInstalled. Installed-but-register-failed STILL THROWS on Create. Dump catch Exception papers over for seismic only; other CreateTransientPayload callers remain hard-fail.
2. Silencing dump != Ocean ready. Even if Environment starts completing, Boot->Menu->New Game->WORLD->swim is a long chain. Prior debt: KCC gate FAIL 0x42, headless ecology != play, content blocks (life-pod, hazard), world roots historically off, ecosystem population solve never ran.
3. Cement bot is a product risk. df139d50f auto-cemented *.bak_v0boot + Tools/_cline_scratch/** into main. Not hygiene; contamination.

### Which implemented systems are NOT integrated to gameplay?
(Non-exhaustive; exist in tree; did not participate in playable route on L06.)

| System / area | Why not integrated on evidence |
| --- | --- |
| OceanKinematicsRuntimeService + Crest/vault providers | Environment node failed; no live ocean gameplay path proven |
| EmergencyMockOceanKinematicsAdapter | Rejected as V0 provider (correct); temptation mock |
| Celestial/seismic telemetry dump | Side channel only; never gameplay; further isolated |
| Abyssal deferred caustics | Cosmetic; reflection-wired from Ocean node; not gameplay gate |
| MainMenuController / New Game | Never live (WorldLoad BLOCKED 120s) |
| World driver / WORLD scene | worldDriver.started=false; never left bootstrap |
| Player movement / KCC | Prior V0-L01 MEASURED FAIL; not re-exercised |
| Ecosystem population solve | Historical never-run; L06 never reached ecology play |
| Quest/hazard/life-pod/craft/save | NOT_EXERCISED or CONTENT-BLOCKED on L06 JSON |
| Lockstep master hash buffer | Owner present, buffer unallocated - not comparable |
| HeadlessSimulationRunner ecology path | Different product path; not play proof (correctly rejected) |

---

## 4. Risk: OceanKinematics still failing for a DIFFERENT root cause?

### YES - high probability we only silenced symptoms / side channels.

| Claim in P0 narrative | Adversarial read |
| --- | --- |
| Dump registration killed Environment boot | UNPROVEN. Dump stack != Ocean init stack. Seismic already running from earlier Environment node. |
| !IsInstalled is the dump failure mode | INCOMPLETE. Same exception string for any failed register; P0 Create still throws if installed+register fails. |
| Caustics Invoke was killing Ocean node | SPECULATIVE. No L06 frame mentions caustics. Wrap is cheap insurance, not root-cause fix. |
| Logging exception.ToString() fixes boot | DIAGNOSTICS ONLY. Necessary; not sufficiency. |
| Real integration, not mocks | Partially true intent (no forceMenuLoad, no mock ocean) but outcome unproven - zero play integration evidence. |

Belt-and-suspenders on dump path is fine engineering (Create soft + dump catch). Calling that OceanKinematics fixed is not.

If Ocean still throws post-P0, the new logger should finally print it. Until that run: Ocean root cause = UNKNOWN.

---

## 5. Git hygiene - FAIL

**Observed in df139d50f (cement):**
- Assets/.../*.cs.bak_v0boot (+ .meta) for GameBootstrapper, CoreLowLevelUtilities, HectonSeismicTideDirector
- Docs/PLAYTEST/...md.bak_v0boot
- Mass of Tools/_cline_scratch/** (apply scripts, exc slices, gate outs, multi-k line dumps)
- ~32k insertions, majority junk

**Required policy (non-negotiable):**
- Allowlist commits of product code + intentional docs/evidence only.
- Denylist / NEVER commit: Tools/_cline_scratch/**, *.bak_v0boot, *.bak_v0boot.meta, agent scratch unless promoted to Docs/AgentLogs with measured gate name.
- Cement/auto commit must not sweep denylist paths onto main.

**Action debt:** history already contains junk at df139d50f. Do not fix by another cement. Prefer deliberate remove commit; stop cement allowlist hole.

git check-ignore does NOT protect _cline_scratch or *.bak_v0boot today - denylist missing from ignore rules.

---

## 6. Reject list (reaffirm - do not accept as proof)

| Proposal | Ruling |
| --- | --- |
| forceMenuLoad / -h8ForceMenuLoad | REJECTED - mock menu on dead boot |
| -h8headless ecology short-circuit as play proof | REJECTED - different product path |
| EmergencyMockOcean as V0 play provider | REJECTED |
| Captain checklist [x] without PLAYER PNGs + controllable spawn | REJECTED |
| Ledger P0 applied as Boot PASS | REJECTED - no post-fix MEASURED |
| Treating dump catch alone as Ocean root-cause closure | REJECTED |

---

## 7. Sufficiency scorecard

| Bar | Status |
| --- | --- |
| Honest L06 FAIL retained | PASS (ledger still FAIL; checklist open) |
| forceMenuLoad not used as proof | PASS |
| Dump cannot kill LateFrameTick | LIKELY PASS (catch-all + softer dispose) |
| CreateTransientPayload never kills when tracking sick | PARTIAL - !IsInstalled soft; installed+fail still throws |
| Ocean node root cause identified | FAIL - exception text never captured |
| Ocean node root cause fixed | FAIL / UNKNOWN |
| Post-fix playprobe Boot>=menu | FAIL - not run |
| PLAYER PNGs / swim / tool | FAIL - zero |
| Git hygiene | FAIL - bak + scratch cemented |
| Overall P0 boot product fix sufficient | FAIL |

---

## 8. What would change the verdict to P0 held

Minimum, in order - no shortcuts:

1. Ignore/denylist Tools/_cline_scratch/ and *.bak_v0boot*; remove them from tree in a deliberate non-cement commit.
2. Re-run same playprobe class as L06: batch playprobe, forceMenuLoad=false, no headless short-circuit as proof.
3. Require log proof: either Environment phase completes and Boot moment improves (allSystemsReady / menu eligible), OR Ocean node still fails with full exception.ToString() -> new root cause ticket (not another telemetry bandage).
4. Only then graphics-on route for V0-S01..S03 under Docs/Screenshots/V0_Playtest/.
5. Checklist stays open until PLAYER evidence exists.

---

## 9. Bottom line

P0 is a reasonable defensive patch set for a non-critical telemetry side channel and a necessary logger fix, plus a speculative caustics shield. It is NOT a demonstrated fix for OceanKinematicsRuntimeService bootstrap failure, NOT a boot->player proof, and the ledger causal story overfits concurrent dump noise.

Git cement of bak+scratch is an independent FAIL that must not ride along as product fix.

**Keep the board red. Prefer FAIL honesty over green theater.**

---

## Relevant file paths
Assets/_Project/Scripts/Core/Contracts/CoreLowLevelUtilities.cs
Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs
Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs
Assets/_Project/Scripts/Core/OceanKinematicsRuntimeService.cs
Docs/PLAYTEST/V0_VERTICAL_SLICE_EVIDENCE_2026-07-30.md
Docs/AgentLogs/h8_playprobe_v0_L06.json
Tools/_cline_scratch/v0_L06_exc_slice.txt
Tools/_cline_scratch/v0_L06_boot_errors.txt
Tools/_cline_scratch/subagent_critique_v0_boot_p0_applied.md
