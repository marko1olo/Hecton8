# HECTON-8 EVENT BUS MAP

**Date:** 2026-04-29  
**Status:** PENDING VERIFICATION  
**Scope:** Historical summary of the event-bus layer.  
**Chronology Note:** The previous version carried an impossible future scan date. This rewrite removes that contradiction.

---

## Purpose

This file is a lightweight orientation map only.

It should not be treated as the definitive publisher/subscriber truth table.  
For the larger static readout, use:

- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/EVENT_FLOW_MAP.md`

## Buses In Scope

| Bus family | Notes |
|---|---|
| `HectonEventBus` typed events | Generic event-bus layer used by first-party and mod-facing systems |
| Static zero-alloc buses | `InteractionEvents`, `CraftingEvents`, `SaveEvents`, `FlashlightEvents`, `PDAEvents`, `ModuleStatusEvents`, `ScanEvents` |

## Current Documentation Boundary

The present workspace contains two event-mapping documents:

1. This file in `01_GENERAL_INFO`
2. `EVENT_FLOW_MAP.md` in `02_ACTUAL_REPORTS`

The detailed routing document is the better source for raw mappings.  
This file should remain a short orientation page, not a second large truth table.

## Verified Constraints From Project Instructions

| Constraint | Source |
|---|---|
| Event buses are expected to be static and zero-allocation | `AGENTS.md` |
| String-based event names are forbidden in first-party event-bus design | `AGENTS.md` |
| Queue-backed / late-flush behavior is the mandated direction for the canonical event bus | `AGENTS.md` |

## Open Risk

No live event replay or Unity runtime trace was executed in this documentation-only pass.  
Publisher/subscriber truth therefore remains `PENDING VERIFICATION`.
