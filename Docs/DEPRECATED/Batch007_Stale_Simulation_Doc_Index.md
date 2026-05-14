# [DEPRECATED] Batch007 Stale Simulation Doc Index

Date: 2026-05-14
Agent: MANDATE_EVOLUTION_CHRONICLER
Status: STATIC DOC DEPRECATION / PENDING RUNTIME VERIFICATION

Purpose: mark old markdown surfaces that mention `Update()`, GameObjects, or simulation ownership in ways that can be misread as current authority.

Current replacements:

- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/ARCH_Signal_Lane_Segregation.txt`
- `.agents-skills/MATH_AUP_Determinism_Sync.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `Docs/PROJECT_ATLAS.md`

## Deprecated For Runtime Authority

| Deprecated file | Reason | Replacement |
|---|---|---|
| `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/GLOSSARY.md` | Contains old explanatory code snippets using `Update()` and direct `GlobalRegistry` access from `Update()`. | Use `ARCH_Execution_Phases.txt` and `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`. |
| `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/FRAME_TIMELINE.md` | Describes old dual-layer timeline with raw Unity loop names and mojibake. | Use `ARCH_Execution_Phases.txt` for phase authority. |
| `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/*.md` | Obsolete inventory audit files reference old `Update()` and GameObject decomposition guidance. | Use current mandates and source-backed docs only. |
| `Docs/Legacy_Backlog/*` | Legacy backlog text is not current architecture. | Use current mandates, `Docs/PROJECT_ATLAS.md`, and source-backed docs. |
| `Docs/Scatter_Runtime/SCATTER_REFACTORING_MANIFESTO_V2.md` | Historical refactor manifesto; local owner intent may remain useful, but it is not phase authority. | Use execution phases, DataVault, and signal lane mandates. |

[RULE] These files may be used as historical context only.
[FORBID] Citing these files as current permission for runtime `Update()`, per-entity GameObjects, monolithic EventBus traffic, or local NativeArray ownership.
