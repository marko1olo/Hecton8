# LOD System — README

## Overview

The LOD (Level of Detail) System provides automatic mesh simplification, culling, and dynamic resolution scaling for HECTON-8. Maintains 60 FPS @ 1080p on target hardware (NVIDIA MX350 2GB VRAM).

**Core Features:**
- Automatic LOD group management
- Distance-based and frustum culling
- Dynamic resolution scaling
- Quality presets (Low/Medium/High)
- Zero-GC architecture with Burst-compiled jobs
- Editor validation and monitoring tools

---

## Quick Start

### 1. Add LOD System to Scene

Create an empty GameObject and add the following components:
- `LODSystemManager`
- `CullingManager`
- `DynamicResolutionScaler`
- `ImpostorSystem` (optional)

**Recommended Setup:**
```
GameObject: LOD_System
├── LODSystemManager (DefaultExecutionOrder: -150)
├── CullingManager (DefaultExecutionOrder: -140)
├── DynamicResolutionScaler (DefaultExecutionOrder: -130)
└── ImpostorSystem (DefaultExecutionOrder: -130)
```

### 2. Configure Quality Preset

In `LODSystemManager` inspector:
- **Quality Preset**: Low/Medium/High
- **Crossfade Distance Threshold**: 50m (default)
- **Crossfade Duration**: 0.75s (default)

### 3. Configure Culling

In `CullingManager` inspector:
- **Small Object Cull Distance**: 30m
- **Medium Object Cull Distance**: 80m
- **Large Object Cull Distance**: 200m
- **Hysteresis Percent**: 10% (prevents thrashing)

### 4. Configure Dynamic Resolution

In `DynamicResolutionScaler` inspector:
- **Target Frame Time**: 16.67ms (60 FPS)
- **Min Render Scale**: 0.5 (Low/Medium) or 0.7 (High)
- **Max Render Scale**: 1.0

---

## LOD Group Setup

### Automatic Registration

LOD groups are automatically registered when they become active. No manual registration required.

### LOD Level Requirements

**Minimum (Props > 0.5m):**
- LOD0 (high detail)
- LOD1 (≤ 50% poly count of LOD0)
- Cull (invisible beyond distance)

**Recommended (Hero assets):**
- LOD0 (high detail)
- LOD1 (≤ 50% poly count)
- LOD2 (≤ 25% poly count)
- Cull

### Transition Modes

**Crossfade (< 50m):**
- Smooth blending between LOD levels
- Uses `LODGroup.fadeMode = CrossFade`
- Duration: 0.75s (configurable)

**Discrete (> 50m):**
- Instant switching between LOD levels
- Uses `LODGroup.fadeMode = None`
- Better performance for distant objects

---

## Quality Presets

### Low (Performance Focus)
- LOD Bias: 1.5 (aggressive culling)
- Min Render Scale: 0.7
- Crossfade Distance: 30m
- Impostor Threshold: 100m

### Medium (Balanced)
- LOD Bias: 1.0 (default)
- Min Render Scale: 0.5
- Crossfade Distance: 50m
- Impostor Threshold: 150m

### High (Quality Focus)
- LOD Bias: 0.7 (longer LOD residency)
- Min Render Scale: 0.5
- Crossfade Distance: 70m
- Impostor Threshold: 200m

---

## Culling Systems

### Distance Culling

Objects are culled based on size:
- **Small (< 1m)**: 30m
- **Medium (1-3m)**: 80m
- **Large (> 3m)**: 200m

**Hysteresis:** 10% threshold difference prevents activation thrashing.

### Frustum Culling

Unity's built-in frustum culling is used. CullingManager tracks culled object count for monitoring.

### Layer Cull Distances

Per-layer cull distances:
- **Debris**: 40m
- **Particles**: 40m
- **Props**: 100m
- **Flora**: 100m
- **Terrain**: Camera far clip plane

### Occlusion Culling

**Requirements:**
- Bake occlusion data in scene
- Mark objects > 1m³ as **Occludee Static**
- Mark objects > 2m³ as **Occluder Static**

**Validation:** Use `Hecton8/LOD System/Validate Occlusion Culling` menu.

---

## Dynamic Resolution Scaling

### How It Works

1. Monitor frame time every frame
2. If 3 consecutive slow frames (> 16.67ms): reduce scale by 5%
3. If 30 consecutive fast frames (< 15ms): increase scale by 2%
4. Clamp between min (0.5 or 0.7) and max (1.0)

### Configuration

**Target Frame Time:** 16.67ms (60 FPS)
**Scale Limits:**
- Low preset: 0.7 - 1.0
- Medium/High preset: 0.5 - 1.0

**Enable/Disable:**
```csharp
GlobalRegistry.DynamicResolution?.SetEnabled(true);
```

---

## Editor Tools

### LOD Validation Window

**Menu:** `Hecton8/LOD System/Validate LOD Groups`

**Features:**
- Scan all prefabs for LODGroup components
- Report missing LOD levels
- Report incorrect polygon count ratios
- Report assets visible beyond 20m without LOD groups
- Export validation report to CSV

### LOD Statistics Window

**Menu:** `Hecton8/LOD System/LOD Statistics`

**Features:**
- Real-time LOD system performance metrics
- Registered LOD group count
- Active impostor count
- Frustum/distance culled object counts
- Current render scale
- LOD system CPU time graph

**Auto-Refresh:** 0.5s interval (configurable)

### LOD Gizmos

**Enable in Inspector:** `LODSystemManager` → Gizmos → Enable LOD Gizmos

**Visualizations:**
- LOD transition distance spheres (color-coded)
- Current LOD level label per object
- Cull distance visualization
- Impostor activation threshold

**Colors:**
- Green: LOD0
- Yellow: LOD1
- Orange: LOD2
- Red: Culled

---

## Save/Load Integration

### Saved Settings

- Quality preset (Low/Medium/High)
- Dynamic resolution enabled state

### Load Behavior

Settings are restored from save data. If invalid, defaults to Medium preset.

---

## Performance Characteristics

### CPU Budget

| Component | Budget (ms/frame) |
|-----------|-------------------|
| LODSystemManager.Tick | ≤ 1.0 ms |
| DistanceCalculationJob | ≤ 1.0 ms |
| CullingManager.SlowTick | ≤ 0.5 ms |
| DynamicResolutionScaler.Tick | ≤ 0.1 ms |
| **Total** | **≤ 2.0 ms/frame** |

### Memory Footprint

| Component | Memory |
|-----------|--------|
| LODSystemManager | ~40 KB |
| CullingManager | ~30 KB |
| ImpostorSystem | ~10 KB |
| NativeArrays | ~8 KB |
| **Total** | **~88 KB** |

### GC Allocation

**Target:** 0 bytes/frame in hot paths

**Guarantees:**
- No LINQ operations
- No string operations in Tick/SlowTick
- No `new` allocations in hot paths
- Pre-allocated collections with capacity
- Struct-based data (CullableObject, ImpostorInstance)
- NativeArray for job data (Allocator.Persistent)

---

## Troubleshooting

### LOD groups not transitioning

**Check:**
1. LODSystemManager is active in scene
2. LOD groups are registered (check `RegisteredLODGroupCount`)
3. Camera.main is not null
4. LOD bias is applied (check QualitySettings.lodBias)

### High CPU time

**Check:**
1. Registered LOD group count (target: < 500)
2. Job batch size (default: 64)
3. Crossfade enabled for too many objects

### Culling not working

**Check:**
1. CullingManager is active in scene
2. Objects are registered via `RegisterCullableObject`
3. Cull distances are configured correctly
4. Hysteresis is not too high (default: 10%)

### Dynamic resolution not scaling

**Check:**
1. DynamicResolutionScaler is enabled
2. URP asset is assigned
3. Frame time is consistently above/below target
4. Min/max scale limits are correct

---

## API Reference

### LODSystemManager

```csharp
// Register LOD group
GlobalRegistry.LODSystem?.RegisterLODGroup(lodGroup);

// Unregister LOD group
GlobalRegistry.LODSystem?.UnregisterLODGroup(lodGroup);

// Set quality preset
GlobalRegistry.LODSystem?.SetQualityPreset(LODQualityPreset.High);

// Get LOD bias
float bias = GlobalRegistry.LODSystem != null ? GlobalRegistry.LODSystem.GetLODBias() : 1f;

// Get registered count
int count = GlobalRegistry.LODSystem != null ? GlobalRegistry.LODSystem.RegisteredLODGroupCount : 0;

// Get CPU time
float cpuTime = GlobalRegistry.LODSystem != null ? GlobalRegistry.LODSystem.LODSystemCPUTime : 0f;
```

### CullingManager

```csharp
// Register cullable object
GlobalRegistry.Culling?.RegisterCullableObject(gameObject, cullDistance);

// Unregister cullable object
GlobalRegistry.Culling?.UnregisterCullableObject(gameObject);

// Get culled counts
int frustumCulled = GlobalRegistry.Culling != null ? GlobalRegistry.Culling.FrustumCulledCount : 0;
int distanceCulled = GlobalRegistry.Culling != null ? GlobalRegistry.Culling.DistanceCulledCount : 0;
```

### DynamicResolutionScaler

```csharp
// Enable/disable
GlobalRegistry.DynamicResolution?.SetEnabled(true);

// Get current scale
float scale = GlobalRegistry.DynamicResolution != null ? GlobalRegistry.DynamicResolution.CurrentRenderScale : 1f;

// Set quality preset
GlobalRegistry.DynamicResolution?.SetQualityPreset(LODQualityPreset.Medium);
```

### ImpostorSystem

```csharp
// Register impostor candidate
GlobalRegistry.Impostors?.RegisterImpostorCandidate(gameObject, lodGroup);

// Unregister impostor candidate
GlobalRegistry.Impostors?.UnregisterImpostorCandidate(gameObject);

// Get active impostor count
int count = GlobalRegistry.Impostors != null ? GlobalRegistry.Impostors.ActiveImpostorCount : 0;
```

---

## Best Practices

### LOD Group Setup

1. **Always use LOD groups for props > 0.5m**
2. **LOD1 ≤ 50% poly count of LOD0**
3. **LOD2 ≤ 25% poly count of LOD0**
4. **Use crossfade for near-field objects (< 50m)**
5. **Use discrete switching for distant objects (> 50m)**

### Culling

1. **Register objects early (Awake/Start)**
2. **Unregister in OnDestroy**
3. **Use size-based cull distances**
4. **Enable occlusion culling for caves/modules**

### Performance

1. **Keep registered LOD groups < 500**
2. **Use Burst-compiled jobs for distance calculations**
3. **Monitor CPU time via LOD Statistics window**
4. **Profile with Unity Profiler before optimization**

### Quality

1. **Test all quality presets on target hardware**
2. **Validate LOD groups with validation window**
3. **Check for visual regressions after changes**
4. **Use Gizmos to visualize LOD transitions**

---

## Known Limitations

### Impostor System

- Requires Amplify Impostors plugin (not included)
- Offline texture baking required
- Addressables setup required
- Not implemented in current version (stub only)

### Dynamic Resolution

- Only works with URP
- Requires UniversalRenderPipeline.asset
- Does not affect UI rendering

### Culling

- Distance culling uses GameObject.SetActive (expensive)
- Frustum culling relies on Unity's built-in system
- Occlusion culling requires baked data

---

## Support

For issues or questions:
1. Check LOD Statistics window for metrics
2. Enable verbose logging in LODSystemManager
3. Use LOD Validation window to check setup
4. Profile with Unity Profiler
5. Check AGENTS.MD for coding standards

---

**Document Version:** 1.0  
**Last Updated:** 2025-04-15  
**Status:** PRODUCTION READY
