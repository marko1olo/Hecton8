# HECTON-8 Modding SDK Product Blueprint

Date: 2026-05-19
Status: PRODUCT BLUEPRINT / ENVELOPE-ONLY / PENDING IMPLEMENTATION
Owner domain: Modding SDK product contract
Parent authority:

- `Docs/Modding/README.md`
- `Docs/Modding/Mod_API_Sandbox_Quarantine.md`
- `Docs/Modding/SDK_Authoring_Interface_Plan.md`

## Purpose

This document turns the envelope-only modding architecture into an actual product plan for people who create mods.

The problem is not "how do we let players run code". That path is rejected. The problem is "how do we let creators build useful mods while the game keeps Zero-GC, rollback determinism, thermal shedding, and no direct engine mutation".

The SDK therefore has to feel like a creation suite, not like a binary packet torture chamber. The packet torture remains internal.

## Product Definition

HECTON-8 Modding SDK is four products sharing one schema:

| Product | Audience | Job |
|---|---|---|
| HECTON Mod Workbench | normal modders, designers, artists | create and validate mods without writing binary or C#. |
| `h8mod` CLI | technical modders, CI, Workshop automation | validate, simulate, pack, inspect, and publish. |
| Envelope Schema Kit | advanced tool authors | generate valid envelopes and manifests in external tools. |
| Runtime Quarantine Monitor | QA, support, creators | explain accepted/rejected packets and package failures. |

None of these products grants runtime code execution.

## Current Implemented Surface

The implemented Unity Editor surfaces are `Hecton/Modding/SDK Hub` and `Hecton/Modding/External Starter Kit Workbench`.

It is not the final Workbench, but it must answer the first public modder question without forcing people to inspect source:

- create `ModdingSDK/ExternalStarterKit/`;
- open `ModdingSDK/ExternalStarterKit/`;
- open the External Starter Kit Workbench;
- link `Docs/Modding/External_Starter_Kit_File_Contract.md`;
- link the API spec, authoring plan, product blueprint, sample, and runtime playbook;
- run `Docs/Modding/Validate_Mod_API_Static.ps1` asynchronously so Unity Editor repaint is not blocked by validator stdout/stderr, with failed validator runs shown as Editor error UI;
- keep the legacy Mod Builder behind an explicit internal warning.

The external starter kit is the current file contract for random internet authors. It is versioned at `ModdingSDK/ExternalStarterKit/`, and the SDK Hub can refresh missing files non-destructively. The Hub generator keeps executable root/tool scripts on checked-in starter templates only, with embedded executable C# fallbacks removed, while docs, manifests, schemas, and VS Code configuration still prefer checked-in files before bounded C# fallbacks. The current Workbench is a Unity Editor facade over that same folder: it reuses the Hub generator for create/refresh, shows required-file health from the same required-file list as `Tools/validate_structure.ps1` with current schema names (`assets.schema.json`, `settings_table.schema.json`, `locale.schema.json`, `.vscode/tasks.json`), shows a Manifest Contract panel for allowlisted capability metadata and capped budgets, shows a Dependency Contract panel through `Tools/configure_dependencies.ps1`, shows a Graph Contract Preview over `Graphs/main.h8graph.json` and `Reference/allowed_opcodes.csv` for runtime flag, node count, duplicate IDs, invalid opcodes, and graph-budget drift, shows an Authoring Data Preview over `Tables/settings.h8table.json`, `Content/assets.h8manifest.json` plus `Content/Assets/`, and `Locales/en.h8loc.json`, generates validated graph/settings/locale/content asset snippets through `Tools/create_graph_node_snippet.ps1`, `Tools/create_settings_row_snippet.ps1`, `Tools/create_locale_entry_snippet.ps1`, and `Tools/create_asset_entry_snippet.ps1`, exposes Graph Opcode Picker, Parameters JSON, disabled-node, and replace-on-apply controls for graph nodes, applies graph/settings/locale/content asset snippets through `Tools/apply_graph_node_snippet.ps1`, `Tools/apply_settings_row_snippet.ps1`, `Tools/apply_locale_entry_snippet.ps1`, and `Tools/apply_asset_entry_snippet.ps1` with duplicate rejection, graph budget repair, content byte budget repair, CRC/byte proof, post-write validation, and rollback, configures manifest capabilities/budgets through `Tools/configure_manifest_contract.ps1`, runs the read-only package doctor through `Tools/run_doctor.ps1` including submission zip hash/entry integrity and non-ready nonzero process exits, builds reviewed submission zips through `Tools/build_submission_package.ps1`, installs reviewed local discovery copies through `Tools/install_local_mod.ps1` with exact lowercase review proof, diagnoses local `Mods` folders through read-only `Tools/diagnose_local_mods.ps1` with exact lowercase review proof with recursive manifest discovery, duplicate ID detection, missing dependency detection, dependency cycle detection, and load-order preview, shows current submission package path/freshness plus bounded zip integrity against `Reports/review_manifest.json`, opens `Generated/<mod-id>_submission.zip` or the Generated folder, requires and opens the root `h8mod.ps1` launcher plus `.vscode/settings.json` and `.vscode/tasks.json`, identity edits call `Tools/set_mod_identity.ps1`, validation/review calls `Tools/prepare_mod.ps1`, opcode discovery calls `Tools/list_allowed_opcodes.ps1`, key starter files open from one screen, nonzero starter tool exits are shown as Editor error UI, and the latest `Reports/review_manifest.json` identity/file/byte summary is visible without scanning raw JSON by hand. Its copied opcode/tuning CSV references are statically compared against `Docs/Modding/allowed_opcodes.csv` and `Docs/Modding/kernel_tuning_profiles.csv`. It states that no full Unity project is required for manifest, graph, table, content asset, locale, validation, and review-handoff authoring. It also includes root `h8mod.ps1` for no-Unity menu/first-mod/setup/validate/review/prepare/doctor/install-local/diagnose-local/dependencies/submission/opcode/snippet/apply-snippet/manifest-contract/capabilities actions, with `-NodeParametersJson` and `-NodeDisabled` pass-through for graph node snippet creation plus asset snippet/apply parameters for `Content/Assets/`, JSON Schemas, `.vscode/settings.json`, and `.vscode/tasks.json` for schema-aware editor autocomplete plus VS Code `Tasks: Run Task` execution with package doctor, local install, local diagnosis, disabled-node, and explicit replace task labels, `Tools/create_first_mod.ps1` for one-command first playable draft creation, `Tools/run_doctor.ps1` for read-only starter readiness checks against structure, review hashes, current source files, submission zip freshness, and zip entry integrity with exit `0` only for `ready`, `2` for `needs_review`, and `1` for `invalid` preserved by the root launcher, `Tools/install_local_mod.ps1` for reviewed source-copy local discovery under `Mods/<mod-id>`, `Tools/diagnose_local_mods.ps1` for read-only installed package diagnosis with dependency graph summary, `Tools/prepare_mod.ps1` for one-command no-Unity identity setup when `-Id` is supplied plus repeat validation/review-manifest rebuilds without identity arguments, `Tools/set_mod_identity.ps1` for safe no-Unity identity edits across both manifests with semantic version validation, `Tools/list_allowed_opcodes.ps1` for text/JSON graph opcode discovery, `Tools/create_graph_node_snippet.ps1` for graph node snippet generation with bounded parameter objects and disabled state, `Tools/apply_graph_node_snippet.ps1` for bounded graph node insertion with duplicate rejection, graph/manifest minimum budget repair, validation, and rollback, `Tools/create_settings_row_snippet.ps1` and `Tools/create_locale_entry_snippet.ps1` for settings/locale snippet generation, `Tools/apply_settings_row_snippet.ps1` and `Tools/apply_locale_entry_snippet.ps1` for bounded settings/locale insertion with duplicate rejection and rollback, `Tools/create_asset_entry_snippet.ps1` and `Tools/apply_asset_entry_snippet.ps1` for bounded content asset manifest generation/application with kind/path/extension/CRC/byte checks, duplicate rejection, `MaxAssetBytes` repair, validation, and rollback, `Tools/configure_manifest_contract.ps1` for bounded Manifest Contract capability/budget edits with unknown capability rejection, budget caps, validation, and rollback, `Tools/configure_dependencies.ps1` for dependency metadata edits mirrored across both manifests with invalid/self/duplicate rejection, validation, and rollback, `Tools/build_submission_package.ps1` for reviewed submission zip generation, plus `Tools/validate_structure.ps1`, a local no-Unity validator for required files including `h8mod.ps1`, `.vscode/tasks.json`, JSON parseability, schema file parseability, exact editor schema URL/fileMatch mapping, VS Code task version/labels/inputs/launcher routing plus package doctor, local install, local diagnosis, disabled-node and explicit replace flags, canonical mod/dependency IDs, matching authoring/runtime manifest IDs, matching `DisplayName`/`Name`, `Author`, and `Version` fields, semantic package versions, allowlisted manifest capabilities, manifest budget caps, settings row schema/ID/kind/default type constraints, locale schema/code/key/value constraints, content asset schema/id/kind/path/extension/CRC/byte/budget constraints, envelope-only flags, graph node ID uniqueness, graph node cap, graph opcode allowlist membership against `Reference/allowed_opcodes.csv`, graph budget parity with `mod.h8manifest.json`, and empty managed entry fields. `Tools/build_review_manifest.ps1` runs that validator first and writes exact `Reports/review_manifest.json` with package identity, sorted file paths, byte counts, total bytes, explicit limits, and SHA-256 hashes while excluding `Generated/` and `Reports/` outputs; it fails before hashing if a copied kit exceeds `256` source files, `4194304` bytes per source file, `33554432` total source bytes, or duplicate/case-fold duplicate source paths. `Tools/build_submission_package.ps1` runs prepare against exact `Reports/review_manifest.json`, validates review byte/SHA-256 rows, rejects duplicate/case-fold duplicate source entries, and writes `Generated/<mod-id>_submission.zip` with reviewed starter sources plus `Reports/review_manifest.json`; it writes to temp first and restores the previous zip if final replacement fails. It is a review/submission artifact only; local discovery install and local diagnosis are separate, explicit, envelope-only-safe actions. The public scripts chain local tools in-process and compose child paths through normalized `Join-Path` segments so authors can use Windows PowerShell or `pwsh` on macOS/Linux. Unity remains optional for authoring and advanced asset preview; runtime stays envelope-only.

Workbench submission integrity is case-exact: the Unity package panel rejects path-case mismatches, case-fold duplicate entries, oversized review manifests, and invalid SHA-256 review rows before an archive is treated as a portable handoff artifact.

The no-Unity package doctor and package builders follow the same case-exact handoff rule: CLI/VS Code authors cannot pass a submission zip whose entry casing differs from `Reports/review_manifest.json`, whose review manifest exceeds the cap, whose source list contains case-fold duplicates, whose reserved top-level folder casing differs from the starter contract, or whose review rows contain non-lowercase/invalid SHA-256 proof. Review/submission builders exclude only exact `Generated/` and `Reports/`; case variants are invalid source layout, not output aliases.

## Creator Personas

### Visual Creator

Wants:

- new cosmetic props;
- ambience packs;
- light/fog presets;
- safe decorative spawn effects;
- screenshots and Workshop presentation.

Needs:

- asset import wizard;
- preview scene;
- compression/size warnings;
- "will this run on low hardware" score;
- one-click package export.

Must not see:

- `double3`;
- `XXHash3`;
- `NativeArray`;
- DataVault handles;
- command offsets.

### Technical Modder

Wants:

- deterministic logic;
- graph/state machines;
- command budgeting;
- inspection of binary envelopes;
- local simulations;
- CI validation.

Needs:

- graph compiler diagnostics;
- `h8mod dump-envelope`;
- exact rejection codes;
- schema docs;
- package hash and co-op compatibility proof.

Must not get:

- Unity runtime object handles;
- arbitrary C# callback authority;
- engine private events.

### Total Conversion Team

Wants:

- large asset sets;
- tuning tables;
- replacement progression;
- custom biome presentation;
- compatibility matrix.

Needs:

- package sharding;
- residency budget reports;
- table versioning;
- migration scripts;
- conflict resolver.

Must accept:

- not co-op compatible by default;
- stricter review;
- no first-party save mutation without an owner-approved migration path.

### QA And Support

Wants:

- answer why a mod failed;
- reproduce a failure;
- isolate bad packages;
- get telemetry without engine internals leaking.

Needs:

- package validation report;
- rejection histogram;
- 300-frame envelope telemetry dump;
- exact SDK/compiler version;
- package hash.

## Workbench Screen Map

### Home

Shows:

- recent projects;
- package status;
- current External Starter Kit required-file health matching the validator file list;
- root `h8mod.ps1` launcher access;
- review freshness status;
- local Mods diagnosis status;
- async starter tool status and failed-tool error state;
- SDK version;
- game schema version;
- "runtime API: envelope-only" banner.

Hard rule:

- no "create C# mod" button.

### Project Manifest

Fields:

- `Id`;
- `Name`;
- `Version`;
- `Author`;
- `RequiredAPIVersion`;
- `ModPriority`;
- dependencies;
- capabilities;
- co-op flags;
- rollback flags;
- package signature.

Validation:

- id must be reverse-DNS or stable hash source;
- API version must match current schema;
- capabilities must match graph/asset usage;
- `EntryAssembly` and `EntryType` are forbidden in envelope-only packages.
- current implemented Workbench delegates fast checks to `Tools/validate_structure.ps1`, package readiness to `Tools/run_doctor.ps1`, review handoff to `Tools/prepare_mod.ps1`, manifest capability/budget edits to `Tools/configure_manifest_contract.ps1`, dependency metadata edits to `Tools/configure_dependencies.ps1`, graph/settings/locale/content asset snippet generation to `Tools/create_graph_node_snippet.ps1`, `Tools/create_settings_row_snippet.ps1`, `Tools/create_locale_entry_snippet.ps1`, and `Tools/create_asset_entry_snippet.ps1`, graph/settings/locale/content asset apply to `Tools/apply_graph_node_snippet.ps1`, `Tools/apply_settings_row_snippet.ps1`, `Tools/apply_locale_entry_snippet.ps1`, and `Tools/apply_asset_entry_snippet.ps1`, submission zips to `Tools/build_submission_package.ps1`, local discovery install to `Tools/install_local_mod.ps1`, and read-only local Mods diagnosis to `Tools/diagnose_local_mods.ps1` with dependency graph summary; it launches those tools asynchronously, shows nonzero tool exits as Editor error UI, shows Capability Matrix, Manifest Contract, graph contract preview, authoring data preview, review manifest freshness, submission package path/freshness, submission zip integrity against `Reports/review_manifest.json`, opens the Generated handoff folder, and does not create runtime ingress.

### Capability Matrix

Current implemented panel displays the file-backed starter capability state: supported graph/settings/locale/content/review authoring surfaces, declared manifest capabilities, allowed opcode counts, budget values, missing contract files, and forbidden runtime rights.

Future richer cards can expand into:

- Cosmetic Spawn;
- Acoustic Ping;
- Fauna Stimulus;
- Mod Memory;
- Asset Reference;
- Subtitle Cue (reserved);
- Telemetry Marker (reserved);
- Survival Override (reserved).

Each card shows:

- public/private/reserved state;
- required owner;
- runtime risk;
- minimum-budget;
- rejection reasons;
- sample graph.

Reserved capabilities can be previewed only as future seams. They cannot export active runtime opcodes.

### Asset Lab

Panels:

- source asset list;
- import settings;
- compression preview;
- low/mid/high/ultra variant table;
- CRC and content hash;
- byte budget;
- engine fallback preview.

Rules:

- every asset gets a stable asset id;
- every asset gets a CRC record;
- asset variants must have explicit caps;
- no raw Unity object reference appears in exported mod data.

### Graph Lab

Node categories:

- triggers;
- conditions;
- deterministic math;
- cooldowns;
- envelope emitters;
- asset references;
- mod memory;
- telemetry;
- unsupported/reserved seams.

Every graph must display:

- worst-case envelopes per frame;
- worst-case envelopes per trigger;
- local memory footprint;
- deterministic state count;
- rollback behavior;
- minimum-budget shed behavior.

The graph compiler fails on:

- unbounded loop;
- recursion;
- local time dependency;
- non-deterministic RNG;
- missing capability;
- unsupported opcode;
- payload lane type mismatch;
- asset id without CRC approval.

### Envelope Inspector

Advanced view.

Displays:

- 64-byte packet table;
- field offsets;
- hash source;
- integrity hash input range;
- endian interpretation;
- payload lane type per opcode;
- expected rejection/acceptance path.

Purpose:

- teach technical modders what is happening without requiring normal creators to edit bytes.

### Simulator

Inputs:

- frames;
- `GlobalQualityWeight`;
- `CpuThermalPressure01`;
- rollback freeze frames;
- command spam multiplier;
- asset CRC corruption toggle;
- platform profile.

Outputs:

- accepted envelopes;
- rejected envelopes;
- DevNull routed envelopes;
- thermal shed count;
- max frame command budget;
- asset rejects;
- rollback drops;
- estimated hot-path cost class;
- 300-frame telemetry preview.

The simulator must not claim runtime proof. It is authoring proof only.

### Package Report

Sections:

- summary;
- manifest;
- capability usage;
- asset manifest;
- graph proof;
- envelope count proof;
- low/mid/high/ultra behavior;
- rejection forecast;
- co-op compatibility;
- export hash;
- known unsupported features.

This report travels inside `.h8mod` and is what support asks for first.

### Publish

Targets:

- local developer folder;
- zipped `.h8mod`;
- Steam Workshop draft;
- CI artifact folder.

Publishing must run validation first. No validation, no package.

## CLI Design

### Commands

```text
h8mod init <id>
h8mod validate <project>
h8mod graph-check <project>
h8mod asset-check <project>
h8mod simulate <project> --frames 300 --quality 0.1 --thermal 0.9
h8mod pack <project> --out <file.h8mod>
h8mod inspect <file.h8mod>
h8mod dump-envelope <commands.h8bin>
h8mod explain <rejection-code>
h8mod schema export --format json
h8mod workshop prepare <project>
```

### Exit Codes

| Code | Meaning |
|---:|---|
| 0 | success |
| 1 | validation failed |
| 2 | schema mismatch |
| 3 | package unsafe for envelope-only runtime |
| 4 | asset budget failure |
| 5 | graph determinism failure |
| 6 | co-op compatibility failure |
| 7 | internal SDK error |

### CI Contract

Minimum CI:

```powershell
h8mod validate .
h8mod simulate . --frames 300 --quality 0.1 --thermal 0.9
h8mod simulate . --frames 300 --quality 1.0 --thermal 0.0
h8mod pack . --out .\Build\Package.h8mod
```

CI output must include the package hash. Co-op requires exact package hash match between peers.

## Package Format

### `.h8mod` Logical Layout

```text
manifest.h8json
package.h8header
commands/
  startup.h8bin
  graph_main.h8bin
assets/
  assets.h8manifest
  variants/
tables/
  tuning.h8bin
locales/
  tokens.h8loc
reports/
  validation.txt
  simulation_low.txt
  simulation_ultra.txt
signature/
  package.h8sig
```

### Package Header

The package header should be fixed-size and little-endian.

| Field | Type | Rule |
|---|---|---|
| Magic | `uint` | `H8MD` or equivalent package magic. |
| PackageVersion | `ushort` | package format version. |
| EnvelopeVersion | `ushort` | active envelope ABI version. |
| ManifestBytes | `uint` | byte length. |
| AssetManifestBytes | `uint` | byte length. |
| CommandBytes | `uint` | total command stream bytes. |
| TableBytes | `uint` | total table bytes. |
| PackageHashLo/Hi | `ulong` x2 | content hash. |

Runtime should reject malformed package headers before looking at any deeper content.

### `commands.h8bin`

Command streams are flat 64-byte aligned envelope records.

Rules:

- file length must be multiple of 64;
- stream endian is little-endian unless compatibility flag says otherwise;
- every record still validates at runtime;
- no record can request a capability absent from manifest;
- no record can reference an asset absent from asset manifest.

### `assets.h8manifest`

Asset records should be fixed-size after SDK bake.

Fields:

- asset id;
- source hash;
- CRC32;
- declared bytes;
- kind;
- variant tier;
- compression;
- fallback id.

Runtime asset references use id/hash/CRC. They do not use file paths.

## Graph Compiler Rules

### Allowed Trigger Types

| Trigger | Runtime meaning |
|---|---|
| OnPackageLoaded | cold package setup only. |
| OnSettingChanged | user setting changed in approved UI. |
| OnApprovedEventSample | future redacted event sample, not direct SignalBus. |
| OnTimerTick | deterministic tick counter, not wall-clock. |
| OnZoneHashEntered | hashed zone trigger, no Transform reference. |
| OnEnvelopeRejected | local support/telemetry response. |

### Allowed State

State lives in mod sandbox memory or baked graph state.

Allowed:

- bytes;
- ushort/uint counters;
- fixed-point values;
- deterministic flags;
- fixed-size ring counters.

Forbidden:

- managed objects;
- strings in runtime state;
- arrays allocated by mod;
- pointers;
- Unity object ids except approved hashes;
- first-party entity references.

### Deterministic RNG

Graph RNG must seed from:

- package hash;
- modder signature;
- frame/tick;
- optional sector hash when the owner exposes one.

Graph RNG must not seed from:

- system time;
- frame duration;
- local player name;
- OS entropy;
- floating camera coordinates.

## Authoring API Examples

These are SDK-side examples. They do not run inside the game.

### Visual Spawn Preset

```text
Trigger: OnZoneHashEntered(zone=0x12A9FE04)
Condition: Cooldown(300 ticks)
Action: Emit CosmeticSpawn asset=0x7788AA10 localOffset=(0, 1.2, 0)
Budget: max 1 envelope per trigger
Budget pressure: cosmetic drop probability = smoothstep(0.0, 0.35, 1.0 - GlobalQualityWeight) when the queue is over budget
```

### Acoustic Lure Preset

```text
Trigger: OnSettingChanged(lure_enabled=true)
Action: Emit AcousticPing strength=0.35 radius=12m
Budget: max 1 envelope per 120 ticks
Rollback: deterministic, no wall-clock
```

### Unsupported Survival Cheat

```text
Trigger: OnSettingChanged(infinite_o2=true)
Action: SurvivalOverride oxygenFloor=1.0
Result: Build error until PlayerSurvival owner exposes a public envelope kernel
```

The SDK should be honest here. It must not create a fake "works" sample for a command the runtime cannot accept.

## User-Facing Error Language

SDK errors should be blunt and actionable.

Bad:

```text
Validation failed.
```

Good:

```text
Package rejected: graph emits SurvivalOverride, but PlayerSurvival has no public mod kernel.
Fix: remove the node or target an SDK version where SurvivalOverride is public.
Runtime result if ignored: packet rejected as InvalidOpcode.
```

Bad:

```text
Asset invalid.
```

Good:

```text
Asset rejected: TX_RustedPanel_A is 19.4 MB. Current envelope-only asset cap is 8 MB.
Fix: reduce source texture, choose BC7 compression, or split into approved variants.
```

## Workshop And Moderation

The SDK should generate a moderation summary:

- package id;
- author;
- version;
- package hash;
- capabilities;
- total command streams;
- max envelopes per frame;
- asset bytes;
- co-op safe flag;
- contains reserved future seams yes/no;
- contains managed entry or top-level DLL yes/no;
- validation status.

Workshop ingestion can reject before human review when:

- package id or dependency id is non-canonical;
- managed entry or top-level DLL is present;
- package validation fails;
- asset budget exceeds hard cap;
- graph is nondeterministic;
- package hash is missing;
- package claims co-op safe without deterministic proof.

## Runtime Support Surface

The game should expose mod status to players in plain terms:

| State | Player text |
|---|---|
| Loaded | Mod package loaded; runtime packets still validated. |
| Partially active | Some packets accepted; some dropped by budget or thermal pressure. |
| Quarantined | Mod was disabled after repeated invalid packets or policy violation. |
| Unsupported | Package targets a newer schema or reserved feature. |
| Cosmetic only | Mod affects local presentation and is not network-authoritative. |

Do not show raw stack traces or engine internals in public UI. Keep detailed dumps for developer/support mode.

## Security Model

Threats:

- command flood;
- malformed AUP;
- NaN payload;
- CRC-forged asset reference;
- loose filesystem bypass;
- managed callback allocation;
- rollback desync;
- co-op package mismatch;
- save truth mutation;
- resource handle leak.

Countermeasures:

- 64-byte fixed envelope;
- opcode allowlist;
- integrity hash;
- CRC asset manifest;
- byte caps;
- per-mod budget;
- thermal shed;
- rollback freeze;
- no managed callbacks;
- no direct file content ingress;
- no first-party save mutation;
- 300-frame blackbox telemetry.

## Minimum Viable SDK

The first useful SDK should not attempt total conversion support.

Ship first:

- manifest editor;
- asset CRC manifest;
- envelope packer;
- static validator;
- continuous quality sweep simulator;
- package report;
- two sample cosmetic packages;
- one sample rejected survival cheat showing honest failure.

Do not ship first:

- arbitrary scripting;
- Workshop auto-publish;
- co-op-safe gameplay mods;
- total conversion tools;
- direct asset bundle import into runtime.

## Backlog

### Must Build

- package schema;
- Workbench project creation;
- CLI validation;
- envelope dump tool;
- graph compiler skeleton;
- asset CRC pipeline;
- runtime rejection code dictionary;
- package report format.

### Should Build

- visual graph UI;
- preview scene;
- Workshop draft exporter;
- package signing;
- continuous quality sweep report;
- compatibility checker.

### Do Not Build Yet

- public C# runtime SDK;
- scripting VM;
- reflection bridge;
- live hot-reload of arbitrary packages in gameplay;
- co-op gameplay command expansion.

## Acceptance Criteria

The SDK is not acceptable until:

- a non-programmer can create a harmless cosmetic package without touching binary;
- a technical modder can inspect exact envelopes;
- the CLI can fail a bad package with a useful reason;
- the simulator catches thermal flood behavior;
- package validation rejects managed entry or stale/top-level DLL ingress;
- package validation rejects loose runtime assets;
- runtime docs still say envelope-only;
- static modding validator passes after doc/schema changes.

Runtime verification remains separate. The SDK can be useful before runtime is marked verified, but it must not claim the runtime proof exists.
