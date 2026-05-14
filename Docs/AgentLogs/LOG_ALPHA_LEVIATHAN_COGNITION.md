# LOG_ALPHA_LEVIATHAN_COGNITION

Status: PENDING VERIFICATION

What was wrong:
- Standard predator cognition could collapse into direct chase/bite behavior.
- Alpha Leviathan had no explicit fog-stalking phase lane, no false-charge/no-hit authority, and no dedicated black-box phase telemetry.
- Local generated build path is not trustworthy: `Hecton8.Core.csproj` fails with 131 missing cross-asmdef/generated references before a useful domain compile.

What was done:
- Added `Hecton8.AI.Cognition` contract asmdef with Alpha phase constants and 64-byte telemetry entry.
- Added Alpha SoA state to `PredatorCognitionDomain`: phase byte, phase start time, 300-entry telemetry ring, and binary dump on invalid state.
- Consumed `AcousticPingSignal` in `CreatureUtilityBrain` as an AUP target; ignored Leviathan roar echo.
- Added fog-ring circling at `FogEnd - 10m`, gaze/headlight dive behavior, low-tier radial fallback, 30 m/s false charge, roar signal, and <15m veer-off with `ShouldAttack` cleared.
- Bypassed biomass/ecology overrides for apex predators.
- Kept Alpha evaluation on 10Hz slow-tick cadence.

Cinematic Cheats used:
- Fog silhouette ring, not honest orbit physics.
- One-gradient SDF dive fake on high tier; radial break on low tier.
- Dear-lie false charge: Feint + roar + no hit.
- Binary black box instead of per-frame text logs.

Microseconds saved:
- Exact measured microseconds: unavailable; Unity session unavailable and local generated build is dependency-red.
- Static hot-path estimate: radial low-tier dive saves the SDF-gradient branch, estimated ~0.09 us per active Alpha slow tick versus high tier.
- Static hot-path estimate: no NavMesh/path spline saves an unbounded main-thread query; Alpha branch remains scalar Burst math, estimated <0.1 us per active Alpha eval.
- Static allocation saving: 0 B/frame in Alpha hot path; telemetry is fixed 19.2 KB session memory.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`: BLOCKED BY DEPENDENCY, 131 global missing-reference errors.
- `dotnet build-server shutdown`: completed.
- Unity MCP refresh: timed out after 60s; console unavailable because no Unity session was attached.
- Static scans: Alpha direction/distance paths use `math.rsqrt`; no new managed collection/LINQ allocation in Alpha hot branch.

---

Second-pass upgrade: 2026-05-14

What was wrong:
- Acoustic ping AUP conversion used the implicit current-origin helper before cognition input packed its own floating-origin offset. That allowed a mixed-origin target if rebasing happened inside the evaluation window.
- Phase byte `3` behaved correctly as the no-hit veer-off but was not explicitly exposed as `Strike`, which weakened the batch contract for telemetry consumers.
- The false charge relied on acoustic/physiology ordering for the stress spike, while prompt DOD requires immediate `PlayerStress01` pressure.

What was done:
- Re-extracted `<AGENT_PROMPT id="ALPHA_LEVIATHAN_COGNITION" ...>` from `Docs/Tasks/CURRENT_BATCH.md` with an attribute-aware CLI regex.
- Captured `HectonFloatingOrigin.CurrentTotalOffset` once per `CreatureUtilityBrain.Evaluate`, used that same `float3` for acoustic `AUPMath.ToRuntimeFloat3(...)` and `CognitionInput.FloatingOriginOffset`.
- Added `AlphaLeviathanPhase.Strike = 3` and kept `VeerOff = Strike` so byte phase 3 satisfies the prompt while still enforcing the no-hit escape.
- Added a one-shot `PlayerStressSignal` on Alpha roar emission via `GlobalSignals`, with `Stress01 = 1`, apex/acoustic flags, and no direct physiology mutation.

Cinematic Cheats used:
- Coordinate lock: one committed-origin snapshot for the full cognition evaluation instead of chasing live Transform state.
- Phase-3 lie: telemetry can call it Strike; runtime still veers off and strips attack.
- Stress spike: one 32-byte global signal, not a sustained simulation or direct player-system coupling.

Exact Microseconds saved:
- Measured microseconds unavailable; Unity editor MCP transport is offline and local generated build remains dependency-red.
- AUP fix cost: one `Vector3` read + `float3` pack per slow evaluation; expected <0.01 us on i3/MX350-class hardware, 0 B/frame.
- Strike alias cost: compile-time constant only, 0 us.
- Stress signal cost: one 32-byte queue write on false-charge transition only, 0 B/frame hot path.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`: still blocked by generated/cross-asmdef dependency wall, now 127 errors. Primary examples: missing `Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Audio.Virtualization`, stale generated project reference for `Hecton8.AI.Cognition`.
- Unity MCP `refresh_unity` and `read_console`: failed at HTTP transport to `127.0.0.1:8088/mcp`; no Unity console proof available.
- `git diff --check`: no whitespace errors; only repository LF-to-CRLF warnings.
- Static scans: no remaining implicit `PositionAup.ToRuntimeFloat3()` acoustic conversion in `FaunaBrain.Compatibility.cs`; Alpha hot-path allocation scan only reports pre-existing instance scratch lists in `FaunaBrain`.

---

Third-pass upgrade: 2026-05-14

What was wrong:
- Hidden/dive phase could collapse back to circling after one 10Hz evaluation once gaze or retinal exposure stopped.
- Alpha black-box telemetry could claim SDF dive on low tier even though low tier intentionally uses radial fake steering.
- Telemetry did not recompute and record the player-gaze break bit, reducing postmortem value.

What was done:
- Added `AlphaHiddenHoldSeconds = 1.15f` to keep phase 0 readable after gaze/headlight break without adding another state lane.
- Added `ResolveAlphaTelemetryDirection(...)` using `math.rsqrt` and zero-allocation scalar math.
- Changed Alpha telemetry flags so `PlayerGazeBreak` reflects the actual dot test and `SdfDiveRequested` only appears for high-tier Hidden with a player target.

Cinematic Cheats used:
- Long-enough vanish: a 1.15s fake hide, not pathfinding or burrow simulation.
- Truthful telemetry: low tier records the cheap radial fake; high tier records the SDF visual fake.

Exact Microseconds saved:
- Measured microseconds unavailable; Unity editor transport remains offline.
- Hidden hold costs one scalar comparison per 10Hz Alpha evaluation, estimated <0.01 us on i3/MX350.
- Telemetry direction recompute costs two `math.rsqrt` paths per active Alpha post-eval write; no hot-frame heap pressure.

Verification:
- Prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md` with attribute-aware CLI regex.
- Static math scan found no sqrt/normalize/length calls in Alpha-scoped files.
- Allocation scan found no new managed allocation pattern in `PredatorCognitionDomain`; matches are pre-existing `FaunaBrain` scratch lists and guarded/fault logs.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`: still blocked by generated/cross-asmdef reference wall, now 132 errors.
- Unity MCP `refresh_unity` and `read_console`: still offline at `127.0.0.1:8088/mcp`.

---

Fourth-pass upgrade: 2026-05-14

What was wrong:
- Alpha-specific stalking, telemetry, SDF dive, false-charge roar, and stress spike were keyed off generic apex/Leviathan status.
- That made AmbushBurst or SentinelPressure apex creatures eligible for first-hour PresenceCircle behavior, wasting 10Hz cognition budget and contaminating encounter identity.
- Parallel build verification also hit a generated DLL file lock, so the compile signal needed a serialized retry before reporting.

What was done:
- Added `UseAlphaLeviathanCognition` to `CognitionInputFlags`.
- Added `UseAlphaLeviathanCognition` to `CreatureUtilityContext` and packed it through `CreatureUtilityBrain.Evaluate`.
- Added `ShouldUseAlphaLeviathanCognition()` in `FaunaBrain`: legacy species-profile Leviathans stay supported; archetypes opt in through `useFeintRush` or `useLeviathanPresence + LeviathanEncounterType.PresenceCircle`.
- Changed `PredatorCognitionDomain` so Alpha 10Hz cadence, Alpha telemetry, and the false-charge override use the explicit Alpha flag instead of generic `IsApexPredator`.
- Changed `EmitLeviathanThreatPulse` so the Alpha roar/stress spike only fires for the Alpha cognition profile; generic Leviathan pulses still scatter microfauna without publishing Alpha stress.

Cinematic Cheats used:
- Encounter gate: PresenceCircle/feint Alpha gets the deep-fog psychological fake; other apex contracts keep their own pressure model.
- Budget fence: non-Alpha Leviathans no longer pay for Alpha black-box telemetry or 10Hz SDF/gaze checks.
- Roar restraint: one-shot stress remains a designed false-charge beat, not every Feint state.

Exact Microseconds saved:
- Measured runtime microseconds unavailable; Unity runtime/MCP session is still not attached.
- Static avoided work per non-Alpha apex: no Alpha telemetry write, no Alpha phase branch, no Alpha SDF/gaze decision, estimated ~0.05-0.12 us per non-Alpha apex slow evaluation on i3/MX350.
- Hot-path allocation remains 0 B/frame for Alpha cognition; allocation scan only reports pre-existing `FaunaBrain` scratch lists.
- Compile cost was not optimized; build proof was restored by serialized MSBuild after a parallel generated-DLL lock.

Verification:
- Prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md`: `PROMPT_BYTES=2997`, `TASK_MARKERS=17`.
- `rg` scan confirmed `UseAlphaLeviathanCognition` gates Alpha cadence, telemetry, and override paths; generic `IsApexPredator` remains for non-Alpha apex systems.
- Allocation scan found no new managed collection/LINQ path in the Alpha hot branch.
- `git diff --check`: no whitespace errors; only repository LF-to-CRLF warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`: failed once on locked generated `Temp/obj/Hecton8.World.Contracts/Hecton8.World.Contracts.dll`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /clp:ErrorsOnly`: succeeded with 0 errors.

---

Fifth-pass upgrade: 2026-05-14

What was wrong:
- Alpha black-box telemetry still had one evidence-quality hole: if an Alpha slot had no current player/acoustic target, telemetry could resolve the default `PlayerTargetAup` and report a fake player position.
- Adding a new public telemetry flag in `Hecton8.AI.Cognition` risks stale generated asmdef visibility in the local `Hecton8.Core.csproj` path.

What was done:
- Hardened `UpdateAlphaLeviathanPostEvaluationTelemetry` so no-target samples keep `PlayerPosition = core.Position` and `DistanceToPlayerMeters = 0`.
- Added a local domain bit, `AlphaLeviathanTelemetryNoPlayerTarget = 1 << 5`, without changing the 64-byte public telemetry contract layout.
- Preserved high-tier SDF/gaze telemetry only when `HasPlayerTarget` is present, so no-target samples cannot claim gaze or SDF intent.

Cinematic Cheats used:
- Postmortem truth fake: no-target entries are deliberately local and flagged instead of pretending the player exists at a decoded default AUP.
- Contract restraint: local bit 5 buys diagnostics without forcing an asmdef contract refresh.

Exact Microseconds saved:
- Measured runtime microseconds unavailable; Unity runtime/MCP session is still not attached.
- No-target telemetry skips the default-AUP double3 conversion; static estimate is below 0.01 us per no-target Alpha telemetry sample on i3/MX350.
- Hot-path allocation remains 0 B/frame; no new managed collections, LINQ, or heap writes were added.

Verification:
- Prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md`; domain file re-read.
- `rg` scan confirmed no new allocations/LINQ, no `math.sqrt`, no `math.normalize`, no `.normalized`, and no `math.length(...)` in Alpha-scoped hot files.
- `git diff --check`: no whitespace errors; only repository LF-to-CRLF warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /clp:ErrorsOnly`: succeeded with 0 errors.
- Runtime proof remains pending because no Unity MCP/editor session is attached.

---

Sixth-pass upgrade: 2026-05-14

What was wrong:
- The mixed legacy species-profile branch of `ShouldUseAlphaLeviathanCognition()` still accepted broad `useLeviathanPresence` for non-Leviathan archetypes.
- That could let non-PresenceCircle legacy hybrids pay for Alpha 10Hz stalking, telemetry, SDF/gaze checks, and false-charge stress.
- Verification also exposed a shared compile wall: `GlobalSignals.cs` referenced `ScanLogChangedSignal` without the explicit alias pattern used for other signal structs, even though the struct already existed.

What was done:
- Tightened the mixed legacy branch so it now requires `useFeintRush` or `useLeviathanPresence && LeviathanEncounterType.PresenceCircle`.
- Added the explicit `ScanLogChangedSignal = Hecton8.Core.Signals.ScanLogChangedSignal` alias in `GlobalSignals.cs`.
- Left unrelated dirty files untouched and did not change scan-log signal behavior.

Cinematic Cheats used:
- Encounter gate: Alpha fog stalking stays tied to feint/PresenceCircle authoring instead of generic presence pressure.
- Budget fence: legacy non-Alpha apex content keeps normal cognition cadence and avoids Alpha black-box writes.
- Compile-boundary restraint: one alias restored build proof without moving shared signal contracts.

Exact Microseconds saved:
- Measured runtime microseconds unavailable; Unity runtime/MCP session is still not attached.
- Avoided work for misconfigured legacy hybrids: no Alpha phase branch, no Alpha telemetry write, no Alpha SDF/gaze branch, and no false-charge roar/stress queue write; static estimate ~0.05-0.12 us per avoided non-Alpha apex slow eval on i3/MX350.
- Alias fix is compile-only: 0 us runtime, 0 B/frame.

Verification:
- Current `Docs/Tasks/CURRENT_BATCH.md` no longer contains the Alpha tag; durable Alpha status/rationale files remain the assignment record.
- `rg` scan confirmed `ShouldUseAlphaLeviathanCognition()` uses `useFeintRush` or `useLeviathanPresence + PresenceCircle` for the mixed legacy branch.
- `git diff --check`: no whitespace errors; only repository LF-to-CRLF warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /clp:ErrorsOnly`: initially failed on missing `ScanLogChangedSignal` name resolution; after alias patch, succeeded with 0 errors.
- Later retry: one run returned exit 1 with no diagnostics while Unity/Roslyn and another build were active; after contention cleared, the same serialized command succeeded again with 0 errors in 2.51s.
- Runtime proof remains pending because no Unity MCP/editor session is attached.

---

Seventh-pass upgrade: 2026-05-14

What was wrong:
- Alpha phase state could stay stale when the player/acoustic target disappeared or a rival apex interrupted the PresenceCircle loop.
- Reacquiring after that interruption could resume an old Circling or FalseCharge phase age and produce an incoherent immediate charge.

What was done:
- Added `ResetAlphaLeviathanInterruptedPhase(slot, currentTime)` in `PredatorCognitionDomain`.
- When `UseAlphaLeviathanCognition` is active but the Alpha override cannot run because target authority is missing or a rival apex is visible, the slot now refreshes to Hidden and updates `StalkingPhaseStartTimes`.
- Kept the reset inside the existing SoA phase/timestamp lanes; no new timer lane or managed state was added.

Cinematic Cheats used:
- Reacquire reset: the monster vanishes back into Hidden during interruption instead of preserving an invisible old charge timer.
- Cheap authority: byte phase + float timestamp writes, not path replanning or extra behavior graph state.

Exact Microseconds saved:
- Measured runtime microseconds unavailable; Unity runtime/MCP session is still not attached.
- The patch adds one byte write and one float write on interrupted Alpha 10Hz evaluations only, estimated <0.01 us on i3/MX350, 0 B/frame.
- It prevents wasted/stale false-charge presentation on reacquire; visual budget stays on a deterministic hide/stalk restart.

Verification:
- Static scan found no new managed allocation/LINQ pattern and no `math.sqrt`, `math.normalize`, `.normalized`, or `math.length(...)` in the Alpha hot path.
- First compile retry failed on missing generated `Temp/bin/Debug` dependencies during Unity/Roslyn churn, including Crest/EasySave/Input/World contracts.
- After generated DLLs repopulated, `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /clp:ErrorsOnly` succeeded with 0 errors in 1:41.69.
- Runtime proof remains pending because no Unity MCP/editor session is attached.
