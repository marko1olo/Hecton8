# HECTON-8 Codex Codebase Reality Audit

Date: 2026-04-29  
Status: PENDING VERIFICATION  
Scope: first-party audit of `Assets/_Project/Scripts` plus current Unity Editor readback

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
2. Which subsystems are structurally strong, merely survivable, or actively risky.
3. Which risks are proven by source and current Unity Editor evidence.
4. Which claims remain blocked by missing runtime proof.

## Evidence Basis

- Direct readback of `AGENTS.md`, root docs, and `Docs/ARCHIVARIUS REPORTS`.
- Physical inventory of `Assets/_Project/Scripts`.
- Static rule scans across all first-party scripts.
- Manual readback of owner files in bootstrap, core dispatch, save, interaction, UI, world, and audio.
- Live Unity MCP checks:
  - Build Settings scene list.
  - Loaded scene state.
  - Unity Console warnings/errors.

## Coverage Snapshot

- First-party C# files under `Assets/_Project`: `1010`
- First-party C# files under `Assets/_Project/Scripts`: `970`
- First-party test scripts under `Assets/_Project/Tests`: `4`
- Total first-party script lines under `Assets/_Project/Scripts`: `420468`
- Average lines per script: `433.47`
- Scripts stored directly in `Assets/_Project/Scripts` root: `312`

This is enough coverage to call out structural truths. It is not enough to claim gameplay fixes are verified.

## Immediate Verdict

- The project contains real architecture work, not just random Unity sludge.
- The project is also carrying severe structural debt, especially in startup ownership, file-size concentration, and root-folder sprawl.
- Current reachable Unity Editor state is materially cleaner than the older first-party compile-break snapshots, but it is not console-clean.
- Latest console readback surfaced `15` package-side MCP `ManageAsset` errors against `ResourceNodeTemplate_*` assets and did not surface first-party compile errors.
- Because of that, current reality is worse than "clean and proven" but better than the older "actively broken compile" narrative.

## Most Important Proven Findings

1. Current Unity MCP readback shows Build Settings still aligned to `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`, with `02_HECTON_WORLD` loaded and active in editor.
2. The runtime core has a serious foundation: registry-driven dispatch, native job lanes, packetized interaction, and explicit bootstrap/service patterns.
3. Several older document claims were no longer true at recheck time:
   - `SpatialAudioManager` is a direct `IAudioService` owner
   - `SuitHUDV4CanvasOverlay` is a direct `IUIService` owner
   - `HabitatIntegrityManager` is the current direct `Hecton8.Core.IDamageReceiver` owner found by source scan
   - queue-backed event buses now include `SaveEvents`, `QuestEvents`, `ScanEvents`, `NarrativeEvents`, and `AudioLogEvents`
4. That foundation is still undermined by fragmentation:
   - two bootstrap authorities (`Bootstrap/GameBootstrapper.cs` and `SceneBootstrap.cs`)
   - very large god-object files in world/scatter/player/UI domains
   - mixed queue-backed and direct static event architectures
5. Several older red flags are stale and were removed from the current truth set:
   - no current `Resources.UnloadUnusedAssets()` hit was found under `Assets/_Project/Scripts`
   - no current `DG.Tweening` / `DOTween` hit was found under `Assets/_Project/Scripts`
   - current UI/interaction `SetActive(...)` hits are low, not the broad hot-path spread described in older notes

## Files In This Folder

- `SYSTEM_SCORECARD.md`
  Verdict by subsystem: good / acceptable / bad.
- `FINDINGS_AND_EVIDENCE.md`
  Raw findings, counts, references, and current editor-state evidence.

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
- Console state can change between editor sessions and can also be polluted by MCP package-side tooling errors.
- Static scans detect real smoke, but not every semantic fire.
- Runtime feel, pacing, visual quality, and hitch behavior still require profiler captures and controlled playthroughs.

## Why This Version Was Kept

Kept because it is evidence-backed and hostile to fake certainty.
Rejected alternatives:
- a flattering summary
- an outdated doom summary that keeps stale compile-breakage claims after the editor state changed
