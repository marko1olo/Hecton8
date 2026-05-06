# HECTON-8 SAVE / LOAD RUNTIME TRUTH

Date: 2026-05-07
Status: PENDING VERIFICATION
Scope: current source-backed truth for first-party save/load runtime behavior
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

## Purpose

This file exists because the active docset had save/load references scattered across atlas, event docs, and dated reports, but no single current runtime truth page.

This document describes what the first-party save/load stack currently does according to source.

It does not claim successful live save/load traversal in editor or build.

## Proof Boundary

Primary evidence:

- `Assets/_Project/Scripts/SaveManager.cs`
- `Assets/_Project/Scripts/SaveEvents.cs`
- `Assets/_Project/Scripts/Quest/QuestManager.cs`
- `Assets/_Project/Scripts/ConstructionManager.cs`

Secondary evidence:

- active save participants found through source scan
- current event topology already corrected in `EVENT_FLOW_MAP.md`

Not proven here:

- live slot creation and restore in play mode
- corruption recovery under real disk fault
- no-GC behavior under repeated save/load stress

## Current Save Runtime Owner

`Assets/_Project/Scripts/SaveManager.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `32` | class is `SaveManager : MonoBehaviour, ISaveService, IUpdatable` |
| `528` | save entry point is `SaveGameAsync(string slotName)` |
| `707` | load entry point is `LoadGameAsync(string slotName)` |
| `931` | metadata audit surface exists through `TryGetSaveMetadata(...)` |
| `971` | repair surface exists through `TryRepairSaveSlot(...)` |
| `999` | audit surface exists through `TryAuditSaveSlot(...)` |

This is not a thin file writer.
It is the central runtime orchestrator for:

- save participant registry
- async save snapshot build
- async load application
- backup rotation
- metadata inspection
- repair/audit flow

## Event Lane Truth

`Assets/_Project/Scripts/SaveEvents.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `31` | `SaveEvents` is the global save/load event owner |
| `35` | owns `NativeQueue<SaveEventPayload>` |
| `67` | pending events are flushed through `FlushPending()` |
| `84-109` | exposes explicit save/load started/completed/failed raisers |
| `123` | event queue is persistent and explicitly documented as dispatcher-late-update flushed |

Current truth:

`SaveEvents` is queue-backed, not a naÃ¯ve direct static action bus.

## Save Flow

Confirmed flow inside `SaveManager.cs`:

| Source line | Fact |
|---|---|
| `528` | save starts at `SaveGameAsync(...)` |
| `566` | raises `SaveEvents.RaiseSaveStarted(slotName)` |
| `596` | iterates registered participants and calls `PopulateSaveData(data)` |
| `599` | mod save state is folded in after base participant collection |
| `642` | transitions to `Awaitable.BackgroundThreadAsync()` |
| `663` | returns to `Awaitable.MainThreadAsync()` for finalize path |

What that means operationally:

1. Save is explicitly evented.
2. Registered `ISaveable` participants populate a shared `SaveData` container.
3. Compression and file-writing work is offloaded.
4. Finalization returns to main thread.

This matches the project mandate more closely than many generic Unity save systems.

## Load Flow

Confirmed flow inside `SaveManager.cs`:

| Source line | Fact |
|---|---|
| `707` | load starts at `LoadGameAsync(...)` |
| `745` | raises `SaveEvents.RaiseLoadStarted(slotName)` |
| `751` | transitions to `Awaitable.BackgroundThreadAsync()` for background load work |
| `806` | returns to `Awaitable.MainThreadAsync()` for apply stage |
| `816` | applies mod save state |
| `834` | iterates registered participants and calls `LoadFromSaveData(data)` |

Operationally:

1. Slot candidate is loaded and decoded off-thread.
2. Application of restored state is returned to main thread.
3. Registered participants restore in load-priority order.

## File Artifact Truth

Confirmed save-slot path helpers:

| Source line | Fact |
|---|---|
| `1076` | primary artifact is `{slotName}.sav` |
| `1081-1083` | backups are `{slotName}.sav.bak` and generation variants |
| `1085` | temp artifact is `{slotName}.sav.tmp` |

This confirms:

- primary save file exists
- temp write artifact exists
- backup artifact exists

That aligns with the mandate direction better than older docs suggested.

## Async Boundary Truth

Confirmed async transitions:

| Source line | Fact |
|---|---|
| `428`, `642`, `751`, `868` | background execution points use `Awaitable.BackgroundThreadAsync()` |
| `434`, `663`, `806`, `879` | apply/finalize points return through `Awaitable.MainThreadAsync()` |

This matters because the current implementation is not pretending save/load is purely synchronous main-thread disk work.

## Participant Contract Truth

The save runtime still depends on `ISaveable`.
Participant registration and ordering are real, not theoretical.

Confirmed participant examples:

| System | Evidence |
|---|---|
| `QuestManager` | `Assets/_Project/Scripts/Quest/QuestManager.cs:17`, `52`, `54`, `77-78`, `314`, `341` |
| `ConstructionManager` | `Assets/_Project/Scripts/ConstructionManager.cs:38`, `169-170`, `177`, `372-373`, `388`, `497`, `742` |

This proves save/load is integrated into gameplay systems rather than isolated inside one manager.

## Priority Truth

Confirmed examples from current code:

| System | SavePriority | LoadPriority | Notes |
|---|---:|---:|---|
| `QuestManager` | `7` | `7` | early gameplay/system restoration |
| `ConstructionManager` | `90` | `90` | later world/construction restoration |

This does not prove the entire project has perfect priority discipline, but it does prove the ordering model is active.

## Failure / Recovery Surface

Confirmed repair and audit surfaces:

| Source line | Fact |
|---|---|
| `971` | runtime repair API exists |
| `976` | static artifact repair API exists |
| `999` | runtime audit API exists |
| `1004` | static artifact audit API exists |

This matters because the current save system is not limited to write/read.
It already exposes artifact repair and audit entry points.

## What Looks Good

- Save/load has a real orchestrator, not scattered ad-hoc serialization.
- `SaveEvents` is queue-backed.
- async background/main-thread boundaries are explicit.
- temp and backup artifacts are confirmed in code.
- gameplay systems like quest and construction are directly integrated through `ISaveable`.

## What Looks Merely Acceptable

- Source proves the intended pipeline, but not its live correctness under all scenarios.
- Participant examples are verified, but a full project-wide save participant registry was not rebuilt in this pass.
- Repair and audit APIs exist, but their behavior under hostile/corrupt inputs was not runtime-tested here.

## What Looks Weak

- No new live save/load traversal was captured during this doc pass.
- No fresh build proof exists for slot recovery, backup fallback, or migration edge cases.
- No measured GC/profiler capture was taken around repeated save/load operations.

## Failure Modes To Watch

- participant priority collisions can still create hidden restore-order defects
- save participants can register correctly but fail on missing keys or stale data
- async success path can look clean in code while error path remains weak in runtime
- repair APIs can exist without being exercised in a real corrupted-slot test

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | None. Documentation-only pass. |
| GC | None. Documentation-only pass. |
| Memory | None. Documentation-only pass. |
| Cadence | None. Runtime code unchanged. |
| Correctness | Improves source-backed understanding of slot flow, eventing, artifacts, and participant ordering. |

## Verdict

Current save/load truth is stronger than the old fragmented docs suggested.

Confirmed source-backed facts:

- `SaveManager` is the central runtime owner
- save and load run through explicit async entry points
- `SaveEvents` is queue-backed
- `.sav`, `.sav.tmp`, and `.sav.bak*` artifacts all exist in code
- gameplay systems such as quest and construction are direct `ISaveable` participants
- repair and audit APIs exist

What is still missing is runtime proof, not basic pipeline definition.

STATUS: PENDING VERIFICATION
