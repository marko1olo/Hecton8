# Subnautica Public Mod Ecosystem Deep-Dive

Date: 2026-05-17
Lane: SUBNAUTICA_RESEARCHER
Purpose: extract clean-room architectural lessons for HECTON-8 without copying proprietary game files or copyleft source into runtime.

## Executive Verdict

The public Subnautica mod ecosystem is useful because it shows where modders actually need high-level hooks:

- crafting and item registration;
- PDA/databank entries;
- scanner / known-tech / story-goal unlocks;
- loot and world-entity distribution;
- save-data extension;
- options UI;
- prefab/resource handles;
- terrain/world patch ordering;
- multiplayer packet/state boundaries.

HECTON-8 already has a safer direction than classic Subnautica modding: bounded API calls, command dispatch, resource registries, and direct Unity instance restrictions. The current problem is not philosophy. The problem is incomplete productization: manifest mismatch, empty mod root, missing data overlay handlers, and no proof that data-only content mods can travel through ContentAuthority/DataMonolith safely.

## Source Set

| Project | Public role | License signal | HECTON-8 use |
| --- | --- | --- | --- |
| Nautilus | Modern Subnautica modding API / SMLHelper successor | GPL-3.0 | Borrow handler taxonomy and timing lessons only. Do not copy code. |
| Nitrox | Open-source multiplayer modification | GPL-3.0 | Borrow state-boundary lessons only. Do not copy serialization/runtime code. |
| BepInEx.Subnautica | Preconfigured BepInEx pack for Subnautica on Windows/macOS/Linux/SteamOS | ISC repo metadata | Useful as packaging/install UX reference, not as H8 runtime architecture. |
| TerrainPatcher | Terrain patch library for Subnautica/Below Zero | AGPL-3.0 | Borrow patch-package shape and load-order concept only. Do not copy code or formats. |
| QModManager | Older config-based patch manager | repository archived | Historical migration warning: avoid building H8 around fragile arbitrary patching. |

## Nautilus Lessons

Nautilus is the richest public reference for content mod API shape. Its public handler taxonomy is the actionable part:

- `CraftDataHandler`: recipes, ingredients, item metadata.
- `CraftTreeHandler`: fabrication tree nodes.
- `PrefabHandler`: prefab registration and spawning pipeline hooks.
- `PDAHandler`: encyclopedia/log/scanner/databank entries.
- `KnownTechHandler`: unlock requirements and hard locks.
- `LootDistributionHandler`: loot/world distribution entries.
- `WorldEntityDatabaseHandler`: world entity metadata.
- `SaveDataHandler`: mod data load/save lifecycle.
- `OptionsPanelHandler`: mod settings UI.
- `LanguageHandler`: localization injection.
- `CustomSoundHandler` and `SpriteHandler`: resource registration.
- `StoryGoalHandler`: goal/progression triggers.

HECTON-8 mapping:

| Nautilus-style need | HECTON-8 current state | Required HECTON-8 shape |
| --- | --- | --- |
| Item and recipe registration | `HectonAPI.RegisterCustomItem` and `RegisterRecipe` exist. | Keep, but route through static data overlay validation and stable hashes. |
| PDA/databank entries | H8 has lore/PDA systems, but no confirmed public mod overlay handler. | Add `ModDatabankOverlayHandler` with localization keys and budget checks. |
| Scanner / known-tech unlocks | H8 has scanner and recipe gates, but route proof is weak. | Add `ModScanUnlockOverlayHandler` that cannot create soft locks. |
| Loot/world distribution | H8 has world systems, but no public distribution overlay equivalent. | Add `ModWorldDistributionOverlayHandler` with sector/biome budgets. |
| Save data | H8 exposes save string helpers. | Keep save mod state cold-path only; add schema version and size caps. |
| Options UI | H8 exposes setting registration. | Keep as cold UI path; no hot managed callbacks in simulation. |
| Resource handles | H8 has `TryResolvePrefab`, `TryResolveAudioClip`, `TryResolveTexture`. | Keep indirect handles; no raw Unity object mutation as the main API. |

Primary lesson: content modding needs high-level handlers, not raw runtime object access.

## Nitrox Lessons

Nitrox proves how expensive multiplayer retrofit becomes when the original game was not authored around explicit network/state contracts.

Useful HECTON-8 lessons:

- Separate authoritative state from presentation early.
- Design save/world mutations as packets or typed commands even before shipping co-op.
- Keep object identity stable through hashes/IDs, not scene object references.
- Treat vehicle/base/power/world-state replication as separate domains.
- Build diagnostics for state divergence.

Do not borrow:

- GPL source.
- Reflection-heavy runtime patching.
- Dependence on game assemblies for server authority.
- Serialization format or packet classes directly.

HECTON-8 should pre-shape co-op readiness through typed lanes and command logs, not through a later Harmony-style retrofit.

## TerrainPatcher Lessons

TerrainPatcher is useful because it treats world changes as patch packages with ordering and dependency concerns.

Clean-room lessons:

- World edits need a declared patch file, not arbitrary runtime mutation.
- Patch load order must be explicit.
- Conflicting patches need a deterministic reject/priority model.
- Terrain/world deltas need dependency metadata.
- Patch data should be data-only and validated before entering runtime.

HECTON-8 translation:

- Add a `WorldOverlayPatch` package concept for mods.
- Patches should target sector hashes and payload families, not Unity scenes.
- Merge should happen before world residency, with ContentAuthority validating size, format, and dependency order.
- Player save deltas remain separate from mod/base-world overlay deltas.

Do not copy `.optoctreepatch` internals. HECTON-8 needs its own sector/SDF/object-batch overlay format.

## BepInEx.Subnautica Lessons

BepInEx.Subnautica is mostly a packaging and install experience reference:

- Preconfigured pack reduces install friction.
- First run generates required folders.
- Steam Deck/Linux/macOS path support matters.
- Logging helpers and file tree diagnostics help support.

HECTON-8 should not use BepInEx-style arbitrary managed patching as its primary mod strategy. It is powerful, but it makes platform certification, IL2CPP, security, hot-path GC, and deterministic DOD harder.

Useful H8 equivalent:

- A first-run mod workspace generator.
- A mod validator CLI.
- Human-readable rejection logs.
- Platform-specific install docs.
- Data-only packages as default.

## QModManager Lesson

QModManager being archived is a warning: old patch-manager ecosystems age poorly when game updates and loader expectations change.

HECTON-8 should version mod APIs aggressively:

- Manifest version.
- Required API version.
- Content schema version.
- Engine build hash.
- Optional compatibility shims.
- Clear rejection reasons.

Current H8 issue: `ModLoader` requires `RequiredAPIVersion`, but `ModBuilderWindow` does not emit it. This is exactly the kind of lifecycle mismatch that makes a mod ecosystem feel broken on day one.

## HECTON-8 Immediate Work

1. Fix manifest v2 emission.
   - Add `RequiredAPIVersion = 2` and `ModPriority = 0` to SDK output.
   - Preserve existing fields.
   - Add validator output that tells modders exactly why a manifest fails.

2. Add data-only overlay handlers.
   - `ModDatabankOverlayHandler`
   - `ModScanUnlockOverlayHandler`
   - `ModKnownTechOverlayHandler`
   - `ModLootDistributionOverlayHandler`
   - `ModWorldDistributionOverlayHandler`
   - `ModAudioBankOverlayHandler`

3. Route overlays through ContentAuthority.
   - Stable hashes.
   - Size caps.
   - Platform tier labels.
   - Dependency graph.
   - Reject-on-missing route proof.

4. Keep managed code mods secondary.
   - Mono/editor/dev builds can allow managed DLLs.
   - IL2CPP/release builds should favor data-only packages and precompiled/approved extension points.

5. Add a mod black-box lane.
   - Last 300 frames of mod command summaries.
   - Failed command reason hashes.
   - Load order hash.
   - Manifest hash.

## Architectural Borrow List

Borrow:

- Handler taxonomy.
- Load-order/dependency thinking.
- Data patch package concept.
- Save-data lifecycle concept.
- Options/localization/resource registration shape.
- Multiplayer state-boundary warnings.

Do not borrow:

- GPL/AGPL implementation code.
- Proprietary game data or binary formats.
- Harmony patching as the default H8 contract.
- Raw Unity GameObject mutation APIs.
- Arbitrary file reads in hot paths.

## Source Links

- Nautilus: https://github.com/SubnauticaModding/Nautilus
- Nitrox: https://github.com/SubnauticaNitrox/Nitrox
- BepInEx.Subnautica: https://github.com/toebeann/BepInEx.Subnautica
- TerrainPatcher: https://github.com/Esper89/Subnautica-TerrainPatcher
- QModManager: https://github.com/SubnauticaModding/QModManager
- Subnautica 2 Steam page: https://store.steampowered.com/app/1962700/Subnautica_2/

## Proof Limits

This pass used public repository/source metadata and HECTON-8 local source inspection. It did not run external mod packages inside HECTON-8, did not decompile Subnautica, did not parse proprietary terrain/cache payloads, and did not copy third-party code.

Exact runtime microseconds saved: 0us. This is foundation-risk documentation. Future runtime impact depends on implementation and profiler evidence.
