# Rationale - KINEMATICS_HYDRO_DRAG

Status: PENDING VERIFICATION

## Decision 0 - Bootstrap

Problem: Prompt requires true buoyancy and hydro drag while 20+ agents may edit adjacent systems.
Solution: Bound implementation to Echelon 4 kinematics/physics. Use existing interfaces, GlobalRegistry, or event signals discovered in code. Avoid direct dependencies on invented inventory/logistics/tether systems.
Rejected Alternatives: Direct scene wiring or concrete cross-domain class references. These break parallel-agent isolation and will fail when adjacent systems are not compiled yet.
Scalability potential: Low uses cached cargo mass scalar and fewer recomputations. Middle/High/Ultra can spend saved CPU on richer breach, rumble, and water response signals if existing buses are present.
Hardware Impact: Expected low-end gain is avoiding per-frame cargo iteration and Unity built-in drag coupling; target is 0 B/frame and sub-0.1 ms solver contribution on i3/MX350.

## Decision 1 - Cargo Mass Contract

Problem: The prompt asks for SOA submarine storage mass, but a stable submarine-storage API is not present and other agents are editing inventory/logistics.
Solution: Implemented `IInventoryEventListener` on `SubmarineFluidDynamics`, reading cached inventory mass through `InventoryEvents` / `GlobalRegistry.PlayerInventoryMassKg`, plus public `SetSubmarineCargoMassKilograms` and `SetCargoMassScalar` for the future submarine storage owner. This keeps hydro independent from storage internals.
Rejected Alternatives: Iterating item SOA arrays from the submarine each fixed tick; hard-coding a concrete storage component; inventing a logistics API. Those add GC/compile coupling and violate parallel-agent boundaries.
Scalability potential: Low = one cached scalar. Middle = event-updated exact cargo. High = separate mass compartments can feed the same scalar. Ultra = per-compartment cargo center-of-mass can extend the same packet without changing the solver boundary.
Hardware Impact: Estimated low-end saving 15-35 us/tick versus scanning inventory structures; 0 B/frame in the hydro fixed loop.

## Decision 2 - True Draft And Crush Buoyancy

Problem: The submarine rode like a balloon because dry mass, cargo mass, and hull compression did not affect resting waterline strongly enough.
Solution: Rigidbody target mass now includes cargo mass; exterior buoyancy sampling uses a cargo draft offset and applies 0.85 buoyancy scale below safe crush depth.
Rejected Alternatives: Raising gravity, fake downward AddForce, or changing global water density. Those break predictability and contaminate unrelated physics.
Scalability potential: Low = scalar draft offset. Middle = authored cargo capacity response. High = depth/material hull curves. Ultra = visual hull creak/VFX can scale from the same crush scalar.
Hardware Impact: Estimated saving 8-20 us/tick versus per-sample cargo iteration; immersion gain comes from a cheap waterline lie, not simulating hull deformation.

## Decision 3 - Directional Hydro Drag Job

Problem: Isotropic drag makes sideways submarine motion cheap and fake.
Solution: Added Burst `HydroKinematicDragJob` using local-axis dot products: forward, lateral, vertical speeds. Lateral coefficient is `ForwardDrag * 5`. Output acceleration is applied through `PhysicsForceRouter`.
Rejected Alternatives: Unity `Rigidbody.linearDamping/angularDamping`; direct `AddForce`; MonoBehaviour `Update` integration. Built-in damping is isotropic and uncontrolled, direct forces bypass the project router, and `Update` violates dispatcher cadence.
Scalability potential: Low = one drag job with scalar coefficients. Middle = material/shape coefficients. High = sample hull section coefficients. Ultra = saved cycles buy breach spray, rumble, cockpit stress visuals.
Hardware Impact: Estimated saving 20-60 us/tick over sample-heavy main-thread drag; deterministic packet is one `IJob` and 0 B/frame.

## Decision 4 - Angular Drag And Leveling

Problem: A flooded or overloaded submarine must resist angular velocity and recover pitch/roll without snapping.
Solution: Hydro torque uses `-angularVelocity * AngularDragCoefficient * waterDensity * submersion`; righting torque uses `math.cross(up, worldUp) * mass * submersion`.
Rejected Alternatives: `Rigidbody.angularDamping`, Transform rotation correction, or PID controller in `Update`. These are either isotropic, visually abrupt, or cadence-unsafe.
Scalability potential: Low = scalar torque. Middle = mass-scaled leveling. High = inertia tensor bias by flood/cargo state. Ultra = cockpit camera and audio can exaggerate pitch/roll stress.
Hardware Impact: Estimated saving 10-25 us/tick versus main-thread stabilizer logic while preserving controllability.

## Decision 5 - Ballast, Towing, Cavitation Feedback

Problem: Ballast, towing load, and stalled thrust need physical signals without hard dependencies on unfinished logistics/tether/audio owners.
Solution: Implemented public ballast and towing APIs, local compressed-air reserve, tether tension vector injection, and cavitation feedback through existing haptics/audio event lanes.
Rejected Alternatives: Concrete logistics component references, Unity joints for towing, and direct audio source manipulation. Those break domains and add scene coupling.
Scalability potential: Low = scalar air and tension. Middle = logistics owner feeds compressed air. High = tether system feeds exact tension. Ultra = procedural rumble and cockpit VFX scale from cavitation state.
Hardware Impact: Estimated saving 12-30 us/event by avoiding object searches and joint solver cost.

## Decision 6 - Player Suit Weight

Problem: Inventory mass must affect upward swim speed; 50 titanium chunks must reduce ascent by 40%.
Solution: Added cached upward swim multiplier in `HectonPlayerMovement`, resolving full-load upward swim to 0.6x while preserving existing movement-load logic.
Rejected Alternatives: Full movement lock, global gravity changes, or per-item checks during swim force calculation. Those are too punishing, cross-system, or hot-path expensive.
Scalability potential: Low = load scalar. Middle = equipment mass categories. High = buoyancy suit upgrades. Ultra = visor warnings and exertion audio can use the same load scalar.
Hardware Impact: Estimated saving 5-15 us/tick versus item-level swim mass checks.

## Decision 7 - Built-In Damping Recon

Problem: Unity built-in damping conflicts with custom hydro drag and makes solver behavior opaque.
Solution: Submarine runtime damping is forced to zero and `PFB_Submarine_Core.prefab` was corrected from `m_AngularDamping: 0.05` to `0`. Full recon is logged in `Docs/Tasks/RECON_KINEMATICS_HYDRO_DRAG.md`.
Rejected Alternatives: Erasing every non-zero damping in pickups/fauna/transport prefabs. That would be cross-domain sabotage; those owners may intentionally use built-in damping.
Scalability potential: Low = core submarine clean. Middle = transport-domain review of gliders/sleds. High/Ultra = all water vehicles route through custom hydro coefficients.
Hardware Impact: Estimated saving is not raw CPU; impact is determinism and fewer hidden damping variables during tuning.

## Decision 8 - Hydro Black Box

Problem: Hydro is critical physics. Without a local 300-frame state ring, NaN reports cannot be reconstructed.
Solution: Added `NativeArray<HydroBlackBoxEntry>[300]` to `SubmarineFluidDynamics`, sampled once per fixed tick. Invalid hydro velocity/output/buoyancy paths dump `Docs/AgentLogs/Dump_KINEMATICS_HYDRO_DRAG.bin`.
Rejected Alternatives: Relying only on debug inspector fields or global logs. They are not fixed-size, not binary, and do not preserve the last 300 hydro frames.
Scalability potential: Low = compact high-level state. Middle = integrator binary reader can display state hashes. High = replay harness can diff state hashes. Ultra = QA bot can correlate dump with visual breach/cavitation events.
Hardware Impact: Fixed 300-entry persistent native buffer; estimated hot-path cost 3-8 us/tick on i3/MX350, justified for crash reconstruction.

## Decision 9 - Verification Boundaries

Problem: Full project compile is currently blocked by unrelated files, so a clean global compile claim would be false.
Solution: Validated `SubmarineFluidDynamics.cs` through Unity MCP `validate_script` with 0 diagnostics after the black-box and polish edits. `HectonPlayerMovement.cs` validator times out due file size/regex; current Unity console errors are outside hydro in `NativeArenaArrayEditTests.cs` and `SaveBinaryStorage.cs`.
Rejected Alternatives: Reporting success without console evidence; reverting another agent's Fauna work; forcing a broad refactor loop.
Scalability potential: Low/Middle/High/Ultra unchanged; this is verification hygiene.
Hardware Impact: No runtime impact. Prevents false integration state.

## OMEGA POLISH CHANGES

Problem: The OMEGA polish mandate required an anti-bloat pass after core tasks were checked. The first pass still had unnecessary exact math in the Burst righting torque.
Solution: Removed the `math.sqrt` + `math.rsqrt` pair from righting torque. The expression `normalize(axis) * length(axis)` was reduced to `axis`, preserving the same visual torque curve with fewer operations. Also replaced unconditional axis `math.normalizesafe` calls in the hydro job with finite selection because the scheduled inputs come from Unity transform direction vectors and constant world up.
Rejected Alternatives: Keeping the mathematically verbose form for "honesty"; adding a full hull sample mesh solver; editing unrelated damping owners to fake a clean recon.
Scalability potential: Low = scalar draft/drag/torque only. Middle = event-updated mass and towing vectors. High = richer breach/cavitation feedback. Ultra = saved cycles buy cinematic splash/audio/cockpit stress, not more particle-level physics.
Hardware Impact: Estimated 4-12 us/tick saved on i3/MX350 by deleting redundant normalization/sqrt work from the Burst drag solver.

Exact cinematic cheats used:
- Cargo draft is a scalar waterline offset, not a fluid volume solve.
- Crush depth uses a 0.85 buoyancy scale below safe depth, not hull deformation.
- Lateral drag is a 5x cross-section coefficient, not CFD.
- Cavitation is a speed/throttle gate with audio/haptics, not bubble simulation.
- Breach splash uses deterministic signal amplification, not continuous spray simulation.
- Low-tier cargo mass uses cached `CargoMassScalar`, not per-item iteration.

Scoped final diff:
- `Assets/_Project/Scripts/SubmarineFluidDynamics.cs`: added cargo mass/draft/crush fields, Burst directional hydro drag job, ballast/towing/cavitation APIs, surfacing breach signal, zero built-in damping enforcement, and 300-frame hydro black box dump.
- `Assets/_Project/Scripts/HectonPlayerMovement.cs`: added upward swim load multiplier resolving full load to 0.6x.
- `Assets/_Project/Prefabs/PFB_Submarine_Core.prefab`: changed `m_AngularDamping: 0.05` to `0`.
- `Docs/Tasks/RECON_KINEMATICS_HYDRO_DRAG.md`: added built-in damping recon.
- `Docs/Tasks/Status_KINEMATICS_HYDRO_DRAG.md`: updated checklist and verification state.
- `Docs/AgentLogs/Rationale_KINEMATICS_HYDRO_DRAG.md`: updated decisions and polish evidence.

Verification after polish:
- `SubmarineFluidDynamics.cs` Unity MCP `validate_script`: 0 diagnostics.
- `rg "math\.sqrt|math\.normalize\(|math\.normalizesafe" Assets/_Project/Scripts/SubmarineFluidDynamics.cs`: no matches.
- `rg --fixed-strings "foreach"`, `string.Format`, `.ToString(`, and `$"` on the two touched scripts: no matches.
- OMEGA `dotnet build Assembly-CSharp.csproj /p:HectonSkipAssemblyProjectReferences=true /p:BuildProjectReferences=false /m:1`: failed on missing Unity-generated metadata assemblies; log `Docs/AgentLogs/KINEMATICS_HYDRO_DRAG_dotnet_polish.log`.
- Full Unity compile remains blocked by non-hydro files: `Assets/_Project/Tests/Editor/NativeArenaArrayEditTests.cs` missing Burst symbols and `Assets/_Project/Scripts/SaveBinaryStorage.cs` Burst `catch` filter error.
- Status intentionally remains `PENDING VERIFICATION` because the agent prompt explicitly mandates that status and the project compile is not clean.
