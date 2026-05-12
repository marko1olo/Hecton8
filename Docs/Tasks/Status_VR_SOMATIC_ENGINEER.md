# Status_VR_SOMATIC_ENGINEER

Agent: VR_SOMATIC_ENGINEER
Role: UX_ENGINEER
Domain: ECHELON 4 VR Somatic Comfort / OpenXR Kinematics
Batch Source: Docs/Tasks/CURRENT_BATCH.md
Task Count: 18

Mandates read:
- ARCH_Global_Registry_ServiceLocator_DI_Init
- ARCH_Project_Bootstrap_Sequence_Init_Safety
- CTRL_Device_Abstraction_Haptics
- PHYS_Kinematic_Interaction_Hands
- MATH_Coordinate_Precision_AUP_FloatingOrigin
- OPT_Zero_GC_Policy_AllocFree_Mandate
- REND_Foveated_Simulation_LOD
- DBG_Telemetry_Crash_Reporting_PostMortem

Loop 0 state:
- Extracted the VR_SOMATIC_ENGINEER XML tag from Docs/Tasks/CURRENT_BATCH.md by CLI regex.
- Read project domain boundaries and mandate set before source edits.
- Initial source survey found no VRManager.Instance call sites and no XRDevice usage in gameplay tools.

Checklist:
- [ ] Task 1: Delete VRManager.Instance dependency and register VR Bridge via GameBootstrapper. DOD: bootstrap-owned service creation only. Rejected: runtime self-spawn as primary authority. Estimate: pending.
- [ ] Task 2: Tools do not check VR controllers directly; VR rig pushes ToolTriggerSignal through EventBus/GlobalSignals. DOD: XR trigger decoded in bridge/input layer only. Rejected: tool-local XR polling. Estimate: pending.
- [ ] Task 3: Hecton8.VR asmdef depends only on Hecton8.Core.Contracts and OpenXR. DOD: assembly boundary verified or blocked with dependency note. Rejected: widening Core dependencies. Estimate: pending.
- [ ] Task 4: Pure Action-Based OpenXR bridge. DOD: action values translated once into contract data. Rejected: legacy XRDevice presence checks. Estimate: pending.
- [ ] Task 5: VR Camera Rig not child of submarine; own AUP, sync matrix to submarine interior via Burst job with rotational smoothing. DOD: decoupled runtime root and Burst root sync. Rejected: childing rig to player/submarine. Estimate: pending.
- [ ] Task 6: Horizon locking above 15 degrees pitch/roll, max tilt 15. DOD: no Euler hot path, bounded visual correction. Rejected: physically rotating player body. Estimate: pending.
- [ ] Task 7: NativeArray<float3> HandTargets and HandPhysicalPositions. DOD: persistent native buffers. Rejected: managed per-frame hand arrays. Estimate: pending.
- [ ] Task 8: Burst hand spring job moves physical toward target. DOD: scheduled Burst job. Rejected: transform-only hand snap. Estimate: pending.
- [ ] Task 9: Ghost holographic hand if distance >0.2m, low tier disables. DOD: deterministic ghost mask/LOD gate. Rejected: always-on visual ghost. Estimate: pending.
- [ ] Task 10: FOV tunneling from headset angular velocity and publish _VRComfortVignette. DOD: shader global scalar from smoothed angular speed. Rejected: extra post process pass. Estimate: pending.
- [ ] Task 11: Integrate comfort vignette into HectonVisorUberPost, no new pass. DOD: existing shader path consumes scalar. Rejected: separate renderer feature pass. Estimate: pending.
- [ ] Task 12: Lever grab math uses dot/cross, no joints. DOD: inspect or block by domain if absent. Rejected: Joint/Rigidbody constraints. Estimate: pending.
- [ ] Task 13: Pilot chair nlerp root to socket and disable manual KCC. DOD: inspect or block by ownership if absent. Rejected: instant recenter. Estimate: pending.
- [ ] Task 14: Haptics listen to ImpactSignal. DOD: contract-path haptic enqueue. Rejected: direct haptic calls in collision code. Estimate: pending.
- [ ] Task 15: AUP shift sync. DOD: root/hand runtime state survives origin shifts. Rejected: world-space drift. Estimate: pending.
- [ ] Task 16: Math LOD disables ghost hand on low tier. DOD: low-tier flag suppresses ghost. Rejected: balanced middle setting. Estimate: pending.
- [ ] Task 17: Zero-GC OpenXR polling. DOD: no managed allocations in hot poll path. Rejected: LINQ/device list allocation. Estimate: pending.
- [ ] Task 18: Verify Burst hand job compiles. DOD: dotnet build or Unity compile gate. Rejected: chat-only claim. Estimate: pending.

Verification:
- Compile: pending.
- Prompt reread after task 3: pending.
- Prompt reread after task 6: pending.
- Prompt reread after task 9: pending.
- Prompt reread after task 12: pending.
- Prompt reread after task 15: pending.
- Polish mandate: locked until checklist is complete or blocked.
