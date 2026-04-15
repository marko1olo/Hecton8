# Design Document - Shell/UX Production System

Status: PENDING VERIFICATION
Spec type: feature
Spec path: `.kiro/specs/shell-ux-production/`

## 1. Purpose

This document replaces the previous placeholder body with a production-grade shell/UX design spec grounded in the current repository state.

This is not a marketing summary.
This is the normative handoff for the player-facing shell layer:

- main menu
- save/load shell
- settings shell
- pause shell
- rebinding UX
- localization pass-through
- save/load trust messaging

Subnautica-grade does not mean "more effects."
It means the shell never drops the player into dead states, never lies about save/load status, preserves trust during failure, and remains readable and responsive on target hardware.

## 2. Evidence Base

This document was authored against the current repo state, not assumptions.

Verified source files:

- `Assets/_Project/Scripts/MainMenuController.cs`
- `Assets/_Project/Scripts/SaveSlotUI.cs`
- `Assets/_Project/Scripts/UI/PauseMenuController.cs`
- `Assets/_Project/Scripts/UI/SettingsManager.cs`
- `Assets/_Project/Scripts/UI/SettingsPanel.cs`
- `Assets/_Project/Scripts/UI/SettingsLivePreview.cs`
- `Assets/_Project/Scripts/UI/SettingsComparisonView.cs`
- `Assets/_Project/Scripts/UI/SettingsPanelAnimator.cs`
- `Assets/_Project/Scripts/UI/SaveSlotHoverPreview.cs`
- `Assets/_Project/Scripts/UI/SaveThumbnailCapture.cs`
- `Assets/_Project/Scripts/UI/UIAudioFeedback.cs`
- `Assets/_Project/Scripts/Input/RebindingManager.cs`
- `Assets/_Project/Scripts/UI/PauseControlsPanel.cs`
- `Assets/_Project/Scripts/UI/PDAControlsRebindUI.cs`

Verified scene object names in `Assets/_Project/Scenes/01_MAIN_MENU.unity`:

- `Panel_MainMenu`
- `Panel_Sideload Popup`
- `Panel_Settings`
- `Panel_LoadingScreen`
- `BTN_Start`
- `BTN_ResumeLog`
- `BTN_Settings`
- `BTN_Abort`
- `ScrollView_Slots`

Supporting documents consulted:

- `AGENTS.md`
- `Docs/2026-04-13_Final_Audit/Workstreams/01_SHELL_UI_WORKSTREAM.md`
- `Docs/QUALITY_GATES.md`
- `Assets/_Project/Scripts/UI/SETTINGS_WIRING_GUIDE.md`
- `Assets/_Project/Scripts/UI/SETTINGS_SYSTEM_GUIDE.md`

## 3. Design Goals

### 3.1 Player-facing goals

- The player always understands where they are in the shell.
- Every Back, Cancel, Confirm, Retry, and Exit path is explicit and closed.
- Save slots communicate trust state, not just existence.
- Settings changes are staged, previewed where safe, applied intentionally, and reverted cleanly.
- Gamepad and keyboard navigation remain first-class, not mouse-only afterthoughts.

### 3.2 Technical goals

- Zero GC in hot shell paths.
- No `SetActive`-driven panel churn for runtime shell transitions.
- No hidden ownership for settings or persistence.
- No optimistic status claims without logs or runtime verification.
- No scene magic that silently depends on undocumented inspector state.

## 4. Scope

### In scope

- `01_MAIN_MENU` shell flow
- in-world pause shell
- settings ownership and settings UI staging
- save slot presentation
- save/load confirmation and failure presentation
- rebinding UX in pause/PDA surfaces
- localization refresh flow
- loading screen shell behavior

### Out of scope

- narrative systems
- world bootstrap redesign
- save backend contract redesign
- progression data changes
- art direction changes unrelated to shell readability or trust

## 5. System Ownership

The shell already has owners. Do not invent new ones unless current owners are provably insufficient.

| Area | Current owner | Notes |
|---|---|---|
| Main menu state and panel routing | `MainMenuController` | `ITickable`, scene-owned |
| Save slot authored button presentation | `SaveSlotUI` | Scene/prefab slot entry owner |
| Pause overlay state | `PauseMenuController` | Builds/owns runtime pause shell |
| Settings persistence/application | `SettingsManager` | Singleton, `DontDestroyOnLoad` |
| Settings UI staging | `SettingsPanel` | UI owner, staged values, apply/cancel |
| Input rebinding backend | `RebindingManager` | Existing input owner |
| Pause controls UI | `PauseControlsPanel` | Pause-specific rebind surface |
| PDA controls UI | `PDAControlsRebindUI` | Secondary rebind surface |
| Save contract and metadata | `SaveManager` | Atomic slot backend owner |
| Localization updates | `LocalizationManager` | Text refresh authority |

## 6. Current Architecture Snapshot

### 6.1 Main menu scene topology

`MainMenuController` auto-resolves scene children by authored names. The scene therefore remains the source of truth for shell layout, while the controller is the source of truth for behavior.

Expected runtime shell panels:

- `Panel_MainMenu`
- `Panel_Sideload Popup`
- `Panel_Settings`
- `Panel_LoadingScreen`

Expected authored buttons:

- `BTN_Start`
- `BTN_ResumeLog`
- `BTN_Settings`
- `BTN_Abort`
- authored back button(s) for save/settings return paths

Expected save slot container:

- `ScrollView_Slots`

Design consequence:

- if these authored names drift, `MainMenuController` degrades immediately
- the design therefore depends on scene naming discipline, not freeform inspector creativity

### 6.2 Main menu flow

Normative flow:

1. `00_BOOTSTRAP` hands off to `01_MAIN_MENU`
2. `MainMenuController` validates bootstrap route and registers to `GameTickManager`
3. Main menu opens on `mainMenuGroup`
4. New Game path opens confirm modal, then writes `GameStartContext` and loads `02_HECTON_WORLD`
5. Load Game path opens save/load shell, resolves three slots, then confirms selected slot before loading
6. Settings path routes to authored settings panel only if the authored panel is actually available
7. Quit path always confirms before application exit

Design intent:

- authored scene owns presentation
- controller owns panel state machine and scene-loading handoff
- no dead-end panel state is acceptable

### 6.3 Pause shell flow

`PauseMenuController` is the runtime owner of the in-world pause overlay.
It:

- blocks opening when PDA or fabricator menus are already open
- switches `InputManager` to UI mode
- controls cursor visibility and lock state
- optionally freezes time via `Time.timeScale = 0`
- builds sectioned pause content for Main / Saves / Help / Settings

Pause sections:

- Main
- Saves
- Help
- Settings

Design intent:

- pause is a stateful overlay, not a bag of independent panels
- Back/Escape from subsection returns to Main
- Escape from Main closes pause
- exit-to-menu must restore timescale and input state safely before scene handoff

### 6.4 Settings ownership and staging

`SettingsManager` is the unified runtime owner for graphics/audio/video options.

Current architectural characteristics visible in code:

- singleton with auto-instantiation fallback
- `DontDestroyOnLoad`
- loads persisted values on `Awake`
- applies values immediately through property setters
- persists through `UserOptionsPersistence`
- holds scene references for `mainCamera` and `urpVolume`

`SettingsPanel` is not the owner.
It is a staging/controller surface:

- reads current values from `SettingsManager`
- caches UI-facing values
- previews safe changes through `SettingsLivePreview`
- commits through Apply
- discards preview/staged state through Cancel

Design rule:

- persistence authority stays in `SettingsManager`
- UI never becomes a shadow owner

### 6.5 Save/load trust layer

`SaveSlotUI` is the presentation layer for slot health, timestamp, playtime, scene, and thumbnail.

Design intent:

- empty slot must look empty
- populated slot must communicate more than slot name
- degraded slot must communicate integrity risk
- user trust depends on visible metadata, not blind button labels

### 6.6 Rebinding surfaces

Current repo indicates two UI entry points:

- `PauseControlsPanel`
- `PDAControlsRebindUI`

Backend owner:

- `RebindingManager`

Design intent:

- both surfaces reflect the same persisted overrides
- cancel/reset/apply semantics remain deterministic
- missing binding text must degrade safely to fallback output, not broken UI

## 7. Release-Grade Behavioral Contracts

### 7.1 Navigation contract

Every panel or section must answer three questions:

1. how the player enters
2. how the player exits
3. what receives focus after transition

Anything that cannot answer all three is not release-ready.

### 7.2 Focus contract

Focus is part of functionality, not polish.

Required behavior:

- main menu defaults to the first valid primary action
- save/load defaults to first valid slot or Back
- settings defaults to first valid settings control
- modal defaults to confirm or safest action per context
- section switches clear stale selection before assigning new focus

### 7.3 Save/load trust contract

Required shell outcomes:

- empty slots clearly read as empty
- save failures are surfaced immediately
- load failures are surfaced immediately
- backup recovery is visible when used
- the player is never left unsure whether data was written

### 7.4 Settings contract

Settings behavior must separate three states:

- persisted
- staged in UI
- preview-applied but not yet committed

Release-grade expectation:

- preview is reversible
- Apply commits staged state
- Cancel reverts preview/staged state to last persisted state
- Reset uses explicit defaults and persists them intentionally

### 7.5 Localization contract

Visible shell text must refresh when language changes.
Fallback behavior must be deterministic if localization is unavailable.

## 8. Performance Design

Shell code is not exempt from runtime discipline.

### 8.1 Required hot-path rules

- `ITickable` instead of gameplay `Update()` loops for shell state that must tick
- `CanvasGroup` alpha/interactable/blockRaycasts for panel visibility
- cached references only in hot paths
- dirty-flagged text refresh when value unchanged
- no LINQ
- no per-frame string formatting in hot state
- no `SetActive` churn for runtime panel transitions

### 8.2 Current evidence in code

Visible patterns already in repo:

- `MainMenuController : ITickable`
- `PauseMenuController : ITickable`
- `SettingsPanel` caches staged values and static display-name arrays
- `SettingsPanelAnimator`, `SettingsComparisonView`, `SettingsLivePreview`, `SaveSlotHoverPreview` exist as dedicated shell subcomponents
- `SettingsManager` caches applied state and persists only on value change

### 8.3 Performance verification requirement

Do not claim shell performance solved without:

- profiler pass on main menu open/close
- profiler pass on settings panel interaction
- profiler pass on pause open/close
- GC confirmation during repeated navigation and slider movement

## 9. Verified Current State vs Unverified Claims

This section exists because adjacent shell docs overstate readiness.

### Verified now

- shell-related scripts listed in Section 2 exist in repo
- `01_MAIN_MENU.unity` contains the main authored shell object names listed in Section 2
- `MainMenuController` auto-resolves authored menu objects by name
- `SettingsManager` applies camera FOV through `mainCamera`
- `SettingsManager` applies Bloom and Motion Blur through a referenced `Volume`
- `SettingsPanel` integrates preview, comparison, animator, apply/cancel/reset hooks

### Not verified in this pass

- full inspector/reference completeness in authored scenes
- zero-GC measurements from profiler
- full end-to-end pause -> settings -> save/load -> return path
- all localization keys present in live data
- all audio feedback hooks authored in scene
- all hover preview / thumbnail UX present and wired
- backup-load failure UX in live runtime

### Known stale or risky claims in existing docs

- "100% complete"
- "release-ready"
- "only wiring remains"
- "fully functional after 2-3 hours"

These are not acceptable status claims without logs, profiler captures, and manual verification.

## 10. Known Risks and Release Blockers

### Blocker A - verification gap

The largest blocker is not necessarily missing code.
It is missing proof.

Without runtime evidence:

- shell status remains PENDING VERIFICATION
- Subnautica-grade claim remains unproven

### Blocker B - scene/inspector drift

`MainMenuController` depends on authored names and component presence.
If scene naming drifts, auto-resolution fails.

### Blocker C - settings application parity

Current supporting docs already note a mismatch around Ambient Occlusion:

- persistence flag exists
- runtime visual application is not yet fully evidenced through a stable renderer-feature owner

This remains a release risk until verified in scene and profiler.

### Blocker D - pause save path hardening

`PauseMenuController` currently contains `async void SaveSlot(string slotName)`.
That is a risk surface under the repo rules because async void is forbidden for gameplay-facing flows and exception propagation is weaker.

This document does not patch that code.
It records the risk.

WARNING: Regression risk in pause save UX and exception handling until this path is hardened or explicitly accepted.

### Blocker E - split truth across docs

Current shell documentation is fragmented across:

- `.kiro/specs/shell-ux-production/*`
- `AI_AGENT_WORK/*`
- `Assets/_Project/Scripts/UI/*GUIDE.md`
- final audit workstream docs

This document should be treated as the normative design source for shell architecture until the rest is cleaned up.

## 11. Wiring Strategy

Release-grade shell wiring must follow this order:

1. authored scene shell objects
2. owner component references
3. navigation/focus paths
4. save slot metadata and state
5. settings preview/application references
6. localization refresh
7. audio/polish hooks

### Main menu authoring requirements

- all panel `CanvasGroup` references valid
- back buttons assigned
- loading percent text assigned
- slot container populated with three `SaveSlotUI` entries
- settings panel authored enough for `DetermineSettingsAvailability()` to return true

### Settings authoring requirements

- `SettingsPanel` UI references assigned or resolvable
- `SettingsManager.mainCamera` valid
- `SettingsManager.urpVolume` valid
- preview/comparison/animator components wired only if actually present

### Pause authoring/runtime requirements

- pause can rebuild/open/close without relying on missing manager state
- save slots list matches slot contract
- language status and controls panel remain consistent after open/close cycles

## 12. Verification Matrix

Status remains PENDING VERIFICATION until the following are executed.

### 12.1 Main menu verification

- boot into `01_MAIN_MENU`
- verify default focus
- open Load Game, Back, Settings, Back, Quit modal, cancel
- spam Escape during transitions
- verify no dead panels, no stuck focus, no null spam

### 12.2 New game flow verification

- confirm New Game modal
- verify loading shell becomes visible
- verify world handoff completes
- verify no shell state leakage after world load

### 12.3 Load game verification

- test empty slot presentation
- test populated slot presentation
- confirm load modal
- test broken/corrupt slot behavior if fixtures exist

### 12.4 Pause verification

- open pause from world
- switch Main -> Saves -> Back -> Help -> Back -> Settings -> Back
- close pause from Main
- verify timescale, cursor, and input map restoration

### 12.5 Settings verification

- move FOV slider and confirm immediate preview
- toggle Bloom and Motion Blur and confirm live preview
- toggle Ambient Occlusion and record actual visual result
- press Cancel and verify reversion
- press Apply and verify persistence after scene reload

### 12.6 Save trust verification

- successful save from pause
- save failure path
- busy/save-in-progress rejection path
- load failure and backup path if fixtures exist

### 12.7 Performance verification

- repeated menu open/close for 30 seconds
- repeated slider drag for 30 seconds
- repeated pause open/close for 30 seconds
- profiler capture for GC/frame spikes

Required report format:

- BEFORE: numeric evidence
- AFTER: numeric evidence
- STATUS: PENDING VERIFICATION / NO REGRESSION / REGRESSION DETECTED

## 13. Definition of Done

The shell reaches release-grade only when all conditions below are true:

- every authored entry path has an exit path
- focus/default selection is deterministic across mouse, keyboard, and gamepad
- save slots communicate empty/healthy/degraded state clearly
- settings preview/apply/cancel behavior is deterministic
- localization refresh works on visible shell surfaces
- pause restores time/input/cursor state correctly
- profiler evidence shows no shell hot-path GC regressions
- failure paths are visible and understandable to the player
- the status can be supported by logs and test results

If any of the above is missing, status remains PENDING VERIFICATION.

## 14. Immediate Next Actions

1. Treat this document as the normative shell design spec.
2. Reconcile `.kiro/specs/shell-ux-production/tasks.md` against verified reality instead of optimistic summaries.
3. Run the verification matrix in Section 12 with profiler and console open.
4. Hard-audit pause save async path before declaring shell production-safe.
5. Clean stale AI-agent docs that still claim release readiness without evidence.

## 15. Final Position

The shell is not allowed to be "mostly good."
It is either trustworthy under pressure or it is still a liability.

Current codebase shows serious progress and real architecture.
Current documentation quality did not match that reality.

This file now does.
