# HECTON-8 Master Release Plan

Date: 2026-05-24
Status: PENDING VERIFICATION
Owner: root roadmap anchor
Evidence: STATIC_DOC only unless an artifact path is cited

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
| Data Monolith | `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` exists; `1,064,384` bytes; H8DM header `64` |
| Runtime proof | Unity import, Console, Play Mode, profiler, GCMonitor, player build, save/load, scene wiring, and visual proof remain pending unless a current artifact is cited |
| EXTERNAL_CODEX source cleanup | Latest zero-warning CLI_COMPILE: `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup122_tick_registration.log`, 0 warning/error text matches. Latest compile attempt: `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup158_world_dispatcher_rebind.log`; build failed before C# with `NETSDK1004` missing `Temp/obj/Hecton8.Editor/project.assets.json`, 0 warnings, no `CS*` diagnostics; latest retry blocked by `BUILD_GUARD cpu=100 compiler_count=2`. Latest slices remove runtime `?? GlobalRegistry`, `GlobalRegistry.TryGet`, `ActiveRuntimeContext`, `ActiveRuntimeInstance`, SaveRuntime, owner-cache, Dispatcher rebind, Save registration, DataVault swap, interaction scene-scan, Atlas read-model/DataVault read tails, duplicate generated source inputs, slow/updatable/fixed/late-frame register/probe tails, non-`this` static-driver/renderable residues, info-only release log callsites across loops134/139/141/142, HectonVoxelVolume sonar DataVault runtime polls, context getter mutation, PlayerSensory getter mutation, Dispatcher/DataVault/service rebind losses through loop151, loop152 persistent-world tombstone Save reads, loop153 persistent-world Player/PlayerInventory hydration/catalog reads, loop154-156 UI/audio/construction stale Dispatcher registration tails plus death-dump Player read, loop157 UI/Construction singleton runtime tails, loop158 world/environment/AI Dispatcher stale registration tails, and loop159 GI/weather fallback owner tails; targeted greps pass; broad file-local scan still includes known split-line/static-driver/legacy-stub false positives. |

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
- Use visual fakes before physical simulation unless gameplay truth requires simulation.
- Protect weak, middle, high, and ultra tiers with continuous budgets, not hard dichotomies.
