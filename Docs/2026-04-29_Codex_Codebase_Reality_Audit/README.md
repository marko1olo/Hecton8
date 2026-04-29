# HECTON-8 Codex Codebase Reality Audit

Date: 2026-04-29  
Status: PENDING VERIFICATION  
Scope: first-party audit of `Assets/_Project/Scripts` plus live Unity Editor readback

Mandates followed:
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`

## What This Audit Covers

This folder is a blunt project-health readout, not a comfort document.
It answers four questions:

1. What is physically present in first-party code right now.
2. Which subsystems are structurally strong, merely survivable, or actively broken.
3. Which risks are proven by source and live Unity console evidence.
4. Which claims remain blocked by missing runtime proof.

## Evidence Basis

- Direct readback of `AGENTS.md`, root docs, and `Docs/ARCHIVARIUS REPORTS`.
- Physical inventory of `Assets/_Project/Scripts`.
- Static rule scans across all first-party scripts.
- Manual readback of owner files in bootstrap, core dispatch, save, interaction, UI, world, and audio.
- Live Unity MCP checks:
  - Build Settings scene list.
  - Loaded scene state.
  - Unity Console compilation/runtime errors.

## Coverage Snapshot

- First-party C# files under `Assets/_Project`: `976`
- First-party C# files under `Assets/_Project/Scripts`: `936`
- First-party test scripts under `Assets/_Project/Tests`: `4`
- Total first-party script lines under `Assets/_Project/Scripts`: `398253`
- Average lines per script: `425.48`
- Scripts stored directly in `Assets/_Project/Scripts` root: `306`

This is enough coverage to call out structural truths. It is not enough to claim gameplay fixes are verified.

## Immediate Verdict

- The project contains real architecture work, not just random Unity sludge.
- The project is also carrying severe structural debt and live breakage.
- Current highest truth: the first-party codebase is not in a clean compilable state.
- Because of that, any claim about current real-game behavior beyond limited scene/editor evidence is blocked.

## Most Important Proven Findings

1. Live Unity console currently shows first-party compile errors in quest, save, fluid, world, and UI systems.
2. The runtime core has a serious foundation: registry-driven dispatch, native job lanes, packetized interaction, and explicit bootstrap/service patterns.
3. That foundation is undermined by fragmentation:
   - two bootstrap authorities (`Bootstrap/GameBootstrapper.cs` and `SceneBootstrap.cs`)
   - fragmented `IUIService`
   - ghost `IAudioService`
   - very large god-object files in world/scatter/player/UI domains
4. A small but meaningful set of mandate violations remains live in runtime code:
   - `Resources.UnloadUnusedAssets()` in `UI/PauseMenuController.cs:1004`
   - DOTween dependency in `VFX/CameraJuiceSystem.cs`
   - runtime `LateUpdate()` owners outside the dispatcher-only ideal
   - heavy UI `SetActive()` usage across PDA / pause / tooltip flows

## Files In This Folder

- `SYSTEM_SCORECARD.md`
  Verdict by subsystem: good / acceptable / bad.
- `FINDINGS_AND_EVIDENCE.md`
  Raw findings, counts, references, and live console blockers.

## Regression Model

CPU: no runtime code changed by this audit package  
GC: no gameplay-path code changed; audit only  
Memory: no runtime asset mutation performed by this report  
Cadence: documentation only  
Correctness: improved only in the sense that current project state is described more honestly

## Hot Path Impact

None. Documentation-only pass.

## Failure Modes

- Counts will drift as files move or new scripts land.
- Compile errors can change between editor sessions.
- Static scans detect real smoke, but not every semantic fire.
- Runtime feel, pacing, visual quality, and hitch behavior still require profiler captures and controlled playthroughs after the compile state is repaired.

## Why This Version Was Kept

Kept because it is evidence-backed and hostile to fake certainty.
Rejected alternative: a flattering summary that ignores the compile state and structural fragmentation.

