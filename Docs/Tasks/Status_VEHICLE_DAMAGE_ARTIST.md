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
- [x] Task 6: SIGNAL INGESTION: Submarine hit AUP -> submarine local space. | Justification: direct runtime `CombatDamageSignal.WorldPoint` is transformed through `submarineRoot.InverseTransformPoint`; legacy mirrored local points are accepted by flag | Alternative rejected: storing world/AUP position in shader buffer | Estimate: 1 matrix inverse per accepted impact, 1-4 us/impact
- [x] Task 7: RING BUFFER: Push impact into fixed dent array. | Justification: 16-slot power-of-two write head overwrites oldest slot and merges near dents | Alternative rejected: dynamic list or unbounded history | Estimate: 16 squared-distance checks, 1-3 us/impact
- [x] Task 8: SHADER UPLOAD: `Shader.SetGlobalVectorArray`, no MPB. | Justification: global array and params upload only when dirty | Alternative rejected: per-renderer `MaterialPropertyBlock` | Estimate: 16 vectors per dirty frame; no per-sub renderer walk
- [x] Task 9: VERTEX SHADER: 16 dents with squared-distance dot math. | Justification: `Hecton_CoreLit.hlsl` iterates `HECTON_HULL_DENT_MAX` with `distSq = dot(delta, delta)` | Alternative rejected: `distance()`/sqrt or CPU vertex deformation | Estimate: high tier max 16 dot tests/vertex, low tier bypass
- [x] Task 10: DEPRESSION MATH: inward normal offset by falloff * depth. | Justification: object-space vertex offset subtracts normalized local normal by `falloff^2 * depth` | Alternative rejected: recalculating mesh vertices/colliders | Estimate: no CPU cost; shader ALU only on active high-tier path
- [x] Task 11: NORMAL CHEAT: darken albedo/smoothness by depression. | Justification: fragment path uses dent shadow to darken albedo and reduce smoothness without normal recalculation | Alternative rejected: physical normal rebuild or tangent-space dent normal map generation | Estimate: 2 lerps/pixel on affected shader path
- [x] Task 12: COLLIDER CHEAT: no MeshCollider update. | Justification: controller never touches `MeshCollider`, `Mesh`, or vertex arrays; deformation is shader-only | Alternative rejected: collider rebake or runtime mesh write | Estimate: saves broadphase/collider rebuild cost entirely
- [x] Task 13: REPAIR COUPLING: healed breach fades matching dent depth to 0. | Justification: reads `ISubmarineHullBreachReadModel` active local breach outputs and fades dents not backed by a breach | Alternative rejected: physics-to-VFX direct dependency or new repair-only signal | Estimate: max 1024 squared-distance checks on repair frames, 6-20 us low-end
- [x] Task 14: AUP SHIFT SAFETY: local-space dents survive origin shifts. | Justification: dents are stored and evaluated in submarine local/object space, no world/AUP coordinates persist | Alternative rejected: world-space dent centers with rebase correction | Estimate: 5-20 us/frame rebase correction avoided during active dents
- [x] Task 15: MATH LOD: Low tier bypasses vertex loop, uses decal/scar scalar. | Justification: HLSL returns early when `_HectonHullDentParams.y` is set and applies a texture-masked scar in fragment | Alternative rejected: full 16-loop on MX350 | Estimate: saves 16 dot tests/vertex on low tier
- [x] Task 16: ZERO-GC: preallocated arrays, 0 bytes per impact. | Justification: dent storage is one cold `Vector4[16]`; signal consumption uses `ReadOnlySpan`; impact path uses structs only | Alternative rejected: lists, LINQ, MPBs, compute buffers | Estimate: 0 B/impact managed allocations
- [x] Task 17: TELEMETRY: write `ActiveHullDents` to Blackbox. | Justification: `CrashTelemetryBuffer.ReportHullDentState` writes active dent count into black-box ring | Alternative rejected: log spam or analytics-only counters | Estimate: 1 telemetry ring write on dent/repair change
- [x] Task 18: EVENT BUS: emit `HullDeformedSignal` for audio groaning. | Justification: typed `HullDeformedSignal` lane added and controller publishes on accepted dent impact | Alternative rejected: direct audio call or legacy event coupling | Estimate: one unmanaged signal enqueue per accepted impact
- [x] Task 19: OMEGA COMPILE CHECK: verify shader loop unroll intent. [BLOCKED BY DEPENDENCY] | Justification: source has `[unroll]` fixed `HECTON_HULL_DENT_MAX` loop and no `distance()` in dent path; Unity compile unavailable | Alternative rejected: declaring green compile without editor/compiler proof | Estimate: low-tier 0 vertex-loop ALU, high-tier max 16 dot tests/vertex

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

### Loop 2 - Tasks 6-10 Local Impact To Vertex Lie

- Finished AUP/world-to-submarine-local ingestion in `HullDentShaderController`.
- Implemented fixed ring buffer, merge radius, quantized radius/depth packing, and dirty-only global shader upload.
- Added `_HectonHullDents` and `_HectonHullDentParams` to `Hecton_CoreLit.hlsl`.
- Added HLSL unpack/evaluate/apply helpers with `[unroll]` 16-loop and squared-distance `dot` math; grep found no `distance()` use in the new dent path.
- Wired `Hecton_DryZoneLit.shader` vertex path to object-space dents and carried dent shadow to the fragment path.
- Re-extracted the prompt from `CURRENT_BATCH.md` after Task 9 per anti-amnesia rule.
- Compile verification still pending because Unity MCP session remained unavailable after editor readiness timeout.

### Loop 3 - Tasks 11-15 Cheat, Repair, LOD

- Added surface-only dent darkening and smoothness reduction through `HectonCoreLitApplyHullDentSurfaceCheat`; physical normals remain unchanged.
- Verified the controller has no `MeshCollider`, `Mesh.vertices`, or runtime vertex-buffer mutation path.
- Implemented repair fade against `ISubmarineHullBreachReadModel.TryGetActiveBreach`.
- Reconfirmed local-space storage and object-space shader evaluation for AUP shift immunity.
- Added low-tier bypass using `_HectonHullDentParams.y` plus `_DetailMask` texture scar sampling in `Hecton_DryZoneLit.shader`.
- Re-extracted the prompt from `CURRENT_BATCH.md` after Task 15 per anti-amnesia rule.

### Loop 4 - Tasks 16-19 GC, Telemetry, Event, Compile Wall

- Zero-GC audit: impact path uses fixed array, spans, structs, and no LINQ/list allocations; cold allocations are documented.
- Telemetry path: added `CrashTelemetryBuffer.ReportHullDentState` and controller writes `ActiveHullDents` on dent/repair changes.
- Event bus path: added `HullDeformedSignal`, configured lane capacity, and published signal on accepted impact.
- Shader unroll proof: `Hecton_CoreLit.hlsl` uses `[unroll] for (int i = 0; i < HECTON_HULL_DENT_MAX; i++)`, and dent distance is squared `dot`.
- Compile wall: `dotnet build Hecton8.Core.csproj` fails on pre-existing assembly reference gaps; Unity refresh compile timed out twice and console access returns `no_unity_session`. Task 19 marked blocked by dependency, not green.

### Loop 5 - Omega Polish

- Parsed `<POLISH_MANDATE id="OMEGA_POLISH">` only after all core tasks were checked or dependency-blocked.
- Anti-bloat pass replaced controller divisions with `math.rcp` reciprocal multiplies.
- Re-ran source scans for `foreach`, LINQ, `string.Format`, `.ToString()`, unconditional `math.sqrt`, and `math.normalize` in the dent controller/path; no new violations found.
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore`; still blocked by global project reference gaps unrelated to hull deformation.
- Status remains PENDING VERIFICATION because Unity compiler/console proof is unavailable.
