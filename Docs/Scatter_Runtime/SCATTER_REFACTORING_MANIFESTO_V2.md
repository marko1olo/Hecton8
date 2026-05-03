# SCATTER REFACTORING MANIFESTO v2.0

Status: REFERENCE
Verification: PENDING VERIFICATION

## 2026-05-02 Current-State Boundary

- Read `Docs/Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md` and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using this manifesto as current project truth.
- This manifesto is a refactor reference, not a runtime proof or current mandate override.
- Any `Complete()` / `Dispose()` teardown examples below are superseded by `OPT_Native_Memory_Collections_JobSystem_Protocol.txt` and `AGENTS.md`: prefer owner-safe deferred disposal and do not introduce local gameplay barriers without current source proof.
- `WorldProceduralScatterDirector` remains the runtime owner; do not create a parallel scatter/flora owner.
# WorldProceduralScatterDirector.cs — Deep Architectural Refactoring
# Target: Data-Oriented Pipeline, Zero-GC Hot Paths, Burst-Compatible Jobs

---

## MISSION

Refactor WorldProceduralScatterDirector.cs (4000+ lines) from God Object monolith
into a Data-Oriented coordinator pipeline.

CONSTRAINTS (NON-NEGOTIABLE):
- Eliminate CPU spikes from Job stalls
- Absolute Zero-GC in all hot paths and tick methods
- Preserve full determinism of world generation math
- Preserve all existing serialization (no broken inspector references)

---

## SECTION 1 — ABSOLUTE RULES (VIOLATION = HARD REJECT)

### 1.1 NO NEW MonoBehaviour COMPONENTS

WorldProceduralScatterDirector remains THE ONLY scene component.
Do NOT split into independent scripts with their own Update() or SlowTick().
Director becomes a coordinator that calls services and utilities in strict sequence.
All new files are plain C# classes, static utilities, or structs — never MonoBehaviour.

### 1.2 STRICT ZERO-GC (ALL TICK AND HOT PATHS)

FORBIDDEN:
- Any LINQ: .Where() .Select() .FirstOrDefault() .Any() .Count() .ToList() .ToArray()
- Closures or lambdas in sort comparisons — use IComparable<T> on structs instead
- String concatenation or interpolation ($"") outside debug guards
- boxing of any value type in hot paths

REQUIRED:
- Classical indexed loops: for (int i = 0; i < count; i++)
- foreach ONLY on structs with struct enumerators (e.g. Dictionary<K,V>.Enumerator)
  to avoid boxing — confirm enumerator is a struct before use
- All debug strings wrapped: #if UNITY_EDITOR || DEVELOPMENT_BUILD

### 1.3 SERIALIZATION SAFETY

DO NOT touch the inspector layout.
All [SerializeField] fields (prefabs, settings, manager references) STAY in Director.
If ANY variable is renamed: add [FormerlySerializedAs("OldName")] attribute.
New services receive settings via constructor or Init() — they never own SerializeField.

### 1.4 MATH DETERMINISM — SACRED CODE

The following methods must be copied BYTE FOR BYTE. Zero logic changes.
Zero constant changes. Zero bitshift changes:
- StableRandom01()
- ComputeStableHash()
- ComputeRuleIdHash()
- ComposeKey()

These functions define world generation determinism.
Any modification breaks all existing saved worlds. NEVER TOUCH.

### 1.5 GPU INSTANCER BUFFER — NO TYPE CHANGE

When extracting FloraGPUInstancingService logic:
DO NOT change Matrix4x4[] to List<Matrix4x4> or NativeArray<Matrix4x4>.
Manual resize via Array.Copy is REQUIRED — it is a hard constraint of the GPUInstancer plugin API.
The Director owns the Matrix4x4[] buffer via SerializeField or field.
The service receives it by reference and returns it — it does not own it.

### 1.6 JOB SYSTEM CONCURRENCY SAFETY

Add bool _isSamplingJobRunning field to Director.
While job is running, SlowTick() must early-return immediately.
FORBIDDEN to read or write any NativeArray while JobHandle is not Complete().
OnDisable/OnDestroy teardown must follow current `OPT_Native_Memory_Collections_JobSystem_Protocol.txt` and `AGENTS.md`.
Do not add local `.Complete()` barriers from this manifesto without rechecking source ownership; prefer owner-safe deferred disposal when a job may still reference the collection.
Unsafe disposal while a job owns the memory still causes native memory corruption.

### 1.7 ref struct LIFETIME — HARD CONSTRAINT

ScatterRescueContext is a ref struct.
It CANNOT be stored in a class field.
It CANNOT be passed between frames.
It MUST be created strictly inside the Processing phase (ScatterState.Processing),
after the sampling job is known complete through the current owner-approved swap/completion path.
Lifetime = single method scope within the Processing tick. No exceptions.

### 1.8 Span<T> SAFETY INSIDE ref struct

All Span<T> fields inside ScatterRescueContext MUST point to:
- NativeArray<T> via NativeArray.AsSpan(), OR
- stackalloc memory

DO NOT create Span<T> over managed T[] arrays inside ref struct context.
Managed arrays can be relocated by GC. NativeArray memory is pinned. Use it.

---

## SECTION 2 — STATE MACHINE (JOB PIPELINE FIX)

### 2.1 PROBLEM

Current: ScheduleCellSamplingJob() and Complete() called sequentially in one method.
Main thread freezes waiting for workers. Job System provides zero benefit.

### 2.2 SOLUTION — TWO-TICK PIPELINE STATE MACHINE

Add to Director:

```csharp
private enum ScatterState { Idle, Sampling, Processing, Spawning }
private ScatterState _scatterState;
private JobHandle _samplingJobHandle;
private bool _isSamplingJobRunning;
private SamplingSnapshot _samplingSnapshot;
```

STATE MACHINE FLOW IN SlowTick():

```
ScatterState.Idle:
    → Populate _cellSamplingInputs NativeArray
    → Capture SamplingSnapshot (see Section 5)
    → Call Schedule() → store JobHandle
    → Set _isSamplingJobRunning = true
    → Set _scatterState = ScatterState.Sampling
    → RETURN (exit SlowTick, do not block)

ScatterState.Sampling:
    → Check: if (!_samplingJobHandle.IsCompleted) RETURN immediately
    → Call _samplingJobHandle.Complete()
    → Set _isSamplingJobRunning = false
    → Set _scatterState = ScatterState.Processing
    → FALL THROUGH to Processing in same tick (data is ready now)

ScatterState.Processing:
    → Read _cellSamplingOutputs
    → Run budget calculations using _samplingSnapshot (NOT current player position)
    → Check IsPlacementSuppressed() per candidate (see Section 8)
    → Build _desiredPlacements list
    → Create ScatterRescueContext (ref struct, stack only)
    → Run InjectRescuePlacements via context
    → Set _scatterState = ScatterState.Spawning

ScatterState.Spawning:
    → Call ScatterSpawningService.ProcessBatch()
    → If spawning complete: _scatterState = ScatterState.Idle
    → If not complete: RETURN, continue next SlowTick
```

---

## SECTION 3 — FastCandidateMap (O(1) HASH MAP)

### 3.1 PROBLEM

Current CandidateMap uses for-loop over array for key lookup.
At 512 elements: exponential CPU spike. O(N) is unacceptable in hot path.

### 3.2 IMPLEMENTATION SPEC

```csharp
internal struct FastCandidateMap
{
    private ScatterCandidate[] _values;
    private long[]             _keys;
    private bool[]             _occupied;
    private int                _capacity;     // MUST be power of two
    private int                _mask;         // = _capacity - 1
    private int                _count;

    // Init: called ONCE. capacity MUST be power of two.
    // Internal storage = capacity (caller must pass capacity * 2 for safe load factor)
    public void Init(int capacity);

    // O(1) average. Returns false if key not found.
    public bool TryGet(long key, out ScatterCandidate value);

    // O(1) average. Fail-safe on overflow: logs Warning (dev build only), returns false.
    public bool TrySet(long key, ScatterCandidate value);

    // O(1). Returns true if key exists.
    public bool Contains(long key);

    // Reset count to zero. Does NOT deallocate. Reuse same arrays.
    public void Clear();
}
```

LOAD FACTOR RULE (CRITICAL):
Linear probing degrades to O(N) above 70% fill rate due to clustering.
Rule: caller MUST pass capacity = desiredMaxElements * 2 (minimum).
This ensures max fill rate ≤ 50% under normal operation.
Document this clearly in Init() XML summary.
If count reaches capacity * 3 / 4: emit Warning (dev build only), block further inserts.

HASH FUNCTION:
```csharp
private int GetBucket(long key)
{
    // Fibonacci hashing for better distribution with sequential keys
    ulong hash = (ulong)key * 11400714819323198485UL;
    return (int)(hash >> (64 - _log2Capacity)) & _mask;
}
```
Store _log2Capacity = Mathf.RoundToInt(Mathf.Log(_capacity, 2)) in Init().

COLLISION RESOLUTION: Linear probing. Step = 1.
Probe sequence: (bucket + i) & _mask for i = 0, 1, 2, ...

COLD ALLOC RULE:
Arrays allocated ONCE in Init(). Never again.
Array.Resize is FORBIDDEN in runtime.
On overflow: emit Warning (dev only), return false. Zero allocations.

### 3.3 DELETION

If deletion is needed: use tombstone pattern.
Add `bool[] _deleted` array.
TryGet skips tombstones but continues probing.
TrySet can reuse tombstone slots.

---

## SECTION 4 — ScatterWorkingMemory

### 4.1 RESPONSIBILITY

Owns ALL collections used during scatter computation:
- NativeArrays for job input/output
- FastCandidateMap instance
- _desiredPlacements buffer
- Any temporary lists used in Processing phase

Director does NOT own these. Director holds one reference: `ScatterWorkingMemory _memory`.

### 4.2 INTERFACE CONTRACT

```csharp
internal sealed class ScatterWorkingMemory : IDisposable
{
    // NativeArrays — Burst-compatible, allocated with Allocator.Persistent
    public NativeArray<CellSamplingInput>  CellSamplingInputs;
    public NativeArray<CellSamplingOutput> CellSamplingOutputs;

    // FastCandidateMap — O(1) lookup
    public FastCandidateMap CandidateMap;

    // Desired placements buffer — pre-allocated, reused
    public ScatterPlacement[] DesiredPlacements;
    public int DesiredPlacementsCount;

    public void Init(int maxCells, int maxCandidates, int maxPlacements);

    public void Dispose()
    {
        // Current rule: dispose only after producer ownership is recovered, or use deferred Dispose(JobHandle).
        if (CellSamplingInputs.IsCreated)  CellSamplingInputs.Dispose();
        if (CellSamplingOutputs.IsCreated) CellSamplingOutputs.Dispose();
        // FastCandidateMap arrays are managed — GC handles them
    }
}
```

Director calls in OnDestroy():
```csharp
// Current rule: recover ownership through the active source-approved shutdown path.
// If a job may still reference memory, use deferred disposal instead of copying a local barrier from this manifesto.
_memory.Dispose();
```

---

## SECTION 5 — SamplingSnapshot

### 5.1 PROBLEM

Reading playerTransform.position in Processing tick (different frame than Schedule tick)
causes mathematical inconsistency and potential thread-safety issues on Transform access.

### 5.2 SOLUTION

```csharp
internal struct SamplingSnapshot
{
    public int     CenterCellX;
    public int     CenterCellZ;
    public Vector3 PlayerPosition;
    public float   CaptureTime;     // Time.time at capture — for diagnostics only
}
```

RULE: Snapshot is captured in Idle→Sampling transition, BEFORE Schedule().
ALL distance calculations and cell selection in Processing phase MUST use
_samplingSnapshot.PlayerPosition, never playerTransform.position.
This guarantees mathematical integrity of generation and frame-independence.

---

## SECTION 6 — ScatterRescueContext (ref struct)

### 6.1 SPEC

```csharp
public ref struct ScatterRescueContext
{
    public Span<ScatterPlacement> DesiredPlacements;
    public ref int                DesiredPlacementsCount;
    public Span<CellSamplingOutput> SamplingOutputs;  // from NativeArray.AsSpan()
    public int                    MaxRescuePlacements;
    public SamplingSnapshot       Snapshot;
    // Add other required data fields here — all value types or Span
}
```

LIFETIME RULE:
Created at start of Processing phase. Destroyed at end of same method.
Never stored. Never passed to another class as field.
Only passed as `ref ScatterRescueContext ctx` parameter between static methods
within the same call stack — this is valid and safe.

---

## SECTION 7 — ScatterHeuristicsUtility

### 7.1 SPEC

```csharp
public static class ScatterHeuristicsUtility
{
    // All methods: pure functions, no state, no allocations, Burst-friendly math
    public static float GetPatternHeatScale(ScatterPattern pattern, float depth);
    public static float GetDepthDomainScale(float depth, BiomeType biome);
    public static float ComputePlacementScore(in CellSamplingOutput sample, in ScatterRule rule);
    public static float ComputeHeat(in ScatterCandidate candidate, float timeSinceLastVisit);
    public static bool  EvaluatePlacementRule(in ScatterRule rule, in CellSamplingOutput sample);
    // Move ALL switch-case biome/pattern math here
}
```

RULES:
- No instance state
- No allocations
- All methods static
- All input via `in` parameter (readonly ref, zero copy for structs)
- Fully testable in isolation without Unity runtime (no UnityEngine dependencies where possible)

---

## SECTION 8 — IsPlacementSuppressed CHECK

### 8.1 RULE — CRITICAL

proceduralStateRegistry.IsPlacementSuppressed(key) accesses a managed C# object.
It CANNOT run inside a Burst Job or be scheduled on worker threads.

MANDATORY: This check runs STRICTLY in Processing phase (main thread),
BEFORE any candidate is added to _memory.DesiredPlacements.

```csharp
// Inside Processing phase loop:
for (int i = 0; i < candidateCount; i++)
{
    long key = ComposeKey(candidates[i].CellX, candidates[i].CellZ, candidates[i].RuleId);
    if (proceduralStateRegistry.IsPlacementSuppressed(key)) continue; // skip harvested
    // Only now: add to DesiredPlacements
    _memory.DesiredPlacements[_memory.DesiredPlacementsCount++] = BuildPlacement(candidates[i]);
}
```

Zero items marked as "collected" in WorldProceduralStateRegistry may appear in spawn list.

---

## SECTION 9 — ScatterSpawningService

### 9.1 STATELESS BATCH INTERFACE

ScatterSpawningService is NOT a state machine.
It does NOT track "how many spawned so far" internally.
Director owns spawn progress state explicitly.

Director fields:
```csharp
private int _spawnProgressIndex;  // how many items spawned so far this cycle
```

Service interface:
```csharp
internal static class ScatterSpawningService
{
    // Returns: number of items processed this call (may be less than full list)
    // Caller advances _spawnProgressIndex
    public static int ProcessBatch(
        ScatterPlacement[]   placements,
        int                  startIndex,
        int                  count,
        int                  maxThisFrame,       // maxInitialScatterCreatesPerRebuild
        ObjectPoolManager    pool,
        SpawnPriorityMode    priorityMode);
}
```

### 9.2 PRIORITY SYSTEM

Two-pass within single ProcessBatch call:
Pass 1: family.expectsInteraction == true (resources, quest items) — spawn first
Pass 2: decoration (flora, corals, ambient) — spawn up to remaining frame budget

If frame budget exhausted after Pass 1: return. Decorations wait for next SlowTick.
Resources are NEVER deferred. Decorations are ALWAYS deferrable.

### 9.3 FRAME BUDGET ENFORCEMENT

Respect existing constants (do NOT rename, add [FormerlySerializedAs] if moved):
- maxInitialScatterCreatesPerRebuild
- maxPoolWarmupPerRebuild

Hard cap: if ProcessBatch processes maxThisFrame items, return immediately.
Next SlowTick in Spawning state: Director passes _spawnProgressIndex as startIndex.
Continue until all placements spawned, then _scatterState = ScatterState.Idle.

---

## SECTION 10 — MULTI-FLOOR CELL SUPPORT

### 10.1 RULE

fieldSample.caveProximity and fieldSample.seafloorHeight define vertical layers.
One XZ grid cell can have MULTIPLE valid placement heights (surface + cave floor + seafloor).

FORBIDDEN logic pattern:
```csharp
// WRONG — assumes one height per cell
if (cellAlreadyHasPlacement[cellKey]) continue;
```

REQUIRED: Budget and placement logic must track per (cellKey + heightLayer) pair.
Use ComposeKey(cellX, cellZ, heightLayerIndex) as the unique placement key.
HasLayerBudget() and CanAcceptCandidate() must receive the full sample including
caveProximity and seafloorHeight to determine which vertical domain is being evaluated.

---

## SECTION 11 — FILE STRUCTURE

All new files are plain C# (no MonoBehaviour). Director stays as single scene component.

```
WorldProceduralScatterDirector.cs   ← coordinator only, owns all [SerializeField]
ScatterWorkingMemory.cs             ← IDisposable, owns NativeArrays and buffers
FastCandidateMap.cs                 ← Linear probing hash map, zero-alloc
ScatterHeuristicsUtility.cs         ← Pure static math, all biome/pattern logic
ScatterSpawningService.cs           ← Static batch spawner, stateless
ScatterDiagnosticsTracker.cs        ← ALL debug fields and methods
                                       ENTIRE FILE wrapped in:
                                       #if UNITY_EDITOR || DEVELOPMENT_BUILD
SamplingSnapshot.cs                 ← Plain struct, capture at Schedule time
ScatterRescueContext.cs             ← ref struct, Processing phase only
```

Director passes settings to services via Init() parameters or method arguments.
Services NEVER have [SerializeField]. Director is the single source of truth for config.

---

## SECTION 12 — BURST JOB CONSTRAINTS

### 12.1 WHAT RUNS IN BURST JOBS

Only data that exists in NativeArray<T> where T : struct, IComponentData (or blittable).
CellSamplingInput and CellSamplingOutput must be unmanaged structs.

```csharp
[BurstCompile]
public struct CellSamplingJob : IJobParallelFor
{
    [ReadOnly]  public NativeArray<CellSamplingInput>  Inputs;
    [WriteOnly] public NativeArray<CellSamplingOutput> Outputs;

    public void Execute(int index) { /* pure math, no managed refs */ }
}
```

### 12.2 WHAT NEVER RUNS IN BURST JOBS

- proceduralStateRegistry access (managed object)
- ObjectPoolManager access (managed object)
- Any MonoBehaviour or UnityEngine.Object access
- String operations
- Any managed array or List<T>

---

## SECTION 13 — LIFECYCLE AND SAFETY SEQUENCE

### 13.1 INIT SEQUENCE (Awake / Start)

```
1. _memory = new ScatterWorkingMemory()
2. _memory.Init(maxCells, maxCandidates * 2, maxPlacements)  // *2 for load factor
3. _candidateMap initialized inside _memory.Init()
4. _scatterState = ScatterState.Idle
5. _isSamplingJobRunning = false
```

### 13.2 DESTROY SEQUENCE (OnDestroy)

```
1. Recover native ownership through the current source-approved shutdown path.
2. Dispose immediately only when no job can reference the memory; otherwise defer disposal through JobHandle-backed ownership.
3. null out service references
```

### 13.3 OnDisable

Same as OnDestroy sequence. Must be safe to call multiple times (idempotent).
Add guard: `if (_memoryDisposed) return;` with bool flag.

---

## SECTION 14 — DIAGNOSTICS (EDITOR/DEV ONLY)

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD

internal sealed class ScatterDiagnosticsTracker
{
    // Move ALL _debug* fields here
    // Move ALL diagnostic logging here
    // Expose via methods called by Director only inside same #if guard
    public void RecordSpawnBatch(int count, ScatterState phase);
    public void RecordJobDuration(float ms);
    public void DrawGizmos(FastCandidateMap map, ScatterWorkingMemory memory);
}

#endif
```

Director holds: `#if UNITY_EDITOR || DEVELOPMENT_BUILD ScatterDiagnosticsTracker _diagnostics; #endif`

---

## SECTION 15 — WHAT IS EXPLICITLY OUT OF SCOPE

Narrative AI (Atlas-6) systems are a SEPARATE refactoring task.
Do NOT modify GetNearestUndiscoveredPOI or any NarrativeEvent code in this task.

The Scatter system exposes one read-only accessor for external systems:
```csharp
public IReadOnlyDictionary<long, ScatterPlacement> GetGridPlacements() => _gridPlacements;
```
Narrative AI uses this accessor. Do not change its signature or remove it.

---

## EXECUTION ORDER FOR CODING AGENT

Implement in this exact sequence. Complete each step before starting the next.

```
STEP 1: FastCandidateMap
  - Implement struct with Init(), TryGet(), TrySet(), Contains(), Clear()
  - Fibonacci hash, linear probing, load factor guard
  - Zero allocations after Init()
  - Unit test: insert 100 items, retrieve all, verify no collisions corrupt data

STEP 2: SamplingSnapshot + ScatterState enum
  - Plain structs, no dependencies

STEP 3: ScatterWorkingMemory
  - NativeArray fields with Allocator.Persistent
  - FastCandidateMap field
  - IDisposable with null/IsCreated guards

STEP 4: CellSamplingJob refactor
  - Verify [BurstCompile] attribute
  - Verify all fields are unmanaged
  - Verify no managed references

STEP 5: State machine in Director.SlowTick()
  - Idle → Sampling → Processing → Spawning → Idle
  - _isSamplingJobRunning guard
  - SamplingSnapshot capture before Schedule()

STEP 6: ScatterRescueContext (ref struct)
  - NativeArray.AsSpan() for all Span fields
  - Verify compiler rejects field storage attempt

STEP 7: ScatterHeuristicsUtility
  - Move all switch/math from Director
  - All methods static, all inputs via `in`

STEP 8: ScatterSpawningService
  - Static ProcessBatch()
  - Two-pass priority (interaction first, decoration second)
  - Frame budget enforcement
  - Director owns _spawnProgressIndex

STEP 9: IsPlacementSuppressed integration
  - Verify check is ONLY in Processing phase
  - Verify no suppressed item can reach DesiredPlacements

STEP 10: FloraGPUInstancingService (OPTIONAL — only if API allows clean separation)
  - If Matrix4x4[] must stay in Director: keep as private methods in Director
  - Do NOT create a fake service wrapper that just passes arrays back and forth
  - Only extract if it provides real encapsulation

STEP 11: ScatterDiagnosticsTracker
  - Move all _debug* fields
  - Wrap entire file in #if UNITY_EDITOR || DEVELOPMENT_BUILD

STEP 12: Multi-floor cell support audit
  - Verify HasLayerBudget() uses (cellKey + heightLayer) composite key
  - Verify caveProximity and seafloorHeight are passed through full pipeline

STEP 13: Final audit
  - Zero LINQ in hot paths
  - Zero string interpolation outside #if guards
  - All NativeArrays have IsCreated check before Dispose()
  - NativeArray disposal follows current NativeJobs mandate: no unsafe disposal while a job owns memory, no copied local barrier without owner proof
  - All renamed fields have [FormerlySerializedAs]
  - StableRandom01, ComputeStableHash, ComputeRuleIdHash, ComposeKey: UNCHANGED
```

---

## ANTI-PATTERNS — INSTANT REJECT LIST

```
❌ new List<T>() inside SlowTick or any tick method
❌ .Where() .Select() .FirstOrDefault() anywhere in runtime code
❌ Lambda in sort: array.Sort((a,b) => ...) — use IComparable<T>
❌ string.Format() or $"" outside #if UNITY_EDITOR || DEVELOPMENT_BUILD
❌ NativeArray access after Complete() has not been called
❌ ScatterRescueContext stored in class field
❌ Span<T> over managed T[] inside ref struct
❌ ScatterSpawningService with internal spawn-progress state
❌ FloraGPUInstancingService that just wraps Director's own array
❌ IsPlacementSuppressed() called inside Burst job
❌ playerTransform.position read in Processing phase (use Snapshot)
❌ Single-height assumption per XZ cell (breaks cave multi-floor)
❌ Any modification to StableRandom01 / ComputeStableHash / ComposeKey
❌ Array.Resize() in FastCandidateMap after Init()
❌ New [SerializeField] in any file except WorldProceduralScatterDirector.cs
``
