# Rationale_MAELSTROM_KINEMATICS

Status: PENDING VERIFICATION

## Decision 0 - Authority Route
Problem: The prompt requests `AnomalySpawnedSignal(Maelstrom)`, but `GlobalSignals.cs` is already dirty before this agent touched it. Editing it would risk overwriting another agent/user change.
Solution: Use existing `HectonFluidEngine` analytical flow ownership for the first implementation pass and avoid dirty global signal mutation. If a new signal lane is unavoidable, add it only after inspecting the dirty diff.
Rejected Alternatives: Adding a new `WhirlpoolManager.Instance` violates singleton eradication. Editing dirty `GlobalSignals.cs` blindly violates shared-worktree safety. Collider triggers/AreaEffectors violate deterministic physics rules.
Scalability potential: Low = one maelstrom, suction only. Middle = two maelstroms, suction plus tangent. High = richer tangent and visual warp. Ultra = stronger GPU particle swirl and post warp without extra PhysX.
Hardware Impact: On i3/MX350, replacing trigger stay/AddForce with 1-2 squared-distance samples avoids broadphase and managed callback churn; expected hot-path cost is microseconds, not tenths of a millisecond.

## Decision 1 - Visual Fake First
Problem: A physically simulated whirlpool would invite trigger volumes, per-body PhysX force application, and unpredictable solver outcomes.
Solution: Treat maelstroms as a deterministic field: kinematics sample a bounded array, VFX/audio/post process sell the phenomenon, and only event-horizon damage becomes gameplay truth.
Rejected Alternatives: Per-particle water simulation, AreaEffector, PointEffector, and OnTriggerStay all spend CPU on invisible causes instead of visible player belief.
Scalability potential: Low = mathematical suction and sparse particle swirl. Middle = tangent velocity and rumble. High = spiral UV warp and boid panic. Ultra = dense particle vortex and stronger distortion.
Hardware Impact: Low-end avoids trigger callbacks and per-rigidbody solver noise; top-tier spends saved CPU/GPU headroom on marine snow density and visor distortion.

## Decision 2 - Dual Runtime Shape
Problem: The prompt requires a compact `NativeArray<float4>` for active maelstroms, but player/submarine jobs need pull, spin, vertical force, radius, and event-horizon metadata without unpacking magic constants from one float.
Solution: Publish both shapes from the same HectonFluidEngine authority: compact `float4` for GPU/boids, full `NativeArray<WhirlpoolFlow>` for kinematic jobs. Both are fixed capacity and rebased on origin shift.
Rejected Alternatives: Encoding every parameter into `float4.w` would hide gameplay-critical math and break escape-velocity tuning. Allocating a managed list per consumer violates zero-GC.
Scalability potential: Low = one compact float4 and one WhirlpoolFlow sample. Middle = two samples. High = richer tangent without additional API churn. Ultra = same authority feeding stronger GPU visual passes.
Hardware Impact: On i3/MX350, keeping the CPU path full-struct avoids per-frame decoding and keeps branch count predictable; expected cost is a few microseconds for one controlled body.

## Decision 3 - Authority-Safe Force Application
Problem: The task demands suction and tangent force but project physics rules forbid random direct Rigidbody.AddForce calls.
Solution: Player velocity is adjusted inside PlayerKinematicsBodyJob. Submarine maelstrom acceleration is emitted through PhysicsForceRouter.QueueAmbientForce after PID job completion.
Rejected Alternatives: Direct Rigidbody.AddForce in HectonFluidEngine or a trigger callback would bypass the packet authority and produce non-deterministic solver outcomes.
Scalability potential: Low = suction-only velocity delta. Middle = tangent plus suction. High = stronger visual/audio response while physical acceleration remains clamped. Ultra = additional presentation distortion, not extra solver truth.
Hardware Impact: On low-end silicon, this removes PhysX broadphase/callback overhead. On high-end, saved CPU can buy denser marine snow and stronger post warp without adding physical bodies.

## Decision 4 - Event Horizon Damage Lane
Problem: A maelstrom center needs gameplay truth without concrete health references or per-target coupling.
Solution: HectonFluidEngine checks player/submarine event-horizon proximity on a slow cadence and publishes Core.Signals.CombatDamageSignal with Pressure damage.
Rejected Alternatives: Calling player health, hull integrity, or submarine modules directly would cross domain boundaries and break event-bus discipline. Per-frame damage would waste CPU and spam listeners.
Scalability potential: Low = same event cadence and lower visual detail. Middle/High/Ultra = unchanged damage truth; only presentation intensifies.
Hardware Impact: Rare signal publication is cheaper than permanent trigger volumes. Low-end impact is below measurable frame budget without profiler proof.

## Decision 5 - Service Lookup Hygiene
Problem: Initial submarine/fauna hooks introduced new GlobalRegistry.Fluid reads inside scheduled runtime paths.
Solution: Submarine caches `_fluid` in cold reference binding and refreshes on SlowTick if missing. Sargassum caches `_fluidEngine` through its existing ResolveDependencies path; the new maelstrom threat reader uses the cached field.
Rejected Alternatives: Leave new hot-path registry reads or build a new dependency injection layer during a crowded batch. Both add risk outside the prompt.
Scalability potential: Low/Middle/High/Ultra all share the same cached service path; tier differences stay in math and visual density.
Hardware Impact: Saves repeated registry property access in PID/threat refresh code and keeps low-end behavior stable under many agents' systems.

## Decision 6 - Compile Wall Handling
Problem: Unity MCP validation is unavailable and local `dotnet build Hecton8.Core.csproj` fails before a clean maelstrom verdict on missing unrelated contracts/namespaces.
Solution: Apply the 3-strikes protocol: do not repair unrelated Core/Audio/Inventory/World contracts from a locomotion prompt; mark OMEGA COMPILE CHECK as dependency-blocked and document exact blockers.
Rejected Alternatives: Editing GlobalSignals/Core contracts blindly, creating fake stubs, or reverting unrelated dirty files would be architectural sabotage in a shared batch.
Scalability potential: No runtime tier impact; compile wall is integration dependency.
Hardware Impact: None until dependency wall is cleared and Unity can import/compile.

## Decision 7 - Low-Tier Strongest Selection
Problem: The first low-tier pass capped the loop to slot 0, which could suppress an active stronger maelstrom in slot 1.
Solution: Scan the fixed two-slot analytical buffer, validate each whirlpool, and publish/sample only the strongest one on Low/MX350. High tier still samples both slots.
Rejected Alternatives: Keep slot-0-only logic for minimum CPU cost, or sort active maelstroms every frame. Slot starvation breaks authoring control; sorting is wasted for capacity two.
Scalability potential: Low = one strongest suction-only field. Middle = two fields if tier allows. High = suction plus tangent on both. Ultra = same gameplay truth with heavier GPU/visor presentation.
Hardware Impact: i3/MX350 pays a two-entry scalar scan and saves tangent/extra sample work. Expected net remains microseconds while preserving visible hazard priority.

## Decision 8 - Presentation Bandwidth Discipline
Problem: The GPU/presentation side could re-upload unchanged maelstrom data and the visor feature still had a maelstrom-specific GlobalRegistry lookup in the render path.
Solution: Add maelstrom upload hashing, double-buffer the marine snow GraphicsBuffer, throttle cached fluid binding in marine snow and visor, and replace shader scalar-swizzle zero literals with explicit typed zeros.
Rejected Alternatives: Upload every frame, rely on backend-specific scalar swizzle parsing, or introduce a new render feature dependency contract during a shared batch.
Scalability potential: Low = cheap single payload and no redundant upload. Middle/High = unchanged data skips CPU driver work. Ultra = double buffer allows stronger particle counts without blocking on same-buffer hazards.
Hardware Impact: On i3/MX350, unchanged payloads avoid estimated 3-20 us CPU driver overhead and one render-path registry access. High-end spends the saved bandwidth on denser snow or stronger post distortion.

## OMEGA POLISH CHANGES
Problem: Omega pass required an anti-bloat audit after all core tasks were done or blocked.
Solution: Diff-only scan found no added `math.sqrt`, unconditional `math.normalize`, `foreach`, LINQ, `ToArray`, string interpolation, `string.Format`, or `.ToString()` in the maelstrom diff. `git diff --check` is clean except line-ending conversion warnings. One new submarine Fluid lookup and one new fauna Fluid lookup were moved to cached cold/SlowTick dependency paths before the first final pass; the second pass also cached visor/marine-snow maelstrom lookups and removed shader scalar-swizzle zero literals.
Rejected Alternatives: Replacing the rsqrt vortex evaluator with a LUT was rejected because the field has dynamic center/radius/strength and only 1-2 samples; LUT indirection would add cache pressure without visual gain. Triangle-wave was kept out of gameplay force truth and used only conceptually as a visual-fake guideline.
Scalability potential: Low = one strongest maelstrom, no tangent, sparse particle response. Middle = two samples and rumble. High = richer tangent, boid panic, visor warp. Ultra = denser marine snow/vortex distortion while physical truth stays clamped.
Hardware Impact: i3/MX350 path avoids colliders, trigger callbacks, sqrt, redundant unchanged GPU uploads, and extra active samples. High-end path spends the saved CPU/GPU budget on particle swirl and post distortion.
Final Git Diff: Full diff remains in the working tree for integrator review.
Status: PENDING due global compile dependency wall, not VERIFIED MASTER GRADE.
