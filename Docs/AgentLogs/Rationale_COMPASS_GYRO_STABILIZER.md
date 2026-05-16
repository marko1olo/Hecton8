# Rationale_COMPASS_GYRO_STABILIZER

Status: IMPLEMENTED CODE + COLD PHYSICAL BINDING BRIDGE + GPU BANDWIDTH REPAIR + VAULT STATE EVICTION + PRESENTATION VAULT EVICTION + INDIRECT SUBMISSION REPAIR + STARTUP/HOT-POLL DEPTH REPAIR; PREFAB/SCENE BINDING NOT SERIALIZED; FINAL BUILD BLOCKED BY EXTERNAL DEPENDENCIES

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

## Decision 015 - Hot-Path Dependency And Physical Binding Repair
Problem: The runtime still attempted cold dependency recovery from `SlowTick()`, and a GUID scan found no serialized prefab/scene reference to `DiegeticGyroCompassRuntime`. That meant the code path was cleaner than the installation path.
Solution: Removed the `SlowTick()` registry fallback; gameplay ticks now use cached `_playerContext`/`_vault` or return. Added `InjectDependencies(...)` for bootstrap-owned dependency injection and `ConfigurePhysicalBinding(...)` for cold physical tool binding. Added explicit struct sizes for ARM64 audit: blackbox 40 bytes, `CompassStateDTO` 136 bytes at that pass, and `InertialNavigationSnapshot` 120 bytes. Decision 021 expands current `CompassStateDTO` to 176 bytes.
Rejected Alternatives: Polling `GlobalRegistry` until dependencies appear would hide initialization debt in a hot path. Raw-editing `Player.prefab` YAML without Unity API readback would risk prefab corruption. Creating a screen fallback would violate the prompt.
Scalability potential: Low/MX350 still takes the cheapest text/SlowTick path. Middle rotates a bound physical dial. High/Ultra can bind the indirect mesh and optional local anomaly particles without increasing gameplay authority cost. Prefab/scene binding still needs Unity-side serialization proof before `VERIFIED MASTER GRADE`.
Hardware Impact: Removing SlowTick registry lookup saves a small but recurring cold-spine read on stressed/low hardware, estimated 1-4 us per SlowTick. Explicit sizes reduce ARM64/Quest layout ambiguity. Physical binding API has no hot-path cost.

## Decision 016 - High-Tier Failure VFX Spend
Problem: `_CompassOverkill01` exposed a shader scalar but did not buy any concrete local visual effect when a physical tool binds no custom shader.
Solution: Added an optional High/Ultra-only `ParticleSystem` emitter for local salt/static compass-glass bursts, emitted only when anomaly interference is above 0.8, power is alive, tier is High/Ultra, and system stress is below 0.8. The authored burst value is hard-clamped to 128 particles per late-frame pass.
Rejected Alternatives: Standard always-on particles would punish MX350 and mobile. Compute silt/visor/hull effects belong to VFX/Visor/Hull domains, not UX Navigation. Full magnetometer physics would spend CPU on fake truth.
Scalability potential: Low = no particles and triangle noise. Middle = no particles, physical dial if bound. High = indirect dial plus local burst. Ultra = same gameplay truth with higher authored particle budgets on the bound emitter.
Hardware Impact: MX350/Quest path is zero because the emitter gate returns before emission. High/Ultra can spend roughly 0-20 us/frame when the optional emitter is assigned and anomaly is saturated; this is deliberate visual currency, not authority cost.

## Decision 017 - Validation After Loop 8
Problem: New layout/binding/VFX repairs needed fresh proof, but the project build wall moved again.
Solution: Re-ran domain forbidden-pattern scan, duplicate compass-signal scan, serialized GUID scan, struct-layout scan, `Hecton8.Core.csproj`, and `Assembly-CSharp.csproj`.
Rejected Alternatives: Claiming Unity scene binding from code review would be false. Editing `InputDispatcher.cs` or restoring Assembly-CSharp packages from this UX task would cross the domain boundary.
Scalability potential: No new runtime branch beyond the High/Ultra optional particle emission; low-tier remains cheaper than the previous coherent-noise path.
Hardware Impact: Static scans show no new GC hot-path pattern. Build is blocked externally: latest Core build stops at `EcosystemRuntimeInstaller.cs` / `SubmarineFluidDynamics.cs` for missing `Hecton8.AI.Ecosystem` and `VaultNativeBuffer<>`; Assembly-CSharp still lacks `Temp/obj/Assembly-CSharp/project.assets.json`.

## Decision 018 - Physical Authoring Bridge
Problem: The runtime had a cold `ConfigurePhysicalBinding(...)` API, but no authoring component existed to carry serialized physical-tool references without editing prefab YAML blindly.
Solution: Added `DiegeticGyroCompassPhysicalBinding` in the navigation domain. It resolves an assigned runtime, or cold-adds one to the physical tool when explicitly allowed; then it applies tool root, dial pivot, diegetic TMP text, indirect mesh/material, and optional anomaly particle emitter. Dependency injection from `GlobalRegistry` is limited to startup/cold authoring, not gameplay ticks.
Rejected Alternatives: Raw-editing `Player.prefab` without Unity API readback risks prefab corruption. Keeping code-only binding leaves no reliable authoring surface. Creating a Canvas fallback violates the XML rule.
Scalability potential: Low/MX350 binds only root/text/pivot and keeps triangle-noise/SlowTick behavior. Middle rotates the physical pivot. High/Ultra can bind indirect mesh and local salt/static failure particles without changing compass truth.
Hardware Impact: Cold binding has no steady-frame cost. Avoided runtime GameObject searches and hot registry polling keep MX350/i3 savings in the previous 1-4 us SlowTick range.

## Decision 019 - Validation After Loop 9
Problem: A new authoring bridge and text-binding edge case needed proof, and the shared worktree changed during validation.
Solution: Re-read XML and status/rationale from disk, scanned navigation for forbidden hot-path patterns, fixed `ValidateDiegeticTextBinding()` so a corrected world-space TMP binding re-enables output, and reran builds.
Rejected Alternatives: Claiming full Unity validation would be false because no MCP scene resources are available and no serialized prefab/scene reference is present. Editing RealtimeCSG or submarine-fluid code from UX Navigation would violate domain ownership.
Scalability potential: No new runtime math branch. The bridge exposes Low/Middle/High/Ultra presentation hooks without duplicating compass authority or signals.
Hardware Impact: Navigation scan shows no new GC/hot-path allocation. `dotnet build Hecton8.Core.csproj --no-restore -m:1 /v:minimal` succeeded once with 0 warnings/0 errors, then after concurrent worktree movement latest Core build stops outside compass at `SubmarineFluidDynamics.cs(2004)` missing `RefreshNativeStateViewsFromVault`. `Assembly-CSharp.csproj` remains blocked by external `RealtimeCSG.csproj` missing-source CS2001 errors and the same submarine-fluid wall.

## Decision 020 - Indirect Dial GPU Bandwidth Repair
Problem: The High/Ultra indirect dial path still uploaded one matrix through `ComputeBuffer.SetData` every draw. That violates the project bandwidth rule and can create avoidable CPU/GPU sync pressure on Steam Deck and other constrained memory systems.
Solution: Replaced the dial upload path with double-buffered `GraphicsBuffer` objects created with `UsageFlags.LockBufferForWrite`. The runtime now writes indirect args and changed dial matrices through `LockBufferForWrite` plus `UnsafeUtility.MemCpy`, binds the material only when the published buffer changes, and skips matrix uploads when heading, position, rotation, and scale are unchanged.
Rejected Alternatives: Keeping `SetData` was simpler but keeps bandwidth debt. Uploading every frame to a single `GraphicsBuffer` would satisfy the API shape but not the double-buffering requirement. Moving this into a new global service would add indirection for a one-instance physical tool.
Scalability potential: Low/Middle paths do not allocate GPU buffers. High/Ultra still get the indirect physical dial, and saved upload bandwidth can be spent on glass shader overkill or optional anomaly particles when stress is low.
Hardware Impact: MX350/Quest path is unchanged because indirect rendering remains tier/platform gated. Steam Deck and High/Ultra PC avoid redundant 64-byte matrix uploads and `SetData` synchronization; estimated saving is 2-8 us on unchanged dial frames, with larger hitch avoidance potential on MicroSD/I/O stressed sessions. Unity batchmode compile was attempted after the repair; it failed outside compass in editor/audio/core dependency errors and showed no `DiegeticGyroCompass` or `Hecton8.UI.Navigation` errors in the log.

## Decision 021 - Vault State Eviction
Problem: After persistent `NativeArray` fields were removed, the runtime still held compass gameplay state in private MonoBehaviour fields: prior AUP, signal-derived anomaly/power/stress, calibration request, drift clock, frame sequence, blackbox cursor, and snapshot cache. That contradicted the `CompassStateDTO` authority requirement.
Solution: Expanded `CompassStateDTO` from 136 to 176 bytes with `PreviousActualAUP`, `SystemStress01`, `NoiseClockSeconds`, `BlackBoxCursor`, and reserved padding, then moved the remaining gameplay state into the vault DTO. Calibration request now uses a state flag, frame sequence uses `state.Frame`, and `TryGetSnapshot()` builds directly from vault state.
Rejected Alternatives: Keeping private fields was faster to code but keeps hidden state. Adding a second DTO would split compass authority. Polling direct producers instead of using typed SignalBus snapshots would break lane segregation.
Scalability potential: Low/Middle/High/Ultra now share the same vault state. Low still uses SlowTick and triangle noise. High/Ultra keep indirect dial and local failure VFX without duplicating authority in presentation fields.
Hardware Impact: Direct frame-time savings are effectively 0 us; this is correctness and ownership hardening. The DTO grows by 40 bytes, still one cache-trivial state record. MX350/i3 impact is below measurement noise, while Quest/ARM gets explicit `Pack = 1, Size = 176` layout proof.

## Decision 022 - Evidence Alignment And NativeArray Audit
Problem: The task status still referenced the earlier loop 11 Unity log while the latest proof is loop 11b. The data-sovereignty audit also needed explicit separation between forbidden private arrays and required vault views/job fields.
Solution: Updated status/log evidence to reference `Unity_COMPASS_GYRO_STABILIZER_loop11b.log`. Re-ran the log search and NativeArray scan: loop 11b errors are external audio/editor/core walls, and compass `NativeArray<T>` hits are vault buffer views, helper parameters, and the required Burst job views over vault-owned state/output. Revalidated `Hecton8.Core.csproj` green after the documentation patch; `Assembly-CSharp.csproj` still stops outside compass on missing RealtimeCSG source files.
Rejected Alternatives: Claiming master grade from a blocked Unity compile would be false. Removing required `NativeArray<float>` job output would violate the XML task. Replacing vault views with managed arrays would break zero-GC and data sovereignty.
Scalability potential: No runtime branch changed. Low/Middle/High/Ultra keep the same vault-owned compass state while presentation tiering remains separate.
Hardware Impact: Runtime impact is 0 us. The value is audit integrity: no hidden allocation or private array ownership was found in the compass domain, and the unresolved compile wall remains outside UX/NAVIGATION. Current external build wall is 216 `RealtimeCSG.csproj` CS2001 missing-source errors.

## Decision 023 - High-Tier Indirect Submission And Presentation Vault Eviction
Problem: The High/Ultra indirect dial path could stop rendering when heading stopped changing because `Graphics.DrawMeshInstancedIndirect` is a per-frame submission, not persistent GPU state. The runtime also still held presentation cache values in private MonoBehaviour fields after gameplay state had been evicted to the vault.
Solution: Added `CompassPresentationStateDTO` (`Pack = 1, Size = 80`) and `BufferID.CompassPresentationState = 467`. Moved cardinal, shader, particle debt, dial heading, dial transform, matrix buffer index, and presentation flags into the vault-owned DTO. The runtime now submits the indirect dial every active High/Ultra visual frame while preserving dirty-gated double-buffered matrix uploads with `GraphicsBuffer.LockBufferForWrite` and `UnsafeUtility.MemCpy`.
Rejected Alternatives: Keeping private presentation fields would fail the data-sovereignty audit. Uploading the matrix every frame would fix visibility but reintroduce bandwidth waste. Stuffing presentation cache fields into `CompassStateDTO` mixed gameplay truth with UI cache state and broke standalone contract build behavior in the stale local assembly graph. Treating indirect draws as persistent would be a rendering correctness bug.
Scalability potential: Low remains snapped `SetCharArray` text with no indirect submission. Middle keeps physical pivot rotation. High/Ultra get persistent indirect dial visibility, richer glass response, and optional anomaly particle bursts without increasing gameplay-authority cost.
Hardware Impact: Low/MX350 direct runtime saving is 0 us. Steam Deck/High PC still avoid redundant 64-byte matrix uploads on unchanged frames, estimated 2-8 us saved versus the old upload path. High/Ultra now pay the necessary one indirect draw submission per active visual frame; that cost buys correct persistent dial visibility instead of hidden failure.

## Decision 024 - Velocity Reciprocal NaN Hardening
Problem: The AUP velocity helper had a finite-output fallback after reciprocal division, but the denominator itself was only guarded by `deltaTime <= 0f`. A non-finite or sub-epsilon delta could still reach the division before being contained.
Solution: Reject non-finite and epsilon-scale `deltaTime` before velocity integration, then clamp the reciprocal denominator with `math.max(deltaTime, math.EPSILON)`. Non-finite velocity output still falls back to `float3.zero`.
Rejected Alternatives: Relying only on the post-division finite check would keep a blind reciprocal in the code path. Removing velocity entirely would weaken blackbox state and service snapshots. Throwing exceptions would violate gameplay survival rules.
Scalability potential: No tier branch changed. Low/Middle/High/Ultra all keep the same AUP velocity semantics with safer denominator handling.
Hardware Impact: Runtime cost is effectively 0 us beyond one finite check and one max operation. It removes a mobile GPU/CPU NaN propagation risk from downstream rendering and telemetry.

## Decision 025 - Integration Typed-Lane Revalidation
Problem: A later integration merge drifted `DiegeticGyroCompassRuntime.ConfigureSignalLanes()` back to `GlobalSignals.InitializeAllQueues()`. That makes a UI/navigation component initialize unrelated signal queues and violates typed-lane segregation, even though the code can compile.
Solution: The integration pass restored explicit lane setup for the compass-owned/consumed typed lanes: `SignalBus<AnomalyProximitySignal>`, `SignalBus<CompassCalibratedSignal>`, `SignalBus<SurvivalVitalsChangedSignal>`, `SignalBus<SystemHealthSignal>`, and `SignalBus<AupShiftSignal>`, then revalidated Core compile and static signal hygiene.
Rejected Alternatives: Keeping the broad global queue initialization was rejected because it hides lane ownership and can create cache-hostile startup fan-out. Moving compass-specific signal setup into a new service was rejected as unnecessary scope expansion for a compile integration pass.
Scalability potential: Low/Quest/Android keep bounded compass signal initialization. Middle retains the same behavior. High/Ultra indirect dial and anomaly VFX remain available without increasing global signal startup surface.
Hardware Impact: Runtime frame savings are 0 us measured. Evidence is `Build_INTEGRATION_ASSEMBLY_SURGEON_20260516_inquisition31_typed_compass_final.log` green and `Scan_INTEGRATION_ASSEMBLY_SURGEON_20260516_inquisition32_static_final.txt` reporting `DIEGETIC_COMPASS_GLOBAL_INIT_HITS=0`.

## Decision 026 - Startup Wiring And Legacy Ribbon Depth Repair
Problem: Loop 14 found three remaining non-master-grade defects: the physical binding bridge still performed cross-component binding in `Awake()`, the legacy `ShaderCompassRibbon` cached no service and therefore read `GlobalRegistry.InertialNavigation` in `LateFrameTick()`, and the legacy compass shader used `ZTest Always`, which can draw through geometry even on a world-space canvas.
Solution: Moved physical binding to self-init only in `Awake()` and deferred runtime resolution/injection/binding to `Start()` or post-start re-enable. `DiegeticGyroCompassRuntime.OnEnable()` now avoids player/vault dependency resolution and High-tier GPU buffer creation before `Start()`. Compass-owned signal lanes explicitly configure their bounded capacities and stable hashes before `EnsureInitialized()`, while consumed lanes are only ensured. The legacy ribbon now caches `IInertialNavigationService` in cold startup and hides if no service was available. The legacy shader now uses `ZTest LEqual`.
Rejected Alternatives: Keeping `Awake()` cross-wiring was rejected because startup order between scripts is nondeterministic. Keeping a per-LateFrame registry read was rejected because `GlobalRegistry` is not a hot live query bus. Deleting the legacy ribbon/shader was rejected because no serialized GUID references were found, but deleting assets plus metas without Unity scene import proof would add avoidable integration risk. Raw-editing prefabs/scenes to bind the compass was rejected again because there is still no Unity API readback in this session.
Scalability potential: Low/MX350 remains the cheapest physical/text path with no indirect draw. Middle keeps physical pivot rotation. High/Ultra retain the indirect dial, chromatic glass scalar, and optional local particle bursts. The legacy fallback, if manually attached, is now depth-respecting and service-cached but still not counted as final diegetic scene binding.
Hardware Impact: 0 us measured. The concrete removal is one legacy per-LateFrame registry property read when that fallback is present, plus reduced overdraw/immersion risk from changing `ZTest Always` to `ZTest LEqual`. Core build is green; full Assembly build remains blocked by external RealtimeCSG missing-source errors.
