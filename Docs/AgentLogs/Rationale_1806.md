# Rationale 1806

Evidence class: STATIC_DOC / STATIC_SOURCE only.

## Decisions

- Selected six mandates: evidence audit, terrain VT, URP/HLOD, cinematic-cheat fake-first, performance/VRAM budgets, and shader/noir fog. Reason: the output is a static route/action manifest for surface, coast, waterline, sky, and photic shallows; it must prevent fake runtime claims and preserve visual/performance gates for a later Unity slot.
- Scope excludes Unity/runtime/editor/profiler actions. Reason: the task explicitly forbids fake proof and says this pass should be static.
- Chose one CSV row per required route beat. Reason: the later Unity implementer needs direct beat-to-object-to-proof routing, not another prose-only report.
- Kept inactive scene objects and rejected placeholder families explicit. Reason: 1801/1802 show several plausible-looking names that are not active proof or final visual references.
- Used future proof labels for screenshots and metrics. Reason: this task is static; proof labels define what must be produced later without implying the artifacts exist now.
