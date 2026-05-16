# Rationale_COMPASS_GYRO_STABILIZER

Status: IMPLEMENTED; FINAL BUILD BLOCKED BY EXTERNAL DEPENDENCIES

## Decision 001 - Scope And Authority
Problem: Existing compass behavior is a screen-space ribbon installed at runtime and driven from camera orientation, while the task requires a diegetic drifting 64-bit compass.
Solution: Build a domain-owned `Assets/_Project/Scripts/UI/Navigation/` runtime that exposes `IInertialNavigationService`, writes state to `GlobalDataVault`, and presents only through 3D tool references.
Rejected Alternatives: Keeping the screen-space ribbon would preserve the exact defect. Adding a singleton would violate `GlobalRegistry` and multi-agent decoupling. Reading camera eulers would corrupt AUP authority.
Scalability potential: Low uses SlowTick and snapped cardinal text. Middle uses smoothed physical transform rotation. High uses indirect dial rendering. Ultra can add richer glass shader response without increasing gameplay truth cost.
Hardware Impact: MX350/i3 avoids Canvas rebuilds and camera polling; expected saved CPU is roughly 7-20 us/frame versus a live screen-space compass update, with zero GC in the compass path.

## Decision 002 - Mandate Selection
Problem: The compass crosses UI, AUP, vault, signals, telemetry, and performance budgets.
Solution: Read eight mandates: diegetic UI, zero-GC UI streaming, zero-GC policy, AUP determinism, GlobalRegistry DI, signal lanes, telemetry blackbox, and frame/VRAM budgets.
Rejected Alternatives: Reading only UI mandates would miss AUP and signal constraints. Reading every registry file would waste time and increase context noise.
Scalability potential: The selected set maps directly to Low/Middle/High/Ultra compass behavior.
Hardware Impact: Mandate-driven path keeps hot-path work bounded below the 0.1 ms suspicion threshold on i3/MX350.

## Decision 003 - Vault DTO And Buffer IDs
Problem: The prompt requires compass state in `GlobalDataVault`, but no compass buffer IDs or DTO existed.
Solution: Added `CompassStateDTO` and `CompassOutputSlot` to the inertial-navigation contract, and added `CompassState`, `CompassHeadingOutput`, and `CompassBlackBox` to `BufferID` after the current allocation map.
Rejected Alternatives: Using `BufferID.Unknown` or a local `NativeArray` would compile faster but would create hidden ownership and break DataVault sovereignty.
Scalability potential: Low reads only the heading/cardinal slots. High/Ultra can consume drift/glitch/max-drift slots for richer cockpit glass and diagnostics.
Hardware Impact: One DTO, eight floats, and 300 compact blackbox entries are below 16 KB; MX350/i3 cost is dominated by a single IJob and bounded memory writes.

## Decision 004 - Legacy Ribbon Containment
Problem: `ProgressionRuntimeInstaller` spawned `ShaderCompassRibbon`, preserving a perfect screen-space compass.
Solution: Removed installer creation and changed the legacy ribbon to require world-space Canvas plus `IInertialNavigationService` false bearing if it exists in old scenes.
Rejected Alternatives: Deleting the class would risk breaking serialized scenes. Keeping it installed would violate the diegetic UI requirement.
Scalability potential: Low tier avoids the ribbon entirely; high tier gets physical dial rendering through the new navigation runtime.
Hardware Impact: Avoids a live Canvas ribbon and camera transform read. Estimated saving remains 7-20 us/frame plus avoided Canvas dirty work.

## Decision 005 - Burst Drift Kernel
Problem: Compass drift needs deterministic failure near anomalies without adding a simulation-heavy compass model.
Solution: Implemented `GyroDriftJob` as a visual fake: heading catches up with global +Z bearing, then bounded `noise.cnoise` anomaly interference and wild-spin are applied.
Rejected Alternatives: Rigidbody gyroscope, magnetometer simulation, or per-frame managed noise would spend frame time on non-gameplay truth.
Scalability potential: Low runs on SlowTick with snapped text. Middle can update transform rotation. High/Ultra can draw the dial indirectly and amplify glass response.
Hardware Impact: Expected job cost is single-digit microseconds on i3/MX350 because it writes one DTO and eight floats, with zero heap allocation.

## Decision 006 - Signal Lane Ownership
Problem: `GlobalSignals` already validates and initializes compass anomaly/calibration lanes, so defining their payload structs in the UI assembly made core compilation unable to resolve the types.
Solution: Place `AnomalyProximitySignal` and `CompassCalibratedSignal` in the core signal contract namespace inside `GlobalSignals.cs`, then consume them from the UI runtime through `SignalBus<T>` snapshots.
Rejected Alternatives: Keeping UI-owned signal payloads would create a reverse dependency from core to UI. Polling anomaly producers directly would couple UX to world/VFX ownership and break multi-agent execution.
Scalability potential: Low tier gets capped anomaly signal frames. High/Ultra can feed more frequent anomaly interference without changing the compass runtime.
Hardware Impact: Signal reads are contiguous snapshot spans; expected MX350/i3 cost remains under 6 us with no managed event allocation.

## Decision 007 - Diegetic Presentation Ladder
Problem: The prompt needs both toaster-safe output and high-end visual overkill without a screen-space Canvas.
Solution: Low tier snaps a fixed TMP char buffer; middle tier rotates the serialized physical dial pivot; high/ultra can submit one indirect mesh instance and shader-driven glass chromatic aberration.
Rejected Alternatives: A screen HUD was explicitly banned. Runtime dial clones would allocate and complicate scene ownership. Particle failure effects would spend frame time on noise instead of readable instrument failure.
Scalability potential: Low = `--`/N/NE labels and SlowTick. Middle = transform dial. High = indirect dial. Ultra = stronger glass material response from the same SOA output.
Hardware Impact: Low path is text-only and avoids Canvas rebuilds; estimated saving versus a live HUD ribbon is 7-20 us/frame. High path spends GPU-side draw setup only when tier and stress allow it.

## Decision 008 - Black Box And NaN Containment
Problem: A drifting compass can fail silently if heading/AUP math produces NaN or runaway drift.
Solution: Normalize headings with `math.fmod`, guard finite state before committing snapshots, log `MaxGyroDriftDegrees` to a fixed 300-entry vault ring, and dump the ring on non-finite detection.
Rejected Alternatives: `Debug.Log` is not telemetry. Letting NaNs propagate would corrupt cockpit consumers through `IInertialNavigationService`.
Scalability potential: The blackbox cost is fixed across tiers; higher tiers can visualize glitch intensity from the same drift/max-drift data.
Hardware Impact: The ring is about 12 KB and one struct write per completed job; MX350/i3 impact is below 2 us/frame.

## Decision 009 - Homeostasis, Power, Calibration
Problem: The compass must degrade under CPU stress, die with suit power, and recalibrate from a beacon without hard dependencies on those systems.
Solution: Cache `SystemHealthSignal`, `SurvivalVitalsChangedSignal`, and `CompassCalibratedSignal` snapshots; gate cadence/power/reset inside the scheduled compass step.
Rejected Alternatives: Querying survival/base systems from FastTick would violate registry cold-path rules. Keeping the compass alive below 1% would contradict the prompt.
Scalability potential: Low and stressed devices fall back to SlowTick; high-end devices spend saved CPU on dial/glass presentation.
Hardware Impact: No per-frame registry search or managed callback; expected signal scan cost is bounded by configured lane caps.

## Decision 010 - Compile Wall Classification
Problem: `dotnet build` remains red after compass-owned fixes, with failures moving through fauna, docking, wakes, ecosystem, and generated assets.
Solution: Fixed the only compass-owned compile defect (signal payload placement) and stopped at the documented dependency wall after repeated builds.
Rejected Alternatives: Editing docking/autopilot/flora/fauna/ecosystem interfaces from this UX task would be cross-domain sabotage. Claiming build green would be a false report.
Scalability potential: No runtime scalability change; this preserves ownership boundaries for the integrator.
Hardware Impact: None at runtime. Integration risk is external compile order/ownership, not compass frame cost.

## Decision 011 - Multiplatform Data Sovereignty Repair
Problem: The runtime still held private `NativeArray` handles to vault buffers, and compass structs were not all explicitly `Pack = 1`.
Solution: Removed persistent `NativeArray` fields from the MonoBehaviour; buffer access now resolves transient vault views only when scheduling, presenting, committing, or dumping. Changed `CompassStateDTO`, `InertialNavigationSnapshot`, and `CompassBlackBoxEntry` to `Pack = 1`.
Rejected Alternatives: Keeping cached NativeArray fields would look like private system state and fail H-Phi inspection. Moving data into managed lists would break zero-GC and DataVault ownership.
Scalability potential: Low/Middle/High/Ultra all read the same vault data; presentation tier changes do not fork authority.
Hardware Impact: Quest/ARM gets deterministic struct layout and avoids private handle lifetime ambiguity. DataVault lookups replace cached handles; expected overhead is below the 0.1 ms suspicion line and buys sovereignty.

## Decision 012 - Dear Lie Noise Ladder
Problem: Low tier still used coherent noise despite being on SlowTick; that is wasteful on i3/MX350 and mobile.
Solution: Low tier now uses triangle noise. Middle uses one coherent-noise sample. High/Ultra with indirect dial enabled uses two-octave noise plus `_CompassOverkill01` for glass/material response.
Rejected Alternatives: Full magnetometer simulation, raymarched field distortion, or particle-heavy failure on all tiers would waste performance on fake physics. A fixed random jitter would look cheap and unreadable.
Scalability potential: Low = triangle-wave lie. Middle = one noise sample. High = two-octave drift and indirect dial. Ultra can bind `_CompassOverkill01` in material shaders for heavier glass/salt/SSS response without touching navigation truth.
Hardware Impact: Low tier saves the coherent-noise sample during drift, estimated 1-3 us per scheduled compass tick on i3/MX350. High tier spends that saved CPU only when stress is below 0.8.

## Decision 013 - Platform Rendering Guard
Problem: Indirect mesh drawing can be invalid on GLES/mobile paths and should not assume DirectX-style support.
Solution: Gate high-tier indirect dial rendering behind `SystemInfo.supportsInstancing`, `SystemInfo.supportsComputeShaders`, and non-GLES graphics device types.
Rejected Alternatives: Always using `Graphics.DrawMeshInstancedIndirect` would risk Android/GLES failure. Disabling high-tier rendering globally would give 4090 users mobile visuals.
Scalability potential: Toaster/mobile uses text or transform dial. PC/Metal/Vulkan/D3D high tier can use indirect dial and overkill material scalars.
Hardware Impact: Quest/GLES avoids unsupported draw paths. High PC keeps the richer visual path.

## Decision 014 - Validation After Inquisition
Problem: The project still cannot produce a green `dotnet build`, and `Assembly-CSharp.csproj` did not finish within the validation window.
Solution: Re-ran hazard scans and build. The compass scan is clean. Core build now fails on external `Hecton8.Core.Bucketing` / `ModuloSimulationBucketer` errors in `GameBootstrapper`. Assembly-CSharp timed out after 124 seconds.
Rejected Alternatives: Editing scheduler/bucketing ownership from UX Navigation would violate domain boundaries. Reporting perfection would be false.
Scalability potential: None; this is integration state, not compass runtime behavior.
Hardware Impact: None from compass. Build-wall risk remains external.
