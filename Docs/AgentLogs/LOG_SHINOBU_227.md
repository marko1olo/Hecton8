# LOG_SHINOBU_227

## 2026-05-20 SHINOBU_227 Seaglide Hydrodynamics Reviser

What was wrong:
- Player Seaglide-equivalent runtime was `MantaScooter`, not `Assets/_Project/Scripts/Equipment`; Equipment directory is absent in this branch.
- `MantaScooter` still carried the legacy transport-force surface and previously depended on local Rigidbody velocity reads for movement/presentation.
- No dedicated Seaglide Burst hydrodynamics pipeline existed for thrust, drag, current force, metabolism, rollback-excluded presentation signals, or black-box crash telemetry.

What was done:
- Removed Seaglide Rigidbody ownership from `MantaScooter`; it now submits AUP propulsion requests through `SeaglideHydrodynamicsRuntime`.
- Added explicit-layout DTO contracts for state, request, force packet, flow sample, tuning, counters, telemetry, body binding, visual, audio, and cavitation signals.
- Added Burst jobs for deterministic thrust, linear/quadratic water drag, abyssal current advection, battery metabolism, audio speed parameters, 1000-record mock generation, and telemetry reduction.
- Added `PhysicsApplySystem.SeaglideQueue` as the only bridge that resolves Rigidbody targets and queues force packets through central physics authority.
- Added `GlobalDataVault` BufferID registrations for SHINOBU Seaglide buffers.
- Added 300-frame telemetry ring and NaN/fault dump target `Docs/AgentLogs/Dump_SHINOBU_227.bin`.
- Added editor-only X-Ray window, current-force gizmo, static Rigidbody scanner, layout trap guard, and `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json`.
- Added architecture note `Docs/ARCHITECTURE/SEAGLIDE_HYDRODYNAMICS_SHINOBU_227.md`.

Cinematic cheats used:
- Low-quality hydrodynamic speed uses dominant-axis approximation instead of full magnitude.
- Flow-field fallback uses deterministic triangle-wave current instead of fluid simulation.
- Battery metabolism is a linear load drain at quality-scaled cadence, not RPM/joule simulation.
- Cavitation is a rollback-excluded VFX/audio signal derived from speed and throttle, not particle physics.

Verification:
- `CURRENT_BATCH.md` XML block was extracted by ID `SHINOBU_227`; task count was 20.
- `git diff --check` on touched files reported no whitespace errors; only LF-to-CRLF warning for `SeaglideHydrodynamicsJobs.cs`.
- Static grep found no `_playerRigidbody`, `MissingRigidbody`, or `Rigidbody` references in `MantaScooter.cs`.
- Static grep found no runtime `float.Parse`, `.Split(`, `Rigidbody.AddForce`, `AddRelativeForce`, or `FixedUpdate` in Seaglide runtime/Manta paths. Only editor scanner string literals matched `AddRelativeForce` and `FixedUpdate`.
- Compile was not launched. CPU gate returned 100 percent three times, and project rule forbids `dotnet build` over 50 percent CPU. No `dotnet`/`csc` process was running.

Exact microseconds saved:
- Certified measured savings: 0 us. No profiler run and no compile run were allowed under the CPU gate.
- Static expected savings, not claimed as measured: one Manta Rigidbody velocity read and one legacy transport force path removed per active tool tick; hot hydrodynamic work moved to contiguous Burst DTO jobs.

## 2026-05-20 SHINOBU_227 Ultra Polish Pass

What was wrong:
- Static pass still carried avoidable hydrodynamic waste: force packets were cleared even though packet count is authoritative.
- Low-quality mode blended math cost but did not yet reduce thrust solve frequency.
- Black-box telemetry did not include the last flow vector/battery scalar or budget-overrun fault bit.
- Editor X-Ray graph allocated a vector array during repaint.

What was done:
- Removed per-solve packet buffer clear in `PhysicsApplySystem.SeaglideQueue`.
- Added continuous thrust cadence accumulator in `SeaglideHydrodynamicsRuntime`; low quality trends toward 20 Hz while high quality tracks fixed tick.
- Added force integration scaling in `CalculateSeaglideThrustJob` so a lower solve cadence preserves approximate impulse.
- Added `FlagBudgetExceeded`, flow/battery telemetry fields, and budget-triggered dump checks.
- Moved X-Ray graph scratch allocation to editor cold path and replaced legacy span `IndexOf` with a manual byte scan.

Cinematic cheats used:
- Cadence collapse is a Dear Lie: fewer physical force solves under thermal pressure, compensated by scaled force packets.
- Flow remains deterministic triangle-current fallback or first-eight trilinear sample, never CPU fluid simulation.
- Battery remains linear metabolism, not a mechanical motor/RPM model.

Verification:
- Static grep found no Seaglide/Manta `LastNetForce`, `Time.deltaTime`, runtime `DontDestroyOnLoad`, runtime `new GameObject`, old `IndexOf(Comma)`, or per-repaint graph array.
- Static grep found no `Rigidbody`, `FixedUpdate`, `AddForce`, `AddRelativeForce`, `_playerRigidbody`, or `MissingRigidbody` in `MantaScooter.cs`.
- Static hot-path grep found no `.Complete()`, private native collections, LINQ, or `foreach` in Seaglide runtime/jobs/queue.
- Burst hydrodynamic jobs remain `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`.
- CPU gate still reports 100 percent; no `dotnet`/`csc` process is running. Build remains blocked by protocol.

Exact microseconds saved:
- Certified measured savings: 0 us. Compile/profiler still blocked.
- Static expected savings, not measured: up to 131072 bytes of force-packet clear writes removed per scheduled solve, plus fewer low-quality hydrodynamic solves from cadence collapse.

## 2026-05-20 SHINOBU_227 SignalBus Closure Pass

What was wrong:
- Audio and cavitation were computed as Vault DTOs but not yet bridged into the existing typed SignalBus lanes.
- `SeaglideAudioSignalDTO` lacked target hash/frame fields, forcing any DSP bridge to infer ownership.

What was done:
- Added target hash and frame index to the 64-byte audio DTO without changing stride.
- Added cold lane warmup for `ToolAcousticSignal` and `BubbleSpawnSignal`.
- Added bounded post-solver publication of acoustic propeller packets and cavitation bubble packets from job-produced DTOs.

Cinematic cheats used:
- Bubble VFX is a scalar `BubbleSpawnSignal`, not particle prefab spawning.
- Signal publish budget is quality-weighted from 1 to 4 packets, not a full 1024-row visual pass.

Verification:
- Static grep found no `Rigidbody`, `FixedUpdate`, `AddForce`, `AddRelativeForce`, `_playerRigidbody`, `MissingRigidbody`, or `Time.deltaTime` in `MantaScooter.cs`.
- Static grep found no hot-path `.Complete()`, private native collections, LINQ, or `foreach` in Seaglide runtime/jobs/queue.
- Static grep found no `ParticleSystem` or `Instantiate(` in Seaglide runtime/Manta paths.
- CPU gate briefly cleared to 26 percent and no `dotnet`/`csc` process was running; generated csproj does not yet include new Seaglide files, so dotnet build would not verify this kernel. CPU returned to 100 percent before any safe compile launch.

Exact microseconds saved:
- Certified measured savings: 0 us.
- Static expected saving: no prefab allocation/destruction path for propeller wash; bounded 1-4 signal publish cost instead of object churn.
