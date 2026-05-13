# GAS_DYNAMICS_SOLVER Rationale

Status: PENDING VERIFICATION

## Decision 0 - Prompt Source And Domain

Problem: The root `CURRENT_BATCH.md` path required by the batch wrapper does not exist, while the active batch lives under `Docs/Tasks/CURRENT_BATCH.md`.
Solution: Extracted only `<AGENT_PROMPT id="GAS_DYNAMICS_SOLVER">` using a PowerShell raw-read regex and ignored neighboring tags. Domain is Echelon 7 Gas Dynamics (Dalton's Law), with HABITAT_ARCHITECT role ownership for compartments.
Rejected Alternatives: Reading archived batch files; using chat memory; using neighboring prompt text. All violate strict parsing and batch hygiene.
Scalability potential: Low tier receives scalar Dalton room state. Middle tier can run more frequent diffusion. High and Ultra can spend saved cycles on richer HUD, alarms, and visual leak feedback rather than particles.
Hardware Impact: Active prompt extraction has no runtime impact. Architectural decision prevents duplicate systems and avoids unmanaged code churn on i3/MX350.

## Decision 1 - Mandate Set

Problem: Gas dynamics touches registry ownership, native memory, pressure math, logistics scrubbers, telemetry, and visual-fake doctrine.
Solution: Loaded eight mandates: GlobalRegistry DI, Zero-GC, Native Memory Jobs, Abyss Survival, Fluid Incursion, Logistics Energy Networks, Debug Telemetry, and Cinematic Cheat Protocol.
Rejected Alternatives: Bulk-loading the full registry; reading archived status files; treating gas as full fluid chemistry. The mandate registry requires 2-8 task-relevant files and fake-first pressure behavior.
Scalability potential: Low = ColdTick scalar diffusion; Middle = standard low-frequency Burst diffusion; High = tighter cadence and richer signal history; Ultra = visual overkill through UI/VFX channels while preserving scalar truth.
Hardware Impact: Expected gain on i3/MX350 is avoiding per-particle gas and avoiding per-room managed objects. Preliminary estimate: 250-600us saved per 50-room solve versus naive MonoBehaviour/Dictionary diffusion.

## Decision 2 - Registry Binding And Singleton Purge

Problem: Gas ownership must not route through `AtmosphereManager.Instance`; the existing `HectonAtmosphereManager` is a visual atmosphere runtime, not a gas solver.
Solution: Removed the public `HectonAtmosphereManager.Instance` facade and added `IGasDynamicsSolver` to `GlobalRegistry` with a dedicated `GasDynamicsRuntime` slot.
Rejected Alternatives: Reusing `GlobalRegistry.Atmosphere` for gas; creating `GasDynamicsSolver.Instance`; coupling gas to the visual sky/fog owner. Those paths mix domains and recreate singleton access under a different name.
Scalability potential: Low = one registered scalar solver; Middle = same contract with more frequent cadence; High = extra visual subscribers; Ultra = richer UI/VFX driven from snapshots without changing gas truth.
Hardware Impact: Direct registry slot lookup keeps query cost effectively flat and avoids hierarchy scans. Estimated i3/MX350 gain: 4-8us per room snapshot query versus scene-object discovery.

## Decision 3 - Native Signal Lane

Problem: CO2 and narcosis consequences must reach Physiology without calling `Player.Instance.Kill()` or concrete physiology jobs.
Solution: Added unmanaged `ToxicitySignal` packets and a solver-owned `NativeQueue<ToxicitySignal>` emitted by the Burst gas job. Physiology can drain through `IGasDynamicsSolver.TryDequeueToxicitySignal`.
Rejected Alternatives: Direct method calls into `SurvivalPhysiologyScalarJob`; C# events/delegates; managed strings for warning reasons. All violate domain decoupling or hot-path allocation rules.
Scalability potential: Low = one signal per toxic room per cold tick, soft-capped; Middle/High = more frequent checks; Ultra = same queue plus richer visual/audio consumers downstream.
Hardware Impact: Queue packet path avoids managed allocations and delegate dispatch. Estimated i3/MX350 gain: 15-40us on toxic-room frames versus managed event fanout.

## Decision 4 - SOA Dalton Solver

Problem: Old oxygen logic was too close to single-float tank semantics; task requires multi-gas partial pressure arrays modified by Burst.
Solution: Added `GasDynamicsSolver` with private `NativeArray<float>` lanes named `RoomO2`, `RoomCO2`, `RoomPressure`, plus nitrogen/back buffers. The job resolves `P_total = P_o2 + P_co2 + P_nitrogen`.
Rejected Alternatives: AoS `RoomGasState` objects; managed dictionaries keyed by room; full gas chemistry. These are slower, harder to Burst, and not needed for controllable habitat gameplay.
Scalability potential: Low = 2s ColdTick scalar diffusion; Middle = 0.5s cadence; High/Ultra = 10Hz scalar truth with visual overkill purchased elsewhere.
Hardware Impact: SOA contiguous arrays reduce cache misses and avoid GC. Estimated i3/MX350 gain: 80-180us per 64-room solve versus managed room objects.

## Decision 5 - Legacy Submarine Atmosphere Handling

Problem: `SubmarineAtmosphereSystem.cs` contains legacy wording and local oxygen variables, but it already owns partial-pressure arrays and Dalton conversion math.
Solution: Removed the misleading "not chemistry" comment and preserved the working partial-pressure conversion locals. New authority is the `IGasDynamicsSolver` lane; the submarine file was not destructively rewritten.
Rejected Alternatives: Deleting local oxygen variables blindly; binding the new solver to submarine internals. Blind deletion risks breaking existing submarine atmosphere snapshots during parallel integration.
Scalability potential: Low = current submarine compatibility remains; Middle/High/Ultra = consumers can migrate to `IGasDynamicsSolver` without a compile-order dependency.
Hardware Impact: No hot-path cost. Prevents a regression that would cost integration time and risk frame spikes from fallback managed repairs.

## Decision 6 - Dalton Linear Pressure

Problem: Gas pressure must be predictable and cheap while still supporting O2, CO2, and nitrogen consequences.
Solution: Used direct Dalton addition: `P_total = P_o2 + P_co2 + P_nitrogen`, after finite non-negative clamps.
Rejected Alternatives: `math.exp`, humidity curves, or thermodynamic chemistry. Those buy false precision and increase frame cost.
Scalability potential: Low = scalar sum per room; Middle = same sum at higher cadence; High/Ultra = saved cycles feed HUD/VFX/audio feedback.
Hardware Impact: Estimated i3/MX350 gain: 20-50us per 64 rooms compared to nonlinear curve pressure code.

## Decision 7 - Conservative Diffusion

Problem: Connected rooms need believable gas movement without CFD or per-particle simulation.
Solution: Burst job moves each gas from high partial pressure to low across unsealed bulkhead edges and clamps transfer fraction below 0.45 per solve. Pair totals are conserved during diffusion.
Rejected Alternatives: CFD grids, particle gas, or full flood-fill per frame. These violate the 0.1ms suspicion threshold.
Scalability potential: Low = every 2 seconds; Middle = half-second; High/Ultra = 10Hz diffusion with visual leak embellishments outside the solver.
Hardware Impact: Estimated i3/MX350 gain: 300-900us per 128 edges versus particle or cell-grid simulation.

## Decision 8 - Room-Local Metabolism

Problem: Player respiration must drain the exact occupied room, not a global oxygen float.
Solution: Added `TrySetPlayerRoom(roomId, stress01, heartRateBpm)` and folded O2 drain/CO2 generation into the Burst gas pass.
Rejected Alternatives: Reading `Player.Instance` or survival singleton inside gas; global oxygen drain. Both lose compartment truth and violate signal isolation.
Scalability potential: Low = one occupied room lane; Middle/High/Ultra = multiple occupants can be added as room counters without changing interface shape.
Hardware Impact: Estimated i3/MX350 gain: 15-30us by avoiding a separate managed physiology-to-room pass.

## Decision 9 - Logistics Power Bit

Problem: Scrubbers must only reduce CO2 when Logistics says power is active, but gas must not depend on Logistics concrete code.
Solution: Exposed `TrySetScrubberPowered(roomId, powerActive)` as the bit ingress; Burst consumes `_roomScrubberPowered` and reduces only `RoomCO2`.
Rejected Alternatives: Pulling `PowerGrid`/`ConstructionManager` from inside gas tick; reducing total pressure directly. Those create domain coupling and incorrect chemistry.
Scalability potential: Low = byte bit per room; Middle/High/Ultra = module owners can batch power writes before the gas step.
Hardware Impact: Estimated i3/MX350 gain: 5-20us plus zero compile-order dependency.

## Decision 10 - Toxicity Coupling

Problem: CO2 and pressure consequences need to reach physiology without direct kill paths.
Solution: The gas job emits `ToxicitySignal` with CO2 KPa, pressure atm, toxicity scalar, narcosis scalar, and flags.
Rejected Alternatives: `Player.Instance.Kill()`, C# events, or managed warning strings from the job. All are brittle or not Burst-safe.
Scalability potential: Low = bounded queue packets; Middle/High/Ultra = same packets feed richer downstream effects.
Hardware Impact: Estimated i3/MX350 gain: 15-40us on toxic frames compared to managed event fanout.

## Decision 11 - Narcosis As Scalar Consequence

Problem: Pressure above 4 atm should affect the player without introducing a direct physiology dependency.
Solution: Reused `ToxicitySignal` to carry `Narcosis01` and exposed the value in `GasRoomSnapshot` and `UIStateStore`.
Rejected Alternatives: Direct writes to `SurvivalPhysiologyScalarJob`; nonlinear pressure curves. The job contract stays one-way and linear.
Scalability potential: Low = scalar signal only; Middle = HUD value; High/Ultra = visual/audio narcosis layers consume the same scalar.
Hardware Impact: Estimated i3/MX350 gain: 10-25us by avoiding separate pressure polling.

## Decision 12 - Fire As Gas Conversion

Problem: Internal fire must punish atmosphere without simulating combustion chemistry.
Solution: `InternalFire` drains `RoomO2` at 5x fire rate and adds the drained amount to `RoomCO2`.
Rejected Alternatives: Flame particles driving gas, reaction equations, or temperature-coupled chemistry. Too expensive and unpredictable.
Scalability potential: Low = scalar conversion; Middle/High = faster cadence; Ultra = visual fire overkill driven by the same room flag.
Hardware Impact: Estimated i3/MX350 gain: 60-150us per burning room versus VFX/particle-coupled atmosphere.

## Decision 13 - Breach Decompression Fake

Problem: Breached rooms need immediate decompression without iterative pressure simulation.
Solution: If `Breached`, final job pass sets O2=0, CO2=0, nitrogen lane to `AmbientPressure`, and total pressure to ambient.
Rejected Alternatives: Multi-step venting solver or fluid-gas coupling. Breach is a gameplay state, not a physics thesis.
Scalability potential: Low = instant scalar; Middle/High/Ultra = downstream leak VFX can exaggerate without changing truth.
Hardware Impact: Estimated i3/MX350 gain: 80-250us per breach event.

## Decision 14 - UIStateStore HUD Sync

Problem: PDA/visor needs diegetic partial pressure data without direct HUD references or formatted text.
Solution: Wrote O2 fraction, O2 KPa, CO2 KPa, pressure KPa, and narcosis scalar to fixed `UIStateStore` numeric slots.
Rejected Alternatives: TMP string generation, direct `VisorHUDController` refs, or pulling gas from UI. Those add GC or invert ownership.
Scalability potential: Low = numeric slots; Middle/High/Ultra = UI can render richer displays from same slots.
Hardware Impact: Estimated i3/MX350 gain: 20-60us per HUD update versus formatted string path.

## Decision 15 - Hull Stress Cinematic Cheat

Problem: High internal pressure should reduce effective depth stress, but real hull stress simulation is out of scope and too slow.
Solution: Exposed `ResolveEffectiveDepthStress01` as a scalar relief function based on room pressure above 1 atm.
Rejected Alternatives: Finite-element hull stress, coupled structural deformation, or pressure-wave propagation. The project requires controllable fake-first pressure.
Scalability potential: Low = scalar relief; Middle/High/Ultra = visual deformation/audio can be exaggerated downstream.
Hardware Impact: Estimated i3/MX350 gain: 500us+ versus even a small structural solve.

## Decision 16 - Local Room IDs

Problem: Internal gas logic must not become another AUP/floating-origin consumer.
Solution: Solver APIs accept local room ids, edge indices, and flags only; no transforms, world positions, or AUP structs are used.
Rejected Alternatives: Binding gas rooms to world coordinates or Unity transforms. That would add origin-shift complexity where none is required.
Scalability potential: Low = cheap local arrays; Middle/High/Ultra = same ids can map to richer room visuals outside the solver.
Hardware Impact: No per-frame AUP math. Estimated i3/MX350 gain: all origin-shift bookkeeping avoided.

## Decision 17 - Tiered ColdTick

Problem: Low-tier hardware should not pay 10Hz gas solves when scalar diffusion can be delayed safely.
Solution: `ResolveMathLod` maps Low/MX350 to 2.0s cadence, Mid to 0.5s, High/Ultra to 0.1s.
Rejected Alternatives: Always-10Hz gas; per-frame gas. Both waste budget that should buy visuals.
Scalability potential: Low = toaster-safe; Middle = responsive; High/Ultra = tighter cadence plus richer downstream overkill.
Hardware Impact: Estimated Low/MX350 gain: 70-95% less gas CPU versus 10Hz.

## Decision 18 - Zero-GC Hot Path

Problem: Gas must not allocate or format strings during runtime ticks.
Solution: Hot path uses `NativeArray`, `NativeQueue`, Burst job math, fixed UI slots, and a fixed telemetry ring. Only black-box failure dump uses managed file APIs on fault path.
Rejected Alternatives: C# events, LINQ, `string.Format`, per-room managed class graphs. These are forbidden in hot simulation.
Scalability potential: Low = no GC spikes; Middle/High/Ultra = downstream systems can spend budget on visuals.
Hardware Impact: Target is 0B GC per gas tick and stable CPU on i3/MX350.

## Decision 19 - Compile Wall And Exp Audit

Problem: Final compile verification is blocked by unrelated workspace dependencies, while the gas prompt also requires `math.exp` audit.
Solution: Ran four `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -clp:ErrorsOnly` attempts and marked compile cleanliness blocked by external dependencies. Runtime `math.exp` scan excluding Editor returned no hits.
Rejected Alternatives: Claiming a clean compile; editing unrelated `Memory`, `Determinism`, `Cartography`, or `DataVault` systems. Those are outside the gas domain and belong to other agents.
Scalability potential: Low/Mid/High/Ultra all use linear gas curves; no hidden exponential cost.
Hardware Impact: Linear approximations preserve the 20-50us pressure-math savings per solve.

## OMEGA POLISH CHANGES

Problem: Final anti-bloat pass required proving the implementation did not hide expensive physical truth, managed string churn, or origin-shift coupling.
Solution: Scanned `GasDynamicsSolver.cs` for `math.sqrt`, `math.normalize`, managed `foreach`, `string.Format`, `.ToString()`, interpolation, AUP/world-position usage, and unconditional division. Hot path uses bitmasks, `math.rcp`, linear sums, NativeArrays, NativeQueue, and Burst. The only managed file/string path is black-box dump failure handling; log output is wrapped in `UNITY_EDITOR || DEVELOPMENT_BUILD`.
Rejected Alternatives: Leaving naked log output; adding a "real" decompression/chemistry model; using expensive math on all tiers. All are unnecessary for playable gas pressure.
Scalability potential: Low = 2s ColdTick scalar truth; Middle = 0.5s; High/Ultra = 10Hz scalar truth with downstream visual overkill. Toaster path stays visually readable; top-tier path can spend saved gas budget on HUD/VFX/audio.
Hardware Impact: Expected i3/MX350 savings remain 300-900us versus particle/CFD diffusion, 70-95% CPU reduction on Low tier versus 10Hz gas, and 0B GC in gas ticks.

## Decision 20 - Post-Completion Static Hardening

Problem: A patient review found small integration risks: breached rooms seeded ambient to zero until an external owner configured them, external flag/configure writes could clear the internally-owned `Occupied` bit, zero authored bulkhead capacity could still accept edge 0 because the native buffer has a minimum allocation length, the diffusion conductance inspector range allowed values above one but scheduling saturated it, and the Burst job carried an unused `RoomPressureFront` payload.
Solution: Seed standard rooms with standard ambient pressure, add `TrySetAmbientPressure(roomId, ambientPressureKPa)` to `IGasDynamicsSolver`, preserve `Occupied` when external flags or full room configuration are written, track authored bulkhead capacity separately from native buffer length, pass authored diffusion conductance through finite non-negative clamp and rely on the existing per-step cap, remove the unused job field, and mark telemetry entries with explicit sequential layout.
Rejected Alternatives: Direct Habitat Integrity dependency, silent zero-ambient breach defaults, letting external systems own the player-occupied bit, or launching another compile after the user explicitly forbade `dotnet build`.
Scalability potential: Low = safer scalar defaults with no extra tick cost; Middle/High/Ultra = integrity and flood systems can push ambient pressure without coupling while high-end visual leak effects remain downstream.
Hardware Impact: No hot-loop allocation added. Removing the unused job payload slightly reduces schedule metadata pressure; ambient and flag writes are cold integration calls. Expected i3/MX350 gain is small but positive, roughly 1-3us per scheduled solve plus avoided breach-state correction work.

## Decision 21 - Native Audit And Re-Enable Lifecycle

Problem: The native-memory mandate requires owner-visible allocation evidence, and a disable/enable during deferred disposal could leave the solver disabled from tick polling before native storage was recreated. Edit-mode `OnEnable` also had no explicit play-mode gate.
Solution: Added `GasDynamicsNativeMemoryAudit` and `IGasDynamicsSolver.TryGetNativeMemoryAudit` so integrators can inspect local allocation count, registered bytes, largest allocation label hash, and sentinel totals without exposing storage. Added an edit-mode `OnEnable` guard, allowed deferred-dispose re-enable to register tick polling, and retried registry binding after native storage finalizes in `FixedTick`.
Rejected Alternatives: Adding managed debug strings to the runtime interface, scanning `NativeMemorySentinel` internals by owner, blocking on `JobHandle.Complete`, or running `dotnet build` against the user's explicit no-build order.
Scalability potential: Low = no new hot-path cost; Middle/High/Ultra = QA and bootstrap systems can audit gas memory before enabling richer leak/HUD/VFX consumers.
Hardware Impact: Audit is cold-path only. Runtime gain is avoided dead service state after rapid enable/disable and no edit-mode native allocation; expected i3/MX350 frame impact is 0us in gas ticks.

## Decision 22 - Defined Cold Memory And Dump Header

Problem: `UninitializedMemory` on gas lanes is a cold optimization that becomes unsafe if designers disable standard atmosphere seeding and rely on external room configuration. The black-box dump also lacked a parse header, making ring order and format version ambiguous.
Solution: Converted O2, CO2, pressure, nitrogen, back-buffer, and ambient lanes to `ClearMemory` on persistent allocation. Added dump magic, version, entry size, capacity, write index, and tick count before telemetry entries, and fixed telemetry entry layout at 32 bytes.
Rejected Alternatives: Keeping uninitialized lanes for a tiny cold boot saving; requiring every external configurator to write every room before the first tick; dumping a raw ring with no header. Those save no meaningful frame time and increase failure ambiguity.
Scalability potential: Low = seed-disabled configurations start from deterministic zero-pressure lanes instead of undefined memory; Middle/High/Ultra = black-box dumps can be parsed by QA tooling without guessing format.
Hardware Impact: ClearMemory cost is cold startup only and tiny at 128-room capacity. Hot-path impact is 0us. Debug recovery value is higher because dumps now identify format and cursor.

## Decision 23 - Fixed-Cap Toxicity Queue Discipline

Problem: A prewarmed `NativeQueue` can still grow native storage if stale toxicity packets are left undrained and the next Burst job enqueues more packets. CO2 and narcosis limits also used raw serialized thresholds in the job payload clamp relationship.
Solution: Drain old toxicity packets before every new gas solve, keeping the queue at the prewarmed capacity boundary and making each solve publish only fresh signals. Sanitize CO2 threshold/fatal and narcosis threshold/full values once on the main thread before copying them into the job.
Rejected Alternatives: Letting stale signals accumulate until the soft cap is exceeded; dynamically growing the queue; forcing direct physiology calls to guarantee delivery. All either risk native allocation or violate domain boundaries.
Scalability potential: Low = bounded signal memory even if Physiology is cold-ticked or missing; Middle/High/Ultra = latest gas consequences remain fresh while richer downstream effects consume the same signal lane.
Hardware Impact: Hot-path allocation risk removed. The main-thread drain is bounded by 128 packets and runs before scheduling; estimated worst-case cost is under 10us on i3/MX350 and prevents larger native queue growth spikes.

Cinematic Cheats used:
- Dalton scalar sum, not thermodynamic simulation.
- Capped linear pair diffusion, not CFD.
- Fire as direct O2-to-CO2 scalar conversion.
- Breach as instant ambient-pressure snap.
- Hull stress as scalar pressure relief.
- PDA/visor uses fixed numeric slots, not generated strings.

Final Git Diff:
- `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs`: new Burst SOA gas solver, toxicity queue, black-box telemetry ring.
- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`: `IGasDynamicsSolver`, gas room snapshot, toxicity signal, gas flags, registry slot.
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs`: `GasDynamics` registry field/property/register/unregister/resolve wiring.
- `Assets/_Project/Scripts/Core/UIStateStore.cs`: added partial-pressure and narcosis numeric slots.
- `Assets/_Project/Scripts/HectonAtmosphereManager.cs`: removed `Instance` singleton facade.
- `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs`: removed stale non-Dalton oxygen comment.
- `Docs/Tasks/Status_GAS_DYNAMICS_SOLVER.md`: task checklist and compile wall evidence.
- `Docs/AgentLogs/Rationale_GAS_DYNAMICS_SOLVER.md`: decision journal and polish audit.
