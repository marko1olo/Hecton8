# Status_CAMERA_JUICE_SYSTEM

Agent: CAMERA_JUICE_SYSTEM
Role: MOTION_ENGINEER
Domain: ECHELON 9 META/POLISH/INTEGRATION - Camera Juice & Shake
Task Count: 19
Status: PENDING VERIFICATION

Mandates loaded before code:
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- MATH_Deterministic_RNG_SlotMachine.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- REND_VR_Stencil_Masking.txt

## Assignment Source

Extracted from Docs/Tasks/CURRENT_BATCH.md:
`<AGENT_PROMPT id="CAMERA_JUICE_SYSTEM" role="MOTION_ENGINEER" chat_name="Procedural Screen Shake">`

## Loop 0 - Setup

- [x] Extract prompt | DOD: CLI regex extraction from CURRENT_BATCH.md, full XML tag captured | Rejected: MCP/basic reader because protocol requires CLI cover-to-cover extraction | Estimate: 80 us
- [x] Verify task count | DOD: counted PRIMARY OBJECTIVES entries 1-19 | Rejected: guessing from domain map item number | Estimate: 20 us
- [x] Load mandates | DOD: read 8 task-relevant registry mandates before code | Rejected: broad registry bulk-load because mandate context must stay scoped | Estimate: 600 us
- [ ] Read existing camera/event/registry systems | DOD pending | Rejected pending | Estimate pending

## Tasks

- [ ] 1. SINGLETON ERADICATION: Purge CameraManager.Instance. Register ICameraJuiceSystem.
- [ ] 2. SIGNAL MIGRATION: No direct calls to Shake(float). Consume ImpactSignal, CombatDamageSignal.
- [ ] 3. ASMDEF ISOLATION: Limit dependencies to Contracts and Mathematics.
- [ ] 4. DEAD CODE HUNT: Eradicate CinemachineImpulseListener from the main camera if it causes GC or overhead.
- [ ] 5. TRAUMA SCALAR S.O.A.: Define float _trauma in the CameraRig. Range 0.0 to 1.0.
- [ ] 6. TRAUMA DECAY: LateUpdate decay with math.max.
- [ ] 7. SHAKE MATH: intensity = _trauma * _trauma.
- [ ] 8. PROCEDURAL PERLIN: six offsets via Unity.Mathematics.noise.cnoise.
- [ ] 9. CAMERA MATRIX MULTIPLY: apply local offsets after mouse-look and KCC.
- [ ] 10. ROLL-SPRING RECOVERY: damped Z-roll recovery.
- [ ] 11. HIT STOP: request CORE_TICK_DILATION 0.05 for exactly 3 frames on severity > 0.8.
- [ ] 12. FOV KICK: temporary FOV bump with CinematicMath.FastNlerp or local equivalent.
- [ ] 13. DIRECTIONAL BIAS: first-frame shake bias from ImpactSignal direction.
- [ ] 14. SIGNAL SEVERITY: map ImpactSignal severity to trauma additions.
- [ ] 15. ORIGIN SHIFT SAFETY: local shake unaffected by AupShiftSignal.
- [ ] 16. VR COMFORT OVERRIDE: disable translation/FOV kick, 10 percent rotation.
- [ ] 17. MATH LOD: Low Tier evaluates noise at 30Hz and interpolates.
- [ ] 18. ZERO-GC: hot path noise calculations allocate 0 bytes.
- [ ] 19. OMEGA COMPILE CHECK: verify float3 and quaternion math.

## Verification

- Compile: PENDING VERIFICATION
- Unity Console: PENDING VERIFICATION
- GC: measured proof absent, PENDING VERIFICATION

