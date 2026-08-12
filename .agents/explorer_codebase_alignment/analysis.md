# HECTON-8 Codebase vs Documentation Mandate Verification Report

**Author**: explorer_codebase_alignment (teamwork_preview_explorer)  
**Date**: 2026-08-11  
**Scope**: Verification of Unity C# scripts in `Assets\_Project\Scripts\` against HECTON-8 governing documents (`AGENTS.md`, `PROJECT_BIBLES.md`, `Docs/SYSTEMS_CONTRACTS.md`, and `.agents-skills/` mandates).

---

## Executive Summary

A systematic read-only audit cross-referencing active HECTON-8 documentation rules against the Unity C# codebase (`Assets/_Project/Scripts/`) revealed multiple concrete discrepancies. While the codebase demonstrates exceptional discipline in several areas (e.g. zero LINQ imports in runtime non-editor code, zero naked `Camera.main` usages, strict `HectonEventBus` isolation for modding), several key violations of product mandates, data contracts, and determinism rules were identified.

---

## Detailed Audit Findings

### 1. SaveData Root Struct Managed Collection Violation
- **Governing Mandate**: `AGENTS.md` line 197; `.agents-skills/DATA_Save_Persistence_Binary_Delta_Checksum.txt` line 43.
  > *"Managed-collections with dynamic allocations (e.g., `Dictionary<string, T>` or `HashSet<string>`) in the root structures of `SaveData.cs` are banned; serialization must rely on `ISerializationCallbackReceiver` and parallel flat lists."*
- **Code Reality**: `Assets/_Project/Scripts/SaveData.cs`
  - Line 153: `public Dictionary<string, float> toolDurabilityMap = new Dictionary<string, float>();`
  - Line 156: `public Dictionary<string, bool> toolBrokenMap = new Dictionary<string, bool>();`
  - Line 159: `public HashSet<int> discoveredBiomeIds;`
  - Line 365: `public Dictionary<string, string> CustomModData = new Dictionary<string, string>();`
- **Impact & Discrepancy**: The root `SaveData` structure direct fields break the binary/delta unmanaged serialization contract by using managed heap-allocating `Dictionary` and `HashSet` instances.

---

### 2. Voxel SDF Distance Function Quality Weight Mutation (Determinism Violation)
- **Governing Mandate**: `AGENTS.md` line 237; `PROJECT_BIBLES.md` (`voxels.md`); `ORIGINAL_REQUEST.md` (R1 & R2).
  > *"`GlobalQualityWeight` may scale visual detail, solver complexity, cadence... It must not change gameplay truth ownership, DTO layout, save identity, authority route, or deterministic state ownership."*
- **Code Reality**: `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakeJobs.cs`
  - Lines 22-25:
    ```csharp
    float floor = SdBox(p - new float3(0f, -18f, 0f), new float3(72f, 10f, 72f));
    float arch = SdVerticalTorus(p, math.lerp(18f, 28f, math.saturate(Config.GlobalQualityWeight)), 5.5f);
    arch = math.max(arch, -p.y - 3f);
    float result = math.min(floor, arch);
    ```
- **Impact & Discrepancy**: The SDF distance evaluation function `SdVerticalTorus` lerps the major torus radius between 18m and 28m directly based on `Config.GlobalQualityWeight`. Changing graphics quality settings mutates the underlying terrain SDF geometry truth, resulting in mesh/collider vertex divergence across quality levels.

---

### 3. Kinematic Arrest Gate Missing Player Suspension
- **Governing Mandate**: `AGENTS.md` line 199.
  > *"To prevent falling through async-generated voxel terrain, spawner/KCC logic must execute a Kinematic Arrest Gate. The player must remain suspended (`IsSuspended = true`, gravity/velocity zero, input locked, screen blacked out) until `HectonVoxelVolume` or `MapMagicBridge` broadcasts `WorldChunkPhysicsBakedSignal`... Time-based coroutine timeouts for loading are banned."*
- **Code Reality**: `Assets/_Project/Scripts/HectonPlayerSpawner.cs`
  - Line 1685: Explicit code comment:
    `/// STILL MISSING (not implemented here): player suspension itself — IsSuspended, gravity/velocity zero, input lock and screen blackout live in the movement/UI route, which this change does not touch.`
  - Line 2109: When `!WorldChunkPhysicsBakedEvents.IsLaneActive` (the physics bake signal lane has not published yet), the spawner releases the player immediately via `TeleportPlayer(_spawnPosition)` instead of holding suspension until terrain physics bakes.
- **Impact & Discrepancy**: The spawner logic checks for point readiness but lacks the actual player suspension lock (`IsSuspended`, velocity zero, input lock, blackout), allowing players to fall through unbaked colliders if spawning occurs before physics bake completion.

---

### 4. ARM64 Struct Layout & Managed Reference Violations in Jobs / DTOs
- **Governing Mandate**: `.agents-skills/DATA_Runtime_Struct_Layout_ARM64.txt` line 49; `AGENTS.md` line 287.
  > *"Runtime DTOs, SignalBus payloads, telemetry entries, save staging records, and GPU upload records must be ARM64-safe: unmanaged fields, no runtime `bool`, no managed references, explicit padding when needed, and size/alignment proof when crossing native/Burst/persistence/GPU boundaries."*
- **Code Reality**:
  - `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs`
    - Line 88: `public bool IsCanonicalCollider;` inside `ExtractSurfaceNetsJob` (Burst `IJob` struct).
  - `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs`
    - Lines 2000-2012: `ChunkBuildPendingJob` struct contains C# `bool` fields (`public bool Active;`, `public bool Cancelled;`) AND a managed interface reference (`public IDataVault ReadPinVault;`) alongside `NativeArray` handles.
  - `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs`
    - Lines 491 & 501: `public bool HasAup;` in job structs.
- **Impact & Discrepancy**: Standard C# `bool` fields in job/DTO structs introduce 1-byte vs 4-byte marshalling ambiguity on ARM64 platforms. Furthermore, embedding managed references (`IDataVault`) inside job tracking structs breaks Burst unmanaged safety guarantees.

---

### 5. `FindAnyObjectByType` Scene Scans and Editor API calls in `[RuntimeInitializeOnLoadMethod]`
- **Governing Mandate**: `AGENTS.md` lines 260 & 209; `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`.
  > *"Forbidden: scene search (`FindObjectOfType`, `GameObject.Find`), `Resources.Load`, reflection, or hot scene queries. `GlobalRegistry` is cold DI/identity only."*
- **Code Reality**: `Assets/_Project/Scripts/Graphics/HectonVisualsOrchestrator.cs`
  - Lines 124-138:
    ```csharp
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (FindAnyObjectByType<HectonVisualsOrchestrator>() == null)
        {
            var go = new GameObject("HectonVisualsOrchestrator_Auto");
            var orch = go.AddComponent<HectonVisualsOrchestrator>();
            orch._celestialEngine = FindAnyObjectByType<HectonCelestialEngine>();
            var oceanRenderer = FindAnyObjectByType<Renderer>();
#if UNITY_EDITOR
            orch._oceanMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Crest/Crest/Materials/Ocean.mat");
#endif
        }
    }
    ```
- **Impact & Discrepancy**: Runtime initialization performs `FindAnyObjectByType<Renderer>()` across every object in the scene to find ocean renderers, and embeds `#if UNITY_EDITOR` `AssetDatabase.LoadAssetAtPath` inside runtime bootstrap code.

---

### 6. Hardcoded `KeyCode` Usage in Gameplay Authority
- **Governing Mandate**: `Docs/SYSTEMS_CONTRACTS.md` line 168; `PROJECT_BIBLES.md` (`input.md`).
  > *"Forbidden: Hardcoded `KeyCode` in gameplay authority. Full keyboard/gamepad rebinding through input action maps is required."*
- **Code Reality**: `Assets/_Project/Scripts/ControlScheme.cs`
  - Lines 21-64:
    ```csharp
    public KeyCode interactKey = KeyCode.E;
    public KeyCode swimAscendPrimary = KeyCode.Space;
    public KeyCode swimDescendPrimary = KeyCode.LeftControl;
    public KeyCode swimDescendAlternate = KeyCode.C;
    public KeyCode swimDescendLegacy = KeyCode.Q;
    public KeyCode toolSlot1 = KeyCode.Alpha1;
    ...
    public KeyCode flashlightKey = KeyCode.F;
    public KeyCode mapKey = KeyCode.M;
    public KeyCode sprintKey = KeyCode.LeftShift;
    ```
- **Impact & Discrepancy**: `ControlScheme.cs` directly defines and uses Unity legacy `KeyCode` fields instead of routing all actions through Unity Input System rebindable Action Maps.

---

### 7. String Interpolation in Tool Hit Logging
- **Governing Mandate**: `AGENTS.md` line 273; `PROJECT_BIBLES.md` (`tools.md`).
  > *"Forbidden: Naked `Debug.Log`, `LogWarning`, or `LogError` in hot paths... Hot paths allocate 0 B/frame."*
- **Code Reality**: `Assets/_Project/Scripts/ToolHitUtility.cs`
  - Line 701: `Hecton8.Core.H8Debug.Log($"[ToolInfo] {messageBuffer.ToString()}");`
  - Line 713: `Hecton8.Core.H8Debug.LogWarning($"[ToolWarning] {messageBuffer.ToString()}");`
- **Impact & Discrepancy**: In `UNITY_EDITOR` builds, mining/welding/cutting tool hits trigger `FixedCharBuffer.ToString()` and string interpolation `$"[ToolInfo] ..."` inside `ToolHitUtility`.

---

## Matrix of Verified Compliance vs Violations

| Domain | Mandate Standard | Code Realized State | Status |
|---|---|---|---|
| **Hot Path LINQ** | `0 LINQ in hot paths` | 0 `using System.Linq;` in non-editor scripts | **COMPLIANT** |
| **Camera.main** | `0 Camera.main usages` | 0 `Camera.main` calls in non-editor scripts | **COMPLIANT** |
| **Modding Signal Bus** | `HectonEventBus for modding only` | Restricted exclusively to `ModdingAPI/` | **COMPLIANT** |
| **SaveData Structs** | `No Dictionary/HashSet in SaveData` | Contains `Dictionary` and `HashSet` fields | **NON-COMPLIANT** |
| **Voxel SDF Determinism**| `SDF independent of QualityWeight` | lerps torus radius in `HadalArchBakeJobs.cs:23` | **NON-COMPLIANT** |
| **Kinematic Arrest Gate**| `Player suspended until bake signal` | Suspension missing in `HectonPlayerSpawner.cs:1685` | **NON-COMPLIANT** |
| **ARM64 DTO Layout** | `No C# bool in Job/DTO structs` | `bool IsCanonicalCollider` in `VoxelSurfaceNetsJobs.cs:88` | **NON-COMPLIANT** |
| **Runtime Initialization**| `No scene search in bootstrap` | `FindAnyObjectByType<Renderer>()` in `HectonVisualsOrchestrator.cs:133` | **NON-COMPLIANT** |
| **Input Action Abstraction**| `No hardcoded KeyCode` | Hardcoded `KeyCode` in `ControlScheme.cs:21-64` | **NON-COMPLIANT** |
