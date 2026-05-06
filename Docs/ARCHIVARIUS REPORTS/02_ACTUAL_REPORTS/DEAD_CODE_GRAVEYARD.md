# DEAD CODE GRAVEYARD

Date: 2026-05-07
Status: PENDING VERIFICATION
Scope: evidence-backed static orphan sweep for first-party runtime C# classes
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `UI_Data_Streaming_ZeroGC_Optimization.txt`, `STRM_Persistent_Object_Registry.txt`

## Purpose

This file does not pretend to be a full Roslyn call graph.

It exists to separate three things that older graveyard reports mixed together:

1. strong orphan candidates with no visible runtime owner
2. embedded diagnostics that still exist in scene/prefab YAML
3. verification infrastructure that is narrow, ugly, but still wired

## Method

Static sweep used three evidence lanes per class:

1. external runtime code references outside the declaring `.cs`
2. YAML references by script GUID across `Assets/_Project/Prefabs`, `Assets/_Project/Scenes`, `Assets/_Project/Data`, `Assets/_Project/Art`
3. bootstrap/service-owner mentions inside the class body itself

Important limit:

- `bootstrap/service-owner mentions` are not proof that a class is alive
- they only prove the class knows how to query runtime context if it ever gets instantiated
- no live Unity execution proof was collected in this pass

## Strong Orphan Candidates

These are the strongest dead-code candidates found in this pass.
They have `0` external runtime code refs and `0` prefab/scene/data YAML refs.

| Class | File | External code refs | YAML refs | Internal bootstrap/service mentions | Current reading |
|---|---|---:|---:|---:|---|
| `SaveSystemRuntimeSmokeTester` | `Assets/_Project/Scripts/SaveSystemRuntimeSmokeTester.cs` | 0 | 0 | 0 | RETAINED / RESTORED by 2026-05-01; file and `.meta` exist, no YAML refs found, current source uses `async Awaitable` not `StartCoroutine` |
| `WorldGenerativeGeologyRuntimeSmokeTester` | `Assets/_Project/Scripts/WorldGenerativeGeologyRuntimeSmokeTester.cs` | 0 in prior scan; current editor refs found | 0 | 0 | retained; `Assets/_Project/Editor/HectonDevToolsMenu.cs` currently references/adds it |
| `WeakToolsRuntimeSmokeTester` | `Assets/_Project/Scripts/Dev/WeakToolsRuntimeSmokeTester.cs` | 0 | 0 | 2 | REMOVED 2026-04-30; file and `.meta` deleted |
| `MantaAcousticRuntimeVerifier` | `Assets/_Project/Scripts/Dev/MantaAcousticRuntimeVerifier.cs` | 0 | 0 | 3 | REMOVED 2026-04-30; file and `.meta` deleted |
| `PhysicalInteractionRuntimeVerifier` | `Assets/_Project/Scripts/Dev/PhysicalInteractionRuntimeVerifier.cs` | 0 | 0 | 3 | REMOVED 2026-04-30; file and `.meta` deleted |

## Embedded Diagnostics, Not Dead Yet

These classes also have `0` external runtime code refs, but they are still attached in YAML and therefore are not honest deletion targets yet.

| Class | File | YAML anchor | Current reading |
|---|---|---|---|
| `FabricationRuntimeSmokeTester` | `Assets/_Project/Scripts/FabricationRuntimeSmokeTester.cs` | `Assets/_Project/Prefabs/Player.prefab` | embedded player-side verification component |
| `ScanRuntimeSmokeTester` | `Assets/_Project/Scripts/ScanRuntimeSmokeTester.cs` | `Assets/_Project/Prefabs/Player.prefab` | embedded player-side verification component |
| `ToolTrialRangeRuntimeSmokeTester` | `Assets/_Project/Scripts/ToolTrialRangeRuntimeSmokeTester.cs` | `Assets/_Project/Prefabs/Player.prefab` | embedded player-side verification component |
| `UIRuntimeSmokeTester` | `Assets/_Project/Scripts/UIRuntimeSmokeTester.cs` | `Assets/_Project/Prefabs/Player.prefab` | embedded player-side verification component |
| `BarterRuntimeSmokeTester` | `Assets/_Project/Scripts/BarterRuntimeSmokeTester.cs` | `Assets/_Project/Prefabs/Player.prefab` | embedded player-side verification component |
| `ToolRuntimeSmokeTester` | `Assets/_Project/Scripts/ToolRuntimeSmokeTester.cs` | `Assets/_Project/Prefabs/Player.prefab` | embedded player-side verification component |
| `FieldToolRuntimeSmokeTester` | `Assets/_Project/Scripts/FieldToolRuntimeSmokeTester.cs` | `Assets/_Project/Prefabs/Player.prefab` | embedded player-side verification component |
| `BuilderRuntimeSmokeTester` | `Assets/_Project/Scripts/BuilderRuntimeSmokeTester.cs` | `Assets/_Project/Prefabs/Player.prefab` | embedded player-side verification component |
| `ShellVerificationRuntimeSmokeTester` | `Assets/_Project/Scripts/Dev/ShellVerificationRuntimeSmokeTester.cs` | `Assets/_Project/Scenes/00_BOOTSTRAP.unity` | bootstrap-scene verification hook, not orphan |
| `DefaultFlowFieldProfile` | `Assets/_Project/Scripts/Compatibility/LegacyStubs/DefaultFlowFieldProfile.cs` | `Assets/_Project/Data/DefaultFlowFieldProfile.asset` | serialized legacy stub asset still exists |

## Narrow Verification Stack, Still Wired

These are not good architecture, but static evidence says they still belong to a verification chain.

| Class | File | Evidence |
|---|---|---|
| `VerificationRuntimeProbe` | `Assets/_Project/Scripts/Tools/VerificationRuntimeProbe.cs` | referenced by `ShellVerificationRuntimeSmokeTester`, `RuntimePerformanceProfiler`, `StateRecoveryVerifier`, `SceneTransitionVerifier`, `PauseSystemVerifier` |
| `PauseSystemVerifier` | `Assets/_Project/Scripts/Tools/PauseSystemVerifier.cs` | referenced by `ShellVerificationRuntimeSmokeTester` |
| `SceneTransitionVerifier` | `Assets/_Project/Scripts/Tools/SceneTransitionVerifier.cs` | referenced by `ShellVerificationRuntimeSmokeTester` |
| `StateRecoveryVerifier` | `Assets/_Project/Scripts/Tools/StateRecoveryVerifier.cs` | referenced by `ShellVerificationRuntimeSmokeTester` |
| `RuntimeDiagnosticsTrace` | `Assets/_Project/Scripts/RuntimeDiagnosticsTrace.cs` | referenced by `BootstrapController`, `SceneBootstrap`, `GameTickManager`, `ObjectPoolManager`, `RuntimePerformanceProfiler`, geology/scatter systems |
| `ScatterDiagnosticsTracker` | `Assets/_Project/Scripts/World/ScatterDiagnosticsTracker.cs` | referenced by `WorldProceduralScatterDirector.cs` |
| `WorldSandboxAttractionProfile` | `Assets/_Project/Scripts/WorldSandboxAttractionProfile.cs` | runtime code refs + `9` authored sandbox-attraction assets |

## What This Sweep Does Not Prove

- It does not prove these candidates are safe to delete.
- It does not prove there are no other dead classes.
- It does not see reflection, inspector event bindings, or live runtime instantiation done outside YAML.
- It does not include package code or editor-only classes as deletion targets.

## Recommended Action Order

1. Keep `WorldGenerativeGeologyRuntimeSmokeTester` until `HectonDevToolsMenu` is migrated or the editor menu entry is removed.
2. Re-run scene/prefab validation after Unity import settles.
3. Do not delete player-attached smoke testers until the verification strategy is explicitly replaced.
4. Continue one-domain-at-a-time cleanup; do not batch-delete verification infrastructure.

## Regression Model

| Dimension | Risk |
|---|---|
| CPU | orphan cleanup can reduce editor/runtime noise if candidates are truly unused |
| GC | neutral unless hidden loaders instantiate these probes |
| Memory | minor improvement possible if dead components or assets are removed |
| Cadence | risk of losing verification hooks if a candidate is only dynamically created |
| Correctness | highest risk is false deletion of a hidden diagnostic path |

## Verdict

The older graveyard report overstated confidence and mixed live verification hooks with dead-code guesses.

Current evidence-backed state:

- `3` prior strong orphan candidates remain removed in this worktree: `WeakToolsRuntimeSmokeTester`, `MantaAcousticRuntimeVerifier`, `PhysicalInteractionRuntimeVerifier`
- `1` prior candidate is alive again or was not deleted in the current worktree: `SaveSystemRuntimeSmokeTester`
- `1` prior candidate retained because a current editor owner was found
- `10` embedded or wired diagnostics explicitly retained
- no deletion-safe proof yet

STATUS: PENDING VERIFICATION

## 2026-05-01 Graveyard Delta

Current filesystem truth contradicts the earlier "removed" status for `SaveSystemRuntimeSmokeTester`.

Evidence:

- `Assets/_Project/Scripts/SaveSystemRuntimeSmokeTester.cs` exists.
- `Assets/_Project/Scripts/SaveSystemRuntimeSmokeTester.cs.meta` exists.
- Script GUID `82e0caac595fb7147bbeeb6fbcb440b8` had no matches under `Assets/_Project/Prefabs`, `Assets/_Project/Scenes`, or `Assets/_Project/Data` in this pass.
- Current source contains `async Awaitable` methods and `Awaitable.NextFrameAsync(cancellationToken: destroyCancellationToken)` calls.
- Current source scan found no `StartCoroutine`, `IEnumerator`, or `yield` tokens in that file.

Current reading:

`SaveSystemRuntimeSmokeTester` is not a scene/prefab/data-attached runtime component by YAML evidence, but it is also not deleted. Treat it as a retained orphan/verification harness candidate, not as completed dead-code removal.

STATUS: PENDING VERIFICATION
