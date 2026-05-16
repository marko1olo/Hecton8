# Rationale - TOOL_RESAK_SOLVER

Current state: CORE IMPLEMENTED - FINAL BUILD BLOCKED BY CROSS-DOMAIN DEPENDENCIES

## Decision Log

### 2026-05-16 - Baseline
Problem: Existing implementation unknown. Task requires removal of CSG and replacement with WFC progress plus shader/decal clipping without crossing domain ownership.
Solution: Read project contracts and scan live code before editing. Use existing interfaces/signals/DataVault if present; add minimal local contracts only when no project contract exists.
Rejected Alternatives: Blind replacement or invented service graph. Standard Unity mesh boolean/path using Mesh.vertices is forbidden by prompt and stalls the main thread.
Scalability potential: Low uses decal/progress fake; Middle keeps deterministic progress with modest sparks; High uses shader sphere clip; Ultra can increase molten edge richness without increasing gameplay truth.
Hardware Impact: Expected low-end gain is removal of 200 ms CSG stall. Exact microseconds are PENDING VERIFICATION until compile and runtime profiling.

### 2026-05-16 - Loop 1 Tasks 1-5
Problem: The old cutter path queued generic plasma-cut work and allowed voxel/CSG-style deformation to remain the perceived solution for sealed doors.
Solution: Route WFC sealed doors through the existing `EquipmentInteractionHandler` single-requester `RaycastCommand` lane, then branch in `LaserCutter` before `InteractionEffectType.PlasmaCut`. Store origin/hit in `double3` and pass only presentation floats to shader globals.
Rejected Alternatives: Direct `Physics.Raycast`, per-door managers, mesh boolean cuts, and `Mesh.vertices` mutation. Those approaches add main-thread stalls, create singleton coupling, or violate the batch prompt.
Scalability potential: Low uses an optional growing decal plus sealed-door progress MPB. Middle keeps sparks/audio/haptics. High clips by shader sphere. Ultra can author richer molten materials on the same globals without changing gameplay truth.
Hardware Impact: MX350/i3 path removes CSG editor/runtime DLL load and avoids mesh rebuilds; expected saving remains 200000+ us versus boolean cuts, with added hot-path work below 100 us because state is one cell float plus signal writes.

Problem: Cutter heat and battery were still backed by tool-local native mirrors, so downstream systems could not audit them through the vault.
Solution: Move `ModularEquipmentEngine` heat and battery mirrors to `GlobalDataVault` buffers (`ToolRuntimeHeat01`, `ToolRuntimeBatteryCharge`) with the existing fixed NativeArray fallback only if the vault is unavailable.
Rejected Alternatives: Adding a laser-only duplicate buffer in `LaserCutter`. That would create divergent truth from the modular equipment service.
Scalability potential: Low and High tiers read the same compact SOA; visuals can scale without re-querying tool components.
Hardware Impact: Removes per-system ownership drift and keeps mirror writes contiguous. Expected hot-path delta is neutral to positive; vault-backed cache lines replace two scene-owned arrays.

### 2026-05-16 - Loop 2 WFC SOA, Visuals, Signals
Problem: Sealed-door cutting needed persistent WFC truth without mesh mutation and without inventing a new manager.
Solution: Add `WfcLaserCutRuntime` as a static data-oriented helper that resolves the WFC cell from `SealedDoor`, writes `CutProgress01` to a DataVault `NativeArray<float>`, records a 300-frame black box, and drives existing `GlobalSignals`.
Rejected Alternatives: Door-owned dictionaries, scriptable singleton managers, and spawning VFX/audio objects directly. Those choices allocate or couple gameplay to presentation owners.
Scalability potential: Low only scales an optional decal and door MPB progress. Middle emits modest sparks/audio/haptics. High and Ultra use shader globals to make authored door materials clip and glow without changing gameplay data.
Hardware Impact: One cell float write plus three compact signal pushes replaces CSG mesh rebuilds. MX350 expected saving remains 200000 us class; added CPU work expected below 100 us in the cutting frame.

Problem: Power unlock and laser-cut unlock shared the same WFC `DoorUnlocked` bit, so later power-off telemetry could undo a completed laser cut.
Solution: Add a private `_wfcOutpostLaserUnlocked` latch in `SealedDoor`; power updates may clear power state, but they do not clear a laser-completed unlock.
Rejected Alternatives: Adding a new persistent flag bit outside the documented mutable mask, or publishing duplicate state corrections each frame.
Scalability potential: The latch is local and branch-only; all tiers get deterministic persistence behavior.
Hardware Impact: Negligible CPU cost; prevents repeated state churn and duplicated persistence writes.

### 2026-05-16 - Loop 3 Stability and Build Wall
Problem: Feedback tasks required sparks, audio, haptics, stress adaptation, and postmortem proof without widening runtime ownership.
Solution: Add constants to existing signal structs, publish `DebrisSpawnSignal(Sparks)`, `ToolAcousticSignal(LaserLoop)`, and `HapticRequest(MicroVibration)`, clamp all progress with `math.saturate`, and dump `Dump_TOOL_RESAK_SOLVER.bin` only on invalid numeric state.
Rejected Alternatives: Local prefab instantiation, local audio-only state, or `Debug.Log` as the black box. Standard Unity instantiation would allocate and break lane segregation.
Scalability potential: Low stress drops spark rate to 35 percent; High/Ultra can spend saved CPU on molten shader richness.
Hardware Impact: Stress adaptation cuts non-critical spark pressure by roughly 65 percent above `SystemStress01 > 0.7`; microsecond cost is one signal write per lane.

Problem: Final `dotnet build` is blocked after local cutter/WFC errors were cleared.
Solution: Treat final validation as blocked by dependency after repeated build passes. Errors are outside the assigned gameplay tool surface: missing docking autopilot contracts, VFX wakes, light shaft contracts, and ecosystem interface drift.
Rejected Alternatives: Stubbing foreign-domain contracts from the laser task or reverting WFC implementation. Both would create architectural sabotage or fail the requested feature.
Scalability potential: No effect on runtime; this is an integration dependency wall.
Hardware Impact: No runtime cost. Build remains blocked until owning agents restore the missing contracts.

### 2026-05-16 - Omega Polish
Problem: The WFC hot call surface carried unused direction/normal parameters after the shader and decal paths settled on hit point plus progress.
Solution: Remove dead parameters and keep the cutter-to-runtime call surface to consumed data only.
Rejected Alternatives: Leaving unused data for hypothetical future effects. That hides real dependencies and bloats call sites.
Scalability potential: Low/Middle/High/Ultra behavior unchanged; less argument churn in the hot path.
Hardware Impact: Tiny CPU/register-pressure reduction, estimated below 1 us, but removes ambiguity from the cutting kernel.
