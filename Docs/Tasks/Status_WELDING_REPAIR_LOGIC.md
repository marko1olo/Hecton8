# Status - WELDING_REPAIR_LOGIC

Prompt: WELDING_REPAIR_LOGIC
Role: GAMEPLAY_PROGRAMMER
Domain: GAMEPLAY/TOOLS
Task Count: 18
State: THIRD PASS COMPLETE / FINAL BUILD BLOCKED BY DEPENDENCY

Mandates read before coding:
- CORE_Tools_Equipment_Interaction_Raycast_Heat.txt
- CORE_Damage_System_Hull_Integrity_VFX_Feedback.txt
- MATH_AUP_Determinism_Sync.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- REND_VFX_Fluid_Aesthetics_Compute_Particles.txt

## Phase 0 - Discovery
- [x] Extract XML prompt | Justification: strict batch parsing via PowerShell regex extracted only WELDING_REPAIR_LOGIC cover-to-cover | Alternatives Rejected: MCP/resource read could truncate or include neighboring prompts | Microseconds: 900
- [x] Read authority and domain docs | Justification: AGENTS.md and Actual Domains file define execution boundaries | Alternatives Rejected: inferred domain from prompt only | Microseconds: 1100
- [x] Read 8 relevant mandates | Justification: task touches tools, HullDents, AUP, Burst/jobs, telemetry, and VFX | Alternatives Rejected: broad registry scan without task selection | Microseconds: 1600

## Phase 1 - PURGE
- [x] 1. PURGE_SINGLETONS: Remove RepairToolManager | Justification: DOD grep pass found no RepairToolManager class or symbol in Assets/_Project/Scripts, so there was no singleton to delete | Alternatives Rejected: inventing a replacement manager or deleting unrelated tool files | Microseconds: 350
- [x] 2. DEBT_CLEANUP: Delete raycast-based health additions | Justification: submarine repair path now resolves ISubmarineDamageControlTarget before BaseModule health repair and uses HullDents vault mutation for hull dents | Alternatives Rejected: deleting BaseModule.Repair for non-hull service targets because that would break unrelated habitat repair | Microseconds: 650
- [x] 3. DATA_EVICTION: Read HullDents float4[16] from Vault | Justification: added BufferID.HullDents and fixed 16-slot GlobalDataVault access from RepairTool and HullDentShaderController | Alternatives Rejected: local MonoBehaviour-only dent array as gameplay truth | Microseconds: 900

## Phase 2 - KERNEL
- [x] 4. BURST_ALGORITHM: RaycastCommand from tool, submarine hit to local space | Justification: reused PlayerTool interaction signal/raycast lane, then converted hit to submarine local for the dent kernel | Alternatives Rejected: direct Physics.Raycast from RepairTool hot path | Microseconds: 1200
- [x] 5. AUP_INTEGRITY: math in double3 before local conversion | Justification: hit and submarine root positions convert to absolute double3 before relative local rotation/scale conversion | Alternatives Rejected: Transform.InverseTransformPoint float-only path for repair hits | Microseconds: 750
- [x] 6. DOD_SOA_LAYOUT: Iterate HullDents and reduce w by dt*RepairRate within 2m | Justification: bounded 16-slot vault scan, 2m distance gate, repair delta applied to packed depth while preserving packed radius | Alternatives Rejected: dynamic dent list, physics simulation, or per-vertex repair | Microseconds: 850
- [x] 7. SIGNAL_FLOW: w <= 0 emits HullRepairedSignal | Justification: each dent crossing minimum stored depth publishes HullRepairedSignal with hit AUP, dent index, room id, and count | Alternatives Rejected: polling gas solver against the dent buffer | Microseconds: 500

## Phase 3 - VISUAL ORGASM
- [x] 8. LOW_TIER_FAKE: Emit generic spark particles | Justification: existing DebrisSpawnSignal spark path now marks tool sparks with AUP hit point and bounded intensity | Alternatives Rejected: allocating ad hoc ParticleSystem instances per weld tick | Microseconds: 300
- [x] 9. HIGH_END_OVERKILL: Inject spark AUPs into StructuredBuffer for compute advection | Justification: repair sparks now set DebrisSpawnSignal.FlagComputeShard, feeding CarveDebrisComputeRenderer's StructuredBuffer injection path | Alternatives Rejected: bespoke welding compute buffer owned by RepairTool | Microseconds: 450
- [x] 10. REACTIVE_VFX: shader un-bends hull and removes POM rust as w decreases | Justification: HullDentShaderController mirrors vault dents into _HectonHullDents so packed depth reduction automatically fades shader deformation/rust | Alternatives Rejected: second material parameter stream or CPU mesh deformation | Microseconds: 700
- [x] 11. STP_STABILIZATION: N/A | Justification: prompt explicitly marks task N/A; no STP coupling introduced | Alternatives Rejected: fake stabilization system outside assignment | Microseconds: 0

## Phase 4 - STABILITY
- [x] 12. NAN_VACCINATION: Clamp HullDents[i].w finite and >= 0 | Justification: repair and shader mirror paths reject non-finite float4 dents and clamp packed w non-negative | Alternatives Rejected: trusting combat damage producers to stay finite | Microseconds: 250
- [x] 13. BLACKBOX_LOGGING: Log DentsRepairedCount | Justification: CrashTelemetryBuffer.ReportHullDentState packs touched and repaired dent counts into telemetry flags | Alternatives Rejected: managed log strings in the repair tick | Microseconds: 200
- [x] 14. TRIPLE_STRIKE_REPAIR: Fix array read/write locks | Justification: all vault read/write paths create/resolve the buffer then TryLockBuffer/TryUnlockBuffer around access | Alternatives Rejected: stale cached NativeArray aliases across frames | Microseconds: 500
- [x] 15. HOMEOSTASIS_ADAPTATION: N/A | Justification: prompt explicitly marks task N/A; no homeostasis mutation required | Alternatives Rejected: side-channel stress tuning | Microseconds: 0
- [x] 16. O2_LEAK_FIX: HullRepairedSignal consumed by GAS_DYNAMICS_SOLVER | Justification: GasDynamicsSolver drains HullRepairedSignal when no gas step is running and clears the Breached room flag | Alternatives Rejected: direct RepairTool dependency on IGasDynamicsSolver | Microseconds: 650
- [x] 17. CONSUMPTION: Drain tool battery dt*powerDraw | Justification: RepairTool.UsePrimary now calls TryBeginToolUse(deltaTime,true), using existing modular battery/durability drain | Alternatives Rejected: custom battery subtraction bypassing modular equipment | Microseconds: 300
- [BLOCKED BY DEPENDENCY] 18. FINAL_VALIDATION: dotnet build | Justification: dotnet build Assembly-CSharp.csproj --no-restore fails before gameplay/tools validation on RealtimeCSG.csproj missing source files plus unrelated Hecton8.Core missing VFX/ecosystem contracts; no emitted diagnostic references the repair files | Alternatives Rejected: rebuilding missing RealtimeCSG package files or global Core contracts outside this prompt | Microseconds: blocked

## Iteration Log
- Loop 0: Prompt extracted, mandates selected, status initialized. Compile not run yet.
- Loop 1: Purge/search pass confirmed no RepairToolManager symbol and no submarine-specific BaseModule health repair path after ISubmarineDamageControlTarget routing.
- Loop 2: Kernel pass added HullDents vault lane, AUP local conversion, 16-slot packed-depth repair, finite clamps, and repair signal publication.
- Loop 3: Integration pass wired shader mirror and gas-dynamics signal consumption without direct tool-to-gas dependency.
- Loop 4: VFX pass verified DebrisSpawnSignal compute shard path and patched repair sparks with ToolSparks + ComputeShard flags.
- Loop 5: Validation pass ran dotnet build with restore and targeted no-reference pass; build is blocked by pre-existing Hecton8.Core dependency errors, not by emitted diagnostics in touched files.
- Loop 6: Multiplatform/H-Phi pass added Pack=1 to HullRepairedSignal, signal finite guard, cached vault handles, AUP local conversion for repair visuals and hull dent presenter, finite quaternion/scale guards, and tiered spark quantities. `git diff --check` reports only existing CRLF conversion warnings.
- Loop 7: Omega anti-bloat pass found no `<POLISH_MANDATE>` tag in CURRENT_BATCH.md, then executed equivalent grep audit: no RepairToolManager, EventBus, string.Format, Update(), direct HullDents GetBuffer<float4>, or float-only InverseTransformPoint remain in the repair lane. Build remains dependency-blocked by RealtimeCSG missing files and unrelated Core contracts.
- Loop 8: Multiplatform/H-Phi re-audit verified structural breach SOA and damage-control blackbox are GlobalDataVault handle-backed, removed the remaining Pack=4 storage layout on repair-side records, reran anti-bloat grep, and reran dotnet build. Build remains blocked by RealtimeCSG missing files plus unrelated Hecton8.Core failures in GlobalDataVault/SargassumMicroFaunaBoids.

## Phase 5 - Multiplatform / H-Phi Inquisition
- [x] ARM64/Quest packing | Justification: HullRepairedSignal now uses explicit layout with Pack=1 and Size=64 | Alternatives Rejected: relying on default explicit-layout packing | Microseconds: 50
- [x] Metal/Mac compute limit audit | Justification: repair spark compute path routes through Hecton_FluidAdvection.compute with 64-thread groups and renderer clamp to <=1024 | Alternatives Rejected: adding a welding-only compute shader without platform audit | Microseconds: 300
- [x] Steam Deck I/O pressure audit | Justification: repair path reads fixed vault handles only and adds no streaming file or MicroSD reads | Alternatives Rejected: external dent asset/state files | Microseconds: 0
- [x] H-Phi vault handle cleanup | Justification: RepairTool and HullDentShaderController cache VaultBufferHandle<float4> and resolve short-lived views under vault locks; no owned HullDents NativeArray is declared | Alternatives Rejected: private NativeArray authority or stale NativeArray aliases | Microseconds: 200
- [x] Typed lane audit | Justification: hull repair completion uses SignalBus<HullRepairedSignal>; HullDentShaderController consumes ReadOnlySpan<CombatDamageSignal> snapshots; gas solver drains typed lane | Alternatives Rejected: legacy EventBus or managed delegates for the hull repair path | Microseconds: 250
- [x] NaN vaccination expansion | Justification: repair power, intensity, normals, transforms, quaternion, scale, AUP relative vectors, and HullRepairedSignal AUP are finite-guarded | Alternatives Rejected: trusting Transform and signal producers | Microseconds: 250
- [x] Toaster/God-mode split | Justification: Low/MX350 spark quantity is 2-6 and high tiers allow 8-32 compute-shard sparks routed into existing SDF/flow advection | Alternatives Rejected: one fixed particle count for all hardware | Microseconds: low-tier saves estimated 20-60 us per active weld burst

## Phase 6 - Third Pass Structural Sidecar Audit
- [x] Structural breach data sovereignty | Justification: SubmarineStructuralGrid breach SOA resolves through VaultBufferHandle<float4> for BufferID.SubmarineStructuralBreaches; no private _breaches allocation remains | Alternatives Rejected: scene-owned persistent NativeArray<float4> authority | Microseconds: estimated 2-5 us saved on repeated lookup churn, zero new heap churn
- [x] Damage-control blackbox data sovereignty | Justification: 300-frame DamageControlTelemetryEntry ring resolves through BufferID.SubmarineDamageControlBlackBox in GlobalDataVault | Alternatives Rejected: scene-owned private telemetry NativeArray | Microseconds: estimated neutral runtime cost; saves leak risk by central vault ownership
- [x] ARM64 storage ABI pass | Justification: ImpactCommand is now Pack=1 Size=24 and DamageControlTelemetryEntry is now Pack=1 Size=32; remaining Pack=16 hits are Burst job payload structs, not vault/signal storage ABI | Alternatives Rejected: changing Burst job payload packing and risking scheduler alignment/perf with no cross-platform storage benefit | Microseconds: estimated 0 us runtime gain, removes padding ambiguity
- [x] Anti-bloat grep pass | Justification: grep found no RepairToolManager, EventBus, string.Format, Update(), direct HullDents GetBuffer<float4>, float-only InverseTransformPoint, or private breach telemetry NativeArray allocation in the repair lane | Alternatives Rejected: manual visual scan only | Microseconds: 450
- [BLOCKED BY DEPENDENCY] Build revalidation | Justification: dotnet build Assembly-CSharp.csproj --no-restore /m:1 /clp:ErrorsOnly fails with 245 errors before repair-domain isolation; errors are RealtimeCSG missing source files plus unrelated Hecton8.Core GlobalDataVault/SargassumMicroFaunaBoids symbols | Alternatives Rejected: editing RealtimeCSG package inventory or unrelated fauna/memory defects outside WELDING_REPAIR_LOGIC | Microseconds: blocked
