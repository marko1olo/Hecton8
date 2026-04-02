# Runtime Smoke Hardening - 2026-04-02

## Scope

This pass focused on two goals:

1. Restore clean scan/runtime compilation after the smoke pass exposed a broken string-cache reference.
2. Remove a reproducible runtime-native leak that appeared on clean play-mode startup.

## Changes

### 1. ScannerTool -> ZeroGCStringCache contract was repaired

Files:
- `Assets/_Project/Scripts/ScannerTool.cs`
- `Assets/_Project/Scripts/ZeroGCStringCache.cs`

Problem:
- `ScannerTool` referenced `ZeroGCStringCache` without importing `Hecton8.Core`.
- One call site still used a bare `CachedToUpperInvariant(...)` symbol.
- The cache implementation itself was misleading: it stored the source string, but still called `ToUpperInvariant()` again on every cache hit.

Fix:
- Added the correct `using Hecton8.Core;` import in `ScannerTool`.
- Converted the module-title path to `ZeroGCStringCache.CachedToUpperInvariant(...)`.
- Rebuilt `ZeroGCStringCache` into a small fixed-slot source/result cache so repeated labels reuse the already-created uppercase string.

Runtime value:
- Scan discovery code compiles again.
- Repeated scan labels now behave like a real cache instead of an allocation wrapper.

### 2. HectonFluidEngine now releases persistent native buffers on OnDisable

File:
- `Assets/_Project/Scripts/HectonFluidEngine.cs`

Problem:
- A clean play-mode start consistently reported:
  - `Leak Detected : Persistent allocates 7 individual allocations.`
- The count matches the seven persistent native buffers owned by `HectonFluidEngine`.
- Those buffers were only released in `OnDestroy()`.
- During editor play-mode teardown / domain transitions, `OnDestroy()` is not the most reliable place to protect persistent native memory.

Fix:
- Added `DisposeNativeArrays()` to `OnDisable()` right after unregistering from `GameTickManager`.
- This keeps runtime behavior intact because the buffers are lazily recreated when simulation needs them again.

Runtime value:
- The persistent leak warning no longer appears on the next clean play-mode startup.
- The fluid runtime is now safer across editor play/stop cycles and domain reload boundaries.

## Verification

- Unity recompiles without new `Error`.
- Console after compile contains only third-party/editor warnings.
- On a clean play-mode startup after the `HectonFluidEngine` fix, the previous `Persistent allocates 7 individual allocations` warning no longer appears.

## Investigation Notes

- Tool and geology smoke menu items can be started through the dev menu and both report their startup logs.
- In the current MCP-driven session, those smoke passes still become unreliable because the editor repeatedly falls into a paused `playmode_transition` state after menu execution.
- That behavior now looks separate from the fixed runtime leak and should be treated as a tooling/runtime-observation issue, not as proof that the gameplay systems themselves are broken.

## Recommended Next Step

1. Inspect why play-mode smoke launched through the editor menu enters a paused transition state in MCP sessions.
2. If needed, add a non-interactive smoke trigger path that does not depend on editor selection/ping side effects.
3. After that, resume targeted world/geology smoke validation on a stable runtime session.
