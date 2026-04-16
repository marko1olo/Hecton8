# Master Grade Hecton8 — Systematic Polish & Optimization

This plan outlines the steps required to transition the Hecton8 project from technical stability to "Master Grade" AA commercial fidelity. The primary targets are VRAM/RT footprint reduction, a complete architectural overhaul of the procedural scatter system, and strict enforcement of Zero-GC and Job-driven directives.

## User Review Required

> [!IMPORTANT]
> **WorldProceduralScatterDirector Overhaul**: This is a high-risk refactor of an 11,000-line monolith. It will be broken down into focused handlers (e.g., `ScatterEvaluator`, `ScatterReconstructor`).
> [!WARNING]
> **VRAM / RT RED Status**: The project is currently at ~531 MB RT usage. To reach Master Grade, this must drop below 500 MB. This may require reducing Crest (Ocean) or MapMagic resolution or optimizing the HUD compositor.

## Proposed Changes

### 1. Flora Master Grade Polish (Kelp & Coral)
Goal: Elevate environment visual fidelity to AA standards (NASA-Punk / Deep Sea Noir).

#### [MODIFY] [Hecton_KelpMaster.shader](file:///c:/hades/Hecton8/Assets/_Project/Art/Shaders/Hecton_KelpMaster.shader)
- **Sub-Surface Scattering (SSS)**: Implement wrapped lighting translucency with `_SSSColor`, `_SSSPower`, and `_SSSStrength` based on thickness maps (Mask.r).
- **Prop Wash Interaction**: Add vertex displacement driven by `_HectonPropWashPosition` and `_HectonPropWashForce` global variables.
- **Biolum Synchronization**: Link pulse frequency multiplier to `DepthZoneDirector` signals via `HectonBiolumManager`.

#### [MODIFY] [Hecton_CoralMaster.shader](file:///c:/hades/Hecton8/Assets/_Project/Art/Shaders/Hecton_CoralMaster.shader)
- **Detail PBR**: Add secondary detail normal map for micro-porosity (limestone texture) visible at macro distances.
- **Roughness Audit**: Re-balance reflectivity for deep-sea minerals; implement dithered LOD transitions to prevent silhouette popping.

#### [NEW] `FloraInteractionManager.cs`
- Centralized singleton (ITickable) tracking player and sub pod positions.
- Updates global shader variables (`_HectonPropWashPosition`) for vertex displacement interaction.

---

### 2. VRAM & RT Optimization
Goal: Reduce RT usage from 531 MB to < 500 MB.

#### [MODIFY] [SuitHUDScreenCompositor.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Visor/SuitHUDScreenCompositor.cs)
- Implement dynamic resolution for `sharedProjectionTexture` based on quality settings.
- Ensure strict release of RTs when not in use.

#### Physics & Rendering
- Audit Crest (Ocean) `OceanRenderer` and `UnderwaterRenderer` for RT overhead.
- Audit MapMagic `MapMagicBridge` for unnecessary height/splat buffers.

---

### 2. WorldProceduralScatterDirector Refactor (MONOLITH DECOMPOSITION)
Goal: Replace the 11k line God Object with a distributed, Job-based system.

#### [NEW] `ScatterEvaluationJob` (Unity Job System + Burst)
- Offload the massive rule evaluation loop (currently lines 1020–1338) from the main thread.
- Move `MatchesScatter`, `EvaluateHeatmap`, and score calculations into a Burst-compatible struct.

#### [NEW] `ScatterBudgetHandler` / `ScatterReconciler`
- Extract budget logic and instance lifecycle management into focused, unit-testable classes.

#### [MODIFY] [WorldProceduralScatterDirector.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/WorldProceduralScatterDirector.cs)
- Change `RebuildScatterPreview` to asynchronous: `Schedule()` at the end of Tick, `Complete()` at the start of the next Tick (avoiding frame-sync stutters).
- Remove `new ScatterCandidate` allocations; use a `NativeArray` of candidate data or a pre-allocated pool of structs.

---

### 3. Zero-GC Global Audit
Goal: Ensure 0 Bytes/frame in all Hot Paths.

#### [MODIFY] [HectonBoidController.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/HectonBoidController.cs)
- Verify `GetComponent` and list allocations in `Tick`.

#### [MODIFY] [SaveManager.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/SaveSystem/SaveManager.cs) (Assume path)
- Ensure `.tmp -> verify -> .sav` rename protocol is followed precisely to prevent data corruption.

---

### 4. Build & Environment Consistency
Goal: Fix status ledger "RED" items.

#### [MODIFY] `Editor/BuildSettingsManager` (or similar)
- Re-align `00_BOOTSTRAP`, `01_MAIN_MENU`, `02_HECTON_WORLD` in `BuildSettings`.

## Open Questions

> [!QUESTION]
> Are there specific "Hero Props" or high-fidelity flora assets (e.g., Aegir-themed corals) that currently lack LODs? The ledger indicates LOD Group is mandatory for all props > 0.5m.

## Verification Plan

### Automated Tests
- `RuntimePerformanceProfiler`: Verify frame time < 16.6 ms (main thread < 12 ms).
- `MemoryProfiler`: Verify RT < 500 MB and GC = 0 B/frame.
- `ScriptValidation`: Run `validate_script` on all refactored files.

### Manual Verification
- Walkthrough in `02_HECTON_WORLD` to confirm scatter persistence and smooth LOD transitions.
- Stress test Save/Load during rapid movement (Floating Origin test).
