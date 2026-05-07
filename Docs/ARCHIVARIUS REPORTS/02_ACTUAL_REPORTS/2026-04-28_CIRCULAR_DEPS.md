# CIRCULAR_DEPS.md
Date: 2026-05-07
Status: PENDING VERIFICATION


**Date:** 2026-04-29
**Status:** PENDING VERIFICATION
**Scope:** current `.asmdef` dependency surface under `Assets/_Project/Scripts`

**Mandates Followed:** `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`

---

## Method

- Re-read the current first-party `.asmdef` files under `Assets/_Project/Scripts`.
- Rechecked the dependency list embedded in `Hecton8.Core.asmdef`.
- Limited findings to what is visible from the assembly-definition graph; no Roslyn or full compile graph export was generated.

---

## Current Assembly Inventory

- `Hecton8.Core.asmdef`
- `Hecton8.Bootstrap.Contracts.asmdef`
- `Hecton8.Editor.asmdef`
- `Hecton8.Input.asmdef`
- `Hecton8.Optimization.Editor.asmdef`
- `Hecton8.UI.Editor.asmdef`
- `Hecton8.World.Contracts.asmdef`
- `Hecton8.World.Dots.asmdef`

Total first-party asmdefs rechecked in this pass: `8`

---

## Current Findings

### Cycles

No direct circular dependency was confirmed from the present `.asmdef` readback in this pass.

### Hecton8.Core third-party coupling

`Hecton8.Core.asmdef` currently references these third-party assemblies directly:

- `GPUInstancer`
- `Den.Tools`
- `MapMagic`
- `Crest`
- `WaveHarmonic.Crest`
- `WaveHarmonic.Crest.Shared`
- `VolumetricLightBeam`

This is architecture coupling debt even without a proven cycle.

### Removed false claims from the prior version

- `ShapesRuntime` is not present in the current `Hecton8.Core.asmdef` dependency list.
- `EasySave3` is not present in the current `Hecton8.Core.asmdef` dependency list.

---

## Interpretation

- The old report was directionally right about direct third-party contamination in `Hecton8.Core`.
- It was factually wrong on at least two referenced assemblies.
- "No cycles" remains a narrow graph statement, not a full architecture approval.

---

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | None. Documentation-only rewrite. |
| GC | None. Documentation-only rewrite. |
| Memory | None. Documentation-only rewrite. |
| Cadence | None. Runtime code unchanged. |
| Correctness | Improved by pruning false dependency claims while preserving the real coupling warning. |

---

## Verdict

No direct `.asmdef` cycle was confirmed in this pass.
Direct third-party references inside `Hecton8.Core` remain live architecture debt.
Compile-graph correctness beyond static `.asmdef` readback remains `PENDING VERIFICATION`.
