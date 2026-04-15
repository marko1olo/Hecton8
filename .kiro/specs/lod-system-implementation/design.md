# LOD System Implementation — Design Document

## 1. System Architecture

### 1.1 Overview

The LOD System provides automatic Level of Detail management for HECTON-8, maintaining 60 FPS @ 1080p on target hardware (NVIDIA MX350) through distance-based mesh simplification, impostor rendering, culling systems, and dynamic resolution scaling.

**Core Principle:** Zero-GC, Burst-compiled, job-based architecture integrated with existing GameTickManager and SaveManager systems.

### 1.2 Component Hierarchy

```
LODSystemManager (Singleton, ITickable, ISaveable)
├── LODGroupRegistry (LOD_Group tracking)
├── DistanceCalculationJob (Burst-compiled)
├── CullingManager (ISlowTickable)
│   ├── FrustumCullingJob (Burst-compiled)
│   ├── DistanceCullingSystem
│   └── LayerCullDistanceConfig
├── ImpostorSystem
│   ├── ImpostorBillboardPool (ObjectPoolManager integration)
│   └── AmplifyImpostorsIntegration
└── DynamicResolutionScaler (ITickable)
```

### 1.3 Data Flow

```
Frame Start
    ↓
LODSystemManager.Tick(dt)
    ↓
Schedule DistanceCalculationJob (Burst)
    ↓
Complete Job (NativeArray results)
    ↓
Apply LOD transitions (crossfade/discrete)
    ↓
CullingManager.SlowTick() [~0.5s interval]
    ↓
Distance culling + Frustum culling
    ↓
DynamicResolutionScaler.Tick(dt)
    ↓
Adjust render scale if needed
    ↓
Frame End
```

---

## 2. Core Components

### 2.1 LODSystemManager

**Responsibility:** Central coordinator for all LOD operations.

**Type:** `sealed class LODSystemManager : MonoBehaviour, ITickable, ISaveable`

**Singleton Pattern:**
```csharp
private static LODSystemManager _instance;
public static LODSystemManager Instance => _instance;
```

**Lifecycle:**
- `[DefaultExecutionOrder(-150)]` — runs before gameplay systems
- `Awake()` — singleton setup, pre-allocate collections
- `OnEnable()` — register with GameTickManager
- `OnDisable()` — unregister from GameTickManager
- `OnDestroy()` — dispose NativeArrays, cleanup

**Inspector Settings:**
```csharp
[Header("── LOD Configuration ──────────────────")]
[SerializeField, Tooltip("Quality preset (Low/Medium/High)")]
private QualityPreset _qualityPreset = QualityPreset.Medium;

[SerializeField, Tooltip("Crossfade distance threshold (meters)")]
private float _crossfadeDistanceThreshold = 50f;

[SerializeField, Tooltip("Crossfade duration (seconds)")]
private float _crossfadeDuration = 0.75f;

[Header("── Performance ──────────────────")]
[SerializeField, Tooltip("Max LOD groups to process per frame")]
private int _maxLODGroupsPerFrame = 500;

[SerializeField, Tooltip("Enable dynamic resolution scaling")]
private bool _enableDynamicResolution = true;
```

**Private State:**
```csharp
// COLD ALLOC: List<LODGroup>[500] — registered LOD groups — owner: LODSystemManager
private readonly List<LODGroup> _registeredLODGroups = new List<LODGroup>(500);

// COLD ALLOC: List<Transform>[500] — cached transforms — owner: LODSystemManager
private readonly List<Transform> _lodGroupTransforms = new List<Transform>(500);

// COLD ALLOC: NativeArray<float3>[500] — job input positions — owner: LODSystemManager
private NativeArray<Vector3> _lodGroupPositions;

// COLD ALLOC: NativeArray<float>[500] — job output distances — owner: LODSystemManager
private NativeArray<float> _lodGroupDistances;

private JobHandle _distanceJobHandle;
private bool _jobScheduled;
private bool _registered;

private Camera _mainCamera;
private Transform _cameraTransform;
```

**Public API:**
```csharp
/// <summary>
/// Register LODGroup for automatic management.
/// Called by LODGroup components during OnEnable.
/// </summary>
public void RegisterLODGroup(LODGroup lodGroup);

/// <summary>
/// Unregister LODGroup from management.
/// Called by LODGroup components during OnDisable.
/// </summary>
public void UnregisterLODGroup(LODGroup lodGroup);

/// <summary>
/// Get current LOD bias multiplier based on quality preset.
/// </summary>
public float GetLODBias();

/// <summary>
/// Set quality preset and apply LOD bias immediately.
/// </summary>
public void SetQualityPreset(QualityPreset preset);

/// <summary>
/// Get count of registered LOD groups.
/// </summary>
public int RegisteredLODGroupCount => _registeredLODGroups.Count;

/// <summary>
/// Get LOD system CPU time (milliseconds).
/// </summary>
public float LODSystemCPUTime { get; private set; }
```

**ITickable Implementation:**
```csharp
public void Tick(float dt)
{
    if (_mainCamera == null)
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null) return;
        _cameraTransform = _mainCamera.transform;
    }

    if (_registeredLODGroups.Count == 0) return;

    long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();

    // Complete previous frame's job if still running
    if (_jobScheduled)
    {
        _distanceJobHandle.Complete();
        ApplyLODTransitions();
        _jobScheduled = false;
    }

    // Schedule new distance calculation job
    ScheduleDistanceCalculationJob();

    long endTicks = System.Diagnostics.Stopwatch.GetTimestamp();
    LODSystemCPUTime = (endTicks - startTicks) / (float)System.Diagnostics.Stopwatch.Frequency * 1000f;
}
```

**ISaveable Implementation:**
```csharp
public int SavePriority => 5; // Core system
public int LoadPriority => 5;

public void PopulateSaveData(SaveData data)
{
    // Save quality preset, LOD bias, dynamic resolution enabled
}

public void LoadFromSaveData(SaveData data)
{
    // Restore settings, validate, apply defaults if invalid
}
```

---

### 2.2 DistanceCalculationJob

**Responsibility:** Burst-compiled job for calculating squared distances from camera to LOD groups.

**Type:** `struct DistanceCalculationJob : IJobParallelFor`

```csharp
[BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
private struct DistanceCalculationJob : IJobParallelFor
{
    [ReadOnly] public Vector3 CameraPosition;
    [ReadOnly] public NativeArray<Vector3> LODGroupPositions;
    [WriteOnly] public NativeArray<float> SquaredDistances;

    public void Execute(int index)
    {
        Vector3 delta = LODGroupPositions[index] - CameraPosition;
        SquaredDistances[index] = delta.x * delta.x + delta.y * delta.y + delta.z * delta.z;
    }
}
```

**Scheduling:**
```csharp
private void ScheduleDistanceCalculationJob()
{
    // Copy LOD group positions to NativeArray
    Vector3 camPos = _cameraTransform.position;
    for (int i = 0; i < _registeredLODGroups.Count; i++)
    {
        _lodGroupPositions[i] = _lodGroupTransforms[i].position;
    }

    var job = new DistanceCalculationJob
    {
        CameraPosition = camPos,
        LODGroupPositions = _lodGroupPositions,
        SquaredDistances = _lodGroupDistances
    };

    _distanceJobHandle = job.Schedule(_registeredLODGroups.Count, 64);
    _jobScheduled = true;
}
```

---

### 2.3 CullingManager

**Responsibility:** Manages frustum culling, distance culling, and layer-based cull distances.

**Type:** `sealed class CullingManager : MonoBehaviour, ISlowTickable`

**Singleton Pattern:**
```csharp
private static CullingManager _instance;
public static CullingManager Instance => _instance;
```

**Inspector Settings:**
```csharp
[Header("── Distance Culling ──────────────────")]
[SerializeField, Tooltip("Cull distance for small objects (<1m)")]
private float _smallObjectCullDistance = 30f;

[SerializeField, Tooltip("Cull distance for medium objects")]
private float _mediumObjectCullDistance = 80f;

[SerializeField, Tooltip("Cull distance for large objects")]
private float _largeObjectCullDistance = 200f;

[SerializeField, Tooltip("Hysteresis percentage (prevents thrashing)")]
private float _hysteresisPercent = 10f;

[Header("── Layer Cull Distances ──────────────────")]
[SerializeField, Tooltip("Debris layer cull distance")]
private float _debrisLayerCullDistance = 40f;

[SerializeField, Tooltip("Particles layer cull distance")]
private float _particlesLayerCullDistance = 40f;

[SerializeField, Tooltip("Props layer cull distance")]
private float _propsLayerCullDistance = 100f;

[SerializeField, Tooltip("Flora layer cull distance")]
private float _floraLayerCullDistance = 100f;
```

**Private State:**
```csharp
// COLD ALLOC: List<CullableObject>[1000] — registered cullable objects — owner: CullingManager
private readonly List<CullableObject> _cullableObjects = new List<CullableObject>(1000);

// COLD ALLOC: Plane[6] — frustum planes — owner: CullingManager
private readonly Plane[] _frustumPlanes = new Plane[6];

private Camera _mainCamera;
private bool _registered;
```

**CullableObject Struct:**
```csharp
private struct CullableObject
{
    public GameObject GameObject;
    public Transform Transform;
    public Renderer Renderer;
    public Bounds Bounds;
    public float CullDistance;
    public float ReactivateDistance; // CullDistance * (1 - hysteresis)
    public bool IsActive;
}
```

**Public API:**
```csharp
/// <summary>
/// Register object for distance culling.
/// </summary>
public void RegisterCullableObject(GameObject obj, float cullDistance);

/// <summary>
/// Unregister object from culling.
/// </summary>
public void UnregisterCullableObject(GameObject obj);

/// <summary>
/// Get count of frustum-culled objects this frame.
/// </summary>
public int FrustumCulledCount { get; private set; }

/// <summary>
/// Get count of distance-culled objects.
/// </summary>
public int DistanceCulledCount { get; private set; }
```

**ISlowTickable Implementation:**
```csharp
public void SlowTick()
{
    if (_mainCamera == null)
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null) return;
    }

    // Update frustum planes
    GeometryUtility.CalculateFrustumPlanes(_mainCamera, _frustumPlanes);

    Vector3 camPos = _mainCamera.transform.position;
    int frustumCulled = 0;
    int distanceCulled = 0;

    // Process distance culling with hysteresis
    for (int i = 0; i < _cullableObjects.Count; i++)
    {
        CullableObject obj = _cullableObjects[i];
        if (obj.GameObject == null) continue;

        float sqrDist = (obj.Transform.position - camPos).sqrMagnitude;

        if (obj.IsActive)
        {
            // Check if should deactivate
            if (sqrDist > obj.CullDistance * obj.CullDistance)
            {
                obj.GameObject.SetActive(false);
                obj.IsActive = false;
                distanceCulled++;
            }
        }
        else
        {
            // Check if should reactivate (with hysteresis)
            if (sqrDist < obj.ReactivateDistance * obj.ReactivateDistance)
            {
                obj.GameObject.SetActive(true);
                obj.IsActive = true;
            }
        }

        _cullableObjects[i] = obj;
    }

    FrustumCulledCount = frustumCulled;
    DistanceCulledCount = distanceCulled;
}
```

**Layer Cull Distance Setup:**
```csharp
private void ApplyLayerCullDistances()
{
    if (_mainCamera == null) return;

    float[] distances = new float[32];
    
    // Set layer-specific cull distances
    distances[LayerMask.NameToLayer("Debris")] = _debrisLayerCullDistance;
    distances[LayerMask.NameToLayer("Particles")] = _particlesLayerCullDistance;
    distances[LayerMask.NameToLayer("Props")] = _propsLayerCullDistance;
    distances[LayerMask.NameToLayer("Flora")] = _floraLayerCullDistance;
    
    // Large geometry uses far clip plane
    distances[LayerMask.NameToLayer("Terrain")] = _mainCamera.farClipPlane;
    
    _mainCamera.layerCullDistances = distances;
}
```

---

### 2.4 ImpostorSystem

**Responsibility:** Manages impostor billboard generation and rendering for very distant objects.

**Type:** `sealed class ImpostorSystem : MonoBehaviour`

**Integration:** Uses Amplify Impostors plugin for texture baking, ObjectPoolManager for billboard pooling.

**Inspector Settings:**
```csharp
[Header("── Impostor Configuration ──────────────────")]
[SerializeField, Tooltip("Distance threshold for impostor activation")]
private float _impostorDistanceThreshold = 150f;

[SerializeField, Tooltip("Impostor texture resolution")]
private int _impostorTextureResolution = 512;

[SerializeField, Tooltip("Crossfade duration from LOD2 to impostor")]
private float _impostorCrossfadeDuration = 1f;
```

**Private State:**
```csharp
// COLD ALLOC: Dictionary<int, GameObject>[100] — impostor billboard pool — owner: ImpostorSystem
private readonly Dictionary<int, GameObject> _impostorBillboards = new Dictionary<int, GameObject>(100);

// COLD ALLOC: List<ImpostorInstance>[100] — active impostors — owner: ImpostorSystem
private readonly List<ImpostorInstance> _activeImpostors = new List<ImpostorInstance>(100);
```

**ImpostorInstance Struct:**
```csharp
private struct ImpostorInstance
{
    public GameObject OriginalObject;
    public GameObject BillboardObject;
    public int ImpostorID;
    public float ActivationDistance;
    public bool IsActive;
}
```

**Public API:**
```csharp
/// <summary>
/// Register object for impostor rendering.
/// Bakes impostor texture if not cached.
/// </summary>
public void RegisterImpostorCandidate(GameObject obj, LODGroup lodGroup);

/// <summary>
/// Unregister object from impostor system.
/// </summary>
public void UnregisterImpostorCandidate(GameObject obj);

/// <summary>
/// Get count of active impostors.
/// </summary>
public int ActiveImpostorCount => _activeImpostors.Count;
```

---

### 2.5 DynamicResolutionScaler

**Responsibility:** Adjusts render resolution dynamically to maintain target frame rate.

**Type:** `sealed class DynamicResolutionScaler : MonoBehaviour, ITickable`

**Inspector Settings:**
```csharp
[Header("── Dynamic Resolution ──────────────────")]
[SerializeField, Tooltip("Target frame time (milliseconds)")]
private float _targetFrameTime = 16.67f; // 60 FPS

[SerializeField, Tooltip("Min render scale")]
private float _minRenderScale = 0.5f;

[SerializeField, Tooltip("Max render scale")]
private float _maxRenderScale = 1.0f;

[SerializeField, Tooltip("Scale adjustment speed")]
private float _scaleAdjustmentSpeed = 0.5f;
```

**Private State:**
```csharp
private float _currentRenderScale = 1.0f;
private int _consecutiveSlowFrames = 0;
private int _consecutiveFastFrames = 0;
private bool _registered;
```

**Public API:**
```csharp
/// <summary>
/// Get current render scale.
/// </summary>
public float CurrentRenderScale => _currentRenderScale;

/// <summary>
/// Enable/disable dynamic resolution scaling.
/// </summary>
public void SetEnabled(bool enabled);
```

**ITickable Implementation:**
```csharp
public void Tick(float dt)
{
    float frameTime = dt * 1000f; // Convert to milliseconds

    // Check if frame time exceeds target
    if (frameTime > _targetFrameTime)
    {
        _consecutiveSlowFrames++;
        _consecutiveFastFrames = 0;

        // Reduce scale after 3 consecutive slow frames
        if (_consecutiveSlowFrames >= 3)
        {
            float targetScale = _currentRenderScale * 0.95f; // 5% reduction
            _currentRenderScale = Mathf.Max(targetScale, _minRenderScale);
            ApplyRenderScale();
            _consecutiveSlowFrames = 0;
        }
    }
    else if (frameTime < _targetFrameTime * 0.9f) // 10% margin
    {
        _consecutiveFastFrames++;
        _consecutiveSlowFrames = 0;

        // Increase scale after 30 consecutive fast frames
        if (_consecutiveFastFrames >= 30)
        {
            float targetScale = _currentRenderScale * 1.02f; // 2% increase
            _currentRenderScale = Mathf.Min(targetScale, _maxRenderScale);
            ApplyRenderScale();
            _consecutiveFastFrames = 0;
        }
    }
}

private void ApplyRenderScale()
{
    // Apply to URP render scale
    UniversalRenderPipeline.asset.renderScale = _currentRenderScale;
}
```

---

## 3. Integration Points

### 3.1 GameTickManager Integration

**LODSystemManager:**
- Implements `ITickable`
- Registers in `OnEnable()`, unregisters in `OnDisable()`
- Uses `dt` parameter from `Tick(float dt)`, not `Time.deltaTime`

**CullingManager:**
- Implements `ISlowTickable`
- Registers in `OnEnable()`, unregisters in `OnDisable()`
- Runs approximately every 0.5 seconds

**DynamicResolutionScaler:**
- Implements `ITickable`
- Registers in `OnEnable()`, unregisters in `OnDisable()`

### 3.2 SaveManager Integration

**LODSystemManager:**
- Implements `ISaveable`
- `SavePriority = 5` (Core system)
- `LoadPriority = 5`
- Saves: quality preset, LOD bias, dynamic resolution enabled
- Loads: validates values, applies defaults if invalid

### 3.3 ObjectPoolManager Integration

**ImpostorSystem:**
- Uses `ObjectPoolManager.Instance.Spawn()` for billboard instances
- Uses `ObjectPoolManager.Instance.Despawn()` when deactivating impostors
- Billboard prefabs implement `IPoolable`

### 3.4 Scene Initialization

**Bootstrap Sequence:**
1. `LODSystemManager.Awake()` — singleton setup, pre-allocate NativeArrays
2. `CullingManager.Awake()` — singleton setup, apply layer cull distances
3. `ImpostorSystem.Awake()` — singleton setup, load impostor cache
4. `DynamicResolutionScaler.Awake()` — singleton setup
5. Scene loads → LODGroups register themselves via `RegisterLODGroup()`

---

## 4. Performance Characteristics

### 4.1 CPU Budget

| Component | Budget (ms/frame) | Notes |
|-----------|-------------------|-------|
| LODSystemManager.Tick | ≤ 1.0 ms | Distance job scheduling + completion |
| DistanceCalculationJob | ≤ 1.0 ms | Burst-compiled, parallel |
| CullingManager.SlowTick | ≤ 0.5 ms | Runs every ~0.5s, not per-frame |
| DynamicResolutionScaler.Tick | ≤ 0.1 ms | Simple frame time monitoring |
| **Total** | **≤ 2.0 ms/frame** | Within 12ms main thread budget |

### 4.2 Memory Footprint

| Component | Memory | Notes |
|-----------|--------|-------|
| LODSystemManager | ~40 KB | 500 LODGroups × 80 bytes |
| CullingManager | ~30 KB | 1000 CullableObjects × 30 bytes |
| ImpostorSystem | ~10 KB | 100 ImpostorInstances × 100 bytes |
| NativeArrays | ~8 KB | 500 × (12 + 4) bytes |
| **Total** | **~88 KB** | Negligible overhead |

### 4.3 GC Allocation

**Target:** 0 bytes/frame in hot paths

**Guarantees:**
- No LINQ operations
- No string operations in Tick/SlowTick
- No `new` allocations in hot paths
- Pre-allocated collections with capacity
- Struct-based data (CullableObject, ImpostorInstance)
- NativeArray for job data (Allocator.Persistent)

---

## 5. Quality Presets

### 5.1 Preset Definitions

```csharp
public enum QualityPreset
{
    Low,    // LOD Bias = 1.5 (aggressive culling)
    Medium, // LOD Bias = 1.0 (balanced)
    High    // LOD Bias = 0.7 (quality focus)
}
```

### 5.2 Preset Effects

| Setting | Low | Medium | High |
|---------|-----|--------|------|
| LOD Bias | 1.5 | 1.0 | 0.7 |
| Min Render Scale | 0.7 | 0.5 | 0.5 |
| Crossfade Distance | 30m | 50m | 70m |
| Impostor Threshold | 100m | 150m | 200m |

---

## 6. Editor Tools

### 6.1 LOD Validation Window

**Menu:** `Hecton8/LOD System/Validate LOD Groups`

**Features:**
- Scan all prefabs for LODGroup components
- Report missing LOD levels (LOD0+LOD1+Cull minimum)
- Report incorrect polygon count ratios (LOD1 ≤ 50% LOD0, LOD2 ≤ 25%)
- Report assets visible beyond 20m without LOD groups
- Export validation report to CSV

### 6.2 LOD Statistics Window

**Menu:** `Hecton8/LOD System/LOD Statistics`

**Features:**
- Real-time LOD system performance metrics
- Registered LOD group count
- Active impostor count
- Frustum/distance culled object counts
- Current render scale
- LOD system CPU time graph

### 6.3 LOD Gizmos

**Scene View Visualization:**
- LOD transition distance spheres (color-coded by level)
- Current LOD level label per object
- Cull distance visualization
- Impostor activation threshold

---

## 7. Testing Strategy

### 7.1 Unit Tests

**Coverage Target:** 80%

**Test Cases:**
- LODSystemManager registration/unregistration
- Distance calculation accuracy
- LOD bias application
- Quality preset switching
- Save/load persistence
- NativeArray disposal

### 7.2 Integration Tests

**Test Scenarios:**
- 1000+ LODGroups in scene
- Rapid camera movement (LOD thrashing prevention)
- Scene load/unload (cleanup verification)
- Save/load cycle (settings persistence)
- Dynamic resolution scaling under load

### 7.3 Performance Tests

**Benchmarks:**
- LOD system CPU time < 2ms/frame (10,000 LODGroups)
- Zero GC allocations during gameplay
- 60 FPS maintained @ 1080p on MX350
- SetPass calls ≤ 600, Batches ≤ 1800

---

## 8. Risks & Mitigation

### 8.1 Technical Risks

| Risk | Mitigation |
|------|------------|
| Job system overhead exceeds budget | Profile early, optimize batch sizes, use Burst |
| Crossfade transitions cause frame drops | Limit concurrent crossfades, use distance threshold |
| Impostor generation memory spikes | Generate offline, cache in Addressables |
| LOD thrashing at boundaries | Implement hysteresis, smooth distance calculations |

### 8.2 Integration Risks

| Risk | Mitigation |
|------|------------|
| Amplify Impostors compatibility | Version lock plugin, test thoroughly |
| NativeArray disposal order | Strict lifecycle: Complete() → Dispose() |
| Singleton initialization order | Use `[DefaultExecutionOrder]` attributes |

---

**Document Version:** 1.0  
**Last Updated:** 2025-04-15  
**Status:** READY FOR IMPLEMENTATION  
**Next Phase:** Tasks Document Creation
