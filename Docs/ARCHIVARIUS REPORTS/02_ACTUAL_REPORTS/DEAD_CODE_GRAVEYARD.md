# DEAD CODE GRAVEYARD

Date: 2026-04-30
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
| `SaveSystemRuntimeSmokeTester` | `Assets/_Project/Scripts/SaveSystemRuntimeSmokeTester.cs` | 0 | 0 | 0 | strong orphan candidate |
| `WorldGenerativeGeologyRuntimeSmokeTester` | `Assets/_Project/Scripts/WorldGenerativeGeologyRuntimeSmokeTester.cs` | 0 | 0 | 0 | strong orphan candidate |
| `WeakToolsRuntimeSmokeTester` | `Assets/_Project/Scripts/Dev/WeakToolsRuntimeSmokeTester.cs` | 0 | 0 | 2 | strong orphan candidate; self-resolves player/tool context but no external owner found |
| `MantaAcousticRuntimeVerifier` | `Assets/_Project/Scripts/Dev/MantaAcousticRuntimeVerifier.cs` | 0 | 0 | 3 | strong orphan candidate; internal `GlobalRegistry`/`SceneBootstrap` lookups only |
| `PhysicalInteractionRuntimeVerifier` | `Assets/_Project/Scripts/Dev/PhysicalInteractionRuntimeVerifier.cs` | 0 | 0 | 3 | strong orphan candidate; internal `GlobalRegistry`/`SceneBootstrap` lookups only |

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

1. Review the `5` strong orphan candidates first.
2. Confirm no dynamic `AddComponent`, reflection, or test harness loader instantiates them.
3. Remove one candidate at a time, then re-run compile + scene/prefab validation.
4. Do not delete player-attached smoke testers until the verification strategy is explicitly replaced.

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

- `5` strong orphan candidates
- `10` embedded or wired diagnostics explicitly retained
- no deletion-safe proof yet

STATUS: PENDING VERIFICATION
