# HECTON-8 STRUCTURAL NARRATIVE

Date: 2026-04-29
Status: PENDING VERIFICATION
Scope: architecture narrative only, not a profiler report
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`, `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## 1. Purpose

This document explains how a representative gameplay frame is intended to move through the project.
It does not claim measured frame time, measured GC, or verified runtime health.
Earlier versions mixed architecture description with fake performance certainty. That has been removed.

## 2. High-Level Frame Story

Representative path:

1. input is gathered
2. dispatcher-owned update lanes advance runtime services
3. player/gameplay systems react
4. queued state reaches physics, world, UI, and audio consumers

This is a narrative abstraction. Actual execution order must still be read from code and, when required, from live runtime telemetry.

## 3. Input Layer

Current source indicates `InputDispatcher` is one of the main runtime input owners and implements `IInputService`.

Observed facts:

- `InputDispatcher` exists under `Assets/_Project/Scripts/Core`
- project also contains `InputManager`
- `PlayerPDA` and `PlayerFlashlight` expose their own event surfaces on top of input handling

Unverified claim removed:

- no measured proof is provided here for input cost, allocation profile, or exact dispatch latency

## 4. Dispatcher / Service Layer

Current source indicates a central dispatcher/service pattern rather than uncontrolled gameplay `Update()` spread.

Observed facts:

- `SystemDispatcher` exists
- `GlobalRegistry` is an active service surface
- several core services implement `IUpdatable`, `ITickable`, or `ISlowTickable`

This supports the intended architecture direction described in `AGENTS.md`, but this file does not claim that every gameplay path fully conforms.

## 5. Player / Gameplay Layer

Representative player-facing runtime components visible in current source:

- `PlayerRuntimeContextService`
- `PlayerInventoryManager`
- `PlayerSensoryManager`
- `PlayerPDA`
- `PlayerFlashlight`
- `PlayerToolManager`

Observed structural pattern:

- player subsystems expose a mix of interfaces, direct references, and static event buses
- the codebase is not purely event-driven and not purely service-locator-driven

## 6. Physics / World Layer

Current source shows these relevant owners:

- `PhysicsApplySystem`
- `WorldProceduralScatterDirector`
- `HectonCelestialEngine`
- `GlobalWeatherDirector`
- `OceanKinematicsRuntimeService`

Architecture signal:

- there is a visible attempt to separate environment, world simulation, and runtime service ownership
- there is also clear debt: multiple static-instance and event-driven patterns coexist

## 7. UI / Audio Layer

Current source confirms fragmented UI ownership:

- `HectonFabricatorUI`
- `HectonSuitHUD_v4`
- `SuitHUDV4CanvasOverlay`
- multiple PDA tab controllers

Current source also confirms audio surface fragmentation:

- `SpatialAudioManager` exists
- `IAudioService` implementor was not confirmed in current code audit
- project contains both first-party runtime audio code and specialized event surfaces such as `AudioLogEvents`

## 8. Event Narrative

Current project does not rely on a single bus.
It uses at least three event styles:

- static gameplay buses such as `InteractionEvents`, `CraftingEvents`, `SaveEvents`, `ScanEvents`
- embedded static buses inside feature owners such as `PDAEvents`, `FlashlightEvents`, `RandomEventEvents`
- separate modding bus `HectonEventBus`

This means "one frame" can branch through multiple signaling styles depending on feature.

## 9. What This Document Explicitly Does Not Claim

- no verified `60 FPS`
- no verified `< 16.67 ms` frame total
- no verified `0 B/frame`
- no verified persistent memory totals
- no proof that every cited subsystem is active in the same shipped scene path

Those claims require profiler data, GCMonitor data, or user-provided runtime logs.

## 10. Regression Model

CPU: no runtime code changed
GC: no runtime code changed
Memory: no runtime assets or containers changed
Cadence: only documentation certainty was reduced to match evidence
Correctness: improved because unsupported measurements and fake pass/fail tables were removed

## 11. Hot Path Impact

None. Markdown-only change.

## 12. Failure Modes

- future readers may still mistake narrative flow for verified execution order
- source ownership can drift without synchronized document maintenance
- hidden scene wiring can bypass the architecture described here

## 13. Why This Version Was Kept

Kept because it describes the structure without pretending to be a profiler capture.
Rejected content: unsupported timing tables, unsupported GC tables, and "ETA VERIFIED" status language.

STATUS: PENDING VERIFICATION
