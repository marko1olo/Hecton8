# HECTON-8 — TOTAL ARCHITECTURAL RECONNAISSANCE & SYSTEMS VULNERABILITY REPORT
**STATUS**: FINAL TECHNICAL AUDIT COMPLETE
**DOCUMENT ID**: DEEP_RECON_REPORT.md
**CLASSIFICATION**: READ-ONLY DEEP SCAN
**OS TARGET**: WINDOWS (COPPER WIRE PROOF LADDER)
**PRODUCT LINE**: HECTON-8 (NASA-PUNK / DEEP SEA NOIR)

---

## SECTION 1: [THE BONES: API CONTRACTS] — DEVELOPER ATTACHMENT MANUAL

This manual provides a low-level guide and reference implementation for attaching standalone, pure C# logic (the "Meat") to the performance-critical subsystems of **HECTON-8** (the "Bones") without introducing garbage collection overhead (Zero-GC).

All gameplay logic, simulation, and data queries must be decoupled from the Unity Engine thread dependencies and run in worker Burst threads or aligned dispatcher tick lanes.

```
+───────────────────────────+
│   Standalone C# System    │
+─────────────┬─────────────+
              │
              │ 1. Memory Query
              ▼
+───────────────────────────+
│   GlobalDataVault Heap    │ <─── [Immutable Static Monolith Arena (.h8bin)]
│ (NativeArray Read/Write)  │
+─────────────┬─────────────+
              │
              │ 2. Signal Processing
              ▼
+───────────────────────────+
│       SignalBus<T>        │ <─── [GC-Free Frame Snapshot Queue]
+─────────────┬─────────────+
              │
              │ 3. Phase Aligned Execution
              ▼
+───────────────────────────+
│     SystemDispatcher      │ <─── [ITickable / ISlowTickable Lanes]
+───────────────────────────+
```

---

### 1.1 Memory Allocation & Handle Resolution (`GlobalDataVault` & `H8Memory`)
HECTON-8 utilizes a flat, unmanaged native memory heap called `GlobalDataVault`. To retrieve or allocate gameplay state, systems obtain a generational handle `VaultGenerationHandle<T>` mapping to a static `BufferID` and read/write the memory block.

#### Generational Handle Resolution API Example
If an external C# function wants to read or write the player's survival vital metrics (such as health, oxygen, or nitrogen pressure) from the vault, it must resolve the handle and access the layout:

```csharp
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using System.Runtime.InteropServices;

namespace Hecton8.Gameplay.Survival
{
    // 1. Define the unmanaged DTO layout. No references allowed.
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MetabolicStateDTO
    {
        [FieldOffset(0)] public float Health;
        [FieldOffset(4)] public float Oxygen01;
        [FieldOffset(8)] public float NitrogenSaturation;
        [FieldOffset(12)] public float CoreTemperature;
        [FieldOffset(16)] public uint EntityHash;
        [FieldOffset(20)] public uint SystemFlags;
    }

    public sealed class ExternalSurvivalObserver : ISlowTickable
    {
        private IDataVault _vault;
        private VaultGenerationHandle<MetabolicStateDTO> _metabolicHandle;
        private bool _isRegistered;

        // Custom Buffer ID mapped to metabolic storage in H8Memory contracts
        private const BufferID MetabolicStateBufferId = (BufferID)1044; 

        public void Initialize()
        {
            _vault = GlobalRegistry.DataVault;
            if (_vault == null) return;

            // Resolve or allocate a zeroed native buffer in the vault
            _metabolicHandle = _vault.EnsureGenerationHandle<MetabolicStateDTO>(
                MetabolicStateBufferId,
                1, // Player capacity
                SystemID.GameplaySurvival,
                NativeArrayOptions.ClearMemory
            );

            // Register with SystemDispatcher (Environment/Survival tick priority)
            _isRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
        }

        public void Shutdown()
        {
            if (_isRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
                _isRegistered = false;
            }
            _vault = null;
        }

        // Executed at 10Hz slow tick cadence (Zero-GC)
        public void SlowTick(float dt)
        {
            if (_vault == null || !_metabolicHandle.IsCreated) return;

            // Safe Read-Only resolution
            if (_vault.TryReadOnlyHandle(in _metabolicHandle, out NativeArray<MetabolicStateDTO>.ReadOnly states))
            {
                if (states.Length > 0)
                {
                    MetabolicStateDTO vitals = states[0];
                    float health = vitals.Health;
                    float o2 = vitals.Oxygen01;
                    
                    // Run pure C# analysis code here...
                }
            }
        }
    }
}
```

---

### 1.2 Inter-System Communication (`SignalBus<T>` & `NativeQueue`)
To publish events across boundaries without allocating memory or utilizing standard C# events, systems must push signals directly to the static `SignalBus<T>` corridor.

#### Signal Definition & Publishing
Signals must be unmanaged structs. A drop counter accounts for overflow if the ring buffer capacity is exceeded:

```csharp
[StructLayout(LayoutKind.Explicit, Size = 16)]
public struct PressureHazardSignal : ISignal
{
    public const uint LaneHash = 0x50524553u; // 'PRES'
    [FieldOffset(0)] public uint FrameId;
    [FieldOffset(4)] public float WaterPressureBar;
    [FieldOffset(8)] public uint DamageReceiverId;
}

// Publishing a signal in hot-path loops (Zero GC)
private static uint s_droppedPressureSignalsCount = 0;

public void BroadcastHazard(float pressure, uint receiverId)
{
    PressureHazardSignal hazard = new PressureHazardSignal
    {
        FrameId = TimeSliceScheduler.CurrentFrameId,
        WaterPressureBar = pressure,
        DamageReceiverId = receiverId
    };

    SignalBus<PressureHazardSignal>.TryPushTracked(in hazard, ref s_droppedPressureSignalsCount);
}
```

#### Signal Subscription & Consumption
Signals are consumed as a flat `ReadOnlySpan<T>` representing the frame's snapshot:

```csharp
public void UpdateSignalConsumption()
{
    // Retrieve frame snapshot span without allocation
    ReadOnlySpan<PressureHazardSignal> hazards = SignalBus<PressureHazardSignal>.GetFrameSnapshot();
    
    for (int i = 0; i < hazards.Length; i++)
    {
        ref readonly PressureHazardSignal hazard = ref hazards[i];
        if (hazard.WaterPressureBar > 12.0f)
        {
            // Execute alert logic
        }
    }
}
```

---

### 1.3 System Dispatcher Registration (`SystemDispatcher`)
Gameplay loops must not execute inside MonoBehaviour `Update()` or `FixedUpdate()`. Systems register to phase-aligned execution buckets using `GlobalRegistry`.

#### Aligned Priority Dispatcher Example
```csharp
public sealed class DynamicEcosystemController : ISlowTickable, ILateFrameTickable
{
    private bool _slowTickRegistered;
    private bool _lateFrameRegistered;

    public void RegisterLanes()
    {
        // 10Hz tick lane (SIMULATION phase alignment)
        _slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Ecosystem);
        
        // Late frame render updates (PRESENTATION phase alignment)
        _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.EfficacyPresentation);
    }

    public void UnregisterLanes()
    {
        if (_slowTickRegistered)
            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Ecosystem);
            
        if (_lateFrameRegistered)
            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.EfficacyPresentation);
    }

    public void SlowTick(float dt)
    {
        // Pure C# simulation logic
    }

    public void LateFrameTick(float dt)
    {
        // Presentation logic
    }
}
```

---

## SECTION 2: [DATA MONOLITH ANALYSIS] — `.h8bin` CONCURRENCY & THREAD-SAFETY

The Data Monolith (`static_data.h8bin`) serves as the read-only single source of truth for all game definitions (recipes, localization keys, biological records, and item data).

### 2.1 File Access and Load Flow
1. **Low-Level IO Loader**:
   `H8StaticDataArena.cs` initializes the data arena during cold boot using one of three loaders:
   - **`MemoryMappedFile`**: Default on standalone builds for rapid, zero-copy, operating-system-level page sharing.
   - **`Native P/Invoke`**: Uses `CreateFileW` and `ReadFile` directly on Windows platforms to bypass standard C# file wrapper overhead.
   - **`FileStreamFallback`**: Default fallback on non-supported platforms.

2. **The Resident Vault Buffer**:
   Once the file is loaded, the entire byte payload is locked in a resident native buffer allocated in the vault:
   `_arenaHandle = vault.EnsureGenerationHandle<byte>(DataMonolithResidentBufferId, fileBytes, ...)`

```
[static_data.h8bin] 
     │
     ├── mmaps / Windows ReadFile P/Invoke
     ▼
[GlobalDataVault: NativeArray<byte>] (Resident Buffer)
     │
     ├── locked during initialization (IsWriteLocked = true)
     ▼
[NativeArray<byte>.ReadOnly] (Unmanaged Blob View)
     ├── Passed directly to Burst Jobs (H8CreatureSoAReconstructJob)
     └── Concurrent, Lock-Free reads across all Worker Threads (No GC, No Race)
```

---

### 2.2 Concurrency & Thread-Safety Audit
- **Write Locking**: The static class `H8StaticDataArena` strictly enforces a write lock:
  ```csharp
  public static bool IsWriteLocked => _readyLocked;
  ```
  Once loaded, `IsWriteLocked` is flagged as `true`. Any subsequent attempts to write or re-initialize the static arena fail with `H8DataBlobLoadStatus.ReadyLocked`.
- **Parallel Read Safety**:
  Because the resident buffer is immutable after initialization, read access via `vault.TryReadOnlyHandle` returns a `NativeArray<byte>.ReadOnly` struct wrapper.
  This wrapper is fully thread-safe and can be passed directly into multiple Burst-compiled jobs running concurrently on background threads.

#### Example of Concurrent Burst Reader Job
```csharp
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public unsafe struct H8CreatureSoAReconstructJob : IJobParallelFor
{
    // The data monolith read-only buffer can be read from parallel worker threads simultaneously
    [ReadOnly, NoAlias] public NativeArray<byte> Blob;
    public int CreatureSectionOffsetBytes;

    [WriteOnly, NoAlias] public NativeArray<float> Aggressions;

    public void Execute(int index)
    {
        byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Blob);
        byte* recordPtr = basePtr + CreatureSectionOffsetBytes + (index * H8DataLayoutConstants.CreatureTraitRecordSize);
        H8CreatureTraitRecord record = UnsafeUtility.ReadArrayElement<H8CreatureTraitRecord>(recordPtr, 0);

        Aggressions[index] = record.Genome.Aggression;
    }
}
```

---

## SECTION 3: [THE WEAKEST MEAT] — SYSTEM VULNERABILITY RANKING

Below is the technical audit and ranking of the top 5 most underdeveloped, inefficient, or legacy-coupled gameplay systems currently sitting in the codebase.

### 1. `CachedTriggerVolume.cs` (Spatial Senses)
* **Status**: Critical Bottleneck (Main Thread Collision)
* **Description**: A wrapper script that listens to Unity's `OnTriggerEnter` and `OnTriggerExit` callbacks on MonoBehaviours. It manages internal lists of tracked GameObjects on the main thread and performs linear lookups.
* **Why it's bad**: 
  - Triggers garbage collection overhead when lists expand.
  - Relying on Unity's physics trigger callbacks limits spatial checking to the main thread.
  - Scale limits are hit when hundreds of active creatures enter sensor ranges.
* **Refactoring Strategy**: 
  Extract the trigger queries into a Burst-compiled job that processes a flat array of positions using `RaycastCommand` or `SpherecastCommand` batched together. The volumes themselves should be defined as raw `AABB` or `BoundingSphere` structs in a `NativeArray` inside the vault, with containment checks evaluated on worker threads using `math.distancesq`.

---

### 2. `Floater.cs` (Organism/Prop Buoyancy)
* **Status**: Inefficient Managed Execution
* **Description**: Handles buoyancy and attachment mechanics for floating organisms. It implements `IFixedTickable`, which is an improvement over standard MonoBehaviour updates, but relies heavily on managed `UnityEvent` wrappers (`OnPickedUp`, `OnAttached`, `OnDetached`) for logic propagation.
* **Why it's bad**: 
  - `UnityEvent` invokers cause small but repeated heap allocations during attach/detach cycles.
  - Heavy reliance on cached component checks and GameObject parenting.
  - Forces physics calculations back to the main thread instead of utilising physics jobs.
* **Refactoring Strategy**: 
  Move the buoyancy forces to a global system (`BuoyancySystem`). Register all floater positions, attachment target body indices, and buoyancy scalars into a single `NativeArray` in `GlobalDataVault`. Execute buoyancy force calculation inside a single, parallelized Burst job (`BuoyancyForceJob`) that applies thrust vectors directly to a `NativeArray<float3>` of forces, feeding them into the physics solver as a single batch.

---

### 3. `BarterOfferCatalog.cs` / `BarterOfferData.cs` (Trading System)
* **Status**: Suboptimal Data Layout
* **Description**: Defines bartering offers and trading transactions. Stores definitions in individual `ScriptableObject` assets and validates them using runtime string calculations, calling `LocHash.Compute(offerId)` dynamically.
* **Why it's bad**: 
  - String hashing during gameplay cycles degrades performance.
  - Heavy reference-based memory layout prevents multi-threaded transaction validation.
  - Incompatible with Burst jobs due to reliance on managed classes.
* **Refactoring Strategy**: 
  Move the catalog definitions entirely into the static data monolith (`static_data.h8bin`). Define trade offers as flat, unmanaged structs (`BarterOfferDTO`) with pre-calculated hashes. Build a pure C# transaction evaluator (`ShinobuTransactionEvaluator`) that can run on background worker threads using unmanaged DTO inputs.

---

### 4. `HeavyTowWinch.cs` (Cable/Tether Joint Physics)
* **Status**: Strict Main Thread Locking
* **Description**: Manages structural winch cable physics, joint constraints, and cable snapping.
* **Why it's bad**: 
  - Highly coupled to Unity-specific `ConfigurableJoint` and `CharacterJoint` components.
  - Performs direct physics manipulations and distance comparisons on the main thread.
  - Prone to joint instability and physics frame stutters when multiple winches are active.
* **Refactoring Strategy**: 
  Extract winch joints into a Verlet integration solver. Represent the cable as a series of particle nodes in a `NativeArray<float3>` inside the vault. Let a Burst job (`WinchVerletSolverJob`) execute coordinate constraints and distance adjustments on worker threads, uploading only final vertex buffers to the GPU for cable rendering, entirely bypassing Unity Joint structures.

---

### 5. `LifePodTactilePrologueController.cs` (Tutorial Sequences)
* **Status**: Legacy OOP State Machine
* **Description**: Large class (over 43KB) managing fire extinguishers, sparks, and sequential UI steps for the prologue.
* **Why it's bad**: 
  - Heavy Mono-behavior layout, making it completely untestable via headless bots.
  - Relies on string-based game event hooks and component polling.
  - Directly binds gameplay triggers to scene-specific asset references.
* **Refactoring Strategy**: 
  Deconstruct the prologue into a light data-driven state machine. Store step definitions in unmanaged structures (`PrologueStepDTO`) inside the vault. Drive state transitions using typed signals sent via `SignalBus<PrologueEventSignal>`, keeping the controller as a purely visual view wrapper rather than a controller of state.

---

## SECTION 4: [LORE TRASH AUDIT] — CONTENT QA AUDIT

A complete audit of the narrative files recursively located in the `Docs/Lore/` directory has been performed.

### 4.1 Corpus Scale and Metrics
- **Total Markdown Files**: **18,377 files** recursively mapped.
- **Total Character Bytes**: **49.6 Megabytes** of text files.
- **Total Word Count**: **5,012,621 words** recursively indexed.
- **Structure**: The corpus contains a multilingual layout in the `Grand_Library` directory (15+ locales, including `en_US`, `ru_RU`, `fr_FR`, `de_DE`, `ja_JP`, `zh_CN`, etc.).

---

### 4.2 The "AI Hallucination" Flag and Tone Violations
The HECTON-8 narrative directive mandates a **NASA-punk / deep-sea noir** tone: cold corporate liability, physical hazards, wet paper, and functional details.
The previous LLM generations contaminated the files with **unrealistic progression metaphors** (e.g. key assets "unlocking" secrets of the ocean, fantasy filler adjectives).

The term `unlock` was flagged as a critical tone violation. Below are exact quotes from files demonstrating these hallucinations:

1. **`RS107_NAVIGATION_LINK_SUPPRESSION_BRIDGE.md`**
   * *AI Hallucination Text*: "...coordinates unlock the mysteries of the abyss."
   * *Tone Breach*: Replaces technical mapping with fantasy tropes.
2. **`RS023_FIRST_TOOL_CHAIN_SURVIVAL_GATE.md`**
   * *AI Hallucination Text*: "...using the welder will unlock new pathways of exploration."
   * *Tone Breach*: Breaks NASA-punk mechanical framing by treating a tool as a fantasy progression key.
3. **`RS001_FIRST_DESCENT.md`**
   * *AI Hallucination Text*: "...diving deeper will unlock the history of Atlas-6."
   * *Tone Breach*: Replaces forensic data recovery with simple gamified progression hooks.

Additionally, **72 out of 118** gameplay-facing lore packets in the primary set contain under-length content descriptions (< 200 words), necessitating a manual rewrite cycle using the tone constraints defined in `Lore_Bible.md`.

---
**REPORT END**
