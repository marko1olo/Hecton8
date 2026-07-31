# V0 L16 — Batchmode step-bounded simulation clock (PlayModeProbe)

**Status:** IMPLEMENTED (product). LIVE verification pending.  
**HEAD at implement:** post-L15 `9f4169ffd` + this change.  
**Swim PASS still requires LIVE:** hop2 present + `movementIntent01max > 0`.

## Root (L16 dig)

L15 dual-register heal shipped and LIVE still failed: hop2 ABSENT, `movementIntent01max=0`, while WorldDriver published ~258k overrides and hop1 `currentStateMove` real metrics were `(0,1)`.

Causal chain (product path only):

1. hop2 is recorded only inside `InputDispatcher.GetState` via `DiagRecordReadObservation(2)`.
2. GetState is reached from HPM FixedTick → SampleGameplayLocomotionInputForFixedStep → ProcessPlayerInputFrame → TryReadFrame.
3. FixedTick runs only when `SystemDispatcher.RunFixedStepAccumulator` sees `dilatedDeltaTime > 0`.
4. Batchmode WallClock often yields `unscaledDeltaTime == 0`; pause/dilation collapse → dilatedDt=0 → accumulator early-out → **no FixedTick → no hop2**.
5. `HeadlessSimulationRunner` already arms the real clock: unpause + `RequestHeadlessTimeDilation(100f)` + `SystemDispatcher.EnableStepBoundedTime(0.04f)`.
6. `H8_HeadlessPlayModeProbe` had **zero** calls to `EnableStepBoundedTime` / headless dilation. WorldDriver is an INPUT PRODUCER only — it must not pump FixedTick/GetState.

## Fix (product, not mock)

File: `Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs`

Mirror `HeadlessSimulationRunner.EnsureHeadlessSimulationClock`:

| Piece | Value |
|-------|--------|
| `ProbeTimeDilationScalar` | `100f` |
| `ProbeStepBoundedDeltaSeconds` | `0.04f` (≤ MaxStepBoundedDeltaSeconds) |
| `ProbeClockEnsureIntervalSeconds` | `5f` |
| `ProbeSimClockHash` | `0x48385043u` (`H8PC`) |
| Arm | `EnsureProbeSimulationClock(reason)` on gameplay window start + before `WorldDriver.Begin` |
| Sustain | `MaybeEnsureProbeSimulationClockSustain()` each GameplayWarmup tick (throttled; force if paused/dilation collapsed/step-bound dropped) |
| Reset | clear `_lastProbeClockEnsureRealtime` / `_probeSimClockArmed` in `ResetRunState` |
| Evidence | `[H8_PLAYPROBE] SIMCLOCK ensure reason=... stepBoundAfter=...` |

Access: `SystemDispatcher.EnableStepBoundedTime` / `IsStepBoundedTimeActive` are `internal static`; `InternalsVisibleTo("Hecton8.Editor")` already grants the probe assembly.

**Explicit non-goals (rejected):**

- Mock hop2 / forge INPUTHOP census
- Call `GetState` or `FixedTick` from WorldDriver or probe
- Unregister thrash / dual-register churn as clock substitute
- Treat WORLDDRIVER tick counts as FixedTick evidence

## L15 correction — `currentStateMove=(0,0)` was a poll artifact

L15 LIVE results briefly claimed a regression to `currentStateMove=(0,0)`. That was **false**.

- `_l15_poll.py` used last-match regex on `currentStateMove=` and latched **prose help text** containing the literal `(0,0)`, not the metric line.
- Every real hop1 METRIC line in the L15 log is `currentStateMove=(0,1)` (forward intent published).
- Residual was never “override not applied to CurrentInputState”; residual was **consumer FixedTick never ran**.

## LIVE acceptance (Swim)

| Gate | Pass |
|------|------|
| SIMCLOCK log | `stepBoundAfter=1` at least once in gameplay |
| hop2 | present in INPUTHOP census |
| `movementIntent01max` | `> 0` |
| depth / immersion | prefer depth>0; immersion already 1 on L15 |

If SIMCLOCK arms and hop2 still absent → dig menu-block Sample early-out (`IsGameplayInputBlockedByMenu`) next (L17), not re-arm thrash.

## Related

- L15: dual-register heal (`GlobalRegistry` + sticky player lane) — necessary, not sufficient.
- Pattern source: `HeadlessSimulationRunner.EnsureHeadlessSimulationClock` / `MaybeEnsureHeadlessSimulationClockSustain`.
