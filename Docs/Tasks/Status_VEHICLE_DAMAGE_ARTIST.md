# VEHICLE_DAMAGE_ARTIST Status

Agent: VFX_TECHNICAL_ARTIST
Domain: ECHELON 6 HABITAT & VEHICLES / Hull Integrity VFX
Prompt: Shader Hull Deformation
Status: PENDING VERIFICATION

## Mandates Read

- CORE_Damage_System_Hull_Integrity_VFX_Feedback.txt
- CORE_Submarine_Vehicles_Kinematics_AUP.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- REND_Shader_Noir_Aesthetics_Dithering_Fog.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt

## State Machine

- [x] Task 1: SINGLETON ERADICATION: N/A. | Justification: no new singleton; controller registers through `GlobalRegistry` late-frame lane and uses typed signal bus | Alternative rejected: static owner lifecycle beyond shader property IDs | Estimate: 0 us runtime ownership overhead
- [x] Task 2: SIGNAL MIGRATION: Consume `CombatDamageSignal`. | Justification: `HullDentShaderController` reads `SignalBus<CombatDamageSignal>.GetFrameSnapshot()` once per frame | Alternative rejected: legacy `DamageSignal` receiver or direct structural-grid dependency | Estimate: 2-8 us/frame when snapshot is populated
- [x] Task 3: ASMDEF ISOLATION: `Hecton8.Vehicles.VFX` -> Contracts. | Justification: added `Hecton8.Vehicles.VFX.asmdef` referencing Core Contracts/Core for signal and registry contracts | Alternative rejected: dumping vehicle VFX into root core assembly | Estimate: 0 us runtime, compile-boundary only
- [x] Task 4: DEAD CODE HUNT: Eradicate any `Mesh.vertices` read/write loops used for legacy damage. | Justification: scan found mesh writes in procedural world/UI/VFX builders, not hull damage; no unrelated generators touched | Alternative rejected: deleting non-damage mesh builders | Estimate: avoided 100-800 us per impact versus CPU hull mesh mutation
- [x] Task 5: THE DENT ARRAY: Create global `_HectonHullDents` Vector4[16]. | Justification: fixed `Vector4[16]` preallocated in controller; `w` packs quantized radius/depth | Alternative rejected: material-property blocks, dynamic lists, CPU vertex buffers | Estimate: 0 B/impact, 16-vector upload only when dirty
- [ ] Task 6: SIGNAL INGESTION: Submarine hit AUP -> submarine local space. | Justification pending | Alternative pending | Estimate pending
- [ ] Task 7: RING BUFFER: Push impact into fixed dent array. | Justification pending | Alternative pending | Estimate pending
- [ ] Task 8: SHADER UPLOAD: `Shader.SetGlobalVectorArray`, no MPB. | Justification pending | Alternative pending | Estimate pending
- [ ] Task 9: VERTEX SHADER: 16 dents with squared-distance dot math. | Justification pending | Alternative pending | Estimate pending
- [ ] Task 10: DEPRESSION MATH: inward normal offset by falloff * depth. | Justification pending | Alternative pending | Estimate pending
- [ ] Task 11: NORMAL CHEAT: darken albedo/smoothness by depression. | Justification pending | Alternative pending | Estimate pending
- [ ] Task 12: COLLIDER CHEAT: no MeshCollider update. | Justification pending | Alternative pending | Estimate pending
- [ ] Task 13: REPAIR COUPLING: healed breach fades matching dent depth to 0. | Justification pending | Alternative pending | Estimate pending
- [ ] Task 14: AUP SHIFT SAFETY: local-space dents survive origin shifts. | Justification pending | Alternative pending | Estimate pending
- [ ] Task 15: MATH LOD: Low tier bypasses vertex loop, uses decal/scar scalar. | Justification pending | Alternative pending | Estimate pending
- [ ] Task 16: ZERO-GC: preallocated arrays, 0 bytes per impact. | Justification pending | Alternative pending | Estimate pending
- [ ] Task 17: TELEMETRY: write `ActiveHullDents` to Blackbox. | Justification pending | Alternative pending | Estimate pending
- [ ] Task 18: EVENT BUS: emit `HullDeformedSignal` for audio groaning. | Justification pending | Alternative pending | Estimate pending
- [ ] Task 19: OMEGA COMPILE CHECK: verify shader loop unroll intent. | Justification pending | Alternative pending | Estimate pending

## Iteration Log

### Loop 0 - Prompt And Mandate Intake

- Extracted exact `<AGENT_PROMPT id="VEHICLE_DAMAGE_ARTIST">` from `Docs/Tasks/CURRENT_BATCH.md` via PowerShell regex over full file.
- Read domain authority from `Docs/Actual Domains of Project.txt`.
- Read AGENTS.md, mandate registry, and 8 task-relevant mandates.
- Source reconnaissance started: located `Hecton_CoreLit.hlsl`, `GlobalSignals`, `SubmarineStructuralGrid`, shader inventory, asmdefs, and runtime mesh-vertex write scan.
- Verification status remains PENDING VERIFICATION.

### Loop 1 - Tasks 1-5 Impact Buffer Foundation

- Added `HullDeformedSignal` typed signal lane and validation in `GlobalSignals`.
- Added `Hecton8.Vehicles.VFX` assembly boundary and `HullDentShaderController` with fixed `_HectonHullDents` upload authority.
- Expanded `ISubmarineHullBreachReadModel` with active local breach reads for later repair coupling.
- Added black-box `ReportHullDentState` telemetry path for active dent count.
- Re-extracted the prompt from `CURRENT_BATCH.md` after Task 3 per anti-amnesia rule.
- Compile check: `dotnet build Hecton8.Core.csproj --no-restore` failed on pre-existing project reference gaps (`Hecton8.Core.Scheduling`, `Hecton8.Core.Memory.Layout`, audio propagation, etc.). Unity refresh compile request timed out waiting for editor readiness; final verification remains pending.
