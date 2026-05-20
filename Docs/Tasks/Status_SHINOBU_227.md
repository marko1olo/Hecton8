# Status_SHINOBU_227

Agent: SHINOBU_227
Role: SEAGLIDE_HYDRODYNAMICS_REVISER
Domain: Echelon 4 Player, Kinematics & Tools / Scooter (Seaglide) Kinematics
Task count: 20
Status: PENDING VERIFICATION

## Mandates Loaded Before Coding

- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- MATH_AUP_Determinism_Sync.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- PHYS_Physics_Integrity_Determinism_ForceMode.txt
- CORE_Submarine_Vehicles_Kinematics_AUP.txt
- CORE_Weather_Abyssal_FlowField_Currents.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Execution_Phases.txt
- ARCH_Signal_Lane_Segregation.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt

## State Machine

- Loop 1: Tasks 01-05 archaeology, DTO layout, mock data. Status: PENDING.
- Loop 2: Tasks 06-10 hydrodynamics, flow, battery, cadence, force packets. Status: PENDING.
- Loop 3: Tasks 11-14 audio, rollback fence, uninitialized memory, telemetry dump. Status: PENDING.
- Loop 4: Tasks 15-18 editor/xray/csv/gizmo/static scanner. Status: PENDING.
- Loop 5: Tasks 19-20 layout trap guard, self-audit, compile/static gates. Status: PENDING.

## Task Checklist

- [ ] Task 01 MONOBEHAVIOUR_PHYSICS_INQUISITION | DOD: static source scan + direct Rigidbody/FixedUpdate removal or quarantine note | Alternative rejected: component-local force application | Estimate: pending.
- [ ] Task 02 PARTICLE_INSTANTIATION_PURGE | DOD: scan for hot instantiate/destroy and route through unmanaged signal if local owner exists | Alternative rejected: runtime ParticleSystem prefab churn | Estimate: pending.
- [ ] Task 03 CS1612_HOT_PATH_PROPERTY_ANNIHILATION | DOD: DTOs use raw fields only | Alternative rejected: C# property wrappers in Burst data | Estimate: pending.
- [ ] Task 04 ARM64_ALIGNMENT_AND_PADDING_ASSERTION | DOD: explicit layout + editor/static validator | Alternative rejected: implicit sequential layout for cache-line data | Estimate: pending.
- [ ] Task 05 EMERGENCY_MOCK_PROPULSION_GENERATOR | DOD: Burst deterministic mock request job | Alternative rejected: waiting for input owner | Estimate: pending.
- [ ] Task 06 BURST_HYDRODYNAMIC_THRUST_KERNEL | DOD: Burst job computes thrust + drag | Alternative rejected: Rigidbody force math in MonoBehaviour | Estimate: pending.
- [ ] Task 07 ABYSSAL_CURRENT_ADVECTION_INTEGRATION | DOD: current sample path with trilinear/cheap fallback | Alternative rejected: Unity fluid physics | Estimate: pending.
- [ ] Task 08 THE_DEAR_LIE_BATTERY_CONSUMPTION | DOD: low cadence linear drain job | Alternative rejected: per-frame joule/RPM simulation | Estimate: pending.
- [ ] Task 09 CONTINUOUS_SCALABILITY_PHYSICS_CADENCE | DOD: continuous GlobalQualityWeight cadence math | Alternative rejected: binary low/high switch | Estimate: pending.
- [ ] Task 10 ASYNCHRONOUS_FORCE_PACKET_DISPATCH | DOD: queue ForcePacketDTO for PhysicsApplySystem | Alternative rejected: direct player body mutation | Estimate: pending.
- [ ] Task 11 AUP_PRECISION_AUDIO_DOPPLER_MATH | DOD: double precision AUP delta before float magnitude | Alternative rejected: Rigidbody.velocity audio truth | Estimate: pending.
- [ ] Task 12 ROLLBACK_NETCODE_EXCLUSION_FENCE | DOD: physical and visual DTO segregation | Alternative rejected: hashing presentation state | Estimate: pending.
- [ ] Task 13 ZERO_INIT_OVERHEAD_BYPASS | DOD: uninitialized-memory request path where safe | Alternative rejected: redundant MemClear/zero-fill | Estimate: pending.
- [ ] Task 14 TELEMETRY_PROPULSION_RECORDER | DOD: 300-entry fixed telemetry ring + dump path | Alternative rejected: string logs after crash | Estimate: pending.
- [ ] Task 15 SEAGLIDE_DYNAMICS_XRAY_WINDOW | DOD: editor-only xray window if existing editor pattern allows | Alternative rejected: runtime debug UI | Estimate: pending.
- [ ] Task 16 CSV_VEHICLE_PROFILES_INGESTOR | DOD: cold span/byte parser | Alternative rejected: float.Parse/string split | Estimate: pending.
- [ ] Task 17 LIVE_CURRENT_DEBUG_GIZMO | DOD: editor-only gizmo facade | Alternative rejected: runtime debug objects | Estimate: pending.
- [ ] Task 18 ARCHITECTURAL_METRIC_VALIDATOR | DOD: static scanner/report JSON | Alternative rejected: manual grep report | Estimate: pending.
- [ ] Task 19 UNALIGNED_MEMORY_TRAP_GUARD | DOD: editor initialize validator for DTO size/alignment | Alternative rejected: unchecked layout drift | Estimate: pending.
- [ ] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | DOD: static self-audit, compile/static verification, log append | Alternative rejected: chat-only report | Estimate: pending.

## Latest Readback

Created from extracted CURRENT_BATCH.md prompt. No coding started at creation time.
