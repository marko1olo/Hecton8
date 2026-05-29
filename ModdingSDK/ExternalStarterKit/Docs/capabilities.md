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
| Content manifest | Content/assets.h8manifest.json, Content/Assets/ | Declare content ids, paths, CRCs, and byte budgets through bounded snippet/apply tools. | Approval required; no loose runtime ingestion from this folder. |
| Manifest contract | mod.h8manifest.json | Enable public authoring capability declarations and set capped review budgets through `configure_manifest_contract.ps1`. | Review metadata only; not runtime authority. |
| VS Code task surface | .vscode/tasks.json, .vscode/settings.json | Run setup, validate, prepare, submission, capability/opcode discovery, snippet creation/apply, disabled graph node creation, explicit replace apply actions, and manifest contract actions from `Tasks: Run Task`. | Editor/offline helper only; routes through `h8mod.ps1`. |
| Review package | Reports/review_manifest.json, Generated/*_submission.zip | Produce one hashed handoff artifact for review. | Not a runtime install stamp. |

## Not public rights

- No Harmony, BepInEx, managed gameplay DLL execution, arbitrary Unity scripts, frame callbacks, or direct C# patching.
- No direct GameObject, ScriptableObject, material, save, inventory, world, physics, AI, renderer, or GlobalRegistry mutation.
- No runtime loading of loose AssetBundles, PNGs, audio, localization files, or arbitrary paths from this starter folder.
- No new hot SignalBus lane or GlobalSignals queue from a mod. New lanes require engine owner, capacity, schema, and runtime proof.
- No gameplay truth changes through settings, locale, content manifests, or review zips without an engine-owned validated command or resource route.

## How to create a mod without Unity

1. Run h8mod.ps1 -Action setup with id/name/author/version.
2. If using VS Code, run `Tasks: Run Task` and choose the matching HECTON-8 task; disabled graph node creation and explicit graph/settings/locale/asset replace applies have separate task labels. Change `hecton8.powerShellExecutable` in `.vscode/settings.json` to `pwsh` on macOS/Linux.
3. Inspect h8mod.ps1 -Action capabilities and h8mod.ps1 -Action opcodes.
4. Use h8mod.ps1 -Action manifest-contract or Tools/configure_manifest_contract.ps1 to declare allowed capabilities and capped budgets without hand-editing mod.h8manifest.json.
5. Put content files under Content/Assets/ when declaring data blobs, raw textures, or audio clips for review.
6. Use h8mod.ps1 -Action node-snippet -NodeParametersJson '{}' and optional -NodeDisabled to generate safe graph node JSON under Generated/. For non-empty CLI parameters, strict JSON and flat fallback forms such as {Quantity:3,Item:demo} are accepted.
7. Use h8mod.ps1 -Action apply-node-snippet to insert the generated graph node with duplicate checks, budget repair, validation, and rollback.
8. Use h8mod.ps1 -Action setting-snippet and h8mod.ps1 -Action locale-snippet to generate safe settings/locale JSON under Generated/.
9. Use h8mod.ps1 -Action apply-setting-snippet and h8mod.ps1 -Action apply-locale-snippet to insert the generated settings/locale snippets into Tables/settings.h8table.json and Locales/en.h8loc.json with duplicate checks and validation.
10. Use h8mod.ps1 -Action asset-snippet -AssetCrc32 auto -AssetBytes -1 to generate a safe content asset entry, then h8mod.ps1 -Action apply-asset-snippet to insert it into Content/assets.h8manifest.json with CRC/byte proof, budget repair, validation, and rollback.
11. Run h8mod.ps1 -Action validate.
12. Run h8mod.ps1 -Action submission and hand off Generated/<mod-id>_submission.zip.

## How to create a mod inside the HECTON-8 Unity project

Open Hecton/Modding/External Starter Kit Workbench. Use Starter Health, Capability Matrix, Graph Contract Preview, Authoring Data Preview, Manifest Contract, Authoring Snippets, Content Asset Snippet, Graph Node Snippet, Validation And Review, and Submission Package panels. The Workbench can configure manifest capabilities/budgets and generate/apply graph/settings/locale/content asset snippets through the same bounded starter tools, including Graph Opcode Picker, Parameters JSON, disabled-node, asset kind picker, CRC/byte fields, and replace-on-apply controls; it does not grant extra runtime rights.

## Expansion route

New mod powers must be added as engine-owned capability contracts: schema entry, static validator rule, Workbench visibility, starter docs, review-package proof, runtime owner, bounded budget, and runtime telemetry. Mods request; the engine validates and executes.
