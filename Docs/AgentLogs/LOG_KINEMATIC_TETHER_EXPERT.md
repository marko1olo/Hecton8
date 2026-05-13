# LOG_KINEMATIC_TETHER_EXPERT

## 2026-05-13 - Heavy Towing / Verlet Tether Override

What was wrong:
- Towing attach still used a direct manager call path, not a first-party `NativeQueue` signal.
- The prompt demanded a hard audit for `TetherManager.Instance` and Unity Joint usage. No singleton/joint usage existed, but the evidence was not recorded.
- Existing tether runtime already had custom Verlet/GPU/snap/AUP infrastructure, but solver segment count was not hard-pinned to 10/default and 3/low-tier as requested.
- Existing Verlet constraint treated compression and stretch symmetrically through `abs(delta)`, which made slack cable behave like a rod and generated false tension.
- Existing same-frame job scheduling in tether solver used `Schedule().Complete()`, which is forbidden by the zero-GC / frame-time mandate for this local hot path.
- Manager-level blackbox telemetry for `ActiveTethers` and `PeakTension` did not exist.
- Extreme towing did not project a vehicle command limit signal.

What was done:
- Added `Hecton8.Physics.Tethers.Contracts` and `TetherFiredSignal` as an unmanaged contract.
- Added a fixed 16-slot managed sidecar in `TetherSignals` so the NativeQueue carries only IDs/scalars while Unity object references remain out of native memory.
- Converted `HeavyTowWinch.TryAttach` to publish `TetherFiredSignal`; `TetherManager.DrainTetherFiredSignals` consumes and performs the actual attach.
- Added `HeavyTowWinch.TargetLength`, `SetTargetLength`, and `AdjustTargetLength`; `TetherInstance` refreshes rest length from the winch during fixed step.
- Replaced tether job `Schedule().Complete()` calls with direct `Run()` execution on existing persistent NativeArrays.
- Enforced stretch-only Verlet constraints: `stretch = max(0, distance - restLength)`, then `PeakTension = stretch * SpringStiffness`.
- Applied endpoint forces through `PhysicsForceRouter` using equal/opposite ForceMode.Force packets and `MassSub / (MassSub + MassObject)` scaling.
- Added Low/MX350/Unknown = 3 solver segments; Mid/High/Ultra = 10 solver segments.
- Added `TetherManager` 300-frame fixed-size blackbox ring for active tether count and peak tension; fault dump path: `Docs/AgentLogs/Dump_KINEMATIC_TETHER_EXPERT.bin`.
- Added `VehicleCommandSignalFlags.TowLoadLimit` and published a throttle-limit signal for extreme towing load.
- Note: `VehicleCommandSignals.cs` is currently untracked in the dirty worktree but is included by Unity's Bee Core response file; the only change made there for this task is `TowLoadLimit = 1 << 4`.
- Audited Leviathan spine/tentacle IK and FaunaBrain. No tether/winch references found in Leviathan IK files; bio-cable world visuals are separate from this solver.

Cinematic cheats used:
- Slack cable is visual only. Physics authority exists only under positive stretch.
- GPU cable render remains an impostor path from NativeArray/GraphicsBuffer data; no CPU mesh rebuild.
- Low-tier math LOD uses 3 segments. High-tier gets 10 segments and visual stress overdrive instead of deeper physical truth.
- Manager blackbox stores high-level state only: frame, active count, peak tension, flags. No per-node forensic spam unless fault dumps trigger.

Exact microseconds saved / avoided:
- Low-tier 3-segment solver versus default 10/legacy higher point path: estimated 3-8 us saved per active tether at 50 Hz on i3/MX350.
- Stretch-only slack skip: estimated 2-5 us saved per active tether in slack/reel-in frames.
- Removing same-frame `Schedule().Complete()` from local tether solver path: estimated 3-8 us saved per active tether by avoiding worker scheduling overhead and handle churn.
- Keeping GPU buffer/impostor rendering instead of CPU mesh/LineRenderer rebuild: estimated 20-80 us avoided per visible tether.
- Signal sidecar avoids scene/global lookup on attach: estimated 1-2 us saved per attach event.
- Zero-GC hot path: 0 bytes/frame expected for solver allocations after warm setup.

Verification:
- `rg` found no `TetherManager.Instance` in first-party scripts/scenes/prefabs.
- `rg` found no `ConfigurableJoint`, `SpringJoint`, or `HingeJoint` in first-party scripts/scenes/prefabs.
- `rg "Schedule\\(|\\.Complete\\("` is clean for `TetherInstance.cs` and `TetherVerletJobs.cs`.
- Polish scan of touched tether files found no `foreach`, `string.Format`, interpolated strings, `.ToString()`, `math.sqrt`, `math.normalize`, `Vector3.Distance`, or `.magnitude`.
- Unity Roslyn direct compile of `Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Physics.Tethers.Contracts.rsp` succeeded.
- Unity Roslyn direct compile of `Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.rsp` is blocked by unrelated global dependency errors: missing `Hecton8.Audio.Propagation.SoundEmissionSignal`, missing `IGroundRadarService`, `HectonPlayerMovement.cs` missing `NativeArray<>`, `EcosystemDirector` interface mismatch, and `NoOpAudioService` interface mismatch.
- Required `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /nr:false` was run. It is red with 97 errors because the generated IDE project is stale/missing many active asmdefs (`Core.Scheduling`, `Core.Memory.Layout`, `Audio.Propagation`, GPR, navigation, CCD, vehicle contracts). It also cannot see the new tether contract csproj while Unity MCP is offline. This is not the authoritative Unity compile path.
- No tether-specific compile diagnostics remain in the Unity Bee Core compile output; the dotnet IDE project remains dependency-stale.

Status:
- Core tether work: done.
- Build state: pending due global compile dependencies outside assigned domain.
