# HECTON-8 Master Release Plan

Date: 2026-06-09
Status: PENDING VERIFICATION
Owner: root roadmap anchor
Evidence class: STATIC_DOC only unless an artifact path is cited

## Authority

Read order:

1. `AGENTS.md`
2. `.agents-skills/README.md` and task-relevant mandates
3. `Docs/README.md`
4. `Docs/DOC_GOVERNANCE.md`
5. `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
6. current source under `Assets/_Project`
7. fresh verification artifacts

Full pre-X_012 historical roadmap copy:

- `Docs/DEPRECATED/Root_Bloat_X_012_2026-05-23/MASTER_RELEASE_WORK_PLAN.md`

## Product Gate

Main proof route: `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`.

Required route:

- boot
- main menu
- world load
- swim/orient
- gather resource
- use tool
- craft/repair/build
- encounter hazard
- save/load
- return

Work outside that route must name the blocker it removes.

## Current Source Facts

| Surface | Current fact |
|---|---|
| Save container | `SaveBinaryStorage.CurrentVersion = 0x000B`; header `56`; legacy header `44`; aligned section header `0x000B` |
| Signal registry | `SignalBusRegistry.LaneCapacity = 512` |
| Data Monolith | `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` exists; `7,457,664` bytes, mtime 2026-06-07, measured 2026-08-05; H8DM header `64` |
| Runtime proof | Unity import, Console, Play Mode, profiler, GCMonitor, player build, save/load, scene wiring, and visual proof remain pending unless a current artifact is cited |
| Current CLI build | `PENDING VERIFICATION` — artifact missing. The recorded pass cited `Docs/Reports/BUILD_UNKNOWN_RUNTIME_API_TRAP_CLEANUP_20260526.log` for full `Hecton8.slnx`, exit `0`, `0 Warning(s)`, `0 Error(s)`, but that log does not exist anywhere in the repository as of 2026-07-28, so it is a record and not evidence. `Docs/Reports/Compile_20260726.log` is Unity batchmode, a different proof class, and does not substitute. Detail and re-run instruction: `BUILD_PLAYTEST_ISSUES.md` `Current Build Evidence`. Any new build/import/profiler/player proof still requires the current `AGENTS.md` / `performance.md` CPU and Unity-process gate. |

## Active Work Buckets

| Bucket | Gate |
|---|---|
| Bootstrap/menu/world entry | route proof and clean `GameStartContext` handoff |
| Save/persistence | write/read/corruption/migration artifact |
| World readability | in-world swim proof against first-20-minutes route |
| Performance | MX350/i3 profiler, GC, memory, VRAM, Frame Debugger |
| Global authority | route card, owner, phase, capacity, overflow, telemetry, proof |
| Data Monolith | bake/import/boot/checksum/player-build proof |
| Visual quality | screenshot/clip plus frame/memory proof |

## Hard Rules

- No runtime readiness from static docs.
- No feature is done because source exists.
- No binary quality switches; use continuous `GlobalQualityWeight`.
- Use premium presentation approximations before physical simulation unless gameplay truth requires simulation.
- Protect weak, middle, high, and ultra tiers with continuous budgets, not hard dichotomies.
