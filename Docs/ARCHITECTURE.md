# HECTON-8 — Architecture Specification

## 1. Engine Boundaries & Assembly Definitions
Unity 6000 architecture strictly partitioned into zero-allocation presentation, simulation, and render assemblies.

```mermaid
graph TD
    Core[Hecton.Core (Burst & Jobs)] --> Data[Hecton.Data (ScriptableObjects)]
    Core --> Render[Hecton.Rendering (URP Bathymetric Shaders)]
    Core --> Audio[Hecton.Audio (Acoustic Convolver)]
    Render --> UI[Hecton.Presentation (Canvas HUD)]
```

## 2. Hydrodynamic & Sonar Invariants
- Zero heap allocation in `FixedUpdate()` physics loop.
- Acoustic Sound Velocity Profile (SVP) refraction computed in Burst jobs.
