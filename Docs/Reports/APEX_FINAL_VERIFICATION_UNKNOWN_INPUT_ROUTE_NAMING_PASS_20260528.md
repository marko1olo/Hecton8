# APEX Final Verification - UNKNOWN Input Route Naming Pass - 2026-05-28

Status: `PENDING_RUNTIME_VERIFICATION`.

Verdict: static Core/Input contract fix only. I renamed the private mutating DataVault helper from `TryResolveOrAcquireInputBuffer<T>` to `OpenOrAcquireInputBufferForOwnerRoute<T>` and updated all local owner-route call sites.

JSON SHA-256: `A38DE05D5D31067D593E305C8CA081AC44B843EAFDF1DD2DD9667E93BC062528`.

## What Was Wrong

`InputDispatcher.cs` had a private method named `TryResolveOrAcquireInputBuffer<T>`, but the body calls `vault.EnsureGenerationHandle<T>` at `Assets/_Project/Scripts/Core/InputDispatcher.cs:1006`. That is open/acquire behavior, not a pure resolve/read accessor.

That conflicts with the Core doctrine: read accessors must not publish, allocate/grow buffers, complete jobs, mutate global state, sync scene state, or search the scene.

## What Changed

- `Assets/_Project/Scripts/Core/InputDispatcher.cs`: deterministic input owner opens at lines `864..932`, helper declaration at `992`, XR owner open at `2726`.
- `Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs`: haptic owner opens at lines `407..445`.
- Active old-name scan: `TryResolveOrAcquireInputBuffer` => no match, `rg` exit `1`.

## Zero-GC Static Scan

Scope: `git diff -U0` added lines in the two modified source files.

| Metric | Count |
|---|---:|
| Added lines | 21 |
| Reference-type `new` suspects | 0 |
| `string.Format` | 0 |
| `.ToString()` | 0 |
| LINQ call tokens | 0 |
| `foreach` | 0 |
| `.Complete()` | 0 |
| Added `EnsureGenerationHandle` | 0 |
| Added `GlobalRegistry` | 0 |
| Added old route name | 0 |
| Added new route name | 21 |

## Data Sovereignty

No fields were migrated to `GlobalDataVault` in this pass.

No new `BufferID` constants were introduced.

Existing affected route names cover deterministic input, XR input, and haptic synthesis buffers. This pass did not add a writer path; therefore `TryAcquireWriteLock`/`finally` proof is not claimed. Counts added by this patch: `TryAcquireWriteLock=0`, `ReleaseWriteLock=0`, `finally=0`.

## Scalability / Cinematic Cheat

No physical simulation or visual algorithm was added. No cinematic cheat is needed for this pass.

No binary `isLowEnd` route was added. Existing haptic synthesis continues to consume `HomeostasisBrain.GlobalQualityWeight` as the continuous scalar at `HectonInputRuntime_HapticSynth.cs:133`, `178`, `189`, `289`, `334`, `345`, `514`, `587`, and `588`.

## Compilation Throttle

I did not run `dotnet build`.

Final build decision sample:

- CPU: `51.7%`
- active `dotnet`: none observed
- active `csc` / `VBCSCompiler`: none observed
- reason skipped: CPU exceeded the `AGENTS.md` 50% limit, and the user assigned global compile-wall repair to another agent.

## Static Proof

- `InputDispatcher.cs` SHA-256: `E8196DA9E38B2AD893A03C5867596B0C09CF67879FA33A00F9AA14B402C61450`
- `HectonInputRuntime_HapticSynth.cs` SHA-256: `FBE414B7CD6036146EAA299447E6689E68A2C7CF5AC19082ED841DBC11F06B03`
- Brace counts: `InputDispatcher.cs 339/339`, `HectonInputRuntime_HapticSynth.cs 51/51`
- Scoped `git diff --check`: exit `0`; line-ending warnings only

## Residuals

Runtime proof is absent: no Unity import, Console check, Play Mode, profiler/GCMonitor pass, player build, device run, or crash/NaN dump.

Crash dump path reserved by report: `Docs/AgentLogs/Dump_UNKNOWN_INPUT_ROUTE_NAMING_PASS.bin`; file does not exist because no crash/runtime run occurred.
