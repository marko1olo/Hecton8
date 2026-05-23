# SHINOBU_313 Rationale

Status: POLISH STATIC VERIFIED / ZERO-TARGET GPU CULL HARDENED / CSV SCRATCH INGEST HARDENED / GRAPHICSBUFFER LOCK RELEASE HARDENED / WORLD NAMESPACE PURGED / DEAD FLOW BUFFER PURGED / SCORE TOP-N CORRECTED / COMPILE BLOCKED BY UNITY PROJECT STALE

## Decision 000 - Domain Boundary

Problem: Parasite swarms visually attach to hulls but must not become gameplay truth or netcode state.
Solution: Treat parasite particles as presentation-owned GPU state. CPU stages only compact thermal target DTOs. Gameplay damage/camera effects remain excluded unless an existing approved signal lane exists.
Rejected Alternatives: Standard Unity `ParticleSystem` collision/triggers and GameObject leech agents allocate, force CPU transform updates, and create netcode/hash contamination.
Scalability potential: Low uses sparse impostor-like particles and low curl complexity; Middle increases density; High adds richer curl/flow response; Ultra adds visual-overkill density and shader variation without changing authority.
Hardware Impact: Estimated CPU saving on i3/MX350 is removal of per-particle managed update cost; exact microseconds PENDING VERIFICATION.

## Decision 001 - DTO Layout

Problem: CPU-to-GPU target payload must be stable across ARM64, Burst, and HLSL.
Solution: Primary target DTO will use `[StructLayout(LayoutKind.Explicit, Size = 32)]` with raw fields only: local position offset 0, thermal signature offset 12, velocity offset 16, attraction radius offset 28.
Rejected Alternatives: Auto properties, sequential layout, `Pack=1`, or managed target wrapper classes.
Scalability potential: Same 32-byte payload scales from Low to Ultra; only count and shader math fidelity change.
Hardware Impact: 32-byte stride is cache-line friendly for mobile and avoids misaligned read penalties. Exact gain PENDING VERIFICATION.

## Decision 002 - Fixed Candidate Buffer Instead Of NativeList

Problem: Prompt requested a `NativeList` push path for top-N parasite target extraction, but project doctrine requires persistent arrays to live in `GlobalDataVault` and rejects private hot-path native ownership.
Solution: Use `ShinobuParasiteTargetCandidates[512]` as a fixed unmanaged Vault lane, then run `SelectTopParasiteTargetsJob` over that lane to produce `ParasiteTargetDTO[16]`.
Rejected Alternatives: Persistent private `NativeList<ParasiteTargetCandidateDTO>` and per-frame list construction; both increase ownership ambiguity and allocator pressure.
Scalability potential: Low scans bounded sources and writes top targets; Middle/High/Ultra can raise source scan limits by Vault capacity without changing GPU target ABI.
Hardware Impact: Fixed 64-byte candidates avoid dynamic capacity growth and cache-line churn on i3/MX350. Estimate: 20-80 us CPU saved versus managed/dynamic target list. PENDING PROFILER.

## Decision 003 - No Raw Prefab ParticleSystem Deletion

Problem: The only scoped VFX script `ParticleSystem` is camera speed-lines; prefab ParticleSystem hits are vent bubble columns, not parasite swarm authority.
Solution: Preserve unrelated assets and add `Biological_Particle_Scanner` plus report artifact. Parasite swarm authority is implemented as `ParasiteSwarmGpuRuntime` and compute shader buffers.
Rejected Alternatives: Raw YAML deletion of vent bubble ParticleSystems or camera speed-lines, which would damage unrelated presentation systems.
Scalability potential: Scanner prevents future biological CPU boids while preserving non-swarm VFX. Low/Middle/High/Ultra unaffected by legacy vent assets.
Hardware Impact: No direct microsecond saving from deleting unrelated components; preventing biological CPU boid regressions protects future frame time. Exact gain PENDING VERIFICATION.

## Decision 004 - GPU Dear Lie Attachment

Problem: Exact particle-to-hull triangle collision for hundreds of thousands of micro-parasites would be unsustainable on CPU and too divergent on GPU.
Solution: In HLSL, particles inside `AttractionRadius` snap to a spherical shell around the thermal target and blend to target velocity.
Rejected Alternatives: `ParticleSystem` collision, mesh raycasts, compute triangle BVH, or rigidbody parasites.
Scalability potential: Low uses fewer particles and sparse curl; Middle raises density; High adds stronger curl/flow response; Ultra increases visual density without changing authority.
Hardware Impact: Estimated CPU saving on i3/MX350 is full removal of per-particle collision/update loops. GPU cost scales by `GlobalQualityWeight`; exact microseconds PENDING PROFILER.

## Decision 005 - Rollback And Gameplay Fence

Problem: Visual parasite locations could poison rollback hashes or imply gameplay hull damage authority.
Solution: Do not modify rollback descriptors and do not publish damage/camera signals. Runtime consumes only `AupShiftSignal`; target source data remains owned by thermal/physics domains.
Rejected Alternatives: `ParasiteAttackSignal`, adding visual buffers to Merkle descriptors, or applying hull stress from the VFX renderer.
Scalability potential: All tiers keep the same gameplay truth; only visual count/curl/cadence changes.
Hardware Impact: Avoids bandwidth and hash work for up to the configured visual particle cap. Estimate: catastrophic netcode bandwidth avoided; exact CPU us PENDING VERIFICATION.

## Decision 006 - Compile Guard Obeyed

Problem: Full compile is forbidden while CPU load is >50% or another `dotnet`/`csc` build is active.
Solution: Build was not launched; static audit and targeted code fixes continued. Latest guard state: active `dotnet` process 16552 and CPU load 100%. Generated `*.csproj/*.slnx` files also do not include the new parasite asmdef/scripts, so external `dotnet build` would not prove this domain until Unity regenerates projects.
Rejected Alternatives: Forcing `dotnet build` to satisfy checklist while violating hardware guard or reporting a build that does not include the changed files.
Scalability potential: Protects developer workstation and other agents from contention.
Hardware Impact: Avoided rebuild contention on already saturated machine. Exact saved time depends on other agents.

## Decision 007 - Explicit Parasite Runtime/Editor Assemblies

Problem: An untracked `Hecton8.VFX.Parasites.asmdef` appeared inside the new parasite folder with `autoReferenced=false`, no editor split, and incomplete domain references, creating a compile-risk assembly island for runtime and editor code. Deleting it outright would push parasite files into the parent core assembly, which is also the wrong ownership route.
Solution: Replace it with `Hecton8.VFX.Parasites.Runtime.asmdef` plus `Hecton8.VFX.Parasites.Editor.asmdef`, matching existing VFX assembly patterns and explicitly referencing Core, Core.Contracts, Core.Memory, Burst, Collections, Jobs, and Mathematics. Direct Thermodynamics/KCC references were removed after boundary audit.
Rejected Alternatives: Leaving the bad asmdef, adding speculative dependency edges to unrelated VFX assemblies, or letting parasite presentation compile as part of the root core assembly.
Scalability potential: Low/Middle/High/Ultra parasite fidelity remains controlled by runtime DTOs and compute dispatch, not assembly topology.
Hardware Impact: No frame-time gain. Prevents editor compile churn and dependency-wall risk on the shared workstation.

## Decision 008 - Contract Signal Projection Instead Of Sibling DTO Reads

Problem: The first extraction job compiled directly against `HeatSourceDTO`, `ThermalCellDTO`, `ThermalGridTuningDTO`, and `KinematicStateDTO`, making parasite VFX recompile when Thermodynamics/KCC layouts change.
Solution: Stage existing `ThermalSourceSignal` snapshots into `ShinobuParasiteTargetCandidates`, then run `ExtractParasiteTargetsJob` over the parasite-owned 64-byte candidate DTO. Producer-owned heat/kinematic projections stay outside this runtime until owners publish a neutral contract lane.
Rejected Alternatives: Keeping sibling runtime references, duplicating Thermodynamics/KCC DTOs locally, or editing producer domains to satisfy a VFX task.
Scalability potential: Low reads up to the signal capacity only; Middle/High/Ultra can raise producer signal density without changing parasite ABI or assembly topology.
Hardware Impact: Prevents compile-wall churn and removes KCC/Thermodynamics cold handle scans from VFX. Runtime CPU change is bounded to <=128 signal records, not per-particle work.

## Decision 009 - One-Frame-Late Target Selection

Problem: Same-frame `Schedule(...).Complete()` in `LateFrameTick` blocked the main thread and created a schedule/readback loop for visual-only target selection.
Solution: Resolve only a previously completed `JobHandle`; if it is not complete, reuse the prior GPU target buffer and avoid reading the Vault target array while selection may still be writing. New extraction/selection is scheduled after render telemetry and consumed one frame later. `Complete()` remains only after `IsCompleted` in hot path and as teardown safety.
Rejected Alternatives: Main-thread sorting every frame, blocking completion, or reading NativeArrays while a job safety handle is still outstanding.
Scalability potential: Low/Middle/High/Ultra all tolerate one-frame visual latency because parasite targets are presentation-only; density and curl still scale continuously.
Hardware Impact: Removes worst-case target extraction stalls from `VISUAL_SYNC`. Estimated low-end main-thread savings: 20-80 us under active thermal signals, pending profiler.

## Decision 010 - GPU Alias And Mobile Group Correction

Problem: `CS_RebaseParasites` previously wrote the same buffer it read, and the shader/runtime used 256-wide groups despite the MX350/mobile mandate.
Solution: Rebase now ping-pongs `_H8ParasiteRead` to `_H8ParasiteWrite`; runtime flips buffer parity around the rebase dispatch before advect. Thread groups are 64-wide and `CS_CullParasites` guards visible-index writes.
Rejected Alternatives: In-place UAV rebase, CPU download/reupload, or 256/1024-wide PC-first groups.
Scalability potential: Low reduces occupancy pressure and curl octaves; Middle/High/Ultra buy visual density through particle budget rather than larger thread groups.
Hardware Impact: Reduces mobile wave pressure and removes UAV alias hazard. Exact GPU microseconds pending RenderDoc/Unity profiler.

## Decision 011 - Editor Facade Purge Of IMGUI Graph

Problem: The tuner graph used `IMGUIContainer`, `GUILayoutUtility`, and `Handles`, failing the UI Toolkit facade requirement.
Solution: Replaced it with a `VisualElement.generateVisualContent` painter graph reading `SwarmTelemetryEntry[300]` directly.
Rejected Alternatives: Keeping IMGUI for speed or adding managed UI text churn every repaint.
Scalability potential: Editor-only; runtime tiers unaffected.
Hardware Impact: 0 runtime us. Reduces editor repaint allocation risk during tuning sessions.

## Decision 012 - Camera-Relative Draw Params Buffer

Problem: Compute particles are camera-relative `float3` values, but the procedural shader was treating them as world-space, causing swarm render drift toward origin under camera movement.
Solution: Add a one-element `GraphicsBuffer<float4>` draw params lane containing camera world position and current particle-buffer parity. The shader reads both ping-pong particle buffers plus draw params and reconstructs world position as `cameraWS + localParticle`.
Rejected Alternatives: Per-frame `Material.SetVector`, `MaterialPropertyBlock`, absolute float world positions, or CPU particle rebasing for camera movement.
Scalability potential: Same one-float4 upload for Low/Middle/High/Ultra; particle density remains the only scalable cost.
Hardware Impact: Adds 16 bytes/frame upload and prevents incorrect render-space drift. CPU cost is below measurement noise; visual correctness gain is mandatory.

## Decision 013 - Vault Writer Fences For Target Jobs

Problem: The previous target extraction path passed Vault-backed `NativeArray`s into writer jobs after `TryResolveHandle`, which was structurally correct memory ownership but lacked explicit writer-fence metadata for long-lived job writes.
Solution: `ParasiteSwarmGpuRuntime` now acquires write locks for `ShinobuParasiteTargets`, `ShinobuParasiteTargetCandidates`, and `ShinobuParasiteTargetCount` before scheduling extraction/selection jobs. Locks stay held until the one-frame-late `IsCompleted` fence, then release after `Complete`. Teardown completes pending work and releases the same locks. Telemetry ring and cursor writes use short per-frame write locks.
Rejected Alternatives: Private `NativeArray` staging outside the Vault, blocking same-frame completion, or raw unlocked Vault writes.
Scalability potential: Low/Middle/High/Ultra use the same lock discipline; only refresh cadence, target density, and GPU particle budget scale.
Hardware Impact: Adds bounded metadata writes per target-refresh frame. Avoids undefined read/write overlap and keeps CPU particle simulation at 0 us.

## Decision 014 - No Fake External Build Proof

Problem: CPU guard currently blocks compile, and generated project files are stale for the new parasite assembly.
Solution: Verified with `rg` that `*.csproj/*.sln/*.slnx` contain no `VFX.Parasites`, `ParasiteSwarmGpuRuntime`, `ParasiteSwarmContracts`, or `Hecton_ParasiteSwarm` entries. Task 20 remains open until Unity imports/regenerates projects and a legal compiler pass can actually include these files.
Rejected Alternatives: Running `dotnet build` on stale projects, reporting static scans as compilation, or touching unrelated project-generation files under massive concurrent worktree churn.
Scalability potential: No runtime impact; preserves truthful verification state for every hardware tier.
Hardware Impact: 0 runtime us. Prevents wasted build I/O and false-positive reports.

## Decision 015 - Million-Particle Ceiling Without Forced Mobile Allocation

Problem: The prompt requires RTX-class million-particle support, but the runtime must not force that allocation on mobile or mid-tier devices at component startup.
Solution: Raise the hard supported GPU particle ceiling to 2,000,000 while leaving the serialized default `configuredMaxParticles` at 500,000. The live budget remains `min(configuredMaxParticles, ResolveParticleBudget(GlobalQualityWeight, tuning))`, so Low/Middle tiers keep continuous shedding and Ultra can opt into million-scale buffers.
Rejected Alternatives: Keeping the hard cap at 500k, or setting the default allocation to 2M and spending VRAM on Quest before quality math can shed runtime work.
Scalability potential: Low uses 5k budget, Middle uses the smooth curve under configured cap, High/Ultra can raise configured cap toward 2M for visual overkill without DTO/layout changes.
Hardware Impact: 0 CPU us. Default memory remains roughly 500k particle lanes; 2M memory is paid only by explicit high-end configuration.

## Decision 016 - Dead Mock Kinematics Lane Purge

Problem: `ShinobuParasiteMockKinematics` remained in the shared BufferID enum after the runtime stopped reading KCC/kinematic DTOs.
Solution: Remove the unused enum lane. Parasite target input is now exclusively `ThermalSourceSignal` -> `ParasiteTargetCandidateDTO` -> `ParasiteTargetDTO`.
Rejected Alternatives: Keeping a stale kinematics-named lane as a future placeholder, which weakens compile-wall evidence and invites hidden sibling DTO coupling.
Scalability potential: No tier-specific behavior; this is authority-route hygiene.
Hardware Impact: 0 runtime us. Reduces integration ambiguity.

## Decision 017 - Debug Gizmo Uses Camera-Local ABI

Problem: After camera-relative target packing, `ParasiteAttractionDebugGizmo` still drew `ParasiteTargetDTO.LocalPosition` as world-space.
Solution: Resolve the cached player runtime pose through `GlobalRegistry.Player` and draw `RuntimePosition + LocalPosition`. This keeps the editor facade aligned with the shader draw ABI without `Camera.main`.
Rejected Alternatives: Scene camera search, absolute float target storage, or leaving misleading debug spheres near origin.
Scalability potential: Editor-only; runtime quality tiers unchanged.
Hardware Impact: 0 runtime us.

## Decision 018 - Cold Shader Pass Warmup

Problem: Compute kernels were warmed by the startup init dispatch, but the draw material pass could still first-touch during infestation rendering.
Solution: After persistent buffer binding, cold startup calls `Material.SetPass(0)` for the parasite material. The shader has no `multi_compile` permutations, so variant space stays fixed.
Rejected Alternatives: `Shader.WarmupAllShaders`, runtime first-draw hitch, or broad shader variant collections outside this domain.
Scalability potential: All tiers share one material pass; quality changes are uniform values and buffer counts, not shader variants.
Hardware Impact: Moves material pass touch to startup. Runtime frame saving is hitch avoidance, not steady-state microseconds.

## Decision 019 - Fault Telemetry Must Trigger Dumps

Problem: The top-16 target selection can legally truncate a dense thermal signal frame, and invalid math could be marked inside `RecordTelemetry` without propagating back to the dump trigger.
Solution: Track `_lastCandidateOverflowCount`, mark `TelemetryFlagTargetOverflow`, store the exact overflow count, include overflow in the dump fault mask, and pass telemetry flags by `ref` so invalid math detected during ring recording can trigger the same dump path.
Rejected Alternatives: Silent top-N truncation, or recording invalid math without a raw `.bin` proof artifact.
Scalability potential: Low/Middle/High/Ultra all keep the same GPU target ABI; high-density frames are diagnosed without changing particle authority or widening the shader loop.
Hardware Impact: Adds one integer compare and one telemetry field write per visual frame. Prevents forensic blindness; no measurable steady-state frame cost expected.

## Decision 020 - Fallback Shader Lookup Is Cold-Only

Problem: If the fallback parasite shader is missing and no material is assigned, `ResolveMaterial` would call `Shader.Find` every visual frame.
Solution: Cache a `_fallbackMaterialLookupAttempted` flag; missing shader lookup is attempted once after cold resource setup and then skipped until teardown resets GPU resources.
Rejected Alternatives: Repeating shader name lookup in the render path or forcing every scene to assign a material before the runtime can boot.
Scalability potential: All hardware tiers share the same fixed shader; this only hardens misconfigured scenes.
Hardware Impact: Avoids unknown managed shader search cost under error configuration. Normal configured scenes unchanged.

## Decision 021 - GPU Particle NaN Reset Before Advection

Problem: The cull kernel filtered non-finite particle positions from rendering, but the corrupted particle state could remain in the read/write ping-pong buffers and continue contaminating later force integration.
Solution: `CS_AdvectParasites` now checks finite position, velocity, and life before applying target forces. Non-finite particles take an explicit reset branch to deterministic dormant positions, seed velocity, and hashed life before advection continues, avoiding masked `lerp` with NaN operands.
Rejected Alternatives: Only hiding bad particles during cull, or downloading GPU buffers for CPU repair.
Scalability potential: All tiers keep the same reset path; lower quality simply executes it for fewer active particles.
Hardware Impact: Adds finite checks per active particle. This is accepted because one persistent NaN can poison the visual buffer across long sessions.

## Decision 022 - CSV Header Detection Must Not Drop Headerless Profiles

Problem: The initial CSV parser skipped the first line containing any alphabetic byte, which correctly skipped the shipped header but would discard a valid first species row in a headerless tuning file.
Solution: Replace the broad alpha heuristic with exact first-token checks for `species` or `name`, using byte comparisons only.
Rejected Alternatives: `string.Split`, `Encoding.UTF8.GetString`, or keeping a parser that silently loses the first designer profile.
Scalability potential: Low/Middle/High/Ultra profile data remains designer-owned and hot-reloadable without C# recompilation.
Hardware Impact: Cold boot only. No runtime frame cost.

## Decision 023 - Binary Payload Ledger Entry Required For New Vault Lanes

Problem: SHINOBU_313 creates new BufferID lanes and explicit DTO ABIs; without a binary ledger entry, integrators have no central record of ownership, sizes, fault route, or rollback exclusion.
Solution: Add a concise `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` section listing BufferIDs `71980..71987,71989,71990`, DTO sizes, render route, Dear Lie route, scalability route, and fault dump route.
Rejected Alternatives: Keeping the route only in task logs, or expanding unrelated architecture docs.
Scalability potential: The ledger records that quality changes density/curl cost only, not DTO layout or authority.
Hardware Impact: 0 runtime us. Reduces integration ambiguity.

## Decision 024 - Fixed Visual Tick For GPU Advection

Problem: The parasite compute frame previously used `Time.deltaTime` and `Time.time`. Even though the swarm is visual-only, variable frame deltas weaken long-session reproducibility and conflict with the locked 60 FPS discipline.
Solution: Use a local `SimulationTickDeltaSeconds = 1f / 60f` constant and derive visual phase from a runtime-owned private visual counter. This preserves presentation-only rollback exclusion while removing variable integration deltas and Unity frame-clock growth from compute inputs.
Rejected Alternatives: Adding a new timing dependency to another domain, widening `ParasiteSwarmTuningDTO`, using `Time.frameCount`, or continuing to feed floating frame deltas into HLSL advection.
Scalability potential: Low/Middle/High/Ultra all use the same fixed tick; quality still changes budget and curl complexity only.
Hardware Impact: No measurable CPU saving claimed. It reduces nondeterministic visual drift and avoids jitter from long variable-delta frames.

## Decision 025 - Vent ParticleSystems Are Not Parasite Authority

Problem: Static prefab search found `PFB_Support_Pocket_Hazard.prefab` with `ParasiteA/B` names and three `ParticleSystem` blocks, raising a Task 04 deletion risk.
Solution: Read the YAML structure. `ParasiteA` and `ParasiteB` are mesh cylinders; the ParticleSystems are `VentBubbleColumn_Secondary`, `VentBubbleColumn_LOD1`, and `VentBubbleColumn_Main`. They are localized vent presentation documented by prior underwater visual docs, not parasite swarm simulation.
Rejected Alternatives: Raw YAML deletion of vent columns, or counting mesh parasite silhouettes as CPU particle swarms.
Scalability potential: Parasite swarms remain compute-driven; vent bubbles remain a separate legacy visual target for a different owner.
Hardware Impact: Avoids damaging unrelated assets. No SHINOBU_313 runtime microsecond claim.

## Decision 026 - Shared Rendering Report Must Be Restored

Problem: `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` no longer contained the SHINOBU_313 section after neighboring agent report writes.
Solution: Restore a concise SHINOBU_313 JSON section with static evidence, route, DTO, and compile-gate status, then validate the file with `ConvertFrom-Json`.
Rejected Alternatives: Leaving proof only in task status/log files, or overwriting the shared report and deleting other agents' sections.
Scalability potential: No runtime behavior change. Keeps evidence available to integrators across all hardware tiers.
Hardware Impact: 0 runtime us.

## Decision 027 - Top-N Candidate Pointer Hoist

Problem: `SelectTopParasiteTargetsJob` called `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Candidates)` inside the candidate scan loop.
Solution: Resolve the pointer once before the loop and keep `ref readonly` reads for each 64-byte candidate row.
Rejected Alternatives: Leaving a repeated safety/pointer helper call in a loop that can run every visual refresh.
Scalability potential: Low/Middle/High/Ultra all use the same 512-candidate upper bound; hoist keeps top-N selection mechanically clean if capacity rises later.
Hardware Impact: Sub-microsecond expected on i3/MX350; profiler proof pending.

## Decision 028 - Shader Normalize And Cull Vaccination

Problem: `SafeNormalize` clamped length before deciding fallback, so a zero vector could produce zero velocity instead of the supplied fallback. Cull also checked position finiteness but not life finiteness.
Solution: Evaluate raw length and finiteness first, return fallback on zero/non-finite vectors, floor inverse-square attraction with `radius * radius`, and require finite `Life01` during cull.
Rejected Alternatives: Relying on cull-only hiding or accepting zero-velocity dormant particles.
Scalability potential: All tiers share the same safety path. Low executes it for fewer particles; Ultra gets long-session resistance at higher particle counts.
Hardware Impact: Negligible ALU per particle; prevents persistent poisoned GPU rows and attachment stalls.

## Decision 029 - Static Compile-Risk Audit Instead Of Illegal Build

Problem: The next proof step is compilation, but the workstation currently violates the local build guard and generated project files still do not include the new parasite assembly/files.
Solution: Run targeted static checks only: local Unity API call-site comparison, asmdef JSON parse, DTO/property scan, generated project staleness scan, report JSON parse, and `git diff --check`. Latest escalated guard sample: CPU load 76 and active MSBuild `dotnet.exe` PIDs `5652,15352,1716,22460,21912,19416,13176`.
Rejected Alternatives: Running `dotnet build` under active MSBuild processes, or treating stale generated projects as meaningful compile proof.
Scalability potential: No runtime change. This protects concurrent agents and keeps Task 20 truthfully blocked until Unity import/project regeneration plus a legal compile window.
Hardware Impact: Avoided extra compiler contention on saturated hardware. Runtime impact is 0 us.

## Decision 030 - HLSL Finite Checks Must Be Backend Portable

Problem: `Hecton_ParasiteSwarm.compute` used `isfinite()` even though local project shaders do not establish that helper across Unity backends, which can turn a safety path into an import/compiler failure.
Solution: Replace `isfinite()` with local `H8FiniteScalar` and `H8Finite3` predicates. The comparison rejects NaN because `NaN <= maxFloat` is false and rejects infinities because `abs(v)` exceeds the finite bound.
Rejected Alternatives: Trusting backend-specific `isfinite`, removing the safety branch, or moving NaN repair to CPU readback.
Scalability potential: All tiers keep the same safety predicate; Low pays it for fewer particles, Ultra pays it for higher particle density to prevent persistent poisoned GPU rows.
Hardware Impact: Negligible ALU cost per active particle. It avoids shader import risk and keeps the zero-readback GPU repair path intact.

## Decision 031 - Shared Report Drift Must Be Repaired In Place

Problem: `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` again lost the `shinobu_313_parasitic_fauna_particle_swarms` section after neighboring report writes.
Solution: Re-add only the SHINOBU_313 JSON key with static route, DTO, shader-safety, Vault, compile-gate, and profiler-pending evidence; validate with `ConvertFrom-Json`.
Rejected Alternatives: Overwriting the whole shared report, deleting neighboring agent keys, or relying on task logs instead of the required metric validator artifact.
Scalability potential: No runtime behavior change. It preserves integrator-visible proof for all tiers.
Hardware Impact: 0 runtime us.

## Decision 032 - Compile Remains Legally Blocked

Problem: After the shader/report polish pass, compile proof is still the next verification step, but local guard state is invalid for a build.
Solution: Re-sampled the guard: CPU load is `100`, active `dotnet.exe` PID `7816`, and generated project files still contain no parasite assembly/script hits. Keep Task 20 open.
Rejected Alternatives: Starting `dotnet build` while CPU is saturated, or reporting a stale-project build as SHINOBU_313 evidence.
Scalability potential: No runtime behavior change. It protects the concurrent workstation and keeps verification truthful.
Hardware Impact: Avoided extra compiler I/O/contention. Runtime impact is 0 us.

## Decision 033 - Particle Budget Must Never Exceed Allocated GPU Rows

Problem: Runtime particle budget was derived from `configuredMaxParticles` and tuning, but a live inspector/config increase after `OnEnable` could exceed the already allocated ping-pong `GraphicsBuffer.count`.
Solution: Clamp every visual frame to the minimum valid count of `_particleBufferA` and `_particleBufferB`, and make `DispatchAndRender` fail closed with `TelemetryFlagNoCompute` when required GPU resources are invalid.
Rejected Alternatives: Reallocating million-particle buffers in the hot path, trusting serialized config after cold allocation, or returning early with no blackbox evidence.
Scalability potential: Low/Middle/High/Ultra still scale continuously through `GlobalQualityWeight`; the clamp only enforces the physical allocation ceiling.
Hardware Impact: Adds one buffer-count min path per frame. Prevents GPU out-of-bounds dispatch and avoids hot-path reallocations.

## Decision 034 - Editor Facades Must Preserve Evidence And Reload Tuning

Problem: The scanner could regenerate a minimal SHINOBU_313 JSON section and erase detailed proof fields; CSV profile edits also required a runtime restart to reload.
Solution: Extend `Biological_Particle_Scanner.BuildSection` with route, DTO, shader safety, scalability, Vault, compile, and profiler fields. Add a UI Toolkit reload button to `AbyssalParasiteTunerWindow` that feeds the existing byte-span CSV parser.
Rejected Alternatives: Manual report restoration after every scan, IMGUI reload controls, or designer recompilation/restart for CSV profile edits.
Scalability potential: No runtime tier change. Designers can tune profiles for low/middle/high/ultra without C# rebuilds.
Hardware Impact: Editor-only cold file read and string construction. Runtime impact is 0 us.

## Decision 035 - Source Grep Gates Must Stay Noise-Free

Problem: The parasite source scanner flagged `BindVaultHandles` and scanner-generated report text even though neither represented Unity editor `Handles` or hot CPU particle/material/camera APIs.
Solution: Rename the runtime method to `BindVaultDescriptors` and rewrite the scanner string to avoid exact forbidden API tokens in source literals.
Rejected Alternatives: Leaving known false positives for integrators to mentally filter, or weakening the scanner pattern.
Scalability potential: No runtime behavior change. Clean grep gates protect future low/middle/high/ultra polish passes from false evidence.
Hardware Impact: 0 runtime us.

## Decision 036 - Metric Scanner Must Not Corrupt Shared JSON

Problem: `Biological_Particle_Scanner.UpsertReportSection` always added a comma after replacing an existing SHINOBU_313 section, which would corrupt JSON if that section was the final property.
Solution: Add token-aware whitespace skipping around the replaced object and omit the comma when the next token is the closing root brace; empty-report insertion also omits the trailing comma.
Rejected Alternatives: Assuming SHINOBU_313 remains a middle property forever, or requiring manual JSON repair after scanner runs.
Scalability potential: No runtime behavior change. Keeps integrator reports parseable regardless of agent ordering.
Hardware Impact: Editor-only string scan. Runtime impact is 0 us.

## Decision 037 - Inactive GPU Target Slots Must Not Be Read

Problem: `CS_AdvectParasites` masked inactive target slots with an `active` multiplier but still read and evaluated every target row. On a zero-target frame, an uninitialized GPU target row containing NaN would produce `0 * NaN`, poisoning acceleration and then the ping-pong particle state.
Solution: Convert target count to an integer, `continue` before reading `_H8ParasiteTargets[i]` when `i >= targetCount`, validate active target fields, and reset final non-finite particle state before writing.
Rejected Alternatives: Trusting inactive multiplier masking, CPU-clearing the entire target buffer every empty frame, or downloading the particle buffer for CPU repair.
Scalability potential: Low executes the safety branch over fewer active particles; Middle/High/Ultra keep the same 16-target shader envelope while density scales continuously through `GlobalQualityWeight`.
Hardware Impact: Adds bounded branch/finite checks per active particle. This is accepted because it prevents persistent GPU NaN state without CPU readback.

## Decision 038 - Compute Kernels Must Fail Closed

Problem: Direct `FindKernel` calls can throw during `OnEnable` if the compute asset is missing a kernel or a scene binds the wrong shader, bypassing blackbox telemetry.
Solution: Use `ComputeShader.HasKernel` before `FindKernel` and leave missing kernels at `-1`; `DispatchAndRender` already routes those states to `TelemetryFlagNoCompute`.
Rejected Alternatives: Catching exceptions in the frame path, assuming the assigned asset is correct, or disabling telemetry on missing compute kernels.
Scalability potential: No tier behavior change. Low/Middle/High/Ultra all fail closed instead of crashing on bad content binding.
Hardware Impact: Cold path only; runtime impact is 0 us under valid content.

## Decision 039 - Build Guard Clear Does Not Equal Compile Proof

Problem: The latest workstation sample shows CPU load `43` and no `dotnet/csc/VBCSCompiler` process output, but generated `*.csproj/*.sln/*.slnx` files still contain no parasite assembly or script entries.
Solution: Do not launch `dotnet build`; keep Task 20 open until Unity imports the new asmdefs/scripts and regenerates project files, then compile from a proving target.
Rejected Alternatives: Running a stale external build and reporting it as SHINOBU_313 validation.
Scalability potential: No runtime behavior change. It preserves truthful verification state for every hardware tier.
Hardware Impact: Avoids false-positive compiler evidence and unnecessary I/O. Runtime impact is 0 us.

## Decision 040 - Scanner Regeneration Must Preserve Current Evidence

Problem: The hand-restored shared rendering report had updated shader-safety and compile-gate fields, but `Biological_Particle_Scanner.BuildSection` still generated the older strings. A future scanner run would silently erase the target-slot NaN fence proof and stale-project build truth.
Solution: Patch the scanner's generated SHINOBU_313 section to emit the current shader safety and compile status text.
Rejected Alternatives: Manually repairing the shared report after every scanner run or weakening the scanner to counters only.
Scalability potential: Editor-only evidence path; runtime tier behavior unchanged.
Hardware Impact: 0 runtime us.

## Decision 041 - BufferID Evidence Must Be Exact

Problem: The shared report and scanner text used shorthand `71980..71990`, but `71988` is intentionally unused after the dead mock-kinematics lane purge. Shorthand can make integrators believe the unused ID is occupied.
Solution: Keep the actual enum lanes unchanged and update report/scanner evidence to list `71980..71987 plus 71989,71990`. Re-audit showed SHINOBU_311 moved off the earlier collision range.
Rejected Alternatives: Reusing `71988` just to make the range contiguous, or leaving imprecise range documentation.
Scalability potential: No runtime tier behavior change; this is integration proof hygiene.
Hardware Impact: 0 runtime us.

## Decision 042 - New Unity Metas Need Importer Blocks

Problem: New parasite `.meta` files only contained `fileFormatVersion` and `guid`, while local first-party and third-party examples include explicit importer blocks for folders, asmdefs, scripts, compute shaders, shaders, and CSV text assets.
Solution: Preserve the existing GUIDs and add the matching importer block type to each SHINOBU_313 meta: `DefaultImporter`, `AssemblyDefinitionImporter`, `MonoImporter`, `ComputeShaderImporter`, `ShaderImporter`, and `TextScriptImporter`.
Rejected Alternatives: Letting Unity repair minimal metas during import, deleting/recreating metas, or changing GUIDs under concurrent agent work.
Scalability potential: No tier behavior change. This only reduces Unity import risk before compiler/runtime proof.
Hardware Impact: 0 runtime us. Import stability improvement only.

## Decision 043 - Draw Shader Must Avoid Fragile HLSL ABI

Problem: The procedural draw shader selected `ParasiteGpuParticleDTO` through a struct-valued ternary and passed `float4(world, 1.0)` to `UnityWorldToClipPos`; both are avoidable shader import risks before Unity can compile the new asset.
Solution: Use an explicit branch to read the active particle buffer and call `UnityWorldToClipPos(world)` with the `float3` world position.
Rejected Alternatives: Relying on backend-specific implicit struct/float4 conversions or waiting for Unity import to report a preventable source issue.
Scalability potential: No tier behavior change. This keeps the same indirect draw route from low through ultra particle budgets.
Hardware Impact: 0 CPU us. Shader import risk reduction only.

## Decision 044 - Zero Thermal Targets Must Draw Zero Parasites

Problem: With no active thermal targets, GPU particles stayed in dormant camera-local positions and still incremented the indirect visible instance count, creating false parasite swarms around the camera.
Solution: Gate `CS_CullParasites` liveness by the compute target count with `step(0.5, _H8ParasiteFrameParams0.w)`. Particle state remains resident for cheap reuse, but indirect instance count stays zero until a thermal target exists.
Rejected Alternatives: CPU-clearing particle buffers on empty target frames, destroying/reallocating `GraphicsBuffer`s, or accepting targetless visual noise.
Scalability potential: Low devices avoid all parasite quad/fragment work on empty frames; Middle/High/Ultra resume the same continuous `GlobalQualityWeight` particle budget as soon as targets exist.
Hardware Impact: One HLSL `step` and multiply per active particle during cull; saves indirect draw instances and fragment cost on zero-target frames. CPU impact remains 0 us.

## Decision 045 - BufferID Collision Evidence Must Not Imply 71988 Ownership

Problem: Two SHINOBU_311 proof lines still described parasite VFX ownership as the broad `71980..71990` range even though `71988` is intentionally unused after the mock-kinematics lane purge.
Solution: Patch only those cross-domain evidence strings to `71980..71987 plus 71989,71990`, matching `H8Memory.cs` and SHINOBU_313 ledger/report text.
Rejected Alternatives: Reusing `71988` for cosmetic contiguity, or leaving collision docs imprecise for integrators.
Scalability potential: No runtime behavior change. This preserves stable buffer identity across all quality tiers.
Hardware Impact: 0 runtime us. Integration ambiguity reduction only.

## Decision 046 - CSV Bytes Must Live In Vault Scratch

Problem: Runtime/editor CSV profile reload still used `File.ReadAllBytes`, creating a managed byte array for a task that explicitly requires `ReadOnlySpan<byte>` parsing into unmanaged Vault tables.
Solution: Read `parasite_behavior_profiles.csv` into `ShinobuParasiteCsvScratch` through `FileStream.Read(Span<byte>)`, then pass a pointer-backed `ReadOnlySpan<byte>` to `ParasiteSwarmContracts.LoadProfilesFromCsv`. The editor reload button now calls the same runtime bridge instead of allocating its own byte array. Files larger than the 16KB scratch cap fail closed instead of parsing a truncated row.
Rejected Alternatives: Keeping a cold `byte[]` because it was outside the frame loop, duplicating a second editor-only parser, or partially parsing oversized CSV content.
Scalability potential: Low/Middle/High/Ultra all keep the same profile ABI; designers can reload profiles without C# recompilation or managed CSV payload allocation.
Hardware Impact: Runtime frame impact remains 0 us. Cold reload avoids one managed byte-array allocation up to the 16KB scratch cap.

## Decision 047 - Overflow Telemetry Counts Eligible Signals

Problem: Target overflow telemetry used the already-staged candidate count, so dense thermal frames above the fixed candidate buffer capacity understated the real number of valid heat sources beyond the top-16 GPU target envelope.
Solution: `StageThermalSourceSignals` now scans the full `ThermalSourceSignal` snapshot, counts every eligible source, writes only up to `ShinobuParasiteTargetCandidates` capacity, and computes overflow from `eligibleSignalCount - MaxTargetCount`.
Rejected Alternatives: Reporting only staged candidate overflow, widening the 16-target GPU ABI, or adding a CPU sort/list allocation for all thermal sources.
Scalability potential: Low/Middle/High/Ultra keep the same shader target envelope; high-density scenes get truthful blackbox overflow evidence without increasing GPU target loop width.
Hardware Impact: CPU still scans only macro thermal signals, not particles. Extra work is one integer counter and capacity branch per valid signal; frame impact pending profiler.

## Decision 048 - Scanner Report Must Preserve CSV Evidence

Problem: The shared rendering report and the scanner-generated replacement section can drift; hand-patched evidence is not durable if the editor scanner rewrites the SHINOBU_313 section later.
Solution: Add the CSV scratch ingest proof and current compile-gate wording to both `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` and `Biological_Particle_Scanner.BuildSection`.
Rejected Alternatives: Updating only the JSON report or relying on chat/status memory.
Scalability potential: No runtime tier change. It keeps integrator-visible proof stable across low/middle/high/ultra tuning passes.
Hardware Impact: 0 runtime us. Editor/report-only string output.

## Decision 049 - GraphicsBuffer Locks Must Always Release

Problem: Local `GraphicsBuffer.LockBufferForWrite` upload paths must not leave mapped buffers locked across exceptions or device-loss edges.
Solution: Target and draw-params uploads use `try/finally`, set a local `locked` flag only after successful `LockBufferForWrite`, and unlock in `finally`. The dead empty-flow upload path was later removed entirely.
Rejected Alternatives: Relying on the fact that a one-row assignment is unlikely to throw, or keeping a mapped resource path without an explicit release fence.
Scalability potential: No quality-tier behavior change. Low/Middle/High/Ultra all use the same GPU upload fences; particle density and shader work still scale through `GlobalQualityWeight`.
Hardware Impact: Expected runtime cost is below profiler noise. It prevents mapped-buffer leakage and VRAM/editor instability during repeated Play Mode or device-loss cycles; exact runtime proof remains pending Unity import/profiler.

## Decision 050 - Metric Scanner Must Track Current Upload Proof

Problem: The shared rendering report and scanner-generated SHINOBU_313 section still described the older compile guard and omitted the new lock-release proof.
Solution: Add `bufferUploadSafety` to both the scanner output and `RENDERING_OPTIMIZATION_REPORT.json`, and update compile status to the latest guard sample: CPU load 54, no compiler process output, generated projects still stale.
Rejected Alternatives: Updating only `Status_SHINOBU_313.md`, which would let the next editor scanner overwrite the current evidence.
Scalability potential: No runtime tier behavior change. It preserves the same evidence route for low/middle/high/ultra validation.
Hardware Impact: 0 runtime us. Editor/report-only evidence synchronization.

## Decision 051 - Source Re-Audit Does Not Replace Unity Proof

Problem: Task 20 demands architecture verification, but generated Unity project files still omit the parasite assembly/scripts and no Unity import/shader import/profiler evidence exists.
Solution: Extend static proof only: DTO layout, Burst flags, `[NoAlias]`, asmdef references, and shader ABI scans were rerun. Task 20 remains open until Unity import regenerates project files and a legal compile/runtime/profiler pass includes this domain.
Rejected Alternatives: Treating static scans as completion, running stale `dotnet build`, or adding sibling Thermodynamics/KCC references to match the prompt text while breaking compile-wall law.
Scalability potential: No runtime behavior change. The verified code path still scales by `GlobalQualityWeight` and allocated GPU capacity.
Hardware Impact: 0 runtime us. Prevents false certification and compile-wall churn.

## Decision 052 - World Namespace Is A Compile-Wall Leak

Problem: `ParasiteSwarmGpuRuntime` imported `Hecton8.World` even though the runtime asmdef intentionally has no World-domain reference. The only apparent quality dependency is `HomeostasisBrain.GlobalQualityWeight`, which is owned by `Hecton8.Core`.
Solution: Remove the redundant `using Hecton8.World;` and re-run sibling namespace scans over parasite source and asmdefs.
Rejected Alternatives: Adding a World asmdef reference, leaving the unused namespace until Unity compile, or masking the leak in documentation.
Scalability potential: Runtime quality behavior is unchanged; `GlobalQualityWeight` still comes from Core and continuously scales particle budget/curl/dispatch count.
Hardware Impact: 0 runtime us. Reduces compile-wall and Unity regeneration failure risk.

## Decision 053 - Scanner Literals Must Not Pollute Source Gates

Problem: The editor scanner generated correct report text but embedded exact forbidden GPU copy/readback API names inside a string literal, causing source-level grep gates to report a false hit under the parasite folder.
Solution: Rewrite the generated evidence string to describe the same route as "CPU-side GPU payload copy/readback" without naming the forbidden APIs in scanner source; re-run the forbidden-token scan.
Rejected Alternatives: Weakening the scanner pattern, documenting the hit as harmless, or keeping noisy source evidence.
Scalability potential: No runtime behavior change. Clean static gates protect every quality tier from regression masking.
Hardware Impact: 0 runtime us. Reduces integration verification noise.

## Decision 054 - Idle CPU Does Not Prove Stale Projects

Problem: The latest build guard sample is below the CPU threshold and has no compiler process, but generated Unity project files still contain no parasite assembly/script entries.
Solution: Keep `dotnet build` withheld and refresh the status/report/scanner evidence to CPU load 43 with stale generated project files. A proving compile requires Unity import/project regeneration first.
Rejected Alternatives: Running `dotnet build` because CPU is temporarily legal, then reporting a stale project build that does not compile SHINOBU_313 files.
Scalability potential: No runtime behavior change. This preserves truthful low/middle/high/ultra verification state instead of certifying code outside the current generated solution.
Hardware Impact: 0 runtime us. Avoids false-positive compiler evidence and unnecessary I/O.

## Decision 055 - Remove Dead Empty Flow Structured Buffer

Problem: The compute shader declared `_H8AbyssalFlowBuffer` and the runtime allocated/uploaded `_emptyFlowBuffer`, but advection actually samples `_H8AbyssalFlowField` only. The structured buffer was dead renderer memory and stale proof noise.
Solution: Remove the unused HLSL buffer declaration, runtime field, cold allocation, upload, binding, and disposal path. The cheap fallback remains a 1x1 `Texture3D` because the shader samples a texture, not a structured buffer.
Rejected Alternatives: Keeping the buffer because it was cold, or wiring a second flow route into advection just to justify an already-dead allocation.
Scalability potential: Low devices stop paying for a pointless fallback buffer; Middle/High/Ultra keep the same flow-field texture path for richer GPU advection when an authored volume exists.
Hardware Impact: Removes one cold `GraphicsBuffer` allocation and one cold upload. Runtime frame cost is unchanged; startup/import resource pressure is lower.

## Decision 056 - Top-N Must Rank By The Score It Computes

Problem: `ExtractParasiteTargetsJob` paid to compute a heat/radius/proximity score, but `SelectTopParasiteTargetsJob` inserted targets by `ThermalSignature` only, discarding the proximity and radius terms.
Solution: Keep a stack-local fixed 16-float score lane inside the Burst job and shift scores alongside `ParasiteTargetDTO` rows during insertion. No managed array, `NativeList`, or persistent scratch lane is introduced.
Rejected Alternatives: Widening `ParasiteTargetDTO` with score, sorting a managed list, or keeping heat-only ranking. Widening the GPU DTO would waste shader bandwidth; managed sorting violates the hot path.
Scalability potential: Low/Middle/High/Ultra all keep the same 16-target shader envelope; ranking quality improves without raising GPU target-loop width.
Hardware Impact: Same bounded O(16 * candidateCount) selection. The extra stack scores are 64 bytes per job execution and avoid wasting GPU particles on distant hot targets when closer/radius-better targets exist.

## Decision 057 - Active Compiler Guard Supersedes Idle CPU Sample

Problem: After the score patch, CPU load dropped to 25 percent, but multiple `dotnet` processes and `VBCSCompiler` are active, and generated project files still omit SHINOBU_313 files.
Solution: Keep compile withheld and refresh scanner/report/status compile evidence to the active-compiler plus stale-project blocker.
Rejected Alternatives: Starting another build because CPU is low, or reporting a build target that does not include the parasite assembly/scripts.
Scalability potential: No runtime behavior change. Verification state stays truthful across all hardware tiers.
Hardware Impact: 0 runtime us. Avoids concurrent compiler contention and false-positive compile proof.

## Decision 058 - Runtime Visual Phase Must Not Depend On Unity Frame Clock

Problem: Parasite compute time, mock target phase, telemetry frame, and telemetry hash were derived from `Time.frameCount`, which is a Unity presentation clock and grows without a bounded shader phase.
Solution: Add a runtime-owned private fixed-step visual counter, pass the same `visualFrame`/`visualPhaseRadians` through dispatch, mock extraction, and telemetry, and wrap shader phase through a 4096-tick ramp.
Rejected Alternatives: Keeping `Time.frameCount`, using `Time.time`, or widening the telemetry DTO for an additional clock lane. The DTO ABI stays stable and runtime truth remains presentation-only.
Scalability potential: Low/Middle/High/Ultra keep identical authority and buffer layouts; only phase precision and clock isolation improve.
Hardware Impact: 0 meaningful CPU cost. Removes Unity clock dependency from runtime advection/telemetry and prevents long-session large-float phase precision decay.

## Decision 059 - Per-Particle Curl Should Not Spend Native Trig SFU

Problem: The compute shader used native `sin`/`cos` in curl and dormant particle placement, burning SFU bandwidth in the per-particle path on weak GPUs.
Solution: Replace native shader trig with bounded polynomial `H8FastSin` / `H8FastCos`; mirror the same approximation in Burst/C# fallback target generation and thermal-source velocity synthesis.
Rejected Alternatives: Keeping exact transcendental functions for a visual fake, adding lookup textures that need streaming/warmup, or moving curl back to CPU.
Scalability potential: Low devices get cheaper advection while `GlobalQualityWeight` still controls octave count and particle budget; Middle/High/Ultra spend saved bandwidth on density/flow/detail without changing DTOs or authority.
Hardware Impact: Expected GPU SFU pressure reduction in `CS_AdvectParasites`; exact microseconds pending Unity profiler. CPU fallback top-16 target generation also drops standard trig calls.

## Decision 060 - Empty Thermal Frames Must Not Run Full Advection

Problem: Zero-target frames suppressed visible indirect instances, but runtime still dispatched full-budget rebase/advection/cull before draw suppression. The formula estimator could also flag a GPU budget spike and trigger a dump even when no parasites were visible.
Solution: Compute an explicit `dispatchedParticleBudget` of zero when target count is zero; `DispatchAndRender` then dispatches only `CS_ClearArgs`, returns before rebase/advection/cull/draw, and telemetry records the zero dispatched budget. `EstimateGpuMicroseconds` now returns a clear-only value for zero budget or zero targets.
Rejected Alternatives: Keeping GPU particles warm through full advection on empty scenes, clearing/rewriting particle buffers from CPU, or treating cull-only suppression as enough.
Scalability potential: Low/Middle/High/Ultra all keep the same target authority and DTO layout; empty thermal scenes now collapse to one tiny clear dispatch regardless of configured maximum particle capacity.
Hardware Impact: Saves all per-particle compute on targetless frames. At 500k configured default this removes the advection/cull group workload when there is no thermal attraction source; exact microseconds pending Unity profiler.

## Decision 061 - Upload Fences Need Alternate GPU Payload Buffers

Problem: Target and draw-param uploads used release-safe `LockBufferForWrite`, but still wrote into the same GPU buffers consumed by the previous dispatch/draw, risking driver synchronization stalls.
Solution: Allocate target and draw-param payloads as ping-pong `GraphicsBuffer` pairs. Uploads write the alternate buffer and flip parity only after successful locked upload/unlock; compute/draw bind the current uploaded buffer.
Rejected Alternatives: Keeping single-buffer uploads because they are small, using CPU readback fences, or allocating transient buffers per frame. Per-frame allocation/readback is worse than the original stall risk.
Scalability potential: Low/Middle/High/Ultra keep the same DTO layout and shader ABI; only the GPU payload route gains one-frame-safe upload buffers.
Hardware Impact: Adds one 16-row target buffer and one one-row draw-param buffer. Memory cost is tiny; expected benefit is reduced CPU/GPU sync stalls from mapped writes. Exact stall savings pending Unity profiler.

## Decision 062 - Blackbox Rows Need A Fixed Header

Problem: `Dump_SHINOBU_313.bin` wrote raw telemetry rows only, so QA had no canonical row stride, row count, cursor, version, or payload size inside the dump file.
Solution: Prepend a 64-byte little-endian `H8P3` header with version, header bytes, `SwarmTelemetryEntry` stride, row count, post-write cursor, and payload byte count before writing the fixed telemetry rows.
Rejected Alternatives: Keeping headerless rows, adding JSON sidecars, or writing managed metadata strings into the dump. Sidecars can drift and strings are unnecessary for fixed ABI forensics.
Scalability potential: No quality-tier behavior change. The dump format is independent of `GlobalQualityWeight`.
Hardware Impact: Fault path only. Adds one 64-byte stack header write when a dump is already being emitted; 0 steady-frame cost.

## Decision 063 - Compute Frame Uniforms Need One ABI Row

Problem: Per-frame compute values were sent through three loose vector-param writes, leaving no explicit grouped ABI row for frame timing, quality, attraction, curl, flow, and latch tuning.
Solution: Add explicit 64-byte `ParasiteFrameParamsDTO`, upload it through ping-pong `GraphicsBuffer` rows, and bind that row to init/rebase/advect/cull kernels. HLSL reads `_H8ParasiteFrameParams[0].Frame0..Frame2`.
Rejected Alternatives: Keeping loose global vector params, adding shader variants, or packing the values into target rows. Target rows should remain thermal facts only.
Scalability potential: Low/Middle/High/Ultra retain identical quality math and DTO authority; the uniform route is now one stable GPU payload row.
Hardware Impact: One 64-byte mapped upload per active compute frame. Expected driver-state simplification; exact microseconds pending Unity profiler.

## Decision 064 - Rebase Kernel Must Bind The Frame Params Row

Problem: `CS_RebaseParasites` reads `_H8ParasiteFrameParams0.z` for the particle budget after the frame-param ABI change, but the first grouped binding pass covered init/advect/cull only.
Solution: Bind `_H8ParasiteFrameParams` to `_rebaseKernel` before AUP-shift dispatch, using the same uploaded 64-byte row as the active frame.
Rejected Alternatives: Depending on previous kernel resource state, duplicating particle budget as a separate vector, or widening the rebase command path with another loose scalar.
Scalability potential: Low/Middle/High/Ultra keep identical AUP rebase math and buffer layouts; only the resource-binding proof is closed.
Hardware Impact: One compute buffer bind only on AUP-shift frames. Exact cost is below profiler resolution; correctness risk is removed.

## Decision 065 - Missing Rebase Kernel Must Disable Active Compute

Problem: After the frame-param ABI hardening, a missing `CS_RebaseParasites` kernel could still leave init/advect/cull available. That would make AUP-shift frames unsafe because particles would continue in stale local space.
Solution: Treat `_rebaseKernel < 0` as a no-compute resource failure in `DispatchAndRender`, matching the route card that rebase is mandatory for active dispatch.
Rejected Alternatives: Skipping rebase only when shifts occur, trusting static scene origin, or drawing stale particle-local coordinates until the next initialization. A 100km AUP renderer cannot be correct without the rebase lane.
Scalability potential: Low/Middle/High/Ultra keep identical particle budgets and quality curves; missing kernel scenes fail closed instead of producing false visuals.
Hardware Impact: 0 steady-frame cost in valid scenes. In invalid scenes, saves all parasite compute dispatches and records no-compute telemetry rather than burning GPU work on an unsafe path.

## Decision 066 - Target Ranking Does Not Need Scalar Sqrt

Problem: `ExtractParasiteTargetsJob` used a scalar square root for target distance even though the kernel only needs range rejection and a visual ranking weight.
Solution: Compute `distanceSq` once in double precision, reject non-finite/out-of-range candidates, cast the guarded local value to float, and derive the rank proxy with `distanceSq * math.rsqrt(distanceSq)`.
Rejected Alternatives: Keeping `math.sqrt`, comparing exact distance for a visual-only score, or moving ranking to GPU. The CPU job only selects top macro-targets; exact Euclidean length is not gameplay truth.
Scalability potential: Low devices remove a scalar sqrt from the target extraction lane; Middle/High/Ultra retain the same top-16 target envelope and can spend saved CPU budget on richer GPU density.
Hardware Impact: Small CPU target-scan gain; exact ns/sample requires Burst player benchmark. Static gate now has no `math.sqrt`, `math.length`, `.magnitude`, or `Vector3.Distance` hit in parasite runtime C#.

## Decision 067 - Runtime Must Not Create A Fallback Material

Problem: `ResolveMaterial` performed a cold `Shader.Find` and could allocate an owned fallback `Material` when the serialized parasite material was missing. That is a managed runtime object path and shader lookup in a resource-misconfigured scene.
Solution: Delete the fallback lookup/material route. `parasiteMaterial` is now the only draw resource; if it is missing, `DispatchAndRender` takes the no-compute path and avoids GPU work that cannot be drawn.
Rejected Alternatives: Keeping one-shot lookup, precreating a material in code during `OnEnable`, or running compute without a draw material. Rendering assets must be assigned/imported, not synthesized by gameplay code.
Scalability potential: Low/Middle/High/Ultra keep the same shader ABI and `GlobalQualityWeight` curve when configured. Misconfigured scenes now fail closed consistently across tiers.
Hardware Impact: Configured scenes: 0 us change. Misconfigured scenes: avoids one managed material allocation/search and all parasite compute dispatches because no drawable resource exists.

## Decision 068 - Build Guard Remains Hard-Blocked

Problem: After the resource and sqrt polish, local CPU load sampled at 99 percent and generated project files still contain no SHINOBU_313 assembly/script entries.
Solution: Do not launch `dotnet build`; record static scans, JSON parse, and stale-project state instead. A proving compile requires Unity import/project regeneration and CPU below the guard threshold.
Rejected Alternatives: Starting a build under heavy CPU load, or reporting a build of generated projects that do not include this domain.
Scalability potential: No runtime behavior change. Verification stays truthful for every hardware tier.
Hardware Impact: 0 runtime us. Avoids workstation contention and false compile evidence.

## Decision 069 - ABI Helpers Must Admit Only Unmanaged Payloads

Problem: `CreateStructuredBuffer<T>`, `TryReadHandle<T>`, and `TryResolveHandle<T>` used `where T : struct`, which would technically admit managed-reference structs if someone reused the helpers incorrectly later.
Solution: Tighten those generic constraints to `where T : unmanaged`; all current payloads (`ParasiteGpuParticleDTO`, `ParasiteTargetDTO`, `uint`, `int`, `float4`, `ParasiteFrameParamsDTO`, tuning/telemetry DTOs) already satisfy it.
Rejected Alternatives: Trusting call sites or adding runtime reflection checks. GPU ABI safety belongs at compile time.
Scalability potential: No quality-tier behavior change. Prevents future accidental managed payloads from entering the GPU route.
Hardware Impact: 0 runtime us; compile-time constraint only.

## Decision 070 - Self-Audit Belongs On Disk, Not Chat

Problem: The task requires a forensic self-audit, but chat output is volatile and cannot serve as stable project evidence.
Solution: Add `Docs/Reports/SHINOBU_313_SELF_AUDIT.xml` with all 20 task results, primary DTO offsets, Vault BufferIDs, Dear Lie Big-O, dependency graph, and explicit pending Unity/compiler/profiler proof.
Rejected Alternatives: Waiting for final chat output, or marking Task 20 as satisfied without Unity import/runtime evidence.
Scalability potential: No runtime behavior change. The artifact records low-tier fail-closed behavior and ultra-tier density scaling in the same route.
Hardware Impact: 0 runtime us; documentation/proof artifact only.

## Decision 071 - Camera AUP Must Come From Player Snapshot Only

Problem: The runtime kept a fallback path that reconstructed camera AUP from `renderCamera.transform.position` plus a cached runtime origin. It also allowed active compute with a null render camera, which can make an indirect draw target ambiguous.
Solution: Remove the scene transform fallback. `TryResolveCameraAup` now succeeds only through cached `IPlayerRuntimeContext.TryGetPlayerPoseSnapshot`; `ResolveAupShiftSignals` only accumulates GPU rebase delta and no longer mutates a local runtime-origin cache. Active compute also requires `renderCamera != null`.
Rejected Alternatives: Keeping the camera transform fallback for convenience, polling `GlobalSignals.CurrentRuntimeOriginAup` in the visual loop, maintaining a VFX-owned origin shadow, or relying on null-camera draw behavior. Player pose/AUP has one owner and one snapshot route.
Scalability potential: Low/Middle/High/Ultra keep identical particle budgets and shader ABI; the input authority route is cleaner under every quality setting.
Hardware Impact: Removes a hot Transform property read in fallback configurations, deletes shadow-origin mutation, and suppresses compute/draw in null-camera scenes. Measured us pending Unity profiler; correctness benefit is stronger than the micro-cost.
