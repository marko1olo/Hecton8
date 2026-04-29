# HECTON-8 UI / AUDIO / PRESENTATION SYSTEM MAP

Date: 2026-04-30
Status: PENDING VERIFICATION
Scope: detailed source-backed map for first-party UI, HUD, visor, PDA, and audio presentation ownership
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `UI_Data_Streaming_ZeroGC_Optimization.txt`

## Purpose

Older active docs already established two true facts:

- `SuitHUDV4CanvasOverlay` is the direct `IUIService` implementor.
- `SpatialAudioManager` is the direct `IAudioService` implementor.

That was not enough.

It did not explain the wider presentation stack around those services:

- which classes actually own the HUD runtime
- which classes are only neighboring presenters, not the global service owner
- where visor projection fits
- how PDA and audio-log UI attach to the same presentation layer
- how audio service ownership differs from narrower narrative or UI-facing audio systems

This file maps that stack.

## Proof Boundary

This map is based on current first-party source under `Assets/_Project/Scripts`.

It proves:

- direct class ownership
- direct service publication paths
- neighboring presenter relationships
- current source-level event bridges

It does not prove:

- that every listed owner is active in the current live scene
- that every prefab instance is wired correctly
- that the current presentation stack is low-cost on target hardware
- that all UI surfaces already obey zero-string or zero-rebuild discipline in live runtime

## Reading Rule

Use this file together with:

- `INTERFACE_HEALTH_DASHBOARD.md` for service-contract truth
- `2026-04-29_SCENE_PREFAB_SERVICE_OWNER_TRUTH.md` for authored prefab anchors
- `EVENT_FLOW_MAP.md` for queue-backed versus direct bus behavior
- `AUDIO_ROUTING_AUDIT.md` for older mixer-routing debt claims that still need reread

## 1. Top-Level Ownership Map

| Presentation slice | Current primary owner | Evidence | Role |
|---|---|---|---|
| Global UI service slot | `SuitHUDV4CanvasOverlay` | `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:31`, `849` | direct `IUIService` publisher |
| Helmet HUD composition | `SuitHUDV4CanvasOverlay` | `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:31`, `90-188` | telemetry, gauges, reticle, warning/status surface |
| Visor world-facing bridge | `VisorHUDController` | `Assets/_Project/Scripts/Visor/VisorHUDController.cs:24`, `234` | projection/runtime visor presenter, not the service slot |
| Screen compositor bridge | `SuitHUDScreenCompositor` | `Assets/_Project/Scripts/Visor/SuitHUDScreenCompositor.cs:18`, `323-348` | shared RT / overlay visibility bridge |
| Presentation-mode switching | `SuitHUDPresentationController` | `Assets/_Project/Scripts/Visor/SuitHUDPresentationController.cs:35`, `556-567` | switches projection/presentation mode |
| Quickbar runtime strip | `HUDQuickBar` | `Assets/_Project/Scripts/HUDQuickBar.cs:25`, `650`, `704`, `730` | zero-string quickbar text and slot surface |
| Notification strip | `HUDNotification` | `Assets/_Project/Scripts/HUDNotification.cs:19`, `65`, `369` | transient HUD messaging surface |
| Save notification bridge | `HUDSaveNotificationLink` | `Assets/_Project/Scripts/UI/HUDSaveNotificationLink.cs:12` | save-event to HUD-notification adapter |
| PDA shell chrome | `PDAShellChrome` | `Assets/_Project/Scripts/UI/PDAShellChrome.cs:17`, `252-266`, `430-433`, `758-759` | PDA chrome visibility and tab-reactive shell |
| PDA data-log surface | `PDADataLogTab` | `Assets/_Project/Scripts/UI/PDADataLogTab.cs:37`, `1484-1605` | audio-log archive UI and playback labels |
| PDA construction surface | `PDAConstructionTab` | `Assets/_Project/Scripts/UI/PDAConstructionTab.cs:23`, `214-236`, `1240-1247` | builder-facing PDA tab |
| PDA map / signal / marker overlays | `PDAMapTab`, `PDAAtlasSignalTab`, `PDAMarkerHUDElement` | `Assets/_Project/Scripts/UI/PDAMapTab.cs:19`, `Assets/_Project/Scripts/UI/PDAAtlasSignalTab.cs:34`, `Assets/_Project/Scripts/PDA/PDAMarkerHUDElement.cs:15` | PDA navigation and in-world marker presentation |
| Global audio service slot | `SpatialAudioManager` | `Assets/_Project/Scripts/SpatialAudioManager.cs:71`, `380` | direct `IAudioService` publisher |
| Narrative audio-log runtime | `AudioLogSystem` | `Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs:36`, `149`, `174`, `190`, `217`, `237` | playback/discovery state owner, not the global audio mixer service |
| Submarine presentational runtime | `HectonSubmarineOS` | `Assets/_Project/Scripts/Gameplay/HectonSubmarineOS.cs:130` | submarine-facing presentation and renderable system |

## 2. The Real UI Service Owner

### 2.1 `SuitHUDV4CanvasOverlay` Owns The Service Slot

Current source makes one thing explicit:

- `SuitHUDV4CanvasOverlay` implements `IUIService`
- it guards ownership through `_ownsGlobalUiSlot`
- it registers only if the current `GlobalRegistry.UI` slot is empty or already points to itself

Direct evidence:

- class declaration: `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:31`
- guard + registration: `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:835-849`
- unregister path: `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:851-859`

Current interpretation:

- this is not just a HUD widget
- this class is the current project-wide UI runtime publication point
- the service slot is still singular, even though the wider UI stack is not singular

### 2.2 It Is Also A Bootstrap Listener

`SuitHUDV4CanvasOverlay` implements `ISceneBootstrapEventListener`.

That matters because UI readiness is not expressed only through `GlobalRegistry.UI`.
It also reacts to scene-bootstrap completion/failure signaling.

This means the effective UI truth is split across:

- service publication through `GlobalRegistry`
- event readiness through `SceneBootstrap`
- visual composition through visor and overlay neighbors

### 2.3 The HUD Class Is Large Enough To Be An Architectural Signal

The class is not a tiny adapter.
Its constant surface alone shows it owns:

- telemetry labels
- localized HUD labels
- multiple cadence bands
- shader/material IDs
- acoustic radar overlay
- scanner hologram state
- threat chevrons
- projection geometry and world-space/diegetic presentation

Current conclusion:

- the file is a real runtime owner
- it is also a concentration-risk owner
- any doc that describes UI as "fragmented with no owner" is false
- any doc that describes UI as "solved because one owner exists" is also false

## 3. Neighbor UI Owners That Do Not Own The Global Slot

### 3.1 Legacy / Adjacent HUD Classes Still Exist

Current source still contains:

- `HectonSuitHUD_v4`
- `HectonSuitHUDExtensions`
- `HUDQuickBar`
- `HUDNotification`

These should not be confused with the `IUIService` slot owner.

Current reading:

- they are still active presentation participants
- they are not the direct registry-published UI service
- they increase practical UI complexity even though the registry surface is singular

### 3.2 Visor Presentation Is Its Own Sub-Layer

`VisorHUDController` keeps its own active-controller list and exposes copy-out helpers.

Evidence:

- class declaration: `Assets/_Project/Scripts/Visor/VisorHUDController.cs:24`
- active-list exposure: `Assets/_Project/Scripts/Visor/VisorHUDController.cs:234-242`

This means visor rendering is not a decorative child detail.
It is a reusable presentation sub-layer with its own controller registry behavior.

Supporting neighbors:

- `SuitHUDScreenCompositor`
- `SuitHUDPresentationController`
- `VisorRTManager`

Current conclusion:

- the HUD stack is not just canvas UI
- it includes projection, RT management, and visor-space presentation

### 3.3 PDA Presentation Is Distributed

PDA presentation is not one owner.

It is spread across:

- `PlayerPDA` as the actor/root behavior
- `PDAShellChrome` as shell and open/close/tab-reactive chrome
- `PDADataLogTab` as audio-log archive and playback UI
- `PDAConstructionTab` as construction-facing surface
- `PDAMapTab` and `PDAAtlasSignalTab` as navigational tabs
- `PDAMarkerHUDElement` as in-world HUD marker presenter
- `DiegeticPDAController` as world/diegetic panel bridge

Current conclusion:

- the PDA domain is a presentation subsystem cluster, not a single screen controller
- docs that flatten PDA into one file are incomplete

## 4. Current UI Cadence / Zero-String Signals

The active UI surface shows multiple direct signs of zero-string or reduced-rebuild intent:

- `HUDQuickBar` uses `SetCharArray`
- `PDADataLogTab` uses `SetCharArray` and `TryFormat`
- `BeaconHUDElement`, `RelayHUDElement`, `PDAMarkerHUDElement`, `BuilderStatusOverlay`, `ActionProgressHUD`, `BIOSMessageStreamer`, and several others also use direct char-buffer writes
- many visibility gates use `CanvasGroup.alpha` instead of hard `SetActive`

This does not prove whole-stack compliance.

It does prove that current first-party UI is no longer accurately described as a naive `text = "..."; SetActive(...)` codebase everywhere.

Honest caveat:

- the stack is mixed
- some files still add `CanvasGroup` components at runtime
- measured rebuild cost is still absent
- this map is ownership truth, not measured performance truth

## 5. The Real Audio Service Owner

### 5.1 `SpatialAudioManager` Owns `IAudioService`

Current source makes this direct:

- class declaration: `Assets/_Project/Scripts/SpatialAudioManager.cs:71`
- registration path: `Assets/_Project/Scripts/SpatialAudioManager.cs:380`
- init path: `Assets/_Project/Scripts/SpatialAudioManager.cs:364-381`

The manager is not just a utility.
It is the runtime owner that publishes `GlobalRegistry.Audio`.

### 5.2 The Audio Service Is Larger Than "PlayOneShot Wrapper"

The class surface shows broader ownership than older docs suggested:

- world-source pool management
- 2D helmet/UI pool
- threat ducking
- delayed audio ingress
- cave/acoustic state
- radar emitter telemetry
- manual doppler and binaural state
- ambient/interface/SFX mixer-group routing

Current conclusion:

- `SpatialAudioManager` is a full runtime audio owner
- docs that describe it as a ghost or trivial adapter are false

### 5.3 Narrative Audio Systems Are Adjacent, Not Service-Replacing

`AudioLogSystem` is important but it does not replace `IAudioService`.

It owns:

- discovered-log state
- active playback state
- save participation
- event publication through `AudioLogEvents`

It does not own:

- global mixer service publication
- world-pool audio runtime
- registry-wide audio routing

This distinction matters because older broad audio docs can easily blur:

- "who owns narrative audio content state"
- versus "who owns the project audio service slot"

## 6. Event Bridges Inside Presentation

Current presentation is not driven by one event style.

It mixes at least three patterns:

- queue-backed buses such as `AudioLogEvents`
- queue-backed scene-bootstrap events through `SceneBootstrap`
- direct static buses such as `PDAEvents`

Concrete examples:

- `PDADataLogTab` implements `IAudioLogEventListener`
- `PDAShellChrome`, `PDAConstructionTab`, and `PDAControlsRebindUI` subscribe to `PDAEvents`
- `SuitHUDV4CanvasOverlay` is a `SceneBootstrap` listener
- `HUDSaveNotificationLink` bridges save events into `HUDNotification`

Current conclusion:

- presentation event topology is still mixed
- the codebase contains migration evidence toward queue-backed buses
- the UI domain is not yet architecturally uniform

## 7. Current Truths That Old Docs Commonly Distort

| Distorted claim | Current source-backed truth |
|---|---|
| UI has no real owner | False. `SuitHUDV4CanvasOverlay` owns the `IUIService` slot. |
| UI is one class and therefore simple | False. The service slot is singular, but presentation ownership is distributed across HUD, visor, PDA, and marker layers. |
| Audio is unresolved or ghosted | False. `SpatialAudioManager` is the current `IAudioService` owner. |
| Audio-log systems are the main audio owner | False. They are narrative/content-state owners adjacent to the audio service. |
| PDA is one screen controller | False. The PDA stack is distributed across actor root, shell, tabs, markers, and diegetic bridges. |

## 8. Recommended Read Order

If the task is UI-facing:

1. `UI_AUDIO_PRESENTATION_SYSTEM_MAP.md`
2. `INTERFACE_HEALTH_DASHBOARD.md`
3. `2026-04-29_SCENE_PREFAB_SERVICE_OWNER_TRUTH.md`
4. `EVENT_FLOW_MAP.md`

If the task is audio-facing:

1. `UI_AUDIO_PRESENTATION_SYSTEM_MAP.md`
2. `AUDIO_ROUTING_AUDIT.md`
3. `INTERFACE_HEALTH_DASHBOARD.md`

If the task is visor/PDA-facing:

1. `UI_AUDIO_PRESENTATION_SYSTEM_MAP.md`
2. `PLAYER_GAMEPLAY_CORE_MAP.md`
3. `EVENT_FLOW_MAP.md`

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | None. Documentation-only pass. |
| GC | None. Documentation-only pass. |
| Memory | None. Documentation-only pass. |
| Cadence | None. Runtime code unchanged. |
| Correctness | Improves accuracy for UI/audio ownership and reduces false flattening of the presentation stack. |

## Verdict

The current presentation layer is not ownerless.

It has two clear service anchors:

- `SuitHUDV4CanvasOverlay` for global UI publication
- `SpatialAudioManager` for global audio publication

Around those anchors sits a much broader stack of HUD, visor, PDA, marker, notification, and narrative presentation classes.

That wider stack is real complexity, not noise.
Any future audit that ignores it will under-describe the project.

STATUS: PENDING VERIFICATION
