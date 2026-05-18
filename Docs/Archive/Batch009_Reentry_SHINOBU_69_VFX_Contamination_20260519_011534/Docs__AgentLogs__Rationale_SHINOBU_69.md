# Rationale_SHINOBU_69

Agent: SHINOBU_69  
Domain: Volumetric Plasma / Beam VFX  
Status: ACTIVE - FRESH VFX REENTRY AFTER SAVE-SYSTEM 69 ARCHIVE  

## Decision 00 - Duplicate SHINOBU_69 Disambiguation

Problem: `CURRENT_BATCH.md` contains two `SHINOBU_69` prompts. Existing status/rationale files belonged to the old RLE/WAL SaveSystem role, while the user explicitly assigned `SHINOBU_VOLUMETRIC_PLASMA_BEAM`.

Solution: Archive prior SHINOBU_69 files under `Docs/Archive/Batch009_Reentry_SHINOBU_69_SaveSystem` and bind this run to the second prompt role `VOLUMETRIC_PLASMA_AND_BEAM_DIRECTOR`.

Rejected Alternatives: Mixing VFX and SaveSystem decisions in one status/rationale file was rejected because it destroys auditability. Editing SaveSystem again was rejected because the current request is beam rendering.

Scalability potential: Low/MX350 uses triangle/low-segment tube and no per-vertex noise. Middle uses moderate segment density. High uses stronger noise and richer tint. Ultra uses full 8-radial, 20-length segment geometry and shader-driven emissive overkill.

Hardware Impact: Hygiene has no frame-time effect. It prevents report contamination and compile-wall scope errors.

## Decision 01 - Mandate Set

Problem: Plasma beams touch rendering, GPU upload, Burst mesh generation, ARM64 DTO layout, AUP-relative precision, and fake-first VFX.

Solution: Apply render hot path, VFX fake-first, shader noir, GPU buffer, ARM64 layout, zero-GC, cinematic cheat, and execution phase mandates before coding.

Rejected Alternatives: `LineRenderer`, `TrailRenderer`, Shuriken ParticleSystem, runtime `new Mesh()`, and per-particle plasma truth were rejected because they break batching, allocate or mutate CPU mesh state, and waste frame budget.

Scalability potential: GlobalQualityWeight drives radial/length segments, noise amplitude, and noise evaluation so quality changes continuously instead of through binary low/high switches.

Hardware Impact: Expected low-end gain is avoiding per-beam renderer rebuild and material churn; exact microseconds require Unity Profiler/Frame Debugger proof.

