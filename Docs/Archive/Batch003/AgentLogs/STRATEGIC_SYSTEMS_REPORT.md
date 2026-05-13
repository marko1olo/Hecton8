# STRATEGIC_SYSTEMS_REPORT

STATUS: STRATEGICALLY VERIFIED
Agent: STRATEGIC_AUDITOR_SYSTEMS
Scope: SystemDispatcher, IJob scheduling, WorldChunkResidencyManager IO behavior, unsafe binary blits, AUP determinism, blackbox telemetry.
Verification: Static CLI/source audit only. `dotnet build` and Unity compile were intentionally not run because the latest user instruction forbids build execution.

## Executive Verdict

The foundation is viable but not strategically safe on 4-core/slow-storage hardware. The dispatcher has priority lanes for managed tick order and late-frame event load shedding, but it does not own Burst job admission. Streaming adapts to tier, memory, VRAM, prediction, and activation work, but it does not measure drive latency or slow the player when IO debt grows. Binary DTO discipline exists around AUP and several save records, but too many native/job structs still rely on implicit layout for a codebase that uses raw blits.

Top 3 architectural time bombs:

1. Global Burst job pileup: 80+ systems can schedule before any central scheduler admits or rejects work. Unity worker queues become the hidden priority system.
2. Storage-blind world residency: slow MicroSD stalls can age pending chunk loads while player velocity remains unconstrained.
3. Cross-platform binary blit drift: explicit DTOs exist, but generic unmanaged blits and many unannotated job/native structs leave ARM/Linux IL2CPP layout risk.

## 1. SystemDispatcher And Job Pileup

Evidence:
- `SystemDispatcher` defines four dispatcher lanes and capacities for update/fixed/slow/frost/late/post-fixed lanes in `Assets/_Project/Scripts/Core/SystemDispatcher.cs:58-186`.
- Update loops lanes in order and ticks `IUpdatable` owners in `SystemDispatcher.cs:942-968`.
- It schedules foveated jobs, fixed accumulator, slow tick, frost tick, and dispatcher raycasts after lane ticks in `SystemDispatcher.cs:970-976`.
- Late-frame event load shedding exists: max 1000 events and 2.0 ms budget in `SystemDispatcher.cs:67-75`, plus budget logic around `SystemDispatcher.cs:1552-1592`.
- `DispatcherJobSwap` only controls completion behavior. Non-forced completion returns false if the handle is not done in `Assets/_Project/Scripts/World/DispatcherJobSwap.cs:63-75`.

Strategic answer:
No, `SystemDispatcher` does not prevent global Burst job pileup. It prevents some blocking completions and orders managed tick owners, but it does not provide a hard priority queue for scheduled jobs. There is no evidence of a central "Kinematics > Voxel Meshing" job admission gate. `PriorityLayer` is tick order, not worker-time reservation.

Observed schedule map from CLI:

```text
Domain        Files Jobs Schedules CompleteSites
_Root           336   70        93            14
Atmosphere        7    2         2             0
Audio            16    4         1             0
Bootstrap         9    0         0             0
Construction     36   12         7             0
Core             79    4         2             0
Data              8    2         0             0
Dev               9    5        12             0
Ecosystem         9    1         1             0
Fauna            22   15        12             0
Gameplay        130   16        12             0
Interaction      25    2         2             0
Physics           4    4         0             0
Plugins          16    1        20             4
Power             5    3         2             0
Tools            15    1         1             0
UI               94    6         5             1
VFX               6    1         1             0
World           158  102        73             8
```

Highest schedule density:

```text
HectonVoxelEngine.cs: 28
World/HectonAnomalyEngine.cs: 11
HectonWorldGenerator.cs: 7
Plugins/MapMagic/HectonHydraulicErosionMapMagicNode.cs: 7
Fauna/ProceduralCrabLegIKRuntime.cs: 5
SaveBinaryStorage.cs: 5
SubmarineFluidDynamics.cs: 5
World/VegetationFlowFieldIntegrator.cs: 5
```

Mathematical token-bucket scheduler model:

Let `C` be logical cores, `W = max(1, C - 2)` reserved worker pressure for 4-core hardware, so a 4-core CPU admits work as if only 2 worker lanes are disposable. Let `B_i` be tokens in milliseconds for lane `i`, `R_i` refill ms/frame, `Cap_i` burst cap, and `Cost_j` measured EWMA cost of job `j`.

Per frame:

```text
B_i = min(Cap_i, B_i + R_i * qualityScale * clamp(dt / 16.667ms, 0.5, 2.0))
admit(job j in lane i) if:
    B_i >= Cost_j
    globalInflightJobs <= 2 * W
    criticalDebtMs <= criticalDebtLimit
then:
    B_i -= Cost_j
else:
    defer, degrade, or drop according to lane policy
```

Proposed hard lanes:

```text
Lane 0 Critical Kinematics/Physics: R=1.20 ms, Cap=2.40 ms, never drop, can steal.
Lane 1 World Residency/Collision Safety: R=0.45 ms, Cap=1.20 ms, defer predictive before resident/core.
Lane 2 Voxel Meshing/Generation: R=0.25 ms, Cap=1.50 ms, max 1 concurrent on 4C.
Lane 3 AI/Fauna/Ecosystem: R=0.20 ms, Cap=0.80 ms, foveate and skip low importance.
Lane 4 VFX/Presentation/Telemetry: R=0.10 ms, Cap=0.50 ms, drop/degrade first.
Lane 5 Save/Compression/Background IO: R=0.10 ms, Cap=0.70 ms, background only unless explicit save fence.
```

Cost measurement:

```text
Cost_j = lerp(Cost_j, measuredCompleteMs, 0.10)
Penalty_i = max(0, lastFrameMs - targetFrameMs) * lanePenaltyScale
effectiveRefill_i = max(0, R_i - Penalty_i)
```

Low tier uses lower refill and stricter concurrency. Ultra tier increases caps and spends surplus on visual overkill only after critical debt is zero.

## 2. IO Adaptability And Falling Through The World

Evidence:
- `WorldChunkResidencyManager` has explicit tiering and LOD flags in `WorldChunkResidencyManager.cs:28-51`.
- Load request and residency telemetry structs are fixed-size in `WorldChunkResidencyManager.cs:64-116`.
- Predictive streaming uses player velocity and `math.rsqrt` in `WorldChunkResidencyManager.cs:149-170`.
- Residency jobs schedule at `WorldChunkResidencyManager.cs:1128-1138`.
- Load dispatch budget is tiered at `WorldChunkResidencyManager.cs:1233-1256`.
- Addressables polling only checks validity, `IsDone`, and status in `WorldChunkResidencyManager.cs:1388-1450`.
- Activation is amortized by `MaxActivationsPerFrame` and awaits next frame in `WorldChunkResidencyManager.cs:1488-1564`.
- Async upload tier sets 64/128/256 MB and 1/2/4 time slice in `WorldChunkResidencyManager.cs:2149-2171`.
- Streamer stress metric includes queue, resident, speed, and suspend pressure in `WorldChunkResidencyManager.cs:2323-2331`.
- The only found velocity backpressure is voxel-teardown swim multiplier through `FoveatedSimulationManager` in `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:497-513`, not drive-latency driven.

Strategic answer:
No, the current residency manager does not detect drive latency. It detects queue pressure, memory pressure, VRAM pressure, habitat/transport predictive suspension, and activation pressure. It does not measure read latency for Addressables, additive scene loads, or oldest pending chunk age. A Steam Deck reading from slow MicroSD can have `handle.IsDone == false` for many frames while movement continues.

Required storage-debt model:

```text
requestStartTime[chunk] = unscaledTime at dispatch
latencyMs = (completeTime - requestStartTime) * 1000
latencyEwma = lerp(latencyEwma, latencyMs, 0.08)
oldestPendingMs = max(now - requestStartTime[pending])
criticalHoleDebt = max(0, requiredChunkAgeMs - 250)
storageDebt01 = saturate((latencyEwma - 80)/420 + oldestPendingMs/1000 + criticalHoleDebt/500)
```

Throttle/degrade policy:

```text
storageDebt01 < 0.25: normal.
0.25..0.50: halve predictive distance, prefer LOD1/proxy, dispatch only resident-ring chunks.
0.50..0.75: clamp boost and horizontal player speed to residentRadius / (latencyEwmaSeconds + 0.5).
>0.75: hard gate forward velocity near missing critical chunks, show fog/current/camera resistance, keep collision proxy active.
```

This is a cinematic cheat, not a realism tax: the player feels current/resistance or poor visibility instead of seeing missing world. Low uses aggressive clamps and proxies. High/Ultra prefetch more, but the same storage debt gate remains.

## 3. Memory Alignment And Binary Blitting

Evidence:
- Core AUP save/transfer structs are explicit: `AbsoluteUniversePosition` in `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:24-43`, `AbsoluteUniversePositionBlit128` in `PersistentWorldRegistry.cs:150-162`, and `AbsoluteUniversePositionBlit` in `Assets/_Project/Scripts/World/AbsoluteUniversePositionBlit.cs:6`.
- Several persistence structs are annotated: `PoolSlotData` pack 1 size 40 at `PersistentWorldRegistry.cs:191-202`, `EntityDataRecord` pack 16 size 64 at `PersistentWorldRegistry.cs:204-212`, `PersistentWorldItemRecord` pack 1 size 204 at `PersistentWorldRegistry.cs:222-234`.
- `MemoryInquisitor` uses raw `UnsafeUtility.MemCpy` for unmanaged write/read/stride copy at `Assets/_Project/Scripts/Core/MemoryInquisitor.cs:76-80`, `MemoryInquisitor.cs:114-117`, and `MemoryInquisitor.cs:162-167`.
- `UnsafeMemoryCopyGuard` only validates source/destination byte ranges before `UnsafeUtility.MemCpy` in `Assets/_Project/Scripts/Core/UnsafeMemoryCopyGuard.cs:38-68`; it does not validate ABI layout, endian, or field offsets.
- CLI struct scan: `Sequential=527`, `Explicit=52`, `PackAnnotated=245`, `SizeAnnotated=1121`, `SequentialNoPackOrSize=235`.
- CLI IJob scan found `IJobMissingNearbyStructLayout=204`.
- Save read path still supports old full `EntityDataRecord` byte copy if compact16 is not used in `SaveBinaryStorage.cs:4252-4294`.

Strategic answer:
AUP core DTOs are good. The project as a whole is not proven safe for cross-platform binary blitting. Any persisted or cross-process data that uses a sequential struct without fixed pack/size/offset validation can corrupt between x64/x86/ARM64 or Mono/IL2CPP. ARM alignment and IL2CPP field layout are not the place to use faith.

Required hard rule:

```text
Only these are binary-blittable:
1. [StructLayout(LayoutKind.Explicit, Size=N)] with FieldOffset for every field, or
2. [StructLayout(LayoutKind.Sequential, Pack=P, Size=N)] plus manifest assertions.

At startup/build:
assert UnsafeUtility.SizeOf<T>() == N
assert Marshal.OffsetOf<T>(field) == expected
assert header endian/version/stride == expected
```

All unmanaged generic pickling must take a `BinaryLayoutId` or be limited to process-local scratch buffers. Save files should prefer compact explicit DTOs like the existing compact16 path, not raw runtime structs.

## 4. Determinism Drift Across DX11/Vulkan And Burst Vectorization

Evidence:
- AUP has a millimeter multiplier constant in `Assets/_Project/Scripts/World/AUPMath.cs:11-14`.
- `AUPMath` is `[BurstCompile(FloatMode.Fast, FloatPrecision.Standard)]` in `AUPMath.cs:19-20`.
- AUP distance uses double deltas in `AUPMath.cs:29-33`, but direction and runtime downcast use float paths in `AUPMath.cs:39-43` and `AUPMath.cs:72-83`.
- `DistanceMath` uses Burst Fast and `math.rsqrt` for high-quality normalize/distance in `Assets/_Project/Scripts/Core/DistanceMath.cs:42-105` and `DistanceMath.cs:146-153`.
- Existing floating-origin watchdog runs every 300 frames and drift threshold is 1 mm in `Assets/_Project/Scripts/HectonFloatingOrigin.cs:81-93`.
- The drift job checks only two tracked entities in `HectonFloatingOrigin.cs:88` and schedules at `HectonFloatingOrigin.cs:1169-1187`.
- On invalid drift, it reports jitter and triggers an origin shift in `HectonFloatingOrigin.cs:1204-1238`.

Strategic answer:
Do not claim deterministic equivalence between MX350/DX11 and Steam Deck/Vulkan for Burst Fast/vectorized float math. CPU Burst behavior, vectorization, compiler target, and GPU-side presentation can drift if gameplay authority reads back or depends on approximated floats. The existing AUP design reduces precision loss, but the current 300-frame check is a watchdog for two critical transforms, not a full sync fence.

Required 300-frame sync fence:

```text
Every 300 frames, in a post-fixed dispatcher swap window:
1. Combine critical authority job handles.
2. Complete only inside the explicit sync-fence window.
3. Convert critical positions to AUP authority.
4. Quantize local meters to integer millimeters:
      localMm = round(localMeters * 1000)
      localMeters = localMm * 0.001
5. Rebuild presentation transforms from AUP.
6. Clamp or zero nonfinite velocities.
7. Write hash(frame, entityId, grid, localMm, velocityMm) to blackbox.
8. If remote/replay hash differs, snap authority and blend visuals over 2-4 frames.
```

Gameplay authority should prefer squared distance, dominant-axis approximations, or fixed-mm AUP for far/low-tier lanes. `math.rsqrt` is acceptable for visuals and high-tier local effects, not for authoritative replay without a snap fence.

## 5. Blackbox Overhead And The Wooly Passenger Test

Evidence:
- `CrashTelemetryBuffer` uses fixed 64-byte telemetry entries and fixed export scratch sizes in `Assets/_Project/Scripts/CrashTelemetryBuffer.cs:31-35` and `CrashTelemetryBuffer.cs:192-236`.
- Live telemetry queues a ThreadPool write every 60 frames in `CrashTelemetryBuffer.cs:2418-2456`.
- Live telemetry background write uses `FileStream.Write`, `SetLength`, and `Flush(true)` in `CrashTelemetryBuffer.cs:2464-2487`.
- Crash export snapshots on trigger and queues a background export in `CrashTelemetryBuffer.cs:2510-2548`.
- Crash export builds scratch with guarded native copies in `CrashTelemetryBuffer.cs:2645-2675`.
- Crash export thread writes `ExportScratchSizeBytes` and calls `Flush(true)` in `CrashTelemetryBuffer.cs:2707-2749`.
- `GlobalTelemetryBus.TryEmergencyFlushSynchronous()` does not flush synchronously; it queues async and returns false in `Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs:556-563`.
- `GlobalTelemetryBus` has a dedicated export thread in `GlobalTelemetryBus.cs:919-945` and writes dumps on that thread in `GlobalTelemetryBus.cs:1047-1091`.
- `WorldChunkResidencyManager.DumpTelemetry()` still uses synchronous `FileStream` plus `BinaryWriter` in `WorldChunkResidencyManager.cs:2502-2534`.

Strategic answer:
Main crash trigger is mostly bounded when it only flips interlocked state and signals background IO. However, the blackbox can still exceed 0.05 ms during snapshot staging and any synchronous subsystem dump. Crash export is not a frame feature, but if a subsystem writes inside the runtime path, it becomes the passenger consuming budget while reporting on budget.

Required lock-free background streaming strategy:

```text
Hot path:
- fixed NativeArray ring per critical system
- atomic write cursor
- no FileStream, no BinaryWriter, no string formatting
- crash trigger = Interlocked.Exchange(exportRequest, 1) + event signal

Writer thread:
- preallocated export slots, each slot owns byte buffer and metadata
- SPSC ring from main/crash producers to writer
- writer drains slots, writes sequential append blocks
- fsync only on crash/fatal, not every live sample
- drops oldest noncritical telemetry when writer is behind

Frame budget:
- hot-path record target: <2 us/system
- crash trigger target: <10 us
- background export: unbounded by frame, priority BackgroundIo
```

## Required Architecture Changes

1. Add `IJobAdmissionService` in Core with token buckets, lane budget metadata, and measured EWMA cost per job family. High-volume systems must request admission before `.Schedule()`.
2. Add `IStreamingBackpressureService` fed by `WorldChunkResidencyManager` drive-latency EWMA, pending age, and critical-ring debt. `HectonPlayerMovement` consumes a generic speed multiplier/clamp, not a residency concrete dependency.
3. Add `BinaryLayoutManifest` and build/startup assertions for every persisted, mmap, network, GPU upload, and raw-pickled struct.
4. Add `AupSyncFence300` after fixed-step authority jobs. It quantizes and hashes critical AUP state and writes drift to blackbox.
5. Move remaining synchronous subsystem telemetry dumps to the existing background export pattern or a shared lock-free writer.

## Low/Middle/High/Ultra Behavior

Low:
- 2 disposable worker lanes, strict token caps.
- Predictive streaming clamps early under storage debt.
- LOD1/proxy first; no far high-quality rsqrt authority.
- Blackbox stores hashes/state summaries only.

Middle:
- Adaptive token refill based on frame debt.
- Drive EWMA controls prediction distance.
- Sync fence covers player, vehicles, fauna leaders, active physics hazards.

High:
- Larger token caps after critical debt is zero.
- More prefetch and visual interpolation.
- Richer blackbox with compact state deltas.

Ultra:
- Visual overkill only from surplus tokens: denser voxel meshing, vegetation, particles, and high-quality math.
- Same hard fences for authority, IO debt, and binary layout.

## Final Verdict

The project has serious DOD patterns already: dispatcher lanes, swap-window completion, foveated simulation, fixed-size telemetry, explicit AUP DTOs, async upload budgets, and compact save records. The missing pieces are cross-domain: global job admission, storage-latency backpressure, and formal binary layout enforcement. Without those, the system can pass on a workstation and still fail on the exact target class that matters: 4-core CPU, slow storage, and IL2CPP platform variance.
