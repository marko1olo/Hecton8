# 18 Interaction Construction And Runtime Linkage

Date: 2026-05-07
Status: PENDING VERIFICATION

Mandates followed:
- `CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`

Purpose:
- Audit the systems that connect player intent to world mutation: interaction, tool-side signal routing, construction placement, habitat topology, and maintenance-style runtime modules.
- Decide whether these are real gameplay owners or just content support shells.

## 1. Domain weight

Static snapshot:

| Domain | Files | Lines | `Instance` hits | `DontDestroyOnLoad` hits | `GlobalRegistry.` hits | Native/Burst surface | `Complete()` hits |
|---|---:|---:|---:|---:|---:|---:|---:|
| Interaction | 12 | 3,177 | 23 | 1 | 22 | medium | 2 |
| Tools | 15 | 3,236 | 36 | 5 | 31 | medium | 2 |
| Construction | 24 | 8,932 | 27 | 0 | 39 | high | 9 |
| Scavenging | 2 | 691 | 0 | 0 | 0 | light | 0 |
| Inventory | 2 | 331 | 0 | 0 | 0 | light | 0 |

Interpretation:
- `Construction` is not a support folder. It is another heavyweight domain.
- `Interaction` is more serious than a simple player raycast handler.
- `Tools` is mixed: part runtime system, part verification/governor scaffolding.

## 2. Interaction stack is materially real

Evidence:
- `EquipmentInteractionHandler.cs:18` is an authoritative queued owner, not a thin wrapper.
- It owns `NativeQueue<InteractionSignal>` and staged/scheduled `RaycastCommand`/`RaycastHit` lanes (`43-47`, `161-169`).
- It self-creates at runtime via `EnsureRuntimeInstance()` and persists through scene loads (`66`, `179`).
- It registers through `GlobalRegistry` and also holds dispatcher ownership (`93`, `199`, `205`, `241-244`).
- `PhysicalInteractionHandler.cs:19` and `PlayerInteraction.cs:57` are separate active owners on top of that stack.

What is genuinely good:
- The interaction layer is not trivial. It already thinks in queued signals, staged raycasts, and deferred dispatch.
- This is one of the better cases where mandate language and code reality actually resemble each other.

What is bad:
- It still relies on a runtime singleton root with `DontDestroyOnLoad`.
- There are multiple overlapping owners: `PlayerInteraction`, `PhysicalInteractionHandler`, `EquipmentInteractionHandler`, `PhysicalHandController`.
- The stack is powerful, but not simple. Authority is layered rather than singular.

Verdict:
- Interaction implementation reality: very high.
- Architectural purity: medium.
- This is a real gameplay interaction backbone with transitional ownership choices.

## 3. Construction has serious backend weight

Evidence:
- `HabitatGraphManager.cs:18` is an internal `IDisposable` backend, not a MonoBehaviour faÃ§ade.
- It owns native CSR-style graph buffers and power/atmosphere adjacency data (`43-49`, `877-888`).
- It directly describes itself as feeding downstream power and atmosphere solvers.
- `HabitatConstructionManager.cs:19` owns placement validation, adjacency assembly, build-cost transactions, and Burst-backed integrity validation (`48-53`, `531-542`, `711-719`).
- Folder-level top files include `HabitatGraphManager`, `HabitatConstructionManager`, `RepairDroneEntity`, `AutonomousExtractorSystem`, `LogisticsPipeNode`, `CultivationManager`, `DroneFleetManager`.

What is genuinely good:
- Base building is not fake. It has topology reasoning, graph publication, cost consumption, validation BFS, and module subsystems.
- Construction is one of the projectâ€™s clearest examples of authored gameplay resting on substantial runtime infrastructure.

What is bad:
- Construction is already another system-of-systems.
- It couples into power, world flow, degradation, module sockets, inventory, and logistics in one domain.
- This is rich, but it is also prime monolith territory.

Verdict:
- Construction implementation reality: extremely high.
- Stabilization risk: high.
- This domain is likely one of the most expensive integration surfaces in the project.

## 4. Tools folder is revealing

Evidence:
- `ToolDurabilitySystem.cs:19` is a real saveable runtime owner with native arrays, a `NativeQueue<BreakdownEvent>`, and a Burst decay job (`105-112`, `183-222`, `249`, `530-552`, `772-842`).
- But the folder also contains `PerformanceBudgetController`, `PerformanceMonitor`, `StateRecoveryVerifier`, `PauseSystemVerifier`, `SceneTransitionVerifier`.

What is genuinely good:
- Tool durability is real and technically serious.
- The team clearly built internal monitoring and verification helpers around runtime behavior.

What is bad:
- The folder name hides two different purposes:
  - actual player tool runtime
  - internal verification/governor infrastructure
- That means ownership boundaries here are semantically blurry even when individual files are competent.

Verdict:
- Runtime reality: medium-high.
- Folder cohesion: medium-low.
- This is another place where the project feels evolved rather than cleanly partitioned.

## 5. Construction and power are already intertwined

Evidence:
- `HabitatGraphManager` explicitly rebuilds habitat topology for downstream power and atmosphere use.
- `LogisticsPipeNode` and related construction modules indicate that base modules are not just decorative buildables; they participate in systems graphs.

What this means:
- Construction is not an isolated player loop.
- It is one of the main places where multiple major subsystems converge:
  - placement
  - power/logistics
  - atmosphere
  - structural stress
  - drones/maintenance

This is good for game depth.
This is dangerous for regression radius.

## 6. Inventory and scavenging are smaller but not fake

Evidence:
- `ResourceNodeTemplate.cs` is a `544` line authored template, not a tiny data stub.
- `HarvestableTemplate.cs`, `ItemTemplateRegistry.cs`, and `ItemPhysicalMetadata.cs` indicate a broader item/resource identity layer.
- `HabitatConstructionManager` directly interacts with `PlayerInventory` for build-cost consumption.

Verdict:
- These domains are less structurally dominant than `Construction` or `Interaction`.
- They are still materially integrated into runtime loops.
- They read more like supporting identity/data layers than empty placeholders.

## 7. Most important structural finding

The interaction-to-world-mutation path in HECTON-8 is real end to end:

- player input/interaction intent exists
- tool/interaction routing exists
- world hit/query logic exists
- build placement/integrity logic exists
- habitat graph publication exists
- downstream system coupling exists

That is a strong sign of real game assembly.

The hard criticism:
- too many of these paths are already centralized into large owners
- some paths still depend on singleton/runtime-instancing patterns
- the system graph is increasingly rich, but not increasingly simple

## 8. Hard conclusion

Construction and interaction are no longer prototype domains.

They are core production domains.

That is good news for project substance.
It is bad news for anyone pretending the remaining risk is just "content volume."

The risk here is not emptiness.
The risk is linkage complexity.
