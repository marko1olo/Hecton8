# TRANSFORM ACCESS CRIMES — SMART HOT-PATH AUDIT
Date: 2026-05-04
Status: DEPRECATED


**Audit Date:** 2026-04-28  
**Scope:** `Assets/_Project/Scripts/`  
**Pattern Scope:** `.parent`, `.GetChild(`, `.SetParent(`  
**Hot-Path Scope:** `Update`, `FixedUpdate`, `Tick`, `SlowTick`, Burst job `Execute` only  
**Explicit Exclusions:** `Awake`, `Start`, `OnEnable`, `OnDisable`, `OnDestroy`, Editor classes, Authoring classes, `#if UNITY_EDITOR` blocks

---

## Methodology

This pass is context-aware, not raw text grep.

- Parsed class and method scopes across first-party scripts.
- Suppressed any match inside Editor files, classes ending in `Editor`, classes ending in `Authoring`, and editor-only preprocessor blocks.
- Suppressed any match inside initialization lifecycle methods explicitly allowed by the audit brief.
- Counted only direct occurrences inside the requested hot methods or burst job `Execute`.

---

## Result

**Direct hot-path violations found:** `0`

| File | Line | Pattern | Method | Status |
|---|---:|---|---|---|
| None | — | — | — | No direct `.parent` / `.GetChild` / `.SetParent` hits in requested hot-path scopes |

---

## False Positives Removed From Prior Report

The previous report was materially wrong. These were text hits, not hot-path crimes:

- `Atmosphere/HectonSurfaceWeatherDirector.cs` `CreateRuntimeVfxRig()` uses `SetParent`, but not inside `Update`, `FixedUpdate`, `Tick`, `SlowTick`, or burst `Execute`.
- `Core/PlayerRuntimeContextService.cs` `InitializeService()` uses `SetParent(null, true)` during bootstrap, not hot path.
- Editor and Authoring classes were previously mixed into runtime findings. That is invalid.

---

## Scope Boundary

This report is intentionally narrow. It does **not** claim transform cleanliness for other APIs.

Not covered here:

- `Transform.Find(...)`
- `IsChildOf(...)`
- transform access inside helper methods not directly matching the requested hot-method names

Example outside this report scope:

- `Core/PlayerRuntimeContextService.cs` `Tick()` calls `SyncPlayerContext()`, and `SyncPlayerContext()` still uses `_playerTransform.Find("Suit_Visor")`. That is a separate transform-access concern, but it is **not** a `.parent` / `.GetChild` / `.SetParent` hit and therefore does not belong in this file.

---

## Verdict

The earlier transform report over-reported by treating any raw text hit as a hot-path violation. Under the requested smart audit rules, there are **no direct violations** for `.parent`, `.GetChild`, or `.SetParent` inside the specified hot-path methods.

---

## Mandates Followed

- `OPT_Zero_GC_Policy_AllocFree_Mandate`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety`
- `OPT_Native_Memory_Collections_JobSystem_Protocol`
