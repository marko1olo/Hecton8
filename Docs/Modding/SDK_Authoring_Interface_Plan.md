# HECTON-8 Modding SDK And Authoring Interface Plan

Date: 2026-05-19
Status: SDK ARCHITECTURE PLAN / ENVELOPE-ONLY RUNTIME / PENDING RUNTIME VERIFICATION
Owner prompt: SHINOBU_66
Runtime authority: `Docs/Modding/Mod_API_Sandbox_Quarantine.md`
Product blueprint: `Docs/Modding/SDK_Product_Blueprint.md`

## Purpose

This document defines how human modders should work with HECTON-8 modding after the UGC sandbox quarantine.

The runtime rule is strict:

- no Harmony patches;
- no BepInEx-style runtime patch lane;
- no arbitrary mod `.dll` execution in gameplay;
- no managed callback authority over simulation;
- no direct Unity object handles;
- no direct `SignalBus<T>`, `NativeArray`, `NativeQueue`, or `GlobalDataVault` access;
- no first-party save, inventory, physiology, physics, AI, world, or streaming mutation;
- runtime UGC ingress is fixed 64-byte `FutureCommandEnvelope` packets only.

The SDK exists to make that hard boundary usable for people. It is an authoring layer, validation layer, packaging layer, and local simulation layer. It is not a permission to run user code inside the game frame.

For product-level screens, CLI behavior, package format, graph compiler UX, Workshop/moderation gates, and MVP backlog, read [SDK_Product_Blueprint.md](SDK_Product_Blueprint.md).

## Plain Answer For Modder Interfaces

Yes, modders need interfaces. They are not C# runtime interfaces inside the game. They are tools and data contracts:

- a Mod Workbench for humans;
- a command graph editor;
- a manifest editor;
- an asset import and CRC approval tool;
- a local sandbox simulator;
- a package validator;
- a CLI packer for CI and Workshop publishing;
- generated language bindings that write envelopes, never call engine objects.

The modder should not hand-write binary envelopes unless they are building advanced tooling. A normal creator should work with forms, graphs, presets, CSV tables, and validated assets. The SDK then emits binary packages and envelope streams.

## Contract Layers

| Layer | Human sees | Runtime sees | Rule |
|---|---|---|---|
| Authoring | Workbench UI, graph nodes, settings, asset imports, CSV tables | Nothing | Editor/offline only, allocation allowed. |
| SDK compile | schemas, validation reports, `.h8mod` package, `.h8bin` tables | Nothing during gameplay | Deterministic output, explicit errors. |
| Runtime ingress | package metadata and envelope stream | `FutureCommandEnvelope` only | 64 bytes, aligned, hashed, budgeted. |
| Engine execution | first-party owner kernels | typed internal signals or DevNull | Engine owns truth; mod requests. |
| Telemetry | reports and rejection logs | 300-frame ring entries | No "silent failed mod" state. |

## Current Public Runtime Shape

### `FutureCommandEnvelope`

The runtime packet is 64 bytes:

| Offset | Size | Field | Meaning |
|---:|---:|---|---|
| 0 | 4 | `OpcodeHash` | Stable hash of the requested operation. |
| 4 | 4 | `ModderSignature` | Engine-assigned or publisher-assigned mod signature. |
| 8 | 24 | `TargetAUP` | `double3` AUP target, finite, clamped by sandbox. |
| 32 | 16 | `PayloadData` | Four 32-bit lanes. Some lanes are float, some are raw bits by opcode. |
| 48 | 8 | `IntegrityHash` | XXHash3 over bytes `0..47`. |
| 56 | 8 | `_pad0` | Explicit 64-byte cache-line padding. |

Runtime guarantees only this ingress. Older `ModCommand`, `RequestAup`, render instance, managed event, resource proxy, and content filesystem lanes are legacy/quarantined while envelope-only mode is active.

### Runtime Validation

Every packet must pass:

- opcode allowlist;
- 64-byte layout check;
- integrity hash;
- finite AUP check;
- +/-50 km AUP sandbox bound;
- opcode-specific payload finite check where numeric lanes are used;
- CRC32 asset reference check when an asset is referenced;
- declared asset byte limit;
- per-mod frame budget;
- global drain budget;
- thermal quality shed;
- rollback freeze gate;
- quarantine status gate.

Rejected packets do not throw. They are counted, written to telemetry, or routed to DevNull when the operation is a reserved future seam without an active owner.

## Human Modder Workflow

### 1. Create Project

The Workbench creates:

```text
MyMod/
  mod.h8manifest.json
  Content/
  Graphs/
  Tables/
  Locales/
  Generated/
  Reports/
```

The manifest editor owns fields that should not be hand-edited by most users:

- stable mod id;
- display name;
- author;
- package version;
- required API version;
- requested capabilities;
- command budget request;
- asset budget request;
- deterministic signature seed;
- dependencies;
- compatibility tags;
- publisher signature status.

The current legacy `mod.json` builder gap must be closed by the SDK: `RequiredAPIVersion` and `ModPriority` must be emitted by default. A package missing either field is not load-proof.

### 2. Pick Capabilities

Capabilities are not permissions to touch systems directly. They are requests for opcode families.

Examples:

| Capability | Allows SDK to emit | Runtime owner |
|---|---|---|
| `cosmetic_spawn` | spawn/effect asset reference envelopes | presentation/world owner |
| `acoustic_ping` | acoustic stimulus envelopes | audio/fauna stimulus owner |
| `fauna_stimulus` | bounded stimulus envelopes | AI owner through signal proxy |
| `subtitle_cue` | reserved subtitle cue envelopes | localization owner, future |
| `mod_memory` | sandbox-local memory read/write envelopes | mod sandbox only |
| `telemetry_marker` | reserved diagnostics marker envelopes | telemetry owner, future |

The package validator rejects graphs that emit opcodes outside declared capabilities.

### 3. Author Logic In Graphs Or Tables

The first modder-facing logic system should be dataflow, not user C#.

Approved authoring shapes:

- command graph nodes;
- finite state machines with explicit max states;
- trigger tables;
- cooldown tables;
- probability tables using deterministic seeds;
- visual effect presets;
- asset reference tables;
- localization token tables;
- user setting definitions.

Rejected authoring shapes:

- arbitrary C# scripts;
- reflection;
- runtime delegates;
- arbitrary loops;
- unbounded recursion;
- dynamic file I/O;
- direct Unity API calls;
- direct memory reads;
- direct event bus subscriptions.

The graph compiler must prove:

- max envelopes per frame;
- max envelopes per trigger;
- max state count;
- max local memory bytes;
- no unbounded loop;
- no wall-clock dependency;
- deterministic seed source;
- compatibility with rollback resimulation.

### 4. Import Assets

Assets enter the SDK, not the runtime directly.

The asset compiler should:

- import source files in the Workbench;
- normalize formats;
- enforce size and compression caps;
- compute CRC32 and content hash;
- emit an approved asset manifest;
- emit preview proxies;
- emit low/mid/high/ultra variants when needed;
- generate envelope asset references by hash.

Runtime package loading must not scan `.bundle`, `lang_*.json`, arbitrary PNG, raw meshes, or arbitrary Unity assets while envelope-only mode is active. The game sees approved hashes and byte counts, not loose filesystem content.

### 5. Simulate Locally

The SDK must include a sandbox simulator that runs outside the game or in an editor-only harness.

It should simulate:

- envelope packing;
- hash validation;
- opcode allowlist;
- per-mod frame quota;
- global drain quota;
- thermal pressure collapse;
- rollback freeze;
- CRC asset approval;
- DevNull routing;
- rejection reasons;
- 300-frame telemetry output.

The simulator is not proof that the game runtime is verified. It is a fast authoring guard. Unity Console, GCMonitor, profiler, and player evidence remain required before changing runtime status.

### 6. Package

The CLI packer should output:

```text
MyMod.h8mod
  manifest.h8json
  commands.h8bin
  assets.h8manifest
  tables.h8bin
  locales.h8loc
  sdk_validation_report.txt
  signature.h8sig
```

Package creation should be atomic:

1. write temp directory;
2. validate all binary files;
3. hash package;
4. sign if publisher key exists;
5. rename to final `.h8mod`.

The runtime loader may reject unsigned community packages depending on distribution policy, but signature policy is separate from runtime memory safety. Unsigned does not mean unsafe if the sandbox still validates every envelope.

## SDK Components

### HECTON Mod Workbench

Purpose: primary human interface for non-programmer and technical modders.

Required panels:

- project manifest;
- capabilities;
- graph editor;
- command budget preview;
- asset import;
- asset CRC approval;
- localization/token table;
- settings UI preview;
- envelope inspector;
- rejection report;
- thermal simulation slider;
- rollback/freeze simulation switch;
- package builder;
- Workshop/export metadata.

Workbench can allocate because it is editor/offline tooling. Generated runtime data must remain fixed, binary, and unmanaged.

### Command Graph Editor

Purpose: let modders express behavior without runtime code.

Graph nodes should compile to bounded envelope templates:

- `OnSettingChanged`;
- `OnPlayerNearHashedZone`;
- `OnApprovedSignalSample`;
- `Cooldown`;
- `RandomDeterministic`;
- `EmitEnvelope`;
- `EmitAssetReference`;
- `SetModMemoryByte`;
- `ReadModMemoryByte`;
- `RouteToDevNullWhenUnsupported`;
- `TelemetryMarker`.

Every node must declare:

- worst-case envelope count;
- deterministic state footprint;
- owner capability;
- rollback behavior;
- runtime phase;
- failure mode.

No node may expose a Unity object reference.

### CLI: `h8mod`

Minimum commands:

```text
h8mod init <id>
h8mod validate <project>
h8mod pack <project> --out <file.h8mod>
h8mod simulate <project> --frames 300 --quality 0.1
h8mod simulate <project> --frames 300 --quality 1.0
h8mod dump-envelope <file.h8bin>
h8mod explain-rejection <code>
h8mod schema
```

CI use:

```powershell
h8mod validate .\MyMod
h8mod simulate .\MyMod --frames 300 --quality 0.1 --thermal 0.9
h8mod pack .\MyMod --out .\Build\MyMod.h8mod
```

### Generated Bindings

Bindings may exist for ergonomics, but they must be offline or envelope-writing only.

Allowed:

- C# source generator for editor tests that emits envelope packers;
- TypeScript or Python packer for CI;
- Rust/C CLI encoder for advanced tooling;
- schema stubs for graph nodes.

Forbidden:

- generated runtime code that calls Unity APIs;
- generated runtime callbacks executed by the game;
- generated patches;
- generated reflection tables for gameplay;
- generated direct `SignalBus<T>` consumers.

## Mod Types

### Tier 0: Cosmetic Pack

Uses approved assets and simple effect spawn envelopes.

Runtime risk:

- asset memory;
- effect spam.

Required gates:

- CRC manifest;
- asset byte cap;
- effect spawn cap;
- thermal shed behavior.

### Tier 1: Audio/Atmosphere Pack

Uses acoustic or ambience request envelopes.

Runtime risk:

- audio voice flood;
- repeated cue spam.

Required gates:

- hash-only cues;
- per-mod audio budget;
- no raw AudioClip reference;
- fallback silent cue.

### Tier 2: Gameplay Request Mod

Uses bounded owner-approved command envelopes.

Runtime risk:

- gameplay truth corruption if owner kernel is too broad.

Required gates:

- engine-owned kernel;
- rejection reason;
- rollback compatibility;
- save exclusion or explicit mod-owned save;
- 300-frame blackbox marker.

### Tier 3: Total Conversion Data Pack

Large data replacement or broad world tuning.

Runtime risk:

- streaming pressure;
- shader/material explosion;
- save incompatibility;
- balance collapse.

Required gates:

- separate compatibility channel;
- chunk/asset residency budget;
- migration manifest;
- deterministic seed mapping;
- explicit non-support for joining vanilla co-op unless the network contract is version matched.

## Manifest Proposal

The SDK manifest should be authoring-facing. Runtime may bake it into compact binary metadata.

```json
{
  "Id": "com.example.hecton.mod",
  "Name": "Example HECTON Mod",
  "Version": "1.0.0",
  "Author": "Example",
  "RequiredAPIVersion": 3,
  "ModPriority": 0,
  "Capabilities": [
    "cosmetic_spawn",
    "acoustic_ping"
  ],
  "Budgets": {
    "RequestedCommandsPerFrame": 32,
    "RequestedAssetBytes": 4194304,
    "RequestedModMemoryBytes": 4096
  },
  "Compatibility": {
    "CoopSafe": false,
    "RollbackSafe": true,
    "SaveAffectsFirstPartyTruth": false
  },
  "Entrypoints": {
    "CommandGraph": "Graphs/main.h8graph",
    "AssetManifest": "Generated/assets.h8manifest",
    "BinaryTables": "Generated/tables.h8bin"
  }
}
```

Runtime should not trust authoring JSON directly. The SDK bakes validated metadata into package records.

## Rejection UX

The SDK must explain why the game will reject a mod before the player installs it.

Examples:

| Rejection | Human message | Technical cause |
|---|---|---|
| `InvalidOpcode` | This action is not currently supported by HECTON-8. | Opcode hash absent from allowlist. |
| `AssetCrcMismatch` | Imported asset changed after approval. Rebuild the package. | CRC32 does not match manifest. |
| `ThermalBudgetExceeded` | This mod emits too many commands for low-end hardware. | Simulated quality 0.1 drops backlog. |
| `RollbackUnsafe` | This mod depends on time or non-deterministic state. | Graph uses forbidden nondeterministic source. |
| `ManagedEntryDisabled` | Runtime C# mods are not supported in envelope-only mode. | Manifest declares DLL entry. |

The Workbench should treat these as build errors, not warnings, when the issue would cause runtime quarantine.

## Telemetry And Support

Mod support needs data that does not expose engine internals.

Expose to modders:

- envelope accepted count;
- envelope rejected count;
- rejection reason histogram;
- thermal shed count;
- asset CRC rejects;
- rollback freeze drops;
- per-frame command budget used;
- DevNull routed count;
- package version and signature id.

Do not expose:

- raw DataVault handles;
- first-party native memory addresses;
- save offsets;
- entity object references;
- internal AI/physics owner state beyond approved redacted hashes.

## Compatibility With Co-op

Default package state is `CoopSafe: false`.

A mod can only be co-op safe when:

- it emits deterministic envelopes;
- it does not depend on local wall-clock time;
- it does not depend on local input outside approved input masks;
- it uses deterministic RNG seed sources;
- all clients load the same package hash;
- rollback freeze handling is declared;
- rejected packets are deterministic under the same frame, quality, and thermal inputs.

If a mod affects only local presentation, it can be marked `ClientCosmeticOnly`, but the engine still validates all asset and envelope references.

## Versioning

The SDK must version every public contract:

- envelope layout version;
- opcode schema version;
- manifest schema version;
- asset manifest version;
- graph compiler version;
- localization table version;
- package format version.

Breaking changes must keep old package rejection readable. A player should get "package uses schema 2, game requires schema 3" instead of a silent load failure.

## First SDK Milestones

### Milestone 1: Safe Package And Envelope Tooling

Deliver:

- `h8mod init`;
- manifest editor schema;
- envelope pack/unpack;
- CRC asset manifest;
- static package validation;
- simulator for 300 frames;
- readable rejection report.

No runtime expansion.

### Milestone 2: Workbench UI

Deliver:

- project UI;
- capability selection;
- asset import preview;
- envelope inspector;
- graph-less preset actions;
- package export.

No arbitrary script execution.

### Milestone 3: Command Graph Compiler

Deliver:

- finite-state graph format;
- deterministic graph compiler;
- max envelope proof;
- thermal/rollback simulator;
- example packs.

Runtime still receives only envelopes.

### Milestone 4: Curated Public Mod API Expansion

Deliver only after owner kernels exist:

- one new safe opcode family;
- owner rejection telemetry;
- runtime playbook update;
- static validator update;
- Unity profiler proof.

## Engineering Non-Negotiables

- Modder ergonomics live in SDK tooling, not inside gameplay callbacks.
- The game runtime never trusts SDK output without re-validating packets.
- A valid package can still have packets rejected at runtime when thermal pressure, rollback, or quota requires it.
- High-end hardware may accept more envelopes, but the envelope ABI does not change.
- Low-end hardware must shed UGC work before first-party simulation loses frame budget.
- Every public promise must have a rejection reason and a verification path.

## Open Implementation Decisions

These require owner approval before source work:

1. Package signature policy: unsigned local mods allowed in developer builds only, or allowed for all community mods with visible warning.
2. Workshop distribution: Steam Workshop metadata format and moderation pipeline.
3. Graph format: custom `.h8graph` binary-first format vs authoring JSON baked to `.h8bin`.
4. Public opcode names: stable hash source strings vs numeric schema ids.
5. Asset packaging: Addressables-compatible bake path vs sandbox-native asset bundles with engine-owned import.
6. Co-op policy: strict same-package hash requirement vs client-cosmetic-only exceptions.
7. Localization: token hash export path and font fallback rules.
8. User settings: Workbench-defined settings surface vs engine-curated settings categories only.

None of these change the active runtime boundary until implemented and verified.
