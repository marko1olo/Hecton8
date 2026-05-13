# VR_COCKPIT_MANUAL_OVERRIDE Status

Agent: UX_ENGINEER  
Domain: UX_ENGINEER / OpenXR Physical Levers  
Prompt tasks: 15  
Batch source: `Docs/Tasks/CURRENT_BATCH.md`  
Execution lane: SIMULATION / `PriorityLayer.Player`

## Loop 1 - Tasks 1-5

- [x] 1. Singleton eradication N/A. DOD: no new singleton or runtime owner introduced; lever is scene component registered through `GlobalRegistry` dispatcher. Rejected: static lever manager because 20+ agents would collide on global state. Estimate: 4 us cold registration, 0 us steady singleton overhead.
- [x] 2. Consume `UniversalInputStateSignal` XR Grip; emit `ManualOverridePulledSignal`. DOD: frame `PlayerInputState` adapted into `UniversalInputStateSignal`, grip reads Interact/SecondaryFire mask, latch publishes typed signal. Rejected: direct InputSystem polling in lever because it bypasses dispatcher input authority. Estimate: 0.8 us per tick.
- [x] 3. ASMDEF isolation `Hecton8.UI.VR` -> Contracts. DOD: added `Hecton8.UI.VR.Contracts` read model and runtime asmdef referencing Core plus Universal input. Rejected: placing lever in monolithic Core assembly because prompt required isolation. Estimate: compile boundary only; 0 runtime us.
- [x] 4. Lever S.O.A. native state. DOD: `NativeArray<float>` angles/velocities/targets and `NativeArray<float3>` pivots registered with `NativeMemorySentinel`. Rejected: MonoBehaviour fields as authoritative state because blackbox and job kernels need blittable lanes. Estimate: 0.4 us state access.
- [x] 5. Grab detection. DOD: physical hand receiver caches hand pose only when local distance is within 0.15m and grip is confirmed in tick before lock. Rejected: `HingeJoint` and broad `GetComponent` scanning. Estimate: 1.2 us receiver check after existing overlap.

## Loop 2 - Tasks 6-10

- [ ] 6. Angular solver: local projection with `math.atan2`.
- [ ] 7. Resistance fake: damped spring velocity.
- [ ] 8. Click latch above 85 degrees and emit.
- [ ] 9. Haptic ratchet every 10 degrees.
- [ ] 10. Non-VR fallback hold Interact for 1.5s.

## Loop 3 - Tasks 11-15

- [ ] 11. AUP shift safety via local space.
- [ ] 12. Math LOD: low tier reduced IK smoothing.
- [ ] 13. Execution phase: SIMULATION.
- [ ] 14. Zero-GC projection hot path.
- [ ] 15. Compile check and dot/cross verification.

## Compile Attempts

- Pending.
