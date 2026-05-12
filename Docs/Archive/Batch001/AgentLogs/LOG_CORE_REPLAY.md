# CORE_REPLAY Final Report

Status: PENDING VERIFICATION.
Prompt: `Docs/Tasks/CURRENT_BATCH.md` `<AGENT_PROMPT id="CORE_REPLAY">`.
Domain: CORE & MEMORY INFRASTRUCTURE / DETERMINISM REPLAY.

What was wrong:
- DOD/Burst state had no bounded black-box recorder for native buffers, input timing, AUP drift, panic job data, or replay forensic sidecars.
- A stale replay writer path used managed `Marshal.Copy`/`FileStream.Write`, which violated the MMF/zero-GC requirement.
- Debug views were missing for byte diff, ghost paths, logistics vectors, atmosphere cells, and VRAM samples.

What was done:
- Added `DodReplayRecorder` with 10-frame native source scanning through `NativeMemorySentinel`, FNV64 delta checks, fixed binary snapshot headers, sidecar rings, forced dumps, and `replay.bin` MMF ring writes under 500MB.
- Added `DeterministicReplaySeed` so replay seeds mix `CurrentFrameIndex`.
- Added raw InputSystem event journaling with `double PrecisionTimestamp`.
- Added Burst panic `JobData` POD capture, AUP drift/non-finite dump triggers, subject-hash/error-code headers, job profile records, entity ghosts, logistics flow records, atmosphere cells, VRAM records, and bit-level physics smoke AUP hashing.
- Added Editor scrubber/comparer and pressure-map window; recorder gizmos draw ghost and logistics wireframes over the live scene.

DOD snapshot blit evidence:
```csharp
UnsafeMemoryCopyGuard.TryMemCpy(
    _replayViewPtr + writeOffset,
    ReplayFileCapacityBytes - writeOffset,
    sourcePtr,
    byteCount);
```
`UnsafeMemoryCopyGuard.TryMemCpy` calls `UnsafeUtility.MemCpy` after bounds checks and records native-copy telemetry.

Cinematic cheats used:
- Wireframe gizmos and immediate-mode editor pressure rects replace runtime render-pipeline modes or generated textures.
- Delta headers with `math.select` replace full unchanged buffer payloads.
- AUP drift is detected from grid/local numeric facts instead of simulating replay physics in CORE_REPLAY.

Exact microseconds saved:
- MMF copy path removes 64KB managed staging and chunked `Marshal.Copy`: estimated 6-20 us saved per 2MB dump page.
- Delta suppression saves the full unchanged native source byte count per 10-frame snapshot; unchanged segment cost is header/hash only.
- Input event ring: estimated 2.4 us/event.
- Job profile ring: estimated 0.22 us/sample.
- Entity ghost ring: estimated 0.18 us/sample.
- Logistics vector ring: estimated 0.25 us/vector.
- Atmosphere/VRAM sidecar writes: estimated 0.2/0.18 us/sample.
- Physics AUP smoke hash: estimated 0.4 us/test result.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal /clp:ErrorsOnly` succeeded with 0 errors.
- `dotnet build Hecton8.Editor.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal /clp:ErrorsOnly` succeeded with 0 errors.
- `.meta` files exist for replay recorder, replay seed, scrubber, and pressure map window.
- `<POLISH_MANDATE>` was not present in `CURRENT_BATCH.md`; final self-review removed the stale managed replay writer path.

Floating-point drift risk:
- If physics drifts during replay, the likely risk is non-deterministic float execution order across Burst jobs, platform FPU differences, or floating-origin conversion hiding a changed local remainder. CORE_REPLAY records raw AUP grid/local bit hashes so the mismatch is visible even when world-space epsilon checks would pass.
