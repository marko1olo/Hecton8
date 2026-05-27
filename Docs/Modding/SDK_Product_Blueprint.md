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

### Capability Matrix

Displays capability cards:

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
