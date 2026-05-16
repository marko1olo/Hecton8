# Rationale: VOLUMETRIC_SILT_ADVECTION

## Pre-Code Decisions

Problem: User-provided prompt ID `VOLUMETRIC_PARTICLE_ADVECTION` was absent from `CURRENT_BATCH.md`.
Solution: Use the authoritative matching XML tag `VOLUMETRIC_SILT_ADVECTION` because it owns Marine Snow & Silt Compute in VFX/COMPUTE.
Rejected Alternatives: Inventing a new ID or editing under the absent ID would break the batch contract and logs.
Scalability potential: Low uses 8,000 GPU particles with cheap radial drift; Middle keeps bounded flow sampling; High/Ultra can spend budget on curl/noise/light response.
Hardware Impact: Avoids wasted integration work under the wrong owner; estimated 0 us runtime impact, prevents architectural mismatch.

Problem: Marine snow could be implemented as Unity ParticleSystem.
Solution: GPU-resident compute particles with CPU only feeding wake/throttle snapshots.
Rejected Alternatives: Standard Unity ParticleSystem was rejected because the prompt explicitly bans it for silt and mandates zero CPU manipulation of positions.
Scalability potential: Low/MX350 uses cheap billboard drift and lower dispatch count; Ultra raises particle budget and visual light response without CPU particle loops.
Hardware Impact: Prevents CPU transform/particle array churn; expected CPU savings are pending profiler proof.

Problem: Physical silt collision and floor interaction can consume GPU ALU with no gameplay truth.
Solution: Skip SDF collision and use density/wrap visual fake per prompt task 16.
Rejected Alternatives: SDF collision checks were rejected because silt is presentation-only and visual density hides floor clipping.
Scalability potential: Low skips collision entirely; High spends saved cycles on curl/light response rather than collision truth.
Hardware Impact: Avoids per-particle SDF samples; expected low-end gain pending GPU capture.

Problem: AUP rebase can make particles pop if handled CPU-side after the fact.
Solution: Apply `_AupShiftOffset` in compute shader and keep particle state GPU-side.
Rejected Alternatives: CPU readback/rewrite of particle positions was rejected due to PCIe stall and prompt ban.
Scalability potential: Low applies a single uniform offset; High/Ultra can retain denser particles through rebase without CPU synchronization.
Hardware Impact: One uniform/vector path instead of full buffer transfer; expected PCIe stall prevention pending capture.

## Loop 1 Decisions: Tasks 1-5

Problem: The renderer needed Batch006 wake data without owning another wake simulation.
Solution: Added `HectonFluidEngine.TryGetDynamicWakeGpuPayload` to expose the existing `_DynamicWakes` and `_DynamicWakeVectors` GPU buffers plus packed dispatch params.
Rejected Alternatives: Creating a private marine-snow wake buffer was rejected because it duplicates the Batch006 ring and invents a concrete dependency.
Scalability potential: Low reads only the fluid engine's capped wake slots; High/Ultra can consume the same ring with curl/noise overlays and no extra buffer family.
Hardware Impact: Avoids a duplicate 8-slot upload and cache miss path; estimated 20 us CPU-side coordination saved on low-end silicon when wake traffic is active.

Problem: Vehicle throttle needed to disturb silt without binding VFX to vehicle implementation classes.
Solution: Implemented a Burst `IJob` that consumes the latest `VehicleCommandSignal` throttle sample and publishes a `FluidImpulseSignal` carrying AUP position, velocity, radius, and lifetime into the existing signal lane.
Rejected Alternatives: Direct submarine component references and per-frame `FindObjectOfType` were rejected because they create brittle domain coupling and discovery cost.
Scalability potential: Low publishes a sparse impulse at cooldown cadence; High/Ultra uses the same signal to drive denser GPU advection and light response.
Hardware Impact: One NativeArray result and no managed allocation in the hot path; estimated 30 us avoided versus direct scene dependency/update branching on i3/MX350.

Problem: Existing allocation-time particle bootstrap wrote particle positions on CPU and uploaded both ping-pong buffers.
Solution: Added compute kernel `InitializeParticles` and removed the CPU `BootstrapParticles` position loop and upload cache.
Rejected Alternatives: Keeping the CPU bootstrap as "cold path" was rejected because the prompt explicitly mandates zero CPU manipulation of particle positions.
Scalability potential: Low initializes 8,000 particles on GPU; High/Ultra initializes 100,000 particles with the same kernel and no PCIe position payload.
Hardware Impact: Removes the 64-byte-per-particle upload for seeded state; on 100,000 particles this avoids ~6.4 MiB of cold transfer and a CPU loop spike.

Problem: Floating-origin shifts could desynchronize GPU particles if the CPU rebased state externally.
Solution: Accumulate `_AupShiftOffset` on origin-shift notification and apply it to `Pos`/`PrevPos` inside the simulation kernel before velocity integration.
Rejected Alternatives: Rebuilding particle buffers after every origin shift was rejected because it causes stalls, churn, and visible popping.
Scalability potential: Low pays one dot/uniform offset per active particle; High/Ultra preserve dense clouds through AUP shifts without a frame hitch.
Hardware Impact: Expected gain is stall prevention, not ALU reduction; low-end benefit is avoiding buffer upload and render-thread wait.

## Loop 2 Decisions: Tasks 6-10

Problem: The shader contract still used a generic particle type name despite the prompt requiring `SiltParticle`.
Solution: Renamed compute and render shader GPU structs to `SiltParticle` while preserving existing buffer/property names for stable C# bindings.
Rejected Alternatives: Renaming C# property IDs and material bindings was rejected because it would churn serialized/render contracts without changing layout.
Scalability potential: Low/Mid/High/Ultra all use the same 64B packed GPU struct; tier scaling changes count and math path, not layout.
Hardware Impact: Runtime impact is neutral; the gain is preventing contract ambiguity and accidental CPU-side particle expansion.

Problem: Low-tier wake advection needed visible silt disturbance without 3D texture or curl noise cost.
Solution: Low tier is hard-capped at 8,000 marine-snow particles and uses radial wake flow from `_DynamicWakes`/`_DynamicWakeVectors`; 3D abyssal flow and curl are gated behind high-tier scalability.
Rejected Alternatives: Sampling the 3D flow texture on MX350 was rejected because it spends texture bandwidth on a presentation-only cloud.
Scalability potential: Low = 8,000 particles/radial vector; Middle = bounded flow sampling; High = 100,000 particles + abyssal flow texture; Ultra = 100,000 particles with overkill light/curl response.
Hardware Impact: Avoids 3D texture lookups and curl ALU on low-end silicon; estimated 15 us CPU coordination saved and GPU bandwidth saved pending capture.

Problem: High-end machines should spend saved budget on visible murk, not hidden precision.
Solution: High/Ultra particle caps are 100,000 and the shader samples `_AbyssalFlowFieldTexture` before adding fake curl-noise advection when the texture path is active.
Rejected Alternatives: Raising physics fidelity or SDF collision was rejected because it is invisible for marine snow and violates the visual-fake-first mandate.
Scalability potential: High/Ultra gets chaotic wake swirl and denser headlights; Low retains a deterministic radial fake.
Hardware Impact: Low/MX350 avoids this path; RTX-class hardware spends the budget on texture-driven swirl and visual density.

Problem: Headlights needed to carve through silt without CPU particle-light loops.
Solution: Push global flashlight position/direction/cone/color uniforms and compute a per-particle cone/range boost into `SiltParticle.Pad.y`; forward and motion-vector passes consume that boost.
Rejected Alternatives: CPU per-particle spotlight checks and material keyword toggles were rejected because they allocate/control-flow the wrong side of the pipeline.
Scalability potential: Low evaluates a dot/range fake; High/Ultra render denser boosted particles inside the cone.
Hardware Impact: Avoids a CPU loop over 8,000-100,000 particles; estimated 20 us CPU saved versus managed light influence staging.

## Loop 3 Decisions: Tasks 11-17

Problem: The renderer needed URP-stable particles without CPU meshes.
Solution: Switched the draw path to `Graphics.RenderMeshIndirect` using a procedural quad mesh and added a URP `MotionVectors` pass that reads current/previous GPU positions.
Rejected Alternatives: CPU mesh rebuilds or `DrawMeshInstancedIndirect` without motion vectors were rejected because they either move work to CPU or lose temporal stability.
Scalability potential: Low uses the same indirect draw with fewer particles; High/Ultra keep temporal vectors on dense clouds.
Hardware Impact: Avoids CPU mesh particle updates; estimated 25 us CPU saved compared with rebuilding/rendering managed instances.

Problem: Wake/curl/light accumulation could create explosive velocities or NaN state.
Solution: Added `ClampParticleVelocity` using `MaxSiltSpeed`, finite checks, and hard zero fallback for invalid speed.
Rejected Alternatives: Trusting upstream wake vectors was rejected because a single bad impulse can poison the ping-pong buffer.
Scalability potential: Low clamps cheap radial wakes; High/Ultra clamps combined 3D flow/curl/headlight perturbations.
Hardware Impact: Small ALU cost buys deterministic failure containment; expected recovery cost saved is 5 us plus avoided visual corruption.

Problem: Critical VFX state needed a blackbox without managed logging.
Solution: Added a 300-entry persistent NativeArray circular buffer and binary dump path `Docs/AgentLogs/Dump_VOLUMETRIC_SILT_ADVECTION.bin` for non-finite detection.
Rejected Alternatives: `Debug.Log` per frame or no crash evidence was rejected; both violate the blackbox mandate.
Scalability potential: Same fixed 300-entry cost on all tiers; High/Ultra telemetry includes larger capacity and wake count.
Hardware Impact: Fixed 64B x 300 native memory; avoids managed string allocations and enables postmortem state recovery.

Problem: Silt collision is invisible precision and wastes GPU ALU.
Solution: Gated SDF and depth collision away from marine snow/silt; collision remains only for bubbles/debris where visual behavior already depended on it.
Rejected Alternatives: Removing collision globally was rejected because it would alter bubble/debris behavior outside the silt assignment.
Scalability potential: Low/High/Ultra silt all clip through floors; High spends saved cycles on curl and headlight density.
Hardware Impact: Saves depth/SDF samples per silt particle; especially valuable at 100,000-particle high-tier density.

Problem: Destroy/respawn causes churn and visible discontinuity when particles leave the camera shell.
Solution: Added mathematical wrap around camera shell and hard 50m distance guard.
Rejected Alternatives: Killing particles and CPU/GPU respawn upload was rejected because it churns state and breaks continuity.
Scalability potential: Same cheap wrap on all tiers; high density hides the wrap while preserving fog volume.
Hardware Impact: Estimated 8 us avoided from no destruction/reseed path and fewer visible resets.

## Loop 4 Validation Wall: Task 18

Problem: Unity validation could not reach Vulkan/DX12 shader/API compile.
Solution: Ran Unity batchmode default, `-force-d3d12`, and `-force-vulkan`; all stopped on existing C# compile errors before touched VFX files were compiled as a platform shader path.
Rejected Alternatives: Claiming Vulkan/DX12 success was rejected because the logs prove a pre-existing project compile wall.
Scalability potential: Validation state does not change runtime scalability; implementation remains tier-gated from MX350 to RTX.
Hardware Impact: No runtime impact. The integrator must clear unrelated Audio/Physics/Editor assembly errors before platform shader validation can execute.

## Loop 5 Omega Anti-Bloat

Problem: The Omega mandate required a final circular-dependency and DI abuse check after all core tasks were closed.
Solution: Ran static scans for `GameObject.Find`, `FindObjectOfType`, direct renderer/fluid construction, and prompt-specific shader `distance()` usage. No banned calls were found in touched files.
Rejected Alternatives: Treating global service access as a new circular dependency was rejected because this code uses existing `GlobalRegistry`, `VehicleCommandSignalBus`, and `GlobalSignals` contracts rather than constructing peer systems.
Scalability potential: Dependency shape stays stable across low/high tiers; VFX reads published GPU buffers and signals instead of owning gameplay or fluid state.
Hardware Impact: No runtime cost; prevents future managed discovery spikes and architecture drift.
