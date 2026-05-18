# 22 Scene Prefab And Source Of Truth Discipline

Date: 2026-05-07
Status: PENDING VERIFICATION
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## R4 Interior Actuality Boundary

This document is active only as static documentation/source orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current verification artifacts, and the latest DOC_GLOBAL reports.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->


Mandates followed:
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `STRM_Persistent_Object_Registry.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `PROJECT_LTS_Compatibility_Layer.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

Purpose:
- Audit where project truth actually lives: scenes, prefabs, runtime registries, and fallback roots.
- Judge whether HECTON-8 currently behaves like a disciplined prefab-and-bootstrap project or like a mature project with multiple overlapping truth sources.

## 1. Scene surface is narrow in production and wide in residue

Non-meta scene payload:

| Type | Count |
|---|---:|
| `.unity` scenes | 10 |
| scene-adjacent assets (`.asset`, `.exr`) | 3 |

Known scene set:
- `00_BOOTSTRAP`
- `01_MAIN_MENU`
- `01_ORBIT`
- `02_HECTON_WORLD`
- `03_HECTON_WORLD_CREST5`
- `GeminiSandbox`
- `XXX_SANDBOX`
- `XX_SANDBOX_MASUM`
- `X_GPUSANDBOX`
- `_Temp/FloraBeautyAudit_TMP`

What is genuinely true:
- The normative shipping handoff is still relatively clean.
- But the authored scene surface around it is not small or perfectly curated.

What is risky:
- Alternate world variants, sandbox scenes, GPU sandboxes, and temp beauty-audit scenes all increase ambiguity about what counts as canonical truth.
- That is manageable in development, but expensive in late-stage stabilization.

Verdict:
- Production scene path clarity: medium-high.
- Total scene-surface cleanliness: medium-low.

## 2. Prefab truth exists, but runtime fallback truth exists too

Evidence:
- `PrefabRegistry.cs:44` is a real authoritative owner with `DefaultExecutionOrder(-9500)`.
- It is a singleton, persists via `DontDestroyOnLoad`, and can self-create through `EnsureRuntimeInstance()` (`70-80`, `126`, `207-218`).
- It maintains managed and native maps for prefab identity (`91-95`, `105`, `326`, `337`).

What is genuinely good:
- The project does not treat prefab identity casually.
- Prefab lookup and registry concerns are explicit.

What is bad:
- The registry still contains runtime fallback behavior for missing bootstrap truth.
- A project that has to keep creating safety-net roots like `[PrefabRegistry]` is proving that source-of-truth discipline is still partly defensive rather than fully deterministic.

Verdict:
- Prefab identity infrastructure: real.
- Source-of-truth purity: medium-low.

## 3. The project lives in multiple truth layers at once

Visible layers:
- scene-authored production objects
- prefab-authored reusable gameplay objects
- generated `GEN_` content
- procedural family proxy prefabs
- runtime placeholder prefabs
- singleton/runtime-instanced safety nets

This is not automatically wrong.
It is, however, expensive.

Why it matters:
- every additional truth layer increases debugging cost
- every fallback bootstrap path weakens authoring determinism
- every temporary scene or legacy prefab family raises the probability of drift

## 4. Naming contract is real, but not universal

Evidence:
- `PFB_` and `GEN_` naming is heavily used across production-facing prefabs.
- Construction ghost/final prefabs, world procedural families, resource pickups, support pockets, and baked flora all reflect the intended schema.

Counter-evidence:
- Root and legacy-style names still exist:
  - `Player`
  - `Ocean_Crest`
  - `WorldGenerator`
  - `Sky_System`
  - `Objects`
  - `STRUCTURES`
  - numerous `ENV_...` rock variants

Interpretation:
- The naming system is not fake.
- It is also not yet the sole reality.

Verdict:
- Naming-discipline adoption: medium-high.
- Naming-discipline completeness: medium-low.

## 5. Scene/prefab interaction is one of the projectâ€™s hidden risk surfaces

Evidence:
- Construction has ghost prefabs and final prefabs.
- World runtime uses placeholder prefabs and family proxies.
- Prefab registry and bootstrap both maintain runtime guarantees.
- Player-facing objects like `Player.prefab`, transport prefabs, resource pickups, and world support prefabs coexist with scene-only world scaffolding and sandboxes.

What this means:
- The project has enough content and enough bootstrap coercion that scene/prefab mismatches can become silent bugs rather than obvious failures.
- This is especially true in a project with:
  - runtime self-instancing
  - large editor authoring tooling
  - multiple world variants
  - generated content families

## 6. Hard conclusion

HECTON-8 does have real prefab discipline.

But it does not yet have a single, globally clean source-of-truth model.

The honest state is:
- production path exists
- prefab identity exists
- naming contract exists
- runtime safety nets exist
- alternate and temporary scene surfaces also exist
- legacy and unguided naming residues also exist

That combination is common in large evolving projects.
It is also exactly how late-stage authority drift survives much longer than teams expect.

The threat here is not "missing prefabs" or "missing scenes."
The threat is truth fragmentation.
