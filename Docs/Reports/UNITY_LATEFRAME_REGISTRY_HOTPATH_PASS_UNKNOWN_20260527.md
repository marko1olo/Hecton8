# UNKNOWN Late-Frame Registry Hot-Path Pass - 2026-05-27

Status: SOURCE PATCHED, STATIC PROOF PASSED, BUILD GUARD BLOCKED, DOC GATES PASSED
Agent: UNKNOWN
Evidence class: STATIC_SOURCE / CLI_BUILD_GUARD / DOC_VALIDATION

## Problem

`PhysicalPanelButton.LateFrameTick()` still removed itself from the late-frame lane through `GlobalRegistry.UnregisterLateFrameTickable`.

This violates the current authority doctrine:

- `GlobalRegistry` is cold identity and dependency injection only.
- Hot paths use cached interfaces, typed signals, snapshots, or dispatcher-owned lanes.
- Late-frame presentation code must not poll or route through registry wrappers.

First-20-minutes route impact: this removes a hot registry dependency from the diegetic physical panel/button path, which is part of the player instrument interaction surface.

## Changed Files

| File | Change |
|---|---|
| `Assets/_Project/Scripts/UI/PhysicalPanelButton.cs` | Replaced late-frame register/unregister wrapper calls with direct `SystemDispatcher` lane calls and a single direct unregister helper. |

## Rejected Alternatives

- Keeping the registry wrapper was rejected because `LateFrameTick()` is a hot presentation phase.
- Adding a new global route was rejected because the dispatcher lane already owns late-frame ticking.
- Delaying unregister until `OnDisable()` was rejected because the button is intentionally event-driven and should not stay resident in the late-frame lane after its visual/audio/haptic work drains.

## Static Proof

GlobalRegistry late-frame route residuals in the touched file:

```text
GlobalRegistry.TryRegisterLateFrameTickable=0
GlobalRegistry.UnregisterLateFrameTickable=0
GlobalRegistry.Dispatcher=0
```

Exact route scan:

```text
rg -n "GlobalRegistry\.(TryRegisterLateFrameTickable|UnregisterLateFrameTickable|Dispatcher)" \
  Assets/_Project/Scripts/UI/PhysicalPanelButton.cs

NO_GLOBALREGISTRY_LATEFRAME_ROUTE_HITS
```

Clean production hot-method scanner:

```text
Files: clean first-party C# under Assets/_Project/Scripts, excluding Editor folders and *.Editor.cs.
Methods: FastTick, Tick, FixedTick, SlowTick, ColdTick, FrostTick, LateFrameTick, PostFixedTick.
Forbidden tokens checked: GlobalRegistry, TryGetLatestCreated, scene search, Camera.main, GetComponent array routes, Resources.Load, Shader.Find, ToArray, ToList, LINQ, Complete.
Result after patch: 0 rows.
```

Brace proof:

```text
PhysicalPanelButton.cs braceBalance=0 lines=851
```

`git diff --check` on the touched source file passed with line-ending warnings only.

## Build Guard

Build was not launched because AGENTS forbids `dotnet build` while any compiler/dotnet process is active.

```text
attempts=30
compilerProcessCount=1 on every attempt
launched=False
active process observed after guard: Unity Editor dotnet under 6000.4.1f1 NetCoreRuntime
```

Raw proof: `BUILD_UNKNOWN_LATEFRAME_REGISTRY_HOTPATH_RECHECK_20260527.log`.

## Documentation Validation

```text
VerifyDocStructure.py pass=true activeDocCount=668 encodingWithoutUtf8Sig=0
OOP_Doc_Scanner.py finalPass=true activeFileCount=668 sourceSyncPass=true wordReductionPercent=50.851091154407946
```

## Residuals

- No runtime/profiler microseconds are claimed.
- Other files still use `GlobalRegistry.TryRegisterLateFrameTickable` / `UnregisterLateFrameTickable`, but the precise clean hot-method scan found no remaining direct forbidden token inside hot method bodies after this pass.
- Dirty source files were not edited.
