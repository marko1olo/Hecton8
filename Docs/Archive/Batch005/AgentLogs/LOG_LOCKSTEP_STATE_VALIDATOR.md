# LOCKSTEP_STATE_VALIDATOR LOG

## 2026-05-13 - 300-Frame ECS Hashing

What was wrong:
The project had isolated input and quantized physics, but no system-level proof that physics, player kinematics, habitat water, and entity AUP state remained deterministic across replay windows. Existing compile state is still globally red, so Burst/SIMD verification cannot be completed today.

What was done:
Added `Hecton8.Core.Determinism` and `LockstepStateValidator`.
Registered `SystemID.CoreDeterminism` and `BufferID.PlayerKinematicState`, `RoomWaterLevels`, `EntityAUPs`.
Implemented 300-frame `IPostFixedTickable` snapshot hashing at the end of POST_SIMULATION.
Implemented Burst `IJobParallelFor` element hashing for relative `float3`, room water floats, and player kinematic state.
Reduced subsystem hashes into a 64-bit `MasterStateHash`.
Wrote fixed `.h8replay` blocks: 128-byte header plus 300 fixed-size input frames.
Implemented ghost replay load, input override via `PhysicsDeterminismSignals`, and 10x debug time dilation through `RequestHeadlessTimeDilation`.
On mismatch, emits `DesyncDetectedSignal`, pauses simulation, and dumps the fixed 300-frame blackbox to `Docs/AgentLogs/Dump_LOCKSTEP_STATE_VALIDATOR.bin`.
Excluded VFX, audio, particles, and presentation transforms from truth hashing.

Cinematic Cheats used:
300-frame cadence instead of continuous validation.
Quantized sector-local/AUP-relative values instead of raw world floats.
Category Merkle-style reduction instead of serializing full simulation state.
Low/MX350 normal-play skip; replay mode forces validation.
Presentation systems excluded entirely.

Exact microseconds saved:
Low/MX350 normal gameplay: estimated 65us hash spike and all replay IO avoided by skip gate.
Replay writer: saves one background thread and file handle until first recorded block.
Non-sampled frames: estimated 0us hash work; only fixed telemetry ring write remains.
Sampled frame: estimated 65us for 8K elements per active category plus 8us category/master reduction, pending profiler proof.
Desync path: fault-only dump, 0us normal play cost.

Verification:
`validate_script` on `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs`: 0 errors, 0 warnings.
Unity console filter for `Lockstep`: 0 entries.
Omega audit: no managed `foreach`, interpolated strings, `string.Format`, `.ToString()`, `math.sqrt`, `math.normalize`, or floating divisions in the validator.
`dotnet build Hecton8.Core.csproj --no-restore`: failed with 151 unrelated assembly/reference errors.
Unity compile/Burst entrypoint check: blocked by `SimulationBucketingContracts.cs`, `DeployableSdfDrillContracts.cs`, and missing `Hecton8.UI.Tools` assembly resolution.

Final status:
Tasks 1-18 complete.
Task 19 blocked by dependency.
Status remains PENDING until global compile dependencies clear and Burst/SIMD disassembly/profiler verification can run.

## 2026-05-13 - Replay Hardening Addendum

What was wrong:
Ghost replay validation could false-desync if the validator frame counter was not aligned to the recorded replay block.
Replay recording appended to old `.h8replay` files, which could load stale cross-session blocks later.
The truncation flag treated exactly 8192 elements as truncated even though that is the configured cap.
The sampled frame path reread the input signal after already capturing it.
The cached Low/MX350 tier could go stale after a platform scalability change.

What was done:
Validated replay block headers and contiguous 300-frame windows before loading.
Aligned `_postSimulationFrame` to the first replay block start when ghost replay begins.
Added explicit replay fence-frame mismatch reporting through `DesyncDetectedSignal`.
Changed replay recording to recreate the `.h8replay` file per run instead of appending.
Preserved the currently applied ghost input actions into `PlayerKinematicState` hashing.
Changed truncation to an explicit source-count boolean so exactly 8192 elements are valid.
Registered with `ScalabilityEvents` to update the cached tier without hot-path registry polling.
Reused the captured input signal for sampled player-state mirroring.

Cinematic Cheats used:
No new physical simulation.
Replay proof remains a 300-frame forensic cadence, not continuous state serialization.
Low/MX350 still sheds normal-play hash/input-capture/IO work.

Exact microseconds saved:
Removed one sampled-frame input SignalBus snapshot: estimated 1-3us on i3/MX350, pending profiler proof.
Replay header validation: cold file-load only, 0us normal gameplay.
Scalability tier event listener: 0us per tick beyond the existing cached branch.
File recreation: cold writer startup only; avoids stale forensic load failures.

Verification:
Prompt was re-extracted from `Docs/Tasks/CURRENT_BATCH.md`.
`git diff --check`: clean except existing line-ending warnings.
Manual Roslyn compile of `LockstepStateValidator.cs` against Unity/Bee references: passes with and without `UNITY_EDITOR,DEVELOPMENT_BUILD` defines.
Unity MCP validation could not run after this patch because MCP reports zero active Unity instances.
`dotnet build Hecton8.Core.csproj --no-restore`: still blocked by 92 unrelated assembly/reference errors; the new determinism asmdef is not generated into that project file.

Final status:
Tasks 1-18 remain complete.
Task 19 remains blocked by dependency and current Unity session absence.
Status remains PENDING VERIFICATION.

## 2026-05-13 - Unity Verification Addendum

What was wrong:
Unity MCP initially timed out after a forced script refresh, which made the prior post-hardening validation state stale.
The project still has a compile blocker outside the lockstep validator domain.

What was done:
Retried Unity readiness after timeout.
Added explicit cold-allocation comments to the validator's fixed replay scratch buffers and writer gate.
Re-ran `validate_script` on `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs`.
Cleared and reread the Unity console for current lockstep and global errors.
Confirmed `Library/ScriptAssemblies/Hecton8.Core.Determinism.dll` emission at 2026-05-13 21:03:03.

Cinematic Cheats used:
No change to simulation scope.
Kept deterministic proof isolated to 300-frame sampled replay windows and excluded presentation systems from truth hashing.

Exact microseconds saved:
No runtime change in this addendum.
The existing validator still avoids estimated 65us sampled hash work plus all replay IO on Low/MX350 normal play.

Verification:
`validate_script`: 0 errors, 0 warnings.
Unity console filter for `Lockstep`: 0 entries.
Current global console after clear/recompile: one unrelated syntax error in `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:7393`.
`Hecton8.Core.Determinism.dll`: emitted in `Library/ScriptAssemblies`.

Final status:
Tasks 1-18 complete.
Task 19 remains blocked by dependency because full Unity compile is not clean enough for final Burst/SIMD disassembly/profiler verification.
Status remains PENDING VERIFICATION.

## 2026-05-13 - Replay Corruption Hardening Addendum

What was wrong:
Replay block headers were validated, but individual input frame numbers inside an otherwise valid block were trusted.
A desync paused simulation but left the ghost replay active flag/input override path alive until external state stopped ticking.

What was done:
Added per-frame input number validation while loading `.h8replay` blocks.
Added block sequence validation so stale or reordered blocks are rejected before replay starts.
Added runtime ghost input frame mismatch detection.
Added `StopGhostReplayAfterFault()` to clear ghost replay state and input override after desync without calling the public time-dilation reset path.

Cinematic Cheats used:
Still no continuous state serialization.
Replay corruption is rejected by integer fence checks and the existing 300-frame hash cadence.

Exact microseconds saved:
Normal gameplay: 0us change.
Replay load: one integer compare per recorded input frame.
Replay runtime: one integer compare per replay input frame.
Fault path: clears override state immediately, avoiding additional bad replay inputs after pause.

Verification:
Manual Roslyn compile of `LockstepStateValidator.cs`: pass with and without `UNITY_EDITOR,DEVELOPMENT_BUILD`.
`git diff --check`: clean except existing line-ending warnings.
Unity emitted `Hecton8.Core.Memory.dll` at 2026-05-13 22:21:51.
Unity emitted `Hecton8.Core.Determinism.dll` at 2026-05-13 22:24:19.
Unity MCP `validate_script` and console endpoints timed out after assembly emission, so final Burst/SIMD proof remains blocked.

Final status:
Tasks 1-18 complete.
Task 19 remains blocked by Unity MCP readiness/profiler access after assembly emission.
Status remains PENDING VERIFICATION.
