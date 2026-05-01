# HECTON-8 Codex Project-Wide Audit

Date: 2026-04-29  
Status: PENDING VERIFICATION  
Scope: first-party audit of `Assets/_Project/Scripts`, current Unity Editor state, and supplement pass over existing Codex audit folders

Mandates followed:
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`

## What This Folder Adds

This folder does not replace:

- `Docs/DEPRECATED/2026-04-29_Audit_Bundles/2026-04-29_Codex_Autonomous_Audit`
- `Docs/DEPRECATED/2026-04-29_Audit_Bundles/2026-04-29_CODEX_MANDATE_AUDIT`
- `Docs/DEPRECATED/2026-04-29_Audit_Bundles/2026-04-29_Codex_Codebase_Reality_Audit`

It supplements them with:

1. a refreshed project-wide metrics pass over current first-party scripts
2. a blunt distinction between what is structurally strong, merely survivable, and still dangerous
3. same-day freshness correction after the reachable Unity Editor state changed relative to earlier audit snapshots

## Coverage Snapshot

- First-party scripts under `Assets/_Project/Scripts`: `970`
- First-party scripts under `Assets/_Project`: `1010`
- First-party script lines under `Assets/_Project/Scripts`: `420468`
- Average lines per script: `433.47`
- Scripts larger than `2000` lines: `32`
- Scripts larger than `4000` lines: `7`
- First-party tests under `Assets/_Project/Tests`: `4`

Largest owner files observed in this pass:

- `World/HectonMapMagicVegetationBridge.cs`: `13279` lines
- `WorldProceduralScatterDirector.cs`: `10333` lines
- `HectonPlayerMovement.cs`: `7851` lines
- `HectonUnderwaterVisuals.cs`: `4826` lines
- `UI/SuitHUDV4CanvasOverlay.cs`: `4608` lines
- `Audio/PlayerCriticalProceduralAudioRenderer.cs`: `4200` lines
- `HectonVoxelEngine.cs`: `4138` lines

## Current High-Confidence Verdict

- The codebase has real engine work in it. This is not random Unity trash.
- The codebase is also carrying too many oversized owners, too many persistent bootstrap/lifetime paths, and too much architectural overlap.
- The strongest parts are the dispatcher spine, interaction contracts, and parts of the zero-GC HUD/event migration.
- The weakest parts are bootstrap authority convergence, giant world/UI/player owners, and missing runtime proof.

## Current Editor State

- active Unity scene during this audit: `02_HECTON_WORLD`
- loaded scenes: only `02_HECTON_WORLD`
- Build Settings scenes:
  - `00_BOOTSTRAP`
  - `01_MAIN_MENU`
  - `02_HECTON_WORLD`
- scene is dirty in editor
- latest console readback shows `15` errors, but all visible entries are package-side MCP `ManageAsset` conversion failures rather than first-party compile errors

Observed console pattern:

- tool/package source: `./Library/PackageCache/com.coplaydev.unity-mcp.../Editor/Tools/ManageAsset.cs`
- repeated message: `Failed to convert -1 to a unsigned 32 bit int`
- affected assets: `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_*`

Important evidence detail:

- older same-day audit slices had captured first-party compile blockers
- the latest reachable editor state no longer reports those first-party blockers
- this package therefore treats them as stale historical observations, while separately recording the current package-side MCP console errors

## Files In This Folder

- `SYSTEM_SCORECARD.md`
  subsystem verdicts: good / acceptable / bad
- `FINDINGS_AND_EVIDENCE.md`
  raw counts, owner references, and current behavior-risk notes
- `ACTUALITY_RECHECK_2026-04-29.md`
  same-day freshness correction against updated source/editor state

## Regression Model

CPU: no runtime code changed by this audit package  
GC: no gameplay-path code changed; documentation only  
Memory: no runtime asset mutation performed by this audit package  
Cadence: documentation only  
Correctness: improved only in the sense that current project state is described more precisely and older stale blockers were removed

## Hot Path Impact

None. Markdown-only pass.

## Failure Modes

- counts will drift again as the codebase changes
- current console state may change after the next user patch or Unity reload
- static scans show real pressure points, but not full frame-time cost without profiler captures

## Why This Version Was Kept

Kept because it corrects stale assumptions from older same-day audit slices:

- current reachable editor state is cleaner than earlier snapshots
- runtime `Resources.UnloadUnusedAssets()` is not present in the current first-party script pass
- runtime `DG.Tweening` / `DOTween` hits are `0` in the current first-party script pass
- queue-backed event patterns exist in more places than older summaries implied

Anything stronger than that would be fake certainty.
