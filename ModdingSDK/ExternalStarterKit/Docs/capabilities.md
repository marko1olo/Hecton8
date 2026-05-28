# HECTON-8 Mod Capability Matrix

This file is the public starter-kit answer to: what can a modder create today, what is blocked, and where new capabilities must be added.

Runtime rule: public gameplay ingress is envelope-only. Mods author data and review packages; the engine owns execution, validation, save authority, hot SignalBus lanes, GlobalRegistry routes, and asset loading.

## Supported now

| Surface | Files | What the modder can do | Runtime status |
| --- | --- | --- | --- |
| Identity | mod.h8manifest.json, mod.json | Set id, display name, author, version, dependencies, API version. | Validated before review. |
| Command graph draft | Graphs/main.h8graph.json, Reference/allowed_opcodes.csv | Describe allowed FutureCommandEnvelope requests using approved opcode aliases/hex tokens. | Review/bake required before packets can reach runtime. |
| Settings | Tables/settings.h8table.json | Define user-facing bool/int/float/string/enum options with typed defaults. | Authoring/review contract now; runtime UI binding is engine-owned. |
| Locale | Locales/en.h8loc.json | Provide keyed localized text in canonical key form. | Authoring/review contract now; runtime injection is not a public right. |
| Content manifest | Content/assets.h8manifest.json | Declare content ids, paths, CRCs, and byte budgets. | Approval required; no loose runtime ingestion from this folder. |
| Review package | Reports/review_manifest.json, Generated/*_submission.zip | Produce one hashed handoff artifact for review. | Not a runtime install stamp. |

## Not public rights

- No Harmony, BepInEx, managed gameplay DLL execution, arbitrary Unity scripts, frame callbacks, or direct C# patching.
- No direct GameObject, ScriptableObject, material, save, inventory, world, physics, AI, renderer, or GlobalRegistry mutation.
- No runtime loading of loose AssetBundles, PNGs, audio, localization files, or arbitrary paths from this starter folder.
- No new hot SignalBus lane or GlobalSignals queue from a mod. New lanes require engine owner, capacity, schema, and runtime proof.
- No gameplay truth changes through settings, locale, content manifests, or review zips without an engine-owned validated command or resource route.

## How to create a mod without Unity

1. Run h8mod.ps1 -Action setup with id/name/author/version.
2. Inspect h8mod.ps1 -Action capabilities and h8mod.ps1 -Action opcodes.
3. Edit Graphs/main.h8graph.json and Content/assets.h8manifest.json directly when needed.
4. Use h8mod.ps1 -Action node-snippet to generate safe graph node JSON under Generated/.
5. Use h8mod.ps1 -Action apply-node-snippet to insert the generated graph node with duplicate checks, budget repair, validation, and rollback.
6. Use h8mod.ps1 -Action setting-snippet and h8mod.ps1 -Action locale-snippet to generate safe settings/locale JSON under Generated/.
7. Use h8mod.ps1 -Action apply-setting-snippet and h8mod.ps1 -Action apply-locale-snippet to insert the generated settings/locale snippets into Tables/settings.h8table.json and Locales/en.h8loc.json with duplicate checks and validation.
8. Run h8mod.ps1 -Action validate.
9. Run h8mod.ps1 -Action submission and hand off Generated/<mod-id>_submission.zip.

## How to create a mod inside the HECTON-8 Unity project

Open Hecton/Modding/External Starter Kit Workbench. Use Starter Health, Capability Matrix, Graph Contract Preview, Authoring Data Preview, Authoring Snippets, Graph Node Snippet, Validation And Review, and Submission Package panels. The Workbench can generate and apply graph/settings/locale snippets through the same bounded starter tools; it does not grant extra runtime rights.

## Expansion route

New mod powers must be added as engine-owned capability contracts: schema entry, static validator rule, Workbench visibility, starter docs, review-package proof, runtime owner, bounded budget, and runtime telemetry. Mods request; the engine validates and executes.
