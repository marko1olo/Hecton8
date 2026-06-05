# Visual Reference Path Continuity - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_SOURCE`.
Scope: current mandatory visual reference image path inventory for water, terrain, sky, flora, UI, and product-face visual critique.

CSV companion: `Docs/AssetAudit/VISUAL_REFERENCE_PATH_CONTINUITY_20260605.csv`.

The old Cyrillic mandatory-reference path referenced by earlier asset reports is no longer the current visible folder in this worktree. A new English folder exists:

`Docs/mandatory if you work on systems that user sees (water, terrain, sky, flora, ui) - read this and all images inside (references)/`

This file does not move, restore, delete, or accept any reference image. It records path continuity so future `h8_1475` visual reviewers do not fail because they read stale reference paths.

## Current Inventory

- Current reference image count: 15.
- Largest reference: `BEST ILLUST - ON SURFACE (WITH TREES AND GRASS) - CHECK WATER, GAS GIANT. it is perfect! your goal to look like it! make plan and do it.png`, 2042128 bytes.
- Previous-development references are present for cliffs/water, cliffs/sky/gas giant, and underwater MapMagic/Crest routes.
- Shallows, medium-depth water, kelp forest, biome richness, and sky/coast/terrain references are present.

## Rules

- Do not restore or delete old reference paths from this continuity note.
- Do not treat reference image presence as Unity visual acceptance.
- Do not use stale `Docs/OBYAZATELNYE PRIMERY PO KARTINKAM` or `Docs/ОБЯЗАТЕЛЬНЫЕ ПРИМЕРЫ ПО КАРТИНКАМ` paths without checking current folder state.
- Future visual critique should cite current paths or this continuity ledger.

## Regression Model

- CPU: static path inventory only.
- GC: no runtime code touched.
- Memory/VRAM: no runtime residency claim.
- Cadence: no runtime cadence changed.
- Correctness: visual proof routing now has current reference paths; old path deletion/restoration remains outside this task.

Final status: `PENDING VERIFICATION`.
