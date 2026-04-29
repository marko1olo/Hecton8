# EDITOR / RUNTIME FORENSICS

Date: 2026-04-30
Status: PENDING VERIFICATION
Scope: evidence-backed follow-up sweep for live console health, tick-adjacent UI mutation debt, coroutine-heavy verification stack, and false-positive `JobHandle.Complete()` accusations
Mandates followed: `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `UI_Data_Streaming_ZeroGC_Optimization.txt`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`, `PHYS_Physics_Integrity_Determinism_ForceMode.txt`

## Purpose

This file exists to document current forensic findings that are newer than the dated `2026-04-28_*` bundles and narrower than the broad ownership maps.

It records what was actually re-read in source and what the Unity console is actually surfacing now.

It does not claim measured runtime performance proof.

## Method

Evidence lanes used in this pass:

1. live Unity MCP console readback
2. targeted source reads around current stack traces and grep hits
3. reclassification of suspicious files after line-level inspection

Important boundary:

- grep hits alone were not treated as findings
- a file only entered the findings table after the surrounding code was re-read

## Current Live Console State

The console is not green in the current reachable Unity session.

Recent MCP console readback returned `20` entries, dominated by the same repeated exception family:

- `NullReferenceException`
- source file: `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs:482`
- stack path:
  - `WorldProceduralScatterDirector.get__desiredPlacements()`
  - `WorldProceduralScatterDirector.BuildScatterPreviewGizmoSnapshot(...)`
  - `Editor/WorldProceduralScatterPreviewGizmoDrawer.DrawScatterPreviewGizmos(...)`

This means any same-day claim of `0 errors globally` or fully clean editor state is false for the current session snapshot.

Follow-up source patch on `2026-04-30`:

- `WorldProceduralScatterDirector.BuildScatterPreviewGizmoSnapshot(...)` was tightened to capture `_desiredPlacements` once into a local before enumeration
- `WorldProceduralScatterPreviewGizmoDrawer` no longer treats `NullReferenceException` as expected control flow

Important boundary:

- this patch was applied after the console snapshot above
- post-fix Unity MCP compile/console revalidation was not possible because the Unity session became unavailable
- therefore this document still cannot claim that the console is now clean

## Findings

| ID | File | Current issue | Severity | Evidence |
|---|---|---|---|---|
| ERF-01 | `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs` + `Assets/_Project/Scripts/Editor/WorldProceduralScatterPreviewGizmoDrawer.cs` | live editor console is currently spammed by repeated `NullReferenceException` during scatter preview gizmo drawing | HIGH | MCP console stack traces point to `WorldProceduralScatterDirector.cs:482`, `:1829` and `WorldProceduralScatterPreviewGizmoDrawer.cs:19` |
| ERF-02 | `Assets/_Project/Scripts/UI/AcousticEcholocationTranslator.cs` | tick-driven UI surface still uses direct TMP string mutation via `.text =` on sonar/boot/caption paths | HIGH | file contains `ITickable` owners at `:157`, `:598`, `:901` and direct `.text =` writes at `:329`, `:335`, `:645`, `:947`, `:1034` |
| ERF-03 | `Assets/_Project/Scripts/UI/DiegeticPDAController.cs` | tick-driven diegetic PDA path still uses `tabletRoot.SetActive(openState)` instead of pure CanvasGroup/presentation gating | MEDIUM | controller is `ITickable` at `:108`; `ApplyPresentationState(...)` is called from tick-open-state path and toggles `SetActive` at `:279-280` |
| ERF-04 | verification / smoke stack across `SaveSystemRuntimeSmokeTester`, `ShellVerificationRuntimeSmokeTester`, `PauseSystemVerifier`, `SceneTransitionVerifier`, `StateRecoveryVerifier`, and other smoke testers | active verification infrastructure is still coroutine-driven rather than state-machine/tick driven | MEDIUM | repeated `StartCoroutine(...)` sites exist across bootstrap/player-attached smoke and verifier files |

## Finding Details

### ERF-01 — Scatter Preview Gizmo Console Spam

Current live exception path is editor-side, not gameplay-side.

Relevant source facts:

- `WorldProceduralScatterPreviewGizmoDrawer.DrawScatterPreviewGizmos(...)` unconditionally calls `director.BuildScatterPreviewGizmoSnapshot(_records)` at line `19`
- historical failing version evaluated `_desiredPlacements` more than once across the snapshot path
- current source patch captures `_desiredPlacements` into a local before enumeration to reduce teardown-window null races
- live post-fix verification is still missing because the Unity MCP session dropped before recheck

This does not prove the gameplay scatter loop is broken.
It does prove the editor preview path was not null-safe enough for the reachable authoring state in the last verified console snapshot.

Operational consequence:

- editor console noise was real in the last verified session snapshot
- current active docs must not claim clean console state

### ERF-02 — Acoustic UI Mutation Debt

`AcousticEcholocationTranslator.cs` is not just a passive helper.
It contains three tick-registered presentation owners:

- root translator
- `TerminalBootSequence`
- `AudioCaptionOverlay`

Within that surface, direct text mutation still exists:

- `_headerLabel.text = headerText`
- `_classificationLabel.text = classificationText`
- `_consoleLabel.text = BuildSequenceText()`
- `slot.Label.text = request.CaptionText`
- `text.text = string.Empty`

This is not yet profiler proof of frame hitching.
It is source-backed violation debt against the zero-GC UI mandate because the affected surface is part of a tick-driven HUD/presentation chain.

### ERF-03 — Diegetic PDA Visibility Toggle Debt

`DiegeticPDAController` is tick-driven and polls `PlayerPDA.IsOpen` in `Tick(float deltaTime)`.
When state changes, it routes through `ApplyPresentationState(...)`, where:

- `tabletRoot.SetActive(openState)` is still used
- `CanvasGroup` visibility is also set in the same method

This is lower severity than a per-frame `SetActive` loop because it is state-change gated.
It is still architecture debt because the presentation path keeps a full object-activation toggle in a UI-facing controller.

### ERF-04 — Coroutine-Heavy Verification Stack Still Wired

The current verifier/smoke layer still uses `StartCoroutine(...)` across many files, including:

- `SaveSystemRuntimeSmokeTester`
- `BarterRuntimeSmokeTester`
- `BuilderRuntimeSmokeTester`
- `FieldToolRuntimeSmokeTester`
- `FabricationRuntimeSmokeTester`
- `ScanRuntimeSmokeTester`
- `ToolRuntimeSmokeTester`
- `ToolTrialRangeRuntimeSmokeTester`
- `UIRuntimeSmokeTester`
- `ShellVerificationRuntimeSmokeTester`
- `PauseSystemVerifier`
- `SceneTransitionVerifier`
- `StateRecoveryVerifier`
- `WorldGenerativeGeologyRuntimeSmokeTester`

This is not hidden dead code only.
Part of this stack is still attached in authored YAML and already documented in `DEAD_CODE_GRAVEYARD.md`.

The practical reading is narrower:

- verification stack remains architecturally inconsistent with the tick/state-machine rules
- this is currently verification debt, not proven gameplay hot-path debt

## Rechecked False Positives

These files were re-read after grep and should not be treated as current evidence of illegal mid-frame stalls:

| File | Rechecked result |
|---|---|
| `Assets/_Project/Scripts/Core/SystemDispatcher.cs` | `handle.Complete()` sites at `:751` and `:763` belong to late-frame barrier completion helper, not arbitrary mid-tick work |
| `Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs` | `Complete()` sites inspected in post-fixed swap window and disposal path; not current proof of hot-path serialization |
| `Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs` | `Complete()` sites are gated behind `IsCompleted` and completion/clear-runtime-state handling; not enough to accuse mid-frame stall from source alone |
| `Assets/_Project/Scripts/HectonFabricatorUI.cs` | `Complete()` sites are in teardown, close-menu cleanup, or post-completion consumption paths; current source does not prove a mid-frame `Schedule()+Complete()` stall loop |

## What This Pass Did Not Prove

- no fresh GCMonitor numbers
- no fresh profiler capture
- no measured canvas rebuild cost
- no measured impact of the coroutine verification stack on target hardware
- no live gameplay traversal through the affected UI surfaces
- no fresh proof that the earlier `PersistentWorldRegistry.cs` compile error is still live now

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | editor console spam and UI mutation debt are plausible CPU risks, but unmeasured in this pass |
| GC | direct TMP `.text =` paths remain source-backed GC-risk debt |
| Memory | neutral in this pass; no runtime mutation measured |
| Cadence | coroutine-heavy verifier stack increases architecture inconsistency, but gameplay cadence impact is unmeasured |
| Correctness | improved because false-positive `Complete()` accusations were removed while live console and UI debt were documented honestly |

## Verdict

Current strongest forensic facts are:

1. live console is not clean because the scatter preview gizmo path is throwing repeated `NullReferenceException`
2. tick-adjacent UI presentation code still contains direct TMP `.text =` mutation in at least one active presentation stack
3. diegetic PDA visibility still keeps a `SetActive` toggle in a tick-driven controller
4. verification infrastructure remains coroutine-heavy and partly still wired

Current strongest non-finding:

- several `JobHandle.Complete()` grep hits previously looked suspicious, but the re-read files examined in this pass do not justify blanket hot-path accusations

STATUS: PENDING VERIFICATION
