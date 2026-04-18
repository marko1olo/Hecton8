**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Task List — Master Grade Hecton8 Transition

- `[ ]` **Phase 1: Flora Master Grade Polish**
    - `[ ]` Implement `SSS` and `Prop Wash` in `Hecton_KelpMaster.shader`
    - `[ ]` Implement `Detail PBR` and porosity in `Hecton_CoralMaster.shader`
    - `[ ]` Create `FloraInteractionManager.cs` (Zero-GC, ITickable)
    - `[ ]` Verify vertex interaction in `02_HECTON_WORLD` scene

- `[ ]` **Phase 2: VRAM & RT Optimization**
    - `[ ]` Implement dynamic resolution scaling in `SuitHUDScreenCompositor.cs`
    - `[ ]` Audit `Crest` and `MapMagic` for redundant buffers
    - `[ ]` Verify RT usage < 500 MB via Memory Profiler

- `[ ]` **Phase 3: Scatter Director Refactor**
    - `[ ]` Extract `ScatterEvaluationJob` (Burst-compatible struct)
    - `[ ]` Implement `ScatterReconciler` for main-thread lifecycle
    - `[ ]` Refactor `WorldProceduralScatterDirector.cs` to use Job-based evaluation
    - `[ ]` Verify 60 FPS performance during world exploration

- `[ ]` **Phase 4: Save System & Maintenance**
    - `[ ]` Implement atomic write protocol in `SaveManager.cs`
    - `[ ]` Re-align Build Settings (`00_BOOTSTRAP`, `01_MAIN_MENU`, `02_HECTON_WORLD`)
    - `[ ]` Final project-wide Zero-GC audit
