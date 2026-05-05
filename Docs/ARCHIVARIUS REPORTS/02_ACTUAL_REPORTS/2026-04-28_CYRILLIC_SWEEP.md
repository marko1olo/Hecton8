# CYRILLIC_SWEEP.md
Date: 2026-04-28
Status: REFERENCE


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

- `Assets/_Project/Art/Materials/Fonts/текст.ttf`
- `Assets/_Project/Art/Materials/Fonts/цифры.ttf`

### Mesh assets

- many `Assets/_Project/Art/Meshes/Cleaned/ENV__...` assets with Cyrillic names

### Rock data and prefabs

- multiple `Assets/_Project/Data/RockSockets/...` assets with Cyrillic names
- multiple `Assets/_Project/Prefabs/Nature/...` prefab paths with Cyrillic names

### Sandboxed or content folders

- `Assets/_Project/Art/Models/Rocks/Rock 4 - УНИВЕРСАЛЬНЫЙ ВЫБОР/...`
- `Assets/_Project/_PROLOGUE_CONTENT/Prefabs/облака гектон8.prefab`

### Text assets

- `Assets/_Project/Data/план.txt`
- `Assets/_Project/Data/текст.txt`

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
