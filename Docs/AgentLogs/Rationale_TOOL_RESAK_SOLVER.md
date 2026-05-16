# Rationale - TOOL_RESAK_SOLVER

Current state: PENDING VERIFICATION

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
