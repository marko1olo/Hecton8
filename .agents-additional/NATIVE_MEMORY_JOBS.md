# NATIVE_MEMORY_JOBS.md
# Technical Mandate #5 — Native Memory & Job System Protocol
# Hecton-8 Project | Unity 6 | Classification: ENGINEERING LAW
# Authority: Principal System Architect / CTO
# Version: 1.0.0 | Status: RATIFIED | Supersedes: All prior verbal agreements

---

## PREAMBLE — WHY THIS DOCUMENT EXISTS

Native collections in Unity's Job System operate outside CLR garbage collector.
They are raw, unmanaged memory. They do not forgive misuse.

A single premature `Dispose()` call produces Use-After-Free. single missing
dependency edge produces data races that manifest as non-deterministic, unreproducible
crashes on player hardware — crashes that waste weeks of engineering time and destroy
shipped builds.

This mandate exists because "being careful" is not an engineering process.
**Strict protocol is only engineering process.**

Every rule in this document exists because someone, somewhere, in real shipped game,
paid cost of not having it. Hecton-8 will not pay that cost.

Non-compliance is blocking issue. PRs violating this mandate will not be merged.

---

## SECTION 0 — AUTHORIZED AUTONOMY

```
MANDATE_ID       : NATIVE_MEMORY_JOBS
MANDATE_VERSION  : 1.0.0
AUTHORITY_LEVEL  : PRINCIPAL_ARCHITECT
SCOPE            : ALL_HECTON8_SYSTEMS
ENFORCEMENT      : BLOCKING — CI GATE + HUMAN REVIEW
EXCEPTION_PROCESS: Written justification → CTO approval → Inline comment block
REVISION_CYCLE   : Per major Unity 6 LTS patch or when crash post-mortem demands it
```

This mandate applies to:
- World Streaming subsystem
- AI Perception and Pathfinding subsystems
- Physics simulation and collision broadphase
- Renderer data export pipeline
- Any system that allocates `NativeArray`, `NativeList`, `NativeHashMap`,
  `NativeQueue`, or any `Unity.Collections` type

This mandate applies to every engineer on project regardless of seniority.

---

## SECTION 1 — EXECUTION POLICY

### 1.1 Prime Directives (Memorize These)

```
PRIME_01: Never call .Complete() in middle of Tick(). EVER.
PRIME_02: Never Dispose() collection while job handle referencing it is alive.
PRIME_03: Never expose Back buffer to readers. Front buffer is read-only to all
          external systems.
PRIME_04: Never recreate NativeArray where resizing or pre-allocation suffices.
PRIME_05: Never use managed references inside Burst-compiled jobs.
PRIME_06: NativeDisableContainerSafetyRestriction requires written 3-paragraph
          technical justification committed to source file before merge.
PRIME_07: Data flows in one direction per frame. No circular job dependencies.
```

Violation of any Prime Directive is Severity-1 defect. It will be fixed before
any other work proceeds.

### 1.2 Enforcement Gates

| Gate | Tool | Action on Failure |
|------|------|-------------------|
| Static Analysis | Roslyn Analyzer (custom) | CI blocks merge |
| Burst Safety Check | Burst Inspector in Editor | Build fails |
| Job Leak Detector | JobsDebugger (Development builds) | Automated red flag |
| Memory Profiler Snapshot | Unity Memory Profiler | Weekly review, P1 on leak |
| Code Review | Human reviewer with this document open | Mandatory sign-off |

---

## SECTION 2 — DATA ARCHITECTURE

### 2.1 Canonical System Memory Layout

Every system that participates in job pipeline MUST conform to this layout:

```csharp
/// <summary>
/// Canonical Hecton-8 system memory layout.
/// All job-driven subsystems inherit this pattern exactly.
/// Deviation requires NATIVE_MEMORY_JOBS mandate exception approval.
/// </summary>
public struct SystemNativeMemory : IDisposable
{
    // --- DOUBLE BUFFER (Section 3) ---
    public NativeArray<T> FrontBuffer;       // READ-ONLY to all external systems
    public NativeArray<T> BackBuffer;        // WRITE target for this frame's jobs

    // --- JOB HANDLES (Section 4) ---
    public JobHandle WriteJobHandle;         // Handle for all back-buffer writers
    public JobHandle ReaderFence;            // Combined handle of all front readers

    // --- DISPOSAL QUEUE (Section 5) ---
    // Owned by subsystem manager, not this struct.
    // See PendingDisposalQueue<T>.

    // --- CAPACITY METADATA (Section 6) ---
    public int LogicalCount;                 // Active element count (< Capacity)
    public int Capacity;                     // Allocated slots

    // --- ALLOCATOR RECORD ---
    private readonly Allocator _allocator;

    public SystemNativeMemory(int initialCapacity, Allocator allocator)
    {
        _allocator   = allocator;
        Capacity     = initialCapacity;
        LogicalCount = 0;

        FrontBuffer  = new NativeArray<T>(initialCapacity, allocator,
                           NativeArrayOptions.ClearMemory);
        BackBuffer   = new NativeArray<T>(initialCapacity, allocator,
                           NativeArrayOptions.ClearMemory);

        WriteJobHandle = default;
        ReaderFence    = default;
    }

    public void Dispose()
    {
        // Caller MUST ensure all handles are complete before calling Dispose.
        // Use PendingDisposalQueue for deferred cases. See Section 5.
        if (FrontBuffer.IsCreated) FrontBuffer.Dispose();
        if (BackBuffer.IsCreated)  BackBuffer.Dispose();
    }
}
```

### 2.2 Structural Ownership Rules

```
RULE_ARCH_01: Each NativeCollection has exactly ONE owner system.
              No shared ownership. No "borrow without contract".

RULE_ARCH_02: External systems receive READ-ONLY NativeArray<T>.AsReadOnly() slices
              of FrontBuffer. They never receive reference to BackBuffer.

RULE_ARCH_03: owner system is solely responsible for scheduling all writes
              to BackBuffer and for managing swap.

RULE_ARCH_04: Systems MUST declare their data dependencies explicitly in their
              SystemBase.OnUpdate() or ISystem.OnUpdate() dependency declarations.
              Implicit dependencies are forbidden.

RULE_ARCH_05: All persistent allocations use Allocator.Persistent.
              TempJob is permitted only for allocations whose lifetime is
              provably bounded to single frame and are scheduled + completed
              within that frame boundary. Temp is forbidden for job-scheduled work.
```

---

## SECTION 3 — DOUBLE-BUFFERING PATTERN (MANDATORY)

### 3.1 Rationale

The Renderer and AI subsystems read entity transform and state data every frame.
The World Streaming and Physics jobs write new state every frame. Without isolation,
a reader can observe partially-written state — torn read — producing visual
artifacts, incorrect AI decisions, or outright crashes.

Double-buffering gives readers stable, immutable snapshot (Front) while writers
produce next frame's data in isolation (Back). swap is atomic with respect
to frame boundary.

### 3.2 Buffer Roles — Non-Negotiable

| Buffer | Role | Access | Writer | Reader |
|--------|------|--------|--------|--------|
| Front | Last frame's completed result | Read-Only | NOBODY | All external systems |
| Back | This frame's in-progress result | Write | Owner system jobs | NOBODY external |

**The Back buffer is private. It is never exposed. No exceptions.**

### 3.3 Swap Protocol

The swap MUST follow this exact sequence. No reordering. No shortcuts.

```csharp
/// <summary>
/// Executes Front/Back buffer swap.
/// MUST be called at end-of-frame after ReaderFence is verified complete.
/// MUST NOT be called mid-Tick.
/// </summary>
private void ExecuteBufferSwap(ref SystemNativeMemory memory)
{
    // STEP 1: Verify reader fence is complete.
    // All systems that read FrontBuffer last frame must be done.
    // This is ONLY place Complete() may be called on ReaderFence,
    // and only at end-of-frame in designated swap window.
    if (!memory.ReaderFence.IsCompleted)
    {
        // This path means frame time exceeded budget.
        // Log it. Force complete. Do NOT skip swap.
        HectonProfiler.LogFenceOverrun(nameof(ExecuteBufferSwap));
        memory.ReaderFence.Complete();
    }

    // STEP 2: Verify write job is complete.
    // BackBuffer writers from this frame must have finished.
    if (!memory.WriteJobHandle.IsCompleted)
    {
        HectonProfiler.LogFenceOverrun(nameof(ExecuteBufferSwap) + "_WriteJob");
        memory.WriteJobHandle.Complete();
    }

    // STEP 3: Swap buffer references.
    (memory.FrontBuffer, memory.BackBuffer) =
        (memory.BackBuffer, memory.FrontBuffer);

    // STEP 4: Reset handles. Old handles are now stale.
    memory.WriteJobHandle = default;
    memory.ReaderFence    = default;

    // STEP 5: Update logical count.
    memory.LogicalCount   = ComputeNewLogicalCount();
}
```

### 3.4 Registering Readers

Every external system that reads FrontBuffer MUST register its job handle with
the owning system so ReaderFence can be constructed:

```csharp
// In READING system's OnUpdate:
var readHandle = new ReadEntityStateJob
{
    States = worldStreamer.GetFrontBufferReadOnly()  // NativeArray<T>.AsReadOnly()
}.Schedule(entityCount, 64, inputDeps);

// MANDATORY: Register this handle with owner so it enters ReaderFence.
worldStreamer.RegisterReaderHandle(readHandle);

// In OWNING system, at end-of-frame:
memory.ReaderFence = JobHandle.CombineDependencies(registeredReaderHandles);
registeredReaderHandles.Clear();
```

---

## SECTION 4 — JOBHANDLE DISCIPLINE

### 4.1 Complete() Rule

```
ABSOLUTE RULE: .Complete() is FORBIDDEN inside any Tick(), OnUpdate(), or
               frame-update method EXCEPT in designated end-of-frame swap
               window described in Section 3.3.

The only other permitted use of .Complete() is:
  (a) Inside OnDestroy() / system teardown, draining all pending jobs before Dispose.
  (b) Inside blocking synchronization point that is explicitly annotated with
      [BLOCKING_SYNC_POINT] and approved via mandate exception.

Calling .Complete() mid-frame to "read results immediately" is #1 cause of
CPU stalls in Hecton-8's job pipeline. It serializes worker threads.
It eliminates all parallelism. It is not solution. It is regression.
```

### 4.2 Dependency Chaining with CombineDependencies

All job scheduling MUST thread dependencies explicitly. `inputDeps` pattern
from `ISystem` is baseline. Multi-system dependencies use `CombineDependencies`:

```csharp
/// <summary>
/// Correct dependency chaining for multi-stage pipeline.
/// No .Complete() calls. No orphaned handles.
/// </summary>
private JobHandle ScheduleFramePipeline(JobHandle inputDeps)
{
    // Stage A: Spatial partitioning update (reads transforms)
    var spatialHandle = new UpdateSpatialGridJob
    {
        Transforms = transformBuffer.FrontBuffer.AsReadOnly(),
        Grid       = spatialGrid.BackBuffer
    }.Schedule(entityCount, 32, inputDeps);

    // Stage B: AI perception query (reads spatial grid just written)
    var perceptionHandle = new AIPerceptionJob
    {
        Grid        = spatialGrid.BackBuffer.AsReadOnly(),
        PerceptData = aiMemory.BackBuffer
    }.Schedule(agentCount, 16, spatialHandle);  // <-- depends on Stage A

    // Stage C: Physics broadphase (independent of AI, but needs transforms)
    var broadphaseHandle = new BroadphaseUpdateJob
    {
        Transforms  = transformBuffer.FrontBuffer.AsReadOnly(),
        PairBuffer  = physicsPairs.BackBuffer
    }.Schedule(entityCount, 64, inputDeps);  // <-- depends only on inputDeps

    // Combine: both perception and broadphase feed integration step
    var combinedHandle = JobHandle.CombineDependencies(
        perceptionHandle,
        broadphaseHandle
    );

    // Stage D: Integration (needs both AI decisions and physics pairs)
    var integrationHandle = new IntegrationJob
    {
        AIDecisions   = aiMemory.BackBuffer.AsReadOnly(),
        PhysicsPairs  = physicsPairs.BackBuffer.AsReadOnly(),
        NewTransforms = transformBuffer.BackBuffer
    }.Schedule(entityCount, 32, combinedHandle);

    return integrationHandle;  // Caller tracks this. No .Complete() here.
}
```

### 4.3 Handle Tracking

Every active JobHandle MUST be tracked. An untracked handle is leak.

```csharp
/// <summary>
/// System-level handle registry. Cleared at end of each frame after swap.
/// All scheduled handles are registered here before method returns.
/// </summary>
private NativeList<JobHandle> _activeHandles;

private void RegisterHandle(JobHandle handle)
{
    _activeHandles.Add(handle);
}

/// <summary>
/// End-of-frame: combine all active handles into system's output fence.
/// Called ONCE, in designated end-of-frame window.
/// </summary>
private JobHandle BuildOutputFence()
{
    var fence = JobHandle.CombineDependencies(_activeHandles.AsArray());
    _activeHandles.Clear();
    return fence;
}
```

### 4.4 Forbidden Patterns

```csharp
// ❌ FORBIDDEN — PRIME_01 VIOLATION
void OnUpdate(ref SystemState state)
{
    var handle = new SomeJob().Schedule(state.Dependency);
    handle.Complete();  // SERIALIZES PIPELINE. FORBIDDEN.
    var result = someNativeArray[0];  // This is why you wanted Complete(). Wrong.
    // Use deferred read mechanism or reactive event instead.
}

// ❌ FORBIDDEN — orphaned handle
void OnUpdate(ref SystemState state)
{
    new SomeJob().Schedule(state.Dependency);
    // Handle discarded. Job runs untracked. Dispose() will race with it.
}

// ❌ FORBIDDEN — stale handle reuse
void OnUpdate(ref SystemState state)
{
    if (cachedHandle.IsCompleted)
        cachedHandle = new SomeJob().Schedule();
    // cachedHandle from 3 frames ago. Dependency graph is broken.
}
```

---

## SECTION 5 — DEFERRED DISPOSAL PROTOCOL

### 5.1 Disposal Problem

```
A NativeArray disposed while Job still holds pointer to it is undefined behavior.
The safety system catches this in development builds.
In release builds, it produces silent memory corruption.
Silent memory corruption produces crash reports with no useful stack trace.
That outcome is unacceptable.
```

Never call `Dispose()` on any collection whose associated handle is not confirmed
complete. This applies even when you "know" job should be done by now.
Certainty is not knowledge. `handle.IsCompleted` is knowledge.

### 5.2 PendingDisposalQueue Implementation

```csharp
/// <summary>
/// Thread-safe deferred disposal registry.
/// Populated during Tick() when collection is logically retired.
/// Drained during SlowTick() (every N frames) or on system teardown.
/// </summary>
public sealed class PendingDisposalQueue
{
    private struct DisposalEntry
    {
        public JobHandle     PendingHandle;
        public INativeDisposable Target;
        public string        DebugTag;      // For profiling/crash diagnostics
        public int           EnqueuedFrame;
    }

    private readonly List<DisposalEntry> _queue = new(64);
    private const int MaxFrameAge = 8;  // Escalate to error after 8 frames

    /// <summary>
    /// Register collection for deferred disposal.
    /// Call this instead of target.Dispose() whenever job handle is in flight.
    /// </summary>
    public void Enqueue(JobHandle pendingHandle, INativeDisposable target,
                        string debugTag, int currentFrame)
    {
        _queue.Add(new DisposalEntry
        {
            PendingHandle = pendingHandle,
            Target        = target,
            DebugTag      = debugTag,
            EnqueuedFrame = currentFrame
        });
    }

    /// <summary>
    /// Called from SlowTick() — e.g., every 4 frames or on dedicated thread.
    /// Drains all entries whose jobs have completed.
    /// </summary>
    public void DrainCompleted(int currentFrame)
    {
        for (int i = _queue.Count - 1; i >= 0; i--)
        {
            var entry = _queue[i];

            int age = currentFrame - entry.EnqueuedFrame;
            if (age > MaxFrameAge)
            {
                // Handle has been pending too long. This indicates job leak.
                // Log at Error level. Force complete to unblock disposal.
                Debug.LogError(
                    $"[PendingDisposal] Handle age exceeded {MaxFrameAge} frames. " +
                    $"Tag: {entry.DebugTag}. Forcing complete. Investigate job leak.");
                entry.PendingHandle.Complete();
            }

            if (entry.PendingHandle.IsCompleted)
            {
                entry.Target.Dispose();
                _queue.RemoveAtSwapBack(i);
            }
        }
    }

    /// <summary>
    /// Blocking drain for use in OnDestroy() only.
    /// Forces all pending completions. Not for use during gameplay.
    /// </summary>
    public void ForceCompleteAndDrainAll()
    {
        foreach (var entry in _queue)
        {
            entry.PendingHandle.Complete();
            entry.Target.Dispose();
        }
        _queue.Clear();
    }
}
```

### 5.3 Usage Pattern

```csharp
// When retiring buffer (e.g., after chunk is unloaded):

// ❌ WRONG:
oldBuffer.Dispose();  // Job may still be reading it. Use-After-Free.

// ✅ CORRECT:
disposalQueue.Enqueue(
    pendingHandle: activeWriteHandle,
    target:        oldBuffer,
    debugTag:      $"ChunkBuffer_Unload_{chunkId}",
    currentFrame:  Time.frameCount
);
// oldBuffer is now owned by queue. Do not touch it again.
oldBuffer = default;  // Nullify local reference immediately to prevent accidental use.
```

---

## SECTION 6 — RESIZING STRATEGY

### 6.1 Reallocation Problem

`NativeArray` is fixed-size. Replacing `NativeArray` with larger one requires:
1. Allocating new memory
2. Copying existing data (via job or `NativeArray.Copy`)
3. Deferring disposal of old array (Section 5)
4. Updating all reader handles to point to new array

This is expensive. It produces allocation spikes visible in memory profiler.
Done frequently, it fragments native heap. Done naively, it causes Use-After-Free.

**The goal is to make reallocation rare, controlled event — not per-frame occurrence.**

### 6.2 Pre-Allocation Rule

```
RULE_RESIZE_01: All Allocator.Persistent NativeArrays MUST be pre-allocated to
                their expected maximum operational size at system initialization.
                If maximum is unknowable, allocate 2x expected average.

RULE_RESIZE_02: NativeList with Allocator.Persistent is preferred type when
                logical count varies. Set .Capacity at initialization.
                Do NOT allow NativeList to auto-grow via Add() in hot path.
                Pre-validate capacity before batch insertions.

RULE_RESIZE_03: Reallocation is FORBIDDEN during normal gameplay if system
                has been operational for more than 1 second. Reallocation during
                first-second initialization window is permitted.

RULE_RESIZE_04: Any reallocation event MUST be logged at Warning level with the
                old capacity, new capacity, and system name. This log is monitored.
```

### 6.3 Exponential Growth Implementation

When reallocation is unavoidable (e.g., genuine worst-case data surge), use
exponential growth (doubling) to amortize future reallocations:

```csharp
/// <summary>
/// Grows NativeList's backing capacity using exponential (x2) growth.
/// Schedules old array for deferred disposal.
/// Returns new handle incorporating copy job.
/// MUST NOT be called on main thread during hot frame path.
/// Call from capacity-validation step at frame boundaries only.
/// </summary>
private JobHandle GrowNativeList<T>(
    ref NativeList<T>   list,
    int                 requiredCapacity,
    JobHandle           inputDeps,
    PendingDisposalQueue disposalQueue,
    string              debugTag) where T : unmanaged
{
    if (list.Capacity >= requiredCapacity)
        return inputDeps;  // No growth needed.

    int newCapacity = list.Capacity;
    while (newCapacity < requiredCapacity)
        newCapacity *= 2;  // Exponential growth. Always double.

    Debug.LogWarning(
        $"[NativeGrowth] {debugTag}: {list.Capacity} → {newCapacity} " +
        $"(required: {requiredCapacity}). Frame: {Time.frameCount}");

    // NativeList.SetCapacity handles internal reallocation.
    // This is safe to call before scheduling new jobs on this list,
    // provided inputDeps covers all current jobs touching it.
    inputDeps.Complete();  // EXCEPTION: Capacity change is structural event.
                            // Document this call site. It is not mid-Tick stall;
                            // it is controlled structural resize at frame boundary.

    list.SetCapacity(newCapacity);

    return default;  // Caller reschedules jobs against grown list.
}
```

### 6.4 Capacity Budget Table (Hecton-8 Defaults)

| System | Collection | Initial Capacity | Max Expected | Growth Policy |
|--------|-----------|-----------------|--------------|---------------|
| World Streamer | EntityStateBuffer | 8192 | 16384 | Pre-allocate max |
| AI Perception | PerceptEntryBuffer | 4096 | 8192 | Pre-allocate max |
| Physics | BroadphasePairBuffer | 16384 | 65536 | Exponential x2 |
| Renderer Export | VisibleEntityBuffer | 4096 | 12288 | Pre-allocate max |
| Pathfinding | OpenSetBuffer | 2048 | 8192 | Exponential x2 |

These values are validated against profiler data quarterly. table lives
in `Systems/MemoryBudgets.cs` as compile-time constants, not magic numbers.

---

## SECTION 7 — MEMORY BARRIERS & THREAD SAFETY (SPSC)

### 7.1 SPSC Contract

Single-Producer Single-Consumer queues are used in Hecton-8 for passing results
from worker job threads to main thread (e.g., streaming completion events,
AI decision outputs). SPSC contract is:

```
PRODUCER: Exactly one Job writes to queue. No other writer exists.
CONSUMER: Exactly one consumer (main thread or designated reader job) reads.
Both sides are aware of this contract and uphold it without exception.
```

Violation of SPSC contract by introducing second producer or consumer
immediately invalidates all safety assumptions and requires full MPMC
(Multiple-Producer Multiple-Consumer) design with heavier synchronization.

### 7.2 Memory Ordering Requirements

On modern x86/x64 hardware, store-load ordering is strong enough that
simple volatile reads/writes suffice for head/tail indices in an SPSC queue.
However, Unity jobs run on ARM targets (mobile, console) where memory ordering
is weak. **Treat all target platforms as if they have weak memory ordering.**

Use `Interlocked` or `System.Threading.Volatile` for all index mutations:

```csharp
/// <summary>
/// Lock-free SPSC ring buffer for job-to-main-thread result passing.
/// Producer (job thread) writes. Consumer (main thread) reads.
/// Safe on ARM with explicit memory barriers via Interlocked / Volatile.
/// </summary>
[NativeContainer]
[NativeContainerIsAtomicWriteOnly]  // Hint to safety system re: write access
public unsafe struct SPSCResultQueue<T> where T : unmanaged
{
    [NativeDisableUnsafePtrRestriction]
    private T*  _buffer;
    private int _capacity;

    // Indices are cache-line padded to prevent false sharing.
    // Head is written by consumer, read by producer.
    // Tail is written by producer, read by consumer.
    [NativeDisableUnsafePtrRestriction]
    private int* _head;  // Consumer advances head after reading.
    [NativeDisableUnsafePtrRestriction]
    private int* _tail;  // Producer advances tail after writing.

    /// <summary>
    /// Producer-side enqueue. Called from Job thread ONLY.
    /// Returns false if queue is full (backpressure signal).
    /// </summary>
    public bool TryEnqueue(T item)
    {
        int tail    = Volatile.Read(ref *_tail);
        int nextTail = (tail + 1) % _capacity;

        // Read head with acquire semantics to observe consumer progress.
        int head = Volatile.Read(ref *_head);

        if (nextTail == head)
            return false;  // Full. Producer must handle backpressure.

        _buffer[tail] = item;  // Write item before advancing tail.

        // Release barrier: ensure item write is visible before tail update.
        Interlocked.Exchange(ref *_tail, nextTail);

        return true;
    }

    /// <summary>
    /// Consumer-side dequeue. Called from main thread ONLY.
    /// Returns false if queue is empty.
    /// </summary>
    public bool TryDequeue(out T item)
    {
        // Acquire barrier: observe producer's tail write.
        int tail = Volatile.Read(ref *_tail);
        int head = Volatile.Read(ref *_head);

        if (head == tail)
        {
            item = default;
            return false;  // Empty.
        }

        item = _buffer[head];  // Read item before advancing head.

        // Release barrier: ensure item read is complete before head update.
        Interlocked.Exchange(ref *_head, (head + 1) % _capacity);

        return true;
    }
}
```

### 7.3 Barrier Rules Summary

```
BARRIER_01: All cross-thread index mutations use Interlocked.Exchange or
            Interlocked.CompareExchange. Naked writes to shared indices are FORBIDDEN.

BARRIER_02: All cross-thread index reads use Volatile.Read.
            Naked reads from shared indices are FORBIDDEN on non-x86 targets.

BARRIER_03: SPSC contract (one producer, one consumer) is documented at the
            declaration site of every SPSCResultQueue with comment naming the
            producer and consumer explicitly.

BARRIER_04: Upgrading SPSC to MPMC requires separate architecture review.
            Do not add second producer to an existing SPSC queue.
            Allocate new queue and redesign ownership model.
```

---

## SECTION 8 — BURST SAFETY

### 8.1 Managed Reference Prohibition

Burst-compiled jobs operate on unmanaged code. Burst compiler rejects managed
references because it cannot reason about GC interaction. Beyond compilation
error, managed references inside jobs are architecturally wrong — they create hidden
dependencies on main thread's managed heap from worker threads.

```
RULE_BURST_01: No managed class references in any IJob, IJobParallelFor,
               IJobChunk, or any type decorated with [BurstCompile].
               This includes: string, List<T>, Dictionary<T,U>, any class
               instance, delegates, Action<T>, Func<T>, Unity Object references.

RULE_BURST_02: Strings needed for Burst debugging use FixedString32Bytes,
               FixedString64Bytes, or FixedString128Bytes from Unity.Collections.

RULE_BURST_03: All job struct fields must be blittable value types or
               NativeContainer types. If you cannot make it blittable, you
               cannot put it in job. Redesign data.

RULE_BURST_04: Function pointers in Burst use FunctionPointer<T> from Burst.
               Delegate types are FORBIDDEN in job structs.
```

### 8.2 NativeDisableContainerSafetyRestriction — Last Resort Attribute

This attribute disables Unity's safety handle checking for specific field.
It tells safety system: "Trust me, I know what I'm doing."

The safety system exists because "trust me" has historically been wrong.
Before this attribute may be used, author MUST provide three-paragraph
technical justification as code comment immediately above field declaration.

**The three paragraphs must address:**

1. **Why safety system's assessment is incorrect for this specific case:**
   Explain precisely why container access pattern this attribute suppresses
   would be flagged as unsafe, and provide formal proof (by code analysis or
   invariant argument) that flag is false positive for this use case.

2. **What alternative approaches were considered and why they were rejected:**
   Document at least two alternative designs (e.g., aliased read-only access,
   job restructuring, data duplication) and explain concrete, measured reason
   each is not viable for this system's constraints (performance budget, API
   limitations, architectural impossibility).

3. **What invariant code upholds to guarantee safety in absence of check:**
   State runtime invariant that makes this access safe. For example: "The
   index range [X, Y] is exclusively owned by job instance Z due to partition
   scheme established by scheduling code at line N. No other job instance
   writes to or reads from this range within same frame." This invariant must
   be verifiable by code inspection.

**Template:**

```csharp
// SAFETY_JUSTIFICATION_PARAGRAPH_1:
// [Why safety system's flag is false positive for this specific field and
//  access pattern. Reference exact safety check being suppressed and prove
//  by invariant argument that it does not apply here.]

// SAFETY_JUSTIFICATION_PARAGRAPH_2:
// [Alternative approaches considered: (a) [approach], rejected because [reason].
//  (b) [approach], rejected because [reason]. Minimum two alternatives required.]

// SAFETY_JUSTIFICATION_PARAGRAPH_3:
// [Invariant maintained by code that guarantees safe access. Name the
//  partition scheme, index range, ownership rule, or scheduling constraint
//  that makes this safe without check.]

[NativeDisableContainerSafetyRestriction]
private NativeArray<T> _sharedDataWithSpecialAccess;
```

**A PR with this attribute and no justification comment is rejected at review.**
**A PR with justification that does not address all three paragraphs is rejected.**

### 8.3 Burst Compilation Verification

All IJob structs in Hecton-8 are decorated with `[BurstCompile]` and verified
in CI via `BurstCompiler.CompileFunctionPointer` tests. job that cannot be
Burst-compiled is blocking issue unless mandate exception is filed.

---

## SECTION 9 — SPATIAL LOCALITY & CACHE EFFICIENCY

### 9.1 Cache Reality

L1 cache: ~32 KB, ~4 cycle access latency.
L2 cache: ~256 KB, ~12 cycle access latency.
RAM: ~8 GB+, ~200 cycle access latency (with TLB miss: worse).

A cache miss on 64-byte cache line that crosses 200 pointers to random
heap locations costs 200 × 200 cycles = 40,000 cycles of stall time.
The same 200 elements accessed sequentially in flat array cost
~200 × 4 cycles (L1 hit after first load) = ~800 cycles.

**The performance difference between pointer-chasing and linear access is 50x.**
This is not micro-optimization. It is dominant performance factor for
simulation-heavy systems.

### 9.2 Data Layout Directives

```
LOCALITY_01: All hot-path entity data lives in flat NativeArray<T> structures.
             No linked lists. No tree nodes with heap-allocated children.
             No arrays of class references. No polymorphic virtual dispatch
             in hot path.

LOCALITY_02: Use Structure-of-Arrays (SoA) layout for entity data accessed
             in parallel jobs. If only Position is needed by job, it should
             iterate NativeArray<float3> positions — not NativeArray<Entity>
             where each Entity is 256-byte struct containing Position plus
             20 other fields job never touches.

LOCALITY_03: Group data by access pattern, not by logical entity.
             Transform data and AI decision data are separate arrays even though
             they belong to same entity. Jobs that need both receive both
             arrays. Jobs that need only one receive only one.

LOCALITY_04: Minimize indirection depth. Maximum two levels of indirection
             in any data structure accessed in Burst job's inner loop.
             If you have Array → Struct → Pointer → Data, refactor.
             Target: Array → Data. Acceptable: Array → Struct containing Data.

LOCALITY_05: Sort entities by spatial proximity (chunk, cell) before processing
             in spatial queries. Spatial sorting produces sequential access
             patterns for nearby entities, maximizing cache reuse in
             neighborhood-search algorithms.

LOCALITY_06: NativeArray initializations use NativeArrayOptions.UninitializedMemory
             when array will be fully written before any read. This avoids
             redundant zero-fill pass. Use NativeArrayOptions.ClearMemory only
             when partial initialization is expected.
```

### 9.3 SoA Layout Pattern

```csharp
// ❌ WRONG — Array of Structures (AoS) for hot-path parallel jobs.
// position-only job loads 512 bytes per entity to get 12 bytes of position.
public struct EntityData
{
    public float3   Position;        // 12 bytes  ← job needs this
    public float3   Velocity;        // 12 bytes
    public float3   Acceleration;    // 12 bytes
    public float4   Orientation;     // 16 bytes
    public int      TeamId;          // 4 bytes
    public int      HealthPoints;    // 4 bytes
    public float    Aggression;      // 4 bytes
    public bool     IsAlive;         // 1 byte (padded to 4)
    // ... 60+ more bytes of data position job ignores
}
NativeArray<EntityData> entities;  // 512+ bytes per entity, 1/40 useful for pos job.

// ✅ CORRECT — Structure of Arrays (SoA). Each array is tightly-packed stream.
// position-only job loads 12 bytes per entity, nothing more.
public struct WorldEntityBuffers
{
    public NativeArray<float3>  Positions;      // Position-only jobs use this.
    public NativeArray<float3>  Velocities;     // Integration jobs use positions + velocities.
    public NativeArray<float4>  Orientations;   // Render jobs use positions + orientations.
    public NativeArray<int>     TeamIds;        // AI jobs use positions + teamIds.
    public NativeArray<int>     HealthPoints;   // Damage jobs use this.
    public NativeArray<float>   Aggressions;    // AI personality — infrequently accessed.
    public NativeArray<bool>    IsAlive;        // Filter step — accessed before all others.
}
```

### 9.4 False Sharing Prevention

When parallel jobs write to per-entity output arrays (one job instance per entity
range), ensure job partition boundaries align to cache lines (64 bytes) to prevent
false sharing between threads writing to adjacent memory:

```csharp
// When scheduling IJobParallelFor, choose innerLoopBatchCount to be a
// multiple of (CacheLineSize / sizeof(T)).
// For float3 (12 bytes): next power-of-2 alignment → batch of 8 (96 bytes > 64).
// For int (4 bytes): batch of 16 (64 bytes exact).
var handle = new PositionIntegrationJob().Schedule(
    entityCount,
    innerLoopBatchCount: 16,  // Tuned per type. Document reasoning.
    inputDeps
);
```

---

## SECTION 10 — SCALE & OPERATIONAL NOTES

### 10.1 Memory Budget Accounting

All `Allocator.Persistent` allocations are tracked in `MemoryBudgetTracker`.
At initialization, each system registers its allocation:

```csharp
MemoryBudgetTracker.Register(
    systemName:  "WorldStreamer",
    collection:  "EntityStateBuffer",
    sizeBytes:   capacity * UnsafeUtility.SizeOf<EntityState>(),
    allocator:   Allocator.Persistent
);
```

The total registered native memory budget for Hecton-8 is:

| Platform | Budget Ceiling | Hard Cap |
|----------|---------------|----------|
| PC (High) | 512 MB | 768 MB |
| PC (Low) | 256 MB | 384 MB |
| Console | 384 MB | 512 MB |
| Mobile (Future) | 128 MB | 192 MB |

Exceeding soft ceiling triggers Warning in Memory Profiler report.
Exceeding hard cap triggers CI failure on nightly build.

### 10.2 Profiling Integration

Every job struct MUST include `[Unity.Profiling.ProfilerMarker]` for frame
profiling. marker name format is: `H8/<SystemName>/<JobName>`:

```csharp
[BurstCompile]
public struct BroadphaseUpdateJob : IJobParallelFor
{
    private static readonly ProfilerMarker Marker =
        new ProfilerMarker("H8/Physics/BroadphaseUpdate");

    public void Execute(int index)
    {
        using var _ = Marker.Auto();
        // ... job body
    }
}
```

---

## SECTION 11 — SAFE OPERATING STANDARDS & EXCEPTION PROCESS

### 11.1 What "Safe" Means in This Context

A native memory operation is SAFE if and only if:

```
SAFE_01: Every NativeCollection has single, named owner at all times.
SAFE_02: Every JobHandle is tracked and incorporated into dependency chain.
SAFE_03: No Dispose() is called on collection with live, incomplete handle.
SAFE_04: Front buffer is read-only to all external systems, always.
SAFE_05: Back buffer is invisible to all external systems, always.
SAFE_06: Thread access to shared indices uses explicit memory barriers.
SAFE_07: All Burst jobs are free of managed references.
SAFE_08: NativeDisableContainerSafetyRestriction has 3-paragraph justification.
SAFE_09: Capacity growth uses exponential doubling, not arbitrary increments.
SAFE_10: Data layout maximizes sequential access in job's inner loop.
```

### 11.2 Exception Process

An exception to any rule in this mandate follows this process:

```
Step 1: Engineer identifies that rule cannot be satisfied for specific case.
Step 2: Engineer writes technical justification (minimum 1 page) covering:
        (a) Which rule is being excepted and why it cannot be satisfied.
        (b) risk introduced by exception.
        (c) mitigating measures that replace rule's protection.
Step 3: Justification is reviewed by Lead Engineer.
Step 4: CTO signs off. No exceptions are approved without CTO sign-off.
Step 5: Approved exception is committed as an inline code comment block
        with exception ID (format: NMJE-<YYYY>-<NNN>).
Step 6: Exception is logged in MANDATE_EXCEPTIONS.md with expiry date.
        All exceptions expire after 90 days and must be re-reviewed.
```

### 11.3 Incident Response

When crash is suspected to involve native memory mismanagement:

```
IR_01: Capture Unity Memory Profiler snapshot immediately.
IR_02: Enable Jobs Debugger and reproduce in Development build.
IR_03: Attach Burst Inspector to suspected job pipeline.
IR_04: Identify owning system of faulting collection.
IR_05: Audit that system's handle tracking and disposal queue against this mandate.
IR_06: Root cause MUST be documented in post-mortem before fix is merged.
IR_07: If root cause is mandate violation, fix includes mandate compliance.
       mandate is not relaxed to accommodate bug.
```

---

## APPENDIX — QUICK REFERENCE CARD

```
┌─────────────────────────────────────────────────────────────────┐
│         HECTON-8 NATIVE MEMORY QUICK REFERENCE                  │
├─────────────────────────────────────────────────────────────────┤
│ DOUBLE BUFFER:   Front = Read-Only export. Back = Write target. │
│                  Swap ONLY when ReaderFence.IsCompleted.        │
├─────────────────────────────────────────────────────────────────┤
│ .Complete():     END OF FRAME ONLY. Never mid-Tick. Never.      │
├─────────────────────────────────────────────────────────────────┤
│ Dispose():       Use PendingDisposalQueue. Never immediate.      │
├─────────────────────────────────────────────────────────────────┤
│ Resize:          Pre-allocate max. Exponential x2 if needed.    │
├─────────────────────────────────────────────────────────────────┤
│ SPSC:            Volatile.Read + Interlocked.Exchange. Always.  │
├─────────────────────────────────────────────────────────────────┤
│ Burst:           No managed refs. Blittable types only.         │
│ SafetyDisable:   3-paragraph justification. CTO approval.       │
├─────────────────────────────────────────────────────────────────┤
│ Layout:          SoA. Flat arrays. Max 2 indirection levels.    │
│                  Sort by spatial proximity before hot loops.    │
└─────────────────────────────────────────────────────────────────┘
```

---

## APPENDIX B — GLOSSARY

| Term | Definition |
|------|-----------|
| Front Buffer | immutable, read-only buffer holding last frame's result. Exported to other systems. |
| Back Buffer | mutable, write-target buffer for current frame's job pipeline. Private. |
| ReaderFence | Combined JobHandle of all jobs that read Front buffer. Must complete before swap. |
| WriteJobHandle | Combined JobHandle of all jobs that write Back buffer. Must complete before swap. |
| PendingDisposalQueue | Registry of NativeCollections awaiting deferred Dispose() after handle completion. |
| SPSC | Single-Producer Single-Consumer. lock-free queue pattern with exactly one writer and one reader. |
| SoA | Structure of Arrays. Data layout where each field of an entity type lives in its own flat array. |
| AoS | Array of Structures. Anti-pattern for parallel jobs. Each element is large struct with all fields. |
| False Sharing | Cache performance degradation when two threads write to different data within same cache line. |
| Use-After-Free | Accessing memory after it has been Disposed. Undefined behavior. |

---