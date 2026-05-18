# Rationale_SHINOBU_47

Status: POLISH LOOP 21 CODE PATCHED / STATIC PASS / BUILD DEFERRED BY CPU GUARD / FULL BUILD BLOCKED BY UPSTREAM CORE DEPENDENCY

## Pre-Code Mandate Selection
Problem: Exosuit must move as heavy machinery in caves without Rigidbody, Unity joints, or hot-path GC.
Solution: Use custom Burst-compatible kinematic DTOs, SDF sampling mocks, NativeArray-backed state, ring telemetry, and typed unmanaged signal buffers.
Rejected Alternatives: Unity Rigidbody, ConfigurableJoint, Physics.Raycast, coroutines, LINQ, managed events, and mutable ScriptableObjects are rejected because they introduce nondeterminism, allocation risk, or dependency coupling.
Scalability potential: Low uses one sphere SDF sample and long hydraulic latency; Middle adds limited multi-probe correction; High adds richer clamp/haptic/footstep output; Ultra spends saved CPU on denser diagnostics and stronger tactile feedback without changing deterministic authority.
Hardware Impact: Target is under 0.1 ms on i3/MX350 by reducing collision to simple float3 SDF math, keeping state in 64-byte/16-byte aligned structs, and avoiding managed allocation.

## Selected Registry Mandates
- PHYS_Physics_Integrity_Determinism_ForceMode.txt
- PHYS_Kinematic_Interaction_Hands.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- MATH_AUP_Determinism_Sync.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt

## Decision 1 - Recon And Emergency Mock Constants
Problem: The prompt required scanning legacy archive/StreamingAssets binary layouts before initialization, but the exact exosuit mass/hydraulic binary files are absent and `Assets/StreamingAssets` does not exist in the current tree.
Solution: Use `GenerateEmergencyMockExoData()` as a cold initialization path that writes aligned dummy mass, drag, thrust, clamp, and latency constants into DataVault tuning memory. Historical rationale supports cached scalar mass/drag and visual-cheat feedback, not a concrete exosuit binary layout.
Rejected Alternatives: Inventing byte layouts from unrelated archive files, depending on another agent's future terrain/input systems, or adding Unity Rigidbody/Joints as a fallback. Those would create false authority and unstable cave behavior.
Scalability potential: Low uses one central SDF sphere and long hydraulic response; Middle blends limited secondary probes; High increases haptic/acoustic outputs; Ultra adds denser diagnostics and stronger presentation from the same deterministic DTO.
Hardware Impact: Avoids archive/runtime scans and per-frame managed data; estimated low-end gain is 20-80 us per frame versus any Unity joint/collider fallback in tight caves.

## Decision 2 - Single DTO Authority And Ref Access
Problem: Unity joints, colliders, and property-wrapped state would create jitter, CS1612 copies, and uncontrolled solver order in cave corners.
Solution: Store one `ExosuitStateDTO` in DataVault, mutate cold initialization through `VaultBufferHandle<T>.GetElementAsRef`, and run the hot movement solve through a Burst `IJob` over NativeArray views.
Rejected Alternatives: Rigidbody, ConfigurableJoint, Transform-driven collision probes, managed event callbacks, and property accessors. Standard Unity physics is too slow and unstable for SDF cave sticking.
Scalability potential: Low/Middle/High/Ultra all share the same 64-byte state; quality changes only probe count and presentation intensity, not authority.
Hardware Impact: Removes PhysX joint island overhead; estimated i3/MX350 gain is 100-300 us during wall contact scenes.

## Decision 3 - CSV And Editor Authority Order
Problem: Serialized inspector defaults could overwrite live CSV/editor tuning every frame, destroying the human control facade.
Solution: Serialized values seed the DataVault once through emergency mock data. After initialization, DataVault tuning is authoritative and CSV/editor writes persist into the next solver job.
Rejected Alternatives: Rebuilding tuning from MonoBehaviour fields every Tick, ScriptableObject tuning assets, or managed dictionaries for overrides. Those either allocate or break direct unmanaged control.
Scalability potential: Low can cut mass/thrust/probe quality immediately from CSV; Middle/High/Ultra can push visual-overkill clamp/haptic values without code changes.
Hardware Impact: Parser runs only on file timestamp change; hot-frame cost is 0 us when CSV is unchanged.

## Decision 4 - Blind Crush Mock Instead Of Hull Dependency
Problem: Hull Integrity pressure authority is outside SHINOBU_47 domain and may not be present during integration.
Solution: Define `MockCrushDepthSignal` and degrade `HydraulicPressure` by `ExternalPressure01` inside the solver, proving pressure failure without direct dependency.
Rejected Alternatives: Waiting for Agent 20, reading player stats, or calling managed hull services from the physics job. Those would block parallel agents and violate domain boundaries.
Scalability potential: Low uses scalar pressure only; Middle adds haptic groan; High/Ultra can increase dashboard/acoustic presentation using the same DTO.
Hardware Impact: One scalar multiply in Burst; estimated cost below 1 us on MX350-class silicon.

## Decision 5 - Magnetic Clamp And Heavy Motion Cheat
Problem: The mech must feel heavy and stop cave jitter without PhysX, but a pure collision push-out can still scrape along walls or stick at variable clearance.
Solution: Use semi-implicit Euler for thrust/drag, ramp thrust through hydraulic pressure, resolve an analytic cave SDF sphere, and when Grab is held inside clamp range snap to exact radius clearance with zero velocity.
Rejected Alternatives: Unity hands/feet colliders, constraint joints, raycast ledge grabs, or per-limb IK authority. Those are either unstable in voxel corners or too expensive for <0.1 ms.
Scalability potential: Low/Middle use the same clamp; High/Ultra spend extra cycles only on secondary probes and stronger haptics.
Hardware Impact: Replaces multiple collision queries and joint stabilization with scalar SDF math; estimated low-end gain is 150-400 us in narrow caves.

## Decision 6 - Purge As Cinematic Math, Not Fluid Simulation
Problem: Emergency ascent needs to read as violent mechanical ballast release without simulating thermal ballast, bubbles, or fluid masses.
Solution: On first Purge input, halve `CurrentMass`, reverse/boost vertical velocity, emit `SiltExplosionSignal`, and let VFX/audio systems consume typed signals.
Rejected Alternatives: Simulating ballast tanks, particle-fluid coupling, or buoyancy volumes. Those would spend frame time on invisible mechanics instead of controllable presentation.
Scalability potential: Low emits one silt packet; Middle adds debris splash; High/Ultra can turn the same signal into denser silt and acoustic overkill.
Hardware Impact: Solver cost is one branch and a few scalar writes; estimated gain is 200+ us versus any buoyancy volume approach.

## Decision 7 - Continuous Quality And AUP Local Space
Problem: The solver must scale from weak devices to visual overkill and must not jitter at 100 km coordinates.
Solution: Consume `GlobalQualityWeight` as a continuous float; low values use one central sphere SDF sample, while higher values smoothly blend secondary probes. All collision math runs in float local space relative to `MockTerrainSDF.CameraAup`, then commits back to `double3` AUP.
Rejected Alternatives: Binary low/ultra branches, direct double-coordinate integration, or distance-dependent managed strategy objects. Those create quality cliffs, jitter, or allocations.
Scalability potential: Low central sphere; Middle partial secondary blend; High stronger probe influence and haptics; Ultra can increase diagnostics/presentation without changing authority.
Hardware Impact: Low skips six secondary probes; estimated low-end gain is 10-35 us while preserving deterministic clamp behavior.

## Decision 8 - Footstep And Dashboard Data As DTO Outputs
Problem: Audio and cockpit systems need heavy exosuit feedback but cannot own kinematic state or poll managed components.
Solution: Accumulate floor-contact distance inside a DataVault float and emit `AcousticEchoTap` every stride; pack `HydraulicPressure`, depth, frame, and state into a 16-byte `ExoScreenDTO`.
Rejected Alternatives: Animation events, canvas UI, managed audio callbacks, or direct references to Audio/Terminal agents. Those couple domains and allocate under movement.
Scalability potential: Low keeps one stomp tap per stride; Middle/High/Ultra can layer richer audio/diegetic screens from the same native DTOs.
Hardware Impact: One scalar accumulator and one DTO write per frame; estimated overhead below 2 us, avoiding managed event costs.

## Decision 9 - Telemetry Ring And Fault Dump
Problem: Cave clamps can fail silently if NaNs or corner traps are not recorded before the frame state is overwritten.
Solution: Maintain a 300-frame `NativeArray<ExosuitTelemetryEntry>` plus cursor in DataVault; late-frame readback patches solver time and dumps `Dump_EXO_KINEMATICS.bin` on fault.
Rejected Alternatives: Debug.Log spam, managed lists, or relying on Unity crash reports. Those allocate, lose the last states, or omit SDF push-out context.
Scalability potential: Low records the same 300 high-level frames; Middle/High/Ultra can use the timing data to justify richer probes/haptics without changing the ring.
Hardware Impact: One 64-byte write per frame; estimated cost under 2 us and avoids postmortem guesswork.

## Decision 10 - Human Facades Without Hot-Path Ownership
Problem: Designers need to change mass, latency, thrust, and clamp range while the solver keeps DataVault as authority.
Solution: Add `Exosuit Kinematics Tuner` EditorWindow for play-mode DataVault writes, scene gizmos for SDF probe vectors, and a timestamp-gated CSV parser backed by vault scratch bytes.
Rejected Alternatives: ScriptableObjects, managed dictionaries, in-game UI canvases, or polling strings every frame. Those either allocate or move authority out of unmanaged memory.
Scalability potential: Low/Middle/High/Ultra can all be tuned through continuous floats, including `GlobalQualityWeight`; ultra-tier presentation can be dialed without branching code.
Hardware Impact: Hot path cost is 0 us for editor and unchanged CSV; CSV parse cost occurs only after file timestamp changes.

## Decision 11 - Ultra Polish Compile-Wall And Drag Stabilization
Problem: The prior pass still carried standard Unity rot: Burst did not request synchronous deterministic compile, job fields lacked explicit `[NoAlias]`, solver frame values came from `Time.frameCount`, and late-frame readback directly called Tools/World/audio/VFX presentation contracts.
Solution: Add `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`, mark all job NativeArray lanes `[NoAlias]`, derive frame counters from DataVault screen state, gate CSV polling to editor/development builds through deterministic tick delta, replace Euler drag subtraction with analytical damping, and publish only SHINOBU/Core typed signals.
Rejected Alternatives: Keeping `Time.*` for convenience, direct `ToolHapticsRuntime.EnqueueCommand`, `DebrisSpawnSignal`, `MovementAcousticSignal`, `AcousticPingSignal`, or `AbsoluteUniversePosition.FromAbsolutePosition` calls from the kinematics lane. Those create compile-wall pressure and pull presentation/world ownership into physics.
Scalability potential: Low uses single central SDF plus heavier hydraulic damping; Middle blends secondary probes progressively; High uses stronger secondary correction; Ultra adds one midpoint CCD pre-sample and stronger tactile/acoustic signal data while keeping gameplay authority unchanged.
Hardware Impact: NoAlias and dependency cutting reduce Burst alias pessimism and assembly churn. Analytical drag prevents velocity sign flips that would amplify cave jitter; estimated low-end gain is 5-20 us by avoiding corrective oscillation and concrete-domain dispatch work.

## Decision 12 - Local Re-Polish After User Mandate
Problem: The prior report overstated build health and missed smaller mechanical defects: CSV keys only accepted snake_case despite the required human Excel facade, AUP commits were local-float solved but not millimeter-quantized at the final double3 write, the drag helper was analytical in name but did not scale by speed magnitude, and the enforced dump name `Dump_[AgentID].bin` was absent.
Solution: Add label-key hash aliases for editor/Excel CSV rows, quantize local position before AUP commit and state hashing, use speed-aware analytical drag `v / (1 + k * |v| * dt)`, reject editor tuning writes while the vault buffers are locked by a scheduled job, remove the redundant late-frame registry lookup from the Tick scheduling path, dump both `Dump_EXO_KINEMATICS.bin` and `Dump_SHINOBU_47.bin` on solver fault, and add missing Unity `.meta` files for the new exosuit assets.
Rejected Alternatives: Editing upstream `VolcanicUpdraftDirector` to make global compile green, copying stale `Library/ScriptAssemblies/Hecton8.Core.dll` into `Temp/bin`, or declaring pass from a blocked build. Those would hide dependency rot and violate the domain boundary.
Scalability potential: Low remains one central sphere SDF probe with stable drag and millimeter authority; Middle/High blend secondary probes without position drift; Ultra spends bounded extra SDF taps and haptic/acoustic richness, not unbounded physics truth.
Hardware Impact: Speed-aware analytical drag reduces repeated push-out oscillation in cave corners; estimated low-end gain is 5-20 us in scrape/contact frames. CSV label aliases have 0 us unchanged-frame shipping cost because parsing is editor/development gated.

## Compile Wall Note
Problem: Current full project build fails before SHINOBU_47 can be revalidated through project references because `Hecton8.Core` does not compile.
Solution: Recorded the current exact upstream blocker: `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs(700,26): CS0246 WaterlineBreachSignal missing`. Static source shows the signal type exists under `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereContracts.cs`, so this is an assembly-boundary/export problem outside the exosuit lane.
Rejected Alternatives: Cross-domain repair in Core/Atmosphere without assignment, or reverting unrelated agents' edits. This is outside ECHELON 4 PLAYER/KINEMATICS/TOOLS and must go to the owning integrator.
Scalability potential: None for exosuit runtime; this is compile infrastructure hygiene.
Hardware Impact: No runtime impact measured; build blocked means Unity/runtime/profiler evidence remains pending.

## Decision 13 - Admission Lane And Scalar Step Recheck
Problem: The remaining SHINOBU_47 rot was small but real: the low-probe state flag used a direct `< 0.5f` comparison, and the solver job was scheduled through bare `job.Schedule()` without admission-lane telemetry or H8Memory active-job ownership.
Solution: Convert the low-probe flag to `1.0f - math.step(0.5f, quality)` while keeping actual probe influence on Smooth01 curves; schedule `Exosuit6DIntegratorJob` with `TryScheduleAdmitted(JobAdmissionLane.Lane0_Critical)`, register the resulting handle through `H8Memory.RegisterActiveJob(SystemID.Physics)`, and feed elapsed cost back through `ReportAdmittedJobCompleted` after the late-frame completion gate.
Rejected Alternatives: Keeping raw `Schedule()` because the job is small, adding a binary low/high quality branch, completing the job immediately in Tick, or patching the unrelated World compile error from this lane. Bare scheduling hides player-critical work from the Core admission budget; immediate completion would stall the main thread; World-domain repair would violate SHINOBU_47 ownership.
Scalability potential: Low remains central SDF sphere plus slower hydraulics; Middle ramps secondary probes; High keeps stronger clamp/haptic/acoustic presentation; Ultra spends bounded extra SDF taps on midpoint CCD. The `math.step` result is only a state flag; movement authority still breathes through continuous `GlobalQualityWeight` curves.
Hardware Impact: Admission routing adds negligible hot-path overhead but exposes the job to critical-lane budgeting and teardown fencing. Expected practical gain is 0-5 us in contention frames by avoiding unsupervised worker debt; static no-forbidden-token audit confirms no Rigidbody/Joints/Raycast regression.

## Decision 14 - Purge Persistence And Signal Hygiene Recheck
Problem: The purge event looked correct for one frame but had two deterministic-state defects: `PurgeLatched` was not preserved when the button was released, and `SanitizeTuning` forced `CurrentMass` back to `BaseMass`, erasing the heavy emergency mass cheat after the next solve. Late-frame haptic forwarding also trusted finite duration/frequency values and mapped every impact to crush.
Solution: Carry prior `PurgeLatched` into the new state mask before reading input, use the carried mask as the one-shot guard, allow sanitized `CurrentMass` to stay below `BaseMass` while rejecting zero/NaN, clamp haptic amplitude/duration/frequency before both signal lanes, split low-frequency load into `ChannelCrush` and higher-frequency scrape into `ChannelGearScrape`, and record CSV file timestamps after parse attempts even when rows are unsupported.
Rejected Alternatives: Resetting purge on button release, treating ballast mass as a transient VFX flag, adding a cooldown timer, or keeping all haptics as crush. Resetting would permit repeated mass halving; timers are extra mutable authority; all-crush haptics loses the metal-scrape readout and hides signal mistakes from downstream owners.
Scalability potential: Low keeps one purge packet and one finite haptic request; Middle/High can layer scrape/crush presentation from the same DTOs; Ultra can add richer tactile curves downstream without changing kinematic authority.
Hardware Impact: One carried bit and finite clamps are negligible. Avoiding repeated purge activation prevents runaway velocity/mass correction loops in cave tests; expected low-end protection is frame stability rather than a claimed hot-path speedup. CSV timestamp recording removes repeated editor/development file reads after invalid rows; shipping cost remains 0 us.

## Decision 15 - Fixed-Step Authority And Non-Blocking Teardown
Problem: The 6D solver was scheduled from `IUpdatable.Tick(float deltaTime)`, so exosuit authority inherited render/update cadence instead of the dispatcher fixed simulation step. OnDisable also used a forced `JobHandle.Complete()`, which is defensible for safety but still violates the no-arbitrary-blocking rule under the current mandate pressure.
Solution: Move scheduling into `IFixedTickable.FixedTick(float fixedDeltaTime)`, add `IPostFixedTickable` for same-substep non-blocking completion, keep `ILateFrameTickable` as the final readback/signal window, and remove the forced teardown completion path. Disable now unregisters fixed/post-fixed lanes first, attempts non-blocking completion, and keeps the late-frame lane only until the already-scheduled job reaches `IsCompleted` and unlocks DataVault buffers.
Rejected Alternatives: Keeping `IUpdatable` because it was visually smooth, calling `Complete()` during OnDisable, or moving the whole solve to main thread to dodge job lifetime. Variable render cadence breaks rollback math; forced teardown stalls the main thread; main-thread solve spends CPU that should buy haptics/audio/visual overkill.
Scalability potential: Low/Middle/High/Ultra all share deterministic 0.02s dispatcher cadence. Quality still changes probe count and CCD weight, not the time base. This makes future rollback snapshots and telemetry hashes comparable across visual frame rates.
Hardware Impact: Fixed cadence prevents variable-dt jitter amplification and removes teardown stalls. Raw hot-path cost is roughly neutral; practical gain is frame stability and reduced risk of a disable/unload hitch on low-end silicon.

## Decision 16 - Anti-Stuck SDF Skin, Jump Mock, And Dump Hygiene
Problem: The mock Jump bit only raised hydraulic pressure and did not create upward movement unless another axis was active. SDF push-out and clamp used exact radius clearance with no skin or hysteresis, leaving the mech vulnerable to visual texture penetration and clamp threshold chatter. Silt/acoustic SignalBus packets trusted the DataVault lane, and telemetry dumps used a narrow `EXOK` header instead of the global black-box magic/entry-size contract. CSV also retried empty or oversized files and could reject Excel scientific notation.
Solution: Convert Jump into a real upward hydraulic command before input magnitude is evaluated; add continuous quality-scaled SDF skin (`0.04m` low to `0.015m` ultra) to push-out and clamp clearance; keep a small clamp release hysteresis band for previously clamped states; finite-check AUP and intensity before silt/acoustic publishes; treat empty/overflow CSV reads as timestamped attempts and parse `E/e` exponents; write telemetry dumps with `HECTON8\0` v2 magic plus 64-byte entry-size metadata.
Rejected Alternatives: Adding capsule/feet colliders, using Physics casts to prevent clipping, making Jump a separate Rigidbody impulse, or forcing designers to avoid Excel notation. Colliders and casts violate the SDF-only mandate; Rigidbody impulse reintroduces PhysX authority; designer workflow constraints are a tooling failure, not an acceptable runtime simplification.
Scalability potential: Low uses the larger SDF skin to hide coarse one-sphere probing and reduce texture sticking; Middle/High reduce skin as secondary probes improve clearance; Ultra keeps tighter clearance for better wall feel while using CCD and extra probe blend. The same signals remain presentation-owned, so high-tier can spend saved CPU on richer scrape/silt/haptic response.
Hardware Impact: One Jump boolean, two lerped skin scalars, and finite clamps are below measurable hot-path cost. The practical low-end benefit is fewer repeated push-out correction frames in cave corners; expected protection is frame stability rather than a fake microsecond claim. CSV and dump changes are editor/fault paths, not shipping hot-path cost.

## Build Guard Note
Problem: Loop 11 code was ready for no-reference compile, but the machine was under load.
Solution: Followed AGENTS build guard: active `dotnet/csc` processes were absent, but both `Get-Counter` and WMI CPU probes reported 100% load, so no dotnet build was launched.
Rejected Alternatives: Running `dotnet build` anyway to manufacture a report, or editing upstream Core/Atmosphere compile blockers from the exosuit lane.
Scalability potential: None; this is workstation safety and evidence hygiene.
Hardware Impact: Avoided adding build load on an already saturated CPU. Verification remains static-source until CPU drops below the mandated threshold.

## Decision 17 - Loop 12 Context Recovery Static Recheck
Problem: Context compaction invalidates chat memory, and the newest visible state must be rebuilt from disk before any report or further work. The machine was still under heavy compile load, so launching another build would violate the local CPU guard.
Solution: Re-read `Status_SHINOBU_47.md` and `Rationale_SHINOBU_47.md`, re-extracted the full `SHINOBU_47` XML assignment from `CURRENT_BATCH.md`, reran the exosuit-only forbidden-token audit, checked the active compiler process list, and treated the static audit as the only valid Loop 12 verification.
Rejected Alternatives: Trusting the compacted chat summary, reporting stale Loop 10/11 build results as fresh, or running `dotnet build` while CPU was at 100% and Roslyn was already active. Those would contaminate evidence and risk compile-wall thrash.
Scalability potential: No runtime algorithm change. Low/Middle/High/Ultra behavior remains the Loop 11 SDF sphere, quality-scaled skin, clamp hysteresis, fixed-step hydraulic lag, and bounded extra probes.
Hardware Impact: Avoided adding a second compiler workload on a saturated workstation. Static audit confirms no new PhysX/GC/Time/compile-boundary regression in SHINOBU files.

## Decision 18 - Loop 13 Real Mechanics Patch
Problem: The solver still had concrete physical flaws hidden behind clean reports: secondary probes used the full body radius from offset points, inflating the hull and causing hover/jitter; hydraulic pressure lag did not delay direction changes; clamp release could inherit a stale desired velocity; mock crush ignored CSV/editor `CrushDepthMeters`; dump magic wrote the `HECTON8` header bytes reversed; managed tuning could sanitize a zero current mass into a 1kg mech.
Solution: Make secondary probes shell-sized (`radius - probeOffset`) and apply probe push-out even when the center sphere is clear; feed `previousOutput.DesiredVelocity` through a vector `MoveTowards` actuator delay before thrust so direction changes spool; zero delayed desired velocity while clamped or recovering from NaN; route `BuildCrushDepth` through DataVault tuning; correct the little-endian telemetry magic; guard editor/readback reads during job buffer locks; sanitize zero `CurrentMass` back to `BaseMass`.
Rejected Alternatives: Adding a new core `BufferID` for actuator state, introducing limb colliders, adding Rigidbody impulses, or touching Core/Memory headers. Reusing solver output keeps the compile wall intact and preserves the 64-byte state DTO.
Scalability potential: Low still collapses to central SDF and slower actuator rate; Middle/High blend corrected shell probes without over-inflating the hull; Ultra spends bounded CCD/probe work and gets tighter wall clearance without texture sticking.
Hardware Impact: Probe correction reduces repeated false push-out frames in cave corners. Direction actuator delay costs one vector delta and rsqrt, still cheaper than any joint solver and buys heavy-metal feel without extra vault allocation.

## Decision 19 - Raw Blackbox Dump Instead Of Managed Serialization
Problem: The dump magic and metadata were repaired, but the fault exporter still used `BinaryWriter` and wrote each telemetry field manually. That contradicts the black-box mandate's raw `NativeArray` copy model and hides accidental drift in the 64-byte telemetry DTO layout.
Solution: Replace `BinaryWriter` with a `stackalloc` 24-byte little-endian header and write the ring as raw `ReadOnlySpan<byte>` slices over `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry)`. The exporter validates `UnsafeUtility.SizeOf<ExosuitTelemetryEntry>() == 64` and writes cursor-to-end followed by zero-to-cursor, preserving oldest-to-newest order without a managed snapshot.
Rejected Alternatives: Keeping `BinaryWriter` because this is fault-only, allocating a managed `byte[]`, or dumping JSON/text. Fault paths are for forensic truth; managed field serialization adds noise and weakens the layout proof.
Scalability potential: Low/Middle/High/Ultra gameplay is unchanged. The fixed 300-frame ring stays one evidence contract; high-tier diagnostics get raw cache-line records without increasing hot-path memory or authority state.
Hardware Impact: Hot path is unchanged. Fault export avoids 300 field-by-field managed write loops and uses two native span writes; this is crash-time latency and evidence integrity hygiene, not a frame-time speed claim.

## Build Guard Note - Loop 14
Problem: The raw dump patch needs compile validation, but AGENTS forbids dotnet build while CPU is saturated or another compiler is active.
Solution: Checked active processes and CPU before build. The first guard saw active `csc`/`dotnet` processes and 100% CPU; the final guard saw no active compiler but CPU still reported 100%, so no new build was launched.
Rejected Alternatives: Starting another dotnet build to manufacture a pass, or editing the known Core/Atmosphere `WaterlineBreachSignal` blocker from the exosuit lane.
Scalability potential: None; this is workstation safety and evidence hygiene.
Hardware Impact: Avoided piling another Roslyn workload onto a saturated machine. Loop 14 verification remains static-source until the CPU guard allows a compile.

## Decision 20 - Loop 15 Quality And Purge Authority Patch
Problem: The solver treated quality 0.0 as absent fallback in the input/tuning merge, but 0.0 is the valid minimum-survival state. The purge impulse was also clipped by normal cruise `MaxSpeed`, weakening emergency ascent and making the ballast cheat fight its own limiter.
Solution: Use the lower of input and tuning `GlobalQualityWeight` in the hot solver, and raise the temporary speed cap while `PurgeLatched` is set using a quality-scaled `PurgeImpulse` allowance.
Rejected Alternatives: Binary low-end switches, another actuator/purge buffer, or allowing designer cruise speed to silently cancel emergency purge. Those either violate scalability or weaken the core mechanic.
Scalability potential: Low quality now truly collapses probe/actuator cost; high/ultra keep tighter SDF clearance and faster actuator response. Purge remains scalar math, not buoyancy simulation.
Hardware Impact: No meaningful ALU increase. Low-tier now actually sheds math when thermal quality hits zero, and purge no longer creates half-applied vertical motion that can turn into repeated SDF correction.

## Decision 21 - Loop 16 SDF Anti-Jitter And Metal Impact Response
Problem: Midpoint CCD already writes a correction into `localPosition`, but the later push branch could treat that consumed correction as pending push work. Secondary probes also blended penetration depth itself, which could leave the active shell partly inside the SDF wall and create repeated correction frames. Haptic output was linear and too high-frequency for an 8-ton metal hull.
Solution: Split already-applied CCD push from pending center/secondary push. Secondary probe radius now expands continuously with `GlobalQualityWeight`, and any active shell penetration resolves fully for that active radius. Impact haptics now use mass-scaled sqrt response with lower metallic frequencies and quality-scaled richness.
Rejected Alternatives: Disabling CCD, reintroducing colliders/raycasts, or adding another vault buffer for per-limb contacts. Those either bring back tunneling, violate the SDF-only mandate, or expand authority memory for a presentation problem.
Scalability potential: Low still collapses to a central sphere and coarse SDF skin. Middle gradually expands the secondary shell. High/Ultra get full shell correction, bounded midpoint CCD, and richer tactile output without changing the single-state DTO.
Hardware Impact: No new allocation and no new SDF taps. The patch changes scalar ownership of existing pushes and replaces a potential hot-path logarithm with `sqrt`; practical gain is fewer repeated correction frames and less wall jitter, not a fabricated microsecond claim.

## Decision 22 - Loop 17 Residual Clearance And Scrape Energy Loss
Problem: The push-out branch could clear the largest center/secondary penetration and then immediately continue to floor/clamp logic while a different SDF face remained overlapped. In a floor-wall corner that leaves one-frame residual penetration, which is exactly the pattern that becomes visible jitter or texture sticking on the next fixed step.
Solution: Re-sample the SDF after primary/secondary correction and clear any residual overlap before floor/contact/clamp state is computed. Replace the duplicated inward-velocity removal with `ApplyContactVelocityResponse`, which removes inward normal velocity and applies quality-scaled tangential scrape damping based on contact load. Low quality sheds more tangential energy to hide coarse single-sphere contact; high/ultra keep more tangent movement while relying on tighter shell probes.
Rejected Alternatives: Adding a third collision buffer, adding limb contact points, using Unity Physics casts, or leaving residual overlap to be solved next frame. Extra buffers grow authority state; limb/cast approaches violate the SDF-only mandate; next-frame correction is the jitter source.
Scalability potential: Low/MX350 uses central SDF plus stronger scrape damping and larger skin. Middle expands shell probes and reduces damping. High/Ultra get tight residual clearance, bounded CCD, and less tangential loss so the suit still slides when the math is confident.
Hardware Impact: Adds at most one residual SDF sample only after contact and no allocation. Expected gain is reduced repeated correction frames and fewer stuck-in-texture cases; no raw microsecond number is claimed without profiler capture.

## Build Guard Note - Loop 17
Problem: The residual clearance patch needs compile validation, but local CPU load is above the mandated build threshold and another compiler is active.
Solution: Ran the exosuit forbidden-token scan and `git diff --check` for the touched solver/log files. The latest CPU guard reported 100% load with active dotnet/csc compiler processes, so no build was launched.
Rejected Alternatives: Starting dotnet build above the 50% CPU ceiling, or repairing the known Core/Atmosphere `WaterlineBreachSignal` assembly blocker from the exosuit lane.
Scalability potential: None; this is workstation safety and evidence hygiene.
Hardware Impact: Avoided adding compiler load to an already saturated workstation.

## Decision 23 - Loop 18 Solver Hygiene And Deterministic CSV Exponent
Problem: After contact response moved lost-velocity math into `ApplyContactVelocityResponse`, `preCollisionVelocity` became a stale solver local. The CSV parser also used `Math.Pow` for Excel scientific notation, which is cold-path but still unnecessary platform-libm dependency for tuning hydration.
Solution: Remove the stale solver local and replace `Math.Pow` with `Pow10Clamped`, a bounded 0..38 deterministic multiply loop with reciprocal for negative exponents.
Rejected Alternatives: Leaving the unused local as harmless warning noise, or keeping `Math.Pow` because CSV is editor/development gated. Warning noise hides real mechanical rot, and tuning import should not depend on platform pow behavior when a tiny bounded loop is enough.
Scalability potential: Runtime physics tiers are unchanged. Low/Middle/High/Ultra tuning remains continuous and now parses exponent values through the same deterministic code path on every platform.
Hardware Impact: Hot solver cost is unchanged except for removing one dead local. CSV exponent parsing avoids libm pow and allocates nothing; unchanged-frame shipping cost remains 0 us.

## Build Guard Note - Loop 18
Problem: Loop 18 code needs compile validation, but the workstation is still above the mandated build threshold.
Solution: Ran the exosuit forbidden-token scan and `git diff --check` over touched exosuit code. The latest CPU guard reported 83% load with no active dotnet/csc compiler process, so no build was launched.
Rejected Alternatives: Starting another dotnet build under saturated CPU, or editing the known upstream Core/Atmosphere `WaterlineBreachSignal` assembly blocker from this lane.
Scalability potential: None; this is workstation safety and evidence hygiene.
Hardware Impact: Avoided adding compiler load to an already saturated workstation.

## Decision 24 - Loop 19 Wall-Only SDF Clamp Gate
Problem: The SDF magnetic clamp could still accept any nearby surface if the player held Grab. In cave floors, ceilings, and floor-wall corners that makes the suit feel like it is stuck in the texture instead of intentionally braced against a vertical shaft wall.
Solution: Add a continuous `clampWallness = 1 - Smooth01(0.55, 0.88, abs(normal.y))` gate before clamp eligibility, keep the distance hysteresis for previous wall clamp, anchor to a normalized wall normal, and re-sample/clear residual SDF penetration after clamp correction before freezing velocity.
Rejected Alternatives: Unity trigger volumes, Physics casts for wall classification, a binary low/high clamp mode, or letting animation/IK decide whether the surface is usable. Those either violate SDF authority, add PhysX coupling, or move the mechanical truth out of the Burst solver.
Scalability potential: Low keeps the larger SDF skin and central sphere but stops floor/ceiling magnetic anchors. Middle/High/Ultra get the same wallness curve with tighter skin, shell probes, and CCD; presentation can still fake arms reaching to the valid wall normal.
Hardware Impact: Adds one `abs`, one Smooth01 polynomial, and one residual SDF sample only on active clamp correction. Expected benefit is fewer false stuck states in cave floors/ceilings without new allocations or domain dependencies.

## Build Guard Note - Loop 19
Problem: Loop 19 code needs compile validation, but the workstation is still above the mandated build threshold.
Solution: Ran the exosuit forbidden-token scan. The final CPU guard reported 100% load with active dotnet/csc compiler processes, so no build was launched.
Rejected Alternatives: Starting dotnet build above the 50% CPU ceiling, or patching the known upstream Core/Atmosphere `WaterlineBreachSignal` assembly blocker from the exosuit lane.
Scalability potential: None; this is workstation safety and evidence hygiene.
Hardware Impact: Avoided adding compiler load to a saturated workstation.

## Decision 25 - Loop 20 Corner MTV And Clamp Range Contraction
Problem: A vertical wall clamp can be correct and still leave corner jitter if the secondary shell picks only the single largest probe penetration. In wall-floor or wall-ceiling corners, that discards adjacent penetration vectors and can leave a shell face inside the SDF until the next fixed step. The clamp gate also used wallness as a separate boolean while still evaluating raw acquire/release distance, which is less precise than making wallness contract the effective range.
Solution: Accumulate secondary probe penetrations into a bounded MTV vector, use the strongest penetration as fallback and cap, and run one extra secondary residual pass only after actual contact on quality tiers that enabled shell probes. Clamp eligibility now converts wallness into effective acquire/release distances: floors and ceilings shrink toward `-contactRadius`, true walls keep the configured range, and prior clamp release uses the same continuous curve. Clamp residual correction updates the anchor normal and re-samples after the residual clear.
Rejected Alternatives: Limb contact buffers, Unity colliders, SDF raymarches, or a second contact-manifold DTO. Those expand authority state or reintroduce the exact PhysX/joint failure class this lane is eliminating.
Scalability potential: Low remains one central SDF sphere with larger skin and never pays the extra secondary residual pass. Middle gradually enables shell MTV. High and Ultra get tighter corner clearance and better anti-stuck behavior while preserving the same DataVault handles and DTO layout.
Hardware Impact: No allocation and no new persistent memory. The extra six SDF samples occur only on real contact frames with secondary probes enabled; expected gain is fewer repeated correction frames and less texture sticking, not a profiler-backed microsecond claim.

## Build Guard Note - Loop 20
Problem: Loop 20 solver patch needs compile validation, but the workstation remains above the mandated build threshold.
Solution: Ran the exosuit forbidden-token scan and `git diff --check` for the touched solver. WMI reported 100% CPU and no active dotnet/csc process, so no build was launched under the AGENTS 50% CPU ceiling.
Rejected Alternatives: Launching dotnet build under saturated CPU, or touching the known Core/Atmosphere `WaterlineBreachSignal` compile blocker from the exosuit lane.
Scalability potential: None; this is workstation safety and evidence hygiene.
Hardware Impact: Avoided adding compiler load while the host CPU was already saturated.

## Decision 26 - Loop 21 Secondary Probe Normal Hardening
Problem: The bounded multi-contact MTV introduced in the secondary shell path trusted each penetrating SDF sample normal. The current mock SDF returns unit finite normals, but the real terrain sampler may hand over a denormal, zero, or non-finite normal during streaming or sector-edge faults. That would turn a corner fix into a NaN source.
Solution: Normalize every active penetrating probe normal through `NormalizeWithFallback(sample.Normal, strongestNormal)` before it contributes to the MTV or replaces the strongest fallback normal. Non-penetrating probes still return immediately and pay nothing.
Rejected Alternatives: Trusting terrain ownership, clamping the final MTV only, or adding a persistent contact-manifold buffer. Trusting upstream hides a physics failure; final-only clamping still lets bad normals poison strongest-normal fallback; another buffer expands authority memory for a local math guard.
Scalability potential: Low does not run secondary probes. Middle/High/Ultra get the NaN guard only on real penetrating shell probes, keeping weak-device collapse intact and preserving high-tier corner clearance.
Hardware Impact: Adds one safe normalize only for active penetrating shell probes. No allocation, no new DataVault handles, and no sibling dependency.

## Build Guard Note - Loop 21
Problem: Loop 21 solver patch needs compile validation, but the workstation remains above the mandated build threshold.
Solution: Ran static checks. CPU guard remained above the threshold and active dotnet/csc compiler processes were present, so no build was launched under the AGENTS 50% CPU ceiling.
Rejected Alternatives: Launching dotnet build under saturated CPU, or touching the known Core/Atmosphere `WaterlineBreachSignal` compile blocker from the exosuit lane.
Scalability potential: None; this is workstation safety and evidence hygiene.
Hardware Impact: Avoided adding compiler load while the host CPU was saturated.
