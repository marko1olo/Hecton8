# UNKNOWN Runtime Alloc Route Pass - 2026-05-27

Status: SOURCE PATCHED, STATIC PROOF PASSED, BUILD BOUNDARY BLOCKED BEFORE C#
Agent: UNKNOWN
Evidence class: STATIC_SOURCE / CLI_BUILD_BOUNDARY / DOC_VALIDATION

## Problem

First-party runtime scans after the deeper trap pass still exposed cold copied-array routes in clean files:

- `WorldFidelityRoot` rebuilt renderer, collider, rigidbody, and behaviour caches through `List<T>.ToArray()`.
- `InteractionHighlighter` claimed zero-GC ownership but auto-collected renderers through `List<Renderer>.ToArray()`.
- `H8PrefabRegistryVramEstimator` was editor-only, but still used array `GetComponentsInChildren` and `Renderer.sharedMaterials`.

These are not measured frame spikes. They are avoidable ownership leaks against the copied-array API rule.

## Changed Files

| File | Change |
|---|---|
| `Assets/_Project/Scripts/WorldFidelityRoot.cs` | Reuses owner arrays when counts are stable; removes `ToArray()` cache rebuilds; makes `OnEnable` null-safe. |
| `Assets/_Project/Scripts/InteractionHighlighter.cs` | Reuses the serialized renderer array in auto-collect; removes the zero-GC self-contradiction. |
| `Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistry.cs` | Editor VRAM estimator now uses supplied-list renderer/material APIs. |
| `Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs` | Editor catalog sync reuses `allLogs` when count is stable instead of `ToArray()`. |
| `Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs` | Editor catalog sync reuses `allUpgrades` when count is stable instead of `ToArray()`. |
| `Assets/_Project/Scripts/HectonSurvivalSystem.cs` | Cold markdown-parameter parse copies into the required immutable array without `ToArray()`. |
| `Assets/_Project/Scripts/Gameplay/PlayerExpressionManager.cs` | Editor catalog sync reuses `authoredProfiles` when count is stable instead of `ToArray()`. |
| `Assets/_Project/Scripts/Quest/QuestManager.cs` | Editor quest registry sync reuses `allQuests` when count is stable instead of `ToArray()`. |

## Static Proof

Initial targeted post-patch scan:

```text
rg -n "\.ToArray\(|GetComponentsInChildren<Renderer>\(true\)|\.sharedMaterials\b" \
  Assets/_Project/Scripts/WorldFidelityRoot.cs \
  Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistry.cs \
  Assets/_Project/Scripts/InteractionHighlighter.cs
TARGET_TRAP_SCAN=0
```

Brace balance check:

```text
WorldFidelityRoot.cs: BraceBalance=0, Lines=292
H8PrefabRegistry.cs: BraceBalance=0, Lines=424
InteractionHighlighter.cs: BraceBalance=0, Lines=624
```

First-party runtime scan snapshot:

```text
Resources.Load=0
release-reachable Shader.Find=0
FindObjectOfType/FindObjectsByType/GameObject.Find/FindWithTag=0
Input.touches=0
Physics RaycastAll/OverlapSphere/OverlapBox/OverlapCapsule=0
Renderer.sharedMaterials=0
```

Follow-up `.ToArray()` cleanup after residual classification:

```text
rg -n "\.ToArray\(" Assets/_Project/Scripts -g "*.cs" -g "!**/Editor/**"
Assets/_Project/Scripts\WorldSliceAnchor.cs:264: fidelityRoots = _FidelityRootScratch.ToArray();
Assets/_Project/Scripts\Core\Data\H8DataBaker.cs:1254: rows.Add(cells.ToArray());
```

The five clean residual call sites in `AudioLogSystem`, `SuitUpgradeManager`, `HectonSurvivalSystem`, `PlayerExpressionManager`, and `QuestManager` were removed.
The remaining two files were already dirty under concurrent work and were not touched.

## Residuals

The project is not clean.

- `2` first-party non-Editor-folder `.ToArray()` text occurrences remain.
- `WorldSliceAnchor.cs` is dirty under concurrent work and still has one fidelity-root `ToArray()` route.
- `Core/Data/H8DataBaker.cs` is dirty under concurrent work and still has one CSV row `ToArray()` route.
- `DispatcherJobFence` has the only real runtime `.Complete()` calls; that is dispatcher-owned fence code.
- `SavePersistenceOmegaSmokeTester` has `.Complete(` only as string-audit input.
- `GlobalDataVault.TryGetLatestCreated()` remains in two editor gizmos and one crash-dump route.
- `LocOverflowHandler` uses `TMP_MeshInfo.vertices`; this is not Unity `Mesh.vertices`.

## Build Boundary

No compile-green proof is claimed for this pass.

First guarded build sequence:

```text
guardCpu=51
compilerProcessCount=0
launched=False
reason=CPU_GT_50
retryAttempt=1 cpu=66 compilerProcessCount=0
retryAttempt=2 cpu=86 compilerProcessCount=0
retryAttempt=3 cpu=77 compilerProcessCount=1
retryAttempt=4 cpu=38 compilerProcessCount=0
retryLaunched=True
command=dotnet build Hecton8.slnx /m:1 /nr:false /p:UseSharedCompilation=false
```

Result:

- Exit code: `1`.
- Warnings: `0`.
- Errors: `62`.
- Failure class: `MSB3202`, missing Unity-generated `.csproj` files.
- Root `.csproj` count at verification time: `0`.
- Build did not reach C# compilation.

Raw proof: `BUILD_UNKNOWN_RUNTIME_ALLOC_ROUTE_PASS_20260527.log`.

Second guarded build after the five follow-up source edits:

```text
attempt=1 cpu=34 compilerProcessCount=0
launched=True
command=dotnet build Hecton8.slnx /m:1 /nr:false /p:UseSharedCompilation=false
exitCode=1
0 Warning(s)
62 Error(s)
```

Raw proof: `BUILD_UNKNOWN_RUNTIME_ALLOC_ROUTE_PASS2_20260527.log`.

This is not a source compile diagnostic for `WorldFidelityRoot`, `InteractionHighlighter`, or `H8PrefabRegistry`.
It is the current solution boundary: `Hecton8.slnx` references ignored/generated Unity project files that are not present in the checkout.

## Documentation Validation

Current rerun after this report update:

```text
VerifyDocStructure.py pass=true activeDocCount=666 encodingWithoutUtf8Sig=0
OOP_Doc_Scanner.py finalPass=true activeFileCount=666 sourceSyncPass=true wordReductionPercent=50.90540174249758
```

The first OOP rerun failed on one overlong structured line in `HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
The line was shortened without changing the build-boundary fact, then both validators passed.
