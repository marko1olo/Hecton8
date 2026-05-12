# HABITAT_BUILDER Status

Prompt: `HABITAT_BUILDER` (`HABITAT_ARCHITECT`)
Domain: Echelon 6 - Habitat & Vehicles
Status: PENDING VERIFICATION

Mandates loaded:
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `STRM_ModuleDTO_LZ4_Dictionary.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `PHYS_Fluid_Incursion_Interior.txt`

## Loop 0 - Intake
- [x] Extract prompt | DOD: CLI regex against `Docs/Tasks/CURRENT_BATCH.md`; earlier `.txt` batch disappeared after batch refresh. Alternative rejected: IDE tab memory. Estimate: 70 us.
- [x] Verify hygiene | DOD: checked status/rationale before each user-facing report. Alternative rejected: chat memory as source of truth. Estimate: 25 us.
- [x] Domain check | DOD: read `Docs/Actual Domains of Project.txt`; habitat work is Echelon 6. Alternative rejected: editing Core as owner domain except AUP signal/listener boundary. Estimate: 35 us.

## Loop 1 - Tasks 1-5
- [x] Task 1 - scalable integrity solver | DOD: existing CSR solver preserved; High/Ultra evaluates each module with local current, Low/Mid uses a single average-depth scalar. Alternative rejected: FEM or per-vertex physics. Estimate: Low 38 us per 64 modules, High 95 us per 64 modules.
- [x] Task 2 - hull deformation LOD | DOD: shader displacement is zero outside High/Ultra; Low/Mid emits creak and camera shake only. Alternative rejected: running vertex displacement on MX350. Estimate: saves 70-110 us GPU/CPU sync on low tier.
- [x] Task 3 - deterministic breach math | DOD: breach gate uses `((BaseIDHash ^ TimeSeconds) & 255) < threshold`. Alternative rejected: `Random.value` and mutable RNG state. Estimate: 2 us per breach scan.
- [x] Task 4 - AUP joint recovery | DOD: `ConstructionManager` listens for origin shifts, reanchors world-space connected anchors, preserves rigidbody velocities, and `HectonFloatingOrigin` already emits `AupShiftSignal`. Alternative rejected: draining global queue and stealing events from other consumers. Estimate: 25-80 us per shifted base, cold path only.
- [x] Task 5 - no-trig magnetic snapping | DOD: snap converts runtime to absolute universe, rounds integer millimeters to the 4 m grid, then returns runtime space; yaw remains quantized step logic. Alternative rejected: float `round(world/grid)` drift. Estimate: 3 us per preview snap.

## Loop 2 - Tasks 6-10
- [x] Task 6 - logistics graph event coupling | DOD: placement/removal publishes `HabitatConstructionSignal` after graph mutation while still rebuilding the existing logistics graph directly. Alternative rejected: direct dependency on VFX or UI systems. Estimate: 5 us enqueue.
- [x] Task 7 - transition hatch state | DOD: added `TransitionHatchMeshState` with Open/Closed/Emergency mesh/root states driven by adjacent flooded/ruptured/lockdown flags. Alternative rejected: prefab-specific string lookup. Estimate: 3-12 us on graph publication.
- [x] Task 8 - habitat graph persistence | DOD: verified `ModuleBlitDTO` path writes struct slices through `WriteStructArraySlice` and `UnsafeMemoryCopyGuard.TryMemCpy`. Alternative rejected: string DTO as primary persistence. Estimate: saves 120-220 us per 128 modules at save serialization.
- [x] Task 9 - byte health SOA mirror | DOD: verified `BuildModuleBlitRecord` packs health as byte 0-255. Alternative rejected: persisted float health in blit record. Estimate: 3 bytes saved per module plus vectorized copy alignment.
- [x] Task 10 - zero-GC ghost preview | DOD: verified pooled ghost proxy and shared material path in existing preview factory; no hot `Instantiate` added. Alternative rejected: per-preview material instance. Estimate: avoids 0.2-1.5 ms allocation spike.

## Loop 3 - Tasks 11-15
- [x] Task 11 - construction smoke VFX event | DOD: placement signal includes `HabitatConstructionFlagSmokeVfx` for GPU particle consumers. Alternative rejected: direct ParticleSystem reference in construction manager. Estimate: 5 us enqueue, zero managed allocation.
- [x] Task 12 - emergency bulkheads | DOD: adjacent flooded/ruptured state locks emergency bulkheads and updates reserved graph bit. Alternative rejected: player-facing polling script per door. Estimate: 10-45 us during graph publish.
- [x] Task 13 - 50 percent deconstruction return | DOD: refund is integer `amount / 2`, exactly floor 50 percent. Alternative rejected: 80 percent legacy ratio and floating multiply. Estimate: 1 us per cost line.
- [x] Task 14 - emergency lighting relay | DOD: `_BaseEmergencyState` global int flips when remaining integrity drops below 20 percent. Alternative rejected: mutating every interior Light. Estimate: saves 60-300 us versus per-light updates.
- [x] Task 15 - seismic vibration decay | DOD: construction listens to seismic events, graph attenuates by distance, decays scalar, and publishes `_HectonHabitatVibration01`. Alternative rejected: rigidbody shake impulses on each module. Estimate: 6-40 us per seismic event, 1 us per slow tick decay.

## Loop 4 - Tasks 16-20
- [x] Task 16 - 64-byte DTO | DOD: verified `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]` on `ModuleBlitDTO`. Alternative rejected: implicit struct layout. Estimate: prevents variable-copy branch cost.
- [x] Task 17 - preallocated hierarchies | DOD: joint recovery arrays/lists are cold allocated; ghost factory remains pooled; no hot `SetParent` added. Alternative rejected: dynamic hierarchy creation during placement preview. Estimate: avoids 0.2-2 ms spikes.
- [x] Task 18 - branchless flooded state | DOD: graph reserved flooded bit uses `math.select`. Alternative rejected: branch for flood bit assignment. Estimate: sub-us per node, branch predictor risk removed.
- [x] Task 19 - precomputed inverse volume | DOD: `BaseModule` caches flood capacity and inverse capacity; runtime flood level uses multiply. Alternative rejected: repeated divide in flood level path. Estimate: 2-6 us per flood update on low-end CPU.
- [x] Task 20 - compile/meta/non-ASCII check | DOD: added `.meta`, added new script to `Hecton8.Core.csproj`, verified new script has no non-ASCII, and ran full solution build. Alternative rejected: Unity-only compile assumption. Estimate: 0 runtime us.

## Verification
- [x] Compile check | `dotnet build Hecton8.slnx --no-restore -v:minimal` succeeded with 0 warnings, 0 errors.
- [x] Strict reread pass 1 | Reread AUP snapping code; no trig or float-grid authority found.
- [x] Strict reread pass 2 | Reread analytical stress and deterministic breach gate; no RNG path found.
- [x] Strict reread pass 3 | Reread AUP joint recovery and event coupling; fixed `UnityEngine.Physics.SyncTransforms`.
- [x] Strict reread pass 4 | Reread DTO, health byte, refund, and inverse volume paths.
- [x] Strict reread pass 5 | Reread transition hatch script, meta, csproj include, and non-ASCII scan for new code.
