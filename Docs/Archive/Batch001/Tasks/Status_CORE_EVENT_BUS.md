# Status_CORE_EVENT_BUS

Prompt: CORE_EVENT_BUS
Role: SIGNAL_MASTER
Domain: CORE & MEMORY INFRASTRUCTURE / Global EventBus
Batch source: Docs/Tasks/CURRENT_BATCH.md
Status: VERIFIED MASTER GRADE

## Mandates Selected
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- ARCH_Project_Bootstrap_Sequence_Init_Safety.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt
- CORE_Damage_System_Hull_Integrity_VFX_Feedback.txt

## Core Checklist
- [x] 1. TYPE-SAFE SIGNAL QUEUES | Justification: Added prompt-exact NativeQueue<AupShiftSignal> beside existing Damage/Impact lanes; DOD practice = unmanaged typed queues only | Alternatives Rejected: Replacing RebaseSignal during active batch would mutate public API | Estimate: 0.8 us per enqueue/dequeue pending profiler proof
- [x] 2. MPSC ARCHITECTURE | Justification: Added AupShiftSignalWriter and retained Damage/Impact ParallelWriter; DOD practice = NativeQueue<T>.ParallelWriter for Burst/background producers | Alternatives Rejected: Managed delegates/events and direct cross-system calls | Estimate: 0.6 us producer enqueue pending profiler proof
- [x] 3. FIXED STRUCT ALIGNMENT | Justification: DamageSignal now uses [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)] | Alternatives Rejected: Explicit layout violated prompt wording | Estimate: 0.2 us cache-line fetch pending profiler proof
- [x] 4. NO-STRING RPCs | Justification: DamageSignal carries uint SubjectHash and no managed string fields; signal validation uses unmanaged generic constraints | Alternatives Rejected: string entity names, enum ToString, managed RPC labels | Estimate: avoids managed allocation entirely in signal payload
- [x] 5. SINGLE-PASS DRAINAGE | Justification: SoundscapeSystem already drains ImpactSignal through one bounded TryDequeue loop and GlobalSignals wrapper calls NativeQueue.TryDequeue | Alternatives Rejected: Listener callbacks from physics to audio, multi-pass scans | Estimate: <=16 dequeues per SlowTick, under 10 us pending profiler proof
- [x] 6. SLOW-TICK DRAIN CAPPING | Justification: SoundscapeSystem keeps quality-tier caps at 4/8/16 impact drains per SlowTick; DOD practice = bounded while(TryDequeue) hot loop | Alternatives Rejected: unbounded audio event drain or per-impact direct Play calls | Estimate: <=16 dequeues, ~10 us ceiling pending profiler proof
- [x] 7. AUP-SHIFT BROADCASTER | Justification: HectonFloatingOrigin publishes AupShiftSignal with int3 SectorDelta at committed shift point | Alternatives Rejected: listener-only callbacks and replacing legacy OriginShiftEventData | Estimate: one 32-byte enqueue per shift, <1 us pending profiler proof
- [x] 8. PHYSICS-TO-SOUND CORRIDOR | Justification: Global physics/fluid producers publish ImpactSignal and Soundscape drains the signal lane for clang synthesis | Alternatives Rejected: physics calling audio directly or expanding ImpactSignal past 64 bytes | Estimate: 64-byte payload, <=16 drains per SlowTick
- [x] 9. LOGISTICS-TO-UI CORRIDOR | Justification: PowerGridManager pushes BrownoutSignal snapshots; VisorHUDController drains max 4 per tick and maps supply/severity to HUD brownout | Alternatives Rejected: direct PowerGrid telemetry listener inside visor material path | Estimate: <=4 dequeues per UI tick, ~2 us pending profiler proof
- [x] 10. DAMAGE-ROUTING SIGNALS | Justification: CombatDamageRuntime drains GlobalSignals DamageSignal into CombatDamageSignal queue before scheduling health jobs | Alternatives Rejected: producers calling CombatDamageRuntime.TryQueueDamage directly | Estimate: capped 64 bridge conversions per frame, <=40 us worst-case pending profiler proof
- [x] 11. TELEMETRY ANOMALY SIGNALS | Justification: Added prompt-exact AnomalySignal queue/writer/dequeue/publish beside legacy telemetry lane; DOD practice = unmanaged 32-byte payload | Alternatives Rejected: string exception routing or reusing only legacy TelemetryAnomalySignal name | Estimate: ~0.6 us enqueue/dequeue pending profiler proof
- [x] 12. SONAR-PING SIGNALS | Justification: Added prompt-exact AcousticPingSignal queue/writer/dequeue/publish with AUP payload | Alternatives Rejected: direct predator/audio callbacks and Vector3-only ping positions | Estimate: 64-byte enqueue/dequeue, ~1 us pending profiler proof
- [x] 13. OXYGEN-CRITICAL SIGNALS | Justification: Added HypoxiaSignal queue/writer/dequeue/publish as 32-byte visor/audio lane | Alternatives Rejected: managed survival events or reusing only OxygenCriticalSignal name | Estimate: ~0.6 us enqueue/dequeue pending profiler proof
- [x] 14. RECON-DATA SIGNALS | Justification: Added ScanCompleteSignal queue/writer/dequeue/publish and fixed scan namespace alias for wreck producers | Alternatives Rejected: direct ScanLog/PDA calls from archaeology producers | Estimate: 64-byte enqueue/dequeue, ~1 us pending profiler proof
- [x] 15. RIGIDBODY-SLEEP SIGNALS | Justification: Added RigidbodySleepSignal writer and publishes sleep/wake state from GlobalPhysicsStateManager distance sleep path | Alternatives Rejected: Scatter Overseer polling every rigidbody | Estimate: one 64-byte packet per sleep transition, <2 us pending profiler proof
- [x] 16. GLOBAL-TIME-SYNC SIGNALS | Justification: Added GlobalTimeSyncSignal writer and HectonCelestialEngine publish hook from runtime snapshot | Alternatives Rejected: consumers polling CelestialEngine or using direct callbacks | Estimate: one 32-byte packet per changed celestial snapshot, <1 us pending profiler proof
- [x] 17. DISPOSAL CLEANUP | Justification: DisposeAllQueues disposes every GlobalSignals NativeQueue and GameBootstrapper already calls it on quit/shutdown | Alternatives Rejected: relying only on domain reload cleanup | Estimate: cold-path cleanup only, 0 us frame cost
- [x] 18. PO2 RING BUFFER FALLBACK | Justification: Added SpscSignalRingBuffer<T> with power-of-two capacity, mask wrapping, and Volatile head/tail | Alternatives Rejected: lock/ConcurrentQueue fallback or modulo-index ring | Estimate: O(1), one mask per enqueue/dequeue
- [x] 19. COMPLIANCE ASSERTIONS | Justification: Editor/development validation uses unmanaged generic constraints and RuntimeHelpers.IsReferenceOrContainsReferences<T>() on prompt payloads | Alternatives Rejected: reflection-based field scans and managed test harnesses in hot code | Estimate: editor/bootstrap-only, 0 us runtime frame cost
- [x] 20. OMEGA COMPILE CHECK | Justification: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -v:minimal -p:UseSharedCompilation=false` succeeded after OMEGA code edits with 0 warnings and 0 errors | Alternatives Rejected: stopping at pre-polish build or leaving duplicate helper compile failure | Estimate: compile verification complete

## Verification Log
- Initial prompt extracted from CURRENT_BATCH.txt because CURRENT_BATCH.md does not exist.
- Status/Rationale files were absent at session start; no stale CORE_EVENT_BUS data found.
- Loop 1 code patch applied for tasks 1-5. Build pending.
- Build attempt 1 timed out after 120s with concurrent dotnet workers.
- Build attempt 2 failed before CORE_EVENT_BUS diagnostics because SaveBinaryStorageNativeArrayExtensions.cs was missing; file reappeared as concurrent-agent modification.
- Build attempt 3 found one CORE_EVENT_BUS namespace error in HectonFluidEngine and unrelated HectonCelestialEngine missing-method errors.
- Re-extracted CORE_EVENT_BUS prompt from CURRENT_BATCH.md after task 8 per Anti-Amnesia cadence.
- Loop 2 code patch applied for tasks 6-10.
- Build attempt 4 (`dotnet build Hecton8.Core.csproj --no-restore -m:1 -v:minimal -p:UseSharedCompilation=false`) failed only on unrelated `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` missing-method errors: `TryUnregisterWreckSlowTick`, `ProcessNearFieldDebris`, `ProcessArtifactDiscovery`, `UpdateDebrisGravityStateless`, `ValidateBlackBoxState`.
- Re-extracted CORE_EVENT_BUS prompt from CURRENT_BATCH.md after task 13 per Anti-Amnesia cadence.
- Loop 3 code patch applied for tasks 11-15.
- Build attempt 5 exposed signal/scan namespace integration issues: `ConstructionManager` missing `Hecton8.Core.Signals`, `ProceduralWreckGenerator` missing scan aliases, plus unrelated save/construction errors.
- Build attempt 6 after namespace fixes failed only on unrelated save/construction errors: `SaveBinaryPayloadCodec`, `SaveBinaryStorage`, `HabitatGraphManager`, and `ConstructionManager` missing `Hecton8.Physics.SyncTransforms`.
- Re-extracted CORE_EVENT_BUS prompt from CURRENT_BATCH.md after task 16 per Anti-Amnesia cadence.
- Loop 4 code patch applied for tasks 16-20.
- Build attempt 7 succeeded: `Hecton8.Core -> Temp/bin/Debug/Hecton8.Core.dll`, 0 warnings, 0 errors.
- OMEGA prompt re-extracted from CURRENT_BATCH.md after all 20 tasks were checked.
- OMEGA polish replaced bridge `math.normalizesafe` damage direction with existing dominant-axis direction helper.
- OMEGA polish replaced rigidbody sleep `math.sqrt` distance reconstruction with `distanceSq * math.rsqrt(distanceSq)` on transition-only packets.
- Build attempt 8 failed on duplicate `ResolveDominantAxisDirection` helper introduced during polish; duplicate was removed.
- Build attempt 9 succeeded: `Hecton8.Core -> Temp/bin/Debug/Hecton8.Core.dll`, 0 warnings, 0 errors.
- Unity runtime/GC/profiler proof not run in this terminal pass; no runtime 0 B/frame claim is made.
