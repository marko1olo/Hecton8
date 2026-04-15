# VRAM/RT Optimization System - Architecture

## System Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    VRAMOptimizationBootstrap                                 │
│                  [RuntimeInitializeOnLoadMethod]                             │
│                                                                               │
│  Creates GameObject "__VRAMOptimizationBootstrap" with DontDestroyOnLoad    │
│  Adds components in dependency order:                                        │
│    1. VRAMMonitor (-8000)                                                    │
│    2. RenderTextureLifecycleTracker (-7999)                                  │
│    3. RenderTexturePool (-7998)                                              │
│    4. VisorRTManager (-7997)                                                 │
│    5. CameraRTManager (-7996)                                                │
│    6. PostFXRTManager (-7995)                                                │
│    7. UIRTManager (-7994)                                                    │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           GameTickManager                                    │
│                                                                               │
│  ISlowTickable registration (~0.5s interval):                                │
│    - VRAMMonitor.SlowTick()                                                  │
│    - RenderTextureLifecycleTracker.SlowTick()                                │
│    - VisorRTManager.SlowTick()                                               │
│    - CameraRTManager.SlowTick()                                              │
│    - PostFXRTManager.SlowTick()                                              │
│    - UIRTManager.SlowTick()                                                  │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                    ┌─────────────────┼─────────────────┐
                    ▼                 ▼                 ▼
        ┌───────────────────┐ ┌──────────────┐ ┌──────────────────┐
        │   VRAMMonitor     │ │ Lifecycle    │ │  RTPool          │
        │                   │ │ Tracker      │ │                  │
        │ - Profiler API    │ │              │ │ - O(1) pooling   │
        │ - Budget checks   │ │ - Allocation │ │ - Max 16/format  │
        │ - Threshold logs  │ │ - Disposal   │ │ - Scene cleanup  │
        └───────────────────┘ │ - Leak detect│ └──────────────────┘
                              │ - Category   │
                              │   queries    │
                              └──────────────┘
                                      │
                    ┌─────────────────┼─────────────────┐
                    ▼                 ▼                 ▼
        ┌───────────────────┐ ┌──────────────┐ ┌──────────────────┐
        │ VisorRTManager    │ │ CameraRT     │ │ PostFXRT         │
        │ Budget: 64 MB     │ │ Manager      │ │ Manager          │
        └───────────────────┘ │ Budget:256MB │ │ Budget: 128 MB   │
                              └──────────────┘ └──────────────────┘
                                      │
                                      ▼
                              ┌──────────────┐
                              │ UIRTManager  │
                              │ Budget: 64MB │
                              └──────────────┘
```

## Data Flow

### RT Allocation Flow

```
1. Component requests RT
   │
   ▼
2. RenderTexturePool.Rent(width, height, format, owner)
   │
   ├─ Hash calculation: width ^ (height << 16) ^ ((int)format << 24)
   │
   ├─ Pool lookup: O(1) via Dictionary<int, Queue<RenderTexture>>
   │
   ├─ Pool hit? → Return pooled RT
   │
   └─ Pool miss? → Allocate new RT
                   │
                   ▼
                   RenderTextureLifecycleTracker.RegisterAllocation(rt, owner)
                   │
                   └─ Store in Dictionary<EntityId, RenderTextureAllocationRecord>
```

### RT Disposal Flow

```
1. Component releases RT
   │
   ▼
2. RenderTextureLifecycleTracker.RegisterDisposal(rt)
   │
   └─ Mark IsDisposed = true in Dictionary
   │
   ▼
3. RenderTexturePool.Return(rt)
   │
   ├─ Hash calculation
   │
   ├─ Pool lookup
   │
   ├─ Pool full (count >= 16)? → rt.Release() immediately
   │
   └─ Pool not full? → queue.Enqueue(rt)
```

### VRAM Monitoring Flow

```
Every ~0.5s (ISlowTickable):

1. VRAMMonitor.SlowTick()
   │
   ├─ ProfilerRecorder.LastValue → TextureMemoryBytes
   │
   ├─ ProfilerRecorder.LastValue → RenderTextureMemoryBytes
   │
   ├─ Profiler.GetTotalAllocatedMemoryLong() → TotalVRAMBytes
   │
   └─ Compare against thresholds → Log warning if exceeded (throttled to once per 5s)

2. RenderTextureLifecycleTracker.SlowTick()
   │
   └─ CheckForLeaks() → Log error if owner == null && !IsDisposed && Time.time - AllocationTime > 10f

3. VisorRTManager.SlowTick()
   │
   ├─ GetAllocationsByCategory("Visor", _visorRTs)
   │
   ├─ Calculate total memory
   │
   └─ Compare against 64 MB budget → Log warning if exceeded (throttled to once per 5s)

4. CameraRTManager.SlowTick() → Same as VisorRTManager (256 MB budget)
5. PostFXRTManager.SlowTick() → Same as VisorRTManager (128 MB budget)
6. UIRTManager.SlowTick() → Same as VisorRTManager (64 MB budget)
```

## Component Dependencies

```
VRAMMonitor
├─ Unity.Profiling.ProfilerRecorder (texture/RT memory)
├─ UnityEngine.Profiling.Profiler (total VRAM)
└─ Hecton8.Core.GameTickManager (ISlowTickable)

RenderTextureLifecycleTracker
├─ Hecton8.Core.GameTickManager (ISlowTickable)
└─ UnityEngine.RenderTexture (tracking)

RenderTexturePool
├─ RenderTextureLifecycleTracker (RegisterAllocation)
└─ UnityEngine.SceneManagement.SceneManager (sceneUnloaded event)

VisorRTManager / CameraRTManager / PostFXRTManager / UIRTManager
├─ RenderTextureLifecycleTracker (GetAllocationsByCategory)
└─ Hecton8.Core.GameTickManager (ISlowTickable)

VRAMOptimizationBootstrap
├─ All 7 core components (AddComponent)
└─ UnityEngine.RuntimeInitializeOnLoadMethod (initialization)
```

## Memory Layout

### RenderTextureAllocationRecord (Struct)

```csharp
struct RenderTextureAllocationRecord
{
    RenderTexture RenderTexture;      // 8 bytes (reference)
    Component Owner;                  // 8 bytes (reference)
    int Width;                        // 4 bytes
    int Height;                       // 4 bytes
    RenderTextureFormat Format;       // 4 bytes (enum)
    float AllocationTime;             // 4 bytes
    string AllocationStackTrace;      // 8 bytes (reference)
    bool IsDisposed;                  // 1 byte
    // Total: ~41 bytes + string overhead
    
    long MemoryBytes { get; }         // Calculated property
}
```

### VRAMBudgetThresholds (Struct)

```csharp
struct VRAMBudgetThresholds
{
    long TextureMemoryBudgetBytes;      // 8 bytes (900 MB)
    long RenderTextureMemoryBudgetBytes; // 8 bytes (500 MB)
    long TotalVRAMBudgetBytes;          // 8 bytes (1.2 GB)
    // Total: 24 bytes
}
```

### Pool Memory Footprint

```
RenderTexturePool:
├─ _poolR8: Dictionary<int, Queue<RenderTexture>>[16]
│  └─ Max 16 queues × 16 RTs = 256 RT references = ~2 KB
├─ _poolRG16: Dictionary<int, Queue<RenderTexture>>[16]
│  └─ Max 16 queues × 16 RTs = 256 RT references = ~2 KB
├─ _poolRGBA16: Dictionary<int, Queue<RenderTexture>>[16]
│  └─ Max 16 queues × 16 RTs = 256 RT references = ~2 KB
└─ _poolRGBA32: Dictionary<int, Queue<RenderTexture>>[16]
   └─ Max 16 queues × 16 RTs = 256 RT references = ~2 KB

Total pool overhead: ~10 KB (excluding RT VRAM)
```

### Tracker Memory Footprint

```
RenderTextureLifecycleTracker:
├─ _allocations: Dictionary<EntityId, RenderTextureAllocationRecord>[256]
│  └─ Max 256 entries × 41 bytes = ~10 KB
├─ _leakQueryResults: List<RenderTextureAllocationRecord>[32]
│  └─ Max 32 entries × 41 bytes = ~1.3 KB
└─ _auditBuilder: StringBuilder[2048]
   └─ 2048 bytes = ~2 KB

Total tracker overhead: ~13 KB
```

## Execution Order

### Initialization (BeforeSceneLoad)

```
Frame 0:
├─ VRAMOptimizationBootstrap.Initialize() [RuntimeInitializeOnLoadMethod]
│  ├─ Create GameObject "__VRAMOptimizationBootstrap"
│  ├─ DontDestroyOnLoad(bootstrap)
│  ├─ AddComponent<VRAMMonitor>()
│  │  └─ Awake() → _instance = this, StartRecorders()
│  ├─ AddComponent<RenderTextureLifecycleTracker>()
│  │  └─ Awake() → _instance = this
│  ├─ AddComponent<RenderTexturePool>()
│  │  └─ Awake() → _instance = this
│  ├─ AddComponent<VisorRTManager>()
│  │  └─ Awake() → _instance = this
│  ├─ AddComponent<CameraRTManager>()
│  │  └─ Awake() → _instance = this
│  ├─ AddComponent<PostFXRTManager>()
│  │  └─ Awake() → _instance = this
│  └─ AddComponent<UIRTManager>()
│     └─ Awake() → _instance = this
│
└─ Log: "[VRAMOptimization] Bootstrap complete."

Frame 1:
├─ OnEnable() called on all 7 components
│  └─ Register with GameTickManager.Instance (ISlowTickable)
│
└─ System ready for RT allocation/tracking
```

### Runtime (Per Frame)

```
Frame N:
├─ GameTickManager.Tick(dt)
│  └─ No VRAM monitoring (not ITickable)
│
└─ Component RT allocation/disposal
   ├─ RenderTexturePool.Rent() → O(1) lookup
   └─ RenderTexturePool.Return() → O(1) insertion

Frame N + ~30 (every ~0.5s):
├─ GameTickManager.SlowTick()
│  ├─ VRAMMonitor.SlowTick()
│  │  ├─ MeasureVRAM() → ProfilerRecorder.LastValue
│  │  └─ CheckThresholds() → Log warning if exceeded (throttled)
│  │
│  ├─ RenderTextureLifecycleTracker.SlowTick()
│  │  └─ CheckForLeaks() → Log error if leak detected
│  │
│  ├─ VisorRTManager.SlowTick()
│  │  ├─ MeasureVisorRTMemory() → GetAllocationsByCategory("Visor")
│  │  └─ CheckBudget() → Log warning if exceeded (throttled)
│  │
│  ├─ CameraRTManager.SlowTick() → Same as VisorRTManager
│  ├─ PostFXRTManager.SlowTick() → Same as VisorRTManager
│  └─ UIRTManager.SlowTick() → Same as VisorRTManager
│
└─ Total CPU time: <0.5 ms (negligible)
```

### Scene Unload

```
SceneManager.sceneUnloaded event:
├─ RenderTexturePool.HandleSceneUnloaded(scene)
│  └─ ClearAllPools()
│     ├─ ClearPool(_poolR8) → Release all RTs, clear dictionary
│     ├─ ClearPool(_poolRG16) → Release all RTs, clear dictionary
│     ├─ ClearPool(_poolRGBA16) → Release all RTs, clear dictionary
│     └─ ClearPool(_poolRGBA32) → Release all RTs, clear dictionary
│
└─ Log: "[RTPool] Cleared all pools"
```

## Thread Safety

**All components are MAIN THREAD ONLY:**
- No async/await in hot paths
- No Job System integration
- No multi-threading

**Rationale:**
- Unity API (Profiler, RenderTexture) is main thread only
- ISlowTickable executes on main thread
- Zero-GC architecture requires deterministic execution

**Future Enhancement:**
- Job System for RMSE calculation (Resolution Analyzer)
- Async screenshot capture (Editor tools)

## Error Handling

### Null Checks

```csharp
// Singleton access
if (VRAMMonitor.Instance == null)
{
    Debug.LogWarning("[Component] VRAMMonitor not available");
    return;
}

// Component owner
if (owner == null)
{
    Debug.LogError("[LifecycleTracker] RegisterAllocation called with null owner");
    return;
}

// RenderTexture
if (rt == null)
{
    Debug.LogWarning("[RTPool] Return called with null RenderTexture");
    return;
}
```

### Profiler API Unavailable

```csharp
private void StartRecorders()
{
    _textureMemoryRecorder = ProfilerRecorder.StartNew(
        ProfilerCategory.Memory,
        "Texture Memory",
        1,
        ProfilerRecorderOptions.Default);
    
    if (!_textureMemoryRecorder.Valid)
    {
        Debug.LogWarning("[VRAMMonitor] Texture Memory recorder unavailable");
    }
}
```

### Pool Capacity Exceeded

```csharp
public void Return(RenderTexture rt)
{
    // ...
    
    if (queue.Count >= 16)
    {
        rt.Release(); // Pool full - release immediately
        return;
    }
    
    queue.Enqueue(rt); // Add to pool
}
```

### Duplicate Registration

```csharp
public void RegisterAllocation(RenderTexture rt, Component owner, string allocationStackTrace = null)
{
    // ...
    
    if (_allocations.ContainsKey(instanceID))
    {
        Debug.LogWarning($"[LifecycleTracker] Duplicate registration for RT {rt.name}. Updating existing record.");
        // Update existing record instead of adding duplicate
        return;
    }
    
    // Add new record
}
```

## Performance Optimization Techniques

### 1. ISlowTickable Pattern

**Problem:** Per-frame monitoring = 3600 calls/minute at 60 FPS  
**Solution:** ~0.5s interval = 120 calls/minute (30x reduction)

### 2. O(1) Hash-Based Pooling

**Problem:** Linear search through pool = O(n) lookup  
**Solution:** Dictionary keyed by hash = O(1) lookup

### 3. Pre-Allocated Buffers

**Problem:** StringBuilder/List allocation in hot paths = GC pressure  
**Solution:** Allocate once in Awake, reuse in SlowTick

### 4. Throttled Logging

**Problem:** Log spam = performance degradation  
**Solution:** Log once per 5s using `_nextLogTime` guard

### 5. Category-Based Queries

**Problem:** Iterate all allocations per subsystem = redundant work  
**Solution:** Single iteration, filter by category, store in pre-allocated List

### 6. Struct-Based Data Models

**Problem:** Class allocation = heap pressure  
**Solution:** Struct = stack allocation (when possible)

### 7. Explicit Enumerator

**Problem:** `foreach` on Dictionary = boxing allocation  
**Solution:** `foreach (var kvp in _allocations)` uses struct enumerator (zero-GC)

## Scalability

### Current Limits

| Resource | Limit | Rationale |
|----------|-------|-----------|
| Tracked RTs | 256 | Dictionary capacity |
| Pooled RTs per format | 16 | Queue capacity |
| Total pooled RTs | 64 | 16 × 4 formats |
| Leak query results | 32 | List capacity |
| Audit report size | 2048 chars | StringBuilder capacity |

### Scaling Strategy

**If limits exceeded:**
1. Increase Dictionary/List capacity (COLD ALLOC)
2. Monitor memory overhead (should stay < 100 KB)
3. Profile SlowTick execution time (should stay < 1 ms)

**Example:**
```csharp
// Increase tracked RT capacity from 256 to 512
private readonly Dictionary<EntityId, RenderTextureAllocationRecord> _allocations = 
    new Dictionary<EntityId, RenderTextureAllocationRecord>(512);
```

## Integration Patterns

### Pattern 1: Pooled RT with Lifecycle Tracking

```csharp
public class MyComponent : MonoBehaviour
{
    private RenderTexture _myRT;
    
    private void Start()
    {
        // Rent from pool (automatically registered with LifecycleTracker)
        _myRT = RenderTexturePool.Instance.Rent(1024, 1024, RenderTextureFormat.ARGB32, this);
    }
    
    private void OnDestroy()
    {
        if (_myRT != null)
        {
            // Register disposal
            RenderTextureLifecycleTracker.Instance.RegisterDisposal(_myRT);
            
            // Return to pool
            RenderTexturePool.Instance.Return(_myRT);
            
            _myRT = null;
        }
    }
}
```

### Pattern 2: VRAM Budget Monitoring

```csharp
public class MyPerformanceMonitor : MonoBehaviour, ISlowTickable
{
    private void OnEnable()
    {
        GameTickManager.Instance.Register((ISlowTickable)this);
    }
    
    private void OnDisable()
    {
        GameTickManager.Instance.Unregister((ISlowTickable)this);
    }
    
    public void SlowTick()
    {
        if (VRAMMonitor.Instance == null)
            return;
        
        VRAMMonitor.Instance.GetVRAMBreakdown(
            out long textureMB,
            out long renderTextureMB,
            out long totalVRAMMB
        );
        
        // Display in UI or log
        Debug.Log($"VRAM: Texture={textureMB / (1024f * 1024f):F1}MB " +
                  $"RT={renderTextureMB / (1024f * 1024f):F1}MB " +
                  $"Total={totalVRAMMB / (1024f * 1024f):F1}MB");
    }
}
```

### Pattern 3: Subsystem Budget Enforcement

```csharp
public class MySubsystemManager : MonoBehaviour, ISlowTickable
{
    private const long MyBudgetBytes = 128L * 1024L * 1024L; // 128 MB
    
    public void SlowTick()
    {
        if (RenderTextureLifecycleTracker.Instance == null)
            return;
        
        // Query subsystem RTs
        var myRTs = new List<RenderTextureAllocationRecord>(32);
        RenderTextureLifecycleTracker.Instance.GetAllocationsByCategory("MySubsystem", myRTs);
        
        // Calculate total memory
        long totalBytes = 0L;
        for (int i = 0; i < myRTs.Count; i++)
        {
            if (!myRTs[i].IsDisposed)
                totalBytes += myRTs[i].MemoryBytes;
        }
        
        // Check budget
        if (totalBytes > MyBudgetBytes)
        {
            Debug.LogWarning($"[MySubsystemManager] BUDGET EXCEEDED: " +
                           $"{totalBytes / (1024f * 1024f):F2} MB / " +
                           $"{MyBudgetBytes / (1024f * 1024f):F2} MB");
        }
    }
}
```

## Maintenance Guidelines

### Adding New Subsystem Manager

1. **Create manager class:**
   ```csharp
   [DisallowMultipleComponent]
   [DefaultExecutionOrder(-7993)] // Next available order
   public sealed class MySubsystemRTManager : MonoBehaviour, ISlowTickable
   ```

2. **Add to bootstrap:**
   ```csharp
   bootstrap.AddComponent<MySubsystemRTManager>();
   ```

3. **Define category matching:**
   ```csharp
   case "MySubsystem":
       matches = ownerName.Contains("MySubsystem") || ownerName.Contains("MyPrefix");
       break;
   ```

4. **Update documentation:**
   - Add to README.md subsystem budgets table
   - Add to ARCHITECTURE.md component dependencies
   - Add to INTEGRATION_VERIFICATION.md testing checklist

### Modifying Pool Capacity

1. **Change max capacity constant:**
   ```csharp
   private const int MaxPoolCapacity = 32; // Increased from 16
   ```

2. **Update Return() logic:**
   ```csharp
   if (queue.Count >= MaxPoolCapacity)
   {
       rt.Release();
       return;
   }
   ```

3. **Update documentation:**
   - README.md pool capacity section
   - ARCHITECTURE.md memory layout section

### Adding New RT Format

1. **Add pool dictionary:**
   ```csharp
   private readonly Dictionary<int, Queue<RenderTexture>> _poolMyFormat = 
       new Dictionary<int, Queue<RenderTexture>>(16);
   ```

2. **Update GetPoolForFormat():**
   ```csharp
   case RenderTextureFormat.MyFormat:
       return _poolMyFormat;
   ```

3. **Update TotalPooledCount:**
   ```csharp
   foreach (var kvp in _poolMyFormat)
       total += kvp.Value.Count;
   ```

4. **Update ClearAllPools():**
   ```csharp
   ClearPool(_poolMyFormat);
   ```

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2026-04-15 | Initial implementation |
| | | - 7 core components |
| | | - 2 Editor tools |
| | | - 3 Editor windows |
| | | - Zero-GC architecture |
| | | - O(1) pooling |
| | | - ISlowTickable pattern |

## Future Roadmap

### v1.1.0 (Q2 2026)
- Property-based tests (22 correctness properties)
- Integration tests (full system wiring)
- Hardware verification (NVIDIA MX350 2GB VRAM)

### v1.2.0 (Q3 2026)
- Pixel-perfect format validation
- Actual RMSE measurement for resolution optimization
- Screenshot capture for visual regression testing

### v1.3.0 (Q4 2026)
- Reflection-based owner detection
- Duplicate RT detection
- RT usage heatmap

### v2.0.0 (Q1 2027)
- Job System integration for RMSE calculation
- Async screenshot capture
- Custom RMSE thresholds per category
- Advanced leak detection (stack trace analysis)
