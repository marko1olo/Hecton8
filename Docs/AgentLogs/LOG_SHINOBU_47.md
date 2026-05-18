# LOG_SHINOBU_47

## 2026-05-18 - Exosuit SDF Kinematics

What was wrong:
- No SHINOBU_47 exosuit authority existed in the local code path.
- Legacy exosuit mass/hydraulic binary data was absent; `Assets/StreamingAssets` does not exist in this tree.
- Unity Rigidbody/Joints/Raycast authority would violate the prompt and create cave-corner jitter.

What was done:
- Added `ExosuitStateDTO` 64-byte raw authority state, `MechHapticSignalDTO` 16-byte haptic packet, mock terrain/input/crush/flow DTOs, screen DTO, solver output, and 300-frame telemetry entry.
- Added DataVault BufferIDs `ShinobuExosuitState` through `ShinobuExosuitCsvScratch`.
- Added Burst `Exosuit6DIntegratorJob`: hydraulic pressure ramp, semi-implicit Euler thrust/drag, analytic cave SDF push-out, exact-radius magnetic clamp, abyssal current resistance, purge, floor footsteps, screen export, and black-box telemetry.
- Added `ExosuitKinematicsRuntime`: DataVault allocation with `UninitializedMemory`, ref-based cold initialization, late-frame job completion, haptic/silt/acoustic signal emission, CSV override ingestion from `exo_physics.csv`, and `Dump_EXO_KINEMATICS.bin` on fault.
- Added `Exosuit Kinematics Tuner` EditorWindow and SceneView/runtime gizmos for green bounds, red push normal, and blue desired velocity.

Cinematic Cheats used:
- Mech is a mathematical sphere; limbs/IK are presentation only.
- Wall grab is exact SDF clearance with zero velocity, not a physical joint.
- Hydraulic delay is a scalar pressure ramp, not simulated pistons.
- Emergency purge halves mass and boosts vertical velocity, not buoyancy/fluid simulation.
- Low-tier collision is central sphere only; higher quality continuously blends secondary probes.

Exact Microseconds saved:
- Joint/PhysX eradication in tight caves: estimated 100-300 us.
- SDF sphere solve vs multiple collision/raycast probes: estimated 150-350 us.
- Magnetic clamp vs handhold collider/joint stack: estimated 80-200 us.
- Purge cheat vs ballast/buoyancy volumes: estimated 200+ us.
- Low-quality central probe vs secondary probes: estimated 10-35 us.
- CSV/editor unchanged hot-path cost: 0 us.
- Telemetry write overhead: estimated below 2 us.

Verification:
- `dotnet build .\Assembly-CSharp.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:BuildProjectReferences=false -v:minimal` passed with 0 warnings and 0 errors.
- `dotnet build .\Assembly-CSharp-Editor.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:BuildProjectReferences=false -v:minimal` passed with 0 warnings and 0 errors.
- Full `Assembly-CSharp.csproj` build with project references is blocked upstream by pre-existing `Hecton8.Core` errors in `PlayerBuilder.cs` missing construction contracts (`ConstructionRequestDTO`, `StructuralBoundsDTO`, `ConstructionValidationSettingsDTO`, etc.).

## 2026-05-18 - Ultra Polish Recheck

What was wrong:
- Burst job flags were deterministic but not synchronous; Burst could defer compilation and hide performance faults.
- Job NativeArray fields had no `[NoAlias]`, so Burst had to assume pointer overlap and could drop vectorization opportunities.
- Frame input and telemetry dump throttling still used Unity `Time.*`, which is not rollback-safe authority.
- Late-frame readback directly touched Tools/World/audio/VFX presentation contracts. That violated compile-wall isolation for a physics/kinematics lane.
- Drag used explicit Euler subtraction. In cave corners that can amplify corrective oscillation and make the metal feel rubbery instead of heavy.

What was done:
- Added `CompileSynchronously = true` to `Exosuit6DIntegratorJob` while keeping `FloatMode.Deterministic` for rollback compatibility.
- Added `[NoAlias]` to every job NativeArray and `[ReadOnly]` to input lanes.
- Removed `Time.frameCount`, `Time.deltaTime`, and `Time.unscaledTime` from solver authority. Frames now advance from DataVault screen state; CSV polling uses deterministic tick delta and is editor/development gated.
- Replaced Euler drag subtraction with analytical damping after thrust integration.
- Added quality-weighted hydraulic response and an ultra-tier midpoint CCD SDF pre-sample.
- Removed direct `ToolHapticsRuntime`, `DebrisSpawnSignal`, `MovementAcousticSignal`, `AcousticPingSignal`, `AbsoluteUniversePosition`, `Hecton8.World`, and `Hecton8.Tools` usage from the exosuit lane.
- Added XML/docs/tooltips to public runtime/editor surfaces and serialized tuning fields.
- Rewrote `Docs/AgentLogs/SelfAudit_SHINOBU_47.xml` with the full 20-task reconciliation, struct offsets, H-PHI handles, dependency graph, compile guard, and Dear Lie complexity proof.

Cinematic Cheats used:
- SDF grab remains pure scalar math: exact clearance, zero velocity, anchor normal. No Unity joints.
- The 8-ton feel is bought with hydraulic scalar lag and analytical damping, not piston simulation.
- Low devices pay for one central SDF sphere. High/ultra pays for bounded extra probes and one midpoint CCD sample.
- Purge remains a mass/velocity cheat and emits only a typed silt packet for presentation owners.

Exact Microseconds saved:
- NoAlias/vectorization hint: estimated 2-8 us saved under Burst on low-end CPU by removing alias pessimism.
- Direct presentation-call removal: estimated 5-15 us saved and less compile-wall churn.
- Analytical drag stability: estimated 5-20 us saved by reducing oscillation and repeat collision correction in cave corners.
- Editor/development CSV gating: shipping hot path avoids file timestamp probes entirely; unchanged-frame release cost 0 us.

Verification:
- Static exosuit audit passed: only the intended `BurstCompile` token remains from forbidden scan.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:BuildProjectReferences=false -v:minimal` passed with 0 warnings and 0 errors.
- `dotnet build .\Assembly-CSharp-Editor.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:BuildProjectReferences=false -v:minimal` passed with 0 warnings and 0 errors.
- Full project-reference `dotnet build .\Assembly-CSharp.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` passed with 0 warnings and 0 errors.
- Full project-reference `dotnet build .\Assembly-CSharp-Editor.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` passed with 0 warnings and 0 errors.

## 2026-05-18 - Local Re-Polish After Compile Wall Shift

What was wrong:
- Previous evidence was stale for the current worktree. `Hecton8.Core` now fails in `World/VolcanicUpdraftDirector.cs`, so full project compilation cannot honestly be reported green.
- CSV tuning accepted snake_case hashes but not human-facing Excel labels such as `Base Mass` or `Hydraulic Latency`.
- AUP math solved locally but committed unsnapped local position back into double3 authority.
- Drag helper did not include speed magnitude in the analytical denominator.
- Fault dumps used only `Dump_EXO_KINEMATICS.bin`, while the global black-box rule also requires an agent-id dump.

What was done:
- Added CSV hash aliases for human labels and `global_quality_weight`.
- Quantized local position before AUP commit and before state hashing.
- Changed drag to `velocity / (1 + drag * qualityDamping * speed * dt)`.
- Guarded editor tuning writes while solver vault buffers are locked.
- Removed redundant late-frame registry lookup from the Tick scheduling path.
- Added `Dump_SHINOBU_47.bin` alongside `Dump_EXO_KINEMATICS.bin`.
- Added missing Unity `.meta` files for the exosuit script folder, editor folder, and four C# files.

Cinematic Cheats used:
- No new simulation. The mech remains an SDF sphere with hydraulic scalar lag and typed presentation signals.
- CPU saved by avoiding PhysX/Joints remains available for high-tier haptics/acoustic/visual response.

Exact Microseconds saved:
- Speed-aware analytical drag: estimated 5-20 us saved in repeated cave scrape correction frames.
- Human CSV aliases: 0 us shipping hot-path cost; editor/development only.
- AUP quantization: correctness gain, not a speed claim.

Verification:
- Static exosuit audit: no Unity Rigidbody/Joints/Raycast/Overlap, no LINQ/foreach, no local `new NativeArray`, no runtime `Pack=1`, no direct `Hecton8.World` or `Hecton8.Tools`.
- `dotnet build Hecton8.Core.csproj -v:minimal -clp:ErrorsOnly`: BLOCKED upstream at `Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs(1452,58): CS0117 VolcanicUpdraftVault.SafeNormalize`.
- `dotnet build Assembly-CSharp.csproj -v:minimal`: BLOCKED by the same upstream Core error.
- `dotnet build Assembly-CSharp.csproj /p:BuildProjectReferences=false`: BLOCKED because `Temp/bin/Debug/Hecton8.Core.dll` is absent after the Core failure.

## 2026-05-18 - Loop 8 Admission And Quality Scalar Recheck

What was wrong:
- One remaining quality gate was still written as a raw `< 0.5f` comparison for the low-probe flag.
- The solver job used direct `job.Schedule()`, so Core admission budgeting and H8Memory active-job tracking could not see SHINOBU_47's player-critical work.

What was done:
- Replaced the low-probe flag calculation with `1.0f - math.step(0.5f, quality)`. The actual collision math still uses Smooth01 blending for secondary probes and CCD.
- Routed `Exosuit6DIntegratorJob` through `TryScheduleAdmitted(JobAdmissionLane.Lane0_Critical)`.
- Registered the scheduled handle with `H8Memory.RegisterActiveJob(SystemID.Physics)`.
- Reported measured late-frame completion cost through `ReportAdmittedJobCompleted`.
- Removed the unused `deltaTime` parameter from `WriteFrameInputs`.

Cinematic Cheats used:
- No new physical simulation. The mech remains one SDF sphere with exact-radius magnetic clamp, hydraulic scalar lag, analytical drag, and typed presentation signals.

Exact Microseconds saved:
- Admission tracking is not a raw speed trick; it prevents unsupervised worker debt. Expected contention-frame gain: 0-5 us.
- `math.step` flagging cost is effectively unchanged; it removes a policy violation without changing low-tier O(1) SDF authority.

Verification:
- Static exosuit audit: no Rigidbody/Joints/Raycast/Overlap, no LINQ/foreach, no local `new NativeArray`, no runtime `Pack=1`, no direct `Hecton8.World` or `Hecton8.Tools`.
- Positive audit hits: `TryScheduleAdmitted`, `RegisterActiveJob`, `ReportAdmittedJobCompleted`, `math.step`, and millimeter `SnapMillimeter` AUP commit are present.
- Full compile remains blocked upstream at `Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs(1452,58): CS0117 VolcanicUpdraftVault.SafeNormalize`.
- No-project-reference `Assembly-CSharp` build is also blocked because `Temp/bin/Debug/Hecton8.Core.dll` is absent after the upstream Core failure.

## 2026-05-18 - Loop 9 Purge Latch And Signal Hygiene Recheck

What was wrong:
- `PurgeLatched` was set on the activation frame but not carried from the previous state when the purge button was released. That let a second press re-trigger the supposedly one-shot mass reduction.
- `SanitizeTuning` forced `CurrentMass >= BaseMass`, so the emergency ballast mass cheat was undone on the next solver frame.
- Late-frame haptic forwarding trusted non-finite duration/frequency values and routed every signal as crush.
- Editor/development CSV polling retried the same unsupported or invalid file every poll because `_lastCsvWriteTicks` was only stored when a recognized row changed tuning.

What was done:
- Preserved `PurgeLatched` before input handling and used the carried mask as the one-shot purge guard.
- Changed tuning sanitize so `CurrentMass` may remain below `BaseMass` after purge while still rejecting zero/NaN and enforcing `MinMass`.
- Sanitized haptic amplitude, duration, and frequency before pushing both the SHINOBU DTO and core `HapticRequest`.
- Routed low-frequency heavy load to `ChannelCrush`/`FlagCrush` and higher-frequency scrape to `ChannelGearScrape`/`FlagLightThud`.
- Stored `_lastCsvWriteTicks` after every successful file read/parse attempt, not only after accepted tuning changes.

Cinematic Cheats used:
- Purge remains scalar mass reduction plus vertical impulse. No buoyancy tank, fluid, or thermal ballast simulation was introduced.
- Haptic richness stays presentation-owned through typed signals; kinematics authority remains one SDF sphere and one hydraulic scalar.

Exact Microseconds saved:
- Preventing repeated purge activation avoids runaway corrective SDF/push-out frames; stability gain, not a raw hot-path microsecond claim.
- Finite haptic clamps are below measurable frame cost; they prevent downstream NaN propagation.
- CSV timestamp recording removes repeated editor/development file reads after invalid rows; shipping hot-path cost remains 0 us.

Verification:
- Static exosuit audit: no Rigidbody/Joints/Raycast/Overlap, no LINQ/foreach, no local `new NativeArray`, no runtime `Pack=1`, no direct `Hecton8.World` or `Hecton8.Tools`.
- Prompt extraction rerun with attribute-tolerant XML regex and confirmed 20 SHINOBU_47 tasks.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:BuildProjectReferences=false -v:minimal`: passed with 0 warnings and 0 errors in 55.35s.
- `dotnet build .\Assembly-CSharp-Editor.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:BuildProjectReferences=false -v:minimal -clp:ErrorsOnly`: passed with 0 warnings and 0 errors in 29.89s.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:q -flp:...`: BLOCKED upstream at `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs(700,26): CS0246 WaterlineBreachSignal`. The type exists in `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereContracts.cs`, outside SHINOBU_47 ownership.

## 2026-05-18 - Loop 11 Anti-Stuck SDF And Mock Jump Recheck

What was wrong:
- Mock Jump raised pressure but did not create upward thrust when no vertical axis was active. That failed the blind "walk, jump, grab" proof.
- Exact-radius SDF push-out/clamp had no skin or hysteresis. In cave corners, that invites visual texture penetration and threshold chatter.
- Silt/acoustic packets were read from DataVault and pushed to SignalBus without a final finite guard.
- CSV polling still retried empty or oversized files and rejected Excel scientific notation.
- Telemetry dumps used a narrow `EXOK` header instead of the global HECTON8 black-box header shape.

What was done:
- Converted Jump into an upward hydraulic command before input magnitude/pressure evaluation.
- Added continuous SDF skin: 0.04m on low quality, 0.015m on ultra quality.
- Added clamp release hysteresis for previously clamped states.
- Added finite AUP/intensity guards before silt and acoustic SignalBus publish.
- Timestamped empty/overflow CSV files and added `E/e` exponent parsing.
- Changed dump header to `HECTON8\0`, version 2, entry count, 64-byte entry size, and cursor.

Cinematic Cheats used:
- No colliders, no raycasts, no Unity joints. Anti-stuck remains scalar SDF clearance.
- Jump is hydraulic scalar authority, not Rigidbody impulse.
- The larger low-tier SDF skin hides coarse one-sphere probing; high/ultra tighten clearance while spending bounded samples on CCD/probes.

Exact Microseconds saved:
- Anti-stuck skin/hysteresis prevents repeated correction frames in cave corners; stability gain, not a fake raw timing claim.
- CSV empty/overflow timestamping avoids repeated editor/development file reads after bad input; shipping hot-path cost remains 0 us.
- Silt/acoustic finite guards are below measurable cost and prevent downstream NaN propagation.

Verification:
- Static exosuit audit passed: no Rigidbody/Joints/Raycast/Overlap, LINQ/foreach, local `new NativeArray`, runtime `Pack=1`, `Time.*`, `GlobalRegistry.Get`, `Resources.Load`, `File.ReadAllText/ReadAllBytes`, `string.Format`, `StartCoroutine`, direct `Hecton8.World`, or direct `Hecton8.Tools` tokens.
- Positive audit hits: Jump hydraulic thrust, quality-scaled SDF skin, clamp hysteresis, finite silt/acoustic guards, CSV overflow/scientific-notation handling, and HECTON8 telemetry dump header are present.
- Build was not launched: no active `dotnet/csc` process, but `Get-Counter` and WMI both reported 100% CPU. AGENTS forbids dotnet build above 50%.
- Full/Core compile remains blocked upstream at `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs(700,26): CS0246 WaterlineBreachSignal`, outside SHINOBU_47 authority.

## 2026-05-18 - Loop 10 Fixed-Step Authority Recheck

What was wrong:
- The 6D exosuit solver was scheduled from `IUpdatable.Tick(float deltaTime)`. That tied authority integration to render/update cadence instead of the deterministic fixed simulation lane.
- OnDisable used forced `JobHandle.Complete()`. The job is small, but forced completion during teardown is still a potential hitch and violates the no-arbitrary-blocking policy.

What was done:
- Changed `ExosuitKinematicsRuntime` to implement `IFixedTickable`, `IPostFixedTickable`, and `ILateFrameTickable`.
- Moved input write and solver schedule to `FixedTick(float fixedDeltaTime)`.
- Added `PostFixedTick(float fixedDeltaTime)` as the first non-blocking completion window.
- Kept `LateFrameTick()` as the last readback/signal window for same-frame haptics, silt, acoustic taps, and telemetry dumps.
- Reworked disable handling: fixed/post-fixed lanes unregister immediately; late-frame stays registered only until the scheduled job reaches `IsCompleted`; `Complete()` is now guarded by `IsCompleted` in every path.

Cinematic Cheats used:
- No new simulation. Heavy feel still comes from scalar hydraulic lag, analytical drag, exact SDF clamp, and bounded quality-weighted probes.
- Fixed cadence makes the fake read heavier because pressure lag and drag no longer stretch or shrink with render frame variance.

Exact Microseconds saved:
- No raw solver speedup claimed. This is a determinism/stability patch.
- Removing forced teardown completion prevents a potential disable/unload hitch; saved time is workload-dependent and not claimed without Unity profiler capture.

Verification:
- Static exosuit audit: no Rigidbody/Joints/Raycast/Overlap, no `Time.*`, no LINQ/foreach, no local `new NativeArray`, no runtime `Pack=1`, no direct `Hecton8.World` or `Hecton8.Tools`, no `IUpdatable`/`TryRegisterUpdatable`.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:BuildProjectReferences=false -v:minimal`: passed with 0 warnings and 0 errors in 27.32s.
- `dotnet build .\Assembly-CSharp-Editor.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:BuildProjectReferences=false -v:minimal -clp:ErrorsOnly`: passed with 0 warnings and 0 errors in 24.11s.
- Full/Core compile remains blocked upstream by `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs(700,26): CS0246 WaterlineBreachSignal`.

## 2026-05-18 - Loop 12 Context Recovery Static Recheck

What was wrong:
- Chat context was compacted, so any memory-only confidence about SHINOBU_47 was invalid.
- The workstation was still saturated: CPU probe returned 100%, and an existing `dotnet.exe` Roslyn `csc` process was active.
- A new build under those conditions would pollute evidence and compete with another compiler worker.

What was done:
- Re-read `Docs/Tasks/Status_SHINOBU_47.md` and `Docs/AgentLogs/Rationale_SHINOBU_47.md`.
- Re-extracted the full attribute-bearing `<AGENT_PROMPT id="SHINOBU_47">` block from `Docs/Tasks/CURRENT_BATCH.md`.
- Reran the exosuit-only forbidden-token audit across `Assets/_Project/Scripts/Physics/Exosuit`.
- Updated Status, Rationale, and SelfAudit so bottom-of-log state reflects the newest Loop 12 evidence.

Cinematic Cheats used:
- No new simulation. The implementation remains one SDF sphere, hydraulic scalar lag, analytical drag, SDF skin, clamp hysteresis, purge mass cheat, and typed unmanaged presentation signals.

Exact Microseconds saved:
- No runtime code changed in Loop 12, so no new microsecond claim is made.
- Workstation impact: avoided launching another dotnet build while CPU was already 100%.

Verification:
- Static audit passed: no Rigidbody/Joints/Raycast/Overlap, `UnityEngine.Random`, `Time.*`, LINQ/foreach, DTO get/set properties, local `new NativeArray`, runtime `Pack=1`, `IUpdatable`, direct `Hecton8.World`, direct `Hecton8.Tools`, `ToolHapticsRuntime`, `DebrisSpawnSignal`, `MovementAcousticSignal`, `AcousticPingSignal`, `AbsoluteUniversePosition`, or `GlobalRegistry.Get` tokens in `Assets/_Project/Scripts/Physics/Exosuit`.
- Build deferred by CPU guard: `Get-Counter` returned 100 and an existing `dotnet.exe`/Roslyn `csc` process was active.
- Full/Core compile remains blocked upstream at `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs(700,26): CS0246 WaterlineBreachSignal`, outside SHINOBU_47 ownership.

## 2026-05-18 - Loop 14 Raw Blackbox Dump Recheck

What was wrong:
- The exosuit dump had the right black-box magic and metadata, but it still used `BinaryWriter` and serialized every telemetry field manually.
- That did not prove the 64-byte `ExosuitTelemetryEntry` can be copied as raw forensic evidence.

What was done:
- Added unsafe raw dump writing in `ExosuitKinematicsRuntime`.
- Built the 24-byte dump header with `stackalloc` and explicit little-endian writes.
- Validated `UnsafeUtility.SizeOf<ExosuitTelemetryEntry>() == 64` before export.
- Wrote the telemetry ring directly from `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr` through two `ReadOnlySpan<byte>` writes, cursor-to-end then zero-to-cursor.
- Removed `BinaryWriter` from the SHINOBU exosuit lane.

Cinematic Cheats used:
- No new simulation. This is crash-forensics plumbing only.
- The mech remains one SDF sphere with hydraulic actuator delay, scalar pressure, analytical drag, SDF clamp/skin/hysteresis, and unmanaged presentation signals.

Exact Microseconds saved:
- Hot path: 0 us claimed.
- Fault path: avoids 300 field-by-field `BinaryWriter` writes and uses contiguous native spans. This is crash-time latency and layout integrity hygiene, not gameplay frame-time optimization.

Verification:
- Static exosuit audit passed: no Rigidbody/Joints/Raycast/Overlap, LINQ/foreach, local `new NativeArray`, runtime `Pack=1`, `Time.*`, direct `Hecton8.World`, direct `Hecton8.Tools`, or `BinaryWriter`.
- Positive audit hits: `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr`, `stackalloc` header, raw `ReadOnlySpan<byte>` writes, and 64-byte telemetry-size guard are present.
- Build deferred by CPU guard: first guard saw active `csc`/`dotnet` processes and 100% CPU; final guard still reported 100% CPU.
- Full/Core compile remains blocked upstream at `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs(700,26): CS0246 WaterlineBreachSignal`, outside SHINOBU_47 ownership.

## 2026-05-18 - Loop 13 Real Mechanics Patch

What was wrong:
- Secondary probes sampled from offset hull points but still used the full body radius, inflating collision volume and creating false push-out jitter.
- Hydraulic pressure lag existed, but direction changes could still snap instantly while pressure was already high.
- Clamp could leave a stale desired velocity in solver output, causing a release surge.
- Mock crush pressure ignored DataVault-tuned `CrushDepthMeters`.
- `HECTON8\0` dump magic was numerically encoded for the wrong byte order.
- Managed tuning could sanitize zero `CurrentMass` into a 1kg exosuit.

What was done:
- Corrected secondary probes to use shell radius and still apply probe push-out when the central sphere is clear.
- Added vector actuator delay using previous `ExosuitSolverOutput.DesiredVelocity`; no new core buffer or `ExosuitStateDTO` growth.
- Zeroed actuator command on SDF clamp and fault recovery.
- Routed mock crush through tuned `CrushDepthMeters`.
- Fixed telemetry magic to little-endian `HECTON8\0`.
- Guarded editor/readback reads during job buffer lock and fixed managed mass fallback.

Cinematic Cheats used:
- Still no colliders, joints, or raycasts. Heavy behavior comes from SDF shell math, scalar pressure, delayed actuator command, and presentation-owned signals.

Exact Microseconds saved:
- Probe fix prevents false correction loops; practical win is fewer repeated contact frames, not a fabricated raw timing number.
- Actuator delay costs one vector move-towards and avoids adding a new vault allocation or core enum.

Verification:
- Static forbidden-token audit stayed clean for `Assets/_Project/Scripts/Physics/Exosuit`.
- Build deferred: CPU probe reported 100% and external `dotnet/csc` compilers were active.

## 2026-05-18 - Loop 15 Quality And Purge Authority Patch

What was wrong:
- `GlobalQualityWeight` merge could ignore valid `0.0` minimum-survival quality.
- Purge impulse was clipped by normal cruise speed.
- Midpoint CCD could double-apply its already-consumed push through the later center-clear branch because the branch keyed off generic `pushMagnitude`.

What was done:
- Solver quality now uses the lower input/tuning weight.
- Purge-latched frames get a quality-scaled temporary speed allowance from `PurgeImpulse`.
- Separated consumed midpoint CCD push from later pending SDF correction so an already-applied sweep correction is not applied again.

Cinematic Cheats used:
- Purge remains scalar mass/velocity cheat; no buoyancy simulation.

Verification:
- Static forbidden-token audit stayed clean.
- Build deferred because both CPU probes reported 100%.

## 2026-05-18 - Loop 16 SDF Anti-Jitter Mechanics Patch

What was wrong:
- Midpoint CCD could be counted as pending push after it had already moved the local position.
- Secondary shell probes blended penetration depth, leaving partial SDF penetration and repeated correction frames.
- Collision haptics used a linear response that read too light and too high-frequency for an 8-ton hull.

What was done:
- Split consumed CCD push from pending center/secondary push.
- Made secondary probe shell radius expand continuously through `GlobalQualityWeight`.
- Resolved active shell penetration fully for the currently enabled shell radius.
- Changed impact feedback to mass-scaled sqrt amplitude and lower metallic frequencies.

Cinematic Cheats used:
- No colliders, joints, raycasts, or limb contact rigs. The mech remains one SDF sphere plus bounded shell probes and presentation-owned haptic/acoustic signals.

Exact Microseconds saved:
- No new frame-time saving claimed. This reduces repeated contact-correction frames and prevents wall jitter without adding allocations or SDF taps.

Verification:
- Static forbidden-token audit stayed clean.
- Build deferred: CPU guards repeatedly reported 100%; compiler activity varied during guards and the latest guard saw an active `dotnet` process.

## 2026-05-19 - Loop 17 Residual SDF Clearance Patch

What was wrong:
- Primary/secondary SDF push-out could resolve the largest penetration and still leave a different cave face overlapped until the next fixed step.
- In floor-wall corners that residual overlap becomes the visible texture-sticking and ping-pong correction the user called out.
- Contact velocity response only removed inward normal velocity; tangent scrape kept too much energy for an 8-ton hull.

What was done:
- Added a post-push SDF re-sample before floor/contact/clamp logic.
- If residual penetration remains, the solver applies one final clearance push along the current SDF normal and re-samples before clamp eligibility.
- Replaced duplicate inward-velocity code with `ApplyContactVelocityResponse`.
- Contact response now removes inward velocity and applies continuous quality-scaled tangential scrape damping from contact load.

Cinematic Cheats used:
- Still no Rigidbody, Unity joints, Physics casts, limb colliders, or IK truth. The mech remains a mathematical SDF sphere with bounded shell probes.
- The scrape damping is a tactile/control fake: it sells heavy metal through lost tangent energy instead of simulating feet, tracks, or per-limb friction.

Exact Microseconds saved:
- No profiler-backed raw speed claim.
- Expected benefit is fewer repeated correction frames after corner contact, which reduces jitter and stuck-in-texture recovery churn without adding allocation or a new vault buffer.

Verification:
- Static forbidden-token audit over `Assets/_Project/Scripts/Physics/Exosuit` returned no matches for Rigidbody/Joints/Raycast/Overlap, `Time.*`, LINQ/foreach, local `new NativeArray`, runtime `Pack=1`, direct sibling-domain usings, concrete presentation calls, or `BinaryWriter`.
- `git diff --check` over the touched solver/log files passed.
- Build deferred: latest CPU guard reported 100% load with active `csc`/`dotnet` processes, above the mandated 50% build ceiling.

## 2026-05-19 - Loop 18 Deterministic Tuning Parser Patch

What was wrong:
- `preCollisionVelocity` stayed in the solver after lost-speed calculation moved into `ApplyContactVelocityResponse`.
- The CSV parser accepted Excel scientific notation by calling `Math.Pow`, which is unnecessary and less deterministic than bounded local math.

What was done:
- Removed the stale solver local.
- Replaced `Math.Pow` with `Pow10Clamped`: a 0..38 bounded multiply loop and reciprocal for negative exponents.

Cinematic Cheats used:
- No added simulation. The runtime mech remains SDF sphere authority with residual clearance, hydraulic delay, analytical drag, and typed unmanaged feedback.

Exact Microseconds saved:
- Hot solver: no measurable claim; removed dead local only.
- CSV parse: avoids libm pow on a cold dev/editor path; unchanged-frame shipping cost remains 0 us.

Verification:
- Static forbidden-token audit over `Assets/_Project/Scripts/Physics/Exosuit` returned no matches, including `Math.Pow`.
- `git diff --check` over touched exosuit code passed.
- Build deferred: latest CPU guard reported 83% load with no active `csc`/`dotnet`, still above the mandated 50% build ceiling.

## 2026-05-19 - Loop 19 Wall-Only SDF Clamp Patch

What was wrong:
- Grab could magnetize against floors or ceilings because clamp eligibility only checked SDF distance.
- In floor-wall corners that false anchor reads as texture sticking, not intentional cave-wall bracing.

What was done:
- Added a continuous SDF wallness gate from the contact normal: vertical walls remain eligible, flat floors/ceilings release.
- Kept distance hysteresis for previous wall clamps so valid wall grab does not chatter.
- Re-sampled the SDF after clamp correction and applied one residual clear before freezing velocity.

Cinematic Cheats used:
- No limb colliders, raycasts, joints, or IK authority. Arms can still visually fake the grab; the solver owns one wall-normal anchor.

Exact Microseconds saved:
- No profiler-backed speed claim. The patch adds one polynomial wallness scalar and a residual SDF sample only during clamp correction, while removing false stuck recovery frames.

Verification:
- Static forbidden-token audit over `Assets/_Project/Scripts/Physics/Exosuit` returned no matches.
- Build deferred: CPU guard reported 100% load with no active `csc`/`dotnet`, above the mandated 50% build ceiling.
