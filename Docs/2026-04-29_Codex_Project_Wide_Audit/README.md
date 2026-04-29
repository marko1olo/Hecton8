# HECTON-8 Codex Project-Wide Audit

Date: 2026-04-29  
Status: PENDING VERIFICATION  
Scope: first-party audit of `Assets/_Project/Scripts`, current Unity Editor compile state, and supplement pass over existing Codex audit folders

Mandates followed:
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`

## What This Folder Adds

This folder does not replace:

- `Docs/2026-04-29_Codex_Autonomous_Audit`
- `Docs/2026-04-29_CODEX_MANDATE_AUDIT`
- `Docs/2026-04-29_Codex_Codebase_Reality_Audit`

It supplements them with three things:

1. a fresh project-wide metrics pass over current first-party scripts
2. an explicit split between what is genuinely strong, merely survivable, and actively broken
3. current Unity compile facts after a new compile request, including places where single-file validation and full-project compile disagree

## Coverage Snapshot

- First-party scripts under `Assets/_Project/Scripts`: `948`
- Runtime-like scripts after excluding `Editor`, `Dev`, `SmokeTester`, `Verifier`: `822`
- First-party script lines under `Assets/_Project/Scripts`: `406783`
- Average lines per script: `429.1`
- Scripts larger than `2000` lines: `31`
- Scripts larger than `4000` lines: `6`
- First-party tests under `Assets/_Project/Tests`: `4`

Largest owner files observed in this pass:

- `World/HectonMapMagicVegetationBridge.cs`: `13205` lines
- `WorldProceduralScatterDirector.cs`: `10316` lines
- `HectonPlayerMovement.cs`: `7847` lines
- `HectonUnderwaterVisuals.cs`: `4826` lines
- `UI/SuitHUDV4CanvasOverlay.cs`: `4524` lines
- `HectonVoxelEngine.cs`: `4130` lines

## Current High-Confidence Verdict

- The codebase has real engine work in it. This is not random Unity trash.
- The codebase is also carrying too many oversized owners, too many persistent bootstrap/lifetime paths, and a live compile break.
- The strongest parts are the low-level contracts and some newer zero-GC paths.
- The weakest parts are current build health, bootstrap ownership convergence, and large UI/world owners that still carry too much responsibility.

## Proven Current Editor State

- Active Unity scene during this audit: `02_HECTON_WORLD`
- Loaded scenes: only `02_HECTON_WORLD`
- Build Settings scenes:
  - `00_BOOTSTRAP`
  - `01_MAIN_MENU`
  - `02_HECTON_WORLD`
- Scene is dirty in editor
- Full Unity compile request completed during this pass

Current console-backed compile blockers after the compile request:

- `Visor/VolumetricLightFeature.cs`
  - `CS1503` on `ComputeCommandBuffer` to `UnsafeCommandBuffer` conversion
- `FluidCompartmentTemplate.cs`
  - `CS0122` on inaccessible `CompartmentRecord` fields
- `Construction/BaseDegradationSystem.cs`
  - `CS0103` on unresolved `ConnectionSplineBatchRenderer`
  - `CS0103` on unresolved `HectonFloatingOrigin`

Important evidence detail:

- `validate_script` returned clean results for `VolumetricLightFeature.cs`, `SeamRegistry.cs`, and `UI/PDALoadoutTab.cs`
- the full-project compile still fails
- conclusion: per-file validation is not sufficient proof of assembly-wide health in this workspace

## Files In This Folder

- `SYSTEM_SCORECARD.md`
  - subsystem verdicts: good / acceptable / bad
- `FINDINGS_AND_EVIDENCE.md`
  - raw counts, owner references, compile facts, and behavior risk notes

## Regression Model

CPU: no runtime code changed by this audit package  
GC: no gameplay-path code changed; documentation only  
Memory: no runtime asset mutation performed by this audit package  
Cadence: documentation only  
Correctness: improved only in the sense that the current project state is described more precisely

## Hot Path Impact

None. Markdown-only pass.

## Failure Modes

- counts will drift again as the codebase changes
- current console errors may change after the next user patch or Unity reload
- editor render-texture stats are suspicious but not yet a play-mode memory proof
- static scans show real pressure points, but not full frame-time cost without profiler captures

## Why This Version Was Kept

Kept because it corrects stale assumptions from older audit slices:

- there is a live compile break now
- runtime `Resources.UnloadUnusedAssets()` is not present in the current first-party script pass
- runtime `GameObject.Find` hits are editor/dev heavy and currently `0` in runtime-like code
- packetized/queued event patterns exist in more places than older summaries implied

Anything stronger than that would be fake certainty.
