# Status_MAELSTROM_KINEMATICS

Agent: LOCOMOTION_ENGINEER  
Prompt: MAELSTROM_KINEMATICS  
Domain: ECHELON 4 - PLAYER, KINEMATICS & TOOLS / LOCOMOTION  
Status: PENDING VERIFICATION

## Pre-Code Analysis
Target: replace collider/trigger whirlpool authority with a deterministic maelstrom field sampled by kinematics and visual systems.  
Affected systems: HectonFluidEngine, PlayerKinematicsRuntime, SubmarineAutoLevelBallastController, marine snow compute/renderer, visor uber post, SargassumMicroFaunaBoids.  
Zero GC proof: fixed-capacity NativeArray/GraphicsBuffer state, bounded for loops, no LINQ, no collider triggers, no AreaEffector/PointEffector path.
State check: status/rationale were missing at session start; static scans found no WhirlpoolManager.Instance, no Tornado.cs, no first-party AreaEffector/PointEffector references under Assets/_Project.
Rule quote: "Default solution is a deterministic presentation fake" and "Direct Rigidbody.AddForce calls are owned by PhysicsApplySystem after force packet gather."

## Mandates Read
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- PHYS_Physics_Integrity_Determinism_ForceMode.txt
- CORE_Submarine_Vehicles_Kinematics_AUP.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- CORE_Weather_Abyssal_FlowField_Currents.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt

## Checklist
- [x] 01. SINGLETON ERADICATION | DOD: `rg` over Assets/_Project found no WhirlpoolManager or WhirlpoolManager.Instance, and no new singleton was introduced | Alternative rejected: WhirlpoolManager.Instance authority | Estimate: 0.0 us hot-path cost
- [BLOCKED BY DEPENDENCY] 02. SIGNAL MIGRATION | DOD: no `AnomalySpawnedSignal(Maelstrom)` contract exists locally and `GlobalSignals.cs` was dirty before this task; maelstrom authoring is exposed through `HectonFluidEngine.TrySetMaelstrom` until a stable signal contract exists | Alternative rejected: blind edit of dirty global signal lane | Estimate: 0.0 us until contract exists
- [x] 03. ASMDEF ISOLATION | DOD: no concrete `Hecton8.Physics.Anomalies` dependency was added; consumers use `GlobalRegistry.Fluid`/cached `HectonFluidEngine` runtime access already present in the assembly | Alternative rejected: new cyclic anomaly assembly reference | Estimate: 0.0 us hot-path cost
- [x] 04. DEAD CODE HUNT | DOD: `rg` over Assets/_Project found no AreaEffector, PointEffector, Tornado, or WhirlpoolManager references; no YAML mutation required | Alternative rejected: raw prefab text edits without evidence | Estimate: 0.0 us runtime cost
- [x] 05. VORTEX S.O.A. | DOD: `NativeArray<float4>` active maelstrom buffer plus compact metadata is maintained from analytical whirlpool slots | Alternative rejected: List<T> volumes or collider GameObjects | Estimate: 0.5-2.0 us per sampled body
- [x] 06. BURST EVALUATOR | DOD: PlayerKinematicsBodyJob and SubmarineAutoLevelPidJob sample bounded NativeArray<WhirlpoolFlow> with squared-distance/rsqrt math | Alternative rejected: sqrt falloff and managed callbacks | Estimate: 1-4 us per controlled body
- [x] 07. SUCTION & TANGENT | DOD: `SampleWhirlpoolVelocity` computes inward suction plus `math.cross(up,toCenter)` tangent; low tier suppresses tangent | Alternative rejected: PhysX trigger/AddForce swirl | Estimate: 1-3 us per active maelstrom
- [x] 08. FORCE APPLICATION | DOD: player receives deterministic velocity delta inside kinematics job; submarine receives queued ambient force through PhysicsForceRouter | Alternative rejected: direct Rigidbody.AddForce outside physics authority | Estimate: bounded by active count
- [x] 09. GPU PARTICLE SYNC | DOD: HectonMarineSnowRenderer double-buffers compact maelstrom data, uploads only on payload hash changes, and Hecton_MarineSnow.compute swirls particles using rsqrt/inverse-square fake with typed zero fallbacks | Alternative rejected: CPU per-particle swirl or per-frame redundant GPU upload | Estimate: GPU-only, max two maelstrom samples; unchanged payload upload saves ~3-20 us CPU driver overhead
- [x] 10. POST-PROCESS WARP | DOD: HectonVisorUberPostFeature samples `TrySampleMaelstromWarp` through a cached/throttled fluid binding and feeds existing pressure warp scalar when camera is inside radius | Alternative rejected: camera trigger volume or render-path registry lookup every frame | Estimate: one scalar path, no per-pixel CPU work
- [x] 11. AUDIO RUMBLE | DOD: HectonFluidEngine emits `AcousticPingSignal` at a slow cadence from the primary maelstrom AUP | Alternative rejected: AudioSource.PlayOneShot | Estimate: one signal every 0.45 s
- [x] 12. AUP SHIFT SAFETY | DOD: active whirlpool/maelstrom centers are rebased on floating-origin shift | Alternative rejected: stale runtime world centers | Estimate: active-count loop only during rebase
- [x] 13. ESCAPE VELOCITY | DOD: `SampleWhirlpoolVelocity` rejects non-finite center/strength data and clamps finite output to 18 m/s high tier and 10 m/s low tier; player/submarine callers apply lower local clamps | Alternative rejected: unbounded inverse-square pull | Estimate: clamp plus finite guard only
- [x] 14. MATH LOD | DOD: low tier publishes/samples the strongest valid maelstrom from all analytical slots, caps active output to one, and zeros tangent/spin math | Alternative rejected: slot-0-only cap that can hide a stronger slot-1 maelstrom on MX350 | Estimate: saves tangent cross and extra loop samples; strongest scan is 2 slots
- [x] 15. ZERO-GC | DOD: post-edit scan found no LINQ/ToArray/new hot collections in touched loops; no scalar-swizzle zero literals remain in marine snow compute; only pre-existing cold List fields remain in HectonFluidEngine | Alternative rejected: managed allocations in Tick/FixedTick | Estimate: 0 B/frame target, measured proof absent
- [x] 16. BLACKBOX DUMP | DOD: fixed 300-entry NativeArray telemetry ring stores active count/hash/radius/warp and dumps to Docs/AgentLogs/Dump_MAELSTROM_KINEMATICS.bin on invalid maelstrom state | Alternative rejected: no post-mortem data | Estimate: one 64B entry/fixed frame
- [x] 17. EVENT BUS DAMAGE | DOD: event-horizon check publishes packet-native Core.Signals.CombatDamageSignal with Pressure damage through GlobalSignals | Alternative rejected: direct health/component calls | Estimate: rare cadence, bounded
- [x] 18. CROSS-DOMAIN AUDIT | DOD: SargassumMicroFaunaBoids reads cached fluid service active maelstrom buffer and registers predator fear bursts through existing threat path | Alternative rejected: concrete fauna coupling or custom swarm API | Estimate: refresh throttled to 0.22 s/hash changes
- [BLOCKED BY DEPENDENCY] 19. OMEGA COMPILE CHECK | DOD: Unity MCP validation unavailable (`no_unity_session`) and `dotnet build Hecton8.Core.csproj` still fails on pre-existing missing contracts/namespaces unrelated to this maelstrom diff (`Environment.Fluids`, `Core.Memory.Layout`, `Physics.CCD`, acoustic propagation, ground radar, inventory corrosion, TetherFiredSignal`) | Alternative rejected: repairing cross-domain dependency wall inside locomotion prompt | Estimate: no runtime delta

## Loop Log
- Loop 1: Tasks 1-5. Prompt extracted from CURRENT_BATCH.md. Mandates read. Static purge showed no singleton/tornado/effector artifacts; active maelstrom state was placed in HectonFluidEngine fixed NativeArray flow buffers.
- Loop 2: Tasks 6-10. Player and submarine jobs now sample squared-distance maelstrom flow; marine snow compute and visor post-process use visual fakes to sell vortex motion.
- Loop 3: Tasks 11-14. Acoustic rumble, AUP shift rebasing, velocity clamp, and low-tier math LOD were checked against the prompt. Prompt re-extracted after task group.
- Loop 4: Tasks 15-18. Zero-GC scan completed. Black-box telemetry, pressure damage signal, and boid scatter bridge added through existing buses/registries.
- Loop 5: Self-review pass. Removed one new hot-path `GlobalRegistry.Fluid` lookup from submarine PID scheduling and one from the new fauna maelstrom threat reader. Re-ran banned pattern scans; no math.sqrt/trigger/effector/singleton/tornado matches in touched surfaces.
- Loop 6: Verification pass. Unity MCP unavailable. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /nr:false /p:UseSharedCompilation=false` failed twice after a timed/stale build-server pass; third strict compile attempt still fails on unrelated missing contracts. Task 19 marked dependency-blocked per 3-strikes protocol.
- Loop 7: Quality pass. Fixed low-tier slot starvation by selecting the strongest valid maelstrom across both analytical slots, added finite guards at authoring and shared evaluator entry points, and removed an unused runtime-signal parameter.
- Loop 8: Presentation/bandwidth pass. Double-buffered marine snow maelstrom uploads, added raw-float upload hashing, cached visor/VFX fluid bindings, and replaced shader scalar-swizzle zero literals with explicit typed zeros.
- Loop 9: Re-verification pass. Prompt re-extracted. Static banned-pattern and diff-only allocation scans are clean. Unity MCP remains unavailable. Local build still fails on 113 global dependency errors before a maelstrom-specific verdict.

## Verification
- Static banned-pattern scan: PASS for touched files and Assets/_Project purge terms.
- Diff-only hot allocation scan: PASS; no added LINQ, ToArray, foreach, string interpolation, string.Format, ToString, math.sqrt, or math.normalize patterns.
- Shader portability scan: PASS; no `0.0.xxx` or `0.0.xxxx` zero swizzles remain in Hecton_MarineSnow.compute.
- Hot allocation scan: PASS for new maelstrom code; pre-existing HectonFluidEngine cold Lists remain.
- Prompt re-read after task groups: PASS.
- Unity MCP validation: BLOCKED, no Unity session.
- Local compile: BLOCKED BY DEPENDENCY, 113 errors from missing cross-domain contracts before clean maelstrom verdict.
