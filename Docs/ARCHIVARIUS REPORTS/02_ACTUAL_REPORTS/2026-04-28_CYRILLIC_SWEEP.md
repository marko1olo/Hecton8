# CYRILLIC_SWEEP.md
Date: 2026-05-07
Status: PENDING VERIFICATION
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## R4 Interior Actuality Boundary

This document is active only as static documentation/source orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current verification artifacts, and the latest DOC_GLOBAL reports.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->



**Date:** 2026-04-29
**Status:** PENDING VERIFICATION
**Scope:** non-ASCII path sweep under `Assets/_Project`

**Mandates Followed:** `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

---

## Method

- Re-ran a filesystem sweep for file paths containing non-ASCII characters under `Assets/_Project`.
- Counted file entries only.
- This pass does not attempt a complete audit of source comments, string literals, or external docs.

---

## Current Scale

Non-ASCII file paths currently present under `Assets/_Project`: `606`

The older report materially understated the scope.

---

## Confirmed Examples

### Fonts

- `Assets/_Project/Art/Materials/Fonts/Ñ‚ÐµÐºÑÑ‚.ttf`
- `Assets/_Project/Art/Materials/Fonts/Ñ†Ð¸Ñ„Ñ€Ñ‹.ttf`

### Mesh assets

- many `Assets/_Project/Art/Meshes/Cleaned/ENV__...` assets with Cyrillic names

### Rock data and prefabs

- multiple `Assets/_Project/Data/RockSockets/...` assets with Cyrillic names
- multiple `Assets/_Project/Prefabs/Nature/...` prefab paths with Cyrillic names

### Sandboxed or content folders

- `Assets/_Project/Art/Models/Rocks/Rock 4 - Ð£ÐÐ˜Ð’Ð•Ð Ð¡ÐÐ›Ð¬ÐÐ«Ð™ Ð’Ð«Ð‘ÐžÐ /...`
- `Assets/_Project/_PROLOGUE_CONTENT/Prefabs/Ð¾Ð±Ð»Ð°ÐºÐ° Ð³ÐµÐºÑ‚Ð¾Ð½8.prefab`

### Text assets

- `Assets/_Project/Data/Ð¿Ð»Ð°Ð½.txt`
- `Assets/_Project/Data/Ñ‚ÐµÐºÑÑ‚.txt`

---

## Corrected Conclusion

This is not a small comment-hygiene issue.
It is a broad asset-path naming problem spanning fonts, meshes, data assets, prefabs, and content folders.

---

## What This Sweep Does Not Cover

- full source-comment language audit
- shader string-literal audit
- CI/build breakage proof on external locales

Those require separate targeted passes.

---

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | None. Documentation-only rewrite. |
| GC | None. Documentation-only rewrite. |
| Memory | None. Documentation-only rewrite. |
| Cadence | None. Runtime code unchanged. |
| Correctness | Improved by replacing a tiny misleading sample with a measured path-count snapshot. |

---

## Verdict

Non-ASCII path debt is widespread, not isolated.
Exact build impact remains `PENDING VERIFICATION`, but the naming exposure is real and large.
