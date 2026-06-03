# UI / HUD Mandate Actuality Report

Status: YELLOW_VALID_BUT_LOCALIZATION_AND_UI_PROOF_GAPS
Date: 2026-06-02
Evidence class: `STATIC_DOC` + `STATIC_SOURCE`

## What Exists

- UI route bibles exist: `ui.md`, `UI_MENU_SCREEN_STANDARDS.md`, `UI_DIEGETIC_HUD_STANDARDS.md`, `localization.md`, `settings.md`, and `accessibility.md`.
- `UI_Data_Streaming_ZeroGC_Optimization.txt` and `UI_Diegetic_Physical_Interfaces.txt` are route-covered.
- `LINE_LEVEL_CLASSIFICATION.md` classified 241 runtime suspect lines.

## What Is Not Correct Enough Yet

- `RB-131` remains: acoustic radar, visor mesh, cockpit damage hologram, localization/glitch/state-store prewarm, and relay HUD composition need authored-asset and profiler proof.
- `UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt` lacks explicit current quality scaling wording and must be read through the root UI/accessibility bibles.

## Current Correct Mandate Interpretation

UI must be diegetic, readable, state-truthful, zero-GC after bootstrap, and authored. Runtime fallback cube meshes, projection meshes, emergency materials, and hierarchy repair are not release composition.

## Required Proof

- Desktop/mobile/controller screenshots.
- Localization expansion and RTL/CJK/font proof.
- 300-frame UI interaction profiler with 0 B/frame text/state updates.
- Authored radar/visor/cockpit/relay assets and no post-bootstrap mesh/material/hierarchy creation.

