# Rationale - SIGNAL_AUTHORITY_VALIDATOR

## Session Start

Problem: Signal lanes are reported as duplicated and diluted across names like CombatEvent, HitSignal, and DamageSignal.
Solution: Use task mandate to centralize signal payloads under Hecton8.Core.Contracts.Signals and SignalBus<T> lanes.
Rejected Alternatives: Monolithic EventBus and direct managed Action/UnityEvent dispatch violate lane segregation and zero-GC hot-path rules.
Scalability potential: Low uses bounded snapshots and drops optional VFX traffic under stress; Middle consumes full nearby gameplay lanes; High keeps richer telemetry; Ultra spends saved CPU on visual/audio overkill in VISUAL_SYNC.
Hardware Impact: Expected low-end i3/MX350 gain depends on existing duplication count; target is lower cache churn and no managed hot-path dispatch. Exact microseconds pending scan/build evidence.

## Mandates Selected

- ARCH_Signal_Lane_Segregation: required for typed SignalBus<T> lanes and duplicate eradication.
- OPT_Zero_GC_Policy_AllocFree_Mandate: required for no managed delegates, strings, LINQ, or copying snapshots in hot paths.
- ARCH_Execution_Phases: required for AupShiftSignal PRE_SIMULATION and VISUAL_SYNC-only presentation fan-out.
- DBG_Telemetry_Crash_Reporting_PostMortem: required for telemetry ring and fault hashes without hot-path logging.
- OPT_Performance_Budgets_FrameTime_VRAM_Limits: required for stress-based load shedding and 0.1 ms suspicion threshold.
- CORE_Damage_System_Hull_Integrity_VFX_Feedback: required for canonical damage payload semantics.
- AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC: required for audio transition signal threading constraints.
- MATH_AUP_Determinism_Sync: required for AUP shift signal ordering and finite spatial payloads.

## Decision - Canonical Damage Lane

Problem: Combat damage was split across a legacy DamageSignal surface and the newer CombatDamageSignal lane.
Solution: Removed the DamageSignal payload and publish/dequeue overloads, kept the compatibility writer name backed by NativeQueue<CombatDamageSignal>.ParallelWriter, and rewired producers to populate CombatDamageSignal directly.
Rejected Alternatives: A wrapper adapter would preserve duplicate semantics and keep two payload shapes alive in hot code.
Scalability potential: Low uses one damage packet and one sanitizer path; Middle/High/Ultra spend the saved branch/cache budget on richer hull, HUD, and audio consumers.
Hardware Impact: Estimated 3-8 us saved in damage-heavy frames on i3/MX350 by removing duplicate queue pressure and legacy conversion branches.

## Decision - Contract Namespace Eviction

Problem: Signal payloads were defined and referenced through Hecton8.Core.Signals plus several feature namespaces.
Solution: Moved all ISignal payload declarations and references to Hecton8.Core.Contracts.Signals and left feature systems in their existing domains.
Rejected Alternatives: Keeping per-feature signal namespaces would make SignalBus<T> discovery dependent on producer ownership instead of contract authority.
Scalability potential: Low gets one namespace scan and one typed-lane registry; High/Ultra can add VFX/audio lanes without scattering authority.
Hardware Impact: Runtime gain is indirect; expected 1-2 us saved in bootstrap/audit paths and lower integration churn on cheap devices.

## Decision - Lane Registry Authority

Problem: Feature systems configured lanes locally, which allowed capacity/hash drift.
Solution: Centralized SignalBus<T>.Configure calls inside GlobalSignals.InitializeAllQueues() and reduced feature bootstraps to GlobalSignals.InitializeAllQueues().
Rejected Alternatives: Keeping local Configure calls would allow a late feature to mutate lane capacity after allocation.
Scalability potential: Low gets deterministic capacity limits; Middle/High/Ultra keep full optional lanes without per-feature disagreement.
Hardware Impact: Startup cost is centralized; frame hot path avoids reconfiguration checks. Estimated 2-4 us saved on bootstrap-heavy scene loads.

## Decision - Stress Based Visual Load Shedding

Problem: Optional VFX signals could flood the bus under high SystemStress01.
Solution: Added SignalBusRegistry.SystemStress01 and per-lane ResolveFrameLimit. Non-critical VFX lanes drop to zero when stress >= 0.8 and propagate fully when stress <= 0.2.
Rejected Alternatives: Static low-tier caps would punish high-end hardware and still overload weak hardware during spikes.
Scalability potential: Low drops debris/droplet/reentry/bullet-time VFX under stress; Middle keeps bounded cinematic lanes; High and Ultra propagate 100% optional feedback when health allows.
Hardware Impact: Estimated worst-spike recovery saves 20-80 us on i3/MX350 by avoiding non-critical snapshot writes.

## Decision - Overflow Fault Handling

Problem: Lane storms above 1024 packets could silently burn frame time and hide the responsible producer.
Solution: Clear the queue, publish GlobalTelemetryBus system degradation with LOVF hash, set the non-critical VFX kill switch bit, and emit a development [LANE_OVERFLOW_FAULT] warning.
Rejected Alternatives: Dropping only oldest packets hides storm severity and leaves producers active.
Scalability potential: Low survives lane storms by cutting optional producers; High/Ultra retain normal overkill while the lane is healthy.
Hardware Impact: Estimated 0.1-0.4 ms saved during storm frames by replacing drain loops with queue clear and kill switch feedback.

## Decision - ABI Layout Exception

Problem: The batch requested sequential Pack=1 for all signal structs, but 130 legacy signal structs use explicit fixed-size layouts and several are ABI unions such as ImpactSignal Force/Velocity and HighSpeedImpactSignal LostKineticEnergy/KineticEnergy.
Solution: Converted newly evicted external signal payloads to sequential Pack=1 fixed-size structs, but retained existing explicit union lanes to avoid corrupting validated offsets and caller semantics.
Rejected Alternatives: Mechanical FieldOffset removal would compile-risk high and would change binary payload offsets for live consumers.
Scalability potential: Low stays stable because binary-compatible lanes are not churned; High/Ultra can get a staged layout migration once union aliases are replaced with properties.
Hardware Impact: No immediate frame gain; avoids an estimated multi-millisecond integration failure cost from invalid payload interpretation.

## Decision - Compile Wall Classification

Problem: Earlier `dotnet build Hecton8.Core.csproj` attempts failed on unrelated dependencies, while full `Assembly-CSharp.csproj` attempts were unstable in the wider third-party project graph.
Solution: Kept repairing CORE/SIGNALS drift until `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` succeeded with 0 warnings and 0 errors. Recorded the remaining `Assembly-CSharp.csproj` wall as MSBuild child-node/SDK resolver failure across third-party projects and stopped the leftover dotnet worker.
Rejected Alternatives: Editing XR, Submarine, GPUInstancer, RealtimeCSG, MoreMountains, or VolumetricLightBeam project dependencies would violate the CORE/SIGNALS boundary and risk overwriting other agents.
Scalability potential: Signal lanes now compile as a clean core assembly; unrelated full-project dependency repair can proceed independently.
Hardware Impact: No runtime gain; prevents background compiler workers from burning CPU after failed full-graph attempts.

## Decision - Late Agent Signal Drift

Problem: After the first audit, new files introduced stale `Hecton8.Core.Signals` imports and two local compass lane Configure calls.
Solution: Moved `AnomalyProximitySignal` and `CompassCalibratedSignal` into `Hecton8.Core.Contracts.Signals`, converted both to sequential Pack=1 fixed-size payloads, and registered their lanes in `GlobalSignals.InitializeAllQueues()`.
Rejected Alternatives: Treating late files as out-of-scope would leave the final audit false.
Scalability potential: Low keeps compass anomaly traffic bounded at 4 low-tier packets; High/Ultra allow 16 anomaly packets and 8 calibration packets per frame.
Hardware Impact: Estimated 1-3 us saved at compass startup by avoiding local lane reconfiguration and stale namespace resolution.

## Decision - Multiplatform Sanitizer Hardening

Problem: Late signal lanes carried finite-sensitive payloads that could feed rendering, physics, docking, or telemetry without a central Push-time guard.
Solution: Added sanitizer cases for tether tension/snap/fire, visual flare, voxel carve, docking request/complete/fail, anomaly proximity, compass calibration, and system glitch payloads. Invalid `float2`, `float3`, `double3`, and AUP blit fields are clamped to safe fallbacks and publish numeric math-guard telemetry.
Rejected Alternatives: Producer-only validation would leave every new writer as a single-point failure; shader/GPU trust would let one NaN poison mobile rendering.
Scalability potential: Low/Quest/Android get safe fallback packets instead of GPU pipeline collapse; Middle keeps bounded clean lanes; High and Ultra retain full signal richness for visor salt, silt wake, hull deformation, and reactive lighting consumers.
Hardware Impact: This is not a speed win. Estimated overhead is <=1-5 us per normal burst on i3/MX350, buying crash containment and deterministic bad-packet attribution.

## Decision - Storm Clear Hot Path

Problem: Overflow cleanup drained stormed `NativeQueue<T>` lanes one packet at a time after the >1024 fault threshold.
Solution: Replaced the per-packet drain loop with `NativeQueue<T>.Clear()` while keeping LOVF degradation, kill-switch feedback, and development-only fault reporting.
Rejected Alternatives: `TryDequeue` in a loop burns time exactly when the frame is already overloaded; silent drop hides producer faults.
Scalability potential: Low survives burst storms by cutting non-critical producers; High and Ultra still run full cinematic propagation until a real overflow fault occurs.
Hardware Impact: Estimated 50-300 us saved on storm frames with >1024 queued packets. Normal frames are unchanged.

## Decision - Late Decentralized Lane Drift

Problem: Lockstep, compass, anomaly, and tether code owned or referenced lane configuration outside the central registry after the first pass.
Solution: Moved `LockstepSnapshotSignal`, `SystemGlitchSignal`, `TetherFiredSignal`, compass, and anomaly lane policies into `GlobalSignals.InitializeAllQueues()` and reduced producers/readers to `GlobalSignals.InitializeAllQueues()` plus `EnsureInitialized()`.
Rejected Alternatives: Local `SignalBus<T>.Configure` calls would keep capacity/hash authority mutable and order-dependent.
Scalability potential: Low gets deterministic caps and stable hashes; Middle/High/Ultra can expand visual/audio consumers without feature-owned lane drift.
Hardware Impact: Estimated 2-6 us saved during bootstrap or scene-load reconfiguration. Runtime value is correctness, not throughput.

## Decision - Data Sovereignty Exception

Problem: The DataVault mandate bans system-owned persistent native containers, but `SignalBus<T>` is the central transport primitive and currently owns typed `NativeQueue<T>` and `NativeList<T>` lane storage directly.
Solution: Added no new local NativeArrays. Kept the existing central `SignalBus<T>` containers as cold Session allocations registered with `NativeMemorySentinel`, owner labels, and explicit disposal. Recorded this as architecture debt until `GlobalDataVault` exposes a typed lane queue API with owner id, capacity, generation, lifetime, and disposal semantics.
Rejected Alternatives: Inventing an ad hoc DataVault queue wrapper in CORE/SIGNALS without an established vault contract would hide ownership risk and create new integration debt; managed delegates or managed queues violate the batch assignment.
Scalability potential: Low retains bounded native transport with stress shedding; High/Ultra retain typed snapshots and visual overkill consumers without managed fan-out.
Hardware Impact: No direct runtime gain. It prevents a false sovereignty claim and avoids replacing one central transport with unsafe scattered ownership.

## Decision - Physics Determinism Lane Centralization

Problem: `PhysicsDeterminismSignals` still configured five deterministic lanes locally and declared their signal structs in `Hecton8.Physics`, contradicting the central lane and contract namespace rules.
Solution: Moved `InputSignal`, `StateCorrectionSignal`, `DesyncDetectedSignal`, `SyncFenceSignal`, and `KccVelocitySignal` to `Hecton8.Core.Contracts.Signals`; added central lane configuration and size validation in `GlobalSignals`; reduced `PhysicsDeterminismSignals` to sidecar/latest-state logic plus typed publish/read calls.
Rejected Alternatives: Leaving the generic local `ConfigureLane<T>` helper would keep a hidden capacity/hash authority; disposing typed lanes from `PhysicsDeterminismSignals` would race the central `GlobalSignals` ownership flag.
Scalability potential: Low/MX350 gets bounded deterministic input/fence lanes and central finite fallback; High/Ultra can consume richer KCC and replay telemetry without mutating lane authority.
Hardware Impact: Estimated 2-5 us saved during deterministic bootstrap by removing local reconfiguration and disposal churn. Runtime gain is small; correctness gain is centralized ownership and clean Push-time NaN vaccination.

## Decision - Laser Cutter Lane Closure

Problem: A late zero-GC refactor introduced `LaserCutterEventPayload` in `Hecton8.Gameplay` and a local `SignalBus<LaserCutterEventPayload>.Configure` call inside `LaserCutterEvents`.
Solution: Moved the cutter event enum/payload to `Hecton8.Core.Contracts.Signals`, added central lane policy and exact 16-byte validation in `GlobalSignals`, removed the local Configure call, and added Push-time `Heat01` saturation.
Rejected Alternatives: Reintroducing the old local `NativeQueue` sidecar would bypass `SignalBus<T>` snapshots; keeping the local Configure call would leave a hidden capacity/hash authority in gameplay.
Scalability potential: Low/MX350 keeps cutter heat/beam feedback bounded at 16 packets; High/Ultra can spend the stable lane on richer beam heat shimmer, salt crystal glow, and visor feedback without changing gameplay truth cost.
Hardware Impact: Estimated 1-3 us saved during cutter event bootstrap by removing local lane configuration. Push guard cost is below 1 us for normal event counts.

## Decision - Latest Compile Wall

Problem: After CORE/SIGNALS scans were clean, concurrent out-of-domain edits moved the current `Hecton8.Core.csproj` failure to `SubmarineFluidDynamics.cs(1250)` missing `RefreshVaultNativeStateAfterRelocation`.
Solution: Stopped dotnet workers, recorded the dependency wall, and did not edit submarine fluid/vault code because it is outside CORE/SIGNALS and unrelated to the signal lane rewrite.
Rejected Alternatives: Adding a guessed submarine vault method would violate the domain boundary and risk corrupting another agent's data-sovereignty work.
Scalability potential: Signal authority remains independent; submarine vault relocation repair can proceed in its owning domain.
Hardware Impact: No runtime gain. Prevents cross-domain churn and false build ownership claims.

## Decision - Bridge Signal DTO Compile Recovery

Problem: The current core build reached bridge DTOs carrying `[BinaryBlittableSafe]` markers, but `H8BridgeContracts.cs` imported `Hecton8.Core.Memory` without the `Hecton8.Core.Memory.Layout` namespace that owns the attribute.
Solution: Added the missing layout namespace import only. No layout, field order, lane policy, or runtime logic changed.
Rejected Alternatives: Removing the attributes would weaken binary layout enforcement; moving or duplicating `BinaryBlittableSafeAttribute` would recreate the prior duplicate-owner warning problem.
Scalability potential: Low/Quest/Android keep explicit ABI markers on bridge signal DTOs; High/Ultra keep the same verified signal contracts for richer bridge-driven visual/audio consumers.
Hardware Impact: Runtime gain is 0 us. Compile recovery unblocks `Hecton8.Core.csproj` verification and preserves layout safety for packed signal DTOs.

## Decision - Final Project Graph Boundary

Problem: After the core assembly passed, `Assembly-CSharp.csproj` failed in `RealtimeCSG.csproj` with 216 missing third-party source file errors and 131 third-party warnings.
Solution: Classified the full graph failure as outside CORE/SIGNALS because the signal/core assemblies already compiled and the failing paths are absent RealtimeCSG plugin files.
Rejected Alternatives: Editing generated third-party `.csproj` entries or recreating vendor source files from the signal pass would violate asset integrity and domain boundaries.
Scalability potential: SignalBus remains independently verified; third-party project hygiene can be handled by the integrator without mutating signal contracts.
Hardware Impact: No runtime gain. Prevents signal work from becoming a third-party asset repair pass.

## Decision - Tether Payload ABI Padding Recovery

Problem: A fresh ARM64/Quest layout audit found `TetherSnappedSignal` at 72 bytes and `TetherFiredSignal` at 40 bytes. Both were sequential Pack=1 but not 16-byte multiples, so the previous layout claim was incomplete.
Solution: Increased the payload sizes to 80 and 48 bytes, added reserved padding fields, and updated the `GlobalSignals` runtime size validators to the new ABI sizes.
Rejected Alternatives: Leaving only `StructLayout(Size=...)` without visible reserved fields would hide the ABI intent; changing tether physics logic or moving the feature file would violate the signal-only domain boundary; mechanically converting legacy explicit union layouts remains unsafe.
Scalability potential: Low/Quest/Android get stable packed payload alignment for mobile native/Burst lanes; Middle keeps the same bounded tether traffic; High/Ultra can consume the same event stream for richer tether snap sparks, cable recoil, visor warnings, and audio without changing gameplay truth.
Hardware Impact: Runtime gain is 0 us. The value is ABI stability and preventing misaligned payload reads on stricter ARM64/mobile targets.

## Decision - Signal Telemetry ABI Hardening

Problem: `SignalLaneTelemetry` is not an `ISignal`, but it crosses the `GlobalSignals` to Architect Eye/DataVault boundary through `NativeArray<SignalLaneTelemetry>` and previously relied on implicit sequential padding.
Solution: Changed the telemetry packet to `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]` and added explicit reserved fields to make the 32-byte ABI visible.
Rejected Alternatives: Leaving CLR-selected padding would be platform-sensitive on ARM64/Quest/Android; expanding telemetry semantics would mix ABI hardening with feature work.
Scalability potential: Low/MX350 and mobile keep stable compact telemetry lanes; Middle/High/Ultra keep the same signal pressure surface for richer Architect Eye diagnostics and visual overkill without changing gameplay lane cost.
Hardware Impact: Runtime gain is 0 us. The value is preventing implicit-padding drift and making telemetry copy size stable across IL2CPP/Burst/native readers.

## Decision - Recurrent Lane Configure Drift Reclosure

Problem: Fresh scans found `SignalBus<T>.Configure` calls had returned outside `GlobalSignals` in lockstep/glitch, laser cutter, and compass/anomaly code.
Solution: Removed the local Configure calls and reduced those feature surfaces to `GlobalSignals.InitializeAllQueues()` plus typed `EnsureInitialized()`/Push/snapshot operations.
Rejected Alternatives: Keeping feature-owned Configure calls would leave capacity/hash authority mutable and order-dependent; moving gameplay/UI code beyond signal initialization would violate domain boundaries.
Scalability potential: Low gets deterministic lane caps and stable hashes; Middle/High/Ultra can consume the same clean lanes for richer compass glass effects, cutter heat shimmer, glitch presentation, visor salt, and wake/silt overkill without increasing authority cost.
Hardware Impact: Estimated 2-6 us saved during cold bootstrap/reinitialization by avoiding repeated lane policy mutation. Runtime hot path is unchanged.

## Decision - Current Dependency Compile Wall

Problem: After signal scans were clean, `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false` failed with 41 errors in `EcosystemDirector.cs`, `DiegeticGyroCompassRuntime.cs`, and `InputDispatcher.cs`.
Solution: Classified the failure as outside CORE/SIGNALS. The current errors are World generic native utility inference, UI compass missing-field/missing-method drift, and InputDispatcher haptic span/read-only mismatch; no SignalBus registry or signal contract namespace errors were emitted.
Rejected Alternatives: Editing World/UI/Input implementation from the signal authority pass would violate the domain boundary and risk overwriting concurrent agents; reverting unrelated dirty files is prohibited.
Scalability potential: SignalBus remains clean and bounded; owning World/UI/Input agents can repair their native utility, compass state, and haptic bridge changes without signal contract churn.
Hardware Impact: 0 us runtime gain. This preserves ownership isolation and avoids a false green report.

## Decision - Splash And Physics Event Contract Eviction

Problem: Fresh registry scans found `SplashEvent`, `PhysicsEventPayload`, and `DeferredSubmarineImpactSignal` as feature-owned `ISignal` payloads with local lane Configure calls.
Solution: Moved the active payload contracts into `Hecton8.Core.Contracts.Signals`, added central lane policy, fixed-size validators, and Push-time finite guards in `GlobalSignals`, and reduced `FluidFeedbackEvents`/`PhysicsEventBus`/`PhysicsApplySystem` to central init plus typed Push/snapshot calls.
Rejected Alternatives: Leaving private feature-owned signal structs would keep hidden queue policy and namespace drift; rewriting the physics or UI dispatch logic would exceed the signal authority boundary.
Scalability potential: Low/MX350 gets bounded splash and physics-event lanes with low-tier caps; Middle/High/Ultra can spend the same clean packets on richer splash, pressure, EMP, acoustic, trauma, visor, silt, and hull feedback without increasing simulation truth cost.
Hardware Impact: Estimated 2-6 us saved during cold lane bootstrap/reinitialization. Runtime guard overhead is expected below 1-4 us per normal burst and buys NaN containment.

## Decision - ARM64 ABI Polish On Adjacent Event DTOs

Problem: The signal namespace and Configure scans were clean, but adjacent physics event DTOs and one flood mass result still relied on implicit final stride or a 44-byte explicit stride, which is poor ARM64/Quest hygiene around the newly centralized physics event lane.
Solution: Added explicit Pack=1 sizes for `PressureImpulseEvent` 80 bytes, `ElectromagneticPulseEvent` 32 bytes, `AcousticPingEvent` 48 bytes, `AcousticImpulseEvent` 48 bytes, and `LargeAcousticImpulseEvent` 48 bytes; added Size=16 to the AUP snapshot transformer; padded `FloodMassPropertiesResult` from 44 to 48 bytes with a reserved field.
Rejected Alternatives: Rewriting these DTOs into new signal payloads would exceed the current authority pass; leaving implicit final stride would keep platform layout behavior harder to audit.
Scalability potential: Low/Quest/Android get predictable event packet strides near the signal bridge; Middle keeps the same dispatch behavior; High/Ultra can consume the same stable physics events for denser acoustic, pressure, silt, hull, and visor presentation without changing simulation truth.
Hardware Impact: Runtime gain is 0 us. This is stability work: it prevents stride ambiguity and avoids stricter mobile/Burst/native read hazards.

## Decision - Current Sargassum Compile Wall

Problem: After re-closing concurrent local Configure drift, the current `Hecton8.Core.csproj` build fails only in `World/SargassumMicroFaunaBoids.cs` because `SaturateFinite01` is referenced nine times without a visible definition.
Solution: Classified the failure as outside CORE/SIGNALS. The signal authority scans are clean and the build emitted no SignalBus registry or signal payload namespace errors.
Rejected Alternatives: Adding a guessed World helper from the signal authority pass would violate the domain boundary and risk overwriting the World owner; reverting unrelated concurrent edits is prohibited.
Scalability potential: SignalBus remains bounded and clean for low-tier devices; World/Sargassum can repair its finite clamp helper independently without mutating signal contracts.
Hardware Impact: 0 us runtime gain. This preserves ownership isolation and avoids a false green report.

## Decision - Architect Eye Debug Signal Eviction

Problem: A new `DebugSignal : ISignal` appeared under `Hecton8.Core.Diagnostics.Visuals`, outside the contract namespace and without central lane policy.
Solution: Moved `DebugSignal` and `DebugSignalKind` into `Hecton8.Core.Contracts.Signals`, added a central `DebugSignal` lane policy and 64-byte validator in `GlobalSignals`, marked the lane non-critical VFX for stress shedding, and routed `ArchitectEyeDebugBus.EnsureInitialized()` through `GlobalSignals.InitializeAllQueues()`.
Rejected Alternatives: Keeping diagnostics-owned signal contracts would leave a hidden visual lane authority; pushing diagnostics through managed callbacks would violate the zero-GC lane mandate.
Scalability potential: Low/Quest/Android can shed debug visual packets under stress; Middle keeps bounded diagnostics; High/Ultra can spend the clean lane on dense Architect Eye overlays without changing gameplay truth.
Hardware Impact: Estimated 1-3 us saved during diagnostic bootstrap by avoiding unmanaged default-lane reinitialization. Runtime hot path is unchanged except stress shedding for debug visuals.

## Decision - Current UI/SystemDispatcher Compile Wall

Problem: After debug signal eviction, the current `Hecton8.Core.csproj` build fails with 46 out-of-domain errors in `DiegeticGyroCompassRuntime.cs` and `SystemDispatcher.cs`; the errors are missing presentation DTO fields, compass method signature mismatch, and missing dispatcher blackbox/raycast members.
Solution: Classified the failure as outside CORE/SIGNALS. Signal scans are clean and the build emitted no SignalBus registry or signal payload namespace errors.
Rejected Alternatives: Guessing UI presentation fields or dispatcher blackbox/raycast buffers from the signal authority pass would violate ownership boundaries and risk corrupting concurrent UI/dispatcher work.
Scalability potential: SignalBus remains bounded and clean; UI and dispatcher owners can repair their DTO/state fields without mutating signal contracts.
Hardware Impact: 0 us runtime gain. This preserves ownership isolation and avoids a false green report.

## Decision - Final Core Build Recovery

Problem: A final build attempt initially failed because `Temp/obj/Hecton8.Core/Hecton8.Core.csproj.nuget.g.targets` was missing after concurrent workspace cleanup.
Solution: Ran `dotnet restore Hecton8.Core.csproj /nr:false` to regenerate the project target, then reran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false`; the core signal project passed at that point with 0 warnings and 0 errors.
Rejected Alternatives: Treating the missing generated target as a code failure would be false; claiming full Unity graph health from a core-project build would also be false.
Scalability potential: Clean core signal assembly gives low-tier and high-tier consumers the same stable typed-lane contract; full Unity graph validation remains a separate integrator concern.
Hardware Impact: Runtime gain is 0 us. This is verification recovery and proves the signal authority changes compile in the core project.

## Decision - Final Build Warning Recheck

Problem: One intermediate build emitted 2 `CS2002` duplicate-source warnings while Unity-generated project files were changing under concurrent agent work.
Solution: Forced a clean project rebuild with `dotnet build Hecton8.Core.csproj -t:Rebuild --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false`; the current core rebuild passes with 0 warnings and 0 errors. Fresh signal scans remain clean: `CONFIGURE_OUTSIDE_GLOBALSIGNALS=0`, `OLD_SIGNAL_NAMESPACE_HITS=0`, `ISIGNAL_NAMESPACE_VIOLATIONS=0`, and `ISIGNAL_LAYOUT_VIOLATIONS=0`.
Rejected Alternatives: Editing root `Directory.Build.targets` or generated Unity `.csproj` entries after a transient warning would exceed the domain boundary and risk conflicting with concurrent integrator/build agents.
Scalability potential: Runtime signal behavior is unchanged; low-tier bounded lanes and high-tier visual overkill consumers retain the same typed contracts. Build-plumbing cleanup should be owned by the integration/build authority.
Hardware Impact: Runtime gain is 0 us. This was verification recovery, not a frame-time or GC change.

## Decision - SPSC Memory Ownership Closure

Problem: The generic `SpscSignalRingBuffer<T>` fallback in `GlobalSignals.cs` allocated a backing `NativeArray<T>` directly, which violated the current H-Phi/Data Sovereignty re-inquisition even though no live call sites use the type.
Solution: Replaced the direct `new NativeArray<T>` path with `H8Memory.Allocate<T>(..., SystemID, ...)` and paired disposal with `H8Memory.Release`. The legacy constructor remains as an audio-default compatibility path, and an owner-explicit constructor exists for future non-audio use.
Rejected Alternatives: Deleting the public generic ring buffer would be an API break; leaving direct allocation would make the memory sentinel blind; inventing a new GlobalDataVault queue/ring API inside the signal pass would create unreviewed ownership semantics.
Scalability potential: Low/MX350 and Quest get tracked native ownership if the fallback is activated; Middle/High/Ultra keep the same SPSC semantics without managed callbacks or queue boxing.
Hardware Impact: Runtime gain is 0 us while unused. If activated, leak attribution improves and shutdown cleanup avoids untracked native memory; no frame-time cost is added.

## Decision - Recurrent Compass And Lockstep Lane Drift Closure

Problem: Fresh scans again found four local `SignalBus<T>.Configure` calls in `DiegeticGyroCompassRuntime.cs` and `LockstepStateValidator.cs`, reintroducing feature-owned lane authority after prior closure.
Solution: Removed those local Configure calls and stale local capacity/hash constants. Both surfaces now enter through `GlobalSignals.InitializeAllQueues()` and then only call typed `EnsureInitialized()`.
Rejected Alternatives: Keeping feature-local Configure calls would leave lane capacity/hash authority order-dependent; editing compass or lockstep gameplay logic beyond signal initialization would exceed the signal authority boundary.
Scalability potential: Low keeps deterministic small anomaly/compass/glitch caps from the central registry; Middle/High/Ultra can spend stable packets on richer compass glass, glitch feedback, visor cues, and replay diagnostics without local lane policy drift.
Hardware Impact: Estimated 2-6 us saved during cold bootstrap/reinitialization by removing repeated local policy mutation. Runtime hot path is unchanged.

## Decision - Current Dependency Compile Wall

Problem: After signal scans were clean, build verification became unstable under concurrent workspace churn. Completed attempts moved through unrelated `World/EcosystemDirector.cs` helper merge errors and `Gameplay/PlayerKinematicsRuntime.cs` vault binding errors; one later build attempt timed out after 304 seconds and spawned dotnet/VBCSCompiler workers that had to be stopped.
Solution: Classified this as a dependency wall after re-closing all signal lane drift again. Final static signal scans are clean; no current completed green build exists after the final reclosure.
Rejected Alternatives: Editing World/Ecosystem, PlayerKinematics, or generated build plumbing from the signal authority pass would violate the domain boundary and risk overwriting owning agents; claiming a current green build would be false.
Scalability potential: Signal lanes remain clean and bounded for low-tier devices and retain high-tier propagation semantics. Owning World/Gameplay/Integrator agents must repair their compile walls before core build green can be re-certified.
Hardware Impact: 0 us runtime gain. This is ownership isolation and compile-wall truth maintenance.

## Decision - 2026-05-17 Recurrent Drift Closure And Core Build Green

Problem: A fresh re-inquisition after status/rationale/XML reload found local `SignalBus<T>.Configure` drift had returned in lockstep/glitch, compass/anomaly, and Architect Eye debug surfaces.
Solution: Removed the local Configure calls and stale local capacity/hash constants again. Those surfaces now call `GlobalSignals.InitializeAllQueues()` and only use typed `EnsureInitialized()`/Push/snapshot APIs. Re-ran bounded scans and `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false`, which passed with 0 warnings and 0 errors.
Rejected Alternatives: Leaving feature-owned Configure calls would keep lane capacity/hash authority order-dependent; editing unrelated submarine compile debt was rejected after the first failed attempt because the final post-reclosure core build passed without it.
Scalability potential: Low/MX350 and Quest keep deterministic central lane caps, low-tier VFX/debug shedding, and no managed signal strings; Middle/High/Ultra can consume stable signal packets for richer compass glass, glitch overlays, dense debug visuals, visor salt, wake/silt feedback, and hull response without increasing gameplay broadcast authority cost.
Hardware Impact: Estimated 2-6 us saved during cold bootstrap/reinitialization by removing repeated local policy mutation. Runtime hot path remains bounded; build recovery adds 0 us runtime.

## Decision - 2026-05-17 Final Post-Churn Compile Wall

Problem: After another local Configure reclosure, a final build attempt failed outside CORE/SIGNALS with `SubmarineFluidDynamics.cs(729,43): CS0102` because `SubmarineFluidDynamics` now contains a duplicate `_exteriorBuoyancySampleLocalPoints` definition.
Solution: Classified the final build state as dependency-blocked and corrected the status/audit records so the earlier green core build is not represented as the current final state. Signal scans remain clean after the final reclosure.
Rejected Alternatives: Editing submarine fluid dynamics from the signal authority pass would violate the domain boundary; claiming a current green build after the final failure would be false.
Scalability potential: Signal lanes remain bounded and centrally owned for low-tier devices and retain high-tier visual/audio propagation semantics. The submarine owner must resolve its duplicate field before build green can be re-certified.
Hardware Impact: 0 us runtime gain. This is compile-wall truth maintenance and ownership isolation.

## Decision - 2026-05-17 Acoustic Zone Signal Closure

Problem: `AcousticZoneChangedEvent : ISignal` was declared in `Hecton8.Audio`, used a 1-byte payload, and `AcousticZoneEvents` configured its own lane/hash outside `GlobalSignals`.
Solution: Moved the payload into `Hecton8.Core.Contracts.Signals`, padded it to `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`, removed local Configure/hash state, and registered the lane centrally in `GlobalSignals.InitializeAllQueues()`.
Rejected Alternatives: Leaving the audio-owned signal as a facade-local event would preserve duplicate authority; adding a new audio-specific bus would violate the typed lane registry mandate.
Scalability potential: Low/MX350 keeps acoustic zone transitions at a four-packet central cap; Middle/High/Ultra can use the same packet for richer interior/exterior mix transitions and high-tier acoustic presentation without increasing gameplay broadcast cost.
Hardware Impact: Estimated 1-2 us saved during cold bootstrap by removing repeated local lane configuration. Runtime hot path remains a single typed Push and bounded snapshot read.

## Decision - 2026-05-17 Used SignalBus Lane Closure

Problem: An alias-aware scan of every concrete `SignalBus<T>` use found four lanes that could still fall back to default generic policy: `DataVaultUpdateSignal`, `PrefabAcousticSignatureSignal`, `PrefabLoreLinkSignal`, and `ScalabilityChangedEvent`. A second pass also exposed `DirectorAIMusicSignal` as a typed lane without central registry policy, while concurrent churn reintroduced compass/anomaly and lockstep/glitch local Configure helpers.
Solution: Added central capacities, low-tier caps, hashes, and size validators for the bridge lanes and DirectorAI music cue in `GlobalSignals.InitializeAllQueues()`. Moved `ScalabilityChangedEvent` into `Hecton8.Core.Contracts.Signals`, padded it to Pack=1 Size=16, and changed `ScalabilityEvents` to enter through `GlobalSignals.InitializeAllQueues()` instead of local `Configure`. Compass and lockstep helper methods now only call central init plus typed `EnsureInitialized()`.
Rejected Alternatives: Letting `SignalBus<T>` defaults stand would silently bypass low-tier caps and stable hashes; keeping `ScalabilityChangedEvent` at Size=2 would leave an ARM64/Quest hostile payload; deleting the existing listener bridge in one pass would exceed the signal authority boundary and risk breaking 30+ scalability listeners.
Scalability potential: Low/MX350 gets deterministic four-packet scalability events, 16-packet low-tier bridge bursts, and 8-packet DirectorAI music cues. Middle keeps bounded registry traffic. High and Ultra can spend stable data-vault, prefab, and music cue packets on richer acoustic lore, material/audio presentation, dense debug overlays, visor salt, silt wake, and hull feedback without increasing broadcast authority cost.
Hardware Impact: Estimated 2-8 us saved during cold bootstrap/reinitialization by eliminating default-policy fallback and recurrent local Configure mutation. Runtime frame gain is 0 us for the struct padding; the value is stable ABI and bounded low-tier propagation. Verification build adds 0 us runtime.

## Decision - 2026-05-17 Physical Signal Authority And Residual Lane Policy Closure

Problem: The contract namespace was clean, but many `ISignal` payload declarations still physically lived in feature files. That left authority split across physics, tether, docking, voxel, movement, homeostasis, diagnostics, and bridge surfaces. A stricter alias-aware audit also found eight active `SignalBus<T>` lanes still falling through generic default policy: `BrownoutSignal`, `DebrisSpawnSignal`, `HUDNotificationSignal`, `ToolAcousticSignal`, `SeismicSignal`, `SubmarineLightsChangedSignal`, `PhysiologyStateSignal`, and `PlayerStressSignal`.
Solution: Moved the remaining payload definitions into `Assets/_Project/Scripts/Core/GlobalSignals.cs`, deleted the duplicate feature-file definitions, stripped reintroduced compass and lockstep `Configure` calls, and added explicit central capacity/hash/low-tier policies for the eight residual lanes. The final non-rebuild core compile passed with 0 warnings and 0 errors.
Rejected Alternatives: Keeping feature-file payload declarations would preserve two authority surfaces even with the correct namespace; relying on `SignalBus<T>` default policy would bypass low-tier throttles and stable lane hashes; running another rebuild was rejected because the user explicitly asked not to rebuild every time and a normal build is sufficient after scoped source edits.
Scalability potential: Low/MX350 and Quest now get deterministic bounded policies for stress, physiology, HUD, seismic, brownout, debris, tool acoustic, and submarine light traffic. Middle keeps full bounded gameplay/presentation lanes. High and Ultra can spend the same clean packets on richer camera shake, hull light response, material decay, tool audio, brownout ambience, dense debris, visor salt, silt wake, and overkill lighting without local lane policy drift.
Hardware Impact: Estimated 3-10 us saved during cold bootstrap/reinitialization by removing residual default-policy fallback and local Configure mutation. Runtime frame savings are workload-dependent; ABI centralization and struct relocation add 0 us but remove mobile/IL2CPP integration risk.

## Decision - 2026-05-17 Warning Sweep And Late Signal Drift Closure

Problem: A fresh warning pass found recurrent helper-level lane authority in procedural audio, camera juice, ambient biota, lockstep, gyro-compass, and scalability code. The core build also emitted `CS2002` because `HectonSignalLaneContract.cs` was included by both the generated project and `Directory.Build.targets`.
Solution: Moved `AudioEvent` and `CameraJuiceImpactSignal` into `Assets/_Project/Scripts/Core/GlobalSignals.cs`, registered their lanes centrally, and reduced the feature helpers to `GlobalSignals.InitializeAllQueues()` plus typed `EnsureInitialized()`/Push/snapshot APIs. Added a `Compile Remove` for `HectonSignalLaneContract.cs` before the target re-adds it, eliminating the duplicate-source warning. Updated the acoustic smoke assertion to look at the central contract file.
Rejected Alternatives: Keeping audio/camera payloads in feature files would preserve physical contract drift; leaving local `Configure` calls would keep lane authority order-dependent; suppressing `CS2002` was rejected because the duplicate include was fixable. `dotnet rebuild` was not run.
Scalability potential: Low/MX350 and Quest now use the same bounded central audio, camera, biome, spawn, debris, compass, glitch, and scalability lanes. Middle keeps deterministic propagation. High and Ultra can spend the clean packets on richer procedural audio, camera impact response, ambient debris, compass glass, glitch overlays, visor salt, silt wake, and hull lighting without reopening lane policy.
Hardware Impact: Estimated 4-12 us saved during cold bootstrap/reinitialization by removing repeated helper-level lane mutation. Runtime frame gain is 0 us for the project-file warning fix; the value is clean build evidence and stable lane authority.
