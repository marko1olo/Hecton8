# PROFILING PREPAREDNESS AUDIT

**Версия:** 2026-04-28 | **Статус:** ETA VERIFIED

---

## 📋 PROFILER MARKER COVERAGE

### ✅ SYSTEMS WITH PROFILER MARKERS

| System | File | Markers | Status |
|--------|------|---------|--------|
| SystemDispatcher | SystemDispatcher.cs | 12 markers | ✅ COMPLETE |
| SubmarineCoreDirector | SubmarineCoreDirector.cs | 1 marker | ✅ COMPLETE |
| SubmarineStructuralGrid | SubmarineStructuralGrid.cs | 3 markers | ✅ COMPLETE |
| HectonWorldGenerator | HectonWorldGenerator.cs | 2 markers | ✅ COMPLETE |
| ProfilerRegistry | ProfilerRegistry.cs | 6 markers | ✅ COMPLETE |
| HectonSpatialHash | HectonSpatialHash.cs | 4 markers | ✅ COMPLETE |
| ChunkLocalOffsetQuantization | ChunkLocalOffsetQuantization.cs | 2 markers | ✅ COMPLETE |
| WorldProceduralScatterDirector | WorldProceduralScatterDirector.cs | 18 markers | ✅ COMPLETE |
| ScatterRuntimeBackendFacade | ScatterRuntimeBackendFacade.cs | 2 markers | ✅ COMPLETE |

**Total Markers:** ~50+ ProfilerMarkers across codebase

---

### ❌ SYSTEMS WITHOUT PROFILER MARKERS (BLIND SPOTS)

#### CRITICAL BLIND SPOTS (Hot Path)

| System | File | Why It Matters |
|--------|------|----------------|
| **PhysicsApplySystem** | PhysicsApplySystem.cs | Force packet processing |
| **HectonFluidEngine** | HectonFluidEngine.cs | Buoyancy/wave simulation |
| **SubmarineAtmosphereSystem** | SubmarineAtmosphereSystem.cs | Air/oxygen simulation |
| **HectonPlayerMovement** | HectonPlayerMovement.cs | Player locomotion |
| **PlayerToolManager** | PlayerToolManager.cs | Tool switching |
| **EquipmentInteractionHandler** | EquipmentInteractionHandler.cs | Interaction raycasts |
| **AbyssalThermalManager** | AbyssalThermalManager.cs | Temperature simulation |
| **HectonDiscoveryManager** | HectonDiscoveryManager.cs | Scanning/progression |
| **HectonBiomeMatrixDirector** | HectonBiomeMatrixDirector.cs | Biome transitions |
| **PowerGridManager** | PowerGridManager.cs | Power distribution |

#### MEDIUM PRIORITY BLIND SPOTS

| System | File | Why It Matters |
|--------|------|----------------|
| **VoxelDeltaProcessor** | VoxelDeltaProcessor.cs | Voxel changes |
| **ContextualPhysicalIkRuntime** | ContextualPhysicalIkRuntime.cs | IK solving |
| **PhysicalHandController** | PhysicalHandController.cs | Hand physics |
| **EcosystemDirector** | EcosystemDirector.cs | AI population |
| **PredatorCognitionDomain** | PredatorCognitionDomain.cs | AI decision-making |
| **SargassumMicroFaunaBoids** | SargassumMicroFaunaBoids.cs | Boid simulation |
| **FloraInteractionManager** | FloraInteractionManager.cs | Flora interactions |
| **DebrisManager** | DebrisManager.cs | Debris bursts |

#### LOW PRIORITY BLIND SPOTS

| System | File | Why It Matters |
|--------|------|----------------|
| **SaveManager** | SaveManager.cs | Save/load (not hot path) |
| **HectonFabricatorUI** | HectonFabricatorUI.cs | UI (not hot path) |
| **PlayerPDA** | PlayerPDA.cs | UI (not hot path) |
| **MapMagicBridge** | MapMagicBridge.cs | Terrain (one-time) |

---

## 📋 RECOMMENDED MARKER INJECTION

### Priority 1: Physics & Fluid

```csharp
// PhysicsApplySystem.cs
private static readonly ProfilerMarker _forceApplyMarker = new ProfilerMarker("H8.Physics.ForceApply");

// SubmarineAtmosphereSystem.cs  
private static readonly ProfilerMarker _atmosphereTickMarker = new ProfilerMarker("H8.Atmosphere.Tick");

// HectonFluidEngine.cs
private static readonly ProfilerMarker _buoyancyMarker = new ProfilerMarker("H8.Fluid.Buoyancy");
private static readonly ProfilerMarker _waveQueryMarker = new ProfilerMarker("H8.Fluid.WaveQuery");
```

### Priority 2: Player Systems

```csharp
// HectonPlayerMovement.cs
private static readonly ProfilerMarker _movementTickMarker = new ProfilerMarker("H8.Player.Movement");

// PlayerToolManager.cs
private static readonly ProfilerMarker _toolSwitchMarker = new ProfilerMarker("H8.Player.ToolSwitch");

// EquipmentInteractionHandler.cs
private static readonly ProfilerMarker _interactionMarker = new ProfilerMarker("H8.Interaction.Handle");
```

### Priority 3: AI & World

```csharp
// EcosystemDirector.cs
private static readonly ProfilerMarker _ecosystemTickMarker = new ProfilerMarker("H8.AI.Ecosystem.Tick");

// PredatorCognitionDomain.cs
private static readonly ProfilerMarker _cognitionMarker = new ProfilerMarker("H8.AI.Cognition.Process");

// HectonBiomeMatrixDirector.cs
private static readonly ProfilerMarker _biomeMarker = new ProfilerMarker("H8.Biome.Transition");
```

---

## 📋 PROFILER MARKER NAMING CONVENTION

All markers follow pattern: `H8.{Domain}.{System}.{Action}`

| Domain | Systems |
|--------|---------|
| H8.Physics | PhysicsApplySystem, SubmarineStructuralGrid |
| H8.Fluid | HectonFluidEngine, Buoyancy |
| H8.Player | Movement, ToolManager, Inventory |
| H8.AI | EcosystemDirector, PredatorCognition, Boids |
| H8.World | Generator, Scatter, Flora |
| H8.Atmosphere | SubmarineAtmosphereSystem |
| H8.Thermal | AbyssalThermalManager |
| H8.Debris | DebrisManager |
| H8.Voxel | VoxelDeltaProcessor |
| H8.UI | (not needed) |

---

## 📋 CURRENT PROFILER MARKER USAGE

### Per-Frame Markers (Hot Path):

| Marker | Typical Duration | Status |
|--------|-----------------|--------|
| H8.Dispatcher.Update | 0.1-2 ms | ✅ OK |
| H8.Dispatcher.FixedUpdate | 0.5-4 ms | ✅ OK |
| H8.Dispatcher.SlowTick | 5-15 ms | ⚠️ HEAVY |
| H8.WorldGenerator.Tick | 0-10 ms | ✅ OK |
| H8.WorldScatter.Tick | 1-8 ms | ✅ OK |
| H8.Submarine.CoreDirector.FixedTick | 0.5-3 ms | ✅ OK |
| H8.Submarine.StructuralGrid.FixedTick | 1-5 ms | ✅ OK |

---

## 📋 BLIND SPOT RISK ASSESSMENT

| Risk Level | Systems | Action Required |
|------------|---------|-----------------|
| 🔴 CRITICAL | PhysicsApplySystem, HectonFluidEngine | Inject markers NOW |
| 🟠 HIGH | PlayerMovement, AtmosphereSystem | Inject before alpha |
| 🟡 MEDIUM | AI systems, Flora | Inject before beta |
| 🟢 LOW | UI, Save | Not required |

---

**STATUS:** ETA VERIFIED ✅

**Covered:** ~50 markers across 9 systems  
**Blind Spots:** 20+ systems need markers  
**Critical:** 2 systems (Physics, Fluid) need immediate injection