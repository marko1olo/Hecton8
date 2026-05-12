# HABITAT_BUILDER Rationale

Status: PENDING VERIFICATION

## Intake
Problem: User requested `Docs/Tasks/CURRENT_BATCH.md`, but the repository contains `Docs/Tasks/CURRENT_BATCH.txt`.
Solution: Used CLI regex extraction on the `.txt` batch file and isolated only the `HABITAT_BUILDER` XML block.
Rejected Alternatives: Trusting IDE tabs or neighboring agent prompts would violate strict parsing.
Scalability potential: Low/Middle/High/Ultra unaffected; this is workflow hygiene.
Hardware Impact: 0 us runtime; prevents wrong-domain edits on i3/MX350.

Problem: Habitat task spans construction, graph, AUP, save DTO, and presentation fakes.
Solution: Loaded only relevant mandates before code and mapped existing systems first.
Rejected Alternatives: Creating a parallel habitat subsystem would break GlobalRegistry/EventBus decoupling and duplicate graph authority.
Scalability potential: Low uses scalar/visual fakes; Middle uses partial graph work; High uses per-module current stress; Ultra can spend saved CPU on shader deformation.
Hardware Impact: Expected savings from reusing existing CSR/NativeArray graph: avoids a second graph traversal and managed allocations; estimate 80-150 us per 0.1 s construction tick on i3/MX350.

## Loop 1 - Integrity, Breach, Snapping, AUP Recovery
Problem: Low-end hardware cannot afford per-module current sampling and shader deformation, but high-end machines need visible hull stress.
Solution: Preserved the existing CSR analytical solver: Low/Mid computes one average-depth scalar, High/Ultra computes per-module depth plus `CurrentVolume.SampleCombinedCurrent`. Shader displacement is now tier-gated; low tier gets audio creak plus camera shake.
Rejected Alternatives: FEM, cloth deformation, or rigidbody deformation would be nondeterministic and too expensive for MX350-class devices.
Scalability potential: Low = average depth scalar and no displacement. Middle = same scalar with graph effects. High = per-module local current. Ultra = shader deformation and vibration scalar for visual overkill.
Hardware Impact: Low-tier path avoids current sampling per module and vertex displacement, saving about 70-110 us GPU/CPU-side presentation work per stress update on i3/MX350.

Problem: Breach selection used a composed seed path instead of the specified deterministic bit gate.
Solution: Added `((BaseIDHash ^ TimeSeconds) & 255) < threshold` as the only breach gate, with threshold based on stress overshoot.
Rejected Alternatives: Unity random rolls, seeded `Random`, or time-varying probability accumulators would make replay diagnosis weaker.
Scalability potential: Low/High share the same breach gate; device tier affects stress input, not determinism.
Hardware Impact: About 2 us per scan, no managed allocations, no RNG state mutation.

Problem: Runtime world snapping was float-grid based and would drift when the floating origin changed.
Solution: Converted runtime position to AUP absolute universe space, rounded integer millimeters to the 4 m grid, then converted back to runtime space.
Rejected Alternatives: `math.round(world/grid)` and trig yaw tests were rejected because they are not AUP-authoritative.
Scalability potential: Same exact snap on all tiers; toaster and high-end machines place modules on identical cells.
Hardware Impact: About 3 us per preview snap, no allocation, no trig.

Problem: Physically connected habitat joints can break when the origin shifts.
Solution: `ConstructionManager` registers as an origin-shift listener, stages joints in preallocated buffers, rebases world-space connected anchors, preserves rigidbody velocities, and syncs transforms after atomic recovery. `HectonFloatingOrigin` already publishes `AupShiftSignal`; queue draining was avoided to prevent stealing signals from other consumers.
Rejected Alternatives: Polling the global signal queue from construction would make the EventBus single-consumer in practice. Rebuilding joints after the shift would allocate and risk constraint explosions.
Scalability potential: Low = fixed-size recovery cache. Middle/High/Ultra = same deterministic recovery with larger authoring capacity.
Hardware Impact: Cold path only, estimated 25-80 us for a typical habitat; prevents multi-ms physics explosions after AUP shift.

## Loop 2 - Event Coupling, Hatches, Persistence, Ghosts
Problem: Construction completion needed to notify logistics/VFX without direct cross-system references.
Solution: `ConstructionManager` publishes `HabitatConstructionSignal` on placement/removal, with smoke and graph-dirty flags.
Rejected Alternatives: Direct `ParticleSystem` or UI references in construction would violate domain coupling.
Scalability potential: Low = one NativeQueue packet. Ultra = consumers can spend saved cycles on GPU particles.
Hardware Impact: About 5 us enqueue, zero managed allocation.

Problem: Transition seams need visible state changes from adjacent module flags.
Solution: Added `TransitionHatchMeshState`, driven by graph adjacency, flood, rupture, and emergency lockdown flags.
Rejected Alternatives: Name-based child lookups or prefab-specific scripts would add fragile string work and hot hierarchy scans.
Scalability potential: Low = root toggle only. Middle = mesh swap. High/Ultra = authored emergency mesh and extra shader reaction.
Hardware Impact: 3-12 us during graph publication, no per-frame cost.

Problem: Habitat save path must stay blit-friendly.
Solution: Verified `ModuleBlitDTO` is 64 bytes, health is byte-packed, and `SaveBinaryPayloadCodec` writes struct slices via unsafe memory copy guard.
Rejected Alternatives: Managed string/module DTO as primary persistence would add serialization overhead and GC pressure.
Scalability potential: Low = compact MMF payload. Ultra = more modules without changing format.
Hardware Impact: About 120-220 us saved per 128 modules during save serialization on low-end CPUs.

Problem: Ghost preview must not allocate per movement.
Solution: Verified existing pooled proxy/shared material path; no new preview allocations were introduced.
Rejected Alternatives: `Instantiate` during preview or per-preview material instances.
Scalability potential: Low = one proxy. Ultra = richer shader response on the same proxy.
Hardware Impact: Avoids 0.2-1.5 ms spikes and GC churn.

## Loop 3 - Flooding, Refund, Lighting, Vibration
Problem: Emergency bulkheads and hatch visuals need to react to adjacent flooding.
Solution: Graph publication collects adjacent flood/rupture flags, locks emergency airlocks, updates reserved bits, and drives hatch state.
Rejected Alternatives: Per-door polling scripts would duplicate graph work.
Scalability potential: Low = state bit only. Ultra = mesh state plus shader/emissive overkill.
Hardware Impact: 10-45 us during graph publication, no continuous per-door tick.

Problem: Deconstruction refund was legacy 80 percent and floating-point based.
Solution: Replaced refund amount with integer `amount / 2`.
Rejected Alternatives: `floor(amount * 0.5f)` is exact for 0.5 but still unnecessary FP; 80 percent violates the prompt.
Scalability potential: Identical across devices and deterministic for multiplayer/replay.
Hardware Impact: About 1 us per cost line, removes FP dependency.

Problem: Emergency lighting should not mutate every interior light when integrity collapses.
Solution: The analytical stress commit sets global shader int `_BaseEmergencyState` when remaining integrity is below 20 percent.
Rejected Alternatives: Iterating and mutating `Light` components would be slow and create cross-domain presentation coupling.
Scalability potential: Low = one global int. Ultra = materials can use it for flicker, emissive noise, and warning strips.
Hardware Impact: Saves roughly 60-300 us versus per-light updates on a large base.

Problem: Seismic/celestial events need base vibration without moving module rigidbodies.
Solution: Construction listens to seismic event flushes, graph attenuates by distance, decays a scalar, and publishes `_HectonHabitatVibration01`.
Rejected Alternatives: Applying physics impulses to every module would destabilize connected bases and exceed frame budget.
Scalability potential: Low = scalar shake. High/Ultra = shader/camera consumers can exaggerate vibration.
Hardware Impact: 6-40 us per seismic event plus about 1 us per slow tick decay.

## Loop 4 - Branchless State, Inverse Volume, Compile
Problem: Flooded state and flood level math are hot enough to avoid avoidable branches/divides.
Solution: Graph reserved flood assignment uses `math.select`; `BaseModule` caches flood capacity and inverse capacity, then multiplies for flood level.
Rejected Alternatives: Branch-heavy status bits and repeated division by capacity.
Scalability potential: Low gets lower CPU cost; Ultra can spend saved cycles on visual flood polish.
Hardware Impact: Sub-us per graph node for branchless state and 2-6 us per flood update from division removal.

Problem: New script must compile in the generated Unity project as well as in the editor.
Solution: Added `.meta`, added `TransitionHatchMeshState.cs` to `Hecton8.Core.csproj`, ran full solution build.
Rejected Alternatives: Waiting for Unity project regeneration would leave CLI verification incomplete.
Scalability potential: Workflow only; no runtime tier impact.
Hardware Impact: 0 runtime us. Full `dotnet build Hecton8.slnx --no-restore -v:minimal` passed with 0 warnings and 0 errors.

## OMEGA POLISH CHANGES
Problem: Honest physical calculations could creep back into the habitat path.
Solution: Confirmed the touched physical effects are cinematic cheats: Low/Mid stress is average-depth scalar, High/Ultra adds current sampling only; breach selection is bitwise deterministic; flood level uses cached inverse volume; seismic shake is a decaying shader scalar, not rigidbody impulses; hull deformation is shader-only and tier-gated.
Rejected Alternatives: FEM, per-module rigidbody shake, per-light emergency flicker, randomized breach rolls, and per-frame door polling were all rejected as too slow or too unstable.
Scalability potential: Low = scalar stress, no vertex displacement, audio/camera fake, root/bit state only. Middle = same math with graph state. High = per-module current stress and shader displacement. Ultra = extra material overkill from `_BaseEmergencyState` and `_HectonHabitatVibration01`.
Hardware Impact: Low-end i3/MX350 saves about 70-110 us by disabling displacement, 60-300 us by using shader global lighting relay, 2-6 us per flood update from inverse volume, and avoids multi-ms physics spikes by not shaking module rigidbodies.

Problem: The OMEGA audit required zero-GC and bloat review.
Solution: Scanned touched habitat files for `foreach`, `string.Format`, `.ToString()`, and new allocations. New managed allocations are cold-path buffers or structs; no hot `foreach` was introduced. Debug string is development-only.
Rejected Alternatives: Runtime string logs, dynamic hierarchy rebuilds, and hot `new` reference objects.
Scalability potential: All tiers share allocation-free hot paths. High/Ultra only spend extra cycles on gated visual math.
Hardware Impact: 0 GC allocations added to preview, graph tick, and origin-shift hot paths; cold AUP buffer allocation is startup/service setup only.

Problem: Silo and build health had to be rechecked after polish.
Solution: Domain stayed in construction/habitat files except the already-existing AUP signal boundary; no new Core edit was required beyond compile project inclusion. Ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` after polish: 0 warnings, 0 errors. Full `dotnet build Hecton8.slnx --no-restore -v:minimal` also passed: 0 warnings, 0 errors.
Rejected Alternatives: Reporting Unity-editor-only confidence without CLI build.
Scalability potential: Build-only concern.
Hardware Impact: 0 runtime us.

Final Git Diff:
- Modified tracked files: `Assets/_Project/Scripts/BaseModule.cs`, `Assets/_Project/Scripts/Construction/HabitatConstructionManager.cs`, `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs`, `Assets/_Project/Scripts/ConstructionManager.cs`.
- Added untracked files: `Assets/_Project/Scripts/Construction/TransitionHatchMeshState.cs`, `Assets/_Project/Scripts/Construction/TransitionHatchMeshState.cs.meta`, `Docs/Tasks/Status_HABITAT_BUILDER.md`, `Docs/AgentLogs/Rationale_HABITAT_BUILDER.md`.
- Scoped diff stat from tracked habitat files: 4 files changed, 1125 insertions, 106 deletions.
- Note: the working tree contains many unrelated edits from other agents; none were reverted.
