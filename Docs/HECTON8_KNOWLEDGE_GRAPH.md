# HECTON-8 Knowledge Graph & Master Navigation Hub

Status: CANONICAL NAVIGATION HUB
Owner: DOCS_GOVERNANCE
Last Updated: 2026-08-11

---

## Executive Overview

This Knowledge Graph serves as the centralized navigation hub, architectural reference, and authority index for the **HECTON-8** engine and codebase. HECTON-8 is a singleplayer underwater survival game built on Unity 6000.4 URP, featuring a 30km geology world, voxel SDF terrain, zero-GC Burst/JobSystem runtime, and custom binary `.h8bin` persistence.

All documentation, codebase structures, and mandate enforcement rules within HECTON-8 are organized into 7 primary domains detailed below.

---

## Domain 1: Supreme Authority & Project Governance

The governance chain forms the immutable product and architectural baseline. All lower-level documents, subsystem specifications, and C# implementations must strictly adhere to these authority files.

* **[AGENTS.md](../AGENTS.md)** — Supreme HECTON-8 Agent Law, Performance Budgets (60 FPS / 16.67ms, 0 B/frame GC, 1800 MB VRAM ceiling), Task Intake Regulations, and Anti-Cheat Mandates.
* **[PROJECT_BIBLES.md](../PROJECT_BIBLES.md)** — Root Domain Bibles Index, Authority File Map, and Technical Standards.
* **[VISION_LOCKS.md](../VISION_LOCKS.md)** — Product Vision Locks, 100% Singleplayer Supremacy, Ambiguity Resolution, and Core Design Decisions.
* **[TASTE.md](../TASTE.md)** — Player-Facing Quality Floor for Visuals, Audio, Diegetic HUD, Lighting, and Art Direction.
* **[GEMINI.md](../GEMINI.md)** — Gemini / Antigravity Agent Shim & Routing Adapter.
* **[COMMON_SENSE.md](../COMMON_SENSE.md)** — 18 Architectural AI Cognitive Constraints for Zero-GC and Unmanaged Memory.
* **[AGENT_AUTHORITY_ROUTING.md](AGENT_AUTHORITY_ROUTING.md)** — Task Classification & Mandatory Intake Sequence.

---

## Domain 2: Mandate Registry & Verification

Technical mandates enforce specific coding, architectural, and optimization laws across the engine. Mandates are centralized in `.agents-skills/` and verified via automated static gates.

* **[Mandate Registry Index](../.agents-skills/README.md)** — Inventory of 80 enforced technical mandate `.txt` files covering AI, Audio/DSP, Memory, Math, Rendering, Physics, and Networking.
* **[TestMandateRegistry.py](../Tools/Docs/TestMandateRegistry.py)** — Primary static gate enforcing mandate command language, valid prefixes, and registry integrity (`python Tools/Docs/TestMandateRegistry.py --strict`).
* **[TestAgentRuleRouting.py](../Tools/Docs/TestAgentRuleRouting.py)** — Static validator preventing unauthorized markdown files in root directory.
* **[BuildProjectRootBiblesCombined.py](../Tools/Docs/BuildProjectRootBiblesCombined.py)** — Tool for compiling single-file reference bibles.

### Key Mandates:
* `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` — Zero-allocation hot path mandate.
* `DATA_Runtime_Struct_Layout_ARM64.txt` — ARM64 struct alignment & bool/reference prohibition.
* `MATH_AUP_Determinism_Sync.txt` — 64-bit float Absolute Universe Position math.
* `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt` — Thread-safe single-producer single-consumer audio queues.

---

## Domain 3: Core Engine & Subsystems

The HECTON-8 engine is partitioned into high-performance subsystems operating over unmanaged data structures and Unity C# JobSystem + Burst.

### 1. Voxels & Dual Surface Nets
* **Bible**: `voxels.md` | `Docs/ARCHITECTURE/STATIC_CAVE_SDF_VOLUME_BAKER.md`
* **Subsystem**: `HectonVoxelEngine`, `VoxelSurfaceNetsJobs`, `OfflineHadalArchBaker`.
* **Rules**: SDF sampling must evaluate canonical position without bias from camera view or `GlobalQualityWeight`. Dual Surface Nets produces watertight cave meshes.

### 2. Terrain & Geology
* **Bible**: `terrain.md` | `world.md`
* **Subsystem**: MapMagic 2 Bridge (`HectonMapMagicVegetationBridge`), `WorldProceduralTerrainThermalWeatheringJobs`, `HydraulicErosionJob`.
* **Scale**: Bound to 30km (±15,000m) playable world extent with 5-degree shelf drop.

### 3. Physics & Kinematics
* **Bible**: `physics.md` | `player.md` | `vehicles.md`
* **Subsystem**: PhysX integration, Kinematic Character Controller (`HectonPlayerSpawner`), Kinematic Arrest Gate.
* **Rules**: Player remains suspended until `WorldChunkPhysicsBakedSignal` is published by voxel/terrain bakes.

### 4. Audio & DSP
* **Bible**: `audio.md` | `sonar.md`
* **Subsystem**: Spatial Audio DSP (`AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`), Hydroacoustic Sonar, Binaural HRTF.
* **Rules**: SPSC lock-free queues using `NativeArray<float>` with `Allocator.AudioKernel`.

### 5. Input System
* **Bible**: `CTRL_Device_Abstraction_Haptics.txt` | `Docs/SYSTEMS_CONTRACTS.md`
* **Subsystem**: `ControlScheme`, Unity Input System Action Maps.
* **Rules**: Hardcoded `KeyCode` fields are forbidden in gameplay authority; all inputs route through action maps.

### 6. Render Pipeline & Graphics
* **Bible**: `rendering.md` | `shaders.md` | `lighting.md`
* **Subsystem**: Unity 6000.4 URP RenderGraph, SRP Batcher, CBUFFER/BRG uniform uploads.
* **Rules**: `MaterialPropertyBlock` banned on SRP-batched geometry; continuous `GlobalQualityWeight` scales visual tier dynamically.

### 7. World Scaling & Coordinate Math
* **Bible**: `math.md` | `CORE_Submarine_Vehicles_Kinematics_AUP.txt`
* **Subsystem**: Absolute Universe Position (AUP) 64-bit origin shifting (`double3`).
* **Bounds**: Playable terrain geometry = 30km (±15,000m); AUP math precision headroom = ±50km.

---

## Domain 4: Codebase Compliance & Audit Registry

Continuous audit findings track codebase adherence to engine mandates. Key findings from recent alignment audits include:

1. **[SaveData.cs](../Assets/_Project/Scripts/SaveData.cs)** — *Audit Finding*: Root DTO structure currently contains managed `Dictionary` and `HashSet` collections (lines 153, 156, 159, 365), violating zero-allocation binary save mandates. Slated for refactoring into flat unmanaged structs + `ISerializationCallbackReceiver`.
2. **[HadalArchBakeJobs.cs](../Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakeJobs.cs)** — *Audit Finding*: Line 23 scaled torus SDF radius via `GlobalQualityWeight`, causing determinism breach. Corrected to evaluate canonical geometry truth independent of quality tier.
3. **[HectonPlayerSpawner.cs](../Assets/_Project/Scripts/HectonPlayerSpawner.cs)** — *Audit Finding*: Kinematic Arrest Gate requires holding player suspension (`IsSuspended = true`, zero velocity, input lock, screen blackout) until `WorldChunkPhysicsBakedSignal` completes.
4. **[VoxelSurfaceNetsJobs.cs](../Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs)** — *Audit Finding*: Line 88 contained C# `bool` field in Burst job struct (`ExtractSurfaceNetsJob`), violating ARM64 struct layout rules. Slated for conversion to `byte` bitfields.

---

## Domain 5: Data & Serialization Standards

All persistent state and runtime DTOs in HECTON-8 use explicit unmanaged memory layouts for high cache locality and fast serialization.

* **Binary Save Format (`.h8bin`)**: Custom zero-allocation binary payload format with checksum validation (`DATA_Save_Persistence_Binary_Delta_Checksum.txt`).
* **Unmanaged DTO Layouts**: All job structs and data vault handles use explicit struct packing and 64-bit alignment (`DATA_Runtime_Struct_Layout_ARM64.txt`).
* **Forbidden in Job/DTO Structs**: C# `bool` fields (must use `byte`), managed class/interface references, object references, and managed arrays (`T[]`).
* **Zero-GC Allocation Policy**: No LINQ, string concatenation, `ToString()` calls, or `new` allocations in Update/FixedUpdate hot paths. Preallocated `char[]` buffers and `TMP_Text.SetCharArray()` are mandatory.

---

## Domain 6: Singleplayer & Modding Architecture

HECTON-8 is architected as a singleplayer-first experience with a decoupled DTO layer to support safe user modding and future co-op extension.

* **Singleplayer Supremacy**: Bounded by `VISION_LOCKS.md` and `NO_COOP_PUBLIC_POSITIONING.md`. Public marketing strictly positions the game as a singleplayer underwater survival experience.
* **AUP Coordinate Math**: 64-bit `double3` Absolute Universe Position provides a ±50km sandbox coordinate window without floating-point origin jitter.
* **Modding SDK & Sandbox**: Authoring interface (`Docs/Modding/SDK_Authoring_Interface_Plan.md`) isolates user scripts inside a quarantined API sandbox (`Mod_API_Sandbox_Quarantine.md`), communicating via `SignalBus<T>` and `HectonEventBus`.

---

## Domain 7: Documentation Archive Index

Historical task logs, deprecated architectural proposals, and legacy scan reports are preserved in `Docs/Archive/` for anti-amnesia provenance:

* **[Docs/Archive/Batch014_LegacyTasks/](Archive/Batch014_LegacyTasks/)** — Archive of 84 historical task status log files (`Status_1428.md` through `Status_3257.md`) from June 2026.
* **[Docs/Archive/V0_Playtest/](Archive/V0_Playtest/)** — Temporary playtest logs and live execution measurements (`NEXT_CHAT_L*.md`, `V0_L*_LIVE_RESULTS.md`).
* **[Docs/Archive/DocumentationCompleteness_20260605/](Archive/DocumentationCompleteness_20260605/)** — Legacy static completeness scan reports from June 2026.
* **[Docs/Archive/](Archive/)** — Root historical archive directory containing legacy batches (`Batch001/` through `Batch013/`), deprecated bibles, and historical audit outputs.
