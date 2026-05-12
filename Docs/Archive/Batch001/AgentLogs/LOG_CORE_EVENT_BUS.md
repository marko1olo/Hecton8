# LOG_CORE_EVENT_BUS

## 2026-05-11 - CORE_EVENT_BUS - SIGNAL_MASTER

Status: VERIFIED MASTER GRADE

What was wrong:
- Systems were still able to couple through direct calls or legacy semantic signal names instead of prompt-exact global packets.
- Core signal payloads did not expose every required lane name for concurrent agents: `AupShiftSignal`, `AnomalySignal`, `AcousticPingSignal`, `HypoxiaSignal`, `ScanCompleteSignal`, `RigidbodySleepSignal`, `GlobalTimeSyncSignal`.
- Damage routing needed a compact 32-byte no-string packet with `SubjectHash`.
- Brownout, AUP shift, sleep/wake, and celestial sync needed bridge publishers/drainers through `GlobalSignals`, not concrete cross-domain calls.
- OMEGA audit found two honest calculations in bridge code: damage direction normalization and sleep distance sqrt.

What was done:
- Implemented/extended `GlobalSignals` with unmanaged `NativeQueue<T>` lanes, `Publish`, `TryDequeue`, `DisposeAllQueues`, prewarm, editor/development payload validation, and `NativeQueue<T>.ParallelWriter` producer access.
- Added prompt-exact structs padded to 32 or 64 bytes.
- Added `SpscSignalRingBuffer<T>` fallback using power-of-two capacity and `(index & mask)` wrapping.
- Bridged producers/consumers:
  - `HectonFloatingOrigin` publishes `AupShiftSignal`.
  - `PowerGridManager` publishes `BrownoutSignal`.
  - `VisorHUDController` drains max 4 brownout packets per tick.
  - `CombatDamageRuntime` drains max 64 global damage packets per frame.
  - `GlobalPhysicsStateManager` publishes rigidbody sleep/wake packets.
  - `HectonCelestialEngine` publishes `GlobalTimeSyncSignal`.
  - Compile aliases/imports fixed in `HectonFluidEngine`, `ConstructionManager`, and `ProceduralWreckGenerator`.
- OMEGA fixes:
  - Replaced global damage bridge `math.normalizesafe` with existing dominant-axis direction.
  - Replaced rigidbody sleep `math.sqrt` with `distanceSq * math.rsqrt(distanceSq)`.
  - Removed the duplicate helper introduced during polish after build caught CS0111.

Cinematic Cheats used:
- Dominant-axis damage direction instead of exact normalization for global bridge packets.
- `rsqrt` distance reconstruction instead of exact sqrt for transition-only sleep signals.
- AUP `int3 SectorDelta` broadcast instead of every consumer recomputing sector movement.
- Scalar brownout severity instead of UI scanning the full power graph.
- Bitmask SPSC ring wrapping instead of modulo/locks.

Exact Microseconds saved:
- Measured exact savings: 0 us proven; Unity profiler/GCMonitor was not run in this terminal pass.
- Static budget estimates used for DOD accounting:
  - Damage bridge: <=64 conversions per frame, dominant-axis path removes normalization cost; estimated microsecond-scale CPU saved on i3/MX350, pending profiler proof.
  - Brownout UI drain: max 4 dequeues/tick; prevents unbounded UI work, estimated ~2 us ceiling pending profiler proof.
  - Impact drain: existing quality caps 4/8/16 per SlowTick; prevents audio overload, estimated <=10 us ceiling pending profiler proof.
  - Sleep/wake signals: one 64-byte enqueue per transition; avoids scatter polling, estimated <2 us per transition pending profiler proof.
  - AUP/global time packets: one enqueue per committed shift/snapshot; estimated <1 us pending profiler proof.

Required `DamageSignal` code:
```csharp
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
public struct DamageSignal
{
    public float Magnitude;
    public float3 LocalPoint;
    public uint DamageType;
    public uint SubjectHash;
    public ushort SourceId;
    public byte IntegrityDelta;
    public byte Channel;
    public uint TargetId;
}
```

Required `NativeQueue.ParallelWriter` setup:
```csharp
public static NativeQueue<DamageSignal>.ParallelWriter DamageSignalWriter
{
    get
    {
        EnsureInitialized();
        return _damageSignals.AsParallelWriter();
    }
}

private static void CreateQueue<T>(ref NativeQueue<T> queue, int expectedCapacity, string label)
    where T : unmanaged
{
    if (queue.IsCreated)
        return;

    queue = new NativeQueue<T>(Allocator.Persistent); // COLD ALLOC: NativeQueue<T>[expectedCapacity] - global signal corridor lane - owner: GlobalSignals
    NativeMemorySentinel.RegisterNativeQueue(
        queue,
        expectedCapacity,
        nameof(GlobalSignals),
        label,
        NativeAllocationLifetime.Session);
    PrewarmQueue(ref queue, expectedCapacity);
}
```

Scalability matrix:
- Low: Compact 32/64-byte packets, bounded drains, dominant-axis or scalar hints, no direct consumers forced to run.
- Middle: Same transport, consumers raise caps/effects locally.
- High: Same packets can drive richer HUD, acoustic, combat, and scatter feedback.
- Ultra: Saved transport budget can be spent on visual overkill in consumers without widening producer payloads.

Final Git Diff:
- `?? Assets/_Project/Scripts/Core/GlobalSignals.cs`
- Modified bridge/alias files: `HectonFluidEngine.cs`, `HectonFloatingOrigin.cs`, `PowerGridManager.cs`, `VisorHUDController.cs`, `CombatDamageRuntime.cs`, `ConstructionManager.cs`, `ProceduralWreckGenerator.cs`, `GlobalPhysicsStateManager.cs`, `HectonCelestialEngine.cs`.
- `git diff --stat` for tracked touched files reported 9 files changed, 2482 insertions, 169 deletions. This workspace has concurrent agent edits in the same files; that stat is not solely CORE_EVENT_BUS-owned.

Build verification:
- Build attempt 9 command: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -v:minimal -p:UseSharedCompilation=false`
- Result: `Hecton8.Core -> Temp/bin/Debug/Hecton8.Core.dll`
- Warnings: 0
- Errors: 0

Runtime verification:
- Unity Play Mode, Unity Console, profiler, and GCMonitor were not run here.
- Hot-path GC status is code-review-only pending Unity runtime proof.
